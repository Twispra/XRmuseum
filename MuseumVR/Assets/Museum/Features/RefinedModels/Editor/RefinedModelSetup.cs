#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Museum.Experience;
using Museum.Exploration;
using Museum.Inspection;
using Museum.Refined;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.RefinedModels.EditorTools
{
    /// <summary>Scoped upgrade of saved visual assets, never rebuilds the gallery or XR rig.</summary>
    public static class RefinedModelSetup
    {
        const string Root="Assets/Museum/Features/RefinedModels";
        const string Prefab="Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab";
        const string Scene="Assets/Museum/Scenes/MuseumStart.unity";
        public static void RunBatch()
        {
            try { Install(); EditorApplication.Exit(0); }
            catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        [MenuItem("Museum/Refined Models/Install saved models")]
        public static void Install()
        {
            GuardScene();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<RefinedHelmetBinding>()) throw new InvalidOperationException("Models already installed; do not overwrite later edits.");
            var backup=Path.GetFullPath("../tmp/refined-backups/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            foreach(var path in new[]{Prefab,Scene}){var target=Path.Combine(backup,path);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(path,target);}
            foreach(string folder in new[]{"Materials","Data","Prefabs"}) if(!AssetDatabase.IsValidFolder(Root+"/"+folder)) AssetDatabase.CreateFolder(Root,folder);
            foreach(string path in AssetDatabase.FindAssets("t:Model",new[]{Root+"/Models"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.importAnimation=true;
                importer.animationType=ModelImporterAnimationType.Legacy;importer.globalScale=1;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                // Retain the authored fine motion without lossy keyframe reduction.
                // FBX child translation units also need parent-scale-aware validation.
                importer.animationCompression=ModelImporterAnimationCompression.Off;
                importer.SaveAndReimport();
            }
            foreach(string path in AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"/Textures"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=path.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=path.Contains("BaseColor");importer.maxTextureSize=2048;importer.SaveAndReimport();
            }
            var equipmentRoot=new GameObject("Refined Connection Equipment");
            var pump=Model("mark-iii-pump-runtime",equipmentRoot.transform);var surface=Model("surface-telephone-runtime",equipmentRoot.transform);var diver=Model("diver-telephone-runtime",equipmentRoot.transform);
            ApplyMaterials(pump);ApplyMaterials(surface);ApplyMaterials(diver);
            var equipment=equipmentRoot.AddComponent<RefinedConnectionEquipment>();
            equipment.Configure(pump.transform,surface.transform,diver.transform,Clip("mark-iii-pump-runtime"),Clip("surface-telephone-runtime"),Clip("diver-telephone-runtime"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/gentle-air.wav"),AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/gentle-voice.wav"));
            if(!equipment.IsReady)throw new InvalidOperationException("FBX animations/anchors missing.");
            PrefabUtility.SaveAsPrefabAsset(equipmentRoot,Root+"/Prefabs/ConnectionEquipment.prefab");UnityEngine.Object.DestroyImmediate(equipmentRoot);
            var helmet=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var inspection=helmet.GetComponent<HelmetInspectionController>();var so=new SerializedObject(inspection);
                var clipped=Refs<Renderer>(so.FindProperty("clippedRenderers"));var windowParts=Refs<Renderer>(so.FindProperty("highlightedRenderers"));
                var originals=clipped.Concat(windowParts).Distinct().ToArray();
                var model=Model("mark-v-runtime",helmet.transform);model.name="Refined Mark V Visual";ApplyMaterials(model);
                var renderers=model.GetComponentsInChildren<Renderer>(true);
                var front=renderers.Single(r=>r.name.StartsWith("FrontGlass_"));
                // This exporter/importer retains source axes. First normalize raw height
                // to -Z, then rotate that height into Unity +Y.
                if(shellHeight(model)>0)model.transform.localRotation=Quaternion.Euler(0,180,0);
                model.transform.localRotation=Quaternion.Euler(90,0,0)*model.transform.localRotation;
                var importedDoor=Find(model.transform,"FrontWindowPivot");
                var hinge=UnityEngine.Object.Instantiate(importedDoor.gameObject).transform;hinge.name="Refined front window";
                importedDoor.gameObject.SetActive(false);
                var newHinge=helmet.transform.InverseTransformPoint(importedDoor.position);
                var oldHinge=inspection.WindowPivot.localPosition;inspection.WindowPivot.localPosition=newHinge;
                hinge.SetParent(inspection.WindowPivot,false);hinge.SetPositionAndRotation(importedDoor.position,importedDoor.rotation);hinge.localScale=importedDoor.lossyScale;
                renderers=renderers.Where(r=>!r.transform.IsChildOf(importedDoor)).Concat(hinge.GetComponentsInChildren<Renderer>(true)).ToArray();
                front=renderers.Single(r=>r.name.StartsWith("FrontGlass_"));
                var doorBounds=LocalBounds(front,inspection.WindowPivot);
                inspection.WindowPivot.localPosition=oldHinge;
                var shell=renderers.Single(r=>r.name=="Bonnet_HollowCopper_TinFinish");
                var air=renderers.First(r=>r.name=="AirSubmarineThreadCore_1and1sixteenth");
                var phone=renderers.Where(r=>r.transform.IsChildOf(Find(model.transform,"TelephoneAssembly"))).ToArray();
                var section=ScriptableObject.CreateInstance<HelmetSectionMeshData>();
                section.entries=originals.Concat(renderers).Where(r=>r.sharedMaterial.shader.name!="Museum/Inspection Glass")
                    .Select(r=>r.GetComponent<MeshFilter>()).Where(f=>f && f.sharedMesh).Select(f=>f.sharedMesh).Distinct()
                    .Select(mesh=>new HelmetSectionMeshData.Entry{source=mesh,vertices=mesh.vertices,triangles=mesh.triangles}).ToArray();
                AssetDatabase.CreateAsset(section,Root+"/Data/RefinedSectionMeshes.asset");
                var cap=AssetDatabase.LoadAssetAtPath<Material>("Assets/Museum/Features/HelmetExperience/Materials/SectionFace.mat");
                var binder=helmet.AddComponent<RefinedHelmetBinding>();
                binder.Configure(originals,renderers,clipped,windowParts,hinge,front,shell,air,Find(model.transform,"Anchor_AirOutlet"),phone,Find(model.transform,"Anchor_TelephoneOutlet"),newHinge,doorBounds.center,doorBounds.size+Vector3.one*.012f,section,cap);
                PrefabUtility.SaveAsPrefabAsset(helmet,Prefab);
                Debug.Log("REFINED_IMPORT meshes="+renderers.Length+" triangles="+renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)+" front="+front.bounds.center+" hinge="+newHinge+" bounds="+shell.bounds);
            }
            finally{PrefabUtility.UnloadPrefabContents(helmet);}
            var scene=EditorSceneManager.OpenScene(Scene);var originalRoots=scene.GetRootGameObjects().Select(g=>g.name).OrderBy(n=>n).ToArray();
            var exhibit=UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            var demo=UnityEngine.Object.FindObjectOfType<HelmetConnectionDemonstration>();
            demo.ConfigureRefinedEquipment(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/ConnectionEquipment.prefab").GetComponent<RefinedConnectionEquipment>());
            // Default to the completed Mark V asset; other two eras retain their existing placeholders.
            var catalogue=AssetDatabase.LoadAssetAtPath<Museum.Eras.HelmetEraCatalogue>("Assets/Museum/Features/HelmetEras/HelmetEraCatalogue.asset");
            var catalogueSO=new SerializedObject(catalogue);catalogueSO.FindProperty("defaultIndex").intValue=1;catalogueSO.ApplyModifiedPropertiesWithoutUndo();
            exhibit.GetComponent<RefinedHelmetBinding>().RefreshVisualBinding();
            PrefabUtility.RecordPrefabInstancePropertyModifications(demo);EditorUtility.SetDirty(demo);
            if(!originalRoots.SequenceEqual(scene.GetRootGameObjects().Select(g=>g.name).OrderBy(n=>n)))throw new InvalidOperationException("Unexpected root change.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("REFINED_MODELS_SETUP_OK; saved main scene, original rig/root/controls retained. backup="+backup);
        }
        static void GuardScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save or discard scene edits before installing models.");
        }
        public static void ReimportMotionBatch()
        {
            try
            {
                GuardScene();
                foreach(string path in AssetDatabase.FindAssets("t:Model",new[]{Root+"/Models"}).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                    importer.animationCompression=ModelImporterAnimationCompression.Off;importer.SaveAndReimport();
                    foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")))
                    {
                        Debug.Log("REFINED_MOTION "+path+" duration="+clip.length+" curves="+AnimationUtility.GetCurveBindings(clip).Length);
                        foreach(var curveBinding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.path.Contains("Diaphragm")&&b.propertyName.Contains("Position")))
                        {
                            var curve=AnimationUtility.GetEditorCurve(clip,curveBinding);
                            Debug.Log("REFINED_DIAPHRAGM_CURVE "+curveBinding.path+" "+curveBinding.propertyName+" keys="+curve.length+" range="+(curve.keys.Max(k=>k.value)-curve.keys.Min(k=>k.value))+" time="+curve.keys.Min(k=>k.time)+".."+curve.keys.Max(k=>k.time));
                        }
                    }
                }
                AssetDatabase.SaveAssets();EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static T[] Refs<T>(SerializedProperty p) where T:UnityEngine.Object => Enumerable.Range(0,p.arraySize).Select(i=>p.GetArrayElementAtIndex(i).objectReferenceValue as T).Where(v=>v).ToArray();
        static float shellHeight(GameObject model)=>model.transform.parent.InverseTransformPoint(model.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="Bonnet_HollowCopper_TinFinish").bounds.center).z;
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static GameObject Model(string name,Transform parent)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+name+".fbx");if(!source)throw new InvalidOperationException("Missing model "+name);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(parent,false);
            foreach(var animation in model.GetComponentsInChildren<Animation>(true)){animation.playAutomatically=false;animation.enabled=false;}
            return model;
        }
        static AnimationClip Clip(string model)=>AssetDatabase.LoadAllAssetsAtPath(Root+"/Models/"+model+".fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview"));
        static Bounds LocalBounds(Renderer r,Transform parent)
        {
            var b=r.GetComponent<MeshFilter>().sharedMesh.bounds;var matrix=parent.worldToLocalMatrix*r.transform.localToWorldMatrix;
            var result=new Bounds(matrix.MultiplyPoint3x4(b.center),Vector3.zero);
            for(int i=0;i<8;i++)result.Encapsulate(matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));return result;
        }
        static void ApplyMaterials(GameObject root)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials=renderer.sharedMaterials.Select(old=>MaterialFor(old)).ToArray();
        }
        static Material MaterialFor(Material source)
        {
            if(!source)throw new InvalidOperationException("Imported model contains a missing material.");
            string name=source.name;var path=Root+"/Materials/"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing)return existing;
            bool glass=name.Contains("HistoricalGlass");var shader=Shader.Find(glass?"Museum/Inspection Glass":"Museum/Inspection Shell");
            if(!shader)throw new InvalidOperationException("Shader unavailable "+name);
            var mat=new Material(shader){name=name};Color color=source.HasProperty("_Color")?source.color:Color.white;
            mat.SetColor("_BaseColor",glass?new Color(.16f,.24f,.25f,.10f):color);mat.SetFloat("_Metallic",name.Contains("Bronze")||name.Contains("Tin")||name.Contains("Steel")||name.Contains("Gunmetal")?.85f:0);
            mat.SetFloat("_Roughness",.65f);
            string texture=name.Contains("AgedTin")?"AgedTin_OriginalInterpretation":name.Contains("CastBronze")?"CastBronze_OriginalInterpretation":name.Contains("OriginalAsh")?"OriginalAshInterpretation":name.Contains("OriginalGunmetal")?"OriginalGunmetalInterpretation":null;
            if(texture!=null)
            {
                mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_TextureStrength",1);
                mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+texture+"_BaseColor.png"));
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+texture+"_Normal.png"));
                mat.SetTexture("_RoughnessMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+texture+"_Roughness.png"));
                if(!mat.GetTexture("_BaseMap")||!mat.GetTexture("_BumpMap")||!mat.GetTexture("_RoughnessMap"))
                    throw new InvalidOperationException("Missing texture set "+texture);
            }
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
    }
}
#endif
