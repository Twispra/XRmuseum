#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Museum.Inspection;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Exploration.Editor
{
    /// <summary>Focused recovery checks; never generates or saves product assets.</summary>
    [InitializeOnLoad]
    public static class VisitorViewRecoveryValidation
    {
        const string Prefix = "Museum.ViewRecoveryQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static HelmetExplorationPanel panel;
        static HelmetInspectionPalette palette;
        static HelmetExplorationController exploration;
        static ActionBasedController rightController, leftController;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD hmd;
        static Keyboard keyboard;
        static XROrigin origin;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static bool changedSettings, finishing;
        static double started, exitAt;
        static int lastFrame = -1, pendingExit = -1;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", SessionState.GetString(Prefix + "Run", "missing"));

        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class Report
        {
            public string utc, finishedUtc, commit, dirty, unity, failure;
            public string method = "Edit Mode public-API height fixtures; saved MuseumStart queued synthetic HMD/controller/keyboard states in normal Dynamic input updates through production Input Actions and XR ray selection. Late-camera fixture clones only the Play Mode palette. No manual SelectEnter, HoverEnter, TriggerProgrammatically or InputSystem.Update calls.";
            public string limitations = "No real headset/controller, comfort, learning study or executable build verification. Floor/Device pose fixtures are labelled separately from main-scene synthetic input. Input injection refuses a real InputSystem XRHMD. Product scene, prefab and input assets are never saved.";
            public List<Check> checks = new List<Check>();
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<string> errors = new List<string>();
        }
        [Serializable] sealed class Visit
        {
            public bool air, voice, selected, exploring, reading, floating, cutaway;
            public int hints, page, clue, reset, era;
            public Vector3 helmetPosition;
            public Quaternion helmetRotation;
        }

        static VisitorViewRecoveryValidation()
        { EditorApplication.playModeStateChanged += OnPlayMode; EditorApplication.update += Tick; }
        [MenuItem("Museum/Exploration/Validate visitor view recovery")]
        public static void RunInEditor() => Begin();
        public static void RunBatch() => Begin();
        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetString(Prefix + "Run", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-view-recovery-" + Guid.NewGuid().ToString("N"));
            report = new Report { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion };
            foreach (string path in AssetDatabase.GetDependencies(ScenePath, true).Concat(new[] {
                "Assets/Museum/Features/HelmetExploration/Runtime/VisitorViewRecovery.cs",
                "Assets/Museum/Features/HelmetExploration/Runtime/HelmetExplorationPanel.cs",
                "Assets/Museum/Features/HelmetExploration/Editor/VisitorViewRecoveryValidation.cs",
                "Packages/manifest.json", "Packages/packages-lock.json" }).Distinct().Where(File.Exists).OrderBy(p => p))
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                    report.files.Add(new Fingerprint { path = path, sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() });
            HeightFixtures();
            SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
            SessionState.SetBool(Prefix + "Active", true);
            EditorApplication.EnterPlaymode();
        }
        static void HeightFixtures()
        {
            foreach (float height in new[] { .85f, 1.20f, 1.65f, 1.80f })
                HeightFixture("Floor " + height, height, 0, 1, 0, XROrigin.TrackingOriginMode.Floor);
            HeightFixture("Floor on elevated platform", 1.20f, 3, 1, 0, XROrigin.TrackingOriginMode.Floor);
            HeightFixture("Floor scaled origin", 1.20f, 0, 2, 0, XROrigin.TrackingOriginMode.Floor);
            HeightFixture("Device with downward pose", -.25f, 0, 1, 1.65f, XROrigin.TrackingOriginMode.Device);
        }
        static void HeightFixture(string name, float height, float floor, float scale, float offsetHeight, XROrigin.TrackingOriginMode mode)
        {
            var root = new GameObject("Recovery QA fixture"); root.SetActive(false);
            try
            {
                root.transform.position = new Vector3(0, floor, 2.2f); root.transform.localScale = Vector3.one * scale;
                var offset = new GameObject("Tracking offset"); offset.transform.SetParent(root.transform, false);
                var cameraObject = new GameObject("Tracked pose"); cameraObject.transform.SetParent(offset.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                var fixtureOrigin = root.AddComponent<XROrigin>(); fixtureOrigin.Origin = root; fixtureOrigin.Camera = camera;
                fixtureOrigin.CameraFloorOffsetObject = offset; fixtureOrigin.CameraYOffset = 1.65f; fixtureOrigin.RequestedTrackingOriginMode = mode;
                offset.transform.localPosition = new Vector3(0, offsetHeight, 0);
                camera.transform.localPosition = new Vector3(.25f, height, -.18f);
                camera.transform.localRotation = Quaternion.Euler(15, 20, 0);
                var recovery = root.AddComponent<VisitorViewRecovery>(); recovery.Configure(fixtureOrigin, null);
                root.transform.position += new Vector3(.8f, .3f, -1f); root.transform.rotation = Quaternion.Euler(0, 90, 0);
                float expectedEyeY = floor + (height + offsetHeight) * scale;
                CheckResult(recovery.ResetView(), name + ": reset succeeds", "Public API pose fixture.");
                CheckResult(Mathf.Abs(root.transform.position.y - floor) < .0001f, name + ": initial floor restored", "origin Y=" + root.transform.position.y + "; expected=" + floor);
                CheckResult(Mathf.Abs(camera.transform.position.y - expectedEyeY) < .0001f, name + ": tracked eye height retained", "camera Y=" + camera.transform.position.y + "; expected=" + expectedEyeY);
                CheckResult(Vector2.Distance(new Vector2(camera.transform.position.x, camera.transform.position.z), new Vector2(0, 2.2f)) < .0001f,
                    name + ": horizontal camera position restored", camera.transform.position.ToString("F4"));
                CheckResult(Vector3.Angle(Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up), Vector3.forward) < .01f,
                    name + ": horizontal heading restored", camera.transform.forward.ToString("F4"));
                var position = root.transform.position; var rotation = root.transform.rotation;
                CheckResult(recovery.ResetView() && Vector3.Distance(position, root.transform.position) < .0001f && Quaternion.Angle(rotation, root.transform.rotation) < .01f,
                    name + ": repeated reset stable", "Second call must not add another offset.");
                if (name == "Floor 0.85")
                {
                    camera.transform.localPosition = new Vector3(.25f, 1.65f, -.18f);
                    CheckResult(Mathf.Abs(camera.transform.position.y - 1.65f) < .0001f, "Crouched reset then stand retains standing height", "camera Y=" + camera.transform.position.y);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
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
                if (Application.isBatchMode) { pendingExit = SessionState.GetInt(Prefix + "Exit", 1); exitAt = EditorApplication.timeSinceStartup + 20; }
            }
        }
        static void Tick()
        {
            if (pendingExit >= 0 && EditorApplication.timeSinceStartup >= exitAt) { int code = pendingExit; pendingExit = -1; EditorApplication.Exit(code); return; }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 90) { Finish("Validation exceeded 90 seconds."); return; }
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
            if (InputSystem.devices.OfType<XRHMD>().Any(d => !(d is XRSimulatedHMD)))
                throw new InvalidOperationException("Real HMD found; refusing synthetic input.");
            foreach (var simulator in UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>()) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            panel = UnityEngine.Object.FindObjectOfType<HelmetExplorationPanel>(); palette = panel.GetComponent<HelmetInspectionPalette>(); exploration = panel.Exploration;
            origin = Camera.main.GetComponentInParent<XROrigin>();
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>().Single(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>().Single(c => c.name == "Left Controller");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            hmd = InputSystem.AddDevice<XRSimulatedHMD>(); keyboard = InputSystem.AddDevice<Keyboard>();
            QueueHead(); ParkHands(); yield return Wait(.5f);
            exploration.SelectClue(HelmetExplorationController.Clue.Air); exploration.RequestHint(); exploration.ShowReview(); panel.ShowExploration();
            var before = Snapshot();
            var floorY = origin.Origin.transform.position.y;
            origin.Origin.transform.position += new Vector3(.8f, 0, -.5f); origin.Origin.transform.rotation = Quaternion.Euler(0, 35, 0);
            palette.RecenterNow(); yield return Wait(.3f);
            yield return PressCenter(); yield return Wait(.3f);
            CheckResult(JsonUtility.ToJson(before) == JsonUtility.ToJson(Snapshot()), "Ray reset preserves nonempty visit and exhibit state", JsonUtility.ToJson(Snapshot()));
            CheckResult(Mathf.Abs(origin.Origin.transform.position.y - floorY) < .005f, "Main scene floor stable after Update and physics", "origin Y=" + origin.Origin.transform.position.y + "; before=" + floorY);
            CheckResult(Vector2.Distance(new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z), new Vector2(0, 2.2f)) < .01f,
                "Ray selection restores main camera XZ", Camera.main.transform.position.ToString("F4"));
            origin.Origin.transform.position += new Vector3(.4f, 0, -.4f);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F11))); yield return Wait(.1f);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState())); yield return Wait(.3f);
            CheckResult(JsonUtility.ToJson(before) == JsonUtility.ToJson(Snapshot()), "F11 preserves visit and exhibit state", JsonUtility.ToJson(Snapshot()));
            CheckResult(Vector2.Distance(new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z), new Vector2(0, 2.2f)) < .01f,
                "F11 restores main camera XZ", Camera.main.transform.position.ToString("F4"));
            yield return LateCameraFixture();
        }
        static Visit Snapshot()
        {
            var inspect = palette.Controller; var era = UnityEngine.Object.FindObjectOfType<Museum.Eras.HelmetEraSwitcher>();
            return new Visit { air = exploration.AirSeen, voice = exploration.VoiceSeen, selected = exploration.HasSelectedClue, hints = exploration.HintRequests,
                page = (int)exploration.CurrentPage, clue = (int)exploration.SelectedClue, exploring = panel.IsExploring, reading = panel.IsReading,
                reset = inspect.ResetCount, era = era ? era.CurrentIndex : -1, floating = inspect.IsFloating, cutaway = inspect.IsCutaway,
                helmetPosition = inspect.transform.position, helmetRotation = inspect.transform.rotation };
        }
        static IEnumerator LateCameraFixture()
        {
            var savedPanel = panel; var savedPalette = palette; var camera = origin.Camera; string tag = camera.tag;
            var inactiveParent = new GameObject("Late camera QA"); inactiveParent.SetActive(false);
            var copy = UnityEngine.Object.Instantiate(palette.gameObject, inactiveParent.transform);
            foreach (var child in copy.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Exploration CENTER" || t.name == "Exploration CENTER label").ToArray()) UnityEngine.Object.Destroy(child.gameObject);
            var recovery = copy.GetComponent<VisitorViewRecovery>(); if (recovery) UnityEngine.Object.Destroy(recovery);
            var clonePalette = copy.GetComponent<HelmetInspectionPalette>(); clonePalette.Viewer = camera.transform;
            try
            {
                savedPalette.gameObject.SetActive(false); camera.tag = "Untagged"; yield return Wait(.1f);
                inactiveParent.SetActive(true); yield return Wait(.3f);
                CheckResult(Camera.main == null && !copy.GetComponentsInChildren<HelmetExplorationButton>(true).Any(b => b.Command == HelmetExplorationButton.Action.CenterView),
                    "Late-camera fixture starts after palette Start with no recovery button", "The clone is a Play Mode instance only.");
                camera.tag = tag; yield return Wait(.3f);
                panel = copy.GetComponent<HelmetExplorationPanel>(); palette = clonePalette;
                var center = Center();
                var controls = (XRBaseInteractable[])typeof(HelmetInspectionPalette).GetField("controls", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(palette);
                CheckResult(controls.Contains(center), "Late-created CENTER included in palette hover cache", "Cached controls=" + controls.Length);
                yield return PressCenter();
            }
            finally
            {
                camera.tag = tag; panel = savedPanel; palette = savedPalette;
                UnityEngine.Object.Destroy(inactiveParent); savedPalette.gameObject.SetActive(true); ParkHands();
            }
        }
        static XRSimpleInteractable Center() => panel.GetComponentsInChildren<HelmetExplorationButton>(true).Single(b => b.Command == HelmetExplorationButton.Action.CenterView).GetComponent<XRSimpleInteractable>();
        static IEnumerator PressCenter()
        {
            var center = Center(); var collider = center.GetComponent<Collider>(); int events = 0; string interactor = "";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; interactor = args.interactorObject.GetType().Name; };
            center.selectEntered.AddListener(listener);
            try
            {
                var touch = collider.bounds.center - center.transform.forward * .55f;
                var rotation = Quaternion.LookRotation(collider.bounds.center - touch, Vector3.up);
                QueueController(rightDevice, touch, rotation, false); yield return Wait(.3f);
                CheckResult(center.isHovered, "CENTER ray hover", "Hovered=" + center.isHovered);
                CheckResult(palette.IsFrozen, "CENTER ray hover freezes palette", "Frozen=" + palette.IsFrozen);
                touch = collider.bounds.center - center.transform.forward * .55f; rotation = Quaternion.LookRotation(collider.bounds.center - touch, Vector3.up);
                QueueController(rightDevice, touch, rotation, true); yield return Wait(.25f);
                CheckResult(events == 1 && interactor == nameof(XRRayInteractor), "CENTER selected once through production ray", "Events=" + events + "; interactor=" + interactor);
                QueueController(rightDevice, touch, rotation, false); yield return Wait(.2f); ParkHands(); yield return Wait(.15f);
            }
            finally { center.selectEntered.RemoveListener(listener); }
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
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, hmd, keyboard }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedSettings) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; changedSettings = false; }
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
            bool passed = failure == null && report.errors.Count == 0 && report.checks.All(c => c.passed);
            SessionState.SetInt(Prefix + "Exit", passed ? 0 : 1);
            Debug.Log("Visitor view recovery QA: " + (passed ? "PASS" : "FAIL") + "; checks=" + report.checks.Count + "; output=" + Output);
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
