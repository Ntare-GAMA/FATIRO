using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fatiro.Content;
using Fatiro.Core;
using Fatiro.Domain;
using Fatiro.Input;
using Fatiro.World;
using UnityEngine;

namespace Fatiro.States
{
    /// <summary>Requests the UI can make. Commands flow down through this interface (SRS 4.2).</summary>
    public interface ISimulationController
    {
        SimulationStateId CurrentStateId { get; }
        SimulationContext Context { get; }
        bool IsBusy { get; }
        bool CanEditStructure { get; }
        bool CanEndSession { get; }
        bool PausedByTracking { get; }
        bool Hologram { get; }
        void SetHologram(bool hologram);

        void StartNewSession();
        Task ConfirmSetup(HouseDesign design, int slopeDegrees, SoilType soil, BuildMode mode);
        Task ResumeAutosave();
        void BackToMenu();

        void ConfirmPlacement();
        void Reposition();

        void SelectTrayMember(MemberSpec spec);
        void BeginMemberDrag(MemberSpec spec);
        void DragMember(Vector2 screenPosition);
        void EndMemberDrag(Vector2 screenPosition);
        void SelectMember(string memberId);
        void ChangeMaterial(string memberId, string materialId);
        void RemoveMember(string memberId);
        void Undo();
        void Redo();

        void OpenTestBench();
        void RunLoadTest(LoadCaseConfig config);
        void ModifyStructure();

        void EndSession();
        void Restart();
        void ReturnToMenu();

        void Pause();
        void Resume();
    }

    /// <summary>
    /// Owns the session state machine and is the only system that starts transitions (SRS 4.1).
    /// UI requests are forwarded to the current state, which decides whether they are legal.
    /// </summary>
    public sealed class SimulationManager : ISimulationController
    {
        private readonly Dictionary<SimulationStateId, SimulationState> states = new Dictionary<SimulationStateId, SimulationState>();
        private SimulationState current;
        private SimulationStateId history = SimulationStateId.Building;
        private readonly Dictionary<Renderer, Material[]> skinMaterials = new Dictionary<Renderer, Material[]>();
        private Material hologramMaterial;

        public SimulationManager(SimulationContext context, InputRouter input)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Register(new StartState(this));
            Register(new SetupState(this));
            Register(new PlacementState(this));
            Register(new BuildingState(this));
            Register(new ReviewingState(this));
            Register(new LoadTestingState(this));
            Register(new PausedState(this));
            Register(new EndedState(this));

            input.GestureRecognised += gesture => current?.HandleGesture(gesture);
            Context.Events.Subscribe<TrackingLostTooLong>(_ => PauseForTracking());
            Context.Events.Subscribe<TrackingRecovered>(_ => ResumeAfterTracking());
            Context.Events.Subscribe<StageCompleted>(OnStageCompleted);

            current = states[SimulationStateId.Start];
            current.Enter(false);
        }

        public SimulationContext Context { get; }
        public SimulationStateId CurrentStateId => current.Id;

        /// <summary>The state being entered while a transition is in progress.</summary>
        public SimulationStateId TransitionTarget { get; private set; }

        public bool IsBusy { get; private set; }
        public bool PausedByTracking { get; private set; }
        public bool IsRepositioning { get; private set; }
        public LoadCaseConfig PendingTest { get; private set; }
        public bool CanEditStructure => current.CanEditStructure;
        public bool CanEndSession => current.CanEndSession;

        public T State<T>() where T : SimulationState
        {
            foreach (var state in states.Values)
                if (state is T typed) return typed;
            return null;
        }

        public void Tick(float deltaTime)
        {
            if (Context.Site != null) Context.Effects.SetWorldScale(Context.Site.WorldScale);
            current.Tick(deltaTime);
        }

        public void ChangeState(SimulationStateId next, bool resumed = false)
        {
            if (current.Id == next) return;
            var previous = current.Id;
            TransitionTarget = next;
            current.Exit();
            current = states[next];
            current.Enter(resumed);
            TransitionTarget = next;
            Context.Events.Publish(new StateChanged(previous, next));
        }

