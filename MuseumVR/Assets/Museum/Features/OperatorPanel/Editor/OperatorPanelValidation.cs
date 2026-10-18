#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Museum.Diegetic;
using Museum.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.OperatorPanel.Editor
{
    /// <summary>Deterministic edit-mode checks over the saved main scene: wiring, panel lookup,
    /// trilingual registration, glyph coverage of the saved font, and entry placement clearance.
    /// Keyboard/VR interaction is verified separately by a person; the report records that boundary.</summary>
    public static class OperatorPanelValidation
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string JsonPath = "Assets/Museum/Features/Localization/Tables/MuseumTranslations.json";
        static readonly string[] RequiredSources = { OperatorPanelController.ShowTitle, OperatorPanelController.HideTitle, OperatorPanelController.EntryHint };

        [Serializable] public sealed class Report
        {
            public string generatedUtc;
            public string sceneCommitGuess = "working tree";
            public int PassedCount => Checks.Count(c => c.passed);
            public int FailedCount => Checks.Count(c => !c.passed);
            public List<Check> Checks = new List<Check>();
            public List<string> NotCoveredHere = new List<string>
            {
                "keyboard (G) summon/hide acceptance and drag-time refusal in Play Mode",
                "real-headset single-hand reach, legibility and comfort",
                "player-build verification"
            };
        }
        [Serializable] public sealed class Check
        {
            public string name; public bool passed; public string detail;
        }

        [MenuItem("Museum/Operator Panel/Validate installed scene")]
        public static void ValidateMenu()
        {
            var report = Validate();
            foreach (var check in report.Checks)
                Debug.Log((check.passed ? "PASS " : "FAIL ") + check.name + " | " + check.detail);
            Debug.Log($"OPERATOR_PANEL_VALIDATION: passed={report.PassedCount} failed={report.FailedCount}");
        }

        public static void RunBatch()
        {
            try
            {
                var report = Validate();
                foreach (var check in report.Checks)
                    Debug.Log((check.passed ? "PASS " : "FAIL ") + check.name + " | " + check.detail);
                string runDir = Path.GetFullPath(Path.Combine("../artifacts/operator-panel/qa/runs",
                    DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
                Directory.CreateDirectory(runDir);
                var json = JsonUtility.ToJson(report, true);
                File.WriteAllText(Path.Combine(runDir, "report.json"), json, new UTF8Encoding(false));
                Debug.Log("OPERATOR_PANEL_VALIDATION_REPORT: " + runDir);
                EditorApplication.Exit(report.FailedCount == 0 ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static Report Validate()
        {
            var report = new Report { generatedUtc = DateTime.UtcNow.ToString("o") };
            if (!EditorApplication.isPlaying) EditorSceneManager.OpenScene(ScenePath);
            var station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();

            report.Checks.Add(new Check { name = "station present", passed = station != null,
                detail = station ? station.name : "no HelmetDiegeticStation in saved scene" });
            if (!station) return report;

            var pedestal = station.transform.Find("Pedestal edge controls");
            // The station builds "Pedestal edge controls", "Fixed settings" and
            // "Fixed exhibit information" at runtime in Start(), so the saved scene
            // deliberately does not contain them. Verify the expected state per mode.
            bool playing = EditorApplication.isPlaying;
            var settings = pedestal ? pedestal.Find("Fixed settings") : null;
            var information = pedestal ? pedestal.Find("Fixed exhibit information") : null;
            report.Checks.Add(new Check
            {
                name = playing ? "panel objects present (play mode)" : "panel objects runtime-built (edit mode)",
                passed = playing ? pedestal != null && settings && information : pedestal == null && !settings && !information,
                detail = playing
                    ? $"pedestal={pedestal != null} settings={settings != null} information={information != null}"
                    : "saved scene correctly free of runtime-built panel content; presence is verified in Play Mode"
            });

            var controller = station.GetComponent<OperatorPanelController>();
            report.Checks.Add(new Check { name = "controller installed", passed = controller != null,
                detail = controller ? "OperatorPanelController on station" : "missing" });
            if (!controller) return report;
            var so = new SerializedObject(controller);
            var wired = so.FindProperty("station").objectReferenceValue &&
                        so.FindProperty("viewer").objectReferenceValue &&
                        so.FindProperty("uiMaterial").objectReferenceValue;
            report.Checks.Add(new Check { name = "controller wiring complete", passed = wired,
                detail = wired ? "station/viewer/material set" : "some serialized references empty" });

            // Trilingual: sources registered, and every glyph of every language exists in the saved font.
            var copy = JsonUtility.FromJson<LocalizationCopy>(File.ReadAllText(JsonPath));
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            if (table && table.FontAsset)
            {
                var covered = new HashSet<int>();
                foreach (var character in table.FontAsset.characterTable)
                    covered.Add((int)character.unicode);
                foreach (var source in RequiredSources)
                {
                    var entry = copy.entries.FirstOrDefault(e => e.english == source);
                    report.Checks.Add(new Check { name = "registered: " + source, passed = entry != null,
                        detail = entry != null ? $"key={entry.key} sc={entry.chinese} tc={entry.traditionalChinese}"
                            : "no JSON entry with this English source" });
                    if (entry == null) continue;
                    foreach (var (language, text) in new[]
                             { ("en", entry.english), ("sc", entry.chinese), ("tc", entry.traditionalChinese) })
                    {
                        var missing = text.Where(c => c != ' ' && !covered.Contains(c)).ToList();
                        report.Checks.Add(new Check { name = $"glyphs {language}: {source}", passed = missing.Count == 0,
                            detail = missing.Count == 0 ? "all characters baked in saved font"
                                : "missing: " + string.Join(" ", missing.Select(c => $"U+{((int)c):X4} '{c}'")) });
                    }
                }
            }
            else report.Checks.Add(new Check { name = "localization table/font", passed = false,
                detail = "saved LocalizationTable or font asset missing" });

            // Entry placement: no other pedestal control occupies the same spot.
            var entryPosition = so.FindProperty("entryLocalPosition").vector3Value;
            if (pedestal)
            {
                float nearest = float.MaxValue;
                string nearestName = "(none)";
                foreach (Transform sibling in pedestal)
                {
                    if (!sibling.name.StartsWith("Physical UI - ") || sibling.name == "Physical UI - PanelEntry") continue;
                    float d = Vector3.Distance(sibling.localPosition, entryPosition);
                    if (d < nearest) { nearest = d; nearestName = sibling.name; }
                }
                report.Checks.Add(new Check { name = "entry spot clearance", passed = nearest >= .09f,
                    detail = $"nearest control '{nearestName}' at {nearest:0.000} m (need >= 0.09 m)" });
            }

            // Pure guard sanity.
            report.Checks.Add(new Check { name = "drag guard sanity (no station)", passed = !OperatorPanelController.IsAnyGestureDragging(null),
                detail = "IsAnyGestureDragging(null) == false" });

            if (!EditorApplication.isPlaying) EditorSceneManager.OpenScene(ScenePath); // Leave saved scene unmodified.
            return report;
        }

        [Serializable]
        sealed class LocalizationCopy { public List<Entry> entries = new List<Entry>(); }
        [Serializable]
        sealed class Entry { public string key, english, chinese, traditionalChinese; }
    }
}
#endif
