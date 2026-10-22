using System;
using System.Collections;
using Fatiro.Core;
using Fatiro.Domain;
using UnityEngine;

namespace Fatiro.World
{
    public enum MemberVisual
    {
        Material,
        Utilisation,
        Ghost
    }

    /// <summary>
    /// Visual for one member (placed or ghost). Owns no rules: it draws the shape for a spec,
    /// colours itself, and plays the failure animation. Pooled per shape.
    /// </summary>
    public sealed class MemberView : MonoBehaviour, IPoolable
    {
        public static readonly Color Safe = new Color(0.23f, 0.70f, 0.45f);
        public static readonly Color Warning = new Color(0.95f, 0.76f, 0.26f);
        public static readonly Color Danger = new Color(0.90f, 0.28f, 0.30f);
        public static readonly Color GhostTint = new Color(1f, 0.95f, 0.70f, 0.40f);
        public static readonly Color GhostReady = new Color(1f, 0.82f, 0.15f, 0.65f);

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapTiling = Shader.PropertyToID("_BaseMap_ST");
        private const float FootingEmbed = 0.25f;
        private const float WallEmbed = 0.1f;

        [SerializeField] private MeshFilter bodyFilter;
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private MeshCollider bodyCollider;

        private MaterialPropertyBlock block;
        private Material structureMaterial;
        private Material ghostMaterial;
        private Mesh sharedMesh;
        private Mesh archMesh;
        private MemberSurface surface = new MemberSurface(null, Color.white, 1f);
        private Vector2 surfaceMetres = Vector2.one;
        private Color currentColor;
        private Coroutine failure;
        private Action<Vector3, Color> spawnDebris;
        private bool selected;
        private float pulse;
        private Vector3 pivotRest;

        public MemberSpec Spec { get; private set; }
        public string MemberId => Spec?.Id;
        public MemberVisual Visual { get; private set; }
        public bool IsFailedVisual { get; private set; }
        public Renderer BodyRenderer => bodyRenderer;

        public void Initialise(Material structure, Material ghost, Action<Vector3, Color> debrisSpawner)
        {
            structureMaterial = structure;
            ghostMaterial = ghost;
            spawnDebris = debrisSpawner;
            sharedMesh = bodyFilter.sharedMesh;
            block ??= new MaterialPropertyBlock();
        }

        /// <summary>Builds the shape for the member in structure space (metres).</summary>
        public void Bind(MemberSpec spec, ResolvedGeometry geometry)
        {
            Spec = spec;
            name = spec.Id;
            var body = bodyFilter.transform;
            var a = geometry.A.ToUnity();
            var b = geometry.B.ToUnity();

            // The pivot sits at the member's centre so the collapse animation tips it about itself.
            Vector3 centre;
            switch (spec.Shape)
            {
                case MemberShape.Arch:
                {
                    var rise = spec.CrownHeight - (a.y + b.y) * 0.5f;
                    centre = (a + b) * 0.5f + Vector3.up * rise * 0.5f;
                    archMesh = MemberMeshes.BuildArch(a, b, spec.CrownHeight, spec.Thickness, archMesh);
                    surfaceMetres = Vector2.one; // arch UVs are already in metres
                    bodyFilter.sharedMesh = archMesh;
                    body.localPosition = -centre;
                    body.localRotation = Quaternion.identity;
                    body.localScale = Vector3.one;
                    break;
                }
                case MemberShape.Strut:
                {
                    var direction = b - a;
                    var length = Mathf.Max(direction.magnitude, 0.01f);
                    centre = (a + b) * 0.5f;
                    body.localPosition = Vector3.zero;
                    body.localRotation = Quaternion.FromToRotation(Vector3.up, direction / length);
                    // The built-in cylinder is 2 m tall and 1 m wide.
                    body.localScale = new Vector3(spec.Thickness, length * 0.5f, spec.Thickness);
                    surfaceMetres = new Vector2(Mathf.PI * spec.Thickness, length);
                    break;
                }
                case MemberShape.Pad:
                {
                    // A footing is a brick pier: bedded 0.25 m into the ground, the rest standing above it.
                    var size = spec.PanelSize.ToUnity();
                    centre = a + Vector3.up * (size.y * 0.5f - FootingEmbed);
                    body.localPosition = Vector3.zero;
                    body.localRotation = Quaternion.identity;
                    body.localScale = size;
                    surfaceMetres = new Vector2(Mathf.Max(size.x, size.z), size.y);
                    break;
                }
                case MemberShape.Block:
                {
                    // A wall standing on the ground between a and b, its base following the slope.
                    var direction = b - a;
                    var length = Mathf.Max(direction.magnitude, 0.01f);
                    var height = geometry.Area / length;
                    var depth = height + WallEmbed;
                    centre = (a + b) * 0.5f + Vector3.up * (depth * 0.5f - WallEmbed);
                    body.localPosition = Vector3.zero;
                    body.localRotation = Quaternion.LookRotation(direction / length, Vector3.up);
                    body.localScale = new Vector3(spec.Thickness, depth, length);
                    surfaceMetres = new Vector2(length, depth);
                    break;
                }
                default:
                    centre = a;
                    body.localPosition = Vector3.zero;
                    body.localRotation = Quaternion.Euler(spec.PanelEuler.ToUnity());
                    body.localScale = spec.PanelSize.ToUnity();
                    surfaceMetres = new Vector2(spec.PanelSize.X, spec.PanelSize.Z);
                    break;
            }

            pivotRest = centre;
            var pivot = body.parent;
            pivot.localPosition = centre;
            pivot.localRotation = Quaternion.identity;

            if (bodyCollider != null)
            {
                bodyCollider.sharedMesh = null;
                bodyCollider.sharedMesh = bodyFilter.sharedMesh;
            }
        }

