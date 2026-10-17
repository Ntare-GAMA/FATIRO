using Fatiro.Domain;
using UnityEngine;

namespace Fatiro.Content
{
    /// <summary>
    /// Material definition stored as an Addressable ScriptableObject. Every member made of this
    /// material shares one MaterialSpec instance (Flyweight).
    /// </summary>
    [CreateAssetMenu(menuName = "Fatiro/Material", fileName = "Material")]
    public sealed class MaterialData : ScriptableObject
    {
        [SerializeField] private string materialId;
        [SerializeField] private string displayName;
        [SerializeField] private float axialStrength = 1f;
        [SerializeField] private float bendingStrength = 1f;
        [SerializeField] private float unitWeight = 0.2f;
        [SerializeField] private float unitCost = 1f;
        [SerializeField] private float drainCapacity;
        [SerializeField] private Color color = Color.white;
        [SerializeField, TextArea] private string description;

        private MaterialSpec spec;

        public string MaterialId => materialId;
        public string DisplayName => displayName;
        public Color Color => color;
        public string Description => description;

        public MaterialSpec Spec =>
            spec ??= new MaterialSpec(materialId, displayName, axialStrength, bendingStrength, unitWeight, unitCost, drainCapacity);

        public void Configure(MaterialSpec source, Color tint, string text)
        {
            materialId = source.Id;
            displayName = source.DisplayName;
            axialStrength = source.AxialStrength;
            bendingStrength = source.BendingStrength;
            unitWeight = source.UnitWeight;
            unitCost = source.UnitCost;
            drainCapacity = source.DrainCapacity;
            color = tint;
            description = text;
            spec = null;
        }
    }
}
