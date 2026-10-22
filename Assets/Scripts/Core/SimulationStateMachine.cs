<<<<<<< HEAD
using UnityEngine;

namespace Fatiro.Core
{
    public sealed class SimulationStateMachine : MonoBehaviour
    {
        [SerializeField] private SimulationState initialState = SimulationState.Start;

        public SimulationState CurrentState { get; private set; }

        private void Awake()
        {
            CurrentState = initialState;
        }

        private void Start()
        {
            SimulationEvents.RaiseStateChanged(CurrentState);
        }

        public void TransitionTo(SimulationState nextState)
        {
            if (CurrentState == nextState)
            {
                return;
            }

            CurrentState = nextState;
            SimulationEvents.RaiseStateChanged(CurrentState);
        }
    }
}
=======
using UnityEngine;

namespace Fatiro.Core
{
    public sealed class SimulationStateMachine : MonoBehaviour
    {
        [SerializeField] private SimulationState initialState = SimulationState.Start;

        public SimulationState CurrentState { get; private set; }

        private void Awake()
        {
            CurrentState = initialState;
        }

        private void Start()
        {
            SimulationEvents.RaiseStateChanged(CurrentState);
        }

        public void TransitionTo(SimulationState nextState)
        {
            if (CurrentState == nextState)
            {
                return;
            }

            CurrentState = nextState;
            SimulationEvents.RaiseStateChanged(CurrentState);
        }
    }
}
>>>>>>> 36e112157c1a8b4e037fd29414f5c88e591d5a75
