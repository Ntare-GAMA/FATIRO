<<<<<<< HEAD
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
=======
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
>>>>>>> 36e112157c1a8b4e037fd29414f5c88e591d5a75
