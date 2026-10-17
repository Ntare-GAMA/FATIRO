using System;

namespace Fatiro.Domain
{
    /// <summary>Everything a member needs to know about its surroundings to work out its capacity.</summary>
    public readonly struct CapacityContext
    {
        public CapacityContext(SiteConditions site, FoundationStyle style, bool isBraced, bool isPropped)
        {
            Site = site;
            Style = style;
            IsBraced = isBraced;
            IsPropped = isPropped;
        }

        public SiteConditions Site { get; }
        public FoundationStyle Style { get; }

        /// <summary>A working brace is connected to this member.</summary>
        public bool IsBraced { get; }

        /// <summary>An optional extra support (prop) is present under this member.</summary>
        public bool IsPropped { get; }
    }

    /// <summary>Result of one load test for one member.</summary>
    public sealed class MemberResult
    {
        public MemberResult(string memberId, float verticalDemand, float lateralDemand, float utilisation,
            bool failed, string failureReason, int failureWave)
        {
            MemberId = memberId;
            VerticalDemand = verticalDemand;
            LateralDemand = lateralDemand;
            Utilisation = utilisation;
            Failed = failed;
            FailureReason = failureReason ?? string.Empty;
            FailureWave = failureWave;
        }

        public string MemberId { get; }
        public float VerticalDemand { get; }
        public float LateralDemand { get; }

        /// <summary>Applied load ÷ capacity (FR-16). Above 1 means failure.</summary>
        public float Utilisation { get; }

        public bool Failed { get; }
        public string FailureReason { get; }

        /// <summary>0 for members that fail first, 1+ for members brought down by them (FR-18).</summary>
        public int FailureWave { get; }
    }

    /// <summary>
    /// Base class for every structural member. Subclasses decide how capacity is computed
    /// (buckling, bending, arch action, membrane action...). State and utilisation are
    /// read-only outside the class and change only through ApplyResult (encapsulation).
    /// </summary>
    public abstract class StructuralMember
    {
        protected StructuralMember(MemberSpec spec, ResolvedGeometry geometry, MaterialSpec material)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            Material = material ?? throw new ArgumentNullException(nameof(material));
        }

        public MemberSpec Spec { get; }
        public ResolvedGeometry Geometry { get; }
        public MaterialSpec Material { get; private set; }

        public string Id => Spec.Id;
        public MemberKind Kind => Spec.Kind;

        public MemberState State { get; private set; } = MemberState.Placed;
        public float Utilisation { get; private set; }
        public float VerticalDemand { get; private set; }
        public float LateralDemand { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;
        public bool IsFailed => State == MemberState.Failed;

        /// <summary>Size relative to the 0.2 m reference section; capacity and weight scale with its square.</summary>
        protected float SectionFactor
        {
            get
            {
                var ratio = Spec.Thickness / Calibration.ReferenceThickness;
                return ratio * ratio;
            }
        }

        /// <summary>Amount of material, in metres for linear members and m² for surfaces.</summary>
        protected virtual float Quantity => Geometry.Length * SectionFactor;

        public float SelfWeight => Material.UnitWeight * Quantity;
        public float Cost => Material.UnitCost * Quantity;

        /// <summary>Short phrase describing how this member resists load, shown in the inspector.</summary>
        public abstract string ResistanceMode { get; }

        public abstract float VerticalCapacity(in CapacityContext context);

        /// <summary>Capacity against sideways load. Zero means the member is not part of the lateral system.</summary>
        public virtual float LateralCapacity(in CapacityContext context) => 0f;

        /// <summary>Relative share of the sideways load this member attracts.</summary>
        public virtual float LateralStiffness(in CapacityContext context) => 0f;

        /// <summary>Capacity against wind lifting the member upwards.</summary>
        public virtual float UpliftCapacity(in CapacityContext context) => VerticalCapacity(context);

        public void ChangeMaterial(MaterialSpec material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (!Spec.AllowsMaterial(material.Id))
                throw new InvalidOperationException($"{Spec.DisplayName} cannot be made of {material.DisplayName}.");
            Material = material;
        }

        public void ApplyResult(MemberResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.MemberId != Id) throw new ArgumentException($"Result for {result.MemberId} applied to {Id}.");
            Utilisation = result.Utilisation;
            VerticalDemand = result.VerticalDemand;
            LateralDemand = result.LateralDemand;
            FailureReason = result.FailureReason;
            State = result.Failed ? MemberState.Failed : MemberState.Placed;
        }

        /// <summary>Clears the last load test, e.g. before the learner modifies and re-tests (FR-21).</summary>
        public void ResetResult()
        {
            Utilisation = 0f;
            VerticalDemand = 0f;
            LateralDemand = 0f;
            FailureReason = string.Empty;
            State = MemberState.Placed;
        }
    }
}
