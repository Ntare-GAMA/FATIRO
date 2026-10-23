using System;
using System.Numerics;

namespace Fatiro.Domain
{
    /// <summary>
    /// A point in design space. When GroundRelative is true, Y is measured from the sloped
    /// ground under the point, so the same blueprint stretches its stilts on steeper sites.
    /// </summary>
    public readonly struct Endpoint
    {
        public Endpoint(float x, float y, float z, bool groundRelative = false)
        {
            Position = new Vector3(x, y, z);
            GroundRelative = groundRelative;
        }

        public Endpoint(Vector3 position, bool groundRelative)
        {
            Position = position;
            GroundRelative = groundRelative;
        }

        public Vector3 Position { get; }
        public bool GroundRelative { get; }

        public static Endpoint Ground(float x, float heightAboveGround, float z) => new Endpoint(x, heightAboveGround, z, true);
        public static Endpoint At(float x, float y, float z) => new Endpoint(x, y, z);
    }

    /// <summary>
    /// Immutable description of one member in a house design (Flyweight data shared by every
    /// session that builds that design). Engine-free so it can be unit tested.
    /// </summary>
    public sealed class MemberSpec
    {
        public MemberSpec(
            string id,
            string displayName,
            MemberKind kind,
            BuildStage stage,
            MemberShape shape,
            Endpoint a,
            Endpoint b,
            float thickness,
            string defaultMaterialId,
            string[] allowedMaterialIds,
            string[] supports = null,
            string[] extraSupports = null,
            bool required = true,
            float crownHeight = 0f,
            Vector3 panelSize = default,
            Vector3 panelEuler = default,
            float tributaryArea = 0f,
            bool isRoof = false,
            string hint = null,
            string component = null,
            ShellPatch patch = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Member id is required.", nameof(id));
            if (allowedMaterialIds == null || allowedMaterialIds.Length == 0)
                throw new ArgumentException($"Member {id} needs at least one allowed material.", nameof(allowedMaterialIds));
            if (Array.IndexOf(allowedMaterialIds, defaultMaterialId) < 0)
                throw new ArgumentException($"Default material {defaultMaterialId} of {id} is not in its allowed list.");

            Id = id;
            DisplayName = displayName;
            Kind = kind;
            Stage = stage;
            Shape = shape;
            A = a;
            B = b;
            Thickness = thickness;
            DefaultMaterialId = defaultMaterialId;
            AllowedMaterialIds = allowedMaterialIds;
            Supports = supports ?? Array.Empty<string>();
            ExtraSupports = extraSupports ?? Array.Empty<string>();
            Required = required;
            CrownHeight = crownHeight;
            PanelSize = panelSize;
            PanelEuler = panelEuler;
            TributaryArea = tributaryArea;
            IsRoof = isRoof;
            Hint = hint ?? string.Empty;
            Component = string.IsNullOrEmpty(component) ? displayName : component;
            Patch = patch;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public MemberKind Kind { get; }
        public BuildStage Stage { get; }
        public MemberShape Shape { get; }

        /// <summary>Start point (struts, arches) or centre (pads, panels).</summary>
        public Endpoint A { get; }

        /// <summary>End point for struts and arches. Unused for pads and panels.</summary>
        public Endpoint B { get; }

        public float Thickness { get; }
        public string DefaultMaterialId { get; }
        public string[] AllowedMaterialIds { get; }

        /// <summary>Members that must already be placed before this one (FR-11) and that carry its load.</summary>
        public string[] Supports { get; }

        /// <summary>Optional members that also carry load when present (e.g. a prop under a cantilever).</summary>
        public string[] ExtraSupports { get; }

        /// <summary>Required members must be placed to complete their stage (FR-14). Others are reinforcements.</summary>
        public bool Required { get; }

        /// <summary>Absolute height of an arch crown. Lower feet on a slope give a taller arch.</summary>
        public float CrownHeight { get; }

        /// <summary>Panel width (x), thickness (y) and length (z).</summary>
        public Vector3 PanelSize { get; }

        /// <summary>Panel rotation in degrees.</summary>
        public Vector3 PanelEuler { get; }

        /// <summary>Floor area (m²) this member carries for occupancy loads.</summary>
        public float TributaryArea { get; }

        /// <summary>Roof panels collect rain and suffer wind uplift.</summary>
        public bool IsRoof { get; }

        /// <summary>Short engineering note shown in the inspector.</summary>
        public string Hint { get; }

        /// <summary>The building component this member belongs to, e.g. "Ground-floor walls". Built in one go (fast build).</summary>
        public string Component { get; }

        /// <summary>For shell members: which piece of the design's lofted shell this member is.</summary>
        public ShellPatch Patch { get; }

        public bool AllowsMaterial(string materialId) => Array.IndexOf(AllowedMaterialIds, materialId) >= 0;
    }
}
