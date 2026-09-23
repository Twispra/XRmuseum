#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Editor
{
    /// <summary>
    /// Editor-only acceptance runner. Drives real Input System XR device state through the
    /// scene's unmodified actions/controllers/interactors; never calls SelectEnter/SelectExit.
    /// Batch: -executeMethod Museum.Editor.HelmetPrototypeValidation.RunBatch (omit -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class HelmetPrototypeValidation
    {
        const string ActiveKey = "Museum.HelmetQA.Active";
        const string BatchKey = "Museum.HelmetQA.Batch";
        const string ResultKey = "Museum.HelmetQA.ExitCode";
        const string ProbeKey = "Museum.HelmetQA.Probe";
        const string RunIdKey = "Museum.HelmetQA.RunId";
        const string ProvenanceKey = "Museum.HelmetQA.Provenance";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
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
        static Rigidbody body;
        static Collider[] helmetColliders;
        static double started;
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
            public bool selected;
            public int resetCount;
            public bool homeKeyDown;
            public Vector3 position, velocity, hand, target;
        }
        [Serializable] class Report
        {
            public string timestampUtc, unityVersion, scene, inputMethod, limitations, status = "running", failure = "";
            public Provenance provenance;
            public float durationSeconds;
            public bool officialSimulatorWasPresent;
            public int colliderCount, completedCycles;
            public List<Check> checks = new List<Check>();
            public List<Cycle> cycles = new List<Cycle>();
            public List<string> errors = new List<string>();
            public List<Sample> samples = new List<Sample>();
            public List<string> inputDiagnostics = new List<string>();
        }

        static HelmetPrototypeValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
        }

        [MenuItem("Museum/Validate Helmet Interaction (Play Mode)")]
        public static void RunInEditor() { SessionState.SetBool(ProbeKey, false); Begin(false); }
        public static void RunBatch() { SessionState.SetBool(ProbeKey, false); Begin(true); }
        public static void RunInputProbe() { SessionState.SetBool(ProbeKey, true); Begin(true); }

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

        static void Begin(bool batch)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run helmet validation from Edit Mode.");
            // Do not save a user's dirty scene as a side effect of an acceptance test.
            if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N");
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
                "MuseumVR/Assets/Museum/Prefabs/HelmetExhibit.prefab",
                "MuseumVR/Assets/Museum/Scripts/HelmetExhibitReset.cs",
                "MuseumVR/Assets/Museum/Editor/HelmetPrototypeValidation.cs"
            };
            foreach (var relativePath in paths)
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
                    scene = ScenePath,
                    inputMethod = "InputSystem.QueueStateEvent in the player Dynamic input update on XRSimulatedController/HMD -> production Input Actions -> ActionBasedController -> existing XRDirectInteractor -> XRGrabInteractable and Rigidbody. Home uses a synthetic KeyboardState. Hidden batch frames are paced to approximately 90 Hz to remain above XRI 2.6's 1 ms throw-sampling cutoff.",
                    limitations = "Automated desktop simulation only. Official XR Device Simulator presence is checked, then its device writer is disabled to avoid competing states. Its mouse/keyboard navigation mappings, real headset tracking, hand comfort, and device performance still require manual testing. The test driver does not call XRI selection APIs."
                };
                Application.logMessageReceived += OnLog;
                InputSystem.onBeforeUpdate += FlushPlayerInput;
                InputSystem.onAfterUpdate += InspectPlayerKeyboard;
                Routines.Clear();
                Routines.Push(Validate());
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                var code = SessionState.GetInt(ResultKey, 1);
                var batch = SessionState.GetBool(BatchKey, false);
                SessionState.SetBool(ActiveKey, false);
                if (batch) EditorApplication.delayCall += () =>
                {
                    try { if (code == 0) HelmetPrototypePreview.CaptureToDirectory(Path.Combine(OutputDirectory, "preview")); }
                    catch (Exception exception) { Debug.LogException(exception); code = 1; }
                    EditorApplication.Exit(code < 0 ? 1 : code);
                };
            }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 115)
            {
                Finish("Acceptance test exceeded 115 seconds.");
                return;
            }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            // A hidden batch player otherwise runs at >2000 Hz. XRI 2.6 deliberately skips
            // throw samples with deltaTime < .001, which makes that test environment invalid
            // for release velocities. Pace only the batch harness to approximately 90 Hz.
            if (Application.isBatchMode) System.Threading.Thread.Sleep(11);
            if (body && rightController && Time.realtimeSinceStartup >= nextSample)
            {
                nextSample = Time.realtimeSinceStartup + .1f;
                report.samples.Add(new Sample { cycle = cycleNumber, frame = Time.frameCount, phase = phase,
                    time = Time.realtimeSinceStartup, deltaTime = Time.deltaTime, selected = helmet.isSelected,
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
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        static IEnumerator Validate()
        {
            yield return Wait(2f);
            var root = GameObject.Find("HelmetExhibit");
            Require(root != null, "helmet exists", "Scene object HelmetExhibit must exist.");
            helmet = root.GetComponent<XRGrabInteractable>();
            body = root.GetComponent<Rigidbody>();
            reset = root.GetComponent<HelmetExhibitReset>();
            Require(helmet && body && reset, "runtime components", "XRGrabInteractable, Rigidbody and HelmetExhibitReset are on the root.");
            Require(!body.isKinematic && body.useGravity, "physical body", "Dynamic Rigidbody uses gravity.");
            Require(Mathf.Abs(body.mass - 1.5f) < .01f, "body mass", "Prototype mass is 1.5 kg.");
            Require(helmet.movementType == XRBaseInteractable.MovementType.VelocityTracking && helmet.useDynamicAttach,
                "grab configuration", "VelocityTracking with dynamic attachment.");
            helmetColliders = root.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
            report.colliderCount = helmetColliders.Length;
            Require(helmetColliders.Length > 1 && helmetColliders.All(c => c is BoxCollider || c is SphereCollider || c is CapsuleCollider),
                "compound colliders", report.colliderCount + " active primitive colliders.");
            homePosition = body.position;
            homeRotation = body.rotation;
            var simulators = UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>();
            report.officialSimulatorWasPresent = simulators.Length > 0;
            Require(report.officialSimulatorWasPresent, "official simulator instantiated", "XR Device Simulator is automatically instantiated in Play Mode.");
            foreach (var simulator in simulators) simulator.enabled = false;
            originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            changedInputSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // The pre-scene keyboard can already have been disabled by the unfocused batch
            // window. Changing BackgroundBehavior does not re-enable that existing device.
            InputSystem.EnableDevice(keyboard);
            if (mouse != null && addedMouse) InputSystem.EnableDevice(mouse);
            Require(keyboard.added && keyboard.enabled, "synthetic keyboard enabled", "Home events reach the player's input update in the unfocused batch editor.");
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).FirstOrDefault(c => c.name == "Right Controller");
            Require(rightController != null, "right action controller", "Existing XR rig Right Controller.");
            Require(rightController.GetComponentInChildren<XRDirectInteractor>(true) != null, "right direct interactor", "Existing direct interactor is present; tracked input will activate it.");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            hmd = InputSystem.AddDevice<XRSimulatedHMD>();
            if (keyboard == null) { keyboard = InputSystem.AddDevice<Keyboard>(); addedKeyboard = true; }
            QueueHead();
            QueueController(leftDevice, new Vector3(-.6f, 1.1f, 1f), Quaternion.identity, false);
            QueueRight(new Vector3(.6f, 1.1f, 1f), Quaternion.identity, false);
            yield return Wait(.5f);
            Require(rightController.selectAction.action != null && rightController.selectAction.action.enabled,
                "select input action", "Existing select action is enabled.");

            for (var cycleIndex = 0; cycleIndex < (SessionState.GetBool(ProbeKey, false) ? 0 : 10); cycleIndex++)
            {
                Debug.Log("Helmet QA: grip/lift/rotate/return/release cycle " + (cycleIndex + 1));
                var record = new Cycle { number = cycleIndex + 1 };
                cycleNumber = record.number;
                phase = "touch";
                var before = body.position;
                var beforeRotation = body.rotation;
                var resetsBeforeCycle = reset.ResetCount;
                // A collider centre ensures a real overlap with the direct interactor sphere.
                var touch = helmetColliders.OrderByDescending(c => c.bounds.size.sqrMagnitude).First().bounds.center;
                QueueRight(touch, Quaternion.identity, false);
                yield return Wait(.25f);
                QueueRight(touch, Quaternion.identity, true);
                yield return Wait(.3f);
                Require(helmet.isSelected, "cycle " + record.number + " input grab",
                    "Queued right-controller grip selects the exhibit; hand=" + rightController.transform.position + "; target=" + touch);
                record.interactor = helmet.interactorsSelecting[0].transform.name + " / " + helmet.interactorsSelecting[0].GetType().Name;
                Require(helmet.interactorsSelecting[0] is XRDirectInteractor, "cycle " + record.number + " direct grab", record.interactor);
                var delta = new Vector3(.12f, .32f, -.12f);
                var turn = Quaternion.Euler(8f, 32f, 8f);
                phase = "lift";
                yield return MoveHand(touch, touch + delta, Quaternion.identity, turn, .45f, true);
                yield return Wait(.3f);
                record.liftMeters = body.position.y - before.y;
                record.rotationDegrees = Quaternion.Angle(beforeRotation, body.rotation);
                Require(helmet.isSelected && record.liftMeters > .20f && record.rotationDegrees > 20f,
                    "cycle " + record.number + " follows pose", "Lift=" + record.liftMeters.ToString("F3") + " m; turn=" + record.rotationDegrees.ToString("F1") + " deg.");
                phase = "return";
                yield return MoveHand(touch + delta, touch, turn, Quaternion.identity, .55f, true);
                yield return Wait(.4f);
                phase = "release";
                QueueRight(touch, Quaternion.identity, false);
                yield return Wait(.2f);
                Require(!helmet.isSelected, "cycle " + record.number + " input release", "Releasing grip ends XRI selection.");
                QueueRight(touch + new Vector3(.65f, .3f, -.4f), Quaternion.identity, false);
                phase = "settle";
                yield return Wait(1.25f);
                record.returnErrorMeters = Vector3.Distance(body.position, before);
                record.settlingSpeed = body.velocity.magnitude;
                record.settlingAngularSpeed = body.angularVelocity.magnitude;
                record.deepestPenetrationMeters = DeepestStaticPenetration();
                var settled = body.position;
                yield return Wait(.2f);
                record.restingJitterMeters = Vector3.Distance(settled, body.position);
                record.automaticResets = reset.ResetCount - resetsBeforeCycle;
                report.cycles.Add(record);
                Require(record.automaticResets == 0, "cycle " + record.number + " no recovery masked a drop", "Automatic reset count=" + record.automaticResets);
                Require(record.returnErrorMeters < .10f && record.settlingSpeed < .15f && record.settlingAngularSpeed < .6f && record.restingJitterMeters < .006f,
                    "cycle " + record.number + " stable placement", JsonUtility.ToJson(record));
                Require(record.deepestPenetrationMeters < .006f, "cycle " + record.number + " no deep penetration",
                    "Maximum static-collider overlap=" + record.deepestPenetrationMeters.ToString("F4") + " m (tolerance 0.006 m).");
                report.completedCycles++;
            }

            // Reset while the grip is held must release the helmet without immediately
            // selecting it again, even when the same hand still overlaps the home pose.
            var resetTouch = helmetColliders.OrderByDescending(c => c.bounds.size.sqrMagnitude).First().bounds.center;
            phase = "held reset";
            QueueRight(resetTouch, Quaternion.identity, false);
            yield return Wait(.25f);
            QueueRight(resetTouch, Quaternion.identity, true);
            yield return Wait(.25f);
            Require(helmet.isSelected, "grab before held reset", "Grip input selects the exhibit before reset.");
            var previousCount = reset.ResetCount;
            QueueKeyboard(new KeyboardState(Key.Home));
            yield return Wait(.1f);
            QueueKeyboard(new KeyboardState());
            yield return Wait(.4f);
            Require(reset.ResetCount == previousCount + 1 && !helmet.isSelected && Vector3.Distance(body.position, homePosition) < .06f,
                "Home while held", "Reset delta=" + (reset.ResetCount - previousCount) + "; selected=" + helmet.isSelected + "; error=" + Vector3.Distance(body.position, homePosition));
            QueueRight(resetTouch, Quaternion.identity, false);
            yield return Wait(.25f);
            QueueRight(resetTouch, Quaternion.identity, true);
            yield return Wait(.25f);
            Require(helmet.isSelected, "re-grab after releasing reset grip", "A new grip press works after reset.");
            QueueRight(resetTouch, Quaternion.identity, false);
            yield return Wait(.2f);
            QueueRight(resetTouch + new Vector3(.65f, .3f, -.4f), Quaternion.identity, false);
            yield return Wait(.5f);

            previousCount = reset.ResetCount;
            phase = "free reset";
            body.position = homePosition + new Vector3(.55f, .2f, -.7f);
            body.rotation = Quaternion.Euler(20, 70, 15);
            body.velocity = Vector3.one;
            body.angularVelocity = Vector3.one;
            QueueKeyboard(new KeyboardState(Key.Home));
            yield return Wait(.1f);
            QueueKeyboard(new KeyboardState());
            yield return Wait(.8f);
            Require(reset.ResetCount == previousCount + 1 && Vector3.Distance(body.position, homePosition) < .06f && Quaternion.Angle(body.rotation, homeRotation) < 3f,
                "Home key reset", "Reset count delta=" + (reset.ResetCount - previousCount) + "; position error=" + Vector3.Distance(body.position, homePosition).ToString("F4") + " m.");
            Require(body.velocity.magnitude < .15f && body.angularVelocity.magnitude < .6f, "reset settles without stale velocity", "Linear=" + body.velocity.magnitude.ToString("F3") + "; angular=" + body.angularVelocity.magnitude.ToString("F3"));
            phase = "reset button";
            var button = GameObject.Find("Helmet Reset Button").GetComponent<XRSimpleInteractable>();
            var buttonTouch = button.GetComponent<Collider>().bounds.center;
            previousCount = reset.ResetCount;
            body.position = homePosition + new Vector3(.1f, .2f, .1f);
            QueueRight(buttonTouch, Quaternion.identity, false);
            yield return Wait(.25f);
            QueueRight(buttonTouch, Quaternion.identity, true);
            yield return Wait(.3f);
            Require(button.isSelected && reset.ResetCount == previousCount + 1 && Vector3.Distance(body.position, homePosition) < .06f,
                "physical RESET button", "A controller grip selects the scene button and invokes its persistent reset event.");
            QueueRight(buttonTouch + Vector3.right, Quaternion.identity, false);
            yield return Wait(.3f);
            phase = "fall recovery";
            previousCount = reset.ResetCount;
            body.position = homePosition + Vector3.down * 6;
            body.velocity = Vector3.down;
            yield return Wait(1.3f);
            Require(reset.ResetCount == previousCount + 1 && Vector3.Distance(body.position, homePosition) < .06f,
                "fall recovery", "Object below fall threshold returns to display automatically.");
            yield return Wait(.3f);
            Require(report.errors.Count == 0, "no runtime errors", string.Join("\n", report.errors));
        }

        static void QueueRight(Vector3 world, Quaternion rotation, bool grip) => QueueController(rightDevice, world, rotation, grip);

        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip)
        {
            var parent = rightController.transform.parent;
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
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || phase != "held reset" || report.inputDiagnostics.Count >= 80) return;
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
            var path = Path.Combine(OutputDirectory, SessionState.GetBool(ProbeKey, false) ? "input-probe.json" : "playmode-validation.json");
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
