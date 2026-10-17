using System;
using System.Collections.Generic;

namespace Fatiro.Domain
{
    /// <summary>Answer to "may this member be placed now?" (FR-11).</summary>
    public sealed class PlacementCheck
    {
        private PlacementCheck(bool allowed, string reason, MemberSpec missing)
        {
            Allowed = allowed;
            Reason = reason ?? string.Empty;
            MissingPrerequisite = missing;
        }

        public bool Allowed { get; }
        public string Reason { get; }
        public MemberSpec MissingPrerequisite { get; }

        public static PlacementCheck Accept() => new PlacementCheck(true, null, null);
        public static PlacementCheck Reject(string reason, MemberSpec missing = null) => new PlacementCheck(false, reason, missing);
    }

    /// <summary>
    /// Construction-sequence rules for one design (pure logic behind the Task Manager).
    /// Guided Build locks stages in order (FR-09); every mode enforces prerequisites (FR-11);
    /// a stage completes when all its required members are placed (FR-14).
    /// </summary>
    public sealed class BuildSequence
    {
        private static readonly BuildStage[] Stages =
            { BuildStage.Foundation, BuildStage.PrimaryStructure, BuildStage.Bracing, BuildStage.Roof, BuildStage.Envelope };

        private readonly HouseBlueprint blueprint;
        private readonly HashSet<string> placed = new HashSet<string>();
        private readonly HashSet<BuildStage> completedStages = new HashSet<BuildStage>();

        public BuildSequence(HouseBlueprint blueprint, BuildMode mode)
        {
            this.blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Mode = mode;
        }

        public BuildMode Mode { get; }
        public IReadOnlyCollection<string> Placed => placed;

        /// <summary>Lowest stage that still has required members missing.</summary>
        public BuildStage CurrentStage
        {
            get
            {
                foreach (var stage in Stages)
                    if (RequiredRemaining(stage) > 0) return stage;
                return BuildStage.Envelope;
            }
        }

        public bool IsComplete
        {
            get
            {
                foreach (var stage in Stages)
                    if (RequiredRemaining(stage) > 0) return false;
                return true;
            }
        }

        public bool IsPlaced(string memberId) => placed.Contains(memberId);

        public int RequiredTotal(BuildStage? stage = null) => CountRequired(stage, false);
        public int RequiredPlaced(BuildStage? stage = null) => CountRequired(stage, true);
        public int RequiredRemaining(BuildStage stage) => RequiredTotal(stage) - RequiredPlaced(stage);

        public float Progress
        {
            get
            {
                var total = RequiredTotal();
                return total == 0 ? 1f : (float)RequiredPlaced() / total;
            }
        }

        public PlacementCheck CanPlace(MemberSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (blueprint.Get(spec.Id) == null) return PlacementCheck.Reject($"{spec.DisplayName} is not part of {blueprint.Name}.");
            if (placed.Contains(spec.Id)) return PlacementCheck.Reject($"{spec.DisplayName} is already placed.");

            if (Mode == BuildMode.GuidedBuild && !IsComplete && spec.Stage > CurrentStage)
                return PlacementCheck.Reject($"Finish the {EnumText.StageName(CurrentStage)} stage before {EnumText.StageName(spec.Stage)}.");

            foreach (var supportId in spec.Supports)
            {
                if (placed.Contains(supportId)) continue;
                var missing = blueprint.Get(supportId);
                return PlacementCheck.Reject($"{spec.DisplayName} needs {missing.DisplayName} first.", missing);
            }

            return PlacementCheck.Accept();
        }

        /// <summary>Records a placement and returns the stages it completed (usually none or one).</summary>
        public IReadOnlyList<BuildStage> MarkPlaced(string memberId)
        {
            if (blueprint.Get(memberId) == null) throw new ArgumentException($"Unknown member {memberId}.");
            placed.Add(memberId);
            return RefreshCompletedStages();
        }

        public PlacementCheck CanRemove(MemberSpec spec)
        {
            if (!placed.Contains(spec.Id)) return PlacementCheck.Reject($"{spec.DisplayName} is not placed.");
            foreach (var other in blueprint.Members)
            {
                if (!placed.Contains(other.Id)) continue;
                if (Array.IndexOf(other.Supports, spec.Id) >= 0 || Array.IndexOf(other.ExtraSupports, spec.Id) >= 0)
                    return PlacementCheck.Reject($"Remove {other.DisplayName} first; it rests on {spec.DisplayName}.", other);
            }
            return PlacementCheck.Accept();
        }

        public void MarkRemoved(string memberId)
        {
            placed.Remove(memberId);
            RefreshCompletedStages();
        }

        /// <summary>Next required member of the current stage whose prerequisites are met (ghost outline, FR-09).</summary>
        public MemberSpec NextRequired()
        {
            if (IsComplete) return null;
            var stage = CurrentStage;
            foreach (var spec in blueprint.Members)
            {
                if (spec.Stage != stage || !spec.Required || placed.Contains(spec.Id)) continue;
                if (CanPlace(spec).Allowed) return spec;
            }
            return null;
        }

        /// <summary>Members the learner may place right now, required ones first.</summary>
        public List<MemberSpec> Available()
        {
            var result = new List<MemberSpec>();
            foreach (var required in new[] { true, false })
            {
                foreach (var spec in blueprint.Members)
                {
                    if (spec.Required != required || placed.Contains(spec.Id)) continue;
                    if (!required && !IsComplete && Mode == BuildMode.GuidedBuild && spec.Stage > CurrentStage) continue;
                    if (CanPlace(spec).Allowed) result.Add(spec);
                }
            }
            return result;
        }

        private IReadOnlyList<BuildStage> RefreshCompletedStages()
        {
            var newlyCompleted = new List<BuildStage>();
            foreach (var stage in Stages)
            {
                var complete = RequiredRemaining(stage) == 0;
                if (complete && completedStages.Add(stage)) newlyCompleted.Add(stage);
                if (!complete) completedStages.Remove(stage);
            }
            return newlyCompleted;
        }

        private int CountRequired(BuildStage? stage, bool onlyPlaced)
        {
            var count = 0;
            foreach (var spec in blueprint.Members)
            {
                if (!spec.Required) continue;
                if (stage.HasValue && spec.Stage != stage.Value) continue;
                if (onlyPlaced && !placed.Contains(spec.Id)) continue;
                count++;
            }
            return count;
        }
    }
}
