using System;
using System.Collections.Generic;

namespace Fatiro.Domain
{
    /// <summary>
    /// Scratchpad that load cases write their loads into. The solver then pushes these loads
    /// through the connection graph. Load cases never touch members directly.
    /// </summary>
    public sealed class LoadContext
    {
        private readonly Dictionary<string, float> vertical = new Dictionary<string, float>();
        private readonly Dictionary<string, float> uplift = new Dictionary<string, float>();
        private readonly Dictionary<string, float> drainFlow = new Dictionary<string, float>();

        public LoadContext(IReadOnlyList<StructuralMember> activeMembers, SiteConditions site, bool designHasDrainage)
        {
            ActiveMembers = activeMembers ?? throw new ArgumentNullException(nameof(activeMembers));
            Site = site ?? throw new ArgumentNullException(nameof(site));
            DesignHasDrainage = designHasDrainage;
        }

        public IReadOnlyList<StructuralMember> ActiveMembers { get; }
        public SiteConditions Site { get; }
        public bool DesignHasDrainage { get; }

        /// <summary>Total sideways force on the structure, in kN.</summary>
        public float LateralForce { get; private set; }

        public float RunoffLitresPerHour { get; set; }
        public float DrainedLitresPerHour { get; set; }

        public IReadOnlyDictionary<string, float> Vertical => vertical;
        public IReadOnlyDictionary<string, float> Uplift => uplift;
        public IReadOnlyDictionary<string, float> DrainFlow => drainFlow;

        public void AddVertical(StructuralMember member, float kiloNewtons) => Add(vertical, member.Id, kiloNewtons);
        public void AddUplift(StructuralMember member, float kiloNewtons) => Add(uplift, member.Id, kiloNewtons);
        public void AddDrainFlow(StructuralMember member, float litresPerHour) => Add(drainFlow, member.Id, litresPerHour);

        public void AddLateral(float kiloNewtons)
        {
            if (kiloNewtons < 0f) throw new ArgumentOutOfRangeException(nameof(kiloNewtons));
            LateralForce += kiloNewtons;
        }

        /// <summary>Weight that shakes in an earthquake: everything above the footings, plus finishes.</summary>
        public float ShakingWeight()
        {
            var total = 0f;
            foreach (var member in ActiveMembers)
                if (member.Kind != MemberKind.Foundation) total += DeadLoad.WeightOf(member);
            return total;
        }

        private static void Add(Dictionary<string, float> map, string id, float value)
        {
            map.TryGetValue(id, out var current);
            map[id] = current + value;
        }
    }
}
