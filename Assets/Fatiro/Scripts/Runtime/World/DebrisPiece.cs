using UnityEngine;

namespace Fatiro.World
{
    /// <summary>A broken plank or log thrown off a failed member.</summary>
    public sealed class DebrisPiece : PooledEffect
    {
        private const float Lifetime = 1.6f;

        [Tooltip("Broken plank and log meshes; each launch picks one so a collapse never looks repetitive.")]
        [SerializeField] private Mesh[] variants = new Mesh[0];
        [SerializeField] private MeshFilter meshFilter;
        [Tooltip("Size of the debris relative to one model metre.")]
        [SerializeField] private float size = 0.4f;

        private Vector3 velocity;
        private Vector3 spin;
        private float worldScale;

        public void Launch(Vector3 worldPosition, Color color, float scale)
        {
            worldScale = scale;
            if (meshFilter != null && variants.Length > 0) meshFilter.sharedMesh = variants[UnityEngine.Random.Range(0, variants.Length)];
            transform.position = worldPosition;
            transform.rotation = UnityEngine.Random.rotation;
            transform.localScale = Vector3.one * scale * size * UnityEngine.Random.Range(0.7f, 1.3f);
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
