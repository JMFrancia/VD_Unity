using UnityEngine;

namespace VoidDay.Data
{
    /// One asset per station type (§14). The single generic Producer is data, not subclasses (CLAUDE.md).
    /// Placeholder-art fields (§12.6) render a tinted primitive until a real prefab is assigned (§12.8) —
    /// swapping art is only ever an SO reference edit, never a code change.
    [CreateAssetMenu(menuName = "VoidDay/Station", fileName = "Station")]
    public sealed class StationSO : ScriptableObject
    {
        [Header("Identity")]
        public string stationType;   // "field", "silo", "orderBoard"
        public string displayName;

        [Header("Footprint (§4.1) — 1x1 for now")]
        public int width = 1;
        public int height = 1;

        [Header("Art — real prefab wins; primitive is the placeholder (§12.6/§12.8)")]
        [Tooltip("Real mesh prefab. When assigned, StationView uses it instead of the primitive fallback.")]
        public GameObject prefab;

        [Tooltip("Primitive silhouette used until a real prefab is assigned.")]
        public PrimitiveType placeholderPrimitive = PrimitiveType.Cube;

        [Tooltip("Local scale of the placeholder primitive (1 unit = 1 cell).")]
        public Vector3 placeholderScale = new Vector3(0.9f, 0.9f, 0.9f);

        [Tooltip("Local euler rotation of the placeholder primitive — e.g. lay a Quad flat with x=90.")]
        public Vector3 placeholderEuler = Vector3.zero;

        [Tooltip("Local Y offset so the body sits on the ground plane.")]
        public float placeholderYOffset = 0.45f;

        [Tooltip("Placeholder tint (§12.6). Applied to the primitive's URP Lit material.")]
        public Color placeholderColor = Color.white;
    }
}
