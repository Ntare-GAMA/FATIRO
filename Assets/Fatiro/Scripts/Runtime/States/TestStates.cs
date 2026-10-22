using System.Collections.Generic;
using Fatiro.Domain;
using Fatiro.Input;
using Fatiro.World;
using UnityEngine;

namespace Fatiro.States
{
    /// <summary>
    /// The test bench: choose a load case and magnitude, inspect members, compare with the last
    /// run, then modify and re-test or finish the session.
    /// </summary>
    public sealed class ReviewingState : SimulationState
    {
        public ReviewingState(SimulationManager manager) : base(manager) { }

        public override SimulationStateId Id => SimulationStateId.Reviewing;
        public override bool RequiresTracking => true;
        public override bool CanEndSession => true;

        public override void RunLoadTest(LoadCaseConfig config) => Manager.BeginLoadTest(config);

        public override void ModifyStructure()
        {
            Context.Entities.ClearResults();
            Manager.SetSkinVisible(true);
            Manager.ChangeState(SimulationStateId.Building);
        }

        public override void Reposition() => Manager.BeginReposition();

        public override void HandleGesture(in Gesture gesture)
        {
            switch (gesture.Type)
            {
                case GestureType.Tap:
                    var ray = Context.AR.Camera.ScreenPointToRay(gesture.Position);
                    var view = Physics.Raycast(ray, out var hit, 20f) ? hit.collider.GetComponentInParent<MemberView>() : null;
                    Manager.SelectMember(view != null && view.Visual != MemberVisual.Ghost ? view.MemberId : null);
                    break;
                case GestureType.Pinch:
                    Context.Site.Pinch(gesture.Scale);
                    break;
                case GestureType.Twist:
                    Context.Site.Twist(gesture.TwistDegrees);
                    break;
            }
        }
    }

    /// <summary>
    /// Runs one load test and plays it out: shaking, rain or wind, members turning green/amber/red,
    /// then failures collapsing wave by wave (FR-15 to FR-19). Returns to the test bench when done.
    /// </summary>
    public sealed class LoadTestingState : SimulationState
    {
        private const float LeadIn = 1.2f;
        private const float WaveGap = 0.5f;

        private readonly List<float> waveTimes = new List<float>();
        private float elapsed;
        private float duration;
        private int nextWave;

        public LoadTestingState(SimulationManager manager) : base(manager) { }

        public override SimulationStateId Id => SimulationStateId.LoadTesting;
        public override bool RequiresTracking => true;

        public LoadTestResult Result { get; private set; }

        public override void Enter(bool resumed)
        {
            if (resumed) return;

            var config = Manager.PendingTest;
            Context.Entities.ClearResults();
            Result = Context.LoadTests.Run(config, Context.Setup.Site);
            // Hide the decorative skin so the coloured members underneath can be read.
            Manager.SetSkinVisible(false);
            PlayLoadEffect(config);

            waveTimes.Clear();
            var lastWave = -1;
            foreach (var failed in Result.Failed)
            {
                var delay = LeadIn + failed.FailureWave * WaveGap;
                Context.Entities.View(failed.MemberId)?.PlayFailure(delay);
                if (failed.FailureWave != lastWave)
                {
                    waveTimes.Add(delay);
                    lastWave = failed.FailureWave;
                }
            }

            elapsed = 0f;
            nextWave = 0;
            duration = Mathf.Max(3f, LeadIn + waveTimes.Count * WaveGap + 1.6f);
        }

        public override void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            while (nextWave < waveTimes.Count && elapsed >= waveTimes[nextWave])
            {
                Context.Audio?.PlayFailureWave(nextWave);
                nextWave++;
            }

            if (elapsed < duration) return;
            if (!Result.Survived) Context.Audio?.StopAmbience();
            Manager.ChangeState(SimulationStateId.Reviewing);
        }

        private void PlayLoadEffect(LoadCaseConfig config)
        {
            switch (config.Kind)
            {
                case LoadCaseKind.Seismic:
                    Context.Site.Shake(config.Magnitude, 2.6f);
                    break;
                case LoadCaseKind.Wind:
                    Context.Site.Shake(config.Magnitude / 150f, 2.2f);
                    break;
                case LoadCaseKind.Rain:
                    StartRain(config.Magnitude);
                    break;
            }
        }

        /// <summary>Drops fall on every roof panel and run to the drain (or off the downhill edge).</summary>
        private void StartRain(float millimetresPerHour)
        {
            var roofPoints = new List<Vector3>();
            Vector3? drain = null;
            foreach (var member in Context.Entities.Graph.Members)
            {
                var view = Context.Entities.View(member.Id);
                if (view == null) continue;
                if (member.Kind == MemberKind.Panel && member.Spec.IsRoof) roofPoints.Add(view.BodyRenderer.bounds.center);
                if (member.Kind == MemberKind.Drain)
                    drain = Context.Site.StructureRoot.TransformPoint(member.Geometry.B.X, member.Geometry.B.Y, member.Geometry.B.Z);
            }

            if (roofPoints.Count == 0) return;
            var fallback = Context.Site.StructureRoot.TransformPoint(0f, Context.Site.Layout.GroundY(6f), 6f);
            Context.Effects.SetWorldScale(Context.Site.WorldScale);
            Context.Effects.StartRain(roofPoints, drain ?? fallback, millimetresPerHour, 3.2f);
        }
    }
}
