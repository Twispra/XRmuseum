#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Museum.Eras;
using Museum.Hotspots;
using Museum.Inspection;
using Museum.Localization;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using Command = Museum.Exploration.HelmetExplorationButton.Action;

namespace Museum.Exploration.Editor
{
    /// <summary>Exercises the saved visitor buttons and their cross-module state contract.</summary>
    [InitializeOnLoad]
    public static class VisitorNewVisitValidation
    {
        const string Prefix = "Museum.NewVisitQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string Prompt = "Select a highlighted point to inspect the helmet.";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static HelmetExplorationPanel panel;
        static HelmetInspectionPalette palette;
        static HelmetExplorationController exploration;
        static HelmetInspectionController inspection;
        static HelmetHotspotTour tour;
        static HelmetHotspot[] hotspots;
        static ActionBasedController rightController, leftController;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD hmd;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedMouse, changedSettings, finishing;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static double started, exitAt;
        static int lastFrame = -1, pendingExit = -1;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", SessionState.GetString(Prefix + "Run", "missing"));

        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class Report
        {
            public string utc, finishedUtc, commit, dirty, unity, failure;
            public string method = "Saved MuseumStart; queued synthetic HMD/controller state in normal Dynamic input updates through production Input Actions and XR ray selection for New visit, Inspection Reset and Reset View. Separate public-API lifecycle fixtures are labelled. No manual SelectEnter, HoverEnter, TriggerProgrammatically or InputSystem.Update calls.";
            public string limitations = "No real headset/controller, comfort, learning study or executable build verification. Product scene, prefab and input assets are never saved. Runtime fixture objects are destroyed before leaving Play Mode.";
            public List<Check> checks = new List<Check>();
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<string> errors = new List<string>();
        }
        [Serializable] sealed class Discovery
        {
            public bool air, voice, selected, complete;
            public int hints, page, clue, visited;
            public string identifiers;
        }
        sealed class RetainedState
        {
            public MuseumLanguage language;
            public bool sound, reduceMotion, floating, cutaway;
            public int era, reset;
            public float window;
            public Vector3 position, originPosition;
            public Quaternion rotation, originRotation;
        }

