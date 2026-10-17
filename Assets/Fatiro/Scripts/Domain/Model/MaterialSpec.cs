using System;

namespace Fatiro.Domain
{
    /// <summary>
    /// Immutable, shared material definition (Flyweight): hundreds of members reference
    /// one instance instead of copying its values. Values are simplified educational factors.
    /// </summary>
    public sealed class MaterialSpec
    {
        public MaterialSpec(string id, string displayName, float axialStrength, float bendingStrength,
            float unitWeight, float unitCost, float drainCapacity = 0f)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Material id is required.", nameof(id));
            Id = id;
            DisplayName = displayName;
            AxialStrength = axialStrength;
            BendingStrength = bendingStrength;
            UnitWeight = unitWeight;
            UnitCost = unitCost;
            DrainCapacity = drainCapacity;
        }

        public string Id { get; }
        public string DisplayName { get; }

        /// <summary>Relative strength in compression/tension.</summary>
        public float AxialStrength { get; }

        /// <summary>Relative strength in bending.</summary>
        public float BendingStrength { get; }

        /// <summary>Self weight in kN per metre (linear members) or kN per m² (panels).</summary>
        public float UnitWeight { get; }

        /// <summary>Relative cost per metre or per m².</summary>
        public float UnitCost { get; }

        /// <summary>Litres per hour a drain of this material can carry.</summary>
        public float DrainCapacity { get; }
    }
}
