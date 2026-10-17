using System.Collections.Generic;
using System.Text;
using Fatiro.Content;
using Fatiro.Core;
using Fatiro.Domain;
using Fatiro.States;
using UnityEngine;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>Shown while Addressable assets for a session are loading.</summary>
    public sealed class LoadingOverlay : UIScreen
    {
        public LoadingOverlay(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            UIKit.Panel(Root, Theme.Scrim, "Scrim", false).rectTransform.Stretch();
            var card = UIKit.Card(Root, Theme.Surface, 48);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760, 260);
            UIKit.Label(card.transform, "Loading the design…", Theme.Subheading, align: TextAnchor.MiddleCenter, style: FontStyle.Bold);
            UIKit.Label(card.transform, "Members, site and effects are streamed in with Addressables.", Theme.Small, Theme.Muted, TextAnchor.MiddleCenter);
        }
    }

    /// <summary>Short messages at the bottom of the screen. They are the visual twin of every sound cue (NFR-06).</summary>
    public sealed class ToastOverlay : UIScreen
    {
        private const float Lifetime = 2.8f;
        private readonly Queue<(string message, ToastKind kind)> queue = new Queue<(string, ToastKind)>();
        private Image card;
        private Text label;
        private float hideAt;

        public ToastOverlay(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            card = UIKit.Panel(Root, Theme.SurfaceRaised, "Toast");
            var rect = card.rectTransform;
            rect.anchorMin = new Vector2(0.06f, 0.5f);
            rect.anchorMax = new Vector2(0.94f, 0.5f);
            rect.sizeDelta = new Vector2(0, 150);
            rect.anchoredPosition = new Vector2(0, -80);
            card.raycastTarget = false;
            label = UIKit.Label(card.transform, string.Empty, Theme.Body, align: TextAnchor.MiddleCenter, style: FontStyle.Bold);
            label.rectTransform.Stretch(30, 10, 30, 10);
            card.gameObject.SetActive(false);

            Ui.Events.Subscribe<ToastRequested>(message => queue.Enqueue((message.Message, message.Kind)));
            Ui.Events.Subscribe<PlacementRejected>(message => queue.Enqueue(("× " + message.Reason, ToastKind.Error)));
            Ui.Events.Subscribe<StageCompleted>(message =>
            {
                if (!message.BuildComplete) queue.Enqueue(($"√ {EnumText.StageName(message.Stage)} stage complete", ToastKind.Success));
            });
        }

        public override void Tick()
        {
            if (card.gameObject.activeSelf && Time.unscaledTime > hideAt) card.gameObject.SetActive(false);
            if (card.gameObject.activeSelf || queue.Count == 0) return;

            var (message, kind) = queue.Dequeue();
            label.text = message;
            card.color = kind switch
            {
                ToastKind.Success => new Color(0.10f, 0.32f, 0.21f, 0.97f),
                ToastKind.Warning => new Color(0.40f, 0.31f, 0.08f, 0.97f),
                ToastKind.Error => new Color(0.42f, 0.12f, 0.13f, 0.97f),
                _ => Theme.SurfaceRaised
            };
            hideAt = Time.unscaledTime + (queue.Count > 1 ? Lifetime * 0.6f : Lifetime);
            card.gameObject.SetActive(true);
        }
    }

    /// <summary>Paused by the learner or by lost tracking (FR-05).</summary>
    public sealed class PausedOverlay : UIScreen
    {
        private Text message;

        public PausedOverlay(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            UIKit.Panel(Root, Theme.Scrim, "Scrim", false).rectTransform.Stretch();
            var card = UIKit.Card(Root, Theme.Surface, 56, 26);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = new Vector2(0.08f, 0.5f);
            rect.anchorMax = new Vector2(0.92f, 0.5f);
            rect.sizeDelta = new Vector2(0, 640);
            UIKit.Label(card.transform, "Paused", Theme.Heading, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            message = UIKit.Label(card.transform, string.Empty, Theme.Body, align: TextAnchor.MiddleCenter);
            UIKit.Size(message.gameObject, minHeight: 160);
            UIKit.Button(card.transform, "Resume", () => { Click(); Simulation.Resume(); });
            UIKit.Button(card.transform, "Main menu", () => { Click(); Time.timeScale = 1f; Simulation.ReturnToMenu(); }, ButtonStyle.Secondary);
        }

        protected override void OnShow()
        {
            message.text = Simulation.PausedByTracking
                ? "AR tracking was lost.\nMove the device slowly over the table until it reconnects."
                : "The simulation is paused. Your structure is safe.";
        }
    }

    /// <summary>Explains why the camera is needed when permission is denied, with a way to grant it (FR-01).</summary>
    public sealed class CameraPermissionOverlay : UIScreen
    {
        public CameraPermissionOverlay(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            UIKit.Panel(Root, Theme.Backdrop, "Backdrop", false).rectTransform.Stretch();
            var card = UIKit.Card(Root, Theme.Surface, 56, 26);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = new Vector2(0.06f, 0.5f);
            rect.anchorMax = new Vector2(0.94f, 0.5f);
            rect.sizeDelta = new Vector2(0, 900);
            UIKit.Label(card.transform, "Camera needed", Theme.Heading, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            var text = UIKit.Label(card.transform,
                "Fatiro AR uses the camera to find your table and anchor the hillside site on it, so you can walk around the house you build.\n\n" +
                "Nothing is recorded or uploaded: camera frames stay on this phone.", Theme.Body, align: TextAnchor.MiddleCenter);
            UIKit.Size(text.gameObject, minHeight: 380);
            UIKit.Button(card.transform, "Allow camera", () =>
            {
                Click();
                AR.CameraPermission.Request(granted => Ui.Events.Publish(new CameraPermissionResult(granted)));
            });
            UIKit.Button(card.transform, "Open app settings", () => { Click(); AR.CameraPermission.OpenAppSettings(); }, ButtonStyle.Secondary);

            Ui.Events.Subscribe<CameraPermissionResult>(message => SetVisible(!message.Granted));
        }

        public override void Tick()
        {
            // The learner may grant access from system settings and come back.
            if (AR.CameraPermission.IsGranted) SetVisible(false);
        }
    }

    /// <summary>Banner shown while a load test is running, with the colour legend (FR-17, NFR-06).</summary>
    public sealed class LoadTestBanner : UIScreen
    {
        private Text title;

        public LoadTestBanner(UIContext context) : base(context) { }

        protected override void OnBuild()
        {
            var card = UIKit.Card(Root, Theme.Surface, 28, 10);
            ((RectTransform)card.transform).Dock(TextAnchor.UpperCenter, 230, 40);
            title = UIKit.Label(card.transform, string.Empty, Theme.Subheading, Theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Label(card.transform, Legend(), Theme.Small, align: TextAnchor.MiddleCenter);
            Ui.Events.Subscribe<LoadTestStarted>(message => title.text = $"Testing: {LoadCaseFactory.Create(message.Config).Describe()}");
        }

        public static string Legend() =>
            $"{UIKit.Colour("■ under 70% safe", Theme.Success)}    {UIKit.Colour("■ 70–100% near limit", Theme.Warning)}    {UIKit.Colour("■ over 100% fails", Theme.Danger)}";
    }

    /// <summary>Developer overlay: frame rate, Addressables handles and pool usage (evidence for NFR-01).</summary>
    public sealed class DebugOverlay : UIScreen
    {
        private readonly IContentCatalogue catalogue;
        private readonly System.Func<IEnumerable<IPoolStats>> pools;
        private readonly StringBuilder builder = new StringBuilder();
        private Text text;
        private float smoothedDelta = 1f / 30f;
        private float nextRefresh;

        public DebugOverlay(UIContext context, IContentCatalogue catalogue, System.Func<IEnumerable<IPoolStats>> pools) : base(context)
        {
            this.catalogue = catalogue;
            this.pools = pools;
        }

        protected override void OnBuild()
        {
            var panel = UIKit.Panel(Root, new Color(0, 0, 0, 0.6f), "Debug");
            panel.raycastTarget = false;
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(12, 120);
            rect.sizeDelta = new Vector2(560, 470);
            text = UIKit.Label(panel.transform, string.Empty, 24);
            text.rectTransform.Stretch(18, 14, 18, 14);
            text.alignment = TextAnchor.UpperLeft;
        }

        public override void Tick()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.1f);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;

            builder.Clear();
            builder.AppendLine($"<b>{1f / smoothedDelta:0} fps</b>   state: {Simulation.CurrentStateId}");
            builder.AppendLine($"Addressables handles: {catalogue.ActiveHandleCount}");
            builder.AppendLine($"  app {catalogue.HandleCount(AssetScope.App)} · session {catalogue.HandleCount(AssetScope.Session)} · skin {catalogue.HandleCount(AssetScope.Skin)}");
            builder.AppendLine($"Members placed: {Session.Entities.Count}");
            builder.AppendLine("Pools (active / idle / created):");
            foreach (var pool in pools())
                builder.AppendLine($"  {pool.Name}: {pool.CountActive} / {pool.CountInactive} / {pool.TotalCreated}");
            text.text = builder.ToString();
        }
    }
}