        // ---- Menu and setup ------------------------------------------------------------

        public void StartNewSession()
        {
            if (Context.Data.ActiveProfile == null)
            {
                Toast("Create or choose a learner profile first.", ToastKind.Warning);
                return;
            }
            ChangeState(SimulationStateId.Setup);
        }

        public async Task ConfirmSetup(HouseDesign design, int slopeDegrees, SoilType soil, BuildMode mode)
        {
            if (current.Id != SimulationStateId.Setup || IsBusy) return;
            var setup = new SessionSetup(design, new SiteConditions(slopeDegrees, soil), mode);
            await BeginSession(setup, null);
        }

        public async Task ResumeAutosave()
        {
            if (IsBusy) return;
            var snapshot = Context.Data.LoadAutosave();
            if (snapshot == null)
            {
                Toast("There is no unfinished session to resume.", ToastKind.Info);
                return;
            }

            HouseDesign design = null;
            foreach (var candidate in Context.Designs)
                if (candidate.DesignId == snapshot.record.design.designId) design = candidate;
            if (design == null || !Enum.TryParse(snapshot.record.site.soil, true, out SoilType soil) ||
                !Enum.TryParse(snapshot.record.site.mode, out BuildMode mode))
            {
                Toast("The saved session uses a design that is no longer installed.", ToastKind.Error);
                Context.Data.DiscardAutosave();
                return;
            }

            var setup = new SessionSetup(design, new SiteConditions(snapshot.record.site.slopeDeg, soil), mode)
            {
                ScaleDenominator = snapshot.scaleDenominator,
                Restore = snapshot
            };
            await BeginSession(setup, snapshot.record);
        }

        public void BackToMenu()
        {
            if (current.Id == SimulationStateId.Setup) ChangeState(SimulationStateId.Start);
        }

        /// <summary>Loads the session's Addressable assets, creates the site and starts recording.</summary>
        private async Task BeginSession(SessionSetup setup, SessionRecord restoredRecord)
        {
            IsBusy = true;
            Context.Events.Publish(new StateChanged(current.Id, current.Id));
            try
            {
                Context.Setup = setup;
                var content = Context.Content;
                var strut = await content.LoadAsync<GameObject>(AssetKeys.StrutPrefab, AssetScope.Session);
                var pad = await content.LoadAsync<GameObject>(AssetKeys.PadPrefab, AssetScope.Session);
                var panel = await content.LoadAsync<GameObject>(AssetKeys.PanelPrefab, AssetScope.Session);
                var arch = await content.LoadAsync<GameObject>(AssetKeys.ArchPrefab, AssetScope.Session);
                var ghostMaterial = await content.LoadAsync<Material>(AssetKeys.GhostMaterial, AssetScope.Session);
                hologramMaterial = await content.LoadAsync<Material>(AssetKeys.HologramMaterial, AssetScope.Session);
                var lineMaterial = await content.LoadAsync<Material>(AssetKeys.HoloLineMaterial, AssetScope.Session);
                var sitePrefab = await content.LoadAsync<GameObject>(AssetKeys.SitePrefab, AssetScope.Session);
                var debris = await content.LoadAsync<GameObject>(AssetKeys.DebrisPrefab, AssetScope.Session);
                var rain = await content.LoadAsync<GameObject>(AssetKeys.RainDropPrefab, AssetScope.Session);
                var spark = await content.LoadAsync<GameObject>(AssetKeys.SparkPrefab, AssetScope.Session);

                Context.Effects.Initialise(debris, rain, spark);
                var prefabs = new Dictionary<MemberShape, GameObject>
                {
                    { MemberShape.Strut, strut },
                    { MemberShape.Pad, pad },
                    { MemberShape.Panel, panel },
                    { MemberShape.Arch, arch },
                    { MemberShape.Block, panel }
                };
                Context.Views = new MemberViewFactory(prefabs, ghostMaterial, Context.Effects.SpawnDebris, Context.WorldRoot);

                var siteObject = UnityEngine.Object.Instantiate(sitePrefab, Context.WorldRoot);
                siteObject.name = "Site";
                Context.Site = siteObject.GetComponent<SiteController>();
                var blueprint = setup.Design.Blueprint;
                Context.Site.Configure(setup.Site, blueprint.UphillReferenceZ, setup.ScaleDenominator, Footprint(blueprint));
                siteObject.SetActive(false);

                Context.Entities.BeginSession(blueprint, Context.Site.Layout, Context.Materials, Context.Views, Context.Site.StructureRoot);
                Context.Entities.HologramMaterial = hologramMaterial;
                Context.Entities.SetHologram(Context.Data.Settings.Hologram);
                Context.Site.SetHologram(Context.Data.Settings.Hologram);
                WorldHud.Create(Context.Site.StructureRoot, Context.Site.Layout, Footprint(blueprint), setup.Design, lineMaterial,
                    Context.AR.Camera, Context.Events, Context.Tasks, Context.LoadTests);
                Context.Tasks.Begin(blueprint, setup.Mode);
                Context.Build.ClearHistory();
                Context.LoadTests.Reset();
                Context.SelectedMemberId = null;

                var profile = Context.Data.ActiveProfile;
                Context.Record = restoredRecord ?? SessionRecord.Start(profile.learnerId, profile.displayName, blueprint, setup.Site, setup.Mode, DateTime.UtcNow);
                Context.Events.Publish(new SessionStarted(Context.Record));
                Context.Data.BindSnapshotSources(Context.Entities.Snapshot, () => Context.Site != null ? Context.Site.ScaleDenominator : 50,
                    id => Context.Entities.Member(id)?.Material.Id);

                IsRepositioning = false;
                IsBusy = false;
                ChangeState(SimulationStateId.Placement);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                IsBusy = false;
                CleanupSession();
                Toast("Could not load the design's assets. Please try again.", ToastKind.Error);
                ChangeState(SimulationStateId.Start);
            }
        }

