using UnityEngine;

namespace Fatiro.World
{
    /// <summary>A quick expanding flash where a member snapped into place.</summary>
    public sealed class SnapSpark : PooledEffect
    {
        private const float Lifetime = 0.45f;
        private float baseScale;
        private Color color;

        public void Flash(Vector3 worldPosition, float scale, Color tint)
        {
            transform.position = worldPosition;
            baseScale = scale;
            color = tint;
        }

        protected override void Tick(float deltaTime)
        {
            var t = Age / Lifetime;
            transform.localScale = Vector3.one * baseScale * Mathf.Lerp(0.3f, 1.4f, t);
            var faded = color;
            faded.a = 1f - t;
            Tint(faded);
            if (t >= 1f) Finish();
        }
    }
}
