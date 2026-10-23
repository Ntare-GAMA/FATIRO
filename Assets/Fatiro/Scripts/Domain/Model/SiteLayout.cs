using System;
using System.Numerics;

namespace Fatiro.Domain
{
    /// <summary>Concrete geometry of a member once the site slope is known.</summary>
    public sealed class ResolvedGeometry
    {
        public ResolvedGeometry(Vector3 a, Vector3 b, float length, float area)
        {
            A = a;
            B = b;
            Length = length;
            Area = area;
        }

        /// <summary>Start point, or centre for pads and panels.</summary>
        public Vector3 A { get; }
        public Vector3 B { get; }
        public float Length { get; }

        /// <summary>Surface area for panels, plan area for pads.</summary>
        public float Area { get; }
    }

    /// <summary>
    /// Places a blueprint on a sloped site. Downhill is +Z: ground drops by tan(slope) per metre,
    /// so ground-anchored members on the downhill side become longer and more slender.
    /// </summary>
    public sealed class SiteLayout
    {
        private readonly float referenceZ;
        private readonly float tangent;

        public SiteLayout(SiteConditions site, float uphillReferenceZ)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            referenceZ = uphillReferenceZ;
            tangent = site.SlopeTangent;
        }

        public SiteConditions Site { get; }

        public float GroundY(float z) => -(z - referenceZ) * tangent;

        public Vector3 Resolve(Endpoint endpoint)
        {
            var p = endpoint.Position;
            return endpoint.GroundRelative ? new Vector3(p.X, GroundY(p.Z) + p.Y, p.Z) : p;
        }

        public ResolvedGeometry Resolve(MemberSpec spec)
        {
            var a = Resolve(spec.A);
            switch (spec.Shape)
            {
                case MemberShape.Pad:
                {
                    var size = spec.PanelSize;
                    return new ResolvedGeometry(a, a, Math.Max(size.X, size.Z), size.X * size.Z);
                }
                case MemberShape.Panel:
                case MemberShape.Shell:
                {
                    var size = spec.PanelSize;
                    return new ResolvedGeometry(a, a, Math.Max(size.X, size.Z), size.X * size.Z);
                }
                case MemberShape.Block:
                {
                    // Walls either have a fixed height (plinth walls) or rise to a level top (CrownHeight),
                    // such as a ground-floor wall under a beam. Area / Length gives the height back.
                    var b = Resolve(spec.B);
                    var length = Vector3.Distance(a, b);
                    var height = spec.CrownHeight > 0f ? Math.Max(0.3f, spec.CrownHeight - Math.Max(a.Y, b.Y)) : spec.PanelSize.Y;
                    return new ResolvedGeometry(a, b, length, length * height);
                }
                case MemberShape.Arch:
                {
                    var b = Resolve(spec.B);
                    var span = Vector3.Distance(a, b);
                    var rise = ArchRise(spec, a, b);
                    // Parabolic arc length approximation.
                    var length = span + 8f * rise * rise / (3f * Math.Max(span, 0.01f));
                    return new ResolvedGeometry(a, b, length, 0f);
                }
                default:
                {
                    var b = Resolve(spec.B);
                    return new ResolvedGeometry(a, b, Vector3.Distance(a, b), 0f);
                }
            }
        }

        /// <summary>Arches keep their crown at a fixed height, so lower feet mean a taller arch.</summary>
        public static float ArchRise(MemberSpec spec, Vector3 a, Vector3 b) =>
            Math.Max(0.2f, spec.CrownHeight - Math.Max(a.Y, b.Y));
    }
}
