using System.Collections.Generic;
using Fatiro.Core;
using UnityEngine;

namespace Fatiro.World
{
    /// <summary>
    /// Owns the effect pools. Prefabs come from Addressables at session start; the pools are
    /// cleared before those assets are released at session end.
    /// </summary>
    public sealed class EffectsController : MonoBehaviour
    {
        private ObjectPool<DebrisPiece> debris;
        private ObjectPool<RainDrop> rain;
        private ObjectPool<SnapSpark> sparks;
        private readonly List<Vector3> roofPoints = new List<Vector3>();
        private Vector3 drainPoint;
        private float rainPerSecond;
        private float rainAccumulator;
        private float rainStopsAt;
        private float worldScale = 0.02f;
        private Transform storage;

        public IEnumerable<IPoolStats> Pools
        {
            get
            {
                if (debris != null) yield return debris;
                if (rain != null) yield return rain;
                if (sparks != null) yield return sparks;
            }
        }

        public bool IsReady => debris != null;

        public void Initialise(GameObject debrisPrefab, GameObject rainPrefab, GameObject sparkPrefab)
        {
            Clear();
            storage = new GameObject("Pooled Effects").transform;
            storage.SetParent(transform, false);
            debris = new ObjectPool<DebrisPiece>("Debris", () => Create<DebrisPiece>(debrisPrefab, item => debris.Release((DebrisPiece)item)), storage, prewarm: 24, maxSize: 96);
            rain = new ObjectPool<RainDrop>("Rain drops", () => Create<RainDrop>(rainPrefab, item => rain.Release((RainDrop)item)), storage, prewarm: 48, maxSize: 160);
            sparks = new ObjectPool<SnapSpark>("Snap sparks", () => Create<SnapSpark>(sparkPrefab, item => sparks.Release((SnapSpark)item)), storage, prewarm: 4, maxSize: 16);
        }

        public void SetWorldScale(float scale) => worldScale = scale;

        public void SpawnDebris(Vector3 worldPosition, Color color)
        {
            if (debris == null) return;
            debris.Get().Launch(worldPosition, color, worldScale);
        }

        public void Spark(Vector3 worldPosition, Color color)
        {
            if (sparks == null) return;
            sparks.Get().Flash(worldPosition, worldScale * 1.5f, color);
        }

        /// <summary>Starts drops falling on the roof points and running to the drain (FR-19).</summary>
        public void StartRain(IReadOnlyList<Vector3> roofWorldPoints, Vector3 drainWorldPoint, float millimetresPerHour, float seconds)
        {
            roofPoints.Clear();
            roofPoints.AddRange(roofWorldPoints);
            drainPoint = drainWorldPoint;
            rainPerSecond = Mathf.Lerp(10f, 70f, millimetresPerHour / 100f);
            rainStopsAt = Time.time + seconds;
        }

        public void StopRain() => rainPerSecond = 0f;

        private void Update()
        {
            if (rain == null || rainPerSecond <= 0f || roofPoints.Count == 0) return;
            if (Time.time > rainStopsAt)
            {
                rainPerSecond = 0f;
                return;
            }

            rainAccumulator += rainPerSecond * Time.deltaTime;
            while (rainAccumulator >= 1f)
            {
                rainAccumulator -= 1f;
                var roof = roofPoints[Random.Range(0, roofPoints.Count)] +
                           new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * worldScale;
                rain.Get().Drop(roof + Vector3.up * 4f * worldScale, roof, drainPoint, worldScale);
            }
        }

        public void Clear()
        {
            debris?.Clear();
            rain?.Clear();
            sparks?.Clear();
            debris = null;
            rain = null;
            sparks = null;
            if (storage != null) Destroy(storage.gameObject);
        }

        private T Create<T>(GameObject prefab, System.Action<PooledEffect> release) where T : PooledEffect
        {
            var instance = Instantiate(prefab, storage);
            var effect = instance.GetComponent<T>();
            if (effect == null) effect = instance.AddComponent<T>();
            effect.BindPool(release);
            return effect;
        }
    }
}
