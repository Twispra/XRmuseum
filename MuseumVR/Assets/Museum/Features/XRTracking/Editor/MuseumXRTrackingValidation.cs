#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using Unity.XR.CoreUtils;

namespace Museum.XRTracking.EditorTools
{
    /// <summary>Saved-scene synthetic input regression. Never saves scenes or claims physical headset acceptance.</summary>
    [InitializeOnLoad]
    public static class MuseumXRTrackingValidation
    {
        const string Prefix = "Museum.XRTrackingQA.", ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static IEnumerator routine;
        static Report report;
        static XRHMD fixture;
        static Keyboard addedKeyboard;
        static Mouse addedMouse;
        static XROrigin rig;
        static Vector3 oldOffset, originPosition;
        static Quaternion originRotation;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static bool changedInput, changedOffset, finishing;
        static int lastFrame = -1, pendingExit = -1;
        static double started, exitAt;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => SessionState.GetString(Prefix + "Output", "");
        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Sample { public string name; public Vector3 expected, localPosition, rigRelativePosition, offset; public Quaternion localRotation; }
        [Serializable] sealed class Report
        {
            public string status = "running", failure = "", utc, commit, branch, dirty, unity, environment, sceneSha256;
            public string method = "Saved MuseumStart; generic non-XRSimulated XRHMD fixture added normally, then QueueDeltaStateEvent in Dynamic input updates through production head actions and TrackedPoseDriver.";
            public string limits = "Synthetic physical-class fixture only, not real Quest 3 / Virtual Desktop acceptance. Floor/device offsets are temporary pose fixtures; runtime floor calibration, OS simulator controls, frame submission, comfort and executable builds are not validated.";
            public float durationSeconds;
            public List<Check> checks = new List<Check>();
            public List<Sample> samples = new List<Sample>();
            public List<string> errors = new List<string>();
        }
        static MuseumXRTrackingValidation() { EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick; }
        [MenuItem("Museum/XR/Validate headset tracking (synthetic Play Mode)")]
        public static void RunInEditor() => Begin(false);
        // Omit -quit; this entry point exits only after Play Mode checks and report writing.
        public static void RunBatch() => Begin(true);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareDesktopInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            // Batch mode has no OS devices; the official simulator help UI reads these controls in Start.
            if (Keyboard.current == null) addedKeyboard = InputSystem.AddDevice<Keyboard>("XR tracking QA keyboard");
            if (Mouse.current == null) addedMouse = InputSystem.AddDevice<Mouse>("XR tracking QA mouse");
        }
        static void Begin(bool batch)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before XR tracking validation.");
            if (!batch && SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene changes before validation; no scene is saved by this validator.");
            var provenance = new Report { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), branch = Git("branch --show-current"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion,
                environment = SystemInfo.operatingSystem + "; " + SystemInfo.processorType + "; " + SystemInfo.graphicsDeviceName, sceneSha256 = Hash(ScenePath) };
            string output = Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-xr-tracking-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output); SessionState.SetString(Prefix + "Output", output); SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(provenance));
            SessionState.SetBool(Prefix + "Active", true); SessionState.SetBool(Prefix + "Batch", batch); SessionState.SetInt(Prefix + "Exit", -1);
            EditorSceneManager.OpenScene(ScenePath); EditorApplication.EnterPlaymode();
        }
        static void Mode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}")); started = EditorApplication.timeSinceStartup;
                finishing = false; lastFrame = -1; Application.logMessageReceived += Log; InputSystem.onBeforeUpdate += Flush; routine = Validate();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && !finishing && report != null) Finish("Play Mode exited before validation completed.", false);
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Prefix + "Active", false);
                if (SessionState.GetBool(Prefix + "Batch", false)) { int code = SessionState.GetInt(Prefix + "Exit", 1); pendingExit = code < 0 ? 1 : code; exitAt = EditorApplication.timeSinceStartup + 20; }
            }
        }
        static void Tick()
        {
            if (pendingExit >= 0 && EditorApplication.timeSinceStartup >= exitAt) { int code = pendingExit; pendingExit = -1; EditorApplication.Exit(code); return; }
            if (routine == null || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try { if (EditorApplication.timeSinceStartup - started > 90) throw new TimeoutException("XR tracking validation exceeded 90 seconds."); if (!routine.MoveNext()) Finish(null); }
            catch (Exception ex) { Finish(ex.ToString()); }
        }
        static IEnumerator Validate()
        {
            yield return null; yield return null; yield return null;
            var displays = new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(displays);
            Require(!displays.Any(d => d.running) && !InputSystem.devices.Any(d => d is XRHMD && !(d is XRSimulatedHMD)), "synthetic test has no connected physical XR session", "Run without a headset; actual hardware is never removed by this test.");
            Require(SceneManager.GetActiveScene().path == ScenePath, "saved main scene loaded", SceneManager.GetActiveScene().path);
            rig = UnityEngine.Object.FindObjectOfType<XROrigin>(); Require(rig && rig.Camera && rig.CameraFloorOffsetObject, "production XR origin and camera exist");
            var simulators = UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>();
            Require(simulators.Length == 1 && !simulators[0].removeOtherHMDDevices, "no-hardware desktop simulator is instantiated safely", "Count=" + simulators.Length);
            Require(InputSystem.devices.OfType<XRSimulatedHMD>().Count() == 1, "official simulator owns one simulated HMD");
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedInput = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            fixture = InputSystem.AddDevice<XRHMD>("XR tracking QA physical-class fixture");
            Require(fixture.added, "late hardware-class fixture survives simulator device-change callbacks");
            Require(NoSimulatedDevices(), "late hardware synchronously releases simulated HMD and controllers");
            yield return null; yield return null;
            Require(NoSimulation(), "late hardware removes the inactive owned simulator hierarchy");
            var driver = rig.Camera.GetComponent<TrackedPoseDriver>(); Require(driver, "saved camera uses the production TrackedPoseDriver");
            foreach (var input in new[] { driver.positionInput, driver.rotationInput, driver.trackingStateInput })
                Require(input.reference != null && input.action != null && input.action.enabled && input.action.controls.Any(c => c.device == fixture), "saved head action resolves and enables physical-class fixture", input.reference ? input.reference.name : "missing reference");
            InputSystem.EnableDevice(fixture); oldOffset = rig.CameraFloorOffsetObject.transform.localPosition; changedOffset = true;
            originPosition = rig.Origin.transform.position; originRotation = rig.Origin.transform.rotation;
            rig.CameraFloorOffsetObject.transform.localPosition = new Vector3(oldOffset.x, 0, oldOffset.z);
            QueuePose(new Vector3(.2f, 1.7f, -.15f), Quaternion.Euler(12, 35, 0)); yield return null; yield return null; yield return null;
            PoseCheck("floor standing: tracked height is not added twice", new Vector3(.2f, 1.7f, -.15f), Quaternion.Euler(12, 35, 0), 1.7f);
            QueuePose(new Vector3(-.1f, 1.1f, .25f), Quaternion.Euler(-18, -50, 6)); yield return null; yield return null; yield return null;
            PoseCheck("floor crouching: camera follows physical-class head translation and rotation", new Vector3(-.1f, 1.1f, .25f), Quaternion.Euler(-18, -50, 6), 1.1f);
            rig.CameraFloorOffsetObject.transform.localPosition = new Vector3(oldOffset.x, 1.65f, oldOffset.z);
            QueuePose(Vector3.zero, Quaternion.identity); yield return null; yield return null; yield return null;
            PoseCheck("device-origin fixture: zero local head height uses one 1.65m offset", Vector3.zero, Quaternion.identity, 1.65f);
            rig.CameraFloorOffsetObject.transform.localPosition = oldOffset; changedOffset = false;
            InputSystem.RemoveDevice(fixture); yield return null; yield return null; yield return null;
            Require(NoSimulation(), "disconnect does not recreate desktop simulation during the same session");
            fixture = InputSystem.AddDevice<XRHMD>("XR tracking QA startup fixture");
            foreach (var gate in UnityEngine.Object.FindObjectsOfType<MuseumXRSimulatorGate>(true)) UnityEngine.Object.DestroyImmediate(gate.gameObject);
            var initialize = typeof(MuseumXRSimulatorGate).GetMethod("Initialize", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Require(initialize != null, "gate exposes runtime initialization callback"); initialize.Invoke(null, null);
            yield return null; yield return null;
            Require(fixture.added && NoSimulation(), "hardware-class fixture present at initialization bypasses simulation");
            Require(UnityEngine.Object.FindObjectsOfType<MuseumXRSimulatorGate>(true).Length == 1, "runtime owns one transient simulator gate");
        }
        static bool NoSimulatedDevices() => !InputSystem.devices.Any(d => d is XRSimulatedHMD || d is XRSimulatedController);
        static bool NoSimulation() => NoSimulatedDevices() && UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>(true).Length == 0;
        static void QueuePose(Vector3 position, Quaternion rotation) => PendingInput.Enqueue(() =>
        {
            InputSystem.QueueDeltaStateEvent(fixture.isTracked, (byte)1); InputSystem.QueueDeltaStateEvent(fixture.trackingState, 3);
            InputSystem.QueueDeltaStateEvent(fixture.centerEyePosition, position); InputSystem.QueueDeltaStateEvent(fixture.centerEyeRotation, rotation);
            InputSystem.QueueDeltaStateEvent(fixture.devicePosition, position); InputSystem.QueueDeltaStateEvent(fixture.deviceRotation, rotation);
        });
        static void Flush() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static void PoseCheck(string name, Vector3 expected, Quaternion rotation, float height)
        {
            Transform camera = rig.Camera.transform; Vector3 relative = rig.Origin.transform.InverseTransformPoint(camera.position);
            report.samples.Add(new Sample { name = name, expected = expected, localPosition = camera.localPosition, rigRelativePosition = relative, localRotation = camera.localRotation, offset = rig.CameraFloorOffsetObject.transform.localPosition });
            Require(Vector3.Distance(camera.localPosition, expected) < .005f && Quaternion.Angle(camera.localRotation, rotation) < .5f && Mathf.Abs(relative.y - height) < .005f, name, "local=" + camera.localPosition + "; rig-relative height=" + relative.y + "; angle error=" + Quaternion.Angle(camera.localRotation, rotation));
            Require(Vector3.Distance(rig.Origin.transform.position, originPosition) < .0001f && Quaternion.Angle(rig.Origin.transform.rotation, originRotation) < .01f, "head movement leaves rig origin unchanged / " + name);
        }
        static void Require(bool passed, string name, string detail = "") { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); if (!passed) throw new InvalidOperationException(name + ": " + detail); }
        static void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + message + "\n" + stack); }
        static void Finish(string failure, bool exitPlay = true)
        {
            if (finishing) return; finishing = true; routine = null; Application.logMessageReceived -= Log; InputSystem.onBeforeUpdate -= Flush; PendingInput.Clear();
            if (fixture != null && fixture.added) InputSystem.RemoveDevice(fixture);
            if (addedKeyboard != null && addedKeyboard.added) InputSystem.RemoveDevice(addedKeyboard);
            if (addedMouse != null && addedMouse.added) InputSystem.RemoveDevice(addedMouse);
            if (changedOffset && rig && rig.CameraFloorOffsetObject) rig.CameraFloorOffsetObject.transform.localPosition = oldOffset;
            if (changedInput) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; }
            bool unchanged = Hash(ScenePath) == report.sceneSha256; report.checks.Add(new Check { name = "saved scene is unchanged", passed = unchanged, detail = Hash(ScenePath) });
            report.failure = failure ?? ""; report.durationSeconds = (float)(EditorApplication.timeSinceStartup - started);
            report.status = failure == null && report.errors.Count == 0 && report.checks.All(c => c.passed) ? "passed" : "failed";
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false)); SessionState.SetInt(Prefix + "Exit", report.status == "passed" ? 0 : 1);
            Debug.Log("XR_TRACKING_QA_" + report.status.ToUpperInvariant() + " " + Path.Combine(Output, "report.json")); if (exitPlay) EditorApplication.ExitPlaymode();
        }
        static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static string Git(string arguments)
        {
            try
            {
                using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments) { WorkingDirectory = RepoRoot, UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true }))
                { var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); if (!process.WaitForExit(5000)) { process.Kill(); return "UNAVAILABLE: git timeout"; } return process.ExitCode == 0 ? stdout.Result.Trim() : "UNAVAILABLE: " + stderr.Result.Trim(); }
            }
            catch (Exception ex) { return "UNAVAILABLE: " + ex.Message; }
        }
    }
}
#endif
