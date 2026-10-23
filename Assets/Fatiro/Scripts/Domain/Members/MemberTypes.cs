using System;

namespace Fatiro.Domain
{
    /// <summary>Footing that passes load into the soil. Weakens on slopes and soft soil.</summary>
    public sealed class Foundation : StructuralMember
    {
        public Foundation(MemberSpec spec, ResolvedGeometry geometry, MaterialSpec material)
            : base(spec, geometry, material) { }

        protected override float Quantity => Math.Max(Geometry.Area, 0.25f) * SectionFactor;

        public override string ResistanceMode => "soil bearing and sliding";

        public override float VerticalCapacity(in CapacityContext context) =>
            Calibration.FoundationBearing * Material.AxialStrength * context.Site.BearingFactor *
            context.Style.BearingFactor * Math.Max(Geometry.Area, 0.25f) * SectionFactor;

        /// <summary>Sliding resistance. A brick plinth wall tying this footing to its neighbours doubles it.</summary>
        public override float LateralCapacity(in CapacityContext context)
        {
            var slopeLoss = context.Site.SlopeDegrees / Calibration.SlopeForZeroGrip * (1f - context.Style.SlopeResistance);
            var slopeFactor = Math.Max(0.15f, 1f - slopeLoss);
            return (context.IsBraced ? Calibration.TiedFootingFactor : 1f) * Calibration.FoundationSliding * Material.AxialStrength * context.Style.LateralGrip * slopeFactor *
                   Math.Max(Geometry.Area, 0.25f) * SectionFactor;
        }
    }

    /// <summary>Column, leg or stilt. Long unbraced columns buckle.</summary>
    public sealed class Column : StructuralMember
    {
        public Column(MemberSpec spec, ResolvedGeometry geometry, MaterialSpec material)
            : base(spec, geometry, material) { }

        public override string ResistanceMode => "compression with buckling";

        private float EffectiveLength(in CapacityContext context) =>
            Geometry.Length * (context.IsBraced ? Calibration.BracedLengthFactor : 1f);

        public override float VerticalCapacity(in CapacityContext context)
        {
            var slenderness = EffectiveLength(context) / Calibration.ColumnBucklingLength;
            return Calibration.ColumnSquash * Material.AxialStrength * SectionFactor / (1f + slenderness * slenderness);
        }

        public override float LateralCapacity(in CapacityContext context) =>
            Calibration.ColumnLateral * Material.BendingStrength * SectionFactor / Math.Max(EffectiveLength(context), 0.5f);

        public override float LateralStiffness(in CapacityContext context) =>
            Material.BendingStrength * SectionFactor * (context.IsBraced ? 2f : 1f);
    }

    /// <summary>Straight member spanning between supports. Resists by bending.</summary>
    public sealed class Beam : StructuralMember
    {
        public Beam(MemberSpec spec, ResolvedGeometry geometry, MaterialSpec material)
            : base(spec, geometry, material) { }

        public override string ResistanceMode => "bending";

        public override float VerticalCapacity(in CapacityContext context) =>
            Calibration.BeamBending * Material.BendingStrength * SectionFactor / Math.Max(Geometry.Length, 0.5f);
    }

    /// <summary>Curved rib. Very strong under vertical load, weak sideways unless braced.</summary>
    public sealed class Arch : StructuralMember
    {
        public Arch(MemberSpec spec, ResolvedGeometry geometry, MaterialSpec material)
            : base(spec, geometry, material) { }

        public override string ResistanceMode => "arch thrust";

        public override float VerticalCapacity(in CapacityContext context) =>
            Calibration.ArchThrust * Material.AxialStrength * SectionFactor;

        public override float LateralCapacity(in CapacityContext context) =>
            Calibration.ArchLateral * Material.BendingStrength * SectionFactor *
            (context.IsBraced ? Calibration.BracedArchFactor : 1f);

        public override float LateralStiffness(in CapacityContext context) =>
            Material.BendingStrength * SectionFactor * 0.5f;
    }

    /// <summary>Member supported at one end only. Bending grows with the square of its reach.</summary>
    public sealed class Cantilever : StructuralMember
    {
        public Cantilever(MemberSpec spec, ResolvedGeometry geometry, MaterialSpec material)
            : base(spec, geometry, material) { }

        public override string ResistanceMode => "cantilever bending";

        public override float VerticalCapacity(in CapacityContext context)
        {
            var reach = Math.Max(Geometry.Length, 0.5f);
            var capacity = Calibration.CantileverBending * Material.BendingStrength * SectionFactor / (reach * reach) * 4f;
            return context.IsPropped ? capacity * Calibration.ProppedCantileverFactor : capacity;
        }

        /// <summary>Connections are made for gravity, so an untied cantilever resists little uplift.</summary>
        public override float UpliftCapacity(in CapacityContext context) =>
            VerticalCapacity(context) * Calibration.UntiedCantileverUpliftFactor *
            (context.IsBraced ? Calibration.TiedCantileverUpliftFactor : 1f);
    }

    /// <summary>Diagonal member that stiffens frames against sideways load.</summary>
    public sealed class Brace : StructuralMember
    {
        public Brace(MemberSpec spec, ResolvedGeometry geometry, MaterialSpec material)
            : base(spec, geometry, material) { }

        public override string ResistanceMode => "axial tension and compression";

        public override float VerticalCapacity(in CapacityContext context) =>
            Calibration.BraceAxial * Material.AxialStrength * SectionFactor;

        public override float LateralCapacity(in CapacityContext context) =>
            Calibration.BraceLateral * Material.AxialStrength * SectionFactor;

        public override float LateralStiffness(in CapacityContext context) =>
            Calibration.BraceStiffness * Material.AxialStrength * SectionFactor;
    }

    /// <summary>Roof or wall surface. Carries rain and wind by membrane action.</summary>
    public sealed class Panel : StructuralMember
    {
        public Panel(MemberSpec spec, ResolvedGeometry geometry, MaterialSpec material)
            : base(spec, geometry, material) { }

        private float PanelFactor => Spec.Thickness / Calibration.PanelReferenceThickness;

        protected override float Quantity => Geometry.Area * PanelFactor * 0.5f;

        public override string ResistanceMode => Spec.IsRoof ? "membrane action" : "wall compression";

        /// <summary>Roof panels bend between supports; walls mostly carry load in their own plane.</summary>
        public override float VerticalCapacity(in CapacityContext context) =>
            Calibration.PanelMembrane * (Spec.IsRoof ? Material.BendingStrength : Material.AxialStrength) *
            PanelFactor * Geometry.Area;

        /// <summary>
        /// A roof is held down by its own weight plus its fixings: nails into the timber, or storm
        /// straps (a brace tied to the roof) that hold far more. Light iron sheets need the straps.
        /// </summary>
        public override float UpliftCapacity(in CapacityContext context)
        {
            if (!Spec.IsRoof) return VerticalCapacity(context);
            var holdDown = context.IsBraced ? Calibration.StrappedRoofHoldDown : Calibration.NailedRoofHoldDown;
            return SelfWeight + holdDown * Geometry.Area;
        }
    }

    /// <summary>Downpipe or channel that carries roof runoff to a cistern.</summary>
    public sealed class Drain : StructuralMember
    {
        public Drain(MemberSpec spec, ResolvedGeometry geometry, MaterialSpec material)
            : base(spec, geometry, material) { }

        public override string ResistanceMode => "water flow";

        public float FlowCapacity => Material.DrainCapacity * SectionFactor;

        public override float VerticalCapacity(in CapacityContext context) =>
            Calibration.DrainBody * Material.AxialStrength * SectionFactor;
    }
}
