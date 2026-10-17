using System.Threading.Tasks;
using UnityEngine;

namespace Fatiro.AR
{
    /// <summary>
    /// Everything the simulation needs from AR, with no AR Foundation types leaking out.
    /// The rest of the app can be tested against a fake implementation.
    /// </summary>
    public interface IARService
    {
        Camera Camera { get; }
        bool IsTracking { get; }
        int PlaneCount { get; }
        bool IsScanning { get; }

        /// <summary>True when running in the editor without an AR device, using a virtual floor.</summary>
        bool IsFallback { get; }

        void BeginScanning();
        void StopScanning();

        /// <summary>Raycasts against detected horizontal planes (FR-03).</summary>
        bool TryGetPlacementPose(Vector2 screenPosition, out Pose pose);

        /// <summary>Creates an anchor at the pose; the site is parented to it so it stays fixed in the room.</summary>
        Task<Transform> CreateSiteAnchorAsync(Pose pose);

        void ClearSiteAnchor();
    }
}