        public void ShowAsGhost(bool ready = false)
        {
            Visual = MemberVisual.Ghost;
            bodyRenderer.sharedMaterial = ghostMaterial;
            if (bodyCollider != null) bodyCollider.enabled = true;
            SetColor(ready ? GhostReady : GhostTint);
        }

        /// <summary>Shows the member in its building material: textured bamboo, timber, steel, concrete or earth block.</summary>
        public void ShowMaterial(MemberSurface look)
        {
            surface = look;
            Visual = MemberVisual.Material;
            ApplySurface();
            SetColor(look.Tint);
        }

        /// <summary>Colour by utilisation (FR-17): green below 70 %, amber to 100 %, red above.</summary>
        public void ShowUtilisation(float utilisation)
        {
            Visual = MemberVisual.Utilisation;
            ApplySurface();
            // The colour multiplies the texture, so the grain stays visible under green, amber or red.
            SetColor(UtilisationColor(utilisation));
        }

        public static Color UtilisationColor(float utilisation) =>
            utilisation > 1f ? Danger : utilisation >= 0.7f ? Warning : Safe;

        public void SetSelected(bool value)
        {
            selected = value;
            if (!selected) SetColor(currentColor);
        }

        /// <summary>Plays the collapse: a red flash, then the member tips and drops and breaks into debris.</summary>
        public void PlayFailure(float delay)
        {
            if (failure != null) StopCoroutine(failure);
            failure = StartCoroutine(FailRoutine(delay));
        }

        /// <summary>Puts a failed member back for the next test (FR-21).</summary>
        public void Restore()
        {
            if (failure != null) StopCoroutine(failure);
            failure = null;
            IsFailedVisual = false;
            bodyFilter.transform.parent.localPosition = pivotRest;
            bodyFilter.transform.parent.localRotation = Quaternion.identity;
            bodyRenderer.enabled = true;
            if (bodyCollider != null) bodyCollider.enabled = true;
            ShowMaterial(surface);
        }

        public void OnSpawned()
        {
            IsFailedVisual = false;
            selected = false;
            bodyRenderer.enabled = true;
            if (bodyCollider != null) bodyCollider.enabled = true;
        }

        public void OnDespawned()
        {
            if (failure != null) StopCoroutine(failure);
            failure = null;
            Spec = null;
            selected = false;
            IsFailedVisual = false;
            var pivot = bodyFilter.transform.parent;
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
            // Arch views keep their generated mesh for reuse; other shapes keep the prefab mesh.
            if (archMesh == null) bodyFilter.sharedMesh = sharedMesh;
        }

        private void Update()
        {
            if (!selected && Visual != MemberVisual.Ghost) return;
            pulse += Time.deltaTime * (Visual == MemberVisual.Ghost ? 3f : 6f);
            var wave = (Mathf.Sin(pulse) + 1f) * 0.5f;
            var color = selected ? Color.Lerp(currentColor, Color.white, 0.25f + wave * 0.35f) : currentColor;
            if (Visual == MemberVisual.Ghost) color.a = Mathf.Lerp(0.2f, currentColor.a + 0.15f, wave);
            ApplyColor(color);
        }

        private void OnDestroy()
        {
            if (archMesh != null) Destroy(archMesh);
        }

        private IEnumerator FailRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            IsFailedVisual = true;
            SetColor(Danger);
            if (bodyCollider != null) bodyCollider.enabled = false;

            var pivot = bodyFilter.transform.parent;
            var bounds = bodyRenderer.bounds;
            var spin = new Vector3(UnityEngine.Random.Range(-90f, 90f), 0f, UnityEngine.Random.Range(-90f, 90f));
            var fallSpeed = 0f;
            var elapsed = 0f;
            const float duration = 0.9f;
            while (elapsed < duration)
            {
                var step = Time.deltaTime;
                elapsed += step;
                fallSpeed += 9.81f * step;
                pivot.localPosition += Vector3.down * fallSpeed * step;
                pivot.localRotation = Quaternion.Euler(spin * (elapsed / duration) * 0.6f);
                yield return null;
            }

            for (var i = 0; i < 4; i++)
                spawnDebris?.Invoke(bounds.center + UnityEngine.Random.insideUnitSphere * bounds.extents.magnitude * 0.4f, surface.Tint);
            bodyRenderer.enabled = false;
            failure = null;
        }

        private void ApplySurface()
        {
            bodyRenderer.sharedMaterial = surface.Material != null ? surface.Material : structureMaterial;
            block ??= new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(block);
            block.SetVector(BaseMapTiling, new Vector4(surfaceMetres.x / surface.TileMetres, surfaceMetres.y / surface.TileMetres, 0f, 0f));
            bodyRenderer.SetPropertyBlock(block);
        }

        private void SetColor(Color color)
        {
            currentColor = color;
            ApplyColor(color);
        }

        private void ApplyColor(Color color)
        {
            block ??= new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            bodyRenderer.SetPropertyBlock(block);
        }
    }
}
