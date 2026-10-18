#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.Experience;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhysicalHands.EditorTools
{
    /// <summary>Local, explicitly invoked bake. Never changes models, importers, input, or scenes.</summary>
    public static class MuseumGripSurfaceBaker
    {
        public const string HelmetPrefabPath = "Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab";
        public const string CacheAssetPath = "Assets/Museum/Features/PhysicalHands/GripSurfaceCache.asset";

        [MenuItem("Museum/Physical Hands/Bake saved helmet grip surfaces")]
        public static void BakeSavedHelmet()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Grip surfaces are baked in Edit Mode from the saved helmet prefab.");
            var root = PrefabUtility.LoadPrefabContents(HelmetPrefabPath);
            try
            {
                var surface = Bake(root, CacheAssetPath);
                PrefabUtility.SaveAsPrefabAsset(root, HelmetPrefabPath, out bool succeeded);
                if (!succeeded) throw new InvalidOperationException("Could not save the local helmet grip-surface binding.");
                Debug.Log("GRIP_SURFACE_BAKE filters=" + surface.ModelFilters.Count + " meshes=" + surface.Cache.entries.Length +
                    " triangles=" + surface.Cache.entries.Sum(e => e.triangles.Length / 3) + " cache=" + CacheAssetPath +
                    "; original model/import/input assets were not changed.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// Bakes imported FBX/OBJ mesh positions/triangles, including currently disabled eras.
        /// Creates/updates the supplied owned cache asset and binds the supplied root. The caller
        /// decides whether to save that root; this method does not save scenes or other dirty assets.
        /// </summary>
        public static MuseumGripSurface Bake(GameObject root, string cachePath = CacheAssetPath)
        {
            if (!root || !root.GetComponent<XRGrabInteractable>())
                throw new ArgumentException("The grip-surface root must own an XRGrabInteractable.", nameof(root));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Grip-surface baking must not run during Play Mode.");
            string path = (cachePath ?? string.Empty).Replace('\\', '/');
            if (!path.StartsWith("Assets/Museum/Features/PhysicalHands/", StringComparison.Ordinal) ||
                !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.Contains("/../"))
                throw new ArgumentException("Use a separate owned .asset under PhysicalHands; existing section caches are protected.", nameof(cachePath));
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                throw new ArgumentException("The cache asset directory must already exist.", nameof(cachePath));

            var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(f => IsModelSurface(f, root.transform)).ToArray();
            if (filters.Length == 0) throw new InvalidOperationException("No imported model meshes were found; primitive UI/outline meshes are not grip surfaces.");
            // Read everything successfully before mutating the cache or supplied prefab contents.
            var entries = filters.Select(f => f.sharedMesh).Distinct().Select(ReadMesh).ToArray();
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing && !(existing is HelmetSectionMeshData))
                throw new InvalidOperationException("The cache path already contains another asset type: " + path);
            var data = existing as HelmetSectionMeshData;
            if (!data)
            {
                data = ScriptableObject.CreateInstance<HelmetSectionMeshData>();
                data.entries = entries;
                AssetDatabase.CreateAsset(data, path);
            }
            else { data.entries = entries; EditorUtility.SetDirty(data); }
            AssetDatabase.SaveAssetIfDirty(data);
            var surface = root.GetComponent<MuseumGripSurface>();
            if (!surface) surface = root.AddComponent<MuseumGripSurface>();
            surface.Configure(data, filters); EditorUtility.SetDirty(surface);
            return surface;
        }

        static bool IsModelSurface(MeshFilter filter, Transform root)
        {
            if (!filter || !filter.sharedMesh || !filter.GetComponent<MeshRenderer>()) return false;
            string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            bool supported = path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase);
            if (!supported || !(AssetImporter.GetAtPath(path) is ModelImporter)) return false;
            for (Transform t = filter.transform; t && t != root; t = t.parent)
            {
                string name = t.name.ToLowerInvariant();
                if (name.Contains("outline") || name.Contains("hotspot") || name.Contains("marker") || name.Contains("hover") ||
                    name.Contains("selection cue") || t.GetComponent<Canvas>()) return false;
            }
            return true;
        }

        static HelmetSectionMeshData.Entry ReadMesh(Mesh source)
        {
            // Unity 2022.3 Editor API explicitly bypasses Mesh.isReadable; no importer toggle.
            // https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MeshUtility.AcquireReadOnlyMeshData.html
            using (var snapshot = MeshUtility.AcquireReadOnlyMeshData(source))
            {
                var mesh = snapshot[0];
                using (var positions = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
                {
                    mesh.GetVertices(positions);
                    var vertices = positions.ToArray();
                    var triangles = new List<int>();
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        var descriptor = mesh.GetSubMesh(submesh);
                        if (descriptor.topology != MeshTopology.Triangles && descriptor.topology != MeshTopology.Quads) continue;
                        using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                        {
                            mesh.GetIndices(indices, submesh, true); // Include each submesh's baseVertex.
                            int stride = descriptor.topology == MeshTopology.Quads ? 4 : 3;
                            if (indices.Length % stride != 0) throw new InvalidOperationException("Invalid triangle/quad index count in " + source.name);
                            for (int i = 0; i < indices.Length; i += stride)
                            {
                                for (int j = 0; j < stride; j++)
                                    if (indices[i + j] < 0 || indices[i + j] >= vertices.Length)
                                        throw new InvalidOperationException("Out-of-range mesh index in " + source.name);
                                triangles.Add(indices[i]); triangles.Add(indices[i + 1]); triangles.Add(indices[i + 2]);
                                if (stride == 4) { triangles.Add(indices[i]); triangles.Add(indices[i + 2]); triangles.Add(indices[i + 3]); }
                            }
                        }
                    }
                    if (triangles.Count == 0) throw new InvalidOperationException("Imported model mesh has no triangle surfaces: " + source.name);
                    foreach (var vertex in vertices)
                        if (float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) || float.IsNaN(vertex.y) || float.IsInfinity(vertex.y) || float.IsNaN(vertex.z) || float.IsInfinity(vertex.z))
                            throw new InvalidOperationException("Non-finite mesh position in " + source.name);
                    return new HelmetSectionMeshData.Entry { source = source, vertices = vertices, triangles = triangles.ToArray() };
                }
            }
        }
    }
}
#endif
