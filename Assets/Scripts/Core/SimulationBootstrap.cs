using UnityEngine;

namespace Fatiro.Core
{
    public sealed class SimulationBootstrap : MonoBehaviour
    {
        [SerializeField] private SimulationStateMachine stateMachine;

        public void ConfirmPlacement()
        {
            stateMachine.TransitionTo(SimulationState.Placement);
            SimulationEvents.RaiseSimulationPlaced();
        }
    }
}
