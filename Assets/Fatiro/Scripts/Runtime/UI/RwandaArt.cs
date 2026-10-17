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
                Color[] bands = { Theme.Ink, Theme.Paper, Theme.Ink, Theme.Accent };
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
                    texture.SetPixel(x, y, ring % 2 == 0 ? Theme.Ink : ring == 1 ? Theme.Accent : Theme.Paper);
                }
                texture.Apply();
                imigongoVertical = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return imigongoVertical;
            }
        }

        /// <summary>Layered hills with tea terraces, the Virunga volcanoes and a rising sun.</summary>
        public static Sprite Hills
        {
            get
            {
                if (hills != null) return hills;
                const int width = 540;
                const int height = 300;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Thousand hills",
                    wrapMode = TextureWrapMode.Clamp
                };

                var skyTop = new Color(0.97f, 0.93f, 0.80f);
                var skyLow = new Color(0.99f, 0.85f, 0.55f);
                var volcano = new Color(0.47f, 0.56f, 0.55f);
                Color[] layers =
                {
                    new Color(0.62f, 0.74f, 0.52f),
                    new Color(0.44f, 0.62f, 0.36f),
                    new Color(0.29f, 0.48f, 0.27f),
                    new Color(0.18f, 0.35f, 0.21f)
                };
                float[] baseHeights = { 0.58f, 0.46f, 0.33f, 0.18f };
                float[] amplitudes = { 0.06f, 0.08f, 0.09f, 0.08f };
                float[] frequencies = { 2.3f, 3.1f, 2.6f, 3.7f };
                var sun = new Vector2(width * 0.70f, height * 0.66f);

                for (var x = 0; x < width; x++)
                {
                    var u = (float)x / width;

                    // Virunga volcanoes on the far horizon.
                    var peaks = Mathf.Max(Peak(u, 0.20f, 0.13f, 0.30f), Mathf.Max(Peak(u, 0.36f, 0.10f, 0.22f), Peak(u, 0.86f, 0.11f, 0.24f)));
                    var volcanoTop = 0.60f + peaks;

                    for (var y = 0; y < height; y++)
                    {
                        var v = (float)y / height;
                        var colour = Color.Lerp(skyLow, skyTop, Mathf.InverseLerp(0.45f, 1f, v));
                        var toSun = Vector2.Distance(new Vector2(x, y), sun);
                        if (toSun < 34f) colour = Theme.Sun;
                        else if (toSun < 60f) colour = Color.Lerp(colour, Theme.Sun, (60f - toSun) / 26f * 0.35f);
                        if (v < volcanoTop) colour = volcano;

                        for (var layer = 0; layer < layers.Length; layer++)
                        {
                            var ridge = baseHeights[layer] + amplitudes[layer] * (Mathf.Sin(u * frequencies[layer] * Mathf.PI * 2f + layer * 1.7f) +
                                                                                 0.45f * Mathf.Sin(u * frequencies[layer] * 5.3f + layer));
                            if (v > ridge) continue;
                            colour = layers[layer];
                            // Tea terraces: contour lines that follow the ridge on the nearer hills.
                            if (layer >= 1)
                            {
                                var depth = (ridge - v) * height;
                                if (depth % (layer == 3 ? 9f : 7f) < 1.6f) colour *= 0.86f;
                            }
                            colour.a = 1f;
                        }

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
