using System.Collections.Generic;
using System.Numerics;
using static Fatiro.Domain.BlueprintBuilder;

namespace Fatiro.Domain
{
    /// <summary>
    /// Authoring definitions of the three MVP designs. The editor tool converts these into
    /// HouseDesign assets; at runtime the app only reads those assets (designs as data, NFR-12).
    /// Coordinates are metres. Downhill is +Z; the uphill reference edge has ground height 0.
    /// </summary>
    public static class Blueprints
    {
        public const string PantherId = "PN-01";
        public const string ElephantId = "EL-01";
        public const string CarId = "CR-01";

        public static IReadOnlyList<HouseBlueprint> All() => new[] { Panther(), Elephant(), Car() };

        public static HouseBlueprint Get(string designId)
        {
            foreach (var blueprint in All())
                if (blueprint.DesignId == designId) return blueprint;
            return null;
        }

        /// <summary>Ingwe: a crouching panther made of rib arches, head cantilevered over the slope.</summary>
        public static HouseBlueprint Panther()
        {
            var b = new BlueprintBuilder();
            float[] z = { -4.0f, -2.4f, -0.8f, 0.8f, 2.4f, 4.0f };
            float[] crown = { 4.2f, 4.0f, 3.8f, 3.6f, 3.4f, 3.3f };
            const float halfSpan = 2.6f;
            var ribs = new string[z.Length];
            var paws = new string[z.Length];

            for (var i = 0; i < z.Length; i++)
            {
                paws[i] = b.Pad($"PN-PAW-{i + 1}", $"Paw strip footing {i + 1}", 0f, z[i], 5.8f, 0.9f, 0.6f, MaterialLibrary.EarthBlock,
                    hint: "Clay-brick strip footing that spreads the rib's thrust into the hillside.");
            }

            for (var i = 0; i < z.Length - 1; i++)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    b.Wall($"PN-PLINTH-{(side < 0 ? "L" : "R")}{i + 1}", $"{(side < 0 ? "Left" : "Right")} plinth wall {i + 1}", BuildStage.Foundation,
                        Endpoint.Ground(side * halfSpan, 0f, z[i]), Endpoint.Ground(side * halfSpan, 0f, z[i + 1]), 0.7f, 0.35f,
                        MaterialLibrary.EarthBlock, On(paws[i], paws[i + 1]),
                        hint: "Brick plinth wall tying the paw footings together so they cannot slide apart.");
                }
            }

            for (var i = 0; i < z.Length; i++)
            {
                ribs[i] = b.Arch($"PN-RIB-{i + 1}", $"Rib arch {i + 1}", Endpoint.Ground(-halfSpan, 0.35f, z[i]),
                    Endpoint.Ground(halfSpan, 0.35f, z[i]), crown[i], 0.25f, MaterialLibrary.Timber, On(paws[i]),
                    hint: "Strong under its own weight, but tips over sideways unless braced.");
            }

            for (var i = 0; i < z.Length - 1; i++)
            {
                b.Strut($"PN-SPINE-{i + 1}", $"Spine beam {i + 1}", MemberKind.Beam, BuildStage.PrimaryStructure,
                    Endpoint.At(0f, crown[i], z[i]), Endpoint.At(0f, crown[i + 1], z[i + 1]), 0.22f, MaterialLibrary.Timber,
                    MaterialLibrary.Frame, On(ribs[i], ribs[i + 1]), hint: "Ties neighbouring ribs together along the back.");
            }

