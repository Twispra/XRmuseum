#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Museum.Hotspots;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace Museum.Localization.EditorTools
{
    public static class HelmetHotspotAssetRepair
    {
        const string LocalizationRoot = "Assets/Museum/Features/Localization/";
        const string TablePath = LocalizationRoot + "Tables/LocalizationTable.asset";
        const string ChannelPath = LocalizationRoot + "Tables/LanguageChannel.asset";
        const string FontPath = LocalizationRoot + "Fonts/NotoSansSC-MuseumSubset SDF.asset";
        const string SourceFontPath = LocalizationRoot + "Fonts/NotoSansSC-MuseumSubset.ttf";
        const string CharacterPath = LocalizationRoot + "Fonts/characters-required.txt";
        const string PrefabPath = "Assets/Museum/Features/HelmetHotspots/Prefabs/HelmetHotspotTour.prefab";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string Prompt = "Select a highlighted point to inspect the helmet.";

        [Serializable] sealed class Report
        {
            public string startedUtc, finishedUtc, status, failure, unity, output;
            public string sceneBefore, sceneAfter, prefabBefore, prefabAfter, tableBefore, tableAfter;
            public string sourceFontSha256, fontBefore, fontAfter;
            public string missingStaticCharacters, missingSourceCharacters;
            public bool fontRebaked, identitiesPreserved, sceneUnchanged;
            public int translationRows, oldGlyphCount, newGlyphCount;
            public Vector3 panelPosition, frontMarkerPosition;
            public List<string> backups = new List<string>();
        }

        [MenuItem("Museum/Hotspots/Apply reviewed localization and layout repair")]
        public static void RunBatch()
        {
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion, status = "failed" };
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var folder = Argument("-hotspotRepairOutput") ?? Path.GetFullPath(Path.Combine(project, "../tmp/helmet-hotspots-repair/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N")));
            report.output = folder;
            bool created = false;
            int exitCode = 1;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before repairing saved assets.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    Require(!SceneManager.GetSceneAt(i).isDirty, "Save or close dirty scenes before this explicit repair.");
                Require(!Directory.Exists(folder), "Refusing to overwrite existing repair evidence: " + folder);
                Directory.CreateDirectory(folder);
                created = true;
                Repair(project, folder, report);
                report.status = "passed";
                exitCode = 0;
            }
            catch (Exception ex) { report.failure = ex.ToString(); Debug.LogError("HOTSPOT_ASSET_REPAIR_FAILED " + ex); }
            finally
            {
                report.finishedUtc = DateTime.UtcNow.ToString("o");
                if (created) File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report, true));
                Debug.Log("HOTSPOT_ASSET_REPAIR_" + report.status.ToUpperInvariant() + " " + folder);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
            if (exitCode != 0) throw new InvalidOperationException(report.failure);
        }

        static void Repair(string project, string folder, Report report)
        {
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(TablePath);
            var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(ChannelPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(table && channel && font && prefab, "A required saved localization or hotspot asset is missing.");
            Require(AssetDatabase.AssetPathToGUID(FontPath) == "73d312c39c8c91948afde3c8604a6c94", "Unexpected shared font identity.");
            Require(AssetDatabase.AssetPathToGUID(PrefabPath) == "a2f0d4237079f5a4194e3a4b3567ce82", "Unexpected hotspot prefab identity.");
            Require(!EditorUtility.IsDirty(table) && !EditorUtility.IsDirty(font) && !EditorUtility.IsDirty(prefab), "Required assets already have unsaved edits.");
            var copy = TrilingualMuseumSetup.ReadCopy();
            Require(copy.entries.Count(e => e.key.StartsWith("hotspots.", StringComparison.Ordinal)) == 9, "Expected all nine hotspot translations.");
            Require(copy.entries.Select(e => e.key).Distinct().Count() == copy.entries.Length, "Duplicate translation key.");
            Require(copy.entries.Select(e => e.english).Distinct().Count() == copy.entries.Length, "Duplicate English source.");
            foreach (var row in copy.entries)
                Require(!string.IsNullOrEmpty(row.chinese) && !string.IsNullOrEmpty(row.traditionalChinese) && !string.IsNullOrEmpty(row.english), "Incomplete translation: " + row.key);
            report.translationRows = copy.entries.Length;
            report.sceneBefore = Hash(project, ScenePath);
            report.prefabBefore = Hash(project, PrefabPath);
            report.tableBefore = Hash(project, TablePath);
            report.fontBefore = Hash(project, FontPath);
            report.sourceFontSha256 = Hash(project, SourceFontPath);
            report.oldGlyphCount = font.characterTable.Count;

            var required = new SortedSet<uint>(font.characterTable.Select(c => c.unicode));
            foreach (char c in File.ReadAllText(Path.Combine(project, CharacterPath)))
                if (!char.IsWhiteSpace(c)) required.Add(c);
            var missing = required.Where(c => !font.HasCharacter((int)c)).ToArray();
            report.missingStaticCharacters = string.Concat(missing.Select(c => char.ConvertFromUtf32((int)c)));
            Require(FontEngine.InitializeFontEngine() == FontEngineError.Success, "Font engine initialization failed.");
            Require(FontEngine.LoadFontFace(Path.Combine(project, SourceFontPath)) == FontEngineError.Success, "Could not load the saved source font.");
            var missingSource = required.Where(c => !FontEngine.TryGetGlyphIndex(c, out uint glyph) || glyph == 0).ToArray();
            report.missingSourceCharacters = string.Concat(missingSource.Select(c => char.ConvertFromUtf32((int)c)));
            Require(missingSource.Length == 0, "Source TTF needs these characters before repair: " + report.missingSourceCharacters);

            var paths = new[] { TablePath, PrefabPath, FontPath, SourceFontPath };
            var metadata = paths.ToDictionary(p => p, p => File.ReadAllBytes(Path.Combine(project, p + ".meta")));
            foreach (var path in paths.SelectMany(p => new[] { p, p + ".meta" }))
            {
                string backup = Path.Combine(folder, "before", path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(Path.Combine(project, path), backup);
                report.backups.Add(path);
            }
            if (missing.Length != 0)
            {
                TrilingualFontBaker.RunBatch();
                report.fontRebaked = true;
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            }
            Require(required.All(c => font.HasCharacter((int)c)), "Saved shared font is still missing a required character.");
            TrilingualMuseumSetup.ApplyTable(table, font);
            AssetDatabase.SaveAssetIfDirty(table);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Require(root.GetComponentsInChildren<HelmetHotspotTour>(true).Length == 1, "Unexpected hotspot tour structure.");
                var adapter = root.GetComponent<HelmetHotspotLocalization>();
                if (!adapter) adapter = root.AddComponent<HelmetHotspotLocalization>();
                adapter.Configure(table, channel);
                Bind(root, "Information", Prompt, table, channel, font, new Vector3(0, .042f, -.011f), new Vector2(.51f, .12f), 1.2f, TextAlignmentOptions.TopLeft);
                Bind(root, "Progress", "0 / 3 inspected", table, channel, font, new Vector3(0, -.049f, -.011f), new Vector2(.51f, .04f), .8f, TextAlignmentOptions.Center);
                Bind(root, "Complete", "Exploration complete", table, channel, font, new Vector3(0, -.09f, -.012f), new Vector2(.51f, .04f), .75f, TextAlignmentOptions.Center);
                var panel = root.transform.Find("Information Panel");
                Require(panel, "Hotspot information panel is missing.");
                panel.localPosition = new Vector3(0, .01f, -.80f);
                panel.localRotation = Quaternion.identity;
                panel.localScale = Vector3.one * .75f;
                var backing = panel.Find("Panel Backing");
                Require(backing, "Hotspot panel backing is missing.");
                backing.localScale = new Vector3(.56f, .23f, .015f);
                var front = root.GetComponentsInChildren<HelmetHotspot>(true).Single(h => new SerializedObject(h).FindProperty("identifier").stringValue == "front-window");
                front.transform.localPosition = new Vector3(-.11f, .30f, -.24f);
                report.panelPosition = panel.localPosition;
                report.frontMarkerPosition = front.transform.localPosition;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                Require(saved, "Could not save the repaired hotspot prefab.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            report.identitiesPreserved = metadata.All(p => p.Value.SequenceEqual(File.ReadAllBytes(Path.Combine(project, p.Key + ".meta"))));
            Require(report.identitiesPreserved, "Existing asset metadata changed.");
            report.sceneAfter = Hash(project, ScenePath);
            report.sceneUnchanged = report.sceneBefore == report.sceneAfter;
            Require(report.sceneUnchanged, "This scoped repair must not write the main scene.");
            report.prefabAfter = Hash(project, PrefabPath);
            report.tableAfter = Hash(project, TablePath);
            report.fontAfter = Hash(project, FontPath);
            report.newGlyphCount = font.characterTable.Count;
        }

        static void Bind(GameObject root, string name, string english, LocalizationTable table, LanguageChannel channel, TMP_FontAsset font, Vector3 position, Vector2 size, float maximum, TextAlignmentOptions alignment)
        {
            var text = root.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == name);
            Require(!text.GetComponent<LocalizedText>(), "Refusing to add a source binding beside a fixed key: " + name);
            var binding = text.GetComponent<LocalizedSourceText>();
            if (!binding) binding = text.gameObject.AddComponent<LocalizedSourceText>();
            binding.Configure(table, channel, english);
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.GetComponent<Renderer>().sharedMaterial = font.material;
            text.enableAutoSizing = true;
            text.fontSizeMin = .28f;
            text.fontSizeMax = maximum;
            text.fontSize = maximum;
            text.enableWordWrapping = true;
            text.alignment = alignment;
            text.rectTransform.sizeDelta = size;
            text.transform.localPosition = position;
            text.text = english;
        }

        static string Hash(string project, string path)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(project, path)))).Replace("-", "").ToLowerInvariant();
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static string Argument(string key) { var args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == key) return args[i + 1]; return null; }
    }
}
#endif
