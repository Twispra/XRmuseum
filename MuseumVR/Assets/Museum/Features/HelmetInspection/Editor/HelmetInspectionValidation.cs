#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Museum.PhysicalHands.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Inspection.Editor
{
    /// <summary>
    /// Editor-only acceptance runner. Drives real Input System XR device state through the
    /// scene's unmodified actions/controllers/interactors; never calls SelectEnter/SelectExit.
    /// Batch: -executeMethod Museum.Inspection.Editor.HelmetInspectionValidation.RunBatch (omit -quit).
    /// Integrated museum: use RunMainBatch; the existing entry point still validates the isolated scene.
    /// </summary>
    [InitializeOnLoad]
    public static class HelmetInspectionValidation
    {
        const string ActiveKey = "Museum.HelmetInspectionQA.Active";
        const string BatchKey = "Museum.HelmetInspectionQA.Batch";
        const string ResultKey = "Museum.HelmetInspectionQA.ExitCode";
        const string RunIdKey = "Museum.HelmetInspectionQA.RunId";
        const string ProvenanceKey = "Museum.HelmetInspectionQA.Provenance";
        const string ScenePathKey = "Museum.HelmetInspectionQA.ScenePath";
        const string MainScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const double BatchExitSettleSeconds = 20;
        static string ScenePath => SessionState.GetString(ScenePathKey, HelmetInspectionBuilder.ScenePath);
        static bool IsMainSceneRun => ScenePath == MainScenePath;
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD hmd;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedKeyboard;
        static bool addedMouse;
        static ActionBasedController rightController;
        static XRGrabInteractable helmet;
        static HelmetExhibitReset reset;
        static HelmetInspectionController inspection;
        static HelmetWindowInteractable window;
        static ActionBasedController leftController;
        static Rigidbody body;
        static Collider[] helmetColliders;
        static double started;
        static double exitNotBefore;
        static int pendingBatchExit = -1;
        static int lastFrame = -1;
        static bool finishing;
        static Vector3 homePosition;
        static Quaternion homeRotation;
        static InputSettings.BackgroundBehavior originalBackgroundBehavior;
        static InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
        static bool changedInputSettings;
        static string phase = "setup";
        static int cycleNumber;
        static float nextSample;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string OutputDirectory
        {
            get
            {
                var runId = SessionState.GetString(RunIdKey, "");
                if (string.IsNullOrEmpty(runId)) throw new InvalidOperationException("Helmet QA run ID is unavailable; refusing to overwrite historical evidence.");
                return Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", runId);
            }
        }

        [Serializable] class FileFingerprint
        {
            public string path, status = "unknown", sha256 = "", error = "";
        }
        [Serializable] class Provenance
        {
            public string runId, capturedAtUtc, unityVersion;
            public string gitCommit = "unknown", gitBranch = "unknown", workingTreeState = "unknown", gitStatusPorcelain = "";
            public string note = "Snapshot taken before Play Mode. Git status includes tracked changes and untracked paths, not file contents. A passed simulation does not establish a clean checkout, a merged main branch, or hardware validation.";
            public List<FileFingerprint> files = new List<FileFingerprint>();
            public List<string> errors = new List<string>();
        }

        [Serializable] class Check { public string name; public bool passed; public string detail; }
        [Serializable] class Cycle
        {
            public int number;
            public int automaticResets;
            public string interactor;
            public float liftMeters, rotationDegrees, returnErrorMeters, settlingSpeed, settlingAngularSpeed, restingJitterMeters, deepestPenetrationMeters;
        }
        [Serializable] class Sample
        {
            public int cycle, frame;
            public string phase;
            public float time, deltaTime;
            public bool selected, windowSelected, cutaway;
            public float windowAngle;
            public int resetCount;
            public bool homeKeyDown;
            public Vector3 position, velocity, hand, target;
        }
        [Serializable] class SectionInputStep
        {
            public float inputDegrees, inputDeltaDegrees, guideRotationDeltaDegrees, maximumRotationDeltaDegrees;
            public float knobDisplacementMeters, maximumKnobDisplacementMeters;
            public float guideRotationErrorDegrees, knobPositionErrorMeters, normalErrorDegrees, centerErrorMeters;
            public float rootPositionErrorMeters, rootRotationErrorDegrees;
            public bool toolSelected;
        }
        [Serializable] class SectionInputTrace
        {
            public string name;
            public Vector3 rootEulerDegrees;
            public List<SectionInputStep> steps = new List<SectionInputStep>();
        }
        [Serializable] class Report
        {
            public string timestampUtc, unityVersion, scene, inputMethod, limitations, status = "running", failure = "";
            public Provenance provenance;
            public float durationSeconds;
            public double batchExitSettleSeconds;
            public bool officialSimulatorWasPresent;
            public int colliderCount, completedCycles;
            public List<Check> checks = new List<Check>();
            public List<Cycle> cycles = new List<Cycle>();
            public List<string> errors = new List<string>();
            public List<Sample> samples = new List<Sample>();
            public List<string> inputDiagnostics = new List<string>();
            public List<SectionInputTrace> sectionInputContinuity = new List<SectionInputTrace>();
        }

        static HelmetInspectionValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
        }

        [MenuItem("Museum/Inspection/Validate isolated scene (Play Mode)")]
        public static void RunInEditor() { Begin(false, HelmetInspectionBuilder.ScenePath); }
        public static void RunBatch() { Begin(true, HelmetInspectionBuilder.ScenePath); }

        [MenuItem("Museum/Inspection/Validate main museum (Play Mode)")]
        public static void RunMainInEditor() { Begin(false, MainScenePath); }
        public static void RunMainBatch() { Begin(true, MainScenePath); }
        

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareBatchInput()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            // Batch mode has no native keyboard/mouse. The official simulator's help UI
            // reads resolved bindings in Start, so provide devices before the scene loads.
            // Use an isolated synthetic keyboard, never the editor's native keyboard.
            keyboard = InputSystem.AddDevice<Keyboard>("Helmet QA Keyboard");
            addedKeyboard = true;
            mouse = Mouse.current;
            if (mouse == null) { mouse = InputSystem.AddDevice<Mouse>(); addedMouse = true; }
        }

        static void Begin(bool batch, string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run helmet validation from Edit Mode.");
            pendingBatchExit = -1;
            // Do not save a user's dirty scene as a side effect of an acceptance test.
            if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(ScenePathKey, scenePath);
            EditorSceneManager.OpenScene(scenePath);
            // Review-only camera must not disable the production XR rig or simulator during QA.
            // The main scene must contain no desktop preview at all; do not hide a defect there.
            if (!IsMainSceneRun)
            {
                foreach (var preview in UnityEngine.Object.FindObjectsOfType<HelmetInspectionDesktopPreview>(true))
                {
                    SessionState.SetBool("Museum.HelmetInspectionQA.Preview." + preview.name, preview.gameObject.activeSelf);
                    preview.gameObject.SetActive(false); // Intentionally not saved.
                }
            }
            var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") +
                (IsMainSceneRun ? "-inspection-main-" : "-inspection-") + Guid.NewGuid().ToString("N");
            SessionState.SetString(RunIdKey, runId);
            SessionState.SetString(ProvenanceKey, JsonUtility.ToJson(CaptureProvenance(runId)));
            Directory.CreateDirectory(OutputDirectory);
            Debug.Log("Helmet QA run " + runId + ": " + OutputDirectory);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetInt(ResultKey, -1);
            EditorApplication.EnterPlaymode();
        }

        static Provenance CaptureProvenance(string runId)
        {
            var snapshot = new Provenance
            {
                runId = runId, capturedAtUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion
            };
            if (!Directory.Exists(Path.Combine(RepoRoot, ".git")) && !File.Exists(Path.Combine(RepoRoot, ".git")))
                snapshot.errors.Add("The expected repository root has no .git directory or worktree file; Git state is unknown.");
            else
            {
                if (TryReadGit("rev-parse --verify HEAD", out var commit, out var commitError) && !string.IsNullOrWhiteSpace(commit))
                    snapshot.gitCommit = commit.Trim();
                else snapshot.errors.Add("Git commit: " + (string.IsNullOrEmpty(commitError) ? "empty output" : commitError));

                if (TryReadGit("branch --show-current", out var branch, out var branchError))
                    snapshot.gitBranch = string.IsNullOrWhiteSpace(branch) ? "(detached HEAD)" : branch.Trim();
                else snapshot.errors.Add("Git branch: " + branchError);

                if (TryReadGit("-c core.quotepath=false status --porcelain=v1 --untracked-files=all", out var status, out var statusError))
                {
                    snapshot.gitStatusPorcelain = status;
                    snapshot.workingTreeState = string.IsNullOrEmpty(status) ? "clean" : "dirty";
                }
                else snapshot.errors.Add("Git status: " + statusError);
            }

            var paths = new[]
            {
                "MuseumVR/Packages/manifest.json",
                "MuseumVR/Packages/packages-lock.json",
                "MuseumVR/ProjectSettings/ProjectSettings.asset",
                "MuseumVR/ProjectSettings/ProjectVersion.txt",
                "MuseumVR/Assets/Samples/XR Interaction Toolkit/2.6.4/Starter Assets/XRI Default Input Actions.inputactions",
                "MuseumVR/Assets/Samples/XR Interaction Toolkit/2.6.4/XR Device Simulator/XR Device Simulator Controls.inputactions",
                "MuseumVR/Assets/Samples/XR Interaction Toolkit/2.6.4/XR Device Simulator/XR Device Controller Controls.inputactions",
                "MuseumVR/Assets/Samples/XR Interaction Toolkit/2.6.4/XR Device Simulator/XR Device Hand Controls.inputactions",
                "MuseumVR/Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset",
                "MuseumVR/Assets/Museum/Scenes/MuseumStart.unity",
                "MuseumVR/Assets/Museum/Models/Helmet/helmet-graybox-v02.fbx",
                "MuseumVR/Assets/Museum/Prefabs/HelmetExhibit.prefab",
                "MuseumVR/Assets/Museum/Scripts/HelmetExhibitReset.cs",
                "MuseumVR/Assets/Museum/Features/HelmetInspection/Editor/HelmetInspectionValidation.cs"
            };
            var featureFiles = Directory.GetFiles(Path.Combine(RepoRoot, "MuseumVR/" + HelmetInspectionBuilder.Root), "*", SearchOption.AllDirectories)
                .OrderBy(path => path).Select(path => path.Substring(RepoRoot.Length + 1).Replace('\\', '/'));
            foreach (var relativePath in paths.Concat(featureFiles).Distinct())
            {
                var fingerprint = new FileFingerprint { path = relativePath };
                try
                {
                    using (var stream = File.OpenRead(Path.Combine(RepoRoot, relativePath)))
                    using (var hash = SHA256.Create())
                        fingerprint.sha256 = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    fingerprint.status = "captured";
                }
                catch (Exception exception)
                {
                    fingerprint.status = "unavailable";
                    fingerprint.error = exception.GetType().Name + ": " + exception.Message;
                }
                snapshot.files.Add(fingerprint);
            }
            return snapshot;
        }

        static bool TryReadGit(string arguments, out string output, out string error)
        {
            output = "";
            error = "";
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git", Arguments = arguments, WorkingDirectory = RepoRoot,
                        UseShellExecute = false, CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true, RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                    };
                    process.StartInfo.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";
                    if (!process.Start()) { error = "Git process did not start."; return false; }
                    // Drain both pipes asynchronously so a large status listing cannot deadlock.
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(3000))
                    {
                        error = "Git query timed out after 3 seconds; state is unknown.";
                        try { process.Kill(); process.WaitForExit(1000); }
                        catch (Exception exception) { error += " " + exception.GetType().Name; }
                        return false;
                    }
                    if (!stdout.Wait(1000) || !stderr.Wait(1000))
                    {
                        error = "Git output was not completely read; state is unknown.";
                        return false;
                    }
                    if (process.ExitCode != 0)
                    {
                        error = "Exit " + process.ExitCode + ": " + stderr.Result.Trim();
                        return false;
                    }
                    output = stdout.Result;
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        static Provenance RestoreProvenance()
        {
            var json = SessionState.GetString(ProvenanceKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var snapshot = JsonUtility.FromJson<Provenance>(json);
                    if (snapshot != null) return snapshot;
                }
                catch (Exception exception)
                {
                    return new Provenance { runId = SessionState.GetString(RunIdKey, "unknown"),
                        errors = new List<string> { "Unable to restore pre-run provenance: " + exception.Message } };
                }
            }
            return new Provenance { runId = SessionState.GetString(RunIdKey, "unknown"),
                errors = new List<string> { "Pre-run provenance is unavailable; Git state and hashes are unknown." } };
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup;
                finishing = false;
                lastFrame = -1;
                report = new Report
                {
                    timestampUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                    provenance = RestoreProvenance(),
                    batchExitSettleSeconds = SessionState.GetBool(BatchKey, false) ? BatchExitSettleSeconds : 0,
                    scene = SceneManager.GetActiveScene().path,
                    inputMethod = "InputSystem.QueueStateEvent in the player Dynamic input update on XRSimulatedController/HMD -> production Input Actions -> ActionBasedController -> existing XRDirectInteractor / XRRayInteractor -> window, physical controls, XRGrabInteractable and Rigidbody. Home uses a synthetic KeyboardState. Hidden batch frames are paced to approximately 90 Hz to remain above XRI 2.6's 1 ms throw-sampling cutoff.",
                    limitations = "Automated desktop simulation only. Official XR Device Simulator presence is checked, then its device writer is disabled to avoid competing states. Its mouse/keyboard navigation mappings, real headset tracking, hand comfort, and device performance still require manual testing. The test driver does not call XRI selection APIs. Input-driven checks and explicitly labelled public-API checks are separate. This run validates only the recorded scene and checkout; it does not validate a player build, visual rendering, historical accuracy, accessibility with users, or a merged main branch."
                };
                Application.logMessageReceived += OnLog;
                InputSystem.onBeforeUpdate += FlushPlayerInput;
                InputSystem.onAfterUpdate += InspectPlayerKeyboard;
                Routines.Clear();
                Routines.Push(Validate());
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (!IsMainSceneRun)
                {
                    foreach (var preview in UnityEngine.Object.FindObjectsOfType<HelmetInspectionDesktopPreview>(true))
                        preview.gameObject.SetActive(SessionState.GetBool("Museum.HelmetInspectionQA.Preview." + preview.name, preview.gameObject.activeSelf));
                }
                var code = SessionState.GetInt(ResultKey, 1);
                var batch = SessionState.GetBool(BatchKey, false);
                SessionState.SetBool(ActiveKey, false);
                if (batch)
                {
                    // Match the other acceptance runners: returning to Edit Mode
                    // still queues imports and native render/worker cleanup. An
                    // immediate Exit can finish every check yet spin in shutdown.
                    pendingBatchExit = code < 0 ? 1 : code;
                    exitNotBefore = EditorApplication.timeSinceStartup + BatchExitSettleSeconds;
                    Debug.Log("HELMET_INSPECTION_BATCH_SETTLING code=" + pendingBatchExit +
                        "; seconds=" + BatchExitSettleSeconds + "; utc=" + DateTime.UtcNow.ToString("o"));
                }
            }
        }

        static void Tick()
        {
            // Keep pumping Editor updates after Active is cleared; never block the
            // main thread while the asset pipeline and workers finish their work.
            if (pendingBatchExit >= 0)
            {
                if (EditorApplication.timeSinceStartup < exitNotBefore || EditorApplication.isCompiling ||
                    EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                int code = pendingBatchExit;
                pendingBatchExit = -1;
                Debug.Log("HELMET_INSPECTION_BATCH_SETTLED_EXIT code=" + code + "; utc=" + DateTime.UtcNow.ToString("o"));
                EditorApplication.Exit(code);
                return;
            }
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 150)
            {
                Finish("Acceptance test exceeded 150 seconds.");
                return;
            }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            // A hidden batch player otherwise runs at >2000 Hz. XRI 2.6 deliberately skips
            // throw samples with deltaTime < .001, which makes that test environment invalid
            // for release velocities. Pace only the batch harness to approximately 90 Hz.
            if (Application.isBatchMode) System.Threading.Thread.Sleep(11);
            if (body && helmet && rightController && Time.realtimeSinceStartup >= nextSample)
            {
                nextSample = Time.realtimeSinceStartup + .1f;
                report.samples.Add(new Sample { cycle = cycleNumber, frame = Time.frameCount, phase = phase,
                    time = Time.realtimeSinceStartup, deltaTime = Time.deltaTime, selected = helmet.isSelected,
                    windowSelected = window && window.isSelected, cutaway = inspection && inspection.IsCutaway, windowAngle = inspection ? inspection.WindowAngle : 0,
                    position = body.position, velocity = body.velocity, hand = rightController.transform.position,
                    target = helmet.GetTargetPose().position, resetCount = reset.ResetCount,
                    homeKeyDown = keyboard != null && keyboard.homeKey.isPressed });
            }
            try
            {
                while (Routines.Count > 0)
                {
                    var routine = Routines.Peek();
                    if (!routine.MoveNext()) { Routines.Pop(); continue; }
                    if (routine.Current is IEnumerator nested) { Routines.Push(nested); continue; }
                    return;
                }
                Finish(null);
            }
            catch (Exception exception)
            {
                string failure = exception.ToString();
                if (exception.Message.Contains("Physical palm did not arrive"))
                    failure += "\n" + PalmObstacles(rightController);
                Finish(failure);
            }
        }

        static string PalmObstacles(XRBaseController controller)
        {
            var hand = MuseumPhysicalHandsTestPose.Hand(controller);
            if (!hand || !hand.CollisionCollider) return "Palm obstacle diagnostics unavailable.";
            var bounds = hand.CollisionCollider.bounds;
            var nearby = Physics.OverlapBox(bounds.center, bounds.extents + Vector3.one * .12f,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => c && !c.transform.IsChildOf(hand.transform))
                .Select(c => c.name + " [" + c.GetType().Name + "; center=" + c.bounds.center +
                    "; size=" + c.bounds.size + "; body=" + (c.attachedRigidbody ? c.attachedRigidbody.name : "static") + "]");
            return "Palm nearby solids: " + string.Join("; ", nearby);
        }

        static IEnumerator Validate()
        {
            yield return Wait(2f);
            Require(SceneManager.GetActiveScene().path == ScenePath, "requested scene loaded",
                "Requested=" + ScenePath + "; actual=" + SceneManager.GetActiveScene().path);
            if (IsMainSceneRun)
            {
                Require(UnityEngine.Object.FindObjectsOfType<HelmetInspectionDesktopPreview>(true).Length == 0,
                    "main scene has no desktop preview", "The integrated museum must use the production XR view, including during manual Play Mode.");
                var xrOrigin = Camera.main ? Camera.main.GetComponentInParent<XROrigin>() : null;
                Require(xrOrigin && xrOrigin.name == "XR Origin (XR Rig)" && xrOrigin.Camera == Camera.main,
                    "main camera belongs to original XR rig", "Camera.main is the saved XR Origin camera, not a separate exhibit camera.");
                Require(GameObject.Find("Gallery Blockout") && GameObject.Find("Exhibit 02 - Placeholder") && GameObject.Find("Exhibit 03 - Placeholder"),
                    "original gallery and other exhibits retained", "Gallery Blockout and the two unaffected placeholder exhibits remain active in MuseumStart.");
                Require(UnityEngine.Object.FindObjectsOfType<HelmetInspectionController>(true).Length == 1,
                    "one integrated inspection exhibit", "Exactly one inspection controller exists, including inactive scene objects.");
            }
            inspection = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            Require(inspection != null, "inspection exhibit exists", ScenePath);
            var root = inspection.gameObject;
            helmet = root.GetComponent<XRGrabInteractable>();
            body = root.GetComponent<Rigidbody>();
            reset = root.GetComponent<HelmetExhibitReset>();
            window = root.GetComponentInChildren<HelmetWindowInteractable>();
            Require(helmet && body && reset && window && inspection.WindowPivot, "runtime components", "Root grab/reset/body and constrained window are present.");
            Require(!body.isKinematic && body.useGravity, "physical body", "Idle helmet retains the baseline dynamic gravity body.");
            Require(helmet.movementType == XRBaseInteractable.MovementType.VelocityTracking && helmet.useDynamicAttach,
                "grab configuration", "VelocityTracking and dynamic attachment remain enabled.");
            helmetColliders = helmet.colliders.Where(c => c && c.enabled && !c.isTrigger).ToArray();
            Require(helmetColliders.Length > 1, "compound colliders", helmetColliders.Length + " solid root colliders.");
            Require(window.colliders.Count > 0 && window.colliders.All(c => c && c.enabled && !helmet.colliders.Contains(c)),
                "window collider ownership", "Window's registered hit colliders are separate from the whole helmet grab.");
            var meshes = root.GetComponentsInChildren<MeshRenderer>();
            Require(meshes.Any(r => r.enabled) && meshes.All(r => r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh && r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0),
                "model meshes imported", meshes.Length + " mesh renderers have imported geometry.");
            homePosition = reset.HomePose.position;
            homeRotation = reset.HomePose.rotation;
            var simulators = UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>();
            report.officialSimulatorWasPresent = simulators.Length > 0;
            Require(report.officialSimulatorWasPresent, "official simulator instantiated", "Production rig creates the official simulator.");
            foreach (var simulator in simulators) simulator.enabled = false;
            originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            changedInputSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard);
            if (mouse != null && addedMouse) InputSystem.EnableDevice(mouse);
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).FirstOrDefault(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).FirstOrDefault(c => c.name == "Left Controller");
            Require(rightController && leftController, "action controllers", "Both production controller components exist.");
            Require(rightController.GetComponentInChildren<XRDirectInteractor>(true) != null, "right direct interactor", "Tracking state drives the saved direct interactor.");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            hmd = InputSystem.AddDevice<XRSimulatedHMD>();
            if (IsMainSceneRun && UnityEngine.Object.FindObjectOfType<HelmetInspectionPalette>())
            {
                // Inspect the official simulator's actual startup pose before the test writer changes it.
                bool concise = GameObject.Find("Exploration MORE TOOLS") != null;
                var labels = concise ? new[] { "WINDOW", "SECTION", "FLOAT", "RESET" }
                    : new[] { "WINDOW", "SECTION", "FLOAT", "RESET", "TURN LEFT", "TURN RIGHT", "TILT UP", "TILT DOWN" };
                foreach (var label in labels)
                {
                    var button = GameObject.Find("Inspection " + label);
                    var viewport = Camera.main.WorldToViewportPoint(button.transform.position);
                    Require(viewport.z > 0 && viewport.x > .02f && viewport.x < .98f && viewport.y > .02f && viewport.y < .98f,
                        "startup button visible: " + label, "Original startup camera; viewport=" + viewport + "; eye=" + Camera.main.transform.position);
                }
                CaptureMain("00-startup-controls");
                if (concise)
                    Require(GameObject.Find("Inspection TURN RIGHT") == null && GameObject.Find("Exploration MORE TOOLS") != null,
                        "advanced tools have a visible entry", "Concise presentation starts with core controls; original turn/tilt controls are reached through More tools below.");
            }
            QueueHead();
            QueueController(leftDevice, new Vector3(-.7f, 1f, 1f), Quaternion.identity, false);
            QueueRight(new Vector3(.7f, 1f, 1f), Quaternion.identity, false);
            yield return Wait(.5f);
            Require(rightController.selectAction.action != null && rightController.selectAction.action.enabled, "select action enabled", "Production input actions, not direct selection calls.");

            phase = "window tap";
            Vector3 touch = WindowTouch();
            QueueRight(touch, Quaternion.identity, false);
            yield return Wait(.25f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, phase);
            QueueRight(touch, Quaternion.identity, true);
            yield return Wait(.2f);
            Require(window.isSelected && !helmet.isSelected, "window input select", SelectionDetail());
            Require(window.interactorsSelecting[0] is XRDirectInteractor, "window direct interactor", window.interactorsSelecting[0].GetType().Name);
            Require(body.isKinematic, "window stabilizes body", "Window selection temporarily pins the exhibit.");
            QueueRight(touch, Quaternion.identity, false);
            yield return Wait(.85f);
            Require(!window.isSelected && !body.isKinematic && Mathf.Abs(inspection.WindowAngle - HelmetInspectionController.MaximumWindowAngle) < .5f,
                "window click opens", "Short grip/release animates to " + inspection.WindowAngle + " degrees and unpins the body.");
            yield return PressButton("WINDOW");
            yield return Wait(.85f);
            Require(inspection.WindowAngle < .5f, "window button closes", "World-space control closes the previously opened window.");

            phase = "distant ray window tap";
            touch = WindowTouch();
            var rayPosition = touch + inspection.transform.rotation * Vector3.back * .5f;
            var rayRotation = Quaternion.LookRotation(touch-rayPosition, Vector3.up);
            QueueRight(rayPosition, rayRotation, false);
            yield return Wait(.35f);
            QueueRight(rayPosition, rayRotation, true);
            yield return Wait(.2f);
            Require(window.isSelected && !helmet.isSelected, "distant ray selects window", SelectionDetail());
            Require(window.interactorsSelecting[0] is XRRayInteractor, "window uses ray beyond direct reach", "Controller is 0.5 m from the hit target; actual selected interactor=" + window.interactorsSelecting[0].GetType().Name);
            QueueRight(rayPosition, rayRotation, false);
            yield return Wait(.85f);
            Require(!window.isSelected && Mathf.Abs(inspection.WindowAngle-HelmetInspectionController.MaximumWindowAngle)<.5f,
                "distant ray short click opens", "Real XR ray grip/release invokes click-to-toggle without an arm arc; angle="+inspection.WindowAngle);
            yield return PressButton("WINDOW");
            yield return Wait(.8f);
            Require(inspection.WindowAngle < .5f, "window closes after ray test", "Return to a known closed state through the physical button.");

            phase = "window direct drag";
            touch = WindowTouch();
            QueueRight(touch, Quaternion.identity, false);
            yield return Wait(.25f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, phase);
            QueueRight(touch, Quaternion.identity, true);
            yield return Wait(.25f);
            Require(window.isSelected && !helmet.isSelected, "window select for drag", SelectionDetail());
            var pivot = inspection.WindowPivot;
            var localVector = pivot.parent.InverseTransformVector(MuseumPhysicalHandsTestPose.TrackingPosition(rightController, touch, Quaternion.identity) - pivot.position);
            // Builder uses a local Y hinge; sign is an explicit serialized interpretation parameter.
            var serialized = new SerializedObject(inspection);
            float sign = serialized.FindProperty("openAngleSign").floatValue < 0 ? -1f : 1f;
            yield return DragWindowArc(localVector, sign, 0f, 130f, .9f);
            yield return Wait(.15f);
            Require(window.isSelected && Mathf.Abs(inspection.WindowAngle - HelmetInspectionController.MaximumWindowAngle) < .6f,
                "drag clamps at maximum", "130-degree hand arc yields " + inspection.WindowAngle + " degree hinge; direct input.");
            var heldBodyPosition = body.position;
            QueueController(leftDevice, RootTouch(leftController), Quaternion.identity, false);
            yield return Wait(.2f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(leftController, phase + " / left root");
            QueueController(leftDevice, RootTouch(leftController), Quaternion.identity, true);
            yield return Wait(.25f);
            if (inspection.AllowHeldChildInteraction)
                Require(window.isSelected && helmet.isSelected && !body.isKinematic,
                    "different hand grabs root during window drag", "Both selections coexist and XRI retains the dynamic root; " + SelectionDetail());
            else
                Require(window.isSelected && !helmet.isSelected && Vector3.Distance(body.position, heldBodyPosition) < .01f,
                    "whole helmet blocked during window drag", "Second hand grip cannot steal or move the pinned root.");
            QueueController(leftDevice, new Vector3(-.7f, 1f, 1f), Quaternion.identity, false);
            if (inspection.AllowHeldChildInteraction)
            {
                yield return Wait(.2f);
                Require(window.isSelected && !helmet.isSelected && body.isKinematic,
                    "last root hand release restores child pin", "The window grip remains active without a throw or a falling root.");
            }
            yield return DragWindowArc(localVector, sign, 130f, 0f, .9f);
            Require(inspection.WindowAngle < .6f, "drag closes", "Returning the controller arc closes the hinge; angle=" + inspection.WindowAngle);
            QueueRight(WindowTouch(), Quaternion.identity, false);
            yield return Wait(.3f);
            Require(!window.isSelected && !body.isKinematic && inspection.WindowAngle < .6f,
                "drag release is not a click", "A long drag releases without toggling the closed window.");

            phase = "window held reset";
            yield return PressButton("SECTION");
            Require(inspection.IsCutaway, "cutaway button enables", "Real controller grip selected SECTION.");
            touch = WindowTouch();
            QueueRight(touch, Quaternion.identity, false);
            yield return Wait(.2f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, phase);
            QueueRight(touch, Quaternion.identity, true);
            yield return Wait(.2f);
            Require(window.isSelected, "window selected before Home", SelectionDetail());
            int resetsBefore = reset.ResetCount;
            yield return PressHome();
            yield return Wait(.5f);
            Require(reset.ResetCount == resetsBefore + 1 && !window.isSelected && !helmet.isSelected && !body.isKinematic && !inspection.IsCutaway && inspection.WindowAngle < .1f,
                "Home cancels window and restores shell", "Reset delta=" + (reset.ResetCount-resetsBefore) + "; " + SelectionDetail());
            QueueRight(WindowTouch(), Quaternion.identity, true);
            yield return Wait(.4f);
            Require(!window.isSelected && !helmet.isSelected, "held reset grip cannot reselect", "Same held grip remains blocked after cooldown, even over the restored window.");
            QueueRight(WindowTouch(), Quaternion.identity, false);
            yield return Wait(.2f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, phase + " / window reselect");
            QueueRight(WindowTouch(), Quaternion.identity, true);
            yield return Wait(.2f);
            Require(window.isSelected, "window usable after releasing reset grip", SelectionDetail());
            yield return PressHome();
            QueueRight(new Vector3(.7f, 1f, 1f), Quaternion.identity, false);
            yield return Wait(.25f);

            phase = "section and turn controls";
            yield return PressButton("SECTION");
            CheckPlane("cutaway property blocks enabled", true);
            Quaternion rotationBefore = body.rotation;
            yield return PressButton("TURN RIGHT");
            yield return Wait(.15f);
            Require(Mathf.Abs(Quaternion.Angle(rotationBefore, body.rotation)-30f)<1.5f, "turn button rotates display", "Root rotation delta="+Quaternion.Angle(rotationBefore,body.rotation));
            CheckPlane("cut plane follows rotated helmet", true);
            yield return PressButton("SECTION");
            CheckPlane("cutaway property blocks disabled", false);
            yield return PressButton("WINDOW");
            yield return Wait(.75f);
            yield return PressButton("RESET");
            yield return Wait(.4f);
            Require(Quaternion.Angle(body.rotation, homeRotation)<3f && Vector3.Distance(body.position,homePosition)<.06f && inspection.WindowAngle<.1f && !inspection.IsCutaway,
                "inspection RESET restores full state", "World pose, closed window and full shell are restored by the physical control.");

            phase = "whole helmet grab and placement";
            cycleNumber = 1;
            var before = body.position;
            rotationBefore = body.rotation;
            resetsBefore = reset.ResetCount;
            touch = RootTouch();
            QueueRight(touch, Quaternion.identity, false);
            yield return Wait(.25f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, phase);
            QueueRight(touch, Quaternion.identity, true);
            yield return Wait(.3f);
            Require(helmet.isSelected && !window.isSelected, "whole helmet input grab", SelectionDetail());
            Require(helmet.interactorsSelecting[0] is XRDirectInteractor, "whole helmet direct grab", helmet.interactorsSelecting[0].GetType().Name);
            QueueController(leftDevice, WindowTouch(leftController), Quaternion.identity, false);
            yield return Wait(.2f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(leftController, phase + " / left window");
            QueueController(leftDevice, WindowTouch(leftController), Quaternion.identity, true);
            yield return Wait(.2f);
            if (inspection.AllowHeldChildInteraction)
                Require(helmet.isSelected && window.isSelected && !body.isKinematic,
                    "different hand opens window while root held", "Both selections coexist without pinning the held Rigidbody; " + SelectionDetail());
            else
                Require(helmet.isSelected && !window.isSelected, "window blocked during whole helmet grab", SelectionDetail());
            QueueController(leftDevice,new Vector3(-.7f,1f,1f),Quaternion.identity,false);
            var delta = new Vector3(.12f,.32f,-.12f);
            var turn = Quaternion.Euler(8,32,8);
            yield return MoveHand(touch,touch+delta,Quaternion.identity,turn,.5f,true);
            yield return Wait(.2f);
            Require(helmet.isSelected && body.position.y-before.y>.20f && Quaternion.Angle(rotationBefore,body.rotation)>20f,
                "whole helmet follows pose", "Lift="+(body.position.y-before.y)+"; turn="+Quaternion.Angle(rotationBefore,body.rotation));
            yield return MoveHand(touch+delta,touch,turn,Quaternion.identity,.6f,true);
            yield return Wait(.4f);
            QueueRight(touch,Quaternion.identity,false);
            yield return Wait(.2f);
            QueueRight(touch+new Vector3(.65f,.3f,-.4f),Quaternion.identity,false);
            yield return Wait(1.3f);
            var settled=body.position;
            yield return Wait(.2f);
            Require(!helmet.isSelected && reset.ResetCount == resetsBefore && Vector3.Distance(body.position,before)<.10f && body.velocity.magnitude<.15f && body.angularVelocity.magnitude<.6f && Vector3.Distance(body.position,settled)<.006f,
                "whole helmet stable release", "One real-input grab/lift/rotate/return/release cycle; no recovery masked a fall. Speed="+body.velocity.magnitude+"; angular="+body.angularVelocity.magnitude);
            Require(DeepestStaticPenetration()<.006f,"placement has no deep penetration","Maximum overlap="+DeepestStaticPenetration()+" m.");
            report.completedCycles=1;

            phase = "whole helmet held reset";
            Vector3 windowAtHome = WindowTouch();
            touch=RootTouch();
            QueueRight(touch,Quaternion.identity,false);
            yield return Wait(.25f);
            // The prior placement can leave the glove on another side or at a
            // different wrist angle. Approach the same root touch around the solid
            // helmet rather than asking the collision proxy to cross through it.
            yield return MuseumPhysicalHandsTestPose.Approach(rightController, helmet,
                (position, rotation, grip) => QueueRight(position, rotation, grip), phase);
            QueueRight(touch,Quaternion.identity,true);
            yield return Wait(.25f);
            Require(helmet.isSelected,"whole helmet selected before Home",SelectionDetail());
            // Root reset must also prevent the same held grip from transferring to its child window.
            QueueRight(windowAtHome,Quaternion.identity,true);
            yield return Wait(.25f);
            resetsBefore=reset.ResetCount;
            yield return PressHome();
            yield return Wait(.4f);
            Require(!helmet.isSelected && reset.ResetCount==resetsBefore+1 && Vector3.Distance(body.position,homePosition)<.06f,
                "whole helmet held Home remains safe",SelectionDetail());
            QueueRight(windowAtHome,Quaternion.identity,true);
            yield return Wait(.4f);
            Require(!helmet.isSelected && !window.isSelected,"whole helmet reset blocks transfer to window","Grip remains pressed over the home window hit target; "+SelectionDetail());
            QueueRight(WindowTouch(),Quaternion.identity,false);
            yield return Wait(.22f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, phase + " / window reselect");
            QueueRight(WindowTouch(),Quaternion.identity,true);
            yield return Wait(.22f);
            Require(window.isSelected && !helmet.isSelected,"window available after root-reset grip release",SelectionDetail());
            yield return PressHome();
            QueueRight(new Vector3(.7f,1f,1f),Quaternion.identity,false);
            yield return Wait(.3f);

            phase = "fall recovery";
            // Deliberate test setup moves the dynamic body out of bounds; the production reset detects it.
            inspection.SetWindowOpen(true,true);
            inspection.SetCutaway(true);
            resetsBefore=reset.ResetCount;
            body.position=homePosition+Vector3.down*6f;
            body.velocity=Vector3.down;
            yield return Wait(1.3f);
            Require(reset.ResetCount==resetsBefore+1 && Vector3.Distance(body.position,homePosition)<.06f && inspection.WindowAngle<.1f && !inspection.IsCutaway,
                "fall recovery also restores inspection", "Out-of-bounds setup is direct; recovery runs through normal component updates.");

            phase = "public API edge cases";
            inspection.SetWindowAngle(float.NaN);
            Require(!float.IsNaN(inspection.WindowAngle),"API rejects invalid angle","Explicit public-API unit check, not XR input.");
            inspection.ReduceMotion=true;
            inspection.SetWindowOpen(true);
            Require(Mathf.Abs(inspection.WindowAngle-HelmetInspectionController.MaximumWindowAngle)<.1f,"API reduced motion opens immediately","Explicit public-API check.");
            inspection.SetCutaway(true);
            inspection.enabled=false;
            Require(inspection.WindowAngle<.1f && !inspection.IsCutaway && !body.isKinematic,"API disable restores state","Disabling the feature restores its display state and body.");
            inspection.enabled=true;
            inspection.ReduceMotion=false;
            yield return Wait(.3f);
            if (IsMainSceneRun && UnityEngine.Object.FindObjectOfType<HelmetInspectionPalette>())
                yield return ValidateAdvancedInspection();
            Require(report.errors.Count==0,"no runtime errors",string.Join("\n",report.errors));
        }

        static IEnumerator ValidateAdvancedInspection()
        {
            phase = "advanced section and portable controls";
            var palette = UnityEngine.Object.FindObjectOfType<HelmetInspectionPalette>();
            var tool = inspection.GetComponentInChildren<HelmetSectionInteractable>(true);
            Require(palette && tool, "advanced tools integrated", "Saved main scene includes the portable controls and section handle.");
            palette.FollowEnabled = false;
            yield return PressButton("SECTION");
            yield return PressButton("EDIT CUT");
            Require(inspection.IsCutaway && tool.ShowTool, "input reveals section tool", "Both commands travel through production grip input.");
            QueueController(leftDevice, new Vector3(-1,1,1), Quaternion.Euler(-80,0,0), false);
            var touch = SectionTouch(tool, Quaternion.identity);
            QueueRight(touch, Quaternion.identity, false); yield return Wait(.3f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, phase + " / section");
            QueueRight(touch, Quaternion.identity, true); yield return Wait(.25f);
            Require(tool.isSelected && inspection.IsSectionHeld && !helmet.isSelected && !window.isSelected,
                "direct grip owns section tool", "Tool is selected without stealing the helmet or window.");
            Require(tool.interactorsSelecting[0] is XRDirectInteractor && body.isKinematic, "section direct input pins exhibit", "Production direct interactor owns the relative-pose tool.");
            var normalBefore = inspection.SectionNormalLocal;
            yield return MoveHand(touch, touch + new Vector3(.055f,.025f,0), Quaternion.identity, Quaternion.Euler(35,25,28), 1f, true);
            yield return Wait(.2f);
            Require(Vector3.Angle(normalBefore, inspection.SectionNormalLocal) > 20f && Mathf.Abs(inspection.SectionOffset) > .008f,
                "hand translation and wrist rotation edit cut", "Normal="+inspection.SectionNormalLocal+"; offset="+inspection.SectionOffset);
            QueueRight(touch + new Vector3(.055f,.025f,-.3f), Quaternion.Euler(-70,0,0), false); yield return Wait(.4f);
            var releasedNormal=inspection.SectionNormalLocal; var releasedOffset=inspection.SectionOffset;
            yield return Wait(.4f);
            Require(!tool.isSelected && !body.isKinematic && Vector3.Angle(releasedNormal,inspection.SectionNormalLocal)<.1f && Mathf.Abs(releasedOffset-inspection.SectionOffset)<.001f,
                "released cut remains fixed", "No continuing drift after releasing the grip.");
            CaptureMain("05-oblique-cut");
            yield return PressButton("ALIGN VIEW"); yield return Wait(.25f);
            var aligned = inspection.WorldSectionPlane;
            Require(Vector3.Dot(new Vector3(aligned.x,aligned.y,aligned.z), (Camera.main.transform.position-inspection.transform.TransformPoint(inspection.SectionCenterLocal)).normalized) > .99f,
                "align view exposes near side", "A single grip click aligns the plane to the current visitor position.");
            yield return PressButton("EDIT CUT"); yield return Wait(.2f);
            CaptureMain("08-view-aligned-interior");
            var depthBefore=inspection.SectionOffset;
            yield return PressButton("DEEPER");yield return Wait(.25f);
            Require(inspection.SectionOffset<depthBefore-.005f,"depth button changes cut without dragging","Single-hand alternative to the spatial guide.");
            CaptureMain("09-deeper-section");
            releasedNormal=inspection.SectionNormalLocal;

            // Explicit API geometry check: point/normal remain attached to the object under a full 3D root rotation.
            inspection.ToggleFloating();
            inspection.RotateDisplayAround(new Vector3(1,0,1).normalized, 35f);
            yield return Wait(.4f);
            var plane=inspection.WorldSectionPlane;var n=new Vector3(plane.x,plane.y,plane.z);
            var point=inspection.transform.TransformPoint(inspection.SectionPointLocal);
            Require(Mathf.Abs(Vector3.Dot(n,point)+plane.w)<.001f && Vector3.Angle(inspection.SectionNormalLocal,releasedNormal)<.1f,
                "cut follows pitch and roll", "Explicit geometry API check; a point on the local plane remains on the world plane.");
            Require(inspection.IsFloating && body.isKinematic && !body.useGravity, "floating inspection holds pose", "Suspended at the current pose after release.");
            var floatingPose=body.position; yield return Wait(.5f);
            Require(Vector3.Distance(floatingPose,body.position)<.003f, "floating has no gravity drift", "Pose checked over half a second.");
            yield return PressButton("RESET"); yield return Wait(.3f);
            Require(!inspection.IsFloating && !body.isKinematic && body.useGravity && !tool.ShowTool, "reset restores physics and hides tool", "Original RESET path restores ordinary exhibit mode.");

            yield return PressButton("FLOAT");
            touch=RootTouch(); QueueRight(touch,Quaternion.identity,false);yield return Wait(.25f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, phase + " / floating root");
            QueueRight(touch,Quaternion.identity,true);yield return Wait(.25f);
            Require(helmet.isSelected && !tool.isSelected,"floating exhibit still supports ordinary grab","Same XRGrabInteractable, controlled by a single hand.");
            var rootPosition=body.position;var rootRotation=body.rotation;
            yield return MoveHand(touch,touch+new Vector3(.12f,.24f,-.12f),Quaternion.identity,Quaternion.Euler(20,35,15),1.1f,true);
            yield return Wait(.3f);
            Require(body.position.y>rootPosition.y+.1f && Quaternion.Angle(body.rotation,rootRotation)>20,"single hand lifts and turns in 3D","Uses production grip, hand position and full rotation input.");
            QueueRight(touch+new Vector3(.12f,.24f,-.12f),Quaternion.Euler(20,35,15),false);yield return Wait(.3f);
            var heldPose=body.position;var heldRotation=body.rotation;yield return Wait(.6f);
            Require(!helmet.isSelected && body.isKinematic && Vector3.Distance(heldPose,body.position)<.004f && Quaternion.Angle(heldRotation,body.rotation)<.5f,
                "one hand can release and keep inspection pose","No throw or sag after releasing a floating exhibit.");
            CaptureMain("06-floating-tilted");
            yield return PressButton("RESET");yield return Wait(.3f);

            yield return PressButton("SECTION");yield return PressButton("EDIT CUT");
            touch=tool.colliders[0].bounds.center;
            var distant=touch+new Vector3(.15f,.03f,-.65f); var aim=Quaternion.LookRotation(touch-distant);
            QueueRight(distant,aim,false);yield return Wait(.3f);
            QueueRight(distant,aim,true);yield return Wait(.25f);
            Require(tool.isSelected && tool.interactorsSelecting[0] is XRRayInteractor,"distant ray grips section tool","Tool can be manipulated without reaching across the exhibit.");
            var before=inspection.SectionNormalLocal;
            yield return MoveHand(distant,distant+new Vector3(.03f,.04f,0),aim,Quaternion.Euler(0,25,20)*aim,.8f,true);
            Require(Vector3.Angle(before,inspection.SectionNormalLocal)>10,"ray wrist rotation changes cut","Relative pose does not snap the cutting plane to the remote hand.");
            var resetsBefore=reset.ResetCount;
            yield return PressHome();yield return Wait(.4f);
            Require(reset.ResetCount==resetsBefore+1 && !tool.isSelected && !inspection.IsCutaway && !body.isKinematic,"Home cancels held section tool","The former grip cannot leave a stale pinned body.");
            inspection.SetCutaway(true);tool.ShowTool=true; // labelled reset-lock setup, not claimed as input.
            touch=SectionTouch(tool, Quaternion.identity);QueueRight(touch,Quaternion.identity,true);yield return Wait(.35f);
            Require(!tool.isSelected && !helmet.isSelected && !window.isSelected,"held reset grip cannot reselect another part","Test continues past the original 0.15 second cooldown.");
            QueueRight(touch,Quaternion.identity,false);yield return Wait(.2f);
            yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, phase + " / section reselect");
            QueueRight(touch,Quaternion.identity,true);yield return Wait(.2f);
            Require(tool.isSelected,"release enables section grip again","User can continue after releasing the previous grip.");
            QueueRight(touch+Vector3.back*.3f,Quaternion.Euler(-80,0,0),false);yield return Wait(.2f);
            yield return PressButton("RESET");yield return Wait(.2f);

            phase="portable controls from close, rear and seated viewpoints";
            var camera=Camera.main;var startPosition=camera.transform.position;var startRotation=camera.transform.rotation;
            var views=new[]{new Vector3(.48f,1.62f,3.1f),new Vector3(.2f,1.62f,4.2f),new Vector3(0,1.2f,2.55f)};
            for(int i=0;i<views.Length;i++)
            {
                QueueRight(new Vector3(1,1,1),Quaternion.Euler(-80,0,0),false);
                QueueController(leftDevice,new Vector3(-1,1,1),Quaternion.Euler(-80,0,0),false);
                var rotation=Quaternion.LookRotation(inspection.transform.position+Vector3.up*.30f-views[i]);
                QueueHeadPose(views[i],rotation);palette.FollowEnabled=true;yield return Wait(1.8f);
                Require(Vector3.Distance(camera.transform.position,palette.transform.position)<1.1f && Vector3.Dot(camera.transform.forward,palette.transform.position-camera.transform.position)>0,
                    "palette remains reachable at view "+i,"Includes close approach, the far side of the exhibit and seated eye height.");
                var button=GameObject.Find("Inspection SECTION").GetComponent<XRSimpleInteractable>();var target=button.GetComponent<Collider>().bounds.center;
                var hand=camera.transform.position+camera.transform.right*.08f-camera.transform.up*.08f;
                var direction=Quaternion.LookRotation(target-hand);
                QueueRight(hand,direction,false);yield return Wait(.3f);
                var palettePosition=palette.transform.position;
                Require(button.isHovered && palette.IsFrozen,"aiming freezes palette at view "+i,"Production ray hover stops following.");
                QueueHeadPose(views[i]+Vector3.right*.27f,rotation);yield return Wait(.9f);
                Require(Vector3.Distance(palettePosition,palette.transform.position)<.003f,"palette does not escape aimed ray at view "+i,"Head movement while targeting does not move the control.");
                bool wasCut=inspection.IsCutaway;QueueRight(hand,direction,true);yield return Wait(.25f);
                Require(button.isSelected && inspection.IsCutaway!=wasCut,"ray selects section at view "+i,"Real grip hits the same world-space button from this viewpoint.");
                QueueRight(hand,Quaternion.Euler(-80,0,0),false);yield return Wait(.3f);
                CaptureMain("07-view-"+i);
            }
            palette.FollowEnabled=false;QueueHeadPose(startPosition,startRotation);yield return Wait(.3f);palette.RecenterNow();
            yield return PressButton("RESET");yield return Wait(.3f);

            phase="advanced API boundaries";
            inspection.SetSectionPlane(Vector3.zero,float.NaN);
            Require(!float.IsNaN(inspection.SectionOffset) && inspection.SectionNormalLocal.sqrMagnitude>.9f,"invalid cut inputs rejected","Explicit API check.");
            inspection.SetSectionPlane(Vector3.up,100f);
            Require(Mathf.Abs(inspection.SectionOffset)<=inspection.SectionRange+.001f,"section depth remains bounded","Explicit API clamp prevents accidental disappearance.");
            inspection.ResetSection();
            // The following is an isolated solver fixture. The panel's input
            // paths were exercised above; keep its solids out of the wrist arc.
            bool paletteWasActive = palette.gameObject.activeSelf;
            palette.gameObject.SetActive(false);
            yield return ValidateSectionInputContinuity(tool);
            palette.gameObject.SetActive(paletteWasActive);
            palette.FollowEnabled=true;
        }

        static IEnumerator ValidateSectionInputContinuity(HelmetSectionInteractable tool)
        {
            // Only the fixture uses direct APIs. Every select, move, wrist rotation, release and
            // regrab below is sent through the production controller's Input System state.
            var rootPoses = new[] { Vector3.zero, new Vector3(25,40,17) };
            var positive = new[] {77f,78f,78.51f,78.53f,80f,84f,88f,89.5f,90f,90.5f,92f,96f,
                92f,90.5f,90f,89.5f,88f,84f,80f,78.53f,78.51f,78f,77f};
            var negative = positive.Select(angle => -angle).ToArray();
            var roll = new[] {0f,5f,15f,30f,45f,30f,15f,0f,-15f,-30f,-45f};
            foreach (var euler in rootPoses)
            {
                yield return SectionInputSequence(tool,euler,"positive threshold and pole",positive,false);
                yield return SectionInputSequence(tool,euler,"negative threshold and pole",negative,false);
                yield return SectionInputSequence(tool,euler,"pure roll",roll,true);
            }
            phase="section continuity explicit API boundaries";
            var knob=tool.colliders[0].transform;
            var beforeFlipRotation=tool.transform.rotation;var beforeFlipKnob=knob.position;
            var beforeFlipNormal=inspection.SectionNormalLocal;var beforeFlipOffset=inspection.SectionOffset;
            inspection.FlipSection();yield return Wait(.1f);
            var flippedPlane=inspection.WorldSectionPlane;
            Require(Vector3.Dot(beforeFlipNormal,inspection.SectionNormalLocal)<-.9999f && Mathf.Abs(inspection.SectionOffset+beforeFlipOffset)<.0001f &&
                Vector3.Angle(tool.transform.forward,new Vector3(flippedPlane.x,flippedPlane.y,flippedPlane.z))<.1f,
                "API first flip reverses the actual plane", "Check the first flip took effect before checking its round trip.");
            inspection.FlipSection();yield return Wait(.1f);
            Require(SectionQuaternionAngle(beforeFlipRotation,tool.transform.rotation)<.1f && Vector3.Distance(beforeFlipKnob,knob.position)<.001f,
                "API two flips restore guide frame and knob", "Explicit API round-trip check after a nonzero XR wrist roll. "+SectionPoseDetail(tool,knob,beforeFlipRotation,beforeFlipKnob));
            var localGuideRotation=Quaternion.Inverse(inspection.transform.rotation)*tool.transform.rotation;
            var localKnob=inspection.transform.InverseTransformPoint(knob.position);
            var rootBeforeHide=inspection.transform.rotation;
            var toolRenderers=tool.GetComponentsInChildren<Renderer>(true);
            tool.ShowTool=false;
            Require(!tool.ShowTool && tool.colliders.Count>0 && tool.colliders.All(c=>!c.enabled) && toolRenderers.Length>0 && toolRenderers.All(r=>!r.enabled),
                "API hides guide renderers and colliders", "Hidden guide must not remain visible or selectable.");
            inspection.RotateDisplayAround(Vector3.up,35f);
            yield return Wait(.1f);
            tool.ShowTool=true;yield return Wait(.1f);
            Require(tool.ShowTool && tool.colliders.All(c=>c.enabled && c.gameObject.activeInHierarchy) && toolRenderers.All(r=>r.enabled && r.gameObject.activeInHierarchy),
                "API reshow restores guide renderers and colliders", "Reappearing guide must restore both visuals and interaction targets.");
            float localFrameError=SectionQuaternionAngle(localGuideRotation,Quaternion.Inverse(inspection.transform.rotation)*tool.transform.rotation);
            float localKnobError=Vector3.Distance(localKnob,inspection.transform.InverseTransformPoint(knob.position));
            Require(SectionQuaternionAngle(rootBeforeHide,inspection.transform.rotation)>34f && localFrameError<.1f && localKnobError<.001f,
                "API hide rotate show preserves helmet-local frame", "Explicit API fixture check; local rotation error="+localFrameError+" degrees; local knob error="+localKnobError+" m.");
            QueueRight(new Vector3(1,1,1),Quaternion.Euler(-80,0,0),false);
            yield return Wait(.15f);
            inspection.ResetInspection(); // Explicit fixture cleanup; does not count as an input test.
            yield return Wait(.15f);
        }

        static IEnumerator SectionInputSequence(HelmetSectionInteractable tool, Vector3 euler, string label, float[] angles, bool roll)
        {
            phase="section XR continuity: "+label+" root "+euler;
            var trace=new SectionInputTrace { name=label,rootEulerDegrees=euler };
            report.sectionInputContinuity.Add(trace);
            QueueRight(new Vector3(1,1,1),Quaternion.Euler(-80,0,0),false);
            QueueController(leftDevice,new Vector3(-1,1,1),Quaternion.Euler(-80,0,0),false);
            yield return Wait(.15f);
            inspection.ResetInspection();
            // Let XRI consume Reset's pending target pose before the fixture makes the body kinematic.
            yield return Wait(.15f);
            inspection.ToggleFloating();
            var fixedRootPosition=homePosition+Vector3.up*.35f;
            var fixedRootRotation=Quaternion.Euler(euler);
            body.position=fixedRootPosition;body.rotation=fixedRootRotation;
            inspection.transform.SetPositionAndRotation(fixedRootPosition,fixedRootRotation);
            float initialTheta=roll ? 30f : angles[0];
            float radians=initialTheta*Mathf.Deg2Rad;
            inspection.SetSectionPlane(new Vector3(Mathf.Cos(radians),Mathf.Sin(radians),0),0f);
            if (roll && MuseumPhysicalHandsTestPose.Hand(rightController))
                inspection.SetSectionPlane(inspection.SectionNormalLocal, inspection.SectionRange);
            inspection.SetCutaway(true);tool.ShowTool=true;
            Physics.SyncTransforms();
            yield return Wait(.15f);
            var knob=tool.colliders[0].transform;
            var center=inspection.transform.TransformPoint(inspection.SectionPointLocal);
            var initialKnob=knob.position;
            var initialGuideRotation=tool.transform.rotation;
            var initialNormal=tool.transform.forward;
            var axis=roll ? initialNormal : fixedRootRotation*Vector3.forward;
            var outward = roll ? axis : (Vector3.Dot(initialKnob-center,axis) < 0 ? -axis : axis);
            var initialHandRotation=MuseumPhysicalHandsTestPose.Hand(rightController)
                ? Quaternion.LookRotation(-outward, Mathf.Abs(Vector3.Dot(outward.normalized,Vector3.up)) > .95f ? Vector3.right : Vector3.up)
                : Quaternion.identity;
            // The contact offset lies on the rotation axis, so this external-hand
            // fixture retains the original exact plane/knob trajectory under wrist roll.
            var touch=SectionTouch(tool, initialHandRotation, outward);
            var initialHandTrackingPosition=MuseumPhysicalHandsTestPose.TrackingPosition(rightController, touch, initialHandRotation);
            int resetsBefore=reset.ResetCount;
            Require(inspection.IsFloating && body.isKinematic && !body.useGravity && tool.ShowTool,
                phase+" floating fixture", "API setup fixes root/initial normal and puts pure roll at the bounded outer depth. Unrelated panel solids are hidden; hand/exhibit collision and all precision assertions remain active. Selection and wrist motion are synthetic XR input.");
            QueueRight(touch,initialHandRotation,false);yield return Wait(.15f);
            yield return MuseumPhysicalHandsTestPose.Approach(rightController, helmet,
                (position, rotation, grip) => QueueRight(position, rotation, grip), phase + " / section trace");
            QueueRight(touch,initialHandRotation,true);yield return Wait(.15f);
            Require(tool.isSelected && tool.interactorsSelecting[0] is XRDirectInteractor && !helmet.isSelected && !window.isSelected,
                phase+" direct grip", SectionSelectionDiagnostics(tool,touch));
            Require(SectionQuaternionAngle(initialGuideRotation,tool.transform.rotation)<.1f && Vector3.Distance(initialKnob,knob.position)<.001f,
                phase+" grip does not jump", "Selecting a stationary handle must preserve its visual orientation and knob position.");
            var previousGuide=tool.transform.rotation;
            var previousKnob=knob.position;
            var previousTargetKnob=initialKnob;
            var lastHandRotation=initialHandRotation;
            var lastHandPosition=touch;
            float previousDegrees=angles[0];
            for(int i=1;i<angles.Length;i++)
            {
                float degrees=angles[i]-angles[0];
                var delta=Quaternion.AngleAxis(degrees,axis);
                // Move the held hand on the same circular path as the saved knob. Combined
                // translation + wrist rotation keeps the plane's canonical center stationary.
                var targetKnob=center+delta*(initialKnob-center);
                lastHandRotation=delta*initialHandRotation;
                lastHandPosition=MuseumPhysicalHandsTestPose.ControllerPosition(rightController,
                    initialHandTrackingPosition+(targetKnob-initialKnob), lastHandRotation);
                QueueRight(lastHandPosition,lastHandRotation,true);
                yield return Wait(.065f);
                float inputDelta=Mathf.Abs(angles[i]-previousDegrees);
                var plane=inspection.WorldSectionPlane;
                var planeNormal=new Vector3(plane.x,plane.y,plane.z);
                var step=new SectionInputStep
                {
                    inputDegrees=angles[i],inputDeltaDegrees=inputDelta,
                    guideRotationDeltaDegrees=SectionQuaternionAngle(previousGuide,tool.transform.rotation),
                    maximumRotationDeltaDegrees=inputDelta*1.5f+.12f,
                    knobDisplacementMeters=Vector3.Distance(previousKnob,knob.position),
                    maximumKnobDisplacementMeters=Vector3.Distance(previousTargetKnob,targetKnob)*2f+.001f,
                    guideRotationErrorDegrees=SectionQuaternionAngle(delta*initialGuideRotation,tool.transform.rotation),
                    knobPositionErrorMeters=Vector3.Distance(targetKnob,knob.position),
                    normalErrorDegrees=Mathf.Max(Vector3.Angle(delta*initialNormal,tool.transform.forward),Vector3.Angle(planeNormal,tool.transform.forward)),
                    centerErrorMeters=Vector3.Distance(center,tool.transform.position),
                    rootPositionErrorMeters=Vector3.Distance(fixedRootPosition,inspection.transform.position),
                    rootRotationErrorDegrees=SectionQuaternionAngle(fixedRootRotation,inspection.transform.rotation),
                    toolSelected=tool.isSelected && inspection.IsSectionHeld && !helmet.isSelected && !window.isSelected
                };
                trace.steps.Add(step);
                previousGuide=tool.transform.rotation;previousKnob=knob.position;previousTargetKnob=targetKnob;previousDegrees=angles[i];
            }
            string diagnostics=SectionTraceDiagnostics(trace);
            Require(trace.steps.All(s=>s.toolSelected) && reset.ResetCount==resetsBefore,
                phase+" grip stays on section",diagnostics);
            Require(trace.steps.All(s=>s.guideRotationDeltaDegrees<=s.maximumRotationDeltaDegrees && s.knobDisplacementMeters<=s.maximumKnobDisplacementMeters),
                phase+" visual guide and knob remain continuous",diagnostics);
            Require(trace.steps.All(s=>s.guideRotationErrorDegrees<.15f && s.knobPositionErrorMeters<.001f && s.normalErrorDegrees<.15f && s.centerErrorMeters<.001f),
                phase+" guide follows full wrist pose",diagnostics);
            Require(trace.steps.All(s=>s.rootPositionErrorMeters<.001f && s.rootRotationErrorDegrees<.1f),
                phase+" root remains fixed",diagnostics);
            if(roll)
                Require(Vector3.Angle(initialNormal,tool.transform.forward)<.15f && SectionQuaternionAngle(initialGuideRotation,tool.transform.rotation)>40f && Vector3.Distance(initialKnob,knob.position)>.15f,
                    phase+" roll is visible with unchanged normal", "Normal-only behavior is insufficient: actual guide orientation and offset knob must respond to the wrist roll. "+diagnostics);

            // Keep the final nonzero roll in place when releasing and approaching the same
            // knob with a differently oriented hand. Regrabbing must establish a new relative pose.
            var releasedGuide=tool.transform.rotation;var releasedKnob=knob.position;
            QueueRight(lastHandPosition,lastHandRotation,false);yield return Wait(.15f);
            Require(!tool.isSelected && body.isKinematic && inspection.IsFloating && SectionQuaternionAngle(releasedGuide,tool.transform.rotation)<.1f && Vector3.Distance(releasedKnob,knob.position)<.001f,
                phase+" release preserves guide pose", "Release is real XR grip-up; floating removes gravity as a confounding factor. "+SectionPoseDetail(tool,knob,releasedGuide,releasedKnob));
            var regrabRotation=Quaternion.Euler(10,40,20);
            var regrabPosition=SectionTouch(tool, regrabRotation, outward);
            QueueRight(regrabPosition,regrabRotation,false);yield return Wait(.15f);
            yield return MuseumPhysicalHandsTestPose.Approach(rightController, helmet,
                (position, rotation, grip) => QueueRight(position, rotation, grip), phase + " / section regrab");
            QueueRight(regrabPosition,regrabRotation,true);yield return Wait(.15f);
            Require(tool.isSelected && tool.interactorsSelecting[0] is XRDirectInteractor && SectionQuaternionAngle(releasedGuide,tool.transform.rotation)<.1f && Vector3.Distance(releasedKnob,knob.position)<.001f,
                phase+" regrab preserves guide pose", "New hand orientation must not reset the guide roll. "+SectionPoseDetail(tool,knob,releasedGuide,releasedKnob));
            yield return Wait(.1f);
            Require(tool.isSelected && SectionQuaternionAngle(releasedGuide,tool.transform.rotation)<.1f && Vector3.Distance(releasedKnob,knob.position)<.001f,
                phase+" stationary regrab has no drift",SectionPoseDetail(tool,knob,releasedGuide,releasedKnob));
            QueueRight(regrabPosition,regrabRotation,false);yield return Wait(.15f);
        }

        static string SectionTraceDiagnostics(SectionInputTrace trace)
        {
            return "root="+trace.rootEulerDegrees+"; "+string.Join(" | ",trace.steps.Select(s=>
                "input="+s.inputDegrees+" delta="+s.inputDeltaDegrees+" guideDelta="+s.guideRotationDeltaDegrees+"/"+s.maximumRotationDeltaDegrees+
                " knobDelta="+s.knobDisplacementMeters+"/"+s.maximumKnobDisplacementMeters+" guideError="+s.guideRotationErrorDegrees+
                " knobError="+s.knobPositionErrorMeters+" normalError="+s.normalErrorDegrees+" centerError="+s.centerErrorMeters+
                " rootError="+s.rootPositionErrorMeters+","+s.rootRotationErrorDegrees+" selected="+s.toolSelected));
        }

        static string SectionPoseDetail(HelmetSectionInteractable tool,Transform knob,Quaternion rotation,Vector3 position)
        {
            return "selected="+tool.isSelected+"; guide rotation drift="+SectionQuaternionAngle(rotation,tool.transform.rotation)+"; knob drift="+Vector3.Distance(position,knob.position);
        }

        static float SectionQuaternionAngle(Quaternion a,Quaternion b)
        {
            double dot=(double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z+(double)a.w*b.w;
            double a2=(double)a.x*a.x+(double)a.y*a.y+(double)a.z*a.z+(double)a.w*a.w;
            double b2=(double)b.x*b.x+(double)b.y*b.y+(double)b.z*b.z+(double)b.w*b.w;
            return (float)(2*Math.Acos(Math.Min(1,Math.Abs(dot)/Math.Sqrt(a2*b2)))*180/Math.PI);
        }

        static void QueueHeadPose(Vector3 world,Quaternion rotation)
        {
            var parent=Camera.main.transform.parent;
            var p=parent.InverseTransformPoint(world);var q=Quaternion.Inverse(parent.rotation)*rotation;
            var state=new XRSimulatedHMDState{isTracked=true,trackingState=3,devicePosition=p,centerEyePosition=p,leftEyePosition=p+q*(Vector3.left*.032f),rightEyePosition=p+q*(Vector3.right*.032f),deviceRotation=q,centerEyeRotation=q,leftEyeRotation=q,rightEyeRotation=q};
            PendingInput.Enqueue(()=>InputSystem.QueueStateEvent(hmd,state));
        }

        static void CaptureMain(string name)
        {
            var camera=Camera.main;var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(1280,960,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;
            try
            {
                foreach(var text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>())text.ForceMeshUpdate(true);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;pixels=new Texture2D(1280,960,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,960),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(OutputDirectory,name+".jpg"),pixels.EncodeToJPG(92));
            }
            finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);if(pixels)UnityEngine.Object.DestroyImmediate(pixels);}
        }

        static string SelectionDetail() => "window="+window.isSelected+"; root="+helmet.isSelected+"; angle="+inspection.WindowAngle+"; hand="+(rightController?rightController.transform.position.ToString():"missing")+
            "; "+MuseumPhysicalHandsTestPose.SelectionDiagnostics(rightController, window);

        static Vector3 WindowTouch(XRBaseController controller = null)
        {
            var collider=window.colliders[0];
            // Frontmost point of the explicit hit target avoids the whole-shell collider behind it.
            Vector3 outward=inspection.transform.rotation*Vector3.back;
            Vector3 legacy=collider.bounds.center + outward*.009f;
            return MuseumPhysicalHandsTestPose.WindowTouch(collider, controller ? controller : rightController,
                Quaternion.identity, outward, legacy);
        }

        static Vector3 RootTouch(XRBaseController controller = null)
        {
            controller=controller ? controller : rightController;
            var collider=helmetColliders.OrderByDescending(c=>c.bounds.size.sqrMagnitude).First();
            // Exercise an ordinary shell grasp away from the authored right-side clue.
            Vector3 outward=helmet.transform.rotation*Vector3.left;
            return MuseumPhysicalHandsTestPose.OutsideTouch(collider, controller, Quaternion.identity, outward, collider.bounds.center);
        }

        static Vector3 SectionTouch(HelmetSectionInteractable tool, Quaternion rotation, Vector3 outward = default)
        {
            var collider=tool.colliders[0];
            if(outward.sqrMagnitude<.000001f)
                outward=collider.bounds.center-inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            return MuseumPhysicalHandsTestPose.OutsideTouch(collider, rightController, rotation, outward, collider.bounds.center);
        }

        static IEnumerator PressButton(string label)
        {
            var button=GameObject.Find("Inspection "+label)?.GetComponent<XRSimpleInteractable>();
            if (!button && IsMainSceneRun && label != "MORE TOOLS")
            {
                var hidden = UnityEngine.Object.FindObjectsOfType<XRSimpleInteractable>(true)
                    .FirstOrDefault(b => b.name == "Inspection " + label && !b.gameObject.activeInHierarchy);
                if (hidden && GameObject.Find("Exploration MORE TOOLS"))
                {
                    // Reach the advanced controls through production input, not by unhiding test fixtures.
                    yield return PressButton("MORE TOOLS");
                    yield return Wait(.25f);
                    button = GameObject.Find("Inspection " + label)?.GetComponent<XRSimpleInteractable>();
                }
            }
            if (!button && label == "MORE TOOLS")
                button = GameObject.Find("Exploration MORE TOOLS")?.GetComponent<XRSimpleInteractable>();
            if (!button && label == "RESET" && IsMainSceneRun)
                button=GameObject.Find("Helmet Reset Button")?.GetComponent<XRSimpleInteractable>();
            Require(button,"button exists: "+label,"Independent world-space control.");
            var buttonCollider=button.GetComponent<Collider>();
            bool movedFloatingReset = label == "RESET" && inspection.IsFloating && Vector3.Distance(body.position, homePosition) > .15f;
            Quaternion touchRotation = movedFloatingReset ? button.transform.rotation : Quaternion.identity;
            var touch=movedFloatingReset
                ? MuseumPhysicalHandsTestPose.ControlTouch(buttonCollider, rightController, touchRotation,
                    -button.transform.forward, buttonCollider.bounds.center, helmet)
                : MuseumPhysicalHandsTestPose.OutsideTouch(buttonCollider, rightController, Quaternion.identity,
                    -button.transform.forward, buttonCollider.bounds.center);
            QueueRight(touch,touchRotation,false);
            yield return Wait(.22f);
            yield return MuseumPhysicalHandsTestPose.Approach(rightController, helmet,
                (position, rotation, grip) => QueueRight(position, rotation, grip), "button " + label);
            QueueRight(touch,touchRotation,true);
            yield return Wait(.22f);
            Require(button.isSelected,"input selects button: "+label,"Controller grip selects the saved control; touch="+touch+
                "; button="+buttonCollider.bounds.center+"; "+MuseumPhysicalHandsTestPose.SelectionDiagnostics(rightController, button));
            QueueRight(touch+Vector3.back*.25f,Quaternion.identity,false);
            yield return Wait(.22f);
        }

        static IEnumerator PressHome()
        {
            QueueKeyboard(new KeyboardState(Key.Home));
            yield return Wait(.1f);
            QueueKeyboard(new KeyboardState());
            yield return Wait(.1f);
        }

        static IEnumerator DragWindowArc(Vector3 closedLocalVector,float sign,float from,float to,float duration)
        {
            float start=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-start<duration)
            {
                float t=Mathf.SmoothStep(0,1,(Time.realtimeSinceStartup-start)/duration);
                Vector3 v=Quaternion.AngleAxis(Mathf.Lerp(from,to,t)*sign,Vector3.up)*closedLocalVector;
                QueueRight(MuseumPhysicalHandsTestPose.ControllerPosition(rightController,
                    inspection.WindowPivot.position+inspection.WindowPivot.parent.TransformVector(v), Quaternion.identity),Quaternion.identity,true);
                yield return null;
            }
            Vector3 last=Quaternion.AngleAxis(to*sign,Vector3.up)*closedLocalVector;
            QueueRight(MuseumPhysicalHandsTestPose.ControllerPosition(rightController,
                inspection.WindowPivot.position+inspection.WindowPivot.parent.TransformVector(last), Quaternion.identity),Quaternion.identity,true);
        }

        static void CheckPlane(string name,bool enabled)
        {
            var serialized=new SerializedObject(inspection);
            var array=serialized.FindProperty("clippedRenderers");
            Require(array.arraySize>0,name+" renderer list","At least one explicitly clipped shell renderer.");
            var block=new MaterialPropertyBlock();
            // Test the intended geometry, independently of the implementation's matrix formula:
            // two local X=0 points lie on the plane; +X and -X lie on opposite sides.
            Vector3 onA=inspection.transform.TransformPoint(Vector3.zero);
            Vector3 onB=inspection.transform.TransformPoint(new Vector3(0,.14f,.08f));
            Vector3 positive=inspection.transform.TransformPoint(new Vector3(.04f,.14f,.08f));
            Vector3 negative=inspection.transform.TransformPoint(new Vector3(-.04f,.14f,.08f));
            var faults=new List<string>();
            for(int i=0;i<array.arraySize;i++)
            {
                var renderer=(Renderer)array.GetArrayElementAtIndex(i).objectReferenceValue;
                if(!renderer) { faults.Add("Missing renderer at index "+i); continue; }
                renderer.GetPropertyBlock(block);
                Vector4 plane=block.GetVector("_CutPlane");
                Vector3 normal=new Vector3(plane.x,plane.y,plane.z);
                float atA=Vector3.Dot(normal,onA)+plane.w;
                float atB=Vector3.Dot(normal,onB)+plane.w;
                float atPositive=Vector3.Dot(normal,positive)+plane.w;
                float atNegative=Vector3.Dot(normal,negative)+plane.w;
                if(Mathf.Abs(block.GetFloat("_CutEnabled")-(enabled?1:0))>.001f ||
                    !IsFinite(plane.x) || !IsFinite(plane.y) || !IsFinite(plane.z) || !IsFinite(plane.w) ||
                    Mathf.Abs(normal.magnitude-1f)>.001f || Mathf.Abs(atA)>.001f || Mathf.Abs(atB)>.001f ||
                    atPositive<=.001f || atNegative>=-.001f)
                    faults.Add(renderer.name+": enabled="+block.GetFloat("_CutEnabled")+"; plane="+plane+"; signed points="+atA+", "+atB+", "+atPositive+", "+atNegative);
            }
            Require(inspection.IsCutaway==enabled && faults.Count==0,name,
                faults.Count==0 ? array.arraySize+" renderer blocks match the enabled state and helmet-local cutting geometry. Pixel rendering is not tested here." : string.Join("\n",faults));
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void QueueRight(Vector3 world, Quaternion rotation, bool grip) => QueueController(rightDevice, world, rotation, grip);
        static string SectionSelectionDiagnostics(HelmetSectionInteractable tool,Vector3 target)
        {
            var direct=rightController.GetComponentInChildren<XRDirectInteractor>(true);
            var candidates=new List<IXRInteractable>();direct.GetValidTargets(candidates);
            var sphere=direct.GetComponent<SphereCollider>();
            var query=direct.GetAttachTransform(null).TransformPoint(sphere.center);
            var overlaps=Physics.OverlapSphere(query,sphere.radius*direct.transform.lossyScale.x,direct.physicsLayerMask,QueryTriggerInteraction.Collide);
            return "tool="+tool.isSelected+" hovered="+tool.isHovered+" window="+window.isSelected+" helmet="+helmet.isSelected+" input="+target+" controller="+rightController.transform.position+" query="+query+" radius="+sphere.radius+" improve="+direct.usingSphereColliderAccuracyImprovement+" knob="+tool.colliders[0].bounds.center+" collider="+tool.colliders[0].enabled+" active="+tool.colliders[0].gameObject.activeInHierarchy+" selectable="+tool.IsSelectableBy(direct)+" group="+direct.containingGroup?.activeInteractor?.transform.name+" candidates="+string.Join(",",candidates.Select(c=>c.transform.name))+" overlaps="+string.Join(",",overlaps.Select(c=>c.name));
        }

        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip)
        {
            var parent = (device == leftDevice ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState
            {
                isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(world),
                deviceRotation = Quaternion.Inverse(parent.rotation) * rotation,
                grip = grip ? 1f : 0f
            }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }

        static void QueueHead()
        {
            var camera = Camera.main;
            if (!camera) return;
            var parent = camera.transform.parent;
            // Use the scene's starting position and view, including the rig's camera-height offset.
            var position = parent ? parent.InverseTransformPoint(camera.transform.position) : camera.transform.position;
            var rotation = parent ? Quaternion.Inverse(parent.rotation) * camera.transform.rotation : camera.transform.rotation;
            var state = new XRSimulatedHMDState
            {
                isTracked = true, trackingState = 3, devicePosition = position, centerEyePosition = position,
                leftEyePosition = position + Vector3.left * .032f, rightEyePosition = position + Vector3.right * .032f,
                deviceRotation = rotation, centerEyeRotation = rotation, leftEyeRotation = rotation, rightEyeRotation = rotation
            };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(hmd, state));
        }

        static void QueueKeyboard(KeyboardState state) => PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, state));

        static void FlushPlayerInput()
        {
            // EditorApplication.update can run in the Editor's separate keyboard state buffer.
            // Queue events just before the normal player input update so Update sees key edges.
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            while (PendingInput.Count > 0) PendingInput.Dequeue()();
        }

        static void InspectPlayerKeyboard()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || (phase != "window held reset" && phase != "whole helmet held reset") || report.inputDiagnostics.Count >= 80) return;
            report.inputDiagnostics.Add("frame=" + Time.frameCount + "; keyboard=" + keyboard?.name + "; id=" + keyboard?.deviceId +
                "; added=" + keyboard?.added + "; enabled=" + keyboard?.enabled + "; key=" + keyboard?.homeKey.isPressed +
                "; edge=" + keyboard?.homeKey.wasPressedThisFrame + "; current=" + Keyboard.current?.name +
                "; currentKey=" + Keyboard.current?.homeKey.isPressed + "; resetEnabled=" + reset.isActiveAndEnabled + "; resetCount=" + reset.ResetCount);
        }

        static IEnumerator MoveHand(Vector3 from, Vector3 to, Quaternion fromRotation, Quaternion toRotation, float duration, bool grip)
        {
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < duration)
            {
                var t = Mathf.SmoothStep(0, 1, (Time.realtimeSinceStartup - start) / duration);
                QueueRight(Vector3.Lerp(from, to, t), Quaternion.Slerp(fromRotation, toRotation, t), grip);
                yield return null;
            }
            QueueRight(to, toRotation, grip);
        }

        static IEnumerator Wait(float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static float DeepestStaticPenetration()
        {
            var depth = 0f;
            foreach (var own in helmetColliders)
            foreach (var other in Physics.OverlapBox(own.bounds.center, own.bounds.extents + Vector3.one * .01f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other.attachedRigidbody != null || other.transform.IsChildOf(helmet.transform)) continue;
                if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out var distance))
                    depth = Mathf.Max(depth, distance);
            }
            return depth;
        }

        static void Require(bool condition, string name, string detail)
        {
            report.checks.Add(new Check { name = name, passed = condition, detail = detail });
            if (!condition) throw new InvalidOperationException(name + ": " + detail);
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (report != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                report.errors.Add(type + ": " + message + "\n" + stack);
        }

        static void Finish(string failure)
        {
            if (finishing) return;
            finishing = true;
            Application.logMessageReceived -= OnLog;
            InputSystem.onBeforeUpdate -= FlushPlayerInput;
            InputSystem.onAfterUpdate -= InspectPlayerKeyboard;
            PendingInput.Clear();
            report.durationSeconds = (float)(EditorApplication.timeSinceStartup - started);
            report.failure = failure ?? "";
            report.status = failure == null && report.errors.Count == 0 ? "passed" : "failed";
            Directory.CreateDirectory(OutputDirectory);
            var path = Path.Combine(OutputDirectory, "report.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Routines.Clear();
            if (rightDevice != null && rightDevice.added) InputSystem.RemoveDevice(rightDevice);
            if (leftDevice != null && leftDevice.added) InputSystem.RemoveDevice(leftDevice);
            if (hmd != null && hmd.added) InputSystem.RemoveDevice(hmd);
            if (addedKeyboard && keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (addedMouse && mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (changedInputSettings)
            {
                InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
            }
            SessionState.SetInt(ResultKey, report.status == "passed" ? 0 : 1);
            Debug.Log("Helmet QA " + report.status + ": " + path + (failure == null ? "" : "\n" + failure));
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
