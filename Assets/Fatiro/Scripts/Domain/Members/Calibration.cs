namespace Fatiro.Domain
{
    /// <summary>
    /// Simplified educational constants for the solver. Collected in one place so they can be
    /// tuned against hand-calculated benchmarks (NFR-14) without touching member logic.
    /// </summary>
    public static class Calibration
    {
        public const float ReferenceThickness = 0.2f;

        // Foundations
        public const float FoundationBearing = 80f;
        public const float FoundationSliding = 25f;
        public const float SlopeForZeroGrip = 40f;
        public const float TiedFootingFactor = 2f;

        // Columns and stilts
        public const float ColumnSquash = 120f;
        public const float ColumnBucklingLength = 3.5f;
        public const float ColumnLateral = 20f;
        public const float BracedLengthFactor = 0.5f;

        // Beams
        public const float BeamBending = 200f;

        // Arches
        public const float ArchThrust = 90f;
        public const float ArchLateral = 4f;
        public const float BracedArchFactor = 3f;

        // Cantilevers
        public const float CantileverBending = 40f;
        public const float ProppedCantileverFactor = 4f;
        public const float TiedCantileverUpliftFactor = 3.5f;
        public const float UntiedCantileverUpliftFactor = 0.25f;

        // Braces
        public const float BraceAxial = 30f;
        public const float BraceLateral = 40f;
        public const float BraceStiffness = 4f;

        // Panels
        public const float PanelMembrane = 8f;
        public const float PanelReferenceThickness = 0.1f;
        public const float NailedRoofHoldDown = 0.4f;
        public const float StrappedRoofHoldDown = 1.5f;

        // Drains
        public const float DrainBody = 20f;

        // Loads
        public const float SeismicCoefficientPerLevel = 0.1f;
        public const float WindPressureCoefficient = 0.8f;
        public const float WindUpliftCoefficient = 1.0f;
        public const float PondingLoadPerMm = 0.02f;
        public const float DrainedRainLoadPerMm = 0.004f;
    }
}
