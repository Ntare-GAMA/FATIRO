using System.Collections.Generic;
using System.Numerics;

namespace Fatiro.Domain
{
    /// <summary>Small fluent helper that keeps the blueprint definitions readable.</summary>
    public sealed class BlueprintBuilder
    {
        private readonly List<MemberSpec> members = new List<MemberSpec>();

        public IReadOnlyList<MemberSpec> Members => members;

        public string Pad(string id, string name, float x, float z, float width, float depth, float thickness,
            string material, bool required = true, string hint = null)
        {
            members.Add(new MemberSpec(id, name, MemberKind.Foundation, BuildStage.Foundation, MemberShape.Pad,
                Endpoint.Ground(x, 0f, z), Endpoint.Ground(x, 0f, z), thickness, material, MaterialLibrary.Footing,
                required: required, panelSize: new Vector3(width, thickness, depth), hint: hint));
            return id;
        }

        public string Strut(string id, string name, MemberKind kind, BuildStage stage, Endpoint a, Endpoint b,
            float thickness, string material, string[] allowed, string[] supports, bool required = true,
            string[] extraSupports = null, float tributaryArea = 0f, string hint = null)
        {
            members.Add(new MemberSpec(id, name, kind, stage, MemberShape.Strut, a, b, thickness, material, allowed,
                supports, extraSupports, required, tributaryArea: tributaryArea, hint: hint));
            return id;
        }

        public string Arch(string id, string name, Endpoint footA, Endpoint footB, float crownHeight, float thickness,
            string material, string[] supports, bool required = true, string hint = null)
        {
            members.Add(new MemberSpec(id, name, MemberKind.Arch, BuildStage.PrimaryStructure, MemberShape.Arch, footA, footB,
                thickness, material, MaterialLibrary.Frame, supports, required: required, crownHeight: crownHeight, hint: hint));
            return id;
        }

        public string Panel(string id, string name, BuildStage stage, Vector3 centre, Vector3 size, Vector3 euler,
            string material, string[] supports, bool isRoof, bool required = true, string hint = null)
        {
            members.Add(new MemberSpec(id, name, MemberKind.Panel, stage, MemberShape.Panel,
                new Endpoint(centre, false), new Endpoint(centre, false), size.Y, material, MaterialLibrary.Cladding,
                supports, required: required, panelSize: size, panelEuler: euler, isRoof: isRoof, hint: hint));
            return id;
        }

        public string Drain(string id, string name, Endpoint a, Endpoint b, float thickness, string material,
            string[] supports, string hint = null)
        {
            members.Add(new MemberSpec(id, name, MemberKind.Drain, BuildStage.Envelope, MemberShape.Strut, a, b,
                thickness, material, MaterialLibrary.Drainage, supports, hint: hint));
            return id;
        }

        /// <summary>
        /// A brick wall standing on the ground between two points (ground-relative, so it steps with
        /// the slope). As a Brace it ties together whatever it is built against: footings for a plinth
        /// wall, legs or stilts for an infill shear wall.
        /// </summary>
        public string Wall(string id, string name, BuildStage stage, Endpoint a, Endpoint b, float height, float thickness,
            string material, string[] ties, bool required = true, string hint = null, float topHeight = 0f)
        {
            members.Add(new MemberSpec(id, name, MemberKind.Brace, stage, MemberShape.Block, a, b, thickness, material,
                MaterialLibrary.Masonry, ties, required: required, crownHeight: topHeight, panelSize: new Vector3(thickness, height, 0f), hint: hint));
            return id;
        }

        public static string[] On(params string[] ids) => ids;
    }
}
