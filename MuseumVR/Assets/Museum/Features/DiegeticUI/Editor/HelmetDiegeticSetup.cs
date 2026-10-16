#if UNITY_EDITOR
using System;
using System.IO;
using Museum.Eras;
using Museum.Exploration;
using Museum.Inspection;
using Museum.Localization;
using Museum.Localization.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Museum.Diegetic.Editor
{
    public static class HelmetDiegeticSetup
    {
        const string ScenePath="Assets/Museum/Scenes/MuseumStart.unity";
        const string Root="Assets/Museum/Features/DiegeticUI";
        [MenuItem("Museum/Exhibit UI/Install into current MuseumStart")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var inspection=UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            var era=UnityEngine.Object.FindObjectOfType<HelmetEraSwitcher>();
            var exploration=UnityEngine.Object.FindObjectOfType<HelmetExplorationController>();
            var demo=UnityEngine.Object.FindObjectOfType<HelmetConnectionDemonstration>();
            if(!inspection || !era || !exploration || !demo || !Camera.main)throw new InvalidOperationException("Saved exhibit components missing.");
            string backup=Path.GetFullPath("../tmp/diegetic-install-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(backup);File.Copy(ScenePath,Path.Combine(backup,"MuseumStart.before.unity"));
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/ExhibitUI.shader");
            if(!shader)throw new InvalidOperationException("Exhibit UI shader missing.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/ExhibitUI.mat");
            if(!material){material=new Material(shader){name="Exhibit UI"};AssetDatabase.CreateAsset(material,Root+"/Materials/ExhibitUI.mat");}
            material.SetColor("_BaseColor",Color.white);
            var feature=UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
            if(!feature)feature=new GameObject("Helmet physical UI station").AddComponent<HelmetDiegeticStation>();
            feature.Configure(inspection,era,exploration,demo,material,Camera.main.transform);
            EditorUtility.SetDirty(feature);
            var table=AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            TrilingualMuseumSetup.ApplyTable(table,table.FontAsset);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("DIEGETIC_UI_INSTALLED: additive main-scene station; original prefab/rig/input untouched. "+backup);
        }
        public static void InstallBatch()
        {
            try{Install();TrilingualFontBaker.RunBatch();AssetDatabase.SaveAssets();EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        /// <summary>Refresh visitor text without reserializing the scene or rebuilding an unchanged font.</summary>
        public static void RefreshTextBatch()
        {
            try
            {
                var table=AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
                if(!table || !table.FontAsset)throw new InvalidOperationException("Saved localization table/font missing.");
                TrilingualMuseumSetup.ApplyTable(table,table.FontAsset);
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [MenuItem("Museum/Exhibit UI/Open main scene")]
        public static void OpenMain()=>EditorSceneManager.OpenScene(ScenePath);
    }
}
#endif