        // ---- AR placement -----------------------------------------------------------------

        public void ConfirmPlacement() => current.ConfirmPlacement();

        public void Reposition() => current.Reposition();

        /// <summary>Lifts the site off its anchor so it can be placed again; members stay attached.</summary>
        public void BeginReposition()
        {
            Context.Site.transform.SetParent(Context.WorldRoot, true);
            Context.AR.ClearSiteAnchor();
            IsRepositioning = true;
            ChangeState(SimulationStateId.Placement);
        }

        /// <summary>Called by the placement state once the site is anchored.</summary>
        public void OnSiteAnchored()
        {
            var setup = Context.Setup;
            if (IsRepositioning)
            {
                IsRepositioning = false;
                ChangeState(Context.Tasks.IsComplete && Context.LoadTests.TestsRun > 0 ? SimulationStateId.Reviewing : SimulationStateId.Building);
                return;
            }

            if (setup.Restore != null)
            {
                Context.Build.Restore(setup.Restore.placed);
                setup.Restore = null;
                Toast("Resumed your unfinished session.", ToastKind.Success);
            }

            if (setup.Mode == BuildMode.LoadTest)
            {
                // Load Test mode starts from a finished structure.
                var prebuilt = new List<Fatiro.Build.PlacedMemberState>();
                foreach (var spec in Context.Blueprint.Members)
                    if (spec.Required && !Context.Entities.IsPlaced(spec.Id))
                        prebuilt.Add(new Fatiro.Build.PlacedMemberState { memberId = spec.Id, materialId = spec.DefaultMaterialId });
                Context.Build.Restore(prebuilt);
                ShowSkin();
                ChangeState(SimulationStateId.Reviewing);
                return;
            }

            if (Context.Tasks.IsComplete) ShowSkin();
            ChangeState(SimulationStateId.Building);
        }

        // ---- Building ---------------------------------------------------------------------

        public void SelectTrayMember(MemberSpec spec) => current.SelectTrayMember(spec);
        public void BeginMemberDrag(MemberSpec spec) => current.BeginMemberDrag(spec);
        public void DragMember(Vector2 screenPosition) => current.DragMember(screenPosition);
        public void EndMemberDrag(Vector2 screenPosition) => current.EndMemberDrag(screenPosition);

