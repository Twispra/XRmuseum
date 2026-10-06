#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.Exploration;
using Museum.Inspection;
using Museum.Localization;
using Museum.Localization.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Museum.Experience.EditorTools
{
    /// <summary>Explicit additive fitting migration. Never re-exports or overwrites the original FBX.</summary>
    public static class HelmetRearConnectionsSetup
    {
        const string Root="Assets/Museum/Features/HelmetExperience";
        const string Prefab="Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab";
        const string Scene="Assets/Museum/Scenes/MuseumStart.unity";
        public static void RunBatch()
        {
            try{Install();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [MenuItem("Museum/Experience/Install rear telephone fitting")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetSceneManagerSetup().Any(s=>s.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Exit Play and resolve unsaved scenes before migration.");
            string backup=Path.GetFullPath(Path.Combine(Application.dataPath,"../../tmp/rear-connection-backups",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
            foreach(string file in new[]{Prefab,Scene,Root+"/Data/SectionMeshData.asset",Root+"/Meshes/TelephoneGooseneck.asset",Root+"/Meshes/TelephoneResponseRing.asset","Assets/Museum/Features/Localization/Tables/LocalizationTable.asset"})
                foreach(string path in new[]{file,file+".meta"})
                {if(!File.Exists(path))continue;string dest=Path.Combine(backup,path);Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(path,dest);}
            var contents=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var air=contents.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="GB_AirConnector");
                var thread=contents.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="GB_AirConnectorThread");
                Vector3 local=contents.transform.InverseTransformPoint(air.bounds.center);
                // From the rear (+local Z), screen-left is +local X; phone goes left and air right.
                float desiredX=-Mathf.Abs(local.x);
                Vector3 shift=contents.transform.TransformVector(new Vector3(desiredX-local.x,0,0));
                air.transform.position+=shift;thread.transform.position+=shift;
                var bindings=contents.GetComponent<HelmetRearConnections>();
                if(!bindings)
                {
                    bindings=contents.AddComponent<HelmetRearConnections>();
                    if(!AssetDatabase.IsValidFolder(Root+"/Meshes"))AssetDatabase.CreateFolder(Root,"Meshes");
                    var unionBounds=thread.bounds;
                    Vector3 airEndLocal=contents.transform.InverseTransformPoint(new Vector3(unionBounds.center.x,unionBounds.center.y,unionBounds.max.z));
                    var airEnd=Node(contents.transform,"Air hose outlet anchor",airEndLocal);
                    Vector3 rearBase=contents.transform.InverseTransformPoint(air.bounds.min);
                    rearBase.x=Mathf.Abs(desiredX);rearBase.z+=.016f;
                    var phone=Node(contents.transform,"Telephone gooseneck",rearBase);
                    var mesh=BuildFitting();
                    AssetDatabase.CreateAsset(mesh,Root+"/Meshes/TelephoneGooseneck.asset");
                    var visual=Node(phone,"Telephone elbow and cable union",Vector3.zero);
                    visual.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=visual.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=thread.sharedMaterial;
                    var phoneEnd=Node(phone,"Telephone cable outlet anchor",new Vector3(0,.047f,.069f));
                    var halo=Node(phoneEnd,"Telephone receive indicator",Vector3.zero);
                    var haloMesh=BuildRing(.0155f,.0172f,.0015f);
                    AssetDatabase.CreateAsset(haloMesh,Root+"/Meshes/TelephoneResponseRing.asset");
                    halo.gameObject.AddComponent<MeshFilter>().sharedMesh=haloMesh;
                    var haloRenderer=halo.gameObject.AddComponent<MeshRenderer>();
                    haloRenderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Museum/Features/HelmetExploration/Materials/VoiceSignal.mat");
                    if(!haloRenderer.sharedMaterial)throw new InvalidOperationException("Existing Voice material missing.");
                    haloRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    halo.gameObject.SetActive(false);
                    bindings.Configure(air,airEnd,new[]{renderer},phoneEnd,halo);
                    var inspection=new SerializedObject(contents.GetComponent<HelmetInspectionController>());
                    var clipped=inspection.FindProperty("clippedRenderers");clipped.InsertArrayElementAtIndex(clipped.arraySize);
                    clipped.GetArrayElementAtIndex(clipped.arraySize-1).objectReferenceValue=renderer;inspection.ApplyModifiedPropertiesWithoutUndo();
                }
                if(!bindings.TelephoneOutlet || !bindings.AirOutlet)throw new InvalidOperationException("Incomplete fitting references.");
                var brass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Museum/Features/HelmetInspection/Materials/Brass.mat");
                if(!brass)throw new InvalidOperationException("Existing brass material missing.");
                air.sharedMaterial=brass;thread.sharedMaterial=brass;
                foreach(var fitting in bindings.TelephoneSurfaces)fitting.sharedMaterial=brass;
                var phoneRoot=bindings.TelephoneOutlet.parent;
                Vector3 basePosition=contents.transform.InverseTransformPoint(air.bounds.min);
                basePosition.x=Mathf.Abs(desiredX);basePosition.z+=.016f;phoneRoot.localPosition=basePosition;
                // Update only this task's additive mesh, preserving its GUID and saved references.
                var savedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/TelephoneGooseneck.asset");
                var revisedMesh=BuildFitting();EditorUtility.CopySerialized(revisedMesh,savedMesh);UnityEngine.Object.DestroyImmediate(revisedMesh);EditorUtility.SetDirty(savedMesh);
                var data=AssetDatabase.LoadAssetAtPath<HelmetSectionMeshData>(Root+"/Data/SectionMeshData.asset");
                var filters=contents.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh && !f.name.Contains("Glass") &&
                    !f.name.Contains("receive indicator") && !f.GetComponentInParent<HelmetSectionInteractable>());
                data.entries=filters.Select(f=>f.sharedMesh).Distinct().Select(mesh=>new HelmetSectionMeshData.Entry{source=mesh,vertices=mesh.vertices,triangles=mesh.triangles}).ToArray();
                EditorUtility.SetDirty(data);PrefabUtility.SaveAsPrefabAsset(contents,Prefab);
                Debug.Log("REAR_CONNECTIONS_PREFAB air="+air.bounds.center+" phone="+bindings.TelephoneOutlet.position+"; metal meshes="+data.entries.Length);
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            var scene=EditorSceneManager.OpenScene(Scene);
            var target=UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            var rear=target.GetComponent<HelmetRearConnections>();
            var demo=UnityEngine.Object.FindObjectOfType<HelmetConnectionDemonstration>();
            demo.ConfigureRearConnections(rear);EditorUtility.SetDirty(demo);
            PrefabUtility.RecordPrefabInstancePropertyModifications(demo);
            // The existing AIR clue caches a saved local anchor: refresh after relocating its fitting.
            foreach(var hotspot in target.GetComponentsInChildren<HelmetExplorationHotspot>(true))
            {
                var serialized=new SerializedObject(hotspot);serialized.FindProperty("anchorReady").boolValue=false;
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(hotspot);
                PrefabUtility.RecordPrefabInstancePropertyModifications(hotspot);
            }
            var table=AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            TrilingualMuseumSetup.ApplyTable(table,table.FontAsset);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("REAR_CONNECTIONS_SETUP_OK "+backup);
        }
        static Transform Node(Transform parent,string name,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        static Mesh BuildFitting()
        {
            var v=new List<Vector3>();var t=new List<int>();
            var points=new List<Vector3>{Vector3.zero,new Vector3(0,.008f,.012f),new Vector3(0,.018f,.012f)};
            for(int i=1;i<=10;i++){float a=i*Mathf.PI*.5f/10;points.Add(new Vector3(0,.018f+.029f*Mathf.Sin(a),.012f+.029f*(1-Mathf.Cos(a))));}
            points.Add(new Vector3(0,.047f,.057f));
            Tube(points,.0105f,.0073f,24,v,t);
            Tube(new List<Vector3>{new Vector3(0,.047f,.046f),new Vector3(0,.047f,.065f)},.014f,.0073f,6,v,t);
            Tube(new List<Vector3>{new Vector3(0,.047f,.063f),new Vector3(0,.047f,.069f)},.0116f,.0073f,24,v,t);
            Tube(new List<Vector3>{new Vector3(0,0,-.003f),new Vector3(0,0,.006f)},.017f,.0073f,32,v,t);
            return Mesh(v,t,"Illustrative telephone gooseneck");
        }
        static Mesh BuildRing(float inner,float outer,float length)
        {var v=new List<Vector3>();var t=new List<int>();Tube(new List<Vector3>{Vector3.zero,Vector3.forward*length},outer,inner,40,v,t);return Mesh(v,t,"Telephone response ring");}
        static Mesh Mesh(List<Vector3> v,List<int> t,string name)
        {var m=new Mesh{name=name};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
        static void Tube(List<Vector3> points,float outer,float inner,int sides,List<Vector3> vertices,List<int> triangles)
        {
            int start=vertices.Count;
            for(int p=0;p<points.Count;p++)
            {
                Vector3 forward=(points[Mathf.Min(p+1,points.Count-1)]-points[Mathf.Max(p-1,0)]).normalized;
                Vector3 across=Vector3.right,vertical=Vector3.Cross(forward,across).normalized;
                for(int layer=0;layer<2;layer++)for(int i=0;i<sides;i++)
                {float a=i*Mathf.PI*2/sides;vertices.Add(points[p]+(across*Mathf.Cos(a)+vertical*Mathf.Sin(a))*(layer==0?outer:inner));}
            }
            for(int p=0;p<points.Count-1;p++)for(int layer=0;layer<2;layer++)for(int i=0;i<sides;i++)
            {
                int a=start+p*sides*2+layer*sides+i,b=start+p*sides*2+layer*sides+(i+1)%sides,c=b+sides*2,d=a+sides*2;
                if(layer==0)Quad(a,b,c,d);else Quad(d,c,b,a);
            }
            for(int end=0;end<2;end++)for(int i=0;i<sides;i++)
            {
                int a=start+(end==0?0:points.Count-1)*sides*2+i,b=start+(end==0?0:points.Count-1)*sides*2+(i+1)%sides;
                if(end==0)Quad(a,a+sides,b+sides,b);else Quad(b,b+sides,a+sides,a);
            }
            void Quad(int a,int b,int c,int d){triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);}
        }
    }
}
#endif
