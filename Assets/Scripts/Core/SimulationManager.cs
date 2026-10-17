using System;
using UnityEngine;

namespace Fatiro.Core
{
    public sealed class SimulationManager : MonoBehaviour
    {
        [SerializeField] private SimulationStateMachine stateMachine;
        [SerializeField] private int actionsRequired = 3;

        private SessionRepository sessionRepository;
        private float startedAt;
        private int score;
        private int completedActions;
        private int incorrectActions;

        public int Score => score;
        public int CompletedActions => completedActions;
        public int ActionsRequired => actionsRequired;

        private void Awake()
        {
            sessionRepository = new SessionRepository();
        }

        private void OnEnable()
        {
            SimulationEvents.SimulationPlaced += HandleSimulationPlaced;
            SimulationEvents.InteractionCompleted += HandleInteractionCompleted;
        }

        private void OnDisable()
        {
            SimulationEvents.SimulationPlaced -= HandleSimulationPlaced;
            SimulationEvents.InteractionCompleted -= HandleInteractionCompleted;
        }

        public void StartSimulation()
        {
            ResetMetrics();
            stateMachine.TransitionTo(SimulationState.ArSetup);
        }

        public void PauseSimulation() => stateMachine.TransitionTo(SimulationState.Paused);

        public void ResumeSimulation() => stateMachine.TransitionTo(SimulationState.Running);

        public void RestartSimulation()
        {
            ResetMetrics();
            stateMachine.TransitionTo(SimulationState.ArSetup);
        }

        public void CompleteInteraction(InteractionResult result)
        {
            SimulationEvents.RaiseInteractionCompleted(result);
        }

        private void HandleSimulationPlaced()
        {
            startedAt = Time.time;
            stateMachine.TransitionTo(SimulationState.Running);
        }

        private void HandleInteractionCompleted(InteractionResult result)
        {
            score = Mathf.Max(0, score + result.ScoreDelta);
            if (result.Successful)
            {
                completedActions++;
            }
            else
            {
                incorrectActions++;
            }

            if (completedActions >= actionsRequired)
            {
                var session = new SimulationSession
                {
                    sessionId = Guid.NewGuid().ToString("N"),
                    completedAtUtc = DateTime.UtcNow.ToString("O"),
                    durationSeconds = Mathf.Max(0f, Time.time - startedAt),
                    score = score,
                    completedActions = completedActions,
                    incorrectActions = incorrectActions,
                    outcome = incorrectActions == 0 ? "Successful" : "Completed with errors"
                };
                sessionRepository.Save(session);
                SimulationEvents.RaiseSimulationCompleted(session);
                stateMachine.TransitionTo(SimulationState.Completion);
            }
        }

        private void ResetMetrics()
        {
            score = 0;
            completedActions = 0;
            incorrectActions = 0;
        }
    }
}
