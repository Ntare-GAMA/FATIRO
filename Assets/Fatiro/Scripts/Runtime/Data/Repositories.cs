using System;
using System.Collections.Generic;
using System.IO;
using Fatiro.Domain;
using UnityEngine;

namespace Fatiro.Data
{
    /// <summary>Lightweight row for the history list.</summary>
    public sealed class SessionSummary
    {
        public string SessionId;
        public string LearnerId;
        public string DesignName;
        public string Mode;
        public string Site;
        public DateTime StartedAtUtc;
        public int Score;
        public bool Passed;
        public int LoadTests;
    }

    /// <summary>Repository pattern: game logic never sees file paths or formats.</summary>
    public interface ISessionRepository
    {
        void Save(SessionRecord record);
        SessionRecord Load(string learnerId, string sessionId);
        List<SessionSummary> ListFor(string learnerId);
        void DeleteAllFor(string learnerId);
        int Purge(string learnerId, int keepLatest, int maxAgeDays);
    }

    public interface IProfileRepository
    {
        IReadOnlyList<LearnerProfile> List();
        LearnerProfile Get(string learnerId);
        LearnerProfile Create(string displayName);
        void Delete(string learnerId);
    }

    /// <summary>One JSON file per session at sessions/{learnerId}/{sessionId}.json (DMP 6.1).</summary>
    public sealed class JsonSessionRepository : ISessionRepository
    {
        private readonly string root;

        public JsonSessionRepository(string rootDirectory)
        {
            root = Path.Combine(rootDirectory, "sessions");
            Directory.CreateDirectory(root);
        }

        public void Save(SessionRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            AtomicFile.WriteAllText(PathFor(record.learnerId, record.sessionId), JsonUtility.ToJson(record, true));
        }

        public SessionRecord Load(string learnerId, string sessionId)
        {
            if (!AtomicFile.TryReadAllText(PathFor(learnerId, sessionId), out var json)) return null;
            return Parse(json);
        }

        public List<SessionSummary> ListFor(string learnerId)
        {
            var result = new List<SessionSummary>();
            var directory = Path.Combine(root, Safe(learnerId));
            if (!Directory.Exists(directory)) return result;

            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                SessionRecord record;
                try
                {
                    record = Parse(File.ReadAllText(file));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Fatiro] Skipping unreadable session {Path.GetFileName(file)}: {exception.Message}");
                    continue;
                }

                if (record == null) continue;
                result.Add(new SessionSummary
                {
                    SessionId = record.sessionId,
                    LearnerId = record.learnerId,
                    DesignName = record.design?.designName ?? record.design?.designId,
                    Mode = record.site?.mode,
                    Site = record.site == null ? string.Empty : $"{record.site.slopeDeg}° {record.site.soil}",
                    StartedAtUtc = ParseDate(record.startedAt),
                    Score = record.score?.total ?? 0,
                    Passed = record.score?.passed ?? false,
                    LoadTests = record.loadTests?.Count ?? 0
                });
            }

