using System.Collections.Generic;

namespace Fatiro.Domain
{
    /// <summary>The five MVP materials (FR-12), with simplified educational values.</summary>
    public static class MaterialLibrary
    {
        public const string Bamboo = "bamboo";
        public const string Timber = "timber";
        public const string Steel = "steel";
        public const string Concrete = "concrete";
        public const string EarthBlock = "earth-block";

        public static readonly string[] Frame = { Bamboo, Timber, Steel, Concrete };
        public static readonly string[] Footing = { EarthBlock, Concrete, Steel };
        public static readonly string[] Masonry = { EarthBlock, Concrete };
        public static readonly string[] Bracing = { Bamboo, Timber, Steel };
        public static readonly string[] Cladding = { Bamboo, Timber, Steel, EarthBlock };
        public static readonly string[] Drainage = { Bamboo, Steel, Concrete };

        public static IReadOnlyList<MaterialSpec> CreateDefaults() => new[]
        {
            new MaterialSpec(Bamboo, "Bamboo", axialStrength: 1.0f, bendingStrength: 1.1f, unitWeight: 0.12f, unitCost: 1.0f, drainCapacity: 2500f),
            new MaterialSpec(Timber, "Timber", axialStrength: 1.2f, bendingStrength: 1.2f, unitWeight: 0.25f, unitCost: 1.6f, drainCapacity: 3000f),
            new MaterialSpec(Steel, "Steel", axialStrength: 3.0f, bendingStrength: 3.0f, unitWeight: 0.8f, unitCost: 4.5f, drainCapacity: 12000f),
            new MaterialSpec(Concrete, "Reinforced concrete", axialStrength: 3.0f, bendingStrength: 1.8f, unitWeight: 2.4f, unitCost: 3.0f, drainCapacity: 9000f),
            new MaterialSpec(EarthBlock, "Compressed-earth block", axialStrength: 1.4f, bendingStrength: 0.25f, unitWeight: 1.8f, unitCost: 0.6f)
        };
    }
}
