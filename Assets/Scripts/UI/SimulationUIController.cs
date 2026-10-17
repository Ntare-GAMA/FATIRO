using Fatiro.Core;
using UnityEngine;

namespace Fatiro.UI
{
    public sealed class SimulationUIController : MonoBehaviour
    {
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject setupPanel;
        [SerializeField] private GameObject simulationPanel;
        [SerializeField] private GameObject completionPanel;
        [SerializeField] private SimulationManager simulationManager;

        private void OnEnable()
        {
            SimulationEvents.StateChanged += HandleStateChanged;
            SimulationEvents.SimulationCompleted += HandleSimulationCompleted;
        }

        private void OnDisable()
        {
            SimulationEvents.StateChanged -= HandleStateChanged;
            SimulationEvents.SimulationCompleted -= HandleSimulationCompleted;
        }

        public void StartSimulation()
        {
            simulationManager.StartSimulation();
        }

        private void HandleStateChanged(SimulationState state)
        {
            SetPanelActive(startPanel, state == SimulationState.Start);
            SetPanelActive(setupPanel, state == SimulationState.ArSetup || state == SimulationState.Placement);
            SetPanelActive(simulationPanel, state == SimulationState.Running || state == SimulationState.Paused);
            SetPanelActive(completionPanel, state == SimulationState.Completion);
        }

        private void HandleSimulationCompleted(SimulationSession session)
        {
            Debug.Log($"Simulation completed with score {session.score}: {session.outcome}");
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }
    }
}
