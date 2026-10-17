using Fatiro.Audio;
using Fatiro.Core;
using Fatiro.States;
using UnityEngine;

namespace Fatiro.UI
{
    /// <summary>What every screen needs: the controller to send requests to and the bus to listen on.</summary>
    public sealed class UIContext
    {
        public ISimulationController Simulation { get; set; }
        public IEventBus Events { get; set; }
        public AudioManager Audio { get; set; }
        public RectTransform Canvas { get; set; }
        public SimulationContext Session => Simulation.Context;
    }

    /// <summary>
    /// Base class for screens and overlays: builds its hierarchy once, then is shown or hidden by
    /// the UI Manager as the simulation state changes.
    /// </summary>
    public abstract class UIScreen
    {
        protected UIScreen(UIContext context)
        {
            Ui = context;
        }

        protected UIContext Ui { get; }
        protected ISimulationController Simulation => Ui.Simulation;
        protected SimulationContext Session => Ui.Session;
        public RectTransform Root { get; private set; }
        public bool Visible => Root != null && Root.gameObject.activeSelf;

        public void Build(Transform parent)
        {
            Root = UIKit.Rect(GetType().Name, parent).Stretch();
            OnBuild();
            Root.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (Root == null || visible == Visible) return;
            Root.gameObject.SetActive(visible);
            if (visible) OnShow();
            else OnHide();
        }

        /// <summary>Called every frame while visible, for live values such as plane counts.</summary>
        public virtual void Tick() { }

        protected abstract void OnBuild();
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        protected void Click() => Ui.Audio?.PlayTap();
    }
}
