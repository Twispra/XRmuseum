#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Museum.Inspection;
using Museum.Exploration;
using Museum.Localization;
using Museum.Localization.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Experience.EditorTools
{
    /// <summary>Explicit, backed-up local upgrade of the existing prefab and main scene; never a gallery builder.</summary>
    public static class HelmetExperienceSetup
    {
        const string Root="Assets/Museum/Features/HelmetExperience";
        const string Prefab="Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab";
        const string Scene="Assets/Museum/Scenes/MuseumStart.unity";
        public static void RunBatch()
        {
            try{Install();EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [MenuItem("Museum/Experience/Apply scoped experience upgrade")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetSceneManagerSetup().Any(s=>s.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Exit Play and resolve dirty scenes before the explicit upgrade.");
            string backup=Path.GetFullPath(Path.Combine(Application.dataPath,"../../tmp/experience-backups",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
            foreach(string file in new[]{Prefab,Scene,"Assets/Museum/Features/HelmetInspection/Materials/Window.mat",
                "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset"})
                foreach(string path in new[]{file,file+".meta"})
                {string target=Path.Combine(backup,path);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(path,target);}
            if(!AssetDatabase.IsValidFolder(Root+"/Data"))AssetDatabase.CreateFolder(Root,"Data");
            if(!AssetDatabase.IsValidFolder(Root+"/Materials"))AssetDatabase.CreateFolder(Root,"Materials");
            var glassShader=Shader.Find("Museum/Inspection Glass");var faceShader=Shader.Find("Museum/Section Face");
            if(!glassShader || !faceShader)throw new InvalidOperationException("Experience shaders did not import.");
            var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Museum/Features/HelmetInspection/Materials/Window.mat");
            glass.shader=glassShader;glass.SetColor("_BaseColor",new Color(.16f,.30f,.32f,.16f));EditorUtility.SetDirty(glass);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/SectionFace.mat");
            if(!material){material=new Material(faceShader){name="SectionFace"};AssetDatabase.CreateAsset(material,Root+"/Materials/SectionFace.mat");}
            var data=AssetDatabase.LoadAssetAtPath<HelmetSectionMeshData>(Root+"/Data/SectionMeshData.asset");
            if(!data){data=ScriptableObject.CreateInstance<HelmetSectionMeshData>();AssetDatabase.CreateAsset(data,Root+"/Data/SectionMeshData.asset");}
            var prefab=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var filters=prefab.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f=>f.sharedMesh && !f.name.Contains("Glass") && !f.GetComponentInParent<HelmetSectionInteractable>()).ToArray();
                data.entries=filters.Select(f=>f.sharedMesh).Distinct().Select(mesh=>new HelmetSectionMeshData.Entry
                    {source=mesh,vertices=mesh.vertices,triangles=mesh.triangles}).ToArray();
                EditorUtility.SetDirty(data);
                var surface=prefab.GetComponent<HelmetSectionSurface>()??prefab.AddComponent<HelmetSectionSurface>();surface.Configure(data,material);
                if(!prefab.GetComponent<HelmetMechanicalFeedback>())prefab.AddComponent<HelmetMechanicalFeedback>();
                PrefabUtility.SaveAsPrefabAsset(prefab,Prefab);
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var table=AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            TrilingualMuseumSetup.ApplyTable(table,table.FontAsset);
            var scene=EditorSceneManager.OpenScene(Scene);
            var resetButton=GameObject.Find("Helmet Reset Button").GetComponent<XRSimpleInteractable>();
            var reset=UnityEngine.Object.FindObjectOfType<HelmetExhibitReset>();
            bool updated=false;
            for(int i=0;i<resetButton.selectEntered.GetPersistentEventCount();i++)
            {
                if(resetButton.selectEntered.GetPersistentTarget(i)!=reset)continue;
                string method=resetButton.selectEntered.GetPersistentMethodName(i);
                if(method!="ResetToDisplay" && method!="RequestResetToDisplay")throw new InvalidOperationException("Unexpected reset wiring: "+method);
                UnityEventTools.RegisterVoidPersistentListener(resetButton.selectEntered,i,reset.RequestResetToDisplay);updated=true;
            }
            if(!updated)throw new InvalidOperationException("Existing reset button binding missing.");
            var panel=UnityEngine.Object.FindObjectOfType<HelmetExplorationPanel>();
            var control=panel.GetComponentsInChildren<HelmetExplorationButton>(true).First(b=>b.Command==HelmetExplorationButton.Action.ReduceMotion);
            var existing=panel.GetComponentsInChildren<HelmetExplorationButton>(true).FirstOrDefault(b=>b.Command==HelmetExplorationButton.Action.Sound);
            if(!existing)
            {
                var go=UnityEngine.Object.Instantiate(control.gameObject,control.transform.parent);go.name="Exploration SOUND";
                go.GetComponent<HelmetExplorationButton>().Configure(UnityEngine.Object.FindObjectOfType<HelmetExplorationController>(),panel,HelmetExplorationButton.Action.Sound);
                var template=control.transform.parent.Find("Exploration MOTION label");
                if(!template)template=control.transform.parent.Find("Exploration REDUCE MOTION label");
                if(!template)template=panel.GetComponentsInChildren<TextMeshPro>(true).First(t=>t.text=="Motion: on" || t.name.Contains("MOTION")).transform;
                var label=UnityEngine.Object.Instantiate(template.gameObject,go.transform.parent);label.name="Exploration SOUND label";
                var text=label.GetComponent<TextMeshPro>();text.text="Sound: on";text.font=table.FontAsset;
                var fixedKey=label.GetComponent<LocalizedText>();if(fixedKey)UnityEngine.Object.DestroyImmediate(fixedKey);
                var source=label.GetComponent<LocalizedSourceText>()??label.AddComponent<LocalizedSourceText>();
                source.Configure(table,MuseumText.Channel??AssetDatabase.LoadAssetAtPath<LanguageChannel>("Assets/Museum/Features/Localization/Channels/LanguageChannel.asset"),"Sound: on");
            }
            // Bind the source with the actual scene channel rather than relying on an editor static.
            var soundLabel=panel.GetComponentsInChildren<TextMeshPro>(true).Single(t=>t.name=="Exploration SOUND label");
            var channel=UnityEngine.Object.FindObjectOfType<LanguageButton>().GetComponent<LanguageButton>();
            var so=new SerializedObject(channel);var language=so.FindProperty("channel").objectReferenceValue as LanguageChannel;
            soundLabel.GetComponent<LocalizedSourceText>().Configure(table,language,"Sound: on");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("EXPERIENCE_SETUP_OK "+backup+"; captured mesh count="+data.entries.Length+"; triangles="+data.entries.Sum(e=>e.triangles.Length/3));
        }
    }
}
#endif
