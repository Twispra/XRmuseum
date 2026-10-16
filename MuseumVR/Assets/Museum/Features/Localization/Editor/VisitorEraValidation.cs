#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Museum.Eras;
using Museum.Exploration;
using Museum.Hotspots;
using Museum.Inspection;
using Museum.PhysicalHands.EditorTools;
using Museum.Refined;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Localization.EditorTools
{
    /// <summary>Read-only visitor-era acceptance on the saved scene. Never builds product assets.</summary>
    [InitializeOnLoad]
    public static class VisitorEraValidation
    {
        const string Prefix = "Museum.VisitorEraQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string EarlyCaption = "Diving helmet\n<size=62%>Early (1839) - placeholder study</size>";
        const string EarlyDetail = "Placeholder; not a historical replica.";
        const string MarkVCaption = "Diving helmet\n<size=62%>Mark V-inspired study</size>";
        const string SuperLiteCaption = "Diving helmet\n<size=62%>SuperLite 17B-inspired study</size>";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static HelmetEraSwitcher era;
        static RefinedHelmetBinding binding;
        static HelmetEraVisitorContent content;
        static HelmetInspectionController inspection;
        static HelmetWindowInteractable window;
        static HelmetHotspotTour tour;
        static HelmetHotspot[] hotspots;
        static HelmetEraButton[] buttons;
        static TMP_Text information;
        static LocalizationTable table;
        static LanguageChannel language;
        static ActionBasedController rightController, leftController;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD hmd;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedMouse, changedInput, finishing;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static double started;
        static int lastFrame = -1;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => SessionState.GetString(Prefix + "Output", "");

        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class Point
        {
            public string identifier, title, description;
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        [Serializable] sealed class Capture
        {
            public string file, purpose;
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView;
        }
        [Serializable] sealed class Report
        {
            public string utc, finishedUtc, commit, dirty, unity, status = "running", failure;
            public string method = "Saved MuseumStart scene; public API fixtures seed completed hotspot visits and request era/language changes. Queued synthetic controller/HMD states flow through normal Dynamic Input Actions and existing XR ray interactors to both era buttons and the held window. No manual SelectEnter/SelectExit, HoverEnter, TriggerProgrammatically, private-field writes or InputSystem.Update. The official simulator state writer is disabled; physical hands, production colliders and select filters remain enabled.";
            public string limits = "Desktop synthetic XR and explicit API fixtures, not real headset/controller, historical authentication, stereo readability, user study, performance or executable build. Captures use temporary diagnostic monocular cameras; no production camera or product asset is saved. explanationElapsed is read only to detect timer restarts. Parent process must additionally verify source fingerprints after natural Editor exit.";
            public float batchExitSettleSeconds = 20;
            public List<Check> checks = new List<Check>();
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<Point> markVPoints = new List<Point>();
            public List<Capture> captures = new List<Capture>();
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        static VisitorEraValidation()
        { EditorApplication.playModeStateChanged += OnPlayMode; EditorApplication.update += Tick; }

        [MenuItem("Museum/Localization/Validate visitor era content")]
        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("A dirty scene is open; save separately before validation.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string run = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-visitor-era-" + Guid.NewGuid().ToString("N");
            string outputBase = Environment.GetEnvironmentVariable("MUSEUM_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputBase)) outputBase = Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs");
            string output = Path.GetFullPath(Path.Combine(outputBase, run));
            if (Directory.Exists(output)) throw new InvalidOperationException("Evidence already exists: " + output);
            Directory.CreateDirectory(output); SessionState.SetString(Prefix + "Output", output);
            report = new Report { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion };
            foreach (string path in AssetDatabase.GetDependencies(ScenePath, true).Concat(new[] {
                "Assets/Museum/Features/Localization/Editor/VisitorEraValidation.cs",
                "Assets/Museum/Features/Localization/Runtime/HelmetEraVisitorContent.cs",
                "Assets/Museum/Features/RefinedModels/Runtime/RefinedHelmetBinding.cs",
                "Assets/Museum/Features/Localization/Tables/MuseumTranslations.json",
                "Packages/manifest.json", "Packages/packages-lock.json" }).Distinct().Where(File.Exists).OrderBy(p => p))
                report.files.Add(new Fingerprint { path = path, sha256 = Hash(path) });
            var savedButtons = UnityEngine.Object.FindObjectsOfType<HelmetEraButton>(true);
            Require(savedButtons.Length == 2, "Saved scene contains two era buttons", "count=" + savedButtons.Length);
            foreach (var button in savedButtons) ValidateSavedButton(button);
            var savedContent = UnityEngine.Object.FindObjectOfType<HelmetEraVisitorContent>();
            Require(savedContent && savedContent.Instructions, "Saved era content has caption binding", "Existing product component.");
            var savedTour = UnityEngine.Object.FindObjectOfType<HelmetHotspotTour>();
            foreach (var point in savedTour.GetComponentsInChildren<HelmetHotspot>(true))
                report.markVPoints.Add(new Point { identifier = point.Identifier, title = point.Title, description = point.Description,
                    position = point.transform.localPosition, rotation = point.transform.localRotation, scale = point.transform.localScale });
            Require(report.markVPoints.Count == 3, "Saved Mark V baseline has exactly three observation points", "count=" + report.markVPoints.Count);
            SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
            SessionState.SetInt(Prefix + "Exit", 1); SessionState.SetBool(Prefix + "PendingExit", false);
            SessionState.SetBool(Prefix + "Active", true); EditorApplication.EnterPlaymode();
        }

        static void ValidateSavedButton(HelmetEraButton button)
        {
            string expected = button.Direction == HelmetEraButton.StepDirection.Previous ? "era.previous" : "era.next";
            var labels = button.GetComponentsInChildren<LocalizedText>(true);
            Require(button.GetComponent<XRSimpleInteractable>() && button.Switcher,
                "Saved era button retains its existing XRI and switcher / " + button.name, button.Direction.ToString());
            Require(labels.Length == 1 && labels[0].Key == expected && labels[0].GetComponent<TMP_Text>(),
                "Saved era button has static localized key / " + button.name,
                "expected=" + expected + "; actual=" + string.Join(",", labels.Select(l => l.Key)));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("Visitor era QA keyboard");
            mouse = Mouse.current; addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>("Visitor era QA mouse");
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                started = EditorApplication.timeSinceStartup; lastFrame = -1; finishing = false;
                PendingInput.Clear(); Routines.Clear(); Routines.Push(ValidateRuntime());
                Application.logMessageReceived += OnLog; InputSystem.onBeforeUpdate += FlushInput;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && !finishing && report != null) Finish("Play Mode ended before validation completed.", false);
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Prefix + "Active", false);
                if (Application.isBatchMode)
                {
                    SessionState.SetFloat(Prefix + "ExitAt", (float)EditorApplication.timeSinceStartup + 20);
                    SessionState.SetBool(Prefix + "PendingExit", true);
                    Debug.Log("VISITOR_ERA_QA_SETTLING seconds=20; utc=" + DateTime.UtcNow.ToString("o"));
                }
            }
        }

        static void Tick()
        {
            if (SessionState.GetBool(Prefix + "PendingExit", false))
            {
                if (EditorApplication.timeSinceStartup < SessionState.GetFloat(Prefix + "ExitAt", float.MaxValue) ||
                    EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Prefix + "PendingExit", false);
                EditorApplication.Exit(SessionState.GetInt(Prefix + "Exit", 1)); return;
            }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 180) { Finish("Visitor-era validation exceeded 180 seconds."); return; }
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            if (Application.isBatchMode) System.Threading.Thread.Sleep(11);
            try
            {
                while (Routines.Count > 0)
                {
                    var routine = Routines.Peek(); if (!routine.MoveNext()) { Routines.Pop(); continue; }
                    if (routine.Current is IEnumerator nested) { Routines.Push(nested); continue; }
                    return;
                }
                Finish(null);
            }
            catch (Exception ex) { Finish(ex.ToString()); }
        }

        static IEnumerator ValidateRuntime()
        {
            yield return Wait(2);
            Require(!InputSystem.devices.OfType<XRHMD>().Any(d => !(d is XRSimulatedHMD)), "Synthetic input refuses real HMD", "No hardware state is overwritten.");
            content = UnityEngine.Object.FindObjectOfType<HelmetEraVisitorContent>();
            binding = UnityEngine.Object.FindObjectOfType<RefinedHelmetBinding>(); era = binding.GetComponent<HelmetEraSwitcher>();
            inspection = binding.GetComponent<HelmetInspectionController>(); window = binding.GetComponentInChildren<HelmetWindowInteractable>(true);
            tour = binding.GetComponentInChildren<HelmetHotspotTour>(true); hotspots = tour.GetComponentsInChildren<HelmetHotspot>(true).OrderBy(h => h.Identifier).ToArray();
            buttons = UnityEngine.Object.FindObjectsOfType<HelmetEraButton>().OrderBy(b => b.Direction).ToArray();
            information = tour.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Information");
            table = MuseumText.Table; language = MuseumText.Channel;
            Require(content && binding && era && inspection && window && table && language && buttons.Length == 2,
                "Saved runtime dependencies are intact", "No runtime-generated replacement buttons.");
            SetupInput(); yield return Wait(.4f);
            era.ShowEra(1); yield return Wait(.35f);
            foreach (var point in hotspots) point.Activate();
            var selected = hotspots.Single(h => h.Identifier == "front-window"); selected.Activate();
            Require(tour.VisitedCount == 3 && hotspots.All(h => h.IsVisited), "API fixture seeds exactly three completed observations", "Activate is a labelled fixture, not an XR interaction test.");
            yield return Wait(.15f);
            Require(ExplanationElapsed() > .05f, "Selected explanation has an advancing timer before transitions", "elapsed=" + ExplanationElapsed());
            foreach (int index in new[] { 0, 1, 2 })
            {
                float elapsed = ExplanationElapsed();
                era.ShowEra(index); yield return Wait(.35f);
                Require(ExplanationElapsed() + .005f >= elapsed, "Era change does not restart selected explanation timer / " + index, "read-only elapsed=" + ExplanationElapsed());
                var demo = UnityEngine.Object.FindObjectOfType<HelmetConnectionDemonstration>();
                Require(demo, "Connection demonstration exists", "Existing production component.");
                foreach (var nextLanguage in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
                {
                    // Each language has its own explicit playback fixture. Diagnostic
                    // screenshot rendering may outlast a complete demonstration.
                    demo.Play(HelmetExplorationController.Clue.Air); yield return Wait(.25f);
                    float playback = demo.NormalizedProgress, timer = ExplanationElapsed();
                    language.SetLanguage(nextLanguage); yield return Wait(.15f);
                    Require(demo.IsPlaying && demo.NormalizedProgress + .001f >= playback,
                        "Language does not restart connection animation / " + index + "/" + nextLanguage,
                        "progress before=" + playback + "; after=" + demo.NormalizedProgress);
                    Require(ExplanationElapsed() + .005f >= timer, "Language does not restart observation timer / " + index + "/" + nextLanguage,
                        "before=" + timer + "; after=" + ExplanationElapsed());
                    CheckEra(index, nextLanguage, selected);
                    if (index == 0)
                    {
                        // Explicit API fixtures also render every full early card, rather than
                        // accepting a translated caption beside an English-fallback detail.
                        foreach (var point in hotspots)
                        {
                            point.Activate();
                            CheckText(information, point.Title + "\n" + point.Description, "All early selected cards / " + nextLanguage + "/" + point.Identifier);
                        }
                        selected.Activate();
                    }
                    CaptureLabels(index + "-" + nextLanguage + "-caption.png", new[] { content.Instructions }, "Current front exhibit caption; diagnostic view.");
                    CaptureLabels(index + "-" + nextLanguage + "-buttons.png", buttons.SelectMany(b => b.GetComponentsInChildren<TMP_Text>()), "Both saved era-button captions; diagnostic view.");
                }
                demo.Stop();
            }
            // Exercise the other important source transition with the same selected ID.
            era.ShowEra(0); yield return Wait(.35f);
            foreach (var nextLanguage in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
            {
                language.SetLanguage(nextLanguage); yield return Wait(.1f);
                CheckEra(0, nextLanguage, selected);
            }
            era.ShowEra(1); yield return Wait(.35f); language.SetLanguage(MuseumLanguage.English); yield return Wait(.1f);
            CheckEra(1, language.Current, selected);
            yield return PressEra(buttons.Single(b => b.Direction == HelmetEraButton.StepDirection.Previous));
            CheckEra(0, language.Current, selected);
            yield return PressEra(buttons.Single(b => b.Direction == HelmetEraButton.StepDirection.Next));
            CheckEra(1, language.Current, selected);
            yield return HeldWindowDefersCaption(selected);
            ParkHands();
        }

        static void CheckEra(int index, MuseumLanguage currentLanguage, HelmetHotspot selected)
        {
            string suffix = index + "/" + currentLanguage;
            Require(era.CurrentIndex == index && binding.AppliedEraIndex == index, "Requested and applied era agree / " + suffix,
                "request=" + era.CurrentIndex + "; applied=" + binding.AppliedEraIndex);
            Require(binding.ActiveShell && binding.ActiveShell.enabled && binding.ActiveGlass && binding.ActiveGlass.enabled &&
                binding.IsRefinedActive == (index != 0) && binding.IsAdditionalVisualActive == (index == 2),
                "Applied geometry matches visitor caption / " + suffix, "shell=" + binding.ActiveShell.name + "; glass=" + binding.ActiveGlass.name);
            CheckText(content.Instructions, Caption(index), "Exhibit caption / " + suffix);
            if (index == 0) Require(!content.Instructions.text.Contains("Mark V"), "Early caption has no Mark V attribution / " + suffix, content.Instructions.text);
            foreach (var point in hotspots)
            {
                var original = report.markVPoints.Single(p => p.identifier == point.Identifier);
                string expectedTitle = index == 2 ? SuperLiteTitle(point.Identifier) : original.title;
                string expectedDetail = index == 0 ? EarlyDetail : index == 2 ? SuperLiteDetail(point.Identifier) : original.description;
                Require(point.Title == expectedTitle && point.Description == expectedDetail,
                    "Observation content follows applied era / " + suffix + "/" + point.Identifier,
                    point.Title + "\n" + point.Description);
                if (index == 2)
                {
                    var attachment = content.Attachments.Single(a => a.hotspot == point);
                    Require(attachment.surface && attachment.surface.enabled &&
                        Vector3.Distance(point.transform.position, attachment.surface.transform.TransformPoint(attachment.localPoint)) < .0002f,
                        "SuperLite point follows saved mesh attachment / " + suffix + "/" + point.Identifier, point.transform.position.ToString("F6"));
                }
                else Require(Vector3.Distance(point.transform.localPosition, original.position) < .0002f &&
                    Quaternion.Angle(point.transform.localRotation, original.rotation) < .02f && Vector3.Distance(point.transform.localScale, original.scale) < .0002f,
                    "Original observation local pose restored / " + suffix + "/" + point.Identifier, point.transform.localPosition.ToString("F6"));
            }
            Require(tour.VisitedCount == 3 && hotspots.All(h => h.IsVisited), "Era/language retains all exact visited identities / " + suffix, "count=" + tour.VisitedCount);
            CheckText(information, selected.Title + "\n" + selected.Description, "Selected observation card remains current / " + suffix);
            foreach (var button in buttons)
            {
                string key = button.Direction == HelmetEraButton.StepDirection.Previous ? "era.previous" : "era.next";
                var label = button.GetComponentsInChildren<LocalizedText>().Single();
                string english = button.Direction == HelmetEraButton.StepDirection.Previous ? "Previous era" : "Next era";
                Require(label.Key == key && table.Get(key, MuseumLanguage.English) == english && label.GetComponent<TMP_Text>().text == table.Get(key, currentLanguage),
                    "Static era-button key and translated label / " + suffix + "/" + key, label.GetComponent<TMP_Text>().text);
                CheckLayout(label.GetComponent<TMP_Text>(), "Era button / " + suffix + "/" + key);
            }
        }

        static IEnumerator PressEra(HelmetEraButton source)
        {
            int before = era.CurrentIndex;
            int expected = (before + (source.Direction == HelmetEraButton.StepDirection.Next ? 1 : 2)) % 3;
            var target = source.GetComponent<XRSimpleInteractable>(); var hit = target.colliders.FirstOrDefault(c => c && c.enabled) ?? target.GetComponent<Collider>();
            int events = 0; string interactor = "none";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; interactor = args.interactorObject.GetType().Name; };
            target.selectEntered.AddListener(listener);
            try
            {
                Vector3 position = hit.bounds.center - target.transform.forward * .55f;
                Quaternion rotation = Quaternion.LookRotation(hit.bounds.center - position, Vector3.up);
                QueueController(rightDevice, position, rotation, false); yield return Wait(.35f);
                string rayDetail = string.Join(" | ", rightController.GetComponentsInChildren<XRRayInteractor>(true)
                    .Where(r => r.isActiveAndEnabled).Select(r => r.name + " origin=" + r.rayOriginTransform.position +
                        "; hovered=" + string.Join(",", r.interactablesHovered.Select(t => t.transform.name)) +
                        "; hits=" + string.Join(",", Physics.RaycastAll(r.rayOriginTransform.position, r.rayOriginTransform.forward, 2f, ~0, QueryTriggerInteraction.Collide)
                            .OrderBy(h => h.distance).Take(8).Select(h => h.collider.name + "@" + h.distance.ToString("F3")))));
                Require(target.isHovered, "Production ray hovers era button / " + source.name,
                    "Saved collider; physical hands remain enabled. Controller=" + rightController.transform.position + "; target=" + hit.bounds.center + "; " + rayDetail);
                QueueController(rightDevice, position, rotation, true); yield return Wait(.25f);
                Require(events == 1 && interactor == nameof(XRRayInteractor) && era.CurrentIndex == expected,
                    "Production grip selects era button exactly once / " + source.name,
                    "events=" + events + "; interactor=" + interactor + "; expected era=" + expected + "; actual=" + era.CurrentIndex);
                QueueController(rightDevice, position, rotation, false); yield return Wait(.25f); ParkHands(); yield return Wait(.15f);
            }
            finally { if (target) target.selectEntered.RemoveListener(listener); }
        }

        static IEnumerator HeldWindowDefersCaption(HelmetHotspot selected)
        {
            inspection.SetWindowAngle(0); yield return Wait(.2f);
            var collider = window.colliders[0]; Vector3 outward = inspection.transform.rotation * Vector3.back;
            Vector3 touch = MuseumPhysicalHandsTestPose.WindowTouch(collider, rightController, Quaternion.identity, outward, collider.bounds.center + outward * .009f);
            Vector3 position = touch + outward * .5f; Quaternion rotation = Quaternion.LookRotation(touch - position, Vector3.up);
            QueueController(rightDevice, position, rotation, false); yield return Wait(.35f);
            QueueController(rightDevice, position, rotation, true); yield return Wait(.25f);
            Require(window.isSelected && inspection.IsWindowHeld && window.interactorsSelecting[0] is XRRayInteractor,
                "Production ray holds the original window", "No select events are invoked directly.");
            era.ShowEra(2); yield return Wait(.35f);
            Require(era.CurrentIndex == 2 && binding.AppliedEraIndex == 1 && !binding.IsAdditionalVisualActive && inspection.IsWindowHeld,
                "Held window defers actual geometry while accepting era request", "requested=" + era.CurrentIndex + "; applied=" + binding.AppliedEraIndex);
            CheckText(content.Instructions, MarkVCaption, "Deferred request retains Mark V caption");
            language.SetLanguage(MuseumLanguage.Chinese); yield return Wait(.25f);
            CheckText(content.Instructions, MarkVCaption, "Language switch while held follows actual Mark V geometry");
            Require(tour.VisitedCount == 3, "Pending request preserves observations", "count=" + tour.VisitedCount);
            yield return Wait(.3f); // A long hold avoids treating the release as a short window click.
            QueueController(rightDevice, position, rotation, false); yield return Wait(.4f); ParkHands(); yield return Wait(.2f);
            Require(!inspection.IsWindowHeld && binding.AppliedEraIndex == 2, "Window release applies pending era", "applied=" + binding.AppliedEraIndex);
            CheckEra(2, language.Current, selected);
            CaptureLabels("deferred-release-Chinese-caption.png", new[] { content.Instructions }, "Caption after releasing pending era request.");
        }

        static string Caption(int index) => index == 0 ? EarlyCaption : index == 1 ? MarkVCaption : SuperLiteCaption;
        static string SuperLiteTitle(string id) => id == "front-window" ? "Front window" : id == "bonnet" ? "Helmet shell" : "Neck seal";
        static string SuperLiteDetail(string id) => id == "front-window" ? "Window and rim. Side opening is a museum demonstration."
            : id == "bonnet" ? "Inspect the upper shell of this lightweight helmet study." : "Inspect the neck seal and surrounding clamp below the shell.";
        static float ExplanationElapsed()
        {
            var field = typeof(HelmetHotspotTour).GetField("explanationElapsed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Read-only explanation timer evidence is unavailable.");
            return (float)field.GetValue(tour);
        }
        static void CheckText(TMP_Text label, string source, string name)
        {
            var entry = table.Entries.FirstOrDefault(e => e.English == source);
            Require(entry.English == source && !string.IsNullOrWhiteSpace(entry.Chinese) && !string.IsNullOrWhiteSpace(entry.TraditionalChinese) &&
                entry.Chinese != source && entry.TraditionalChinese != source,
                "Saved full trilingual source exists / " + name, source);
            var localized = label.GetComponent<LocalizedSourceText>();
            Require(localized && localized.SourceEnglish == source && label.text == entry.For(language.Current),
                "Actual dynamic source and exact translation / " + name,
                "source=" + (localized ? localized.SourceEnglish : "missing") + "; text=" + label.text);
            CheckLayout(label, name);
        }
        static void CheckLayout(TMP_Text label, string name)
        {
            label.ForceMeshUpdate(true, true);
            string plain = Regex.Replace(label.text, "<[^>]*>", "");
            string missing = !label.font ? "NO_FONT" : new string(plain.Where(c => !char.IsWhiteSpace(c)).Distinct().Where(c => !label.font.HasCharacter(c, false, false)).ToArray());
            Require(missing.Length == 0 && !label.isTextOverflowing, "Saved glyphs and layout fit / " + name,
                "missing=" + missing + "; overflowing=" + label.isTextOverflowing + "; fontSize=" + label.fontSize);
        }

        static void SetupInput()
        {
            var simulators = UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>();
            Require(simulators.Length > 0, "Official simulator is present", "Disable only its automated device-state writer.");
            foreach (var simulator in simulators) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedInput = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard); if (addedMouse) InputSystem.EnableDevice(mouse);
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Left Controller");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            hmd = InputSystem.AddDevice<XRSimulatedHMD>();
            var camera = Camera.main; var parent = camera.transform.parent;
            Vector3 position = parent ? parent.InverseTransformPoint(camera.transform.position) : camera.transform.position;
            Quaternion rotation = parent ? Quaternion.Inverse(parent.rotation) * camera.transform.rotation : camera.transform.rotation;
            var state = new XRSimulatedHMDState { isTracked = true, trackingState = 3, devicePosition = position, centerEyePosition = position,
                leftEyePosition = position + Vector3.left * .032f, rightEyePosition = position + Vector3.right * .032f,
                deviceRotation = rotation, centerEyeRotation = rotation, leftEyeRotation = rotation, rightEyeRotation = rotation };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(hmd, state)); ParkHands();
        }
        static void QueueController(XRSimulatedController device, Vector3 position, Quaternion rotation, bool grip)
        {
            var parent = (device == leftDevice ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(position), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation,
                grip = grip ? 1f : 0f }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        { QueueController(rightDevice, new Vector3(.8f, .8f, 1f), Quaternion.identity, false); QueueController(leftDevice, new Vector3(-.8f, .8f, 1f), Quaternion.identity, false); }
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        static void CaptureLabels(string file, IEnumerable<TMP_Text> labels, string purpose)
        {
            var subjects = labels.ToArray(); Require(subjects.Length > 0, "Capture contains text / " + file, purpose);
            foreach (var label in subjects) label.ForceMeshUpdate(true, true);
            var bounds = subjects[0].GetComponent<Renderer>().bounds;
            foreach (var label in subjects.Skip(1)) bounds.Encapsulate(label.GetComponent<Renderer>().bounds);
            float distance = Mathf.Max(.35f, (bounds.extents.magnitude + .06f) / Mathf.Tan(24f * Mathf.Deg2Rad));
            Vector3 position = bounds.center - subjects[0].transform.forward * distance;
            Quaternion rotation = Quaternion.LookRotation(bounds.center - position, subjects[0].transform.up);
            var temporary = new GameObject("Temporary visitor era diagnostic camera") { hideFlags = HideFlags.HideAndDontSave };
            var previous = RenderTexture.active; var target = RenderTexture.GetTemporary(1440, 1080, 24, RenderTextureFormat.ARGB32); Texture2D pixels = null;
            try
            {
                var camera = temporary.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false; camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.targetTexture = null; camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = 48; camera.aspect = 4f / 3f;
                var additional = temporary.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); additional.allowXRRendering = false; additional.renderPostProcessing = false;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); pixels.Apply(); File.WriteAllBytes(Path.Combine(Output, file), pixels.EncodeToPNG());
                report.captures.Add(new Capture { file = file, purpose = purpose, position = position, rotation = rotation, fieldOfView = 48 });
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(temporary); }
        }
        static void Require(bool passed, string name, string detail)
        { report.checks.Add(new Check { name = name, detail = detail, passed = passed }); if (!passed) throw new InvalidOperationException(name + ": " + detail); }
        static void OnLog(string message, string stack, LogType type)
        {
            if (report == null) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + message + "\n" + stack);
            else if (type == LogType.Warning) report.warnings.Add(message);
        }
        static void Finish(string failure, bool exitPlay = true)
        {
            if (finishing) return; finishing = true;
            Application.logMessageReceived -= OnLog; InputSystem.onBeforeUpdate -= FlushInput; Routines.Clear(); PendingInput.Clear();
            report.failure = failure ?? "";
            foreach (var file in report.files) report.checks.Add(new Check { name = "Validator preserved saved source / " + file.path, passed = Hash(file.path) == file.sha256, detail = Hash(file.path) });
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, hmd, keyboard, addedMouse ? mouse : null }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedInput) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; }
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.status = string.IsNullOrEmpty(failure) && report.errors.Count == 0 && report.checks.All(c => c.passed) ? "passed" : "failed";
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            SessionState.SetInt(Prefix + "Exit", report.status == "passed" ? 0 : 1);
            Debug.Log("VISITOR_ERA_QA_" + report.status.ToUpperInvariant() + " " + Path.Combine(Output, "report.json") + (failure == null ? "" : "\n" + failure));
            if (exitPlay) EditorApplication.ExitPlaymode();
        }
        static string Hash(string path)
        { if (!File.Exists(path)) return "MISSING"; using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static string Git(string arguments)
        {
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", arguments) { WorkingDirectory = RepoRoot, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
                start.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(5000)) { process.Kill(); return "UNAVAILABLE: git timeout"; }
                    if (!stdout.Wait(1000) || !stderr.Wait(1000)) return "UNAVAILABLE: incomplete output";
                    return process.ExitCode == 0 ? stdout.Result.Trim() : "UNAVAILABLE: " + stderr.Result.Trim();
                }
            }
            catch (Exception ex) { return "UNAVAILABLE: " + ex.Message; }
        }
    }
}
#endif
