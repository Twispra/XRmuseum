#if UNITY_EDITOR
using System;
using System.Linq;
using Museum.Eras;
using Museum.Localization;
using Museum.Localization.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhotoHelmetTrial.EditorTools
{
    /// <summary>Places the photo study on the unused left plinth without touching the era exhibit.</summary>
    public static class PhotoHelmetGallerySetup
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string PhotoPrefabPath = "Assets/Museum/Features/PhotoHelmetTrial/Prefabs/PhotoHelmetStudy.prefab";
        const string TablePath = "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset";
        const string ChannelPath = "Assets/Museum/Features/Localization/Tables/LanguageChannel.asset";
        const string PanelPath = "Assets/Museum/Features/HelmetExploration/Materials/InformationPanel.mat";
        const string ButtonPath = "Assets/Museum/Features/Localization/Materials/Language-Button.mat";
        const string PressedPath = "Assets/Museum/Features/Localization/Materials/Language-ButtonPressed.mat";
        const string ExhibitName = "Exhibit 01 - Placeholder";
        const string DisplayName = "1988 Photo Helmet Display";

        [MenuItem("Museum/Photo helmet/Frame 1988 study from visitor side")]
        public static void FrameFromVisitorSide()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (!sceneView) throw new InvalidOperationException("Open a Scene view first.");
            sceneView.orthographic = false;
            sceneView.LookAt(new Vector3(-3.1f, 1.14f, 3.5f), Quaternion.identity, 1.35f);
            sceneView.Repaint();
        }

        [MenuItem("Museum/Photo helmet/Place 1988 study on left plinth")]
        public static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before editing MuseumStart.");
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save or discard your current scene edits before opening MuseumStart.");

            var photoPrefab = Required<GameObject>(PhotoPrefabPath);
            var table = Required<LocalizationTable>(TablePath);
            var channel = Required<LanguageChannel>(ChannelPath);
            var panelMaterial = Required<Material>(PanelPath);
            var buttonMaterial = Required<Material>(ButtonPath);
            var pressedMaterial = Required<Material>(PressedPath);
            var copy = TrilingualMuseumSetup.ReadCopy();
            foreach (var key in new[] { "exhibit.photo1988.title", "exhibit.photo1988.boundary" })
                if (!copy.entries.Any(entry => entry.key == key &&
                    !string.IsNullOrEmpty(entry.english) &&
                    !string.IsNullOrEmpty(entry.chinese) &&
                    !string.IsNullOrEmpty(entry.traditionalChinese)))
                    throw new InvalidOperationException("Missing trilingual photo-study text: " + key);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var stands = scene.GetRootGameObjects().SingleOrDefault(go => go.name == "Exhibit Placeholders");
            var left = stands ? stands.transform.Find(ExhibitName) : null;
            if (!left)
                throw new InvalidOperationException("The unused left plinth was not found in MuseumStart.");
            if (left.Find(DisplayName))
                throw new InvalidOperationException("The 1988 study is already placed; this command never replaces a saved exhibit.");
            var top = left.Find("Display Top");
            var topRenderer = top ? top.GetComponent<Renderer>() : null;
            if (!topRenderer) throw new InvalidOperationException("The left plinth has no Display Top renderer.");
            var interactionManager = InteractionManager(scene);

            TrilingualMuseumSetup.ApplyTable(table, table.FontAsset);
            AssetDatabase.SaveAssets();

            var root = new GameObject(DisplayName);
            root.transform.SetParent(left, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(photoPrefab, root.transform);
            model.name = "Photo helmet model - visual interpretation";
            model.transform.position = new Vector3(topRenderer.bounds.center.x, 0, topRenderer.bounds.center.z);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Photo helmet prefab has no visible renderers.");
            var modelBounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) modelBounds.Encapsulate(renderer.bounds);
            if (modelBounds.size.y < 0.35f || modelBounds.size.y > 0.6f)
                throw new InvalidOperationException("Photo helmet display height is outside the expected 0.45 m range.");
            model.transform.position += Vector3.up * (topRenderer.bounds.max.y + 0.02f - modelBounds.min.y);
            var display = root.AddComponent<PhotoHelmetGalleryDisplay>();
            display.Configure(model.transform);

            var frontZ = topRenderer.bounds.min.z - 0.035f;
            var centerX = topRenderer.bounds.center.x;
            MakePanel(root.transform, "Photo study label backing",
                new Vector3(centerX, 0.75f, frontZ), new Vector3(1.34f, 0.75f, 0.025f), panelMaterial);
            MakeText(root.transform, "Photo study title", new Vector3(centerX, 0.96f, frontZ - 0.023f),
                new Vector2(1.25f, 0.16f), 0.65f, "exhibit.photo1988.title", table, channel);
            MakeText(root.transform, "Photo study boundary", new Vector3(centerX, 0.70f, frontZ - 0.023f),
                new Vector2(1.25f, 0.28f), 0.42f, "exhibit.photo1988.boundary", table, channel);

            float buttonY = topRenderer.bounds.max.y + 0.055f;
            float buttonZ = topRenderer.bounds.min.z + 0.19f;
            MakeButton(root.transform, "Turn photo helmet left", new Vector3(centerX - 0.42f, buttonY, buttonZ),
                PhotoHelmetGalleryButton.Action.TurnLeft, "text.b4b2cfcf7329", display,
                table, channel, buttonMaterial, pressedMaterial, interactionManager);
            MakeButton(root.transform, "Reset photo helmet", new Vector3(centerX, buttonY, buttonZ),
                PhotoHelmetGalleryButton.Action.Reset, "text.7ef2fad58d1f", display,
                table, channel, buttonMaterial, pressedMaterial, interactionManager);
            MakeButton(root.transform, "Turn photo helmet right", new Vector3(centerX + 0.42f, buttonY, buttonZ),
                PhotoHelmetGalleryButton.Action.TurnRight, "text.75cf34e2bbba", display,
                table, channel, buttonMaterial, pressedMaterial, interactionManager);

            Validate(display, topRenderer, table);
            var oldLabel = left.Find("Exhibit Number");
            if (oldLabel) oldLabel.gameObject.SetActive(false);
            left.name = "Exhibit 01 - 1988 Photo Helmet Study";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("PHOTO_HELMET_GALLERY placed=left-plinth; three-era catalogue and central helmet unchanged; photo-study identity remains unverified.");
        }

        static T Required<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidOperationException("Required asset is missing: " + path);
            return asset;
        }

        static void MakePanel(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(parent, true);
            panel.transform.position = position;
            panel.transform.localScale = scale;
            panel.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());
        }

        static TextMeshPro MakeText(Transform parent, string name, Vector3 position,
            Vector2 size, float fontSize, string key, LocalizationTable table, LanguageChannel channel)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, true);
            var label = textObject.AddComponent<TextMeshPro>();
            textObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            label.font = table.FontAsset;
            label.fontSize = fontSize;
            label.fontSizeMin = fontSize * 0.70f;
            label.fontSizeMax = fontSize;
            label.enableAutoSizing = true;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Truncate;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = size;
            textObject.AddComponent<LocalizedText>().Configure(table, channel, key);
            return label;
        }

        static void MakeButton(Transform parent, string name, Vector3 position,
            PhotoHelmetGalleryButton.Action action, string labelKey, PhotoHelmetGalleryDisplay display,
            LocalizationTable table, LanguageChannel channel, Material normal, Material pressed,
            XRInteractionManager interactionManager)
        {
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(parent, true);
            button.transform.position = position;
            button.transform.localScale = new Vector3(0.34f, 0.09f, 0.17f);
            var renderer = button.GetComponent<Renderer>();
            renderer.sharedMaterial = normal;
            var interactable = button.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Add(button.GetComponent<Collider>());
            interactable.interactionManager = interactionManager;
            button.AddComponent<PhotoHelmetGalleryButton>().Configure(display, action, renderer, pressed);
            MakeText(parent, name + " label", position + new Vector3(0, 0, -0.094f),
                new Vector2(0.32f, 0.075f), 0.50f, labelKey, table, channel);
        }

        [MenuItem("Museum/Photo helmet/Update saved 1988 exhibit")]
        public static void RepairSavedLabels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before editing MuseumStart.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved, unmodified MuseumStart scene first.");
            var stands = scene.GetRootGameObjects().Single(go => go.name == "Exhibit Placeholders");
            var left = stands.transform.Find("Exhibit 01 - 1988 Photo Helmet Study");
            if (!left) throw new InvalidOperationException("The 1988 independent exhibit is absent.");
            var oldLabel = left.Find("Exhibit Number");
            if (oldLabel) oldLabel.gameObject.SetActive(false);
            var root = left.Find(DisplayName);
            if (!root) throw new InvalidOperationException("The 1988 display root is absent.");
            var backing = root.Find("Photo study label backing");
            if (!backing) throw new InvalidOperationException("The 1988 label backing is absent.");
            backing.position = new Vector3(backing.position.x, 0.75f, backing.position.z);
            backing.localScale = new Vector3(1.34f, 0.75f, 0.025f);
            RepairText(root, "Photo study title", 0.65f, 0.96f, 0.16f);
            RepairText(root, "Photo study boundary", 0.42f, 0.70f, 0.28f);
            RepairText(root, "Turn photo helmet left label", 0.50f);
            RepairText(root, "Reset photo helmet label", 0.50f);
            RepairText(root, "Turn photo helmet right label", 0.50f);
            var manager = InteractionManager(scene);
            foreach (var button in root.GetComponentsInChildren<PhotoHelmetGalleryButton>())
                button.GetComponent<XRSimpleInteractable>().interactionManager = manager;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PHOTO_HELMET_GALLERY_LABELS front-facing, fitted, old placeholder sign hidden.");
        }

        static XRInteractionManager InteractionManager(Scene scene)
        {
            var managerObject = scene.GetRootGameObjects().SingleOrDefault(go => go.name == "XR Interaction Manager");
            var manager = managerObject ? managerObject.GetComponent<XRInteractionManager>() : null;
            if (!manager) throw new InvalidOperationException("MuseumStart has no XR Interaction Manager.");
            return manager;
        }

        static void RepairText(Transform root, string name, float fontSize,
            float? worldY = null, float? height = null)
        {
            var text = root.Find(name);
            if (!text) throw new InvalidOperationException("Missing 1988 exhibit label: " + name);
            text.rotation = Quaternion.identity;
            var label = text.GetComponent<TextMeshPro>();
            if (!label) throw new InvalidOperationException("Missing TextMeshPro on: " + name);
            if (worldY.HasValue) text.position = new Vector3(text.position.x, worldY.Value, text.position.z);
            if (height.HasValue)
                label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, height.Value);
            label.fontSize = fontSize;
            label.fontSizeMin = fontSize * 0.7f;
            label.fontSizeMax = fontSize;
            label.ForceMeshUpdate();
        }

        [MenuItem("Museum/Photo helmet/Validate independent exhibit (Play Mode)")]
        public static void ValidatePlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play Mode in MuseumStart first.");
            var root = GameObject.Find(DisplayName);
            Check(root, "1988 display root is missing");
            var display = root.GetComponent<PhotoHelmetGalleryDisplay>();
            Check(display && display.Model, "1988 photo model is missing");
            var buttons = root.GetComponentsInChildren<PhotoHelmetGalleryButton>();
            Check(buttons.Length == 3, "1988 display needs exactly three independent buttons");
            var manager = InteractionManager(SceneManager.GetActiveScene());
            foreach (var button in buttons)
            {
                var interaction = button.GetComponent<XRSimpleInteractable>();
                Check(button.Display == display && interaction &&
                    interaction.colliders.Contains(button.GetComponent<Collider>()) &&
                    interaction.interactionManager == manager,
                    "1988 button has no saved interaction manager/collider: " + button.name);
            }

            var eras = UnityEngine.Object.FindObjectOfType<HelmetEraSwitcher>();
            Check(eras && eras.AvailableEraCount == 3, "central three-era catalogue is missing");
            int initialEra = eras.CurrentIndex;
            var initialRotation = display.Model.localRotation;
            var left = buttons.Single(b => b.ButtonAction == PhotoHelmetGalleryButton.Action.TurnLeft);
            var reset = buttons.Single(b => b.ButtonAction == PhotoHelmetGalleryButton.Action.Reset);
            var right = buttons.Single(b => b.ButtonAction == PhotoHelmetGalleryButton.Action.TurnRight);
            left.TriggerProgrammatically();
            Check(Quaternion.Angle(initialRotation, display.Model.localRotation) > 29f,
                "left button did not rotate the photo model");
            Check(Vector3.Dot(display.Model.forward, Vector3.left) > 0.45f,
                "left button turned the photo model to the visitor's right");
            reset.TriggerProgrammatically();
            Check(Quaternion.Angle(initialRotation, display.Model.localRotation) < 0.1f,
                "reset button did not restore the photo model");
            right.TriggerProgrammatically();
            Check(Quaternion.Angle(initialRotation, display.Model.localRotation) > 29f,
                "right button did not rotate the photo model");
            Check(Vector3.Dot(display.Model.forward, Vector3.right) > 0.45f,
                "right button turned the photo model to the visitor's left");
            reset.TriggerProgrammatically();
            Check(eras.CurrentIndex == initialEra, "photo buttons changed the central era");

            var table = Required<LocalizationTable>(TablePath);
            var channel = Required<LanguageChannel>(ChannelPath);
            var initialLanguage = channel.Current;
            var title = root.transform.Find("Photo study title");
            var boundary = root.transform.Find("Photo study boundary");
            Check(title && boundary, "1988 exhibit labels are missing");
            foreach (var language in new[] {
                MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
            {
                channel.SetLanguage(language);
                Check(title.GetComponent<TextMeshPro>().text == table.Get("exhibit.photo1988.title", language),
                    "1988 title did not follow the language channel");
                Check(boundary.GetComponent<TextMeshPro>().text == table.Get("exhibit.photo1988.boundary", language),
                    "1988 caution did not follow the language channel");
            }
            channel.SetLanguage(initialLanguage);
            Debug.Log("PHOTO_HELMET_GALLERY_VALIDATION PASS: model, 3 XR colliders, turn/reset, 3 languages, central era unchanged. XR headset input not tested.");
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Validate(PhotoHelmetGalleryDisplay display, Renderer top, LocalizationTable table)
        {
            if (!display.Model || display.GetComponentsInChildren<PhotoHelmetGalleryButton>().Length != 3)
                throw new InvalidOperationException("The 1988 exhibit needs one model and three controls.");
            if (!table.Entries.Any(e => e.Key == "exhibit.photo1988.title") ||
                !table.Entries.Any(e => e.Key == "exhibit.photo1988.boundary"))
                throw new InvalidOperationException("The saved localization table lacks the photo-study copy.");
            var bounds = display.Model.GetComponentsInChildren<Renderer>().Select(r => r.bounds).ToArray();
            if (bounds.Length == 0 || bounds.Min(b => b.min.y) < top.bounds.max.y - 0.01f)
                throw new InvalidOperationException("The photo helmet intersects the left plinth.");
            var initial = display.Model.localRotation;
            display.TurnLeft();
            if (Quaternion.Angle(initial, display.Model.localRotation) < 20f)
                throw new InvalidOperationException("The independent exhibit did not rotate.");
            display.ResetDisplay();
            if (Quaternion.Angle(initial, display.Model.localRotation) > 0.1f)
                throw new InvalidOperationException("The independent exhibit did not reset.");
        }
    }
}
#endif
