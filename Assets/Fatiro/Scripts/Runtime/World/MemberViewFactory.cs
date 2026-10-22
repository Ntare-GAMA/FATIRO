using System.Collections.Generic;
using Fatiro.Core;
using Fatiro.Domain;
using UnityEngine;

namespace Fatiro.World
{
    /// <summary>
    /// Creates member visuals from the Addressable member prefabs and recycles them through one
    /// pool per shape. Ghost outlines use separate pools so a ghost is never mistaken for a member.
    /// </summary>
    public sealed class MemberViewFactory
    {
        private readonly Dictionary<MemberShape, ObjectPool<MemberView>> memberPools = new Dictionary<MemberShape, ObjectPool<MemberView>>();
        private readonly Dictionary<MemberShape, ObjectPool<MemberView>> ghostPools = new Dictionary<MemberShape, ObjectPool<MemberView>>();
        private readonly Transform storage;

        public MemberViewFactory(IReadOnlyDictionary<MemberShape, GameObject> prefabs, Material ghostMaterial,
            System.Action<Vector3, Color> debrisSpawner, Transform storageRoot)
        {
            storage = storageRoot;
            foreach (var pair in prefabs)
            {
                var shape = pair.Key;
                var prefab = pair.Value;
                memberPools[shape] = new ObjectPool<MemberView>($"{shape} members", () => Create(prefab, ghostMaterial, debrisSpawner), storage, maxSize: 128);
                ghostPools[shape] = new ObjectPool<MemberView>($"{shape} ghosts", () => Create(prefab, ghostMaterial, debrisSpawner), storage, prewarm: 1, maxSize: 8);
            }
        }

        public IEnumerable<IPoolStats> Pools
        {
            get
            {
                foreach (var pool in memberPools.Values) yield return pool;
                foreach (var pool in ghostPools.Values) yield return pool;
            }
        }

        public MemberView SpawnMember(MemberSpec spec, ResolvedGeometry geometry, Transform parent, MemberSurface surface)
        {
            var view = memberPools[spec.Shape].Get(parent);
            ResetLocal(view.transform);
            view.Bind(spec, geometry);
            view.ShowMaterial(surface);
            return view;
        }

        public void ReleaseMember(MemberView view)
        {
            if (view == null || view.Spec == null) return;
            memberPools[view.Spec.Shape].Release(view);
        }

        public MemberView SpawnGhost(MemberSpec spec, ResolvedGeometry geometry, Transform parent)
        {
            var view = ghostPools[spec.Shape].Get(parent);
            ResetLocal(view.transform);
            view.Bind(spec, geometry);
            view.ShowAsGhost();
            return view;
        }

        public void ReleaseGhost(MemberView view)
        {
            if (view == null || view.Spec == null) return;
            ghostPools[view.Spec.Shape].Release(view);
        }

        public void Clear()
        {
            foreach (var pool in memberPools.Values) pool.Clear();
            foreach (var pool in ghostPools.Values) pool.Clear();
        }

        private static MemberView Create(GameObject prefab, Material ghostMaterial, System.Action<Vector3, Color> debrisSpawner)
        {
            var instance = Object.Instantiate(prefab);
            var view = instance.GetComponent<MemberView>();
            var renderer = instance.GetComponentInChildren<MeshRenderer>();
            view.Initialise(renderer.sharedMaterial, ghostMaterial, debrisSpawner);
            return view;
        }

        private static void ResetLocal(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }
}
