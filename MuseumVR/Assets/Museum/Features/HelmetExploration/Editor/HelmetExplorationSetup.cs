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
    /// <summary>Explicit first-time addition to the saved museum. Never regenerates the helmet or XR rig.</summary>
    public static class HelmetExplorationSetup
    {
        const string Root = "Assets/Museum/Features/HelmetExploration";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";

        public static void InstallBatch()
        {
            try { Install(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Museum/Exploration/Install into MuseumStart (first time only)")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before installing.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (Object.FindObjectOfType<HelmetExplorationController>(true))
                throw new InvalidOperationException("Exploration already installed. Open the saved scene; do not reinstall.");
            var inspection = Object.FindObjectOfType<HelmetInspectionController>();
            var palette = Object.FindObjectOfType<HelmetInspectionPalette>();
            var air = inspection ? inspection.GetComponentsInChildren<Renderer>(true).SingleOrDefault(r => r.name == "GB_AirConnector") : null;
            var controlsMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Museum/Features/HelmetInspection/Materials/Controls.mat");
            if (!inspection || !palette || !air || !Camera.main || !controlsMaterial)
                throw new InvalidOperationException("Saved inspection palette / air connector / XR camera missing; no scene saved.");
            var originalCamera = new Pose(Camera.main.transform.position, Camera.main.transform.rotation);
            var originalObjects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var originalContents = Enumerable.Range(0, palette.transform.childCount).Select(i => palette.transform.GetChild(i).gameObject)
                .Where(g => g.name != "Palette back").ToArray();
            string backup = Path.GetFullPath("../tmp/exploration-install-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, Path.Combine(backup, "MuseumStart.before.unity"));

            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            var airMat = Material("AirSignal", new Color(.18f, .82f, .80f));
            var voiceMat = Material("VoiceSignal", new Color(1f, .65f, .30f));
            var inkMat = Material("DiagramInk", new Color(.035f, .09f, .12f));
            var feature = new GameObject("Helmet Exploration");
            var demonstration = feature.AddComponent<HelmetConnectionDemonstration>();
            demonstration.Configure(inspection, air, airMat, voiceMat, inkMat);
            var controller = feature.AddComponent<HelmetExplorationController>();
            controller.Configure(inspection, demonstration);
            var clue = Child(feature.transform, "Air connection clue");
            clue.AddComponent<HelmetExplorationHotspot>().Configure(controller, inspection, air, airMat, Camera.main.transform);

            var tabs = Child(palette.transform, "Exploration tabs");
            var page = Child(palette.transform, "Exploration page");
            var panel = palette.gameObject.AddComponent<HelmetExplorationPanel>();
            var title = Label(page.transform, "Exploration title", "EXPLORE AT YOUR PACE", .281f, .046f, .85f);
            var progress = Label(page.transform, "Exploration progress", "AIR: unexplored   /   VOICE: unexplored", .24f, .028f, .48f);
            var body = Label(page.transform, "Exploration explanation", "Could a diver work alone in this helmet?\nLook around it. Choose AIR or VOICE in any order.", .069f, .158f, .60f);
            body.alignment = TextAlignmentOptions.TopLeft;
            var footer = Label(page.transform, "Exploration footer", "Grip to select. HINT is optional. INSPECT returns to the tools.", -.277f, .052f, .44f);

            Button(tabs.transform, controller, panel, "INSPECT", -.12f, .363f, HelmetExplorationButton.Action.Inspect, controlsMaterial);
            Button(tabs.transform, controller, panel, "EXPLORE", .12f, .363f, HelmetExplorationButton.Action.Explore, controlsMaterial);
            Button(page.transform, controller, panel, "AIR", -.12f, .192f, HelmetExplorationButton.Action.Air, controlsMaterial);
            Button(page.transform, controller, panel, "VOICE", .12f, .192f, HelmetExplorationButton.Action.Voice, controlsMaterial);
            Button(page.transform, controller, panel, "HINT", -.12f, -.065f, HelmetExplorationButton.Action.Hint, controlsMaterial);
            Button(page.transform, controller, panel, "REVIEW", .12f, -.065f, HelmetExplorationButton.Action.Review, controlsMaterial);
            Button(page.transform, controller, panel, "REPLAY", -.12f, -.13f, HelmetExplorationButton.Action.Replay, controlsMaterial);
            Button(page.transform, controller, panel, "STOP", .12f, -.13f, HelmetExplorationButton.Action.Stop, controlsMaterial);
            Button(page.transform, controller, panel, "BACK", -.12f, -.195f, HelmetExplorationButton.Action.Back, controlsMaterial);
            Button(page.transform, controller, panel, "NEW VISIT", .12f, -.195f, HelmetExplorationButton.Action.NewVisit, controlsMaterial);
            panel.Configure(controller, palette, page, tabs, originalContents, title, body, progress, footer);
            palette.ConfigureAdditionalPage(.085f);
            palette.RecenterNow();
            foreach (var renderer in feature.GetComponentsInChildren<Renderer>(true).Concat(page.GetComponentsInChildren<Renderer>(true)).Concat(tabs.GetComponentsInChildren<Renderer>(true)))
            { renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; }
            if (originalObjects.Any(o => !o) || Camera.main.transform.position != originalCamera.position || Camera.main.transform.rotation != originalCamera.rotation)
                throw new InvalidOperationException("Unexpected original object removal or camera change; refusing to save.");
            if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                throw new InvalidOperationException("Missing scripts; refusing to save.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("HELMET_EXPLORATION_INSTALLED: saved main scene, existing XR and inspection retained. Backup: " + backup);
        }

        static Material Material(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) throw new InvalidOperationException("Owned material already exists: " + path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) throw new InvalidOperationException("URP Unlit shader missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        static GameObject Child(Transform parent, string name)
        { var result = new GameObject(name); result.transform.SetParent(parent, false); return result; }
        static TextMeshPro Label(Transform parent, string name, string value, float y, float height, float font)
        {
            var text = Child(parent, name).AddComponent<TextMeshPro>();
            text.transform.localPosition = new Vector3(0f, y, -.043f);
            text.font = TMP_Settings.defaultFontAsset; text.text = value; text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(.436f, height);
            text.enableWordWrapping = true; text.enableAutoSizing = true;
            text.fontSizeMin = .02f; text.fontSizeMax = font;
            text.alignment = TextAlignmentOptions.Center;
            text.ForceMeshUpdate(true);
            return text;
        }
        static void Button(Transform parent, HelmetExplorationController controller, HelmetExplorationPanel panel,
            string label, float x, float y, HelmetExplorationButton.Action action, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Exploration " + label; go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, -.027f);
            go.transform.localScale = new Vector3(.222f, .055f, .025f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            var interactable = go.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Add(go.GetComponent<Collider>()); interactable.interactionLayers = 1;
            go.AddComponent<HelmetExplorationButton>().Configure(controller, panel, action);
            var text = Label(parent, "Exploration " + label + " label", label, y, .049f, .8f);
            text.transform.localPosition += Vector3.right * x;
            text.rectTransform.sizeDelta = new Vector2(.211f, .049f);
        }
    }
}
#endif
