using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fatiro.AR;
using Fatiro.Audio;
using Fatiro.Bootstrap;
using Fatiro.Content;
using Fatiro.Domain;
using Fatiro.Input;
using Fatiro.UI;
using Fatiro.World;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Fatiro.EditorTools
{
    /// <summary>
    /// One-click (or batch-mode) project setup: player and XR settings, URP AR background,
    /// generated materials, audio, prefabs, design data, Addressables groups and the scene.
    /// Everything it makes is deterministic, so it can be re-run safely.
    /// </summary>
    public static class FatiroProjectSetup
    {
        private const string Root = "Assets/Fatiro";
        private const string Generated = Root + "/Content";
        private const string MaterialsFolder = Generated + "/Materials";
        private const string PrefabsFolder = Generated + "/Prefabs";
        private const string DataFolder = Generated + "/Data";
        private const string AudioFolder = Generated + "/Audio";
        private const string SkinsFolder = Generated + "/Skins";
        private const string TexturesFolder = Generated + "/Textures";
        private const string ScenePath = Root + "/Scenes/Fatiro.unity";

        [MenuItem("Fatiro/Build Project Content", priority = 0)]
        public static void BuildEverything()
        {
            EnsureFolders();
            ConfigurePlayer();
            ConfigureXR();
            ConfigureRenderer();

            ImportedAssets.Begin();
            var materials = CreateMaterials();
            var surfaces = CreateSurfaces();
            AudioSynth.WriteAll(AudioFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureAudioImport();

            var prefabs = CreatePrefabs(materials, surfaces);
            var skins = CreateSkins();
            var materialAssets = CreateMaterialData(surfaces);
            var designs = CreateDesigns(skins);
            ConfigureAddressables(prefabs, skins, materialAssets, designs, materials);
            CreateScene(materials);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Fatiro] Project content built.");
        }

        [MenuItem("Fatiro/Build Android APK", priority = 20)]
        public static void BuildAndroid()
        {
            BuildEverything();
            AddressableAssetSettings.BuildPlayerContent(out var addressablesResult);
            if (!string.IsNullOrEmpty(addressablesResult.Error))
                throw new BuildFailedException("Addressables build failed: " + addressablesResult.Error);

            Directory.CreateDirectory("Builds/Android");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/FatiroAR.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException($"Android build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            Debug.Log($"[Fatiro] APK built: {options.locationPathName} ({new FileInfo(options.locationPathName).Length / (1024 * 1024)} MB)");
        }

        // ---- Settings -------------------------------------------------------------------

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Fatiro";
            PlayerSettings.productName = "Fatiro AR";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.fatiro.ar");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        /// <summary>Enables the ARCore loader for Android in XR Plug-in Management.</summary>
        private static void ConfigureXR()
        {
            const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null) perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, path);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);

            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            var android = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            android.InitManagerOnStart = true;
            if (android.Manager == null)
            {
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = "Android Providers";
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                android.Manager = manager;
            }

            if (!android.Manager.activeLoaders.Any(loader => loader != null && loader.GetType().Name == "ARCoreLoader"))
            {
                var assigned = XRPackageMetadataStore.AssignLoader(android.Manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android);
                Debug.Log($"[Fatiro] ARCore loader assigned for Android: {assigned}");
            }

            EditorUtility.SetDirty(android);
            EditorUtility.SetDirty(perTarget);
        }

        /// <summary>URP needs the AR background renderer feature or the camera feed is black.</summary>
        private static void ConfigureRenderer()
        {
            foreach (var path in new[] { "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/PC_Renderer.asset" })
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null || data.rendererFeatures.Any(feature => feature is ARBackgroundRendererFeature)) continue;

                var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                feature.name = "ARBackgroundRendererFeature";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var serialized = new SerializedObject(data);
                var features = serialized.FindProperty("m_RendererFeatures");
                var map = serialized.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
        }

        // ---- Materials ------------------------------------------------------------------

        private sealed class MaterialSet
        {
            public Material Structure;
            public Material Ghost;
            public Material Plot;
            public Material Debris;
            public Material Rain;
            public Material Spark;
            public Material PlaneGrid;
        }

        private static MaterialSet CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");

            var set = new MaterialSet
            {
                Structure = SaveMaterial("Structure", lit, Color.white, smoothness: 0.25f),
                Debris = SaveMaterial("Debris", lit, new Color(0.5f, 0.4f, 0.3f), smoothness: 0.1f),
                Ghost = SaveMaterial("Ghost", unlit, MemberView.GhostTint, transparent: true),
                Rain = SaveMaterial("RainDrop", unlit, new Color(0.55f, 0.8f, 1f, 0.85f), transparent: true),
                Spark = SaveMaterial("Spark", unlit, new Color(1f, 0.9f, 0.5f, 0.8f), transparent: true),
                Plot = SaveMaterial("Plot", particles, Color.white, smoothness: 0.05f),
                PlaneGrid = SaveMaterial("PlaneGrid", unlit, new Color(1f, 1f, 1f, 0.9f), transparent: true)
            };

            var grid = CreateGridTexture();
            set.PlaneGrid.SetTexture("_BaseMap", grid);
            set.PlaneGrid.SetTextureScale("_BaseMap", new Vector2(10f, 10f));
            set.Plot.SetFloat("_Cull", (float)CullMode.Off);
            EditorUtility.SetDirty(set.PlaneGrid);
            EditorUtility.SetDirty(set.Plot);
            return set;
        }

        private static Material SaveMaterial(string name, Shader shader, Color color, bool transparent = false, float smoothness = 0.3f,
            Color? emission = null)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateGridTexture()
        {
            var path = $"{TexturesFolder}/PlaneGrid.png";
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var line = x < 2 || y < 2;
                var dot = (x - size / 2) * (x - size / 2) + (y - size / 2) * (y - size / 2) < 6;
                texture.SetPixel(x, y, line ? new Color(1f, 1f, 1f, 0.75f) : dot ? new Color(1f, 0.75f, 0.35f, 0.6f) : new Color(1f, 1f, 1f, 0.08f));
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- Imported surfaces (Asset Store textures) -------------------------------------

        /// <summary>Real textures for each building material and for the hillside, from the imported packs.</summary>
        private sealed class SurfaceSet
        {
            public readonly Dictionary<string, (Material material, float tileMetres)> Members = new Dictionary<string, (Material, float)>();
            public Material[] Ground;
            public Material Platform;
        }

        private const string Polyeler = "Assets/Polyeler/EssentialNaturePack";
        private const string SimpleNature = "Assets/SimpleNaturePack/Prefabs";
        private const string Stones = "Assets/Low Poly Stones/Prefabs";
        private const string GroundTextures = "Assets/GG-Textures/Textures";
        private const string NatureAudio = "Assets/Nature - Essentials";
        private const string ImportedModels = "Assets/Fatiro/Imported/Models";
        private const string ImportedTextures = "Assets/Fatiro/Imported/Textures";
        private static readonly string[] NatureClips = { Cues.Birds, Cues.RainLoop, Cues.WindLoop };

        private static SurfaceSet CreateSurfaces()
        {
            var set = new SurfaceSet();

            // Bamboo001B from ambientCG (CC0): a mat of bamboo poles, used for bamboo members.
            var bamboo = ImportedAssets.SurfaceMaterial("Bamboo",
                ImportedAssets.LoadTexture($"{ImportedTextures}/Bamboo001B/Bamboo001B_1K-JPG_Color.jpg"),
                ImportedAssets.LoadTexture($"{ImportedTextures}/Bamboo001B/Bamboo001B_1K-JPG_NormalGL.jpg", normalMap: true),
                Color.white, smoothness: 0.35f);
            var timber = ImportedAssets.SurfaceMaterial("Timber",
                ImportedAssets.LoadTexture("Assets/LowlyPoly/Stylize Wood Texture/Textures/vol_2_3_Base_Color.png"),
                ImportedAssets.LoadTexture("Assets/LowlyPoly/Stylize Wood Texture/Textures/vol_2_3_Normal.png", normalMap: true),
                Color.white, smoothness: 0.2f);
            var steel = ImportedAssets.SurfaceMaterial("Steel",
                ImportedAssets.LoadTexture("Assets/Models/Metal_Plates/Textures/Metal_Plates_1_AlbedoTransparency.tga"),
                ImportedAssets.LoadTexture("Assets/Models/Metal_Plates/Textures/Metal_Plates_Normal.tga", normalMap: true),
                new Color(0.85f, 0.88f, 0.92f), metallic: 0.75f, smoothness: 0.55f);
            var concrete = ImportedAssets.SurfaceMaterial("Concrete",
                ImportedAssets.LoadTexture("Assets/Sat Productions/B-01/Concrete Props Pack/Textures/A_Wall_Tiled/ConcretePack_A_Wall_BaseColor.png"),
                ImportedAssets.LoadTexture("Assets/Sat Productions/B-01/Concrete Props Pack/Textures/A_Wall_Tiled/ConcretePack_A_Wall_Normal.png", normalMap: true),
                Color.white, smoothness: 0.15f);

            // Compressed-earth block: the clay-brick quarter of the brick-house atlas, cropped to tile cleanly.
            var brickRegion = new RectInt(0, 0, 1024, 1024);
            var earthBlock = File.Exists("Assets/Brick_House/Textures/brick_house_D.PNG")
                ? ImportedAssets.SurfaceMaterial("EarthBlock",
                    ImportedAssets.CropToTexture("Assets/Brick_House/Textures/brick_house_D.PNG", brickRegion, "ClayBrick_Colour", false),
                    ImportedAssets.CropToTexture("Assets/Brick_House/Textures/Brick_house_N.png", brickRegion, "ClayBrick_Normal", true),
                    new Color(1f, 0.9f, 0.8f), smoothness: 0.1f)
                : ImportedAssets.SurfaceMaterial("EarthBlock", null, null, new Color(0.76f, 0.45f, 0.30f));

            set.Members[MaterialLibrary.Bamboo] = (bamboo, 0.9f);
            set.Members[MaterialLibrary.Timber] = (timber, 1.4f);
            set.Members[MaterialLibrary.Steel] = (steel, 1.6f);
            set.Members[MaterialLibrary.Concrete] = (concrete, 2.0f);
            set.Members[MaterialLibrary.EarthBlock] = (earthBlock, 1.2f);

            // Hillside: grass terraces in two tones over laterite (sand tinted red-brown).
            var grass = ImportedAssets.LoadTexture($"{GroundTextures}/grass1/grass1_diffuse.bmp", maxSize: 512);
            var grassNormal = ImportedAssets.LoadTexture($"{GroundTextures}/grass1/grass1_normal.bmp", normalMap: true, maxSize: 512);
            var sand = ImportedAssets.LoadTexture($"{GroundTextures}/sand1/sand1_diffuseOriginal.bmp", maxSize: 512);
            var sandNormal = ImportedAssets.LoadTexture($"{GroundTextures}/sand1/sand1_normal.bmp", normalMap: true, maxSize: 512);
            set.Ground = new[]
            {
                ImportedAssets.SurfaceMaterial("TerraceLight", grass, grassNormal, new Color(0.92f, 1f, 0.82f), smoothness: 0.05f),
                ImportedAssets.SurfaceMaterial("TerraceDark", grass, grassNormal, new Color(0.66f, 0.80f, 0.58f), smoothness: 0.05f),
                ImportedAssets.SurfaceMaterial("Laterite", sand, sandNormal, new Color(0.95f, 0.55f, 0.38f), smoothness: 0.05f)
            };
            foreach (var ground in set.Ground)
            {
                ground.SetTextureScale("_BaseMap", new Vector2(0.4f, 0.4f));
                EditorUtility.SetDirty(ground);
            }

            // Compacted laterite and gravel for the building platform under each house.
            set.Platform = ImportedAssets.SurfaceMaterial("Platform",
                ImportedAssets.LoadTexture($"{GroundTextures}/rock1/rock1_diffuseOriginal.bmp", maxSize: 512),
                ImportedAssets.LoadTexture($"{GroundTextures}/rock1/rock1_normal.bmp", normalMap: true, maxSize: 512),
                new Color(0.92f, 0.74f, 0.60f), smoothness: 0.1f);
            set.Platform.SetTextureScale("_BaseMap", new Vector2(0.6f, 0.6f));
            EditorUtility.SetDirty(set.Platform);
            return set;
        }

        private static void ConfigureAudioImport()
        {
            // Field recordings from the Nature - Essentials pack: mono, compressed, streamed from memory.
            foreach (var name in NatureClips)
            {
                if (!(AssetImporter.GetAtPath($"{NatureAudio}/{name}.wav") is AudioImporter nature)) continue;
                nature.forceToMono = true;
                nature.loadInBackground = true;
                var natureSettings = nature.defaultSampleSettings;
                natureSettings.loadType = AudioClipLoadType.CompressedInMemory;
                natureSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                natureSettings.quality = 0.35f;
                natureSettings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                natureSettings.sampleRateOverride = 22050;
                nature.defaultSampleSettings = natureSettings;
                nature.SaveAndReimport();
            }

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var isLoop = Path.GetFileName(path).StartsWith("loop_");
                importer.forceToMono = true;
                importer.loadInBackground = isLoop;
                var settings = importer.defaultSampleSettings;
                settings.loadType = isLoop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = isLoop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        // ---- Prefabs --------------------------------------------------------------------

        private sealed class PrefabSet
        {
            public GameObject Strut;
            public GameObject Pad;
            public GameObject Panel;
            public GameObject Arch;
            public GameObject Site;
            public GameObject Debris;
            public GameObject RainDrop;
            public GameObject Spark;
            public GameObject Plane;
        }

        private static PrefabSet CreatePrefabs(MaterialSet materials, SurfaceSet surfaces)
        {
            return new PrefabSet
            {
                Strut = MemberPrefab("Member_Strut", PrimitiveType.Cylinder, materials.Structure),
                Pad = MemberPrefab("Member_Pad", PrimitiveType.Cube, materials.Structure),
                Panel = MemberPrefab("Member_Panel", PrimitiveType.Cube, materials.Structure),
                Arch = MemberPrefab("Member_Arch", null, materials.Structure),
                Site = SitePrefab(surfaces.Ground, surfaces.Platform),
                Debris = DebrisPrefab(materials.Debris),
                RainDrop = EffectPrefab<RainDrop>("FX_RainDrop", PrimitiveType.Sphere, materials.Rain),
                Spark = EffectPrefab<SnapSpark>("FX_Spark", PrimitiveType.Sphere, materials.Spark),
                Plane = PlanePrefab(materials.PlaneGrid)
            };
        }

        private static GameObject MemberPrefab(string name, PrimitiveType? primitive, Material material)
        {
            var root = new GameObject(name);
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);

            GameObject body;
            if (primitive.HasValue)
            {
                body = GameObject.CreatePrimitive(primitive.Value);
                Object.DestroyImmediate(body.GetComponent<Collider>());
            }
            else
            {
                body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            }
            body.name = "Body";
            body.transform.SetParent(pivot.transform, false);
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var collider = body.AddComponent<MeshCollider>();
            collider.sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;

            var view = root.AddComponent<MemberView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("bodyFilter").objectReferenceValue = body.GetComponent<MeshFilter>();
            serialized.FindProperty("bodyRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("bodyCollider").objectReferenceValue = collider;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, $"{PrefabsFolder}/{name}.prefab");
        }

        /// <summary>A piece of imported scenery and where it stands on the plot (model metres).</summary>
        private readonly struct Scenery
        {
            public Scenery(string prefab, float size, float x, float z, float yaw = 0f, bool fitHeight = true, bool carPaint = false)
            {
                CarPaint = carPaint;
                Prefab = prefab;
                Size = size;
                Position = new Vector2(x, z);
                Yaw = yaw;
                FitHeight = fitHeight;
            }

            public string Prefab { get; }
            public float Size { get; }
            public Vector2 Position { get; }
            public float Yaw { get; }
            public bool FitHeight { get; }

            /// <summary>Repaint with a glossy car finish (for models whose own materials import without textures).</summary>
            public bool CarPaint { get; }
        }

        /// <summary>
        /// Bamboo, trees, bushes, flowers, rocks and a neighbouring clay-brick house along the plot
        /// edges, clear of every design's footprint. Heights follow the slope at runtime.
        /// </summary>
        private static readonly Scenery[] Hillside =
        {
            new Scenery($"{Polyeler}/Prefabs/Bamboo.prefab", 5.0f, -7.3f, -3.0f, 20f),
            new Scenery($"{Polyeler}/Prefabs/Bamboo.prefab", 5.5f, 7.4f, -1.4f, 140f),
            new Scenery($"{Polyeler}/Prefabs/Bamboo.prefab", 4.5f, -7.4f, 6.0f, 75f),
            new Scenery($"{Polyeler}/Prefabs/Bamboo.prefab", 5.0f, 7.2f, 7.4f, 200f),
            new Scenery($"{Polyeler}/Prefabs/LittleBamboo.prefab", 2.5f, 7.4f, 3.0f, 30f),
            new Scenery($"{Polyeler}/Prefabs/LittleBamboo.prefab", 2.5f, -7.3f, 2.0f, 110f),
            new Scenery($"{SimpleNature}/Tree_01.prefab", 4.5f, 7.0f, -5.6f, 15f),
            new Scenery($"{SimpleNature}/Tree_03.prefab", 4.0f, -3.5f, 8.0f, 60f),
            new Scenery($"{SimpleNature}/Tree_05.prefab", 3.5f, 4.0f, 8.1f, 230f),
            new Scenery($"{SimpleNature}/Bush_01.prefab", 1.0f, -6.9f, -1.2f),
            new Scenery($"{SimpleNature}/Bush_02.prefab", 1.1f, 6.9f, 5.6f, 90f),
            new Scenery($"{SimpleNature}/Bush_03.prefab", 0.9f, -1.8f, 8.2f, 45f),
            new Scenery($"{SimpleNature}/Flowers_01.prefab", 0.5f, 1.8f, 8.2f),
            new Scenery($"{SimpleNature}/Flowers_02.prefab", 0.5f, -7.0f, 4.3f, 70f),
            new Scenery($"{SimpleNature}/Flowers_01.prefab", 0.5f, 7.0f, -3.6f, 160f),
            new Scenery($"{Stones}/ST_Stone1.prefab", 0.9f, 6.9f, 1.6f, 40f, false),
            new Scenery($"{Stones}/ST_Stone3.prefab", 0.8f, -7.1f, 0.6f, 120f, false),
            new Scenery($"{Stones}/ST_Stone5.prefab", 0.7f, -2.9f, -6.1f, 10f, false),
            new Scenery($"{SimpleNature}/Rock_02.prefab", 0.6f, 2.6f, 8.3f, 0f, false),
            new Scenery($"{SimpleNature}/Rock_04.prefab", 0.8f, 4.0f, -6.1f, 80f, false),
            new Scenery($"{SimpleNature}/Grass_01.prefab", 0.4f, 6.8f, -0.4f),
            new Scenery($"{SimpleNature}/Grass_02.prefab", 0.4f, -6.9f, 7.6f, 50f),
            new Scenery($"{SimpleNature}/Grass_01.prefab", 0.35f, 0.9f, 8.4f, 130f),
            new Scenery("Assets/Brick_House/Prefabs/Brick_House_2.79.prefab", 3.6f, -6.0f, -5.3f, 90f, false),
            // A real-size car (Nissan 180MX by Grzybek) parked uphill, for scale.
            new Scenery($"{ImportedModels}/Nissan180MX.fbx", 4.4f, 4.6f, -5.7f, 90f, false, carPaint: true)
        };

        private static GameObject SitePrefab(Material[] groundMaterials, Material platformMaterial)
        {
            var root = new GameObject("Site_Plot");
            var content = new GameObject("Content").transform;
            content.SetParent(root.transform, false);
            var plot = new GameObject("Plot", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            plot.transform.SetParent(content, false);
            var renderer = plot.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = groundMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var platform = new GameObject("Building platform", typeof(MeshFilter), typeof(MeshRenderer));
            platform.transform.SetParent(content, false);
            var platformRenderer = platform.GetComponent<MeshRenderer>();
            platformRenderer.sharedMaterial = platformMaterial;
            platformRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var structure = new GameObject("Structure").transform;
            structure.SetParent(content, false);

            var trees = new List<Transform>();
            var sceneryRoot = new GameObject("Scenery").transform;
            sceneryRoot.SetParent(content, false);
            var triangles = 0;
            foreach (var item in Hillside)
            {
                var prefab = ImportedAssets.LoadPrefab(item.Prefab);
                if (prefab == null) continue;
                var prop = ImportedAssets.Instantiate(prefab, item.Size, item.FitHeight, null, prefab.name);
                if (item.CarPaint)
                {
                    var paint = SaveMaterial("CarPaint", Shader.Find("Universal Render Pipeline/Lit"), new Color(0.12f, 0.32f, 0.55f), smoothness: 0.75f);
                    paint.SetFloat("_Metallic", 0.4f);
                    foreach (var part in prop.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = part.sharedMaterials;
                        for (var i = 0; i < materials.Length; i++) materials[i] = paint;
                        part.sharedMaterials = materials;
                    }
                }
                foreach (var filter in prop.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) triangles += filter.sharedMesh.triangles.Length / 3;
                trees.Add(ImportedAssets.Grounded(prop, sceneryRoot, new Vector3(item.Position.x, 0f, item.Position.y), item.Yaw, prefab.name));
            }
            Debug.Log($"[Fatiro] Hillside scenery: {trees.Count} props, about {triangles} triangles including LODs.");

            var site = root.AddComponent<SiteController>();
            var serialized = new SerializedObject(site);
            serialized.FindProperty("content").objectReferenceValue = content;
            serialized.FindProperty("structureRoot").objectReferenceValue = structure;
            serialized.FindProperty("plotFilter").objectReferenceValue = plot.GetComponent<MeshFilter>();
            serialized.FindProperty("plotCollider").objectReferenceValue = plot.GetComponent<MeshCollider>();
            serialized.FindProperty("platformFilter").objectReferenceValue = platform.GetComponent<MeshFilter>();
            var treeArray = serialized.FindProperty("trees");
            treeArray.arraySize = trees.Count;
            for (var i = 0; i < trees.Count; i++) treeArray.GetArrayElementAtIndex(i).objectReferenceValue = trees[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, $"{PrefabsFolder}/Site_Plot.prefab");
        }

        /// <summary>Collapse debris: broken planks and logs from the Static Soul wood pack, picked at random per piece.</summary>
        private static GameObject DebrisPrefab(Material fallbackMaterial)
        {
            const string folder = "Assets/Static Soul Studio/Wood Pack/Built-in/Prefabs";
            var meshes = new List<Mesh>();
            Material woodMaterial = null;
            var largest = 0f;
            foreach (var name in new[] { "Broken_plank_1", "Broken_plank_2", "Broken_plank_3", "Broken_plank_4", "Broken_plank_5", "Broken_plank_6", "Broken log_1", "Broken log_2" })
            {
                var prefab = ImportedAssets.LoadPrefab($"{folder}/{name}.prefab");
                var filter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null) continue;
                meshes.Add(filter.sharedMesh);
                largest = Mathf.Max(largest, filter.sharedMesh.bounds.size.magnitude);
                woodMaterial ??= ImportedAssets.ToUrp(prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            }

            if (meshes.Count == 0) return EffectPrefab<DebrisPiece>("FX_Debris", PrimitiveType.Cube, fallbackMaterial);

            var root = new GameObject("FX_Debris", typeof(MeshFilter), typeof(MeshRenderer));
            var meshFilter = root.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = meshes[0];
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = woodMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var debris = root.AddComponent<DebrisPiece>();
            var serialized = new SerializedObject(debris);
            serialized.FindProperty("effectRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("meshFilter").objectReferenceValue = meshFilter;
            // About 0.6 model metres across, so a plank reads as a splinter of the member it came from.
            serialized.FindProperty("size").floatValue = 0.6f / Mathf.Max(largest, 0.01f);
            var variants = serialized.FindProperty("variants");
            variants.arraySize = meshes.Count;
            for (var i = 0; i < meshes.Count; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = meshes[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, $"{PrefabsFolder}/FX_Debris.prefab");
        }

        private static GameObject EffectPrefab<T>(string name, PrimitiveType primitive, Material material) where T : PooledEffect
        {
            var root = GameObject.CreatePrimitive(primitive);
            root.name = name;
            Object.DestroyImmediate(root.GetComponent<Collider>());
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var effect = root.AddComponent<T>();
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("effectRenderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, $"{PrefabsFolder}/{name}.prefab");
        }

        /// <summary>Detected planes are drawn as a dotted grid with an outline (FR-02).</summary>
        private static GameObject PlanePrefab(Material grid)
        {
            var root = new GameObject("AR_Plane", typeof(ARPlane), typeof(ARPlaneMeshVisualizer), typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer));
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = grid;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var line = root.GetComponent<LineRenderer>();
            line.widthMultiplier = 0.004f;
            line.loop = true;
            line.useWorldSpace = false;
            line.sharedMaterial = grid;
            line.startColor = line.endColor = new Color(1f, 0.75f, 0.35f, 0.9f);
            return SavePrefab(root, $"{PrefabsFolder}/AR_Plane.prefab");
        }

        private static GameObject SavePrefab(GameObject instance, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        // ---- Creature skins (decorative, Addressable) ------------------------------------

        private static Dictionary<string, GameObject> CreateSkins()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var black = SaveMaterial("Skin_Black", lit, new Color(0.06f, 0.06f, 0.07f), smoothness: 0.6f);
            var grey = SaveMaterial("Skin_Grey", lit, new Color(0.55f, 0.55f, 0.58f), smoothness: 0.2f);
            var ivory = SaveMaterial("Skin_Ivory", lit, new Color(0.95f, 0.92f, 0.82f), smoothness: 0.5f);
            var red = SaveMaterial("Skin_Red", lit, new Color(0.75f, 0.08f, 0.08f), smoothness: 0.8f);
            var eyes = SaveMaterial("Skin_Eyes", lit, new Color(1f, 0.7f, 0.1f), smoothness: 0.9f, emission: new Color(1.6f, 0.9f, 0.1f));
            var lights = SaveMaterial("Skin_Lights", lit, new Color(1f, 0.95f, 0.7f), smoothness: 0.9f, emission: new Color(1.5f, 1.4f, 0.9f));
            var tail = SaveMaterial("Skin_TailLights", lit, new Color(1f, 0.1f, 0.1f), smoothness: 0.9f, emission: new Color(1.5f, 0.1f, 0.1f));

            var skins = new Dictionary<string, GameObject>();

            // Ingwe: head over the cantilever, ears, glowing eyes, curling tail behind the haunches.
            // Preferred: the Poly by Google tiger, recoloured as a black panther, crouched over the ribs.
            var panther = new GameObject("Skin_PN-01");
            var pantherFur = SaveMaterial("Skin_PantherFur", lit, new Color(0.07f, 0.07f, 0.08f), smoothness: 0.15f);
            if (!FitModel(panther, $"{ImportedModels}/Tiger.glb", 13.5f, 1.6f, 0.1f, pantherFur))
            {
            Piece(panther, PrimitiveType.Sphere, black, new Vector3(0f, 3.75f, 7.1f), new Vector3(2.4f, 1.6f, 2.2f));
            Piece(panther, PrimitiveType.Capsule, black, new Vector3(-0.8f, 4.55f, 6.9f), new Vector3(0.45f, 0.5f, 0.3f), new Vector3(0, 0, 20));
            Piece(panther, PrimitiveType.Capsule, black, new Vector3(0.8f, 4.55f, 6.9f), new Vector3(0.45f, 0.5f, 0.3f), new Vector3(0, 0, -20));
            Piece(panther, PrimitiveType.Sphere, eyes, new Vector3(-0.55f, 3.95f, 8.1f), new Vector3(0.38f, 0.22f, 0.2f));
            Piece(panther, PrimitiveType.Sphere, eyes, new Vector3(0.55f, 3.95f, 8.1f), new Vector3(0.38f, 0.22f, 0.2f));
            for (var i = 0; i < 6; i++)
            {
                var t = i / 5f;
                Piece(panther, PrimitiveType.Sphere, black, new Vector3(0.4f * Mathf.Sin(t * 3f), 3.4f + 1.4f * t, -4.4f - 1.2f * t + 0.6f * t * t),
                    Vector3.one * Mathf.Lerp(0.55f, 0.3f, t));
            }
            }
            skins["PN-01"] = SavePrefab(panther, $"{SkinsFolder}/Skin_PN-01.prefab");

            // Inzovu: head at the front arch, eyes and ivory tusks; the trunk is the real drain member.
            // Preferred: the Poly by Google elephant standing over the legs and body arches.
            var elephant = new GameObject("Skin_EL-01");
            if (!FitModel(elephant, $"{ImportedModels}/Elephant.glb", 11.0f, 0.6f, 0.2f, null))
            {
            Piece(elephant, PrimitiveType.Sphere, grey, new Vector3(0f, 5.0f, 3.2f), new Vector3(2.6f, 2.4f, 1.6f));
            Piece(elephant, PrimitiveType.Sphere, black, new Vector3(-0.75f, 5.4f, 3.9f), new Vector3(0.25f, 0.25f, 0.15f));
            Piece(elephant, PrimitiveType.Sphere, black, new Vector3(0.75f, 5.4f, 3.9f), new Vector3(0.25f, 0.25f, 0.15f));
            Piece(elephant, PrimitiveType.Capsule, ivory, new Vector3(-0.7f, 4.1f, 4.2f), new Vector3(0.22f, 0.8f, 0.22f), new Vector3(60, 0, 0));
            Piece(elephant, PrimitiveType.Capsule, ivory, new Vector3(0.7f, 4.1f, 4.2f), new Vector3(0.22f, 0.8f, 0.22f), new Vector3(60, 0, 0));
            Piece(elephant, PrimitiveType.Capsule, grey, new Vector3(0f, 5.2f, -2.9f), new Vector3(0.15f, 0.9f, 0.15f), new Vector3(-30, 0, 0));
            }
            skins["EL-01"] = SavePrefab(elephant, $"{SkinsFolder}/Skin_EL-01.prefab");

            // Imodoka: a real low-poly car sized to the stilted deck.
            var car = new GameObject("Skin_CR-01");
            var carPrefab = ImportedAssets.LoadPrefab("Assets/Alstra Infinite/Vehicles LowPoly/Prefabs/Version 1.2/MuscleCarV1.prefab");
            if (FitModel(car, $"{ImportedModels}/DodgeChallenger.glb", 8.6f, 0.3f, 2.25f, null))
            {
                // The Dodge Challenger (Grzybek, Poly Pizza) sourced for this project.
            }
            else if (carPrefab != null)
            {
                var body = ImportedAssets.Instantiate(carPrefab, 8.6f, false, null, "Muscle car");
                var bounds = ImportedAssets.Bounds(body);
                if (bounds.size.x > bounds.size.z) body.transform.Rotate(0f, 90f, 0f, Space.World);
                bounds = ImportedAssets.Bounds(body);
                // Wheels on the deck (y 2.2), centred over the stilts, nose pointing downhill.
                body.transform.position += new Vector3(-bounds.center.x, 2.25f - bounds.min.y, 0.3f - bounds.center.z);
                body.transform.SetParent(car.transform, true);
            }
            else
            {
                foreach (var x in new[] { -2.05f, 2.05f })
                foreach (var z in new[] { -2.7f, 2.9f })
                    Piece(car, PrimitiveType.Cylinder, black, new Vector3(x, 2.25f, z), new Vector3(1.3f, 0.18f, 1.3f), new Vector3(0, 0, 90));
                Piece(car, PrimitiveType.Sphere, lights, new Vector3(-1.2f, 2.7f, 4.75f), new Vector3(0.6f, 0.25f, 0.2f));
                Piece(car, PrimitiveType.Sphere, lights, new Vector3(1.2f, 2.7f, 4.75f), new Vector3(0.6f, 0.25f, 0.2f));
                Piece(car, PrimitiveType.Cube, tail, new Vector3(0f, 2.9f, -4.25f), new Vector3(2.6f, 0.15f, 0.08f));
                Piece(car, PrimitiveType.Cube, red, new Vector3(0f, 4.15f, -3.9f), new Vector3(3.2f, 0.08f, 0.6f), new Vector3(-8, 0, 0));
            }
            skins["CR-01"] = SavePrefab(car, $"{SkinsFolder}/Skin_CR-01.prefab");
            return skins;
        }

        /// <summary>
        /// Places an imported model (glb via glTFast, or fbx) inside a skin: scaled so its length matches,
        /// turned to run along +Z (downhill), centred over the structure and sat at the given height.
        /// Returns false if the model is not in the project, so the caller can fall back to primitives.
        /// </summary>
        private static bool FitModel(GameObject skin, string path, float length, float centreZ, float baseY, Material overrideMaterial)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[Fatiro] Skin model not found at {path}; using the primitive skin.");
                return false;
            }

            var model = ImportedAssets.Instantiate(asset, length, false, null, Path.GetFileNameWithoutExtension(path));
            var bounds = ImportedAssets.Bounds(model);
            if (bounds.size.x > bounds.size.z) model.transform.Rotate(0f, 90f, 0f, Space.World);

            if (overrideMaterial != null)
            {
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++) materials[i] = overrideMaterial;
                    renderer.sharedMaterials = materials;
                }
            }

            bounds = ImportedAssets.Bounds(model);
            model.transform.position += new Vector3(-bounds.center.x, baseY - bounds.min.y, centreZ - bounds.center.z);
            model.transform.SetParent(skin.transform, true);
            return true;
        }

        private static void Piece(GameObject parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Vector3 euler = default)
        {
            var piece = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.transform.SetParent(parent.transform, false);
            piece.transform.localPosition = position;
            piece.transform.localEulerAngles = euler;
            piece.transform.localScale = scale;
            var renderer = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---- Design data ----------------------------------------------------------------

        private static List<MaterialData> CreateMaterialData(SurfaceSet surfaces)
        {
            var colours = new Dictionary<string, (Color colour, string text)>
            {
                { MaterialLibrary.Bamboo, (new Color(0.78f, 0.80f, 0.36f), "Light, cheap and flexible. Grows fast in Rwanda.") },
                { MaterialLibrary.Timber, (new Color(0.62f, 0.42f, 0.24f), "Moderate strength and cost.") },
                { MaterialLibrary.Steel, (new Color(0.62f, 0.66f, 0.72f), "Very strong in every direction, but costly.") },
                { MaterialLibrary.Concrete, (new Color(0.80f, 0.80f, 0.78f), "Strong in compression, heavy.") },
                { MaterialLibrary.EarthBlock, (new Color(0.76f, 0.45f, 0.30f), "Cheap and local; good in compression, poor in bending.") }
            };

            var result = new List<MaterialData>();
            foreach (var spec in MaterialLibrary.CreateDefaults())
            {
                var path = $"{DataFolder}/Material_{spec.Id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<MaterialData>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<MaterialData>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                var surface = surfaces.Members[spec.Id];
                asset.Configure(spec, colours[spec.Id].colour, colours[spec.Id].text, surface.material, surface.tileMetres);
                EditorUtility.SetDirty(asset);
                result.Add(asset);
            }
            return result;
        }

        private static List<HouseDesign> CreateDesigns(Dictionary<string, GameObject> skins)
        {
            var accents = new Dictionary<string, Color>
            {
                { Blueprints.PantherId, new Color(0.55f, 0.35f, 0.95f) },
                { Blueprints.ElephantId, new Color(0.55f, 0.62f, 0.70f) },
                { Blueprints.CarId, new Color(0.90f, 0.25f, 0.25f) }
            };
            var focus = new Dictionary<string, LoadCaseKind[]>
            {
                { Blueprints.PantherId, new[] { LoadCaseKind.Seismic, LoadCaseKind.Live } },
                { Blueprints.ElephantId, new[] { LoadCaseKind.Wind, LoadCaseKind.Rain } },
                { Blueprints.CarId, new[] { LoadCaseKind.Seismic } }
            };

            var designs = new List<HouseDesign>();
            var order = 0;
            foreach (var blueprint in Blueprints.All())
            {
                var path = $"{DataFolder}/Design_{blueprint.DesignId}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<HouseDesign>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<HouseDesign>();
                    AssetDatabase.CreateAsset(asset, path);
                }

                var skinPath = AssetDatabase.GetAssetPath(skins[blueprint.DesignId]);
                var skinGuid = AssetDatabase.AssetPathToGUID(skinPath);
                asset.Configure(blueprint, accents[blueprint.DesignId], order++, focus[blueprint.DesignId], new AssetReferenceGameObject(skinGuid),
                    skinLiesOnSlope: blueprint.DesignId != Blueprints.CarId);
                EditorUtility.SetDirty(asset);
                designs.Add(asset);
            }
            return designs;
        }

        // ---- Addressables ---------------------------------------------------------------

        private static void ConfigureAddressables(PrefabSet prefabs, Dictionary<string, GameObject> skins, List<MaterialData> materials,
            List<HouseDesign> designs, MaterialSet materialSet)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.AddLabel(AssetKeys.DesignLabel);
            settings.AddLabel(AssetKeys.MaterialLabel);
            settings.AddLabel(AssetKeys.AudioLabel);

            var catalogue = Group(settings, "Catalogue", BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            foreach (var design in designs) Entry(settings, design, catalogue, $"Designs/{design.DesignId}", AssetKeys.DesignLabel);
            foreach (var material in materials) Entry(settings, material, catalogue, $"Materials/{material.MaterialId}", AssetKeys.MaterialLabel);

            var audio = Group(settings, "Audio", BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                Entry(settings, clip, audio, $"Audio/{clip.name}", AssetKeys.AudioLabel);
            }
            foreach (var name in NatureClips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{NatureAudio}/{name}.wav");
                if (clip != null) Entry(settings, clip, audio, $"Audio/{name}", AssetKeys.AudioLabel);
            }

            var session = Group(settings, "Session Prefabs", BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            Entry(settings, prefabs.Strut, session, AssetKeys.StrutPrefab);
            Entry(settings, prefabs.Pad, session, AssetKeys.PadPrefab);
            Entry(settings, prefabs.Panel, session, AssetKeys.PanelPrefab);
            Entry(settings, prefabs.Arch, session, AssetKeys.ArchPrefab);
            Entry(settings, materialSet.Ghost, session, AssetKeys.GhostMaterial);
            Entry(settings, prefabs.Site, session, AssetKeys.SitePrefab);
            Entry(settings, prefabs.Debris, session, AssetKeys.DebrisPrefab);
            Entry(settings, prefabs.RainDrop, session, AssetKeys.RainDropPrefab);
            Entry(settings, prefabs.Spark, session, AssetKeys.SparkPrefab);

            // Typefaces (SIL Open Font License): DM Serif Display for titles, Lato for text.
            var ui = Group(settings, "Interface", BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            Entry(settings, AssetDatabase.LoadAssetAtPath<Font>($"{Generated}/Fonts/DMSerifDisplay-Regular.ttf"), ui, AssetKeys.DisplayFont);
            Entry(settings, AssetDatabase.LoadAssetAtPath<Font>($"{Generated}/Fonts/Lato-Regular.ttf"), ui, AssetKeys.BodyFont);
            Entry(settings, AssetDatabase.LoadAssetAtPath<Font>($"{Generated}/Fonts/Lato-Bold.ttf"), ui, AssetKeys.BoldFont);
            Entry(settings, AssetDatabase.LoadAssetAtPath<Font>($"{Generated}/Fonts/Lato-Italic.ttf"), ui, AssetKeys.ItalicFont);

            // One bundle per skin so a finished build loads only its own creature.
            var skinGroup = Group(settings, "Creature Skins", BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
            foreach (var pair in skins) Entry(settings, pair.Value, skinGroup, $"Skins/{pair.Key}");

            EditorUtility.SetDirty(settings);
        }

        private static AddressableAssetGroup Group(AddressableAssetSettings settings, string name, BundledAssetGroupSchema.BundlePackingMode packing)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode = packing;
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            schema.IncludeInBuild = true;
            EditorUtility.SetDirty(group);
            return group;
        }

        private static void Entry(AddressableAssetSettings settings, Object asset, AddressableAssetGroup group, string address, string label = null)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            if (label != null) entry.SetLabel(label, true, true, false);
        }

        // ---- Scene ----------------------------------------------------------------------

        private static void CreateScene(MaterialSet materials)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var session = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));

            var origin = new GameObject("XR Origin", typeof(XROrigin));
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(origin.transform, false);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(ARCameraManager),
                typeof(ARCameraBackground), typeof(TrackedPoseDriver));
            cameraObject.transform.SetParent(offset.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 30f;
            cameraObject.transform.localPosition = new Vector3(0f, 0.45f, 0.55f);
            cameraObject.transform.localRotation = Quaternion.Euler(38f, 180f, 0f);

            var poseDriver = cameraObject.GetComponent<TrackedPoseDriver>();
            var position = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            position.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotation = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
            poseDriver.positionInput = new InputActionProperty(position);
            poseDriver.rotationInput = new InputActionProperty(rotation);

            var xrOrigin = origin.GetComponent<XROrigin>();
            xrOrigin.Camera = camera;
            xrOrigin.CameraFloorOffsetObject = offset;

            var planeManager = origin.AddComponent<ARPlaneManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planeManager.planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/AR_Plane.prefab");
            var raycastManager = origin.AddComponent<ARRaycastManager>();
            var anchorManager = origin.AddComponent<ARAnchorManager>();

            var light = new GameObject("Directional Light", typeof(Light));
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var lightComponent = light.GetComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.1f;
            lightComponent.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var fatiro = new GameObject("Fatiro");
            var world = new GameObject("World").transform;
            world.SetParent(fatiro.transform, false);

            var ar = fatiro.AddComponent<ARManager>();
            Set(ar, "session", session.GetComponent<ARSession>());
            Set(ar, "planeManager", planeManager);
            Set(ar, "raycastManager", raycastManager);
            Set(ar, "anchorManager", anchorManager);
            Set(ar, "arCamera", camera);

            var input = fatiro.AddComponent<InputRouter>();

            var audioObject = new GameObject("Audio");
            audioObject.transform.SetParent(fatiro.transform, false);
            var effectsSource = audioObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            var ambienceSource = audioObject.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            var worldSource = audioObject.AddComponent<AudioSource>();
            worldSource.playOnAwake = false;
            worldSource.loop = true;
            var audio = audioObject.AddComponent<AudioManager>();
            Set(audio, "effects", effectsSource);
            Set(audio, "ambience", ambienceSource);
            Set(audio, "world", worldSource);

            var effects = new GameObject("Effects").AddComponent<EffectsController>();
            effects.transform.SetParent(fatiro.transform, false);
            var ui = new GameObject("UI").AddComponent<UIManager>();
            ui.transform.SetParent(fatiro.transform, false);

            var bootstrap = fatiro.AddComponent<AppBootstrap>();
            Set(bootstrap, "arManager", ar);
            Set(bootstrap, "inputRouter", input);
            Set(bootstrap, "audioManager", audio);
            Set(bootstrap, "effects", effects);
            Set(bootstrap, "uiManager", ui);
            Set(bootstrap, "worldRoot", world);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void Set(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new System.InvalidOperationException($"{target.GetType().Name} has no field '{property}'.");
            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { Generated, MaterialsFolder, PrefabsFolder, DataFolder, AudioFolder, SkinsFolder, TexturesFolder, Root + "/Scenes" })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }
    }
}
