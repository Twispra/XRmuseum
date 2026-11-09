#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Museum.Exploration;
using Museum.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.Diegetic.Editor
{
    public static partial class HelmetDiegeticValidation
    {
        [MenuItem("Museum/Diegetic UI/Validate narration in saved main scene")]
        public static void RunNarrationInEditor() => Begin(true);
        public static void RunNarrationBatch() => Begin(true);

        static IEnumerator NarrationFixtures()
        {
            report.limitations += " Narration audio assertions inspect Unity AudioSource and AudioClip state. Actual listening, headset intelligibility and binocular layout remain unverified.";
            var control = station.Controls[HelmetDiegeticStation.Command.Narration];
            var caption = station.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Guide subtitle");
            var source = NarrationAudioSource();

            var imported = new[] { "intro", "air", "voice" }.SelectMany(cue => new[] { "en", "zh" }.Select(language =>
                Resources.Load<AudioClip>("HelmetNarration/" + cue + "-" + language))).ToArray();
            CheckResult(imported.All(clip => clip && clip.length > 1f && clip.samples > 0 && clip.channels > 0) &&
                imported.Distinct().Count() == 6, "Six distinct narration recordings import in the saved scene",
                string.Join(", ", imported.Select(clip => clip ? clip.name + "=" + clip.length.ToString("F2") + "s" : "missing")), "saved-assets");

            if (station.SettingsVisible) station.Execute(HelmetDiegeticStation.Command.Settings);
            station.Execute(HelmetDiegeticStation.Command.Reset);
            yield return Wait(.5f);
            for (int attempt = 0; eras.CurrentIndex != 1 && attempt < eras.AvailableEraCount; attempt++)
            {
                station.Execute(HelmetDiegeticStation.Command.NextEra);
                yield return Wait(.2f);
            }
            inspection.SoundEnabled = true;
            inspection.ReduceMotion = false;
            MuseumText.Channel.SetLanguage(MuseumLanguage.English);
            ParkHands(); QueueHead(); yield return Wait(.55f);
            CheckResult(binding.AppliedEraIndex == 1 && !demo.IsVisible && !source.isPlaying && !NarrationCaptionVisible(caption) &&
                NarrationLabelSource(control.Caption) == "Play narration", "Saved narrator starts idle with the play label",
                "Era=" + binding.AppliedEraIndex + "; Playing=" + source.isPlaying + "; Label=" + control.Caption.text);
            yield return NarrationConfirmFromIdle(control, source, caption);
            CheckNarrationCaption(caption, source, "orientation", "intro-en", MuseumLanguage.English, true);
            CheckResult(NarrationLabelSource(control.Caption) == "Stop narration", "Active narration exposes a stop label", control.Caption.text);

            // A head turn during a live clip must neither retrigger nor reset it.
            int confirmed = control.ConfirmedCount;
            int samples = source.timeSamples;
            QueueHead(new Vector3(0, 1.65f, 0), Quaternion.Euler(0, 24, 0)); yield return Wait(.3f);
            CheckResult(source.isPlaying && source.timeSamples > samples && control.ConfirmedCount == confirmed,
                "Turning away leaves narration playing without restarting it",
                "Samples=" + samples + "->" + source.timeSamples + "; Confirmations=" + control.ConfirmedCount, "production-input");
            QueueHead(); yield return Wait(.15f);
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            CheckNarrationStopped(caption, source, "Stop click");
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            CheckResult(source.isPlaying && source.timeSamples < source.clip.frequency * 1.5f,
                "Replay click restarts orientation from the beginning", "Samples=" + source.timeSamples, "production-input");
            if (source.isPlaying) yield return Wait(source.clip.length + .3f);
            CheckNarrationStopped(caption, source, "Natural end");
            CheckResult(NarrationLabelSource(control.Caption) == "Play narration",
                "Natural end restores the play label", control.Caption.text);

            foreach (var language in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
            {
                MuseumText.Channel.SetLanguage(language);
                string suffix = language == MuseumLanguage.English ? "-en" : "-zh";
                foreach (var cue in new[] { "orientation", "air", "voice" })
                {
                    if (cue != "orientation")
                    {
                        station.Execute(cue == "air" ? HelmetDiegeticStation.Command.Air : HelmetDiegeticStation.Command.Voice);
                        yield return Wait(.2f);
                    }
                    yield return RayClick(HelmetDiegeticStation.Command.Narration);
                    CheckNarrationCaption(caption, source, cue, (cue == "orientation" ? "intro" : cue) + suffix, language, true);
                    CaptureNarrationSubtitle(caption, cue, language);
                    yield return RayClick(HelmetDiegeticStation.Command.Narration);
                    CheckNarrationStopped(caption, source, "Stop " + cue + "/" + language);
                    if (cue != "orientation") { station.Execute(HelmetDiegeticStation.Command.CloseDemo); yield return Wait(.15f); }
                }
            }

            MuseumText.Channel.SetLanguage(MuseumLanguage.English);
            station.Execute(HelmetDiegeticStation.Command.Voice); yield return Wait(.25f);
            var equipmentSource = demo.RefinedEquipment.GetComponent<AudioSource>();
            CheckResult(equipmentSource && equipmentSource.isPlaying, "Voice demonstration plays its own audio before narration",
                equipmentSource && equipmentSource.clip ? equipmentSource.clip.name : "No playing equipment clip");
            float beforeProgress = demo.NormalizedProgress;
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            yield return Wait(1.1f);
            CheckResult(source.isPlaying && !equipmentSource.isPlaying && demo.IsVisible && demo.IsPlaying &&
                demo.NormalizedProgress > beforeProgress && demo.PhaseIndex >= 1,
                "Narration and equipment voice do not overlap while the demonstration advances through phases",
                "Narration=" + source.isPlaying + "; Equipment=" + equipmentSource.isPlaying +
                "; Phase=" + demo.PhaseIndex + "; Demo progress=" + demo.NormalizedProgress, "production-input");
            yield return RayClick(HelmetDiegeticStation.Command.CloseDemo);
            CheckNarrationStopped(caption, source, "Close equipment demonstration");
            station.Execute(HelmetDiegeticStation.Command.Voice); yield return Wait(.2f);
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.2f);
            CheckResult(demo.IsVisible && demo.CurrentClue == HelmetExplorationController.Clue.Air,
                "Changing the visible demonstration opens the new connection", demo.CurrentClue.ToString());
            CheckNarrationStopped(caption, source, "Change connection demonstration");
            station.Execute(HelmetDiegeticStation.Command.CloseDemo); yield return Wait(.15f);

            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            station.Execute(HelmetDiegeticStation.Command.Settings); yield return Wait(.3f);
            yield return RayClick(HelmetDiegeticStation.Command.Chinese);
            CheckNarrationStopped(caption, source, "Physical language switch");
            CheckResult(MuseumText.Channel.Current == MuseumLanguage.Chinese && !source.isPlaying,
                "Language switch waits for a new click", "Language=" + MuseumText.Channel.Current, "production-input");
            station.Execute(HelmetDiegeticStation.Command.Settings); yield return Wait(.2f);
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            CheckNarrationCaption(caption, source, "orientation", "intro-zh", MuseumLanguage.Chinese, true);
            station.Execute(HelmetDiegeticStation.Command.Settings); yield return Wait(.2f);
            yield return RayClick(HelmetDiegeticStation.Command.Sound);
            CheckResult(!inspection.SoundEnabled && !source.isPlaying && NarrationCaptionVisible(caption),
                "Physical sound toggle stops current narration audio and keeps its subtitle",
                "Sound=" + inspection.SoundEnabled + "; Subtitle=" + caption.text, "production-input");
            yield return RayClick(HelmetDiegeticStation.Command.Sound);
            CheckResult(inspection.SoundEnabled && !source.isPlaying, "Unmuting does not resume old narration audio",
                "Sound=" + inspection.SoundEnabled + "; Playing=" + source.isPlaying, "production-input");
            station.Execute(HelmetDiegeticStation.Command.Settings); yield return Wait(.15f);
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            CheckNarrationStopped(caption, source, "Stop after unmute");
            inspection.SoundEnabled = false;
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            CheckNarrationCaption(caption, source, "orientation", null, MuseumLanguage.Chinese, false);
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            CheckNarrationStopped(caption, source, "Stop silent subtitle");

            inspection.SoundEnabled = true;
            inspection.ReduceMotion = true;
            station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.2f);
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            CheckResult(inspection.ReduceMotion && demo.IsVisible && !demo.IsPlaying && source.isPlaying,
                "Reduced-motion diagram still supports narration", "Diagram visible=" + demo.IsVisible + "; Narration=" + source.isPlaying);
            yield return RayClick(HelmetDiegeticStation.Command.NextEra);
            CheckNarrationStopped(caption, source, "Change era with an active connection narration");
            foreach (int hiddenEra in new[] { 2, 0 })
            {
                station.Execute(HelmetDiegeticStation.Command.Air);
                station.Execute(HelmetDiegeticStation.Command.Voice);
                CheckResult(binding.AppliedEraIndex == hiddenEra && !demo.IsVisible,
                    "Connection cannot open in non-Mark-V era " + hiddenEra, "Actual era=" + binding.AppliedEraIndex);
                yield return RayClick(HelmetDiegeticStation.Command.Narration);
                CheckNarrationCaption(caption, source, "orientation", "intro-zh", MuseumLanguage.Chinese, true);
                yield return RayClick(HelmetDiegeticStation.Command.NextEra);
                CheckNarrationStopped(caption, source, "Change era from " + hiddenEra);
            }

            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            yield return RayClick(HelmetDiegeticStation.Command.Reset);
            CheckNarrationStopped(caption, source, "Reset exhibit");
            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            station.enabled = false; yield return Wait(.2f);
            CheckNarrationStopped(caption, source, "Disable station");
            station.enabled = true; yield return Wait(.35f);
            source = NarrationAudioSource();
            caption = station.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Guide subtitle");
            CheckNarrationStopped(caption, source, "Re-enable station without autoplay");

            yield return RayClick(HelmetDiegeticStation.Command.Narration);
            CheckResult(source.isPlaying, "Narration is live immediately before scene exit", "Playing=" + source.isPlaying);
            Scene exhibitScene = station.gameObject.scene;
            Scene scratch = SceneManager.CreateScene("Temporary narration exit validation");
            SceneManager.SetActiveScene(scratch);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(exhibitScene);
            if (unload == null) throw new InvalidOperationException("Could not unload the saved museum scene.");
            while (!unload.isDone) yield return null;
            CheckResult(!exhibitScene.isLoaded && !source && !caption &&
                !UnityEngine.Object.FindObjectsOfType<AudioSource>(true).Any(audio => audio.gameObject.name == "Helmet narration playback"),
                "Exiting the scene removes narration audio and subtitle", "Museum loaded=" + exhibitScene.isLoaded, "scene-lifecycle");
        }

        static IEnumerator NarrationConfirmFromIdle(HelmetPhysicalControl control, AudioSource source, TMP_Text caption)
        {
            var collider = control.Target.colliders.First(item => item && item.gameObject.activeInHierarchy);
            Vector3 point = collider.bounds.center;
            Vector3 position = RayApproach(HelmetDiegeticStation.Command.Narration, collider, point);
            Quaternion rotation = Quaternion.LookRotation(point - position, Vector3.up);
            int confirmed = control.ConfirmedCount;
            QueueController(rightDevice, position, rotation, false); yield return Wait(.3f);
            CheckResult(control.Target.isHovered && !source.isPlaying && !NarrationCaptionVisible(caption),
                "Hovering narration never starts a clip", DescribeRightRayHit(), "production-input");
            QueueController(rightDevice, position, rotation, true); yield return Wait(.2f);
            CheckResult(control.IsPressed && !source.isPlaying && control.ConfirmedCount == confirmed,
                "Grip press waits for a deliberate release", "Pressed=" + control.IsPressed, "production-input");
            QueueController(rightDevice, position, rotation, false); yield return Wait(.2f);
            CheckResult(control.ConfirmedCount == confirmed + 1 && source.isPlaying && NarrationCaptionVisible(caption),
                "Grip release starts exactly one narration", "Confirmations=" + control.ConfirmedCount, "production-input");
            ParkHands(); yield return Wait(.15f);
        }

        static bool NarrationCaptionVisible(TMP_Text caption) =>
            caption && caption.isActiveAndEnabled && !string.IsNullOrWhiteSpace(caption.text);

        static AudioSource NarrationAudioSource() => UnityEngine.Object.FindObjectsOfType<AudioSource>(true)
            .Single(audio => audio.gameObject.name == "Helmet narration playback");

        static string NarrationLabelSource(TMP_Text label)
        {
            var localized = label.GetComponent<LocalizedSourceText>();
            return localized ? localized.SourceEnglish : "";
        }

        static void CheckNarrationCaption(TMP_Text caption, AudioSource source, string cue, string clip, MuseumLanguage language, bool audible)
        {
            string key = "narration." + cue;
            var table = MuseumText.Table;
            bool localized = table.TryGet(key, language, out string expected);
            bool english = table.TryGet(key, MuseumLanguage.English, out string englishSource);
            var intended = clip == null ? null : Resources.Load<AudioClip>("HelmetNarration/" + clip);
            CheckResult(localized && english && NarrationCaptionVisible(caption) && caption.text == expected &&
                NarrationLabelSource(caption) == englishSource && (language == MuseumLanguage.English || expected != englishSource),
                "Saved exact narration caption " + cue + "/" + language, caption.text, "localization");
            CheckResult(audible ? intended && source.isPlaying && source.clip == intended : !source.isPlaying,
                "Narration AudioSource matches cue, language and sound setting " + cue + "/" + language,
                "Audible=" + source.isPlaying + "; Clip=" + (source.clip ? source.clip.name : "none"));
            caption.ForceMeshUpdate(true, true);
            string text = Regex.Replace(caption.text, "<[^>]*>", "");
            string missing = !caption.font ? "NO_FONT" : new string(text.Where(character => !char.IsWhiteSpace(character))
                .Distinct().Where(character => !caption.font.HasCharacter(character, false, false)).ToArray());
            CheckResult(caption.font == table.FontAsset && missing.Length == 0 && !caption.isTextOverflowing,
                "Saved glyphs and subtitle geometry fit " + cue + "/" + language,
                "Missing=" + missing + "; Overflow=" + caption.isTextOverflowing + "; Font=" + caption.fontSize, "localization");
        }

        static void CheckNarrationStopped(TMP_Text caption, AudioSource source, string reason)
        {
            bool playing = source && source.isPlaying;
            CheckResult(!playing && !NarrationCaptionVisible(caption),
                reason + " clears narration audio and subtitle",
                "Playing=" + playing + "; Subtitle visible=" + NarrationCaptionVisible(caption), "narration-lifecycle");
        }

        static void CaptureNarrationSubtitle(TMP_Text caption, string cue, MuseumLanguage language)
        {
            if (!NarrationCaptionVisible(caption)) return;
            caption.ForceMeshUpdate(true, true);
            Bounds bounds = caption.GetComponent<Renderer>().bounds;
            float distance = Mathf.Max(.45f, (bounds.extents.magnitude + .08f) / Mathf.Tan(24f * Mathf.Deg2Rad));
            Vector3 position = bounds.center - caption.transform.forward * distance;
            Quaternion rotation = Quaternion.LookRotation(bounds.center - position, caption.transform.up);
            string file = "narration-" + cue + "-" + language + ".png";
            HelmetDiegeticPreview.CaptureView(Path.Combine(Output, file), position, rotation, 48f);
            report.diagnosticCaptures.Add(file);
        }
    }
}
#endif
