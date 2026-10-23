using System;
using System.Collections;
using System.IO;
using System.Linq;
using Fatiro.Bootstrap;
using Fatiro.Domain;
using Fatiro.States;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Fatiro.Tests
{
    /// <summary>
    /// Opt-in visual review: renders the main screens at phone resolution into PNG files.
    /// Run with -testFilter ScreenshotTour (needs graphics, so not with -nographics).
    /// Output folder: the FATIRO_SCREENSHOTS environment variable, or Screenshots/ in the project.
    /// </summary>
    [Explicit, Category("Screenshots")]
    public sealed class ScreenshotTour
    {
        private const int Width = 1080;
        private const int Height = 1920;

        [UnityTest]
        public IEnumerator CaptureMainScreens()
        {
            var folder = Environment.GetEnvironmentVariable("FATIRO_SCREENSHOTS");
            if (string.IsNullOrEmpty(folder)) folder = Path.Combine(Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(folder);

            SceneManager.LoadScene("Fatiro");
            AppBootstrap bootstrap = null;
            yield return WaitFor(() => (bootstrap = UnityEngine.Object.FindFirstObjectByType<AppBootstrap>()) != null && bootstrap.Simulation != null);
            var simulation = bootstrap.Simulation;
            var context = simulation.Context;

            var camera = context.AR.Camera;
            var target = new RenderTexture(Width, Height, 24);
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.30f, 0.33f, 0.30f);
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.1f;

            var profile = context.Data.Profiles.Create("Ingabire");
            context.Data.SelectProfile(profile);
            try
            {
                // Re-show the start screen so it picks up the profile.
                simulation.StartNewSession();
                simulation.BackToMenu();
                yield return Capture(camera, target, folder, "01-start");

                simulation.StartNewSession();
                yield return Capture(camera, target, folder, "02-setup");

                var design = context.Designs.First(d => d.DesignId == Blueprints.CarId);
                var setup = simulation.ConfirmSetup(design, 20, SoilType.Laterite, BuildMode.GuidedBuild);
                yield return WaitFor(() => setup.IsCompleted);
                yield return WaitFor(() => ((SimulationManager)simulation).State<PlacementState>().HasPose);
                yield return Capture(camera, target, folder, "03-placement");

                simulation.ConfirmPlacement();
                yield return WaitFor(() => simulation.CurrentStateId == SimulationStateId.Building);
                for (var i = 0; i < 2; i++) context.Build.TryPlace(context.Tasks.NextComponent());
                simulation.SelectTrayComponent(context.Tasks.NextComponent());
                yield return Capture(camera, target, folder, "04-building");

                while (!context.Tasks.IsComplete) context.Build.TryPlace(context.Tasks.NextComponent());
                simulation.OpenTestBench();
                yield return new WaitForSeconds(1.1f);
                yield return Capture(camera, target, folder, "05a-reveal");
                yield return WaitFor(() => ((SimulationManager)simulation).IsRevealing == false);
                yield return new WaitForSeconds(1.5f); // skin loads through Addressables
                yield return Capture(camera, target, folder, "05b-real");
                simulation.RunLoadTest(new LoadCaseConfig(LoadCaseKind.Seismic, 3f));
                yield return new WaitForSeconds(1.8f);
                yield return Capture(camera, target, folder, "05-load-test");
                yield return WaitFor(() => simulation.CurrentStateId == SimulationStateId.Reviewing);
                yield return new WaitForSeconds(0.5f);
                yield return Capture(camera, target, folder, "06-results");

                simulation.EndSession();
                yield return Capture(camera, target, folder, "07-completion");
                simulation.ReturnToMenu();

                // Each design finished, closer up: revealed as built, then as its hologram.
                foreach (var id in new[] { Blueprints.PantherId, Blueprints.ElephantId, Blueprints.CarId, Blueprints.BungalowId, Blueprints.TwoStoreyId, Blueprints.FlatsId })
                {
                    simulation.StartNewSession();
                    var finished = simulation.ConfirmSetup(context.Designs.First(d => d.DesignId == id), 20, SoilType.Laterite, BuildMode.LoadTest);
                    yield return WaitFor(() => finished.IsCompleted);
                    yield return WaitFor(() => ((SimulationManager)simulation).State<PlacementState>().HasPose);
                    context.Site.SetScale(32);
                    simulation.ConfirmPlacement();
                    yield return WaitFor(() => simulation.CurrentStateId == SimulationStateId.Reviewing);
                    yield return new WaitForSeconds(2.6f); // the fit-out loads through Addressables; the reveal plays
                    yield return Capture(camera, target, folder, $"08-{id}-real");
                    simulation.SetHologram(true);
                    yield return null;
                    yield return Capture(camera, target, folder, $"09-{id}-hologram");
                    simulation.EndSession();
                    simulation.ReturnToMenu();
                }
            }
            finally
            {
                context.Data.Profiles.Delete(profile.learnerId);
                camera.targetTexture = null;
                target.Release();
            }
        }

        private static IEnumerator Capture(Camera camera, RenderTexture target, string folder, string name)
        {
            // Two frames so layout groups and content size fitters settle.
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeout = 30f)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out.");
                yield return null;
            }
        }
    }
}
