using UnityEngine;

namespace Fatiro.UI
{
    /// <summary>
    /// Procedural artwork that gives the interface its Rwandan identity, generated once at runtime:
    /// an Imigongo band (the zigzag relief art traditionally made in black, white and red-brown)
    /// and a "thousand hills" landscape of tea terraces under the Virunga volcanoes.
    /// </summary>
    public static class RwandaArt
    {
        private static Sprite imigongo;
        private static Sprite imigongoVertical;
        private static Sprite hills;

        /// <summary>Horizontally tiling band of nested Imigongo chevrons.</summary>
        public static Sprite Imigongo
        {
            get
            {
                if (imigongo != null) return imigongo;
                const int width = 128;
                const int height = 64;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Imigongo band",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };
                // Neon Imigongo: cyan and lime chevrons with dark gaps, like light traced over relief art.
                var clear = new Color(0f, 0f, 0f, 0f);
                Color[] bands = { clear, new Color(0.27f, 0.86f, 1f, 0.95f), clear, new Color(0.79f, 0.98f, 0.30f, 0.9f) };
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    // Nested chevrons: distance from the centre line of each 64 px zigzag tooth.
                    var tooth = Mathf.Abs((x % 64) - 31.5f);
                    var band = Mathf.FloorToInt((y + tooth) / 8f);
                    texture.SetPixel(x, y, bands[((band % bands.Length) + bands.Length) % bands.Length]);
                }
                texture.Apply();
                imigongo = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return imigongo;
            }
        }

        /// <summary>Diamond lattice used down the edge of design cards.</summary>
        public static Sprite ImigongoDiamonds
        {
            get
            {
                if (imigongoVertical != null) return imigongoVertical;
                const int size = 48;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Imigongo diamonds",
                    wrapMode = TextureWrapMode.Repeat
                };
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Abs(x - 23.5f) + Mathf.Abs(y - 23.5f);
                    var ring = Mathf.FloorToInt(d / 6f);
                    texture.SetPixel(x, y, ring % 2 == 0 ? new Color(0f, 0f, 0f, 0f) : ring == 1 ? Theme.Accent : Theme.Primary);
                }
                texture.Apply();
                imigongoVertical = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return imigongoVertical;
            }
        }

        /// <summary>
        /// Holographic "thousand hills": layered ridges traced as glowing lines with tea-terrace
        /// contours, the Virunga volcanoes outlined on the horizon, and a perspective grid floor.
        /// </summary>
        public static Sprite Hills
        {
            get
            {
                if (hills != null) return hills;
                const int width = 540;
                const int height = 300;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Thousand hills hologram",
                    wrapMode = TextureWrapMode.Clamp
                };

                var skyTop = new Color(0.010f, 0.030f, 0.050f);
                var skyLow = new Color(0.020f, 0.090f, 0.120f);
                var cyan = Theme.Primary;
                var lime = Theme.Accent;
                float[] baseHeights = { 0.56f, 0.44f, 0.31f, 0.17f };
                float[] amplitudes = { 0.06f, 0.08f, 0.09f, 0.08f };
                float[] frequencies = { 2.3f, 3.1f, 2.6f, 3.7f };
                var sun = new Vector2(width * 0.72f, height * 0.70f);

                for (var x = 0; x < width; x++)
                {
                    var u = (float)x / width;
                    var peaks = Mathf.Max(Peak(u, 0.20f, 0.13f, 0.30f), Mathf.Max(Peak(u, 0.36f, 0.10f, 0.22f), Peak(u, 0.86f, 0.11f, 0.24f)));
                    var volcanoTop = 0.60f + peaks;

                    for (var y = 0; y < height; y++)
                    {
                        var v = (float)y / height;
                        var colour = Color.Lerp(skyLow, skyTop, Mathf.InverseLerp(0.2f, 1f, v));

                        // Perspective grid floor near the bottom.
                        if (v < 0.16f)
                        {
                            var depth = 1f / (v + 0.05f);
                            var row = Mathf.Abs(depth % 1.4f) < 0.12f;
                            var column = Mathf.Abs(((u - 0.5f) * depth * 2.2f) % 1f) < 0.035f * depth * 0.4f;
                            if (row || column) colour = Color.Lerp(colour, cyan, 0.35f);
                        }

                        // Sun as a glowing ring.
                        var toSun = Vector2.Distance(new Vector2(x, y), sun);
                        if (Mathf.Abs(toSun - 26f) < 1.6f) colour = Theme.Sun;
                        else if (toSun < 26f) colour = Color.Lerp(colour, Theme.Sun, 0.08f);

                        // Volcano outlines on the horizon.
                        if (Mathf.Abs(v - volcanoTop) < 0.006f && peaks > 0.01f) colour = Color.Lerp(colour, cyan, 0.55f);

                        for (var layer = 0; layer < baseHeights.Length; layer++)
                        {
                            var ridge = baseHeights[layer] + amplitudes[layer] * (Mathf.Sin(u * frequencies[layer] * Mathf.PI * 2f + layer * 1.7f) +
                                                                                 0.45f * Mathf.Sin(u * frequencies[layer] * 5.3f + layer));
                            if (v > ridge + 0.006f) continue;
                            var brightness = 0.35f + layer * 0.2f;
                            if (v > ridge - 0.006f)
                            {
                                // The ridge line itself; the nearest hill is traced in lime.
                                colour = layer == baseHeights.Length - 1 ? lime : Color.Lerp(skyLow, cyan, brightness + 0.2f);
                            }
                            else
                            {
                                colour = skyTop;
                                var terrace = (ridge - v) * height;
                                if (terrace % (layer == 3 ? 9f : 7f) < 1.1f) colour = Color.Lerp(skyTop, cyan, brightness * 0.55f);
                            }
                        }

                        colour.a = 1f;
                        texture.SetPixel(x, y, colour);
                    }
                }

                texture.Apply();
                hills = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return hills;
            }
        }

        private static float Peak(float u, float centre, float halfWidth, float heightAmount) =>
            Mathf.Max(0f, 1f - Mathf.Abs(u - centre) / halfWidth) * heightAmount;
    }
}
