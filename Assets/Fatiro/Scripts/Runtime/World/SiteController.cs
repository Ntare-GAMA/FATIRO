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

        private static readonly int WorldToModel = Shader.PropertyToID("_FatiroWorldToModel");
        private const float PlotMinX = -8f;
        private const float PlotMaxX = 8f;
        private const float PlotMinZ = -6.5f;
        private const float PlotMaxZ = 8.5f;

        [SerializeField] private Transform content;
        [SerializeField] private Transform structureRoot;
        [SerializeField] private MeshFilter plotFilter;
        [SerializeField] private MeshCollider plotCollider;
        [Tooltip("Compacted building platform under the house footprint.")]
        [SerializeField] private MeshFilter platformFilter;
        [Tooltip("Grass, platform and scenery: shown in the real view.")]
        [SerializeField] private GameObject realGround;
        [Tooltip("The same hillside drawn as a glowing grid: shown in the hologram view.")]
        [SerializeField] private MeshFilter hologramPlot;
        [Tooltip("Scenery props; their X/Z are kept and their height follows the slope.")]
        [SerializeField] private Transform[] trees = new Transform[0];

        private Mesh plotMesh;
        private Mesh platformMesh;
        private Coroutine shake;
        private Vector3 contentRestPosition;

        public Transform StructureRoot => structureRoot;
        public int ScaleDenominator { get; private set; } = 50;
        public SiteLayout Layout { get; private set; }

        /// <summary>Real-world metres per model metre.</summary>
        public float WorldScale => 1f / ScaleDenominator;

        public void Configure(SiteConditions site, float uphillReferenceZ, int scaleDenominator, Rect footprint)
        {
            Layout = new SiteLayout(site, uphillReferenceZ);
            float Ground(float z) => Layout.GroundY(Mathf.Clamp(z, PlotMinZ, PlotMaxZ));
            var lowest = Mathf.Min(Ground(PlotMinZ), Ground(PlotMaxZ));
            var baseY = lowest - 0.6f;

            if (plotMesh != null) Destroy(plotMesh);
            plotMesh = MemberMeshes.BuildPlot(PlotMinX, PlotMaxX, PlotMinZ, PlotMaxZ, Ground, baseY);
            plotFilter.sharedMesh = plotMesh;
            if (plotCollider != null) plotCollider.sharedMesh = plotMesh;
            if (hologramPlot != null) hologramPlot.sharedMesh = plotMesh;

            // Firm ground: a compacted platform cut into the hillside under the house, a metre wider all round.
            if (platformFilter != null)
            {
                if (platformMesh != null) Destroy(platformMesh);
                var pad = new Rect(footprint.xMin - 1f, footprint.yMin - 1f, footprint.width + 2f, footprint.height + 2f);
                platformMesh = MemberMeshes.BuildPlatform(pad, Ground, 0.05f);
                platformFilter.sharedMesh = platformMesh;
            }
            // Props follow the slope; any standing where this design is built (bigger houses reach further) are cleared.
            var clearing = new Rect(footprint.xMin - 1.2f, footprint.yMin - 1.2f, footprint.width + 2.4f, footprint.height + 2.4f);
            foreach (var tree in trees)
            {
                var position = tree.localPosition;
                tree.localPosition = new Vector3(position.x, Ground(position.z), position.z);
                tree.gameObject.SetActive(!clearing.Contains(new Vector2(position.x, position.z)));
            }

            // Sit the bottom of the plot on the table surface.
            contentRestPosition = new Vector3(0f, -baseY, 0f);
            content.localPosition = contentRestPosition;
            SetScale(scaleDenominator);
        }

        /// <summary>Hologram view shows the hillside as a glowing contour grid; real view shows terraces and scenery.</summary>
        public void SetHologram(bool hologram)
        {
            if (realGround != null) realGround.SetActive(!hologram);
            if (hologramPlot != null) hologramPlot.gameObject.SetActive(hologram);
        }

        public void SetScale(int denominator)
        {
            ScaleDenominator = Mathf.Clamp(denominator, MinScale, MaxScale);
            transform.localScale = Vector3.one * WorldScale;
            // The hologram shader draws its grid in building metres, whatever the tabletop scale.
            Shader.SetGlobalFloat(WorldToModel, ScaleDenominator);
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
            if (platformMesh != null) Destroy(platformMesh);
        }
    }
}
