using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.Localization.EditorTools
{
    /// <summary>Apply localization to the saved main scene without rebuilding assets or changing its layout.</summary>
    public static class MuseumIntegration
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string Root = "Assets/Museum/Features/Localization";
        const string RootName = "Language Selection";
        static readonly Vector3 Placement = new Vector3(2.6f, 0f, 6.2f);
        sealed class Sign
        {
            public string name, key, english;
            public bool active;
            public Sign(string name, string key, string english, bool active)
            { this.name = name; this.key = key; this.english = english; this.active = active; }
        }
        // These are the exact texts and visibility decisions in main after PR #9.
        // The two hidden labels are prepared for future activation, never enabled here.
        static readonly Sign[] Signs = {
            new Sign("Gallery masthead", "museum.sign.masthead", "Beneath the surface", true),
            new Sign("Gallery subtitle", "museum.sign.gallery", "THE DIVING GALLERY", false),
            new Sign("Arrival brand", "museum.sign.arrival_brand", "The diving gallery", true),
            new Sign("Arrival introduction", "museum.sign.intro", "One object. Many ways to explore.", false)
        };
        static readonly Dictionary<string, string> Legacy = new Dictionary<string, string> {
            { "Museum Title", "museum.sign.title" },
            { "Museum Subtitle", "museum.sign.subtitle" },
            { "Welcome", "museum.sign.welcome" }
        };

        [MenuItem("Museum/Localization/Integrate Into MuseumStart", priority = 30)]
        public static void IntegrateIntoMuseumStart() => Apply();

        // Use only for the explicitly authorized scene integration, never as a prerequisite to validation.
        public static void IntegrateBatch()
        {
            try { Apply(); if (Application.isBatchMode) EditorApplication.Exit(0); }
            catch (Exception exception)
            { Debug.LogException(exception); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }

        static void Apply()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before integration.");
            RefuseDirtyScenes();
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(Root + "/Tables/LocalizationTable.asset");
            var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(Root + "/Tables/LanguageChannel.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/LanguageShowcase.prefab");
            Require(table && channel && prefab && table.FontAsset, "Missing saved localization assets; prepare them explicitly before integration.");
            Require(channel.Initial == MuseumLanguage.English, "Saved initial language must remain English.");
            foreach (var sign in Signs)
            {
                Require(table.ContainsKey(sign.key), "Missing translation key: " + sign.key);
                Require(table.Get(sign.key, MuseumLanguage.English) == sign.english, "English must match main exactly: " + sign.key);
                string chinese = table.Get(sign.key, MuseumLanguage.Chinese);
                Require(!string.IsNullOrWhiteSpace(chinese), "Missing Chinese: " + sign.key);
                foreach (char character in sign.english + chinese)
                    if (!char.IsWhiteSpace(character))
                        Require(table.FontAsset.HasCharacter(character, false, false),
                            "Saved font lacks U+" + ((int)character).ToString("X4") + " for " + sign.key + "; bake it before integration.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var targets = new Dictionary<Sign, TMP_Text>();
            foreach (var sign in Signs)
            {
                var matches = objects.Where(item => item.name == sign.name).ToArray();
                Require(matches.Length == 1, "Expected one target: " + sign.name + "; found " + matches.Length);
                var target = matches[0].GetComponent<TMP_Text>();
                Require(target && target.GetComponent<Renderer>(), "Target requires TMP and a Renderer: " + sign.name);
                Require(target.gameObject.activeSelf == sign.active, "Unexpected main visibility: " + sign.name);
                Require(target.GetComponent<Renderer>().enabled, "Target Renderer is disabled: " + sign.name);
                var existing = target.GetComponent<LocalizedText>();
                Require(!existing || (existing.Key == sign.key && existing.enabled), "Conflicting binding on " + sign.name);
                if (!existing) Require(target.text == sign.english, "Target English differs from main: " + sign.name);
                targets.Add(sign, target);
            }
            var bindings = objects.Select(item => item.GetComponent<LocalizedText>()).Where(item => item).ToArray();
            foreach (var binding in bindings)
            {
                if (Legacy.Values.Contains(binding.Key))
                    Require(Legacy.TryGetValue(binding.name, out string key) && key == binding.Key,
                        "Legacy key occurs outside its owned target; inspect manually: " + binding.name);
                var sign = Signs.FirstOrDefault(item => item.key == binding.Key);
                if (sign != null) Require(binding.gameObject == targets[sign].gameObject, "Duplicate sign binding: " + binding.Key);
            }
            var roots = scene.GetRootGameObjects().Where(item => item.name == RootName).ToArray();
            Require(roots.Length <= 1, "Multiple Language Selection roots; inspect manually.");
            if (roots.Length == 1)
                Require(roots[0].GetComponentsInChildren<LanguageButton>(true).Length >= 2, "Existing language panel is incomplete.");

            string backupFolder = Path.Combine(Application.temporaryCachePath, "MuseumLocalizationIntegration");
            Directory.CreateDirectory(backupFolder);
            string backup = Path.Combine(backupFolder, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".unity");
            File.Copy(ScenePath, backup, false);

            // Keep the existing panel's transform and instance overrides intact.
            if (roots.Length == 0)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = RootName;
                instance.transform.SetPositionAndRotation(Placement, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
            }
            foreach (var binding in bindings)
                if (Legacy.TryGetValue(binding.name, out string key) && key == binding.Key)
                    UnityEngine.Object.DestroyImmediate(binding);

            channel.SetLanguage(channel.Initial); // Nonserialized state only; saved default remains untouched.
            foreach (var pair in targets)
            {
                var target = pair.Value;
                var renderer = target.GetComponent<Renderer>();
                bool active = target.gameObject.activeSelf, enabled = renderer.enabled;
                Vector3 position = target.transform.localPosition, scale = target.transform.localScale;
                Quaternion rotation = target.transform.localRotation;
                var localized = target.GetComponent<LocalizedText>() ?? target.gameObject.AddComponent<LocalizedText>();
                // Explicitly use the saved bilingual font even if a previous binding opted out.
                var serialized = new SerializedObject(localized);
                serialized.FindProperty("applyTableFont").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                localized.Configure(table, channel, pair.Key.key);
                Require(target.text == pair.Key.english && target.font == table.FontAsset,
                    "Binding did not retain English and the saved bilingual font: " + target.name);
                Require(target.gameObject.activeSelf == active && renderer.enabled == enabled &&
                    target.transform.localPosition == position && target.transform.localScale == scale && target.transform.localRotation == rotation,
                    "Integration changed visibility or transform: " + target.name);
                foreach (UnityEngine.Object item in new UnityEngine.Object[] { localized, target, renderer })
                {
                    EditorUtility.SetDirty(item);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save integrated main scene.");
            Debug.Log("LOCALIZATION_INTEGRATION_SAVED: four bindings; hidden labels remain hidden; backup=" + backup);
        }

        [MenuItem("Museum/Localization/Remove From MuseumStart", priority = 31)]
        public static void RemoveFromMuseumStart()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before removal.");
            RefuseDirtyScenes();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == RootName) { UnityEngine.Object.DestroyImmediate(root); continue; }
                foreach (var binding in root.GetComponentsInChildren<LocalizedText>(true))
                {
                    bool owned = Signs.Any(sign => binding.name == sign.name && binding.Key == sign.key) ||
                        (Legacy.TryGetValue(binding.name, out string key) && binding.Key == key);
                    if (owned) UnityEngine.Object.DestroyImmediate(binding);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save removal.");
        }

        static void RefuseDirtyScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
                Require(!SceneManager.GetSceneAt(index).isDirty, "Save or close modified scenes explicitly before running this tool.");
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
