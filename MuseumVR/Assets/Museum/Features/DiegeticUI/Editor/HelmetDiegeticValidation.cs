#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Museum.Eras;
using Museum.Exploration;
using Museum.Hotspots;
using Museum.Inspection;
using Museum.Localization;
using Museum.Refined;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Diegetic.Editor
{
    /// <summary>Tests the saved product scene. Never installs, generates or saves product assets.</summary>
    [InitializeOnLoad]
    public static class HelmetDiegeticValidation
    {
        const string Prefix = "Museum.DiegeticQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static HelmetDiegeticStation station;
        static HelmetInspectionController inspection;
        static HelmetEraSwitcher eras;
        static RefinedHelmetBinding binding;
        static HelmetConnectionDemonstration demo;
        static ActionBasedController rightController, leftController;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD head;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedMouse, changedSettings, finishing;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static double started, exitAt;
        static int lastFrame = -1, pendingExit = -1;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", SessionState.GetString(Prefix + "Run", "missing"));

        [Serializable] sealed class Check { public string name, detail, layer; public bool passed; }
        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class HotspotRecord { public string id; public Vector3 position; public bool enabled; }
        [Serializable] sealed class Report
        {
            public string utc, finishedUtc, commit, dirty, unity, failure;
            public string method = "Saved MuseumStart Play Mode: public command and gesture-calculation fixtures, plus queued synthetic right controller/HMD states in ordinary Dynamic Input System updates through production Input Actions and XR ray interactions. Production-ray checks are explicitly separate from public-API fixtures. No direct SelectEnter/SelectExit, InputSystem.Update, scene saves or prototype generator.";
            public string limitations = "No real headset/controller, comfort, reachability study, user test or executable build validation. RenderTexture images are diagnostic views of the product scene, not headset screenshots. Direct physical hand interaction is not independently validated by this runner.";
            public List<Check> checks = new List<Check>();
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<HotspotRecord> originalHotspots = new List<HotspotRecord>();
            public List<string> errors = new List<string>();
            public List<string> diagnosticCaptures = new List<string>();
        }

        static HelmetDiegeticValidation()
        { EditorApplication.playModeStateChanged += OnPlayMode; EditorApplication.update += Tick; }
        [MenuItem("Museum/Diegetic UI/Validate saved main scene")]
        public static void RunInEditor() => Begin();
        public static void RunBatch() => Begin();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            // The official simulator resolves keyboard/mouse bindings in Start.
            keyboard = InputSystem.AddDevice<Keyboard>("Diegetic QA keyboard");
            mouse = Mouse.current; addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>("Diegetic QA mouse");
        }
        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validation.");
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetString(Prefix + "Run", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-precision-close-hints-20261017-" + Guid.NewGuid().ToString("N"));
            report = new Report { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion };
            foreach (string path in new[] { ScenePath, "Packages/manifest.json", "Packages/packages-lock.json" }
                .Concat(Directory.GetFiles("Assets/Museum/Features/DiegeticUI", "*", SearchOption.AllDirectories))
                .Concat(new[] { "Assets/Museum/Features/HelmetInspection/Runtime/HelmetInspectionController.cs", "Assets/Museum/Features/HelmetExploration/Runtime/HelmetConnectionDemonstration.cs",
                    "Assets/Museum/Features/RefinedModels/Runtime/RefinedConnectionEquipment.cs", "Assets/Museum/Features/VisitorFlow/Runtime/VisitorViewRecovery.cs",
                    "Assets/Museum/Features/Localization/Tables/MuseumTranslations.json", "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset",
                    "Assets/Museum/Features/Localization/Fonts/NotoSansSC-MuseumSubset SDF.asset", "Assets/Museum/Features/Localization/Fonts/characters-required.txt" }).Where(File.Exists).OrderBy(p => p))
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                    report.files.Add(new Fingerprint { path = path.Replace('\\', '/'), sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() });
            var savedInspection = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            if (savedInspection)
                foreach (var hotspot in savedInspection.GetComponentsInChildren<HelmetHotspot>(true))
                    report.originalHotspots.Add(new HotspotRecord { id = hotspot.Identifier, position = savedInspection.transform.InverseTransformPoint(hotspot.transform.position), enabled = hotspot.enabled });
            SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
            SessionState.SetBool(Prefix + "Active", true);
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
                if (Application.isBatchMode) { pendingExit = SessionState.GetInt(Prefix + "Exit", 1); exitAt = EditorApplication.timeSinceStartup + 5; }
            }
        }
        static void Tick()
        {
            if (pendingExit >= 0 && EditorApplication.timeSinceStartup >= exitAt) { int code = pendingExit; pendingExit = -1; EditorApplication.Exit(code); return; }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 300) { Finish("Validation exceeded 300 seconds."); return; }
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
            station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
            CheckResult(station && station.IsReady, "Saved scene starts installed diegetic station", station ? "Ready=" + station.IsReady : "Station missing");
            if (!station || !station.IsReady) throw new InvalidOperationException("Diegetic station was not ready in the saved product scene.");
            CheckResult(!station.RotationHelpLearned && !station.RotationHintsCondensed,
                "New visitor starts with complete rotation guidance", "Learned=" + station.RotationHelpLearned + "; Condensed=" + station.RotationHintsCondensed);
            inspection = station.Inspection; demo = station.Demonstration;
            eras = inspection.GetComponent<HelmetEraSwitcher>(); binding = inspection.GetComponent<RefinedHelmetBinding>();
            CheckResult(station.Controls.Count == Enum.GetValues(typeof(HelmetDiegeticStation.Command)).Length, "All commands retain registered interaction metadata", "Count=" + station.Controls.Count);
            CheckResult(!UnityEngine.Object.FindObjectsOfType<HelmetInspectionPalette>(true).Any(p => p.gameObject.activeInHierarchy), "No old following palette remains active", "Retired presentation, existing content engines retained.");
            var hotspots = inspection.GetComponentsInChildren<HelmetHotspot>(true);
            CheckResult(hotspots.Length == 3 && hotspots.All(h => h.enabled), "Three original knowledge hotspots remain enabled", string.Join(",", hotspots.Select(h => h.Identifier)));
            foreach (var original in report.originalHotspots)
            {
                var hotspot = hotspots.SingleOrDefault(h => h.Identifier == original.id);
                CheckResult(hotspot && Vector3.Distance(inspection.transform.InverseTransformPoint(hotspot.transform.position), original.position) < .0001f,
                    "Knowledge hotspot position preserved: " + original.id, hotspot ? hotspot.transform.localPosition.ToString("F4") : "Missing");
            }
            CheckResult(!inspection.GetComponent<XRGrabInteractable>().enabled && inspection.GetComponent<Rigidbody>().isKinematic, "Exhibit cannot be freely translated or dropped", "Legacy grab disabled; rigidbody pinned.");
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Left Controller");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            head = InputSystem.AddDevice<XRSimulatedHMD>(); InputSystem.EnableDevice(keyboard); if (addedMouse) InputSystem.EnableDevice(mouse);
            QueueHead(); ParkHands(); yield return Wait(.5f);
            MuseumText.Channel.SetLanguage(MuseumLanguage.English);
            // Explicitly exercise the normal animation mode first.
            inspection.ReduceMotion = false;
            if (station.AlwaysShowHints) station.Execute(HelmetDiegeticStation.Command.Hints);
            yield return Wait(1.1f);
            Capture("01-main-view", new Vector3(0, .15f, -1.55f));
            Capture("02-close-observation", new Vector3(-.44f, .24f, -.83f));
            yield return BasicCommands();
            yield return GestureFixtures();
            yield return RotationPrecisionFixtures();
            yield return StableSectionRailFixtures();
            yield return Demonstrations();
            yield return AnchoredDemonstrationFixtures();
            yield return ProductionEquipmentRotation();
            yield return PreferencesAndReset();
            yield return ViewHeightFixtures();
            yield return EraFixtures();
            yield return ProductionRayCommands();
            yield return ProductionReleaseFeedback();
            yield return KnowledgeHotspots();
            yield return ProductionRayGesture();
            yield return RotationHintFixtures();
            yield return ProductionRaySection();
            yield return CrossTargetReleaseGuard();
            yield return ProductionViewRecoveryWhileHeld();
            yield return ModuleLifecycle();
            yield return FadeAndLanguage();
            yield return ViewRecovery();
        }

        static IEnumerator BasicCommands()
        {
            station.Execute(HelmetDiegeticStation.Command.Window); yield return Wait(.7f);
            CheckResult(inspection.WindowAngle > 90, "Click command opens front window", "Angle=" + inspection.WindowAngle);
            CheckResult(inspection.WindowTargetOpen && station.Controls[HelmetDiegeticStation.Command.Window].Caption.GetComponent<LocalizedSourceText>().SourceEnglish=="Click to close window", "Window hint describes authoritative next click", "Target open="+inspection.WindowTargetOpen);
            station.Execute(HelmetDiegeticStation.Command.Window); yield return Wait(.7f);
            CheckResult(inspection.WindowAngle < .1f, "Second click closes front window", "Angle=" + inspection.WindowAngle);
            station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.1f);
            CheckResult(inspection.IsCutaway && station.SectionGesture.gameObject.activeInHierarchy, "Section click reveals cut and translation handle", "Cut=" + inspection.IsCutaway);
            Capture("03-section", new Vector3(-.47f, .20f, -.95f));
            station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.1f);
            CheckResult(!inspection.IsCutaway && !station.SectionGesture.gameObject.activeInHierarchy, "Second section click restores full exterior", "Cut=" + inspection.IsCutaway);
            station.Execute(HelmetDiegeticStation.Command.Settings); yield return Wait(.1f);
            CheckResult(station.SettingsVisible, "Fixed settings opens", "Settings=" + station.SettingsVisible);
            station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.1f);
            foreach (var command in new[] { HelmetDiegeticStation.Command.Section, HelmetDiegeticStation.Command.Slide })
            {
                var control = station.Controls[command];
                CheckResult(control.HasSymbol && !control.ContextualOnly,
                    "Operation has graphical affordance: " + command, "Symbol=" + control.HasSymbol);
            }
            var window = station.Controls[HelmetDiegeticStation.Command.Window];
            CheckResult(window.ContextualOnly && !window.HasSymbol,
                "Actual front window uses contextual hint without a permanent operation tile", "ContextualOnly=" + window.ContextualOnly + "; Symbol=" + window.HasSymbol);
            var rotation = station.Controls[HelmetDiegeticStation.Command.Rotate];
            CheckResult(rotation.ContextualOnly && !rotation.HasSymbol && !rotation.GetComponentsInChildren<Collider>().Any(c => c.enabled && c.gameObject.activeInHierarchy),
                "Whole helmet is the rotation target without a duplicate button", "ContextualOnly=" + rotation.ContextualOnly + "; Symbol=" + rotation.HasSymbol + "; Body colliders=" + rotation.Target.colliders.Count);
            CheckResult(station.SectionRail && station.SectionRailHalfTravel > .01f,
                "Section has a visible fixed one-dimensional rail", "Rail=" + (station.SectionRail ? HierarchyPath(station.SectionRail) : "missing") + "; Half travel=" + station.SectionRailHalfTravel);
            foreach (var pair in station.Controls)
                foreach (var collider in pair.Value.Target.colliders.Where(c => c && c.enabled && c.gameObject.activeInHierarchy))
                {
                    IXRInteractable registered;
                    bool found = pair.Value.Target.interactionManager.TryGetInteractableForCollider(collider, out registered);
                    CheckResult(found && ReferenceEquals(registered, pair.Value.Target), "XRI collider registered for " + pair.Key,
                        "Registered=" + found + "; Target=" + (registered is UnityEngine.Object value ? value.name : "null"), "xri-registration");
                }
            station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.1f);
            station.Execute(HelmetDiegeticStation.Command.About); yield return Wait(.1f);
            CheckResult(station.AboutVisible && !station.SettingsVisible, "About opens exclusively", "About=" + station.AboutVisible + "; Settings=" + station.SettingsVisible);
            station.Execute(HelmetDiegeticStation.Command.About); yield return Wait(.1f);
            CheckResult(!station.AboutVisible, "About closes on same click", "About=" + station.AboutVisible);
        }
        static IEnumerator GestureFixtures()
        {
            var centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            var hand = centre + new Vector3(.12f, .12f, -.35f);
            // Test the user's visible result rather than expecting a particular
            // Euler axis. An arbitrarily oriented exhibit must follow a planar
            // drag from the front, side and back in the viewer's own image axes.
            var specimenStart = inspection.transform.rotation;
            foreach (float viewYaw in new[] { 0f, 90f, 180f })
            {
                var view = Quaternion.Euler(0, viewYaw, 0);
                foreach (var planarAxis in new[] { Vector3.right, Vector3.up })
                {
                    var before = inspection.transform.rotation;
                    var surfaceLocal = inspection.transform.InverseTransformPoint(centre - view * Vector3.forward * .2f);
                    var surfaceBefore = inspection.transform.TransformPoint(surfaceLocal);
                    station.RotationGesture.BeginForValidation(hand, view);
                    CheckResult(station.RotationGesture.IsDragging && Quaternion.Angle(before, inspection.transform.rotation) < .001f,
                        "View-relative gesture begins without snap: " + viewYaw + "/" + planarAxis, "Calculation fixture, not XR input.");
                    station.RotationGesture.ApplyForValidation(hand + view * planarAxis * .08f);
                    var surfaceMovement = Quaternion.Inverse(view) * (inspection.transform.TransformPoint(surfaceLocal) - surfaceBefore);
                    float intended = Vector3.Dot(surfaceMovement, planarAxis);
                    float crossAxis = Vector3.Dot(surfaceMovement, planarAxis == Vector3.right ? Vector3.up : Vector3.right);
                    CheckResult(intended > .015f && Mathf.Abs(crossAxis) < .005f,
                        "Visible surface follows viewed drag: " + viewYaw + "/" + planarAxis,
                        "Movement in view=" + surfaceMovement.ToString("F5") + "; Intended=" + intended);
                    CheckResult(Vector3.Distance(centre, inspection.transform.TransformPoint(inspection.SectionCenterLocal)) < .0001f,
                        "View-relative rotation pins actual centre: " + viewYaw + "/" + planarAxis, inspection.transform.TransformPoint(inspection.SectionCenterLocal).ToString("F5"));
                    var windowAngle = inspection.WindowAngle;
                    station.Execute(HelmetDiegeticStation.Command.Window);
                    CheckResult(Mathf.Abs(inspection.WindowAngle - windowAngle) < .001f,
                        "Dragging excludes simultaneous window click: " + viewYaw + "/" + planarAxis, "Window stays unchanged during gesture.");
                    station.RotationGesture.EndForValidation();
                    var stopped = inspection.transform.rotation;
                    station.RotationGesture.ApplyForValidation(hand + view * planarAxis * .25f);
                    CheckResult(Quaternion.Angle(stopped, inspection.transform.rotation) < .001f,
                        "Release freezes view-relative rotation: " + viewYaw + "/" + planarAxis, "Unpressed motion has no effect.");
                }
                var depthBefore = inspection.transform.rotation;
                station.RotationGesture.BeginForValidation(hand, view);
                station.RotationGesture.ApplyForValidation(hand + view * Vector3.forward * .25f);
                CheckResult(Quaternion.Angle(depthBefore, inspection.transform.rotation) < .001f,
                    "Depth movement does not create hidden roll: " + viewYaw, "Angle=" + Quaternion.Angle(depthBefore, inspection.transform.rotation));
                station.RotationGesture.EndForValidation();
            }
            var curvedBefore = inspection.transform.rotation;
            station.RotationGesture.BeginForValidation(hand, Quaternion.identity);
            foreach (var point in new[] { new Vector3(.08f, 0, 0), new Vector3(.08f, .08f, 0), new Vector3(0, .08f, 0), Vector3.zero })
                station.RotationGesture.ApplyForValidation(hand + point);
            var curvedDelta = inspection.transform.rotation * Quaternion.Inverse(curvedBefore);
            var curvedRight = curvedDelta * Vector3.right;
            CheckResult(Mathf.Abs(curvedRight.y) > .015f, "Curved planar drag can produce visible roll", "Rotated view-right=" + curvedRight.ToString("F5"));
            CheckResult(Vector3.Distance(centre, inspection.transform.TransformPoint(inspection.SectionCenterLocal)) < .0001f,
                "Curved planar drag keeps centre pinned", inspection.transform.TransformPoint(inspection.SectionCenterLocal).ToString("F5"));
            station.RotationGesture.EndForValidation();
            var released = inspection.transform.rotation;
            station.RotationGesture.BeginForValidation(hand + Vector3.one, Quaternion.Euler(20, 145, 12));
            station.RotationGesture.ApplyForValidation(hand + Vector3.one);
            CheckResult(Quaternion.Angle(released, inspection.transform.rotation) < .001f,
                "Regrabbing after curved drag never snaps to previous anchor", "Angle=" + Quaternion.Angle(released, inspection.transform.rotation));
            station.RotationGesture.EndForValidation();
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
            CheckResult(Quaternion.Angle(specimenStart, inspection.transform.rotation) < .001f, "Reset restores original orientation after all view fixtures", "Angle=" + Quaternion.Angle(specimenStart, inspection.transform.rotation));
            station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.1f);
            inspection.SetSectionPlane(Vector3.right, 0);
            var normal = inspection.SectionNormalLocal;
            hand = station.SectionGesture.transform.position;
            station.SectionGesture.BeginForValidation(hand, Camera.main.transform.rotation);
            var offset = inspection.SectionOffset;
            station.SectionGesture.ApplyForValidation(hand + station.SectionRail.up * .2f);
            CheckResult(Mathf.Abs(inspection.SectionOffset - offset) < .0001f, "Off-rail gesture does not move cut", "Offset=" + inspection.SectionOffset);
            station.SectionGesture.ApplyForValidation(hand + station.SectionRail.right * .045f);
            float expectedOffset = offset + .045f * inspection.SectionRange / station.SectionRailHalfTravel;
            CheckResult(Mathf.Abs(inspection.SectionOffset - expectedOffset) < .0001f, "Visible rail direction translates cut at matching scale", "Expected=" + expectedOffset + "; Offset=" + inspection.SectionOffset);
            CheckResult(Vector3.Distance(normal, inspection.SectionNormalLocal) < .0001f, "Cut direction remains unchanged", inspection.SectionNormalLocal.ToString("F5"));
            station.SectionGesture.ApplyForValidation(hand + station.SectionRail.right * 100);
            CheckResult(Mathf.Abs(inspection.SectionOffset - inspection.SectionRange) < .0001f, "Cut translation clamps positive extent", "Offset=" + inspection.SectionOffset + "; Range=" + inspection.SectionRange);
            station.SectionGesture.ApplyForValidation(hand - station.SectionRail.right * 100);
            CheckResult(Mathf.Abs(inspection.SectionOffset + inspection.SectionRange) < .0001f, "Cut translation clamps negative extent", "Offset=" + inspection.SectionOffset);
            station.SectionGesture.EndForValidation();
            var remembered = inspection.SectionOffset;
            station.Execute(HelmetDiegeticStation.Command.Section); station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.1f);
            CheckResult(Mathf.Abs(inspection.SectionOffset - remembered) < .0001f, "Reopening cut retains previous position", "Offset=" + inspection.SectionOffset);
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
        }
        struct RotationTrial
        {
            public float Angle, Gain;
            public Quaternion Delta;
        }
        static float RotationDistance(Quaternion first, Quaternion second)
        {
            var delta = Quaternion.Inverse(first) * second;
            return 2f * Mathf.Rad2Deg * Mathf.Atan2(new Vector3(delta.x, delta.y, delta.z).magnitude, Mathf.Abs(delta.w));
        }
        static RotationTrial SampleRotationPath(HelmetGestureInteractable gesture, Vector3 hand, float distance, float seconds, int framesPerSecond)
        {
            var root = gesture.TargetRoot;
            var before = root.rotation;
            gesture.BeginForValidation(hand, Quaternion.identity);
            int steps = Mathf.RoundToInt(seconds * framesPerSecond);
            float deltaTime = seconds / steps;
            for (int i = 1; i <= steps; i++)
                gesture.ApplyForValidation(hand + Vector3.right * distance * i / steps, deltaTime);
            var result = new RotationTrial { Angle = Quaternion.Angle(before, root.rotation),
                Delta = root.rotation * Quaternion.Inverse(before), Gain = gesture.CurrentRotationGainMultiplier };
            gesture.EndForValidation();
            return result;
        }
        static IEnumerator RotationPrecisionFixtures()
        {
            // These calculation fixtures vary the supplied sample duration, not
            // Unity's global time scale. Production input regression remains below.
            station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.2f);
            var apparatus = demo.EquipmentExhibits.First(e => e.IsVisible);
            var equipmentGesture = apparatus.Root.GetComponentInChildren<HelmetGestureInteractable>();
            foreach (var gesture in new[] { station.RotationGesture, equipmentGesture })
            {
                string specimen = gesture == station.RotationGesture ? "helmet" : apparatus.Id;
                var root = gesture.TargetRoot;
                Vector3 centerLocal = gesture == station.RotationGesture ? inspection.SectionCenterLocal : apparatus.CenterLocal;
                var fixedCenter = root.TransformPoint(centerLocal);
                var hand = fixedCenter + Vector3.back * .35f;
                CheckResult(gesture.RotationDeadbandMetres > 0 && gesture.PrecisionGainMultiplier > 0 && gesture.PrecisionGainMultiplier < 1 &&
                        gesture.PrecisionSpeedMetresPerSecond < gesture.FullSpeedMetresPerSecond,
                    "Rotation precision parameters have a bounded low-speed range: " + specimen,
                    "Deadband=" + gesture.RotationDeadbandMetres + "; Precision gain=" + gesture.PrecisionGainMultiplier);
                gesture.BeginForValidation(hand, Quaternion.identity);
                var restingHand = hand + Vector3.right * .025f;
                gesture.ApplyForValidation(restingHand, 1f / 60f);
                for (int i = 0; i < 30; i++) gesture.ApplyForValidation(restingHand, 1f / 60f);
                var stillRotation = root.rotation;
                float maximumJitter = 0, accumulatedJitter = 0;
                for (int i = 1; i <= 180; i++)
                {
                    float time = i / 90f;
                    var beforeSample = root.rotation;
                    var noise = new Vector3(Mathf.Sin(time * Mathf.PI * 18f), Mathf.Sin(time * Mathf.PI * 22f), 0) * .0003f;
                    gesture.ApplyForValidation(restingHand + noise, 1f / 90f);
                    maximumJitter = Mathf.Max(maximumJitter, Quaternion.Angle(stillRotation, root.rotation));
                    accumulatedJitter += RotationDistance(beforeSample, root.rotation);
                }
                CheckResult(gesture.IsManipulating && maximumJitter < .07f && accumulatedJitter < .5f,
                    "Continuous filtering suppresses small stationary tremor after manipulation starts: " + specimen,
                    "Maximum angle=" + maximumJitter + "; Accumulated angle=" + accumulatedJitter);
                var stopped = root.rotation;
                for (int i = 0; i < 90; i++) gesture.ApplyForValidation(restingHand, 1f / 90f);
                CheckResult(Quaternion.Angle(stopped, root.rotation) < .001f,
                    "A stationary held hand produces no catch-up rotation: " + specimen,
                    "Angle=" + Quaternion.Angle(stopped, root.rotation));
                gesture.EndForValidation();
                var released = root.rotation;
                for (int i = 0; i < 60; i++) gesture.ApplyForValidation(restingHand + Vector3.one, 1f / 60f);
                CheckResult(!gesture.IsDragging && Quaternion.Angle(released, root.rotation) < .001f,
                    "Release stops filtered rotation immediately without a trailing drift: " + specimen,
                    "Angle=" + Quaternion.Angle(released, root.rotation));
                var slow = SampleRotationPath(gesture, hand, .06f, 6f, 90);
                var fast = SampleRotationPath(gesture, hand, .06f, .2f, 90);
                CheckResult(slow.Angle > 1f,
                    "Sub-deadband individual samples still accumulate into an intentional slow turn: " + specimen,
                    "Distance per sample=" + (.01f / 90f) + "; Total angle=" + slow.Angle);
                CheckResult(slow.Angle < fast.Angle * .65f && fast.Angle > 8f && slow.Gain < fast.Gain,
                    "The same hand path gives finer control at low speed: " + specimen,
                    "Slow angle/gain=" + slow.Angle + "/" + slow.Gain + "; Fast=" + fast.Angle + "/" + fast.Gain);
                var rates = new[] { 30, 60, 90, 120 };
                var trials = rates.Select(rate => SampleRotationPath(gesture, hand, .18f, 1.5f, rate)).ToArray();
                float maximumDifference = trials.Max(trial => Quaternion.Angle(trials[0].Delta, trial.Delta));
                float spread = trials.Max(trial => trial.Angle) - trials.Min(trial => trial.Angle);
                CheckResult(maximumDifference < 2f && spread < trials.Average(trial => trial.Angle) * .05f + .2f,
                    "Equivalent motion remains consistent at 30/60/90/120 samples per second: " + specimen,
                    "Angles=" + string.Join(",", rates.Select((rate, i) => rate + ":" + trials[i].Angle.ToString("F4"))) + "; Maximum quaternion difference=" + maximumDifference);
                gesture.BeginForValidation(hand, Quaternion.identity);
                var depthStart = root.rotation;
                for (int i = 1; i <= 90; i++) gesture.ApplyForValidation(hand + Vector3.forward * (.2f * i / 90f), 1f / 90f);
                CheckResult(!gesture.IsManipulating && Quaternion.Angle(depthStart, root.rotation) < .001f,
                    "Continuous pure depth motion cannot arm or rotate the specimen: " + specimen,
                    "Angle=" + Quaternion.Angle(depthStart, root.rotation));
                gesture.CancelGesture();
                gesture.BeginForValidation(hand + Vector3.one, Quaternion.identity);
                var regrabbed = root.rotation;
                gesture.ApplyForValidation(hand + Vector3.one, 1f / 60f);
                CheckResult(!gesture.IsManipulating && Quaternion.Angle(regrabbed, root.rotation) < .001f &&
                        Mathf.Abs(gesture.CurrentRotationGainMultiplier - gesture.PrecisionGainMultiplier) < .0001f,
                    "Cancellation clears cursor and gain history before a new grab: " + specimen,
                    "Angle=" + Quaternion.Angle(regrabbed, root.rotation) + "; Gain=" + gesture.CurrentRotationGainMultiplier);
                gesture.EndForValidation();
                CheckResult(Vector3.Distance(fixedCenter, root.TransformPoint(centerLocal)) < .001f,
                    "All precision and frame-rate fixtures preserve the specimen center: " + specimen,
                    "Center delta=" + Vector3.Distance(fixedCenter, root.TransformPoint(centerLocal)));
            }
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
        }
        static IEnumerator StableSectionRailFixtures()
        {
            var section = station.Controls[HelmetDiegeticStation.Command.Section].transform;
            Vector3 sectionPosition = section.position, railPosition = station.SectionRail.position;
            Quaternion sectionRotation = section.rotation, railRotation = station.SectionRail.rotation;
            var centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            var hand = centre + Vector3.back * .4f;
            station.RotationGesture.BeginForValidation(hand, Quaternion.identity);
            station.RotationGesture.ApplyForValidation(hand + new Vector3(.25f, .18f, 0));
            station.RotationGesture.EndForValidation(); yield return Wait(.2f);
            CheckResult(Vector3.Distance(sectionPosition, section.position) < .0001f && Quaternion.Angle(sectionRotation, section.rotation) < .001f,
                "Section switch stays in place when the helmet rotates", "Position delta=" + Vector3.Distance(sectionPosition, section.position) + "; Angle=" + Quaternion.Angle(sectionRotation, section.rotation));
            CheckResult(Vector3.Distance(railPosition, station.SectionRail.position) < .0001f && Quaternion.Angle(railRotation, station.SectionRail.rotation) < .001f,
                "Section rail stays anchored when the helmet rotates", "Position delta=" + Vector3.Distance(railPosition, station.SectionRail.position) + "; Angle=" + Quaternion.Angle(railRotation, station.SectionRail.rotation));
            station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.15f);
            inspection.SetSectionPlane(Vector3.right, 0); yield return Wait(.1f);
            Vector3 normal = inspection.SectionNormalLocal;
            hand = station.SectionGesture.transform.position;
            Vector3 railStart = station.SectionRail.InverseTransformPoint(hand);
            station.SectionGesture.BeginForValidation(hand, Camera.main.transform.rotation);
            station.SectionGesture.ApplyForValidation(hand + station.SectionRail.right * .06f);
            station.SectionGesture.EndForValidation(); yield return Wait(.1f);
            var railEnd = station.SectionRail.InverseTransformPoint(station.SectionGesture.transform.position);
            CheckResult(Mathf.Abs(inspection.SectionOffset - .06f * inspection.SectionRange / station.SectionRailHalfTravel) < .0001f && Vector3.Distance(normal, inspection.SectionNormalLocal) < .0001f,
                "Rail keeps its visible drag direction after arbitrary model rotation", "Offset=" + inspection.SectionOffset + "; Normal=" + inspection.SectionNormalLocal);
            CheckResult(Mathf.Abs(railEnd.x - railStart.x - .06f) < .0001f && Mathf.Abs(railEnd.y - railStart.y) < .0001f && Mathf.Abs(railEnd.z - railStart.z) < .0001f,
                "Visible section handle follows hand travel on the fixed rail", "Handle travel=" + (railEnd - railStart).ToString("F5"));
            float remembered = inspection.SectionOffset;
            Vector3 handlePosition = station.SectionGesture.transform.position;
            station.RotationGesture.BeginForValidation(hand, Quaternion.identity);
            station.RotationGesture.ApplyForValidation(hand + new Vector3(-.16f, .12f, 0));
            station.RotationGesture.EndForValidation(); yield return Wait(.15f);
            CheckResult(Vector3.Distance(handlePosition, station.SectionGesture.transform.position) < .0001f && Mathf.Abs(remembered - inspection.SectionOffset) < .0001f,
                "Helmet rotation neither shifts the rail handle nor changes cut position", "Handle delta=" + Vector3.Distance(handlePosition, station.SectionGesture.transform.position));
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
        }
        static IEnumerator Demonstrations()
        {
            station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(2);
            CheckResult(demo.IsVisible && demo.IsPlaying && demo.CurrentClue == HelmetExplorationController.Clue.Air, "Air click starts animated equipment", "Clue=" + demo.CurrentClue + "; Stage=" + demo.AnimationStage);
            CheckResult(demo.IsUsingRefinedEquipment, "Existing refined equipment remains bound", "Ready=" + demo.IsUsingRefinedEquipment);
            Capture("04-air-demo", new Vector3(-.6f, .22f, -1.4f));
            station.Execute(HelmetDiegeticStation.Command.Voice); yield return Wait(3);
            CheckResult(demo.IsVisible && demo.CurrentClue == HelmetExplorationController.Clue.Voice, "Voice click replaces air exclusively", "Current clue=" + demo.CurrentClue);
            CheckResult(station.Controls[HelmetDiegeticStation.Command.Voice].IsEmphasized && !station.Controls[HelmetDiegeticStation.Command.Air].IsEmphasized, "Visible demo keeps its close entry clear", "Voice emphasized; air released.");
            Capture("05-voice-demo", new Vector3(.64f, .20f, -1.4f));
            var close=station.Controls[HelmetDiegeticStation.Command.CloseDemo];
            CheckResult(close.gameObject.activeInHierarchy,"Visible demo exposes its fixed close entry","Active="+close.gameObject.activeInHierarchy);
            var closePosition=close.transform.position;var centerBefore=inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            station.RotationGesture.BeginForValidation(Vector3.zero,Camera.main.transform.rotation);
            station.RotationGesture.ApplyForValidation(Camera.main.transform.right*.12f);station.RotationGesture.EndForValidation();yield return Wait(.1f);
            CheckResult(Vector3.Distance(closePosition,close.transform.position)<.0001f && Vector3.Distance(centerBefore,inspection.transform.TransformPoint(inspection.SectionCenterLocal))<.0001f,
                "Demo close entry stays fixed when the helmet turns","Close delta="+Vector3.Distance(closePosition,close.transform.position));
            yield return RayClick(HelmetDiegeticStation.Command.CloseDemo);
            CheckResult(!demo.IsVisible && !close.gameObject.activeInHierarchy,"Production ray closes demo through fixed entry","Visible="+demo.IsVisible,"production-input");
            station.Execute(HelmetDiegeticStation.Command.Voice);yield return Wait(.2f);
            station.Execute(HelmetDiegeticStation.Command.Voice); yield return Wait(.2f);
            CheckResult(!demo.IsVisible && !demo.IsPlaying, "Same voice click closes equipment", "Visible=" + demo.IsVisible);
            station.Execute(HelmetDiegeticStation.Command.Voice); yield return Wait(.3f);
            CheckResult(demo.IsVisible && demo.NormalizedProgress < .06f, "Reopening replay starts from beginning", "Progress=" + demo.NormalizedProgress);
            station.Execute(HelmetDiegeticStation.Command.Voice); station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.2f);
            station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.1f);
            CheckResult(!demo.IsVisible, "Same air click closes equipment", "Visible=" + demo.IsVisible);
        }
        static IEnumerator PreferencesAndReset()
        {
            station.Execute(HelmetDiegeticStation.Command.Traditional);
            station.Execute(HelmetDiegeticStation.Command.Sound); station.Execute(HelmetDiegeticStation.Command.Motion); station.Execute(HelmetDiegeticStation.Command.AlwaysHints);
            int era = eras.CurrentIndex; var language = MuseumText.Channel.Current;
            bool sound = inspection.SoundEnabled, motion = inspection.ReduceMotion, hints = station.AlwaysShowHints;
            station.Execute(HelmetDiegeticStation.Command.Window); station.Execute(HelmetDiegeticStation.Command.Section); station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.1f);
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
            CheckResult(inspection.WindowAngle < .1f && !inspection.IsCutaway && !demo.IsVisible, "Reset closes window, cut and demo", "Angle=" + inspection.WindowAngle + "; Cut=" + inspection.IsCutaway + "; Demo=" + demo.IsVisible);
            CheckResult(eras.CurrentIndex == era && MuseumText.Channel.Current == language && inspection.SoundEnabled == sound && inspection.ReduceMotion == motion && station.AlwaysShowHints == hints,
                "Reset retains era, language, sound, motion and hint preferences", "Era=" + era + "; Language=" + language + "; Sound=" + sound + "; Motion=" + motion + "; Hints=" + hints);
            inspection.ReduceMotion = false; inspection.SoundEnabled = true;
            MuseumText.Channel.SetLanguage(MuseumLanguage.English);
            if (station.AlwaysShowHints) station.Execute(HelmetDiegeticStation.Command.Hints);
        }
        static IEnumerator ViewHeightFixtures()
        {
            var height = station.ViewHeight;
            CheckResult(height && height.IsReady, "Saved station provides an initialized view-height preference", height ? "Offset=" + height.OffsetMetres : "Missing");
            if (!height || !height.IsReady) yield break;
            height.ResetHeight(); yield return Wait(.1f);
            var camera = Camera.main.transform;
            var origin = camera.GetComponentInParent<XROrigin>();
            var floor = origin.CameraFloorOffsetObject.transform;
            Vector3 trackedPosition = camera.localPosition, floorPosition = floor.localPosition;
            Quaternion trackedRotation = camera.localRotation, floorRotation = floor.localRotation;
            Vector3 originPosition = origin.Origin.transform.position;
            var plinth = UnityEngine.Object.FindObjectsOfType<MeshRenderer>().First(r => r.name == "Plinth");
            Vector3 plinthPosition = plinth.transform.position;
            Vector3 helmetCenter = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            float viewY = camera.position.y;
            if (!station.SettingsVisible) station.Execute(HelmetDiegeticStation.Command.Settings);
            yield return Wait(.2f);
            yield return RayClick(HelmetDiegeticStation.Command.HeightUp);
            CheckResult(Mathf.Abs(height.OffsetMetres - VisitorViewHeight.StepMetres) < .0001f && Mathf.Abs(camera.position.y - viewY - VisitorViewHeight.StepMetres) < .001f,
                "Production settings click raises the view by the advertised step", "Offset=" + height.OffsetMetres + "; View delta=" + (camera.position.y - viewY), "production-input");
            CheckResult(Vector3.Distance(trackedPosition, camera.localPosition) < .0001f && Quaternion.Angle(trackedRotation, camera.localRotation) < .001f,
                "Height preference preserves the tracked camera's local pose", "Local position delta=" + Vector3.Distance(trackedPosition, camera.localPosition));
            CheckResult(Vector3.Distance(floorPosition, floor.localPosition) < .0001f && Quaternion.Angle(floorRotation, floor.localRotation) < .001f && Vector3.Distance(originPosition, origin.Origin.transform.position) < .0001f,
                "Height preference leaves floor calibration and locomotion origin unchanged", "Floor delta=" + Vector3.Distance(floorPosition, floor.localPosition) + "; Origin delta=" + Vector3.Distance(originPosition, origin.Origin.transform.position));
            CheckResult(Vector3.Distance(plinthPosition, plinth.transform.position) < .0001f && Vector3.Distance(helmetCenter, inspection.transform.TransformPoint(inspection.SectionCenterLocal)) < .0001f,
                "Height preference moves neither the museum nor the exhibit", "Plinth and helmet center unchanged.");
            yield return RayClick(HelmetDiegeticStation.Command.HeightDown);
            CheckResult(Mathf.Abs(height.OffsetMetres) < .0001f && Mathf.Abs(camera.position.y - viewY) < .001f,
                "Production settings click lowers by the same step", "Offset=" + height.OffsetMetres, "production-input");
            station.Execute(HelmetDiegeticStation.Command.HeightUp); yield return Wait(.1f);
            yield return RayClick(HelmetDiegeticStation.Command.HeightReset);
            CheckResult(Mathf.Abs(height.OffsetMetres) < .0001f && Mathf.Abs(camera.position.y - viewY) < .001f,
                "Production settings click restores the original viewing height", "Offset=" + height.OffsetMetres, "production-input");
            // Keep both tracked hands stationary for this command fixture. A real
            // button click deliberately moves the aiming hand, so comparing its
            // pre-click and post-click poses would mix input motion with height.
            ParkHands(); yield return Wait(.15f);
            var trackedHands = new[] { rightController.transform, leftController.transform };
            var handLocalPositions = trackedHands.Select(hand => hand.localPosition).ToArray();
            var handLocalRotations = trackedHands.Select(hand => hand.localRotation).ToArray();
            var handViewPositions = trackedHands.Select(hand => camera.InverseTransformPoint(hand.position)).ToArray();
            var handViewRotations = trackedHands.Select(hand => Quaternion.Inverse(camera.rotation) * hand.rotation).ToArray();
            station.Execute(HelmetDiegeticStation.Command.HeightUp); yield return Wait(.1f);
            for (int i = 0; i < trackedHands.Length; i++)
            {
                var trackedHand = trackedHands[i];
                CheckResult(Vector3.Distance(handViewPositions[i], camera.InverseTransformPoint(trackedHand.position)) < .0001f &&
                        Quaternion.Angle(handViewRotations[i], Quaternion.Inverse(camera.rotation) * trackedHand.rotation) < .001f &&
                        Vector3.Distance(handLocalPositions[i], trackedHand.localPosition) < .0001f && Quaternion.Angle(handLocalRotations[i], trackedHand.localRotation) < .001f,
                    "View height preserves the controller-to-head pose and tracked local pose: " + trackedHand.name,
                    "View-space position delta=" + Vector3.Distance(handViewPositions[i], camera.InverseTransformPoint(trackedHand.position)) +
                    "; Tracked position delta=" + Vector3.Distance(handLocalPositions[i], trackedHand.localPosition));
            }
            station.Execute(HelmetDiegeticStation.Command.HeightReset); yield return Wait(.1f);
            height.Adjust(100f); yield return Wait(.1f);
            CheckResult(Mathf.Abs(height.OffsetMetres - VisitorViewHeight.MaximumOffsetMetres) < .0001f && !height.CanRaise,
                "View height clamps at its positive limit", "Offset=" + height.OffsetMetres + "; CanRaise=" + height.CanRaise);
            height.Adjust(-200f); yield return Wait(.1f);
            CheckResult(Mathf.Abs(height.OffsetMetres - VisitorViewHeight.MinimumOffsetMetres) < .0001f && !height.CanLower,
                "View height clamps at its negative limit", "Offset=" + height.OffsetMetres + "; CanLower=" + height.CanLower);
            height.ResetHeight(); height.Raise(); yield return Wait(.1f);
            float rememberedHeight = height.OffsetMetres;
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.4f);
            CheckResult(Mathf.Abs(rememberedHeight - height.OffsetMetres) < .0001f,
                "Exhibit reset retains the visitor's height preference", "Offset=" + height.OffsetMetres);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F11)));
            yield return Wait(.2f);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
            yield return Wait(.1f);
            CheckResult(Mathf.Abs(rememberedHeight - height.OffsetMetres) < .0001f && Mathf.Abs(camera.position.y - viewY - rememberedHeight) < .001f,
                "Production F11 retains the visitor's height preference", "Offset=" + height.OffsetMetres + "; View delta=" + (camera.position.y - viewY), "production-input");
            float beforeInvalid = height.OffsetMetres;
            CheckResult(!height.Adjust(float.NaN) && !height.Adjust(float.PositiveInfinity) && Mathf.Abs(height.OffsetMetres - beforeInvalid) < .0001f,
                "Invalid height input cannot corrupt the rig pose", "Offset=" + height.OffsetMetres);
            Vector3 raisedCamera = camera.position;
            height.enabled = false; yield return Wait(.1f);
            CheckResult(!height.IsReady && Vector3.Distance(camera.position, raisedCamera - Vector3.up * rememberedHeight) < .001f && Vector3.Distance(trackedPosition, camera.localPosition) < .0001f,
                "Disabling the height component removes only its preference offset", "Ready=" + height.IsReady + "; Camera=" + camera.position.ToString("F4"), "module-lifecycle");
            height.enabled = true; yield return Wait(.1f);
            CheckResult(height.IsReady && Vector3.Distance(camera.position, raisedCamera) < .001f && Mathf.Abs(height.OffsetMetres - rememberedHeight) < .0001f,
                "Reenabling height reapplies the existing preference without cumulative offsets", "Ready=" + height.IsReady + "; Offset=" + height.OffsetMetres, "module-lifecycle");
            var hand = inspection.transform.TransformPoint(inspection.SectionCenterLocal) + Vector3.back * .4f;
            station.RotationGesture.BeginForValidation(hand, camera.rotation);
            var beforeRotation = inspection.transform.rotation;
            station.Execute(HelmetDiegeticStation.Command.HeightDown); yield return Wait(.1f);
            CheckResult(!station.RotationGesture.IsDragging && Quaternion.Angle(beforeRotation, inspection.transform.rotation) < .001f,
                "Changing view height cancels an active helmet gesture before moving the tracking parent", "Dragging=" + station.RotationGesture.IsDragging);
            height.ResetHeight();
            if (station.SettingsVisible) station.Execute(HelmetDiegeticStation.Command.Settings);
            ParkHands(); yield return Wait(.2f);
        }

        static IEnumerator AnchoredDemonstrationFixtures()
        {
            foreach (var clue in new[] { HelmetExplorationController.Clue.Air, HelmetExplorationController.Clue.Voice })
            {
                station.Execute(clue == HelmetExplorationController.Clue.Air ? HelmetDiegeticStation.Command.Air : HelmetDiegeticStation.Command.Voice);
                yield return Wait(.25f);
                var equipment = demo.EquipmentExhibits.Where(e => e.IsVisible).ToArray();
                CheckResult(equipment.Length > 0, "Visible demonstration exposes manipulable equipment: " + clue, "Count=" + equipment.Length);
                var positions = equipment.Select(e => e.Root.position).ToArray();
                var rotations = equipment.Select(e => e.Root.rotation).ToArray();
                QueueHead(new Vector3(.25f, 1.72f, -.10f), Quaternion.Euler(12, 65, 0)); yield return Wait(.3f);
                for (int i = 0; i < equipment.Length; i++)
                    CheckResult(Vector3.Distance(positions[i], equipment[i].Root.position) < .0001f && Quaternion.Angle(rotations[i], equipment[i].Root.rotation) < .001f,
                        "Moving and turning the head cannot billboard equipment: " + equipment[i].Id,
                        "Position delta=" + Vector3.Distance(positions[i], equipment[i].Root.position) + "; Angle=" + Quaternion.Angle(rotations[i], equipment[i].Root.rotation));
                QueueHead(); yield return Wait(.15f);
                var hand = inspection.transform.TransformPoint(inspection.SectionCenterLocal) + Vector3.back * .35f;
                station.RotationGesture.BeginForValidation(hand, Camera.main.transform.rotation);
                station.RotationGesture.ApplyForValidation(hand + Camera.main.transform.right * .12f);
                station.RotationGesture.EndForValidation(); yield return Wait(.15f);
                for (int i = 0; i < equipment.Length; i++)
                    CheckResult(Vector3.Distance(positions[i], equipment[i].Root.position) < .0001f && Quaternion.Angle(rotations[i], equipment[i].Root.rotation) < .001f,
                        "Turning the helmet cannot carry the equipment around: " + equipment[i].Id,
                        "Position delta=" + Vector3.Distance(positions[i], equipment[i].Root.position) + "; Angle=" + Quaternion.Angle(rotations[i], equipment[i].Root.rotation));
                CheckResult(clue == HelmetExplorationController.Clue.Air
                        ? Vector3.Distance(demo.AirEndpointWorld, demo.AirTargetWorld) < .001f
                        : Vector3.Distance(demo.VoiceEndpointWorld, demo.VoiceTargetWorld) < .001f,
                    "Signal route follows the real helmet endpoint after turning: " + clue,
                    "Air endpoint error=" + Vector3.Distance(demo.AirEndpointWorld, demo.AirTargetWorld) + "; Voice endpoint error=" + Vector3.Distance(demo.VoiceEndpointWorld, demo.VoiceTargetWorld));
                station.Execute(HelmetDiegeticStation.Command.CloseDemo); yield return Wait(.1f);
                CheckResult(!demo.IsVisible && equipment.All(e => !e.IsVisible), "Closing the demonstration hides its physical equipment: " + clue, "Visible=" + demo.IsVisible);
                station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.4f);
            }
        }
        static IEnumerator ProductionEquipmentRotation()
        {
            foreach (var clue in new[] { HelmetExplorationController.Clue.Air, HelmetExplorationController.Clue.Voice })
            {
                station.Execute(clue == HelmetExplorationController.Clue.Air ? HelmetDiegeticStation.Command.Air : HelmetDiegeticStation.Command.Voice);
                yield return Wait(2.4f);
                foreach (var equipment in demo.EquipmentExhibits.Where(e => e.IsVisible).ToArray())
                {
                    var target = equipment.Root.GetComponentsInChildren<XRBaseInteractable>(true).FirstOrDefault(t => t.enabled);
                    CheckResult(target && target.colliders.Count > 0,
                        "Demonstration equipment has a registered direct-manipulation target: " + equipment.Id,
                        target ? "Colliders=" + target.colliders.Count : "Missing", "xri-registration");
                    if (!target || target.colliders.Count == 0) continue;
                    foreach (var collider in target.colliders.Where(c => c && c.enabled && c.gameObject.activeInHierarchy))
                    {
                        IXRInteractable owner;
                        bool found = target.interactionManager.TryGetInteractableForCollider(collider, out owner);
                        CheckResult(found && ReferenceEquals(owner, target), "Equipment collider is registered to its own target: " + equipment.Id,
                            "Registered=" + found + "; Collider=" + collider.name, "xri-registration");
                    }
                    EquipmentRayPose(equipment, target, out Vector3 pose, out Quaternion rayRotation);
                    QueueController(rightDevice, pose, rayRotation, false); yield return Wait(.25f);
                    CheckResult(target.isHovered, "Production ray targets physical equipment: " + equipment.Id,
                        "Hovered=" + target.isHovered + "; Ray=" + DescribeRightRayHit(), "production-input");
                    var center = equipment.Root.TransformPoint(equipment.CenterLocal);
                    var before = equipment.Root.rotation;
                    var view = Camera.main.transform.rotation;
                    var nearPointLocal = equipment.Root.InverseTransformPoint(center - view * Vector3.forward * .1f);
                    var surfaceBefore = equipment.Root.TransformPoint(nearPointLocal);
                    QueueController(rightDevice, pose, rayRotation, true); yield return Wait(.15f);
                    CheckResult(target.isSelected && Quaternion.Angle(before, equipment.Root.rotation) < .001f,
                        "Equipment press begins without snapping or rotating: " + equipment.Id, "Selected=" + target.isSelected, "production-input");
                    QueueController(rightDevice, pose + view * Vector3.right * .002f, rayRotation, true); yield return Wait(.12f);
                    CheckResult(Quaternion.Angle(before, equipment.Root.rotation) < .001f,
                        "Equipment rotation filters small initial hand tremor: " + equipment.Id,
                        "Angle=" + Quaternion.Angle(before, equipment.Root.rotation), "production-input");
                    QueueController(rightDevice, pose + view * Vector3.forward * .12f, rayRotation, true); yield return Wait(.12f);
                    CheckResult(Quaternion.Angle(before, equipment.Root.rotation) < .001f && Vector3.Distance(center, equipment.Root.TransformPoint(equipment.CenterLocal)) < .0001f,
                        "Reaching toward equipment adds neither translation nor hidden roll: " + equipment.Id,
                        "Angle=" + Quaternion.Angle(before, equipment.Root.rotation), "production-input");
                    var mechanicalBefore = equipment.Renderers.Where(r => r).Select(r => Quaternion.Inverse(equipment.Root.rotation) * r.transform.rotation).ToArray();
                    var mechanicalPositionsBefore = equipment.Renderers.Where(r => r).Select(r => equipment.Root.InverseTransformPoint(r.transform.position)).ToArray();
                    float progressBefore = demo.NormalizedProgress;
                    float sampleBefore = demo.RefinedEquipment ? demo.RefinedEquipment.LastSampleTime : 0f;
                    Vector3 handDelta = view * new Vector3(.08f, .035f, .12f);
                    QueueController(rightDevice, pose + handDelta, rayRotation, true); yield return Wait(.27f);
                    var visibleMovement = Quaternion.Inverse(view) * (equipment.Root.TransformPoint(nearPointLocal) - surfaceBefore);
                    CheckResult(target.isSelected && Quaternion.Angle(before, equipment.Root.rotation) > 10f && visibleMovement.x > .01f && visibleMovement.y > .001f,
                        "Equipment surface follows the user's right/up drag: " + equipment.Id,
                        "Selected=" + target.isSelected + "; Angle=" + Quaternion.Angle(before, equipment.Root.rotation) + "; Viewed surface=" + visibleMovement.ToString("F5"), "production-input");
                    CheckResult(Vector3.Distance(center, equipment.Root.TransformPoint(equipment.CenterLocal)) < .001f,
                        "Production equipment rotation pins its visible center: " + equipment.Id,
                        "Center delta=" + Vector3.Distance(center, equipment.Root.TransformPoint(equipment.CenterLocal)), "production-input");
                    CheckResult(demo.IsPlaying && demo.NormalizedProgress > progressBefore && (!demo.RefinedEquipment || demo.RefinedEquipment.LastSampleTime > sampleBefore),
                        "Equipment manipulation does not stop its demonstration clock: " + equipment.Id,
                        "Progress=" + progressBefore + " -> " + demo.NormalizedProgress + "; Sample=" + sampleBefore + " -> " + (demo.RefinedEquipment ? demo.RefinedEquipment.LastSampleTime : 0f), "production-input");
                    if (equipment.Id == "air-pump")
                    {
                        var mechanicalAfter = equipment.Renderers.Where(r => r).Select(r => Quaternion.Inverse(equipment.Root.rotation) * r.transform.rotation).ToArray();
                        var mechanicalPositionsAfter = equipment.Renderers.Where(r => r).Select(r => equipment.Root.InverseTransformPoint(r.transform.position)).ToArray();
                        CheckResult(mechanicalBefore.Length == mechanicalAfter.Length && mechanicalBefore.Where((r, i) => Quaternion.Angle(r, mechanicalAfter[i]) > .1f || Vector3.Distance(mechanicalPositionsBefore[i], mechanicalPositionsAfter[i]) > .0001f).Any(),
                            "Pump mechanism continues moving while the display pivot is held",
                            "Compared renderer transforms=" + mechanicalBefore.Length, "production-input");
                    }
                    CheckResult(clue == HelmetExplorationController.Clue.Air
                            ? Vector3.Distance(demo.AirEndpointWorld, demo.AirTargetWorld) < .001f
                            : Vector3.Distance(demo.VoiceEndpointWorld, demo.VoiceTargetWorld) < .001f,
                        "Manipulated equipment keeps the signal destination connected: " + equipment.Id,
                        "Air source=" + demo.AirSourceWorld.ToString("F4") + "; Voice source=" + demo.VoiceSourceWorld.ToString("F4"), "production-input");
                    var route = demo.GetComponentsInChildren<LineRenderer>(true).Single(line => line.name == "Connection route");
                    Vector3 source = clue == HelmetExplorationController.Clue.Air ? demo.AirSourceWorld : demo.VoiceSourceWorld;
                    CheckResult(route.positionCount > 1 && Vector3.Distance(route.GetPosition(0), source) < .001f,
                        "Signal cable starts at the rotated equipment's actual outlet: " + equipment.Id,
                        "Start error=" + (route.positionCount > 0 ? Vector3.Distance(route.GetPosition(0), source) : -1f), "production-input");
                    QueueController(rightDevice, pose + handDelta, rayRotation, false); yield return Wait(.2f);
                    var stopped = equipment.Root.rotation;
                    QueueController(rightDevice, pose + handDelta + view * Vector3.right * .12f, rayRotation, false); yield return Wait(.15f);
                    CheckResult(!target.isSelected && Quaternion.Angle(stopped, equipment.Root.rotation) < .001f,
                        "Released equipment stays at its chosen angle while animation continues: " + equipment.Id,
                        "Selected=" + target.isSelected + "; Angle=" + Quaternion.Angle(stopped, equipment.Root.rotation), "production-input");
                    ParkHands(); yield return Wait(.1f);
                }
                station.Execute(HelmetDiegeticStation.Command.CloseDemo); yield return Wait(.1f);
                station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.4f);
            }
            yield return EquipmentCancellationFixtures();
        }
        static void EquipmentRayPose(HelmetConnectionDemonstration.EquipmentExhibit equipment, XRBaseInteractable target, out Vector3 pose, out Quaternion rotation)
        {
            Physics.SyncTransforms();
            var bounds = target.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy).bounds;
            foreach (var collider in target.colliders.Where(c => c && c.enabled && c.gameObject.activeInHierarchy)) bounds.Encapsulate(collider.bounds);
            foreach (var direction in new[] { Vector3.back, new Vector3(-.5f, .4f, -1), new Vector3(.5f, .4f, -1), Vector3.left, Vector3.right, Vector3.forward, new Vector3(0, 1, -.3f) })
                foreach (float height in new[] { .15f, .5f, 0f, -.35f })
                {
                    Vector3 aim = bounds.center + Vector3.up * bounds.extents.y * height;
                    Vector3 outside = aim + direction.normalized * (bounds.extents.magnitude + .35f);
                    var ray = new Ray(outside, (aim - outside).normalized);
                    foreach (var hit in Physics.RaycastAll(ray, Vector3.Distance(outside, aim) + .02f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
                    {
                        if (!hit.collider || hit.collider.GetComponentInParent<XRBaseInteractor>()) continue;
                        IXRInteractable owner;
                        if (target.interactionManager.TryGetInteractableForCollider(hit.collider, out owner) && ReferenceEquals(owner, target))
                        {
                            pose = outside; rotation = Quaternion.LookRotation(hit.point - outside, Vector3.up); return;
                        }
                        break;
                    }
                }
            throw new InvalidOperationException("No unobstructed registered equipment ray found for " + equipment.Id + "; Bounds=" + bounds);
        }
        static IEnumerator EquipmentCancellationFixtures()
        {
            int initialEra = eras.CurrentIndex;
            foreach (var command in new[] { HelmetDiegeticStation.Command.CloseDemo, HelmetDiegeticStation.Command.Reset,
                HelmetDiegeticStation.Command.NextEra, HelmetDiegeticStation.Command.HeightUp })
            {
                station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.2f);
                var equipment = demo.EquipmentExhibits.First(e => e.IsVisible);
                var target = equipment.Root.GetComponentsInChildren<XRBaseInteractable>(true).FirstOrDefault(t => t.enabled);
                if (!target) { CheckResult(false, "Cancellation fixture has equipment interaction target", equipment.Id); continue; }
                EquipmentRayPose(equipment, target, out Vector3 pose, out Quaternion rotation);
                QueueController(rightDevice, pose, rotation, false); yield return Wait(.2f);
                QueueController(rightDevice, pose, rotation, true); yield return Wait(.15f);
                CheckResult(target.isSelected, "Cancellation fixture holds physical equipment before " + command, "Selected=" + target.isSelected, "production-input");
                station.Execute(command); yield return Wait(.45f);
                CheckResult(!target.isSelected, "Command cancels an active equipment grab: " + command, "Selected=" + target.isSelected, "production-input");
                BodyRayPose(out pose, out rotation);
                QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
                CheckResult(!station.RotationGesture.isSelected && !station.RotationGesture.IsDragging,
                    "Cancelled equipment grip cannot transfer to the helmet while held: " + command,
                    "Selected=" + station.RotationGesture.isSelected + "; Dragging=" + station.RotationGesture.IsDragging, "production-input");
                QueueController(rightDevice, pose, rotation, false); yield return Wait(.2f);
                QueueController(rightDevice, pose, rotation, true); yield return Wait(.15f);
                CheckResult(station.RotationGesture.isSelected && station.RotationGesture.IsDragging,
                    "Release then new grip restores helmet interaction after " + command, "Dragging=" + station.RotationGesture.IsDragging, "production-input");
                QueueController(rightDevice, pose, rotation, false); ParkHands(); yield return Wait(.15f);
                if (demo.IsVisible) station.Execute(HelmetDiegeticStation.Command.CloseDemo);
                station.ViewHeight.ResetHeight();
                if (eras.CurrentIndex != initialEra) station.Execute(HelmetDiegeticStation.Command.PreviousEra);
                station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.4f);
            }
            station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.2f);
            var heldEquipment = demo.EquipmentExhibits.First(e => e.IsVisible);
            var heldTarget = heldEquipment.Root.GetComponentsInChildren<XRBaseInteractable>(true).FirstOrDefault(t => t.enabled);
            if (!heldTarget) yield break;
            var feedbackSurfaces = heldEquipment.Renderers.Where(renderer => renderer).ToArray();
            var idleHover = feedbackSurfaces.Select(RendererHover).ToArray();
            CheckResult(feedbackSurfaces.Length > 0 && idleHover.All(value => Mathf.Abs(value) < .0001f),
                "Equipment lifecycle feedback fixture starts from zero hover baseline",
                "Surfaces=" + feedbackSurfaces.Length + "; Nonzero=" + idleHover.Count(value => Mathf.Abs(value) >= .0001f), "module-lifecycle");
            EquipmentRayPose(heldEquipment, heldTarget, out Vector3 heldPose, out Quaternion heldRotation);
            QueueController(rightDevice, heldPose, heldRotation, false); yield return Wait(.2f);
            QueueController(rightDevice, heldPose, heldRotation, true); yield return Wait(.15f);
            CheckResult(heldTarget.isSelected, "Lifecycle fixture begins with a held equipment model", "Selected=" + heldTarget.isSelected, "production-input");
            var selectedHover = feedbackSurfaces.Select(RendererHover).ToArray();
            CheckResult(selectedHover.Length > 0 && selectedHover.All(value => value > .4f),
                "Held equipment visibly arms the feedback-restoration regression",
                "Surfaces=" + selectedHover.Length + "; Selected highlight=" + selectedHover.Count(value => value > .4f), "module-lifecycle");
            station.enabled = false; yield return Wait(.15f);
            CheckResult(!heldTarget.isSelected && !heldTarget.gameObject.activeInHierarchy,
                "Disabling the station cancels and hides its held equipment", "Selected=" + heldTarget.isSelected + "; Active=" + heldTarget.gameObject.activeInHierarchy, "module-lifecycle");
            var disabledHover = feedbackSurfaces.Select(RendererHover).ToArray();
            CheckResult(disabledHover.All(value => Mathf.Abs(value) < .0001f),
                "Disabling the station restores every equipment surface's zero hover baseline",
                "Surfaces=" + disabledHover.Length + "; Residual highlight=" + disabledHover.Count(value => Mathf.Abs(value) >= .0001f), "module-lifecycle");
            station.enabled = true; yield return Wait(.2f);
            BodyRayPose(out heldPose, out heldRotation);
            QueueController(rightDevice, heldPose, heldRotation, true); yield return Wait(.2f);
            CheckResult(!station.RotationGesture.isSelected && !station.RotationGesture.IsDragging,
                "Reenabled station blocks held equipment grip from transferring to the helmet", "Dragging=" + station.RotationGesture.IsDragging, "production-input");
            QueueController(rightDevice, heldPose, heldRotation, false); yield return Wait(.2f);
            QueueController(rightDevice, heldPose, heldRotation, true); yield return Wait(.15f);
            CheckResult(station.RotationGesture.isSelected && station.RotationGesture.IsDragging,
                "Reenabled station permits a new helmet grip after release", "Dragging=" + station.RotationGesture.IsDragging, "production-input");
            QueueController(rightDevice, heldPose, heldRotation, false); ParkHands(); yield return Wait(.15f);
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.4f);
        }
        static float RendererHover(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetFloat("_Hover");
        }
        static IEnumerator EraFixtures()
        {
            int original = eras.CurrentIndex;
            CheckResult(eras.AvailableEraCount == 3, "Three era choices retained", "Count=" + eras.AvailableEraCount);
            for (int i = 0; i < eras.AvailableEraCount; i++)
            {
                int before = eras.CurrentIndex;
                station.Execute(HelmetDiegeticStation.Command.Window); station.Execute(HelmetDiegeticStation.Command.Section); station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.1f);
                station.Execute(HelmetDiegeticStation.Command.NextEra); yield return Wait(.5f);
                CheckResult(eras.CurrentIndex == (before + 1) % eras.AvailableEraCount, "Next era command advances " + i, "Current=" + eras.CurrentIndex);
                CheckResult(!inspection.IsCutaway && inspection.WindowAngle < .1f && !demo.IsVisible, "Era starts with complete quiet exterior " + eras.CurrentIndex, "Window=" + inspection.WindowAngle);
                CheckResult(binding.AirOutlet && binding.TelephoneOutlet && binding.ActiveShell && binding.ActiveGlass, "Era visual and connector bindings remain valid " + eras.CurrentIndex, "Air=" + binding.AirOutlet.name + "; Voice=" + binding.TelephoneOutlet.name);
                var control = station.Controls[HelmetDiegeticStation.Command.Air];
                CheckResult(Vector3.Distance(control.transform.position, binding.AirOutlet.position) < .2f, "Air marker follows active connector " + eras.CurrentIndex, "Distance=" + Vector3.Distance(control.transform.position, binding.AirOutlet.position));
                CheckResult(Vector3.Distance(station.Controls[HelmetDiegeticStation.Command.Voice].transform.position, binding.TelephoneOutlet.position) < .2f, "Voice marker follows active connector " + eras.CurrentIndex, "Distance=" + Vector3.Distance(station.Controls[HelmetDiegeticStation.Command.Voice].transform.position, binding.TelephoneOutlet.position));
                var visor = station.Controls[HelmetDiegeticStation.Command.Window];
                var visorCollider = visor.Target.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy);
                CheckResult(binding.ActiveGlass && Vector3.Distance(visorCollider.bounds.ClosestPoint(binding.ActiveGlass.bounds.center), binding.ActiveGlass.bounds.center) < .06f,
                    "Actual visor hit area tracks active era glass " + eras.CurrentIndex,
                    "Collider=" + HierarchyPath(visorCollider.transform) + "; Glass=" + binding.ActiveGlass.name);
                yield return RayClick(HelmetDiegeticStation.Command.Window);
                CheckResult(inspection.WindowAngle > 90, "Actual visor click opens era " + eras.CurrentIndex, "Angle=" + inspection.WindowAngle, "production-input");
                yield return RayClick(HelmetDiegeticStation.Command.Window);
                CheckResult(inspection.WindowAngle < .1f, "Clicking open visor closes era " + eras.CurrentIndex, "Angle=" + inspection.WindowAngle, "production-input");
                BodyRayPose(out Vector3 shellPose,out Quaternion shellRotation);
                var bodyCenter=inspection.transform.TransformPoint(inspection.SectionCenterLocal);var bodyRotation=inspection.transform.rotation;
                QueueController(rightDevice,shellPose,shellRotation,false);yield return Wait(.2f);
                QueueController(rightDevice,shellPose,shellRotation,true);yield return Wait(.15f);
                CheckResult(station.RotationGesture.IsDragging,"Actual shell can be held without a rotation button: era "+eras.CurrentIndex,"Dragging="+station.RotationGesture.IsDragging,"production-input");
                QueueController(rightDevice,shellPose+Camera.main.transform.right*.035f,shellRotation,true);yield return Wait(.15f);
                CheckResult(Quaternion.Angle(bodyRotation,inspection.transform.rotation)>1f && Vector3.Distance(bodyCenter,inspection.transform.TransformPoint(inspection.SectionCenterLocal))<.0001f,
                    "Actual shell rotates in place: era "+eras.CurrentIndex,"Angle="+Quaternion.Angle(bodyRotation,inspection.transform.rotation),"production-input");
                QueueController(rightDevice,shellPose,shellRotation,false);ParkHands();yield return Wait(.15f);
                station.Execute(HelmetDiegeticStation.Command.Reset);yield return Wait(.4f);
                Capture("era-" + eras.CurrentIndex, new Vector3(-.42f, .18f, -1.1f));
            }
            CheckResult(eras.CurrentIndex == original, "Three switches wrap to original era", "Index=" + eras.CurrentIndex);
            station.Execute(HelmetDiegeticStation.Command.PreviousEra); yield return Wait(.3f);
            CheckResult(eras.CurrentIndex == (original + eras.AvailableEraCount - 1) % eras.AvailableEraCount, "Previous era wraps correctly", "Index=" + eras.CurrentIndex);
            station.Execute(HelmetDiegeticStation.Command.NextEra); yield return Wait(.3f);
        }

        static IEnumerator ProductionRayCommands()
        {
            yield return RayClick(HelmetDiegeticStation.Command.Window);
            CheckResult(inspection.WindowAngle > 90, "Production ray click opens window", "Angle=" + inspection.WindowAngle, "production-input");
            yield return RayClick(HelmetDiegeticStation.Command.Window);
            CheckResult(inspection.WindowAngle < .1f, "Production ray second click closes window", "Angle=" + inspection.WindowAngle, "production-input");
            yield return RayClick(HelmetDiegeticStation.Command.Section);
            CheckResult(inspection.IsCutaway, "Production ray click enables section", "Cut=" + inspection.IsCutaway, "production-input");
            yield return RayClick(HelmetDiegeticStation.Command.Section);
            CheckResult(!inspection.IsCutaway, "Production ray second click disables section", "Cut=" + inspection.IsCutaway, "production-input");
            // Rotate using the same production calculation before the rear
            // connector checks. The marker pose fixture then approaches its own
            // visible face; it does not assume a fixed Euler angle from the drag.
            var hand = inspection.transform.TransformPoint(inspection.SectionCenterLocal) + Vector3.back * .4f;
            station.RotationGesture.BeginForValidation(hand, Quaternion.identity);
            station.RotationGesture.ApplyForValidation(hand + Vector3.right * .6f);
            station.RotationGesture.EndForValidation(); yield return Wait(.2f);
            yield return RayClick(HelmetDiegeticStation.Command.Air);
            CheckResult(demo.IsVisible && demo.CurrentClue == HelmetExplorationController.Clue.Air, "Production ray click opens air demo", "Visible=" + demo.IsVisible, "production-input");
            yield return RayClick(HelmetDiegeticStation.Command.Voice);
            CheckResult(demo.IsVisible && demo.CurrentClue == HelmetExplorationController.Clue.Voice, "Production ray click switches to voice demo", "Visible=" + demo.IsVisible, "production-input");
            yield return RayClick(HelmetDiegeticStation.Command.Voice);
            CheckResult(!demo.IsVisible, "Production ray same voice click closes demo", "Visible=" + demo.IsVisible, "production-input");
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
            yield return RayClick(HelmetDiegeticStation.Command.Settings);
            CheckResult(station.SettingsVisible, "Production ray click opens fixed settings", "Visible=" + station.SettingsVisible, "production-input");
            yield return RayClick(HelmetDiegeticStation.Command.Settings);
            CheckResult(!station.SettingsVisible, "Production ray second click closes settings", "Visible=" + station.SettingsVisible, "production-input");
            int original = eras.CurrentIndex;
            yield return RayClick(HelmetDiegeticStation.Command.NextEra);
            CheckResult(eras.CurrentIndex == (original + 1) % eras.AvailableEraCount, "Production ray click changes era", "Era=" + eras.CurrentIndex, "production-input");
            yield return RayClick(HelmetDiegeticStation.Command.PreviousEra);
        }
        static IEnumerator RayClick(HelmetDiegeticStation.Command command)
        {
            var target = station.Controls[command].Target; int events = 0; string kind = "";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; kind = args.interactorObject.GetType().Name; };
            target.selectEntered.AddListener(listener);
            try
            {
                var collider = target.colliders.First(c => c && c.gameObject.activeInHierarchy);
                Vector3 point = command == HelmetDiegeticStation.Command.Window ? VisorAimPoint(collider) : collider.bounds.center;
                Vector3 pose = RayApproach(command, collider, point);
                Quaternion rotation = Quaternion.LookRotation(point - pose, Vector3.up);
                QueueController(rightDevice, pose, rotation, false); yield return Wait(.35f);
                CheckResult(target.isHovered, "Production ray hover " + command, "Hovered=" + target.isHovered + "; Ray=" + DescribeRightRayHit(), "production-input");
                QueueController(rightDevice, pose, rotation, true); yield return Wait(.25f);
                CheckResult(events == 1 && kind == nameof(XRRayInteractor), "Production ray selects once " + command, "Events=" + events + "; Type=" + kind, "production-input");
                if (command == HelmetDiegeticStation.Command.Window)
                    CheckResult(!station.RotationGesture.IsDragging, "Actual visor click wins over model rotation", "Dragging=" + station.RotationGesture.IsDragging, "production-input");
                QueueController(rightDevice, pose, rotation, false); yield return Wait(.5f); ParkHands(); yield return Wait(.15f);
            }
            finally { target.selectEntered.RemoveListener(listener); }
        }
        static IEnumerator ProductionReleaseFeedback()
        {
            if (inspection.IsCutaway) station.Execute(HelmetDiegeticStation.Command.Section);
            var control = station.Controls[HelmetDiegeticStation.Command.Section];
            var collider = control.Target.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy);
            var point = collider.bounds.center;
            var pose = RayApproach(HelmetDiegeticStation.Command.Section, collider, point);
            var rotation = Quaternion.LookRotation(point - pose, Vector3.up);
            int confirmed = control.ConfirmedCount, cancelled = control.CancelledCount;
            QueueController(rightDevice, pose, rotation, false); yield return Wait(.3f);
            CheckResult(control.Target.isHovered && control.State == HelmetPhysicalControl.FeedbackState.Hovered && !control.IsPressed,
                "Pointing at a command has a distinct hover state", "State=" + control.State + "; Hover=" + control.Target.isHovered, "production-input");
            QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
            CheckResult(control.IsPressed && control.State == HelmetPhysicalControl.FeedbackState.Pressed && !inspection.IsCutaway && control.ConfirmedCount == confirmed,
                "Command press gives selected feedback without prematurely acting", "State=" + control.State + "; Cut=" + inspection.IsCutaway + "; Confirmed=" + control.ConfirmedCount, "production-input");
            QueueController(rightDevice, pose, rotation, false); yield return Wait(.08f);
            CheckResult(inspection.IsCutaway && !control.IsPressed && control.ConfirmedCount == confirmed + 1 && control.ConfirmationVisible,
                "Release on the original command confirms once and shows completion feedback", "Cut=" + inspection.IsCutaway + "; Confirmed=" + control.ConfirmedCount + "; Feedback=" + control.ConfirmationVisible, "production-input");
            yield return Wait(.4f);
            QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
            var awayRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            QueueController(rightDevice, pose, awayRotation, true); yield return Wait(.2f);
            QueueController(rightDevice, pose, awayRotation, false); yield return Wait(.15f);
            CheckResult(inspection.IsCutaway && !control.IsPressed && control.ConfirmedCount == confirmed + 1 && control.CancelledCount == cancelled + 1,
                "Moving off the command before release cancels without toggling", "Cut=" + inspection.IsCutaway + "; Confirmed=" + control.ConfirmedCount + "; Cancelled=" + control.CancelledCount, "production-input");
            CheckResult(!control.Target.isSelected && !control.ConfirmationVisible,
                "Cancelled release clears selection and does not claim success", "Selected=" + control.Target.isSelected + "; Success feedback=" + control.ConfirmationVisible, "production-input");
            ParkHands(); yield return Wait(.2f);
            yield return RayClick(HelmetDiegeticStation.Command.Section);
            CheckResult(!inspection.IsCutaway, "Fresh click works after a cancelled command", "Cut=" + inspection.IsCutaway, "production-input");
        }
        static Vector3 VisorAimPoint(Collider collider)
        {
            // Keep the original front-window knowledge dot selectable at its own
            // unchanged origin. Aim at an unobstructed part of the real glass.
            if (!(collider is BoxCollider box)) return collider.bounds.center;
            var size = box.size;
            var offset = size * .22f;
            if (size.x <= size.y && size.x <= size.z) offset.x = 0;
            else if (size.y <= size.z) offset.y = 0;
            else offset.z = 0;
            return box.transform.TransformPoint(box.center + offset);
        }
        static Vector3 RayApproach(HelmetDiegeticStation.Command command, Collider collider, Vector3? aim = null)
        {
            var target = station.Controls[command].Target;
            Vector3 point = aim ?? collider.bounds.center;
            if (command != HelmetDiegeticStation.Command.Window)
                return point - target.transform.forward * .48f;
            // A visor is not a viewer-facing billboard. Keep aiming at its real
            // collider after the hinge opens and when another era moves the glass.
            Vector3 centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            Vector3 outward = point - centre;
            if (outward.sqrMagnitude < .0001f) outward = -inspection.transform.forward;
            outward.Normalize();
            // The thinnest local box dimension is the window surface normal.
            // Choose the sign facing away from the shell so the ray does not
            // travel through an intact helmet before reaching the actual glass.
            if (collider is BoxCollider box)
            {
                var size = box.size;
                Vector3 localNormal = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
                Vector3 normal = box.transform.TransformDirection(localNormal).normalized;
                if (Vector3.Dot(normal, outward) < 0) normal = -normal;
                outward = normal;
            }
            return point + outward * .40f;
        }
        static IEnumerator KnowledgeHotspots()
        {
            var tour = inspection.GetComponentInChildren<HelmetHotspotTour>(true);
            var info = (TMP_Text)new SerializedObject(tour).FindProperty("informationText").objectReferenceValue;
            var centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            foreach (var hotspot in inspection.GetComponentsInChildren<HelmetHotspot>(true))
            {
                tour.ResetTour();
                hotspot.Activate();
                CheckResult(tour.HasVisited(hotspot.Identifier) && info && !string.IsNullOrWhiteSpace(info.text) && info.text.Contains(hotspot.Title),
                    "Original knowledge activation retains text and visit: " + hotspot.Identifier, info ? info.text : "Missing information text");
                tour.ResetTour();
                var target = hotspot.GetComponent<XRSimpleInteractable>();
                var collider = target.colliders.First(c => c && c.enabled);
                var point = collider.bounds.center;
                var outward = (point - centre).normalized;
                if (outward.sqrMagnitude < .1f) outward = Vector3.back;
                var pose = point + outward * .35f;
                if (hotspot.Identifier == "breastplate")
                {
                    // A radial approach to the low breastplate points down into
                    // the plinth. Use a visible front/upper observation position.
                    outward = Vector3.ProjectOnPlane(point - centre, Vector3.up).normalized;
                    if (outward.sqrMagnitude < .1f) outward = -inspection.transform.forward;
                    pose = point + outward * .35f + Vector3.up * .25f;
                }
                var rotation = Quaternion.LookRotation(point - pose, Vector3.up);
                int events = 0; string kind = "";
                UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; kind = args.interactorObject.GetType().Name; };
                target.selectEntered.AddListener(listener);
                try
                {
                    QueueController(rightDevice, pose, rotation, false); yield return Wait(.3f);
                    string rayHit = DescribeRightRayHit();
                    QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
                    CheckResult(events == 1 && kind == nameof(XRRayInteractor) && tour.HasVisited(hotspot.Identifier),
                        "Production ray selects knowledge hotspot: " + hotspot.Identifier, "Events=" + events + "; Type=" + kind + "; Visited=" + tour.HasVisited(hotspot.Identifier) +
                        "; Requested pose=" + pose.ToString("F4") + "; Target point=" + point.ToString("F4") + "; Prepress=" + rayHit + "; Pressed=" + DescribeRightRayHit(), "production-input");
                    CheckResult(!station.RotationGesture.IsDragging, "Knowledge selection wins over whole-model rotation: " + hotspot.Identifier, "Rotation dragging=" + station.RotationGesture.IsDragging, "production-input");
                    QueueController(rightDevice, pose, rotation, false); yield return Wait(.2f); ParkHands(); yield return Wait(.2f);
                }
                finally { target.selectEntered.RemoveListener(listener); }
            }
            tour.ResetTour();
        }
        static string DescribeRightRayHit()
        {
            return string.Join(" | ", rightController.GetComponentsInChildren<XRRayInteractor>(true).Select(ray =>
            {
                bool found = ray.TryGetCurrent3DRaycastHit(out var hit);
                string path = found && hit.collider ? HierarchyPath(hit.collider.transform) : "none";
                IXRInteractable owner=null;
                if(found && hit.collider)ray.interactionManager.TryGetInteractableForCollider(hit.collider,out owner);
                var valid=new List<IXRInteractable>();ray.GetValidTargets(valid);
                var window=station.Controls[HelmetDiegeticStation.Command.Window].Target;
                var direct=rightController.GetComponentsInChildren<XRDirectInteractor>(true).FirstOrDefault();
                return ray.name + ": Active=" + ray.isActiveAndEnabled + "; Pose=" + ray.transform.position.ToString("F4") + "; Hit=" + path + "; HitPoint=" + (found ? hit.point.ToString("F4") : "none")+
                    "; Collider="+(found?hit.collider.GetInstanceID():0)+"; Owner="+(owner as UnityEngine.Object)?.name+"; Valid="+string.Join(",",valid.Select(v=>(v as UnityEngine.Object)?.name))+"; Window hoverable="+window.IsHoverableBy((IXRHoverInteractor)ray)+"; Direct hovered="+(direct?string.Join(",",direct.interactablesHovered.Select(v=>(v as UnityEngine.Object)?.name)):"none");
            }));
        }
        static string HierarchyPath(Transform target)
        {
            var parts = new List<string>();
            for (var current = target; current; current = current.parent) parts.Add(current.name);
            parts.Reverse(); return string.Join("/", parts);
        }
        static IEnumerator ProductionRayGesture()
        {
            var gesture = station.RotationGesture;
            var marker = station.Controls[HelmetDiegeticStation.Command.Rotate];
            Vector3 centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            BodyRayPose(out Vector3 pose, out Quaternion rotation);
            int events = 0; string kind = "";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; kind = args.interactorObject.GetType().Name; };
            gesture.selectEntered.AddListener(listener);
            try
            {
                QueueController(rightDevice, pose, rotation, false); yield return Wait(.35f);
                var untouched = inspection.transform.rotation;
                CheckResult(gesture.isHovered && marker.Opacity > .85f,
                    "Pointing at actual shell reveals the rotation affordance", "Hover=" + gesture.isHovered + "; Opacity=" + marker.Opacity + "; Ray=" + DescribeRightRayHit(), "production-input");
                QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
                CheckResult(gesture.IsDragging && !gesture.IsManipulating && marker.IsPressed && marker.State == HelmetPhysicalControl.FeedbackState.Pressed,
                    "Body hold shows selected feedback before any motion", "Dragging=" + gesture.IsDragging + "; Manipulating=" + gesture.IsManipulating + "; State=" + marker.State, "production-input");
                CheckResult(Quaternion.Angle(untouched, inspection.transform.rotation) < .001f,
                    "Pressing the shell without moving cannot rotate the exhibit", "Angle=" + Quaternion.Angle(untouched, inspection.transform.rotation), "production-input");
                QueueController(rightDevice, pose, rotation, false); yield return Wait(.2f);
                CheckResult(!gesture.IsDragging && Quaternion.Angle(untouched, inspection.transform.rotation) < .001f,
                    "A shell tap does not rotate or translate the exhibit", "Angle=" + Quaternion.Angle(untouched, inspection.transform.rotation), "production-input");
                events = 0;
                QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
                CheckResult(events == 1 && kind == nameof(XRRayInteractor) && gesture.IsDragging, "Production ray hold begins rotation gesture", "Events=" + events + "; Type=" + kind + "; Drag=" + gesture.IsDragging, "production-input");
                var before = inspection.transform.rotation;
                QueueController(rightDevice, pose + Vector3.right * .002f, rotation, true); yield return Wait(.15f);
                CheckResult(!gesture.IsManipulating && Quaternion.Angle(before, inspection.transform.rotation) < .001f,
                    "Small hand tremor is filtered before rotation starts", "Angle=" + Quaternion.Angle(before, inspection.transform.rotation), "production-input");
                QueueController(rightDevice, pose + new Vector3(.08f, .06f, .035f), rotation, true); yield return Wait(.2f);
                CheckResult(gesture.IsManipulating && Quaternion.Angle(before, inspection.transform.rotation) > 10, "Held tracked position rotates exhibit through production actions", "Angle=" + Quaternion.Angle(before, inspection.transform.rotation), "production-input");
                CheckResult(Vector3.Distance(centre, inspection.transform.TransformPoint(inspection.SectionCenterLocal)) < .001f, "Production rotation pins visible centre", "Distance=" + Vector3.Distance(centre, inspection.transform.TransformPoint(inspection.SectionCenterLocal)), "production-input");
                QueueController(rightDevice, pose + new Vector3(.08f, .06f, .035f), rotation, false); yield return Wait(.25f);
                CheckResult(!gesture.IsDragging, "Production release finishes rotation", "Drag=" + gesture.IsDragging, "production-input");
                var stopped = inspection.transform.rotation;
                QueueController(rightDevice, pose + Vector3.right * .25f, rotation, false); yield return Wait(.2f);
                CheckResult(Quaternion.Angle(stopped, inspection.transform.rotation) < .01f, "Unpressed hand movement cannot rotate exhibit", "Angle=" + Quaternion.Angle(stopped, inspection.transform.rotation), "production-input");
            }
            finally { gesture.selectEntered.RemoveListener(listener); ParkHands(); }
            yield return Wait(.2f);
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
        }
        static IEnumerator RotationHintFixtures()
        {
            if (station.AlwaysShowHints) station.Execute(HelmetDiegeticStation.Command.Hints);
            CheckResult(station.RotationHelpLearned && station.RotationHintsCondensed,
                "Successful production rotation enables shorter session guidance", "Learned=" + station.RotationHelpLearned + "; Condensed=" + station.RotationHintsCondensed);
            bool oldMotion = inspection.ReduceMotion;
            // Freeze mechanism sampling only for collider-size comparisons. The
            // preceding production tests already validate moving pump mechanisms.
            inspection.ReduceMotion = true;
            station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.2f);
            var equipment = demo.EquipmentExhibits.First(e => e.IsVisible);
            var equipmentGesture = equipment.Root.GetComponentInChildren<HelmetGestureInteractable>();
            var equipmentHint = station.EquipmentRotationHints.Single(h => h.TargetRoot == equipment.Root);
            foreach (var pair in new[] { new KeyValuePair<HelmetGestureInteractable, HelmetRotationHint>(station.RotationGesture, station.BodyRotationHint),
                new KeyValuePair<HelmetGestureInteractable, HelmetRotationHint>(equipmentGesture, equipmentHint) })
            {
                var gesture = pair.Key; var hint = pair.Value;
                string specimen = gesture == station.RotationGesture ? "helmet" : equipment.Id;
                ParkHands(); yield return Wait(1.2f);
                Vector3 pose; Quaternion rotation;
                if (gesture == station.RotationGesture) BodyRayPose(out pose, out rotation);
                else EquipmentRayPose(equipment, gesture, out pose, out rotation);
                var ray = new Ray(pose, rotation * Vector3.forward);
                var hit = Physics.RaycastAll(ray, 3f, ~0, QueryTriggerInteraction.Collide).OrderBy(value => value.distance)
                    .First(value => gesture.colliders.Contains(value.collider));
                Vector3 gripPoint = hit.point;
                Vector3 outward = (pose - gripPoint).normalized;
                Vector3 nearView = gripPoint + outward * .20f;
                Quaternion viewRotation = Quaternion.LookRotation(gripPoint - nearView, Vector3.up);
                var colliders = gesture.colliders.Where(c => c && c.enabled && c.gameObject.activeInHierarchy).ToArray();
                var sizes = colliders.Select(c => c.bounds.size).ToArray();
                QueueHeadWorld(nearView, viewRotation);
                QueueController(rightDevice, pose, rotation, false); yield return Wait(.4f);
                CheckResult(gesture.isHovered && hint.IsNear && hint.IsAnchored && hint.Caption.enabled && hint.Leader.enabled,
                    "Near physical targeting preserves a visible hint and leader: " + specimen,
                    "Hover=" + gesture.isHovered + "; Near=" + hint.IsNear + "; Anchored=" + hint.IsAnchored, "production-input");
                var corners = new Vector3[4]; hint.Caption.rectTransform.GetWorldCorners(corners);
                var left = (corners[0] + corners[1]) * .5f; var right = (corners[2] + corners[3]) * .5f;
                float actualAngle = Vector3.Angle(left - Camera.main.transform.position, right - Camera.main.transform.position);
                CheckResult(actualAngle <= station.MaximumHintAngle + .1f && hint.AngularWidthDegrees <= station.MaximumHintAngle + .01f,
                    "Near rotation caption respects its angular width using rendered world geometry: " + specimen,
                    "Measured=" + actualAngle + "; Reported=" + hint.AngularWidthDegrees + "; Limit=" + station.MaximumHintAngle);
                // An entire helmet can exceed the viewport at close range. Its
                // non-focus silhouette is then a valid overlap area; visibility
                // and a clear selected point take priority over being outside it.
                CheckHintViewport(hint, gripPoint, "initial " + specimen);
                CheckResult(hint.GripScale > 0 && hint.GripScale < 1f && !hint.GetComponentsInChildren<Collider>(true).Any() &&
                        !hint.GetComponentsInChildren<XRBaseInteractable>(true).Any(),
                    "Smaller near feedback adds no competing interaction target: " + specimen,
                    "Grip scale=" + hint.GripScale);
                CheckResult(colliders.Select((collider, index) => collider.enabled && Vector3.Distance(collider.bounds.size, sizes[index]) < .0001f).All(value => value),
                    "Near visual adaptation leaves the actual selection bounds unchanged: " + specimen,
                    "Colliders=" + colliders.Length);
                foreach (var language in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
                {
                    MuseumText.Channel.SetLanguage(language); yield return Wait(.12f);
                    hint.Caption.ForceMeshUpdate(true, true);
                    CheckResult(hint.Caption.GetComponent<LocalizedSourceText>().SourceEnglish == "Hold to rotate" && hint.Caption.text == MuseumText.Translate("Hold to rotate") && !hint.Caption.isTextOverflowing,
                        "Learned short rotation guidance is localized and fits: " + specimen + "/" + language, hint.Caption.text);
                    CaptureNearHint("near-" + specimen + "-hover-" + language);
                }
                station.Execute(HelmetDiegeticStation.Command.Hints); yield return Wait(.2f);
                CheckResult(!station.RotationHintsCondensed && hint.Caption.GetComponent<LocalizedSourceText>().SourceEnglish == "Hold + move to rotate" &&
                        hint.Caption.text == MuseumText.Translate("Hold + move to rotate"),
                    "Visitors can restore complete rotation help after learning: " + specimen, hint.Caption.text);
                foreach (var language in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
                {
                    MuseumText.Channel.SetLanguage(language); yield return Wait(.12f);
                    hint.Caption.ForceMeshUpdate(true, true);
                    CheckResult(hint.Caption.GetComponent<LocalizedSourceText>().SourceEnglish == "Hold + move to rotate" &&
                            hint.Caption.text == MuseumText.Translate("Hold + move to rotate") && !hint.Caption.isTextOverflowing,
                        "Restored complete help follows all three languages and fits: " + specimen + "/" + language, hint.Caption.text);
                }
                station.Execute(HelmetDiegeticStation.Command.Hints); yield return Wait(.2f);
                var anchor = hint.AnchorPosition;
                var ring = gesture == station.RotationGesture
                    ? station.Controls[HelmetDiegeticStation.Command.Rotate].GetComponentsInChildren<LineRenderer>(true).Single(line => line.name == "Body grip feedback")
                    : station.GetComponentsInChildren<LineRenderer>(true).Single(line => line.name == "Equipment grip ring" && line.transform.parent.name.EndsWith(equipment.Id));
                float hoverWidth = ring.widthMultiplier;
                QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
                CheckResult(gesture.isSelected && ring.enabled && ring.gameObject.activeInHierarchy && ring.widthMultiplier > hoverWidth * 1.5f &&
                        hint.Caption.GetComponent<LocalizedSourceText>().SourceEnglish == "Move to rotate",
                    "Near selected state remains visible without a large text strip: " + specimen,
                    "Selected=" + gesture.isSelected + "; Ring width=" + ring.widthMultiplier + "; Caption=" + hint.Caption.text, "production-input");
                CaptureNearHint("near-" + specimen + "-hold");
                QueueHeadWorld(anchor - Camera.main.transform.forward * .01f, viewRotation); yield return Wait(.15f);
                CheckResult(hint.CaptionSuppressed && !hint.Caption.enabled && !hint.Leader.enabled && ring.enabled && gesture.isSelected &&
                        Vector3.Distance(anchor, hint.AnchorPosition) < .0001f,
                    "Entering the caption space hides text while keeping the selected control point: " + specimen,
                    "Suppressed=" + hint.CaptionSuppressed + "; Ring visible=" + ring.enabled + "; Selected=" + gesture.isSelected, "production-input");
                QueueHeadWorld(nearView, viewRotation); yield return Wait(.15f);
                CheckResult(!hint.CaptionSuppressed && hint.Caption.enabled && ring.enabled,
                    "Returning to the clear view restores the existing caption without moving its anchor: " + specimen,
                    "Suppressed=" + hint.CaptionSuppressed + "; Anchor delta=" + Vector3.Distance(anchor, hint.AnchorPosition));
                var viewShift = Camera.main.transform.right * .04f;
                QueueHeadWorld(nearView + viewShift, viewRotation); yield return Wait(.2f);
                CheckResult(Vector3.Distance(anchor, hint.AnchorPosition) < .0001f && Vector3.Distance(anchor, hint.Caption.transform.position) < .0001f,
                    "Caption stays anchored while the visitor moves their head during a hold: " + specimen,
                    "Anchor delta=" + Vector3.Distance(anchor, hint.AnchorPosition));
                QueueController(rightDevice, pose + Camera.main.transform.right * .05f, rotation, true); yield return Wait(.2f);
                CheckResult(gesture.IsManipulating && Vector3.Distance(anchor, hint.AnchorPosition) < .0001f &&
                        hint.Caption.GetComponent<LocalizedSourceText>().SourceEnglish == "Release to stop" && ring.enabled,
                    "Rotating the specimen keeps its caption fixed and gives a short stop cue: " + specimen,
                    "Manipulating=" + gesture.IsManipulating + "; Anchor delta=" + Vector3.Distance(anchor, hint.AnchorPosition), "production-input");
                QueueController(rightDevice, pose, rotation, false); ParkHands(); QueueHead(); yield return Wait(1.2f);
                CheckResult(!hint.IsAnchored && !hint.Leader.enabled,
                    "Leaving the target releases the temporary caption anchor: " + specimen, "Anchored=" + hint.IsAnchored);
                // Re-enter at a deliberately extreme 7.5 cm distance to exercise
                // initial viewport-edge placement, not a head-following correction.
                // This is a boundary fixture, not a recommended viewing distance.
                if (gesture == station.RotationGesture) BodyRayPose(out pose, out rotation);
                else EquipmentRayPose(equipment, gesture, out pose, out rotation);
                ray = new Ray(pose, rotation * Vector3.forward);
                hit = Physics.RaycastAll(ray, 3f, ~0, QueryTriggerInteraction.Collide).OrderBy(value => value.distance)
                    .First(value => gesture.colliders.Contains(value.collider));
                gripPoint = hit.point;
                outward = (pose - gripPoint).normalized;
                var boundaryView = gripPoint + outward * .075f;
                var boundaryRotation = Quaternion.LookRotation(gripPoint - boundaryView, Vector3.up);
                QueueHeadWorld(boundaryView, boundaryRotation);
                QueueController(rightDevice, pose, rotation, false); yield return Wait(.4f);
                CheckResult(gesture.isHovered && hint.UsesViewportFallback && hint.IsNear && !hint.CaptionSuppressed && hint.Caption.enabled,
                    "Extreme close targeting explicitly chooses the initial viewport-edge fallback: " + specimen,
                    "Hover=" + gesture.isHovered + "; Fallback=" + hint.UsesViewportFallback + "; Suppressed=" + hint.CaptionSuppressed, "production-input");
                CheckHintViewport(hint, gripPoint, "viewport fallback " + specimen);
                var boundaryAnchor = hint.AnchorPosition;
                foreach (var language in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
                {
                    MuseumText.Channel.SetLanguage(language); yield return Wait(.12f);
                    hint.Caption.ForceMeshUpdate(true, true);
                    CheckResult(!hint.CaptionSuppressed && hint.Caption.enabled && !hint.Caption.isTextOverflowing &&
                            hint.Caption.text == MuseumText.Translate("Hold to rotate") && Vector3.Distance(boundaryAnchor, hint.AnchorPosition) < .0001f,
                        "Viewport-edge guidance is readable and remains anchored across languages: " + specimen + "/" + language,
                        hint.Caption.text + "; Anchor delta=" + Vector3.Distance(boundaryAnchor, hint.AnchorPosition));
                    CaptureNearHint("viewport-fallback-" + specimen + "-" + language);
                }
                ParkHands(); QueueHead(); yield return Wait(1.2f);
            }
            MuseumText.Channel.SetLanguage(MuseumLanguage.English);
            inspection.ReduceMotion = oldMotion;
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
        }
        static void QueueHeadWorld(Vector3 position, Quaternion rotation)
        {
            var parent = Camera.main.transform.parent;
            QueueHead(parent.InverseTransformPoint(position), Quaternion.Inverse(parent.rotation) * rotation);
        }
        static void CaptureNearHint(string file)
        {
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) text.ForceMeshUpdate(true);
            string name = file + ".png";
            var view = Camera.main;
            HelmetDiegeticPreview.CaptureView(Path.Combine(Output, name), view.transform.position, view.transform.rotation, view.fieldOfView, view.aspect, view.nearClipPlane);
            report.diagnosticCaptures.Add(name);
        }
        static Vector3 MonoViewportPoint(Camera view, Vector3 world)
        {
            Vector3 local = view.transform.InverseTransformPoint(world);
            float halfHeight = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * view.aspect;
            if (local.z <= .00001f) return new Vector3(float.NaN, float.NaN, local.z);
            return new Vector3(.5f + local.x / (2f * local.z * halfWidth), .5f + local.y / (2f * local.z * halfHeight), local.z);
        }
        static void CheckHintViewport(HelmetRotationHint hint, Vector3 gripPoint, string fixture)
        {
            var view = Camera.main;
            var corners = new Vector3[4]; hint.Caption.rectTransform.GetWorldCorners(corners);
            var projected = corners.Select(corner => MonoViewportPoint(view, corner)).ToArray();
            bool fits = projected.All(point => point.z > view.nearClipPlane && point.x >= .02f && point.x <= .98f && point.y >= .02f && point.y <= .98f);
            CheckResult(fits && hint.Caption.enabled && !hint.CaptionSuppressed,
                "Whole initial rotation caption is visible inside the actual mono viewport: " + fixture,
                "FOV=" + view.fieldOfView + "; Aspect=" + view.aspect + "; Corners=" + string.Join(",", projected.Select(point => point.ToString("F4"))));
            var rectangle = Rect.MinMaxRect(projected.Min(point => point.x) - .003f, projected.Min(point => point.y) - .003f,
                projected.Max(point => point.x) + .003f, projected.Max(point => point.y) + .003f);
            var grip = MonoViewportPoint(view, gripPoint); var center = MonoViewportPoint(view, hint.TargetBounds.center);
            CheckResult(grip.z > 0 && !rectangle.Contains(new Vector2(grip.x, grip.y)) &&
                    (center.z <= 0 || !rectangle.Contains(new Vector2(center.x, center.y))),
                "Viewport-edge placement keeps the selected point and specimen center clear: " + fixture,
                "Caption=" + rectangle + "; Grip=" + grip.ToString("F4") + "; Center=" + center.ToString("F4"));
            CheckResult(hint.AngularWidthDegrees <= station.MaximumHintAngle + .01f,
                "Viewport placement retains the maximum caption angle: " + fixture,
                "Angle=" + hint.AngularWidthDegrees + "; Limit=" + station.MaximumHintAngle);
        }
        static void BodyRayPose(out Vector3 pose, out Quaternion rotation)
        {
            // This fixture deliberately targets a registered shell surface, never
            // the removed rotation tile. Candidate rays query the real colliders;
            // the actual hover and select are still driven by production input.
            var gesture = station.RotationGesture;
            var bounds = binding.ActiveShell.bounds;
            Vector3 centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            var directions = new[] { new Vector3(-.7f, .45f, -1), new Vector3(.7f, .45f, -1),
                new Vector3(-1, .6f, -.15f), new Vector3(1, .6f, -.15f), new Vector3(-.7f, .45f, 1), new Vector3(.7f, .45f, 1), Vector3.up };
            foreach (var direction in directions)
                foreach (float height in new[] { .55f, .25f, .75f, 0f })
                {
                    Vector3 aim = bounds.center + Vector3.up * bounds.extents.y * height;
                    var outside = aim + inspection.transform.TransformDirection(direction.normalized) * (bounds.extents.magnitude + .4f);
                    var ray = new Ray(outside, (aim - outside).normalized);
                    foreach (var hit in Physics.RaycastAll(ray, Vector3.Distance(outside, aim) + .04f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
                    {
                        if (!hit.collider || hit.collider.GetComponentInParent<XRBaseInteractor>()) continue;
                        IXRInteractable owner;
                        if (gesture.interactionManager.TryGetInteractableForCollider(hit.collider, out owner) && ReferenceEquals(owner, gesture))
                        {
                            pose = outside;
                            rotation = Quaternion.LookRotation(hit.point - outside, Vector3.up);
                            // Looking almost straight down needs a nonparallel up.
                            if (Mathf.Abs(Vector3.Dot(ray.direction, Vector3.up)) > .98f)
                                rotation = Quaternion.LookRotation(hit.point - outside, Vector3.forward);
                            return;
                        }
                        // A knowledge dot, window, another control or solid
                        // environment surface in front makes this ray unsuitable.
                        break;
                    }
                }
            throw new InvalidOperationException("No unobstructed registered helmet-body ray was found; do not substitute the removed rotation button. Body=" + bounds + "; Centre=" + centre);
        }
        static IEnumerator ProductionRaySection()
        {
            yield return RayClick(HelmetDiegeticStation.Command.Section);
            var gesture = station.SectionGesture;
            var marker = station.Controls[HelmetDiegeticStation.Command.Slide];
            var collider = marker.GetComponentsInChildren<Collider>().First();
            Vector3 normal = inspection.SectionNormalLocal;
            Vector3 centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            Vector3 pose = collider.bounds.center - marker.transform.forward * .5f;
            Quaternion rotation = Quaternion.LookRotation(collider.bounds.center - pose, Vector3.up);
            int events = 0; string kind = "";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; kind = args.interactorObject.GetType().Name; };
            gesture.selectEntered.AddListener(listener);
            try
            {
                QueueController(rightDevice, pose, rotation, false); yield return Wait(.35f);
                QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
                CheckResult(events == 1 && kind == nameof(XRRayInteractor) && gesture.IsDragging, "Production ray hold begins section translation", "Events=" + events + "; Type=" + kind + "; Drag=" + gesture.IsDragging, "production-input");
                float before = inspection.SectionOffset;
                var movement = station.SectionRail.right * .04f;
                QueueController(rightDevice, pose + movement, Quaternion.Euler(20, 35, 15) * rotation, true); yield return Wait(.2f);
                CheckResult(Mathf.Abs(inspection.SectionOffset - before - .04f * inspection.SectionRange / station.SectionRailHalfTravel) < .001f && Vector3.Distance(normal, inspection.SectionNormalLocal) < .001f,
                    "Production rail gesture moves offset without changing normal", "Before=" + before + "; Offset=" + inspection.SectionOffset + "; Normal=" + inspection.SectionNormalLocal, "production-input");
                CheckResult(Vector3.Distance(centre, inspection.transform.TransformPoint(inspection.SectionCenterLocal)) < .001f, "Cut gesture does not translate exhibit", "Centre retained.", "production-input");
                QueueController(rightDevice, pose + movement, rotation, false); yield return Wait(.2f);
                CheckResult(!gesture.IsDragging, "Production release finishes section translation", "Drag=" + gesture.IsDragging, "production-input");
            }
            finally { gesture.selectEntered.RemoveListener(listener); ParkHands(); }
            yield return Wait(.2f);
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
        }

        static IEnumerator CrossTargetReleaseGuard()
        {
            yield return RayClick(HelmetDiegeticStation.Command.Section);
            var section = station.SectionGesture;
            var marker = station.Controls[HelmetDiegeticStation.Command.Slide];
            var collider = marker.GetComponentsInChildren<Collider>().First();
            var pose = collider.bounds.center - marker.transform.forward * .5f;
            var rotation = Quaternion.LookRotation(collider.bounds.center - pose, Vector3.up);
            QueueController(rightDevice, pose, rotation, false); yield return Wait(.3f);
            QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
            CheckResult(section.IsDragging && section.isSelected, "Release guard fixture begins held section via production ray", "Dragging=" + section.IsDragging, "production-input");
            station.ResetHelmet(); yield return Wait(.6f);
            CheckResult(!section.IsDragging && !section.isSelected, "Reset cancels held section", "Dragging=" + section.IsDragging, "production-input");
            var rotate = station.Controls[HelmetDiegeticStation.Command.Rotate];
            BodyRayPose(out pose, out rotation);
            QueueController(rightDevice, pose, rotation, true); yield return Wait(.4f);
            CheckResult(!station.RotationGesture.IsDragging && !station.RotationGesture.isSelected,
                "Still-held cancelled grip cannot switch from section to rotation", "Dragging=" + station.RotationGesture.IsDragging + "; Selected=" + station.RotationGesture.isSelected, "production-input");
            QueueController(rightDevice, pose, rotation, false); yield return Wait(.3f);
            QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
            CheckResult(station.RotationGesture.IsDragging && station.RotationGesture.isSelected,
                "Release then fresh grip permits rotation", "Dragging=" + station.RotationGesture.IsDragging, "production-input");
            QueueController(rightDevice, pose, rotation, false); yield return Wait(.2f); ParkHands(); yield return Wait(.2f);
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
        }
        static IEnumerator ProductionViewRecoveryWhileHeld()
        {
            var marker = station.Controls[HelmetDiegeticStation.Command.Rotate];
            BodyRayPose(out Vector3 pose, out Quaternion rotation);
            QueueController(rightDevice, pose, rotation, false); yield return Wait(.3f);
            QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
            CheckResult(station.RotationGesture.IsDragging, "F11 fixture starts held rotation through production ray", "Dragging=" + station.RotationGesture.IsDragging, "production-input");
            var specimenPosition = inspection.transform.position; var specimenRotation = inspection.transform.rotation;
            var origin = Camera.main.GetComponentInParent<XROrigin>();
            origin.Origin.transform.position += new Vector3(.6f, 0, -.4f);
            origin.Origin.transform.rotation = Quaternion.Euler(0, 40, 0);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F11)));
            yield return Wait(.3f);
            CheckResult(!station.RotationGesture.IsDragging && !station.RotationGesture.isSelected, "Production F11 cancels held gesture before rig recovery", "Dragging=" + station.RotationGesture.IsDragging, "production-input");
            CheckResult(Vector3.Distance(specimenPosition, inspection.transform.position) < .001f && Quaternion.Angle(specimenRotation, inspection.transform.rotation) < .01f,
                "F11 rig jump adds no unintended specimen movement", "Position delta=" + Vector3.Distance(specimenPosition, inspection.transform.position) + "; Angle=" + Quaternion.Angle(specimenRotation, inspection.transform.rotation), "production-input");
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
            BodyRayPose(out pose, out rotation);
            QueueController(rightDevice, pose, rotation, true); yield return Wait(.3f);
            CheckResult(!station.RotationGesture.IsDragging, "F11 cancelled grip stays guarded while held", "Dragging=" + station.RotationGesture.IsDragging, "production-input");
            QueueController(rightDevice, pose, rotation, false); yield return Wait(.3f);
            QueueController(rightDevice, pose, rotation, true); yield return Wait(.2f);
            CheckResult(station.RotationGesture.IsDragging, "F11 guard allows fresh grip after release", "Dragging=" + station.RotationGesture.IsDragging, "production-input");
            QueueController(rightDevice, pose, rotation, false); yield return Wait(.2f); ParkHands(); yield return Wait(.2f);
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.5f);
        }
        static IEnumerator ModuleLifecycle()
        {
            int count = station.Controls.Count;
            station.enabled = false; yield return Wait(.2f);
            CheckResult(station.Controls.Values.All(c => !c.gameObject.activeInHierarchy), "Disabled module hides all new controls", "Count=" + count, "module-lifecycle");
            CheckResult(inspection.GetComponent<XRGrabInteractable>().enabled && UnityEngine.Object.FindObjectsOfType<HelmetInspectionPalette>(true).Any(p => p.gameObject.activeInHierarchy),
                "Disabled module restores original controls", "Legacy grab and palette active.", "module-lifecycle");
            station.enabled = true; yield return Wait(.3f);
            CheckResult(station.Controls.Count == count && station.IsReady, "Reenabled module reuses original generated controls", "Count=" + station.Controls.Count, "module-lifecycle");
            CheckResult(!inspection.GetComponent<XRGrabInteractable>().enabled && !UnityEngine.Object.FindObjectsOfType<HelmetInspectionPalette>(true).Any(p => p.gameObject.activeInHierarchy),
                "Reenabled module retires legacy controls without overlap", "Legacy grab and palette inactive.", "module-lifecycle");
            CheckResult(station.Controls[HelmetDiegeticStation.Command.Window].gameObject.activeInHierarchy && inspection.GetComponentsInChildren<HelmetHotspot>(true).All(h => h.enabled),
                "Reenabled module restores new controls and knowledge hotspots", "Window marker active; hotspots enabled.", "module-lifecycle");
            yield return RayClick(HelmetDiegeticStation.Command.Window);
            CheckResult(inspection.WindowAngle > 90, "Production ray controls work after module reenable", "Angle=" + inspection.WindowAngle, "production-input");
            station.Execute(HelmetDiegeticStation.Command.Window); yield return Wait(.7f);
        }
        static IEnumerator FadeAndLanguage()
        {
            var control = station.Controls[HelmetDiegeticStation.Command.Section];
            var body = station.Controls[HelmetDiegeticStation.Command.Rotate];
            var window = station.Controls[HelmetDiegeticStation.Command.Window];
            if (station.AlwaysShowHints) station.Execute(HelmetDiegeticStation.Command.Hints);
            ParkHands(); yield return Wait(1.25f);
            var colliders = control.Target.colliders.ToArray();
            var bounds = colliders.Select(c => c.bounds.size).ToArray();
            CheckResult(control.Opacity < .35f, "Idle operation UI fades to subtle marker", "Opacity=" + control.Opacity);
            var point = colliders[0].bounds.center; var pose = RayApproach(HelmetDiegeticStation.Command.Section, colliders[0]);
            QueueController(rightDevice, pose, Quaternion.LookRotation(point - pose, Vector3.up), false); yield return Wait(.4f);
            CheckResult(control.Opacity > .85f && control.Target.isHovered, "Ray intent reveals operation UI", "Opacity=" + control.Opacity + "; Hover=" + control.Target.isHovered, "production-input");
            CheckResult(colliders.All(c => c.enabled) && colliders.Select((c, i) => Vector3.Distance(c.bounds.size, bounds[i]) < .0001f).All(v => v), "Opacity changes preserve original click bounds", "Enabled and bounds sizes unchanged.");
            ParkHands(); yield return Wait(1.2f);
            CheckResult(control.Opacity < .35f, "Leaving UI fades after delay", "Opacity=" + control.Opacity);
            CheckResult(window.Opacity < .05f && window.Caption.color.a < .05f && window.Hint.color.a < .05f,
                "Unattended visor has no permanent prompt", "Opacity=" + window.Opacity + "; Caption=" + window.Caption.color.a + "; Hint=" + window.Hint.color.a);
            CheckResult(body.Opacity < .05f && body.Caption.color.a < .05f && body.Hint.color.a < .05f,
                "Unattended helmet has no duplicate rotation button or permanent label", "Opacity=" + body.Opacity + "; Caption=" + body.Caption.color.a);
            var visorCollider = window.Target.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy);
            point = VisorAimPoint(visorCollider); pose = RayApproach(HelmetDiegeticStation.Command.Window, visorCollider, point);
            QueueController(rightDevice, pose, Quaternion.LookRotation(point - pose, Vector3.up), false); yield return Wait(.4f);
            CheckResult(window.Target.isHovered && window.Opacity > .85f && window.Caption.color.a > .85f,
                "Targeting actual visor reveals its click-open hint", "Hover=" + window.Target.isHovered + "; Opacity=" + window.Opacity + "; Caption=" + window.Caption.text, "production-input");
            ParkHands(); yield return Wait(1.2f);
            station.Execute(HelmetDiegeticStation.Command.Hints); yield return Wait(.35f);
            CheckResult(station.AlwaysShowHints && station.Controls.Where(pair => pair.Value.gameObject.activeInHierarchy && !pair.Value.ContextualOnly).All(pair => pair.Value.Opacity > .85f), "Hint option keeps graphical and fixed controls readable", "Hints=" + station.AlwaysShowHints);
            CheckResult(window.Opacity < .05f && window.Caption.color.a < .05f,
                "Pinned hints do not reintroduce a permanent front-window button", "Visor opacity=" + window.Opacity);
            CheckResult(body.Opacity < .05f && body.Caption.color.a < .05f,
                "Pinned hints do not reintroduce a duplicate rotation button", "Body opacity=" + body.Opacity);
            station.Execute(HelmetDiegeticStation.Command.Settings); yield return Wait(.2f);
            station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.2f);
            var languages = new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese };
            foreach (var language in languages)
            {
                MuseumText.Channel.SetLanguage(language); yield return Wait(.2f);
                // LocalizedSourceText applies again on OnEnable; a hidden text need not
                // receive changes until it is shown. Slide and settings are active here.
                foreach (var text in station.GetComponentsInChildren<TMP_Text>(false))
                {
                    text.ForceMeshUpdate(true, true);
                    CheckResult(!text.isTextOverflowing, language + ": UI text fits " + text.transform.parent.name + "/" + text.name, text.text);
                    var missing = text.text.Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c)).Distinct().Where(c => !text.font.HasCharacter(c, true, false)).ToArray();
                    CheckResult(missing.Length == 0, language + ": saved font glyphs " + text.transform.parent.name + "/" + text.name, missing.Length == 0 ? "All characters available without dynamic additions." : "Missing=" + new string(missing));
                }
                var resetCaption = station.Controls[HelmetDiegeticStation.Command.Reset].Caption.text;
                CheckResult(resetCaption == MuseumText.Translate("Reset"), language + ": dynamic caption follows language channel", resetCaption);
                Capture("language-" + language, new Vector3(0, .35f, -1.8f));
            }
            MuseumText.Channel.SetLanguage(MuseumLanguage.English);
            station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.1f);
            station.Execute(HelmetDiegeticStation.Command.Settings); yield return Wait(.1f);
            station.Execute(HelmetDiegeticStation.Command.About); yield return Wait(.1f);
            Capture("06-fixed-about", new Vector3(0, -.10f, -1f));
            station.Execute(HelmetDiegeticStation.Command.About); station.Execute(HelmetDiegeticStation.Command.Hints);
        }
        static IEnumerator ViewRecovery()
        {
            var origin = Camera.main.GetComponentInParent<XROrigin>();
            station.Execute(HelmetDiegeticStation.Command.Window); station.Execute(HelmetDiegeticStation.Command.Section); station.Execute(HelmetDiegeticStation.Command.Voice); yield return Wait(.7f);
            int era = eras.CurrentIndex; float angle = inspection.WindowAngle; var position = inspection.transform.position; var rotation = inspection.transform.rotation;
            var language = MuseumText.Channel.Current; bool sound = inspection.SoundEnabled, motion = inspection.ReduceMotion, hints = station.AlwaysShowHints;
            var cameraHome = Camera.main.transform.position;
            origin.Origin.transform.position += new Vector3(.7f, 0, -.5f); origin.Origin.transform.rotation = Quaternion.Euler(0, 35, 0);
            station.Execute(HelmetDiegeticStation.Command.ResetView); yield return Wait(.3f);
            CheckResult(Vector2.Distance(new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z), new Vector2(cameraHome.x, cameraHome.z)) < .01f,
                "Reset View restores initial horizontal view", Camera.main.transform.position.ToString("F4"));
            CheckResult(eras.CurrentIndex == era && Mathf.Abs(inspection.WindowAngle - angle) < .01f && inspection.IsCutaway && demo.IsVisible && demo.CurrentClue == HelmetExplorationController.Clue.Voice &&
                Vector3.Distance(position, inspection.transform.position) < .001f && Quaternion.Angle(rotation, inspection.transform.rotation) < .01f && language == MuseumText.Channel.Current && sound == inspection.SoundEnabled && motion == inspection.ReduceMotion && hints == station.AlwaysShowHints,
                "Reset View preserves exhibit and preferences", "View recovery does not reset specimen or demos.");
            station.Execute(HelmetDiegeticStation.Command.Reset); yield return Wait(.4f);
        }

        static void Capture(string file, Vector3 offset)
        {
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) text.ForceMeshUpdate(true);
            var centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            var go = new GameObject("Temporary diegetic QA camera"); var camera = go.AddComponent<Camera>();
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
            camera.transform.position = centre + offset; camera.transform.LookAt(centre + Vector3.down * .08f);
            camera.fieldOfView = 49; camera.nearClipPlane = .015f; camera.farClipPlane = 70;
            var target = RenderTexture.GetTemporary(1600, 1200, 24); var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                else { camera.targetTexture = target; camera.Render(); }
                RenderTexture.active = target;
                pixels = new Texture2D(1600, 1200, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1600, 1200), 0, 0); pixels.Apply();
                Directory.CreateDirectory(Output); file += ".png"; File.WriteAllBytes(Path.Combine(Output, file), pixels.EncodeToPNG()); report.diagnosticCaptures.Add(file);
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(go); }
        }
        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip)
        {
            var parent = (device == leftDevice ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(world), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation, grip = grip ? 1 : 0 }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        { QueueController(rightDevice, new Vector3(2, .45f, -.5f), Quaternion.identity, false); QueueController(leftDevice, new Vector3(-2, .45f, -.5f), Quaternion.identity, false); }
        static void QueueHead() => QueueHead(new Vector3(0, 1.65f, 0), Quaternion.identity);
        static void QueueHead(Vector3 position, Quaternion rotation)
        {
            var state = new XRSimulatedHMDState { isTracked = true, trackingState = 3, devicePosition = position, centerEyePosition = position,
                leftEyePosition = position + rotation * Vector3.left * .032f, rightEyePosition = position + rotation * Vector3.right * .032f, deviceRotation = rotation,
                centerEyeRotation = rotation, leftEyeRotation = rotation, rightEyeRotation = rotation };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(head, state));
        }
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        static void CheckResult(bool passed, string name, string detail, string layer = "public-api") => report.checks.Add(new Check { passed = passed, name = name, detail = detail, layer = layer });
        static void OnLog(string condition, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(condition + "\n" + stack); }
        static void Finish(string failure)
        {
            if (finishing) return; finishing = true; report.failure = failure; report.finishedUtc = DateTime.UtcNow.ToString("o");
            Application.logMessageReceived -= OnLog; InputSystem.onBeforeUpdate -= FlushInput; PendingInput.Clear();
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, head, keyboard, addedMouse ? mouse : null }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedSettings) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; changedSettings = false; }
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
            bool passed = failure == null && report.errors.Count == 0 && report.checks.All(c => c.passed);
            SessionState.SetInt(Prefix + "Exit", passed ? 0 : 1);
            Debug.Log("Diegetic UI QA: " + (passed ? "PASS" : "FAIL") + "; checks=" + report.checks.Count + "; output=" + Output);
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
