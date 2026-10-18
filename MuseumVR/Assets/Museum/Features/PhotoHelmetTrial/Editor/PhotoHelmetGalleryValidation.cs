#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Museum.Diegetic;
using Museum.Eras;
using Museum.Hotspots;
using Museum.Localization;
using Museum.PhysicalHands;
using Museum.PhysicalHands.EditorTools;
using Museum.Refined;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.PhotoHelmetTrial.EditorTools
{
    /// <summary>Read-only saved-main-scene acceptance of the independent photo exhibit.</summary>
    [InitializeOnLoad]
    public static class PhotoHelmetGalleryValidation
    {
        const string Prefix = "Museum.PhotoGalleryQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static readonly Dictionary<XRRayInteractor, bool> RayStates = new Dictionary<XRRayInteractor, bool>();
        static Report report;
        static PhotoHelmetGalleryDisplay display;
        static PhotoHelmetGalleryButton[] buttons;
        static HelmetDiegeticStation station;
        static HelmetEraSwitcher eras;
        static HelmetHotspotTour tour;
        static LocalizationTable table;
        static LanguageChannel language;
        static ActionBasedController rightController, leftController;
        static XRRayInteractor rightRay, leftRay;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD head;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedMouse, changedSettings, finishing;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static int lastFrame = -1;
        static double started;
        static Vector3 rayPosition;
        static Quaternion rayRotation;
        static Vector3 visitorPosition;
        static Quaternion visitorRotation;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => SessionState.GetString(Prefix + "Output", "");

        [Serializable] sealed class Check { public string name, detail, layer; public bool passed; }
        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class CaptureRecord { public string file, purpose; public Vector3 position; public Quaternion rotation; public float fieldOfView; }
        [Serializable] sealed class Report
        {
            public string startedUtc, finishedUtc, commit, dirty, unity, status = "running", failure = "";
            public string method = "Saved MuseumStart, no generator or asset saves. Queued synthetic tracked controllers, HMD and F11 use ordinary Dynamic Input System updates, production Input Actions, XRRayInteractor/ConeCast and physical-glove XRDirectInteractor. Ray cases retain authored ray configuration, colliders and filters; rays are temporarily disabled only in the separate labelled Direct fixture. Public gesture Begin/End and one hotspot Activate establish labelled protection/state preconditions; they do not count as XR selections. No TriggerProgrammatically, event Invoke, SelectEnter/SelectExit, manual InputSystem.Update or private-state writes.";
            public string limitations = "Desktop synthetic input and temporary diagnostic monocular cameras, not headset/controllers, stereo readability, comfort/reachability, user research, performance, historical authentication or executable build. The model/date remain the documented photo-label interpretation. The external runner must inspect the full Unity log through natural exit and compare all saved project fingerprints; report errors cover its subscribed Play lifetime.";
            public float batchExitSettleSeconds = 20;
            public List<Check> checks = new List<Check>();
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<CaptureRecord> captures = new List<CaptureRecord>();
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        static PhotoHelmetGalleryValidation()
        { EditorApplication.playModeStateChanged += OnPlayMode; EditorApplication.update += Tick; }
        [MenuItem("Museum/Photo helmet/Validate saved gallery XR interactions")]
        public static void RunBatch() => Begin();
        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("A dirty scene is open; validation never saves it.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string root = Environment.GetEnvironmentVariable("MUSEUM_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs");
            string output = Path.GetFullPath(Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-gallery-" + Guid.NewGuid().ToString("N")));
            if (Directory.Exists(output)) throw new InvalidOperationException("Evidence exists: " + output);
            Directory.CreateDirectory(output); SessionState.SetString(Prefix + "Output", output);
            report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion };
            foreach (var path in AssetDatabase.GetDependencies(ScenePath, true)
                .Concat(Directory.GetFiles("Assets/Museum/Features/PhotoHelmetTrial", "*.cs", SearchOption.AllDirectories))
                .Concat(new[] { "Packages/manifest.json", "Packages/packages-lock.json", "Assets/Museum/Features/Localization/Tables/MuseumTranslations.json" })
                .Distinct().Where(File.Exists).OrderBy(p => p, StringComparer.Ordinal))
                report.files.Add(new Fingerprint { path = path.Replace('\\', '/'), sha256 = Hash(path) });
            SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
            SessionState.SetInt(Prefix + "Exit", 1); SessionState.SetBool(Prefix + "PendingExit", false);
            SessionState.SetBool(Prefix + "Active", true); EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("Photo gallery QA keyboard");
            mouse = Mouse.current; addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>("Photo gallery QA mouse");
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                started = EditorApplication.timeSinceStartup; finishing = false; lastFrame = -1;
                PendingInput.Clear(); Routines.Clear(); RayStates.Clear(); Routines.Push(Validate());
                Application.logMessageReceived += OnLog; InputSystem.onBeforeUpdate += FlushInput;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && !finishing && report != null) Finish("Play Mode ended before acceptance completed.", false);
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Prefix + "Active", false);
                if (Application.isBatchMode)
                {
                    SessionState.SetFloat(Prefix + "ExitAt", (float)EditorApplication.timeSinceStartup + 20);
                    SessionState.SetBool(Prefix + "PendingExit", true);
                    Debug.Log("PHOTO_GALLERY_QA_SETTLING seconds=20; utc=" + DateTime.UtcNow.ToString("o"));
                }
            }
        }
        static void Tick()
        {
            if (SessionState.GetBool(Prefix + "PendingExit", false))
            {
                if (EditorApplication.timeSinceStartup < SessionState.GetFloat(Prefix + "ExitAt", float.MaxValue) ||
                    EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Prefix + "PendingExit", false); EditorApplication.Exit(SessionState.GetInt(Prefix + "Exit", 1)); return;
            }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 300) { Finish("Gallery acceptance exceeded 300 seconds."); return; }
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
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        static IEnumerator Validate()
        {
            yield return Wait(2);
            Require(!InputSystem.devices.OfType<XRHMD>().Any(d => !(d is XRSimulatedHMD)), "Synthetic input refuses real HMD", "No hardware state is overwritten.");
            display = UnityEngine.Object.FindObjectOfType<PhotoHelmetGalleryDisplay>();
            station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
            Require(display && display.isActiveAndEnabled && display.Model && station && station.isActiveAndEnabled && station.IsReady, "Saved independent display and current station are ready", SceneManager.GetActiveScene().path);
            buttons = display.GetComponentsInChildren<PhotoHelmetGalleryButton>(true);
            Require(buttons.Length == 3 && buttons.Select(b => b.ButtonAction).Distinct().Count() == 3 && buttons.All(b => b.Display == display), "Exactly three separately wired saved gallery controls", string.Join(",", buttons.Select(b => b.name)));
            foreach (var button in buttons)
            {
                var control = button.PhysicalControl;
                Require(control && control.Target && control.Target.isActiveAndEnabled && control.Target.colliders.Count == 1,
                    "Gallery control uses production physical confirmation / " + button.name, control ? "target=" + control.Target : "missing physical control");
                Require(Enumerable.Range(0, control.Target.selectFilters.count).Any(i => ReferenceEquals(control.Target.selectFilters.GetAt(i), station)) &&
                    Enumerable.Range(0, control.Target.hoverFilters.count).Any(i => ReferenceEquals(control.Target.hoverFilters.GetAt(i), station)),
                    "Gallery target inherits station select and hover protection / " + button.name, "Registered runtime extension, not an unguarded callback.");
            }
            eras = station.Inspection.GetComponent<HelmetEraSwitcher>(); tour = station.Inspection.GetComponentInChildren<HelmetHotspotTour>(true);
            table = MuseumText.Table; language = MuseumText.Channel;
            Require(eras && eras.AvailableEraCount == 3 && tour && table && language, "Independent display is outside central three-era catalogue", "era=" + (eras ? eras.CurrentIndex : -1));
            var observed = tour.GetComponentsInChildren<HelmetHotspot>(true).First(); observed.Activate();
            Require(tour.HasVisited(observed.Identifier), "Labelled API fixture seeds a central discovered observation", observed.Identifier, "fixture-api");
            SetupInput(); yield return Wait(.6f);
            var bounds = ModelBounds(); visitorPosition = new Vector3(bounds.center.x, 1.45f, bounds.min.z - 2.0f);
            visitorRotation = Quaternion.LookRotation(new Vector3(bounds.center.x, 1.10f, bounds.center.z) - visitorPosition, Vector3.up);
            CheckModel();
            yield return Obstruction();
            yield return ReleaseIntoObstruction();
            yield return DistantRayClick();
            yield return Click(Button(PhotoHelmetGalleryButton.Action.Reset), false);
            foreach (var action in new[] { PhotoHelmetGalleryButton.Action.TurnLeft, PhotoHelmetGalleryButton.Action.Reset, PhotoHelmetGalleryButton.Action.TurnRight, PhotoHelmetGalleryButton.Action.Reset })
            {
                yield return Click(Button(action), false);
                if (action != PhotoHelmetGalleryButton.Action.Reset) Capture("turn-" + action, "Same visitor-side diagnostic camera after real ray release.", visitorPosition, visitorRotation, 55);
            }
            yield return AbandonAndCrossTarget();
            yield return RecoveryAndLifecycle();
            yield return GestureProtection();
            SetRays(false);
            try
            {
                foreach (var action in new[] { PhotoHelmetGalleryButton.Action.TurnLeft, PhotoHelmetGalleryButton.Action.Reset, PhotoHelmetGalleryButton.Action.TurnRight, PhotoHelmetGalleryButton.Action.Reset })
                    yield return Click(Button(action), true);
            }
            finally { RestoreRays(); }
            yield return Wait(.2f);
            yield return Languages();
            CheckModel();
            Capture("gallery-and-central", "Diagnostic overview shows independent left display alongside current central station; not a headset screenshot.", new Vector3(-1.5f, 1.65f, .65f), Quaternion.LookRotation(new Vector3(-1.45f, 1.15f, 3.5f) - new Vector3(-1.5f, 1.65f, .65f)), 66);
            Require(report.errors.Count == 0, "No product errors during acceptance", string.Join("\n", report.errors));
        }

        static PhotoHelmetGalleryButton Button(PhotoHelmetGalleryButton.Action action) => buttons.Single(b => b.ButtonAction == action);
        static string CentralState() => eras.CurrentIndex + "|" + station.Inspection.transform.position.ToString("F6") + "|" + station.Inspection.transform.rotation.ToString("F6") + "|" +
            station.Inspection.WindowTargetOpen + "|" + station.Inspection.IsCutaway + "|" + station.Inspection.SectionOffset + "|" + station.Inspection.ResetCount + "|" +
            string.Join(",", tour.GetComponentsInChildren<HelmetHotspot>(true).OrderBy(h => h.Identifier).Select(h => h.Identifier + ":" + tour.HasVisited(h.Identifier)));
        static IEnumerator DistantRayClick()
        {
            var eye = Camera.main.transform; Vector3 originalEye = eye.position; Quaternion originalFacing = eye.rotation;
            // Keep the initial HMD and the same torso-adjacent controller origin.
            // Candidates change aim within a saved button, never the controller origin or target.
            Vector3 origin = originalEye + eye.right * .22f - Vector3.up * .18f - eye.forward * .08f;
            Require(!Physics.OverlapSphere(origin, .08f, rightRay.raycastMask, QueryTriggerInteraction.Ignore).Any(SceneSolid),
                "Initial-view controller origin is outside scene solids", "eye=" + originalEye.ToString("F6") + "; origin=" + origin.ToString("F6"));
            foreach (var action in new[] { PhotoHelmetGalleryButton.Action.TurnLeft, PhotoHelmetGalleryButton.Action.TurnRight, PhotoHelmetGalleryButton.Action.Reset })
                foreach (float verticalAim in new[] { 0f, .6f })
                {
                    var button = Button(action); var control = button.PhysicalControl; var target = control.Target;
                    var bounds = target.colliders.Single().bounds; var aim = bounds.center + Vector3.up * bounds.extents.y * verticalAim;
                    var facing = Quaternion.LookRotation(aim - origin, Vector3.up);
                    QueueController(rightDevice, origin, facing, false); yield return Wait(.3f);
                    if (!FirstHit(target) || !target.IsHovered(rightRay)) continue;
                    var rotation = display.Model.localRotation; var position = display.Model.position; string central = CentralState();
                    int confirmed = control.ConfirmedCount, enters = 0, exits = 0; bool selectedByRay = false;
                    UnityEngine.Events.UnityAction<SelectEnterEventArgs> enter = a => { enters++; selectedByRay = a.interactorObject is XRRayInteractor; };
                    UnityEngine.Events.UnityAction<SelectExitEventArgs> exit = a => exits++;
                    target.selectEntered.AddListener(enter); target.selectExited.AddListener(exit);
                    try
                    {
                        QueueController(rightDevice, origin, facing, true); yield return Wait(.3f);
                        Require(Vector3.Distance(origin, bounds.center) > 2.5f && FirstHit(target) && enters == 1 && selectedByRay && control.IsPressed &&
                            control.ConfirmedCount == confirmed && Quaternion.Angle(rotation, display.Model.localRotation) < .01f,
                            "Initial central viewing position reaches gallery by actual distant ray without firing on press", "distance=" + Vector3.Distance(origin, bounds.center) + "; origin=" + origin.ToString("F6") + "; " + InputDetail(target), "production-input");
                        QueueController(rightDevice, origin, facing, false); yield return Wait(.25f);
                        Quaternion expected = action == PhotoHelmetGalleryButton.Action.Reset ? display.FrontRotation : Quaternion.AngleAxis(action == PhotoHelmetGalleryButton.Action.TurnLeft ? 30 : -30, Vector3.up) * rotation;
                        Require(exits == 1 && control.ConfirmedCount == confirmed + 1 && !target.isSelected && Quaternion.Angle(expected, display.Model.localRotation) < .05f && Vector3.Distance(position, display.Model.position) < .0001f,
                            "Distant ray release performs the requested action exactly once", "action=" + action + "; exits=" + exits + "; confirmed=" + control.ConfirmedCount, "production-input");
                        Require(Vector3.Distance(originalEye, eye.position) < .001f && Quaternion.Angle(originalFacing, eye.rotation) < .01f && CentralState() == central,
                            "Distant gallery action preserves initial HMD pose and central exhibit state", "Only controller orientation changed; HMD and saved targets did not move.");
                    }
                    finally { target.selectEntered.RemoveListener(enter); target.selectExited.RemoveListener(exit); }
                    ParkHands(); yield return Wait(.2f); yield break;
                }
            Require(false, "Initial central viewing position has a legal production gallery ray", "Fixed controller origin=" + origin.ToString("F6") + "; " + InputDetail(buttons[0].PhysicalControl.Target), "production-input");
        }
        static IEnumerator Click(PhotoHelmetGalleryButton button, bool direct)
        {
            ParkHands(); yield return Wait(.15f);
            var control = button.PhysicalControl; var target = control.Target;
            var beforeRotation = display.Model.localRotation; var beforePosition = display.Model.position; string central = CentralState();
            int confirmed = control.ConfirmedCount, enters = 0, exits = 0; string selector = "none";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> enter = a => { enters++; selector = a.interactorObject.GetType().Name; };
            UnityEngine.Events.UnityAction<SelectExitEventArgs> exit = a => exits++;
            target.selectEntered.AddListener(enter); target.selectExited.AddListener(exit);
            try
            {
                if (direct) yield return AimDirect(button); else yield return AimRay(button, true);
                QueueController(rightDevice, rayPosition, rayRotation, true); yield return Wait(.3f);
                Require(control.IsPressed && target.isSelected && enters == 1 && selector == (direct ? nameof(XRDirectInteractor) : nameof(XRRayInteractor)),
                    "Production " + (direct ? "Direct" : "ray") + " Grip selects exactly once / " + button.ButtonAction,
                    "enters=" + enters + "; selector=" + selector + "; " + InputDetail(target), "production-input");
                Require(control.ConfirmedCount == confirmed && Quaternion.Angle(beforeRotation, display.Model.localRotation) < .01f,
                    "Holding gallery control has no action / " + button.ButtonAction, "Confirmed=" + control.ConfirmedCount, "production-input");
                Require(!((IXRSelectFilter)station).Process(leftRay, station.RotationGesture), "Gallery press rejects central gesture start in other hand", "Shared station filter while a gallery control is pressed.", "protection");
                QueueController(rightDevice, rayPosition, rayRotation, false); yield return Wait(.25f);
                Require(!target.isSelected && !control.IsPressed && exits == 1 && control.ConfirmedCount == confirmed + 1,
                    "Release confirms gallery action exactly once / " + button.ButtonAction, "exits=" + exits + "; confirmations=" + control.ConfirmedCount, "production-input");
                Quaternion expected = button.ButtonAction == PhotoHelmetGalleryButton.Action.Reset ? display.FrontRotation :
                    Quaternion.AngleAxis(button.ButtonAction == PhotoHelmetGalleryButton.Action.TurnLeft ? 30 : -30, Vector3.up) * beforeRotation;
                Require(Quaternion.Angle(expected, display.Model.localRotation) < .05f && Vector3.Distance(beforePosition, display.Model.position) < .0001f,
                    "Direction, exact 30 degree step and fixed pivot / " + button.ButtonAction, "expected=" + expected + "; actual=" + display.Model.localRotation + "; pivot=" + display.Model.position);
                Require(CentralState() == central, "Gallery action preserves central era, pose, cut/window and discovered state / " + button.ButtonAction, central);
            }
            finally { target.selectEntered.RemoveListener(enter); target.selectExited.RemoveListener(exit); }
            ParkHands(); yield return Wait(.15f);
        }

        static IEnumerator AimRay(PhotoHelmetGalleryButton button, bool requireHover)
        {
            var target = button.PhysicalControl.Target; var collider = target.colliders.Single();
            var point = collider.bounds.center;
            foreach (var offset in new[] { new Vector3(0, 0, -.48f), new Vector3(0, .12f, -.48f), new Vector3(-.12f, .10f, -.50f), new Vector3(.12f, .10f, -.50f) })
            {
                rayPosition = point + offset; rayRotation = Quaternion.LookRotation(point - rayPosition, Vector3.up);
                var overlaps = Physics.OverlapSphere(rayPosition, .08f, rightRay.raycastMask, QueryTriggerInteraction.Ignore).Where(SceneSolid).ToArray();
                if (overlaps.Length > 0) continue;
                QueueController(rightDevice, rayPosition, rayRotation, false); yield return Wait(.25f);
                if (FirstHit(target) && (!requireHover || target.IsHovered(rightRay)))
                {
                    Require(true, "Actual authored production ray first hits gallery target / " + button.ButtonAction, InputDetail(target), "production-input");
                    yield break;
                }
            }
            Require(false, "Unobstructed external production ray reaches gallery target / " + button.ButtonAction, InputDetail(target), "production-input");
        }
        static bool FirstHit(XRBaseInteractable target) => rightRay.TryGetCurrent3DRaycastHit(out var hit) && target.colliders.Contains(hit.collider);
        static bool SceneSolid(Collider c) => c && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && !c.GetComponentInParent<XRBaseInteractor>() && !c.GetComponentInParent<MuseumPhysicalHand>();
        static string InputDetail(XRBaseInteractable target)
        {
            bool hit = rightRay && rightRay.TryGetCurrent3DRaycastHit(out _);
            rightRay.TryGetCurrent3DRaycastHit(out var first);
            return "first=" + (hit && first.collider ? first.collider.name : "none") + "; origin=" + rightRay.rayOriginTransform.position.ToString("F6") + "; direction=" + rightRay.rayOriginTransform.forward.ToString("F6") +
                "; detection=" + rightRay.hitDetectionType + "; mask=" + rightRay.raycastMask.value + "; trigger=" + rightRay.raycastTriggerInteraction + "; targetHovered=" + target.isHovered + "; selected=" + target.isSelected +
                "; selectTrigger=" + rightRay.selectActionTrigger + "; isSelectActive=" + rightRay.isSelectActive + "; rawGrip=" + rightController.currentControllerState.selectInteractionState.value + "; rawGripActive=" + rightController.currentControllerState.selectInteractionState.active;
        }
        static IEnumerator AimDirect(PhotoHelmetGalleryButton button)
        {
            var target = button.PhysicalControl.Target; var collider = target.colliders.Single();
            var hand = MuseumPhysicalHandsTestPose.Hand(rightController);
            Require(hand && hand.IsTrackingValid && hand.DirectInteractor.isActiveAndEnabled, "Saved physical glove is active for Direct fixture", "No collider/filter/palm override.");
            rayRotation = Quaternion.identity;
            rayPosition = MuseumPhysicalHandsTestPose.OutsideTouch(collider, rightController, rayRotation, Vector3.back, collider.bounds.center, collider.bounds.center);
            foreach (var waypoint in new[] { collider.bounds.center + new Vector3(0, .3f, -.55f), collider.bounds.center + new Vector3(0, .15f, -.30f), rayPosition })
            { QueueController(rightDevice, waypoint, rayRotation, false); yield return MuseumPhysicalHandsTestPose.WaitForPalm(rightController, "gallery Direct approach"); }
            yield return Wait(.15f);
            Require(hand.IsDirectContact(target) && target.IsHovered(hand.DirectInteractor), "Open physical glove actually contacts gallery button / " + button.ButtonAction,
                MuseumPhysicalHandsTestPose.SelectionDiagnostics(rightController, target), "production-input");
        }

        static IEnumerator AbandonAndCrossTarget()
        {
            var a = Button(PhotoHelmetGalleryButton.Action.TurnLeft); var b = Button(PhotoHelmetGalleryButton.Action.TurnRight);
            yield return AimRay(a, true);
            var before = display.Model.localRotation; int ca = a.PhysicalControl.ConfirmedCount, cb = b.PhysicalControl.ConfirmedCount, cancelled = a.PhysicalControl.CancelledCount;
            QueueController(rightDevice, rayPosition, rayRotation, true); yield return Wait(.2f);
            Require(a.PhysicalControl.IsPressed, "Ray holds gallery control before abandonment", InputDetail(a.PhysicalControl.Target), "production-input");
            QueueController(rightDevice, rayPosition + Vector3.left * .8f, Quaternion.LookRotation(Vector3.back), true); yield return Wait(.25f);
            Require(!a.PhysicalControl.IsPressed && a.PhysicalControl.CancelledCount > cancelled, "Moving off while Grip held cancels gallery press", "Cancelled=" + a.PhysicalControl.CancelledCount, "production-input");
            var collider = b.PhysicalControl.Target.colliders.Single(); rayPosition = collider.bounds.center + Vector3.back * .48f; rayRotation = Quaternion.LookRotation(collider.bounds.center - rayPosition);
            QueueController(rightDevice, rayPosition, rayRotation, true); yield return Wait(.25f);
            Require(FirstHit(b.PhysicalControl.Target) && !b.PhysicalControl.Target.isSelected && !b.PhysicalControl.IsPressed,
                "Abandoned held Grip cannot transfer to another gallery target", InputDetail(b.PhysicalControl.Target), "production-input");
            yield return AimCentralWhileHeld();
            QueueController(rightDevice, rayPosition, rayRotation, false); yield return Wait(.2f);
            Require(a.PhysicalControl.ConfirmedCount == ca && b.PhysicalControl.ConfirmedCount == cb && Quaternion.Angle(before, display.Model.localRotation) < .01f,
                "Off-target and cross-target releases perform no action", "Original and destination confirmation counts unchanged.");
            yield return Click(b, false); yield return Click(Button(PhotoHelmetGalleryButton.Action.Reset), false);
        }
        static IEnumerator RecoveryAndLifecycle()
        {
            var button = Button(PhotoHelmetGalleryButton.Action.TurnLeft);
            foreach (string fixture in new[] { "F11", "component", "display-component", "display-root", "station" })
            {
                ParkHands(); yield return Wait(.2f); yield return AimRay(button, true);
                var control = button.PhysicalControl; var rotation = display.Model.localRotation; int count = control.ConfirmedCount;
                int visuals = display.GetComponentsInChildren<Renderer>(true).Length;
                int controlCount = UnityEngine.Object.FindObjectsOfType<HelmetPhysicalControl>(true).Length;
                QueueController(rightDevice, rayPosition, rayRotation, true); yield return Wait(.2f);
                Require(control.IsPressed, "Lifecycle fixture holds gallery press / " + fixture, InputDetail(control.Target), "production-input");
                if (fixture == "F11") PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F11)));
                else if (fixture == "component") button.enabled = false;
                else if (fixture == "display-component") display.enabled = false;
                else if (fixture == "display-root") display.gameObject.SetActive(false);
                else station.enabled = false;
                yield return Wait(.3f);
                Require(!control.IsPressed && !control.Target.isSelected && control.ConfirmedCount == count && Quaternion.Angle(rotation, display.Model.localRotation) < .01f,
                    "Recovery/disable cancels without confirmation / " + fixture, "count=" + control.ConfirmedCount, "production-input");
                if (fixture == "component" || fixture == "display-component" || fixture == "station")
                {
                    int disabledEnters = 0; var disabledTarget = control.Target;
                    UnityEngine.Events.UnityAction<SelectEnterEventArgs> disabledListener = a => disabledEnters++;
                    disabledTarget.selectEntered.AddListener(disabledListener);
                    try
                    {
                        QueueController(rightDevice, rayPosition, rayRotation, false); yield return Wait(.2f);
                        QueueController(rightDevice, rayPosition, rayRotation, true); yield return Wait(.25f);
                        Require(disabledEnters == 0 && !disabledTarget.isSelected && !control.IsPressed && control.ConfirmedCount == count && Quaternion.Angle(rotation, display.Model.localRotation) < .01f,
                            "Fresh production Grip cannot select while component is unavailable / " + fixture,
                            "enters=" + disabledEnters + "; confirmed=" + control.ConfirmedCount + "; " + InputDetail(disabledTarget), "production-input");
                    }
                    finally { disabledTarget.selectEntered.RemoveListener(disabledListener); }
                    // Keep this newly held Grip through restoration below: enabling the
                    // presentation must still require a later release and fresh press.
                }
                PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
                if (fixture == "component") button.enabled = true;
                else if (fixture == "display-component") display.enabled = true;
                else if (fixture == "display-root") display.gameObject.SetActive(true);
                else if (fixture == "station") station.enabled = true;
                yield return Wait(.3f);
                var hit = button.PhysicalControl.Target.colliders.Single(); rayPosition = hit.bounds.center + Vector3.back * .48f; rayRotation = Quaternion.LookRotation(hit.bounds.center - rayPosition);
                QueueController(rightDevice, rayPosition, rayRotation, true); yield return Wait(.25f);
                Require(!button.PhysicalControl.IsPressed && !button.PhysicalControl.Target.isSelected && Quaternion.Angle(rotation, display.Model.localRotation) < .01f,
                    "Restoring availability does not reuse still-held Grip / " + fixture, InputDetail(button.PhysicalControl.Target), "production-input");
                QueueController(rightDevice, rayPosition, rayRotation, false); yield return Wait(.25f);
                Require(display.GetComponentsInChildren<Renderer>(true).Length == visuals && UnityEngine.Object.FindObjectsOfType<HelmetPhysicalControl>(true).Length == controlCount,
                    "Lifecycle restoration creates no duplicate controls or visual copies / " + fixture, "renderers=" + visuals + "; controls=" + controlCount);
                yield return Click(button, false); yield return Click(Button(PhotoHelmetGalleryButton.Action.Reset), false);
            }
        }
        static IEnumerator AimCentralWhileHeld()
        {
            var target = station.RotationGesture; var bounds = station.Inspection.GetComponent<RefinedHelmetBinding>().ActiveShell.bounds;
            int enters = 0, exits = 0; bool raySelected = false;
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> enter = a => { enters++; raySelected = a.interactorObject is XRRayInteractor; };
            UnityEngine.Events.UnityAction<SelectExitEventArgs> exit = a => exits++;
            target.selectEntered.AddListener(enter); target.selectExited.AddListener(exit);
            try
            {
                foreach (var direction in new[] { new Vector3(-.7f, .45f, -1), new Vector3(.7f, .45f, -1), new Vector3(-1, .6f, -.15f), new Vector3(1, .6f, -.15f), new Vector3(-.7f, .45f, 1), new Vector3(.7f, .45f, 1) })
                    foreach (float height in new[] { .55f, .25f, .75f, 0f })
                    {
                        var aim = bounds.center + Vector3.up * bounds.extents.y * height;
                        var position = aim + station.Inspection.transform.TransformDirection(direction.normalized) * (bounds.extents.magnitude + .4f);
                        if (Physics.OverlapSphere(position, .08f, rightRay.raycastMask, QueryTriggerInteraction.Ignore).Any(SceneSolid)) continue;
                        var facing = Quaternion.LookRotation(aim - position, Vector3.up);
                        QueueController(rightDevice, position, facing, true); yield return Wait(.2f);
                        if (!FirstHit(target)) continue;
                        // StateChange can report isSelectActive=false after the old selection ends,
                        // even though the controller's raw Grip remains held. Test the real behavior.
                        Require(rightController.currentControllerState.selectInteractionState.active && rightController.currentControllerState.selectInteractionState.value > .9f &&
                            enters == 0 && !target.isSelected && !target.IsDragging,
                            "Abandoned gallery Grip cannot transfer to actual central body surface", InputDetail(target), "production-input");
                        Vector3 centralPosition = station.Inspection.transform.position; Quaternion centralRotation = station.Inspection.transform.rotation;
                        QueueController(rightDevice, position, facing, false); yield return Wait(.2f);
                        QueueController(rightDevice, position, facing, true); yield return Wait(.25f);
                        Require(FirstHit(target) && enters == 1 && raySelected && target.IsSelected(rightRay) && target.IsDragging,
                            "Release then fresh Grip actually selects central body at the same pose", "enters=" + enters + "; " + InputDetail(target), "production-input");
                        QueueController(rightDevice, position, facing, false); yield return Wait(.2f);
                        Require(exits == 1 && !target.isSelected && !target.IsDragging && Vector3.Distance(centralPosition, station.Inspection.transform.position) < .001f && Quaternion.Angle(centralRotation, station.Inspection.transform.rotation) < .05f,
                            "Fresh body Grip releases once without movement at a fixed controller pose", "exits=" + exits + "; " + InputDetail(target), "production-input");
                        yield break;
                    }
                Require(false, "Cross-target fixture reaches actual central body first surface", InputDetail(target), "production-input");
            }
            finally { target.selectEntered.RemoveListener(enter); target.selectExited.RemoveListener(exit); }
        }
        static IEnumerator GestureProtection()
        {
            var button = Button(PhotoHelmetGalleryButton.Action.TurnRight);
            station.Execute(HelmetDiegeticStation.Command.Air); yield return Wait(.3f);
            var equipment = UnityEngine.Object.FindObjectsOfType<HelmetGestureInteractable>().FirstOrDefault(g => g != station.RotationGesture && g != station.SectionGesture && g.isActiveAndEnabled);
            Require(equipment, "Active demonstration provides equipment protection fixture", "Labelled public Air command.", "fixture-api");
            foreach (var gesture in new[] { station.RotationGesture, station.SectionGesture, equipment })
            {
                bool sectionFixture = gesture == station.SectionGesture && !station.Inspection.IsCutaway;
                if (sectionFixture) { station.Execute(HelmetDiegeticStation.Command.Section); yield return Wait(.2f); }
                ParkHands(); yield return Wait(.2f); yield return AimRay(button, true);
                var rotation = display.Model.localRotation; int count = button.PhysicalControl.ConfirmedCount;
                gesture.BeginForValidation(Vector3.zero, Quaternion.identity);
                try
                {
                    Require(gesture.IsDragging, "Labelled public gesture precondition / " + gesture.GestureMode, "Gesture API establishes a drag; gallery input remains production XR.", "fixture-api");
                    QueueController(rightDevice, rayPosition, rayRotation, true); yield return Wait(.25f);
                    Require(FirstHit(button.PhysicalControl.Target) && !button.PhysicalControl.IsPressed && !button.PhysicalControl.Target.isSelected && gesture.IsDragging,
                        "Active central/equipment drag rejects gallery Grip / " + gesture.GestureMode, InputDetail(button.PhysicalControl.Target), "production-input");
                    QueueController(rightDevice, rayPosition, rayRotation, false); yield return Wait(.2f);
                    Require(button.PhysicalControl.ConfirmedCount == count && Quaternion.Angle(rotation, display.Model.localRotation) < .01f, "Blocked drag input leaves gallery model unchanged", gesture.GestureMode.ToString());
                }
                finally
                {
                    gesture.EndForValidation();
                    if (sectionFixture && station.Inspection.IsCutaway) station.Execute(HelmetDiegeticStation.Command.Section);
                }
            }
            station.Execute(HelmetDiegeticStation.Command.CloseDemo); ParkHands(); yield return Wait(.25f);
            yield return Click(button, false); yield return Click(Button(PhotoHelmetGalleryButton.Action.Reset), false);
        }
        static IEnumerator Obstruction()
        {
            var button = Button(PhotoHelmetGalleryButton.Action.TurnLeft); yield return AimRay(button, true);
            var target = button.PhysicalControl.Target;
            var rotation = display.Model.localRotation; int count = button.PhysicalControl.ConfirmedCount, enters = 0; string selector = "none";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = a => { enters++; selector = a.interactorObject.GetType().Name; };
            target.selectEntered.AddListener(listener);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name = "Temporary gallery QA solid blocker";
            blocker.transform.position = Vector3.Lerp(rayPosition, button.PhysicalControl.Target.colliders.Single().bounds.center, .55f);
            blocker.transform.localScale = new Vector3(.22f, .22f, .10f); Physics.SyncTransforms();
            try
            {
                QueueController(rightDevice, rayPosition, rayRotation, false); yield return Wait(.25f);
                var origin = rightRay.rayOriginTransform;
                var physicalHits = Physics.RaycastAll(origin.position, origin.forward, rightRay.maxRaycastDistance, rightRay.raycastMask, rightRay.raycastTriggerInteraction)
                    .Where(h => SceneSolid(h.collider)).OrderBy(h => h.distance).ToArray();
                Require(physicalHits.Length > 0 && physicalHits[0].collider == blocker.GetComponent<Collider>(),
                    "Nearest physical surface is the real intervening solid", "physical=" + string.Join(",", physicalHits.Select(h => h.collider.name + ":" + h.distance.ToString("F6"))) + "; production=" + InputDetail(target), "production-input");
                QueueController(rightDevice, rayPosition, rayRotation, true); yield return Wait(.2f);
                bool pressClear = enters == 0 && !target.isSelected && !button.PhysicalControl.IsPressed && button.PhysicalControl.ConfirmedCount == count && Quaternion.Angle(rotation, display.Model.localRotation) < .01f;
                string press = "press enters=" + enters + "; selector=" + selector + "; confirmedDelta=" + (button.PhysicalControl.ConfirmedCount - count) + "; angle=" + Quaternion.Angle(rotation, display.Model.localRotation) + "; " + InputDetail(target);
                // Complete normal release even if selection was wrong, so the failure
                // distinguishes an unwanted hover/selection from a real action through a solid.
                QueueController(rightDevice, rayPosition, rayRotation, false); yield return Wait(.2f);
                Require(pressClear && enters == 0 && !target.isSelected && button.PhysicalControl.ConfirmedCount == count && Quaternion.Angle(rotation, display.Model.localRotation) < .01f,
                    "Real solid obstruction prevents selection and release action", press + " | release enters=" + enters + "; confirmedDelta=" + (button.PhysicalControl.ConfirmedCount - count) + "; angle=" + Quaternion.Angle(rotation, display.Model.localRotation) + "; " + InputDetail(target), "production-input");
            }
            finally { target.selectEntered.RemoveListener(listener); UnityEngine.Object.Destroy(blocker); }
            ParkHands(); yield return Wait(.25f);
        }
        static IEnumerator ReleaseIntoObstruction()
        {
            var button = Button(PhotoHelmetGalleryButton.Action.TurnLeft); yield return AimRay(button, true);
            var control = button.PhysicalControl; var target = control.Target;
            var rotation = display.Model.localRotation; int count = control.ConfirmedCount, cancelled = control.CancelledCount, enters = 0, exits = 0;
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> enter = a => enters++;
            UnityEngine.Events.UnityAction<SelectExitEventArgs> exit = a => exits++;
            target.selectEntered.AddListener(enter); target.selectExited.AddListener(exit);
            GameObject blocker = null;
            try
            {
                QueueController(rightDevice, rayPosition, rayRotation, true); yield return Wait(.25f);
                Require(enters == 1 && control.IsPressed && target.IsSelected(rightRay) && control.ConfirmedCount == count && Quaternion.Angle(rotation, display.Model.localRotation) < .01f,
                    "Clear gallery button is genuinely held before release-time obstruction", "enters=" + enters + "; " + InputDetail(target), "production-input");
                blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name = "Temporary release-time gallery QA solid";
                blocker.transform.position = Vector3.Lerp(rayPosition, target.colliders.Single().bounds.center, .55f);
                blocker.transform.localScale = new Vector3(.22f, .22f, .10f);
                Physics.SyncTransforms();
                // Deliberately no yield/Update between inserting the real solid and
                // queuing release: a hold-only Update guard is insufficient here.
                QueueController(rightDevice, rayPosition, rayRotation, false); yield return Wait(.3f);
                var origin = rightRay.rayOriginTransform;
                var hits = Physics.RaycastAll(origin.position, origin.forward, rightRay.maxRaycastDistance, rightRay.raycastMask, rightRay.raycastTriggerInteraction)
                    .Where(h => SceneSolid(h.collider)).OrderBy(h => h.distance).ToArray();
                Require(hits.Length > 0 && hits[0].collider == blocker.GetComponent<Collider>(),
                    "Release-time solid is the nearest physical surface", string.Join(",", hits.Select(h => h.collider.name + ":" + h.distance.ToString("F6"))), "production-input");
                Require(enters == 1 && exits == 1 && !target.isSelected && !control.IsPressed && control.ConfirmedCount == count && control.CancelledCount > cancelled && Quaternion.Angle(rotation, display.Model.localRotation) < .01f,
                    "Same-frame inserted solid cancels release before confirmation or action",
                    "enters=" + enters + "; exits=" + exits + "; confirmedDelta=" + (control.ConfirmedCount - count) + "; cancelledDelta=" + (control.CancelledCount - cancelled) + "; angle=" + Quaternion.Angle(rotation, display.Model.localRotation) + "; " + InputDetail(target), "production-input");
            }
            finally
            {
                target.selectEntered.RemoveListener(enter); target.selectExited.RemoveListener(exit);
                if (blocker) UnityEngine.Object.Destroy(blocker);
            }
            ParkHands(); yield return Wait(.25f);
            yield return Click(button, false); yield return Click(Button(PhotoHelmetGalleryButton.Action.Reset), false);
        }

        static Bounds ModelBounds()
        {
            var renderers = display.Model.GetComponentsInChildren<MeshRenderer>(false).Where(r => r.enabled).ToArray();
            Require(renderers.Length > 0, "Photo model has active mesh renderers", "No placeholder stand substitutes for the saved model.");
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        static void CheckModel()
        {
            var filters = display.Model.GetComponentsInChildren<MeshFilter>(true);
            Require(filters.Length > 0 && filters.All(f => f.sharedMesh && f.gameObject.activeInHierarchy), "All saved photo meshes exist and are active", "filters=" + filters.Length);
            int triangles = filters.Sum(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(i => checked((int)f.sharedMesh.GetIndexCount(i)) / 3));
            Require(triangles == 117304, "Full saved photo model has 117304 triangles", triangles.ToString());
            foreach (var filter in filters)
                Require(filter.sharedMesh.bounds.size.sqrMagnitude > .001f && filter.sharedMesh.vertexCount > 0,
                    "Nonempty imported mesh / " + filter.name, filter.sharedMesh.bounds.ToString());
            foreach (var renderer in display.Model.GetComponentsInChildren<MeshRenderer>(true))
                Require(renderer.enabled && renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m && m.shader && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader"),
                    "Photo renderer has supported non-error materials / " + renderer.name, string.Join(",", renderer.sharedMaterials.Select(m => m ? m.name + ":" + (m.shader ? m.shader.name : "NO_SHADER") : "missing")));
            var bounds = ModelBounds(); var top = display.transform.parent.Find("Display Top").GetComponent<Renderer>().bounds;
            Require(bounds.min.y >= top.max.y + .015f && bounds.size.y >= .35f && bounds.size.y <= .60f,
                "Photo display has saved 0.45m study height and plinth clearance", "model=" + bounds + "; top=" + top + "; clearance=" + (bounds.min.y - top.max.y));
        }
        static IEnumerator Languages()
        {
            string central = CentralState(); var rotation = display.Model.localRotation;
            var expected = new Dictionary<string, string> {
                { "Photo study title", "exhibit.photo1988.title" }, { "Photo study boundary", "exhibit.photo1988.boundary" },
                { "Turn photo helmet left label", "text.b4b2cfcf7329" }, { "Reset photo helmet label", "text.7ef2fad58d1f" }, { "Turn photo helmet right label", "text.75cf34e2bbba" } };
            foreach (var choice in new[] { LanguageButton.Choice.English, LanguageButton.Choice.Chinese, LanguageButton.Choice.TraditionalChinese })
            {
                var button = UnityEngine.Object.FindObjectsOfType<LanguageButton>().Single(b => b.Action == choice);
                var target = button.GetComponent<XRSimpleInteractable>(); var hit = target.colliders.First();
                var position = hit.bounds.center - button.transform.forward * .55f; var facing = Quaternion.LookRotation(hit.bounds.center - position);
                int events = 0; UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = a => { if (a.interactorObject is XRRayInteractor) events++; };
                target.selectEntered.AddListener(listener);
                try
                {
                    QueueController(rightDevice, position, facing, false); yield return Wait(.25f);
                    Require(FirstHit(target) && target.IsHovered(rightRay), "Production ray reaches saved language button / " + choice, InputDetail(target), "production-input");
                    QueueController(rightDevice, position, facing, true); yield return Wait(.2f);
                    QueueController(rightDevice, position, facing, false); yield return Wait(.2f); ParkHands(); yield return Wait(.2f);
                    var expectedLanguage = choice == LanguageButton.Choice.Chinese ? MuseumLanguage.Chinese : choice == LanguageButton.Choice.TraditionalChinese ? MuseumLanguage.TraditionalChinese : MuseumLanguage.English;
                    Require(events == 1 && language.Current == expectedLanguage, "Real language input selects requested channel exactly once / " + choice, "events=" + events, "production-input");
                }
                finally { target.selectEntered.RemoveListener(listener); }
                foreach (var pair in expected)
                {
                    var label = display.transform.Find(pair.Key).GetComponent<TMP_Text>(); var binding = label.GetComponent<LocalizedText>();
                    label.ForceMeshUpdate(true, true);
                    Require(binding && binding.Key == pair.Value && label.text == table.Get(pair.Value, language.Current) && label.font == table.FontAsset && !label.GetComponent<LocalizedSourceText>(),
                        "Exact static key, text and saved font / " + language.Current + "/" + pair.Key, label.text);
                    string plain = Regex.Replace(label.text, "<[^>]*>", "");
                    Require(label.gameObject.activeInHierarchy && label.GetComponent<Renderer>().enabled && !label.isTextOverflowing && !label.isTextTruncated &&
                        plain.Where(c => !char.IsWhiteSpace(c)).Distinct().All(c => label.font.HasCharacter(c, false, false)),
                        "Saved glyphs and full untruncated label / " + language.Current + "/" + pair.Key,
                        "fontSize=" + label.fontSize + "; overflow=" + label.isTextOverflowing + "; truncated=" + label.isTextTruncated + "; rect=" + label.rectTransform.sizeDelta);
                    var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                    foreach (var point in corners.Concat(new[] { label.GetComponent<Renderer>().bounds.center }))
                    {
                        var local = Quaternion.Inverse(visitorRotation) * (point - visitorPosition);
                        float vertical = Mathf.Tan(55f * .5f * Mathf.Deg2Rad);
                        var viewport = new Vector3(.5f + local.x / (2 * local.z * vertical * (4f / 3f)), .5f + local.y / (2 * local.z * vertical), local.z);
                        Require(viewport.z > .02f && viewport.x >= .02f && viewport.x <= .98f && viewport.y >= .02f && viewport.y <= .98f,
                            "Whole label rectangle fits fixed diagnostic camera / " + language.Current + "/" + pair.Key, "viewport=" + viewport.ToString("F6"));
                        var delta = point - visitorPosition;
                        var blockers = Physics.RaycastAll(visitorPosition, delta.normalized, Mathf.Max(0, delta.magnitude - .002f), ~0, QueryTriggerInteraction.Ignore).Where(h => SceneSolid(h.collider)).ToArray();
                        Require(blockers.Length == 0, "Visitor-side line of sight to label / " + language.Current + "/" + pair.Key, string.Join(",", blockers.Select(h => h.collider.name)));
                    }
                }
                Require(CentralState() == central && Quaternion.Angle(rotation, display.Model.localRotation) < .01f, "Language change preserves both exhibit states / " + language.Current, central);
                Capture("labels-" + language.Current, "Fixed visitor-side diagnostic camera, actual current display and five localized labels; no camera or product asset is saved.", visitorPosition, visitorRotation, 55);
            }
        }

        static void SetupInput()
        {
            foreach (var simulator in UnityEngine.Object.FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>()) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard); if (addedMouse) InputSystem.EnableDevice(mouse);
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Left Controller");
            rightRay = rightController.GetComponentsInChildren<XRRayInteractor>(true).First(); leftRay = leftController.GetComponentsInChildren<XRRayInteractor>(true).First();
            foreach (var ray in rightController.GetComponentsInChildren<XRRayInteractor>(true).Concat(leftController.GetComponentsInChildren<XRRayInteractor>(true))) RayStates[ray] = ray.enabled;
            rightDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            head = InputSystem.AddDevice<XRSimulatedHMD>(); var camera = Camera.main; var parent = camera.transform.parent;
            Vector3 position = parent.InverseTransformPoint(camera.transform.position); Quaternion rotation = Quaternion.Inverse(parent.rotation) * camera.transform.rotation;
            var state = new XRSimulatedHMDState { isTracked = true, trackingState = 3, devicePosition = position, centerEyePosition = position,
                leftEyePosition = position + rotation * Vector3.left * .032f, rightEyePosition = position + rotation * Vector3.right * .032f,
                deviceRotation = rotation, centerEyeRotation = rotation, leftEyeRotation = rotation, rightEyeRotation = rotation };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(head, state)); ParkHands();
        }
        static void SetRays(bool enabled) { foreach (var item in RayStates) if (item.Key) item.Key.enabled = enabled; }
        static void RestoreRays() { foreach (var item in RayStates) if (item.Key) item.Key.enabled = item.Value; }
        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip)
        {
            var parent = (device == leftDevice ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(world), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation, grip = grip ? 1 : 0 }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        { QueueController(rightDevice, new Vector3(.7f, .8f, 1), Quaternion.identity, false); QueueController(leftDevice, new Vector3(-.7f, .8f, 1), Quaternion.identity, false); }
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        static void Capture(string name, string purpose, Vector3 position, Quaternion rotation, float fieldOfView)
        {
            string file = name + ".png";
            Museum.Diegetic.Editor.HelmetDiegeticPreview.CaptureView(Path.Combine(Output, file), position, rotation, fieldOfView);
            report.captures.Add(new CaptureRecord { file = file, purpose = purpose, position = position, rotation = rotation, fieldOfView = fieldOfView });
        }
        static void Require(bool passed, string name, string detail, string layer = "saved-state")
        { report.checks.Add(new Check { passed = passed, name = name, detail = detail, layer = layer }); if (!passed) throw new InvalidOperationException(name + ": " + detail); }
        static void OnLog(string message, string stack, LogType type)
        {
            if (report == null) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + message + "\n" + stack);
            else if (type == LogType.Warning) report.warnings.Add(message);
        }
        static void Finish(string failure, bool exitPlay = true)
        {
            if (finishing) return; finishing = true;
            RestoreRays(); if (display) { display.gameObject.SetActive(true); display.enabled = true; } if (station) station.enabled = true;
            if (buttons != null) foreach (var button in buttons) if (button) button.enabled = true;
            Application.logMessageReceived -= OnLog; InputSystem.onBeforeUpdate -= FlushInput; PendingInput.Clear(); Routines.Clear();
            foreach (var file in report.files) report.checks.Add(new Check { name = "Validator preserved saved source / " + file.path, passed = Hash(file.path) == file.sha256, detail = Hash(file.path), layer = "source-integrity" });
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, head, keyboard, addedMouse ? mouse : null }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedSettings) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; }
            report.failure = failure ?? ""; report.finishedUtc = DateTime.UtcNow.ToString("o");
            report.status = failure == null && report.errors.Count == 0 && report.checks.All(c => c.passed) ? "passed" : "failed";
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            SessionState.SetInt(Prefix + "Exit", report.status == "passed" ? 0 : 1);
            Debug.Log("PHOTO_GALLERY_QA_" + report.status.ToUpperInvariant() + " " + Path.Combine(Output, "report.json") + (failure == null ? "" : "\n" + failure));
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
                    if (!process.WaitForExit(5000) || !stdout.Wait(1000) || !stderr.Wait(1000)) return "UNAVAILABLE: git timeout";
                    return process.ExitCode == 0 ? stdout.Result.Trim() : "UNAVAILABLE: " + stderr.Result.Trim();
                }
            }
            catch (Exception exception) { return "UNAVAILABLE: " + exception.Message; }
        }
    }
}
#endif