        static VisitorNewVisitValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            if (SessionState.GetBool(Prefix + "ExitPending", false))
            { pendingExit = SessionState.GetInt(Prefix + "Exit", 1); exitAt = EditorApplication.timeSinceStartup + 20; }
        }
        [MenuItem("Museum/Exploration/Validate new visitor reset")]
        public static void RunInEditor() => Begin();
        public static void RunBatch() => Begin();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("New visitor QA keyboard");
            mouse = Mouse.current; addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>("New visitor QA mouse");
        }
        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save modified scenes first; validation never saves them.");
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetString(Prefix + "Run", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-new-visit-" + Guid.NewGuid().ToString("N"));
            report = new Report { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion };
            var paths = AssetDatabase.GetDependencies(ScenePath, true).Concat(Directory.GetFiles("Assets/Museum/Features/HelmetExploration", "*", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/Museum/Features/HelmetHotspots/Runtime", "*", SearchOption.AllDirectories))
                .Concat(new[] { "Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectSettings.asset" });
            foreach (string path in paths.Select(p => p.Replace('\\', '/')).Distinct().OrderBy(p => p, StringComparer.Ordinal))
                if (File.Exists(path)) using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                    report.files.Add(new Fingerprint { path = path, sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() });
            SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
            SessionState.SetBool(Prefix + "ExitPending", false); SessionState.SetBool(Prefix + "Active", true);
            EditorApplication.EnterPlaymode();
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                started = EditorApplication.timeSinceStartup; lastFrame = -1; finishing = false;
                Application.logMessageReceived += OnLog; InputSystem.onBeforeUpdate += FlushInput;
                Routines.Clear(); PendingInput.Clear(); Routines.Push(ValidateMain());
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Prefix + "Active", false);
                if (Application.isBatchMode)
                {
                    SessionState.SetBool(Prefix + "ExitPending", true);
                    pendingExit = SessionState.GetInt(Prefix + "Exit", 1); exitAt = EditorApplication.timeSinceStartup + 20;
                }
            }
        }
        static void Tick()
        {
            if (pendingExit >= 0 && !EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.timeSinceStartup >= exitAt)
            { int code = pendingExit; pendingExit = -1; SessionState.SetBool(Prefix + "ExitPending", false); EditorApplication.Exit(code); return; }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 120) { Finish("Validation exceeded 120 seconds."); return; }
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
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
            catch (Exception exception) { Finish(exception.ToString()); }
        }
        static IEnumerator ValidateMain()
        {
            yield return Wait(2);
            if (InputSystem.devices.OfType<XRHMD>().Any(d => !(d is XRSimulatedHMD))) throw new InvalidOperationException("Real HMD found; refusing synthetic input.");
            foreach (var simulator in UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>()) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            panel = UnityEngine.Object.FindObjectOfType<HelmetExplorationPanel>(); palette = panel.GetComponent<HelmetInspectionPalette>();
            exploration = panel.Exploration; inspection = palette.Controller;
            tour = inspection.GetComponentsInChildren<HelmetHotspotTour>(true).Single(t => t.GetComponentInParent<HelmetInspectionController>(true) == inspection);
            hotspots = tour.GetComponentsInChildren<HelmetHotspot>(true);
            CheckResult(hotspots.Length == 3, "Saved exhibit has three inspection hotspots", "count=" + hotspots.Length);
            CheckResult(Reference<HelmetHotspotTour>(exploration, "visitTour") == tour, "Saved New visit explicitly binds the matching exhibit tour", tour.name);
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Left Controller");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            hmd = InputSystem.AddDevice<XRSimulatedHMD>(); InputSystem.EnableDevice(keyboard); if (addedMouse) InputSystem.EnableDevice(mouse);
            QueueHead(); ParkHands(); yield return Wait(.5f);
            foreach (var language in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
                yield return ValidateLanguage(language);
            yield return LifecycleFixtures();
        }
        static IEnumerator ValidateLanguage(MuseumLanguage language)
        {
            string prefix = language + ": ";
            MuseumText.Channel.SetLanguage(language);
            var era = inspection.GetComponent<HelmetEraSwitcher>();
            int chosenEra = language == MuseumLanguage.English ? 2 : language == MuseumLanguage.Chinese ? 0 : 1;
            era.ShowEra(chosenEra); yield return Wait(.35f);
            CheckResult(era.CurrentIndex == chosenEra, prefix + "fixture selects a different era", "era=" + chosenEra);
            inspection.ReduceMotion = language != MuseumLanguage.English;
            inspection.SoundEnabled = language == MuseumLanguage.TraditionalChinese;
            hotspots[0].Activate(); exploration.SelectClue(HelmetExplorationController.Clue.Air); exploration.RequestHint();
            string discovery = JsonUtility.ToJson(SnapshotDiscovery()); int resetBefore = inspection.ResetCount;
            var preferences = SnapshotRetained();
            panel.ShowInspection(); yield return Wait(.2f);
            var reset = panel.GetComponentsInChildren<HelmetInspectionControls>(true).Single(c => c.name == "Inspection RESET").GetComponent<XRSimpleInteractable>();
            yield return Press(reset, prefix + "Inspection Reset"); yield return Wait(.4f);
            CheckResult(inspection.ResetCount == resetBefore + 1, prefix + "ordinary Reset reaches physical return", "before=" + resetBefore + "; after=" + inspection.ResetCount);
            CheckResult(discovery == JsonUtility.ToJson(SnapshotDiscovery()), prefix + "ordinary Reset preserves partial discoveries and hint", JsonUtility.ToJson(SnapshotDiscovery()));
            CheckPreferences(preferences, prefix + "ordinary Reset");

            panel.ShowExploration(); var origin = Camera.main.GetComponentInParent<XROrigin>().Origin.transform;
            origin.position += new Vector3(.25f, 0, -.2f); origin.rotation = Quaternion.Euler(0, 20, 0);
            palette.RecenterNow(); yield return Wait(.3f);
            yield return Press(Button(Command.CenterView), prefix + "Reset View");
            CheckResult(discovery == JsonUtility.ToJson(SnapshotDiscovery()), prefix + "Reset View preserves partial discoveries and hint", JsonUtility.ToJson(SnapshotDiscovery()));
            CheckPreferences(preferences, prefix + "Reset View");

            if (!inspection.IsFloating) inspection.ToggleFloating();
            inspection.RotateDisplay(25); inspection.SetWindowOpen(true, true); inspection.SetCutaway(true);
            foreach (var hotspot in hotspots) hotspot.Activate();
            tour.SetHotspotsVisible(true); tour.AdvanceExplanation(30.4f);
            exploration.SelectClue(HelmetExplorationController.Clue.Voice); exploration.RequestHint(); exploration.ShowReview();
            CheckResult(exploration.AirSeen && exploration.VoiceSeen && exploration.HintRequests > 0 && tour.IsComplete,
                prefix + "seed has both connections, hint and 3/3 completion", JsonUtility.ToJson(SnapshotDiscovery()));
            CheckResult(Reference<GameObject>(tour, "completionFeedback").activeSelf, prefix + "completed tour shows completion feedback", "3/3 before New visit");
            CheckResult(tour.ExplanationOpacity > 0 && tour.ExplanationOpacity < 1, prefix + "seed has a live fading explanation", "opacity=" + tour.ExplanationOpacity);
            foreach (var hotspot in hotspots) CheckCue(hotspot, true, prefix + "before New visit");
            hotspots[2].gameObject.SetActive(false);
            panel.ShowExploration(); yield return Wait(.2f); var retained = SnapshotRetained();
            yield return Press(Button(Command.NewVisit), prefix + "New visit");
            CheckEmpty(prefix + "New visit"); CheckRetained(retained, prefix + "New visit");
            CheckResult(panel.IsExploring && !panel.IsReading, prefix + "New visit returns to exploration welcome", "exploring=" + panel.IsExploring + "; reading=" + panel.IsReading);
            CheckResult(!Reference<GameObject>(tour, "completionFeedback").activeSelf, prefix + "New visit hides completion feedback", "0/3 after New visit");
            var progress = Reference<TMP_Text>(tour, "progressText"); var information = Reference<TMP_Text>(tour, "informationText");
            CheckResult(progress.text == MuseumText.Translate("0 / 3 inspected"), prefix + "progress resets in current language", progress.text);
            CheckResult(information.text == MuseumText.Translate(Prompt), prefix + "old explanation becomes current-language prompt", information.text);
            tour.AdvanceExplanation(90);
            CheckResult(Mathf.Approximately(tour.ExplanationOpacity, 1) && information.text == MuseumText.Translate(Prompt), prefix + "old explanation countdown is stopped", "opacity=" + tour.ExplanationOpacity);
            hotspots[2].gameObject.SetActive(true); tour.SetHotspotsVisible(true); yield return Wait(.1f);
            foreach (var hotspot in hotspots) CheckCue(hotspot, false, prefix + "after New visit, including reenabled hotspot");
            exploration.RestartVisit(); CheckEmpty(prefix + "repeated New visit"); CheckRetained(retained, prefix + "repeated New visit");
            inspection.ResetInspection(); yield return Wait(.1f);
        }
        static Discovery SnapshotDiscovery() => new Discovery { air = exploration.AirSeen, voice = exploration.VoiceSeen, selected = exploration.HasSelectedClue,
            hints = exploration.HintRequests, page = (int)exploration.CurrentPage, clue = (int)exploration.SelectedClue, visited = tour.VisitedCount, complete = tour.IsComplete,
            identifiers = string.Join(",", hotspots.Where(h => h.IsVisited).Select(h => h.Identifier).OrderBy(id => id, StringComparer.Ordinal)) };
        static RetainedState SnapshotRetained()
        {
            var origin = Camera.main.GetComponentInParent<XROrigin>().Origin.transform; var era = UnityEngine.Object.FindObjectOfType<HelmetEraSwitcher>();
            return new RetainedState { language = MuseumText.Channel.Current, sound = inspection.SoundEnabled, reduceMotion = inspection.ReduceMotion,
                floating = inspection.IsFloating, cutaway = inspection.IsCutaway, era = era ? era.CurrentIndex : -1, reset = inspection.ResetCount, window = inspection.WindowAngle,
                position = inspection.transform.position, rotation = inspection.transform.rotation, originPosition = origin.position, originRotation = origin.rotation };
        }
        static void CheckPreferences(RetainedState before, string prefix)
        {
            CheckResult(MuseumText.Channel.Current == before.language && inspection.SoundEnabled == before.sound && inspection.ReduceMotion == before.reduceMotion,
                prefix + " preserves language, sound and motion preferences", "language=" + MuseumText.Channel.Current + "; sound=" + inspection.SoundEnabled + "; reduced=" + inspection.ReduceMotion);
        }
        static void CheckRetained(RetainedState before, string prefix)
        {
            CheckPreferences(before, prefix); var after = SnapshotRetained();
            CheckResult(before.era == after.era && before.reset == after.reset && before.floating == after.floating && before.cutaway == after.cutaway && Mathf.Abs(before.window - after.window) < .01f,
                prefix + " preserves era and inspection mode", "era=" + after.era + "; reset=" + after.reset + "; floating=" + after.floating + "; cutaway=" + after.cutaway + "; window=" + after.window);
            CheckResult(Vector3.Distance(before.position, after.position) < .002f && Quaternion.Angle(before.rotation, after.rotation) < .1f,
                prefix + " preserves physical exhibit pose", "position=" + after.position + "; rotation=" + after.rotation.eulerAngles);
            CheckResult(Vector3.Distance(before.originPosition, after.originPosition) < .002f && Quaternion.Angle(before.originRotation, after.originRotation) < .1f,
                prefix + " preserves visitor viewpoint", "origin=" + after.originPosition + "; rotation=" + after.originRotation.eulerAngles);
        }
        static void CheckEmpty(string prefix) => CheckResult(!exploration.AirSeen && !exploration.VoiceSeen && exploration.HintRequests == 0 && !exploration.HasSelectedClue &&
            exploration.CurrentPage == HelmetExplorationController.Page.Welcome && !exploration.IsPlaying && !exploration.IsDiagramVisible && tour.VisitedCount == 0 && !tour.IsComplete,
            prefix + " clears both discovery modules and stops demonstration", JsonUtility.ToJson(SnapshotDiscovery()));
        static void CheckCue(HelmetHotspot hotspot, bool visited, string prefix)
        {
            var check = hotspot.transform.Find("State Cues/Visited Check");
            CheckResult(hotspot.IsVisited == visited && check && check.gameObject.activeSelf == visited, prefix + ": " + hotspot.Identifier + " visit and check shape", "visited=" + hotspot.IsVisited + "; check=" + (check && check.gameObject.activeSelf));
        }
        static IEnumerator LifecycleFixtures()
        {
            var holder = new GameObject("New visitor lifecycle fixtures"); holder.SetActive(false);
            try
            {
                var first = FixtureHelmet(holder.transform, "First exhibit"); var second = FixtureHelmet(holder.transform, "Second exhibit");
                var firstTour = FixtureTour(first.transform, "First tour"); var secondTour = FixtureTour(second.transform, "Second tour");
                var controllerObject = new GameObject("Lifecycle exploration"); controllerObject.transform.SetParent(holder.transform, false);
                var controller = controllerObject.AddComponent<HelmetExplorationController>();
                controller.Configure(first, null); holder.SetActive(true); yield return null;
                firstTour.Visit("first", "first", "first"); secondTour.Visit("second", "second", "second"); controller.RestartVisit();
                CheckResult(firstTour.VisitedCount == 0 && secondTour.VisitedCount == 1, "Lifecycle: initial binding resets only matching exhibit", "first=" + firstTour.VisitedCount + "; second=" + secondTour.VisitedCount);
                controller.Configure(second, null); firstTour.Visit("first", "first", "first"); controller.RestartVisit();
                CheckResult(firstTour.VisitedCount == 1 && secondTour.VisitedCount == 0, "Lifecycle: reconfiguration leaves previous exhibit intact", "first=" + firstTour.VisitedCount + "; second=" + secondTour.VisitedCount);
                controller.enabled = false; secondTour.gameObject.SetActive(false); secondTour.Visit("second", "second", "second"); controller.enabled = true; controller.RestartVisit();
                CheckResult(firstTour.VisitedCount == 1 && secondTour.VisitedCount == 0, "Lifecycle: reenable can reset its inactive tour", "Previous exhibit is untouched.");
                controller.Configure(null, null); controller.SelectClue(HelmetExplorationController.Clue.Air); controller.RequestHint(); controller.RestartVisit();
                CheckResult(!controller.AirSeen && controller.HintRequests == 0 && firstTour.VisitedCount == 1, "Lifecycle: no inspection/tour standalone remains usable", "Exploration clears without a global tour search.");
                var late = FixtureHelmet(holder.transform, "Late tour exhibit"); controller.Configure(late, null);
                controller.SelectClue(HelmetExplorationController.Clue.Voice); controller.RequestHint(); controller.RestartVisit();
                CheckResult(!controller.VoiceSeen && controller.HintRequests == 0 && firstTour.VisitedCount == 1, "Lifecycle: inspection with no tour remains usable", "No unrelated exhibit is reset.");
                var lateTour = FixtureTour(late.transform, "Late tour"); lateTour.Visit("late", "late", "late"); controller.RestartVisit();
                CheckResult(lateTour.VisitedCount == 0 && firstTour.VisitedCount == 1, "Lifecycle: tour created after Configure is found on New visit", "No initial tour binding required.");
                var ambiguous = FixtureHelmet(holder.transform, "Ambiguous exhibit"); var a = FixtureTour(ambiguous.transform, "A"); var b = FixtureTour(ambiguous.transform, "B");
                controller.Configure(ambiguous, null); a.Visit("a", "a", "a"); b.Visit("b", "b", "b"); controller.RestartVisit();
                CheckResult(a.VisitedCount == 1 && b.VisitedCount == 1, "Lifecycle: ambiguous tours are never chosen arbitrarily", "Both discoveries retained until explicitly bound.");
                var serialized = new SerializedObject(controller); serialized.FindProperty("visitTour").objectReferenceValue = b; serialized.ApplyModifiedPropertiesWithoutUndo(); controller.RestartVisit();
                CheckResult(a.VisitedCount == 1 && b.VisitedCount == 0, "Lifecycle: explicit valid tour resolves ambiguity", "Only B resets.");
                serialized.Update(); serialized.FindProperty("visitTour").objectReferenceValue = firstTour; serialized.ApplyModifiedPropertiesWithoutUndo(); controller.RestartVisit();
                CheckResult(firstTour.VisitedCount == 1 && a.VisitedCount == 1, "Lifecycle: invalid explicit reference cannot clear another exhibit", "Foreign tour rejected.");
            }
            finally { UnityEngine.Object.Destroy(holder); }
            yield return null;
        }
        static HelmetInspectionController FixtureHelmet(Transform parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.SetActive(false); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, 10, 0);
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var controller = go.AddComponent<HelmetInspectionController>(); go.SetActive(true); return controller;
        }
        static HelmetHotspotTour FixtureTour(Transform parent, string name)
        {
            var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(parent, false);
            var result = go.AddComponent<HelmetHotspotTour>();
            // This binding fixture must not alter the scene's shared interactor query settings.
            var serialized = new SerializedObject(result); serialized.FindProperty("enableTriggerQueries").boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true); return result;
        }
        static T Reference<T>(UnityEngine.Object target, string name) where T : UnityEngine.Object => new SerializedObject(target).FindProperty(name).objectReferenceValue as T;
        static XRSimpleInteractable Button(Command action) => panel.GetComponentsInChildren<HelmetExplorationButton>(true).Single(b => b.Command == action).GetComponent<XRSimpleInteractable>();
        static IEnumerator Press(XRSimpleInteractable button, string name)
        {
            var collider = button.GetComponent<Collider>(); int events = 0; string interactor = "";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; interactor = args.interactorObject.GetType().Name; };
            button.selectEntered.AddListener(listener);
            try
            {
                CheckResult(button.gameObject.activeInHierarchy, name + " is visible before selection", button.name);
                var touch = collider.bounds.center - button.transform.forward * .55f; var rotation = Quaternion.LookRotation(collider.bounds.center - touch, Vector3.up);
                QueueController(rightDevice, touch, rotation, false); yield return Wait(.3f);
                CheckResult(button.isHovered, name + " ray hover", "Hovered=" + button.isHovered);
                touch = collider.bounds.center - button.transform.forward * .55f; rotation = Quaternion.LookRotation(collider.bounds.center - touch, Vector3.up);
                QueueController(rightDevice, touch, rotation, true); yield return Wait(.2f);
                CheckResult(events == 1 && interactor == nameof(XRRayInteractor), name + " selected once through production ray", "Events=" + events + "; interactor=" + interactor);
                QueueController(rightDevice, touch, rotation, false); yield return Wait(.2f); ParkHands(); yield return Wait(.15f);
            }
            finally { button.selectEntered.RemoveListener(listener); }
        }
        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip)
        {
            var parent = (device == leftDevice ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(world), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation, grip = grip ? 1 : 0 }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        { QueueController(rightDevice, new Vector3(.7f, .8f, 1), Quaternion.identity, false); QueueController(leftDevice, new Vector3(-.7f, .8f, 1), Quaternion.identity, false); }
        static void QueueHead()
        {
            var state = new XRSimulatedHMDState { isTracked = true, trackingState = 3, devicePosition = new Vector3(0, 1.65f, 0), centerEyePosition = new Vector3(0, 1.65f, 0),
                leftEyePosition = new Vector3(-.032f, 1.65f, 0), rightEyePosition = new Vector3(.032f, 1.65f, 0), deviceRotation = Quaternion.identity,
                centerEyeRotation = Quaternion.identity, leftEyeRotation = Quaternion.identity, rightEyeRotation = Quaternion.identity };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(hmd, state));
        }
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        static void CheckResult(bool passed, string name, string detail) => report.checks.Add(new Check { passed = passed, name = name, detail = detail });
        static void OnLog(string condition, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(condition); }
        static void Finish(string failure)
        {
            if (finishing) return; finishing = true; report.failure = failure; report.finishedUtc = DateTime.UtcNow.ToString("o");
            Application.logMessageReceived -= OnLog; InputSystem.onBeforeUpdate -= FlushInput; PendingInput.Clear();
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, hmd, keyboard, addedMouse ? mouse : null }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedSettings) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; changedSettings = false; }
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
            bool passed = failure == null && report.errors.Count == 0 && report.checks.All(c => c.passed);
            SessionState.SetInt(Prefix + "Exit", passed ? 0 : 1);
            Debug.Log("New visitor reset QA: " + (passed ? "PASS" : "FAIL") + "; checks=" + report.checks.Count + "; output=" + Output);
            EditorApplication.ExitPlaymode();
        }
        static string Git(string arguments)
        {
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments) { WorkingDirectory = RepoRoot, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }))
            { string result = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit(); return result; }
        }
    }
}
#endif
