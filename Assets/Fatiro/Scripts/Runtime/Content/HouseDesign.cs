using System;
using System.Collections.Generic;
using Fatiro.Core;
using Fatiro.Domain;
using UnityEngine;
using UnityEngine.AddressableAssets;


namespace Fatiro.Content
{
    /// <summary>Serialized form of one MemberSpec inside a HouseDesign asset.</summary>
    [Serializable]
    public sealed class MemberData
    {
        public string id;
        public string displayName;
        public MemberKind kind;
        public BuildStage stage;
        public MemberShape shape;
        public Vector3 a;
        public bool aGroundRelative;
        public Vector3 b;
        public bool bGroundRelative;
        public float thickness;
        public string defaultMaterial;
        public string[] allowedMaterials;
        public string[] supports;
        public string[] extraSupports;
        public bool required = true;
        public float crownHeight;
        public Vector3 panelSize;
        public Vector3 panelEuler;
        public float tributaryArea;
        public bool isRoof;
        [TextArea] public string hint;
        public string component;
        public bool hasPatch;
        public int patchProfile;
        public int patchFrom;
        public int patchTo;
        public float patchAroundFrom;
        public float patchAroundTo;

        public static MemberData From(MemberSpec spec) => new MemberData
        {
            id = spec.Id,
            displayName = spec.DisplayName,
            kind = spec.Kind,
            stage = spec.Stage,
            shape = spec.Shape,
            a = spec.A.Position.ToUnity(),
            aGroundRelative = spec.A.GroundRelative,
            b = spec.B.Position.ToUnity(),
            bGroundRelative = spec.B.GroundRelative,
            thickness = spec.Thickness,
            defaultMaterial = spec.DefaultMaterialId,
            allowedMaterials = spec.AllowedMaterialIds,
            supports = spec.Supports,
            extraSupports = spec.ExtraSupports,
            required = spec.Required,
            crownHeight = spec.CrownHeight,
            panelSize = spec.PanelSize.ToUnity(),
            panelEuler = spec.PanelEuler.ToUnity(),
            tributaryArea = spec.TributaryArea,
            isRoof = spec.IsRoof,
            hint = spec.Hint,
            component = spec.Component,
            hasPatch = spec.Patch.IsSet,
            patchProfile = spec.Patch.Profile,
            patchFrom = spec.Patch.From,
            patchTo = spec.Patch.To,
            patchAroundFrom = spec.Patch.AroundFrom,
            patchAroundTo = spec.Patch.AroundTo
        };

        public MemberSpec ToSpec() => new MemberSpec(id, displayName, kind, stage, shape,
            new Endpoint(a.ToNumerics(), aGroundRelative), new Endpoint(b.ToNumerics(), bGroundRelative), thickness,
            defaultMaterial, allowedMaterials, supports, extraSupports, required, crownHeight,
            panelSize.ToNumerics(), panelEuler.ToNumerics(), tributaryArea, isRoof, hint, component,
            hasPatch ? new ShellPatch(patchProfile, patchFrom, patchTo, patchAroundFrom, patchAroundTo) : default);
    }

    /// <summary>Serialized form of one shell station.</summary>
    [Serializable]
    public sealed class StationData
    {
        public float z;
        public float baseY;
        public float topY;
        public float halfWidth;
        public float roundness = 2.5f;
        public float centreX;
        public bool baseOnGround;

        public static StationData From(ShellStation station) => new StationData
        {
            z = station.Z,
            baseY = station.BaseY,
            topY = station.TopY,
            halfWidth = station.HalfWidth,
            roundness = station.Roundness,
            centreX = station.CentreX,
            baseOnGround = station.BaseOnGround
        };

        public ShellStation ToStation() => new ShellStation(z, baseY, topY, halfWidth, roundness, centreX, baseOnGround);
    }

    /// <summary>Serialized form of one lofted shell (a body, a roof, an ear).</summary>
    [Serializable]
    public sealed class ShellData
    {
        public string name;
        public List<StationData> stations = new List<StationData>();

        public static ShellData From(ShellProfile profile)
        {
            var data = new ShellData { name = profile.Name };
            foreach (var station in profile.Stations) data.stations.Add(StationData.From(station));
            return data;
        }