            var forepawLeft = b.Pad("PN-FOREPAW-L", "Left forepaw pier", -0.9f, 6.6f, 1.0f, 1.0f, 0.6f, MaterialLibrary.EarthBlock,
                required: false, hint: "Optional footing for a prop under the head.");
            var forepawRight = b.Pad("PN-FOREPAW-R", "Right forepaw pier", 0.9f, 6.6f, 1.0f, 1.0f, 0.6f, MaterialLibrary.EarthBlock,
                required: false, hint: "Optional footing for a prop under the head.");
            var forelegLeft = b.Strut("PN-FORELEG-L", "Left foreleg prop", MemberKind.Column, BuildStage.PrimaryStructure,
                Endpoint.Ground(-0.9f, 0.35f, 6.6f), Endpoint.At(-0.9f, 3.05f, 6.6f), 0.26f, MaterialLibrary.Steel, MaterialLibrary.Frame,
                On(forepawLeft), required: false, hint: "Props the head so it no longer acts as a cantilever.");
            var forelegRight = b.Strut("PN-FORELEG-R", "Right foreleg prop", MemberKind.Column, BuildStage.PrimaryStructure,
                Endpoint.Ground(0.9f, 0.35f, 6.6f), Endpoint.At(0.9f, 3.05f, 6.6f), 0.26f, MaterialLibrary.Steel, MaterialLibrary.Frame,
                On(forepawRight), required: false, hint: "Props the head so it no longer acts as a cantilever.");

            var head = b.Strut("PN-HEAD", "Head cantilever", MemberKind.Cantilever, BuildStage.PrimaryStructure,
                Endpoint.At(0f, 3.25f, 4.0f), Endpoint.At(0f, 3.0f, 7.2f), 0.32f, MaterialLibrary.Timber, MaterialLibrary.Frame,
                On(ribs[5]), extraSupports: On(forelegLeft, forelegRight), tributaryArea: 9.6f,
                hint: "The viewing room hangs 3.2 m over the slope. People on it bend it hard.");

