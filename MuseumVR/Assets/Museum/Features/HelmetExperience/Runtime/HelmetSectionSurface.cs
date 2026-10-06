using System.Collections.Generic;
using Museum.Inspection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Museum.Experience
{
    [DisallowMultipleComponent]
    public sealed class HelmetSectionSurface : MonoBehaviour
    {
        [SerializeField] HelmetSectionMeshData geometry;
        [SerializeField] Material sectionMaterial;
        HelmetInspectionController controller;
        readonly List<Part> parts = new List<Part>();
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> indices = new List<int>();
        Vector3 previousNormal,previousPoint;
        float previousWindow=float.NaN;
        bool initialized,wasCut;
        public int TriangleCount { get; private set; }
        public int RebuildCount { get; private set; }
        public int RejectedOpenBands { get; private set; }
        sealed class Part { public Renderer source; public HelmetSectionMeshData.Entry data; public Mesh mesh; public GameObject visual; }
        public void Configure(HelmetSectionMeshData data,Material material) { geometry=data;sectionMaterial=material; }
        void Awake() => controller=GetComponent<HelmetInspectionController>();
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            if(!controller) controller=GetComponent<HelmetInspectionController>();
            if(!controller || !geometry || !sectionMaterial) return;
            if(!controller.IsCutaway)
            {
                if(wasCut) foreach(var p in parts) if(p.visual)p.visual.SetActive(false);
                wasCut=false; return;
            }
            if(!initialized)
            {
                // Capture only existing metal meshes. Transparent glass retains its own cut shader.
                foreach(var source in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(source.name.Contains("Glass") || source.GetComponentInParent<HelmetSectionInteractable>()) continue;
                    var filter=source.GetComponent<MeshFilter>();if(!filter)continue;
                    var data=geometry.Find(filter.sharedMesh);if(data==null)continue;
                    var go=new GameObject("Section face - "+source.name);go.transform.SetParent(transform,false);
                    var mesh=new Mesh{name=go.name};mesh.MarkDynamic();
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=sectionMaterial;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    parts.Add(new Part{source=source,data=data,mesh=mesh,visual=go});
                }
                initialized=true;
            }
            bool changed=!wasCut || (previousNormal-controller.SectionNormalLocal).sqrMagnitude>1e-10f ||
                (previousPoint-controller.SectionPointLocal).sqrMagnitude>1e-10f || Mathf.Abs(previousWindow-controller.WindowAngle)>.05f;
            if(!changed)return;
            previousNormal=controller.SectionNormalLocal;previousPoint=controller.SectionPointLocal;previousWindow=controller.WindowAngle;
            TriangleCount=0;RejectedOpenBands=0;RebuildCount++;
            foreach(var p in parts)
            {
                if(!p.source)continue;
                RejectedOpenBands+=SectionCapGeometry.Build(p.data.vertices,p.data.triangles,
                    transform.worldToLocalMatrix*p.source.transform.localToWorldMatrix,previousNormal,previousPoint,vertices,indices);
                p.mesh.Clear();p.mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                p.mesh.SetVertices(vertices);p.mesh.SetTriangles(indices,0);p.mesh.RecalculateNormals();p.mesh.RecalculateBounds();
                p.visual.SetActive(indices.Count>0);TriangleCount+=indices.Count/3;
            }
            wasCut=true;
        }
        void OnDisable() { foreach(var p in parts) if(p.visual)p.visual.SetActive(false);wasCut=false; }
        void OnDestroy()
        {
            foreach(var p in parts){DestroyOwned(p.mesh);DestroyOwned(p.visual);}parts.Clear();
        }
        static void DestroyOwned(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
