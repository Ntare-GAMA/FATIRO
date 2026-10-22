<<<<<<< HEAD
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
=======
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
>>>>>>> 36e112157c1a8b4e037fd29414f5c88e591d5a75
