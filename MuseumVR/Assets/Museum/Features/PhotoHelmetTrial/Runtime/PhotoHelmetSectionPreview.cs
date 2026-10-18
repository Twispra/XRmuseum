using System;
using System.Collections.Generic;
using Museum.Experience;
using UnityEngine;
using UnityEngine.Rendering;

namespace Museum.PhotoHelmetTrial
{
    /// <summary>Isolated editor-scene study of the photo model's hollow shell; not a museum interaction.</summary>
    public sealed class PhotoHelmetSectionPreview : MonoBehaviour
    {
        [Serializable]
        public sealed class SurfaceBinding
        {
            public Renderer surface;
            public Material[] display;
            public Material[] cut;
        }

        [SerializeField] SurfaceBinding[] surfaces;
        [SerializeField] MeshFilter shell;
        [SerializeField] Material capMaterial;

        static readonly int CutEnabled = Shader.PropertyToID("_CutEnabled");
        static readonly int CutPlane = Shader.PropertyToID("_CutPlane");
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> indices = new List<int>();
        MaterialPropertyBlock properties;
        Mesh capMesh;
        GameObject capObject;
        bool enabledCut;
        float normalizedDepth;
        Vector3 sectionPointLocal;
        int capTriangleCount;

        public bool IsCutaway => enabledCut;
        public float Depth => normalizedDepth;
        public int CapTriangles => capTriangleCount;

        public void Configure(SurfaceBinding[] bindings, MeshFilter hollowShell, Material cap)
        {
            if (bindings == null || bindings.Length == 0)
                throw new ArgumentException("Photo section needs imported renderers.");
            foreach (var binding in bindings)
                if (!binding.surface || binding.display == null || binding.cut == null ||
                    binding.display.Length == 0 || binding.display.Length != binding.cut.Length)
                    throw new ArgumentException("Each photo renderer needs matching display and cut material slots.");
            surfaces = bindings;
            shell = hollowShell;
            capMaterial = cap;
        }

        public void ToggleCutaway()
        {
            enabledCut = !enabledCut;
            Refresh();
        }

        public void AdjustDepth(float step)
        {
            if (!enabledCut) return;
            normalizedDepth = Mathf.Clamp(normalizedDepth + step, -0.65f, 0.65f);
            Refresh();
        }

        public void ResetPreview()
        {
            enabledCut = false;
            normalizedDepth = 0;
            Refresh();
        }

        void OnDisable()
        {
            enabledCut = false;
            RestoreDisplay();
            if (capObject) capObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (enabledCut) ApplyWorldPlane();
        }

        void OnDestroy()
        {
            if (capMesh)
            {
                if (Application.isPlaying) Destroy(capMesh);
                else DestroyImmediate(capMesh);
            }
        }

        void Refresh()
        {
            if (surfaces == null || shell == null || shell.sharedMesh == null) return;
            if (!enabledCut)
            {
                RestoreDisplay();
                if (capObject) capObject.SetActive(false);
                capTriangleCount = 0;
                return;
            }
            if (!capMaterial) throw new InvalidOperationException("Photo section face material is missing.");
            if (!shell.sharedMesh.isReadable) throw new InvalidOperationException("Photo OBJ must be imported with Read/Write enabled for section preview.");

            // Local +X removes the right side in the front-facing comparison view.
            var bounds = LocalBounds();
            Vector3 normal = Vector3.right;
            sectionPointLocal = bounds.center + normal * (bounds.extents.x * normalizedDepth);
            foreach (var binding in surfaces)
            {
                if (!binding.surface) continue;
                binding.surface.sharedMaterials = binding.cut;
            }
            ApplyWorldPlane();

            EnsureCap();
            SectionCapGeometry.Build(shell.sharedMesh.vertices, shell.sharedMesh.triangles,
                transform.worldToLocalMatrix * shell.transform.localToWorldMatrix,
                normal, sectionPointLocal, vertices, indices);
            capMesh.Clear();
            capMesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            capMesh.SetVertices(vertices);
            capMesh.SetTriangles(indices, 0);
            capMesh.RecalculateNormals();
            capMesh.RecalculateBounds();
            capTriangleCount = indices.Count / 3;
            capObject.SetActive(indices.Count > 0);
        }

        void ApplyWorldPlane()
        {
            Vector3 worldNormal = transform.worldToLocalMatrix.transpose.MultiplyVector(Vector3.right).normalized;
            Vector3 worldPoint = transform.TransformPoint(sectionPointLocal);
            var plane = new Vector4(worldNormal.x, worldNormal.y, worldNormal.z, -Vector3.Dot(worldNormal, worldPoint));
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetFloat(CutEnabled, 1);
            properties.SetVector(CutPlane, plane);
            foreach (var binding in surfaces)
                if (binding.surface) binding.surface.SetPropertyBlock(properties);
        }

        Bounds LocalBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            bool first = true;
            var bounds = new Bounds();
            foreach (var renderer in renderers)
            {
                if (renderer.gameObject == capObject) continue;
                var local = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = transform.InverseTransformPoint(corner);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        void EnsureCap()
        {
            if (capObject) return;
            capObject = new GameObject("Photo shell section face - interpretive");
            capObject.transform.SetParent(transform, false);
            capMesh = new Mesh { name = "Photo hollow-shell section" };
            capMesh.MarkDynamic();
            capObject.AddComponent<MeshFilter>().sharedMesh = capMesh;
            var renderer = capObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = capMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void RestoreDisplay()
        {
            if (surfaces == null) return;
            foreach (var binding in surfaces)
            {
                if (!binding.surface) continue;
                binding.surface.sharedMaterials = binding.display;
                binding.surface.SetPropertyBlock(null);
            }
        }
    }
}
