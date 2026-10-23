using System.Collections.Generic;
using Fatiro.Core;
using Fatiro.Domain;
using Fatiro.Input;
using Fatiro.World;
using UnityEngine;

namespace Fatiro.States
{
    /// <summary>
    /// Assembly, component by component (fast build). The selected component glows as a ghost over
    /// the faint whole-house blueprint. Dragging its card from the tray and releasing within 5 cm
    /// (real-world) of the ghost builds it (FR-10); tapping the ghost builds it too. Its pieces then
    /// rise one after another. Tapping a built member inspects it (FR-20).
    /// </summary>
    public sealed class BuildingState : SimulationState
    {
        /// <summary>Snap distance in real-world metres (FR-10).</summary>
        public const float SnapRadius = 0.05f;

        /// <summary>Delay between the pieces of a component materialising, so the crew's work reads as a sequence.</summary>
        public const float PieceInterval = 0.07f;

        private BuildComponent selected;
        private readonly List<MemberView> ghosts = new List<MemberView>();
        private readonly List<MemberView> blueprint = new List<MemberView>();
        private bool dragging;

        public BuildingState(SimulationManager manager) : base(manager) { }

        public override SimulationStateId Id => SimulationStateId.Building;
        public override bool RequiresTracking => true;
        public override bool CanEditStructure => true;
        public override bool CanEndSession => true;

        public BuildComponent Selected => selected;

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
            if (Manager.TransitionTarget != SimulationStateId.Paused)
            {
                Select(null);
                ClearBlueprint();
            }
        }

        public override void SelectTrayComponent(BuildComponent component) => Select(component);

        public override void BeginComponentDrag(BuildComponent component)
        {
            Select(component);
            dragging = true;
        }

        public override void DragComponent(Vector2 screenPosition)
        {
            if (!dragging || ghosts.Count == 0) return;
            var ready = IsNearGhost(screenPosition);
            foreach (var ghost in ghosts) ghost.ShowAsGhost(ready);
        }

        public override void EndComponentDrag(Vector2 screenPosition)
        {
            if (!dragging) return;
            dragging = false;
            if (ghosts.Count == 0) return;

            if (IsNearGhost(screenPosition))
            {
                TryBuildSelected();
            }
            else
            {
                foreach (var ghost in ghosts) ghost.ShowAsGhost();
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
            Manager.RevealReal();
        }

        public override void Reposition() => Manager.BeginReposition();

        private void HandleTap(Vector2 screenPosition)
        {
            // The glowing outline wins: interior components (partitions, floors) sit behind walls that are
            // already built, so a tap that passes through the outline anywhere builds it.
            var ray = Context.AR.Camera.ScreenPointToRay(screenPosition);
            var hits = Physics.RaycastAll(ray, 20f);
            foreach (var hit in hits)
            {
                var view = hit.collider.GetComponentInParent<MemberView>();
                if (view != null && ghosts.Contains(view))
                {
                    TryBuildSelected();
                    return;
                }
            }

            if (ghosts.Count > 0 && IsNearGhost(screenPosition))
            {
                TryBuildSelected();
                return;
            }

            // Otherwise a tap inspects the nearest built member.
            MemberView nearest = null;
            var nearestDistance = float.MaxValue;
            foreach (var hit in hits)
            {
                var view = hit.collider.GetComponentInParent<MemberView>();
                if (view == null || view.Visual == MemberVisual.Ghost || hit.distance >= nearestDistance) continue;
                nearest = view;
                nearestDistance = hit.distance;
            }
            Manager.SelectMember(nearest != null ? nearest.MemberId : null);
        }

        /// <summary>Builds the selected component; its pieces then rise one after another with a spark each.</summary>
        private void TryBuildSelected()
        {
            if (selected == null) return;
            var command = Context.Build.TryPlace(selected);
            if (command == null) return;

            var delay = 0f;
            foreach (var spec in command.Members)
            {
                var view = Context.Entities.View(spec.Id);
                if (view == null) continue;
                view.Materialize(delay);
                if (delay < PieceInterval * 6f)
                {
                    var material = Context.Entities.Material(spec.DefaultMaterialId);
                    Context.Effects.Spark(view.BodyRenderer.bounds.center, material != null ? material.Color : Color.white);
                }
                delay += PieceInterval;
            }
            // OnProgressChanged picks the next component.
        }

        private void OnProgressChanged(BuildProgressChanged message)
        {
            if (selected == null || Context.Tasks.IsBuilt(selected)) SelectDefault();
            else Select(selected);
        }

        private void OnRejected(PlacementRejected message)
        {
            foreach (var ghost in ghosts) ghost.ShowAsGhost();
        }

        /// <summary>Guided Build highlights the next required component; Free Build waits for the learner.</summary>
        private void SelectDefault()
        {
            Select(Context.Tasks.Mode == BuildMode.GuidedBuild ? Context.Tasks.NextComponent() : null);
        }

        private void Select(BuildComponent component)
        {
            foreach (var ghost in ghosts) Context.Views.ReleaseGhost(ghost);
            ghosts.Clear();

            selected = component;
            if (component != null)
            {
                foreach (var spec in Context.Tasks.Remaining(component))
                    ghosts.Add(Context.Views.SpawnGhost(spec, Context.Entities.Geometry(spec), Context.Site.StructureRoot));
            }
            RefreshBlueprint();
            Context.Events.Publish(new TraySelectionChanged(component));
        }

        /// <summary>
        /// The whole house floats as a faint hologram blueprint; the learner fills it in component by
        /// component. The selected component is drawn separately as the bright, pulsing ghost.
        /// </summary>
        private void RefreshBlueprint()
        {
            ClearBlueprint();
            foreach (var spec in Context.Blueprint.Members)
            {
                if (!spec.Required && Context.Tasks.Mode == BuildMode.GuidedBuild && !Context.Tasks.IsComplete) continue;
                if (Context.Entities.IsPlaced(spec.Id) || (selected != null && selected.Contains(spec.Id))) continue;
                blueprint.Add(Context.Views.SpawnBlueprint(spec, Context.Entities.Geometry(spec), Context.Site.StructureRoot));
            }
        }

        private void ClearBlueprint()
        {
            foreach (var view in blueprint) Context.Views?.ReleaseGhost(view);
            blueprint.Clear();
        }

        /// <summary>True when the finger's ray passes within the snap radius of the component's ghost.</summary>
        private bool IsNearGhost(Vector2 screenPosition)
        {
            if (ghosts.Count == 0) return false;
            var ray = Context.AR.Camera.ScreenPointToRay(screenPosition);
            var bounds = ghosts[0].BodyRenderer.bounds;
            foreach (var ghost in ghosts) bounds.Encapsulate(ghost.BodyRenderer.bounds);
            bounds.Expand(SnapRadius * 2f);
            return bounds.IntersectRay(ray);
        }
    }
}
