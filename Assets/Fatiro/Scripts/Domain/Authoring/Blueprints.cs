using System;
using System.Collections.Generic;
using System.Numerics;
using static Fatiro.Domain.BlueprintBuilder;

namespace Fatiro.Domain
{
    /// <summary>
    /// Authoring definitions of the six designs. The editor tool converts these into HouseDesign
    /// assets; at runtime the app only reads those assets (designs as data, NFR-12).
    /// Coordinates are metres. Downhill is +Z; the uphill reference edge has ground height 0.
    /// Biomimetic houses wear a lofted ferrocement shell shaped like their animal or object, over a
    /// real concrete and brick house; the Rwandan classics are reinforced-concrete frames with brick infill.
    /// </summary>
    public static class Blueprints
    {
        public const string PantherId = "PN-01";
        public const string ElephantId = "EL-01";
        public const string CarId = "CR-01";
        public const string BungalowId = "BG-01";
        public const string TwoStoreyId = "TS-01";
        public const string FlatsId = "AP-01";

        public const string Biomimetic = "Biomimetic";
        public const string Classic = "Rwandan classic";

        private const float ShellThickness = 0.08f;
        private const float GlassThickness = 0.04f;

        public static IReadOnlyList<HouseBlueprint> All() => new[] { Panther(), Elephant(), Car(), Bungalow(), TwoStorey(), Flats() };

        public static HouseBlueprint Get(string designId)
        {
            foreach (var blueprint in All())
                if (blueprint.DesignId == designId) return blueprint;
            return null;
        }

        // ---- Biomimetic ------------------------------------------------------------------

        /// <summary>
        /// Ingwe: a black panther crouched down the hillside. Concrete rib arches on brick paws carry a
        /// ferrocement body; split-level floors step down inside; its raised head is a viewing room.
        /// </summary>
        public static HouseBlueprint Panther()
        {
            var b = new BlueprintBuilder();
            float[] z = { -4.0f, -2.4f, -0.8f, 0.8f, 2.4f, 4.0f };
            float[] crown = { 4.2f, 4.0f, 3.8f, 3.6f, 3.4f, 3.3f };
            const float halfSpan = 2.6f;
            var ribs = new string[z.Length];
            var paws = new string[z.Length];
            var plinthLeft = new string[z.Length - 1];
            var plinthRight = new string[z.Length - 1];

            b.Group("Paw strip footings");
            for (var i = 0; i < z.Length; i++)
            {
                paws[i] = b.Pad($"PN-PAW-{i + 1}", $"Paw strip footing {i + 1}", 0f, z[i], 5.8f, 0.9f, 0.6f, MaterialLibrary.Concrete,
                    hint: "Concrete strip footing that spreads the rib's thrust into the hillside, like a paw pressed into the ground.");
            }

            b.Group("Brick plinth walls");
            for (var i = 0; i < z.Length - 1; i++)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    var id = b.Wall($"PN-PLINTH-{(side < 0 ? "L" : "R")}{i + 1}", $"{(side < 0 ? "Left" : "Right")} plinth wall {i + 1}", BuildStage.Foundation,
                        Endpoint.Ground(side * halfSpan, 0f, z[i]), Endpoint.Ground(side * halfSpan, 0f, z[i + 1]), 0.7f, 0.35f,
                        MaterialLibrary.EarthBlock, On(paws[i], paws[i + 1]),
                        hint: "Brick plinth wall tying the paw footings together so they cannot slide apart.");
                    if (side < 0) plinthLeft[i] = id;
                    else plinthRight[i] = id;
                }
            }

            b.Group("Forepaw footings");
            var forepawLeft = b.Pad("PN-FOREPAW-L", "Left forepaw pier", -0.9f, 6.6f, 1.0f, 1.0f, 0.6f, MaterialLibrary.Concrete,
                required: false, hint: "Optional footing for a prop under the head.");
            var forepawRight = b.Pad("PN-FOREPAW-R", "Right forepaw pier", 0.9f, 6.6f, 1.0f, 1.0f, 0.6f, MaterialLibrary.Concrete,
                required: false, hint: "Optional footing for a prop under the head.");

            b.Group("Split-level floors");
            for (var i = 0; i < z.Length - 1; i++)
            {
                b.Slab($"PN-FLOOR-{i + 1}", $"Floor level {i + 1}", BuildStage.PrimaryStructure, 0f, 0.78f, (z[i] + z[i + 1]) * 0.5f,
                    halfSpan * 2f - 0.3f, z[i + 1] - z[i], On(plinthLeft[i], plinthRight[i]), groundRelative: true,
                    hint: "Each floor steps down the slope with the ground, like a panther's back.");
            }

            b.Group("Concrete rib arches");
            for (var i = 0; i < z.Length; i++)
            {
                ribs[i] = b.Arch($"PN-RIB-{i + 1}", $"Rib arch {i + 1}", Endpoint.Ground(-halfSpan, 0.35f, z[i]),
                    Endpoint.Ground(halfSpan, 0.35f, z[i]), crown[i], 0.22f, MaterialLibrary.Concrete, On(paws[i]),
                    hint: "Strong under its own weight, but tips over sideways unless braced.");
            }

            b.Group("Spine beams");
            for (var i = 0; i < z.Length - 1; i++)
            {
                b.Bar($"PN-SPINE-{i + 1}", $"Spine beam {i + 1}", MemberKind.Beam, BuildStage.PrimaryStructure,
                    Endpoint.At(0f, crown[i], z[i]), Endpoint.At(0f, crown[i + 1], z[i + 1]), 0.22f, MaterialLibrary.Concrete,
                    MaterialLibrary.Frame, On(ribs[i], ribs[i + 1]), hint: "Ties neighbouring ribs together along the back.");
            }

            b.Group("Foreleg props");
            var forelegLeft = b.Strut("PN-FORELEG-L", "Left foreleg prop", MemberKind.Brace, BuildStage.PrimaryStructure,
                Endpoint.Ground(-0.9f, 0.35f, 6.6f), Endpoint.At(-0.9f, 2.85f, 6.6f), 0.26f, MaterialLibrary.Steel, MaterialLibrary.Frame,
                On(forepawLeft), required: false, hint: "Props the head so it no longer acts as a cantilever.");
            var forelegRight = b.Strut("PN-FORELEG-R", "Right foreleg prop", MemberKind.Brace, BuildStage.PrimaryStructure,
                Endpoint.Ground(0.9f, 0.35f, 6.6f), Endpoint.At(0.9f, 2.85f, 6.6f), 0.26f, MaterialLibrary.Steel, MaterialLibrary.Frame,
                On(forepawRight), required: false, hint: "Props the head so it no longer acts as a cantilever.");

            b.Group("Head cantilever");
            var head = b.Bar("PN-HEAD", "Head cantilever", MemberKind.Cantilever, BuildStage.PrimaryStructure,
                Endpoint.At(0f, 3.05f, 4.0f), Endpoint.At(0f, 2.9f, 7.2f), 0.31f, MaterialLibrary.Concrete, MaterialLibrary.Frame,
                On(ribs[5]), extraSupports: On(forelegLeft, forelegRight),
                hint: "The viewing room hangs 3.2 m over the slope. People in it bend it hard.");
            var headFloor = b.Slab("PN-HEAD-FLOOR", "Viewing-room floor", BuildStage.PrimaryStructure, 0f, 3.28f, 5.6f, 2.6f, 3.0f, On(head),
                hint: "The floor of the panther's head, carried by the cantilever.");

            b.Group("Flank bracing");
            for (var i = 0; i < z.Length - 1; i++)
            {
                if (i == 1) b.Group("Extra flank bracing");
                foreach (var side in new[] { -1, 1 })
                {
                    var label = side < 0 ? "L" : "R";
                    b.Strut($"PN-BRACE-{label}{i + 1}", $"{(side < 0 ? "Left" : "Right")} flank brace {i + 1}", MemberKind.Brace,
                        BuildStage.Bracing, Endpoint.Ground(side * halfSpan, 0.5f, z[i]), Endpoint.Ground(side * 2.3f, 1.15f, z[i + 1]),
                        0.1f, MaterialLibrary.Steel, MaterialLibrary.Bracing, On(ribs[i], ribs[i + 1]), required: i == 0,
                        hint: "Steel brace that stops the ribs falling like dominoes in an earthquake.");
                }
            }

            // The body: a crouched, ground-hugging shell that is highest at the haunches.
            var body = b.Shell("Panther body",
                new ShellStation(-5.0f, 0.7f, 2.7f, 1.3f, 2.2f, baseOnGround: true),
                new ShellStation(-4.2f, 0.7f, 4.4f, 2.95f, 2.4f, baseOnGround: true),
                new ShellStation(-2.4f, 0.7f, 4.2f, 2.95f, 2.4f, baseOnGround: true),
                new ShellStation(-0.8f, 0.7f, 4.0f, 2.9f, 2.4f, baseOnGround: true),
                new ShellStation(0.8f, 0.7f, 3.8f, 2.9f, 2.4f, baseOnGround: true),
                new ShellStation(2.4f, 0.7f, 3.6f, 2.9f, 2.4f, baseOnGround: true),
                new ShellStation(4.1f, 0.7f, 3.5f, 2.85f, 2.4f, baseOnGround: true));
            var headShell = b.Shell("Panther head",
                new ShellStation(3.9f, 2.7f, 5.0f, 2.2f, 2.4f),
                new ShellStation(5.0f, 2.75f, 5.45f, 2.1f, 2.4f),
                new ShellStation(6.2f, 2.85f, 5.35f, 1.85f, 2.4f),
                new ShellStation(7.2f, 2.95f, 5.0f, 1.45f, 2.2f),
                new ShellStation(7.7f, 3.1f, 4.6f, 1.1f, 2.0f));
            var tail = b.Shell("Panther tail",
                new ShellStation(-4.9f, 1.9f, 2.55f, 0.32f, 2f),
                new ShellStation(-5.7f, 2.05f, 2.6f, 0.28f, 2f, 0.25f),
                new ShellStation(-6.4f, 2.4f, 2.9f, 0.24f, 2f, 0.75f),
                new ShellStation(-6.85f, 3.0f, 3.4f, 0.2f, 2f, 1.05f),
                new ShellStation(-7.05f, 3.5f, 3.75f, 0.14f, 2f, 1.1f));

