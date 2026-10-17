using System.Collections.Generic;
using Fatiro.Core;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Fatiro.AR
{
    public sealed class ARPlacementController : MonoBehaviour
    {
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private GameObject placementPreview;
        [SerializeField] private GameObject simulationPrefab;

        private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();
        private GameObject previewInstance;
        private GameObject simulationInstance;
        private ARAnchor simulationAnchor;

        private void Update()
        {
            if (Input.touchCount == 0)
            {
                return;
            }

            var touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began ||
                !raycastManager.Raycast(touch.position, Hits, TrackableType.PlaneWithinPolygon))
            {
                return;
            }

            var pose = Hits[0].pose;
            ShowPreview(pose);
        }

        public async void ConfirmPlacement()
        {
            if (previewInstance == null)
            {
                return;
            }

            if (simulationAnchor != null)
            {
                Destroy(simulationAnchor.gameObject);
            }

            var anchorResult = await anchorManager.TryAddAnchorAsync(new Pose(
                previewInstance.transform.position,
                previewInstance.transform.rotation));
            simulationAnchor = anchorResult.value;
            if (simulationAnchor == null)
            {
                Debug.LogError("Unable to create an AR anchor for the simulation.");
                return;
            }

            simulationInstance = Instantiate(simulationPrefab, simulationAnchor.transform);
            simulationInstance.transform.localPosition = Vector3.zero;
            simulationInstance.transform.localRotation = Quaternion.identity;
            Destroy(previewInstance);
            previewInstance = null;
            planeManager.enabled = false;
            SimulationEvents.RaiseSimulationPlaced();
        }

        public void EnableRepositioning()
        {
            if (simulationInstance != null)
            {
                Destroy(simulationInstance);
                simulationInstance = null;
            }

            if (simulationAnchor != null)
            {
                Destroy(simulationAnchor.gameObject);
                simulationAnchor = null;
            }

            planeManager.enabled = true;
        }

        private void ShowPreview(Pose pose)
        {
            if (previewInstance == null)
            {
                if (placementPreview == null)
                {
                    throw new MissingReferenceException("ARPlacementController requires a placement preview prefab.");
                }

                previewInstance = Instantiate(placementPreview);
            }

            previewInstance.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }
    }
}
