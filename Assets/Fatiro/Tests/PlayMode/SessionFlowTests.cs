using System;
using System.Collections;
using System.Linq;
using Fatiro.Bootstrap;
using Fatiro.Content;
using Fatiro.Domain;
using Fatiro.States;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Fatiro.Tests
{
    /// <summary>
    /// Drives a complete session through the real scene, the same way the UI does: setup, AR
    /// placement (editor floor fallback), drag-and-snap assembly with a sequencing mistake,
    /// undo/redo, a failing earthquake test, reinforcement, a passing re-test, saving and
    /// releasing the session's Addressables.
    /// </summary>
    public sealed class SessionFlowTests
    {
        private LearnerProfile profile;
        private ISimulationController simulation;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (simulation != null && profile != null) simulation.Context.Data.Profiles.Delete(profile.learnerId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CarSession_CollapsesThenSurvivesAfterReinforcing()
        {
            SceneManager.LoadScene("Fatiro");
            AppBootstrap bootstrap = null;
            yield return WaitFor(() => (bootstrap = UnityEngine.Object.FindFirstObjectByType<AppBootstrap>()) != null && bootstrap.Simulation != null, 30f);
            simulation = bootstrap.Simulation;
            var context = simulation.Context;
            Assert.That(simulation.CurrentStateId, Is.EqualTo(SimulationStateId.Start));
            Assert.That(context.Designs.Count, Is.EqualTo(3), "designs load from Addressables");

            profile = context.Data.Profiles.Create("PlayMode test");
            context.Data.SelectProfile(profile);

            // Setup
            simulation.StartNewSession();
            Assert.That(simulation.CurrentStateId, Is.EqualTo(SimulationStateId.Setup));
            var design = context.Designs.First(d => d.DesignId == Blueprints.CarId);
            var setup = simulation.ConfirmSetup(design, 20, SoilType.Laterite, BuildMode.GuidedBuild);
            yield return WaitFor(() => setup.IsCompleted, 20f);
            Assert.That(simulation.CurrentStateId, Is.EqualTo(SimulationStateId.Placement));
            Assert.That(context.Content.HandleCount(AssetScope.Session), Is.GreaterThan(0), "session assets loaded");

            // AR placement
            var manager = (SimulationManager)simulation;
            yield return WaitFor(() => manager.State<PlacementState>().HasPose, 5f);
            simulation.ConfirmPlacement();
            yield return WaitFor(() => simulation.CurrentStateId == SimulationStateId.Building, 5f);

            // A sequencing mistake: a roof panel during the foundation stage is rejected and logged.
            var roof = context.Blueprint.Get("CR-ROOF-REAR");
            DragOnto(roof);
            Assert.That(context.Entities.IsPlaced(roof.Id), Is.False);
            Assert.That(context.Record.placements.Last().result, Is.EqualTo("rejected"));

            // Assemble every required member by dragging its card onto its ghost.
            var guard = 0;
            while (!context.Tasks.IsComplete && guard++ < 100)
            {
                var next = context.Tasks.NextRequired();
                Assert.That(next, Is.Not.Null);
                DragOnto(next);
                Assert.That(context.Entities.IsPlaced(next.Id), Is.True, $"{next.DisplayName} should snap");
                yield return null;
            }
            Assert.That(context.Tasks.IsComplete, Is.True);
            Assert.That(context.Record.AcceptedPlacements, Is.EqualTo(context.Tasks.RequiredTotal));

            // Undo and redo the last action.
            simulation.Undo();
            Assert.That(context.Tasks.IsComplete, Is.False);
            simulation.Redo();
            Assert.That(context.Tasks.IsComplete, Is.True);

            // Earthquake: the unbraced downhill stilts fail.
            simulation.OpenTestBench();
            Assert.That(simulation.CurrentStateId, Is.EqualTo(SimulationStateId.Reviewing));
            Assert.That(simulation.Hologram, Is.False, "the test bench reveals the real structure");
            simulation.RunLoadTest(new LoadCaseConfig(LoadCaseKind.Seismic, 3f));
            Assert.That(simulation.CurrentStateId, Is.EqualTo(SimulationStateId.LoadTesting));
            yield return WaitFor(() => simulation.CurrentStateId == SimulationStateId.Reviewing, 15f);
            Assert.That(context.LoadTests.LastResult.Survived, Is.False);
            Assert.That(context.Entities.Member("CR-STILT-L2").IsFailed, Is.True);

            // Reinforce with the optional downhill braces and re-test.
            simulation.ModifyStructure();
            Assert.That(simulation.CurrentStateId, Is.EqualTo(SimulationStateId.Building));
            Assert.That(simulation.Hologram, Is.True, "modifying goes back to the hologram");
            Assert.That(context.Entities.Member("CR-STILT-L2").IsFailed, Is.False, "results reset for modification");
            foreach (var id in new[] { "CR-BRACE-L1", "CR-BRACE-R1", "CR-SWAY-1", "CR-SWAY-2" }) DragOnto(context.Blueprint.Get(id));
            simulation.OpenTestBench();
            simulation.RunLoadTest(new LoadCaseConfig(LoadCaseKind.Seismic, 3f));
            yield return WaitFor(() => simulation.CurrentStateId == SimulationStateId.Reviewing, 15f);
            Assert.That(context.LoadTests.LastResult.Survived, Is.True);
            Assert.That(context.Record.loadTests.Count, Is.EqualTo(2));

            // Finish: the session is scored and saved, then its Addressables are released.
            simulation.EndSession();
            Assert.That(simulation.CurrentStateId, Is.EqualTo(SimulationStateId.Ended));
            var saved = context.Data.Sessions.ListFor(profile.learnerId);
            Assert.That(saved.Count, Is.EqualTo(1));
            Assert.That(saved[0].Score, Is.GreaterThan(0));

            simulation.ReturnToMenu();
            Assert.That(simulation.CurrentStateId, Is.EqualTo(SimulationStateId.Start));
            Assert.That(context.Content.HandleCount(AssetScope.Session), Is.EqualTo(0));
            Assert.That(context.Content.HandleCount(AssetScope.Skin), Is.EqualTo(0));
        }

        /// <summary>Simulates dragging a tray card and releasing it over the member's target position.</summary>
        private void DragOnto(MemberSpec spec)
        {
            var context = simulation.Context;
            var geometry = context.Entities.Geometry(spec);
            var centre = new Vector3((geometry.A.X + geometry.B.X) * 0.5f, (geometry.A.Y + geometry.B.Y) * 0.5f, (geometry.A.Z + geometry.B.Z) * 0.5f);
            var screen = (Vector2)context.AR.Camera.WorldToScreenPoint(context.Site.StructureRoot.TransformPoint(centre));
            simulation.BeginMemberDrag(spec);
            simulation.DragMember(screen);
            simulation.EndMemberDrag(screen);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out after {timeoutSeconds} s.");
                yield return null;
            }
        }
    }
}
