using UnityEngine;

namespace Fatiro.World
{
    /// <summary>A chunk thrown off a failed member.</summary>
    public sealed class DebrisPiece : PooledEffect
    {
        private const float Lifetime = 1.6f;
        private Vector3 velocity;
        private Vector3 spin;
        private float worldScale;

        public void Launch(Vector3 worldPosition, Color color, float scale)
        {
            worldScale = scale;
            transform.position = worldPosition;
            transform.localScale = Vector3.one * scale * UnityEngine.Random.Range(0.25f, 0.5f);
            velocity = (UnityEngine.Random.insideUnitSphere + Vector3.up * 0.8f) * scale * 3f;
            spin = UnityEngine.Random.insideUnitSphere * 540f;
            Tint(color * 0.8f);
        }

        protected override void Tick(float deltaTime)
        {
            velocity += Vector3.down * 9.81f * worldScale * deltaTime;
            transform.position += velocity * deltaTime;
            transform.Rotate(spin * deltaTime, Space.Self);
            if (Age > Lifetime) Finish();
        }
    }
}
