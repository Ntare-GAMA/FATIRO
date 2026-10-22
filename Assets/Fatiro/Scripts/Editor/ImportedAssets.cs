using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fatiro.EditorTools
{
    /// <summary>
    /// Brings third-party Asset Store content into Fatiro without editing the packages themselves:
    /// converts Built-in Standard materials to URP copies, fits props to a target size, strips
    /// colliders (so scenery never blocks taps on members) and caps texture sizes for mobile.
    /// </summary>
    public static class ImportedAssets
    {
        public const string ConvertedFolder = "Assets/Fatiro/Content/ThirdParty/Converted";
        public const string DerivedFolder = "Assets/Fatiro/Content/ThirdParty/Derived";
        private const string UrpLitGuid = "933532a4fcc9baf4fa0491de14d08ed7";

        private static readonly Dictionary<Material, Material> Converted = new Dictionary<Material, Material>();

        public static void Begin()
        {
            Converted.Clear();
            Directory.CreateDirectory(ConvertedFolder);
            Directory.CreateDirectory(DerivedFolder);
            AssetDatabase.Refresh();
        }

        public static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Debug.LogWarning($"[Fatiro] Imported prefab not found: {path}");
            return prefab;
        }

        public static Texture2D LoadTexture(string path, bool normalMap = false, int maxSize = 1024)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var changed = false;
                if (normalMap && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                }
                if (importer.wrapMode != TextureWrapMode.Repeat)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    changed = true;
                }
                changed |= LimitForAndroid(importer, maxSize);
                if (changed) importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) Debug.LogWarning($"[Fatiro] Imported texture not found: {path}");
            return texture;
        }

        /// <summary>A shared URP Lit material for a building material, using imported textures.</summary>
        public static Material SurfaceMaterial(string name, Texture2D colour, Texture2D normal, Color tint, float metallic = 0f,
            float smoothness = 0.2f)
        {
            var path = $"{ConvertedFolder}/Surface_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", colour);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Instantiates an imported prefab as a plain copy: URP materials, no colliders, no shadows,
        /// scaled so its largest dimension (or height) matches the target in model metres.
        /// </summary>
        public static GameObject Instantiate(GameObject prefab, float targetSize, bool fitHeight, Transform parent, string name)
        {
            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = name;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            // Models can carry their own lights and cameras (a glb car came with a strong point light); scenery must not.
            foreach (var light in instance.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera);
            foreach (var probe in instance.GetComponentsInChildren<ReflectionProbe>(true)) Object.DestroyImmediate(probe);
            foreach (var source in instance.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(source);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++) materials[i] = ToUrp(materials[i]);
                renderer.sharedMaterials = materials;
            }

            var bounds = Bounds(instance);
            var size = fitHeight ? bounds.size.y : Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size > 0.0001f) instance.transform.localScale *= targetSize / size;

            // Sit the prop's base on its pivot so the site can drop it onto the slope.
            bounds = Bounds(instance);
            var offset = instance.transform.position.y - bounds.min.y;
            instance.transform.position += Vector3.up * offset;
            return instance;
        }

        /// <summary>Wraps a prop in an empty parent at its base, so moving the parent places the prop on the ground.</summary>
        public static Transform Grounded(GameObject prop, Transform parent, Vector3 localPosition, float yaw, string name)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPosition;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            prop.transform.SetParent(holder, false);
            prop.transform.localPosition = new Vector3(0f, prop.transform.localPosition.y, 0f);
            return holder;
        }

        public static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>Returns a URP version of a material. Built-in Standard materials are copied once and converted.</summary>
        public static Material ToUrp(Material source)
        {
            if (source == null) return null;
            if (Converted.TryGetValue(source, out var known)) return known;

            var shaderPath = AssetDatabase.GetAssetPath(source.shader);
            var shaderGuid = AssetDatabase.AssetPathToGUID(shaderPath);
            if (shaderGuid == UrpLitGuid || source.shader.name.StartsWith("Universal Render Pipeline") || source.shader.name.StartsWith("Shader Graphs"))
            {
                LimitTextures(source);
                Converted[source] = source;
                return source;
            }

            var path = $"{ConvertedFolder}/{Sanitise(source.name)}_URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            if (source.HasProperty("_MainTex") && source.GetTexture("_MainTex") != null)
            {
                material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                material.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
            }
            material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
            {
                material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.2f);
            material.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
            if (source.IsKeywordEnabled("_EMISSION") && source.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
            }

            // Leaves and grass use cut-out transparency in the Standard shader (_Mode 1).
            var cutout = (source.HasProperty("_Mode") && Mathf.Approximately(source.GetFloat("_Mode"), 1f)) ||
                         source.IsKeywordEnabled("_ALPHATEST_ON") || source.renderQueue == (int)RenderQueue.AlphaTest;
            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            material.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f);
            if (cutout)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetFloat("_Cull", (float)CullMode.Off);
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.renderQueue = -1;
            }

            material.enableInstancing = true;
            LimitTextures(material);
            EditorUtility.SetDirty(material);
            Converted[source] = material;
            return material;
        }

        /// <summary>
        /// Crops a square region out of a texture atlas into its own tileable texture.
        /// Used for the clay-brick quarter of the brick-house atlas.
        /// </summary>
        public static Texture2D CropToTexture(string sourcePath, RectInt topLeftRegion, string name, bool normalMap)
        {
            var target = $"{DerivedFolder}/{name}.png";
            if (!File.Exists(target))
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(sourcePath));
                // LoadImage puts row 0 at the bottom; convert the region from top-left coordinates.
                var y = source.height - topLeftRegion.y - topLeftRegion.height;
                var pixels = source.GetPixels(topLeftRegion.x, y, topLeftRegion.width, topLeftRegion.height);
                var cropped = new Texture2D(topLeftRegion.width, topLeftRegion.height, TextureFormat.RGBA32, false);
                cropped.SetPixels(pixels);
                cropped.Apply();
                File.WriteAllBytes(target, cropped.EncodeToPNG());
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(cropped);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            }
            return LoadTexture(target, normalMap, 1024);
        }

        private static void LimitTextures(Material material)
        {
            foreach (var property in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(property);
                if (texture == null) continue;
                if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is TextureImporter importer && LimitForAndroid(importer, 512))
                    importer.SaveAndReimport();
            }
        }

        /// <summary>Caps the Android texture size: AR runs on mid-range phones and the APK must stay small (NFR-11).</summary>
        private static bool LimitForAndroid(TextureImporter importer, int maxSize)
        {
            var settings = importer.GetPlatformTextureSettings("Android");
            if (settings.overridden && settings.maxTextureSize <= maxSize) return false;
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.format = TextureImporterFormat.Automatic;
            importer.SetPlatformTextureSettings(settings);
            return true;
        }

        private static string Sanitise(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name;
        }
    }
}
