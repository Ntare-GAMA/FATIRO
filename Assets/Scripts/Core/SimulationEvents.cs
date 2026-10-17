using System;

namespace Fatiro.Core
{
    public static class SimulationEvents
    {
        public static event Action<SimulationState> StateChanged;
        public static event Action SimulationPlaced;
        public static event Action<InteractionResult> InteractionCompleted;
        public static event Action<SimulationSession> SimulationCompleted;

        public static void RaiseStateChanged(SimulationState state) => StateChanged?.Invoke(state);
        public static void RaiseSimulationPlaced() => SimulationPlaced?.Invoke();
        public static void RaiseInteractionCompleted(InteractionResult result) => InteractionCompleted?.Invoke(result);
        public static void RaiseSimulationCompleted(SimulationSession session) => SimulationCompleted?.Invoke(session);
    }

    public readonly struct InteractionResult
    {
        public InteractionResult(bool successful, int scoreDelta, string message)
        {
            Successful = successful;
            ScoreDelta = scoreDelta;
            Message = message;
        }

        public bool Successful { get; }
        public int ScoreDelta { get; }
        public string Message { get; }
    }
}
