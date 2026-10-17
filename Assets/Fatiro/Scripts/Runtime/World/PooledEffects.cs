using System;
using Fatiro.Core;
using UnityEngine;

namespace Fatiro.World
{
    /// <summary>Base for short-lived pooled visuals: tints itself and returns to its pool when done.</summary>
    public abstract class PooledEffect : MonoBehaviour, IPoolable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] protected Renderer effectRenderer;

        private MaterialPropertyBlock block;
        private Action<PooledEffect> returnToPool;

        protected float Age { get; private set; }

        public void BindPool(Action<PooledEffect> release) => returnToPool = release;

        public virtual void OnSpawned() => Age = 0f;

        public virtual void OnDespawned()
        {
            Age = 0f;
            transform.localScale = Vector3.one;
        }

        protected void Tint(Color color)
        {
            if (effectRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            effectRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            effectRenderer.SetPropertyBlock(block);
        }

        protected void Finish() => returnToPool?.Invoke(this);

        private void Update()
        {
            Age += Time.deltaTime;
            Tick(Time.deltaTime);
        }

        protected abstract void Tick(float deltaTime);
    }

    /// <summary>A chunk thrown off a failed member.</summary>
    public sealed class DebrisPiece : PooledEffect
    {
        private const float Lifetime = 1.6f;
        private Vector3 velocity;
        private Vector3 spin;
        private float worldScale;

        public void Launch(Vector3 worldPosition, Color color, float scale)
        {
            worldScale = scale;
            transform.position = worldPosition;
            transform.localScale = Vector3.one * scale * UnityEngine.Random.Range(0.25f, 0.5f);
            velocity = (UnityEngine.Random.insideUnitSphere + Vector3.up * 0.8f) * scale * 3f;
            spin = UnityEngine.Random.insideUnitSphere * 540f;
            Tint(color * 0.8f);
        }

        protected override void Tick(float deltaTime)
        {
            velocity += Vector3.down * 9.81f * worldScale * deltaTime;
            transform.position += velocity * deltaTime;
            transform.Rotate(spin * deltaTime, Space.Self);
            if (Age > Lifetime) Finish();
        }
    }

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

    /// <summary>A quick expanding flash where a member snapped into place.</summary>
    public sealed class SnapSpark : PooledEffect
    {
        private const float Lifetime = 0.45f;
        private float baseScale;
        private Color color;

        public void Flash(Vector3 worldPosition, float scale, Color tint)
        {
            transform.position = worldPosition;
            baseScale = scale;
            color = tint;
        }

        protected override void Tick(float deltaTime)
        {
            var t = Age / Lifetime;
            transform.localScale = Vector3.one * baseScale * Mathf.Lerp(0.3f, 1.4f, t);
            var faded = color;
            faded.a = 1f - t;
            Tint(faded);
            if (t >= 1f) Finish();
        }
    }
}
