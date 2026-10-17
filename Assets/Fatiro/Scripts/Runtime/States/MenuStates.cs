using UnityEngine;

namespace Fatiro.States
{
    /// <summary>Title screen: profile, history, start or resume.</summary>
    public sealed class StartState : SimulationState
    {
        public StartState(SimulationManager manager) : base(manager) { }

        public override SimulationStateId Id => SimulationStateId.Start;

        public override void Enter(bool resumed) => Context.Audio?.StopAmbience();
    }

    /// <summary>Choosing design, site conditions and mode.</summary>
    public sealed class SetupState : SimulationState
    {
        public SetupState(SimulationManager manager) : base(manager) { }

        public override SimulationStateId Id => SimulationStateId.Setup;
    }

    /// <summary>
    /// Tracking was lost for more than 3 s, or the learner paused. Simulation time stops; the
    /// manager returns to the previous active state (shallow history) on resume.
    /// </summary>
    public sealed class PausedState : SimulationState
    {
        public PausedState(SimulationManager manager) : base(manager) { }

        public override SimulationStateId Id => SimulationStateId.Paused;

        public override void Enter(bool resumed) => Time.timeScale = 0f;

        public override void Exit() => Time.timeScale = 1f;
    }

    /// <summary>Session finished: score shown, record saved; restart or return to menu.</summary>
    public sealed class EndedState : SimulationState
    {
        public EndedState(SimulationManager manager) : base(manager) { }

        public override SimulationStateId Id => SimulationStateId.Ended;

        public override void Enter(bool resumed) => Context.Audio?.StopAmbience();
    }
}
