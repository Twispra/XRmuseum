#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Museum.Inspection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Museum.PhysicalHands.EditorTools;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using Clue = Museum.Exploration.HelmetExplorationController.Clue;
using Page = Museum.Exploration.HelmetExplorationController.Page;

namespace Museum.Exploration.Editor
{
    /// <summary>Acceptance checks for the saved main scene; never builds or saves product assets.</summary>
    [InitializeOnLoad]
    public static class HelmetExplorationValidation
    {
        const string Prefix = "Museum.HelmetExplorationQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static HelmetExplorationController exploration;
        static HelmetConnectionDemonstration demonstration;
        static HelmetExplorationPanel panel;
        static HelmetInspectionController inspection;
        static HelmetExhibitReset reset;
        static ActionBasedController rightController, leftController;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD hmd;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedMouse, changedSettings, finishing;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static double started;
        static double exitNotBefore;
        static int pendingExitCode = -1;
        static int lastFrame = -1;
        static Report report;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string OutputDirectory => Path.Combine(RepoRoot, "artifacts/helmet-exploration/qa/runs", SessionState.GetString(Prefix + "RunId", "missing-run-id"));

        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class Provenance
        {
            public string utc, commit, branch, dirty, unity;
            public List<Fingerprint> files = new List<Fingerprint>();
        }
        [Serializable] sealed class Check { public string kind, name, detail; public bool passed; }
        [Serializable] sealed class TextSample { public string page, label; public float fontSize; public Vector2 rectangle; public bool overflow; }
        [Serializable] sealed class CaptureSample
        {
            public string file, view;
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView, aspect;
        }
        [Serializable] sealed class AnimationSample
        {
            public string name, stage;
            public int phase;
            public float normalizedProgress, routeProjectionBefore, routeProjectionAfter;
            public Quaternion wheelRotation;
            public Vector3 pistonLocalPosition, packetWorldPosition;
        }
        [Serializable] sealed class ResourceInventory
        {
            public int transforms, renderers, lineRenderers, meshes, materials;
            public bool SameAs(ResourceInventory other) => transforms == other.transforms && renderers == other.renderers &&
                lineRenderers == other.lineRenderers && meshes == other.meshes && materials == other.materials;
        }
        [Serializable] sealed class Report
        {
            public string utc, status = "running", failure = "", scene = ScenePath;
            public string inputMethod = "InputSystem.QueueStateEvent on synthetic XR controller/HMD devices in the normal Dynamic player input update, through production Input Actions and interactors. Home uses a synthetic keyboard. No SelectEnter/SelectExit calls are made by this validator.";
            public string limitations = "Official simulator presence is checked, then its state writer is disabled. OS keyboard/mouse simulator controls, physical headset input, learning outcomes, comfort, historical accuracy, frame time and executable builds are not tested. Public-API state tests and pose fixtures are labelled separately from input-driven tests. Screenshots are monocular editor captures requiring visual review. after-inspect, after-welcome, after-playing and after-help copy one unchanged production camera pose and field of view; no before image is recreated. -panel-closeup and -diagram-closeup files are temporary diagnostic camera views, not production views. Animation QA checks an explicitly schematic mechanism, not physical airflow or an exact historical pump/telephone reconstruction. No text overflow proves only layout containment, not readability in a headset; resolved font sizes are recorded.";
            public Provenance provenance;
            public float durationSeconds;
            public float batchExitSettleSeconds = 20f;
            public bool officialSimulatorWasPresent;
            public List<Check> checks = new List<Check>();
            public List<string> errors = new List<string>();
            public List<string> screenshots = new List<string>();
            public List<CaptureSample> captureSamples = new List<CaptureSample>();
            public List<TextSample> textSamples = new List<TextSample>();
            public List<AnimationSample> animationSamples = new List<AnimationSample>();
        }

        static HelmetExplorationValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
        }

        [MenuItem("Museum/Exploration/Validate main museum (Play Mode)")]
        public static void RunInEditor() => Begin(false);
        // Use -executeMethod with this entry point and omit -quit.
        public static void RunBatch() => Begin(true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("Exploration QA Keyboard");
            mouse = Mouse.current;
            addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>("Exploration QA Mouse");
        }

        static void Begin(bool batch)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before exploration validation.");
            if (!batch && SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save your scene before validation; the validator will not save it.");
            EditorSceneManager.OpenScene(ScenePath);
            string runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-exploration-" + Guid.NewGuid().ToString("N");
            SessionState.SetString(Prefix + "RunId", runId);
            SessionState.SetString(Prefix + "Provenance", JsonUtility.ToJson(CaptureProvenance()));
            Directory.CreateDirectory(OutputDirectory);
            SessionState.SetBool(Prefix + "Active", true);
            SessionState.SetBool(Prefix + "Batch", batch);
            SessionState.SetInt(Prefix + "ExitCode", -1);
            EditorApplication.EnterPlaymode();
        }

        static Provenance CaptureProvenance()
        {
            var result = new Provenance { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), branch = Git("branch --show-current"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion };
            var paths = new HashSet<string>(StringComparer.Ordinal)
            {
                ScenePath, "Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectSettings.asset",
                "Assets/Samples/XR Interaction Toolkit/2.6.4/Starter Assets/XRI Default Input Actions.inputactions",
                "Assets/Samples/XR Interaction Toolkit/2.6.4/XR Device Simulator/XR Device Controller Controls.inputactions"
            };
            foreach (string path in AssetDatabase.GetDependencies(ScenePath, true)) if (File.Exists(path)) paths.Add(path);
            foreach (string path in Directory.GetFiles("Assets/Museum/Features/HelmetExploration", "*", SearchOption.AllDirectories)) paths.Add(path.Replace('\\', '/'));
            foreach (string path in paths.OrderBy(p => p, StringComparer.Ordinal))
                result.files.Add(new Fingerprint { path = path, sha256 = Hash(path) });
            return result;
        }

