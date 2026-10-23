using System;
using Fatiro.Domain;
using Fatiro.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Fatiro.EditorTools
{
    /// <summary>
    /// Builds each design's fit-out: the furniture, lamps and details that make the revealed house
    /// read as a home (beds, sofas, kitchens, rugs), plus the car's wheels and lights and the
    /// animals' eyes. Positions are in structure space (metres), on each design's floors.
    /// </summary>
    public static class FitOutBuilder
    {
        /// <summary>Makes (or reuses) a lit material: name, colour, smoothness, optional emission.</summary>
        public delegate Material MaterialMaker(string name, Color colour, float smoothness, Color? emission);

        private sealed class Palette
        {
            public Material Wood;
            public Material Linen;
            public Material Fabric;
            public Material Cushion;
            public Material Counter;
            public Material Steel;
            public Material Tyre;
            public Material Rim;
            public Material Lamp;
            public Material RedLight;
            public Material Eye;
            public Material Ivory;
            public Material Leaf;
            public Material Pot;
            public Material[] Rug;
            public GameObject Table;
            public GameObject Chair;
            public GameObject Car;
        }

        private static Palette palette;

        public static void Begin(MaterialMaker make, GameObject importedTable, GameObject importedChair, GameObject parkedCar)
        {
            palette = new Palette
            {
                Wood = make("FitOut_Wood", new Color(0.52f, 0.34f, 0.20f), 0.35f, null),
                Linen = make("FitOut_Linen", new Color(0.95f, 0.94f, 0.90f), 0.2f, null),
                Fabric = make("FitOut_Fabric", new Color(0.13f, 0.36f, 0.40f), 0.15f, null),
                Cushion = make("FitOut_Cushion", new Color(0.90f, 0.72f, 0.30f), 0.15f, null),
                Counter = make("FitOut_Counter", new Color(0.82f, 0.82f, 0.80f), 0.6f, null),
                Steel = make("FitOut_Steel", new Color(0.65f, 0.68f, 0.72f), 0.7f, null),
                Tyre = make("FitOut_Tyre", new Color(0.06f, 0.06f, 0.07f), 0.25f, null),
                Rim = make("FitOut_Rim", new Color(0.80f, 0.82f, 0.85f), 0.85f, null),
                Lamp = make("FitOut_Lamp", new Color(1f, 0.92f, 0.70f), 0.9f, new Color(2.2f, 1.8f, 1.0f)),
                RedLight = make("FitOut_TailLight", new Color(1f, 0.15f, 0.12f), 0.9f, new Color(2.0f, 0.15f, 0.1f)),
                Eye = make("FitOut_Eye", new Color(1f, 0.75f, 0.15f), 0.95f, new Color(2.4f, 1.5f, 0.2f)),
                Ivory = make("FitOut_Ivory", new Color(0.96f, 0.93f, 0.84f), 0.55f, null),
                Leaf = make("FitOut_Leaf", new Color(0.24f, 0.55f, 0.25f), 0.2f, null),
                Pot = make("FitOut_Pot", new Color(0.66f, 0.36f, 0.24f), 0.2f, null),
                Rug = new[]
                {
                    make("FitOut_RugBlack", new Color(0.10f, 0.09f, 0.08f), 0.1f, null),
                    make("FitOut_RugCream", new Color(0.93f, 0.88f, 0.78f), 0.1f, null),
                    make("FitOut_RugRed", new Color(0.62f, 0.20f, 0.14f), 0.1f, null)
                },
                Table = importedTable,
                Chair = importedChair,
                Car = parkedCar
            };
        }

        public static GameObject Build(string designId)
        {
            var root = new GameObject($"FitOut_{designId}");
            switch (designId)
            {
                case Blueprints.CarId: Car(root); break;
                case Blueprints.PantherId: Panther(root); break;
                case Blueprints.ElephantId: Elephant(root); break;
                case Blueprints.BungalowId: Bungalow(root); break;
                case Blueprints.TwoStoreyId: TwoStorey(root); break;
                case Blueprints.FlatsId: Flats(root); break;
            }
            return root;
        }

        // ---- Designs ---------------------------------------------------------------------

        private static void Car(GameObject root)
        {
            const float floor = 2.475f;
            foreach (var side in new[] { -1f, 1f })
            {
                foreach (var z in new[] { -2.9f, 2.9f })
                {
                    var wheel = Item(root, "Wheel", new Vector3(side * 2.12f, 1.75f, z));
                    Cylinder(wheel, palette.Tyre, Vector3.zero, new Vector3(1.32f, 0.22f, 1.32f), new Vector3(0, 0, 90));
                    Cylinder(wheel, palette.Rim, new Vector3(side * 0.12f, 0, 0), new Vector3(0.8f, 0.03f, 0.8f), new Vector3(0, 0, 90));
                }
                var head = Item(root, "Headlight", new Vector3(side * 1.25f, 2.72f, 4.62f));
                Sphere(head, palette.Lamp, Vector3.zero, new Vector3(0.55f, 0.24f, 0.25f));
                var tail = Item(root, "Tail light", new Vector3(side * 1.25f, 2.95f, -4.66f));
                Box(tail, palette.RedLight, Vector3.zero, new Vector3(0.7f, 0.14f, 0.08f));
            }

            Bed(root, new Vector3(0f, floor, -2.7f), 0f);
            Sofa(root, new Vector3(-0.75f, floor, -0.2f), 90f);
            Rug(root, new Vector3(0.35f, floor, 0f), new Vector2(1.6f, 2.0f));
            Dining(root, new Vector3(0.55f, floor, 0.4f));
            Kitchen(root, new Vector3(0f, floor, 2.55f), 2.4f, 0f);
            Lamp(root, new Vector3(-1.3f, floor, 1.2f));
        }

        private static void Panther(GameObject root)
        {
            // Split-level floors follow the ground; the head room is level, on its cantilever.
            const float step = 0.855f;
            Bed(root, new Vector3(-0.9f, step, -3.2f), 0f, followGround: true);
            Plant(root, new Vector3(1.9f, step, -3.4f), followGround: true);
            Sofa(root, new Vector3(-1.2f, step, -1.6f), 90f, followGround: true);
            Rug(root, new Vector3(0.2f, step, -1.6f), new Vector2(1.8f, 1.2f), followGround: true);
            Dining(root, new Vector3(0.4f, step, 0.1f), followGround: true);
            Kitchen(root, new Vector3(1.7f, step, 1.6f), 1.6f, 90f, followGround: true);
            Lamp(root, new Vector3(-1.8f, step, 1.4f), followGround: true);
            Bed(root, new Vector3(0.6f, step, 3.2f), 90f, followGround: true);

            const float head = 3.355f;
            Sofa(root, new Vector3(0f, head, 5.2f), 180f);
            Plant(root, new Vector3(-0.9f, head, 6.4f));
            Plant(root, new Vector3(0.9f, head, 6.4f));
            foreach (var side in new[] { -1f, 1f })
            {
                var eye = Item(root, "Eye", new Vector3(side * 0.48f, 4.12f, 7.62f));
                Sphere(eye, palette.Eye, Vector3.zero, new Vector3(0.42f, 0.2f, 0.12f), new Vector3(0, 0, side * -12f));
                var ear = Item(root, "Ear", new Vector3(side * 1.0f, 5.3f, 5.1f));
                Sphere(ear, palette.Tyre, Vector3.zero, new Vector3(0.55f, 0.75f, 0.22f), new Vector3(-10f, 0, side * -25f));
            }
        }

        private static void Elephant(GameObject root)
        {
            const float floor = 3.325f;
            Bed(root, new Vector3(1.3f, floor, -1.3f), 90f);
            Plant(root, new Vector3(-2.3f, floor, -1.8f));
            Sofa(root, new Vector3(-1.4f, floor, 1.2f), 90f);
            Rug(root, new Vector3(0f, floor, 1.3f), new Vector2(2.0f, 1.6f));
            Dining(root, new Vector3(1.3f, floor, 1.4f));
            Lamp(root, new Vector3(-2.2f, floor, 2.0f));
            foreach (var side in new[] { -1f, 1f })
            {
                var eye = Item(root, "Eye", new Vector3(side * 1.45f, 5.45f, 4.55f));
                Sphere(eye, palette.Tyre, Vector3.zero, new Vector3(0.3f, 0.3f, 0.2f));
                var tusk = Item(root, "Tusk", new Vector3(side * 0.6f, 4.25f, 5.25f));
                Cylinder(tusk, palette.Ivory, new Vector3(0f, -0.2f, 0.45f), new Vector3(0.18f, 0.55f, 0.18f), new Vector3(70f, 0f, 0f));
            }
        }

        private static void Bungalow(GameObject root)
        {
            const float floor = 0.45f;
            Bed(root, new Vector3(-2.6f, floor, -2.4f), 0f);
            Bed(root, new Vector3(2.6f, floor, -2.4f), 0f);
            Sofa(root, new Vector3(-3.6f, floor, 2.2f), 90f);
            Rug(root, new Vector3(-2.2f, floor, 2.2f), new Vector2(2.0f, 2.4f));
            Dining(root, new Vector3(1.4f, floor, 2.0f));
            Kitchen(root, new Vector3(4.4f, floor, 2.0f), 2.6f, 90f);
            Lamp(root, new Vector3(-4.4f, floor, 3.4f));
            Plant(root, new Vector3(-0.6f, floor, 3.5f));
        }

        private static void TwoStorey(GameObject root)
        {
            const float ground = 0.45f;
            const float upper = 3.45f;
            Sofa(root, new Vector3(-3.5f, ground, 1.8f), 90f);
            Rug(root, new Vector3(-2.0f, ground, 1.8f), new Vector2(2.0f, 2.2f));
            Dining(root, new Vector3(1.6f, ground, 1.8f));
            Kitchen(root, new Vector3(2.25f, ground, -2.9f), 3.0f, 0f);
            Plant(root, new Vector3(-0.4f, ground, 3.0f));
            Bed(root, new Vector3(2.25f, upper, -1.9f), 0f);
            Bed(root, new Vector3(2.25f, upper, 1.9f), 0f);
            Bed(root, new Vector3(-2.25f, upper, 1.9f), 0f);
            Lamp(root, new Vector3(-0.6f, upper, -0.6f));
            foreach (var x in new[] { -1.4f, 1.4f })
                Chair(root, new Vector3(x, upper, 4.9f), 180f);
            Plant(root, new Vector3(0f, upper, 5.6f));
        }

        private static void Flats(GameObject root)
        {
            foreach (var level in new[] { 3.45f, 6.45f })
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    Bed(root, new Vector3(side * 4.0f, level, -2.4f), 0f);
                    Sofa(root, new Vector3(side * 5.2f, level, 2.4f), side < 0 ? 90f : -90f);
                    Rug(root, new Vector3(side * 3.8f, level, 2.4f), new Vector2(1.8f, 2.0f));
                    Dining(root, new Vector3(side * 2.0f, level, 2.6f));
                    Kitchen(root, new Vector3(side * 0.5f, level, -2.0f), 2.4f, 90f);
                }
            }
            if (palette.Car != null)
            {
                var car = Object.Instantiate(palette.Car, root.transform);
                car.name = "Parked car";
                car.transform.localPosition = new Vector3(-2.0f, 0.45f, 1.6f);
                car.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                car.AddComponent<FitOutItem>();
            }
        }

        // ---- Furniture -------------------------------------------------------------------

        private static void Bed(GameObject root, Vector3 at, float yaw, bool followGround = false)
        {
            var bed = Item(root, "Bed", at, yaw, followGround);
            Box(bed, palette.Wood, new Vector3(0, 0.18f, 0), new Vector3(1.6f, 0.36f, 2.0f));
            Box(bed, palette.Linen, new Vector3(0, 0.44f, 0.1f), new Vector3(1.5f, 0.18f, 1.75f));
            Box(bed, palette.Fabric, new Vector3(0, 0.5f, 0.45f), new Vector3(1.52f, 0.1f, 1.0f));
            Box(bed, palette.Linen, new Vector3(0, 0.6f, -0.72f), new Vector3(1.2f, 0.16f, 0.4f));
            Box(bed, palette.Wood, new Vector3(0, 0.6f, -0.98f), new Vector3(1.6f, 0.9f, 0.08f));
        }

        private static void Sofa(GameObject root, Vector3 at, float yaw, bool followGround = false)
        {
            var sofa = Item(root, "Sofa", at, yaw, followGround);
            Box(sofa, palette.Fabric, new Vector3(0, 0.22f, 0), new Vector3(2.0f, 0.44f, 0.9f));
            Box(sofa, palette.Fabric, new Vector3(0, 0.55f, -0.38f), new Vector3(2.0f, 0.7f, 0.18f));
            Box(sofa, palette.Fabric, new Vector3(-0.92f, 0.4f, 0), new Vector3(0.18f, 0.4f, 0.9f));
            Box(sofa, palette.Fabric, new Vector3(0.92f, 0.4f, 0), new Vector3(0.18f, 0.4f, 0.9f));
            Box(sofa, palette.Cushion, new Vector3(-0.5f, 0.6f, -0.22f), new Vector3(0.45f, 0.4f, 0.14f), new Vector3(-12f, 0, 0));
            Box(sofa, palette.Cushion, new Vector3(0.5f, 0.6f, -0.22f), new Vector3(0.45f, 0.4f, 0.14f), new Vector3(-12f, 0, 0));
        }

        private static void Dining(GameObject root, Vector3 at, bool followGround = false)
        {
            if (palette.Table != null)
            {
                var table = Item(root, "Table", at, 0f, followGround);
                Fit(table, palette.Table, 1.2f);
            }
            else
            {
                var table = Item(root, "Table", at, 0f, followGround);
                Box(table, palette.Wood, new Vector3(0, 0.74f, 0), new Vector3(1.2f, 0.05f, 0.8f));
                foreach (var x in new[] { -0.5f, 0.5f })
                foreach (var z in new[] { -0.32f, 0.32f })
                    Box(table, palette.Wood, new Vector3(x, 0.37f, z), new Vector3(0.06f, 0.74f, 0.06f));
            }
            Chair(root, at + new Vector3(0, 0, -0.7f), 0f, followGround);
            Chair(root, at + new Vector3(0, 0, 0.7f), 180f, followGround);
        }

        private static void Chair(GameObject root, Vector3 at, float yaw, bool followGround = false)
        {
            var chair = Item(root, "Chair", at, yaw, followGround);
            if (palette.Chair != null)
            {
                Fit(chair, palette.Chair, 0.5f);
                return;
            }
            Box(chair, palette.Wood, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f));
            Box(chair, palette.Wood, new Vector3(0, 0.7f, -0.2f), new Vector3(0.45f, 0.5f, 0.05f));
            foreach (var x in new[] { -0.19f, 0.19f })
            foreach (var z in new[] { -0.19f, 0.19f })
                Box(chair, palette.Wood, new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.45f, 0.04f));
        }

        private static void Kitchen(GameObject root, Vector3 at, float length, float yaw, bool followGround = false)
        {
            var kitchen = Item(root, "Kitchen", at, yaw, followGround);
            Box(kitchen, palette.Wood, new Vector3(0, 0.43f, 0), new Vector3(length, 0.86f, 0.6f));
            Box(kitchen, palette.Counter, new Vector3(0, 0.88f, 0), new Vector3(length + 0.04f, 0.05f, 0.64f));
            Box(kitchen, palette.Steel, new Vector3(length * 0.25f, 0.92f, 0), new Vector3(0.5f, 0.04f, 0.4f));
        }

        private static void Rug(GameObject root, Vector3 at, Vector2 size, bool followGround = false)
        {
            // Imigongo zigzag in black, cream and red.
            var rug = Item(root, "Imigongo rug", at, 0f, followGround);
            Box(rug, palette.Rug[0], new Vector3(0, 0.01f, 0), new Vector3(size.x, 0.02f, size.y));
            var stripes = 5;
            for (var i = 0; i < stripes; i++)
            {
                var z = -size.y * 0.5f + size.y * (i + 0.5f) / stripes;
                Box(rug, palette.Rug[1 + i % 2], new Vector3(0, 0.022f, z), new Vector3(size.x * 0.85f, 0.02f, size.y / stripes * 0.35f),
                    new Vector3(0, i % 2 == 0 ? 8f : -8f, 0));
            }
        }

        private static void Lamp(GameObject root, Vector3 at, bool followGround = false)
        {
            var lamp = Item(root, "Lamp", at, 0f, followGround);
            Cylinder(lamp, palette.Steel, new Vector3(0, 0.7f, 0), new Vector3(0.05f, 0.7f, 0.05f));
            Sphere(lamp, palette.Lamp, new Vector3(0, 1.45f, 0), Vector3.one * 0.32f);
        }

        private static void Plant(GameObject root, Vector3 at, bool followGround = false)
        {
            var plant = Item(root, "Plant", at, 0f, followGround);
            Cylinder(plant, palette.Pot, new Vector3(0, 0.2f, 0), new Vector3(0.36f, 0.2f, 0.36f));
            Sphere(plant, palette.Leaf, new Vector3(0, 0.62f, 0), new Vector3(0.6f, 0.65f, 0.6f));
        }

        // ---- Primitives --------------------------------------------------------------------

        private static GameObject Item(GameObject root, string name, Vector3 at, float yaw = 0f, bool followGround = false)
        {
            var item = new GameObject(name);
            item.transform.SetParent(root.transform, false);
            item.transform.localPosition = at;
            item.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            item.AddComponent<FitOutItem>().FollowGround = followGround;
            return item;
        }

        private static void Box(GameObject parent, Material material, Vector3 position, Vector3 scale, Vector3 euler = default) =>
            Primitive(parent, PrimitiveType.Cube, material, position, scale, euler);

        private static void Cylinder(GameObject parent, Material material, Vector3 position, Vector3 scale, Vector3 euler = default) =>
            Primitive(parent, PrimitiveType.Cylinder, material, position, scale, euler);

        private static void Sphere(GameObject parent, Material material, Vector3 position, Vector3 scale, Vector3 euler = default) =>
            Primitive(parent, PrimitiveType.Sphere, material, position, scale, euler);

        private static void Primitive(GameObject parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Vector3 euler)
        {
            var piece = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.transform.SetParent(parent.transform, false);
            piece.transform.localPosition = position;
            piece.transform.localEulerAngles = euler;
            piece.transform.localScale = scale;
            var renderer = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>An imported model, scaled so its longest side matches the given size and stood on the item's floor.</summary>
        private static void Fit(GameObject item, GameObject prefab, float size)
        {
            var model = ImportedAssets.Instantiate(prefab, size, false, null, prefab.name);
            var bounds = ImportedAssets.Bounds(model);
            model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            model.transform.SetParent(item.transform, false);
        }
    }
}