            b.Group("Body shell");
            b.Patch("PN-BACK-1", "Back shell, haunches", BuildStage.Roof, body, 0, 3, 0.3f, 0.7f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, On(ribs[0], ribs[1], ribs[2]), isRoof: true, hint: "Thin ferrocement: cement mortar on steel mesh, curved so it works like an eggshell.");
            b.Patch("PN-BACK-2", "Back shell, shoulders", BuildStage.Roof, body, 3, 6, 0.3f, 0.7f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, On(ribs[3], ribs[4], ribs[5]), isRoof: true);
            b.Patch("PN-FLANK-R", "Right flank shell", BuildStage.Roof, body, 0, 6, 0f, 0.16f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, plinthRight, isRoof: false);
            b.Patch("PN-FLANK-L", "Left flank shell", BuildStage.Roof, body, 0, 6, 0.84f, 1f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, plinthLeft, isRoof: false);
            b.Patch("PN-HAUNCH-R", "Right haunch shell", BuildStage.Roof, body, 0, 1, 0.16f, 0.3f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, On(ribs[0]), isRoof: false);
            b.Patch("PN-HAUNCH-L", "Left haunch shell", BuildStage.Roof, body, 0, 1, 0.7f, 0.84f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, On(ribs[0]), isRoof: false);

            b.Group("Head shell");
            b.Patch("PN-HEAD-TOP", "Head shell, crown", BuildStage.Roof, headShell, 0, 4, 0.25f, 0.75f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, On(headFloor), isRoof: true);
            b.Patch("PN-CHEEK-R", "Right cheek shell", BuildStage.Roof, headShell, 0, 4, 0f, 0.25f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, On(headFloor), isRoof: false);
            b.Patch("PN-CHEEK-L", "Left cheek shell", BuildStage.Roof, headShell, 0, 4, 0.75f, 1f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, On(headFloor), isRoof: false);

            b.Group("Flank glazing");
            b.Patch("PN-GLASS-R", "Right flank glazing", BuildStage.Envelope, body, 1, 6, 0.16f, 0.3f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, On(ribs[1], ribs[5]), isRoof: false, hint: "A ribbon of glass along the flank, like light on a panther's coat.");
            b.Patch("PN-GLASS-L", "Left flank glazing", BuildStage.Envelope, body, 1, 6, 0.7f, 0.84f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, On(ribs[1], ribs[5]), isRoof: false);

            b.Group("Haunch and chest walls");
            var haunch = b.Patch("PN-HAUNCH-WALL", "Haunch end wall", BuildStage.Envelope, body, 0, 0, 0f, 1f, ShellThickness, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, On(ribs[0]), isRoof: false);
            b.Patch("PN-CHEST-GLASS", "Chest window", BuildStage.Envelope, body, 6, 6, 0f, 1f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, On(ribs[5]), isRoof: false, hint: "The panther's chest is one tall window looking down the valley.");

            b.Group("Face window");
            b.Patch("PN-FACE-GLASS", "Face window", BuildStage.Envelope, headShell, 4, 4, 0f, 1f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, On(headFloor), isRoof: false);

            b.Group("Tail canopy");
            b.Patch("PN-TAIL", "Tail canopy", BuildStage.Envelope, tail, 0, 4, 0f, 1f, 0.06f, MaterialLibrary.CharcoalFerrocement,
                MaterialLibrary.Shell, On(haunch), isRoof: false, hint: "A curling tail that shades the uphill terrace.");

            b.Group("Interior walls");
            for (var i = 0; i < 2; i++)
            {
                var wallZ = i == 0 ? z[1] + 0.05f : z[3] + 0.05f;
                var floor = i == 0 ? "PN-FLOOR-2" : "PN-FLOOR-4";
                foreach (var side in new[] { -1, 1 })
                {
                    b.CladWall($"PN-ROOM-{i + 1}{(side < 0 ? "L" : "R")}", $"Room wall {i + 1}{(side < 0 ? "a" : "b")}", BuildStage.Envelope,
                        Endpoint.Ground(side * 2.3f, 0.86f, wallZ), Endpoint.Ground(side * 0.5f, 0.86f, wallZ), 2.1f, 0.12f,
                        MaterialLibrary.EarthBlock, On(floor), hint: "Half-brick partition between rooms.");
                }
            }

