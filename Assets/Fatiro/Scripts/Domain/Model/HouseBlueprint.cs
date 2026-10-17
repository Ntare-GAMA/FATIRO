using System;
using System.Collections.Generic;

namespace Fatiro.Domain
{
    /// <summary>Behaviour of the creature-inspired foundation used by a design.</summary>
    public sealed class FoundationStyle
    {
        public FoundationStyle(string name, string inspiration, float bearingFactor, float slopeResistance, float lateralGrip)
        {
            Name = name;
            Inspiration = inspiration;
            BearingFactor = bearingFactor;
            SlopeResistance = slopeResistance;
            LateralGrip = lateralGrip;
        }

        public string Name { get; }
        public string Inspiration { get; }

        /// <summary>Multiplier on vertical bearing capacity.</summary>
        public float BearingFactor { get; }

        /// <summary>0 = loses grip quickly on slopes, 1 = unaffected by slope.</summary>
        public float SlopeResistance { get; }

        /// <summary>Multiplier on sliding resistance.</summary>
        public float LateralGrip { get; }
    }

    /// <summary>
    /// An experimental house design: an ordered set of member specs plus metadata.
    /// Pure data; the Unity layer stores it in a HouseDesign ScriptableObject (NFR-12).
    /// </summary>
    public sealed class HouseBlueprint
    {
        private readonly Dictionary<string, MemberSpec> byId = new Dictionary<string, MemberSpec>();

        public HouseBlueprint(string designId, int version, string name, string meaning, string description,
            string lesson, int difficulty, FoundationStyle foundation, float uphillReferenceZ,
            IReadOnlyList<MemberSpec> members)
        {
            DesignId = designId;
            Version = version;
            Name = name;
            Meaning = meaning;
            Description = description;
            Lesson = lesson;
            Difficulty = difficulty;
            Foundation = foundation ?? throw new ArgumentNullException(nameof(foundation));
            UphillReferenceZ = uphillReferenceZ;
            Members = members ?? throw new ArgumentNullException(nameof(members));

            foreach (var member in members)
            {
                if (byId.ContainsKey(member.Id))
                    throw new ArgumentException($"Duplicate member id {member.Id} in {designId}.");
                byId.Add(member.Id, member);
            }

            foreach (var member in members)
            {
                foreach (var support in member.Supports)
                    if (!byId.ContainsKey(support))
                        throw new ArgumentException($"{member.Id} depends on unknown member {support}.");
                foreach (var support in member.ExtraSupports)
                    if (!byId.ContainsKey(support))
                        throw new ArgumentException($"{member.Id} depends on unknown member {support}.");
            }
        }

        public string DesignId { get; }
        public int Version { get; }
        public string Name { get; }
        public string Meaning { get; }
        public string Description { get; }
        public string Lesson { get; }
        public int Difficulty { get; }
        public FoundationStyle Foundation { get; }

        /// <summary>Z of the uphill edge of the footprint, where ground height is 0.</summary>
        public float UphillReferenceZ { get; }

        public IReadOnlyList<MemberSpec> Members { get; }

        public bool HasDrainage
        {
            get
            {
                foreach (var member in Members)
                    if (member.Kind == MemberKind.Drain) return true;
                return false;
            }
        }

        public MemberSpec Get(string id) => byId.TryGetValue(id, out var spec) ? spec : null;
    }
}
