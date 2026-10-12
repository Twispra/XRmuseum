#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Museum.Eras;
using Museum.Experience;
using Museum.Exploration;
using Museum.Inspection;
using Museum.Hotspots;
using Museum.Localization;
using Museum.Refined;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.RefinedModels.EditorTools
{
    /// <summary>
    /// Acceptance of the saved MuseumStart integration. This runner never saves assets,
    /// rebuilds a scene, invokes XRI selection events, or writes reflected private fields.
    /// RunBatch requires graphics and must be launched WITHOUT -quit or -nographics.
    /// Existing inspection/localization runners provide the broader original-input regression.
    /// </summary>
    [InitializeOnLoad]
    public static class RefinedModelValidation
    {
        const string Prefix = "Museum.RefinedModelsQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string ColorSourcePath = "artifacts/superlite17b-unity-integration/exports/runtime-readback.json";
        const string ColorProbeShaderPath = "Assets/Museum/Features/RefinedModels/Editor/MaterialColorProbe.shader";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> InputQueue = new Queue<Action>();
        static Keyboard keyboard;
        static Mouse mouse;
        static XRSimulatedController right, left;
        static XRSimulatedHMD head;
        static ActionBasedController rightController, leftController;
        static bool addedMouse, changedInput, finishing;
        static InputSettings.BackgroundBehavior priorBackground;
        static InputSettings.EditorInputBehaviorInPlayMode priorEditorInput;
        static Report report;
        static double started, exitAt;
        static int lastFrame, pendingExit = -1;
        static string output;
        static HelmetInspectionController inspection;
        static RefinedHelmetBinding binding;
        static XRGrabInteractable grab;
        static HelmetExhibitReset reset;
        static HelmetSectionSurface section;
        static HelmetConnectionDemonstration demo;
        static HelmetEraSwitcher era;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));

        [Serializable] sealed class CheckRecord { public string name, method, detail; public bool passed; }
        [Serializable] sealed class FileState { public string path, sha256; }
        [Serializable] sealed class SampleRecord { public string phase; public float windowAngle, time; public Vector3 point; public Quaternion rotation; }
        [Serializable] sealed class SourceMaterial { public string name; public float[] base_color_linear; public float transmission; }
        [Serializable] sealed class SourceReadback { public SourceMaterial[] materials; }
        [Serializable] sealed class ColorProbeRecord { public string material, assetPath; public Color sourceLinear, savedSrgb, shaderLinear; public float maximumError; }
        [Serializable] sealed class SelectionRecord { public string hotspot, mode, selector; public int events; public Vector3 controllerWorld; public bool rootSelected; }
        [Serializable] sealed class Report
        {
            public string status = "running", utc, unity, scene, commit, branch, workingTree, failure = "";
            public string inputMethod = "Queued XRSimulatedController/HMD states in Dynamic Input System updates -> saved actions, controllers, XRDirectInteractor and XRRayInteractor. Selection listeners only observe actual XRI events. No SelectEnter/SelectExit, invoked button events or private-field writes. Section/era/demonstration assertions are explicitly labelled public API checks.";
            public string limitations = "Editor desktop simulation and temporary monocular URP renders only. Official simulator is checked then its input writer is disabled to prevent competing synthetic states. This is not a real headset, keyboard navigation usability, physical sound listening, historical certification, user study, build validation or device performance measurement. The early-era placeholder remains a concept preview; SuperLite dimensions and virtual front-panel articulation remain display assumptions.";
            public int refinedRenderers, refinedTriangles;
            public int superLiteRenderers, superLiteTriangles;
            public float superLiteWorldBottom, superLitePlinthTop;
            public Vector3 superLiteRootWorld, superLiteSavedHomeWorld;
            public List<CheckRecord> checks = new List<CheckRecord>();
            public List<SampleRecord> samples = new List<SampleRecord>();
            public List<ColorProbeRecord> colorProbes = new List<ColorProbeRecord>();
            public List<SelectionRecord> hotspotSelections = new List<SelectionRecord>();
            public List<string> grabDiagnostics = new List<string>();
            public List<FileState> files = new List<FileState>();
            public List<string> errors = new List<string>();
            public List<string> screenshots = new List<string>();
        }

        static RefinedModelValidation()
        {
            EditorApplication.playModeStateChanged += OnMode;
            EditorApplication.update += Tick;
        }

        [MenuItem("Museum/Refined Models/Validate saved main museum")]
        public static void RunInEditor() => Begin(false);
        public static void RunBatch() => Begin(true);

        static void Begin(bool batch)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run from Edit Mode.");
            if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-refined-integration-" + Guid.NewGuid().ToString("N");
            output = Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", runId);
            Directory.CreateDirectory(output);
            var snapshot = new Report
            {
                utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion, scene = ScenePath,
                commit = Git("rev-parse HEAD"), branch = Git("branch --show-current"), workingTree = Git("-c core.quotepath=false status --porcelain=v1 --untracked-files=all")
            };
            foreach (var path in Directory.GetFiles(Path.Combine(Application.dataPath, "Museum"), "*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".cs") || p.EndsWith(".shader") || p.EndsWith(".asset") || p.EndsWith(".prefab") || p.EndsWith(".mat") || p.EndsWith(".unity") || p.Contains("RefinedModels")))
                snapshot.files.Add(new FileState { path = path.Substring(RepoRoot.Length + 1).Replace('\\', '/'), sha256 = Hash(path) });
            snapshot.files.Add(new FileState { path = ColorSourcePath, sha256 = Hash(Path.Combine(RepoRoot, ColorSourcePath)) });
            SessionState.SetString(Prefix + "Snapshot", JsonUtility.ToJson(snapshot));
            SessionState.SetString(Prefix + "Output", output);
            SessionState.SetBool(Prefix + "Batch", batch);
            SessionState.SetInt(Prefix + "Code", -1);
            SessionState.SetBool(Prefix + "Active", true);
            pendingExit = -1; exitAt = 0;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("Refined models QA keyboard");
            mouse = Mouse.current;
            addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>();
        }

        static void OnMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Snapshot", ""));
                output = SessionState.GetString(Prefix + "Output", "");
                started = EditorApplication.timeSinceStartup; lastFrame = -1; finishing = false;
                Application.logMessageReceived += Log;
                InputSystem.onBeforeUpdate += FlushInput;
                Routines.Clear(); InputQueue.Clear(); Routines.Push(Run());
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && !finishing && report != null)
                Finish(new InvalidOperationException("Play Mode ended before refined-model acceptance completed."), false);
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                bool batch = SessionState.GetBool(Prefix + "Batch", false);
                int code = SessionState.GetInt(Prefix + "Code", 1);
                SessionState.SetBool(Prefix + "Active", false);
                if (batch)
                {
                    // Exit from a later Edit Mode tick, after scene/domain/input teardown.
                    // Exiting in the state-change delayCall can race Unity 2022 cleanup.
                    pendingExit = code < 0 ? 1 : code;
                    exitAt = EditorApplication.timeSinceStartup + 20;
                    Debug.Log("REFINED_MODEL_QA_EDIT_MODE exit scheduled with code " + pendingExit);
                }
            }
        }

        static void Tick()
        {
            // This must precede Active/isPlaying/finishing guards: the test has already
            // completed and those guards are intentionally false in Edit Mode.
            if (pendingExit >= 0 && EditorApplication.timeSinceStartup >= exitAt)
            {
                int code = pendingExit; pendingExit = -1;
                Debug.Log("REFINED_MODEL_QA_EDITOR_EXIT " + code);
                EditorApplication.Exit(code); return;
            }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 240) { Finish(new TimeoutException("Refined integration acceptance exceeded 240 seconds.")); return; }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            // Avoid XRI's sub-1ms throw-sample cutoff in an uncapped hidden batch player.
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
            catch (Exception error) { Finish(error); }
        }

        static IEnumerator Run()
        {
            yield return Wait(2f);
            Check(SceneManager.GetActiveScene().path == ScenePath, "saved main scene loaded", "saved asset");
            inspection = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            Check(inspection, "one existing helmet controller", "saved asset");
            Check(UnityEngine.Object.FindObjectsOfType<HelmetInspectionController>(true).Length == 1, "integration has one helmet", "saved asset");
            binding = inspection.GetComponent<RefinedHelmetBinding>();
            grab = inspection.GetComponent<XRGrabInteractable>(); reset = inspection.GetComponent<HelmetExhibitReset>(); section = inspection.GetComponent<HelmetSectionSurface>();
            demo = UnityEngine.Object.FindObjectOfType<HelmetConnectionDemonstration>(); era = inspection.GetComponent<HelmetEraSwitcher>();
            Check(binding && grab && reset && section && demo && era, "original controllers and new binding coexist", "saved asset");
            Check(binding.IsRefinedActive && era.CurrentIndex == 1, "Mark V starts with refined appearance", "runtime startup", "index=" + era.CurrentIndex);
            Check(grab.useDynamicAttach && grab.movementType == XRBaseInteractable.MovementType.VelocityTracking, "original grab settings retained", "saved asset");
            var window = inspection.GetComponentInChildren<HelmetWindowInteractable>(true);
            Check(window && window.colliders.Count > 0 && window.colliders.All(c => c && c.enabled && !grab.colliders.Contains(c)), "front-window hit targets remain independent", "saved asset");
            Check(inspection.GetComponentsInChildren<MonoBehaviour>(true).All(c => c), "helmet has no missing scripts", "saved asset");
            ValidateMeshes();
            ValidateModelAxes();
            ValidateSavedWindowHierarchy();
            Check(binding.AirOutlet && binding.TelephoneOutlet && Vector3.Distance(binding.AirOutlet.position, binding.TelephoneOutlet.position) > .08f, "two refined fittings have separate endpoints", "saved asset");
            CaptureHelmet("01-refined-front-closed", new Vector3(-.38f, .13f, -.88f));
            CaptureHelmet("02-refined-rear-fittings", new Vector3(.30f, .20f, .88f));

            SetupControllers();
            yield return Wait(.45f);
            CaptureCamera("03-saved-XR-view", Camera.main);
            var glassClosed = inspection.transform.InverseTransformPoint(binding.RefinedGlass.bounds.center);
            var hingeClosed = inspection.WindowPivot.localPosition;
            yield return PressInspectionButton("WINDOW");
            yield return Wait(.15f);
            Sample("window opening", binding.RefinedGlass.transform);
            Check(inspection.WindowAngle > 0 && inspection.WindowAngle < HelmetInspectionController.MaximumWindowAngle,
                "actual XR button starts continuous refined opening", "InputSystem -> XRI", "angle=" + inspection.WindowAngle);
            yield return Wait(.7f);
            var glassOpen = inspection.transform.InverseTransformPoint(binding.RefinedGlass.bounds.center);
            var expectedGlassOpen = hingeClosed + Quaternion.AngleAxis(-HelmetInspectionController.MaximumWindowAngle, Vector3.up) * (glassClosed - hingeClosed);
            Check(Mathf.Abs(inspection.WindowAngle - HelmetInspectionController.MaximumWindowAngle) < .05f && Vector3.Distance(glassClosed, glassOpen) > .06f && Vector3.Distance(glassOpen, expectedGlassOpen) < .004f,
                "refined front door follows existing window pivot", "InputSystem -> XRI", "angle=" + inspection.WindowAngle + "; closed=" + glassClosed + "; observed=" + glassOpen + "; expected=" + expectedGlassOpen + "; hinge=" + hingeClosed + "; arc error=" + Vector3.Distance(glassOpen, expectedGlassOpen));
            Check(binding.RefinedGlass.transform.IsChildOf(inspection.WindowPivot), "refined moving glass belongs to existing articulation", "saved asset");
            CaptureHelmet("04-refined-window-open", new Vector3(-.38f, .13f, -.88f));
            yield return PressInspectionButton("WINDOW");
            yield return Wait(.7f);
            Check(inspection.WindowAngle < .05f && Vector3.Distance(glassClosed, inspection.transform.InverseTransformPoint(binding.RefinedGlass.bounds.center)) < .004f,
                "refined front door closes to same alignment", "InputSystem -> XRI");

            yield return GrabAndMove();
            yield return CheckSections();
            yield return CheckEras();
            if (binding.AdditionalVisual != null) yield return CheckSuperLite();
            yield return CheckConnections();
            yield return CheckLanguages();
            demo.Stop(); inspection.ReduceMotion = false; inspection.SoundEnabled = true; inspection.ResetInspection(); ParkHands();
            yield return Wait(.3f);
            Check(!inspection.IsCutaway && !inspection.IsHeld && inspection.WindowAngle < .05f && binding.IsRefinedActive,
                "final reset restores refined resting exhibit", "public recovery API");
            CaptureHelmet("14-final-reset", new Vector3(-.38f, .13f, -.88f));
        }

        static void ValidateMeshes()
        {
            var renderers = binding.RefinedRenderers;
            Check(renderers != null && renderers.Length > 12, "refined model has separate detailed parts", "saved asset", "renderers=" + (renderers == null ? 0 : renderers.Length));
            report.refinedRenderers = renderers.Length;
            int triangles = 0;
            var faults = new List<string>();
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) { faults.Add("inactive or missing renderer"); continue; }
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh || mesh.vertexCount == 0) faults.Add(renderer.name + ": missing mesh");
                else { for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3; }
                foreach (var material in renderer.sharedMaterials)
                    if (!material || !material.shader || !material.shader.isSupported || material.shader.name.Contains("Error")) faults.Add(renderer.name + ": invalid material/shader");
            }
            Check(faults.Count == 0, "refined imported meshes and materials are usable", "saved asset", string.Join("; ", faults));
            report.refinedTriangles = triangles;
            Check(binding.RefinedShell && binding.RefinedGlass && binding.RefinedGlass.sharedMaterial.renderQueue >= 3000,
                "refined shell and transparent window assigned", "saved asset");
            Check(renderers.Any(r => r.sharedMaterials.Any(m => m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap"))),
                "authored refined surface texture survives integration", "saved asset");
            Check(!inspection.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.StartsWith("GB_") && r.enabled && r.gameObject.activeInHierarchy),
                "old greybox exterior is hidden in Mark V", "runtime startup");
        }

        static void ValidateModelAxes()
        {
            // Engineering envelopes of the delivered source mesh, not claimed historical
            // measurements. Test root-local geometry so root physics/tracking cannot mask
            // an FBX axis conversion that leaves the exhibit lying on its side.
            var bounds = MeshBoundsInHelmet(binding.RefinedRenderers[0]);
            foreach (var renderer in binding.RefinedRenderers.Skip(1)) bounds.Encapsulate(MeshBoundsInHelmet(renderer));
            Check(bounds.size.y > .45f && bounds.size.y < .60f && bounds.max.y > .48f && bounds.max.y < .58f && bounds.min.y > -.02f && bounds.min.y < .08f,
                "delivered Mark V mesh stands vertically above its collar", "saved geometry in helmet-local coordinates", "bounds center=" + bounds.center + "; size=" + bounds.size + "; min=" + bounds.min + "; max=" + bounds.max);
            var front = inspection.transform.InverseTransformPoint(binding.RefinedGlass.bounds.center);
            var air = inspection.transform.InverseTransformPoint(binding.AirOutlet.position);
            var telephone = inspection.transform.InverseTransformPoint(binding.TelephoneOutlet.position);
            Check(Mathf.Abs(front.x) < .03f && front.y > .25f && front.y < .42f && front.z < -.10f && front.z > -.22f,
                "refined front glass faces original museum approach", "saved geometry in helmet-local coordinates", "front=" + front);
            Check(air.x < -.03f && telephone.x > .03f && air.z > .10f && telephone.z > .10f && air.y > .15f && telephone.y > .15f,
                "refined air and telephone outlets occupy their respective rear sides", "saved geometry in helmet-local coordinates", "air=" + air + "; telephone=" + telephone);
            Check(Mathf.Abs(inspection.WindowPivot.localPosition.y - front.y) < .025f && Mathf.Abs(inspection.WindowPivot.localPosition.z - front.z) < .025f && Mathf.Abs(inspection.WindowPivot.localPosition.x - front.x) > .05f,
                "refined hinge is at side of vertical front window", "saved geometry in helmet-local coordinates", "hinge=" + inspection.WindowPivot.localPosition + "; front=" + front);
        }

        static void ValidateSavedWindowHierarchy()
        {
            Check(binding.RefinedWindow && binding.RefinedWindow.IsChildOf(inspection.WindowPivot),
                "saved refined door branch belongs to preserved window pivot", "saved hierarchy after scene load");
            var windowMeshes = binding.RefinedWindow.GetComponentsInChildren<MeshRenderer>(true);
            var registered = new HashSet<Renderer>(binding.RefinedRenderers);
            Check(windowMeshes.Length > 3 && windowMeshes.All(r => registered.Contains(r) && r.transform.IsChildOf(inspection.WindowPivot)) &&
                binding.RefinedGlass.transform.IsChildOf(binding.RefinedWindow),
                "all refined window meshes belong to articulated branch", "saved hierarchy after scene load", "window mesh count=" + windowMeshes.Length);
            // Read the saved articulation list, rather than constructing the expectation
            // from the same parent predicate that the binding implementation uses.
            var saved = new SerializedObject(inspection).FindProperty("highlightedRenderers");
            var highlighted = new HashSet<Renderer>();
            for (int i = 0; i < saved.arraySize; i++) highlighted.Add(saved.GetArrayElementAtIndex(i).objectReferenceValue as Renderer);
            Check(windowMeshes.All(r => highlighted.Contains(r)) && highlighted.Where(r => r && registered.Contains(r)).All(r => r.transform.IsChildOf(inspection.WindowPivot)),
                "saved window highlight and clip list covers articulated refined geometry", "read-only saved binding inspection");
        }

        static Bounds MeshBoundsInHelmet(Renderer renderer)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var source = mesh.bounds;
            var matrix = inspection.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(source.center + source.extents), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                result.Encapsulate(matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
            return result;
        }

        static IEnumerator GrabAndMove()
        {
            inspection.ResetInspection(); yield return Wait(.25f);
            if (binding.IsAdditionalVisualActive)
            {
                // Reproduce and record the former interior-centre fixture before
                // testing a visitor's ordinary touch on the actual outer shell.
                var interior = binding.ActiveShell.bounds.center;
                QueueRight(interior, Quaternion.identity, false); yield return Wait(.2f);
                QueueRight(interior, Quaternion.identity, true); yield return Wait(.2f);
                report.grabDiagnostics.Add("Former interior-centre fixture: " + GrabDiagnostic(interior));
                ParkHands(); yield return Wait(.2f);
                inspection.ResetInspection(); yield return Wait(.25f);
            }
            var target = binding.IsAdditionalVisualActive ? OrdinaryShellGrabPoint() :
                grab.colliders.Where(c => c && c.enabled && !c.isTrigger).OrderByDescending(c => c.bounds.size.sqrMagnitude).First().bounds.center;
            QueueRight(target, Quaternion.identity, false); yield return Wait(.2f);
            Check(grab.isHovered && rightController.GetComponentInChildren<XRDirectInteractor>(true).interactablesHovered.Contains(grab),
                "ordinary shell touch actually hovers helmet", "queued controller input -> actual XRI Direct root hover", GrabDiagnostic(target));
            QueueRight(target, Quaternion.identity, true); yield return Wait(.2f);
            Check(grab.isSelected && grab.interactorsSelecting[0] is XRDirectInteractor && !inspection.IsWindowHeld,
                "existing direct grip selects refined exhibit", "InputSystem -> XRI", GrabDiagnostic(target));
            var activeShell = binding.ActiveShell;
            Vector3 oldRoot = inspection.transform.position, oldShell = activeShell.bounds.center;
            Quaternion oldRotation = inspection.transform.rotation;
            float begin = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - begin < .6f)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - begin) / .6f);
                QueueRight(target + new Vector3(.18f, .23f, -.12f) * t, Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(8, 28, 4), t), true);
                yield return null;
            }
            yield return Wait(.3f);
            Check(grab.isSelected && Vector3.Distance(oldRoot, inspection.transform.position) > .12f && Quaternion.Angle(oldRotation, inspection.transform.rotation) > 12f,
                "refined helmet moves and rotates through original physics grab", "InputSystem -> XRI");
            Check(Vector3.Distance(oldShell, activeShell.bounds.center) > .1f && activeShell.transform.IsChildOf(inspection.transform),
                "refined shell follows grabbed root", "InputSystem -> XRI");
            CaptureHelmet(binding.IsAdditionalVisualActive ? "20-superlite-held" : "05-refined-held", new Vector3(-.38f, .13f, -.88f));
            ParkHands(); yield return Wait(.4f);
            Check(!grab.isSelected, "release clears original grab", "InputSystem -> XRI");
            inspection.ReturnInspectionToDisplay();
            yield return Wait(.6f);
            Check(!inspection.IsReturning && Vector3.Distance(inspection.transform.position, reset.HomePose.position) < .02f && Quaternion.Angle(inspection.transform.rotation, reset.HomePose.rotation) < 1f,
                "refined display returns to existing home", "public return API");
        }

        static Vector3 OrdinaryShellGrabPoint()
        {
            // A renderer's AABB centre is inside this helmet, not a visitor's shell
            // touch. Use a real rear/upper shell vertex, away from intentional
            // child controls, and put the saved Direct hand sphere on that vertex.
            var direct = rightController.GetComponentInChildren<XRDirectInteractor>(true);
            var hand = direct.GetComponentsInChildren<SphereCollider>(true).Single(c => c.enabled && c.isTrigger);
            float radius = Mathf.Max(hand.bounds.extents.x, hand.bounds.extents.y, hand.bounds.extents.z);
            var body = grab.colliders.Where(c => c && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger).ToArray();
            var children = inspection.GetComponentsInChildren<XRSimpleInteractable>(true).Where(t => t && t.enabled && t != grab)
                .SelectMany(t => t.colliders).Where(c => c && c.enabled && c.gameObject.activeInHierarchy).Distinct().ToArray();
            var shell = binding.ActiveShell;
            var envelope = MeshBoundsInHelmet(shell);
            Vector3 chosen = Vector3.zero; float best = float.NegativeInfinity, rootDistance = 0;
            foreach (var vertex in shell.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                Vector3 point = shell.transform.TransformPoint(vertex);
                Vector3 local = inspection.transform.InverseTransformPoint(point);
                if (local.z < envelope.center.z + envelope.extents.z * .55f || local.y < envelope.center.y) continue;
                float reach = body.Min(c => Vector3.Distance(c.ClosestPoint(point), point));
                float clearance = children.Min(c => Vector3.Distance(c.ClosestPoint(point), point));
                if (reach > radius * .8f || clearance <= radius + .01f || clearance <= best) continue;
                chosen = point; best = clearance; rootDistance = reach;
            }
            Check(!float.IsNegativeInfinity(best), "ordinary SuperLite grip has a real rear-shell contact clear of child targets",
                "actual mesh vertices, root ClosestPoint and child-collider clearance",
                "touch=" + chosen.ToString("F6") + "; root distance=" + rootDistance + "; child clearance=" + best + "; Direct radius=" + radius);
            Vector3 handOffset = Quaternion.Inverse(rightController.transform.rotation) * (hand.bounds.center - rightController.transform.position);
            return chosen - handOffset; // QueueRight uses world identity rotation for this ordinary grab.
        }

        static string GrabDiagnostic(Vector3 controllerPoint)
        {
            var direct = rightController.GetComponentInChildren<XRDirectInteractor>(true);
            var selected = UnityEngine.Object.FindObjectsOfType<XRBaseInteractable>(true).Where(t => t.isSelected)
                .Select(t => t.name + " by " + string.Join(",", t.interactorsSelecting.Select(i => i.GetType().Name)));
            var hands = direct.GetComponentsInChildren<Collider>(true).Where(c => c.enabled)
                .Select(c => c.name + " " + c.GetType().Name + " center=" + c.bounds.center.ToString("F6") + "; bounds=" + c.bounds.size.ToString("F6"));
            var body = grab.colliders.Where(c => c && c.enabled)
                .Select(c => c.name + " " + c.GetType().Name + " distance=" + Vector3.Distance(c.ClosestPoint(controllerPoint), controllerPoint).ToString("F6"));
            var window = inspection.GetComponentInChildren<HelmetWindowInteractable>(true);
            return "era=" + era.CurrentIndex + "; requested controller=" + controllerPoint.ToString("F6") +
                "; actual controller=" + rightController.transform.position.ToString("F6") +
                "; Direct attach=" + direct.GetAttachTransform(grab).position.ToString("F6") +
                "; root hovered=" + grab.isHovered + "; window hovered=" + window.isHovered + "; window held=" + inspection.IsWindowHeld +
                "; Direct hovered=[" + string.Join(",", direct.interactablesHovered.Select(t => t.transform.name)) +
                "]; selected=[" + string.Join(";", selected) + "]; Direct colliders=[" + string.Join(";", hands) +
                "]; root colliders=[" + string.Join(";", body) + "]";
        }

        static IEnumerator CheckSections()
        {
            inspection.ToggleFloating();
            foreach (var normal in new[] { Vector3.right, Vector3.up, new Vector3(.4f, .3f, -.8f).normalized })
            {
                inspection.SetCutaway(true); inspection.SetSectionPlane(normal, 0); section.Refresh(); yield return null;
                Check(section.TriangleCount > 0, "refined cut has structural cap / " + normal, "public section API", "triangles=" + section.TriangleCount);
                var capMeshes = inspection.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("Section face -") && f.gameObject.activeInHierarchy).ToArray();
                Check(capMeshes.Length > 0 && capMeshes.All(f => f.sharedMesh && f.sharedMesh.vertices.All(v => Mathf.Abs(Vector3.Dot(normal, v - inspection.SectionPointLocal)) < .00015f)),
                    "structural caps stay on requested local plane / " + normal, "public section API");
                var block = new MaterialPropertyBlock();
                var faults = new List<string>();
                foreach (var renderer in binding.RefinedRenderers)
                {
                    renderer.GetPropertyBlock(block);
                    if (Mathf.Abs(block.GetFloat("_CutEnabled") - 1) > .001f || !renderer.sharedMaterials.All(m => m.HasProperty("_CutEnabled"))) faults.Add(renderer.name);
                }
                Check(faults.Count == 0, "refined metal and glass retain clip properties / " + normal, "public section API", string.Join(", ", faults));
                CaptureHelmet("06-section-" + report.screenshots.Count, normal * .95f + Vector3.up * .12f);
            }
            int rebuilds = section.RebuildCount;
            inspection.RotateDisplayAround(Vector3.up, 24); yield return null; section.Refresh();
            Check(section.RebuildCount == rebuilds, "rotating entire refined object reuses section geometry", "public rotation API");
            inspection.SetWindowAngle(35); yield return null; yield return null;
            Check(section.RebuildCount > rebuilds, "refined articulated door updates structural section", "public window API");
            inspection.SetCutaway(false); section.Refresh();
            Check(!inspection.GetComponentsInChildren<Renderer>(true).Any(r => r.name.StartsWith("Section face -") && r.gameObject.activeInHierarchy),
                "closing cut hides all caps", "public section API");
            yield return CheckLockSections();
            inspection.ResetInspection(); yield return Wait(.2f);
        }

        static IEnumerator CheckLockSections()
        {
            var source = binding.RefinedRenderers.Single(r => r.name == "DoorToggleBolt_13TPI_Nominal");
            var geometry = AssetDatabase.LoadAssetAtPath<HelmetSectionMeshData>(
                "Assets/Museum/Features/RefinedModels/Data/RefinedSectionMeshes.asset");
            var data = geometry.Find(source.GetComponent<MeshFilter>().sharedMesh);
            Check(data != null, "saved lock has production cap geometry", "saved asset");
            bool reduced = inspection.ReduceMotion;
            inspection.ReduceMotion = true;
            foreach (var normal in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                inspection.SetWindowAngle(0);
                yield return null; yield return null;
                Vector3 centre = inspection.transform.InverseTransformPoint(source.bounds.center);
                inspection.SetCutaway(true);
                inspection.SetSectionPlane(normal, Vector3.Dot(normal, centre - inspection.SectionCenterLocal));
                yield return null; yield return null;
                CheckLockCap(source, data, "closed baseline / " + normal, true);
                Quaternion closedRotation = source.transform.rotation;
                inspection.SetWindowAngle(5);
                yield return null; yield return null;
                Check(Quaternion.Angle(closedRotation, source.transform.rotation) > .1f,
                    "partial opening moves lock / " + normal, "public window API -> natural LateUpdate");
                CheckLockCap(source, data, "partial opening / " + normal);
                int rebuilds = section.RebuildCount;
                yield return null; yield return null;
                Check(section.RebuildCount == rebuilds, "stationary lock section reuses cache / " + normal,
                    "natural LateUpdate cache regression");
                inspection.SetWindowOpen(true);
                yield return null; yield return null;
                CheckLockCap(source, data, "reduced-motion opening / " + normal);
                inspection.SetWindowOpen(false);
                yield return null; yield return null;
                CheckLockCap(source, data, "reduced-motion closing / " + normal);
                // Existing editor clients can request a cap before this frame's lock motion.
                inspection.SetWindowAngle(5); section.Refresh();
                yield return null; yield return null;
                CheckLockCap(source, data, "early public Refresh recovers / " + normal);
            }
            inspection.ReduceMotion = reduced;
            inspection.SetCutaway(false);
        }

        static void CheckLockCap(Renderer source, HelmetSectionMeshData.Entry data, string phase, bool requireCap = false)
        {
            var cap = inspection.GetComponentsInChildren<MeshFilter>(true)
                .Single(f => f.name == "Section face - " + source.name);
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            SectionCapGeometry.Build(data.vertices, data.triangles,
                inspection.transform.worldToLocalMatrix * source.transform.localToWorldMatrix,
                inspection.SectionNormalLocal, inspection.SectionPointLocal, vertices, indices);
            var actual = cap.sharedMesh.vertices;
            float error = actual.Length == vertices.Count ? 0f : float.PositiveInfinity;
            for (int i = 0; i < actual.Length && i < vertices.Count; i++)
                error = Mathf.Max(error, Vector3.Distance(actual[i], vertices[i]));
            Check((!requireCap || indices.Count > 0) && actual.Length == vertices.Count &&
                cap.sharedMesh.triangles.SequenceEqual(indices) && error < .00001f &&
                cap.gameObject.activeInHierarchy == (indices.Count > 0),
                "lock cap matches current articulated geometry / " + phase,
                "production cap data -> current source matrix after natural Play frames",
                "cached vertices=" + actual.Length + "; current vertices=" + vertices.Count + "; max vertex error metres=" + error);
        }

        static IEnumerator CheckEras()
        {
            Check(era.AvailableEraCount == 3, "all three existing era choices retained", "saved asset");
            int bindingId = binding.GetInstanceID(), grabId = grab.GetInstanceID(), resetId = reset.GetInstanceID();
            Vector3 normal = new Vector3(.4f, .3f, -.8f).normalized;
            const float depth = .2f, windowAngle = 35f;
            inspection.SetWindowAngle(windowAngle);
            inspection.SetCutaway(true);
            inspection.SetSectionPlane(normal, 0);
            inspection.SetSectionPlane(normal, inspection.SectionRange * depth);
            Check(Vector3.Distance(inspection.SectionNormalLocal, normal) < .0001f && Mathf.Abs(inspection.SectionOffset / inspection.SectionRange - depth) < .001f,
                "era fixture starts at requested relative section depth", "public section API before switching",
                "normal=" + inspection.SectionNormalLocal + "; relative depth=" + inspection.SectionOffset / inspection.SectionRange);
            var tool = inspection.GetComponentInChildren<HelmetSectionInteractable>(true);
            Check(tool, "existing section guide remains attached", "saved asset");
            tool.ShowTool = true;
            foreach (int index in new[] { 0, 2, 1, 0, 1 })
            {
                era.ShowEra(index); yield return Wait(.35f);
                bool expectedRefined = index == 1 || binding.AdditionalVisual != null && binding.AdditionalVisual.eraIndex == index;
                Check(era.CurrentIndex == index && binding.IsRefinedActive == expectedRefined, "era visual mode / " + index, "public era API");
                Check(binding.GetInstanceID() == bindingId && grab.GetInstanceID() == grabId && reset.GetInstanceID() == resetId,
                    "era switch retains original interaction component identities / " + index, "public era API");
                Check(inspection.IsCutaway && Vector3.Distance(inspection.SectionNormalLocal, normal) < .0001f &&
                    Mathf.Abs(inspection.SectionOffset / inspection.SectionRange - depth) < .001f && Mathf.Abs(inspection.WindowAngle - windowAngle) < .01f,
                    "era switch preserves window and visitor section state / " + index, "public era and section APIs",
                    "normal=" + inspection.SectionNormalLocal + "; relative depth=" + inspection.SectionOffset / inspection.SectionRange + "; window=" + inspection.WindowAngle);
                section.Refresh(); yield return null; // Destroy() of previous caps completes before inventory.
                Check(section.TriangleCount > 0, "structural section works in era / " + index, "public era and section APIs");
                Vector3 planePoint = inspection.transform.TransformPoint(inspection.SectionPointLocal);
                float guideError = Vector3.Distance(tool.transform.position, planePoint);
                Check(tool.ShowTool && guideError < .001f,
                    "existing section guide stays at current model's section centre / " + index, "public era and section APIs",
                    "guide=" + tool.transform.position + "; requested plane point=" + planePoint + "; centre error metres=" + guideError);
            }
            inspection.SetCutaway(false); section.Refresh(); yield return null;
            ValidateMeshes();
        }

        static IEnumerator CheckSuperLite()
        {
            var visual = binding.AdditionalVisual;
            Check(visual.eraIndex == 2, "reviewed SuperLite occupies era index 2", "saved asset");
            era.ShowEra(2); yield return Wait(.35f);
            inspection.ResetInspection(); yield return Wait(.25f);
            report.superLiteRenderers = visual.renderers.Length;
            report.superLiteTriangles = visual.renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
            Check(binding.IsAdditionalVisualActive && binding.ActiveShell == visual.shell && binding.ActiveGlass == visual.glass,
                "SuperLite resolves active surfaces", "public era API");
            Check(visual.renderers.Length == 143 && report.superLiteTriangles == 50268,
                "actual imported SuperLite retains reviewed mesh and triangle counts", "saved imported meshes", "meshes=" + visual.renderers.Length + "; triangles=" + report.superLiteTriangles);
            Check(visual.renderers.All(r => r.enabled && r.gameObject.activeInHierarchy) && binding.RefinedRenderers.All(r => !r.enabled),
                "SuperLite is visible with Mark V hidden", "public era API");
            Check(visual.renderers.All(r => r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0 && r.GetComponent<MeshFilter>().sharedMesh.uv.Length == r.GetComponent<MeshFilter>().sharedMesh.vertexCount &&
                r.GetComponent<MeshFilter>().sharedMesh.vertices.All(v => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z))),
                "SuperLite imports finite usable meshes and UVs", "saved imported meshes");
            Check(visual.glass.sharedMaterial.shader.name == "Museum/Inspection Glass" && visual.glass.sharedMaterial.renderQueue >= 3000 &&
                visual.renderers.Where(r => r != visual.glass).All(r => r.sharedMaterial.shader.name == "Museum/Inspection Shell"),
                "SuperLite uses transparent glass and compatible section/highlight materials", "saved asset");
            CheckSuperLiteColors(visual);
            Check(visual.window.GetComponentsInChildren<Renderer>(true).Length == 14 && visual.glass.transform.IsChildOf(inspection.WindowPivot),
                "SuperLite front panel remains in original interaction hinge", "saved asset");
            var bounds = MeshBoundsInHelmet(visual.renderers[0]);
            foreach (var r in visual.renderers.Skip(1)) bounds.Encapsulate(MeshBoundsInHelmet(r));
            Check(Vector3.Distance(bounds.size, new Vector3(.2816658f, .3138472f, .3250093f)) < .0001f,
                "SuperLite axis conversion retains width/height/depth without scaling drift", "saved imported meshes", bounds.size.ToString("F6"));
            var worldMin = visual.renderers.Select(r => r.bounds.min.y).Min();
            var plinth = GameObject.Find("Plinth");
            if (plinth && plinth.GetComponent<Renderer>())
            {
                var support = plinth.GetComponent<Renderer>().bounds.max.y;
                report.superLiteWorldBottom = worldMin;
                report.superLitePlinthTop = support;
                report.superLiteRootWorld = inspection.transform.position;
                report.superLiteSavedHomeWorld = reset.HomePose.position;
                Debug.Log("SUPERLITE_SUPPORT_RUNTIME root=" + inspection.transform.position + "; home=" + reset.HomePose.position + "; scale=" + inspection.transform.lossyScale + "; localMin=" + bounds.min.y + "; worldBottom=" + worldMin + "; plinth=" + support);
                Check(worldMin >= support - .005f && worldMin < support + .015f,
                    "SuperLite rests at existing exhibition support height", "saved scene geometry", "bottom=" + worldMin + "; support=" + support);
            }
            CaptureHelmet("15-superlite-front-closed", new Vector3(-.38f, .13f, -.88f));
            CaptureHelmet("16-superlite-reference-side", new Vector3(-.70f, .15f, .30f));
            CaptureCamera("17-superlite-on-display-in-museum", Camera.main);
            var closed = inspection.transform.InverseTransformPoint(visual.glass.bounds.center);
            var frontMarker = Hotspots().Single(h => HotspotSource(h, "identifier") == "front-window");
            var markerClosed = inspection.transform.InverseTransformPoint(frontMarker.transform.position);
            var markerInGlass = visual.glass.transform.InverseTransformPoint(frontMarker.transform.position);
            CheckHotspotSurfaces(visual, "closed window");
            yield return PressInspectionButton("WINDOW"); yield return Wait(.9f);
            var expected = visual.hinge + Quaternion.AngleAxis(-HelmetInspectionController.MaximumWindowAngle, Vector3.up) * (closed - visual.hinge);
            Check(inspection.WindowAngle > 94.9f && Vector3.Distance(inspection.transform.InverseTransformPoint(visual.glass.bounds.center), expected) < .004f,
                "SuperLite virtual inspection panel opens via existing XR button", "InputSystem -> XRI");
            Check(Vector3.Distance(markerInGlass, visual.glass.transform.InverseTransformPoint(frontMarker.transform.position)) < .0001f &&
                Vector3.Distance(markerClosed, inspection.transform.InverseTransformPoint(frontMarker.transform.position)) > .05f,
                "SuperLite front hotspot follows actual moving glass", "InputSystem -> XRI -> observed surface-relative transform");
            CheckHotspotSurfaces(visual, "open window");
            CaptureHelmet("18-superlite-virtual-panel-open", new Vector3(-.38f, .13f, -.88f));
            yield return PressInspectionButton("WINDOW"); yield return Wait(.9f);
            Check(inspection.WindowAngle < .05f && Vector3.Distance(inspection.transform.InverseTransformPoint(visual.glass.bounds.center), closed) < .004f,
                "SuperLite inspection panel closes at original alignment", "InputSystem -> XRI");
            Check(Vector3.Distance(markerClosed, inspection.transform.InverseTransformPoint(frontMarker.transform.position)) < .0001f,
                "SuperLite front hotspot returns with closed glass", "InputSystem -> XRI -> observed marker transform");
            yield return GrabAndMove();
            Check(binding.IsAdditionalVisualActive, "SuperLite persists through original grab/return", "InputSystem -> XRI and public return API");
            foreach (var normal in new[] { Vector3.right, Vector3.up, new Vector3(.4f, .3f, -.8f).normalized })
            {
                inspection.SetCutaway(true); inspection.SetSectionPlane(normal, 0); section.Refresh(); yield return null;
                Check(section.TriangleCount > 0, "SuperLite structural section / " + normal, "public section API", "triangles=" + section.TriangleCount);
                var block = new MaterialPropertyBlock();
                Check(visual.renderers.All(r => { r.GetPropertyBlock(block); return block.GetFloat("_CutEnabled") > .5f; }),
                    "SuperLite visible surfaces receive clip state / " + normal, "public section API");
            }
            CaptureHelmet("19-superlite-section", new Vector3(-.38f, .13f, -.88f));
            inspection.SetCutaway(false);
            demo.Play(HelmetExplorationController.Clue.Air); yield return Wait(2.7f);
            Check(Vector3.Distance(demo.AirEndpointWorld, visual.airOutlet.position) < .0002f,
                "existing AIR demonstration follows SuperLite inlet anchor", "public demonstration API");
            demo.Play(HelmetExplorationController.Clue.Voice); yield return Wait(2.7f);
            Check(Vector3.Distance(demo.VoiceEndpointWorld, visual.telephoneOutlet.position) < .0002f && Vector3.Distance(visual.airOutlet.position, visual.telephoneOutlet.position) > .08f,
                "existing VOICE demonstration follows separate SuperLite teaching anchor", "public demonstration API");
            yield return WaitDemoElapsed(6f);
            var rear = inspection.GetComponent<HelmetRearConnections>();
            Check(demo.HasReachedDestination && rear.VoiceResponseVisible && rear.VoiceFocus > .65f,
                "SuperLite VOICE reaches its illustrative endpoint", "public demonstration API -> observed response");
            CheckSuperLiteMechanicalFocus(visual, "incoming VOICE");
            yield return CheckSuperLiteVoiceText();
            yield return WaitDemoElapsed(9.3f);
            Check(demo.PhaseIndex == 3, "SuperLite VOICE reaches reply phase", "public demonstration API");
            CheckSuperLiteMechanicalFocus(visual, "reply VOICE");
            demo.Stop();
            yield return CheckSuperLiteVisitorContent(visual);
            foreach (int index in new[] { 1, 2, 0, 2, 1 })
            {
                era.ShowEra(index); yield return Wait(.35f);
                Check(visual.renderers.All(r => r.enabled == (index == 2)) && binding.RefinedRenderers.All(r => r.enabled == (index == 1)),
                    "three-way switching hides the inactive refined geometry / " + index, "public era API");
            }
            inspection.ResetInspection(); yield return Wait(.2f);
        }

        static void CheckSuperLiteColors(RefinedHelmetBinding.EraVisual visual)
        {
            Check(QualitySettings.activeColorSpace == ColorSpace.Linear && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat),
                "linear floating-point color probe is available", "project color space and graphics capability");
            var source = JsonUtility.FromJson<SourceReadback>(File.ReadAllText(Path.Combine(RepoRoot, ColorSourcePath)));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ColorProbeShaderPath);
            Check(shader && shader.isSupported && source.materials.Count(m => m.transmission == 0) == 8,
                "probe shader and eight opaque Blender source colors are available", "saved source readback and ordinary Color-property shader", ColorSourcePath);
            foreach (var materialSource in source.materials.Where(m => m.transmission == 0))
            {
                string path = "Assets/Museum/Features/SuperLiteModels/Materials/" + materialSource.name + ".mat";
                var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
                Check(saved && visual.renderers.Any(r => r.sharedMaterials.Contains(saved)) && saved.shader.name == "Museum/Inspection Shell",
                    "actual SuperLite renderer uses saved material / " + materialSource.name, "saved asset identity", path);
                var values = materialSource.base_color_linear;
                var expected = new Color(values[0], values[1], values[2], values[3]);
                var stored = saved.GetColor("_BaseColor");
                var observed = ProbeColor(shader, stored);
                float error = ColorError(expected, observed);
                report.colorProbes.Add(new ColorProbeRecord { material = materialSource.name, assetPath = path,
                    sourceLinear = expected, savedSrgb = stored, shaderLinear = observed, maximumError = error });
                Check(error < .001f, "saved shader color matches Blender linear source / " + materialSource.name,
                    "ordinary Color property -> ARGBFloat linear RT pixel readback (no lighting or display transfer)",
                    "source=" + expected.ToString("F6") + "; saved=" + stored.ToString("F6") + "; shader=" + observed.ToString("F6") + "; max error=" + error);
                if (materialSource.name == "M_Shell_Gelcoat_Yellow")
                {
                    var unencoded = ProbeColor(shader, expected);
                    Check(ColorError(expected, unencoded) > .1f && unencoded.g < expected.g - .1f,
                        "color probe rejects former direct-linear assignment", "negative control using the same Color-property upload",
                        "source=" + expected.ToString("F6") + "; unencoded shader=" + unencoded.ToString("F6"));
                }
            }
        }

        static Color ProbeColor(Shader shader, Color savedColor)
        {
            var probe = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var target = RenderTexture.GetTemporary(4, 4, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active; bool previousWrite = GL.sRGBWrite; Texture2D pixels = null;
            try
            {
                if (target.sRGB) throw new InvalidOperationException("Color probe target unexpectedly applies sRGB transfer.");
                probe.SetColor("_BaseColor", savedColor);
                GL.sRGBWrite = false;
                Graphics.Blit(Texture2D.whiteTexture, target, probe);
                RenderTexture.active = target;
                pixels = new Texture2D(4, 4, TextureFormat.RGBAFloat, false, true);
                pixels.ReadPixels(new Rect(0, 0, 4, 4), 0, 0); pixels.Apply();
                return pixels.GetPixel(2, 2);
            }
            finally
            {
                RenderTexture.active = previous; GL.sRGBWrite = previousWrite;
                RenderTexture.ReleaseTemporary(target);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        static float ColorError(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a));
        static HelmetHotspot[] Hotspots() => inspection.GetComponentsInChildren<HelmetHotspot>(true)
            .OrderBy(h => HotspotSource(h, "identifier"), StringComparer.Ordinal).ToArray();
        static string HotspotSource(HelmetHotspot hotspot, string field) => new SerializedObject(hotspot).FindProperty(field).stringValue;
        static string HotspotDetail(HelmetHotspot hotspot) => HotspotSource(hotspot, "title") + "\n" + HotspotSource(hotspot, "description");

        static void CheckHotspotSurfaces(RefinedHelmetBinding.EraVisual visual, string phase)
        {
            var hotspots = Hotspots();
            Check(hotspots.Length == 3, "three actual SuperLite observation targets / " + phase, "saved scene components");
            foreach (var hotspot in hotspots)
            {
                string id = HotspotSource(hotspot, "identifier");
                var surfaces = visual.renderers.Where(r => id == "front-window" ? r.name == "Glass_Port_520-004" :
                    id == "bonnet" ? r.name.StartsWith("Shell_Yellow", StringComparison.Ordinal) :
                    id == "breastplate" && (r.name == "NeckDam_Neoprene_510-533" || r.name == "NeckClamp_505-055")).ToArray();
                var collider = hotspot.GetComponent<XRSimpleInteractable>().colliders.SingleOrDefault();
                Check(surfaces.Length > 0 && surfaces.All(r => r.enabled && r.gameObject.activeInHierarchy) && collider && collider.enabled && collider.isTrigger,
                    "observation target has its actual active surface / " + phase + "/" + id, "renderer names and live collider");
                Vector3 center = collider.bounds.center;
                float radius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y, collider.bounds.extents.z);
                float distance = surfaces.Min(r => SurfaceDistance(r, center));
                var bounds = surfaces[0].bounds; foreach (var surface in surfaces.Skip(1)) bounds.Encapsulate(surface.bounds);
                bounds.Expand(2 * (radius + .006f));
                Check(bounds.Contains(center) && distance <= radius + .006f,
                    "observation marker intersects matching model surface / " + phase + "/" + id,
                    "actual mesh triangles and world bounds (independent of attachment configuration)",
                    "surfaces=" + string.Join(",", surfaces.Select(r => r.name)) + "; marker=" + center.ToString("F6") +
                    "; radius=" + radius + "; nearest triangle metres=" + distance + "; expanded bounds=" + bounds);
            }
        }

        static float SurfaceDistance(Renderer renderer, Vector3 point)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices; var triangles = mesh.triangles; float nearest = float.PositiveInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = renderer.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 b = renderer.transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = renderer.transform.TransformPoint(vertices[triangles[i + 2]]);
                nearest = Mathf.Min(nearest, Vector3.Distance(point, ClosestTrianglePoint(point, a, b, c)));
            }
            return nearest;
        }

        static Vector3 ClosestTrianglePoint(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a;
            Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + d1 / (d1 - d3) * ab;
            Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + d2 / (d2 - d6) * ac;
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (d4 - d3) / ((d4 - d3) + (d5 - d6)) * (c - b);
            float inverse = 1 / (va + vb + vc);
            return a + ab * (vb * inverse) + ac * (vc * inverse);
        }

        static void CheckSuperLiteMechanicalFocus(RefinedHelmetBinding.EraVisual visual, string phase)
        {
            var mechanical = visual.renderers.Where(r => r.name.StartsWith("Hinge_", StringComparison.Ordinal) ||
                r.name.StartsWith("NeckClamp", StringComparison.Ordinal) || r.name.StartsWith("Neck_Yoke", StringComparison.Ordinal)).ToArray();
            var block = new MaterialPropertyBlock(); var samples = new List<string>();
            bool clear = mechanical.Length >= 3;
            foreach (var renderer in mechanical)
            {
                renderer.GetPropertyBlock(block); float focus = block.GetFloat("_Focus");
                samples.Add(renderer.name + "=" + focus.ToString("F6")); clear &= Mathf.Abs(focus) < .0001f;
            }
            Check(clear, "VOICE does not highlight neck-clamp mechanisms / " + phase,
                "actual renderer MaterialPropertyBlock during natural playback", string.Join("; ", samples));
        }

        static IEnumerator CheckSuperLiteVoiceText()
        {
            var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>("Assets/Museum/Features/Localization/Tables/LanguageChannel.asset");
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            var label = demo.GetComponentsInChildren<TMP_Text>(true).SingleOrDefault(t => t.name == "Destination label");
            Check(label && channel && table, "actual SuperLite VOICE destination label is present", "natural playback TMP hierarchy");
            foreach (var language in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
            {
                channel.SetLanguage(language); yield return Wait(.12f);
                CheckVisitorText(label, "Communication location (illustrative)", table, language, "SuperLite VOICE destination");
                Check(demo.IsVisible && binding.IsAdditionalVisualActive,
                    "trilingual VOICE clarification retains active playback / " + language, "actual demonstration and model state");
            }
            channel.SetLanguage(MuseumLanguage.English);
        }

        static IEnumerator CheckSuperLiteVisitorContent(RefinedHelmetBinding.EraVisual visual)
        {
            var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>("Assets/Museum/Features/Localization/Tables/LanguageChannel.asset");
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            var hotspots = Hotspots();
            var tour = hotspots[0].GetComponentInParent<HelmetHotspotTour>();
            var caption = GameObject.Find("Helmet Instructions")?.GetComponent<TMP_Text>();
            var information = tour ? tour.GetComponentsInChildren<TMP_Text>(true).SingleOrDefault(t => t.name == "Information") : null;
            Check(channel && table && tour && caption && information && hotspots.Length == 3,
                "saved visitor labels, tour and language assets are attached", "saved scene components");
            channel.SetLanguage(MuseumLanguage.English);
            era.ShowEra(1); yield return Wait(.35f);
            inspection.ResetInspection(); tour.ResetTour(); yield return Wait(.2f);
            var positions = hotspots.Select(h => h.transform.localPosition).ToArray();
            var rotations = hotspots.Select(h => h.transform.localRotation).ToArray();
            var scales = hotspots.Select(h => h.transform.localScale).ToArray();
            var originalDetails = hotspots.Select(HotspotDetail).ToArray();
            var originalCaption = caption.GetComponent<LocalizedSourceText>()?.SourceEnglish;
            Check(!string.IsNullOrEmpty(originalCaption) && originalCaption.Contains("Mark V"),
                "restoration baseline shows original Mark V instructions", "actual visible TMP English source", caption.text);
            var lower = hotspots.Single(h => HotspotSource(h, "identifier") == "breastplate");
            yield return SelectObservation(lower, false);
            Check(tour.VisitedCount == 1 && information.GetComponent<LocalizedSourceText>().SourceEnglish == HotspotDetail(lower),
                "Mark V baseline has one actual selected observation card", "InputSystem -> XRI -> actual TMP source");

            era.ShowEra(2); yield return Wait(.35f);
            Check(tour.VisitedCount == 1 && lower.IsVisited && information.GetComponent<LocalizedSourceText>().SourceEnglish == ExpectedSuperLiteDetail("breastplate"),
                "era change refreshes displayed neck-seal card while retaining progress", "public era API -> actual TMP source", information.text);
            CheckHotspotSurfaces(visual, "1 to 2 visitor switch");
            foreach (var language in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
            {
                channel.SetLanguage(language); yield return Wait(.2f);
                Check(channel.Current == language && binding.IsAdditionalVisualActive,
                    "SuperLite remains equipped across language change / " + language, "public language API");
                CheckVisitorText(caption, "Diving helmet\n<size=62%>SuperLite 17B-inspired study</size>", table, language, "model instructions");
                Check(caption.text.Contains("SuperLite 17B") && !caption.text.Contains("Mark V"),
                    "visible trilingual instructions name current SuperLite model / " + language, "actual TMP text", caption.text);
                foreach (var hotspot in hotspots)
                {
                    string id = HotspotSource(hotspot, "identifier");
                    string expected = ExpectedSuperLiteDetail(id);
                    Check(HotspotDetail(hotspot) == expected,
                        "SuperLite observation describes its actual subject / " + language + "/" + id,
                        "independent accepted English copy compared with live hotspot source", HotspotDetail(hotspot));
                    yield return SelectObservation(hotspot, false);
                    CheckVisitorText(information, expected, table, language, id);
                    Check(!new[] { "Mark V", "Breastplate", "shoulders", "胸甲", "肩部" }.Any(information.text.Contains),
                        "visible SuperLite card has no old model or shoulder-plate copy / " + language + "/" + id,
                        "actual selected TMP text", information.text);
                }
                Check(tour.VisitedCount == 3 && hotspots.All(h => h.IsVisited),
                    "three trilingual observations retain exact completed progress / " + language, "actual tour state after XRI selections");
            }
            channel.SetLanguage(MuseumLanguage.English); yield return Wait(.2f);
            foreach (var hotspot in hotspots)
            {
                yield return SelectObservation(hotspot, true);
                CheckVisitorText(information, ExpectedSuperLiteDetail(HotspotSource(hotspot, "identifier")), table, channel.Current, "ray " + hotspot.name);
            }
            Check(tour.VisitedCount == 3, "Ray revisits do not increment completed SuperLite progress", "actual tour state after XRI selections");

            era.ShowEra(1); yield return Wait(.35f);
            for (int i = 0; i < hotspots.Length; i++)
                Check(Vector3.Distance(hotspots[i].transform.localPosition, positions[i]) < .0001f &&
                    Quaternion.Angle(hotspots[i].transform.localRotation, rotations[i]) < .01f &&
                    Vector3.Distance(hotspots[i].transform.localScale, scales[i]) < .0001f && HotspotDetail(hotspots[i]) == originalDetails[i],
                    "1 to 2 to 1 restores original hotspot pose and copy / " + HotspotSource(hotspots[i], "identifier"),
                    "actual local transforms and sources compared with observed Mark V baseline",
                    "position=" + hotspots[i].transform.localPosition.ToString("F6") + "; original=" + positions[i].ToString("F6") + "; detail=" + HotspotDetail(hotspots[i]));
            CheckVisitorText(caption, originalCaption, table, channel.Current, "restored Mark V instructions");
            var last = hotspots.Last();
            Check(tour.VisitedCount == 3 && hotspots.All(h => h.IsVisited) &&
                information.GetComponent<LocalizedSourceText>().SourceEnglish == HotspotDetail(last),
                "1 to 2 to 1 restores displayed card and preserves visited IDs", "actual tour and TMP state", information.text);
            era.ShowEra(2); yield return Wait(.35f);
            CheckHotspotSurfaces(visual, "restored SuperLite after round trip");
            ParkHands();
        }

        static string ExpectedSuperLiteDetail(string identifier)
        {
            switch (identifier)
            {
                case "front-window": return "Front window\nInspect the front viewing area and its rim. Side opening is an observation demonstration, not the real helmet's mechanism.";
                case "bonnet": return "Helmet shell\nInspect the upper shell of this lightweight helmet study.";
                case "breastplate": return "Neck seal\nInspect the neck seal and surrounding clamp below the shell.";
                default: throw new InvalidOperationException("Unexpected observation identifier: " + identifier);
            }
        }

        static void CheckVisitorText(TMP_Text label, string expectedSource, LocalizationTable table, MuseumLanguage language, string subject)
        {
            var source = label.GetComponent<LocalizedSourceText>();
            var entry = table.Entries.FirstOrDefault(e => e.English == expectedSource);
            string expected = entry.For(language);
            Check(table.ContainsEnglish(expectedSource) && !string.IsNullOrWhiteSpace(expected) &&
                (language == MuseumLanguage.English || expected != expectedSource),
                "registered full trilingual visitor copy / " + language + "/" + subject, "saved table independent of runtime source", expected);
            label.ForceMeshUpdate(true, true);
            Check(source && source.SourceEnglish == expectedSource && label.text == expected,
                "actual visitor label uses exact current translation / " + language + "/" + subject,
                "visible TMP source and text", "source=" + (source ? source.SourceEnglish : "missing") + "; actual=" + label.text);
            string plain = Regex.Replace(label.text, "<[^>]*>", "");
            string missing = !label.font ? "missing font" : new string(plain.Where(ch => !char.IsWhiteSpace(ch)).Distinct()
                .Where(ch => !label.font.HasCharacter(ch, false, false)).ToArray());
            Check(label.font == table.FontAsset && missing.Length == 0,
                "visitor label uses saved glyphs / " + language + "/" + subject, "actual TMP font character table without fallback or dynamic population", missing);
            var renderer = label.GetComponent<Renderer>();
            Check(label.gameObject.activeInHierarchy && renderer && renderer.enabled && label.color.a > .9f &&
                !label.isTextOverflowing && label.textInfo.characterCount > 0,
                "translated visitor label has visible complete layout / " + language + "/" + subject,
                "actual TMP generated layout", "fontSize=" + label.fontSize + "; rect=" + label.rectTransform.sizeDelta + "; overflow=" + label.isTextOverflowing);
        }

        static IEnumerator SelectObservation(HelmetHotspot hotspot, bool ray)
        {
            var target = hotspot.GetComponent<XRSimpleInteractable>();
            string id = HotspotSource(hotspot, "identifier"), mode = ray ? "Ray" : "Direct";
            Vector3 point = target.colliders.Single().bounds.center;
            Vector3 outward = inspection.transform.TransformDirection(id == "bonnet" ? Vector3.right : Vector3.back);
            Vector3 hand = ray ? point + outward * .65f : point;
            Quaternion rotation = ray ? Quaternion.LookRotation(point - hand, inspection.transform.up) : Quaternion.identity;
            int events = 0; IXRSelectInteractor selector = null;
            UnityAction<SelectEnterEventArgs> listener = args => { events++; selector = args.interactorObject; };
            target.selectEntered.AddListener(listener);
            try
            {
                ParkHands(); yield return Wait(.15f);
                QueueRight(hand, rotation, false); yield return Wait(.22f);
                Check(target.isHovered, "actual observation hover / " + mode + "/" + id, "InputSystem -> saved actions -> XRI colliders");
                if (ray)
                {
                    var rays = rightController.GetComponentsInChildren<XRRayInteractor>(true);
                    Check(rays.Any(r => r.TryGetCurrent3DRaycastHit(out var hit) && target.colliders.Contains(hit.collider)),
                        "Ray physically hits current observation collider / " + id, "actual saved XRRayInteractor raycast hit");
                    var marker = new SerializedObject(hotspot).FindProperty("targetRenderer").objectReferenceValue as Renderer;
                    Check(marker && marker.enabled && marker.gameObject.activeInHierarchy,
                        "Ray reveals actual observation marker / " + id, "live marker renderer visibility after physical ray hit");
                }
                QueueRight(hand, rotation, true); yield return Wait(.18f);
                report.hotspotSelections.Add(new SelectionRecord { hotspot = id, mode = mode,
                    selector = selector == null ? "none" : selector.GetType().Name, events = events, controllerWorld = hand, rootSelected = grab.isSelected });
                Check(events == 1 && target.isSelected && hotspot.IsVisited && (ray ? selector is XRRayInteractor : selector is XRDirectInteractor),
                    "actual observation selection / " + mode + "/" + id, "queued Grip input and observed selectEntered listener",
                    "events=" + events + "; selector=" + selector + "; controller=" + hand.ToString("F6"));
                Check(!grab.isSelected && !inspection.IsWindowHeld,
                    "observation selection leaves helmet and window unheld / " + mode + "/" + id, "actual XRI interaction state");
                QueueRight(hand, rotation, false); yield return Wait(.15f);
                Check(!target.isSelected, "actual observation release / " + mode + "/" + id, "queued Grip release -> XRI");
                ParkHands(); yield return Wait(.12f);
            }
            finally { target.selectEntered.RemoveListener(listener); }
        }

        static IEnumerator CheckConnections()
        {
            inspection.ResetInspection(); yield return Wait(.2f);
            inspection.SoundEnabled = true;
            demo.Play(HelmetExplorationController.Clue.Air); yield return Wait(2.7f);
            var equipment = demo.RefinedEquipment;
            Check(demo.IsUsingRefinedEquipment && equipment && equipment.IsReady, "saved refined equipment is actually selected", "public demo API");
            Check(VisibleMeshes(equipment.Pump) && !VisibleMeshes(equipment.SurfaceTelephone), "air mode displays authored pump", "public demo API");
            Check(Vector3.Distance(demo.AirSourceWorld, equipment.AirOutlet.position) < .0002f && Vector3.Distance(demo.AirEndpointWorld, binding.AirOutlet.position) < .0002f,
                "air route connects exact refined source and helmet outlets", "public demo API");
            var crank = Descendant(equipment.Pump, "PumpCrankPivot");
            Check(crank, "authored pump articulation present", "saved asset");
            Quaternion firstCrank = crank.localRotation;
            yield return Wait(.45f);
            Check(Quaternion.Angle(firstCrank, crank.localRotation) > 8f && equipment.LastSampleTime > 2f,
                "authored pump movement follows production demo timeline", "public demo API");
            Check(equipment.IsAudioPlaying, "illustrative air sound plays with original sound setting", "public sound API");
            CaptureDemo("09-refined-air-pump");
            inspection.SoundEnabled = false; yield return Wait(.12f);
            Check(!equipment.IsAudioPlaying && demo.IsPlaying, "mute stops refined sound without stopping visual demonstration", "public sound API");
            inspection.SoundEnabled = true; yield return Wait(.12f);
            Check(equipment.IsAudioPlaying, "unmute restores refined sound", "public sound API");
            int resetsBefore = reset.ResetCount;
            foreach (int index in new[] { 0, 2, 1 })
            {
                float progress = demo.NormalizedProgress;
                era.ShowEra(index); yield return Wait(.3f); yield return null;
                Check(demo.IsVisible && demo.IsPlaying && demo.NormalizedProgress > progress && demo.NormalizedProgress - progress < .08f,
                    "era switch continues existing connection playback / " + index, "public era and demo APIs",
                    "progress before=" + progress + "; after=" + demo.NormalizedProgress + "; playing=" + demo.IsPlaying);
                Check(reset.ResetCount == resetsBefore && Vector3.Distance(demo.AirEndpointWorld, binding.AirOutlet.position) < .0002f,
                    "continuous playback follows each era's current fitting without resetting root / " + index, "public era and demo APIs");
            }
            demo.Stop();
            Check(!equipment.IsAudioPlaying && !demo.IsVisible && demo.SourceEmphasis == 0 && demo.DestinationEmphasis == 0,
                "stop clears new sound and original connection emphasis", "public demo API");
            demo.Play(HelmetExplorationController.Clue.Voice); yield return Wait(2.7f);
            Check(VisibleMeshes(equipment.SurfaceTelephone) && !VisibleMeshes(equipment.Pump), "voice mode displays authored surface telephone", "public demo API");
            Check(Vector3.Distance(demo.VoiceSourceWorld, equipment.TelephoneOutlet.position) < .0002f && Vector3.Distance(demo.VoiceEndpointWorld, binding.TelephoneOutlet.position) < .0002f,
                "communication uses two telephone anchors", "public demo API");
            var diaphragm = Descendant(equipment.SurfaceTelephone, "SurfaceDiaphragm");
            Check(diaphragm, "authored telephone diaphragm present", "saved asset");
            Vector3 firstDiaphragm = diaphragm.localPosition;
            var displayBasis = equipment.SurfaceTelephone.parent;
            // FBX can keep centimetre animation keys under an imported scale-100
            // ancestor. Measure physical displacement after that hierarchy, while
            // excluding any movement of the diagram's stable display pivot.
            Vector3 firstDiaphragmDisplay = displayBasis.InverseTransformPoint(diaphragm.position);
            float furthestDiaphragm = 0f, furthestDiaphragmLocal = 0f, sampleUntil = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < sampleUntil)
            {
                Vector3 displayDelta = displayBasis.InverseTransformPoint(diaphragm.position) - firstDiaphragmDisplay;
                furthestDiaphragm = Mathf.Max(furthestDiaphragm, displayBasis.TransformVector(displayDelta).magnitude);
                furthestDiaphragmLocal = Mathf.Max(furthestDiaphragmLocal, Vector3.Distance(firstDiaphragm, diaphragm.localPosition));
                yield return null;
            }
            Check(demo.PhaseIndex == 1 && furthestDiaphragm > .0001f,
                "authored telephone diaphragm animates in outgoing phase", "public demo API",
                "sampled world displacement after display-pivot compensation=" + furthestDiaphragm + "; sampled raw local displacement=" + furthestDiaphragmLocal +
                "; diaphragm lossyScale=" + diaphragm.lossyScale + "; parent lossyScale=" + diaphragm.parent.lossyScale + "; display lossyScale=" + displayBasis.lossyScale);
            inspection.RotateDisplayAround(new Vector3(.2f, 1, .1f), 30); yield return Wait(.15f);
            Check(Vector3.Distance(demo.VoiceEndpointWorld, binding.TelephoneOutlet.position) < .0002f,
                "refined telephone cable follows moved and rotated helmet", "public rotation and demo APIs");
            CaptureDemo("10-refined-voice-surface");
            // A first shader compilation/render can stall several seconds. Start a fresh,
            // real public-API playback if that capture has passed the reception window;
            // never seek the demonstration by writing its private clock.
            if (DemoElapsed > 6.5f) demo.Play(HelmetExplorationController.Clue.Voice);
            yield return WaitDemoElapsed(6f);
            var rear = inspection.GetComponent<HelmetRearConnections>();
            Check(demo.PhaseIndex == 1 && demo.HasReachedDestination && rear.VoiceResponseVisible && rear.VoiceFocus > .65f,
                "arrival highlights actual refined telephone fitting", "public demo API");
            CaptureHelmet("11-refined-phone-arrival", new Vector3(.30f, .20f, .88f));
            yield return WaitDemoElapsed(9.2f);
            Check(demo.PhaseIndex == 3, "refined equipment retains reply phase", "public demo API");
            demo.Stop();
            inspection.ReduceMotion = true; demo.Play(HelmetExplorationController.Clue.Air); yield return Wait(.2f);
            firstCrank = crank.localRotation;
            yield return Wait(.3f);
            Check(!demo.IsPlaying && demo.IsVisible && !equipment.IsAudioPlaying && Quaternion.Angle(firstCrank, crank.localRotation) < .02f && Mathf.Abs(demo.SourceEmphasis - demo.DestinationEmphasis) < .001f,
                "reduced motion presents still refined equipment without sound loop", "public reduce-motion API");
            CaptureDemo("12-refined-reduced-motion");
            demo.Stop(); inspection.ReduceMotion = false; inspection.ResetInspection(); yield return Wait(.2f);
        }

        static IEnumerator CheckLanguages()
        {
            var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>("Assets/Museum/Features/Localization/Tables/LanguageChannel.asset");
            var exploration = UnityEngine.Object.FindObjectOfType<HelmetExplorationController>();
            var panel = UnityEngine.Object.FindObjectOfType<HelmetExplorationPanel>();
            Check(channel && exploration && panel, "existing localization and interpretation remain attached", "saved asset");
            panel.ShowExploration(); exploration.SelectClue(HelmetExplorationController.Clue.Air); demo.Stop();
            string previous = null;
            foreach (var language in new[] { MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese, MuseumLanguage.English })
            {
                channel.SetLanguage(language); yield return Wait(.2f);
                var text = panel.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).ToArray();
                string visible = string.Join(" | ", text);
                Check(channel.Current == language && visible.Length > 0 && (previous == null || visible != previous),
                    "live interpretation changes language with refined content / " + language, "public language API", visible);
                previous = visible;
                Check(binding.IsRefinedActive && !grab.isSelected, "language switch keeps refined visual and interaction state / " + language, "public language API");
            }
            CaptureCamera("13-refined-existing-language-interface", Camera.main);
        }

        static void SetupControllers()
        {
            var simulators = UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>();
            Check(simulators.Length > 0, "official XR simulator starts in saved scene", "runtime startup");
            foreach (var simulator in simulators) simulator.enabled = false;
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).First(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).First(c => c.name == "Left Controller");
            Check(rightController.GetComponentInChildren<XRDirectInteractor>(true) && rightController.selectAction.action != null && rightController.selectAction.action.enabled,
                "existing actions and direct interactor retained", "saved asset");
            priorBackground = InputSystem.settings.backgroundBehavior; priorEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            changedInput = true;
            right = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            left = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
            head = InputSystem.AddDevice<XRSimulatedHMD>();
            var camera = Camera.main;
            Check(camera && camera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>(), "saved XR camera remains the visitor view", "saved asset");
            var parent = camera.transform.parent; Vector3 p = parent.InverseTransformPoint(camera.transform.position); Quaternion q = Quaternion.Inverse(parent.rotation) * camera.transform.rotation;
            var hmdState = new XRSimulatedHMDState { isTracked = true, trackingState = 3, devicePosition = p, centerEyePosition = p, leftEyePosition = p + q * Vector3.left * .032f, rightEyePosition = p + q * Vector3.right * .032f, deviceRotation = q, centerEyeRotation = q, leftEyeRotation = q, rightEyeRotation = q };
            InputQueue.Enqueue(() => InputSystem.QueueStateEvent(head, hmdState)); ParkHands();
        }

        static IEnumerator PressInspectionButton(string label)
        {
            var button = GameObject.Find("Inspection " + label)?.GetComponent<XRSimpleInteractable>();
            Check(button && button.GetComponent<Collider>(), "saved control is available / " + label, "InputSystem -> XRI");
            var touch = button.GetComponent<Collider>().bounds.center;
            QueueRight(touch, Quaternion.identity, false); yield return Wait(.18f);
            QueueRight(touch, Quaternion.identity, true); yield return Wait(.16f);
            Check(button.isSelected, "saved control selected / " + label, "InputSystem -> XRI");
            QueueRight(touch + Vector3.back * .25f, Quaternion.identity, false); yield return Wait(.05f);
        }

        static void QueueRight(Vector3 position, Quaternion rotation, bool grip) => QueueController(right, rightController, position, rotation, grip);
        static void QueueController(XRSimulatedController device, ActionBasedController controller, Vector3 position, Quaternion rotation, bool grip)
        {
            var parent = controller.transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1, devicePosition = parent.InverseTransformPoint(position), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation, grip = grip ? 1 : 0 }.WithButton(ControllerButton.GripButton, grip);
            InputQueue.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        {
            if (right != null) QueueRight(new Vector3(.8f, 1, 1), Quaternion.identity, false);
            if (left != null) QueueController(left, leftController, new Vector3(-.8f, 1, 1), Quaternion.identity, false);
        }
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (InputQueue.Count > 0) InputQueue.Dequeue()(); }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        static float DemoElapsed => demo.NormalizedProgress * HelmetConnectionDemonstration.Duration;
        static IEnumerator WaitDemoElapsed(float elapsed)
        {
            float deadline = Time.realtimeSinceStartup + HelmetConnectionDemonstration.Duration + 1f;
            while (DemoElapsed < elapsed)
            {
                if (!demo.IsVisible || Time.realtimeSinceStartup > deadline) throw new TimeoutException("Demonstration did not reach " + elapsed + " seconds.");
                yield return null;
            }
            yield return null;
        }
        static Transform Descendant(Transform root, string name) => root ? root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name) : null;
        static bool VisibleMeshes(Transform root) => root && root.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled && r.gameObject.activeInHierarchy);
        static void Sample(string phase, Transform point) => report.samples.Add(new SampleRecord { phase = phase, time = Time.realtimeSinceStartup, windowAngle = inspection.WindowAngle, point = point.position, rotation = point.rotation });
        static void Check(bool passed, string name, string method, string detail = "")
        {
            report.checks.Add(new CheckRecord { passed = passed, name = name, method = method, detail = detail });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
        static void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + message + "\n" + stack); }
        static void Finish(Exception error, bool exitPlayMode = true)
        {
            if (finishing) return; finishing = true; Routines.Clear();
            if (error != null) report.failure = error.ToString();
            foreach (var file in report.files)
                try { if (Hash(Path.Combine(RepoRoot, file.path)) != file.sha256) report.errors.Add("File changed during QA: " + file.path); }
                catch (Exception changed) { report.errors.Add("Fingerprint unavailable after QA: " + file.path + " / " + changed.Message); }
            int code = error == null && report.errors.Count == 0 ? 0 : 1;
            report.status = code == 0 ? "passed" : "failed";
            InputSystem.onBeforeUpdate -= FlushInput; Application.logMessageReceived -= Log; InputQueue.Clear();
            foreach (var device in new InputDevice[] { right, left, head, keyboard, addedMouse ? mouse : null }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedInput) { InputSystem.settings.backgroundBehavior = priorBackground; InputSystem.settings.editorInputBehaviorInPlayMode = priorEditorInput; changedInput = false; }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            SessionState.SetInt(Prefix + "Code", code);
            Debug.Log("REFINED_MODEL_QA_" + report.status.ToUpperInvariant() + " " + output);
            if (exitPlayMode) EditorApplication.ExitPlaymode();
        }

        static void CaptureHelmet(string name, Vector3 offset)
        {
            Vector3 centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            Render(name, centre + offset, centre, 46f);
        }
        static void CaptureDemo(string name)
        {
            var surfaces = demo.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (surfaces.Length == 0) throw new InvalidOperationException("No visible equipment to capture.");
            var bounds = surfaces[0].bounds; foreach (var surface in surfaces) bounds.Encapsulate(surface.bounds);
            Vector3 from = Camera.main ? Vector3.ProjectOnPlane(Camera.main.transform.position - bounds.center, Vector3.up).normalized : Vector3.back;
            Render(name, bounds.center + from * 1.15f + Vector3.up * .2f, bounds.center, 48f);
        }
        static void Render(string name, Vector3 position, Vector3 centre, float fov)
        {
            var go = new GameObject("Temporary refined integration QA camera");
            try
            {
                var camera = go.AddComponent<Camera>(); camera.transform.position = position; camera.transform.LookAt(centre);
                camera.fieldOfView = fov; camera.nearClipPlane = .02f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .055f, .075f);
                CaptureCamera(name, camera);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        static void CaptureCamera(string name, Camera camera)
        {
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) text.ForceMeshUpdate(true);
            var target = RenderTexture.GetTemporary(1280, 960, 24); var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels = new Texture2D(1280, 960, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0); pixels.Apply();
                string file = name + ".png"; File.WriteAllBytes(Path.Combine(output, file), pixels.EncodeToPNG()); report.screenshots.Add(file);
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); }
        }
        static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static string Git(string arguments)
        {
            var info = new System.Diagnostics.ProcessStartInfo("git", arguments) { WorkingDirectory = RepoRoot, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (var process = System.Diagnostics.Process.Start(info))
            {
                string value = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(10000) || process.ExitCode != 0) throw new InvalidOperationException("Git snapshot failed: " + arguments + " / " + error);
                return value.Trim();
            }
        }
    }
}
#endif
