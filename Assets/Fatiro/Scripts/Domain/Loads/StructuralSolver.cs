using System;
using System.Collections.Generic;

namespace Fatiro.Domain
{
    /// <summary>Outcome of one load test across all placed members.</summary>
    public sealed class LoadTestResult
    {
        public LoadTestResult(ILoadCase loadCase, IReadOnlyList<MemberResult> members, float runoffLitresPerHour,
            float drainedLitresPerHour, float lateralForce)
        {
            LoadCase = loadCase;
            Members = members;
            RunoffLitresPerHour = runoffLitresPerHour;
            DrainedLitresPerHour = drainedLitresPerHour;
            LateralForce = lateralForce;

            var failed = new List<MemberResult>();
            foreach (var result in members)
            {
                PeakUtilisation = Math.Max(PeakUtilisation, result.Utilisation);
                if (result.Failed) failed.Add(result);
            }
            failed.Sort((left, right) => left.FailureWave.CompareTo(right.FailureWave));
            Failed = failed;
        }

        public ILoadCase LoadCase { get; }
        public IReadOnlyList<MemberResult> Members { get; }
        public IReadOnlyList<MemberResult> Failed { get; }
        public float PeakUtilisation { get; }
        public float RunoffLitresPerHour { get; }
        public float DrainedLitresPerHour { get; }
        public float LateralForce { get; }
        public bool Survived => Failed.Count == 0;

        public MemberResult For(string memberId)
        {
            foreach (var result in Members)
                if (result.MemberId == memberId) return result;
            return null;
        }
    }

    public interface IStructuralSolver
    {
        LoadTestResult Solve(ConnectionGraph graph, SiteConditions site, FoundationStyle style, bool designHasDrainage, ILoadCase loadCase);
    }

    /// <summary>
    /// Simplified load-path solver (pure C#, no Unity calls). Each pass:
    /// 1. applies dead load and the chosen case,
    /// 2. pushes vertical load from roof to foundations, split evenly between working supports,
    /// 3. shares the sideways force between columns, arches and braces by stiffness,
    /// 4. computes utilisation = demand ÷ capacity per member,
    /// 5. fails overloaded members and anything left without support, then repeats so load
    ///    redistributes and failures cascade (progressive collapse).
    /// The solver never mutates members; callers apply the results.
    /// </summary>
    public sealed class StructuralSolver : IStructuralSolver
    {
        private const int MaximumWaves = 8;

        public LoadTestResult Solve(ConnectionGraph graph, SiteConditions site, FoundationStyle style, bool designHasDrainage, ILoadCase loadCase)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (style == null) throw new ArgumentNullException(nameof(style));
            if (loadCase == null) throw new ArgumentNullException(nameof(loadCase));

            var failedWave = new Dictionary<string, int>();
            var finalResults = new Dictionary<string, MemberResult>();
            var runoff = 0f;
            var drained = 0f;
            var lateralForce = 0f;

