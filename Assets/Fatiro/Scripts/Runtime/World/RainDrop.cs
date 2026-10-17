using UnityEngine;

namespace Fatiro.World
{
    /// <summary>A rain drop that falls onto the roof, then runs towards a drain point.</summary>
    public sealed class RainDrop : PooledEffect
    {
        private Vector3 roofPoint;
        private Vector3 drainPoint;
        private float fallSpeed;
        private bool onRoof;
        private float worldScale;

        public void Drop(Vector3 start, Vector3 roof, Vector3 drain, float scale)
        {
            worldScale = scale;
            transform.position = start;
            transform.localScale = new Vector3(0.06f, 0.22f, 0.06f) * scale;
            roofPoint = roof;
            drainPoint = drain;
            fallSpeed = 14f * scale;
            onRoof = false;
            Tint(new Color(0.55f, 0.8f, 1f, 0.85f));
        }

        protected override void Tick(float deltaTime)
        {
            if (!onRoof)
            {
                transform.position = Vector3.MoveTowards(transform.position, roofPoint, fallSpeed * deltaTime);
                if ((transform.position - roofPoint).sqrMagnitude < 0.0001f * worldScale)
                {
                    onRoof = true;
                    transform.localScale = Vector3.one * 0.1f * worldScale;
                }
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, drainPoint, fallSpeed * 0.35f * deltaTime);
                if ((transform.position - drainPoint).sqrMagnitude < 0.0001f * worldScale) Finish();
            }

            if (Age > 4f) Finish();
        }
    }
}
