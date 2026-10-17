using System;

namespace Fatiro.Domain
{
    /// <summary>Site the house is built on (FR-07).</summary>
    public sealed class SiteConditions
    {
        public static readonly int[] AllowedSlopes = { 0, 10, 20, 30 };

        public SiteConditions(int slopeDegrees, SoilType soil)
        {
            if (Array.IndexOf(AllowedSlopes, slopeDegrees) < 0)
                throw new ArgumentOutOfRangeException(nameof(slopeDegrees), "Slope must be 0, 10, 20 or 30 degrees.");
            SlopeDegrees = slopeDegrees;
            Soil = soil;
        }

        public int SlopeDegrees { get; }
        public SoilType Soil { get; }

        public float SlopeTangent => (float)Math.Tan(SlopeDegrees * Math.PI / 180.0);

        /// <summary>How much load the soil can take under a footing.</summary>
        public float BearingFactor
        {
            get
            {
                switch (Soil)
                {
                    case SoilType.Rock: return 1.6f;
                    case SoilType.Laterite: return 1.0f;
                    default: return 0.6f;
                }
            }
        }

        /// <summary>Soft soils amplify ground shaking.</summary>
        public float SeismicAmplification
        {
            get
            {
                switch (Soil)
                {
                    case SoilType.Rock: return 1.0f;
                    case SoilType.Laterite: return 1.2f;
                    default: return 1.5f;
                }
            }
        }

        public override string ToString() => $"{SlopeDegrees}° {Soil.ToString().ToLowerInvariant()}";
    }
}
