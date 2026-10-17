using System;
using System.Collections.Generic;

namespace Fatiro.Domain
{
    /// <summary>Latest result for one load case, as fed into scoring.</summary>
    public sealed class CaseOutcome
    {
        public CaseOutcome(LoadCaseKind kind, float peakUtilisation, int failedCount, int memberCount)
        {
            Kind = kind;
            PeakUtilisation = peakUtilisation;
            FailedCount = failedCount;
            MemberCount = memberCount;
        }

        public LoadCaseKind Kind { get; }
        public float PeakUtilisation { get; }
        public int FailedCount { get; }
        public int MemberCount { get; }
    }

    public sealed class ScoreInput
    {
        public ScoreInput(int placementAttempts, int acceptedPlacements, IReadOnlyList<CaseOutcome> outcomes,
            IReadOnlyList<LoadCaseKind> requiredCases, float materialCost, float baselineCost)
        {
            PlacementAttempts = placementAttempts;
            AcceptedPlacements = acceptedPlacements;
            Outcomes = outcomes ?? Array.Empty<CaseOutcome>();
            RequiredCases = requiredCases ?? Array.Empty<LoadCaseKind>();
            MaterialCost = materialCost;
            BaselineCost = baselineCost;
        }

        public int PlacementAttempts { get; }
        public int AcceptedPlacements { get; }
        public IReadOnlyList<CaseOutcome> Outcomes { get; }
        public IReadOnlyList<LoadCaseKind> RequiredCases { get; }
        public float MaterialCost { get; }
        public float BaselineCost { get; }
    }

    public sealed class ScoreBreakdown
    {
        public ScoreBreakdown(int assembly, int safety, int cost, int passScore)
        {
            Assembly = assembly;
            Safety = safety;
            Cost = cost;
            PassScore = passScore;
        }

        public int Assembly { get; }
        public int Safety { get; }
        public int Cost { get; }
        public int Total => Assembly + Safety + Cost;
        public int PassScore { get; }
        public bool Passed => Total >= PassScore;
    }

    /// <summary>Strategy: how a session is scored. Instructor scenarios can swap the weights.</summary>
    public interface IScoringPolicy
    {
        ScoreBreakdown Score(ScoreInput input);
    }

    /// <summary>Default policy (FR-22): assembly accuracy 40, structural safety 40, cost efficiency 20.</summary>
    public sealed class WeightedScoringPolicy : IScoringPolicy
    {
        public WeightedScoringPolicy(int assemblyWeight = 40, int safetyWeight = 40, int costWeight = 20, int passScore = 60)
        {
            if (assemblyWeight + safetyWeight + costWeight != 100)
                throw new ArgumentException("Score weights must add up to 100.");
            AssemblyWeight = assemblyWeight;
            SafetyWeight = safetyWeight;
            CostWeight = costWeight;
            PassScore = passScore;
        }

        public int AssemblyWeight { get; }
        public int SafetyWeight { get; }
        public int CostWeight { get; }
        public int PassScore { get; }

        public ScoreBreakdown Score(ScoreInput input)
        {
            var accuracy = input.PlacementAttempts == 0 ? 1f : (float)input.AcceptedPlacements / input.PlacementAttempts;
            var safety = SafetyRatio(input);
            var cost = input.MaterialCost <= 0f ? 0f : Clamp01(input.BaselineCost / input.MaterialCost);

            return new ScoreBreakdown(
                (int)Math.Round(accuracy * AssemblyWeight),
                (int)Math.Round(safety * SafetyWeight),
                (int)Math.Round(cost * CostWeight),
                PassScore);
        }

        /// <summary>
        /// Average over the cases that count: surviving scores 1 (less a little for running close to
        /// capacity); failing scores at most 0.4, less for every failed member; untested required cases score 0.
        /// </summary>
        public static float SafetyRatio(ScoreInput input)
        {
            var cases = new List<LoadCaseKind>(input.RequiredCases);
            foreach (var outcome in input.Outcomes)
                if (!cases.Contains(outcome.Kind)) cases.Add(outcome.Kind);
            if (cases.Count == 0) return 0f;

            var total = 0f;
            foreach (var kind in cases)
            {
                CaseOutcome latest = null;
                foreach (var outcome in input.Outcomes)
                    if (outcome.Kind == kind) latest = outcome;
                total += latest == null ? 0f : CaseScore(latest);
            }
            return total / cases.Count;
        }

        public static float CaseScore(CaseOutcome outcome)
        {
            if (outcome.FailedCount == 0)
                return 1f - Math.Max(0f, outcome.PeakUtilisation - 0.7f) * 0.5f;
            var failedShare = outcome.MemberCount == 0 ? 1f : (float)outcome.FailedCount / outcome.MemberCount;
            return Math.Max(0f, 0.4f - failedShare);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