        static string Git(string arguments)
        {
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = RepoRoot, UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                start.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(5000)) { process.Kill(); return "UNAVAILABLE: git timeout"; }
                    if (!stdout.Wait(1000) || !stderr.Wait(1000)) return "UNAVAILABLE: incomplete git output";
                    return process.ExitCode == 0 ? stdout.Result.Trim() : "UNAVAILABLE: " + stderr.Result.Trim();
                }
            }
            catch (Exception ex) { return "UNAVAILABLE: " + ex.Message; }
        }

        static string Hash(string path)
        {
            if (!File.Exists(path)) return "MISSING";
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup; lastFrame = -1; finishing = false;
                report = new Report { utc = DateTime.UtcNow.ToString("o"), provenance = JsonUtility.FromJson<Provenance>(SessionState.GetString(Prefix + "Provenance", "{}")) };
                Application.logMessageReceived += OnLog;
                InputSystem.onBeforeUpdate += FlushInput;
                Routines.Clear(); PendingInput.Clear(); Routines.Push(Validate());
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && !finishing && report != null)
                Finish("Play Mode exited before validation completed.", false);
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                int code = SessionState.GetInt(Prefix + "ExitCode", 1);
                bool batch = SessionState.GetBool(Prefix + "Batch", false);
                SessionState.SetBool(Prefix + "Active", false);
                // Keep pumping the Editor while background services finish; never block the main thread.
                // Earlier immediate batch exits completed all checks but hung in native shutdown.
                if (batch) { pendingExitCode = code < 0 ? 1 : code; exitNotBefore = EditorApplication.timeSinceStartup + 20; }
            }
        }

        static void Tick()
        {
            if (pendingExitCode >= 0 && EditorApplication.timeSinceStartup >= exitNotBefore)
            {
                int code = pendingExitCode; pendingExitCode = -1;
                EditorApplication.Exit(code);
                return;
            }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 240) { Finish("Validation exceeded 240 seconds."); return; }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (Application.isBatchMode) System.Threading.Thread.Sleep(11);
            try
            {
                while (Routines.Count > 0)
                {
                    var current = Routines.Peek();
                    if (!current.MoveNext()) { Routines.Pop(); continue; }
                    if (current.Current is IEnumerator nested) { Routines.Push(nested); continue; }
                    return;
                }
                Finish(null);
            }
            catch (Exception ex) { Finish(ex.ToString()); }
        }

        static IEnumerator Validate()
        {
            yield return Wait(2f);
            Require(SceneManager.GetActiveScene().path == ScenePath, "structure", "saved main scene", SceneManager.GetActiveScene().path);
            var controllers = UnityEngine.Object.FindObjectsOfType<HelmetExplorationController>(true);
            Require(controllers.Length == 1, "structure", "one exploration controller", "Count=" + controllers.Length);
            exploration = controllers[0];
            demonstration = UnityEngine.Object.FindObjectOfType<HelmetConnectionDemonstration>(true);
            panel = UnityEngine.Object.FindObjectOfType<HelmetExplorationPanel>(true);
            inspection = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            reset = inspection ? inspection.GetComponent<HelmetExhibitReset>() : null;
            Require(exploration && demonstration && panel && inspection && reset && Camera.main, "structure", "runtime dependencies", "Exploration and original inspection components coexist.");
            Require(!exploration.AirSeen && !exploration.VoiceSeen && exploration.HintRequests == 0 && exploration.CurrentPage == Page.Welcome && !demonstration.IsPlaying,
                "state", "fresh visit starts without invented discoveries", Describe());

            var simulators = UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>();
            report.officialSimulatorWasPresent = simulators.Length > 0;
            Require(report.officialSimulatorWasPresent, "structure", "official simulator is present", "Its device writer is disabled only for this automated run.");
            foreach (var simulator in simulators) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard); if (addedMouse) InputSystem.EnableDevice(mouse);
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).FirstOrDefault(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).FirstOrDefault(c => c.name == "Left Controller");
            Require(rightController && leftController, "structure", "both production action controllers", "No replacement rig is created.");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            hmd = InputSystem.AddDevice<XRSimulatedHMD>(); QueueHead(); ParkHands();
            yield return Wait(.5f);

            Require(!panel.IsExploring, "state", "existing inspection is the initial mode", "Exploration is optional.");
            Require(panel.ProgressiveLayoutEnabled && !panel.IsReading && !panel.InspectionToolsExpanded,
                "state", "saved scene starts with progressive controls collapsed", "The saved production layout is being tested.");
            CheckButtons(true, "Exploration INSPECT", "Exploration EXPLORE", "Exploration HELP", "Exploration MORE TOOLS",
                "Inspection WINDOW", "Inspection SECTION", "Inspection FLOAT", "Inspection RESET");
            CheckButtons(false, "Inspection TURN LEFT", "Inspection TURN RIGHT", "Inspection TILT UP", "Inspection TILT DOWN",
                "Inspection EDIT CUT", "Inspection DEEPER", "Exploration AIR", "Exploration NEW VISIT");
            Capture("after-inspect");
            yield return Press("Exploration HELP", false);
            Require(panel.IsReading && !panel.IsExploring, "XR input", "Help opens from inspection without changing its context", Describe());
            CheckReadingLayout("help-from-inspection");
            CheckButtons(true, "Exploration CLOSE");
            CheckButtons(false, "Inspection WINDOW", "Exploration REPLAY", "Exploration STOP", "Exploration NEW VISIT");
            yield return Wait(1f);
            Require(panel.IsReading, "state", "Help remains open until explicitly closed", "No reading timeout during the observation interval.");
            yield return Press("Exploration CLOSE", false);
            Require(!panel.IsReading && !panel.IsExploring && !demonstration.IsPlaying,
                "XR input", "closing Help restores inspection without playback", Describe());
            yield return Press("Exploration EXPLORE", false, true);
            Require(panel.IsExploring, "XR input", "exploration tab opens the optional page", "Grip selected the shared palette tab.");
            CheckWelcomeControls();
            Capture("00-welcome");
            Capture("after-welcome");
            yield return OpenMorePage("Exploration REVIEW", false);
            Require(exploration.CurrentPage == Page.Review && !exploration.AirSeen && !exploration.VoiceSeen &&
                ReviewHasUnseenClue("Air") && ReviewHasUnseenClue("Voice"),
                "XR input", "empty review reached through More acknowledges no viewed clues", exploration.Body);
            CheckButtons(true, "Exploration NEW VISIT");
            CheckPageText("empty-review");
            yield return Press("Exploration BACK", false);
            Require(exploration.CurrentPage == Page.Welcome && !exploration.AirSeen && !exploration.VoiceSeen,
                "XR input", "BACK returns to welcome without inventing discovery", Describe());
            yield return Wait(.8f);
            Require(exploration.HintRequests == 0 && !demonstration.IsPlaying && !exploration.AirSeen && !exploration.VoiceSeen,
                "state", "idle does not add hints or autoplay", Describe());

            yield return Press("Exploration AIR", false, true);
            Require(exploration.CurrentPage == Page.Air && exploration.AirSeen && !exploration.VoiceSeen && demonstration.IsPlaying && demonstration.CurrentClue == Clue.Air,
                "XR input", "right hand discovers air first", Describe());
            CheckButtons(true, "Exploration STOP", "Exploration DETAILS");
            CheckButtons(false, "Exploration REPLAY", "Exploration NEW VISIT", "Exploration AIR", "Exploration VOICE");
            Capture("01-air-right-hand");
            Capture("after-playing");
            yield return ValidateInspectHide(true);
            yield return Press("Exploration REPLAY", false);
            yield return ValidateReadingViews();
            yield return OpenMorePage("Exploration REVIEW", false);
            Require(exploration.CurrentPage == Page.Review && exploration.AirSeen && !exploration.VoiceSeen && ReviewHasUnseenClue("Voice"),
                "XR input", "partial review identifies the unviewed clue", exploration.Body);
            yield return Press("Exploration BACK", false);
            yield return Press("Exploration AIR", false);
            Require(!demonstration.IsPlaying && exploration.CurrentPage == Page.Air && !exploration.VoiceSeen,
                "XR input", "revisiting air does not automatically replay", Describe());
            CheckButtons(true, "Exploration REPLAY", "Exploration DETAILS");
            CheckButtons(false, "Exploration STOP", "Exploration NEW VISIT");
            yield return Press("Exploration BACK", true);
            yield return Press("Exploration VOICE", true, true);
            Require(exploration.CurrentPage == Page.Voice && exploration.AirSeen && exploration.VoiceSeen && demonstration.IsPlaying && demonstration.CurrentClue == Clue.Voice,
                "XR input", "left hand discovers voice without an order lock", Describe());
            Capture("02-voice-left-hand");
            yield return Press("Exploration DETAILS", true);
            CheckReadingLayout("voice-details");
            yield return Press("Exploration CLOSE", true);
            Require(!panel.IsReading && exploration.CurrentPage == Page.Voice && demonstration.IsPlaying,
                "XR input", "Voice Details returns to the same running clue", Describe());
            yield return Press("Exploration STOP", true);
            Require(!demonstration.IsPlaying, "XR input", "left hand stops voice", Describe());
            yield return Press("Exploration REPLAY", true);
            Require(demonstration.IsPlaying && demonstration.CurrentClue == Clue.Voice, "XR input", "explicit replay starts selected clue", Describe());
            yield return Press("Exploration STOP", true);
            int hints = exploration.HintRequests;
            yield return OpenMorePage("Exploration HINT", true);
            Require(exploration.HintRequests == hints + 1 && exploration.CurrentPage == Page.Hint && exploration.AirSeen && exploration.VoiceSeen,
                "XR input", "hint is explicitly requested and preserves discoveries", Describe());
            Capture("02b-hint");
            yield return OpenMorePage("Exploration REVIEW", false);
            Require(exploration.CurrentPage == Page.Review && exploration.AirSeen && exploration.VoiceSeen,
                "XR input", "review can be opened after either clue", exploration.Body);
            Capture("03-review");

            yield return Press("Exploration NEW VISIT", true);
            Require(!exploration.AirSeen && !exploration.VoiceSeen && exploration.HintRequests == 0 && exploration.CurrentPage == Page.Welcome && !demonstration.IsPlaying,
                "XR input", "new visit clears discoveries and hints", Describe());
            CheckWelcomeControls();
            yield return OpenMorePage("Exploration HINT", false);
            Require(exploration.HintRequests == 1 && !exploration.AirSeen && !exploration.VoiceSeen && !demonstration.IsPlaying,
                "XR input", "hint itself does not mark clues as viewed", Describe());
            yield return Press("Exploration BACK", false);
            yield return Press("Exploration VOICE", true);
            Require(exploration.VoiceSeen && !exploration.AirSeen && demonstration.CurrentClue == Clue.Voice && demonstration.IsPlaying,
                "XR input", "voice can be the first clue in a fresh visit", Describe());
            yield return Press("Exploration STOP", true);
            yield return Press("Exploration BACK", false);
            yield return Press("Exploration AIR", false);
            Require(exploration.AirSeen && exploration.VoiceSeen && demonstration.IsPlaying && demonstration.CurrentClue == Clue.Air,
                "XR input", "voice then air completes the opposite discovery order", Describe());
            yield return Press("Exploration STOP", false);
            yield return Press("Exploration BACK", false);
            yield return Press("Exploration VOICE", true);
            Require(!demonstration.IsPlaying, "XR input", "repeated voice selection does not autoplay", Describe());

            yield return OpenMorePage("Exploration REVIEW", false);
            yield return Press("Exploration NEW VISIT", true);
            var hotspot = UnityEngine.Object.FindObjectOfType<HelmetExplorationHotspot>();
            Require(hotspot, "structure", "air connector hotspot exists", "An optional clue target is attached to the original helmet.");
            yield return Press(hotspot.gameObject.name, false);
            Require(exploration.AirSeen && !exploration.VoiceSeen && demonstration.IsPlaying,
                "XR input", "connector hotspot opens the air clue", Describe());
            yield return Wait(.15f);
            int resetCount = reset.ResetCount;
            QueueKeyboard(new KeyboardState(Key.Home)); yield return Wait(.15f);
            QueueKeyboard(new KeyboardState()); yield return Wait(.25f);
            Require(reset.ResetCount > resetCount && exploration.AirSeen && !exploration.VoiceSeen && !demonstration.IsPlaying,
                "keyboard input", "model Home reset stops demonstration but preserves discovery", Describe());

            exploration.Replay();
            yield return Wait(.15f);
            Vector3 endpointBefore = demonstration.AirEndpointWorld;
            inspection.RotateDisplayAround(Vector3.up, 48f);
            // A labelled pose fixture tests endpoint attachment, not a synthetic claim of user grabbing.
            var rigidbody = inspection.GetComponent<Rigidbody>();
            inspection.transform.position += new Vector3(.20f, .08f, .03f);
            rigidbody.position = inspection.transform.position; Physics.SyncTransforms();
            yield return Wait(.25f);
            Require(demonstration.IsPlaying && Vector3.Distance(demonstration.AirEndpointWorld, endpointBefore) > .05f && Vector3.Distance(demonstration.AirEndpointWorld, demonstration.AirTargetWorld) < .003f,
                "public API + pose fixture", "air line follows the moved and rotated connector", "endpoint=" + demonstration.AirEndpointWorld + "; target=" + demonstration.AirTargetWorld);
            Capture("04-moved-air-endpoint");
            inspection.ResetInspection(); yield return Wait(.25f);
            Require(exploration.AirSeen && !exploration.VoiceSeen && !demonstration.IsPlaying,
                "public API", "inspection reset also preserves discovery and stops playback", Describe());

            exploration.Replay();
            yield return Wait(.2f);
            yield return Press("Exploration DETAILS", false);
            yield return Wait(HelmetConnectionDemonstration.Duration + .3f);
            Require(!demonstration.IsPlaying && demonstration.IsVisible && exploration.AirSeen && !exploration.VoiceSeen,
                "public API + elapsed time", "completed animation leaves a static diagram for unhurried viewing", Describe());
            Require(panel.IsReading, "state", "finishing playback does not close Details", Describe());
            CheckButtons(true, "Exploration STOP", "Exploration CLOSE");
            CheckReadingLayout("air-details-after-playback-finishes");
            yield return Press("Exploration CLOSE", false);
            Capture("04b-static-air-diagram");
            CheckButtons(true, "Exploration REPLAY", "Exploration STOP", "Exploration DETAILS");
            yield return Press("Exploration DETAILS", false);
            Require(panel.IsReading && !demonstration.IsPlaying && demonstration.IsVisible,
                "XR input", "Details opens over the retained static model", Describe());
            CheckReadingLayout("static-air-details");
            CheckButtons(true, "Exploration STOP", "Exploration CLOSE");
            yield return Press("Exploration CLOSE", false);
            Require(!panel.IsReading && !demonstration.IsPlaying && demonstration.IsVisible,
                "XR input", "closing static Details retains the diagram without replaying", Describe());
            yield return ValidateInspectHide(false);
            Require(!demonstration.IsPlaying && !demonstration.IsVisible && exploration.AirSeen && !exploration.VoiceSeen,
                "XR input", "Hide demo hides the retained diagram without erasing discoveries", Describe());

            bool previousReduceMotion = inspection.ReduceMotion;
            inspection.ReduceMotion = true;
            exploration.Replay();
            yield return Wait(.2f);
            var pulse = demonstration.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Travelling signal");
            var route = demonstration.GetComponentsInChildren<LineRenderer>(true).FirstOrDefault(l => l.name == "Connection route");
            Require(pulse && route && demonstration.IsVisible && !pulse.gameObject.activeInHierarchy,
                "public API", "reduced motion keeps the route visible without the travelling pulse", Describe());
            Vector3[] staticRoute = new Vector3[route.positionCount]; route.GetPositions(staticRoute);
            yield return Wait(.45f);
            Require(staticRoute.Length == route.positionCount && staticRoute.Select((p, i) => Vector3.Distance(p, route.GetPosition(i))).All(distance => distance < .0001f) &&
                !pulse.gameObject.activeInHierarchy && exploration.AirSeen && !exploration.VoiceSeen,
                "public API + elapsed time", "reduced-motion diagram remains still and preserves discovery", "Compared line vertices over 0.45 seconds; this does not establish user comfort.");
            Capture("04c-reduced-motion");
            exploration.Stop(); inspection.ReduceMotion = previousReduceMotion;
            Require(!demonstration.IsVisible, "public API", "STOP hides reduced-motion diagram", Describe());

            panel.ShowExploration();
            var feature = exploration.gameObject;
            var tabs = panel.transform.Find("Exploration tabs");
            Require(tabs && tabs.gameObject.activeInHierarchy, "structure", "exploration tabs are available before disable", "Saved shared palette tabs.");
            feature.SetActive(false);
            yield return Wait(.3f);
            Require(!panel.IsExploring && !tabs.gameObject.activeInHierarchy,
                "component lifecycle", "disabling exploration restores inspection and hides unavailable tabs", "No dead-end optional page remains visible.");
            yield return Press("Inspection WINDOW", false); yield return Wait(.85f);
            Require(inspection.WindowAngle > HelmetInspectionController.MaximumWindowAngle - .5f,
                "XR input", "original WINDOW works while exploration feature is disabled", "Angle=" + inspection.WindowAngle);
            yield return Press("Inspection WINDOW", false); yield return Wait(.85f);
            Require(inspection.WindowAngle < .5f, "XR input", "original WINDOW closes while feature is disabled", "Angle=" + inspection.WindowAngle);
            feature.SetActive(true);
            yield return Wait(.3f);
            Require(tabs.gameObject.activeInHierarchy && exploration.AirSeen && !exploration.VoiceSeen,
                "component lifecycle", "reenabling feature restores tabs and preserves visit records", Describe());
            yield return Press("Exploration EXPLORE", false);
            Require(panel.IsExploring && exploration.AirSeen && !exploration.VoiceSeen, "XR input", "restored exploration tab is usable", Describe());

            yield return Press("Exploration INSPECT", false);
            Require(!panel.IsExploring, "XR input", "inspection mode remains reachable", "Panel switched back to existing inspection controls.");
            yield return Press("Inspection WINDOW", false); yield return Wait(.85f);
            Require(inspection.WindowAngle > HelmetInspectionController.MaximumWindowAngle - .5f, "XR input", "existing WINDOW still opens", "Angle=" + inspection.WindowAngle);
            yield return Press("Inspection SECTION", true);
            Require(inspection.IsCutaway, "XR input", "existing SECTION still toggles with left hand", "Original inspection controller remains active.");
            yield return ValidateInspectionTools();
            yield return Press("Exploration EXPLORE", false);
            Require(panel.IsExploring && exploration.AirSeen && !exploration.VoiceSeen, "XR input", "returning to exploration preserves the visit", Describe());
            yield return ValidateEnrichedDemonstrations();
            inspection.ResetInspection(); exploration.RestartVisit(); panel.ShowExploration();
            yield return Wait(.3f);
            Require(!inspection.IsCutaway && inspection.WindowAngle < .5f && !exploration.AirSeen && !exploration.VoiceSeen && !demonstration.IsPlaying,
                "public API", "clean final visit without changing saved scene", Describe());
            Capture("05-final-welcome");
            Require(report.errors.Count == 0, "logs", "no Unity errors during validation", string.Join("\n", report.errors));
        }

        static IEnumerator OpenMorePage(string name, bool left)
        {
            if (!panel.MoreExpanded) yield return Press("Exploration MORE", left);
            Require(panel.MoreExpanded, "XR input", "More opens optional navigation", Describe());
            CheckButtons(true, "Exploration HINT", "Exploration REVIEW");
            CheckPaletteText("more-navigation");
            yield return Press(name, left);
        }

        static void CheckWelcomeControls()
        {
            Require(exploration.CurrentPage == Page.Welcome && !panel.IsReading && !panel.MoreExpanded,
                "state", "Welcome returns with optional navigation collapsed", Describe());
            CheckButtons(true, "Exploration AIR", "Exploration VOICE", "Exploration MORE");
            CheckButtons(false, "Exploration HINT", "Exploration REVIEW", "Exploration REPLAY", "Exploration STOP",
                "Exploration DETAILS", "Exploration BACK", "Exploration NEW VISIT", "Exploration CLOSE");
        }

        static bool ReviewHasUnseenClue(string clue)
        {
            return exploration.Body.Split('\n').Any(line => line.StartsWith(clue + ":", StringComparison.OrdinalIgnoreCase) &&
                line.IndexOf("not seen", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static IEnumerator ValidateReadingViews()
        {
            var context = exploration.CurrentPage;
            yield return Press("Exploration DETAILS", false);
            Require(panel.IsReading && exploration.CurrentPage == context && demonstration.IsPlaying,
                "XR input", "Details preserves the playing clue context", Describe());
            CheckReadingLayout("playing-air-details");
            CheckButtons(true, "Exploration CLOSE", "Exploration STOP");
            CheckButtons(false, "Exploration DETAILS", "Exploration REPLAY", "Exploration NEW VISIT");
            yield return Wait(1f);
            Require(panel.IsReading, "state", "Details stays open for reading", "No automatic close during the observation interval.");
            float progressBeforeClose = demonstration.NormalizedProgress;
            yield return Press("Exploration CLOSE", false);
            Require(!panel.IsReading && panel.IsExploring && exploration.CurrentPage == context && demonstration.IsPlaying &&
                demonstration.NormalizedProgress >= progressBeforeClose,
                "XR input", "closing Details restores its clue without restarting playback", Describe());
            yield return Press("Exploration HELP", true);
            Require(panel.IsReading && exploration.CurrentPage == context, "XR input", "Help keeps the selected clue context", Describe());
            CheckReadingLayout("help-over-playing-demo");
            CheckButtons(true, "Exploration STOP", "Exploration CLOSE");
            Capture("after-help");
            yield return Press("Exploration STOP", true);
            Require(panel.IsReading && !demonstration.IsPlaying && !demonstration.IsVisible,
                "XR input", "Hide demo remains reachable during Help and keeps reading open", Describe());
            CheckButtons(false, "Exploration STOP", "Exploration REPLAY");
            CheckReadingLayout("help-after-hiding-demo");
            yield return Wait(1f);
            Require(panel.IsReading && !demonstration.IsPlaying, "state", "Help remains open after hiding the demo", Describe());
            yield return Press("Exploration CLOSE", true);
            Require(!panel.IsReading && panel.IsExploring && exploration.CurrentPage == context && !demonstration.IsPlaying && !demonstration.IsVisible,
                "XR input", "closing Help restores the clue without automatic playback", Describe());
            yield return Press("Exploration DETAILS", false);
            CheckReadingLayout("hidden-air-details");
            yield return Press("Exploration CLOSE", false);
            Require(!panel.IsReading && exploration.CurrentPage == context && !demonstration.IsPlaying && !demonstration.IsVisible,
                "XR input", "closing Details over a hidden demo does not start it", Describe());
        }

        static IEnumerator ValidateInspectHide(bool playing)
        {
            var context = exploration.CurrentPage;
            bool airSeen = exploration.AirSeen, voiceSeen = exploration.VoiceSeen;
            bool originalCutaway = inspection.IsCutaway, originalFloat = inspection.IsFloating;
            yield return Press("Exploration INSPECT", false);
            Require(!panel.IsExploring && !panel.IsReading && demonstration.IsVisible && demonstration.IsPlaying == playing,
                "XR input", "Inspect retains the " + (playing ? "playing" : "static") + " demo until explicitly hidden", Describe());
            CheckButtons(true, "Exploration STOP", "Inspection SECTION", "Inspection FLOAT");
            CheckButtons(false, "Exploration AIR", "Exploration VOICE", "Exploration REPLAY", "Exploration DETAILS", "Exploration MORE", "Exploration BACK");
            if (!originalCutaway) yield return Press("Inspection SECTION", false);
            if (!originalFloat) yield return Press("Inspection FLOAT", true);
            Require(inspection.IsCutaway && inspection.IsFloating, "XR input", "inspection state prepared through visible controls", "Cutaway and float are both active before Hide demo.");
            CheckPaletteText("inspect-with-" + (playing ? "playing" : "static") + "-demo");
            yield return Press("Exploration STOP", false);
            Require(!demonstration.IsPlaying && !demonstration.IsVisible && inspection.IsCutaway && inspection.IsFloating &&
                exploration.AirSeen == airSeen && exploration.VoiceSeen == voiceSeen,
                "XR input", "Hide demo in Inspect preserves section, float and discoveries", Describe());
            CheckButtons(false, "Exploration STOP");
            if (!originalCutaway) yield return Press("Inspection SECTION", false);
            if (!originalFloat) yield return Press("Inspection FLOAT", true);
            yield return Press("Exploration EXPLORE", false);
            Require(panel.IsExploring && !panel.IsReading && exploration.CurrentPage == context && !demonstration.IsPlaying && !demonstration.IsVisible &&
                inspection.IsCutaway == originalCutaway && inspection.IsFloating == originalFloat,
                "XR input", "Explore restores the clue after hiding its demo in Inspect", Describe());
            CheckButtons(true, "Exploration REPLAY", "Exploration DETAILS");
        }

        static IEnumerator ValidateInspectionTools()
        {
            CheckButtons(true, "Inspection WINDOW", "Inspection SECTION", "Inspection FLOAT", "Inspection RESET",
                "Inspection EDIT CUT", "Inspection ALIGN VIEW", "Exploration MORE TOOLS");
            CheckButtons(false, "Inspection TURN LEFT", "Inspection TILT UP", "Inspection DEEPER", "Inspection TOP CUT");
            yield return Press("Inspection FLOAT", false);
            Require(inspection.IsFloating, "XR input", "FLOAT remains directly reachable", "Float stabilizes the model while checking optional rotation tools.");
            yield return Press("Exploration MORE TOOLS", false);
            Require(panel.InspectionToolsExpanded, "XR input", "More tools exposes optional inspection controls", "Expanded through the production button.");
            CheckButtons(true, "Inspection TURN LEFT", "Inspection TURN RIGHT", "Inspection TILT UP", "Inspection TILT DOWN",
                "Inspection DEEPER", "Inspection SHALLOWER", "Inspection TOP CUT", "Inspection FLIP CUT");
            CheckPaletteText("inspection-more-tools");
            Quaternion before = inspection.transform.rotation;
            yield return Press("Inspection TURN LEFT", false);
            Require(Quaternion.Angle(before, inspection.transform.rotation) > 20f,
                "XR input", "More tools retains model turn", "Actual production TURN LEFT button changed the exhibit rotation.");
            before = inspection.transform.rotation;
            yield return Press("Inspection TILT UP", true);
            Require(Quaternion.Angle(before, inspection.transform.rotation) > 10f,
                "XR input", "More tools retains model tilt", "Actual production TILT UP button changed the exhibit rotation.");
            float depthBefore = inspection.SectionOffset;
            yield return Press("Inspection DEEPER", false);
            Require(Mathf.Abs(depthBefore - inspection.SectionOffset) > .001f,
                "XR input", "More tools retains section depth", "Section offset=" + inspection.SectionOffset);
            yield return Press("Inspection TOP CUT", true);
            Require(Vector3.Dot(inspection.SectionNormalLocal, Vector3.up) > .99f,
                "XR input", "More tools retains section direction", "Section normal=" + inspection.SectionNormalLocal);
            Vector3 normalBefore = inspection.SectionNormalLocal;
            yield return Press("Inspection FLIP CUT", false);
            Require(Vector3.Dot(normalBefore, inspection.SectionNormalLocal) < -.99f,
                "XR input", "More tools retains section flip", "Section normal=" + inspection.SectionNormalLocal);
            yield return Press("Exploration MORE TOOLS", false);
            Require(!panel.InspectionToolsExpanded, "XR input", "More tools can collapse again", "Core controls remain available.");
            CheckButtons(false, "Inspection TURN LEFT", "Inspection TILT UP", "Inspection DEEPER", "Inspection FLIP CUT");
            CheckButtons(true, "Inspection EDIT CUT", "Inspection ALIGN VIEW", "Inspection RESET");
            var sectionTool = inspection.GetComponentInChildren<HelmetSectionInteractable>(true);
            Require(sectionTool, "structure", "original section handle still exists", "Uses the existing inspection mechanism.");
            bool toolBefore = sectionTool.ShowTool;
            yield return Press("Inspection EDIT CUT", false);
            Require(sectionTool.ShowTool != toolBefore, "XR input", "EDIT CUT remains directly reachable", "Existing handle visibility toggled.");
            yield return Press("Inspection ALIGN VIEW", true);
            Vector4 plane = inspection.WorldSectionPlane;
            Vector3 viewerDirection = (Camera.main.transform.position - inspection.transform.TransformPoint(inspection.SectionCenterLocal)).normalized;
            Require(Vector3.Dot(new Vector3(plane.x, plane.y, plane.z).normalized, viewerDirection) > .99f,
                "XR input", "ALIGN VIEW remains directly reachable", "Actual cut plane aligns to the production camera.");
            yield return Press("Inspection RESET", false);
            yield return Wait(.3f);
            Require(!inspection.IsCutaway && !inspection.IsFloating && inspection.WindowAngle < .5f && exploration.AirSeen && !exploration.VoiceSeen,
                "XR input", "direct RESET restores inspection and preserves discoveries", Describe());
        }

        static IEnumerator ValidateEnrichedDemonstrations()
        {
            // State/animation checks use public playback APIs. Physical UI input is covered above.
            inspection.ResetInspection();
            // Reset is observed by the visit controller on Update; settle it before starting a new demo.
            yield return Wait(.25f);
            exploration.RestartVisit(); panel.ShowExploration();
            bool oldReduceMotion = inspection.ReduceMotion;
            inspection.ReduceMotion = false;
            try
            {
                exploration.SelectClue(Clue.Air);
                yield return WaitForProgress(2.3f / HelmetConnectionDemonstration.Duration);
                Transform visuals = FindVisual("Connection schematic visuals");
                Transform wheel = FindVisual("Pump flywheel");
                Transform pistonA = FindVisual("Pump piston A");
                Transform pistonB = FindVisual("Pump piston B");
                Transform packet = FindVisual("Air packet 0");
                Quaternion wheelBefore = wheel.localRotation;
                Vector3 pistonBefore = pistonA.localPosition, packetBefore = packet.position;
                Require(demonstration.IsPlaying && demonstration.PhaseIndex == 1 && wheel.gameObject.activeInHierarchy && packet.gameObject.activeInHierarchy,
                    "animation state", "AIR pumping stage exposes machinery and moving packets", AnimationDescription());
                RecordAnimation("air-pump-before", wheel, pistonA, packet);
                CaptureDiagram("06-air-pump-before");
                yield return WaitForProgress(2.6f / HelmetConnectionDemonstration.Duration);
                Require(Quaternion.Angle(wheelBefore, wheel.localRotation) > 2f && Vector3.Distance(pistonBefore, pistonA.localPosition) > .0005f,
                    "animation samples", "AIR flywheel and piston visibly change pose", "wheel delta=" + Quaternion.Angle(wheelBefore, wheel.localRotation) + "; piston delta=" + Vector3.Distance(pistonBefore, pistonA.localPosition));
                Require(Vector3.Distance(packetBefore, packet.position) > .003f,
                    "animation samples", "AIR packet advances during supply stage", "packet displacement=" + Vector3.Distance(packetBefore, packet.position));
                Require(Vector3.Distance(demonstration.AirEndpointWorld, demonstration.AirTargetWorld) < .003f,
                    "animation samples", "enriched AIR route still terminates at the helmet connector", "endpoint=" + demonstration.AirEndpointWorld + "; target=" + demonstration.AirTargetWorld);
                RecordAnimation("air-pump-after", wheel, pistonA, packet);
                CaptureDiagram("07-air-pump-after");
                yield return WaitForProgress(14.2f / HelmetConnectionDemonstration.Duration);
                Require(demonstration.PhaseIndex == 2, "animation state", "AIR reaches a distinct summary stage", AnimationDescription());
                CaptureDiagram("08-air-summary");
                yield return WaitForProgress(1f);
                yield return Wait(.1f);
                Require(!demonstration.IsPlaying && demonstration.IsVisible, "animation state", "AIR finishes with the complete apparatus still visible", AnimationDescription());
                yield return CheckMechanismsStayStill("completed AIR", wheel, pistonA, pistonB);

                exploration.SelectClue(Clue.Voice);
                yield return WaitForProgress(2.3f / HelmetConnectionDemonstration.Duration);
                var wave = FindVisual("Voice waveform 0").GetComponent<LineRenderer>();
                var route = FindVisual("Connection route").GetComponent<LineRenderer>();
                Require(wave && route && wave.gameObject.activeInHierarchy && demonstration.PhaseIndex == 1,
                    "animation state", "VOICE begins the outgoing message phase", AnimationDescription());
                float outgoingBefore = RouteProjection(wave, route);
                CaptureDiagram("09-voice-outgoing-before");
                yield return WaitForProgress(2.6f / HelmetConnectionDemonstration.Duration);
                float outgoingAfter = RouteProjection(wave, route);
                Require(outgoingAfter > outgoingBefore + .005f,
                    "animation samples", "VOICE outgoing wave travels from surface toward diver", "path projection=" + outgoingBefore + " -> " + outgoingAfter);
                report.animationSamples.Add(new AnimationSample { name = "voice-outgoing", stage = demonstration.AnimationStage, phase = demonstration.PhaseIndex,
                    normalizedProgress = demonstration.NormalizedProgress, routeProjectionBefore = outgoingBefore, routeProjectionAfter = outgoingAfter });
                CaptureDiagram("10-voice-outgoing-after");
                yield return WaitForProgress(7.5f / HelmetConnectionDemonstration.Duration);
                Require(demonstration.PhaseIndex == 2, "animation state", "VOICE separates message and reply with a turn-taking stage", AnimationDescription());
                CaptureDiagram("11-voice-turn-taking");
                yield return WaitForProgress(9.3f / HelmetConnectionDemonstration.Duration);
                Require(demonstration.PhaseIndex == 3 && wave.gameObject.activeInHierarchy,
                    "animation state", "VOICE reply has a separate visible phase", AnimationDescription());
                float replyBefore = RouteProjection(wave, route);
                CaptureDiagram("12-voice-reply-before");
                yield return WaitForProgress(9.6f / HelmetConnectionDemonstration.Duration);
                float replyAfter = RouteProjection(wave, route);
                Require(replyAfter < replyBefore - .005f,
                    "animation samples", "VOICE reply wave travels back toward surface", "path projection=" + replyBefore + " -> " + replyAfter);
                report.animationSamples.Add(new AnimationSample { name = "voice-reply", stage = demonstration.AnimationStage, phase = demonstration.PhaseIndex,
                    normalizedProgress = demonstration.NormalizedProgress, routeProjectionBefore = replyBefore, routeProjectionAfter = replyAfter });
                CaptureDiagram("13-voice-reply-after");
                yield return WaitForProgress(14.5f / HelmetConnectionDemonstration.Duration);
                Require(demonstration.PhaseIndex == 4, "animation state", "VOICE reaches a two-way summary", AnimationDescription());
                CaptureDiagram("14-voice-summary");
                yield return WaitForProgress(1f); yield return Wait(.1f);
                Require(!demonstration.IsPlaying && demonstration.IsVisible,
                    "animation state", "VOICE ends as a persistent static diagram", AnimationDescription());

                Transform sourceDiaphragm = FindVisual("Surface telephone diaphragm");
                Transform diverDiaphragm = FindVisual("Diver transducer diaphragm");
                yield return CheckMechanismsStayStill("completed VOICE", sourceDiaphragm, diverDiaphragm);
                Require(visuals.GetComponentsInChildren<Collider>(true).All(c => !c.enabled || !c.gameObject.activeInHierarchy) &&
                    visuals.GetComponentsInChildren<XRBaseInteractable>(true).Length == 0 && visuals.GetComponentsInChildren<Rigidbody>(true).Length == 0,
                    "interaction isolation", "new demonstration geometry adds no active collider, XR target, or rigidbody", "The optional air hotspot is outside this visual-only subtree and remains separately tested.");

                // TMP clears a disabled label's MeshFilter; compare the same stopped state on both sides.
                exploration.Stop(); yield return Wait(.08f);
                ResourceInventory originalInventory = Inventory(visuals);
                for (int i = 0; i < 6; i++)
                {
                    exploration.SelectClue(i % 2 == 0 ? Clue.Air : Clue.Voice);
                    exploration.Replay(); yield return Wait(.18f);
                    exploration.Stop(); yield return Wait(.08f);
                    ResourceInventory current = Inventory(visuals);
                    Require(current.SameAs(originalInventory), "resource lifecycle", "replay reuses visual resources, cycle " + (i + 1),
                        "before=" + JsonUtility.ToJson(originalInventory) + "; after=" + JsonUtility.ToJson(current));
                }
                Require(!demonstration.IsVisible && !visuals.gameObject.activeInHierarchy &&
                    visuals.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled || !r.gameObject.activeInHierarchy),
                    "public API", "STOP hides all enriched demonstration renderers", AnimationDescription() + "; root active=" + visuals.gameObject.activeInHierarchy);
                yield return CheckMechanismsStayStill("STOP", wheel, pistonA, pistonB, sourceDiaphragm, diverDiaphragm);

                bool airSeen = exploration.AirSeen, voiceSeen = exploration.VoiceSeen;
                inspection.ReduceMotion = true;
                exploration.SelectClue(Clue.Air); exploration.Replay(); yield return Wait(.2f);
                Transform signals = FindVisual("Travelling signal");
                Require(!demonstration.IsPlaying && demonstration.IsVisible && !signals.gameObject.activeInHierarchy && wheel.gameObject.activeInHierarchy,
                    "public API", "reduced-motion AIR retains its apparatus without animated signal packets", AnimationDescription());
                yield return CheckMechanismsStayStill("reduced-motion AIR", wheel, pistonA, pistonB);
                CaptureDiagram("15-air-reduced-motion");
                exploration.SelectClue(Clue.Voice); exploration.Replay(); yield return Wait(.2f);
                Require(!demonstration.IsPlaying && demonstration.IsVisible && !signals.gameObject.activeInHierarchy && sourceDiaphragm.gameObject.activeInHierarchy,
                    "public API", "reduced-motion VOICE retains equipment without moving waveforms", AnimationDescription());
                yield return CheckMechanismsStayStill("reduced-motion VOICE", sourceDiaphragm, diverDiaphragm);
                CaptureDiagram("16-voice-reduced-motion");
                Require(exploration.AirSeen == airSeen && exploration.VoiceSeen == voiceSeen,
                    "state", "motion preferences do not alter recorded discoveries", Describe());
                exploration.Stop();
            }
            finally { inspection.ReduceMotion = oldReduceMotion; }
        }

        static IEnumerator WaitForProgress(float target)
        {
            float deadline = Time.realtimeSinceStartup + HelmetConnectionDemonstration.Duration + 2f;
            while (demonstration.NormalizedProgress < target && demonstration.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Require(demonstration.NormalizedProgress >= target - .001f, "animation timing", "playback reaches normalized progress " + target,
                AnimationDescription());
        }
        static Transform FindVisual(string name)
        {
            var result = demonstration.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == name);
            Require(result, "visual structure", "demonstration object exists: " + name, "Unique named mechanism or visualization child.");
            return result;
        }
        static float RouteProjection(LineRenderer movingWave, LineRenderer route)
        {
            Require(movingWave.positionCount > 1 && route.positionCount > 1, "visual structure", "wave and route have geometry", "Compare actual world-space line vertices.");
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < movingWave.positionCount; i++) centre += movingWave.GetPosition(i);
            centre /= movingWave.positionCount;
            Vector3 start = route.GetPosition(0), axis = route.GetPosition(route.positionCount - 1) - start;
            Require(axis.sqrMagnitude > .0001f, "visual structure", "VOICE route has distinct ends", "axis length=" + axis.magnitude);
            return Vector3.Dot(centre - start, axis) / axis.sqrMagnitude;
        }
        static IEnumerator CheckMechanismsStayStill(string label, params Transform[] transforms)
        {
            var positions = transforms.Select(t => t.localPosition).ToArray();
            var rotations = transforms.Select(t => t.localRotation).ToArray();
            var scales = transforms.Select(t => t.localScale).ToArray();
            yield return Wait(.4f);
            bool still = transforms.Select((t, i) => Vector3.Distance(t.localPosition, positions[i]) < .0001f &&
                Quaternion.Angle(t.localRotation, rotations[i]) < .05f && Vector3.Distance(t.localScale, scales[i]) < .0001f).All(value => value);
            Require(still, "animation samples", "mechanisms stay still: " + label, "Compared local positions, rotations and scales over 0.4 seconds.");
        }
        static ResourceInventory Inventory(Transform visuals)
        {
            var filters = visuals.GetComponentsInChildren<MeshFilter>(true);
            var renderers = visuals.GetComponentsInChildren<Renderer>(true);
            return new ResourceInventory {
                transforms = visuals.GetComponentsInChildren<Transform>(true).Length,
                renderers = renderers.Length, lineRenderers = visuals.GetComponentsInChildren<LineRenderer>(true).Length,
                meshes = filters.Where(f => f.sharedMesh).Select(f => f.sharedMesh.GetInstanceID()).Distinct().Count(),
                materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.GetInstanceID()).Distinct().Count()
            };
        }
        static string AnimationDescription() => "clue=" + demonstration.CurrentClue + "; stage=" + demonstration.AnimationStage +
            "; phase=" + demonstration.PhaseIndex + "; progress=" + demonstration.NormalizedProgress + "; playing=" + demonstration.IsPlaying + "; visible=" + demonstration.IsVisible;
        static void RecordAnimation(string name, Transform wheel, Transform piston, Transform packet)
        {
            report.animationSamples.Add(new AnimationSample { name = name, stage = demonstration.AnimationStage, phase = demonstration.PhaseIndex,
                normalizedProgress = demonstration.NormalizedProgress, wheelRotation = wheel.localRotation, pistonLocalPosition = piston.localPosition, packetWorldPosition = packet.position });
        }
        static void CaptureDiagram(string name)
        {
            CaptureWithCamera(name + "-diagram-closeup.jpg", false, true);
        }


        static string Describe() => "page=" + exploration.CurrentPage + "; selected=" + exploration.SelectedClue + "; air=" + exploration.AirSeen + "; voice=" + exploration.VoiceSeen + "; hints=" + exploration.HintRequests + "; playing=" + demonstration.IsPlaying + "; diagramVisible=" + demonstration.IsVisible;

        static void CheckButtons(bool visible, params string[] names)
        {
            foreach (var name in names) CheckButtonVisibility(name, visible);
        }

        static void CheckButtonVisibility(string name, bool visible)
        {
            var target = panel.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == name);
            string labelName = (name.StartsWith("Inspection ", StringComparison.Ordinal) ? name.Substring("Inspection ".Length) : name) + " label";
            var label = target ? target.parent.Find(labelName) : null;
            var button = target ? target.GetComponent<XRSimpleInteractable>() : null;
            var text = label ? label.GetComponent<TMPro.TMP_Text>() : null;
            Require(button && text, "control visibility", "button and label exist: " + name, "Expected sibling label=" + labelName);
            bool activeButton = button.isActiveAndEnabled;
            bool activeLabel = text.isActiveAndEnabled && text.GetComponent<Renderer>().enabled;
            var colliders = button.colliders.Where(c => c).ToArray();
            bool selectable = colliders.Any(c => c.enabled && c.gameObject.activeInHierarchy);
            Require(activeButton == visible && activeLabel == visible && selectable == visible,
                "control visibility", (visible ? "visible control and label: " : "hidden control and label: ") + name,
                "button=" + activeButton + "; label=" + activeLabel + "; enabled active collider=" + selectable);
        }

        static IEnumerator Press(string objectName, bool left, bool distant = false)
        {
            if (objectName.StartsWith("Exploration ", StringComparison.Ordinal) || objectName.StartsWith("Inspection ", StringComparison.Ordinal))
                CheckButtonVisibility(objectName, true);
            var button = GameObject.Find(objectName)?.GetComponent<XRSimpleInteractable>();
            Require(button && button.isActiveAndEnabled, "XR input", "button available: " + objectName, left ? "Left controller" : "Right controller");
            var collider = button.colliders.FirstOrDefault(c => c && c.enabled);
            if (!collider) collider = button.GetComponent<Collider>();
            Require(collider, "XR input", "button collider: " + objectName, "An enabled target is required.");
            int events = 0; string interactor = "none";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; interactor = args.interactorObject.GetType().Name; };
            button.selectEntered.AddListener(listener);
            try
            {
                var device = left ? leftDevice : rightDevice;
                Vector3 touch = collider.bounds.center - (distant ? button.transform.forward * .55f : Vector3.zero);
                Quaternion rotation = distant ? Quaternion.LookRotation(collider.bounds.center - touch, Vector3.up) : Quaternion.identity;
                if (!distant) touch = DirectButtonTouch(button, collider, left);
                QueueController(device, touch, rotation, false); yield return Wait(.25f);
                if (objectName.StartsWith("Exploration ", StringComparison.Ordinal))
                {
                    var follower = panel.GetComponent<HelmetInspectionPalette>();
                    Require(button.isHovered && follower && follower.IsFrozen, "XR input", "hover freezes shared palette: " + objectName,
                        "hovered=" + button.isHovered + "; frozen=" + (follower && follower.IsFrozen));
                }
                touch = collider.bounds.center - (distant ? button.transform.forward * .55f : Vector3.zero);
                rotation = distant ? Quaternion.LookRotation(collider.bounds.center - touch, Vector3.up) : Quaternion.identity;
                if (!distant) touch = DirectButtonTouch(button, collider, left);
                QueueController(device, touch, rotation, true); yield return Wait(.22f);
                Require(events == 1, "XR input", "grip selects " + objectName, "hand=" + (left ? "left" : "right") + "; select events=" + events + "; interactor=" + interactor);
                if (distant) Require(interactor == nameof(XRRayInteractor), "XR input", "distant ray selects " + objectName,
                    "Target is 0.55 m from the simulated hand; interactor=" + interactor);
                if (button is HelmetExplorationHotspot)
                    Require(!inspection.GetComponent<XRGrabInteractable>().isSelected, "XR input", "hotspot selection does not grab the helmet", "The child clue wins over the whole-object grab while selected.");
                QueueController(device, touch + Vector3.back * .30f, Quaternion.identity, false); yield return Wait(.22f);
                ParkHands(); yield return Wait(.12f);
            }
            finally { if (button) button.selectEntered.RemoveListener(listener); }
        }

        static Vector3 DirectButtonTouch(XRSimpleInteractable button, Collider collider, bool left)
        {
            Vector3 outward = button is HelmetExplorationHotspot
                ? collider.bounds.center - inspection.transform.TransformPoint(inspection.SectionCenterLocal)
                : -button.transform.forward;
            return MuseumPhysicalHandsTestPose.OutsideTouch(collider, left ? leftController : rightController,
                Quaternion.identity, outward, collider.bounds.center);
        }

        static void ParkHands()
        {
            QueueController(rightDevice, new Vector3(.7f, .8f, 1f), Quaternion.identity, false);
            QueueController(leftDevice, new Vector3(-.7f, .8f, 1f), Quaternion.identity, false);
        }
        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip)
        {
            var parent = (device == leftDevice ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(world), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation, grip = grip ? 1f : 0f }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void QueueHead()
        {
            var camera = Camera.main; var parent = camera.transform.parent;
            Vector3 position = parent ? parent.InverseTransformPoint(camera.transform.position) : camera.transform.position;
            Quaternion rotation = parent ? Quaternion.Inverse(parent.rotation) * camera.transform.rotation : camera.transform.rotation;
            var state = new XRSimulatedHMDState { isTracked = true, trackingState = 3, devicePosition = position, centerEyePosition = position,
                leftEyePosition = position + Vector3.left * .032f, rightEyePosition = position + Vector3.right * .032f,
                deviceRotation = rotation, centerEyeRotation = rotation, leftEyeRotation = rotation, rightEyeRotation = rotation };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(hmd, state));
        }
        static void QueueKeyboard(KeyboardState state) => PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, state));
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        static void Capture(string name)
        {
            CheckPaletteText(name);
            foreach (string labelName in new[] { "Gallery masthead", "Central question" })
            {
                var background = UnityEngine.Object.FindObjectsOfType<Transform>(true).Single(t => t.name == labelName);
                Require(background.gameObject.activeSelf == !demonstration.IsVisible, "visual hierarchy",
                    "background yields to the demonstration: " + name + "/" + labelName,
                    "Titles return when the demonstration is hidden; no timed reading dismissal.");
            }
            if (name.StartsWith("after-", StringComparison.Ordinal))
                foreach (var control in panel.GetComponentsInChildren<XRBaseInteractable>(false))
                {
                    var collider = control.GetComponent<Collider>();
                    if (!collider) continue;
                    var bounds = collider.bounds;
                    bool visible = true;
                    for (int i = 0; i < 8; i++)
                    {
                        var point = bounds.center + Vector3.Scale(bounds.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var screen = Camera.main.WorldToViewportPoint(point);
                        visible &= screen.z > Camera.main.nearClipPlane && screen.x > .01f && screen.x < .99f && screen.y > .01f && screen.y < .99f;
                    }
                    if (!visible) CaptureWithCamera(name + "-viewport-failure.jpg", false);
                    Require(visible, "viewport", "primary-view control stays in frame: " + name + "/" + control.name,
                        "All collider bounds corners must remain inside the forward camera view. Palette=" + panel.transform.position +
                        "; camera=" + Camera.main.transform.position + "; control centre viewport=" + Camera.main.WorldToViewportPoint(bounds.center));
                }
            if (demonstration.IsVisible)
                foreach (var label in demonstration.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    label.ForceMeshUpdate(true, true);
                    Require(!label.isTextOverflowing, "text layout", "diagram text fits rectangle: " + name + "/" + label.name,
                        "resolved font=" + label.fontSize + "; rectangle=" + label.rectTransform.rect.size);
                }
            CaptureWithCamera(name + ".jpg", false);
            if (panel.IsExploring || panel.IsReading) CaptureWithCamera(name + "-panel-closeup.jpg", true);
        }

        static void CheckPageText(string sampleName)
        {
            var page = panel.transform.Find("Exploration page");
            Require(page && page.gameObject.activeInHierarchy, "text layout", "active exploration text page: " + sampleName, "Only the visible optional page is checked.");
            CheckPaletteText(sampleName);
        }

        static void CheckPaletteText(string sampleName)
        {
            var labels = panel.GetComponentsInChildren<TMPro.TMP_Text>(false);
            Require(labels.Length > 0, "text layout", "palette contains visible text: " + sampleName, "Visible TMP count=" + labels.Length);
            foreach (var label in labels)
            {
                label.ForceMeshUpdate(true, true);
                report.textSamples.Add(new TextSample { page = sampleName, label = label.name, fontSize = label.fontSize,
                    rectangle = label.rectTransform.rect.size, overflow = label.isTextOverflowing });
                Require(!label.isTextOverflowing, "text layout", "text fits rectangle: " + sampleName + "/" + label.name,
                    "resolved font=" + label.fontSize + "; rectangle=" + label.rectTransform.rect.size + "; containment is not headset readability.");
            }
        }

        static void CheckReadingLayout(string sampleName)
        {
            Require(panel.IsReading, "text layout", "reading layout is open: " + sampleName, Describe());
            CheckPageText(sampleName);
            var body = panel.GetComponentsInChildren<TMPro.TMP_Text>(false).SingleOrDefault(t => t.name == "Exploration explanation");
            Require(body, "text layout", "reading body is visible: " + sampleName, "Check the actual production text rectangle against active controls.");
            var corners = new Vector3[4];
            body.rectTransform.GetWorldCorners(corners);
            Rect bodyRect = PaletteRect(corners);
            foreach (var button in panel.GetComponentsInChildren<XRSimpleInteractable>(false))
            {
                if (!button.isActiveAndEnabled) continue;
                var box = button.colliders.OfType<BoxCollider>().FirstOrDefault(c => c.enabled && c.gameObject.activeInHierarchy);
                Require(box, "text layout", "reading control has box geometry: " + button.name, sampleName);
                var points = new List<Vector3>();
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            points.Add(box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, new Vector3(x, y, z))));
                Rect controlRect = PaletteRect(points);
                Require(!RectanglesOverlap(bodyRect, controlRect), "text layout", "reading body does not overlap button: " + sampleName + "/" + button.name,
                    "body=" + bodyRect + "; button=" + controlRect);
                string labelName = (button.name.StartsWith("Inspection ", StringComparison.Ordinal) ? button.name.Substring("Inspection ".Length) : button.name) + " label";
                var label = button.transform.parent.Find(labelName)?.GetComponent<TMPro.TMP_Text>();
                Require(label && label.isActiveAndEnabled, "text layout", "reading control label is visible: " + button.name, sampleName);
                label.rectTransform.GetWorldCorners(corners);
                Rect labelRect = PaletteRect(corners);
                Require(!RectanglesOverlap(bodyRect, labelRect), "text layout", "reading body does not overlap button label: " + sampleName + "/" + button.name,
                    "body=" + bodyRect + "; label=" + labelRect);
            }
        }

        static Rect PaletteRect(IEnumerable<Vector3> worldPoints)
        {
            var points = worldPoints.Select(p => panel.transform.InverseTransformPoint(p)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        static bool RectanglesOverlap(Rect first, Rect second) =>
            Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin) > .0005f &&
            Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin) > .0005f;

        static void CaptureWithCamera(string file, bool closeup, bool diagram = false)
        {
            // Copy the production pose and FOV into a 4:3 monocular capture without changing the XR camera.
            var temporary = new GameObject("Temporary Exploration QA Capture") { hideFlags = HideFlags.HideAndDontSave };
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1440, 1080, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                var source = Camera.main;
                var camera = temporary.AddComponent<Camera>();
                camera.CopyFrom(source); camera.enabled = false; camera.targetTexture = null;
                camera.stereoTargetEye = StereoTargetEyeMask.None; camera.aspect = 1440f / 1080f;
                temporary.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                var additional = temporary.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                additional.allowXRRendering = false; additional.renderPostProcessing = false;
                foreach (var text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) text.ForceMeshUpdate(true);
                if (diagram)
                {
                    var renderers = demonstration.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToArray();
                    Require(renderers.Length > 0, "QA camera", "diagram has renderers for close-up framing", "Temporary diagnostic view only.");
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    // Fit the full apparatus, signal route and labels without moving the XR camera.
                    float radius = Mathf.Max(.20f, bounds.extents.magnitude);
                    camera.fieldOfView = 48f;
                    float distance = radius / Mathf.Sin(camera.fieldOfView * .5f * Mathf.Deg2Rad) * 1.12f;
                    Vector3 direction = Vector3.ProjectOnPlane(source.transform.forward, Vector3.up).normalized;
                    if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
                    camera.transform.position = bounds.center - direction * distance;
                    camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                    camera.nearClipPlane = .02f;
                }
                else if (closeup)
                {
                    Vector3 centre = panel.transform.TransformPoint(new Vector3(0f, .04f, -.045f));
                    camera.transform.position = centre - panel.transform.forward * .95f;
                    camera.transform.rotation = Quaternion.LookRotation(centre - camera.transform.position, panel.transform.up);
                    camera.fieldOfView = 48f; camera.nearClipPlane = .02f;
                }
                if (!closeup && !diagram && file.StartsWith("after-", StringComparison.Ordinal))
                {
                    var baseline = report.captureSamples.FirstOrDefault(c => c.view == "production camera" && c.file.StartsWith("after-", StringComparison.Ordinal));
                    if (baseline != null)
                        Require(Vector3.Distance(camera.transform.position, baseline.position) < .0001f &&
                            Quaternion.Angle(camera.transform.rotation, baseline.rotation) < .01f && Mathf.Abs(camera.fieldOfView - baseline.fieldOfView) < .001f,
                            "QA camera", "after states use the same production viewpoint: " + file,
                            "Matches " + baseline.file + "; the original XR camera has not been reframed.");
                }
                foreach (var text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) text.ForceMeshUpdate(true);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(OutputDirectory, file), pixels.EncodeToJPG(92)); report.screenshots.Add(file);
                report.captureSamples.Add(new CaptureSample { file = file,
                    view = diagram ? "temporary diagram close-up" : closeup ? "temporary panel close-up" : "production camera",
                    position = camera.transform.position, rotation = camera.transform.rotation, fieldOfView = camera.fieldOfView, aspect = camera.aspect });
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        static void Require(bool condition, string kind, string name, string detail)
        {
            report.checks.Add(new Check { kind = kind, name = name, detail = detail, passed = condition });
            if (!condition) throw new InvalidOperationException(name + ": " + detail);
        }
        static void OnLog(string message, string stack, LogType type)
        { if (report != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) report.errors.Add(type + ": " + message + "\n" + stack); }

        static void Finish(string failure, bool exitPlayMode = true)
        {
            if (finishing) return;
            finishing = true; Application.logMessageReceived -= OnLog; InputSystem.onBeforeUpdate -= FlushInput;
            PendingInput.Clear(); Routines.Clear();
            report.durationSeconds = (float)(EditorApplication.timeSinceStartup - started);
            report.failure = failure ?? "";
            var sceneBefore = report.provenance.files.FirstOrDefault(f => f.path == ScenePath);
            bool unchanged = sceneBefore != null && sceneBefore.sha256 == Hash(ScenePath);
            report.checks.Add(new Check { kind = "source", name = "saved scene unchanged by validator", passed = unchanged, detail = Hash(ScenePath) });
            report.status = failure == null && report.errors.Count == 0 && report.checks.All(c => c.passed) ? "passed" : "failed";
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, hmd, keyboard, addedMouse ? mouse : null })
                if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedSettings) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; }
            Directory.CreateDirectory(OutputDirectory);
            string path = Path.Combine(OutputDirectory, "report.json"); File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            SessionState.SetInt(Prefix + "ExitCode", report.status == "passed" ? 0 : 1);
            Debug.Log("EXPLORATION_QA_" + report.status.ToUpperInvariant() + " " + path + (failure == null ? "" : "\n" + failure));
            if (exitPlayMode) EditorApplication.ExitPlaymode();
        }
    }
}
#endif
