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
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using InputDevice = UnityEngine.InputSystem.InputDevice;

namespace Museum.PhysicalHands.EditorTools
{
    /// <summary>Runs isolated geometry/API checks and separate production-input checks without saving assets.</summary>
    [InitializeOnLoad]
    public static class MuseumPhysicalHandsValidation
    {
        const string Prefix = "Museum.PhysicalHandsQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static GameObject fixtureRoot;
        static XRSimulatedController leftDevice, rightDevice;
        static XRSimulatedHMD hmd;
        static Keyboard keyboard;
        static Mouse addedMouse;
        static ActionBasedController leftController, rightController;
        static MuseumPhysicalHand leftHand, rightHand;
        static XRGrabInteractable helmet;
        static HelmetExhibitReset reset;
        static HelmetInspectionController inspection;
        static HelmetWindowInteractable window;
        static XRDeviceSimulator[] simulators;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static bool changedInput, finishing;
        static double started, exitAt;
        static int lastFrame = -1, pendingExit = -1;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => SessionState.GetString(Prefix + "Output", "");

        [Serializable] sealed class Check { public string kind, name, detail; public bool passed; }
        [Serializable] sealed class Source { public string path, sha256; }
        [Serializable] sealed class Sample
        {
            public string name;
            public Vector3 target, proxy, objectPosition, objectScale;
            public Quaternion objectRotation;
            public float trackingError, maximumPenetration;
            public bool blocked, tracking;
            public int selectors;
        }
        [Serializable] sealed class Report
        {
            public string status = "running", failure = "", utc, commit, branch, dirty, unity, environment;
            public string method = "Saved MuseumStart integration; isolated swept kinematic glove envelope and XRInteractionManager API fixtures; explicit root/window/section ownership and relative-pose checks; separate XRSimulatedController Grip -> saved input actions -> ActionBasedController -> saved physical hand/direct interactor -> helmet/window. Home uses synthetic keyboard input. No scene saves.";
            public string limits = "Desktop synthetic inputs and explicit API fixtures only. Not real headset/controller tracking, finger tracking, hand comfort, perceived weight, stereo visual acceptance, device performance, accessibility/user research, or an executable build. Collision checks use the simplified box glove envelope spanning the palm and preset finger space, not individual articulated finger colliders. They cover the recorded wall/table fixtures and exercised saved exhibit paths, not absolute nonpenetration for every geometry/speed. API collaboration fixtures use authored local poses and synchronous manager calls; they do not establish physical input usability. Hidden batch frames are paced near 90 Hz; elapsed time is harness time, not a performance benchmark.";
            public float durationSeconds;
            public List<Check> checks = new List<Check>();
            public List<Source> sources = new List<Source>();
            public List<Sample> samples = new List<Sample>();
            public List<string> warnings = new List<string>(), errors = new List<string>();
        }

        static MuseumPhysicalHandsValidation()
        {
            EditorApplication.playModeStateChanged += Mode;
            EditorApplication.update += Tick;
        }

        [MenuItem("Museum/Physical Hands/Validate saved integration (synthetic Play Mode)")]
        public static void RunInEditor() => Begin(false);
        // Batch invocation must omit -quit; exit occurs after report writing and return to Edit Mode.
        public static void RunBatch() => Begin(true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("Physical hands QA keyboard");
            if (Mouse.current == null) addedMouse = InputSystem.AddDevice<Mouse>("Physical hands QA mouse");
        }

