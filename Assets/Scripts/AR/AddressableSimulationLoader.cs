using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Fatiro.AR
{
    public sealed class AddressableSimulationLoader : MonoBehaviour
    {
        [SerializeField] private AssetReferenceGameObject simulationReference;

        private AsyncOperationHandle<GameObject> handle;
        private GameObject loadedPrefab;

        public async Task<GameObject> LoadAsync()
        {
            if (loadedPrefab != null)
            {
                return loadedPrefab;
            }

            if (simulationReference == null)
            {
                throw new MissingReferenceException("AddressableSimulationLoader requires a simulation asset reference.");
            }

            handle = simulationReference.LoadAssetAsync<GameObject>();
            loadedPrefab = await handle.Task;
            if (loadedPrefab == null)
            {
                throw new InvalidOperationException("The simulation Addressable resolved to null.");
            }

            return loadedPrefab;
        }

        public void Release()
        {
            if (!handle.IsValid())
            {
                return;
            }

            Addressables.Release(handle);
            handle = default;
            loadedPrefab = null;
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
