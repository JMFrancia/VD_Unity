using System.Collections.Generic;
using UnityEngine;

namespace VoidDay.View
{
    /// Edit-time scatter of decorative grass across the island's top face. Holds the scatter settings
    /// and owns the baked result; it does nothing at runtime, because by then the tufts are a single
    /// ordinary mesh. GroundScatterEditor drives the bake from the inspector.
    ///
    /// Baked rather than spawned because grass is level dressing, not dynamic content — and because a
    /// runtime Instantiate loop can never be static-batched, leaving one draw call per tuft.
    ///
    /// Placement is a downward raycast rather than a flat bounds fill: the island's footprint is a
    /// rounded shape with sloped soil edges, so a box fill would strand tufts off the rim. A ray that
    /// misses, lands on a slope, or hits a station instead of the ground drops that candidate.
    public sealed class GroundScatter : MonoBehaviour
    {
        [SerializeField] Collider ground;
        [SerializeField] GameObject[] tuftPrefabs;
        [SerializeField] MeshFilter bakeTarget;
        [SerializeField] int count = 340;
        [SerializeField] int seed = 1337;
        [SerializeField] Vector2 scaleRange = new Vector2(0.17f, 0.3f);
        [SerializeField] float minUpDot = 0.99f;
        [SerializeField] float edgeInset = 0.25f;
        [SerializeField] float rayHeight = 20f;
        [SerializeField] float stationClearance = 0.32f;

        public MeshFilter BakeTarget => bakeTarget;

        /// Scatters candidates and merges every placed tuft into one mesh, expressed in bakeTarget's
        /// local space. Returns null when nothing landed, so the caller can report an empty bake
        /// rather than writing a degenerate asset.
        public Mesh BuildCombinedMesh()
        {
            if (ground == null) throw new System.InvalidOperationException($"{name}: ground collider is not assigned");
            if (tuftPrefabs == null || tuftPrefabs.Length == 0)
                throw new System.InvalidOperationException($"{name}: tuftPrefabs is empty");
            if (bakeTarget == null) throw new System.InvalidOperationException($"{name}: bakeTarget is not assigned");

            // Edit-time queries: transforms edited this frame may not have reached the physics scene yet.
            Physics.SyncTransforms();

            var rng = new System.Random(seed);
            Bounds b = ground.bounds;
            Matrix4x4 toLocal = bakeTarget.transform.worldToLocalMatrix;
            var combines = new List<CombineInstance>();

            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(b.min.x + edgeInset, b.max.x - edgeInset, (float)rng.NextDouble());
                float z = Mathf.Lerp(b.min.z + edgeInset, b.max.z - edgeInset, (float)rng.NextDouble());
                var origin = new Vector3(x, b.max.y + rayHeight, z);

                if (!Physics.Raycast(origin, Vector3.down, out var hit, rayHeight * 2f)) continue;
                if (hit.collider != ground) continue;          // a station (or anything else) owns this spot
                if (hit.normal.y < minUpDot) continue;          // sloped soil edge, not the grass top
                if (IsCrowded(hit.point)) continue;             // a station's mesh overhangs its collider

                var prefab = tuftPrefabs[rng.Next(tuftPrefabs.Length)];
                var mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    throw new System.InvalidOperationException($"{name}: tuft prefab '{prefab.name}' has no MeshFilter mesh");

                // Respect any offset the mesh carries inside the prefab, not just the root transform.
                Matrix4x4 meshLocal = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                var scale = Vector3.one * Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());

                combines.Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    transform = toLocal * Matrix4x4.TRS(hit.point, rot, scale) * meshLocal
                });
            }

            if (combines.Count == 0) return null;

            var combined = new Mesh
            {
                name = "GrassScatter",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32   // count is a tunable; do not cap at 65k verts
            };
            combined.CombineMeshes(combines.ToArray(), true, true);
            combined.RecalculateBounds();
            return combined;
        }

        /// A station's visual base overhangs its box/capsule collider, so a tuft that clears the
        /// downward ray can still poke through the model. Keep a margin around anything non-ground.
        bool IsCrowded(Vector3 point)
        {
            foreach (var c in Physics.OverlapSphere(point, stationClearance))
                if (c != ground) return true;
            return false;
        }
    }
}