            for (var i = 0; i < z.Length - 1; i++)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    var label = side < 0 ? "L" : "R";
                    b.Strut($"PN-BRACE-{label}{i + 1}", $"{(side < 0 ? "Left" : "Right")} flank brace {i + 1}", MemberKind.Brace,
                        BuildStage.Bracing, Endpoint.Ground(side * halfSpan, 0.5f, z[i]), Endpoint.Ground(side * 2.3f, 1.15f, z[i + 1]),
                        0.12f, MaterialLibrary.Bamboo, MaterialLibrary.Bracing, On(ribs[i], ribs[i + 1]), required: i == 0,
                        hint: "Stops the ribs falling like dominoes in an earthquake.");
                }
            }

            for (var i = 0; i < z.Length - 1; i++)
            {
                var midZ = (z[i] + z[i + 1]) * 0.5f;
                var midCrown = (crown[i] + crown[i + 1]) * 0.5f;
                var pitch = (float)(System.Math.Atan2(crown[i] - crown[i + 1], z[i + 1] - z[i]) * 180.0 / System.Math.PI);
                foreach (var side in new[] { -1, 1 })
                {
                    var label = side < 0 ? "L" : "R";
                    b.Panel($"PN-SKIN-{label}{i + 1}", $"{(side < 0 ? "Left" : "Right")} skin panel {i + 1}", BuildStage.Roof,
                        new Vector3(side * 1.45f, midCrown - 0.6f, midZ), new Vector3(2.9f, 0.08f, 1.6f), new Vector3(pitch, 0f, -side * 38f),
                        MaterialLibrary.Bamboo, On(ribs[i], ribs[i + 1]), isRoof: true, hint: "Thatched skin between two ribs.");
                }
            }

            b.Panel("PN-HEAD-ROOF", "Head roof", BuildStage.Roof, new Vector3(0f, 3.5f, 5.6f), new Vector3(3.0f, 0.08f, 3.2f),
                new Vector3(4f, 0f, 0f), MaterialLibrary.Bamboo, On(head), isRoof: true);

            // Timber-clad flank walls close the body between each pair of ribs, from the brick plinth up to the eaves.
            for (var i = 0; i < z.Length - 1; i++)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    var label = side < 0 ? "L" : "R";
                    b.CladWall($"PN-FLANK-{label}{i + 1}", $"{(side < 0 ? "Left" : "Right")} flank wall {i + 1}",
                        Endpoint.Ground(side * 2.75f, 0.7f, z[i]), Endpoint.Ground(side * 2.75f, 0.7f, z[i + 1]),
                        System.Math.Min(crown[i], crown[i + 1]) - 1.45f, 0.12f, MaterialLibrary.Timber, On(paws[i], paws[i + 1]),
                        hint: "Cladding that encloses the panther's body. It keeps the weather out but does not brace the ribs.");
                }
            }

            b.Panel("PN-HAUNCH-WALL", "Haunch wall", BuildStage.Envelope, new Vector3(0f, 2.3f, -4.1f), new Vector3(4.4f, 0.1f, 3.4f),
                new Vector3(90f, 0f, 0f), MaterialLibrary.EarthBlock, On(ribs[0]), isRoof: false);
            b.Panel("PN-FACE-GLASS", "Face screen", BuildStage.Envelope, new Vector3(0f, 1.9f, 4.1f), new Vector3(4.4f, 0.06f, 2.8f),
                new Vector3(90f, 0f, 0f), MaterialLibrary.Timber, On(ribs[5]), isRoof: false);

            return new HouseBlueprint(PantherId, 1, "Ingwe", "panther",
                "A black panther crouched down the hillside. Six rib arches form its body; its head is a viewing room cantilevered over the slope.",
                "Rib arches need bracing to resist shaking, and cantilevers need depth, strength or a prop.",
                3, new FoundationStyle("Paw strips", "Panther paws spread weight along the ground", 1.2f, 0.4f, 1.1f), z[0], b.Members);
        }

        /// <summary>Inzovu: an elephant on four legs. Its ears are shade cantilevers and its trunk drains the roof.</summary>
        public static HouseBlueprint Elephant()
        {
            var b = new BlueprintBuilder();
            const float x = 3f;
            const float back = -2.5f;
            const float front = 2.5f;
            const float belly = 3f;

            var footBL = b.Pad("EL-FOOT-BL", "Back-left foot", -x, back, 1.6f, 1.6f, 0.9f, MaterialLibrary.EarthBlock,
                hint: "Wide clay-brick elephant feet keep bearing pressure low on soft soil.");
            var footBR = b.Pad("EL-FOOT-BR", "Back-right foot", x, back, 1.6f, 1.6f, 0.9f, MaterialLibrary.EarthBlock);
            var footFL = b.Pad("EL-FOOT-FL", "Front-left foot", -x, front, 1.6f, 1.6f, 0.9f, MaterialLibrary.EarthBlock);
            var footFR = b.Pad("EL-FOOT-FR", "Front-right foot", x, front, 1.6f, 1.6f, 0.9f, MaterialLibrary.EarthBlock);
            var cistern = b.Pad("EL-CISTERN", "Trunk cistern", 0f, 5.6f, 1.4f, 1.4f, 0.5f, MaterialLibrary.Concrete,
                hint: "Collects water from the trunk downpipe.");

            string Leg(string id, string name, float lx, float lz, string foot) =>
                b.Strut(id, name, MemberKind.Column, BuildStage.PrimaryStructure, Endpoint.Ground(lx, 0.65f, lz),
                    Endpoint.At(lx, belly, lz), 0.55f, MaterialLibrary.Concrete, MaterialLibrary.Frame, On(foot),
                    hint: "Thick legs carry the whole body.");

            var legBL = Leg("EL-LEG-BL", "Back-left leg", -x, back, footBL);
            var legBR = Leg("EL-LEG-BR", "Back-right leg", x, back, footBR);
            var legFL = Leg("EL-LEG-FL", "Front-left leg", -x, front, footFL);
            var legFR = Leg("EL-LEG-FR", "Front-right leg", x, front, footFR);

            // A brick plinth wall runs round all four feet, so the elephant stands on a tied base.
            void Plinth(string id, string name, float ax, float az, float bx, float bz, string f1, string f2) =>
                b.Wall(id, name, BuildStage.Foundation, Endpoint.Ground(ax, 0f, az), Endpoint.Ground(bx, 0f, bz), 0.8f, 0.4f,
                    MaterialLibrary.EarthBlock, On(f1, f2), hint: "Brick plinth wall tying two feet together.");
            Plinth("EL-PLINTH-F", "Front plinth wall", -x, front, x, front, footFL, footFR);
            Plinth("EL-PLINTH-B", "Back plinth wall", -x, back, x, back, footBL, footBR);
            Plinth("EL-PLINTH-L", "Left plinth wall", -x, back, -x, front, footBL, footFL);
            Plinth("EL-PLINTH-R", "Right plinth wall", x, back, x, front, footBR, footFR);

            string Belly(string id, string name, float ax, float az, float bx, float bz, string s1, string s2) =>
                b.Strut(id, name, MemberKind.Beam, BuildStage.PrimaryStructure, Endpoint.At(ax, belly, az), Endpoint.At(bx, belly, bz),
                    0.35f, MaterialLibrary.Timber, MaterialLibrary.Frame, On(s1, s2), tributaryArea: 7.5f,
                    hint: "Carries the floor of the elephant's belly.");

            var bellyFront = Belly("EL-BELLY-F", "Front belly beam", -x, front, x, front, legFL, legFR);
            var bellyBack = Belly("EL-BELLY-B", "Back belly beam", -x, back, x, back, legBL, legBR);
            var bellyLeft = Belly("EL-BELLY-L", "Left belly beam", -x, back, -x, front, legBL, legFL);
            var bellyRight = Belly("EL-BELLY-R", "Right belly beam", x, back, x, front, legBR, legFR);

            var bodyBack = b.Arch("EL-BODY-B", "Rear body arch", Endpoint.At(-x, belly, back), Endpoint.At(x, belly, back), 6.0f, 0.3f,
                MaterialLibrary.Timber, On(bellyBack));
            var bodyMid = b.Arch("EL-BODY-M", "Middle body arch", Endpoint.At(-x, belly, 0f), Endpoint.At(x, belly, 0f), 6.2f, 0.3f,
                MaterialLibrary.Timber, On(bellyLeft, bellyRight));
            var bodyFront = b.Arch("EL-BODY-F", "Front body arch", Endpoint.At(-x, belly, front), Endpoint.At(x, belly, front), 6.0f, 0.3f,
                MaterialLibrary.Timber, On(bellyFront));

            var spars = new Dictionary<int, string>();
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                spars[side] = b.Strut($"EL-EAR-{label}-SPAR", $"{(side < 0 ? "Left" : "Right")} ear spar", MemberKind.Cantilever,
                    BuildStage.PrimaryStructure, Endpoint.At(side * 2.4f, 5.0f, front), Endpoint.At(side * 6.2f, 5.4f, 2.1f), 0.28f,
                    MaterialLibrary.Bamboo, MaterialLibrary.Frame, On(bodyFront),
                    hint: "Holds the ear canopy out from the body. Wind tries to lift it.");
            }

            b.Strut("EL-LEGBRACE-L", "Left leg brace", MemberKind.Brace, BuildStage.Bracing, Endpoint.Ground(-x, 0.9f, back),
                Endpoint.At(-x, 2.9f, front), 0.15f, MaterialLibrary.Timber, MaterialLibrary.Bracing, On(legBL, legFL));
            b.Strut("EL-LEGBRACE-R", "Right leg brace", MemberKind.Brace, BuildStage.Bracing, Endpoint.Ground(x, 0.9f, back),
                Endpoint.At(x, 2.9f, front), 0.15f, MaterialLibrary.Timber, MaterialLibrary.Bracing, On(legBR, legFR));
            b.Strut("EL-LEGBRACE-F", "Front leg brace", MemberKind.Brace, BuildStage.Bracing, Endpoint.Ground(-x, 0.9f, front),
                Endpoint.At(x, 2.9f, front), 0.15f, MaterialLibrary.Timber, MaterialLibrary.Bracing, On(legFL, legFR), required: false);
            b.Strut("EL-LEGBRACE-B", "Back leg brace", MemberKind.Brace, BuildStage.Bracing, Endpoint.Ground(-x, 0.9f, back),
                Endpoint.At(x, 2.9f, back), 0.15f, MaterialLibrary.Timber, MaterialLibrary.Bracing, On(legBL, legBR), required: false);
            b.Strut("EL-EARTIE-L", "Left ear tie-down", MemberKind.Brace, BuildStage.Bracing, Endpoint.At(-x, belly, front),
                Endpoint.At(-5.0f, 5.25f, 2.25f), 0.1f, MaterialLibrary.Bamboo, MaterialLibrary.Bracing, On(spars[-1], legFL), required: false,
                hint: "Ties the ear down so wind cannot rip it off.");
            b.Strut("EL-EARTIE-R", "Right ear tie-down", MemberKind.Brace, BuildStage.Bracing, Endpoint.At(x, belly, front),
                Endpoint.At(5.0f, 5.25f, 2.25f), 0.1f, MaterialLibrary.Bamboo, MaterialLibrary.Bracing, On(spars[1], legFR), required: false,
                hint: "Ties the ear down so wind cannot rip it off.");

            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                var name = side < 0 ? "Left" : "Right";
                b.Panel($"EL-ROOF-{label}B", $"{name} rear roof", BuildStage.Roof, new Vector3(side * 1.55f, 5.3f, -1.25f),
                    new Vector3(3.3f, 0.1f, 2.6f), new Vector3(0f, 0f, -side * 28f), MaterialLibrary.Timber, On(bodyBack, bodyMid), isRoof: true);
                b.Panel($"EL-ROOF-{label}F", $"{name} front roof", BuildStage.Roof, new Vector3(side * 1.55f, 5.3f, 1.25f),
                    new Vector3(3.3f, 0.1f, 2.6f), new Vector3(0f, 0f, -side * 28f), MaterialLibrary.Timber, On(bodyMid, bodyFront), isRoof: true);
                b.Panel($"EL-EAR-{label}", $"{name} ear canopy", BuildStage.Roof, new Vector3(side * 4.3f, 5.15f, 2.2f),
                    new Vector3(3.8f, 0.06f, 3.0f), new Vector3(0f, 0f, side * 6f), MaterialLibrary.Bamboo, On(spars[side]), isRoof: true,
                    hint: "A big shade canopy. Catches rain and wind.");
            }

            // Brick ground floor under the belly: real walls that also stiffen the legs like shear walls.
            b.Wall("EL-GROUND-WALL-B", "Back ground-floor wall", BuildStage.Envelope, Endpoint.Ground(-x, 0f, back), Endpoint.Ground(x, 0f, back),
                0f, 0.3f, MaterialLibrary.EarthBlock, On(legBL, legBR), topHeight: belly - 0.2f,
                hint: "A brick wall between the back legs. It encloses the ground floor and resists racking.");
            b.Wall("EL-GROUND-WALL-F", "Front ground-floor wall", BuildStage.Envelope, Endpoint.Ground(-x, 0f, front), Endpoint.Ground(x, 0f, front),
                0f, 0.3f, MaterialLibrary.EarthBlock, On(legFL, legFR), topHeight: belly - 0.2f,
                hint: "A brick wall between the front legs. It encloses the ground floor and resists racking.");

            b.Drain("EL-TRUNK", "Trunk downpipe", Endpoint.At(0f, 4.6f, 3.0f), Endpoint.Ground(0f, 0.5f, 5.6f), 0.25f, MaterialLibrary.Bamboo,
                On(bodyFront, cistern), hint: "Carries all roof water to the cistern. Too small and the roof floods.");
            // Clad side walls enclose the body between the belly beams and the roof.
            foreach (var side in new[] { -1, 1 })
            {
                b.Panel(side < 0 ? "EL-SIDE-WALL-L" : "EL-SIDE-WALL-R", side < 0 ? "Left body wall" : "Right body wall", BuildStage.Envelope,
                    new Vector3(side * 3.0f, belly + 0.8f, 0f), new Vector3(1.6f, 0.1f, 5.0f), new Vector3(0f, 0f, 90f), MaterialLibrary.Timber,
                    On(side < 0 ? bellyLeft : bellyRight), isRoof: false, hint: "Cladding that closes the side of the elephant's body.");
            }

            b.Panel("EL-REAR-WALL", "Rear wall", BuildStage.Envelope, new Vector3(0f, 4.4f, -2.55f), new Vector3(5.6f, 0.1f, 2.6f),
                new Vector3(90f, 0f, 0f), MaterialLibrary.EarthBlock, On(bodyBack), isRoof: false);
            b.Panel("EL-FACE", "Face wall", BuildStage.Envelope, new Vector3(0f, 4.4f, 2.55f), new Vector3(5.6f, 0.08f, 2.6f),
                new Vector3(90f, 0f, 0f), MaterialLibrary.Timber, On(bodyFront), isRoof: false);

            return new HouseBlueprint(ElephantId, 1, "Inzovu", "elephant",
                "An elephant standing on four thick legs. Its ears are shade canopies and its trunk carries rainwater to a cistern.",
                "Cantilevered canopies must be tied down against wind uplift, and roofs need drains sized for heavy rain.",
                2, new FoundationStyle("Elephant feet", "Wide soft pads spread a huge weight", 1.4f, 0.2f, 1.0f), back, b.Members);
        }

        /// <summary>Imodoka: a car-shaped pod on spider stilts stepping down the slope.</summary>
        public static HouseBlueprint Car()
        {
            var b = new BlueprintBuilder();
            float[] z = { -3.5f, 0f, 3.5f };
            float[] crowns = { 3.8f, 4.0f, 3.2f };
            string[] where = { "uphill", "middle", "downhill" };
            const float x = 1.8f;
            const float deck = 2.2f;
            const float rake = 1.15f;

            var stilts = new Dictionary<string, string>();
            var piers = new Dictionary<string, string>();
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                var name = side < 0 ? "left" : "right";
                for (var i = 0; i < z.Length; i++)
                {
                    var pad = b.Pad($"CR-PAD-{label}{i}", $"{Capitalise(where[i])} {name} brick pier", side * x * rake, z[i], 1.0f, 1.0f, 0.8f,
                        MaterialLibrary.EarthBlock, hint: "A clay-brick pier: the stilt stands on brick, not on bare soil.");
                    piers[$"{label}{i}"] = pad;
                    stilts[$"{label}{i}"] = b.Strut($"CR-STILT-{label}{i}", $"{Capitalise(where[i])} {name} stilt", MemberKind.Column,
                        BuildStage.PrimaryStructure, Endpoint.Ground(side * x * rake, 0.55f, z[i]), Endpoint.At(side * x, deck, z[i]), 0.18f,
                        MaterialLibrary.Timber, MaterialLibrary.Frame, On(pad),
                        hint: i == 2 ? "The longest stilt on the slope. Slender stilts buckle." : "Lifts the car off the hillside.");
                }
            }

            // Brick plinth walls tie the piers together down each side and across the slope.
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

            var deckBeams = new Dictionary<string, string>();
            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                for (var i = 0; i < 2; i++)
                {
                    deckBeams[$"{label}{i}"] = b.Strut($"CR-DECK-{label}{i}", $"{(side < 0 ? "Left" : "Right")} chassis beam {i + 1}",
                        MemberKind.Beam, BuildStage.PrimaryStructure, Endpoint.At(side * x, deck, z[i]), Endpoint.At(side * x, deck, z[i + 1]),
                        0.22f, MaterialLibrary.Timber, MaterialLibrary.Frame, On(stilts[$"{label}{i}"], stilts[$"{label}{i + 1}"]),
                        tributaryArea: 6.3f);
                }
            }

            var bodies = new string[z.Length];
            for (var i = 0; i < z.Length; i++)
            {
                var cross = b.Strut($"CR-CROSS-{i}", $"{Capitalise(where[i])} cross beam", MemberKind.Beam, BuildStage.PrimaryStructure,
                    Endpoint.At(-x, deck, z[i]), Endpoint.At(x, deck, z[i]), 0.22f, MaterialLibrary.Timber, MaterialLibrary.Frame,
                    On(stilts[$"L{i}"], stilts[$"R{i}"]), tributaryArea: 3.0f);
                bodies[i] = b.Arch($"CR-BODY-{i}", $"{Capitalise(where[i])} body hoop", Endpoint.At(-x, deck, z[i]), Endpoint.At(x, deck, z[i]),
                    crowns[i], 0.18f, MaterialLibrary.Steel, On(cross), hint: "Gives the car its rounded body.");
            }

            foreach (var side in new[] { -1, 1 })
            {
                var label = side < 0 ? "L" : "R";
                var name = side < 0 ? "Left" : "Right";
                b.Strut($"CR-BRACE-{label}0", $"{name} uphill X-brace", MemberKind.Brace, BuildStage.Bracing,
                    Endpoint.Ground(side * x * rake, 0.75f, z[0]), Endpoint.At(side * x, deck - 0.1f, z[1]), 0.12f, MaterialLibrary.Timber,
                    MaterialLibrary.Bracing, On(stilts[$"{label}0"], stilts[$"{label}1"]));
                b.Strut($"CR-BRACE-{label}1", $"{name} downhill X-brace", MemberKind.Brace, BuildStage.Bracing,
                    Endpoint.Ground(side * x * rake, 0.75f, z[1]), Endpoint.At(side * x, deck - 0.1f, z[2]), 0.12f, MaterialLibrary.Timber,
                    MaterialLibrary.Bracing, On(stilts[$"{label}1"], stilts[$"{label}2"]), required: false,
                    hint: "Braces the long downhill stilts against shaking.");
            }

            for (var i = 0; i < z.Length; i++)
            {
                b.Strut($"CR-SWAY-{i}", $"{Capitalise(where[i])} sway brace", MemberKind.Brace, BuildStage.Bracing,
                    Endpoint.Ground(-x * rake, 0.75f, z[i]), Endpoint.At(x, deck - 0.1f, z[i]), 0.12f, MaterialLibrary.Timber,
                    MaterialLibrary.Bracing, On(stilts[$"L{i}"], stilts[$"R{i}"]), required: i == 0);
            }

            b.Panel("CR-ROOF-REAR", "Rear roof", BuildStage.Roof, new Vector3(0f, 3.85f, -1.75f), new Vector3(3.4f, 0.08f, 3.6f),
                new Vector3(-3f, 0f, 0f), MaterialLibrary.Steel, On(bodies[0], bodies[1]), isRoof: true);
            b.Panel("CR-ROOF-FRONT", "Windscreen roof", BuildStage.Roof, new Vector3(0f, 3.6f, 1.75f), new Vector3(3.4f, 0.08f, 3.7f),
                new Vector3(13f, 0f, 0f), MaterialLibrary.Steel, On(bodies[1], bodies[2]), isRoof: true);
            b.Panel("CR-TAIL", "Tail panel", BuildStage.Roof, new Vector3(0f, 3.0f, -3.9f), new Vector3(3.4f, 0.08f, 1.8f),
                new Vector3(-65f, 0f, 0f), MaterialLibrary.Steel, On(bodies[0]), isRoof: false);
            b.Panel("CR-HOOD", "Bonnet", BuildStage.Roof, new Vector3(0f, 2.75f, 4.1f), new Vector3(3.4f, 0.08f, 1.6f),
                new Vector3(40f, 0f, 0f), MaterialLibrary.Steel, On(bodies[2]), isRoof: true);

            for (var i = 0; i < 2; i++)
            {
                var midZ = (z[i] + z[i + 1]) * 0.5f;
                b.Panel($"CR-FLOOR-{i}", $"Floor panel {i + 1}", BuildStage.Envelope, new Vector3(0f, deck + 0.13f, midZ),
                    new Vector3(3.6f, 0.08f, 3.5f), Vector3.Zero, MaterialLibrary.Timber, On(deckBeams[$"L{i}"], deckBeams[$"R{i}"]), isRoof: false);
                foreach (var side in new[] { -1, 1 })
                {
                    var label = side < 0 ? "L" : "R";
                    b.Panel($"CR-WINDOW-{label}{i}", $"{(side < 0 ? "Left" : "Right")} window {i + 1}", BuildStage.Envelope,
                        new Vector3(side * x, 3.0f, midZ), new Vector3(1.3f, 0.05f, 3.4f), new Vector3(0f, 0f, 90f), MaterialLibrary.Timber,
                        On(deckBeams[$"{label}{i}"]), isRoof: false);
                }
            }

            return new HouseBlueprint(CarId, 1, "Imodoka", "car",
                "A sports-car pod lifted on raked spider stilts that step down the hillside.",
                "On a slope the downhill stilts are longest and buckle first; bracing and stronger material fix them.",
                1, new FoundationStyle("Spider feet", "Raked spider legs grip the slope", 1.0f, 0.8f, 1.2f), z[0], b.Members);
        }

        private static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