            result.Sort((left, right) => right.StartedAtUtc.CompareTo(left.StartedAtUtc));
            return result;
        }

        public void DeleteAllFor(string learnerId)
        {
            var directory = Path.Combine(root, Safe(learnerId));
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        /// <summary>Keeps the latest N sessions per learner and drops anything older than the age limit.</summary>
        public int Purge(string learnerId, int keepLatest, int maxAgeDays)
        {
            var removed = 0;
            var sessions = ListFor(learnerId);
            var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
            for (var i = 0; i < sessions.Count; i++)
            {
                if (i < keepLatest && sessions[i].StartedAtUtc >= cutoff) continue;
                var path = PathFor(learnerId, sessions[i].SessionId);
                if (!File.Exists(path)) continue;
                File.Delete(path);
                removed++;
            }
            return removed;
        }

        /// <summary>Checks the schema version; older versions would be migrated here, unknown ones rejected.</summary>
        private static SessionRecord Parse(string json)
        {
            var record = JsonUtility.FromJson<SessionRecord>(json);
            if (record == null) return null;
            if (record.schemaVersion > SessionRecord.CurrentSchemaVersion)
                throw new InvalidDataException($"Session schema {record.schemaVersion} is newer than this app supports.");
            if (record.schemaVersion < 1) record.schemaVersion = SessionRecord.CurrentSchemaVersion;
            return record;
        }

        private string PathFor(string learnerId, string sessionId) =>
            Path.Combine(root, Safe(learnerId), Safe(sessionId) + ".json");

        private static DateTime ParseDate(string value) =>
            DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date) ? date.ToUniversalTime() : DateTime.MinValue;

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }
    }

    /// <summary>Learner profiles in profiles.json. Only a display name is stored (NFR-15).</summary>
    public sealed class JsonProfileRepository : IProfileRepository
    {
        [Serializable]
        private sealed class ProfileFile
        {
            public int schemaVersion = 1;
            public List<LearnerProfile> profiles = new List<LearnerProfile>();
        }

        private readonly string path;
        private readonly ISessionRepository sessions;
        private ProfileFile cache;

        public JsonProfileRepository(string rootDirectory, ISessionRepository sessionRepository)
        {
            path = Path.Combine(rootDirectory, "profiles.json");
            sessions = sessionRepository;
        }

        public IReadOnlyList<LearnerProfile> List() => Read().profiles;

        public LearnerProfile Get(string learnerId)
        {
            foreach (var profile in Read().profiles)
                if (profile.learnerId == learnerId) return profile;
            return null;
        }

        public LearnerProfile Create(string displayName)
        {
            var name = (displayName ?? string.Empty).Trim();
            if (name.Length == 0) throw new ArgumentException("Enter a display name.");
            if (name.Length > 24) name = name.Substring(0, 24);

            var profile = new LearnerProfile
            {
                learnerId = Guid.NewGuid().ToString(),
                displayName = name,
                createdAt = DateTime.UtcNow.ToString("o")
            };
            var file = Read();
            file.profiles.Add(profile);
            Write(file);
            return profile;
        }

        /// <summary>Deleting a profile also deletes its sessions (FR-28, DMP 6.3).</summary>
        public void Delete(string learnerId)
        {
            var file = Read();
            file.profiles.RemoveAll(profile => profile.learnerId == learnerId);
            Write(file);
            sessions.DeleteAllFor(learnerId);
        }

        private ProfileFile Read()
        {
            if (cache != null) return cache;
            try
            {
                cache = AtomicFile.TryReadAllText(path, out var json) ? JsonUtility.FromJson<ProfileFile>(json) : new ProfileFile();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Fatiro] Could not read profiles: {exception.Message}");
                cache = new ProfileFile();
            }
            cache ??= new ProfileFile();
            return cache;
        }

        private void Write(ProfileFile file)
        {
            cache = file;
            AtomicFile.WriteAllText(path, JsonUtility.ToJson(file, true));
        }
    }

    /// <summary>Small per-device preferences in PlayerPrefs (DMP: user settings).</summary>
    public sealed class SettingsStore
    {
        private const string MutedKey = "fatiro.muted";
        private const string LastProfileKey = "fatiro.lastProfile";
        private const string DebugKey = "fatiro.debugOverlay";
        private const string LabelsKey = "fatiro.utilisationLabels";
        private const string HologramKey = "fatiro.hologramView";

        public bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set => Set(MutedKey, value);
        }

        public bool ShowDebugOverlay
        {
            get => PlayerPrefs.GetInt(DebugKey, 0) == 1;
            set => Set(DebugKey, value);
        }

        /// <summary>Hologram view (default) or the real textured view.</summary>
        public bool Hologram
        {
            get => PlayerPrefs.GetInt(HologramKey, 1) == 1;
            set => Set(HologramKey, value);
        }

        public bool ShowUtilisationLabels
        {
            get => PlayerPrefs.GetInt(LabelsKey, 1) == 1;
            set => Set(LabelsKey, value);
        }

        public string LastProfileId
        {
            get => PlayerPrefs.GetString(LastProfileKey, string.Empty);
            set
            {
                PlayerPrefs.SetString(LastProfileKey, value ?? string.Empty);
                PlayerPrefs.Save();
            }
        }

        private static void Set(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
