using UnityEngine;

namespace Fatiro.Core
{
    public sealed class InteractionEntity : SimulationEntity
    {
        [SerializeField] private SimulationManager simulationManager;
        [SerializeField] private int scoreForSuccess = 10;
        [SerializeField] private string successMessage = "Correct interaction.";

        public override InteractionResult Interact()
        {
            var result = new InteractionResult(true, scoreForSuccess, successMessage);
            simulationManager.CompleteInteraction(result);
            return result;
        }

        private void OnMouseDown()
        {
            if (simulationManager != null)
            {
                Interact();
            }
        }
    }
}
