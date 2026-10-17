using System.Collections.Generic;
using System.Threading.Tasks;
using Fatiro.Core;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Fatiro.AR
{
    /// <summary>
    /// Wraps AR Foundation: session, plane detection and visualisation, raycasts and anchors (FR-02 to FR-05).
    /// Publishes PlanesUpdated and TrackingChanged, and TrackingLostTooLong once tracking has
    /// been limited for longer than the threshold.
    /// </summary>
    public sealed class ARManager : MonoBehaviour, IARService
    {
        private const float TrackingLossPauseSeconds = 3f;
        private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        [SerializeField] private ARSession session;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private Camera arCamera;
        [Tooltip("In the editor without an AR device, use a virtual floor at y = 0 so the flow can be tested.")]
        [SerializeField] private bool editorFallback = true;

        private IEventBus events;
        private ARAnchor siteAnchor;
        private GameObject fallbackAnchor;
        private bool lastTracking;
        private float limitedSince = -1f;
        private bool pausedForTracking;
        private int lastPlaneCount = -1;

        public Camera Camera => arCamera;
        public bool IsScanning { get; private set; }
        public bool IsFallback { get; private set; }

        public bool IsTracking => IsFallback || ARSession.state == ARSessionState.SessionTracking &&
            ARSession.notTrackingReason == NotTrackingReason.None;

        public int PlaneCount => IsFallback ? 1 : planeManager != null ? planeManager.trackables.count : 0;

        public void Initialise(IEventBus eventBus)
        {
            events = eventBus;
            lastTracking = true;
            if (planeManager != null) planeManager.trackablesChanged.AddListener(OnPlanesChanged);
        }

        private void OnDestroy()
        {
            if (planeManager != null) planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }

        private void Update()
        {
            if (events == null) return;

            IsFallback = Application.isEditor && editorFallback &&
                         (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.Unsupported);

            var tracking = IsTracking;
            if (tracking != lastTracking)
            {
                lastTracking = tracking;
                events.Publish(new TrackingChanged(tracking, DescribeReason()));
            }

            // FR-05: only pause after tracking has been limited for more than 3 seconds.
            if (!tracking)
            {
                if (limitedSince < 0f) limitedSince = Time.unscaledTime;
                if (!pausedForTracking && Time.unscaledTime - limitedSince > TrackingLossPauseSeconds)
                {
                    pausedForTracking = true;
                    events.Publish(new TrackingLostTooLong());
                }
            }
            else
            {
                limitedSince = -1f;
                if (pausedForTracking)
                {
                    pausedForTracking = false;
                    events.Publish(new TrackingRecovered());
                }
            }

            var planes = PlaneCount;
            if (planes != lastPlaneCount)
            {
                lastPlaneCount = planes;
                events.Publish(new PlanesUpdated(planes));
            }
        }

        public void BeginScanning()
        {
            IsScanning = true;
            if (planeManager == null) return;
            planeManager.enabled = true;
            planeManager.SetTrackablesActive(true);
        }

        public void StopScanning()
        {
            IsScanning = false;
            if (planeManager == null) return;
            // Hide the grid and stop looking for new planes; the anchor keeps the site fixed.
            planeManager.SetTrackablesActive(false);
            planeManager.enabled = false;
        }

        public bool TryGetPlacementPose(Vector2 screenPosition, out Pose pose)
        {
            if (IsFallback)
            {
                var ray = arCamera.ScreenPointToRay(screenPosition);
                var floor = new Plane(Vector3.up, Vector3.zero);
                if (floor.Raycast(ray, out var distance))
                {
                    var point = ray.GetPoint(distance);
                    pose = new Pose(point, FacingCamera(point));
                    return true;
                }
                pose = default;
                return false;
            }

            if (raycastManager != null && raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
            {
                var hit = Hits[0];
                var plane = planeManager != null ? planeManager.GetPlane(hit.trackableId) : null;
                if (plane != null && plane.alignment != PlaneAlignment.HorizontalUp && plane.alignment != PlaneAlignment.HorizontalDown)
                {
                    pose = default;
                    return false;
                }
                pose = new Pose(hit.pose.position, FacingCamera(hit.pose.position));
                return true;
            }

            pose = default;
            return false;
        }

        public async Task<Transform> CreateSiteAnchorAsync(Pose pose)
        {
            ClearSiteAnchor();

            if (IsFallback || anchorManager == null)
            {
                fallbackAnchor = new GameObject("Site Anchor (editor)");
                fallbackAnchor.transform.SetPositionAndRotation(pose.position, pose.rotation);
                return fallbackAnchor.transform;
            }

            var result = await anchorManager.TryAddAnchorAsync(pose);
            if (!result.status.IsSuccess() || result.value == null)
            {
                Debug.LogWarning("[Fatiro] Anchor creation failed; using an unanchored site.");
                fallbackAnchor = new GameObject("Site Anchor (unanchored)");
                fallbackAnchor.transform.SetPositionAndRotation(pose.position, pose.rotation);
                return fallbackAnchor.transform;
            }

            siteAnchor = result.value;
            return siteAnchor.transform;
        }

        public void ClearSiteAnchor()
        {
            if (siteAnchor != null)
            {
                if (anchorManager == null || !anchorManager.TryRemoveAnchor(siteAnchor)) Destroy(siteAnchor.gameObject);
                siteAnchor = null;
            }

            if (fallbackAnchor != null)
            {
                Destroy(fallbackAnchor);
                fallbackAnchor = null;
            }
        }

        /// <summary>Turn the site so its downhill front faces the learner.</summary>
        private Quaternion FacingCamera(Vector3 point)
        {
            var toCamera = arCamera.transform.position - point;
            toCamera.y = 0f;
            return toCamera.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> changes)
        {
            if (events == null) return;
            lastPlaneCount = PlaneCount;
            events.Publish(new PlanesUpdated(lastPlaneCount));
        }

        private static string DescribeReason()
        {
            switch (ARSession.notTrackingReason)
            {
                case NotTrackingReason.ExcessiveMotion: return "Moving too fast. Move the device slowly.";
                case NotTrackingReason.InsufficientLight: return "Too dark. Find more light.";
                case NotTrackingReason.InsufficientFeatures: return "Point at a textured surface and move the device slowly.";
                case NotTrackingReason.Initializing:
                case NotTrackingReason.Relocalizing: return "Move the device slowly to reconnect.";
                default: return "Move the device slowly.";
            }
        }
    }
}