            return new HouseBlueprint(PantherId, 2, "Ingwe", "panther",
                "A black panther crouched down the hillside. Concrete rib arches carry its ferrocement body; floors step down inside; its raised head is a viewing room over the valley.",
                "Rib arches need bracing to resist shaking, and cantilevers need depth, strength or a prop.",
                3, new FoundationStyle("Paw strips", "Panther paws spread weight along the ground", 1.2f, 0.4f, 1.1f), z[0], b.Members,
                b.Shells, Biomimetic);
        }

        /// <summary>
        /// Inzovu: an elephant on four thick concrete legs. Its domed ferrocement body holds a home on the
        /// belly floor; its ears are shade canopies and its trunk carries rainwater to a cistern.
        /// </summary>
        public static HouseBlueprint Elephant()
        {
            var b = new BlueprintBuilder();
            const float x = 3f;
            const float back = -2.5f;
            const float front = 2.5f;
            const float belly = 3f;

            b.Group("Elephant-foot footings");
            var footBL = b.Pad("EL-FOOT-BL", "Back-left foot", -x, back, 1.8f, 1.8f, 0.9f, MaterialLibrary.Concrete,
                hint: "Wide concrete elephant feet keep bearing pressure low on soft soil.");
            var footBR = b.Pad("EL-FOOT-BR", "Back-right foot", x, back, 1.8f, 1.8f, 0.9f, MaterialLibrary.Concrete);
            var footFL = b.Pad("EL-FOOT-FL", "Front-left foot", -x, front, 1.8f, 1.8f, 0.9f, MaterialLibrary.Concrete);
            var footFR = b.Pad("EL-FOOT-FR", "Front-right foot", x, front, 1.8f, 1.8f, 0.9f, MaterialLibrary.Concrete);

            b.Group("Rainwater cistern");
            var cistern = b.Pad("EL-CISTERN", "Trunk cistern", 0f, 5.6f, 1.4f, 1.4f, 0.5f, MaterialLibrary.Concrete,
                hint: "Collects water from the trunk downpipe.");

            b.Group("Brick plinth walls");
            void Plinth(string id, string name, float ax, float az, float bx, float bz, string f1, string f2) =>
                b.Wall(id, name, BuildStage.Foundation, Endpoint.Ground(ax, 0f, az), Endpoint.Ground(bx, 0f, bz), 0.8f, 0.4f,
                    MaterialLibrary.EarthBlock, On(f1, f2), hint: "Brick plinth wall tying two feet together.");
            Plinth("EL-PLINTH-F", "Front plinth wall", -x, front, x, front, footFL, footFR);
            Plinth("EL-PLINTH-B", "Back plinth wall", -x, back, x, back, footBL, footBR);
            Plinth("EL-PLINTH-L", "Left plinth wall", -x, back, -x, front, footBL, footFL);
            Plinth("EL-PLINTH-R", "Right plinth wall", x, back, x, front, footBR, footFR);

            b.Group("Concrete legs");
            string Leg(string id, string name, float lx, float lz, string foot) =>
                b.Strut(id, name, MemberKind.Column, BuildStage.PrimaryStructure, Endpoint.Ground(lx, 0.65f, lz),
                    Endpoint.At(lx, belly, lz), 0.75f, MaterialLibrary.Concrete, MaterialLibrary.Frame, On(foot),
                    hint: "Thick legs carry the whole body.");
            var legBL = Leg("EL-LEG-BL", "Back-left leg", -x, back, footBL);
            var legBR = Leg("EL-LEG-BR", "Back-right leg", x, back, footBR);
            var legFL = Leg("EL-LEG-FL", "Front-left leg", -x, front, footFL);
            var legFR = Leg("EL-LEG-FR", "Front-right leg", x, front, footFR);

            b.Group("Belly beams");
            string Belly(string id, string name, float ax, float az, float bx, float bz, string s1, string s2) =>
                b.Bar(id, name, MemberKind.Beam, BuildStage.PrimaryStructure, Endpoint.At(ax, belly, az), Endpoint.At(bx, belly, bz),
                    0.45f, MaterialLibrary.Concrete, MaterialLibrary.Frame, On(s1, s2), hint: "Carries the floor of the elephant's belly.");
            var bellyFront = Belly("EL-BELLY-F", "Front belly beam", -x, front, x, front, legFL, legFR);
            var bellyBack = Belly("EL-BELLY-B", "Back belly beam", -x, back, x, back, legBL, legBR);
            var bellyLeft = Belly("EL-BELLY-L", "Left belly beam", -x, back, -x, front, legBL, legFL);
            var bellyRight = Belly("EL-BELLY-R", "Right belly beam", x, back, x, front, legBR, legFR);

            b.Group("Belly floor");
            var floorBack = b.Slab("EL-FLOOR-B", "Rear belly floor", BuildStage.PrimaryStructure, 0f, belly + 0.25f, back * 0.5f, 6.0f, 2.5f,
                On(bellyBack, bellyLeft, bellyRight), hint: "Concrete floor of the home inside the elephant.");
            var floorFront = b.Slab("EL-FLOOR-F", "Front belly floor", BuildStage.PrimaryStructure, 0f, belly + 0.25f, front * 0.5f, 6.0f, 2.5f,
                On(bellyFront, bellyLeft, bellyRight));

            b.Group("Body arches");
            var bodyBack = b.Arch("EL-BODY-B", "Rear body arch", Endpoint.At(-x, belly, back), Endpoint.At(x, belly, back), 6.0f, 0.3f,
                MaterialLibrary.Concrete, On(bellyBack));
            var bodyMid = b.Arch("EL-BODY-M", "Middle body arch", Endpoint.At(-x, belly, 0f), Endpoint.At(x, belly, 0f), 6.2f, 0.3f,
                MaterialLibrary.Concrete, On(bellyLeft, bellyRight));
            var bodyFront = b.Arch("EL-BODY-F", "Front body arch", Endpoint.At(-x, belly, front), Endpoint.At(x, belly, front), 6.0f, 0.3f,
                MaterialLibrary.Concrete, On(bellyFront));

            b.Group("Ear spars");
            var spars = new Dictionary<int, string>();
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                spars[side] = b.Strut($"EL-EAR-{label}-SPAR", $"{(side < 0 ? "Left" : "Right")} ear spar", MemberKind.Cantilever,
                    BuildStage.PrimaryStructure, Endpoint.At(side * 2.4f, 5.0f, front), Endpoint.At(side * 6.2f, 5.2f, 2.1f), 0.15f,
                    MaterialLibrary.Steel, MaterialLibrary.Frame, On(bodyFront),
                    hint: "Holds the ear canopy out from the body. Wind tries to lift it.");
            }

            b.Group("Leg bracing");
            b.Strut("EL-LEGBRACE-L", "Left leg brace", MemberKind.Brace, BuildStage.Bracing, Endpoint.Ground(-x, 0.9f, back),
                Endpoint.At(-x, 2.9f, front), 0.12f, MaterialLibrary.Steel, MaterialLibrary.Bracing, On(legBL, legFL));
            b.Strut("EL-LEGBRACE-R", "Right leg brace", MemberKind.Brace, BuildStage.Bracing, Endpoint.Ground(x, 0.9f, back),
                Endpoint.At(x, 2.9f, front), 0.12f, MaterialLibrary.Steel, MaterialLibrary.Bracing, On(legBR, legFR));
            b.Group("Extra leg bracing");
            b.Strut("EL-LEGBRACE-F", "Front leg brace", MemberKind.Brace, BuildStage.Bracing, Endpoint.Ground(-x, 0.9f, front),
                Endpoint.At(x, 2.9f, front), 0.12f, MaterialLibrary.Steel, MaterialLibrary.Bracing, On(legFL, legFR), required: false);
            b.Strut("EL-LEGBRACE-B", "Back leg brace", MemberKind.Brace, BuildStage.Bracing, Endpoint.Ground(-x, 0.9f, back),
                Endpoint.At(x, 2.9f, back), 0.12f, MaterialLibrary.Steel, MaterialLibrary.Bracing, On(legBL, legBR), required: false);

            var body = b.Shell("Elephant body",
                new ShellStation(-3.3f, 3.1f, 5.5f, 2.3f, 2.2f),
                new ShellStation(-2.6f, 2.9f, 6.35f, 3.35f, 2.3f),
                new ShellStation(0.0f, 2.9f, 6.65f, 3.45f, 2.3f),
                new ShellStation(2.6f, 2.9f, 6.4f, 3.35f, 2.3f),
                new ShellStation(3.3f, 3.1f, 5.9f, 2.7f, 2.2f));
            var headShell = b.Shell("Elephant head",
                new ShellStation(3.0f, 3.6f, 6.25f, 2.25f, 2.2f),
                new ShellStation(4.0f, 3.55f, 6.35f, 2.05f, 2.2f),
                new ShellStation(4.8f, 3.65f, 6.0f, 1.65f, 2.2f),
                new ShellStation(5.3f, 4.0f, 5.5f, 1.05f, 2.0f));
            int Ear(int side) => b.Shell(side < 0 ? "Left ear" : "Right ear",
                new ShellStation(0.9f, 4.95f, 5.3f, 0.45f, 2f, side * 4.6f),
                new ShellStation(1.6f, 4.75f, 5.55f, 1.9f, 2f, side * 4.6f),
                new ShellStation(2.6f, 4.65f, 5.65f, 2.15f, 2f, side * 4.6f),
                new ShellStation(3.4f, 4.8f, 5.45f, 1.25f, 2f, side * 4.6f),
                new ShellStation(3.75f, 5.0f, 5.2f, 0.3f, 2f, side * 4.6f));
            var earLeft = Ear(-1);
            var earRight = Ear(1);

            b.Group("Body shell");
            b.Patch("EL-BACK-B", "Rear back shell", BuildStage.Roof, body, 0, 2, 0.26f, 0.74f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyBack, bodyMid), isRoof: true, hint: "Thin ferrocement dome: cement mortar on steel mesh.");
            b.Patch("EL-BACK-F", "Front back shell", BuildStage.Roof, body, 2, 4, 0.26f, 0.74f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyMid, bodyFront), isRoof: true);
            b.Patch("EL-SIDE-R", "Right body shell", BuildStage.Roof, body, 0, 4, 0f, 0.12f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bellyRight), isRoof: false);
            b.Patch("EL-SIDE-L", "Left body shell", BuildStage.Roof, body, 0, 4, 0.88f, 1f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bellyLeft), isRoof: false);
            b.Patch("EL-RUMP-R", "Right rump shell", BuildStage.Roof, body, 0, 1, 0.12f, 0.26f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyBack), isRoof: false);
            b.Patch("EL-RUMP-L", "Left rump shell", BuildStage.Roof, body, 0, 1, 0.74f, 0.88f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyBack), isRoof: false);
            b.Patch("EL-SHOULDER-R", "Right shoulder shell", BuildStage.Roof, body, 3, 4, 0.12f, 0.26f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyFront), isRoof: false);
            b.Patch("EL-SHOULDER-L", "Left shoulder shell", BuildStage.Roof, body, 3, 4, 0.74f, 0.88f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyFront), isRoof: false);

            b.Group("Head shell");
            b.Patch("EL-HEAD-TOP", "Head shell, crown", BuildStage.Roof, headShell, 0, 3, 0.25f, 0.75f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyFront), isRoof: true);
            b.Patch("EL-HEAD-R", "Right temple shell", BuildStage.Roof, headShell, 0, 3, 0f, 0.25f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyFront), isRoof: false);
            b.Patch("EL-HEAD-L", "Left temple shell", BuildStage.Roof, headShell, 0, 3, 0.75f, 1f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyFront), isRoof: false);

            b.Group("Ear canopies");
            b.Patch("EL-EAR-L", "Left ear canopy", BuildStage.Roof, earLeft, 0, 4, 0f, 1f, 0.03f, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(spars[-1]), isRoof: true, hint: "A big shade canopy. Catches rain and wind.");
            b.Patch("EL-EAR-R", "Right ear canopy", BuildStage.Roof, earRight, 0, 4, 0f, 1f, 0.03f, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(spars[1]), isRoof: true, hint: "A big shade canopy. Catches rain and wind.");

            b.Group("Ear tie-downs");
            b.Strut("EL-EARTIE-L", "Left ear tie-down", MemberKind.Brace, BuildStage.Envelope, Endpoint.At(-x, belly, front),
                Endpoint.At(-5.0f, 5.05f, 2.25f), 0.08f, MaterialLibrary.Steel, MaterialLibrary.Bracing, On(spars[-1], "EL-EAR-L", legFL), required: false,
                hint: "Ties the ear down so wind cannot rip it off.");
            b.Strut("EL-EARTIE-R", "Right ear tie-down", MemberKind.Brace, BuildStage.Envelope, Endpoint.At(x, belly, front),
                Endpoint.At(5.0f, 5.05f, 2.25f), 0.08f, MaterialLibrary.Steel, MaterialLibrary.Bracing, On(spars[1], "EL-EAR-R", legFR), required: false,
                hint: "Ties the ear down so wind cannot rip it off.");

            b.Group("Panoramic glazing");
            b.Patch("EL-GLASS-R", "Right panoramic window", BuildStage.Envelope, body, 1, 3, 0.12f, 0.26f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, On(bodyBack, bodyFront), isRoof: false, hint: "A band of glass round the elephant's flank.");
            b.Patch("EL-GLASS-L", "Left panoramic window", BuildStage.Envelope, body, 1, 3, 0.74f, 0.88f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, On(bodyBack, bodyFront), isRoof: false);

            b.Group("Rump and face walls");
            b.Patch("EL-REAR-WALL", "Rump wall", BuildStage.Envelope, body, 0, 0, 0f, 1f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyBack), isRoof: false);
            var face = b.Patch("EL-FACE", "Face wall", BuildStage.Envelope, headShell, 3, 3, 0f, 1f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(bodyFront), isRoof: false);

            b.Group("Trunk downpipe");
            b.Drain("EL-TRUNK", "Trunk downpipe", Endpoint.At(0f, 4.4f, 5.3f), Endpoint.Ground(0f, 0.5f, 5.6f), 0.25f, MaterialLibrary.Bamboo,
                On(face, cistern), hint: "Carries all roof water to the cistern. Too small and the roof floods.");

            b.Group("Ground-floor walls");
            b.Wall("EL-GROUND-WALL-B", "Back ground-floor wall", BuildStage.Envelope, Endpoint.Ground(-x, 0f, back), Endpoint.Ground(x, 0f, back),
                0f, 0.3f, MaterialLibrary.EarthBlock, On(legBL, legBR), topHeight: belly - 0.2f,
                hint: "A brick wall between the back legs. It encloses the ground floor and resists racking.");
            b.Wall("EL-GROUND-WALL-F", "Front ground-floor wall", BuildStage.Envelope, Endpoint.Ground(-x, 0f, front), Endpoint.Ground(x, 0f, front),
                0f, 0.3f, MaterialLibrary.EarthBlock, On(legFL, legFR), topHeight: belly - 0.2f,
                hint: "A brick wall between the front legs. It encloses the ground floor and resists racking.");

            b.Group("Interior walls");
            foreach (var side in new[] { -1, 1 })
            {
                b.CladWall($"EL-ROOM-{(side < 0 ? "L" : "R")}", $"Room wall {(side < 0 ? "a" : "b")}", BuildStage.Envelope,
                    Endpoint.At(side * 2.4f, belly + 0.33f, 0f), Endpoint.At(side * 0.5f, belly + 0.33f, 0f), 2.3f, 0.12f,
                    MaterialLibrary.EarthBlock, On(floorBack, floorFront), hint: "Half-brick partition between the living room and the bedroom.");
            }

            return new HouseBlueprint(ElephantId, 2, "Inzovu", "elephant",
                "An elephant on four thick concrete legs. A home sits on its belly floor under a ferrocement dome; its ears shade the sides and its trunk carries rainwater to a cistern.",
                "Cantilevered canopies must be tied down against wind uplift, and roofs need drains sized for heavy rain.",
                2, new FoundationStyle("Elephant feet", "Wide soft pads spread a huge weight", 1.4f, 0.2f, 1.0f), back, b.Members,
                b.Shells, Biomimetic);
        }

        /// <summary>
        /// Imodoka: a house that is a sports car, lifted on raked concrete stilts that step down the hill.
        /// Steel body hoops carry a ferrocement car body with real windows; inside are three rooms.
        /// </summary>
        public static HouseBlueprint Car()
        {
            var b = new BlueprintBuilder();
            float[] z = { -3.5f, 0f, 3.5f };
            float[] hoopCrowns = { 3.45f, 4.75f, 3.4f };
            string[] where = { "uphill", "middle", "downhill" };
            const float x = 1.8f;
            const float deck = 2.2f;
            const float rake = 1.15f;

            var stilts = new Dictionary<string, string>();
            var piers = new Dictionary<string, string>();
            b.Group("Brick piers");
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                var name = side < 0 ? "left" : "right";
                for (var i = 0; i < z.Length; i++)
                {
                    piers[$"{label}{i}"] = b.Pad($"CR-PAD-{label}{i}", $"{Capitalise(where[i])} {name} brick pier", side * x * rake, z[i], 1.0f, 1.0f, 0.8f,
                        MaterialLibrary.EarthBlock, hint: "A clay-brick pier: the stilt stands on brick, not on bare soil.");
                }
            }

            b.Group("Brick plinth walls");
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                for (var i = 0; i < z.Length - 1; i++)
                {
                    b.Wall($"CR-PLINTH-{label}{i}", $"{(side < 0 ? "Left" : "Right")} plinth wall {i + 1}", BuildStage.Foundation,
                        Endpoint.Ground(side * x * rake, 0f, z[i]), Endpoint.Ground(side * x * rake, 0f, z[i + 1]), 0.7f, 0.35f,
                        MaterialLibrary.EarthBlock, On(piers[$"{label}{i}"], piers[$"{label}{i + 1}"]),
                        hint: "Brick plinth wall stepping down the slope between two piers.");
                }
            }
            for (var i = 0; i < z.Length; i++)
            {
                b.Wall($"CR-PLINTH-X{i}", $"{Capitalise(where[i])} cross plinth wall", BuildStage.Foundation,
                    Endpoint.Ground(-x * rake, 0f, z[i]), Endpoint.Ground(x * rake, 0f, z[i]), 0.7f, 0.35f,
                    MaterialLibrary.EarthBlock, On(piers[$"L{i}"], piers[$"R{i}"]), hint: "Brick plinth wall across the slope.");
            }

            b.Group("Concrete stilts");
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                var name = side < 0 ? "left" : "right";
                for (var i = 0; i < z.Length; i++)
                {
                    stilts[$"{label}{i}"] = b.Strut($"CR-STILT-{label}{i}", $"{Capitalise(where[i])} {name} stilt", MemberKind.Column,
                        BuildStage.PrimaryStructure, Endpoint.Ground(side * x * rake, 0.55f, z[i]), Endpoint.At(side * x, deck, z[i]), 0.185f,
                        MaterialLibrary.Concrete, MaterialLibrary.Frame, On(piers[$"{label}{i}"]),
                        hint: i == 2 ? "The longest stilt on the slope. Slender stilts buckle." : "Lifts the car off the hillside.");
                }
            }

            b.Group("Chassis beams");
            var deckBeams = new Dictionary<string, string>();
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                for (var i = 0; i < 2; i++)
                {
                    deckBeams[$"{label}{i}"] = b.Bar($"CR-DECK-{label}{i}", $"{(side < 0 ? "Left" : "Right")} chassis beam {i + 1}",
                        MemberKind.Beam, BuildStage.PrimaryStructure, Endpoint.At(side * x, deck, z[i]), Endpoint.At(side * x, deck, z[i + 1]),
                        0.25f, MaterialLibrary.Concrete, MaterialLibrary.Frame, On(stilts[$"{label}{i}"], stilts[$"{label}{i + 1}"]));
                }
            }
            var crosses = new string[z.Length];
            for (var i = 0; i < z.Length; i++)
            {
                crosses[i] = b.Bar($"CR-CROSS-{i}", $"{Capitalise(where[i])} cross beam", MemberKind.Beam, BuildStage.PrimaryStructure,
                    Endpoint.At(-x, deck, z[i]), Endpoint.At(x, deck, z[i]), 0.25f, MaterialLibrary.Concrete, MaterialLibrary.Frame,
                    On(stilts[$"L{i}"], stilts[$"R{i}"]));
            }

            b.Group("Floor slabs");
            var floors = new string[2];
            for (var i = 0; i < 2; i++)
            {
                floors[i] = b.Slab($"CR-FLOOR-{i}", $"Floor slab {i + 1}", BuildStage.PrimaryStructure, 0f, deck + 0.2f, (z[i] + z[i + 1]) * 0.5f,
                    3.8f, 3.5f, On(deckBeams[$"L{i}"], deckBeams[$"R{i}"]), hint: "Concrete floor of the rooms inside the car.");
            }

            b.Group("Body hoops");
            var hoops = new string[z.Length];
            for (var i = 0; i < z.Length; i++)
            {
                hoops[i] = b.Arch($"CR-BODY-{i}", $"{Capitalise(where[i])} body hoop", Endpoint.At(-x, deck, z[i]), Endpoint.At(x, deck, z[i]),
                    hoopCrowns[i], 0.16f, MaterialLibrary.Steel, On(crosses[i]), hint: "Steel hoop that gives the car its rounded body.");
            }

            b.Group("Uphill bracing");
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                b.Strut($"CR-BRACE-{label}0", $"{(side < 0 ? "Left" : "Right")} uphill X-brace", MemberKind.Brace, BuildStage.Bracing,
                    Endpoint.Ground(side * x * rake, 0.75f, z[0]), Endpoint.At(side * x, deck - 0.1f, z[1]), 0.1f, MaterialLibrary.Steel,
                    MaterialLibrary.Bracing, On(stilts[$"{label}0"], stilts[$"{label}1"]));
            }
            b.Strut("CR-SWAY-0", "Uphill sway brace", MemberKind.Brace, BuildStage.Bracing,
                Endpoint.Ground(-x * rake, 0.75f, z[0]), Endpoint.At(x, deck - 0.1f, z[0]), 0.1f, MaterialLibrary.Steel,
                MaterialLibrary.Bracing, On(stilts["L0"], stilts["R0"]));

            b.Group("Downhill bracing");
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                b.Strut($"CR-BRACE-{label}1", $"{(side < 0 ? "Left" : "Right")} downhill X-brace", MemberKind.Brace, BuildStage.Bracing,
                    Endpoint.Ground(side * x * rake, 0.75f, z[1]), Endpoint.At(side * x, deck - 0.1f, z[2]), 0.1f, MaterialLibrary.Steel,
                    MaterialLibrary.Bracing, On(stilts[$"{label}1"], stilts[$"{label}2"]), required: false,
                    hint: "Braces the long downhill stilts against shaking.");
            }
            for (var i = 1; i < z.Length; i++)
            {
                b.Strut($"CR-SWAY-{i}", $"{Capitalise(where[i])} sway brace", MemberKind.Brace, BuildStage.Bracing,
                    Endpoint.Ground(-x * rake, 0.75f, z[i]), Endpoint.At(x, deck - 0.1f, z[i]), 0.1f, MaterialLibrary.Steel,
                    MaterialLibrary.Bracing, On(stilts[$"L{i}"], stilts[$"R{i}"]), required: false);
            }

            // The car body, from rear bumper to nose: boot, cabin under a raked roof, bonnet.
            var shell = b.Shell("Car body",
                new ShellStation(-4.7f, 1.75f, 3.05f, 1.85f, 3f),
                new ShellStation(-4.2f, 1.6f, 3.55f, 2.05f, 4f),
                new ShellStation(-2.6f, 1.6f, 3.75f, 2.15f, 4f),
                new ShellStation(-1.5f, 1.6f, 4.65f, 2.15f, 3.2f),
                new ShellStation(0.0f, 1.6f, 4.95f, 2.15f, 3.0f),
                new ShellStation(1.4f, 1.6f, 4.75f, 2.15f, 3.0f),
                new ShellStation(2.5f, 1.6f, 3.85f, 2.15f, 4f),
                new ShellStation(4.0f, 1.6f, 3.55f, 2.1f, 4f),
                new ShellStation(4.7f, 1.7f, 3.2f, 1.9f, 3f));
            var allHoops = On(hoops);

            b.Group("Car body shell");
            b.Patch("CR-SILL-R", "Right body side", BuildStage.Roof, shell, 0, 8, 0f, 0.2f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(deckBeams["R0"], deckBeams["R1"]), isRoof: false,
                hint: "Ferrocement car body: cement mortar on steel mesh, moulded like the panels of a car.");
            b.Patch("CR-SILL-L", "Left body side", BuildStage.Roof, shell, 0, 8, 0.8f, 1f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(deckBeams["L0"], deckBeams["L1"]), isRoof: false);
            b.Patch("CR-QUARTER-R", "Right rear quarter", BuildStage.Roof, shell, 0, 3, 0.2f, 0.36f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(hoops[0]), isRoof: false);
            b.Patch("CR-QUARTER-L", "Left rear quarter", BuildStage.Roof, shell, 0, 3, 0.64f, 0.8f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(hoops[0]), isRoof: false);
            b.Patch("CR-WING-R", "Right front wing", BuildStage.Roof, shell, 6, 8, 0.2f, 0.36f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(hoops[2]), isRoof: false);
            b.Patch("CR-WING-L", "Left front wing", BuildStage.Roof, shell, 6, 8, 0.64f, 0.8f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(hoops[2]), isRoof: false);
            b.Patch("CR-ROOF-REAR", "Boot lid", BuildStage.Roof, shell, 0, 2, 0.36f, 0.64f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(hoops[0]), isRoof: true);
            b.Patch("CR-ROOF", "Roof", BuildStage.Roof, shell, 3, 5, 0.36f, 0.64f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(hoops[1]), isRoof: true);
            b.Patch("CR-HOOD", "Bonnet", BuildStage.Roof, shell, 6, 8, 0.36f, 0.64f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(hoops[2]), isRoof: true);

            b.Group("Car glazing");
            b.Patch("CR-WINDOW-R", "Right side windows", BuildStage.Envelope, shell, 3, 6, 0.2f, 0.36f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, allHoops, isRoof: false, hint: "Side windows light the living room and bedroom.");
            b.Patch("CR-WINDOW-L", "Left side windows", BuildStage.Envelope, shell, 3, 6, 0.64f, 0.8f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, allHoops, isRoof: false);
            b.Patch("CR-REAR-GLASS", "Rear window", BuildStage.Envelope, shell, 2, 3, 0.36f, 0.64f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, On(hoops[0], hoops[1]), isRoof: false);
            b.Patch("CR-WINDSCREEN", "Windscreen", BuildStage.Envelope, shell, 5, 6, 0.36f, 0.64f, GlassThickness, MaterialLibrary.Glass,
                MaterialLibrary.Glazing, On(hoops[1], hoops[2]), isRoof: false, hint: "The windscreen is the kitchen's view down the valley.");

            b.Group("Nose and tail");
            b.Patch("CR-TAIL", "Tail end", BuildStage.Envelope, shell, 0, 0, 0f, 1f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(hoops[0]), isRoof: false);
            b.Patch("CR-NOSE", "Nose end", BuildStage.Envelope, shell, 8, 8, 0f, 1f, ShellThickness, MaterialLibrary.Ferrocement,
                MaterialLibrary.Shell, On(hoops[2]), isRoof: false);

            b.Group("Interior walls");
            for (var i = 0; i < 2; i++)
            {
                var wallZ = i == 0 ? -1.6f : 1.6f;
                foreach (var side in new[] { -1, 1 })
                {
                    b.CladWall($"CR-ROOM-{i + 1}{(side < 0 ? "L" : "R")}", $"Room wall {i + 1}{(side < 0 ? "a" : "b")}", BuildStage.Envelope,
                        Endpoint.At(side * 1.4f, deck + 0.3f, wallZ), Endpoint.At(side * 0.45f, deck + 0.3f, wallZ), 1.8f, 0.12f,
                        MaterialLibrary.EarthBlock, On(floors[i]), hint: "Half-brick partition between the bedroom, living room and kitchen.");
                }
            }

            return new HouseBlueprint(CarId, 2, "Imodoka", "car",
                "A house that is a sports car. Raked concrete stilts lift its ferrocement body off the hillside; behind the windscreen are a kitchen, a living room and a bedroom.",
                "On a slope the downhill stilts are longest and buckle first; bracing and stronger material fix them.",
                1, new FoundationStyle("Spider feet", "Raked spider legs grip the slope", 1.0f, 0.8f, 1.2f), z[0], b.Members,
                b.Shells, Biomimetic);
        }

        // ---- Rwandan classics ----------------------------------------------------------------

        /// <summary>Urugo: a single-storey family home with a tin roof, the most common house on Rwanda's hills.</summary>
        public static HouseBlueprint Bungalow()
        {
            var b = new BlueprintBuilder();
            var plan = new ClassicPlan
            {
                Prefix = "BG",
                Xs = new[] { -5f, 0f, 5f },
                Zs = new[] { -4f, 0f, 4f },
                Storeys = 1,
                StoreyHeight = 2.9f,
                RoofRise = 1.8f,
                RoofMaterial = MaterialLibrary.IronSheet,
                RoofThickness = 0.04f,
                RafterMaterial = MaterialLibrary.Timber,
                Openings = (storey, side, edge, length) =>
                {
                    switch (side)
                    {
                        case Side.Downhill:
                            return edge == 0
                                ? new[] { Opening.Window(2.5f, 1.8f) }
                                : new[] { Opening.DoorAt(1.4f, 1.0f), Opening.Window(3.4f, 1.4f) };
                        case Side.Uphill:
                            return new[] { Opening.Window(2.5f, 1.0f, 1.2f) };
                        default:
                            return new[] { Opening.Window(2.0f, 1.2f) };
                    }
                },
                Partitions = new[]
                {
                    (0, new Vector2(-5f, 0f), new Vector2(-3.0f, 0f)),
                    (0, new Vector2(-2.1f, 0f), new Vector2(2.1f, 0f)),
                    (0, new Vector2(3.0f, 0f), new Vector2(5f, 0f)),
                    (0, new Vector2(0f, -4f), new Vector2(0f, 0f))
                }
            };
            var frame = BuildClassic(b, plan);

            // Iron sheets weigh almost nothing: nails alone let a storm peel them off.
            b.Group("Storm straps");
            for (var k = 0; k < frame.RoofSheets.Count; k++)
            {
                var (sheet, x, z) = frame.RoofSheets[k];
                b.Strut($"BG-STRAP-{k + 1}", $"Storm strap {k + 1}", MemberKind.Brace, BuildStage.Envelope,
                    Endpoint.At(x, frame.Plate, z), Endpoint.At(x * 0.55f, frame.Plate + plan.RoofRise * 0.45f + 0.1f, z), 0.05f,
                    MaterialLibrary.Steel, MaterialLibrary.Bracing, On(sheet, frame.RingBeamNear(x, z)), required: false,
                    hint: "A steel strap tying the roof sheets down to the ring beam, so wind cannot lift them.");
            }

            return new HouseBlueprint(BungalowId, 1, "Urugo", "home",
                "A single-storey family home: a concrete frame with fired-brick walls on a stepped brick foundation, and an iron-sheet roof over two bedrooms and a living room.",
                "Light iron-sheet roofs are lifted off by strong wind unless storm straps tie them down to the walls.",
                1, new FoundationStyle("Pad footings", "Reinforced-concrete pads under every column", 1.2f, 0.6f, 1.1f), plan.Zs[0], b.Members,
                b.Shells, Classic);
        }

        /// <summary>Igorofa: a two-storey house with a clay-tile roof and a balcony over the valley.</summary>
        public static HouseBlueprint TwoStorey()
        {
            var b = new BlueprintBuilder();
            var plan = new ClassicPlan
            {
                Prefix = "TS",
                Xs = new[] { -4.5f, 0f, 4.5f },
                Zs = new[] { -3.5f, 0f, 3.5f },
                Storeys = 2,
                StoreyHeight = 3.0f,
                RoofRise = 2.0f,
                RoofMaterial = MaterialLibrary.ClayTile,
                RoofThickness = 0.06f,
                RafterMaterial = MaterialLibrary.Timber,
                Voids = new[] { (1, 0, 0) },
                Openings = (storey, side, edge, length) =>
                {
                    switch (side)
                    {
                        case Side.Downhill when storey == 1:
                            return new[] { Opening.DoorAt(edge == 0 ? 2.5f : 2.0f, 1.2f, 2.2f) };
                        case Side.Downhill:
                            return edge == 0
                                ? new[] { Opening.Window(2.25f, 2.0f, 0.6f, 2.2f) }
                                : new[] { Opening.DoorAt(1.2f, 1.0f), Opening.Window(3.2f, 1.4f) };
                        case Side.Uphill:
                            return new[] { Opening.Window(2.25f, 1.0f, 1.2f) };
                        default:
                            return new[] { Opening.Window(1.75f, 1.2f) };
                    }
                },
                Partitions = new[]
                {
                    (0, new Vector2(0f, 0f), new Vector2(3.2f, 0f)),
                    (1, new Vector2(0f, -3.5f), new Vector2(0f, -0.9f)),
                    (1, new Vector2(0f, 0f), new Vector2(0f, 3.5f)),
                    (1, new Vector2(0.9f, 0f), new Vector2(4.5f, 0f))
                }
            };
            var frame = BuildClassic(b, plan);

            b.Group("Staircase");
            var rise = plan.StoreyHeight;
            const float run = 3.9f;
            var angle = (float)(Math.Atan2(rise, run) * 180.0 / Math.PI);
            b.Panel("TS-STAIR-1", "Stair flight", BuildStage.PrimaryStructure,
                new Vector3(-2.25f, plan.Level(0) + rise * 0.5f, -2.8f), new Vector3(1.0f, 0.18f, (float)Math.Sqrt(rise * rise + run * run)),
                new Vector3(angle, -90f, 0f), MaterialLibrary.Concrete, On(frame.Slabs[0][0, 0], frame.BeamZ(0, 1, 0)), isRoof: false,
                allowed: MaterialLibrary.Slab, hint: "A concrete stair flight up to the bedrooms.");

            // The balcony juts out over the downhill side: two concrete cantilevers carry its slab.
            var level = plan.Level(1);
            const float reach = 2.6f;
            var frontZ = plan.Zs[2];
            b.Group("Balcony prop footings");
            var propPads = new string[2];
            for (var k = 0; k < 2; k++)
                propPads[k] = b.Pad($"TS-BALPAD-{k + 1}", $"Balcony prop footing {k + 1}", k == 0 ? -2f : 2f, frontZ + reach - 0.3f, 0.9f, 0.9f, 0.5f,
                    MaterialLibrary.Concrete, required: false, hint: "Optional footing for a column under the balcony.");
            b.Group("Balcony props");
            var props = new string[2];
            for (var k = 0; k < 2; k++)
                props[k] = b.Bar($"TS-BALPROP-{k + 1}", $"Balcony prop {k + 1}", MemberKind.Brace, BuildStage.PrimaryStructure,
                    Endpoint.Ground(k == 0 ? -2f : 2f, 0f, frontZ + reach - 0.3f), Endpoint.At(k == 0 ? -2f : 2f, level - 0.3f, frontZ + reach - 0.3f),
                    0.22f, MaterialLibrary.Concrete, MaterialLibrary.Frame, On(propPads[k]), required: false,
                    hint: "A column under the balcony turns its cantilevers into simple beams.");
            b.Group("Balcony cantilevers");
            var cantilevers = new string[2];
            for (var k = 0; k < 2; k++)
            {
                var cx = k == 0 ? -2f : 2f;
                cantilevers[k] = b.Bar($"TS-BAL-{(k == 0 ? "L" : "R")}", $"{(k == 0 ? "Left" : "Right")} balcony cantilever", MemberKind.Cantilever,
                    BuildStage.PrimaryStructure, Endpoint.At(cx, level - 0.15f, frontZ), Endpoint.At(cx, level - 0.15f, frontZ + reach), 0.16f,
                    MaterialLibrary.Concrete, MaterialLibrary.Frame, On(frame.BeamX(0, k, 2)), extraSupports: On(props[k]),
                    hint: "Reaches 2.6 m out from the house. A crowd on the balcony bends it hard.");
            }
            b.Group("Balcony slab");
            var balcony = b.Slab("TS-BAL-SLAB", "Balcony slab", BuildStage.PrimaryStructure, 0f, level - 0.075f, frontZ + reach * 0.5f, 5.0f, reach,
                On(cantilevers), hint: "People, chairs and plants stand here.");
            b.Group("Balcony balustrade");
            b.Panel("TS-BAL-RAIL", "Glass balustrade", BuildStage.Envelope, new Vector3(0f, level + 0.5f, frontZ + reach), new Vector3(5.0f, 0.04f, 1.0f),
                new Vector3(90f, 0f, 0f), MaterialLibrary.Glass, On(balcony), isRoof: false, allowed: MaterialLibrary.Glazing);

            return new HouseBlueprint(TwoStoreyId, 1, "Igorofa", "storey house",
                "A two-storey family house: a reinforced-concrete frame with fired-brick walls, a concrete stair, bedrooms upstairs, a clay-tile roof and a balcony over the valley.",
                "A balcony is a cantilever: a crowd on it bends it hard, so it needs depth, strength or columns under it.",
                2, new FoundationStyle("Pad footings", "Reinforced-concrete pads under every column", 1.2f, 0.6f, 1.1f), plan.Zs[0], b.Members,
                b.Shells, Classic);
        }

        /// <summary>Amacumbi: a three-storey block of flats over open ground-floor parking.</summary>
        public static HouseBlueprint Flats()
        {
            var b = new BlueprintBuilder();
            var plan = new ClassicPlan
            {
                Prefix = "AP",
                Xs = new[] { -6f, -2f, 2f, 6f },
                Zs = new[] { -4f, 0f, 4f },
                Storeys = 3,
                StoreyHeight = 3.0f,
                ColumnSize = 0.4f,
                OpenGroundFloor = true,
                RoofRise = 1.6f,
                RoofMaterial = MaterialLibrary.ClayTile,
                RoofThickness = 0.06f,
                RafterMaterial = MaterialLibrary.Steel,
                Voids = new[] { (1, 0, 0), (2, 0, 0) },
                Openings = (storey, side, edge, length) =>
                {
                    switch (side)
                    {
                        case Side.Downhill:
                            return new[] { Opening.Window(2.0f, 2.0f, 0.6f, 2.2f) };
                        case Side.Uphill when edge == 1:
                            return new[] { Opening.DoorAt(1.0f, 1.0f), Opening.Window(2.9f, 1.0f, 1.2f) };
                        case Side.Uphill:
                            return new[] { Opening.Window(2.0f, 1.2f) };
                        default:
                            return new[] { Opening.Window(2.0f, 1.4f) };
                    }
                },
                Partitions = new[]
                {
                    (1, new Vector2(0f, -4f), new Vector2(0f, 4f)),
                    (1, new Vector2(-6f, 1.0f), new Vector2(-3.2f, 1.0f)),
                    (1, new Vector2(3.2f, 1.0f), new Vector2(6f, 1.0f)),
                    (2, new Vector2(0f, -4f), new Vector2(0f, 4f)),
                    (2, new Vector2(-6f, 1.0f), new Vector2(-3.2f, 1.0f)),
                    (2, new Vector2(3.2f, 1.0f), new Vector2(6f, 1.0f))
                }
            };
            var frame = BuildClassic(b, plan);

            b.Group("Staircase");
            var rise = plan.StoreyHeight;
            const float run = 3.5f;
            var angle = (float)(Math.Atan2(rise, run) * 180.0 / Math.PI);
            var length = (float)Math.Sqrt(rise * rise + run * run);
            b.Panel("AP-STAIR-1", "Stair flight 1", BuildStage.PrimaryStructure,
                new Vector3(-4f, plan.Level(0) + rise * 0.5f, -3.3f), new Vector3(1.0f, 0.18f, length), new Vector3(angle, -90f, 0f),
                MaterialLibrary.Concrete, On(frame.Slabs[0][0, 0], frame.BeamZ(0, 1, 0)), isRoof: false, allowed: MaterialLibrary.Slab,
                hint: "Concrete stair flights up to the flats.");
            b.Panel("AP-STAIR-2", "Stair flight 2", BuildStage.PrimaryStructure,
                new Vector3(-4f, plan.Level(1) + rise * 0.5f, -1.7f), new Vector3(1.0f, 0.18f, length), new Vector3(angle, 90f, 0f),
                MaterialLibrary.Concrete, On(frame.BeamZ(0, 1, 0), frame.BeamZ(1, 0, 0)), isRoof: false, allowed: MaterialLibrary.Slab);

            // The open parking floor is a soft storey: only its columns resist an earthquake.
            // Shear walls go on the sides and the back, leaving the downhill front open for cars.
            b.Group("Ground-floor shear walls");
            var shear = 0;
            var lines = new List<(int i0, int j0, int i1, int j1)>();
            for (var j = 0; j < plan.Zs.Length - 1; j++)
            {
                lines.Add((0, j, 0, j + 1));
                lines.Add((plan.Xs.Length - 1, j, plan.Xs.Length - 1, j + 1));
            }
            for (var i = 0; i < plan.Xs.Length - 1; i++) lines.Add((i, 0, i + 1, 0));
            foreach (var (i0, j0, i1, j1) in lines)
            {
                shear++;
                b.Wall($"AP-SHEAR-{shear}", $"Ground-floor shear wall {shear}", BuildStage.Bracing,
                    Endpoint.At(plan.Xs[i0], plan.Level(0), plan.Zs[j0]), Endpoint.At(plan.Xs[i1], plan.Level(0), plan.Zs[j1]),
                    plan.StoreyHeight - 0.3f, 0.4f, MaterialLibrary.EarthBlock, On(frame.Columns[0][i0, j0], frame.Columns[0][i1, j1]),
                    required: false, hint: "A thick brick shear wall stiffens the open parking floor so it cannot sway and collapse.");
            }

            return new HouseBlueprint(FlatsId, 1, "Amacumbi", "flats",
                "A three-storey block of four flats: a reinforced-concrete frame with fired-brick walls, a concrete stair and open parking on the ground floor.",
                "An open ground floor is a soft storey: in an earthquake its columns take all the sway. Shear walls stiffen it.",
                3, new FoundationStyle("Pad footings", "Reinforced-concrete pads under every column", 1.3f, 0.6f, 1.1f), plan.Zs[0], b.Members,
                b.Shells, Classic);
        }

        // ---- Classic frame generator -------------------------------------------------------------

        private enum Side
        {
            Uphill,
            Downhill,
            Left,
            Right
        }

        /// <summary>The layout of a reinforced-concrete frame house on a column grid.</summary>
        private sealed class ClassicPlan
        {
            public string Prefix;
            public float[] Xs;
            public float[] Zs;
            public int Storeys = 1;
            public float StoreyHeight = 3f;
            public float Plinth = 0.45f;
            public float ColumnSize = 0.3f;
            public bool OpenGroundFloor;
            public float RoofRise = 1.8f;
            public float Overhang = 0.6f;
            public string RoofMaterial = MaterialLibrary.IronSheet;
            public float RoofThickness = 0.04f;
            public string RafterMaterial = MaterialLibrary.Timber;
            public Func<int, Side, int, float, Opening[]> Openings;
            public (int storey, Vector2 a, Vector2 b)[] Partitions = Array.Empty<(int, Vector2, Vector2)>();

            /// <summary>Bays (storey, i, j) left without a slab, such as a stairwell.</summary>
            public (int storey, int i, int j)[] Voids = Array.Empty<(int, int, int)>();

            /// <summary>Finished floor level of a storey (absolute height).</summary>
            public float Level(int storey) => Plinth + storey * StoreyHeight;
        }

        /// <summary>The ids the generator created, for the design-specific parts built on top.</summary>
        private sealed class ClassicFrame
        {
            public string[,] Footings;
            public string[][,] Columns;
            public string[][,] BeamsX;
            public string[][,] BeamsZ;
            public string[][,] Slabs;
            public float Plate;
            public float[] Xs;
            public float[] Zs;
            public readonly List<(string id, float x, float z)> RoofSheets = new List<(string, float, float)>();

            public string BeamX(int storey, int i, int j) => BeamsX[storey][i, j];
            public string BeamZ(int storey, int i, int j) => BeamsZ[storey][i, j];

            /// <summary>The top-storey ring beam nearest a point on the roof plan.</summary>
            public string RingBeamNear(float x, float z)
            {
                var top = BeamsX.Length - 1;
                var i = x < (Xs[0] + Xs[Xs.Length - 1]) * 0.5f ? 0 : Xs.Length - 1;
                var j = 0;
                for (var k = 1; k < Zs.Length - 1; k++)
                    if (Math.Abs(Zs[k] - z) < Math.Abs(Zs[j] - z)) j = k;
                return BeamsZ[top][i, Math.Min(j, Zs.Length - 2)];
            }
        }

        private static readonly string[] FloorNames = { "Ground-floor", "First-floor", "Second-floor", "Third-floor" };

        /// <summary>
        /// A Rwandan reinforced-concrete frame house on a hillside: concrete pad footings, a stepped
        /// brick foundation wall up to a level floor, columns and beams storey by storey, brick infill
        /// walls with real window and door openings, a gable roof on rafters, gutters, and partitions.
        /// </summary>
        private static ClassicFrame BuildClassic(BlueprintBuilder b, ClassicPlan plan)
        {
            var p = plan.Prefix;
            var nx = plan.Xs.Length;
            var nz = plan.Zs.Length;
            var frame = new ClassicFrame
            {
                Footings = new string[nx, nz],
                Columns = new string[plan.Storeys][,],
                BeamsX = new string[plan.Storeys][,],
                BeamsZ = new string[plan.Storeys][,],
                Slabs = new string[plan.Storeys][,],
                Plate = plan.Level(plan.Storeys),
                Xs = plan.Xs,
                Zs = plan.Zs
            };
            string Grid(int i, int j) => $"{(char)('A' + i)}{j + 1}";
            bool IsVoid(int storey, int i, int j)
            {
                foreach (var v in plan.Voids)
                    if (v.storey == storey && v.i == i && v.j == j) return true;
                return false;
            }

            b.Group("Concrete footings");
            for (var i = 0; i < nx; i++)
            for (var j = 0; j < nz; j++)
            {
                frame.Footings[i, j] = b.Pad($"{p}-FTG-{Grid(i, j)}", $"Footing {Grid(i, j)}", plan.Xs[i], plan.Zs[j], 1.3f, 1.3f, 0.5f,
                    MaterialLibrary.Concrete, hint: "A reinforced-concrete pad that spreads a column's load into the hillside.");
            }

            b.Group("Brick foundation walls");
            var edges = PerimeterEdges(nx, nz);
            foreach (var (side, i0, j0, i1, j1, k) in edges)
            {
                b.Wall($"{p}-FW-{side.ToString()[0]}{k + 1}", $"{side} foundation wall {k + 1}", BuildStage.Foundation,
                    Endpoint.Ground(plan.Xs[i0], 0f, plan.Zs[j0]), Endpoint.Ground(plan.Xs[i1], 0f, plan.Zs[j1]), 0f, 0.3f,
                    MaterialLibrary.EarthBlock, On(frame.Footings[i0, j0], frame.Footings[i1, j1]), topHeight: plan.Plinth,
                    hint: "Brick foundation wall stepping down the slope to carry a level floor. It also ties the footings together.");
            }

            for (var s = 0; s < plan.Storeys; s++)
            {
                frame.Slabs[s] ??= new string[nx - 1, nz - 1];
                if (s == 0)
                {
                    b.Group("Ground-floor slab");
                    for (var i = 0; i < nx - 1; i++)
                    for (var j = 0; j < nz - 1; j++)
                    {
                        frame.Slabs[0][i, j] = b.Slab($"{p}-SLAB0-{Grid(i, j)}", $"Ground-floor slab {Grid(i, j)}", BuildStage.Foundation,
                            (plan.Xs[i] + plan.Xs[i + 1]) * 0.5f, plan.Level(0) - 0.08f, (plan.Zs[j] + plan.Zs[j + 1]) * 0.5f,
                            plan.Xs[i + 1] - plan.Xs[i], plan.Zs[j + 1] - plan.Zs[j],
                            On(frame.Footings[i, j], frame.Footings[i + 1, j], frame.Footings[i, j + 1], frame.Footings[i + 1, j + 1]),
                            hint: "Concrete ground-floor slab on the brick foundation walls.");
                    }
                }

                var top = s == plan.Storeys - 1;
                b.Group($"{FloorNames[s]} columns");
                frame.Columns[s] = new string[nx, nz];
                for (var i = 0; i < nx; i++)
                for (var j = 0; j < nz; j++)
                {
                    var x = plan.Xs[i];
                    var z = plan.Zs[j];
                    frame.Columns[s][i, j] = b.Bar($"{p}-COL{s}-{Grid(i, j)}", $"{FloorNames[s]} column {Grid(i, j)}", MemberKind.Column,
                        BuildStage.PrimaryStructure, Endpoint.At(x, s == 0 ? plan.Level(0) - 0.15f : plan.Level(s), z),
                        Endpoint.At(x, plan.Level(s + 1), z), plan.ColumnSize, MaterialLibrary.Concrete, MaterialLibrary.Frame,
                        On(s == 0 ? frame.Footings[i, j] : frame.Columns[s - 1][i, j]),
                        hint: "Reinforced-concrete column, cast storey by storey.");
                }

                b.Group(top ? "Roof ring beam" : $"{FloorNames[s + 1]} beams");
                frame.BeamsX[s] = new string[nx - 1, nz];
                frame.BeamsZ[s] = new string[nx, nz - 1];
                var beamY = plan.Level(s + 1) - 0.15f;
                for (var j = 0; j < nz; j++)
                for (var i = 0; i < nx - 1; i++)
                {
                    frame.BeamsX[s][i, j] = b.Bar($"{p}-BM{s}-X{Grid(i, j)}", $"{(top ? "Ring beam" : $"{FloorNames[s + 1]} beam")} {Grid(i, j)}-{Grid(i + 1, j)}",
                        MemberKind.Beam, BuildStage.PrimaryStructure, Endpoint.At(plan.Xs[i], beamY, plan.Zs[j]), Endpoint.At(plan.Xs[i + 1], beamY, plan.Zs[j]),
                        0.3f, MaterialLibrary.Concrete, MaterialLibrary.Frame, On(frame.Columns[s][i, j], frame.Columns[s][i + 1, j]));
                }
                for (var i = 0; i < nx; i++)
                for (var j = 0; j < nz - 1; j++)
                {
                    frame.BeamsZ[s][i, j] = b.Bar($"{p}-BM{s}-Z{Grid(i, j)}", $"{(top ? "Ring beam" : $"{FloorNames[s + 1]} beam")} {Grid(i, j)}-{Grid(i, j + 1)}",
                        MemberKind.Beam, BuildStage.PrimaryStructure, Endpoint.At(plan.Xs[i], beamY, plan.Zs[j]), Endpoint.At(plan.Xs[i], beamY, plan.Zs[j + 1]),
                        0.3f, MaterialLibrary.Concrete, MaterialLibrary.Frame, On(frame.Columns[s][i, j], frame.Columns[s][i, j + 1]));
                }

                if (top) continue;
                b.Group($"{FloorNames[s + 1]} slab");
                frame.Slabs[s + 1] = new string[nx - 1, nz - 1];
                for (var i = 0; i < nx - 1; i++)
                for (var j = 0; j < nz - 1; j++)
                {
                    if (IsVoid(s + 1, i, j)) continue;
                    frame.Slabs[s + 1][i, j] = b.Slab($"{p}-SLAB{s + 1}-{Grid(i, j)}", $"{FloorNames[s + 1]} slab {Grid(i, j)}", BuildStage.PrimaryStructure,
                        (plan.Xs[i] + plan.Xs[i + 1]) * 0.5f, plan.Level(s + 1) - 0.075f, (plan.Zs[j] + plan.Zs[j + 1]) * 0.5f,
                        plan.Xs[i + 1] - plan.Xs[i], plan.Zs[j + 1] - plan.Zs[j],
                        On(frame.BeamsX[s][i, j], frame.BeamsX[s][i, j + 1], frame.BeamsZ[s][i, j], frame.BeamsZ[s][i + 1, j]),
                        hint: "Reinforced-concrete floor slab spanning between four beams.");
                }
            }

            // Brick infill walls, storey by storey, with their window and door openings.
            var openings = new List<(OpeningFrame frame, string[] rest)>();
            for (var s = 0; s < plan.Storeys; s++)
            {
                if (s == 0 && plan.OpenGroundFloor) continue;
                b.Group($"{FloorNames[s]} walls");
                foreach (var (side, i0, j0, i1, j1, k) in edges)
                {
                    var a = new Vector2(plan.Xs[i0], plan.Zs[j0]);
                    var c = new Vector2(plan.Xs[i1], plan.Zs[j1]);
                    var wall = b.WallWithOpenings($"{p}-W{s}{side.ToString()[0]}{k + 1}", $"{FloorNames[s]} {side.ToString().ToLowerInvariant()} wall {k + 1}",
                        BuildStage.Bracing, a, c, plan.Level(s), plan.Level(s + 1) - 0.3f, 0.2f,
                        On(frame.Columns[s][i0, j0], frame.Columns[s][i1, j1]),
                        plan.Openings?.Invoke(s, side, k, Vector2.Distance(a, c)) ?? Array.Empty<Opening>(),
                        hint: "Fired-brick infill wall between two columns. It stiffens the frame against sway.");
                    foreach (var opening in wall.Openings) openings.Add((opening, wall.Rest));
                }
            }

            // Roof: timber or steel rafters on the ring beam, a gable roof with eaves, brick gables.
            var xc = (plan.Xs[0] + plan.Xs[nx - 1]) * 0.5f;
            var halfWidth = (plan.Xs[nx - 1] - plan.Xs[0]) * 0.5f;
            var ridge = frame.Plate + plan.RoofRise;
            var topStorey = plan.Storeys - 1;
            b.Group("Roof trusses");
            var raftersLeft = new string[nz];
            var raftersRight = new string[nz];
            for (var j = 0; j < nz; j++)
            {
                raftersLeft[j] = b.Bar($"{p}-RAFTER-L{j + 1}", $"Left rafter {j + 1}", MemberKind.Beam, BuildStage.Roof,
                    Endpoint.At(plan.Xs[0], frame.Plate, plan.Zs[j]), Endpoint.At(xc, ridge, plan.Zs[j]), 0.2f, plan.RafterMaterial,
                    MaterialLibrary.Frame, On(frame.BeamsX[topStorey][0, j]), hint: "Roof rafter carrying the roof covering down to the ring beam.");
                raftersRight[j] = b.Bar($"{p}-RAFTER-R{j + 1}", $"Right rafter {j + 1}", MemberKind.Beam, BuildStage.Roof,
                    Endpoint.At(plan.Xs[nx - 1], frame.Plate, plan.Zs[j]), Endpoint.At(xc, ridge, plan.Zs[j]), 0.2f, plan.RafterMaterial,
                    MaterialLibrary.Frame, On(frame.BeamsX[topStorey][nx - 2, j]));
            }

            const float lift = 0.12f;
            var eave = frame.Plate - plan.Overhang * plan.RoofRise / halfWidth;
            var zMid = (plan.Zs[0] + plan.Zs[nz - 1]) * 0.5f;
            var roof = b.Shell("Roof",
                new ShellStation(plan.Zs[0] - plan.Overhang, eave + lift, ridge + lift, halfWidth + plan.Overhang, 1f, xc),
                new ShellStation(zMid, eave + lift, ridge + lift, halfWidth + plan.Overhang, 1f, xc),
                new ShellStation(plan.Zs[nz - 1] + plan.Overhang, eave + lift, ridge + lift, halfWidth + plan.Overhang, 1f, xc));
            var gables = b.Shell("Gables",
                new ShellStation(plan.Zs[0], frame.Plate, ridge, halfWidth, 1f, xc),
                new ShellStation(plan.Zs[nz - 1], frame.Plate, ridge, halfWidth, 1f, xc));

            var covering = plan.RoofMaterial == MaterialLibrary.ClayTile ? "Roof tiles" : "Roof sheeting";
            b.Group(covering);
            var halves = new[] { ("uphill", 0, 1), ("downhill", 1, 2) };
            foreach (var (half, from, to) in halves)
            {
                foreach (var right in new[] { true, false })
                {
                    var id = b.Patch($"{p}-ROOF-{(right ? "R" : "L")}{(from == 0 ? "U" : "D")}", $"{(right ? "Right" : "Left")} {half} roof",
                        BuildStage.Roof, roof, from, to, right ? 0f : 0.5f, right ? 0.5f : 1f, plan.RoofThickness, plan.RoofMaterial,
                        MaterialLibrary.Roofing, RaftersBetween(right ? raftersRight : raftersLeft, plan.Zs, from == 0 ? plan.Zs[0] : zMid, from == 0 ? zMid : plan.Zs[nz - 1]), isRoof: true,
                        hint: plan.RoofMaterial == MaterialLibrary.IronSheet ? "Corrugated iron sheets: light and cheap, but wind can lift them." :
                            "Clay tiles: heavy enough to hold themselves down in a storm.");
                    var sx = xc + (right ? 1f : -1f) * halfWidth * 0.5f;
                    var sz = from == 0 ? (plan.Zs[0] + zMid) * 0.5f : (zMid + plan.Zs[nz - 1]) * 0.5f;
                    frame.RoofSheets.Add((id, sx, sz));
                }
            }

            b.Group("Brick gables");
            b.Patch($"{p}-GABLE-U", "Uphill gable", BuildStage.Roof, gables, 0, 0, 0f, 1f, 0.2f, MaterialLibrary.EarthBlock, MaterialLibrary.Masonry,
                On(raftersLeft[0], raftersRight[0]), isRoof: false);
            b.Patch($"{p}-GABLE-D", "Downhill gable", BuildStage.Roof, gables, 1, 1, 0f, 1f, 0.2f, MaterialLibrary.EarthBlock, MaterialLibrary.Masonry,
                On(raftersLeft[nz - 1], raftersRight[nz - 1]), isRoof: false);

            b.Group("Windows");
            foreach (var (opening, rest) in openings)
                if (!opening.IsDoor) b.Fit(opening, rest);
            b.Group("Doors");
            foreach (var (opening, rest) in openings)
                if (opening.IsDoor) b.Fit(opening, rest);

            b.Group("Gutters and downpipes");
            foreach (var right in new[] { false, true })
            {
                var gx = xc + (right ? 1f : -1f) * (halfWidth + plan.Overhang * 0.8f);
                var gz = plan.Zs[nz - 1] + plan.Overhang * 0.6f;
                b.Drain($"{p}-DOWNPIPE-{(right ? "R" : "L")}", $"{(right ? "Right" : "Left")} downpipe", Endpoint.At(gx, eave + 0.1f, gz),
                    Endpoint.Ground(gx, 0.2f, gz), 0.15f, MaterialLibrary.Steel, On($"{p}-ROOF-{(right ? "R" : "L")}D"),
                    hint: "Gutter and downpipe: carries roof water away so it never ponds.");
            }

            if (plan.Partitions.Length > 0)
            {
                b.Group("Internal walls");
                var count = 0;
                foreach (var (storey, a, c) in plan.Partitions)
                {
                    count++;
                    var mid = (a + c) * 0.5f;
                    var slab = SlabUnder(frame, plan, storey, mid);
                    b.CladWall($"{p}-PART-{count}", $"{FloorNames[storey]} partition {count}", BuildStage.Envelope,
                        Endpoint.At(a.X, plan.Level(storey), a.Y), Endpoint.At(c.X, plan.Level(storey), c.Y), plan.StoreyHeight - 0.3f, 0.12f,
                        MaterialLibrary.EarthBlock, On(slab), hint: "Half-brick partition dividing the rooms.");
                }
            }

            return frame;
        }

        /// <summary>Perimeter edges of the column grid: side, the two grid points, and the edge's index along that side.</summary>
        private static List<(Side side, int i0, int j0, int i1, int j1, int k)> PerimeterEdges(int nx, int nz)
        {
            var edges = new List<(Side, int, int, int, int, int)>();
            for (var i = 0; i < nx - 1; i++) edges.Add((Side.Uphill, i, 0, i + 1, 0, i));
            for (var i = 0; i < nx - 1; i++) edges.Add((Side.Downhill, i, nz - 1, i + 1, nz - 1, i));
            for (var j = 0; j < nz - 1; j++) edges.Add((Side.Left, 0, j, 0, j + 1, j));
            for (var j = 0; j < nz - 1; j++) edges.Add((Side.Right, nx - 1, j, nx - 1, j + 1, j));
            return edges;
        }

        /// <summary>Rafters standing on grid lines between two z values (inclusive): what one half of the roof rests on.</summary>
        private static string[] RaftersBetween(string[] rafters, float[] zs, float from, float to)
        {
            var result = new List<string>();
            for (var j = 0; j < zs.Length; j++)
                if (zs[j] >= from - 0.01f && zs[j] <= to + 0.01f) result.Add(rafters[j]);
            return result.ToArray();
        }

        /// <summary>The slab a partition stands on: the bay under its midpoint, or the nearest bay that has a slab.</summary>
        private static string SlabUnder(ClassicFrame frame, ClassicPlan plan, int storey, Vector2 point)
        {
            string best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < plan.Xs.Length - 1; i++)
            for (var j = 0; j < plan.Zs.Length - 1; j++)
            {
                var id = frame.Slabs[storey][i, j];
                if (id == null) continue;
                var centre = new Vector2((plan.Xs[i] + plan.Xs[i + 1]) * 0.5f, (plan.Zs[j] + plan.Zs[j + 1]) * 0.5f);
                var distance = Vector2.Distance(centre, point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = id;
                }
            }
            return best;
        }

        private static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
