using System.Collections.Generic;
using Fatiro.Audio;
using Fatiro.Content;
using Fatiro.Core;
using Fatiro.States;
using UnityEngine;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>
    /// Builds the canvas and every screen, then shows the right ones for the current state.
    /// Screens send requests to the simulation controller and listen to the event bus.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        private readonly List<UIScreen> screens = new List<UIScreen>();
        private UIContext context;
        private StartScreen start;
        private SetupScreen setup;
        private PlacementHud placement;
        private BuildHud build;
        private InspectorPanel inspector;
        private TestBenchPanel testBench;
        private LoadTestBanner banner;
        private PausedOverlay paused;
        private CompletionScreen completion;
        private HistoryScreen history;
        private LoadingOverlay loading;
        private ToastOverlay toasts;
        private DebugOverlay debug;
        private CameraPermissionOverlay camera;
        private bool showingHistory;

        public void Initialise(ISimulationController simulation, IEventBus events, AudioManager audio, IContentCatalogue catalogue,
            System.Func<IEnumerable<IPoolStats>> pools)
        {
            var canvasObject = new GameObject("UI Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = Screen.width > Screen.height ? 1f : 0f;
            canvasObject.AddComponent<GraphicRaycaster>();

            // Keep content clear of notches and the navigation bar.
            var safeArea = UIKit.Rect("Safe area", canvasObject.transform).Stretch();
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            context = new UIContext { Simulation = simulation, Events = events, Audio = audio, Canvas = safeArea };
            var manager = (SimulationManager)simulation;

            start = Add(new StartScreen(context, () => ShowHistory(true)));
            setup = Add(new SetupScreen(context));
            placement = Add(new PlacementHud(context, manager.State<PlacementState>()));
            build = Add(new BuildHud(context));
            testBench = Add(new TestBenchPanel(context));
            banner = Add(new LoadTestBanner(context));
            inspector = Add(new InspectorPanel(context));
            completion = Add(new CompletionScreen(context));
            history = Add(new HistoryScreen(context, () => ShowHistory(false)));
            paused = Add(new PausedOverlay(context));
            loading = Add(new LoadingOverlay(context));
            debug = Add(new DebugOverlay(context, catalogue, pools));
            camera = Add(new CameraPermissionOverlay(context));
            toasts = Add(new ToastOverlay(context));
            toasts.SetVisible(true);

            start.DebugToggled += RefreshDebug;
            events.Subscribe<StateChanged>(_ => Refresh());
            Refresh();
            RefreshDebug();
        }

        private void Update()
        {
            if (context == null) return;
            foreach (var screen in screens)
                if (screen.Visible) UIKit.Safe(screen.Tick);
            loading.SetVisible(context.Simulation.IsBusy);
        }

        private T Add<T>(T screen) where T : UIScreen
        {
            screen.Build(context.Canvas);
            screens.Add(screen);
            return screen;
        }

        private void ShowHistory(bool show)
        {
            showingHistory = show;
            Refresh();
        }

        private void RefreshDebug() => debug.SetVisible(context.Session.Data.Settings.ShowDebugOverlay);

        private void Refresh()
        {
            var state = context.Simulation.CurrentStateId;
            if (state != SimulationStateId.Start) showingHistory = false;

            start.SetVisible(state == SimulationStateId.Start && !showingHistory);
            history.SetVisible(state == SimulationStateId.Start && showingHistory);
            setup.SetVisible(state == SimulationStateId.Setup);
            placement.SetVisible(state == SimulationStateId.Placement);
            build.SetVisible(state == SimulationStateId.Building);
            testBench.SetVisible(state == SimulationStateId.Reviewing);
            banner.SetVisible(state == SimulationStateId.LoadTesting);
            paused.SetVisible(state == SimulationStateId.Paused);
            completion.SetVisible(state == SimulationStateId.Ended);
            if (state != SimulationStateId.Building && state != SimulationStateId.Reviewing) inspector.SetVisible(false);

            // Keep overlays above screens.
            inspector.Root.SetAsLastSibling();
            paused.Root.SetAsLastSibling();
            loading.Root.SetAsLastSibling();
            debug.Root.SetAsLastSibling();
            camera.Root.SetAsLastSibling();
            toasts.Root.SetAsLastSibling();
        }
    }
}