        public void SelectMember(string memberId)
        {
            var previous = Context.SelectedMemberId;
            if (previous != null) Context.Entities.View(previous)?.SetSelected(false);
            Context.SelectedMemberId = memberId != null && Context.Entities.IsPlaced(memberId) ? memberId : null;
            if (Context.SelectedMemberId != null) Context.Entities.View(Context.SelectedMemberId)?.SetSelected(true);
            Context.Events.Publish(new MemberSelected(Context.SelectedMemberId));
        }

        public void ChangeMaterial(string memberId, string materialId)
        {
            if (!current.CanEditStructure)
            {
                Toast("Tap \"Modify\" to change the structure.", ToastKind.Info);
                return;
            }
            if (Context.Build.TryChangeMaterial(memberId, materialId))
                Context.Entities.View(memberId)?.SetSelected(true);
        }

        public void RemoveMember(string memberId)
        {
            if (!current.CanEditStructure)
            {
                Toast("Tap \"Modify\" to change the structure.", ToastKind.Info);
                return;
            }
            if (Context.Build.TryRemove(memberId)) SelectMember(null);
        }

        public void Undo()
        {
            if (current.CanEditStructure) Context.Build.Undo();
        }

        public void Redo()
        {
            if (current.CanEditStructure) Context.Build.Redo();
        }

        private void OnStageCompleted(StageCompleted message)
        {
            if (!message.BuildComplete) return;
            ShowSkin();
            Toast("Structure complete! Open the test bench to load-test it.", ToastKind.Success);
        }

        // ---- Load testing -----------------------------------------------------------------

        public void OpenTestBench() => current.OpenTestBench();

        public void RunLoadTest(LoadCaseConfig config) => current.RunLoadTest(config);

        public void BeginLoadTest(LoadCaseConfig config)
        {
            PendingTest = config;
            SelectMember(null);
            ChangeState(SimulationStateId.LoadTesting);
        }

        public void ModifyStructure() => current.ModifyStructure();

        // ---- Ending -----------------------------------------------------------------------

        public void EndSession()
        {
            if (!current.CanEndSession || Context.Record == null) return;
            SelectMember(null);
            var score = Context.Score.Calculate(Context.Record, Context.LoadTests, Context.Entities, Context.Setup.Design.FocusCases);
            Context.Record.Close(score, DateTime.UtcNow);
            Context.Events.Publish(new SessionEnded(Context.Record, score));
            ChangeState(SimulationStateId.Ended);
        }

        public void Restart()
        {
            if (current.Id != SimulationStateId.Ended) return;
            CleanupSession();
            ChangeState(SimulationStateId.Setup);
        }

        public void ReturnToMenu()
        {
            CleanupSession();
            ChangeState(SimulationStateId.Start);
        }

        // ---- Pause ------------------------------------------------------------------------

        public void Pause()
        {
            if (!current.RequiresTracking) return;
            PausedByTracking = false;
            history = current.Id;
            ChangeState(SimulationStateId.Paused);
        }

        public void Resume()
        {
            if (current.Id != SimulationStateId.Paused) return;
            if (PausedByTracking && !Context.AR.IsTracking)
            {
                Toast("Still looking for the table. Move the device slowly.", ToastKind.Warning);
                return;
            }
            PausedByTracking = false;
            ChangeState(history, resumed: true);
        }

        private void PauseForTracking()
        {
            if (!current.RequiresTracking) return;
            history = current.Id;
            PausedByTracking = true;
            ChangeState(SimulationStateId.Paused);
        }

        private void ResumeAfterTracking()
        {
            if (current.Id != SimulationStateId.Paused || !PausedByTracking) return;
            PausedByTracking = false;
            ChangeState(history, resumed: true);
        }

        // ---- Skin and cleanup -------------------------------------------------------------

        public bool Hologram => Context.Data.Settings.Hologram;

