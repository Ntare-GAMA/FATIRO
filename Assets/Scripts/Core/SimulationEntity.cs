using UnityEngine;

namespace Fatiro.Core
{
    public abstract class SimulationEntity : MonoBehaviour
    {
        [SerializeField] private string entityId;
        [SerializeField] private string displayName;

        public string EntityId => entityId;
        public string DisplayName => displayName;

        public abstract InteractionResult Interact();
    }
}
