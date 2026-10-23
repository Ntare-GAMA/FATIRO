using Fatiro.Domain;
using UnityEngine;

namespace Fatiro.World
{
    /// <summary>
    /// A piece of a design's fit-out (a bed, a sofa, a lamp, a wheel). Most stand on level floors;
    /// those on a stepped, ground-following floor are lifted or lowered onto it for the site's slope.
    /// </summary>
    public sealed class FitOutItem : MonoBehaviour
    {
        [Tooltip("Stands on a floor that follows the ground (the panther's split levels), not a level one.")]
        [SerializeField] private bool followGround;

        public bool FollowGround
        {
            get => followGround;
            set => followGround = value;
        }

        /// <summary>Moves the item onto its floor for this site. Positions are in structure space.</summary>
        public void SettleOn(SiteLayout layout)
        {
            if (!followGround || layout == null) return;
            var position = transform.localPosition;
            transform.localPosition = new Vector3(position.x, position.y + layout.GroundY(position.z), position.z);
        }
    }
}
