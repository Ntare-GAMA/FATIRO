using UnityEngine;

namespace Fatiro.World
{
    /// <summary>How a building material looks on a member: a shared textured material, a tint, and the texture's real-world size.</summary>
    public readonly struct MemberSurface
    {
        public MemberSurface(Material material, Color tint, float tileMetres)
        {
            Material = material;
            Tint = tint;
            TileMetres = Mathf.Max(0.05f, tileMetres);
        }

        /// <summary>Shared material (one per building material, never copied per member). May be null for plain colour.</summary>
        public Material Material { get; }

        public Color Tint { get; }

        /// <summary>Metres covered by one repeat of the texture, so grain and plates stay the same size on long and short members.</summary>
        public float TileMetres { get; }
    }
}
