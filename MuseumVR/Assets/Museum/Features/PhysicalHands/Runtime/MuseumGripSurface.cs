using System;
using System.Collections.Generic;
using Museum.Experience;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhysicalHands
{
    /// <summary>
    /// Resolves a Grip contact on the currently visible model. Call when establishing a grab,
    /// not every frame. Cached vertices are mesh-local; live filter transforms include era
    /// changes, the articulated window, and nonuniform or mirrored object scales.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MuseumGripSurface : MonoBehaviour
    {
        // Reuse the existing, serializable position/index container, in a separate owned asset.
        [SerializeField] HelmetSectionMeshData cache;
        [SerializeField] MeshFilter[] modelFilters = Array.Empty<MeshFilter>();
        readonly Dictionary<Mesh, HelmetSectionMeshData.Entry> geometry = new Dictionary<Mesh, HelmetSectionMeshData.Entry>();
        readonly List<QueryPart> queryParts = new List<QueryPart>();
        HelmetSectionMeshData indexedCache;
        bool indexed;

        struct QueryPart
        {
            public MeshFilter filter;
            public Renderer renderer;
            public HelmetSectionMeshData.Entry data;
            public float boundsDistanceSquared;
        }

        public HelmetSectionMeshData Cache => cache;
        public IReadOnlyList<MeshFilter> ModelFilters => modelFilters;
        public bool LastContactUsedColliderFallback { get; private set; }
        public Renderer LastContactRenderer { get; private set; }
        public int LastTestedTriangleCount { get; private set; }

        /// <summary>Bindings are authored by the local baker; no physics shapes are added.</summary>
        public void Configure(HelmetSectionMeshData data, MeshFilter[] filters)
        {
            cache = data;
            modelFilters = filters != null ? (MeshFilter[])filters.Clone() : Array.Empty<MeshFilter>();
            indexed = false;
            BuildIndex();
        }

        void Awake() => BuildIndex();
        void OnValidate() => indexed = false;

        void BuildIndex()
        {
            if (indexed && indexedCache == cache) return;
            indexed = true; indexedCache = cache; geometry.Clear();
            if (!cache || cache.entries == null) return;
            foreach (var entry in cache.entries)
            {
                if (entry == null || !entry.source || entry.vertices == null || entry.vertices.Length < 3 ||
                    entry.triangles == null || entry.triangles.Length < 3 || entry.triangles.Length % 3 != 0) continue;
                bool valid = true;
                foreach (var vertex in entry.vertices) if (!Finite(vertex)) { valid = false; break; }
                if (!valid) continue;
                foreach (int index in entry.triangles)
                    if (index < 0 || index >= entry.vertices.Length) { valid = false; break; }
                if (valid) geometry[entry.source] = entry;
            }
        }

        /// <summary>
        /// Returns the closest triangle contact and a unit normal facing the external query.
        /// Unknown/invalid active geometry that could be nearer falls back explicitly to the
        /// grab's colliders. A fallback query inside a collider has no exterior normal and fails.
        /// LastContactUsedColliderFallback distinguishes proxy contacts from real mesh contacts.
        /// </summary>
        public bool TryGetContact(XRGrabInteractable grab, Vector3 query, out Vector3 point, out Vector3 normal)
        {
            point = query; normal = Vector3.zero;
            LastContactUsedColliderFallback = false; LastContactRenderer = null; LastTestedTriangleCount = 0;
            if (!grab || !Finite(query)) return false;
            BuildIndex(); queryParts.Clear();
            float unknownBoundsDistanceSquared = float.PositiveInfinity;
            if (modelFilters != null) foreach (var filter in modelFilters)
            {
                if (!filter || !filter.sharedMesh || !filter.transform.IsChildOf(grab.transform)) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                float boundsDistance = WorldBounds(filter).SqrDistance(query);
                if (!geometry.TryGetValue(filter.sharedMesh, out var entry))
                {
                    unknownBoundsDistanceSquared = Mathf.Min(unknownBoundsDistanceSquared, boundsDistance);
                    continue;
                }
                queryParts.Add(new QueryPart { filter = filter, renderer = renderer, data = entry, boundsDistanceSquared = boundsDistance });
            }
            queryParts.Sort((a, b) => a.boundsDistanceSquared.CompareTo(b.boundsDistanceSquared));
            float best = float.PositiveInfinity;
            foreach (var part in queryParts)
            {
                if (part.boundsDistanceSquared > best) break;
                Matrix4x4 matrix = part.filter.transform.localToWorldMatrix;
                var vertices = part.data.vertices; var indices = part.data.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    // World-space triangle distances stay correct for nonuniform scale.
                    Vector3 a = matrix.MultiplyPoint3x4(vertices[indices[i]]);
                    Vector3 b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]);
                    Vector3 c = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                    Vector3 triangleNormal = Vector3.Cross(b - a, c - a);
                    LastTestedTriangleCount++;
                    if (!Finite(a) || !Finite(b) || !Finite(c) || triangleNormal.sqrMagnitude < 1e-16f) continue;
                    Vector3 contact = ClosestPointOnTriangle(query, a, b, c);
                    float distance = (query - contact).sqrMagnitude;
                    if (distance >= best) continue;
                    // Vector3.normalized zeroes small but valid imported triangles.
                    best = distance; point = contact; normal = triangleNormal / Mathf.Sqrt(triangleNormal.sqrMagnitude);
                    if (Vector3.Dot(normal, query - point) < 0f) normal = -normal;
                    LastContactRenderer = part.renderer;
                }
            }
            if (LastContactRenderer && unknownBoundsDistanceSquared > best) return true;
            LastContactRenderer = null;
            LastContactUsedColliderFallback = true;
            return TryColliderContact(grab, query, out point, out normal);
        }
        static Bounds WorldBounds(MeshFilter filter)
        {
            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 matrix = filter.transform.localToWorldMatrix;
            Vector3 x = matrix.MultiplyVector(Vector3.right * local.extents.x);
            Vector3 y = matrix.MultiplyVector(Vector3.up * local.extents.y);
            Vector3 z = matrix.MultiplyVector(Vector3.forward * local.extents.z);
            Vector3 half = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center), half * 2f);
        }

        static bool TryColliderContact(XRGrabInteractable grab, Vector3 query, out Vector3 point, out Vector3 normal)
        {
            point = query; normal = Vector3.zero; float best = float.PositiveInfinity;
            foreach (var collider in grab.colliders)
            {
                if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                Vector3 candidate = collider.ClosestPoint(query);
                float distance = (query - candidate).sqrMagnitude;
                if (!Finite(candidate) || distance >= best) continue;
                best = distance; point = candidate;
            }
            Vector3 outward = query - point;
            if (float.IsInfinity(best) || outward.sqrMagnitude < 1e-12f) return false;
            normal = outward.normalized;
            return true;
        }

        // Triangle Voronoi regions; callers exclude degenerate triangles first.
        static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float inverse = 1f / (va + vb + vc);
            return a + ab * (vb * inverse) + ac * (vc * inverse);
        }

        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
