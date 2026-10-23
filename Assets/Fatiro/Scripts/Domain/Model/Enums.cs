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
        Panel,

        /// <summary>A wall-like box between two points with a height: brick plinth walls and infill walls.</summary>
        Block,

        /// <summary>A straight member with a square section: concrete columns and beams.</summary>
        Bar,

        /// <summary>A piece of a lofted shell (curved body, roof slope, ear canopy, or an end cap).</summary>
        Shell
    }

    public static class EnumText
    {
        public static string StageName(BuildStage stage)
        {
            switch (stage)
            {
                case BuildStage.Foundation: return "Foundation";
                case BuildStage.PrimaryStructure: return "Primary structure";
                case BuildStage.Bracing: return "Walls & bracing";
                case BuildStage.Roof: return "Roof";
                default: return "Envelope";
            }
        }

        /// <summary>What a learner would call the piece: a brick wall is a Brace to the solver, but a wall to a builder.</summary>
        public static string PieceName(MemberSpec spec)
        {
            var material = spec.DefaultMaterialId;
            if (spec.Shape == MemberShape.Shell)
            {
                if (material == "glass") return "Glazing";
                if (material == "iron-sheet") return "Roof sheeting";
                if (material == "clay-tile") return "Roof tiles";
                if (spec.Patch.IsCap) return material == "earth-block" ? "Brick gable" : "End wall";
                return spec.IsRoof ? "Shell roof" : "Shell wall";
            }
            if (spec.Shape == MemberShape.Block)
            {
                if (spec.Stage == BuildStage.Foundation) return "Foundation wall";
                return spec.Kind == MemberKind.Brace ? "Brick wall" : spec.Thickness < 0.15f ? "Partition" : "Brick infill";
            }
            if (material == "window") return "Window";
            if (material == "door") return "Door";
            if (material == "glass") return "Glazing";
            switch (spec.Kind)
            {
                case MemberKind.Foundation: return material == "earth-block" ? "Brick pier" : "Concrete footing";
                case MemberKind.Column: return "Column";
                case MemberKind.Beam: return "Beam";
                case MemberKind.Arch: return "Arch rib";
                case MemberKind.Cantilever: return "Cantilever";
                case MemberKind.Brace: return "Brace";
                case MemberKind.Panel: return spec.IsRoof ? "Roof panel" : spec.TributaryArea > 0f ? "Floor slab" : spec.PanelEuler.X > 20f && spec.PanelEuler.X < 70f ? "Stair flight" : "Wall panel";
                default: return "Drain";
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
