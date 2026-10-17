using System.Collections.Generic;
using Fatiro.Build;
using Fatiro.Domain;

namespace Fatiro.Simulation
{
    /// <summary>Calculates the session score through a swappable scoring policy (FR-22).</summary>
    public sealed class ScoreManager
    {
        private IScoringPolicy policy;

        public ScoreManager(IScoringPolicy policy)
        {
            this.policy = policy;
        }

        public void UsePolicy(IScoringPolicy scoringPolicy) => policy = scoringPolicy;

        public ScoreBreakdown Calculate(SessionRecord record, LoadTestManager loadTests, EntityManager entities,
            IReadOnlyList<LoadCaseKind> requiredCases)
        {
            var outcomes = new List<CaseOutcome>();
            foreach (var pair in loadTests.Latest)
                outcomes.Add(new CaseOutcome(pair.Key, pair.Value.PeakUtilisation, pair.Value.Failed.Count, pair.Value.Members.Count));

            var input = new ScoreInput(record.PlacementAttempts, record.AcceptedPlacements, outcomes, requiredCases,
                entities.TotalCost, entities.BaselineCost);
            return policy.Score(input);
        }
    }
}
