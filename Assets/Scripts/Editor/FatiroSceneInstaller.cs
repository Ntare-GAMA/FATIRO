#if UNITY_EDITOR
using Fatiro.AR;
using Fatiro.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace Fatiro.Editor
{
    public static class FatiroSceneInstaller
    {
        [MenuItem("Fatiro/Install AR Simulation Scene")]
        public static void Install()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var arSession = new GameObject("AR Session");
            arSession.AddComponent<ARSession>();
            arSession.AddComponent<ARInputManager>();

            var xrOriginObject = new GameObject("XR Origin");
            var xrOrigin = xrOriginObject.AddComponent<XROrigin>();
            var cameraObject = new GameObject("AR Camera");
            cameraObject.transform.SetParent(xrOriginObject.transform);
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();
            xrOrigin.Camera = cameraObject.GetComponent<Camera>();
            xrOriginObject.AddComponent<ARPlaneManager>().requestedDetectionMode = PlaneDetectionMode.Horizontal;
            xrOriginObject.AddComponent<ARRaycastManager>();
            xrOriginObject.AddComponent<ARAnchorManager>();

            var stateMachineObject = new GameObject("Simulation Systems");
            var stateMachine = stateMachineObject.AddComponent<SimulationStateMachine>();
            var simulationManager = stateMachineObject.AddComponent<SimulationManager>();
            var placement = stateMachineObject.AddComponent<ARPlacementController>();
            var bootstrap = stateMachineObject.AddComponent<SimulationBootstrap>();
            var loader = stateMachineObject.AddComponent<AddressableSimulationLoader>();

            var simulationRoot = CreateSimulationPrefab(simulationManager);
            var previewRoot = CreatePrimitivePrefab("PlacementPreview", PrimitiveType.Cylinder, new Color(0.2f, 0.8f, 1f, 0.35f));
            var placementSerialized = new SerializedObject(placement);
            placementSerialized.FindProperty("raycastManager").objectReferenceValue = xrOriginObject.GetComponent<ARRaycastManager>();
            placementSerialized.FindProperty("planeManager").objectReferenceValue = xrOriginObject.GetComponent<ARPlaneManager>();
            placementSerialized.FindProperty("anchorManager").objectReferenceValue = xrOriginObject.GetComponent<ARAnchorManager>();
            placementSerialized.FindProperty("placementPreview").objectReferenceValue = previewRoot;
            placementSerialized.FindProperty("simulationPrefab").objectReferenceValue = simulationRoot;
            placementSerialized.ApplyModifiedPropertiesWithoutUndo();

            var managerSerialized = new SerializedObject(simulationManager);
            managerSerialized.FindProperty("stateMachine").objectReferenceValue = stateMachine;
            managerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var bootstrapSerialized = new SerializedObject(bootstrap);
            bootstrapSerialized.FindProperty("stateMachine").objectReferenceValue = stateMachine;
            bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/FatiroARSimulation.unity");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = stateMachineObject;
        }

        private static GameObject CreateSimulationPrefab(SimulationManager manager)
        {
            var root = new GameObject("Simulation");
            CreateEntity(root.transform, "Entity A", new Vector3(-0.25f, 0.1f, 0f), Color.red, manager);
            CreateEntity(root.transform, "Entity B", new Vector3(0f, 0.1f, 0f), Color.green, manager);
            CreateEntity(root.transform, "Entity C", new Vector3(0.25f, 0.1f, 0f), Color.blue, manager);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Simulation.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateEntity(Transform parent, string name, Vector3 position, Color color, SimulationManager manager)
        {
            var entity = GameObject.CreatePrimitive(PrimitiveType.Cube);
            entity.name = name;
            entity.transform.SetParent(parent);
            entity.transform.localPosition = position;
            entity.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color);
            var interaction = entity.AddComponent<InteractionEntity>();
            var serialized = new SerializedObject(interaction);
            serialized.FindProperty("simulationManager").objectReferenceValue = manager;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePrimitivePrefab(string name, PrimitiveType type, Color color)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color);
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"Assets/{name}.prefab");
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static Material CreateMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            return material;
        }
    }
}
#endif
