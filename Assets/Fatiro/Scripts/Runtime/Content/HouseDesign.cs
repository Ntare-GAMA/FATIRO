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
            hint = spec.Hint
        };

        public MemberSpec ToSpec() => new MemberSpec(id, displayName, kind, stage, shape,
            new Endpoint(a.ToNumerics(), aGroundRelative), new Endpoint(b.ToNumerics(), bGroundRelative), thickness,
            defaultMaterial, allowedMaterials, supports, extraSupports, required, crownHeight,
            panelSize.ToNumerics(), panelEuler.ToNumerics(), tributaryArea, isRoof, hint);
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

        [Tooltip("Load cases this design is meant to teach. They count towards the safety score even if untested.")]
        [SerializeField] private LoadCaseKind[] focusCases = { LoadCaseKind.Seismic };

        [Header("Addressable assets")]
        [Tooltip("Decorative creature skin shown when the build is complete. Loaded on demand and released after.")]
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
        public int MemberCount => members.Count;
        public IReadOnlyList<LoadCaseKind> FocusCases => focusCases;

        public HouseBlueprint Blueprint
        {
            get
            {
                if (blueprint != null) return blueprint;
                var specs = new List<MemberSpec>(members.Count);
                foreach (var member in members) specs.Add(member.ToSpec());
                var style = new FoundationStyle(foundationName, foundationInspiration, foundationBearing, foundationSlopeResistance, foundationGrip);
                blueprint = new HouseBlueprint(designId, version, displayName, meaning, description, lesson, difficulty, style, uphillReferenceZ, specs);
                return blueprint;
            }
        }

        public void Configure(HouseBlueprint source, Color accent, int order, LoadCaseKind[] focus, AssetReferenceGameObject skinReference)
        {
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
            skin = skinReference;
            blueprint = null;
        }

        private void OnValidate() => blueprint = null;
    }
}
