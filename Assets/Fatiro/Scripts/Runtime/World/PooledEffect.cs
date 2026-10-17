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
}
