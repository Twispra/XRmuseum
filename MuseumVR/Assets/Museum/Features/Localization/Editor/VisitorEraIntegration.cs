#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Museum.Eras;
using Museum.Exploration;
using Museum.Hotspots;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Museum.Localization.EditorTools
{
    /// <summary>Explicit, scoped migration for Issue 57. Never runs on open or Play.</summary>
    public static class VisitorEraIntegration
    {
        const string Root = "Assets/Museum/Features/Localization";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";

        public static void RunBatch()
        {
            int code = 1;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play before migrating saved visitor content.");
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/NotoSansSC-MuseumSubset SDF.asset");
                string required = File.ReadAllText(Root + "/Fonts/characters-required.txt");
                if (!font || required.Any(c => !char.IsWhiteSpace(c) && !font.HasCharacter(c)))
                    TrilingualFontBaker.RunBatch();
                var scene = EditorSceneManager.OpenScene(ScenePath);
                var all = scene.GetRootGameObjects();
                var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(Root + "/Tables/LocalizationTable.asset");
                var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(Root + "/Tables/LanguageChannel.asset");
                if (!table || !channel || !table.FontAsset) throw new InvalidOperationException("Missing saved localization assets.");
                TrilingualMuseumSetup.ApplyTable(table, table.FontAsset);
                var buttons = all.SelectMany(r => r.GetComponentsInChildren<HelmetEraButton>(true)).ToArray();
                if (buttons.Length != 2) throw new InvalidOperationException("Expected two existing era buttons; refusing to rebuild the exhibit.");
                foreach (var button in buttons)
                {
                    var labelTransform = button.transform.Find("Era label");
                    TMP_Text label;
                    if (labelTransform) label = labelTransform.GetComponent<TMP_Text>();
                    else
                    {
                        var go = new GameObject("Era label", typeof(TextMeshPro));
                        go.transform.SetParent(button.transform, false);
                        label = go.GetComponent<TMP_Text>();
                    }
                    if (!label) throw new InvalidOperationException("Existing era label has an unexpected component.");
                    // Existing button/collider positions and dimensions stay unchanged.
                    // Cancel the button's cube scale so text uses metre-space dimensions.
                    var scale = button.transform.lossyScale;
                    label.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                    label.transform.localPosition = Vector3.back * .55f;
                    label.transform.localRotation = Quaternion.identity;
                    label.font = table.FontAsset;
                    label.fontSharedMaterial = table.FontAsset.material;
                    label.fontSize = .25f;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = .20f;
                    label.fontSizeMax = .25f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = Color.white;
                    label.enableWordWrapping = true;
                    label.rectTransform.sizeDelta = new Vector2(.15f, .064f);
                    label.raycastTarget = false;
                    var localized = label.GetComponent<LocalizedText>();
                    if (!localized) localized = label.gameObject.AddComponent<LocalizedText>();
                    localized.Configure(table, channel, button.Direction == HelmetEraButton.StepDirection.Previous ? "era.previous" : "era.next");
                    label.ForceMeshUpdate(true);
                    if (label.isTextOverflowing) throw new InvalidOperationException("English era button label overflows: " + button.name);
                }
                foreach (var exploration in all.SelectMany(r => r.GetComponentsInChildren<HelmetExplorationController>(true)))
                {
                    var serialized = new SerializedObject(exploration);
                    var inspection = serialized.FindProperty("inspection").objectReferenceValue as Museum.Inspection.HelmetInspectionController;
                    if (!inspection) throw new InvalidOperationException("Missing saved inspection reference.");
                    var tours = inspection.GetComponentsInChildren<HelmetHotspotTour>(true)
                        .Where(t => t.GetComponentInParent<Museum.Inspection.HelmetInspectionController>() == inspection).ToArray();
                    if (tours.Length != 1) throw new InvalidOperationException("A unique tour is required for saved New Visit wiring.");
                    serialized.FindProperty("visitTour").objectReferenceValue = tours[0];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("VISITOR_ERA_INTEGRATION_SAVED: two localized labels, current table, explicit New Visit tour; existing XR, geometry and collider configuration preserved.");
                code = 0;
            }
            catch (Exception ex) { Debug.LogException(ex); }
            EditorApplication.Exit(code);
        }
    }
}
#endif
