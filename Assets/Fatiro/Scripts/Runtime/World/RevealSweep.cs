using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fatiro.World
{
    /// <summary>
    /// The finished hologram turns real: a glowing scan line rises through the structure and every
    /// member it passes changes from light into its building material. The ground, then the skin,
    /// follow as the line climbs. Complete() skips to the end (for example when a test starts).
    /// </summary>
    public sealed class RevealSweep : MonoBehaviour
    {
        private const float Duration = 2.2f;
        private const float Margin = 0.8f;
        private const float SkinAt = 0.6f;

        private readonly List<(MemberView view, MemberSurface surface, float height)> pending =
            new List<(MemberView, MemberSurface, float)>();
        private LineRenderer line;
        private Rect footprint;
        private float bottom;
        private float top;
        private float elapsed;
        private Action revealSkin;
        private Action finished;

        public static RevealSweep Begin(Transform structureRoot, Rect footprint, IEnumerable<(MemberView view, MemberSurface surface)> members,
            Material lineMaterial, Action revealSkin, Action finished)
        {
            var sweep = new GameObject("Reveal sweep").AddComponent<RevealSweep>();
            sweep.transform.SetParent(structureRoot, false);
            sweep.footprint = footprint;
            sweep.revealSkin = revealSkin;
            sweep.finished = finished;

            // Each member turns real when the line reaches its lowest point (structure-local metres).
            sweep.bottom = float.MaxValue;
            sweep.top = float.MinValue;
            foreach (var (view, surface) in members)
            {
                var bounds = view.BodyRenderer.bounds;
                var low = structureRoot.InverseTransformPoint(bounds.min).y;
                var high = structureRoot.InverseTransformPoint(bounds.max).y;
                sweep.pending.Add((view, surface, low));
                sweep.bottom = Mathf.Min(sweep.bottom, low);
                sweep.top = Mathf.Max(sweep.top, high);
            }
            if (sweep.pending.Count == 0) sweep.bottom = sweep.top = 0f;
            sweep.pending.Sort((a, b) => a.height.CompareTo(b.height));
            sweep.CreateLine(lineMaterial);
            return sweep;
        }

        public void Complete()
        {
            elapsed = Duration;
            Step();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Step();
        }

        private void Step()
        {
            var t = Mathf.Clamp01(elapsed / Duration);
            var height = Mathf.Lerp(bottom - 0.2f, top + 0.2f, 1f - (1f - t) * (1f - t));
            PlaceLine(height);

            while (pending.Count > 0 && (pending[0].height <= height || t >= 1f))
            {
                var (view, surface, _) = pending[0];
                pending.RemoveAt(0);
                if (view == null) continue;
                view.ChangeSurface(surface);
                view.Materialize();
            }

            if (revealSkin != null && t >= SkinAt)
            {
                revealSkin();
                revealSkin = null;
            }

            if (t < 1f) return;
            var done = finished;
            finished = null;
            Destroy(gameObject);
            done?.Invoke();
        }

        private void CreateLine(Material material)
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.sharedMaterial = material;
            line.widthMultiplier = 0.006f;
            line.startColor = line.endColor = new Color(0.79f, 0.98f, 0.30f, 1f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void PlaceLine(float height)
        {
            if (line == null) return;
            var xMin = footprint.xMin - Margin;
            var xMax = footprint.xMax + Margin;
            var zMin = footprint.yMin - Margin;
            var zMax = footprint.yMax + Margin;
            line.SetPosition(0, new Vector3(xMin, height, zMin));
            line.SetPosition(1, new Vector3(xMax, height, zMin));
            line.SetPosition(2, new Vector3(xMax, height, zMax));
            line.SetPosition(3, new Vector3(xMin, height, zMax));
        }
    }
}
