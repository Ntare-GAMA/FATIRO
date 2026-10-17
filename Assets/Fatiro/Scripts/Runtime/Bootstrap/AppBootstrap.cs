using System;
using System.Collections.Generic;
using Fatiro.AR;
using Fatiro.Audio;
using Fatiro.Build;
using Fatiro.Content;
using Fatiro.Core;
using Fatiro.Data;
using Fatiro.Domain;
using Fatiro.Input;
using Fatiro.Simulation;
using Fatiro.States;
using Fatiro.UI;
using Fatiro.World;
using UnityEngine;

namespace Fatiro.Bootstrap
{
    /// <summary>
    /// Composition root. Creates every service once and passes interfaces to the classes that
    /// need them (dependency injection), so no manager is a global singleton (SRS 4.2).
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private ARManager arManager;
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private EffectsController effects;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private Transform worldRoot;
        [Tooltip("Frame-rate cap. AR camera frames arrive at 30 Hz, so 30 keeps rendering smooth and saves battery (NFR-01, NFR-16).")]
        [SerializeField] private int targetFrameRate = 30;

        private EventBus events;
        private ContentCatalogue catalogue;
        private DataManager data;
        private SimulationManager simulation;
        private SimulationContext context;

        /// <summary>The controller once start-up has finished; null until then. Used by tests and tooling.</summary>
        public ISimulationController Simulation => simulation;

        private async void Start()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            events = new EventBus();
            catalogue = new ContentCatalogue();
            data = new DataManager(events, Application.persistentDataPath);

            IReadOnlyList<MaterialData> materials;
            IReadOnlyList<HouseDesign> designs;
            IReadOnlyDictionary<string, AudioClip> clips;
            try
            {
                materials = await catalogue.LoadMaterialsAsync();
                designs = await catalogue.LoadDesignsAsync();
                clips = await catalogue.LoadAudioAsync();
                UIKit.UseFonts(
                    await catalogue.LoadAsync<Font>(AssetKeys.DisplayFont, AssetScope.App),
                    await catalogue.LoadAsync<Font>(AssetKeys.BodyFont, AssetScope.App),
                    await catalogue.LoadAsync<Font>(AssetKeys.BoldFont, AssetScope.App),
                    await catalogue.LoadAsync<Font>(AssetKeys.ItalicFont, AssetScope.App));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[Fatiro] Addressable content is missing. Run Fatiro > Build Project Content in the editor.");
                return;
            }

            var materialById = new Dictionary<string, MaterialData>();
            foreach (var material in materials) materialById[material.MaterialId] = material;

            var entities = new EntityManager();
            var tasks = new TaskManager(events);
            context = new SimulationContext
            {
                Events = events,
                AR = arManager,
                Content = catalogue,
                Entities = entities,
                Tasks = tasks,
                Build = new BuildManager(events, entities, tasks),
                LoadTests = new LoadTestManager(events, entities, new StructuralSolver()),
                Score = new ScoreManager(new WeightedScoringPolicy()),
                Data = data,
                Audio = audioManager,
                Effects = effects,
                WorldRoot = worldRoot,
                Designs = designs,
                Materials = materialById
            };

            arManager.Initialise(events);
            audioManager.Initialise(events, data.Settings, clips);
            simulation = new SimulationManager(context, inputRouter);
            uiManager.Initialise(simulation, events, audioManager, catalogue, PoolStats);

            CameraPermission.Request(granted => events.Publish(new CameraPermissionResult(granted)));

            Debug.Log($"[Fatiro] Ready: {designs.Count} designs, {materials.Count} materials, {clips.Count} sounds. Data at {Application.persistentDataPath}");
        }

        private void Update() => simulation?.Tick(Time.deltaTime);

        private void OnApplicationPause(bool paused)
        {
            if (paused) data?.Autosave();
        }

        private void OnDestroy()
        {
            data?.Autosave();
            data?.Dispose();
            catalogue?.ReleaseAll();
            Time.timeScale = 1f;
        }

        private IEnumerable<IPoolStats> PoolStats()
        {
            foreach (var pool in effects.Pools) yield return pool;
            if (context?.Views == null) yield break;
            foreach (var pool in context.Views.Pools) yield return pool;
        }
    }
}
