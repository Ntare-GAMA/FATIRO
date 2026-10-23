using Fatiro.Build;
using Fatiro.Content;
using Fatiro.Core;
using Fatiro.Domain;
using Fatiro.Simulation;
using Fatiro.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Fatiro.World
{
    /// <summary>
    /// Holographic labels floating over the model in AR: the design and its assembly progress,
    /// the latest load stress, a leader line down to the structure and glowing rings round its base.
    /// Listens to the event bus only; it never changes the simulation.
    /// </summary>
    public sealed class WorldHud : MonoBehaviour
    {
        private const float PixelsPerMetre = 130f;

        private IEventBus events;
        private HouseDesign design;
        private TaskManager tasks;
        private LoadTestManager loadTests;
        private Camera viewer;
        private Transform board;
        private Text title;
        private Text status;
        private LineRenderer[] rings;
        private float pulse;
        private bool revealed;

        public static WorldHud Create(Transform structureRoot, SiteLayout layout, Rect footprint, HouseDesign design, Material lineMaterial,
            Camera viewer, IEventBus events, TaskManager tasks, LoadTestManager loadTests)
        {
            var root = new GameObject("World HUD");
            root.transform.SetParent(structureRoot, false);
            var hud = root.AddComponent<WorldHud>();
            hud.events = events;
            hud.design = design;
            hud.tasks = tasks;
            hud.loadTests = loadTests;
            hud.viewer = viewer;
            hud.Build(layout, footprint, lineMaterial);
            hud.Refresh();
            events.Subscribe<BuildProgressChanged>(hud.OnProgress);
            events.Subscribe<LoadTestCompleted>(hud.OnLoadTest);
            return hud;
        }

        private void OnDestroy()
        {
            if (events == null) return;
            events.Unsubscribe<BuildProgressChanged>(OnProgress);
            events.Unsubscribe<LoadTestCompleted>(OnLoadTest);
        }

        /// <summary>Set once the hologram has turned real, cleared when the learner goes back to modify.</summary>
        public bool Revealed
        {
            get => revealed;
            set
            {
                revealed = value;
                Refresh();
            }
        }

        private void OnProgress(BuildProgressChanged message) => Refresh();
        private void OnLoadTest(LoadTestCompleted message) => Refresh();

        private void Build(SiteLayout layout, Rect footprint, Material lineMaterial)
        {
            // Highest point of the design, so the label floats just above it.
            var top = 0f;
            foreach (var spec in design.Blueprint.Members)
                top = Mathf.Max(top, Mathf.Max(spec.A.Position.Y, Mathf.Max(spec.B.Position.Y, spec.CrownHeight)));
            foreach (var shell in design.Blueprint.Shells)
                foreach (var station in shell.Stations)
                    top = Mathf.Max(top, station.TopY);

            var canvasObject = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            board = canvasObject.transform;
            board.SetParent(transform, false);
            board.localPosition = new Vector3(footprint.center.x, top + 2.2f, footprint.center.y);
            board.localScale = Vector3.one / PixelsPerMetre;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)board;
            rect.sizeDelta = new Vector2(1100, 230);

            var panel = UIKit.Panel(board, new Color(0.02f, 0.08f, 0.11f, 0.72f), "Glass");
            panel.rectTransform.Stretch();
            UIKit.Outline(panel, Theme.Primary);
            UIKit.Corners(panel, Theme.Accent);
            title = UIKit.Label(panel.transform, string.Empty, 72, Theme.Primary, TextAnchor.UpperLeft, FontStyle.Bold);
            title.rectTransform.Stretch(40, 22, 40, 100);
            status = UIKit.Label(panel.transform, string.Empty, 50, Theme.Accent, TextAnchor.LowerLeft, FontStyle.Bold);
            status.rectTransform.Stretch(40, 120, 40, 24);

            // Leader line from the label down towards the structure.
            var leader = UIKit.Panel(board, Theme.Primary, "Leader", false);
            leader.rectTransform.anchorMin = leader.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            leader.rectTransform.pivot = new Vector2(0.5f, 1f);
            leader.rectTransform.sizeDelta = new Vector2(5f, 260f);

            // Glowing rings round the footprint, following the slope.
            var radius = Mathf.Max(footprint.width, footprint.height) * 0.5f + 1.2f;
            rings = new[]
            {
                Ring("Inner ring", layout, footprint.center, radius, 360f, Theme.Primary, lineMaterial),
                Ring("Outer arc", layout, footprint.center, radius + 0.9f, 250f, Theme.Accent, lineMaterial)
            };
        }

        private LineRenderer Ring(string name, SiteLayout layout, Vector2 centre, float radius, float degrees, Color colour, Material material)
        {
            const int segments = 72;
            var ring = new GameObject(name).AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.useWorldSpace = false;
            ring.loop = degrees >= 360f;
            ring.sharedMaterial = material;
            ring.widthMultiplier = 0.0035f;
            ring.startColor = ring.endColor = colour;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var count = ring.loop ? segments : segments + 1;
            ring.positionCount = count;
            for (var i = 0; i < count; i++)
            {
                var angle = Mathf.Deg2Rad * (degrees * i / segments + 135f);
                var x = centre.x + Mathf.Cos(angle) * radius;
                var z = centre.y + Mathf.Sin(angle) * radius;
                ring.SetPosition(i, new Vector3(x, layout.GroundY(z) + 0.12f, z));
            }
            return ring;
        }

        private void Refresh()
        {
            if (title == null) return;
            var percent = Mathf.RoundToInt(tasks.Progress * 100f);
            title.text = revealed
                ? $"{design.DisplayName.ToUpperInvariant()} {design.DesignId}  |  COMPLETE"
                : $"{design.DisplayName.ToUpperInvariant()} {design.DesignId}  |  ASSEMBLY {percent}%";

            var result = loadTests.LastResult;
            if (result == null)
            {
                status.text = "LOAD STRESS: NOT TESTED";
                status.color = Theme.Muted;
                return;
            }

            var peak = result.PeakUtilisation;
            var verdict = peak > 1f ? "CRITICAL" : peak >= 0.7f ? "HIGH" : "OPTIMAL";
            status.text = $"LOAD STRESS: {verdict}  ·  PEAK {peak * 100f:0}%  ·  {EnumText.LoadCaseName(result.LoadCase.Kind).ToUpperInvariant()}";
            status.color = peak > 1f ? Theme.Danger : peak >= 0.7f ? Theme.Warning : Theme.Accent;
        }

        private void LateUpdate()
        {
            if (viewer == null || board == null) return;
            // Turn the label to face the learner, staying upright.
            var toViewer = board.position - viewer.transform.position;
            toViewer.y = 0f;
            if (toViewer.sqrMagnitude > 1e-6f) board.rotation = Quaternion.LookRotation(toViewer.normalized, Vector3.up);

            pulse += Time.deltaTime;
            var glow = 0.65f + 0.35f * Mathf.Sin(pulse * 2.4f);
            foreach (var ring in rings)
            {
                var colour = ring.startColor;
                colour.a = glow;
                ring.startColor = ring.endColor = colour;
            }
        }
    }
}