        /// <summary>Switches between the glowing hologram view and the real textured view, live.</summary>
        public void SetHologram(bool hologram)
        {
            Context.Data.Settings.Hologram = hologram;
            Context.Entities.SetHologram(hologram);
            if (Context.Site != null) Context.Site.SetHologram(hologram);
            ApplySkinLook();
            Context.Events.Publish(new SettingsChanged());
        }

        /// <summary>In hologram view the creature skin glows like the rest; in real view it keeps its own materials.</summary>
        private void ApplySkinLook()
        {
            if (Context.Skin == null) return;
            foreach (var renderer in Context.Skin.GetComponentsInChildren<Renderer>(true))
            {
                if (!skinMaterials.ContainsKey(renderer)) skinMaterials[renderer] = renderer.sharedMaterials;
                if (Hologram && hologramMaterial != null)
                {
                    var glowing = new Material[skinMaterials[renderer].Length];
                    for (var i = 0; i < glowing.Length; i++) glowing[i] = hologramMaterial;
                    renderer.sharedMaterials = glowing;
                }
                else
                {
                    renderer.sharedMaterials = skinMaterials[renderer];
                }
            }
        }

        public void SetSkinVisible(bool visible)
        {
            if (Context.Skin != null) Context.Skin.SetActive(visible);
        }

        /// <summary>Loads the creature skin through Addressables once the structure is complete.</summary>
        private async void ShowSkin()
        {
            if (Context.Skin != null)
            {
                Context.Skin.SetActive(true);
                return;
            }

            var design = Context.Setup?.Design;
            var prefab = await Context.Content.LoadSkinAsync(design);
            if (prefab == null || Context.Site == null || Context.Setup?.Design != design || Context.Skin != null) return;
            Context.Skin = UnityEngine.Object.Instantiate(prefab, Context.Site.StructureRoot);
            Context.Skin.name = $"{design.DisplayName} skin";
            skinMaterials.Clear();
            ApplySkinLook();

            // Animals lie along the hillside: pivot about the uphill edge, where the ground is at height 0.
            if (design.SkinFollowsSlope)
            {
                var root = Context.Site.StructureRoot;
                var pivot = root.TransformPoint(0f, 0f, design.Blueprint.UphillReferenceZ);
                Context.Skin.transform.RotateAround(pivot, root.right, Context.Setup.Site.SlopeDegrees);
            }
        }

        /// <summary>Destroys session objects and releases the session's Addressables (mobile memory).</summary>
        private void CleanupSession()
        {
            SelectMember(null);
            Context.Entities.Clear();
            if (Context.Skin != null) UnityEngine.Object.Destroy(Context.Skin);
            Context.Skin = null;
            skinMaterials.Clear();
            Context.Views?.Clear();
            Context.Views = null;
            Context.Effects.Clear();
            if (Context.Site != null) UnityEngine.Object.Destroy(Context.Site.gameObject);
            Context.Site = null;
            Context.AR.ClearSiteAnchor();
            Context.AR.StopScanning();
            Context.LoadTests.Reset();
            Context.Build.ClearHistory();
            Context.Record = null;
            Context.Content.ReleaseScope(AssetScope.Skin);
            Context.Content.ReleaseScope(AssetScope.Session);
            IsRepositioning = false;
        }

        /// <summary>Plan area covered by the design's footings, where the building platform is compacted.</summary>
        private static Rect Footprint(HouseBlueprint blueprint)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var spec in blueprint.Members)
            {
                if (spec.Kind != MemberKind.Foundation || !spec.Required) continue;
                var centre = spec.A.Position;
                minX = Mathf.Min(minX, centre.X - spec.PanelSize.X * 0.5f);
                maxX = Mathf.Max(maxX, centre.X + spec.PanelSize.X * 0.5f);
                minZ = Mathf.Min(minZ, centre.Z - spec.PanelSize.Z * 0.5f);
                maxZ = Mathf.Max(maxZ, centre.Z + spec.PanelSize.Z * 0.5f);
            }
            return minX > maxX ? new Rect(-3f, -3f, 6f, 6f) : Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        private void Register(SimulationState state) => states.Add(state.Id, state);

        private void Toast(string message, ToastKind kind) => Context.Events.Publish(new ToastRequested(message, kind));
    }
}