        static void Begin(bool batch)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before physical hands validation.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or discard your existing scene edits before validation; this runner does not save scenes.");
            pendingExit = -1;
            var snapshot = new Report
            {
                utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), branch = Git("branch --show-current"),
                dirty = Git("-c core.quotepath=false status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion,
                environment = SystemInfo.operatingSystem + "; " + SystemInfo.processorType + "; " + SystemInfo.graphicsDeviceName
            };
            CaptureSources(snapshot);
            string run = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-physical-hands-" + Guid.NewGuid().ToString("N");
            string outputBase = Environment.GetEnvironmentVariable("MUSEUM_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputBase)) outputBase = Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs");
            string output = Path.Combine(Path.GetFullPath(outputBase), run);
            Directory.CreateDirectory(output);
            SessionState.SetString(Prefix + "Output", output);
            SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(snapshot));
            SessionState.SetBool(Prefix + "Active", true);
            SessionState.SetBool(Prefix + "Batch", batch);
            SessionState.SetInt(Prefix + "Exit", -1);
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        static void Mode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                started = EditorApplication.timeSinceStartup;
                finishing = false; lastFrame = -1; changedInput = false;
                Routines.Clear(); PendingInput.Clear();
                Application.logMessageReceived += Log;
                InputSystem.onBeforeUpdate += FlushInput;
                Routines.Push(Validate());
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && !finishing && report != null)
                Finish("Play Mode exited before the validation completed.", false);
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Prefix + "Active", false);
                if (SessionState.GetBool(Prefix + "Batch", false))
                {
                    int code = SessionState.GetInt(Prefix + "Exit", 1);
                    // Let the Play-to-Edit transition and native XR teardown settle before exit.
                    // This follows the existing tracking/height QA delayed-exit harness.
                    EditorApplication.delayCall += () => { pendingExit = code < 0 ? 1 : code; exitAt = EditorApplication.timeSinceStartup + 20; };
                }
            }
        }

        static void Tick()
        {
            if (pendingExit >= 0 && EditorApplication.timeSinceStartup >= exitAt)
            {
                int code = pendingExit; pendingExit = -1; EditorApplication.Exit(code); return;
            }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || finishing || report == null || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            // XRI 2.6 skips throw samples with sub-millisecond deltaTime in unpaced hidden batch runs.
            if (Application.isBatchMode) System.Threading.Thread.Sleep(11);
            try
            {
                if (EditorApplication.timeSinceStartup - started > 150) throw new TimeoutException("Physical hands QA exceeded 150 seconds.");
                while (Routines.Count > 0)
                {
                    var current = Routines.Peek();
                    if (!current.MoveNext()) { Routines.Pop(); continue; }
                    if (current.Current is IEnumerator child) { Routines.Push(child); continue; }
                    return;
                }
                Finish(null);
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        static IEnumerator Validate()
        {
            yield return Wait(.5f);
            var displays = new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(displays);
            Require(!displays.Any(d => d.running) && !InputSystem.devices.Any(d => d is XRHMD && !(d is XRSimulatedHMD)),
                "environment", "no physical XR session is modified", "Run this synthetic validator without a connected headset.");
            Require(SceneManager.GetActiveScene().path == ScenePath, "saved integration", "saved MuseumStart loaded", SceneManager.GetActiveScene().path);
            yield return InspectSavedIntegration();
            yield return CollisionFixtures();
            yield return CollaborationApiFixtures();
            yield return PrepareProductionInputs();
            yield return ProductionGrabAndTwoHands();
            yield return ProductionHeldWindowAndReset();
        }

        static IEnumerator InspectSavedIntegration()
        {
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).SingleOrDefault(c => c.name == "Left Controller");
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).SingleOrDefault(c => c.name == "Right Controller");
            var hands = UnityEngine.Object.FindObjectsOfType<MuseumPhysicalHand>(true);
            Require(leftController && rightController && hands.Length == 2, "saved integration", "two saved controller targets and physical hands", "hand count=" + hands.Length);
            leftHand = hands.SingleOrDefault(h => h.DirectInteractor && h.DirectInteractor.xrController == leftController);
            rightHand = hands.SingleOrDefault(h => h.DirectInteractor && h.DirectInteractor.xrController == rightController);
            Require(leftHand && rightHand && leftHand != rightHand, "saved integration", "each physical hand retains its original controller input reference");
            foreach (var hand in hands)
            {
                var controller = hand == leftHand ? leftController : rightController;
                var direct = hand.DirectInteractor;
                Require(hand.Body && hand.Body.isKinematic && hand.CollisionCollider is BoxCollider && !hand.CollisionCollider.isTrigger && direct && direct.xrController == controller,
                    "saved integration", controller.name + " owns a kinematic glove envelope and original-input direct interactor");
                Require(hand.TrackingTarget && hand.TrackingTarget != hand.transform && !hand.transform.IsChildOf(hand.TrackingTarget) && hand.TrackingTarget != direct.transform && !hand.TrackingTarget.IsChildOf(direct.transform),
                    "saved integration", controller.name + " separates tracked target from collision proxy");
                Require(direct.transform.IsChildOf(controller.transform), "saved integration", controller.name + " retains the saved direct-interactor controller hierarchy");
                var visual = hand.GetComponentInChildren<MuseumHandVisual>(true);
                Require(visual && visual.JointCount == 15 && visual.FingerCount == 5 && visual.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled),
                    "saved integration", controller.name + " has saved articulated glove geometry");
            }
            Require(leftHand.DirectInteractor.containingGroup == null || leftHand.DirectInteractor.containingGroup != rightHand.DirectInteractor.containingGroup,
                "saved integration", "left and right direct interactors do not share an exclusive interaction group");
            inspection = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            Require(inspection, "saved integration", "inspection exhibit exists");
            helmet = inspection.GetComponent<XRGrabInteractable>(); reset = inspection.GetComponent<HelmetExhibitReset>();
            window = inspection.GetComponentInChildren<HelmetWindowInteractable>(true);
            Require(helmet && reset && window && helmet.selectMode == InteractableSelectMode.Multiple && helmet.useDynamicAttach && !helmet.trackScale,
                "saved integration", "helmet supports multiple dynamic grips with scaling disabled");
            Require(helmet.movementType == XRBaseInteractable.MovementType.VelocityTracking && !helmet.addDefaultGrabTransformers && helmet.GetComponent<MuseumHandGrabTransformer>(),
                "saved integration", "helmet has one XRI physics movement owner and the museum grab transformer");
            Require(helmet.GetComponents<Joint>().Length == 0, "saved integration", "helmet has no competing root joint driver");
            yield return null;
        }

        static IEnumerator CollisionFixtures()
        {
            // XRI requires a valid Unity physics scene. HideAndDontSave can detach a
            // hierarchy from that scene, so use ordinary temporary Play Mode objects.
            fixtureRoot = new GameObject("Physical hands QA isolated fixtures");
            Vector3 origin = new Vector3(100f, 1f, 100f);
            var manager = leftHand.DirectInteractor.interactionManager;
            Require(manager && manager.isActiveAndEnabled, "geometry fixture", "isolated fixture uses the existing production interaction manager");
            var targetObject = new GameObject("Fixture tracked target"); targetObject.transform.SetParent(fixtureRoot.transform); targetObject.transform.position = origin;
            var controller = targetObject.AddComponent<ActionBasedController>(); controller.enableInputTracking = false; controller.enableInputActions = false;
            var proxy = new GameObject("Fixture physical palm"); proxy.transform.SetParent(fixtureRoot.transform); proxy.transform.position = origin;
            var hand = proxy.AddComponent<MuseumPhysicalHand>();
            var directObject = new GameObject("Fixture direct interactor"); directObject.SetActive(false); directObject.transform.SetParent(targetObject.transform, false);
            var trigger = directObject.AddComponent<SphereCollider>(); trigger.radius = .15f; trigger.isTrigger = true;
            var queryBody = directObject.AddComponent<Rigidbody>(); queryBody.isKinematic = true; queryBody.useGravity = false;
            var direct = directObject.AddComponent<XRDirectInteractor>(); direct.interactionManager = manager; direct.xrController = controller;
            direct.selectActionTrigger = XRBaseControllerInteractor.InputTriggerType.State; direct.hideControllerOnSelect = false;
            hand.Configure(targetObject.transform, controller, direct, null, true); hand.SetTrackingOverride(true);
            // Only this fixture driver is stepped manually; the production components stay enabled.
            hand.enabled = false; directObject.SetActive(true);
            Require(directObject.scene.IsValid() && direct.xrController == controller && direct.isActiveAndEnabled,
                "geometry fixture", "query has a valid physics scene and controller parent before contacts are collected");
            var wall = Box("Fixture thin wall", origin + Vector3.forward * .7f, new Vector3(2f, 2f, .04f));
            var table = Box("Fixture table", origin + Vector3.down * .45f, new Vector3(2f, .04f, 2f));
            Require(hand.Body && hand.CollisionCollider is BoxCollider && !hand.CollisionCollider.isTrigger, "geometry fixture", "fixture owns a measurable solid glove envelope");
            // Establish continuous tracking before moving the target across the wall. Initial recovery
            // may intentionally reanchor a palm at a clear target and is a different behavior.
            hand.StepPhysics(Time.fixedDeltaTime); yield return FixedSteps(2);
            targetObject.transform.position = origin + Vector3.forward * 1.3f;
            float maximumPenetration = 0;
            for (int step = 0; step < 36; step++)
            {
                hand.StepPhysics(Time.fixedDeltaTime); yield return FixedSteps(1);
                maximumPenetration = Mathf.Max(maximumPenetration, Penetration(hand.CollisionCollider, wall));
            }
            Require(hand.IsBlocked && hand.TrackingError > hand.CollisionRadius && maximumPenetration < .002f && hand.Body.position.z < wall.bounds.min.z,
                "geometry fixture", "rapid target crossing stops the complete simplified glove envelope before a thin wall", "penetration=" + maximumPenetration + "; error=" + hand.TrackingError);
            Record("wall blocked after rapid target move", hand, null, maximumPenetration);
            targetObject.transform.position = origin + new Vector3(.15f, 0, .1f);
            yield return StepFixture(hand, 36);
            Require(!hand.IsBlocked && Vector3.Distance(hand.Body.position, targetObject.transform.position) < .03f,
                "geometry fixture", "return to clear space recovers the palm without stale wall blocking");
            targetObject.transform.position = origin + Vector3.down;
            maximumPenetration = 0;
            for (int step = 0; step < 36; step++)
            {
                hand.StepPhysics(Time.fixedDeltaTime); yield return FixedSteps(1);
                maximumPenetration = Mathf.Max(maximumPenetration, Penetration(hand.CollisionCollider, table));
            }
            Require(hand.IsBlocked && maximumPenetration < .002f && hand.Body.position.y > table.bounds.max.y,
                "geometry fixture", "simplified glove envelope is blocked above the tabletop", "penetration=" + maximumPenetration);
            targetObject.transform.position = origin; yield return StepFixture(hand, 36);
            Vector3 beforeLost = hand.Body.position;
            hand.SetTrackingOverride(false); targetObject.transform.position = origin + Vector3.left * .25f;
            yield return StepFixture(hand, 4);
            Require(!hand.IsTrackingValid && Vector3.Distance(hand.Body.position, beforeLost) < .002f,
                "geometry fixture", "lost tracking freezes the collision proxy");
            hand.SetTrackingOverride(true); yield return StepFixture(hand, 36);
            Require(hand.IsTrackingValid && Vector3.Distance(hand.Body.position, targetObject.transform.position) < .03f,
                "geometry fixture", "tracking recovery returns to the valid target");
            Record("fixture tracking recovered", hand, null);

            // Both endpoint poses are clear, but the glove corner crosses this thin
            // obstacle halfway through a 20-degree turn. Endpoint-only tests miss it.
            Vector3 turnAnchor = hand.Body.position;
            hand.Body.rotation = Quaternion.identity;
            hand.transform.SetPositionAndRotation(turnAnchor, Quaternion.identity);
            targetObject.transform.SetPositionAndRotation(turnAnchor, Quaternion.identity);
            var turnObstacle = Box("Fixture thin rotation obstacle", turnAnchor + new Vector3(.111f, -.01f, .106f), Vector3.one * .002f);
            Physics.SyncTransforms();
            bool startClear = Penetration(hand.CollisionCollider, turnObstacle) < .0001f;
            hand.transform.rotation = Quaternion.Euler(0, 20f, 0);
            Physics.SyncTransforms();
            bool endClear = Penetration(hand.CollisionCollider, turnObstacle) < .0001f;
            hand.Body.rotation = Quaternion.identity; hand.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            Require(startClear && endClear, "geometry fixture", "thin rotation obstacle has clear starting and ending poses");
            targetObject.transform.rotation = Quaternion.Euler(0, 20f, 0);
            yield return StepFixture(hand, 1);
            float turn = Quaternion.Angle(Quaternion.identity, hand.Body.rotation);
            Require(hand.IsBlocked && turn < 15f && Penetration(hand.CollisionCollider, turnObstacle) < .0001f,
                "geometry fixture", "rotation path stops before a thin obstacle even when the destination pose is clear", "turn=" + turn);
            Record("rotation path blocked", hand, null, Penetration(hand.CollisionCollider, turnObstacle));
            UnityEngine.Object.DestroyImmediate(turnObstacle.gameObject);
            targetObject.transform.rotation = Quaternion.identity;
            yield return StepFixture(hand, 4);

            // Explicit API fixture: proximity and select filters are checked before SelectEnter.
            var cubeObject = Box("Fixture grabbable cube", origin + Vector3.left * .25f + Vector3.forward * .35f, Vector3.one * .16f).gameObject;
            cubeObject.SetActive(false);
            var cubeBody = cubeObject.AddComponent<Rigidbody>(); cubeBody.useGravity = false;
            cubeBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var cube = cubeObject.AddComponent<XRGrabInteractable>(); cube.interactionManager = manager;
            cube.useDynamicAttach = true; cube.matchAttachPosition = true; cube.matchAttachRotation = true;
            cube.reinitializeDynamicAttachEverySingleGrab = true; cube.selectMode = InteractableSelectMode.Multiple;
            cube.movementType = XRBaseInteractable.MovementType.VelocityTracking; cube.trackScale = false; cube.addDefaultGrabTransformers = false;
            var transformer = cubeObject.AddComponent<MuseumHandGrabTransformer>();
            cube.startingSingleGrabTransformers.Add(transformer); cube.startingMultipleGrabTransformers.Add(transformer);
            cubeObject.SetActive(true);
            Physics.SyncTransforms();
            Require(Penetration(hand.CollisionCollider, cubeObject.GetComponent<Collider>()) < .0001f,
                "API fixture", "cube starts clear of the recovered glove envelope before approaching");
            targetObject.transform.position = cubeObject.transform.position + Vector3.back * (.08f + EnvelopeSupport(hand, Vector3.forward) + .004f);
            yield return StepFixture(hand, 24);
            yield return FixedSteps(2);
            // Restore the runtime filter/listeners for the API ownership check after the manually
            // stepped geometry fixtures; no further valid-target manual physics steps are run.
            hand.enabled = true;
            var candidates = new List<IXRInteractable>(); direct.GetValidTargets(candidates);
            var nearest = cubeObject.GetComponent<Collider>().ClosestPoint(direct.transform.position);
            string candidateDetail = "candidates=" + candidates.Count + "; query=" + direct.transform.position + "; cube=" + cubeObject.transform.position +
                "; target=" + targetObject.transform.position + "; proxy=" + hand.Body.position + "; sceneValid=" + directObject.scene.IsValid() +
                "; queryEnabled=" + direct.isActiveAndEnabled + "; controller=" + (direct.xrController ? direct.xrController.name : "missing") +
                "; queryToCube=" + Vector3.Distance(direct.transform.position, nearest) + "; hullGap=" + Vector3.Distance(hand.CollisionCollider.ClosestPoint(nearest), nearest);
            Require(candidates.Contains(cube) && manager.IsSelectPossible(direct, cube), "API fixture", "cube is a nearby valid selection before explicit manager selection", candidateDetail);
            manager.SelectEnter((IXRSelectInteractor)direct, cube);
            Require(cube.IsSelected(direct), "API fixture", "explicit manager selection is recorded as an API check");
            controller.currentControllerState.selectInteractionState.SetFrameState(true);
            Vector3 pushStart = targetObject.transform.position;
            Vector3 pushEnd = origin + Vector3.left * .25f + Vector3.forward * .82f;
            float pushStarted = Time.realtimeSinceStartup, heldPenetration = 0;
            while (Time.realtimeSinceStartup - pushStarted < .8f)
            {
                targetObject.transform.position = Vector3.Lerp(pushStart, pushEnd, (Time.realtimeSinceStartup - pushStarted) / .8f);
                heldPenetration = Mathf.Max(heldPenetration, Penetration(cubeObject.GetComponent<Collider>(), wall));
                yield return null;
            }
            targetObject.transform.position = pushEnd;
            yield return Wait(.25f);
            heldPenetration = Mathf.Max(heldPenetration, Penetration(cubeObject.GetComponent<Collider>(), wall));
            Require(cube.IsSelected(direct) && cubeBody.position.z < wall.bounds.min.z && heldPenetration < .005f && Finite(cubeBody.velocity),
                "API fixture", "held velocity-tracked object stays on the near side of a thin wall", "penetration=" + heldPenetration + "; cube=" + cubeBody.position + "; target=" + pushEnd);
            Record("held object wall blocked", hand, cube, heldPenetration);
            hand.SetTrackingOverride(false); hand.StepPhysics(Time.fixedDeltaTime);
            Require(!cube.isSelected && !direct.hasSelection, "API fixture", "tracking loss releases the selecting interactor");
            UnityEngine.Object.DestroyImmediate(fixtureRoot); fixtureRoot = null;
            yield return null;
        }

        static IEnumerator PrepareProductionInputs()
        {
            simulators = UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>();
            Require(simulators.Length == 1, "production input", "official desktop simulator is present before the independent input writer", "count=" + simulators.Length);
            foreach (var simulator in simulators) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedInput = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            leftDevice = InputSystem.AddDevice<XRSimulatedController>("Physical hands QA left"); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            rightDevice = InputSystem.AddDevice<XRSimulatedController>("Physical hands QA right"); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            hmd = InputSystem.AddDevice<XRSimulatedHMD>("Physical hands QA head");
            QueueHead();
            QueueController(leftDevice, new Vector3(-.75f, 1.4f, 2f), Quaternion.identity, false);
            QueueController(rightDevice, new Vector3(.75f, 1.4f, 2f), Quaternion.identity, false);
            yield return Wait(.7f);
            Require(leftHand.IsTrackingValid && rightHand.IsTrackingValid && leftController.selectAction.action != null && leftController.selectAction.action.enabled && rightController.selectAction.action != null && rightController.selectAction.action.enabled,
                "production input", "synthetic device poses resolve through enabled saved controller actions");
            Require(Vector3.Distance(leftHand.DirectInteractor.transform.position, leftHand.Body.position) < .005f && Vector3.Distance(rightHand.DirectInteractor.transform.position, rightHand.Body.position) < .005f,
                "production input", "saved direct-interactor query transforms follow collision proxies instead of raw tracked targets");
            Require(leftController.selectAction.action.controls.Any(c => c.device == leftDevice) && rightController.selectAction.action.controls.Any(c => c.device == rightDevice),
                "production input", "saved grip actions resolve the correct synthetic handed devices");
        }

        static IEnumerator CollaborationApiFixtures()
        {
            fixtureRoot = new GameObject("Physical hands QA ownership fixtures");
            var managerObject = new GameObject("Ownership fixture manager"); managerObject.SetActive(false); managerObject.transform.SetParent(fixtureRoot.transform);
            var manager = managerObject.AddComponent<XRInteractionManager>();
            // These state-machine fixtures are synchronous. The disabled manager cannot perform
            // an unrelated automatic input selection between the explicitly labelled API calls.
            manager.enabled = false;
            managerObject.SetActive(true);
            Vector3 origin = new Vector3(200f, 1f, 200f);
            var root = Box("Ownership fixture helmet", origin, Vector3.one * .4f).gameObject;
            root.SetActive(false);
            var body = root.AddComponent<Rigidbody>(); body.useGravity = true;
            var grab = root.AddComponent<XRGrabInteractable>(); grab.interactionManager = manager;
            grab.colliders.Clear(); grab.colliders.Add(root.GetComponent<BoxCollider>());
            grab.selectMode = InteractableSelectMode.Multiple; grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.useDynamicAttach = true; grab.trackScale = false; grab.throwOnDetach = false;
            var exhibitReset = root.AddComponent<HelmetExhibitReset>(); exhibitReset.ConfigureHomePose(origin, Quaternion.identity);
            var controller = root.AddComponent<HelmetInspectionController>();
            var pivot = new GameObject("Ownership fixture window pivot").transform; pivot.SetParent(root.transform, false); pivot.localPosition = new Vector3(0, .05f, -.21f);
            var paneObject = new GameObject("Ownership fixture window"); paneObject.transform.SetParent(pivot, false);
            var paneCollider = paneObject.AddComponent<BoxCollider>(); paneCollider.size = new Vector3(.10f, .10f, .015f);
            var pane = paneObject.AddComponent<HelmetWindowInteractable>(); pane.interactionManager = manager;
            pane.colliders.Clear(); pane.colliders.Add(paneCollider);
            var toolObject = new GameObject("Ownership fixture section guide"); toolObject.transform.SetParent(root.transform, false); toolObject.transform.localPosition = new Vector3(.26f, .02f, 0);
            var toolCollider = toolObject.AddComponent<BoxCollider>(); toolCollider.size = Vector3.one * .05f;
            var tool = toolObject.AddComponent<HelmetSectionInteractable>(); tool.interactionManager = manager;
            tool.colliders.Clear(); tool.colliders.Add(toolCollider);
            controller.Configure(pivot, new Renderer[0], new Renderer[0]); controller.ConfigureHandCollaboration(true);
            pane.Configure(controller); tool.Configure(controller);
            root.SetActive(true);
            var holder = ApiHand("Ownership holder", manager, origin + Vector3.left * .21f, out var holderRay);
            var operatorHand = ApiHand("Ownership operator", manager, origin + Vector3.back * .24f, out var operatorRay);
            foreach (bool section in new[] { false, true })
            foreach (bool childFirst in new[] { false, true })
            {
                string scenario = (section ? "section" : "window") + (childFirst ? " child then root" : " root then child");
                body.position = origin; body.rotation = Quaternion.identity; root.transform.SetPositionAndRotation(origin, Quaternion.identity);
                controller.SetWindowAngle(23f);
                controller.SetCutaway(section);
                if (section) { controller.SetSectionPlane(new Vector3(.4f, .7f, .1f).normalized, .04f); tool.ShowTool = true; }
                XRSimpleInteractable child = section ? (XRSimpleInteractable)tool : pane;
                holder.xrController.transform.SetPositionAndRotation(origin + Vector3.left * .21f, Quaternion.identity);
                operatorHand.xrController.transform.SetPositionAndRotation(child.colliders[0].bounds.center + Vector3.back * .015f, Quaternion.identity);
                Physics.SyncTransforms();
                Require(manager.IsSelectPossible(holder, grab) && manager.IsSelectPossible(operatorHand, child), "API collaboration", scenario + ": each authored hand can select its initial target");
                if (childFirst)
                {
                    manager.SelectEnter((IXRSelectInteractor)operatorHand, child);
                    Require(child.IsSelected(operatorHand) && body.isKinematic && !body.useGravity,
                        "API collaboration", scenario + ": child alone pins the unheld exhibit");
                    Require(!manager.IsSelectPossible(operatorRay, grab), "API collaboration", scenario + ": the child hand's ray cannot also select the whole helmet");
                    Require(manager.IsSelectPossible(holder, grab), "API collaboration", scenario + ": the other hand can grab an already pinned exhibit");
                    manager.SelectEnter((IXRSelectInteractor)holder, grab);
                }
                else
                {
                    manager.SelectEnter((IXRSelectInteractor)holder, grab);
                    Require(!manager.IsSelectPossible(holderRay, child), "API collaboration", scenario + ": the holder hand's ray cannot select its own child part");
                    Require(manager.IsSelectPossible(operatorHand, child), "API collaboration", scenario + ": the other hand can select a held exhibit part");
                    manager.SelectEnter((IXRSelectInteractor)operatorHand, child);
                }
                Require(grab.IsSelected(holder) && child.IsSelected(operatorHand) && !body.isKinematic,
                    "API collaboration", scenario + ": simultaneous root and child selection relinquishes the body pin");
                Require(!manager.IsSelectPossible(holderRay, child) && !manager.IsSelectPossible(operatorRay, grab),
                    "API collaboration", scenario + ": direct and ray references on either physical hand stay mutually exclusive across root and child");
                float angle = controller.WindowAngle;
                Vector3 localPoint = controller.SectionPointLocal, normal = controller.SectionNormalLocal;
                Vector3 oldPosition = root.transform.position; Quaternion oldRotation = root.transform.rotation;
                Vector3 holderPosition = holder.transform.position, childPosition = operatorHand.transform.position;
                Quaternion holderRotation = holder.transform.rotation, childRotation = operatorHand.transform.rotation;
                Vector3 newPosition = oldPosition + new Vector3(.2f, .1f, -.12f);
                Quaternion delta = Quaternion.Euler(13f, 25f, -9f);
                Quaternion newRotation = delta * oldRotation;
                body.position = newPosition; body.rotation = newRotation; root.transform.SetPositionAndRotation(newPosition, newRotation);
                holder.xrController.transform.SetPositionAndRotation(newPosition + delta * (holderPosition - oldPosition), delta * holderRotation);
                operatorHand.xrController.transform.SetPositionAndRotation(newPosition + delta * (childPosition - oldPosition), delta * childRotation);
                Physics.SyncTransforms(); child.ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase.Dynamic);
                if (section)
                    Require(Vector3.Distance(localPoint, controller.SectionPointLocal) < .001f && Vector3.Angle(normal, controller.SectionNormalLocal) < .1f,
                        "API collaboration", scenario + ": carrying both hands and helmet together preserves the local cutting plane",
                        "point drift=" + Vector3.Distance(localPoint, controller.SectionPointLocal) + "; normal drift=" + Vector3.Angle(normal, controller.SectionNormalLocal));
                else
                    Require(Mathf.Abs(angle - controller.WindowAngle) < .1f,
                        "API collaboration", scenario + ": carrying both hands and helmet together preserves the local window angle", "angle drift=" + Mathf.Abs(angle - controller.WindowAngle));
                manager.SelectExit((IXRSelectInteractor)holder, grab);
                Require(!grab.isSelected && child.IsSelected(operatorHand) && body.isKinematic && !body.useGravity,
                    "API collaboration", scenario + ": releasing the last root grip retains the child-owned pin");
                manager.SelectExit((IXRSelectInteractor)operatorHand, child);
                Require(!grab.isSelected && !child.isSelected && !body.isKinematic && body.useGravity,
                    "API collaboration", scenario + ": releasing every hand restores the original dynamic gravity body");
            }
            UnityEngine.Object.DestroyImmediate(fixtureRoot); fixtureRoot = null;
            yield return null;
        }

        static XRDirectInteractor ApiHand(string name, XRInteractionManager manager, Vector3 position, out XRRayInteractor ray)
        {
            var target = new GameObject(name); target.SetActive(false); target.transform.SetParent(fixtureRoot.transform); target.transform.position = position;
            var controller = target.AddComponent<ActionBasedController>(); controller.enableInputTracking = false; controller.enableInputActions = false;
            var state = controller.currentControllerState; state.isTracked = true;
            state.inputTrackingState = InputTrackingState.Position | InputTrackingState.Rotation;
            state.selectInteractionState.SetFrameState(true);
            var body = target.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var trigger = target.AddComponent<SphereCollider>(); trigger.radius = .04f; trigger.isTrigger = true;
            var direct = target.AddComponent<XRDirectInteractor>(); direct.xrController = controller; direct.interactionManager = manager;
            direct.hideControllerOnSelect = false; direct.selectActionTrigger = XRBaseControllerInteractor.InputTriggerType.State;
            var rayObject = new GameObject(name + " ray"); rayObject.transform.SetParent(target.transform, false);
            ray = rayObject.AddComponent<XRRayInteractor>(); ray.xrController = controller; ray.interactionManager = manager; ray.hideControllerOnSelect = false;
            target.SetActive(true);
            return direct;
        }

        static IEnumerator ProductionGrabAndTwoHands()
        {
            Vector3 initialScale = helmet.transform.localScale;
            Vector3 touch = RootTouch(leftHand, Vector3.left);
            QueueController(leftDevice, touch, Quaternion.identity, false); yield return Wait(.5f);
            Require(NearCandidate(leftHand, helmet), "production input", "left palm hovers the whole helmet from its external surface", HandDetail(leftHand));
            QueueController(leftDevice, touch, Quaternion.identity, true); yield return Wait(.25f);
            Require(helmet.interactorsSelecting.Count == 1 && helmet.IsSelected(leftHand.DirectInteractor), "production input", "grip selects the saved helmet through the left physical direct interactor");
            Vector3 beforeLift = helmet.transform.position;
            touch += Vector3.up * .22f;
            yield return MoveController(leftDevice, leftController.transform.position, touch, true, .45f);
            yield return Wait(.2f);
            Require(helmet.transform.position.y > beforeLift.y + .12f && !helmet.GetComponent<Rigidbody>().isKinematic,
                "production input", "one-hand lift moves the actual dynamic helmet");
            Record("one hand held", leftHand, helmet);
            Vector3 rightTouch = RootTouch(rightHand, Vector3.right);
            QueueController(rightDevice, rightTouch, Quaternion.identity, false); yield return Wait(.5f);
            Require(NearCandidate(rightHand, helmet), "production input", "second palm reaches a valid helmet surface", HandDetail(rightHand));
            Vector3 beforeSecond = helmet.transform.position;
            QueueController(rightDevice, rightTouch, Quaternion.identity, true); yield return Wait(.2f);
            Require(helmet.interactorsSelecting.Count == 2 && helmet.IsSelected(leftHand.DirectInteractor) && helmet.IsSelected(rightHand.DirectInteractor) && Vector3.Distance(beforeSecond, helmet.transform.position) < .08f,
                "production input", "one to two hands preserves grip pose without a large position jump", "jump=" + Vector3.Distance(beforeSecond, helmet.transform.position) +
                "; selectors=" + string.Join(",", helmet.interactorsSelecting.Select(i => i.transform.name)) +
                "; left=" + HandDetail(leftHand) + "; right=" + HandDetail(rightHand) +
                "; right selected=" + string.Join(",", rightHand.DirectInteractor.interactablesSelected.Select(i => i.transform.name)) +
                "; right grip=" + rightController.currentControllerState.selectInteractionState.value);
            Quaternion beforeTurn = helmet.transform.rotation;
            rightTouch += Vector3.up * .14f + Vector3.right * .08f;
            yield return MoveController(rightDevice, rightController.transform.position, rightTouch, true, .4f); yield return Wait(.15f);
            Require(Quaternion.Angle(beforeTurn, helmet.transform.rotation) > 3f && Vector3.Distance(initialScale, helmet.transform.localScale) < .0001f,
                "production input", "two-hand motion rotates the helmet without changing its scale", "turn=" + Quaternion.Angle(beforeTurn, helmet.transform.rotation));
            Record("two hands held", rightHand, helmet);
            Vector3 beforeRelease = helmet.transform.position; Quaternion rotationBeforeRelease = helmet.transform.rotation;
            QueueController(rightDevice, rightTouch, Quaternion.identity, false); yield return Wait(.15f);
            Require(helmet.interactorsSelecting.Count == 1 && helmet.IsSelected(leftHand.DirectInteractor) && Vector3.Distance(beforeRelease, helmet.transform.position) < .06f && Quaternion.Angle(rotationBeforeRelease, helmet.transform.rotation) < 10f,
                "production input", "two to one hand keeps the current pose", "position jump=" + Vector3.Distance(beforeRelease, helmet.transform.position) + "; rotation jump=" + Quaternion.Angle(rotationBeforeRelease, helmet.transform.rotation));
            QueueController(leftDevice, touch, Quaternion.identity, false); yield return Wait(.25f);
            var body = helmet.GetComponent<Rigidbody>();
            Require(!helmet.isSelected && !body.isKinematic && body.useGravity && Finite(body.velocity) && Finite(body.angularVelocity),
                "production input", "final grip release restores an unselected finite dynamic body");
            Require(Vector3.Distance(initialScale, helmet.transform.localScale) < .0001f, "production input", "scale remains unchanged through 1 to 2 to 1 to 0 hands");
        }

        static IEnumerator ProductionHeldWindowAndReset()
        {
            // Known-state fixture cleanup is labelled API; subsequent manipulation/reset use actual saved inputs.
            inspection.ResetInspection(); yield return Wait(.3f);
            Require(!helmet.isSelected && !window.isSelected && inspection.WindowAngle < .1f, "API cleanup", "public reset establishes the closed display state");
            Vector3 touch = RootTouch(leftHand, Vector3.left);
            QueueController(leftDevice, touch, Quaternion.identity, false); yield return Wait(.45f);
            Require(NearCandidate(leftHand, helmet), "production input", "left palm can reapproach the reset exhibit");
            QueueController(leftDevice, touch, Quaternion.identity, true); yield return Wait(.2f);
            Require(helmet.IsSelected(leftHand.DirectInteractor), "production input", "left hand holds the helmet for window manipulation");
            Vector3 windowTouch = WindowTouch(rightHand);
            QueueController(rightDevice, windowTouch, Quaternion.identity, false); yield return Wait(.5f);
            Require(NearCandidate(rightHand, window), "production input", "free right palm can select the window while the other hand holds the helmet", HandDetail(rightHand));
            QueueController(rightDevice, windowTouch, Quaternion.identity, true); yield return Wait(.12f);
            Require(window.IsSelected(rightHand.DirectInteractor) && helmet.IsSelected(leftHand.DirectInteractor) && helmet.interactorsSelecting.Count == 1 && !helmet.GetComponent<Rigidbody>().isKinematic,
                "production input", "held window uses the free hand without stealing the helmet or pinning its rigidbody");
            QueueController(rightDevice, windowTouch, Quaternion.identity, false); yield return Wait(.8f);
            Require(!window.isSelected && helmet.IsSelected(leftHand.DirectInteractor) && Mathf.Abs(inspection.WindowAngle - HelmetInspectionController.MaximumWindowAngle) < .5f,
                "production input", "short window grip and release opens the window while the left grip stays held", "angle=" + inspection.WindowAngle);
            int resetsBefore = reset.ResetCount;
            QueueKeyboard(new KeyboardState(Key.Home)); yield return Wait(.05f); QueueKeyboard(new KeyboardState()); yield return Wait(.6f);
            var body = helmet.GetComponent<Rigidbody>();
            Require(reset.ResetCount == resetsBefore + 1 && !helmet.isSelected && !window.isSelected && !leftHand.DirectInteractor.hasSelection && !rightHand.DirectInteractor.hasSelection && !body.isKinematic && body.useGravity && inspection.WindowAngle < .1f && Vector3.Distance(body.position, reset.HomePose.position) < .06f,
                "production input", "Home clears held object/window ownership and restores the display", "reset delta=" + (reset.ResetCount - resetsBefore));
            yield return Wait(.2f);
            Require(!helmet.isSelected && !window.isSelected, "production input", "a still-held grip cannot regrab immediately after Home");
            QueueController(leftDevice, touch, Quaternion.identity, false); QueueController(rightDevice, windowTouch, Quaternion.identity, false); yield return Wait(.2f);
            touch = RootTouch(leftHand, Vector3.left); QueueController(leftDevice, touch, Quaternion.identity, false); yield return Wait(.4f);
            QueueController(leftDevice, touch, Quaternion.identity, true); yield return Wait(.2f);
            Require(helmet.IsSelected(leftHand.DirectInteractor), "production input", "release and new grip restore grabbing after reset");
            QueueController(leftDevice, touch, Quaternion.identity, false); yield return Wait(.2f);
            Record("saved integration after reset and regrab", leftHand, helmet);
        }

        static BoxCollider Box(string name, Vector3 position, Vector3 scale)
        {
            var instance = new GameObject(name); instance.transform.SetParent(fixtureRoot.transform); instance.transform.position = position;
            var collider = instance.AddComponent<BoxCollider>(); collider.size = scale; return collider;
        }
        static IEnumerator StepFixture(MuseumPhysicalHand hand, int steps)
        {
            for (int i = 0; i < steps; i++) { hand.StepPhysics(Time.fixedDeltaTime); yield return FixedSteps(1); }
        }
        static IEnumerator FixedSteps(int steps)
        {
            float target = Time.fixedTime + Time.fixedDeltaTime * steps;
            while (Time.fixedTime + .00001f < target) yield return null;
        }
        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
        static IEnumerator MoveController(XRSimulatedController device, Vector3 from, Vector3 to, bool grip, float duration)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < duration)
            {
                float t = Mathf.SmoothStep(0, 1, (Time.realtimeSinceStartup - start) / duration);
                QueueController(device, Vector3.Lerp(from, to, t), Quaternion.identity, grip); yield return null;
            }
            QueueController(device, to, Quaternion.identity, grip);
        }
        static Vector3 RootTouch(MuseumPhysicalHand hand, Vector3 localDirection)
        {
            var collider = helmet.colliders.Where(c => c && c.enabled && !c.isTrigger).OrderByDescending(c => c.bounds.size.sqrMagnitude).First();
            Vector3 direction = helmet.transform.rotation * localDirection;
            return collider.ClosestPoint(collider.bounds.center + direction * 2f) + direction * (EnvelopeSupport(hand, -direction) + .004f);
        }
        static Vector3 WindowTouch(MuseumPhysicalHand hand)
        {
            var collider = window.colliders.First(c => c && c.enabled);
            Vector3 front = helmet.transform.rotation * Vector3.back;
            return collider.ClosestPoint(collider.bounds.center + front) + front * (EnvelopeSupport(hand, -front) + .004f);
        }
        static float EnvelopeSupport(MuseumPhysicalHand hand, Vector3 worldDirection)
        {
            var collider = hand.CollisionCollider as BoxCollider;
            if (!collider) return hand.CollisionRadius;
            Transform pose = collider.transform;
            Vector3 half = collider.size * .5f;
            return Vector3.Dot(pose.TransformVector(collider.center), worldDirection) +
                Mathf.Abs(Vector3.Dot(pose.TransformVector(Vector3.right * half.x), worldDirection)) +
                Mathf.Abs(Vector3.Dot(pose.TransformVector(Vector3.up * half.y), worldDirection)) +
                Mathf.Abs(Vector3.Dot(pose.TransformVector(Vector3.forward * half.z), worldDirection));
        }
        static bool NearCandidate(MuseumPhysicalHand hand, IXRInteractable target)
        {
            var candidates = new List<IXRInteractable>(); hand.DirectInteractor.GetValidTargets(candidates);
            return candidates.Contains(target) && (!(target is IXRSelectInteractable selectable) || hand.DirectInteractor.interactionManager.IsSelectPossible(hand.DirectInteractor, selectable));
        }
        static string HandDetail(MuseumPhysicalHand hand)
        {
            var candidates = new List<IXRInteractable>(); hand.DirectInteractor.GetValidTargets(candidates);
            return "target=" + hand.TrackingTarget.position + "; proxy=" + hand.Body.position + "; error=" + hand.TrackingError + "; blocked=" + hand.IsBlocked + "; candidates=" + string.Join(",", candidates.Select(c => c.transform.name));
        }
        static float Penetration(Collider own, Collider other)
        {
            return Physics.ComputePenetration(own, own.transform.position, own.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out float distance) ? distance : 0f;
        }
        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        static void Record(string name, MuseumPhysicalHand hand, XRGrabInteractable grab, float penetration = 0)
        {
            report.samples.Add(new Sample { name = name, target = hand.TrackingTarget.position, proxy = hand.Body.position, trackingError = hand.TrackingError,
                blocked = hand.IsBlocked, tracking = hand.IsTrackingValid, maximumPenetration = penetration, selectors = grab ? grab.interactorsSelecting.Count : 0,
                objectPosition = grab ? grab.transform.position : Vector3.zero, objectRotation = grab ? grab.transform.rotation : Quaternion.identity, objectScale = grab ? grab.transform.localScale : Vector3.one });
        }
        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip)
        {
            var controller = device == leftDevice ? leftController : rightController;
            var parent = controller.transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent ? parent.InverseTransformPoint(world) : world,
                deviceRotation = parent ? Quaternion.Inverse(parent.rotation) * rotation : rotation, grip = grip ? 1f : 0f }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void QueueHead()
        {
            var camera = Camera.main; if (!camera) return;
            var parent = camera.transform.parent;
            Vector3 position = parent ? parent.InverseTransformPoint(camera.transform.position) : camera.transform.position;
            Quaternion rotation = parent ? Quaternion.Inverse(parent.rotation) * camera.transform.rotation : camera.transform.rotation;
            var state = new XRSimulatedHMDState { isTracked = true, trackingState = 3, devicePosition = position, centerEyePosition = position,
                leftEyePosition = position + Vector3.left * .032f, rightEyePosition = position + Vector3.right * .032f,
                deviceRotation = rotation, centerEyeRotation = rotation, leftEyeRotation = rotation, rightEyeRotation = rotation };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(hmd, state));
        }
        static void QueueKeyboard(KeyboardState state) => PendingInput.Enqueue(() => InputSystem.QueueStateEvent(keyboard, state));
        static void FlushInput()
        {
            if (InputState.currentUpdateType == InputUpdateType.Dynamic)
                while (PendingInput.Count > 0) PendingInput.Dequeue()();
        }
        static void Require(bool condition, string kind, string name, string detail = "")
        {
            report.checks.Add(new Check { kind = kind, name = name, detail = detail, passed = condition });
            if (!condition) throw new InvalidOperationException(kind + ": " + name + ": " + detail);
        }
        static void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Warning) report.warnings.Add(message);
            else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + message + "\n" + stack);
        }
        static void Finish(string failure, bool exitPlay = true)
        {
            if (finishing) return; finishing = true; Routines.Clear(); PendingInput.Clear();
            Application.logMessageReceived -= Log; InputSystem.onBeforeUpdate -= FlushInput;
            if (fixtureRoot) UnityEngine.Object.DestroyImmediate(fixtureRoot);
            foreach (var device in new InputDevice[] { leftDevice, rightDevice, hmd, keyboard, addedMouse }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedInput) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; }
            if (simulators != null) foreach (var simulator in simulators) if (simulator) simulator.enabled = true;
            foreach (var source in report.sources)
                report.checks.Add(new Check { kind = "source", name = "source remained unchanged during QA: " + source.path, passed = Hash(source.path) == source.sha256 });
            report.failure = failure ?? ""; report.durationSeconds = (float)(EditorApplication.timeSinceStartup - started);
            report.status = failure == null && report.errors.Count == 0 && report.checks.All(c => c.passed) ? "passed" : "failed";
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            SessionState.SetInt(Prefix + "Exit", report.status == "passed" ? 0 : 1);
            Debug.Log("PHYSICAL_HANDS_QA_" + report.status.ToUpperInvariant() + " " + Path.Combine(Output, "report.json"));
            if (exitPlay) EditorApplication.ExitPlaymode();
        }
        static void CaptureSources(Report target)
        {
            var paths = new List<string> { "MuseumVR/" + ScenePath, "MuseumVR/" + ScenePath + ".meta", "MuseumVR/Assets/Museum/Prefabs/HelmetExhibit.prefab",
                "MuseumVR/Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab",
                "MuseumVR/Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab.meta",
                "MuseumVR/Assets/Museum/Features/HelmetInspection/Editor/HelmetInspectionValidation.cs",
                "MuseumVR/Assets/Museum/Scripts/HelmetExhibitReset.cs", "MuseumVR/Packages/manifest.json", "MuseumVR/Packages/packages-lock.json",
                "MuseumVR/ProjectSettings/ProjectVersion.txt", "MuseumVR/ProjectSettings/ProjectSettings.asset",
                "MuseumVR/Assets/Samples/XR Interaction Toolkit/2.6.4/Starter Assets/XRI Default Input Actions.inputactions" };
            foreach (string folder in new[] { "MuseumVR/Assets/Museum/Features/PhysicalHands", "MuseumVR/Assets/Museum/Features/HelmetInspection/Runtime" })
                if (Directory.Exists(Path.Combine(RepoRoot, folder)))
                    paths.AddRange(Directory.GetFiles(Path.Combine(RepoRoot, folder), "*", SearchOption.AllDirectories).Where(p => p.EndsWith(".cs") || p.EndsWith(".meta") || p.EndsWith(".prefab") || p.EndsWith(".mat") || p.EndsWith(".unity")).Select(p => p.Substring(RepoRoot.Length + 1).Replace('\\', '/')));
            foreach (string path in paths.Distinct().OrderBy(p => p)) target.sources.Add(new Source { path = path, sha256 = Hash(path) });
        }
        static string Hash(string path)
        {
            string absolute = Path.IsPathRooted(path) ? path : Path.Combine(RepoRoot, path);
            if (!File.Exists(absolute)) return "MISSING";
            using (var stream = File.OpenRead(absolute)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string Git(string arguments)
        {
            try
            {
                using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments) { WorkingDirectory = RepoRoot,
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(5000)) { process.Kill(); return "UNAVAILABLE: git timeout"; }
                    return process.ExitCode == 0 ? stdout.Result.Trim() : "UNAVAILABLE: " + stderr.Result.Trim();
                }
            }
            catch (Exception exception) { return "UNAVAILABLE: " + exception.Message; }
        }
    }
}
#endif
