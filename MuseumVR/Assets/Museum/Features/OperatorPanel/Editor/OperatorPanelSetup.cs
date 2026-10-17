#if UNITY_EDITOR
using System;
using System.IO;
using Museum.Diegetic;
using Museum.Localization;
using Museum.Localization.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Museum.OperatorPanel.Editor
{
    /// <summary>Installs the summonable operator panel onto the saved main-scene station.
    /// The scene diff is a single component; the entry button itself is built at runtime.</summary>
    public static class OperatorPanelSetup
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string MaterialPath = "Assets/Museum/Features/DiegeticUI/Materials/ExhibitUI.mat";

        [MenuItem("Museum/Operator Panel/Install into current MuseumStart")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
            if (!station) throw new InvalidOperationException("HelmetDiegeticStation missing; install the diegetic UI first.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material) throw new InvalidOperationException("Exhibit UI material missing.");
            string backup = Path.GetFullPath("../tmp/operator-panel-install-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, Path.Combine(backup, "MuseumStart.before.unity"));
            var controller = station.GetComponent<OperatorPanelController>();
            if (!controller) controller = station.gameObject.AddComponent<OperatorPanelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("station").objectReferenceValue = station;
            so.FindProperty("viewer").objectReferenceValue = Camera.main ? Camera.main.transform : null;
            so.FindProperty("uiMaterial").objectReferenceValue = material;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            if (table) TrilingualMuseumSetup.ApplyTable(table, table.FontAsset);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("OPERATOR_PANEL_INSTALLED: component added to station; entry button is runtime-built. " + backup);
        }

        [MenuItem("Museum/Operator Panel/Remove from current MuseumStart")]
        public static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
            var controller = station ? station.GetComponent<OperatorPanelController>() : null;
            if (!controller) { Debug.Log("OPERATOR_PANEL_REMOVE: nothing to remove."); return; }
            string backup = Path.GetFullPath("../tmp/operator-panel-remove-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, Path.Combine(backup, "MuseumStart.before.unity"));
            UnityEngine.Object.DestroyImmediate(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("OPERATOR_PANEL_REMOVED: component deleted; station and panels untouched. " + backup);
        }

        public static void InstallBatch()
        {
            try { Install(); TrilingualFontBaker.RunBatch(); AssetDatabase.SaveAssets(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Museum/Operator Panel/Bake new glyphs (run after JSON edits)")]
        public static void BakeGlyphs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode.");
            TrilingualFontBaker.RunBatch();
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            if (table && table.FontAsset) TrilingualMuseumSetup.ApplyTable(table, table.FontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log("OPERATOR_PANEL_GLYPHS_BAKED: font and table updated.");
        }
    }
}
#endif
