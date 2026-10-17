using System;
using System.Collections.Generic;

namespace Fatiro.Domain
{
    // Serializable data shapes for the DMP session schema (section 6.2). Public fields are
    // required by Unity's JsonUtility; mutation goes through SessionRecord's methods, which
    // refuse changes once the record is closed.

    [Serializable]
    public sealed class DesignRef
    {
        public string designId;
        public int designVersion;
        public string designName;
    }

    [Serializable]
    public sealed class SiteRecord
    {
        public int slopeDeg;
        public string soil;
        public string mode;
    }

    [Serializable]
    public sealed class PlacementEvent
    {
        public string t;
        public string memberId;
        public string memberName;
        public string stage;
        public string result;
        public string reason;
    }

    [Serializable]
    public sealed class MemberResultRecord
    {
        public string memberId;
        public string material;
        public float utilisation;
        public bool failed;
    }

    [Serializable]
    public sealed class LoadTestRecord
    {
        public string t;
        public string @case;
        public float magnitude;
        public float peakUtilisation;
        public float runoffLitresPerHour;
        public List<string> failedMembers = new List<string>();
        public List<MemberResultRecord> members = new List<MemberResultRecord>();
    }

    [Serializable]
    public sealed class ScoreRecord
    {
        public int total;
        public int assembly;
        public int safety;
        public int cost;
        public bool passed;
    }

    [Serializable]
    public sealed class SessionRecord
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string sessionId;
        public string learnerId;
        public string learnerName;
        public string scenarioId;
        public DesignRef design = new DesignRef();
        public SiteRecord site = new SiteRecord();
        public string startedAt;
        public string endedAt;
        public float durationSeconds;
        public List<PlacementEvent> placements = new List<PlacementEvent>();
        public List<LoadTestRecord> loadTests = new List<LoadTestRecord>();
        public ScoreRecord score;
        public bool closed;

        public static SessionRecord Start(string learnerId, string learnerName, HouseBlueprint blueprint, SiteConditions site,
            BuildMode mode, DateTime nowUtc, string scenarioId = "")
        {
            return new SessionRecord
            {
                sessionId = Guid.NewGuid().ToString(),
                learnerId = learnerId,
                learnerName = learnerName,
                scenarioId = scenarioId,
                design = new DesignRef { designId = blueprint.DesignId, designVersion = blueprint.Version, designName = blueprint.Name },
                site = new SiteRecord { slopeDeg = site.SlopeDegrees, soil = site.Soil.ToString().ToLowerInvariant(), mode = mode.ToString() },
                startedAt = nowUtc.ToString("o")
            };
        }

        public int PlacementAttempts => placements.Count;

        public int AcceptedPlacements
        {
            get
            {
                var count = 0;
                foreach (var placement in placements)
                    if (placement.result == "accepted") count++;
                return count;
            }
        }

        public void AddPlacement(PlacementEvent placement)
        {
            EnsureOpen();
            placements.Add(placement ?? throw new ArgumentNullException(nameof(placement)));
        }

        public void AddLoadTest(LoadTestRecord loadTest)
        {
            EnsureOpen();
            loadTests.Add(loadTest ?? throw new ArgumentNullException(nameof(loadTest)));
        }

        public void Close(ScoreBreakdown breakdown, DateTime nowUtc)
        {
            EnsureOpen();
            score = new ScoreRecord
            {
                total = breakdown.Total,
                assembly = breakdown.Assembly,
                safety = breakdown.Safety,
                cost = breakdown.Cost,
                passed = breakdown.Passed
            };
            endedAt = nowUtc.ToString("o");
            if (DateTime.TryParse(startedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var started))
                durationSeconds = (float)(nowUtc - started).TotalSeconds;
            closed = true;
        }

        private void EnsureOpen()
        {
            if (closed) throw new InvalidOperationException("A closed session record cannot be changed.");
        }
    }

    [Serializable]
    public sealed class LearnerProfile
    {
        public string learnerId;
        public string displayName;
        public string createdAt;
    }
}
