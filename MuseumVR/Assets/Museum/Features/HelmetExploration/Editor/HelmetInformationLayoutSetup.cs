#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Museum.Inspection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Object = UnityEngine.Object;

namespace Museum.Exploration.Editor
{
    /// <summary>One explicit presentation migration of the saved scene. Does not rebuild assets or XR.</summary>
    public static class HelmetInformationLayoutSetup
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string Materials = "Assets/Museum/Features/HelmetExploration/Materials/";

        public static void ApplyBatch()
        {
            try { Apply(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // Refresh presentation after a typography change without running the one-time migration again.
        public static void RefreshSavedLayoutBatch()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath);
                var panel = Object.FindObjectOfType<HelmetExplorationPanel>(true);
                if (!panel || !panel.ProgressiveLayoutEnabled)
                    throw new InvalidOperationException("Expected the already upgraded main scene.");
                panel.ShowInspection();
                panel.ConfigureBackgroundLabels(new[] { Find(scene, "Gallery masthead").gameObject, Find(scene, "Central question").gameObject });
                ChangeLabel(scene, "Helmet Instructions", "Diving helmet\n<size=62%>Mark V-inspired study</size>", .4f);
                ChangeLabel(scene, "Reset Button Label", "Reset", .32f);
                Object.FindObjectOfType<HelmetInspectionPalette>().RecenterNow();
                EditorUtility.SetDirty(panel);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("INFORMATION_LAYOUT_REFRESHED");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Museum/Exploration/Apply concise information layout")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before changing information layout.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var panel = Object.FindObjectOfType<HelmetExplorationPanel>(true);
            var controller = Object.FindObjectOfType<HelmetExplorationController>(true);
            var palette = Object.FindObjectOfType<HelmetInspectionPalette>(true);
            var inspection = Object.FindObjectOfType<HelmetInspectionController>();
            if (!panel || !controller || !palette || !inspection || !Camera.main)
                throw new InvalidOperationException("Saved main-scene dependencies missing; no changes saved.");
            if (palette.transform.Find("Exploration MORE TOOLS"))
                throw new InvalidOperationException("Information layout already applied. Use the saved scene; no migration required.");
            var original = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var cameraPose = new Pose(Camera.main.transform.position, Camera.main.transform.rotation);
            var helmetPose = new Pose(inspection.transform.position, inspection.transform.rotation);
            var backup = Path.GetFullPath("../tmp/information-layout-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, Path.Combine(backup, "MuseumStart.before.unity"));

            var background = NewMaterial("InformationPanel", new Color(.028f, .046f, .055f));
            var buttons = NewMaterial("InformationControl", new Color(.073f, .125f, .142f));
            var tabs = palette.transform.Find("Exploration tabs");
            var page = palette.transform.Find("Exploration page");
            if (!tabs || !page) throw new InvalidOperationException("Original exploration layout missing.");
            AddButton(tabs, controller, panel, "HELP", HelmetExplorationButton.Action.Help, buttons);
            AddButton(page, controller, panel, "MORE", HelmetExplorationButton.Action.More, buttons);
            AddButton(page, controller, panel, "DETAILS", HelmetExplorationButton.Action.Details, buttons);
            AddButton(page, controller, panel, "CLOSE", HelmetExplorationButton.Action.CloseReading, buttons);
            AddButton(page, controller, panel, "REDUCE MOTION", HelmetExplorationButton.Action.ReduceMotion, buttons);
            AddButton(palette.transform, controller, panel, "MORE TOOLS", HelmetExplorationButton.Action.InspectMore, buttons);
            foreach (var renderer in palette.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponent<TMP_Text>()) continue;
                renderer.sharedMaterial = renderer.name == "Palette back" ? background : buttons;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach (var label in palette.GetComponentsInChildren<TextMeshPro>(true))
            {
                label.color = new Color(.94f, .95f, .92f);
                label.fontStyle = FontStyles.Normal;
            }

            // Keep a single interpretive question. Other walls frame the exhibit without repeating instructions.
            ChangeLabel(scene, "Gallery masthead", "Beneath the surface", 2.75f);
            ChangeLabel(scene, "Central question", "Could you work alone\nin this helmet?", 2.05f);
            foreach (var name in new[] { "Gallery subtitle", "Central prompt", "Left rear marker", "Left rear prompt", "Right rear marker", "Right rear prompt",
                "Side caption -1", "Side caption 1", "Side quiet marker -1", "Side quiet marker 1", "Side quiet text -1", "Side quiet text 1",
                "Arrival introduction", "Arrival instructions", "Arrival accessibility" })
                Hide(scene, name);
            ChangeLabel(scene, "Side masthead -1", "Inside the helmet", 2.0f);
            ChangeLabel(scene, "Side masthead 1", "Connected to the surface", 2.0f);
            ChangeLabel(scene, "Arrival brand", "The diving gallery", 2.6f);

            // The existing central object label is the main identity, not a second operating manual.
            ChangeLabel(scene, "Helmet Instructions", "Diving helmet\n<size=62%>Mark V-inspired study</size>", .4f);
            Hide(scene, "Plinth title 0");
            Hide(scene, "Plinth caption 0");
            ChangeLabel(scene, "Reset Button Label", "Reset", .32f);
            var resetButton = Find(scene, "Helmet Reset Button");
            if (resetButton && resetButton.TryGetComponent<Renderer>(out var resetSurface))
            {
                resetSurface.sharedMaterial = buttons;
                PrefabUtility.RecordPrefabInstancePropertyModifications(resetSurface);
            }

            panel.EnableProgressiveLayout();
            panel.ConfigureBackgroundLabels(new[] { Find(scene, "Gallery masthead").gameObject, Find(scene, "Central question").gameObject });
            EditorUtility.SetDirty(panel);
            palette.RecenterNow();
            foreach (var renderer in palette.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            if (original.Any(t => !t) || Camera.main.transform.position != cameraPose.position || Camera.main.transform.rotation != cameraPose.rotation ||
                inspection.transform.position != helmetPose.position || inspection.transform.rotation != helmetPose.rotation)
                throw new InvalidOperationException("Original object removed or exhibit/XR pose changed; refusing to save.");
            if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                throw new InvalidOperationException("Missing script; refusing to save.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("INFORMATION_LAYOUT_SAVED: existing scene and identities retained; backup: " + backup);
        }

        static Material NewMaterial(string name, Color color)
        {
            string path = Materials + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) throw new InvalidOperationException("Owned presentation material already exists: " + path);
            var template = AssetDatabase.LoadAssetAtPath<Material>(Materials + "DiagramInk.mat");
            if (!template || !template.shader) throw new InvalidOperationException("Existing unlit diagram material missing.");
            var result = new Material(template) { name = name };
            result.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(result, path);
            return result;
        }
        static Transform Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).SingleOrDefault(t => t.name == name);
        static void Hide(Scene scene, string name)
        {
            var target = Find(scene, name);
            if (!target) throw new InvalidOperationException("Expected existing label not found: " + name);
            target.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
        }
        static void ChangeLabel(Scene scene, string name, string value, float maximumSize)
        {
            var target = Find(scene, name);
            var label = target ? target.GetComponent<TextMeshPro>() : null;
            if (!label) throw new InvalidOperationException("Expected existing label not found: " + name);
            label.text = value;
            label.fontSizeMax = maximumSize;
            label.enableAutoSizing = true;
            label.fontStyle = FontStyles.Normal;
            label.ForceMeshUpdate(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }
        static void AddButton(Transform parent, HelmetExplorationController controller, HelmetExplorationPanel panel,
            string name, HelmetExplorationButton.Action action, Material material)
        {
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "Exploration " + name;
            button.transform.SetParent(parent, false);
            button.transform.localPosition = new Vector3(0, 0, -.027f);
            button.transform.localScale = new Vector3(.222f, .055f, .025f);
            button.GetComponent<Renderer>().sharedMaterial = material;
            var interactable = button.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Add(button.GetComponent<Collider>());
            interactable.interactionLayers = 1;
            button.AddComponent<HelmetExplorationButton>().Configure(controller, panel, action);
            var text = new GameObject("Exploration " + name + " label").AddComponent<TextMeshPro>();
            text.transform.SetParent(parent, false);
            text.transform.localPosition = new Vector3(0, 0, -.043f);
            text.font = TMP_Settings.defaultFontAsset;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(.211f, .049f);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = .1f; text.fontSizeMax = .45f;
            text.enableWordWrapping = false;
            text.text = name;
        }
    }
}
#endif
