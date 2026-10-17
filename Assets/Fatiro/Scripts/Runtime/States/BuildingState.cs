using Fatiro.Core;
using Fatiro.Domain;
using Fatiro.Input;
using Fatiro.World;
using UnityEngine;

namespace Fatiro.States
{
    /// <summary>
    /// Assembly. The selected member is shown as a glowing ghost outline at its target node.
    /// Dragging a card from the tray and releasing within 5 cm (real-world) of the ghost snaps it
    /// in place (FR-10); tapping the ghost places it too. Tapping a placed member inspects it (FR-20).
    /// </summary>
    public sealed class BuildingState : SimulationState
    {
        /// <summary>Snap distance in real-world metres (FR-10).</summary>
        public const float SnapRadius = 0.05f;

        private MemberSpec selected;
        private MemberView ghost;
        private bool dragging;

        public BuildingState(SimulationManager manager) : base(manager) { }

        public override SimulationStateId Id => SimulationStateId.Building;
        public override bool RequiresTracking => true;
        public override bool CanEditStructure => true;
        public override bool CanEndSession => true;

        public MemberSpec Selected => selected;

        public override void Enter(bool resumed)
        {
            Context.Events.Subscribe<BuildProgressChanged>(OnProgressChanged);
            Context.Events.Subscribe<PlacementRejected>(OnRejected);
            if (!resumed || selected == null) SelectDefault();
        }

        public override void Exit()
        {
            Context.Events.Unsubscribe<BuildProgressChanged>(OnProgressChanged);
            Context.Events.Unsubscribe<PlacementRejected>(OnRejected);
            dragging = false;
            if (Manager.TransitionTarget != SimulationStateId.Paused) Select(null);
        }

        public override void SelectTrayMember(MemberSpec spec) => Select(spec);

        public override void BeginMemberDrag(MemberSpec spec)
        {
            Select(spec);
            dragging = true;
        }

        public override void DragMember(Vector2 screenPosition)
        {
            if (!dragging || ghost == null) return;
            ghost.ShowAsGhost(IsNearGhost(screenPosition));
        }

        public override void EndMemberDrag(Vector2 screenPosition)
        {
            if (!dragging) return;
            dragging = false;
            if (ghost == null) return;

            if (IsNearGhost(screenPosition))
            {
                TryPlaceSelected();
            }
            else
            {
                ghost.ShowAsGhost();
                Toast("Returned to the tray. Release it on the glowing outline.", ToastKind.Info);
            }
        }

        public override void HandleGesture(in Gesture gesture)
        {
            switch (gesture.Type)
            {
                case GestureType.Tap:
                    HandleTap(gesture.Position);
                    break;
                case GestureType.Pinch:
                    Context.Site.Pinch(gesture.Scale);
                    break;
                case GestureType.Twist:
                    Context.Site.Twist(gesture.TwistDegrees);
                    break;
            }
        }

        public override void OpenTestBench()
        {
            if (!Context.Tasks.IsComplete && Context.Tasks.Mode == BuildMode.GuidedBuild)
            {
                Toast("Finish every stage before load testing.", ToastKind.Warning);
                return;
            }
            Manager.ChangeState(SimulationStateId.Reviewing);
        }

        public override void Reposition() => Manager.BeginReposition();

        private void HandleTap(Vector2 screenPosition)
        {
            var ray = Context.AR.Camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit, 20f))
            {
                var view = hit.collider.GetComponentInParent<MemberView>();
                if (view != null && view == ghost)
                {
                    TryPlaceSelected();
                    return;
                }
                if (view != null && view.Visual != MemberVisual.Ghost)
                {
                    Manager.SelectMember(view.MemberId);
                    return;
                }
            }

            if (ghost != null && IsNearGhost(screenPosition))
            {
                TryPlaceSelected();
                return;
            }
            Manager.SelectMember(null);
        }

        private void TryPlaceSelected()
        {
            if (selected == null) return;
            var spec = selected;
            var centre = ghost != null ? ghost.BodyRenderer.bounds.center : Context.Site.transform.position;
            if (!Context.Build.TryPlace(spec)) return;

            var material = Context.Entities.Material(Context.Entities.Member(spec.Id).Material.Id);
            Context.Effects.Spark(centre, material != null ? material.Color : Color.white);
            // OnProgressChanged picks the next member.
        }

        private void OnProgressChanged(BuildProgressChanged message)
        {
            if (selected != null && Context.Tasks.IsPlaced(selected.Id)) SelectDefault();
            else if (selected == null) SelectDefault();
        }

        private void OnRejected(PlacementRejected message)
        {
            if (ghost != null) ghost.ShowAsGhost();
        }

        /// <summary>Guided Build highlights the next required member; Free Build waits for the learner.</summary>
        private void SelectDefault()
        {
            Select(Context.Tasks.Mode == BuildMode.GuidedBuild ? Context.Tasks.NextRequired() : null);
        }

        private void Select(MemberSpec spec)
        {
            if (ghost != null)
            {
                Context.Views.ReleaseGhost(ghost);
                ghost = null;
            }

            selected = spec;
            if (spec != null && !Context.Entities.IsPlaced(spec.Id))
                ghost = Context.Views.SpawnGhost(spec, Context.Entities.Geometry(spec), Context.Site.StructureRoot);
            Context.Events.Publish(new TraySelectionChanged(spec));
        }

        /// <summary>True when the finger's ray passes within the snap radius of the ghost.</summary>
        private bool IsNearGhost(Vector2 screenPosition)
        {
            if (ghost == null) return false;
            var ray = Context.AR.Camera.ScreenPointToRay(screenPosition);
            var bounds = ghost.BodyRenderer.bounds;
            bounds.Expand(SnapRadius * 2f);
            return bounds.IntersectRay(ray);
        }
    }
}
