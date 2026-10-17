namespace Fatiro.Domain
{
    /// <summary>Structural role of a member. Each kind maps to one StructuralMember subclass.</summary>
    public enum MemberKind
    {
        Foundation,
        Column,
        Beam,
        Arch,
        Cantilever,
        Brace,
        Panel,
        Drain
    }

    /// <summary>Construction stages, in the order they must be completed (FR-09).</summary>
    public enum BuildStage
    {
        Foundation = 0,
        PrimaryStructure = 1,
        Bracing = 2,
        Roof = 3,
        Envelope = 4
    }

    public enum LoadCaseKind
    {
        Dead,
        Live,
        Wind,
        Rain,
        Seismic
    }

    public enum SoilType
    {
        Rock,
        Laterite,
        Clay
    }

    public enum MemberState
    {
        Placed,
        Failed
    }

    public enum BuildMode
    {
        GuidedBuild,
        FreeBuild,
        LoadTest
    }

    /// <summary>How a member is drawn. Kept in the domain so blueprints stay engine-free.</summary>
    public enum MemberShape
    {
        Pad,
        Strut,
        Arch,
        Panel
    }

    public static class EnumText
    {
        public static string StageName(BuildStage stage)
        {
            switch (stage)
            {
                case BuildStage.Foundation: return "Foundation";
                case BuildStage.PrimaryStructure: return "Primary structure";
                case BuildStage.Bracing: return "Bracing";
                case BuildStage.Roof: return "Roof";
                default: return "Envelope";
            }
        }

        public static string LoadCaseName(LoadCaseKind kind)
        {
            switch (kind)
            {
                case LoadCaseKind.Dead: return "Dead load";
                case LoadCaseKind.Live: return "Occupancy";
                case LoadCaseKind.Wind: return "Wind";
                case LoadCaseKind.Rain: return "Rainfall";
                default: return "Earthquake";
            }
        }
    }
}