        public ShellProfile ToProfile()
        {
            var list = new List<ShellStation>(stations.Count);
            foreach (var station in stations) list.Add(station.ToStation());
            return new ShellProfile(name, list);
        }
    }

    /// <summary>
    /// An experimental house design stored as data (NFR-12): adding a design means adding one of
    /// these assets to the "design" Addressables label, with no code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Fatiro/House Design", fileName = "HouseDesign")]
    public sealed class HouseDesign : ScriptableObject
    {
        [SerializeField] private string designId;
        [SerializeField] private int version = 1;
        [SerializeField] private string displayName;
        [SerializeField] private string meaning;
        [SerializeField, TextArea] private string description;
        [SerializeField, TextArea] private string lesson;
        [SerializeField, Range(1, 3)] private int difficulty = 1;
        [SerializeField] private Color accentColor = Color.white;
        [SerializeField] private int sortOrder;

        [Header("Foundation")]
        [SerializeField] private string foundationName;
        [SerializeField] private string foundationInspiration;
        [SerializeField] private float foundationBearing = 1f;
        [SerializeField] private float foundationSlopeResistance = 0.5f;
        [SerializeField] private float foundationGrip = 1f;

        [Header("Geometry")]
        [SerializeField] private float uphillReferenceZ;
        [SerializeField] private List<MemberData> members = new List<MemberData>();
        [SerializeField] private List<ShellData> shells = new List<ShellData>();
        [SerializeField] private string category;

        [Tooltip("Load cases this design is meant to teach. They count towards the safety score even if untested.")]
        [SerializeField] private LoadCaseKind[] focusCases = { LoadCaseKind.Seismic };

        [Header("Addressable assets")]
        [Tooltip("Fit-out (furniture, lights, wheels) shown when the house is revealed. Loaded on demand and released after.")]
        [SerializeField] private AssetReferenceGameObject skin;

        private HouseBlueprint blueprint;

        public string DesignId => designId;
        public int Version => version;
        public string DisplayName => displayName;
        public string Meaning => meaning;
        public string Description => description;
        public string Lesson => lesson;
        public int Difficulty => difficulty;
        public Color AccentColor => accentColor;
        public int SortOrder => sortOrder;
        public AssetReferenceGameObject Skin => skin;
        public string Category => category;
        public int MemberCount => members.Count;
        public IReadOnlyList<LoadCaseKind> FocusCases => focusCases;

        public HouseBlueprint Blueprint
        {
            get
            {
                if (blueprint != null) return blueprint;
                var specs = new List<MemberSpec>(members.Count);
                foreach (var member in members) specs.Add(member.ToSpec());
                var profiles = new List<ShellProfile>(shells.Count);
                foreach (var shell in shells) profiles.Add(shell.ToProfile());
                var style = new FoundationStyle(foundationName, foundationInspiration, foundationBearing, foundationSlopeResistance, foundationGrip);
                blueprint = new HouseBlueprint(designId, version, displayName, meaning, description, lesson, difficulty, style, uphillReferenceZ, specs,
                    profiles, category);
                return blueprint;
            }
        }

        public void Configure(HouseBlueprint source, Color accent, int order, LoadCaseKind[] focus, AssetReferenceGameObject skinReference)
        {
            category = source.Category;
            focusCases = focus;
            designId = source.DesignId;
            version = source.Version;
            displayName = source.Name;
            meaning = source.Meaning;
            description = source.Description;
            lesson = source.Lesson;
            difficulty = source.Difficulty;
            accentColor = accent;
            sortOrder = order;
            foundationName = source.Foundation.Name;
            foundationInspiration = source.Foundation.Inspiration;
            foundationBearing = source.Foundation.BearingFactor;
            foundationSlopeResistance = source.Foundation.SlopeResistance;
            foundationGrip = source.Foundation.LateralGrip;
            uphillReferenceZ = source.UphillReferenceZ;
            members = new List<MemberData>();
            foreach (var spec in source.Members) members.Add(MemberData.From(spec));
            shells = new List<ShellData>();
            foreach (var profile in source.Shells) shells.Add(ShellData.From(profile));
            skin = skinReference;
            blueprint = null;
        }

        private void OnValidate() => blueprint = null;
    }
}
