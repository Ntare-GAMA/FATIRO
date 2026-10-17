using System.Collections;
using Fatiro.Domain;
using UnityEngine;

namespace Fatiro.World
{
    /// <summary>
    /// The scaled hillside plot anchored on the table. Content is modelled in metres and scaled
    /// down by the scale denominator (1:100 to 1:20, FR-04). Downhill (+Z) faces the learner.
    /// </summary>
    public sealed class SiteController : MonoBehaviour
    {
        public const int MinScale = 20;
        public const int MaxScale = 100;

        private const float PlotMinX = -8f;
        private const float PlotMaxX = 8f;
        private const float PlotMinZ = -6.5f;
        private const float PlotMaxZ = 8.5f;

        [SerializeField] private Transform content;
        [SerializeField] private Transform structureRoot;
        [SerializeField] private MeshFilter plotFilter;
        [SerializeField] private MeshCollider plotCollider;
        [Tooltip("Decorative banana trees; their X/Z are kept and their height follows the slope.")]
        [SerializeField] private Transform[] trees = new Transform[0];

        private Mesh plotMesh;
        private Coroutine shake;
        private Vector3 contentRestPosition;

        public Transform StructureRoot => structureRoot;
        public int ScaleDenominator { get; private set; } = 50;
        public SiteLayout Layout { get; private set; }

        /// <summary>Real-world metres per model metre.</summary>
        public float WorldScale => 1f / ScaleDenominator;

        public void Configure(SiteConditions site, float uphillReferenceZ, int scaleDenominator)
        {
            Layout = new SiteLayout(site, uphillReferenceZ);
            float Ground(float z) => Layout.GroundY(Mathf.Clamp(z, PlotMinZ, PlotMaxZ));
            var lowest = Mathf.Min(Ground(PlotMinZ), Ground(PlotMaxZ));
            var baseY = lowest - 0.6f;

            if (plotMesh != null) Destroy(plotMesh);
            plotMesh = MemberMeshes.BuildPlot(PlotMinX, PlotMaxX, PlotMinZ, PlotMaxZ, Ground, baseY);
            plotFilter.sharedMesh = plotMesh;
            if (plotCollider != null) plotCollider.sharedMesh = plotMesh;
            foreach (var tree in trees)
            {
                var position = tree.localPosition;
                tree.localPosition = new Vector3(position.x, Ground(position.z), position.z);
            }

            // Sit the bottom of the plot on the table surface.
            contentRestPosition = new Vector3(0f, -baseY, 0f);
            content.localPosition = contentRestPosition;
            SetScale(scaleDenominator);
        }

        public void SetScale(int denominator)
        {
            ScaleDenominator = Mathf.Clamp(denominator, MinScale, MaxScale);
            transform.localScale = Vector3.one * WorldScale;
        }

        public void Pinch(float factor)
        {
            if (factor <= 0f) return;
            SetScale(Mathf.RoundToInt(ScaleDenominator / factor));
        }

        public void Twist(float degrees) => transform.Rotate(Vector3.up, -degrees, Space.World);

        public void MoveOnTable(Vector3 worldDelta)
        {
            worldDelta.y = 0f;
            transform.position += worldDelta;
        }

        /// <summary>Ground-shaking effect during an earthquake test.</summary>
        public void Shake(float level, float seconds)
        {
            if (shake != null) StopCoroutine(shake);
            shake = StartCoroutine(ShakeRoutine(level, seconds));
        }

        private IEnumerator ShakeRoutine(float level, float seconds)
        {
            var elapsed = 0f;
            var amplitude = 0.05f + level * 0.04f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                var fade = 1f - elapsed / seconds;
                content.localPosition = contentRestPosition + new Vector3(
                    Mathf.Sin(elapsed * 47f) * amplitude * fade, 0f, Mathf.Sin(elapsed * 31f + 1.3f) * amplitude * 0.6f * fade);
                yield return null;
            }
            content.localPosition = contentRestPosition;
            shake = null;
        }

        private void OnDestroy()
        {
            if (plotMesh != null) Destroy(plotMesh);
        }
    }
}
