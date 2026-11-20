using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.Diegetic;
using Museum.Eras;
using Museum.Exploration;
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
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Navigation.Editor
{
    [InitializeOnLoad]
    public static class GalleryNavigationValidation
    {
        const string Key = "Museum.GalleryNavigation.QA";
        static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        static readonly Queue<Action> input = new Queue<Action>();
        static GalleryNavigator nav;
        static HelmetInspectionController inspection;
        static HelmetExplorationController exploration;
        static XRSimulatedController left, right;
        static XRSimulatedHMD hmd;
        static Keyboard keyboard;
        static Mouse addedMouse;
        static ActionBasedController leftController, rightController;
        static InputSettings.BackgroundBehavior previousBackground;
        static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        static Report report;
        static bool settingsChanged, finishing;
        static int lastFrame = -1, exitCode = -1;
        static double started, exitAt;
        static string Folder => SessionState.GetString(Key + ".Output", "");

        [Serializable] sealed class CheckResult { public string name; public bool passed; }
        [Serializable] sealed class Report
        {
            public string commit, dirty, unity, scene, status, failure, method =
                "Saved scene runtime bootstrap; public pose fixtures plus queued synthetic HMD and left/right controller states through production Input Actions and XR rays. No manual HoverEnter/SelectEnter, TriggerProgrammatically or InputSystem.Update. Diagnostic monocular captures.";
            public string limits = "No real headset/controller, stereo readability, comfort, performance, Player build or user study validation.";
            public Vector3 initialCameraPosition;
            public float syntheticTrackedHeight;
            public List<CheckResult> checks = new List<CheckResult>();
            public List<string> errors = new List<string>();
        }

        static GalleryNavigationValidation()
        { EditorApplication.playModeStateChanged += OnMode; EditorApplication.update += Tick; }

        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save other scene changes first.");
            string folder = Path.GetFullPath(Argument("-navigationOutput"));
            if (Directory.Exists(folder)) throw new IOException("Use a new output directory: " + folder);
            Directory.CreateDirectory(folder);
            SessionState.SetString(Key + ".Output", folder);
            report = new Report { commit = Git("rev-parse HEAD"), dirty = Git("status --porcelain"), unity = Application.unityVersion,
                scene = Argument("-navigationScene", "Assets/Museum/Scenes/MuseumStart.unity"), status = "running" };
            PoseFixtures();
            EditorSceneManager.OpenScene(report.scene);
            SessionState.SetString(Key + ".Report", JsonUtility.ToJson(report));
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Key, false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("Gallery navigation QA keyboard");
            if (Mouse.current == null) addedMouse = InputSystem.AddDevice<Mouse>("Gallery navigation QA mouse");
        }
        static void OnMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Key + ".Report", ""));
                finishing = false; started = EditorApplication.timeSinceStartup; lastFrame = -1;
                Application.logMessageReceived += OnLog; InputSystem.onBeforeUpdate += FlushInput;
                routines.Clear(); input.Clear(); routines.Push(Validate());
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                exitCode = SessionState.GetInt(Key + ".Exit", 1);
                exitAt = EditorApplication.timeSinceStartup + 20;
            }
        }
        static void Tick()
        {
            if (exitCode >= 0 && EditorApplication.timeSinceStartup >= exitAt && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            { int code = exitCode; exitCode = -1; EditorApplication.Exit(code); return; }
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || finishing || report == null) return;
            if (EditorApplication.timeSinceStartup - started > 150) { Finish("Timed out after 150 seconds."); return; }
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            try
            {
                while (routines.Count > 0)
                {
                    var current = routines.Peek();
                    if (!current.MoveNext()) { routines.Pop(); continue; }
                    if (current.Current is IEnumerator nested) { routines.Push(nested); continue; }
                    return;
                }
                Finish(null);
            }
            catch (Exception ex) { Finish(ex.ToString()); }
        }

        static void PoseFixtures()
        {
            foreach (float scale in new[] { 1f, 2f })
            foreach (float height in new[] { .85f, 1.2f, 1.65f })
            {
                var root = new GameObject("Navigation public pose fixture");
                try
                {
                    root.transform.SetPositionAndRotation(new Vector3(.7f, 3, -.3f), Quaternion.Euler(0, 73, 0));
                    root.transform.localScale = Vector3.one * scale;
                    var offset = new GameObject("Tracking offset"); offset.transform.SetParent(root.transform, false);
                    var eye = new GameObject("Tracked camera"); eye.transform.SetParent(offset.transform, false);
                    eye.transform.localPosition = new Vector3(.24f, height, -.18f);
                    eye.transform.localRotation = Quaternion.Euler(18, 24, 0);
                    var camera = eye.AddComponent<Camera>();
                    var origin = root.AddComponent<XROrigin>(); origin.Camera = camera; origin.Origin = root; origin.CameraFloorOffsetObject = offset;
                    var localPosition = eye.transform.localPosition; var localRotation = eye.transform.localRotation;
                    float expectedY = 3 + height * scale;
                    var destination = new Vector3(-3.1f, 3, 1.5f);
                    string name = "Pose scale=" + scale + " eye=" + height;
                    Check(GalleryNavigator.PlaceTrackedView(origin, destination, Vector3.forward), name + ": move accepted");
                    Check(Mathf.Abs(eye.transform.position.y - expectedY) < .0001f && Mathf.Abs(root.transform.position.y - 3) < .0001f, name + ": floor and tracked height retained");
                    Check(Vector2.Distance(XZ(eye.transform.position), XZ(destination)) < .0001f, name + ": camera reaches horizontal destination");
                    Check(eye.transform.localPosition == localPosition && Quaternion.Angle(eye.transform.localRotation, localRotation) < .001f, name + ": tracked local pose untouched");
                    Check(Vector3.Angle(Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up), Vector3.forward) < .01f, name + ": horizontal heading aligned");
                    var position = root.transform.position;
                    Check(GalleryNavigator.PlaceTrackedView(origin, destination, Vector3.forward) && Vector3.Distance(position, root.transform.position) < .0001f, name + ": repeat has no drift");
                    Check(!GalleryNavigator.PlaceTrackedView(origin, new Vector3(float.NaN, 0, 0), Vector3.forward) && root.transform.position == position, name + ": invalid position rejected");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        static IEnumerator Validate()
        {
            yield return Wait(2);
            if (InputSystem.devices.OfType<XRHMD>().Any(d => !(d is XRSimulatedHMD))) throw new InvalidOperationException("Real HMD present; refusing input injection.");
            foreach (var simulator in UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>()) simulator.enabled = false;
            previousBackground = InputSystem.settings.backgroundBehavior; previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            settingsChanged = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            nav = UnityEngine.Object.FindObjectOfType<GalleryNavigator>();
            Check(nav && nav.IsReady, "Saved scene automatically installs a ready navigator");
            Check(UnityEngine.Object.FindObjectsOfType<GalleryNavigator>().Length == 1 && nav.Buttons.Count == 3, "Exactly one panel with three destinations");
            inspection = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            exploration = UnityEngine.Object.FindObjectOfType<HelmetExplorationController>();
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Left Controller");
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Right Controller");
            left = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
            right = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            hmd = InputSystem.AddDevice<XRSimulatedHMD>();
            // The saved Device-origin camera already has a floor offset. Do not add a second adult eye height.
            float trackedHeight = 1.2f - (nav.Origin.Camera.transform.parent.position.y - nav.Origin.Origin.transform.position.y);
            report.syntheticTrackedHeight = trackedHeight;
            input.Enqueue(() => InputSystem.QueueStateEvent(hmd, new XRSimulatedHMDState {
                isTracked = true, trackingState = 3, devicePosition = new Vector3(.12f, trackedHeight, -.08f), centerEyePosition = new Vector3(.12f, trackedHeight, -.08f),
                leftEyePosition = new Vector3(.088f, trackedHeight, -.08f), rightEyePosition = new Vector3(.152f, trackedHeight, -.08f),
                deviceRotation = Quaternion.identity, centerEyeRotation = Quaternion.identity, leftEyeRotation = Quaternion.identity, rightEyeRotation = Quaternion.identity }));
            ParkHands(); yield return Wait(.5f);
            report.initialCameraPosition = nav.Origin.Camera.transform.position;
            Check(Mathf.Abs(report.initialCameraPosition.y - nav.Origin.Origin.transform.position.y - 1.2f) < .001f, "Synthetic seated eye height is 1.2m above the saved floor");
            var station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
            station.ViewHeight.Raise(); station.ViewHeight.Raise(); yield return Wait(.1f);
            exploration.SelectClue(HelmetExplorationController.Clue.Air); exploration.Stop();
            exploration.SelectClue(HelmetExplorationController.Clue.Voice); exploration.Stop(); exploration.RequestHint();
            var era = UnityEngine.Object.FindObjectOfType<HelmetEraSwitcher>(); era.ShowEra(2); yield return Wait(.3f);
            MuseumText.Channel.SetLanguage(MuseumLanguage.TraditionalChinese);
            string discoveries = Discoveries();
            int resetCount = inspection.ResetCount, eraIndex = era.CurrentIndex;
            var helmetPosition = inspection.transform.position; var helmetRotation = inspection.transform.rotation;
            float floor = nav.Origin.Origin.transform.position.y, eyeHeight = nav.Origin.Camera.transform.position.y;
            var localEye = nav.Origin.Camera.transform.localPosition;
            var localRotation = nav.Origin.Camera.transform.localRotation;
            var locomotion = nav.Origin.GetComponentsInChildren<LocomotionProvider>(true).ToDictionary(p => p, p => p.enabled);

            Check(nav.Request(GalleryNavigator.Exhibit.Left), "Public API starts the left route");
            Check(!nav.Request(GalleryNavigator.Exhibit.Right), "Second request during transition is rejected");
            Check(nav.CompletedNavigations == 0 && nav.FadeOpacity == 0, "No jump at request time");
            Check(locomotion.Keys.All(p => !p.enabled), "Locomotion is suspended during fade");
            yield return Until(() => nav.CompletedNavigations == 1);
            Check(nav.FadeOpacity > .999f, "Position changes only under full black");
            Capture("transition-black", true);
            yield return Until(() => !nav.IsTransitioning);
            Check(locomotion.All(p => p.Key.enabled == p.Value), "Original locomotion states restored");
            AssertDestination(GalleryNavigator.Exhibit.Left, floor, eyeHeight, localEye, localRotation);
            yield return Press(GalleryNavigator.Exhibit.Right, right, true, false);
            AssertDestination(GalleryNavigator.Exhibit.Right, floor, eyeHeight, localEye, localRotation);
            yield return Press(GalleryNavigator.Exhibit.Main, left, false, true);
            yield return Press(GalleryNavigator.Exhibit.Left, right, true, true);
            Check(Discoveries() == discoveries && inspection.ResetCount == resetCount && era.CurrentIndex == eraIndex &&
                inspection.transform.position == helmetPosition && Quaternion.Angle(inspection.transform.rotation, helmetRotation) < .001f,
                "Navigation preserves discoveries, hint count, era, helmet pose and reset count");
            Check(MuseumText.Channel.Current == MuseumLanguage.TraditionalChinese && Mathf.Abs(station.ViewHeight.OffsetMetres - .1f) < .0001f,
                "Language and viewing-height preference retained");

            var cancelButton = nav.Buttons.Single(b => b.Destination == GalleryNavigator.Exhibit.Right);
            Aim(right, cancelButton, false, false); yield return Wait(.25f);
            int completed = nav.CompletedNavigations;
            Aim(right, cancelButton, true, false); yield return Wait(.15f);
            QueueController(right, nav.Origin.Camera.transform.position, Quaternion.LookRotation(Vector3.down), true, false); yield return Wait(.2f);
            ParkHands(); yield return Wait(.4f);
            Check(nav.CompletedNavigations == completed && !nav.IsTransitioning, "Press, move off and release cancels navigation");

            nav.TryGetDestination(GalleryNavigator.Exhibit.Right, out var blockedPlace, out _);
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "Navigation obstacle fixture";
            obstacle.transform.position = blockedPlace + Vector3.up * .8f; obstacle.transform.localScale = new Vector3(.6f, 1.5f, .6f);
            Physics.SyncTransforms();
            Check(!nav.Request(GalleryNavigator.Exhibit.Right) && nav.CompletedNavigations == completed && nav.FadeOpacity == 0,
                "Occupied destination rejected without a fade or jump");
            UnityEngine.Object.Destroy(obstacle); yield return Wait(.1f);
            Check(!nav.Request((GalleryNavigator.Exhibit)42), "Unknown destination rejected");
            Check(nav.Request(GalleryNavigator.Exhibit.Right), "Route available after removing temporary obstruction");
            yield return Wait(.05f);
            var recovery = station.GetComponent<VisitorViewRecovery>(); recovery.ResetView();
            var resetPose = nav.Origin.Camera.transform.position;
            yield return Wait(.5f);
            Check(!nav.IsTransitioning && nav.FadeOpacity == 0 && Vector3.Distance(resetPose, nav.Origin.Camera.transform.position) < .001f,
                "Reset View cancels an in-flight route without a later jump");

            Check(nav.Request(GalleryNavigator.Exhibit.Right), "Can navigate again after view recovery");
            yield return Wait(.05f); nav.enabled = false; yield return Wait(.2f);
            Check(nav.FadeOpacity == 0 && locomotion.All(p => p.Key.enabled == p.Value), "Disabling during a fade restores vision and locomotion");
            nav.enabled = true; yield return Wait(.2f);
            Check(nav.Buttons.Count == 3 && nav.Palette.gameObject.activeInHierarchy, "Re-enable restores one existing navigation panel");
            Check(nav.Request(GalleryNavigator.Exhibit.Main), "Final return to the main exhibit accepted");
            yield return Until(() => !nav.IsTransitioning); ParkHands(); yield return Wait(.1f);
            foreach (MuseumLanguage language in Enum.GetValues(typeof(MuseumLanguage)))
            {
                MuseumText.Channel.SetLanguage(language); yield return Wait(.1f);
                foreach (var label in nav.Palette.GetComponentsInChildren<TextMeshPro>())
                {
                    label.ForceMeshUpdate(true);
                    string key = label.GetComponent<LocalizedText>().Key;
                    Check(label.text == MuseumText.Table.Get(key, language), language + ": localized " + key);
                    Check(!label.isTextOverflowing && label.fontSize >= .25f, language + ": readable bounds " + key);
                    Check(label.text.All(c => char.IsWhiteSpace(c) || label.font.HasCharacter(c, false, false)), language + ": baked glyphs " + key);
                }
                Capture("navigation-" + language);
            }
        }

        static IEnumerator Press(GalleryNavigator.Exhibit destination, XRSimulatedController device, bool grip, bool trigger)
        {
            var button = nav.Buttons.Single(b => b.Destination == destination);
            Aim(device, button, false, false); yield return Wait(.3f);
            Check(button.Target.isHovered && nav.Palette.IsFrozen, destination + ": production ray hover freezes panel");
            int count = nav.CompletedNavigations, confirmations = button.Confirmations;
            Aim(device, button, grip, trigger); yield return Wait(.2f);
            Check(nav.CompletedNavigations == count && !nav.IsTransitioning, destination + ": holding does not navigate");
            Aim(device, button, false, false); yield return Wait(.65f);
            Check(nav.CompletedNavigations == count + 1 && button.Confirmations == confirmations + 1 && nav.CurrentExhibit == destination,
                destination + ": one release produces exactly one navigation (grip=" + grip + ", trigger=" + trigger + ")");
            ParkHands(); yield return Wait(.12f);
        }
        static void AssertDestination(GalleryNavigator.Exhibit destination, float floor, float eyeHeight, Vector3 localEye, Quaternion localRotation)
        {
            nav.TryGetDestination(destination, out var target, out var forward);
            var camera = nav.Origin.Camera.transform;
            Check(Vector2.Distance(XZ(camera.position), XZ(target)) < .001f, destination + ": camera reaches the authored place");
            Check(Mathf.Abs(nav.Origin.Origin.transform.position.y - floor) < .001f && Mathf.Abs(camera.position.y - eyeHeight) < .001f,
                destination + ": floor and current eye height unchanged");
            Check(camera.localPosition == localEye && Quaternion.Angle(camera.localRotation, localRotation) < .001f, destination + ": tracked local pose unchanged");
            Check(Vector3.Angle(Vector3.ProjectOnPlane(camera.forward, Vector3.up), forward) < .01f, destination + ": faces the exhibit");
        }
        static string Discoveries() => exploration.AirSeen + "/" + exploration.VoiceSeen + "/" + exploration.HintRequests + "/" + exploration.HasSelectedClue + "/" + exploration.SelectedClue;
        static void Aim(XRSimulatedController device, GalleryNavigationButton button, bool grip, bool trigger)
        {
            var position = button.Target.colliders.First().bounds.center - button.transform.forward * .45f;
            QueueController(device, position, Quaternion.LookRotation(button.Target.colliders.First().bounds.center - position), grip, trigger);
        }
        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip, bool trigger)
        {
            var parent = (device == left ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(world), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation,
                grip = grip ? 1 : 0, trigger = trigger ? 1 : 0 }.WithButton(ControllerButton.GripButton, grip).WithButton(ControllerButton.TriggerButton, trigger);
            input.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        {
            QueueController(left, new Vector3(-.8f, .6f, 1), Quaternion.LookRotation(Vector3.down), false, false);
            QueueController(right, new Vector3(.8f, .6f, 1), Quaternion.LookRotation(Vector3.down), false, false);
        }
        static void Capture(string name, bool requireBlack = false)
        {
            var obj = new GameObject("Navigation diagnostic camera");
            var camera = obj.AddComponent<Camera>(); camera.CopyFrom(nav.Origin.Camera); camera.enabled = false;
            camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
            camera.transform.SetPositionAndRotation(nav.Origin.Camera.transform.position, nav.Origin.Camera.transform.rotation);
            var target = RenderTexture.GetTemporary(1280, 800, 24);
            var previous = RenderTexture.active;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.aspect = 1.6f; camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
                if (requireBlack) Check(image.GetPixels32().All(c => c.r <= 2 && c.g <= 2 && c.b <= 2), "Opaque fade covers the whole monocular render");
            }
            finally
            { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(obj); }
        }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        static IEnumerator Until(Func<bool> done) { float end = Time.realtimeSinceStartup + 3; while (!done() && Time.realtimeSinceStartup < end) yield return null; Check(done(), "Transition completes within three seconds"); }
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (input.Count > 0) input.Dequeue()(); }
        static Vector2 XZ(Vector3 value) => new Vector2(value.x, value.z);
        static void Check(bool passed, string name)
        { report.checks.Add(new CheckResult { passed = passed, name = name }); if (!passed) throw new InvalidOperationException(name); }
        static void OnLog(string text, string _, LogType type) { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(text); }
        static void Finish(string failure)
        {
            finishing = true; report.failure = failure;
            Application.logMessageReceived -= OnLog; InputSystem.onBeforeUpdate -= FlushInput; input.Clear();
            foreach (var device in new InputDevice[] { left, right, hmd, keyboard, addedMouse }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (settingsChanged) { InputSystem.settings.backgroundBehavior = previousBackground; InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput; }
            bool passed = failure == null && report.errors.Count == 0;
            report.status = passed ? "passed" : "failed";
            File.WriteAllText(Path.Combine(Folder, "report.json"), JsonUtility.ToJson(report, true));
            SessionState.SetInt(Key + ".Exit", passed ? 0 : 1);
            Debug.Log("GALLERY NAVIGATION " + report.status.ToUpperInvariant() + ": " + report.checks.Count + " checks. " + failure);
            EditorApplication.ExitPlaymode();
        }
        static string Argument(string flag, string fallback = null)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback ?? throw new ArgumentException("Missing " + flag);
        }
        static string Git(string arguments)
        {
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments) {
                WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../..")), UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }))
            { string result = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit(); return result; }
        }
    }
}
