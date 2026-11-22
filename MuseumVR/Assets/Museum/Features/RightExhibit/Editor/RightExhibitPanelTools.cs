using System;
using System.Linq;
using Museum.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Museum.RightExhibit.Editor
{
    public static class RightExhibitPanelTools
    {
        internal const string PrefabPath = "Assets/Museum/Features/RightExhibit/RightExhibitPanel.prefab";
        internal const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        internal const string TableFolder = "Assets/Museum/Features/Localization/Tables/";
        const string PanelName = "Right Exhibit Panel";
        const string Materials = "Assets/Museum/Features/MaritimeGallery/Materials/";
        internal const string Prefix = "exhibit.right_connection.";
        internal static readonly Vector3 MountPosition = new Vector3(3.1f, .74f, 2.801f);
        static readonly Color Paper = new Color(.96f, .97f, .94f);
        static readonly Color Quiet = new Color(.72f, .83f, .85f);
        static readonly Color Air = new Color(1f, .82f, .52f);
        static readonly Color Voice = new Color(.61f, .91f, .94f);

        [MenuItem("Museum/Right Exhibit/Build or Refresh Prefab")]
        public static void BuildPrefab()
        {
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(TableFolder + "LocalizationTable.asset");
            var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(TableFolder + "LanguageChannel.asset");
            var panel = new GameObject(PanelName);
            try
            {
                var root = panel.transform;
                Plate(root, "Backing", new Vector2(0, 0), new Vector2(1.24f, .65f), 0f, "Graphite");
                Plate(root, "Face", new Vector2(0, 0), new Vector2(1.216f, .626f), -.006f, "Navy");
                Plate(root, "Header rule", new Vector2(0, .152f), new Vector2(1.10f, .003f), -.010f, "Contour");
                Plate(root, "Row divider", new Vector2(0, -.034f), new Vector2(1.10f, .003f), -.010f, "Contour");
                Plate(root, "Footer rule", new Vector2(0, -.220f), new Vector2(1.10f, .003f), -.010f, "Contour");

                Label(root, table, channel, Prefix + "eyebrow", new Vector2(0, .279f), 1.10f, .46f, Quiet);
                Label(root, table, channel, "text.44fc6ddc6452", new Vector2(0, .208f), 1.10f, .60f, Paper);

                Route(root, table, channel, "air", "surface.air", "text.873b9302d666", .093f, .025f, Air, "WarmLight", false);
                Route(root, table, channel, "voice", "surface.voice", Prefix + "diver", -.092f, -.160f, Voice, "CoolLight", true);

                Label(root, table, channel, Prefix + "demo", new Vector2(.067f, -.266f), .88f, .46f, Paper);
                var wayfinding = new GameObject("Toward main exhibit").transform;
                wayfinding.SetParent(root, false);
                wayfinding.localPosition = new Vector3(-.44f, -.266f, 0);
                Arrow(wayfinding, .044f, -.044f, "WarmLight");

                var asset = PrefabUtility.SaveAsPrefabAsset(panel, PrefabPath);
                if (!asset) throw new InvalidOperationException("Could not save the right exhibit prefab.");
                Debug.Log("Saved " + PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panel);
            }
        }

        // Save the existing prefab instance; rebuilding the prefab is a separate authoring action.
        [MenuItem("Museum/Right Exhibit/Integrate into saved MuseumStart")]
        public static void IntegrateSavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before saving the exhibit.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene changes separately before integrating the exhibit.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MountPreview(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the right exhibit integration.");
            Debug.Log("Saved right exhibit integration in " + ScenePath);
        }

        // Reuses the saved instance, or temporarily mounts the prefab in older scene versions.
        [MenuItem("Museum/Right Exhibit/Preview in MuseumStart (Play Mode)")]
        static void Preview()
        {
            MountPreview(SceneManager.GetActiveScene());
        }

        [MenuItem("Museum/Right Exhibit/Preview in MuseumStart (Play Mode)", true)]
        static bool CanPreview() => EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath;

        internal static GameObject MountPreview(Scene scene)
        {
            var exhibits = scene.GetRootGameObjects().Single(o => o.name == "Exhibit Placeholders");
            var right = exhibits.transform.Find("Exhibit 03 - Placeholder");
            if (!right) throw new InvalidOperationException("Could not find the right exhibit.");
            var mounted = right.Find(PanelName);
            GameObject panel;
            if (mounted) panel = mounted.gameObject;
            else
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (!asset) throw new InvalidOperationException("Could not load the right exhibit prefab.");
                panel = (GameObject)PrefabUtility.InstantiatePrefab(asset, right);
                panel.name = PanelName;
                panel.transform.localPosition = MountPosition;
            }

            // Replace the two old front captions, keeping the existing pipes and top sign.
            foreach (var label in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<TMP_Text>(true)))
            {
                if (label.name == "Plinth title 3.1" || label.name == "Plinth caption 3.1")
                    label.gameObject.SetActive(false);
            }
            return panel;
        }

        static void Route(Transform root, LocalizationTable table, LanguageChannel channel, string routeKey,
            string sourceKey, string destinationKey, float titleY, float pathY, Color color, string material, bool twoWay)
        {
            Label(root, table, channel, Prefix + routeKey, new Vector2(0, titleY), 1.10f, .53f, color);
            Label(root, table, channel, Prefix + sourceKey, new Vector2(-.348f, pathY), .38f, .49f, Paper);
            Label(root, table, channel, destinationKey, new Vector2(.348f, pathY), .38f, .49f, Paper);

            var path = new GameObject(routeKey + " route").transform;
            path.SetParent(root, false);
            path.localPosition = new Vector3(0, pathY, 0);
            Stroke(path, "Shaft", new Vector2(-.102f, 0), new Vector2(.102f, 0), material);
            ArrowHead(path, .102f, .064f, material);
            if (twoWay) ArrowHead(path, -.102f, -.064f, material);
        }

        static void Arrow(Transform root, float from, float to, string material)
        {
            Stroke(root, "Shaft", new Vector2(from, 0), new Vector2(to, 0), material);
            ArrowHead(root, to, Mathf.Lerp(to, from, .42f), material);
        }

        static void ArrowHead(Transform root, float tip, float tail, string material)
        {
            bool pointsLeft = tip < tail;
            string direction = pointsLeft ? "Left" : "Right";
            Stroke(root, direction + " tip top", new Vector2(tail, .023f), new Vector2(tip, 0), material);
            Stroke(root, direction + " tip bottom", new Vector2(tail, -.023f), new Vector2(tip, 0), material);
        }

        static void Stroke(Transform root, string name, Vector2 from, Vector2 to, string material)
        {
            var delta = to - from;
            var stroke = Plate(root, name, (from + to) * .5f, new Vector2(delta.magnitude + .003f, .008f), -.012f, material);
            stroke.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        static Transform Plate(Transform root, string name, Vector2 center, Vector2 size, float depth, string material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = new Vector3(center.x, center.y, depth);
            part.transform.localScale = new Vector3(size.x, size.y, .006f);
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + material + ".mat");
            if (!renderer.sharedMaterial) throw new InvalidOperationException("Missing panel material: " + material);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
        }

        static void Label(Transform root, LocalizationTable table, LanguageChannel channel, string key,
            Vector2 center, float width, float maxSize, Color color)
        {
            if (!table.ContainsKey(key)) throw new InvalidOperationException("Missing panel translation: " + key);
            var label = new GameObject(key).AddComponent<TextMeshPro>();
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(center.x, center.y, -.016f);
            label.rectTransform.sizeDelta = new Vector2(width, .14f);
            label.font = table.FontAsset;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSize = maxSize;
            label.fontSizeMin = .42f;
            label.fontSizeMax = maxSize;
            label.overflowMode = TextOverflowModes.Overflow;
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            label.gameObject.AddComponent<LocalizedText>().Configure(table, channel, key);
            label.text = table.Get(key, MuseumLanguage.English);
        }
    }
}
