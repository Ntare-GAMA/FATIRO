using System.Collections.Generic;
using Fatiro.AR;
using Fatiro.Audio;
using Fatiro.Build;
using Fatiro.Content;
using Fatiro.Core;
using Fatiro.Data;
using Fatiro.Domain;
using Fatiro.Simulation;
using Fatiro.World;
using UnityEngine;

namespace Fatiro.States
{
    public enum SimulationStateId
    {
        Start,
        Setup,
        Placement,
        Building,
        LoadTesting,
        Reviewing,
        Paused,
        Ended
    }

    /// <summary>Choices made on the setup screen (FR-06 to FR-08).</summary>
    public sealed class SessionSetup
    {
        public SessionSetup(HouseDesign design, SiteConditions site, BuildMode mode)
        {
            Design = design;
            Site = site;
            Mode = mode;
        }

        public HouseDesign Design { get; }
        public SiteConditions Site { get; }
        public BuildMode Mode { get; }
        public int ScaleDenominator { get; set; } = 50;

        /// <summary>Set when resuming an autosaved session (NFR-08).</summary>
        public AutosaveSnapshot Restore { get; set; }
    }

    /// <summary>
    /// The services and session objects states work with. Built once by AppBootstrap and passed
    /// in explicitly (dependency injection, no singletons).
    /// </summary>
    public sealed class SimulationContext
    {
        public IEventBus Events { get; set; }
        public IARService AR { get; set; }
        public IContentCatalogue Content { get; set; }
        public EntityManager Entities { get; set; }
        public TaskManager Tasks { get; set; }
        public BuildManager Build { get; set; }
        public LoadTestManager LoadTests { get; set; }
        public ScoreManager Score { get; set; }
        public DataManager Data { get; set; }
        public AudioManager Audio { get; set; }
        public EffectsController Effects { get; set; }
        public Transform WorldRoot { get; set; }

        public IReadOnlyList<HouseDesign> Designs { get; set; }
        public IReadOnlyDictionary<string, MaterialData> Materials { get; set; }

        // Session-scoped objects, created when a session starts and destroyed when it ends.
        public SessionSetup Setup { get; set; }
        public SiteController Site { get; set; }
        public MemberViewFactory Views { get; set; }
        public GameObject Skin { get; set; }
        public SessionRecord Record { get; set; }
        public string SelectedMemberId { get; set; }

        public HouseBlueprint Blueprint => Setup?.Design?.Blueprint;
    }
}
