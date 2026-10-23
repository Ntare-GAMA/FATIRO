using Fatiro.Core;
using Fatiro.Domain;
using Fatiro.Input;
using UnityEngine;

namespace Fatiro.States
{
    /// <summary>
    /// State pattern: each session state accepts only the inputs and transitions that are legal
    /// for it. The defaults below reject everything, so a state opts in by overriding.
    /// </summary>
    public abstract class SimulationState
    {
        protected SimulationState(SimulationManager manager)
        {
            Manager = manager;
        }

        protected SimulationManager Manager { get; }
        protected SimulationContext Context => Manager.Context;

        public abstract SimulationStateId Id { get; }

        /// <summary>States inside "Active" need AR tracking; losing it pauses them (FR-05).</summary>
        public virtual bool RequiresTracking => false;

        /// <param name="resumed">True when returning from Paused, so the state continues instead of restarting.</param>
        public virtual void Enter(bool resumed) { }

        public virtual void Exit() { }

        public virtual void Tick(float deltaTime) { }

        public virtual void HandleGesture(in Gesture gesture) { }

        public virtual void ConfirmPlacement() => NotNow("Confirm placement");
        public virtual void Reposition() => NotNow("Reposition");
        public virtual void SelectTrayComponent(BuildComponent component) => NotNow("Select component");
        public virtual void BeginComponentDrag(BuildComponent component) => NotNow("Drag component");
        public virtual void DragComponent(Vector2 screenPosition) { }
        public virtual void EndComponentDrag(Vector2 screenPosition) { }
        public virtual void OpenTestBench() => NotNow("Open test bench");
        public virtual void RunLoadTest(LoadCaseConfig config) => NotNow("Run load test");
        public virtual void ModifyStructure() => NotNow("Modify structure");
        public virtual bool CanEditStructure => false;
        public virtual bool CanEndSession => false;

        protected void NotNow(string action) =>
            Debug.Log($"[Fatiro] '{action}' is not available in the {Id} state.");

        protected void Toast(string message, ToastKind kind = ToastKind.Info) =>
            Context.Events.Publish(new ToastRequested(message, kind));
    }
}
