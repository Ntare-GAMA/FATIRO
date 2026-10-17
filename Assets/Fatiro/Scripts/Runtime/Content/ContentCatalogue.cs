using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Fatiro.Content
{
    /// <summary>How long a loaded asset is kept. Each scope is released as a block.</summary>
    public enum AssetScope
    {
        /// <summary>Catalogue data and audio, kept for the app lifetime.</summary>
        App,

        /// <summary>Member prefabs, site and effects for the session in progress.</summary>
        Session,

        /// <summary>Decorative creature skin, only while a finished build is on screen.</summary>
        Skin
    }

    /// <summary>Addressable keys and labels used by the app.</summary>
    public static class AssetKeys
    {
        public const string DesignLabel = "design";
        public const string MaterialLabel = "material";
        public const string AudioLabel = "audio";

        public const string StrutPrefab = "Members/Strut";
        public const string PadPrefab = "Members/Pad";
        public const string PanelPrefab = "Members/Panel";
        public const string ArchPrefab = "Members/Arch";
        public const string GhostMaterial = "Members/GhostMaterial";
        public const string SitePrefab = "Site/Plot";
        public const string DebrisPrefab = "FX/Debris";
        public const string RainDropPrefab = "FX/RainDrop";
        public const string SparkPrefab = "FX/Spark";
        public const string DisplayFont = "UI/Fonts/Display";
        public const string BodyFont = "UI/Fonts/Body";
        public const string BoldFont = "UI/Fonts/Bold";
        public const string ItalicFont = "UI/Fonts/Italic";
    }

    public interface IContentCatalogue
    {
        int ActiveHandleCount { get; }
        int HandleCount(AssetScope scope);
        Task<IReadOnlyList<HouseDesign>> LoadDesignsAsync();
        Task<IReadOnlyList<MaterialData>> LoadMaterialsAsync();
        Task<IReadOnlyDictionary<string, AudioClip>> LoadAudioAsync();
        Task<T> LoadAsync<T>(string key, AssetScope scope) where T : UnityEngine.Object;
        Task<GameObject> LoadSkinAsync(HouseDesign design);
        void ReleaseScope(AssetScope scope);
        void ReleaseAll();
    }

    /// <summary>
    /// The only gateway to Addressables. Every load records its handle under a scope, and every
    /// scope is released explicitly (session end, restart, skin hidden) so mobile memory is
    /// returned instead of growing over a 30-minute session.
    /// </summary>
    public sealed class ContentCatalogue : IContentCatalogue
    {
        private readonly Dictionary<AssetScope, List<AsyncOperationHandle>> handles = new Dictionary<AssetScope, List<AsyncOperationHandle>>();
        private readonly Dictionary<string, AsyncOperationHandle> cachedByKey = new Dictionary<string, AsyncOperationHandle>();

        public ContentCatalogue()
        {
            foreach (AssetScope scope in Enum.GetValues(typeof(AssetScope))) handles[scope] = new List<AsyncOperationHandle>();
        }

        public int ActiveHandleCount
        {
            get
            {
                var count = 0;
                foreach (var list in handles.Values) count += list.Count;
                return count;
            }
        }

        public int HandleCount(AssetScope scope) => handles[scope].Count;

        public async Task<IReadOnlyList<HouseDesign>> LoadDesignsAsync()
        {
            var designs = new List<HouseDesign>(await LoadLabelAsync<HouseDesign>(AssetKeys.DesignLabel, AssetScope.App));
            designs.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));
            return designs;
        }

        public async Task<IReadOnlyList<MaterialData>> LoadMaterialsAsync() =>
            await LoadLabelAsync<MaterialData>(AssetKeys.MaterialLabel, AssetScope.App);

        public async Task<IReadOnlyDictionary<string, AudioClip>> LoadAudioAsync()
        {
            var clips = await LoadLabelAsync<AudioClip>(AssetKeys.AudioLabel, AssetScope.App);
            var byName = new Dictionary<string, AudioClip>();
            foreach (var clip in clips) byName[clip.name] = clip;
            return byName;
        }

        public async Task<T> LoadAsync<T>(string key, AssetScope scope) where T : UnityEngine.Object
        {
            var cacheKey = $"{scope}:{key}";
            if (cachedByKey.TryGetValue(cacheKey, out var cached) && cached.IsValid())
                return cached.Result as T;

            var handle = Addressables.LoadAssetAsync<T>(key);
            Track(scope, handle);
            cachedByKey[cacheKey] = handle;
            var asset = await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                throw new InvalidOperationException($"Addressable '{key}' failed to load: {handle.OperationException?.Message}");
            return asset;
        }

        public async Task<GameObject> LoadSkinAsync(HouseDesign design)
        {
            if (design == null || design.Skin == null || !design.Skin.RuntimeKeyIsValid()) return null;
            var handle = Addressables.LoadAssetAsync<GameObject>(design.Skin.RuntimeKey);
            Track(AssetScope.Skin, handle);
            var prefab = await handle.Task;
            return handle.Status == AsyncOperationStatus.Succeeded ? prefab : null;
        }

        public void ReleaseScope(AssetScope scope)
        {
            var list = handles[scope];
            foreach (var handle in list)
                if (handle.IsValid()) Addressables.Release(handle);
            list.Clear();

            var prefix = scope + ":";
            var stale = new List<string>();
            foreach (var key in cachedByKey.Keys)
                if (key.StartsWith(prefix, StringComparison.Ordinal)) stale.Add(key);
            foreach (var key in stale) cachedByKey.Remove(key);

            Debug.Log($"[Fatiro] Released Addressables scope {scope}. Handles still held: {ActiveHandleCount}");
        }

        public void ReleaseAll()
        {
            foreach (AssetScope scope in Enum.GetValues(typeof(AssetScope))) ReleaseScope(scope);
        }

        private async Task<IReadOnlyList<T>> LoadLabelAsync<T>(string label, AssetScope scope) where T : UnityEngine.Object
        {
            var cacheKey = $"{scope}:label:{label}";
            if (cachedByKey.TryGetValue(cacheKey, out var cached) && cached.IsValid())
                return new List<T>((IList<T>)cached.Result);

            var handle = Addressables.LoadAssetsAsync<T>(label, null);
            Track(scope, handle);
            cachedByKey[cacheKey] = handle;
            var assets = await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded || assets == null)
                throw new InvalidOperationException($"Addressables label '{label}' failed to load: {handle.OperationException?.Message}");
            return new List<T>(assets);
        }

        private void Track(AssetScope scope, AsyncOperationHandle handle) => handles[scope].Add(handle);
    }
}
