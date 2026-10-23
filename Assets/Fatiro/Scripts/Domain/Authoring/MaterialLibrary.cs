using System.Collections.Generic;

namespace Fatiro.Domain
{
    /// <summary>Building materials (FR-12), with simplified educational values.</summary>
    public static class MaterialLibrary
    {
        public const string Bamboo = "bamboo";
        public const string Timber = "timber";
        public const string Steel = "steel";
        public const string Concrete = "concrete";
        public const string EarthBlock = "earth-block";
        public const string Ferrocement = "ferrocement";
        public const string CharcoalFerrocement = "ferrocement-charcoal";
        public const string Glass = "glass";
        public const string IronSheet = "iron-sheet";
        public const string ClayTile = "clay-tile";
        public const string Window = "window";
        public const string Door = "door";

        public static readonly string[] Frame = { Bamboo, Timber, Steel, Concrete };
        public static readonly string[] Footing = { EarthBlock, Concrete, Steel };
        public static readonly string[] Masonry = { EarthBlock, Concrete };
        public static readonly string[] Bracing = { Bamboo, Timber, Steel };
        public static readonly string[] Cladding = { Bamboo, Timber, Steel, EarthBlock };
        public static readonly string[] Drainage = { Bamboo, Steel, Concrete };
        public static readonly string[] Shell = { Ferrocement, CharcoalFerrocement, Concrete, Steel };
        public static readonly string[] Glazing = { Glass, Ferrocement };
        public static readonly string[] Roofing = { IronSheet, ClayTile };
        public static readonly string[] Slab = { Concrete, Timber };
        public static readonly string[] Windows = { Window };
        public static readonly string[] Doors = { Door };

        public static IReadOnlyList<MaterialSpec> CreateDefaults() => new[]
        {
            new MaterialSpec(Bamboo, "Bamboo", axialStrength: 1.0f, bendingStrength: 1.1f, unitWeight: 0.12f, unitCost: 1.0f, drainCapacity: 2500f),
            new MaterialSpec(Timber, "Timber", axialStrength: 1.2f, bendingStrength: 1.2f, unitWeight: 0.25f, unitCost: 1.6f, drainCapacity: 3000f),
            new MaterialSpec(Steel, "Steel", axialStrength: 3.0f, bendingStrength: 3.0f, unitWeight: 0.8f, unitCost: 4.5f, drainCapacity: 12000f),
            new MaterialSpec(Concrete, "Reinforced concrete", axialStrength: 3.0f, bendingStrength: 1.8f, unitWeight: 1.2f, unitCost: 3.0f, drainCapacity: 9000f),
            new MaterialSpec(EarthBlock, "Clay brick", axialStrength: 1.4f, bendingStrength: 0.25f, unitWeight: 1.8f, unitCost: 0.6f),
            new MaterialSpec(Ferrocement, "Ferrocement shell", axialStrength: 2.4f, bendingStrength: 1.6f, unitWeight: 2.0f, unitCost: 2.2f),
            new MaterialSpec(CharcoalFerrocement, "Charcoal ferrocement", axialStrength: 2.4f, bendingStrength: 1.6f, unitWeight: 2.0f, unitCost: 2.4f),
            new MaterialSpec(Glass, "Structural glass", axialStrength: 0.9f, bendingStrength: 0.6f, unitWeight: 0.6f, unitCost: 3.5f),
            new MaterialSpec(IronSheet, "Iron sheets", axialStrength: 1.2f, bendingStrength: 1.0f, unitWeight: 0.15f, unitCost: 0.9f),
            new MaterialSpec(ClayTile, "Clay roof tiles", axialStrength: 1.0f, bendingStrength: 0.5f, unitWeight: 1.6f, unitCost: 1.3f),
            new MaterialSpec(Window, "Glazed window", axialStrength: 0.8f, bendingStrength: 0.5f, unitWeight: 0.5f, unitCost: 2.0f),
            new MaterialSpec(Door, "Timber door", axialStrength: 1.2f, bendingStrength: 1.2f, unitWeight: 0.25f, unitCost: 1.8f)
        };
    }
}
