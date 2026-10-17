using System;
using System.Collections.Generic;
using System.IO;
using Fatiro.Build;
using Fatiro.Core;
using Fatiro.Domain;
using UnityEngine;

namespace Fatiro.Data
{
    /// <summary>Snapshot written after every completed stage and load test (NFR-08).</summary>
    [Serializable]
    public sealed class AutosaveSnapshot
    {
        public int schemaVersion = 1;
        public SessionRecord record;
        public int scaleDenominator = 50;
        public List<PlacedMemberState> placed = new List<PlacedMemberState>();
    }

    /// <summary>
    /// The only system that writes to storage. It listens to simulation events (Observer) and
    /// records placements and load tests into the session record, autosaves, and saves the
    /// completed session (FR-23 to FR-25, FR-28).
    /// </summary>
    public sealed class DataManager : IDisposable
    {
        public const int SessionsKeptPerLearner = 50;
        public const int SessionMaxAgeDays = 180;

        private readonly IEventBus events;
        private readonly string autosavePath;
        private Func<List<PlacedMemberState>> snapshotProvider;
        private Func<int> scaleProvider;
        private Func<string, string> materialLookup;

        public DataManager(IEventBus events, string rootDirectory)
        {
            this.events = events;
            Sessions = new JsonSessionRepository(rootDirectory);
            Profiles = new JsonProfileRepository(rootDirectory, Sessions);
            Settings = new SettingsStore();
            autosavePath = Path.Combine(rootDirectory, "autosave.json");

            events.Subscribe<SessionStarted>(OnSessionStarted);
            events.Subscribe<MemberPlaced>(OnMemberPlaced);
            events.Subscribe<PlacementRejected>(OnPlacementRejected);
            events.Subscribe<StageCompleted>(OnStageCompleted);
            events.Subscribe<LoadTestCompleted>(OnLoadTestCompleted);
            events.Subscribe<SessionEnded>(OnSessionEnded);
        }

        public ISessionRepository Sessions { get; }
        public IProfileRepository Profiles { get; }
        public SettingsStore Settings { get; }
        public SessionRecord Current { get; private set; }

        public LearnerProfile ActiveProfile
        {
            get
            {
                var id = Settings.LastProfileId;
                return string.IsNullOrEmpty(id) ? null : Profiles.Get(id);
            }
        }

        public void SelectProfile(LearnerProfile profile)
        {
            Settings.LastProfileId = profile?.learnerId;
            if (profile != null) PurgeOld(profile.learnerId);
        }

        public void BindSnapshotSources(Func<List<PlacedMemberState>> placed, Func<int> scale, Func<string, string> materialOf)
        {
            snapshotProvider = placed;
            scaleProvider = scale;
            materialLookup = materialOf;
        }

        public bool HasAutosave => File.Exists(autosavePath);

        public AutosaveSnapshot LoadAutosave()
        {
            try
            {
                if (!AtomicFile.TryReadAllText(autosavePath, out var json)) return null;
                var snapshot = JsonUtility.FromJson<AutosaveSnapshot>(json);
                return snapshot?.record == null || snapshot.record.closed ? null : snapshot;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Fatiro] Autosave unreadable, ignoring it: {exception.Message}");
                return null;
            }
        }

        public void DiscardAutosave()
        {
            if (File.Exists(autosavePath)) File.Delete(autosavePath);
        }

        /// <summary>Continues a restored session record instead of starting a new one.</summary>
        public void Adopt(SessionRecord record) => Current = record;

        public void Autosave()
        {
            if (Current == null || Current.closed) return;
            try
            {
                var snapshot = new AutosaveSnapshot
                {
                    record = Current,
                    scaleDenominator = scaleProvider?.Invoke() ?? 50,
                    placed = snapshotProvider?.Invoke() ?? new List<PlacedMemberState>()
                };
                AtomicFile.WriteAllText(autosavePath, JsonUtility.ToJson(snapshot));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Fatiro] Autosave failed: {exception.Message}");
            }
        }

        public void Dispose()
        {
            events.Unsubscribe<SessionStarted>(OnSessionStarted);
            events.Unsubscribe<MemberPlaced>(OnMemberPlaced);
            events.Unsubscribe<PlacementRejected>(OnPlacementRejected);
            events.Unsubscribe<StageCompleted>(OnStageCompleted);
            events.Unsubscribe<LoadTestCompleted>(OnLoadTestCompleted);
            events.Unsubscribe<SessionEnded>(OnSessionEnded);
        }

        private void OnSessionStarted(SessionStarted message)
        {
            Current = message.Record;
            Autosave();
        }

        private void OnMemberPlaced(MemberPlaced message)
        {
            if (Current == null || Current.closed || message.IsRedoOrUndo) return;
            Current.AddPlacement(new PlacementEvent
            {
                t = DateTime.UtcNow.ToString("o"),
                memberId = message.Spec.Id,
                memberName = message.Spec.DisplayName,
                stage = message.Spec.Stage.ToString(),
                result = "accepted"
            });
        }

        private void OnPlacementRejected(PlacementRejected message)
        {
            if (Current == null || Current.closed) return;
            Current.AddPlacement(new PlacementEvent
            {
                t = DateTime.UtcNow.ToString("o"),
                memberId = message.Spec.Id,
                memberName = message.Spec.DisplayName,
                stage = message.Spec.Stage.ToString(),
                result = "rejected",
                reason = message.Reason
            });
        }

        private void OnStageCompleted(StageCompleted message) => Autosave();

        private void OnLoadTestCompleted(LoadTestCompleted message)
        {
            if (Current == null || Current.closed) return;
            var result = message.Result;
            var record = new LoadTestRecord
            {
                t = DateTime.UtcNow.ToString("o"),
                @case = result.LoadCase.Kind.ToString().ToLowerInvariant(),
                magnitude = result.LoadCase.Magnitude,
                peakUtilisation = Round(result.PeakUtilisation),
                runoffLitresPerHour = Mathf.Round(result.RunoffLitresPerHour)
            };
            foreach (var failed in result.Failed) record.failedMembers.Add(failed.MemberId);
            foreach (var member in result.Members)
            {
                record.members.Add(new MemberResultRecord
                {
                    memberId = member.MemberId,
                    material = materialLookup?.Invoke(member.MemberId) ?? string.Empty,
                    utilisation = Round(member.Utilisation),
                    failed = member.Failed
                });
            }
            Current.AddLoadTest(record);
            Autosave();
        }

        private void OnSessionEnded(SessionEnded message)
        {
            try
            {
                Sessions.Save(message.Record);
                DiscardAutosave();
                PurgeOld(message.Record.learnerId);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Fatiro] Could not save the session: {exception.Message}");
                events.Publish(new ToastRequested("Could not save this session to the device.", ToastKind.Error));
            }
            Current = null;
        }

        private void PurgeOld(string learnerId)
        {
            try
            {
                Sessions.Purge(learnerId, SessionsKeptPerLearner, SessionMaxAgeDays);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Fatiro] Purge failed: {exception.Message}");
            }
        }

        private static float Round(float value) => Mathf.Round(value * 1000f) / 1000f;
    }
}
