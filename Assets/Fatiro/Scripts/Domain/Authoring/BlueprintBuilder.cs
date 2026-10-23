using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fatiro.Domain
{
    /// <summary>A window or door opening in a wall, measured along the wall from its start.</summary>
    public readonly struct Opening
    {
        public Opening(float offset, float width, float sill, float head, bool isDoor = false)
        {
            Offset = offset;
            Width = width;
            Sill = sill;
            Head = head;
            IsDoor = isDoor;
        }

        public float Offset { get; }
        public float Width { get; }

        /// <summary>Height of the bottom of the opening above the floor (0 for doors).</summary>
        public float Sill { get; }

        /// <summary>Height of the top of the opening above the floor.</summary>
        public float Head { get; }

        public bool IsDoor { get; }

        public static Opening Window(float offset, float width = 1.2f, float sill = 0.9f, float head = 2.1f) =>
            new Opening(offset, width, sill, head);

        public static Opening DoorAt(float offset, float width = 0.9f, float head = 2.1f) =>
            new Opening(offset, width, 0f, head, true);
    }

    /// <summary>An opening's frame, ready to be glazed or fitted with a door.</summary>
    public readonly struct OpeningFrame
    {
        public OpeningFrame(string id, string name, Vector3 centre, Vector3 size, float yaw, bool isDoor)
        {
            Id = id;
            Name = name;
            Centre = centre;
            Size = size;
            Yaw = yaw;
            IsDoor = isDoor;
        }

        public string Id { get; }
        public string Name { get; }
        public Vector3 Centre { get; }
        public Vector3 Size { get; }
        public float Yaw { get; }
        public bool IsDoor { get; }
    }

    /// <summary>What WallWithOpenings built: the brick pieces, and the openings left for windows and doors.</summary>
    public sealed class WallBuild
    {
        public List<string> Pieces { get; } = new List<string>();
        public List<OpeningFrame> Openings { get; } = new List<OpeningFrame>();
        public string[] Rest => Pieces.ToArray();
    }

    /// <summary>Small fluent helper that keeps the blueprint definitions readable.</summary>
    public sealed class BlueprintBuilder
    {
        private readonly List<MemberSpec> members = new List<MemberSpec>();
        private readonly List<ShellProfile> shells = new List<ShellProfile>();
        private string component;

        public IReadOnlyList<MemberSpec> Members => members;
        public IReadOnlyList<ShellProfile> Shells => shells;

        /// <summary>Members added from now on belong to this building component (built in one go).</summary>
        public BlueprintBuilder Group(string name)
        {
            component = name;
            return this;
        }

        public string Pad(string id, string name, float x, float z, float width, float depth, float thickness,
            string material, bool required = true, string hint = null)
        {
            Add(new MemberSpec(id, name, MemberKind.Foundation, BuildStage.Foundation, MemberShape.Pad,
                Endpoint.Ground(x, 0f, z), Endpoint.Ground(x, 0f, z), thickness, material, MaterialLibrary.Footing,
                required: required, panelSize: new Vector3(width, thickness, depth), hint: hint, component: component));
            return id;
        }

        public string Strut(string id, string name, MemberKind kind, BuildStage stage, Endpoint a, Endpoint b,
            float thickness, string material, string[] allowed, string[] supports, bool required = true,
            string[] extraSupports = null, float tributaryArea = 0f, string hint = null)
        {
            Add(new MemberSpec(id, name, kind, stage, MemberShape.Strut, a, b, thickness, material, allowed,
                supports, extraSupports, required, tributaryArea: tributaryArea, hint: hint, component: component));
            return id;
        }

        /// <summary>A square-section member (concrete column or beam) between two points.</summary>
        public string Bar(string id, string name, MemberKind kind, BuildStage stage, Endpoint a, Endpoint b,
            float thickness, string material, string[] allowed, string[] supports, bool required = true,
            string[] extraSupports = null, float tributaryArea = 0f, string hint = null)
        {
            Add(new MemberSpec(id, name, kind, stage, MemberShape.Bar, a, b, thickness, material, allowed,
                supports, extraSupports, required, tributaryArea: tributaryArea, hint: hint, component: component));
            return id;
        }

        public string Arch(string id, string name, Endpoint footA, Endpoint footB, float crownHeight, float thickness,
            string material, string[] supports, bool required = true, string hint = null)
        {
            Add(new MemberSpec(id, name, MemberKind.Arch, BuildStage.PrimaryStructure, MemberShape.Arch, footA, footB,
                thickness, material, MaterialLibrary.Frame, supports, required: required, crownHeight: crownHeight, hint: hint,
                component: component));
            return id;
        }

        public string Panel(string id, string name, BuildStage stage, Vector3 centre, Vector3 size, Vector3 euler,
            string material, string[] supports, bool isRoof, bool required = true, string hint = null, string[] allowed = null,
            float tributaryArea = 0f, bool groundRelative = false)
        {
            Add(new MemberSpec(id, name, MemberKind.Panel, stage, MemberShape.Panel,
                new Endpoint(centre, groundRelative), new Endpoint(centre, groundRelative), size.Y, material, allowed ?? MaterialLibrary.Cladding,
                supports, required: required, panelSize: size, panelEuler: euler, isRoof: isRoof, hint: hint,
                tributaryArea: tributaryArea, component: component));
            return id;
        }

        /// <summary>A level concrete floor slab carrying people and furniture over its area.</summary>
        public string Slab(string id, string name, BuildStage stage, float x, float y, float z, float width, float depth,
            string[] supports, float thickness = 0.15f, string hint = null, bool groundRelative = false, bool required = true)
        {
            return Panel(id, name, stage, new Vector3(x, y, z), new Vector3(width, thickness, depth), Vector3.Zero,
                MaterialLibrary.Concrete, supports, isRoof: false, required: required, hint: hint, allowed: MaterialLibrary.Slab,
                tributaryArea: width * depth, groundRelative: groundRelative);
        }

        public string Drain(string id, string name, Endpoint a, Endpoint b, float thickness, string material,
            string[] supports, string hint = null)
        {
            Add(new MemberSpec(id, name, MemberKind.Drain, BuildStage.Envelope, MemberShape.Strut, a, b,
                thickness, material, MaterialLibrary.Drainage, supports, hint: hint, component: component));
            return id;
        }

        /// <summary>
        /// A brick wall standing between two points. As a Brace it ties together whatever it is built
        /// against: footings for a foundation wall, columns for an infill shear wall. Its top is either
        /// a fixed height above its base or a level top (topHeight) such as the underside of a beam.
        /// </summary>
        public string Wall(string id, string name, BuildStage stage, Endpoint a, Endpoint b, float height, float thickness,
            string material, string[] ties, bool required = true, string hint = null, float topHeight = 0f)
        {
            Add(new MemberSpec(id, name, MemberKind.Brace, stage, MemberShape.Block, a, b, thickness, material,
                MaterialLibrary.Masonry, ties, required: required, crownHeight: topHeight, panelSize: new Vector3(thickness, height, 0f),
                hint: hint, component: component));
            return id;
        }

        /// <summary>
        /// Non-structural wall between two points, rising a fixed height (or to a level top): a partition,
        /// the brick under a window, a lintel. It encloses and catches wind but braces nothing.
        /// </summary>
        public string CladWall(string id, string name, BuildStage stage, Endpoint a, Endpoint b, float height, float thickness,
            string material, string[] supports, string hint = null, float topHeight = 0f, bool required = true, string[] allowed = null)
        {
            Add(new MemberSpec(id, name, MemberKind.Panel, stage, MemberShape.Block, a, b, thickness, material,
                allowed ?? MaterialLibrary.Masonry, supports, required: required, crownHeight: topHeight,
                panelSize: new Vector3(thickness, height, 0f), hint: hint, component: component));
            return id;
        }

        /// <summary>
        /// A brick wall with windows and doors, from a to b on a floor at floorY, up to topY. Full-height
        /// piers between openings tie the columns at its ends (they brace the frame); the brick under
        /// windows and the lintels over openings are infill. Returns the opening frames for later glazing.
        /// </summary>
        public WallBuild WallWithOpenings(
            string prefix, string name, BuildStage stage, Vector2 a, Vector2 b, float floorY, float topY, float thickness,
            string[] ties, Opening[] openings, bool required = true, string hint = null)
        {
            var build = new WallBuild();
            var start = new Vector3(a.X, floorY, a.Y);
            var end = new Vector3(b.X, floorY, b.Y);
            var length = Vector3.Distance(start, end);
            var along = (end - start) / Math.Max(length, 0.01f);
            // Panels are boxes whose local x is their width: turn that x to run along the wall.
            var yaw = (float)(Math.Atan2(-along.Z, along.X) * 180.0 / Math.PI);
            var height = topY - floorY;
            Endpoint At(float offset, float y = 0f) => Endpoint.At(start.X + along.X * offset, floorY + y, start.Z + along.Z * offset);

            var cursor = 0f;
            var piece = 0;
            var sorted = new List<Opening>(openings ?? Array.Empty<Opening>());
            sorted.Sort((left, right) => left.Offset.CompareTo(right.Offset));
            var piers = new List<string>();
            foreach (var opening in sorted)
            {
                var left = opening.Offset - opening.Width * 0.5f;
                var right = opening.Offset + opening.Width * 0.5f;
                if (left - cursor > 0.05f)
                    piers.Add(Wall($"{prefix}-{++piece}", $"{name} {piece}", stage, At(cursor), At(left), height, thickness,
                        MaterialLibrary.EarthBlock, ties, required, hint));
                cursor = right;
            }
            if (length - cursor > 0.05f)
                piers.Add(Wall($"{prefix}-{++piece}", $"{name} {piece}", stage, At(cursor), At(length), height, thickness,
                    MaterialLibrary.EarthBlock, ties, required, hint));
            build.Pieces.AddRange(piers);
            var rests = piers.Count > 0 ? piers.ToArray() : ties;

            var index = 0;
            foreach (var opening in sorted)
            {
                index++;
                var left = opening.Offset - opening.Width * 0.5f;
                var right = opening.Offset + opening.Width * 0.5f;
                if (opening.Sill > 0.05f)
                    build.Pieces.Add(CladWall($"{prefix}-SILL{index}", $"{name} under window {index}", stage, At(left), At(right), opening.Sill, thickness,
                        MaterialLibrary.EarthBlock, rests, required: required));
                if (height - opening.Head > 0.05f)
                    build.Pieces.Add(CladWall($"{prefix}-LINTEL{index}", $"{name} lintel {index}", stage, At(left, opening.Head), At(right, opening.Head),
                        height - opening.Head, thickness, MaterialLibrary.Concrete, rests, required: required));
                var centre = start + along * opening.Offset + new Vector3(0f, (opening.Sill + opening.Head) * 0.5f, 0f);
                build.Openings.Add(new OpeningFrame($"{prefix}-{(opening.IsDoor ? "DOOR" : "WIN")}{index}",
                    $"{name} {(opening.IsDoor ? "door" : "window")} {index}", centre,
                    new Vector3(opening.Width, 0.06f, opening.Head - opening.Sill), yaw, opening.IsDoor));
            }
            return build;
        }

        /// <summary>Glazes or hangs a door in an opening returned by WallWithOpenings.</summary>
        public string Fit(OpeningFrame frame, string[] supports, bool required = true)
        {
            var material = frame.IsDoor ? MaterialLibrary.Door : MaterialLibrary.Window;
            return Panel(frame.Id, frame.Name, BuildStage.Envelope, frame.Centre, frame.Size, new Vector3(90f, frame.Yaw, 0f), material,
                supports, isRoof: false, required: required, allowed: frame.IsDoor ? MaterialLibrary.Doors : MaterialLibrary.Windows);
        }

        /// <summary>Registers a lofted shell; returns its index for shell patches.</summary>
        public int Shell(string name, params ShellStation[] stations)
        {
            shells.Add(new ShellProfile(name, stations));
            return shells.Count - 1;
        }

        /// <summary>
        /// One piece of a shell between two stations and two positions round the section (0 right base,
        /// 0.5 crown, 1 left base); equal stations give an end cap. Its area and centre are measured
        /// from the shell itself, so the solver loads it like any other panel.
        /// </summary>
        public string Patch(string id, string name, BuildStage stage, int profile, int from, int to, float aroundFrom, float aroundTo,
            float thickness, string material, string[] allowed, string[] supports, bool isRoof, bool required = true,
            MemberKind kind = MemberKind.Panel, string hint = null)
        {
            var patch = new ShellPatch(profile, from, to, aroundFrom, aroundTo);
            var (area, centre) = ShellMath.Measure(shells[profile].Resolve(null), patch);
            var side = (float)Math.Sqrt(Math.Max(area, 0.01f));
            Add(new MemberSpec(id, name, kind, stage, MemberShape.Shell, new Endpoint(centre, false), new Endpoint(centre, false),
                thickness, material, allowed, supports, required: required, panelSize: new Vector3(side, thickness, side),
                isRoof: isRoof, hint: hint, component: component, patch: patch));
            return id;
        }

        public static string[] On(params string[] ids) => ids;

        private void Add(MemberSpec spec) => members.Add(spec);
    }
}
