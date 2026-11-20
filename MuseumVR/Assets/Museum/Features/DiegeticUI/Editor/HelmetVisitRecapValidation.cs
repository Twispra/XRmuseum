#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Museum.Exploration;
using Museum.Hotspots;
using Museum.Localization;
using TMPro;
using UnityEngine;
using Command = Museum.Diegetic.HelmetDiegeticStation.Command;

namespace Museum.Diegetic.Editor
{
    public static partial class HelmetDiegeticValidation
    {
        public static void RunVisitRecapBatch() => Begin(Suite.VisitRecap);

        static IEnumerator VisitRecapFixtures()
        {
            var tour = inspection.GetComponentInChildren<HelmetHotspotTour>(true);
            var hotspots = tour.GetComponentsInChildren<HelmetHotspot>(true);
            var lower = hotspots.Single(part => part.Identifier == "breastplate");
            var shell = hotspots.Single(part => part.Identifier == "bonnet");
            var panel = station.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Visit recap");
            var labels = panel.GetComponentsInChildren<TMP_Text>(true);
            var parts = labels.Where(label => label.name.StartsWith("Recap part ")).ToArray();
            var progress = labels.Single(label => label.name == "Recap inspected count");
            var connections = labels.Single(label => label.name == "Recap connections");
            const string none = "Air: not seen yet\nVoice: not seen yet";
            const string air = "Air: seen - air arrives from the surface.\nVoice: not seen yet";
            const string both = "Air: seen - air arrives from the surface.\nVoice: seen - messages travel both ways.";

            CheckResult(!panel.gameObject.activeInHierarchy && binding.AppliedEraIndex == 1,
                "The recap begins closed at the default exhibit", panel.name);
            yield return RayClick(Command.FinishVisit);
            CheckRecap(panel, progress, connections, parts, 0, none);
            yield return RayClick(Command.ContinueVisit);
            CheckResult(!panel.gameObject.activeInHierarchy && !station.IsVisitHandoffOpen && tour.VisitedCount == 0,
                "Continuing an empty visit closes the recap", tour.ProgressEnglish);

            string firstLower = lower.Title + "\n" + lower.Description;
            lower.Activate();
            station.Execute(Command.Air);
            station.Execute(Command.NextEra);
            yield return Wait(.4f);
            CheckResult(binding.AppliedEraIndex == 2 && lower.Title == "Neck seal",
                "The same saved lower hotspot now refers to a different part", lower.Title);
            lower.Activate();
            shell.Activate();
            string firstShell = shell.Title + "\n" + shell.Description;
            yield return RayClick(Command.FinishVisit);
            CheckResult(tour.VisitedCount == 2 &&
                tour.TryGetFirstObservation(lower.Identifier, out string savedLower) && savedLower == firstLower,
                "Revisiting a shared hotspot in another era keeps the first observation", firstLower);
            foreach (var language in new[] { MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese, MuseumLanguage.English })
            {
                MuseumText.Channel.SetLanguage(language);
                yield return Wait(.1f);
                CheckRecap(panel, progress, connections, parts, 2, air, firstLower, firstShell);
                foreach (var label in labels.Where(l => l.isActiveAndEnabled))
                {
                    label.ForceMeshUpdate(true, true);
                    CheckResult(!label.isTextOverflowing && label.text.All(c => char.IsWhiteSpace(c) || label.font.HasCharacter(c, false, false)),
                        "Recap label fits and has baked glyphs " + language + "/" + label.name, label.text, "monocular-layout");
                }
                CaptureVisitRecap(panel, language.ToString());
            }

            yield return RayClick(Command.ContinueVisit);
            CheckResult(!panel.gameObject.activeInHierarchy && tour.VisitedCount == 2 && binding.AppliedEraIndex == 2,
                "Continuing preserves the current era and observations", tour.ProgressEnglish);
            station.Execute(Command.PreviousEra);
            yield return Wait(.4f);
            station.Execute(Command.Voice);
            var window = hotspots.Single(part => part.Identifier == "front-window");
            window.Activate();
            string firstWindow = window.Title + "\n" + window.Description;
            yield return RayClick(Command.FinishVisit);
            CheckRecap(panel, progress, connections, parts, 3, both, firstLower, firstShell, firstWindow);
            yield return RayClick(Command.StartNextVisit);
            CheckResult(!panel.gameObject.activeInHierarchy && !station.IsVisitHandoffOpen && tour.VisitedCount == 0 &&
                !tour.TryGetFirstObservation(lower.Identifier, out _),
                "Next visit clears discoveries and closes the recap", tour.ProgressEnglish);
            yield return RayClick(Command.FinishVisit);
            CheckRecap(panel, progress, connections, parts, 0, none);
            yield return RayClick(Command.ContinueVisit);
        }

        static void CheckRecap(Transform panel, TMP_Text progress, TMP_Text connections, TMP_Text[] parts,
            int observed, string connectionSource, params string[] observations)
        {
            string language = MuseumText.Channel.Current.ToString();
            CheckResult(panel.gameObject.activeInHierarchy && station.IsVisitHandoffOpen,
                "Finish visit opens recap with choices", language, "production-input");
            CheckResult(progress.text == MuseumText.Translate(observed + " / 3 inspected") &&
                connections.text == MuseumText.Translate(connectionSource),
                "Recap reflects current observation count and connections", progress.text + " | " + connections.text, "localization");
            var expected = observations.Select(source => MuseumText.Translate(source).Split('\n')[0]).OrderBy(title => title).ToArray();
            var visible = parts.Where(part => part.isActiveAndEnabled).Select(part => part.text).OrderBy(title => title).ToArray();
            CheckResult(expected.SequenceEqual(visible), "Recap shows the names recorded at first inspection " + language,
                string.Join(" | ", visible), "localization");
        }

        static void CaptureVisitRecap(Transform panel, string language)
        {
            Vector3 center = panel.TransformPoint(new Vector3(-.06f, -.08f, 0));
            Vector3 position = center - panel.forward * 1.03f;
            Quaternion rotation = Quaternion.LookRotation(panel.forward, panel.up);
            Directory.CreateDirectory(Output);
            string file = "visit-recap-" + language + ".png";
            HelmetDiegeticPreview.CaptureView(Path.Combine(Output, file), position, rotation, 52f);
            report.diagnosticCaptures.Add(file);
        }
    }
}
#endif
