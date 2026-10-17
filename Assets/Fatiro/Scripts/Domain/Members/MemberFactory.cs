using System;

namespace Fatiro.Domain
{
    /// <summary>
    /// Factory Method: the only place that maps a member kind to its StructuralMember subclass
    /// and resolves its geometry for the current site, so creation is never duplicated.
    /// </summary>
    public sealed class MemberFactory
    {
        private readonly SiteLayout layout;

        public MemberFactory(SiteLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public StructuralMember Create(MemberSpec spec, MaterialSpec material)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (!spec.AllowsMaterial(material.Id))
                throw new InvalidOperationException($"{spec.DisplayName} cannot be made of {material.DisplayName}.");

            var geometry = layout.Resolve(spec);
            switch (spec.Kind)
            {
                case MemberKind.Foundation: return new Foundation(spec, geometry, material);
                case MemberKind.Column: return new Column(spec, geometry, material);
                case MemberKind.Beam: return new Beam(spec, geometry, material);
                case MemberKind.Arch: return new Arch(spec, geometry, material);
                case MemberKind.Cantilever: return new Cantilever(spec, geometry, material);
                case MemberKind.Brace: return new Brace(spec, geometry, material);
                case MemberKind.Panel: return new Panel(spec, geometry, material);
                case MemberKind.Drain: return new Drain(spec, geometry, material);
                default: throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, "Unknown member kind.");
            }
        }
    }
}
