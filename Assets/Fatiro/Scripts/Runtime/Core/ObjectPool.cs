using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fatiro.Core
{
    /// <summary>Implemented by pooled objects so they can reset themselves on reuse.</summary>
    public interface IPoolable
    {
        /// <summary>Called when the object is taken from the pool, just before it is shown.</summary>
        void OnSpawned();

        /// <summary>Called when the object goes back to the pool. Must clear all per-use state.</summary>
        void OnDespawned();
    }

    public interface IPoolStats
    {
        string Name { get; }
        int CountActive { get; }
        int CountInactive { get; }
        int TotalCreated { get; }
    }

    /// <summary>
    /// Object Pool: reuses ghost outlines, failure debris, rain drops and snap sparks instead of
    /// instantiating and destroying them during collapse and rain. That avoids the garbage-collection
    /// spikes that would break the 30 fps target (NFR-01).
    /// </summary>
    public sealed class ObjectPool<T> : IPoolStats where T : Component
    {
        private readonly Stack<T> inactive = new Stack<T>();
        private readonly HashSet<T> active = new HashSet<T>();
        private readonly Func<T> create;
        private readonly Transform storage;
        private readonly int maxSize;

        public ObjectPool(string name, Func<T> create, Transform storage, int prewarm = 0, int maxSize = 256)
        {
            Name = name;
            this.create = create ?? throw new ArgumentNullException(nameof(create));
            this.storage = storage;
            this.maxSize = Math.Max(1, maxSize);
            for (var i = 0; i < prewarm; i++) inactive.Push(CreateInactive());
        }

        public string Name { get; }
        public int CountActive => active.Count;
        public int CountInactive => inactive.Count;
        public int TotalCreated { get; private set; }

        public T Get(Transform parent = null)
        {
            T item = null;
            while (inactive.Count > 0 && item == null) item = inactive.Pop();
            if (item == null) item = CreateInactive();

            active.Add(item);
            if (parent != null) item.transform.SetParent(parent, false);
            item.gameObject.SetActive(true);
            if (item is IPoolable poolable) poolable.OnSpawned();
            return item;
        }

        public void Release(T item)
        {
            if (item == null || !active.Remove(item)) return;

            if (item is IPoolable poolable) poolable.OnDespawned();
            item.gameObject.SetActive(false);
            if (inactive.Count >= maxSize)
            {
                UnityEngine.Object.Destroy(item.gameObject);
                return;
            }
            item.transform.SetParent(storage, false);
            inactive.Push(item);
        }

        public void ReleaseAll()
        {
            foreach (var item in new List<T>(active)) Release(item);
        }

        /// <summary>Destroys everything, e.g. when the session's assets are released.</summary>
        public void Clear()
        {
            ReleaseAll();
            while (inactive.Count > 0)
            {
                var item = inactive.Pop();
                if (item != null) UnityEngine.Object.Destroy(item.gameObject);
            }
        }

        private T CreateInactive()
        {
            var item = create();
            TotalCreated++;
            item.gameObject.SetActive(false);
            if (storage != null) item.transform.SetParent(storage, false);
            return item;
        }
    }
}
