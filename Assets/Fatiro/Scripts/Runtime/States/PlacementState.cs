using Fatiro.Core;
using Fatiro.Input;
using UnityEngine;

namespace Fatiro.States
{
    /// <summary>
    /// Scan for a horizontal plane, then position the site (FR-02 to FR-04). The preview follows
    /// the centre of the screen until the learner taps; after that a tap or drag moves it, pinch
    /// scales and twist rotates it. Confirming anchors it to the room.
    /// </summary>
    public sealed class PlacementState : SimulationState
    {
        private bool followCentre;
        private bool confirming;

        public PlacementState(SimulationManager manager) : base(manager) { }

        public override SimulationStateId Id => SimulationStateId.Placement;
        public override bool RequiresTracking => true;

        /// <summary>True once the site has been put on a detected surface.</summary>
        public bool HasPose { get; private set; }

        public override void Enter(bool resumed)
        {
            if (resumed) return;
            confirming = false;
            Context.AR.BeginScanning();

            // A site that was placed before (reposition) keeps its pose until the learner moves it.
            HasPose = Manager.IsRepositioning;
            followCentre = !HasPose;
            if (!HasPose) Context.Site.gameObject.SetActive(false);
        }

        public override void Tick(float deltaTime)
        {
            if (!followCentre || confirming) return;
            var camera = Context.AR.Camera;
            var centre = new Vector2(camera.pixelWidth * 0.5f, camera.pixelHeight * 0.45f);
            if (Context.AR.TryGetPlacementPose(centre, out var pose)) MoveSiteTo(pose);
        }

        public override void HandleGesture(in Gesture gesture)
        {
            if (confirming) return;
            switch (gesture.Type)
            {
                case GestureType.Tap:
                case GestureType.Drag:
                    if (Context.AR.TryGetPlacementPose(gesture.Position, out var pose))
                    {
                        followCentre = false;
                        MoveSiteTo(pose, keepRotation: gesture.Type == GestureType.Drag || HasPose);
                    }
                    else if (gesture.Type == GestureType.Tap)
                    {
                        Toast("Tap on the grid of a detected surface.", ToastKind.Warning);
                    }
                    break;
                case GestureType.Pinch:
                    if (HasPose) Context.Site.Pinch(gesture.Scale);
                    break;
                case GestureType.Twist:
                    if (HasPose) Context.Site.Twist(gesture.TwistDegrees);
                    break;
            }
        }

        public override async void ConfirmPlacement()
        {
            if (!HasPose)
            {
                Toast("Point at a table and tap the grid to place the site first.", ToastKind.Warning);
                return;
            }
            if (confirming) return;
            confirming = true;

            var site = Context.Site.transform;
            var anchor = await Context.AR.CreateSiteAnchorAsync(new Pose(site.position, site.rotation));
            if (Manager.CurrentStateId != SimulationStateId.Placement) return;

            site.SetParent(anchor, true);
            Context.AR.StopScanning();
            Context.Events.Publish(new SitePlaced(site));
            Manager.OnSiteAnchored();
        }

        private void MoveSiteTo(Pose pose, bool keepRotation = false)
        {
            var site = Context.Site.transform;
            if (!Context.Site.gameObject.activeSelf) Context.Site.gameObject.SetActive(true);
            site.position = pose.position;
            if (!keepRotation) site.rotation = pose.rotation;
            HasPose = true;
        }
    }
}
