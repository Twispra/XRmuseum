#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Museum.Hotspots;
using Museum.PhysicalHands;
using Museum.PhysicalHands.EditorTools;
using Museum.Refined;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Diegetic.Editor
{
    /// <summary>Saved-scene Direct regression using production Input Actions and the unmodified physical glove.</summary>
    [InitializeOnLoad]
    public static class HelmetDiegeticDirectValidation
    {
        const string Prefix = "Museum.DiegeticDirectQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static HelmetDiegeticStation station;
        static ActionBasedController rightController, leftController;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD head;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedMouse, changedSettings, finishing;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static int lastFrame = -1, pendingExit = -1;
        static double started, exitAt;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", SessionState.GetString(Prefix + "Run", "missing"));

        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class Report
        {
            public string utc, finishedUtc, commit, dirty, unity, failure;
            public string method = "Saved MuseumStart Play Mode; queued synthetic tracked left/right controllers and HMD in ordinary Dynamic Input System updates through production Input Actions and XRDirectInteractor. Rays are disabled only in the transient Play Mode scene to isolate Direct. TestPose converts actual palm envelopes to controller poses and routes around solids. No direct SelectEnter/SelectExit, manual InputSystem.Update, asset saves, filter removal or generator calls. Public reset/window APIs establish independent fixture preconditions. A temporary solid blocker tests existing obstruction rejection.";
            public string limitations = "Synthetic controller input is not real headset/controller, comfort/reachability, user-study or executable-build validation. This focused runner complements the separate saved-scene ray/API regression.";
            public List<Check> checks = new List<Check>();
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<string> errors = new List<string>();
        }
        sealed class Pose
        {
            public ActionBasedController controller;
            public MuseumPhysicalHand hand;
            public XRSimulatedController device;
            public XRDirectInteractor direct => hand.DirectInteractor;
            public Vector3 controllerPosition, outward;
            public Quaternion rotation;
        }

        static HelmetDiegeticDirectValidation()
        { EditorApplication.playModeStateChanged += OnPlayMode; EditorApplication.update += Tick; }
        [MenuItem("Museum/Diegetic UI/Validate saved Direct interactions")]
        public static void RunInEditor() => Begin();
        public static void RunBatch() => Begin();

        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validation.");
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetString(Prefix + "Run", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-direct-contact-" + Guid.NewGuid().ToString("N"));
            report = new Report { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion };
            foreach (var path in new[] { ScenePath, "Packages/manifest.json", "Packages/packages-lock.json",
                "Assets/Samples/XR Interaction Toolkit/2.6.4/Starter Assets/XRI Default Input Actions.inputactions" }
                .Concat(Directory.GetFiles("Assets/Museum/Features/DiegeticUI", "*", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/Museum/Features/PhysicalHands", "*.cs", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/Museum/Features/PhysicalHands", "*.prefab", SearchOption.AllDirectories))
                .Concat(new[] { "Assets/Museum/Features/HelmetInspection/Runtime/HelmetInspectionController.cs", "Assets/Museum/Features/HelmetHotspots/Runtime/HelmetHotspot.cs", "Assets/Museum/Features/RefinedModels/Runtime/RefinedHelmetBinding.cs" })
                .Where(File.Exists).Distinct().OrderBy(p => p))
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                    report.files.Add(new Fingerprint { path = path.Replace('\\', '/'), sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() });
            SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
            SessionState.SetInt(Prefix + "Exit", 1);
            SessionState.SetBool(Prefix + "Active", true);
            EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("Diegetic Direct QA keyboard");
            mouse = Mouse.current; addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>("Diegetic Direct QA mouse");
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                started = EditorApplication.timeSinceStartup; lastFrame = -1; finishing = false;
                Application.logMessageReceived += OnLog; InputSystem.onBeforeUpdate += FlushInput;
                Routines.Clear(); PendingInput.Clear(); Routines.Push(Validate());
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Prefix + "Active", false);
                if (Application.isBatchMode) { pendingExit = SessionState.GetInt(Prefix + "Exit", 1); exitAt = EditorApplication.timeSinceStartup + 20; }
            }
        }
        static void Tick()
        {
            if (pendingExit >= 0 && EditorApplication.timeSinceStartup >= exitAt)
            { int code = pendingExit; pendingExit = -1; EditorApplication.Exit(code); return; }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 180) { Finish("Direct validation exceeded 180 seconds."); return; }
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
            catch (Exception error) { Finish(error.ToString()); }
        }

        static IEnumerator Validate()
        {
            yield return Wait(2);
            if (InputSystem.devices.OfType<XRHMD>().Any(d => !(d is XRSimulatedHMD))) throw new InvalidOperationException("Real HMD present; refusing synthetic input.");
            foreach (var simulator in UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>()) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
            Require(station && station.IsReady, "Saved diegetic station ready");
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Left Controller");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            head = InputSystem.AddDevice<XRSimulatedHMD>(); InputSystem.EnableDevice(keyboard); if (addedMouse) InputSystem.EnableDevice(mouse);
            var headState = new XRSimulatedHMDState { isTracked = true, trackingState = 3,
                devicePosition = new Vector3(0, 1.65f, 0), centerEyePosition = new Vector3(0, 1.65f, 0),
                leftEyePosition = new Vector3(-.032f, 1.65f, 0), rightEyePosition = new Vector3(.032f, 1.65f, 0),
                deviceRotation = Quaternion.identity, centerEyeRotation = Quaternion.identity, leftEyeRotation = Quaternion.identity, rightEyeRotation = Quaternion.identity };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(head, headState));
            ParkHands(); yield return Wait(.7f);
            foreach (var controller in new[] { rightController, leftController })
            {
                var hand = MuseumPhysicalHandsTestPose.Hand(controller);
                Require(hand && hand.IsTrackingValid && hand.DirectInteractor.isActiveAndEnabled, controller.name + " production physical hand initialized");
                foreach (var ray in controller.GetComponentsInChildren<XRRayInteractor>(true)) ray.enabled = false;
            }
            foreach (bool left in new[] { false, true }) yield return BodyRotation(left);
            yield return Button(false);
            yield return Button(true);
            yield return ReachAndObstruction();
            yield return HotspotPriority();
            Require(report.errors.Count == 0, "No Unity errors or exceptions", string.Join("\n", report.errors));
        }

        static IEnumerator BodyRotation(bool left)
        {
            ParkHands(); station.Inspection.ResetInspection(); yield return Wait(.4f);
            var target = station.RotationGesture;
            FindBodySurface(out var collider, out var contact, out var outward);
            var controller = left ? leftController : rightController;
            var pose = NewPose(controller, left ? leftDevice : rightDevice, outward);
            pose.controllerPosition = MuseumPhysicalHandsTestPose.OutsideTouch(collider, controller, pose.rotation, outward, contact, contact);
            yield return Reach(pose, "ordinary body / " + controller.name);
            Require(pose.hand.IsDirectContact(target) && target.IsHovered(pose.direct), "Open glove contacts ordinary body / " + controller.name, Diagnostics(pose, target));
            Quaternion before = station.Inspection.transform.rotation;
            Vector3 centre = station.Inspection.transform.TransformPoint(station.Inspection.SectionCenterLocal);
            Queue(pose, true); yield return Wait(.2f);
            Require(target.IsSelected(pose.direct) && target.IsDragging, "First ordinary Grip selects body / " + controller.name, Diagnostics(pose, target));
            Require(Quaternion.Angle(before, station.Inspection.transform.rotation) < .05f, "Grip alone does not rotate body / " + controller.name);
            Vector3 start = pose.controllerPosition;
            for (int i = 1; i <= 12; i++)
            { pose.controllerPosition = start + Vector3.right * (.07f * i / 12f); Queue(pose, true); yield return Wait(.025f); }
            yield return Wait(.15f);
            Require(target.IsSelected(pose.direct) && Quaternion.Angle(before, station.Inspection.transform.rotation) > 2f &&
                Vector3.Distance(centre, station.Inspection.transform.TransformPoint(station.Inspection.SectionCenterLocal)) < .001f,
                "Held Direct movement rotates around fixed exhibit centre / " + controller.name, Diagnostics(pose, target));
            Queue(pose, false); yield return Wait(.2f);
            Quaternion stopped = station.Inspection.transform.rotation;
            pose.controllerPosition += Vector3.right * .06f; Queue(pose, false); yield return Wait(.2f);
            Require(!target.isSelected && !target.IsDragging && Quaternion.Angle(stopped, station.Inspection.transform.rotation) < .05f,
                "Release stops rotation despite further hand movement / " + controller.name);
            ParkHands(); station.Inspection.ResetInspection(); yield return Wait(.4f);
            FindBodySurface(out collider, out contact, out outward);
            pose = NewPose(controller, left ? leftDevice : rightDevice, outward);
            pose.controllerPosition = MuseumPhysicalHandsTestPose.OutsideTouch(collider, controller, pose.rotation, outward, contact, contact);
            yield return Reach(pose, "second body grip / " + controller.name);
            Queue(pose, true); yield return Wait(.2f);
            Require(target.IsSelected(pose.direct), "Second ordinary Grip selects again / " + controller.name, Diagnostics(pose, target));
            Queue(pose, false); yield return Wait(.15f);
        }

        static IEnumerator Button(bool window)
        {
            ParkHands(); station.Inspection.ResetInspection(); station.Inspection.SetWindowOpen(false, true); yield return Wait(.4f);
            if (!window && station.AboutVisible) station.Execute(HelmetDiegeticStation.Command.About);
            var control = station.Controls[window ? HelmetDiegeticStation.Command.Window : HelmetDiegeticStation.Command.About];
            var target = control.Target;
            var collider = target.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy);
            Vector3 outward = window ? -station.Inspection.transform.forward : -control.transform.forward;
            var pose = NewPose(rightController, rightDevice, outward, Quaternion.identity);
            pose.controllerPosition = window
                ? MuseumPhysicalHandsTestPose.WindowTouch(collider, rightController, pose.rotation, outward, collider.bounds.center)
                : MuseumPhysicalHandsTestPose.ControlTouch(collider, rightController, pose.rotation, outward, collider.bounds.center, station.Inspection.GetComponent<XRGrabInteractable>());
            yield return Reach(pose, (window ? "window" : "ordinary button") + " confirm");
            int confirmed = control.ConfirmedCount;
            Queue(pose, true); yield return Wait(.35f);
            Require(target.IsSelected(pose.direct) && control.IsPressed && control.ConfirmedCount == confirmed &&
                (window ? !station.Inspection.WindowTargetOpen : !station.AboutVisible),
                (window ? "Window" : "Ordinary button") + " remains selected while held without firing", Diagnostics(pose, target));
            Queue(pose, false); yield return Wait(.2f);
            Require(!target.isSelected && control.ConfirmedCount == confirmed + 1 &&
                (window ? station.Inspection.WindowTargetOpen : station.AboutVisible),
                (window ? "Window" : "Ordinary button") + " fires once on release at contact", Diagnostics(pose, target));
            ParkHands(); station.Inspection.SetWindowOpen(false, true);
            if (!window && station.AboutVisible) station.Execute(HelmetDiegeticStation.Command.About);
            yield return Wait(.3f);
            collider = target.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy);
            pose.controllerPosition = window
                ? MuseumPhysicalHandsTestPose.WindowTouch(collider, rightController, pose.rotation, outward, collider.bounds.center)
                : MuseumPhysicalHandsTestPose.ControlTouch(collider, rightController, pose.rotation, outward, collider.bounds.center, station.Inspection.GetComponent<XRGrabInteractable>());
            yield return Reach(pose, (window ? "window" : "ordinary button") + " abandon");
            confirmed = control.ConfirmedCount; int cancelled = control.CancelledCount;
            Queue(pose, true); yield return Wait(.2f);
            Require(target.IsSelected(pose.direct), (window ? "Window" : "Ordinary button") + " selected before moving away", Diagnostics(pose, target));
            pose.controllerPosition += outward * .35f; Queue(pose, true); yield return Wait(.25f);
            Queue(pose, false); yield return Wait(.2f);
            Require(control.ConfirmedCount == confirmed && control.CancelledCount > cancelled &&
                (window ? !station.Inspection.WindowTargetOpen : !station.AboutVisible),
                (window ? "Window" : "Ordinary button") + " moving away cancels release", Diagnostics(pose, target));
        }

        static IEnumerator ReachAndObstruction()
        {
            ParkHands(); station.Inspection.ResetInspection(); yield return Wait(.4f);
            FindBodySurface(out var collider, out var contact, out var outward);
            var target = station.RotationGesture;
            var pose = NewPose(rightController, rightDevice, outward);
            Vector3 touch = MuseumPhysicalHandsTestPose.OutsideTouch(collider, rightController, pose.rotation, outward, contact, contact);
            pose.controllerPosition = touch + outward * .055f;
            yield return Reach(pose, "outside open reach inside wide query");
            var sphere = pose.direct.GetComponent<SphereCollider>();
            Vector3 queryCentre = sphere.transform.TransformPoint(sphere.center);
            float queryRadius = sphere.radius * Mathf.Abs(sphere.transform.lossyScale.x);
            bool withinQuery = target.colliders.Any(c => c && c.enabled && (c.ClosestPoint(queryCentre) - queryCentre).sqrMagnitude < queryRadius * queryRadius);
            Require(withinQuery && !pose.hand.IsDirectContact(target), "Far fixture is inside wide query sphere but outside open glove reach", Diagnostics(pose, target));
            Queue(pose, true); yield return Wait(.2f);
            Require(!target.isSelected && !target.IsHovered(pose.direct), "Wide query overlap cannot select or hover distant body", Diagnostics(pose, target));
            Queue(pose, false); yield return Wait(.15f);
            pose.controllerPosition = touch; yield return Reach(pose, "obstruction baseline");
            Require(pose.hand.IsDirectContact(target), "Unobstructed contact established before blocker", Diagnostics(pose, target));
            Vector3 palm = pose.hand.Body.position;
            Vector3 nearest = target.colliders.Where(c => c && c.enabled && c.gameObject.activeInHierarchy)
                .Select(c => c.ClosestPoint(palm)).OrderBy(p => (p - palm).sqrMagnitude).First();
            var blocker = new GameObject("QA temporary Direct obstruction");
            try
            {
                blocker.transform.position = Vector3.Lerp(palm, nearest, .65f);
                blocker.transform.rotation = Quaternion.LookRotation(nearest - palm);
                var box = blocker.AddComponent<BoxCollider>(); box.size = new Vector3(.06f, .06f, .006f);
                Physics.SyncTransforms();
                Require(!pose.hand.IsDirectContact(target) && !((IXRSelectFilter)station).Process(pose.direct, target),
                    "Solid obstruction rejects otherwise reachable contact", Diagnostics(pose, target));
                Queue(pose, true); yield return Wait(.2f);
                Require(!target.isSelected, "Grip cannot select body through solid blocker", Diagnostics(pose, target));
                Queue(pose, false); yield return Wait(.1f);
            }
            finally { UnityEngine.Object.DestroyImmediate(blocker); Physics.SyncTransforms(); }
        }

        static IEnumerator HotspotPriority()
        {
            ParkHands(); station.Inspection.ResetInspection(); yield return Wait(.4f);
            var hotspot = station.Inspection.GetComponentsInChildren<HelmetHotspot>(true).First(h => h.Identifier == "bonnet");
            var target = hotspot.GetComponent<XRSimpleInteractable>();
            var collider = target.colliders.Single();
            Vector3 outward = station.Inspection.transform.right;
            var pose = NewPose(rightController, rightDevice, outward, Quaternion.identity);
            pose.controllerPosition = MuseumPhysicalHandsTestPose.OutsideTouch(collider, rightController, pose.rotation, outward, collider.bounds.center);
            yield return Reach(pose, "original bonnet knowledge hotspot");
            Require(pose.hand.IsDirectContact(target), "Original knowledge hotspot is in Direct contact", Diagnostics(pose, target));
            Queue(pose, true); yield return Wait(.2f);
            Require(target.IsSelected(pose.direct) && hotspot.IsVisited && !station.RotationGesture.isSelected && !station.RotationGesture.IsDragging,
                "Original knowledge hotspot wins over whole-body rotation", Diagnostics(pose, target));
            Queue(pose, false); yield return Wait(.15f); ParkHands();
        }

        static Pose NewPose(ActionBasedController controller, XRSimulatedController device, Vector3 outward, Quaternion? rotation = null)
        {
            return new Pose { controller = controller, device = device, hand = MuseumPhysicalHandsTestPose.Hand(controller), outward = outward,
                rotation = rotation ?? Quaternion.LookRotation(-outward, Mathf.Abs(outward.y) > .95f ? Vector3.forward : Vector3.up) };
        }
        static IEnumerator Reach(Pose pose, string phase)
        {
            Queue(pose, false); yield return Wait(.06f);
            yield return MuseumPhysicalHandsTestPose.Approach(pose.controller, station.Inspection.GetComponent<XRGrabInteractable>(),
                (position, rotation, grip) => Queue(pose.device, pose.controller, position, rotation, grip), phase);
            yield return Wait(.25f);
        }
        static void FindBodySurface(out Collider surface, out Vector3 contact, out Vector3 normal)
        {
            Physics.SyncTransforms();
            var target = station.RotationGesture;
            var shell = station.Inspection.GetComponent<RefinedHelmetBinding>().ActiveShell.bounds;
            foreach (var direction in new[] { Vector3.back, Vector3.right, Vector3.left, Vector3.forward, new Vector3(-.7f,.45f,-1), new Vector3(.7f,.45f,-1) })
            foreach (float height in new[] { .55f, .25f, .75f, 0f })
            {
                var aim = shell.center + Vector3.up * shell.extents.y * height;
                var outside = aim + direction.normalized * (shell.extents.magnitude + .5f);
                foreach (var hit in Physics.RaycastAll(outside, (aim - outside).normalized, Vector3.Distance(outside, aim) + .04f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
                {
                    if (!hit.collider || hit.collider.GetComponentInParent<XRBaseInteractor>() || hit.collider.GetComponentInParent<MuseumPhysicalHand>()) continue;
                    if (target.interactionManager.TryGetInteractableForCollider(hit.collider, out IXRInteractable owner) && ReferenceEquals(owner, target) && !hit.collider.isTrigger && Mathf.Abs(hit.normal.y) < .9f)
                    { surface = hit.collider; contact = hit.point; normal = hit.normal; return; }
                    break;
                }
            }
            throw new InvalidOperationException("No unobstructed ordinary solid body surface found.");
        }
        static string Diagnostics(Pose pose, XRBaseInteractable target)
        {
            Vector3 palm = pose.hand.Body.position;
            Vector3 nearest = target.colliders.Where(c => c && c.enabled && c.gameObject.activeInHierarchy)
                .Select(c => c.ClosestPoint(palm)).OrderBy(p => (p - palm).sqrMagnitude).First();
            Vector3 local = Quaternion.Inverse(pose.hand.Body.rotation) * (nearest - palm) - pose.hand.OpenEnvelopeCenter;
            Vector3 half = pose.hand.OpenEnvelopeSize * .5f;
            Vector3 outside = new Vector3(Mathf.Max(Mathf.Abs(local.x) - half.x, 0),
                Mathf.Max(Mathf.Abs(local.y) - half.y, 0), Mathf.Max(Mathf.Abs(local.z) - half.z, 0));
            string blockers = string.Join("|", Physics.RaycastAll(palm, (nearest - palm).normalized,
                Vector3.Distance(palm, nearest), ~0, QueryTriggerInteraction.Ignore)
                .Select(hit => hit.collider.name + ":own=" + target.colliders.Contains(hit.collider) + ":distance=" + hit.distance.ToString("F6")));
            return MuseumPhysicalHandsTestPose.SelectionDiagnostics(pose.controller, target) +
                "; contact=" + pose.hand.IsDirectContact(target) + "; stationFilter=" + ((IXRSelectFilter)station).Process(pose.direct, target) +
                "; hovered=" + target.IsHovered(pose.direct) + "; selected=" + target.IsSelected(pose.direct) +
                "; openBoxOutside=" + outside.magnitude.ToString("F6") + "; palmDistance=" + Vector3.Distance(palm, nearest).ToString("F6") +
                "; rayHits=" + blockers;
        }
        static void Queue(Pose pose, bool grip) => Queue(pose.device, pose.controller, pose.controllerPosition, pose.rotation, grip);
        static void Queue(XRSimulatedController device, ActionBasedController controller, Vector3 world, Quaternion rotation, bool grip)
        {
            var parent = controller.transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(world), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation,
                grip = grip ? 1 : 0 }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        {
            Queue(rightDevice, rightController, new Vector3(2, 1.3f, -.8f), Quaternion.identity, false);
            Queue(leftDevice, leftController, new Vector3(-2, 1.3f, -.8f), Quaternion.identity, false);
        }
        static void FlushInput()
        { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static IEnumerator Wait(float seconds)
        { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        static void Require(bool passed, string name, string detail = "")
        {
            report.checks.Add(new Check { name = name, passed = passed, detail = detail });
            if (!passed) throw new InvalidOperationException(name + " / " + detail);
        }
        static void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + stack); }
        static void Finish(string failure)
        {
            if (finishing) return; finishing = true;
            report.failure = failure; report.finishedUtc = DateTime.UtcNow.ToString("o");
            int code = string.IsNullOrEmpty(failure) && report.errors.Count == 0 && report.checks.All(c => c.passed) ? 0 : 1;
            SessionState.SetInt(Prefix + "Exit", code);
            InputSystem.onBeforeUpdate -= FlushInput; Application.logMessageReceived -= OnLog; PendingInput.Clear();
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, head, keyboard, addedMouse ? mouse : null })
                if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedSettings)
            { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; }
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("Diegetic Direct validation " + (code == 0 ? "PASSED" : "FAILED") + ": " + Output + "; " + failure);
            EditorApplication.ExitPlaymode();
        }
        static string Git(string arguments)
        {
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments)
                { WorkingDirectory = RepoRoot, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }))
            { string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd(); process.WaitForExit(); return process.ExitCode == 0 ? output.TrimEnd() : "unavailable: " + error.TrimEnd(); }
        }
    }
}
#endif