            for (var wave = 0; wave < MaximumWaves; wave++)
            {
                bool IsActive(StructuralMember member) => !failedWave.ContainsKey(member.Id);

                var active = new List<StructuralMember>();
                foreach (var member in graph.Members)
                    if (IsActive(member)) active.Add(member);
                if (active.Count == 0) break;

                var context = new LoadContext(active, site, designHasDrainage);
                new DeadLoad().Apply(context);
                if (loadCase.Kind != LoadCaseKind.Dead) loadCase.Apply(context);
                if (wave == 0)
                {
                    runoff = context.RunoffLitresPerHour;
                    lateralForce = context.LateralForce;
                }
                drained = context.DrainedLitresPerHour;

                var vertical = Copy(context.Vertical);
                var uplift = Copy(context.Uplift);
                var lostSupport = new Dictionary<string, string>();

                // Vertical load path, roof first.
                foreach (var member in graph.TopDownOrder(IsActive))
                {
                    var supports = graph.SupportsOf(member, IsActive);
                    if (supports.Count == 0)
                    {
                        if (member.Spec.Supports.Length > 0) lostSupport[member.Id] = DescribeLostSupport(graph, member);
                        continue;
                    }

                    var share = Get(vertical, member.Id) / supports.Count;
                    var upliftShare = Get(uplift, member.Id) / supports.Count;
                    foreach (var support in supports)
                    {
                        vertical[support.Id] = Get(vertical, support.Id) + share;
                        if (upliftShare > 0f && support.Kind == MemberKind.Cantilever)
                            uplift[support.Id] = Get(uplift, support.Id) + upliftShare;
                    }
                }

                // Lateral load: members standing on footings (and the braces between them) share it
                // by stiffness; foundations share the sliding force.
                var contexts = new Dictionary<string, CapacityContext>();
                var resists = new Dictionary<string, bool>();
                var totalStiffness = 0f;
                var foundations = 0;
                foreach (var member in active)
                {
                    var capacityContext = new CapacityContext(site, style, graph.IsBraced(member, IsActive), graph.IsPropped(member, IsActive));
                    contexts[member.Id] = capacityContext;
                    if (member.Kind == MemberKind.Foundation)
                    {
                        foundations++;
                        continue;
                    }

                    resists[member.Id] = IsGroundFrame(graph, member, IsActive);
                    if (resists[member.Id]) totalStiffness += member.LateralStiffness(capacityContext);
                }

                var newlyFailed = new List<string>();
                foreach (var member in active)
                {
                    var capacityContext = contexts[member.Id];
                    var verticalDemand = Get(vertical, member.Id);
                    var utilisation = verticalDemand / Math.Max(member.VerticalCapacity(capacityContext), 0.001f);

                    var lateralDemand = 0f;
                    if (member.Kind == MemberKind.Foundation)
                    {
                        lateralDemand = foundations > 0 ? context.LateralForce / foundations : 0f;
                    }
                    else if (totalStiffness > 0f && resists[member.Id])
                    {
                        lateralDemand = context.LateralForce * member.LateralStiffness(capacityContext) / totalStiffness;
                    }

                    var lateralCapacity = member.LateralCapacity(capacityContext);
                    if (lateralCapacity > 0f && lateralDemand > 0f) utilisation += lateralDemand / lateralCapacity;

                    var upliftDemand = Get(uplift, member.Id);
                    if (upliftDemand > 0f)
                        utilisation = Math.Max(utilisation, upliftDemand / Math.Max(member.UpliftCapacity(capacityContext), 0.001f));

                    if (member is Drain drain && context.DrainFlow.TryGetValue(member.Id, out var flow))
                        utilisation = Math.Max(utilisation, flow / Math.Max(drain.FlowCapacity, 1f));

                    string reason = null;
                    if (lostSupport.TryGetValue(member.Id, out var lost))
                    {
                        reason = lost;
                        utilisation = Math.Max(utilisation, 1.01f);
                    }
                    else if (utilisation > 1f)
                    {
                        reason = $"overloaded in {member.ResistanceMode}";
                    }

                    var failed = reason != null;
                    finalResults[member.Id] = new MemberResult(member.Id, verticalDemand, lateralDemand, utilisation, failed, reason, failed ? wave : -1);
                    if (failed) newlyFailed.Add(member.Id);
                }

                if (newlyFailed.Count == 0) break;
                foreach (var id in newlyFailed) failedWave[id] = wave;
            }

            var ordered = new List<MemberResult>();
            foreach (var member in graph.Members)
                if (finalResults.TryGetValue(member.Id, out var result)) ordered.Add(result);
            return new LoadTestResult(loadCase, ordered, runoff, drained, lateralForce);
        }

        /// <summary>
        /// Columns and arches standing directly on a footing, plus braces tying such members together,
        /// form the frame that resists sideways load. Members higher up only pass it down.
        /// </summary>
        private static bool IsGroundFrame(ConnectionGraph graph, StructuralMember member, Func<StructuralMember, bool> isActive)
        {
            switch (member.Kind)
            {
                case MemberKind.Column:
                case MemberKind.Arch:
                    foreach (var support in graph.SupportsOf(member, isActive, includeExtra: false))
                        if (support.Kind == MemberKind.Foundation) return true;
                    return false;
                case MemberKind.Brace:
                    foreach (var support in graph.SupportsOf(member, isActive, includeExtra: false))
                        if ((support.Kind == MemberKind.Column || support.Kind == MemberKind.Arch) && IsGroundFrame(graph, support, isActive))
                            return true;
                    return false;
                default:
                    return false;
            }
        }

        private static string DescribeLostSupport(ConnectionGraph graph, StructuralMember member)
        {
            foreach (var id in member.Spec.Supports)
            {
                var support = graph.Get(id);
                if (support != null) return $"lost support from {support.Spec.DisplayName}";
            }
            return "lost its support";
        }

        private static Dictionary<string, float> Copy(IReadOnlyDictionary<string, float> source)
        {
            var copy = new Dictionary<string, float>();
            foreach (var pair in source) copy[pair.Key] = pair.Value;
            return copy;
        }

        private static float Get(Dictionary<string, float> map, string id) => map.TryGetValue(id, out var value) ? value : 0f;
    }
}
