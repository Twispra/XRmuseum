#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Museum.Exploration;
using Museum.Inspection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.Localization.EditorTools
{
    /// <summary>Additional main-scene checks run by LocalizationIntegrationValidation. No asset writes.</summary>
    public static class TrilingualMuseumValidation
    {
        static readonly MuseumLanguage[] Languages = { MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese, MuseumLanguage.English };
        static Action<bool, string, string, string> assert;
        static LocalizationTable table;
        static LanguageChannel channel;
        static string output;
        static void Check(bool ok, string name, string detail = "") => assert(ok, "trilingual main scene", name, detail);
        static TMP_Text[] Labels() => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToArray();

        public static IEnumerator Run(LocalizationTable copy, LanguageChannel selection, HelmetExplorationPanel panel,
            HelmetExplorationController exploration, HelmetConnectionDemonstration demo,
            Action<bool, string, string, string> require)
        {
            table = copy; channel = selection; assert = require;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../tmp/trilingual-qa", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            foreach (var entry in table.Entries)
                foreach (var language in Languages)
                {
                    string value = entry.For(language);
                    Check(!string.IsNullOrEmpty(value), "complete copy / " + entry.Key + "/" + language);
                    var missing = Regex.Replace(value, "<[^>]*>", "").Where(c => !char.IsWhiteSpace(c))
                        .Distinct().Where(c => !table.FontAsset.HasCharacter(c, false, false)).ToArray();
                    Check(missing.Length == 0, "stored glyphs / " + entry.Key + "/" + language, new string(missing));
                }
            foreach (var language in Languages)
            {
                channel.SetLanguage(language); yield return Wait(.15f);
                channel.Toggle(); Check(channel.Current != language, "cycle leaves " + language);
                channel.SetLanguage(language);
                exploration.RestartVisit(); panel.ShowInspection(); yield return Wait(.15f);
                Audit("inspection " + language); Capture(panel.transform, "inspection-" + language);
                panel.ToggleInspectionTools(); yield return Wait(.1f); Audit("tools " + language);
                var inspection = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
                inspection.SetCutaway(true); yield return Wait(.15f); Audit("section " + language);
                inspection.SetCutaway(false); panel.ShowHelp(); yield return Wait(.15f);
                Audit("help " + language); Capture(panel.transform, "help-" + language);
                panel.ToggleSound(); yield return Wait(.1f); Audit("help sound toggle " + language);
                panel.ToggleSound(); yield return Wait(.1f); Audit("help sound restored " + language);
                panel.CloseReading(); panel.ShowExploration(); yield return Wait(.1f); Audit("welcome " + language);
                exploration.RequestHint(); yield return Wait(.1f); Audit("initial hint " + language);
                exploration.ShowReview(); yield return Wait(.1f); Audit("empty review " + language);
                exploration.SelectClue(HelmetExplorationController.Clue.Air); exploration.Stop();
                yield return Wait(.1f); Audit("air stopped " + language);
                exploration.RequestHint(); yield return Wait(.1f); Audit("second hint " + language);
                exploration.ShowReview(); yield return Wait(.1f); Audit("partial review " + language);
                exploration.SelectClue(HelmetExplorationController.Clue.Voice); exploration.Stop();
                yield return Wait(.1f); Audit("voice stopped " + language);
                exploration.RequestHint(); yield return Wait(.1f); Audit("complete hint " + language);
                exploration.ShowReview(); yield return Wait(.1f); Audit("full review " + language);
                // Exercise the other partial-review branch separately.
                exploration.RestartVisit(); exploration.SelectClue(HelmetExplorationController.Clue.Voice); exploration.Stop();
                exploration.ShowReview(); yield return Wait(.1f); Audit("voice-only review " + language);
                inspection.ResetInspection(); yield return Wait(.1f);
                Check(channel.Current == language, "reset preserves language " + language);
                panel.gameObject.SetActive(false); channel.SetLanguage(MuseumLanguage.English);
                channel.SetLanguage(language); panel.gameObject.SetActive(true); yield return Wait(.15f);
                Audit("disable-enable " + language);
            }
            panel.ShowExploration(); exploration.RestartVisit();
            foreach (var clue in new[] { HelmetExplorationController.Clue.Air, HelmetExplorationController.Clue.Voice })
            {
                exploration.SelectClue(clue);
                var phases = new HashSet<int>();
                float end = Time.realtimeSinceStartup + HelmetConnectionDemonstration.Duration + .3f;
                int languageIndex = 0, switches = 0;
                while (Time.realtimeSinceStartup < end)
                {
                    float before = demo.NormalizedProgress;
                    var next = Languages[languageIndex++ % Languages.Length];
                    channel.SetLanguage(next); yield return Wait(.12f);
                    Check(exploration.SelectedClue == clue && demo.IsVisible && demo.NormalizedProgress + .001f >= before,
                        "switch does not reset playback " + clue + "/" + switches);
                    Audit("animation " + clue + "/" + demo.PhaseIndex + "/" + next);
                    if (phases.Add(demo.PhaseIndex)) Capture(demo.transform, "demo-" + clue + "-phase" + demo.PhaseIndex);
                    switches++;
                }
                Check(phases.Count >= 3 && switches >= 12, "all animation phases and repeated switches " + clue,
                    "phases=" + string.Join(",", phases) + "; switches=" + switches);
                Check(!demo.IsPlaying && demo.IsVisible, "static diagram after completion " + clue);
                foreach (var language in Languages)
                {
                    channel.SetLanguage(language); panel.ShowDetails(); yield return Wait(.12f); Audit("details " + clue + "/" + language);
                    panel.CloseReading();
                }
                exploration.Stop(); yield return Wait(.1f);
                Check(!demo.IsVisible, "stop still hides diagram " + clue);
            }
            exploration.RestartVisit(); panel.ShowInspection(); channel.SetLanguage(MuseumLanguage.English);
            yield return Wait(.15f); Audit("restored English");
            Debug.Log("TRILINGUAL_QA_CAPTURES " + output);
        }

        static void Audit(string state)
        {
            int visible = 0;
            foreach (var label in Labels())
            {
                var source = label.GetComponent<LocalizedSourceText>(); var fixedKey = label.GetComponent<LocalizedText>();
                bool native = label.name.Contains("Language Button") && label.name.EndsWith(" Label", StringComparison.Ordinal);
                Check(source || fixedKey || native, "label registered / " + state + "/" + label.name);
                if (source) Check(string.IsNullOrEmpty(source.SourceEnglish) || source.SourceEnglish.All(char.IsDigit) || table.ContainsEnglish(source.SourceEnglish),
                    "dynamic source in table / " + state + "/" + label.name, source.SourceEnglish);
                if (!label.gameObject.activeInHierarchy || !label.GetComponent<Renderer>().enabled) continue;
                visible++; label.ForceMeshUpdate(true, true);
                string expected = source ? table.TranslateEnglish(source.SourceEnglish, channel.Current) : fixedKey ? table.Get(fixedKey.Key, channel.Current) : label.text;
                Check(label.text == expected && label.font == table.FontAsset, "translated label / " + state + "/" + label.name, label.text);
                Check(!label.isTextOverflowing, "layout containment / " + state + "/" + label.name,
                    label.text + "; fontSize=" + label.fontSize + "; rect=" + label.rectTransform.sizeDelta);
                var missing = Regex.Replace(label.text, "<[^>]*>", "").Where(c => !char.IsWhiteSpace(c)).Distinct()
                    .Where(c => !label.font.HasCharacter(c, false, false)).ToArray();
                Check(missing.Length == 0, "visible glyphs / " + state + "/" + label.name, new string(missing));
            }
            Check(visible > 0, "visible labels checked / " + state, visible.ToString());
        }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        static void Capture(Transform focus, string name)
        {
            var labels = focus.GetComponentsInChildren<TMP_Text>(true).Where(t => t.gameObject.activeInHierarchy && t.GetComponent<Renderer>().enabled).ToArray();
            if (labels.Length == 0) return;
            var bounds = labels[0].GetComponent<Renderer>().bounds;
            foreach (var text in labels.Skip(1)) bounds.Encapsulate(text.GetComponent<Renderer>().bounds);
            var go = new GameObject("Temporary trilingual QA camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.fieldOfView = 48; camera.aspect = 4f / 3f;
            Vector3 pos = bounds.center - labels[0].transform.forward * Mathf.Max(.6f, (bounds.extents.magnitude + .05f) / Mathf.Tan(24 * Mathf.Deg2Rad));
            camera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(bounds.center - pos, labels[0].transform.up));
            var rt = RenderTexture.GetTemporary(1200, 900, 24); var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                pixels = new Texture2D(1200, 900, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(go); }
        }
    }

    /// <summary>Run the original inspection assertions with a settled batch exit, as the other validators do.</summary>
    [InitializeOnLoad]
    public static class TrilingualInspectionRegression
    {
        const string Prefix = "Museum.TrilingualInspectionRegression.";
        static TrilingualInspectionRegression()
        {
            EditorApplication.playModeStateChanged += OnMode;
            EditorApplication.update += Tick;
        }
        public static void RunBatch()
        {
            SessionState.SetBool(Prefix + "Active", true);
            SessionState.SetBool(Prefix + "Played", false);
            SessionState.SetBool(Prefix + "Ready", false);
            SessionState.SetString(Prefix + "Started", EditorApplication.timeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            try { Museum.Inspection.Editor.HelmetInspectionValidation.RunMainInEditor(); }
            catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Prefix + "Active", false); EditorApplication.Exit(1); }
        }
        static void OnMode(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode) SessionState.SetBool(Prefix + "Played", true);
            if (mode == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Prefix + "Played", false))
            {
                SessionState.SetBool(Prefix + "Ready", true);
                SessionState.SetString(Prefix + "ExitAt", (EditorApplication.timeSinceStartup + 20).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            double started = double.Parse(SessionState.GetString(Prefix + "Started", "0"), System.Globalization.CultureInfo.InvariantCulture);
            if (EditorApplication.timeSinceStartup - started > 420)
            { SessionState.SetBool(Prefix + "Active", false); Debug.LogError("Inspection regression exceeded 420 seconds."); EditorApplication.Exit(1); return; }
            if (!SessionState.GetBool(Prefix + "Ready", false)) return;
            double exitAt = double.Parse(SessionState.GetString(Prefix + "ExitAt", "0"), System.Globalization.CultureInfo.InvariantCulture);
            if (EditorApplication.timeSinceStartup < exitAt) return;
            SessionState.SetBool(Prefix + "Active", false);
            int code = SessionState.GetInt("Museum.HelmetInspectionQA.ExitCode", -1);
            Debug.Log("TRILINGUAL_INSPECTION_SETTLED_EXIT " + code);
            EditorApplication.Exit(code == 0 ? 0 : 1);
        }
    }
}
#endif
