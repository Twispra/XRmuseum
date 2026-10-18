#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Museum.Eras;
using Museum.Diegetic;
using Museum.PhysicalHands;
using Museum.Exploration;
using Museum.Hotspots;
using Museum.Inspection;
using Museum.PhysicalHands.EditorTools;
using Museum.Refined;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Localization.EditorTools
{
    /// <summary>Read-only visitor-era acceptance on the saved scene. Never builds product assets.</summary>
    [InitializeOnLoad]
    public static class VisitorEraValidation
    {
        const string Prefix = "Museum.VisitorEraQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string EarlyCaption = HelmetEraVisitorContent.EarlyModelInstructions;
        const string MarkVCaption = "Diving helmet\n<size=62%>Mark V-inspired study</size>";
        const string SuperLiteCaption = "Diving helmet\n<size=62%>SuperLite 17B-inspired study</size>";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static HelmetEraSwitcher era;
        static RefinedHelmetBinding binding;
        static HelmetEraVisitorContent content;
        static HelmetInspectionController inspection;
        static HelmetWindowInteractable window;
        static HelmetHotspotTour tour;
        static HelmetHotspot[] hotspots;
        static HelmetDiegeticStation station;
        static HelmetPhysicalControl[] eraControls;
        static TMP_Text information;
        static LocalizationTable table;
        static LanguageChannel language;
        static ActionBasedController rightController, leftController;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD hmd;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedMouse, changedInput, finishing;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static double started;
        static int lastFrame = -1;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => SessionState.GetString(Prefix + "Output", "");

        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class Point
        {
            public string identifier, title, description;
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        [Serializable] sealed class Capture
        {
            public string file, purpose;
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView;
        }
        [Serializable] sealed class Report
        {
            public string utc, finishedUtc, commit, dirty, unity, status = "running", failure;
            public bool diagnosticOnly;
            public List<string> geometryDiagnostics = new List<string>();
            public string method = "Saved MuseumStart scene; public API fixtures seed completed hotspot visits and request era/language changes. Queued synthetic controller/HMD states flow through normal Dynamic Input Actions and existing XR ray interactors to current diegetic era/window/section controls, model rotation and all knowledge hotspots. The original held-window deferral runs separately with the optional diegetic presentation disabled. No manual SelectEnter/SelectExit, HoverEnter, TriggerProgrammatically, private-field writes or InputSystem.Update. The official simulator state writer is disabled; physical hands, production colliders and select filters remain enabled.";
            public string limits = "Desktop synthetic XR and explicit API fixtures, not real headset/controller, historical authentication, stereo readability, user study, performance or executable build. Captures use temporary diagnostic monocular cameras; no production camera or product asset is saved. explanationElapsed is read only to detect timer restarts. Parent process must additionally verify source fingerprints after natural Editor exit.";
            public float batchExitSettleSeconds = 20;
            public List<Check> checks = new List<Check>();
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<Point> markVPoints = new List<Point>();
            public List<Capture> captures = new List<Capture>();
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        static VisitorEraValidation()
        { EditorApplication.playModeStateChanged += OnPlayMode; EditorApplication.update += Tick; }

        [MenuItem("Museum/Localization/Validate visitor era content")]
        public static void RunBatch() => Begin(false);

        public static void RunGeometryDiagnosticBatch() => Begin(true);

        static void Begin(bool diagnosticOnly)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("A dirty scene is open; save separately before validation.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string run = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-visitor-era-" + Guid.NewGuid().ToString("N");
            string outputBase = Environment.GetEnvironmentVariable("MUSEUM_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputBase)) outputBase = Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs");
            string output = Path.GetFullPath(Path.Combine(outputBase, run));
            if (Directory.Exists(output)) throw new InvalidOperationException("Evidence already exists: " + output);
            Directory.CreateDirectory(output); SessionState.SetString(Prefix + "Output", output);
            report = new Report { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion, diagnosticOnly = diagnosticOnly };
            if (diagnosticOnly) report.method = "DIAGNOSTIC ONLY: public API switches to SuperLite, records actual marker/mesh/collider bounds and outside-solid candidate poses through normal input and the authored ray. This is not full visitor-era acceptance.";
            foreach (string path in AssetDatabase.GetDependencies(ScenePath, true).Concat(new[] {
                "Assets/Museum/Features/Localization/Editor/VisitorEraValidation.cs",
                "Assets/Museum/Features/Localization/Runtime/HelmetEraVisitorContent.cs",
                "Assets/Museum/Features/RefinedModels/Runtime/RefinedHelmetBinding.cs",
                "Assets/Museum/Features/Localization/Tables/MuseumTranslations.json",
                "Packages/manifest.json", "Packages/packages-lock.json" }).Distinct().Where(File.Exists).OrderBy(p => p))
                report.files.Add(new Fingerprint { path = path, sha256 = Hash(path) });
            var savedButtons = UnityEngine.Object.FindObjectsOfType<HelmetEraButton>(true);
            Require(savedButtons.Length == 2, "Saved scene contains two era buttons", "count=" + savedButtons.Length);
            foreach (var button in savedButtons) ValidateSavedButton(button);
            var savedContent = UnityEngine.Object.FindObjectOfType<HelmetEraVisitorContent>();
            Require(savedContent && savedContent.Instructions, "Saved era content has caption binding", "Existing product component.");
            var savedTour = UnityEngine.Object.FindObjectOfType<HelmetHotspotTour>();
            foreach (var point in savedTour.GetComponentsInChildren<HelmetHotspot>(true))
                report.markVPoints.Add(new Point { identifier = point.Identifier, title = point.Title, description = point.Description,
                    position = point.transform.localPosition, rotation = point.transform.localRotation, scale = point.transform.localScale });
            Require(report.markVPoints.Count == 3, "Saved Mark V baseline has exactly three observation points", "count=" + report.markVPoints.Count);
            SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
            SessionState.SetInt(Prefix + "Exit", 1); SessionState.SetBool(Prefix + "PendingExit", false);
            SessionState.SetBool(Prefix + "Active", true); EditorApplication.EnterPlaymode();
        }

        static void ValidateSavedButton(HelmetEraButton button)
        {
            string expected = button.Direction == HelmetEraButton.StepDirection.Previous ? "era.previous" : "era.next";
            var labels = button.GetComponentsInChildren<LocalizedText>(true);
            Require(button.GetComponent<XRSimpleInteractable>() && button.Switcher,
                "Saved era button retains its existing XRI and switcher / " + button.name, button.Direction.ToString());
            Require(labels.Length == 1 && labels[0].Key == expected && labels[0].GetComponent<TMP_Text>(),
                "Saved era button has static localized key / " + button.name,
                "expected=" + expected + "; actual=" + string.Join(",", labels.Select(l => l.Key)));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("Visitor era QA keyboard");
            mouse = Mouse.current; addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>("Visitor era QA mouse");
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                started = EditorApplication.timeSinceStartup; lastFrame = -1; finishing = false;
                PendingInput.Clear(); Routines.Clear(); Routines.Push(ValidateRuntime());
                Application.logMessageReceived += OnLog; InputSystem.onBeforeUpdate += FlushInput;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && !finishing && report != null) Finish("Play Mode ended before validation completed.", false);
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Prefix + "Active", false);
                if (Application.isBatchMode)
                {
                    SessionState.SetFloat(Prefix + "ExitAt", (float)EditorApplication.timeSinceStartup + 20);
                    SessionState.SetBool(Prefix + "PendingExit", true);
                    Debug.Log("VISITOR_ERA_QA_SETTLING seconds=20; utc=" + DateTime.UtcNow.ToString("o"));
                }
            }
        }

        static void Tick()
        {
            if (SessionState.GetBool(Prefix + "PendingExit", false))
            {
                if (EditorApplication.timeSinceStartup < SessionState.GetFloat(Prefix + "ExitAt", float.MaxValue) ||
                    EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Prefix + "PendingExit", false);
                EditorApplication.Exit(SessionState.GetInt(Prefix + "Exit", 1)); return;
            }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 300) { Finish("Visitor-era validation exceeded 300 seconds."); return; }
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
            catch (Exception ex) { Finish(ex.ToString()); }
        }

        static IEnumerator ValidateRuntime()
        {
            yield return Wait(2);
            Require(!InputSystem.devices.OfType<XRHMD>().Any(d => !(d is XRSimulatedHMD)), "Synthetic input refuses real HMD", "No hardware state is overwritten.");
            content = UnityEngine.Object.FindObjectOfType<HelmetEraVisitorContent>();
            binding = UnityEngine.Object.FindObjectOfType<RefinedHelmetBinding>(); era = binding.GetComponent<HelmetEraSwitcher>();
            inspection = binding.GetComponent<HelmetInspectionController>(); window = binding.GetComponentInChildren<HelmetWindowInteractable>(true);
            tour = binding.GetComponentInChildren<HelmetHotspotTour>(true); hotspots = tour.GetComponentsInChildren<HelmetHotspot>(true).OrderBy(h => h.Identifier).ToArray();
            station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
            Require(station && station.IsReady, "Current diegetic station is ready", "Saved production presentation.");
            eraControls = new[] { station.Controls[HelmetDiegeticStation.Command.PreviousEra], station.Controls[HelmetDiegeticStation.Command.NextEra] };
            Require(!UnityEngine.Object.FindObjectsOfType<HelmetEraButton>().Any(),
                "Legacy era buttons remain retired while current controls are active", "The saved fallback buttons are still checked before Play.");
            information = new SerializedObject(tour).FindProperty("informationText").objectReferenceValue as TMP_Text;
            table = MuseumText.Table; language = MuseumText.Channel;
            Require(content && binding && era && inspection && window && table && language && information && eraControls.Length == 2 && eraControls.All(c => c && c.Target.isActiveAndEnabled),
                "Saved runtime dependencies are intact", "content=" + (bool)content + "; binding=" + (bool)binding +
                "; era=" + (bool)era + "; inspection=" + (bool)inspection + "; window=" + (bool)window +
                "; table=" + (bool)table + "; language=" + (bool)language + "; information=" + (bool)information +
                "; current era controls=" + eraControls.Length);
            SetupInput(); yield return Wait(.4f);
            if (report.diagnosticOnly) { yield return DiagnoseSuperLiteObservation(); yield break; }
            era.ShowEra(1); yield return Wait(.35f);
            foreach (var point in hotspots) point.Activate();
            var selected = hotspots.Single(h => h.Identifier == "front-window"); selected.Activate();
            Require(tour.VisitedCount == 3 && hotspots.All(h => h.IsVisited), "API fixture seeds exactly three completed observations", "Activate is a labelled fixture, not an XR interaction test.");
            yield return Wait(.15f);
            Require(ExplanationElapsed() > .05f, "Selected explanation has an advancing timer before transitions", "elapsed=" + ExplanationElapsed());
            foreach (int index in new[] { 0, 1, 2 })
            {
                float elapsed = ExplanationElapsed();
                era.ShowEra(index); yield return Wait(.35f);
                Require(ExplanationElapsed() + .005f >= elapsed, "Era change does not restart selected explanation timer / " + index, "read-only elapsed=" + ExplanationElapsed());
                var demo = UnityEngine.Object.FindObjectOfType<HelmetConnectionDemonstration>();
                Require(demo, "Connection demonstration exists", "Existing production component.");
                foreach (var nextLanguage in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
                {
                    // Each language has its own explicit playback fixture. Diagnostic
                    // screenshot rendering may outlast a complete demonstration.
                    demo.Play(HelmetExplorationController.Clue.Air); yield return Wait(.25f);
                    float playback = demo.NormalizedProgress, timer = ExplanationElapsed();
                    language.SetLanguage(nextLanguage); yield return Wait(.15f);
                    Require(demo.IsPlaying && demo.NormalizedProgress + .001f >= playback,
                        "Language does not restart connection animation / " + index + "/" + nextLanguage,
                        "progress before=" + playback + "; after=" + demo.NormalizedProgress);
                    Require(ExplanationElapsed() + .005f >= timer, "Language does not restart observation timer / " + index + "/" + nextLanguage,
                        "before=" + timer + "; after=" + ExplanationElapsed());
                    CheckEra(index, nextLanguage, selected);
                    if (index == 0)
                    {
                        // Explicit API fixtures also render every full early card, rather than
                        // accepting a translated caption beside an English-fallback detail.
                        foreach (var point in hotspots)
                        {
                            point.Activate();
                            CheckText(information, point.Title + "\n" + point.Description, "All early selected cards / " + nextLanguage + "/" + point.Identifier);
                            CaptureLabels("early-" + nextLanguage + "-" + point.Identifier + "-card.png", new[] { information }, "Full current early observation card; diagnostic view.");
                        }
                        selected.Activate();
                    }
                    CaptureLabels(index + "-" + nextLanguage + "-caption.png", new[] { content.Instructions }, "Current front exhibit caption; diagnostic view.");
                    CaptureLabels(index + "-" + nextLanguage + "-buttons.png", eraControls.SelectMany(b => new[] { b.Caption }), "Both current diegetic era-button captions; diagnostic view.");
                }
                demo.Stop();
                yield return CurrentEraInteractions(index);
                selected.Activate();
            }
            // Exercise the other important source transition with the same selected ID.
            era.ShowEra(0); yield return Wait(.35f);
            foreach (var nextLanguage in new[] { MuseumLanguage.English, MuseumLanguage.Chinese, MuseumLanguage.TraditionalChinese })
            {
                language.SetLanguage(nextLanguage); yield return Wait(.1f);
                CheckEra(0, nextLanguage, selected);
            }
            era.ShowEra(1); yield return Wait(.35f); language.SetLanguage(MuseumLanguage.English); yield return Wait(.1f);
            CheckEra(1, language.Current, selected);
            yield return PressEra(HelmetDiegeticStation.Command.PreviousEra);
            CheckEra(0, language.Current, selected);
            yield return PressEra(HelmetDiegeticStation.Command.NextEra);
            CheckEra(1, language.Current, selected);
            // The optional legacy interaction remains supported by the saved prefab.
            // Preserve its original held-window deferral assertions independently.
            station.enabled = false; yield return Wait(.3f);
            yield return HeldWindowDefersCaption(selected);
            yield return LegacyEarlySurfaceContacts();
            station.enabled = true; yield return Wait(.3f);
            Require(station.Controls[HelmetDiegeticStation.Command.Window].Target.isActiveAndEnabled && !window.enabled,
                "Current station is restored after legacy-path fixtures", "No production asset was saved.");
            ParkHands();
        }

        static void CheckEra(int index, MuseumLanguage currentLanguage, HelmetHotspot selected)
        {
            string suffix = index + "/" + currentLanguage;
            Require(era.CurrentIndex == index && binding.AppliedEraIndex == index, "Requested and applied era agree / " + suffix,
                "request=" + era.CurrentIndex + "; applied=" + binding.AppliedEraIndex);
            Require(binding.ActiveShell && binding.ActiveShell.enabled && binding.ActiveGlass && binding.ActiveGlass.enabled &&
                binding.IsRefinedActive && binding.IsAdditionalVisualActive == (index != 1),
                "Applied geometry matches visitor caption / " + suffix, "shell=" + binding.ActiveShell.name + "; glass=" + binding.ActiveGlass.name);
            CheckText(content.Instructions, Caption(index), "Exhibit caption / " + suffix);
            if (index == 0) Require(!content.Instructions.text.Contains("Mark V"), "Early caption has no Mark V attribution / " + suffix, content.Instructions.text);
            foreach (var point in hotspots)
            {
                var original = report.markVPoints.Single(p => p.identifier == point.Identifier);
                string expectedTitle = index == 0 ? EarlyTitle(point.Identifier) : index == 2 ? SuperLiteTitle(point.Identifier) : original.title;
                string expectedDetail = index == 0 ? EarlyModelDetail(point.Identifier) : index == 2 ? SuperLiteDetail(point.Identifier) : original.description;
                Require(point.Title == expectedTitle && point.Description == expectedDetail,
                    "Observation content follows applied era / " + suffix + "/" + point.Identifier,
                    point.Title + "\n" + point.Description);
                if (index == 2)
                {
                    var attachment = content.Attachments.Single(a => a.hotspot == point);
                    Require(attachment.surface && attachment.surface.enabled &&
                        Vector3.Distance(point.transform.position, attachment.surface.transform.TransformPoint(attachment.localPoint)) < .0002f,
                        "SuperLite point follows saved mesh attachment / " + suffix + "/" + point.Identifier, point.transform.position.ToString("F6"));
                }
                else if (index == 0)
                {
                    var surface = point.Identifier == "front-window" ? binding.EarlyVisual.glass :
                        point.Identifier == "bonnet" ? binding.EarlyVisual.shell :
                        binding.EarlyVisual.renderers.Single(r => r.name == "Breastplate_DishedCopper");
                    Require(surface && Vector3.Distance(point.transform.position, surface.bounds.center) < .3f,
                        "Early observation point follows the sourced model / " + suffix + "/" + point.Identifier,
                        point.transform.position.ToString("F6"));
                }
                else Require(Vector3.Distance(point.transform.localPosition, original.position) < .0002f &&
                    Quaternion.Angle(point.transform.localRotation, original.rotation) < .02f && Vector3.Distance(point.transform.localScale, original.scale) < .0002f,
                    "Original observation local pose restored / " + suffix + "/" + point.Identifier, point.transform.localPosition.ToString("F6"));
            }
            Require(tour.VisitedCount == 3 && hotspots.All(h => h.IsVisited), "Era/language retains all exact visited identities / " + suffix, "count=" + tour.VisitedCount);
            CheckText(information, selected.Title + "\n" + selected.Description, "Selected observation card remains current / " + suffix);
            foreach (var control in eraControls)
            {
                string english = control == eraControls[0] ? "Previous era" : "Next era";
                string key = control == eraControls[0] ? "era.previous" : "era.next";
                Require(table.Get(key, MuseumLanguage.English) == english,
                    "Original era source key is retained / " + suffix + "/" + key, key);
                if (station.isActiveAndEnabled)
                {
                    Require(control.Target.isActiveAndEnabled, "Current era target is active / " + suffix + "/" + key, control.name);
                    CheckText(control.Caption, english, "Current era button / " + suffix + "/" + key);
                }
            }
        }

        static IEnumerator PressEra(HelmetDiegeticStation.Command command)
        {
            int before = era.CurrentIndex;
            int expected = (before + (command == HelmetDiegeticStation.Command.NextEra ? 1 : 2)) % 3;
            yield return RayClick(command);
            Require(era.CurrentIndex == expected && binding.AppliedEraIndex == expected,
                "Current era command confirms the requested step / " + command,
                "before=" + before + "; expected=" + expected + "; actual=" + era.CurrentIndex);
        }

        static IEnumerator RayClick(HelmetDiegeticStation.Command command)
        {
            var control = station.Controls[command];
            var target = control.Target;
            var hit = target.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy);
            var point = hit.bounds.center;
            if (command == HelmetDiegeticStation.Command.Window && hit is BoxCollider box)
            {
                var offset = box.size * .22f;
                if (box.size.x <= box.size.y && box.size.x <= box.size.z) offset.x = 0;
                else if (box.size.y <= box.size.z) offset.y = 0;
                else offset.z = 0;
                point = box.transform.TransformPoint(box.center + offset);
            }
            var outward = -target.transform.forward;
            if (command == HelmetDiegeticStation.Command.Window && hit is BoxCollider visor)
            {
                var size = visor.size;
                Vector3 axis = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
                outward = visor.transform.TransformDirection(axis).normalized;
                if (Vector3.Dot(outward, point - inspection.transform.TransformPoint(inspection.SectionCenterLocal)) < 0) outward = -outward;
            }
            Vector3 position = point + outward * .48f;
            Quaternion rotation = Quaternion.LookRotation(point - position, Vector3.up);
            int events = 0, confirmed = control.ConfirmedCount; string interactor = "none";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; interactor = args.interactorObject.GetType().Name; };
            target.selectEntered.AddListener(listener);
            try
            {
                QueueController(rightDevice, position, rotation, false); yield return Wait(.35f);
                Require(target.isHovered, "Production ray hovers current control / " + command, RayDetail());
                QueueController(rightDevice, position, rotation, true); yield return Wait(.22f);
                Require(events == 1 && interactor == nameof(XRRayInteractor) && control.ConfirmedCount == confirmed,
                    "Production grip presses exactly once before confirmation / " + command,
                    "events=" + events + "; interactor=" + interactor + "; confirmed=" + control.ConfirmedCount);
                QueueController(rightDevice, position, rotation, false); yield return Wait(.7f);
                Require(control.ConfirmedCount == confirmed + 1,
                    "Release on current control confirms exactly once / " + command, "confirmed=" + control.ConfirmedCount);
                ParkHands(); yield return Wait(.15f);
            }
            finally { if (target) target.selectEntered.RemoveListener(listener); }
        }

        static string RayDetail() => string.Join(" | ", rightController.GetComponentsInChildren<XRRayInteractor>(true).Select(ray =>
        {
            bool found = ray.TryGetCurrent3DRaycastHit(out var hit);
            var origin = ray.rayOriginTransform ? ray.rayOriginTransform : ray.transform;
            return ray.name + ": active=" + ray.isActiveAndEnabled + "; hit=" + (found && hit.collider ? hit.collider.name : "none") +
                "; hitDistance=" + (found ? hit.distance : -1f) + "; origin=" + origin.position.ToString("F6") +
                "; forward=" + origin.forward.ToString("F6") + "; detection=" + ray.hitDetectionType +
                "; mask=" + ray.raycastMask.value + "; triggers=" + ray.raycastTriggerInteraction +
                "; hovered=" + string.Join(",", ray.interactablesHovered.Select(t => t.transform.name));
        }));

        static bool ProductionRayFirstHits(XRBaseInteractable target) =>
            rightController.GetComponentsInChildren<XRRayInteractor>(true).Any(ray => ray.isActiveAndEnabled &&
                ray.TryGetCurrent3DRaycastHit(out var hit) && target.colliders.Contains(hit.collider));

        static IEnumerator CurrentEraInteractions(int index)
        {
            Require(binding.AppliedEraIndex == index, "Interaction fixture uses the applied era / " + index, "actual=" + binding.AppliedEraIndex);
            var active = index == 0 ? binding.EarlyVisual.renderers : index == 2 ? binding.AdditionalVisual.renderers : binding.RefinedRenderers;
            Require(active.Length > 0 && active.All(r => r && r.enabled && r.gameObject.activeInHierarchy &&
                r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh && r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 2),
                "Applied era has imported visible meshes / " + index, "renderers=" + active.Length);
            foreach (var shader in active.SelectMany(r => r.sharedMaterials).Select(m => m.shader).Distinct())
                Require(shader && shader.isSupported && !ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error"),
                    "Active shader compiles for this graphics device / " + index + "/" + shader.name, shader.name);
            if (index == 0)
            {
                var front = hotspots.Single(h => h.Identifier == "front-window");
                Vector3 outward = binding.EarlyVisual.window.parent.TransformDirection(Vector3.back);
                Require(Vector3.Dot(front.transform.position - binding.ActiveGlass.bounds.center, outward) > .03f,
                    "Early front hotspot is outside the visitor-facing glass", "offset=" + (front.transform.position - binding.ActiveGlass.bounds.center));
            }
            yield return RayClick(HelmetDiegeticStation.Command.Window);
            Require(inspection.WindowAngle > 90f, "Current ray opens the actual window / " + index, "angle=" + inspection.WindowAngle);
            yield return RayClick(HelmetDiegeticStation.Command.Window);
            Require(inspection.WindowAngle < .1f, "Current ray closes the actual window / " + index, "angle=" + inspection.WindowAngle);
            foreach (var point in hotspots)
            {
                var target = point.GetComponent<XRSimpleInteractable>();
                Vector3 position = Vector3.zero; Quaternion rotation = Quaternion.identity;
                int selections = 0, releases = 0; string kind = "";
                UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { selections++; kind = args.interactorObject.GetType().Name; };
                UnityEngine.Events.UnityAction<SelectExitEventArgs> release = args => { if (args.interactorObject is XRRayInteractor) releases++; };
                target.selectEntered.AddListener(listener);
                target.selectExited.AddListener(release);
                try
                {
                    int attempts = 0; bool acquired = false;
                    foreach (var approach in ObservationRays(target))
                    {
                        position = approach.origin;
                        rotation = Quaternion.LookRotation(approach.direction,
                            Mathf.Abs(Vector3.Dot(approach.direction, Vector3.up)) > .98f ? Vector3.forward : Vector3.up);
                        QueueController(rightDevice, position, rotation, false); yield return Wait(.3f); attempts++;
                        // Authored cone casting and ray-origin transforms are retained.
                        // A thin Physics ray is only a candidate prefilter; the actual
                        // production ray must report this marker as its first hit.
                        if (target.isHovered && ProductionRayFirstHits(target)) { acquired = true; break; }
                    }
                    Require(acquired, "Current production ray hovers observation as first surface / " + index + "/" + point.Identifier,
                        "candidate poses=" + attempts + "; " + RayDetail());
                    QueueController(rightDevice, position, rotation, true); yield return Wait(.2f);
                    Require(selections == 1 && kind == nameof(XRRayInteractor) && tour.HasVisited(point.Identifier) && !station.RotationGesture.IsDragging,
                        "Current production ray selects observation / " + index + "/" + point.Identifier,
                        "selections=" + selections + "; kind=" + kind + "; origin=" + position + "; marker=" + point.transform.position + "; " + RayDetail());
                    CheckText(information, point.Title + "\n" + point.Description, "Ray-selected observation / " + index + "/" + point.Identifier);
                    QueueController(rightDevice, position, rotation, false); yield return Wait(.2f);
                    Require(releases == 1 && !target.isSelected,
                        "Current production ray releases observation once / " + index + "/" + point.Identifier, "releases=" + releases);
                    ParkHands(); yield return Wait(.15f);
                }
                finally { target.selectEntered.RemoveListener(listener); target.selectExited.RemoveListener(release); }
            }
            yield return RayClick(HelmetDiegeticStation.Command.Section);
            var section = binding.GetComponent<Museum.Experience.HelmetSectionSurface>(); section.Refresh();
            Require(inspection.IsCutaway && section.TriangleCount > 0, "Current ray produces a visible section cap / " + index, "triangles=" + section.TriangleCount);
            var slider = station.SectionGesture;
            var sliderHit = slider.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy);
            Vector3 sliderPosition = sliderHit.bounds.center - slider.transform.forward * .48f;
            Quaternion sliderRotation = Quaternion.LookRotation(sliderHit.bounds.center - sliderPosition, Vector3.up);
            QueueController(rightDevice, sliderPosition, sliderRotation, false); yield return Wait(.3f);
            QueueController(rightDevice, sliderPosition, sliderRotation, true); yield return Wait(.2f);
            Require(slider.IsDragging && slider.interactorsSelecting[0] is XRRayInteractor, "Current ray holds the section rail / " + index, RayDetail());
            float before = inspection.SectionOffset; Vector3 normal = inspection.SectionNormalLocal;
            Vector3 movement = station.SectionRail.right * .04f;
            QueueController(rightDevice, sliderPosition + movement, sliderRotation, true); yield return Wait(.2f);
            Require(Mathf.Abs(inspection.SectionOffset - before - .04f * inspection.SectionRange / station.SectionRailHalfTravel) < .001f &&
                Vector3.Distance(normal, inspection.SectionNormalLocal) < .001f, "Current rail moves only cut depth / " + index, "offset=" + inspection.SectionOffset);
            QueueController(rightDevice, sliderPosition + movement, sliderRotation, false); yield return Wait(.2f); ParkHands(); yield return Wait(.15f);
            CaptureModel("era-" + index + "-section.png");
            yield return RayClick(HelmetDiegeticStation.Command.Section);
            Require(!inspection.IsCutaway, "Current ray restores the full exterior / " + index, "cut=" + inspection.IsCutaway);
            BodyRayPose(out Vector3 pose, out Quaternion rotationBody);
            Vector3 fixedCentre = inspection.transform.TransformPoint(inspection.SectionCenterLocal); Quaternion beforeRotation = inspection.transform.rotation;
            QueueController(rightDevice, pose, rotationBody, false); yield return Wait(.3f);
            QueueController(rightDevice, pose, rotationBody, true); yield return Wait(.2f);
            Require(station.RotationGesture.IsDragging && station.RotationGesture.interactorsSelecting[0] is XRRayInteractor,
                "Current ray holds an actual shell collider / " + index, RayDetail());
            QueueController(rightDevice, pose + Camera.main.transform.right * .06f, rotationBody, true); yield return Wait(.25f);
            Require(Quaternion.Angle(beforeRotation, inspection.transform.rotation) > 1f &&
                Vector3.Distance(fixedCentre, inspection.transform.TransformPoint(inspection.SectionCenterLocal)) < .001f,
                "Current shell rotation retains its visible centre / " + index, "angle=" + Quaternion.Angle(beforeRotation, inspection.transform.rotation));
            QueueController(rightDevice, pose, rotationBody, false); yield return Wait(.2f); ParkHands(); yield return Wait(.15f);
            var currentLanguage = language.Current; int count = inspection.ResetCount;
            yield return RayClick(HelmetDiegeticStation.Command.Reset);
            Require(inspection.ResetCount == count + 1 && Quaternion.Angle(beforeRotation, inspection.transform.rotation) < .1f &&
                era.CurrentIndex == index && language.Current == currentLanguage && tour.VisitedCount == 3,
                "Current ray reset preserves era, language and discoveries / " + index,
                "resets=" + inspection.ResetCount + "; era=" + era.CurrentIndex + "; visited=" + tour.VisitedCount);
            CaptureModel("era-" + index + "-exterior.png");
        }

        static IEnumerable<Ray> ObservationRays(XRBaseInteractable target)
        {
            // XRI may hover multiple valid targets along a ray, including a small
            // marker behind the shell. The production station correctly selects
            // the first surface. Find a real clear approach for this era instead
            // of assuming the Mark V breastplate's high/front pose also exposes
            // the lower SuperLite neck seal. No collider or filter is disabled.
            var directions = new[] { new Vector3(0, .35f, -1), new Vector3(-.7f, .35f, -1), new Vector3(.7f, .35f, -1),
                new Vector3(-1, .2f, -.3f), new Vector3(1, .2f, -.3f), Vector3.back, Vector3.left, Vector3.right,
                new Vector3(-.7f, -.25f, -1), new Vector3(.7f, -.25f, -1), new Vector3(0, -.25f, -1),
                Vector3.up, new Vector3(-1, .35f, 1), new Vector3(1, .35f, 1), new Vector3(0, .35f, 1) };
            Physics.SyncTransforms();
            var marker = target.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy).bounds.center;
            foreach (var direction in directions)
            {
                var outside = marker + inspection.transform.TransformDirection(direction.normalized) * .45f;
                // A raycast that starts inside a solid can miss that enclosing
                // collider. Reject such origins, including the plinth below a
                // neck-seal marker; leave room for the controller's open hand.
                if (Physics.OverlapSphere(outside, .08f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => c && !c.GetComponentInParent<XRBaseInteractor>())) continue;
                var ray = new Ray(outside, (marker - outside).normalized);
                foreach (var hit in Physics.RaycastAll(ray, .5f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
                {
                    if (!hit.collider || hit.collider.GetComponentInParent<XRBaseInteractor>()) continue;
                    if (target.colliders.Contains(hit.collider))
                    {
                        yield return ray;
                    }
                    break;
                }
            }
        }

        static IEnumerator DiagnoseSuperLiteObservation()
        {
            era.ShowEra(2); yield return Wait(.5f); Physics.SyncTransforms();
            var point = hotspots.Single(h => h.Identifier == "breastplate");
            var target = point.GetComponent<XRSimpleInteractable>();
            var marker = target.colliders.First(c => c && c.enabled && c.gameObject.activeInHierarchy);
            report.geometryDiagnostics.Add("marker=" + point.transform.position.ToString("F6") + "; bounds=" + marker.bounds + "; localScale=" + point.transform.lossyScale);
            foreach (var surface in binding.AdditionalVisual.renderers.Where(r => r.name.Contains("Neck") || r.name.Contains("Clamp")))
                report.geometryDiagnostics.Add("mesh=" + surface.name + "; bounds=" + surface.bounds + "; transform=" + surface.transform.position.ToString("F6"));
            foreach (var hit in UnityEngine.Object.FindObjectsOfType<Collider>().Where(c => c.name == "Breastplate Base" || c.name == "Bonnet Capsule" || c.name == "Plinth"))
                report.geometryDiagnostics.Add("solid=" + hit.name + "; type=" + hit.GetType().Name + "; bounds=" + hit.bounds + "; closestMarker=" + hit.ClosestPoint(marker.bounds.center).ToString("F6"));
            CaptureModel("superlite-neck-seal-diagnostic.png");
            int valid = 0;
            foreach (float distance in new[] { .25f, .30f, .45f, .65f })
                foreach (float slope in new[] { .9f, .4f, 0f, -.3f })
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        float radians = angle * Mathf.Deg2Rad;
                        var direction = inspection.transform.TransformDirection(new Vector3(Mathf.Sin(radians), slope, -Mathf.Cos(radians)).normalized);
                        Vector3 position = marker.bounds.center + direction * distance;
                        var solids = Physics.OverlapSphere(position, .08f, ~0, QueryTriggerInteraction.Ignore)
                            .Where(c => c && !c.GetComponentInParent<XRBaseInteractor>()).ToArray();
                        string detail = "distance=" + distance + "; slope=" + slope + "; angle=" + angle + "; origin=" + position.ToString("F6");
                        if (solids.Length > 0)
                        { report.geometryDiagnostics.Add(detail + "; rejected solid origin=" + string.Join(",", solids.Select(c => c.name))); continue; }
                        var ray = new Ray(position, -direction);
                        var first = Physics.RaycastAll(ray, distance + .05f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance)
                            .FirstOrDefault(h => h.collider && !h.collider.GetComponentInParent<XRBaseInteractor>());
                        detail += "; thinFirst=" + (first.collider ? first.collider.name : "none");
                        if (!target.colliders.Contains(first.collider)) { report.geometryDiagnostics.Add(detail); continue; }
                        Quaternion rotation = Quaternion.LookRotation(-direction, Vector3.up);
                        QueueController(rightDevice, position, rotation, false); yield return Wait(.3f);
                        bool actual = target.isHovered && ProductionRayFirstHits(target);
                        report.geometryDiagnostics.Add(detail + "; actualFirstTarget=" + actual + "; " + RayDetail());
                        if (actual) valid++;
                    }
            report.geometryDiagnostics.Add("Legal actual production first-target candidates=" + valid);
            ParkHands(); yield return Wait(.2f);
        }

        static void BodyRayPose(out Vector3 pose, out Quaternion rotation)
        {
            var gesture = station.RotationGesture; var bounds = binding.ActiveShell.bounds;
            var directions = new[] { new Vector3(-.7f, .45f, -1), new Vector3(.7f, .45f, -1), new Vector3(-1, .6f, -.15f),
                new Vector3(1, .6f, -.15f), new Vector3(-.7f, .45f, 1), new Vector3(.7f, .45f, 1), Vector3.up };
            foreach (var direction in directions) foreach (float height in new[] { .55f, .25f, .75f, 0f })
            {
                Vector3 aim = bounds.center + Vector3.up * bounds.extents.y * height;
                Vector3 outside = aim + inspection.transform.TransformDirection(direction.normalized) * (bounds.extents.magnitude + .4f);
                var ray = new Ray(outside, (aim - outside).normalized);
                foreach (var hit in Physics.RaycastAll(ray, Vector3.Distance(outside, aim) + .04f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
                {
                    if (!hit.collider || hit.collider.GetComponentInParent<XRBaseInteractor>()) continue;
                    if (gesture.interactionManager.TryGetInteractableForCollider(hit.collider, out IXRInteractable owner) && ReferenceEquals(owner, gesture))
                    {
                        pose = outside;
                        rotation = Quaternion.LookRotation(hit.point - outside, Mathf.Abs(Vector3.Dot(ray.direction, Vector3.up)) > .98f ? Vector3.forward : Vector3.up);
                        return;
                    }
                    break;
                }
            }
            throw new InvalidOperationException("No unobstructed registered shell collider ray was found for era " + era.CurrentIndex);
        }

        static IEnumerator LegacyEarlySurfaceContacts()
        {
            era.ShowEra(0); yield return Wait(.35f);
            var grab = binding.GetComponent<XRGrabInteractable>(); var surface = binding.GetComponent<MuseumGripSurface>();
            Require(grab.enabled && !station.enabled && surface && surface.Cache, "Traditional surface fixture uses restored legacy path", "Fixed station disabled only in this explicit fixture.");
            var earlyFilters = binding.EarlyVisual.renderers.Select(r => r.GetComponent<MeshFilter>()).ToArray();
            Require(earlyFilters.All(f => surface.ModelFilters.Contains(f)) && earlyFilters.All(f => surface.Cache.entries.Any(e => e.source == f.sharedMesh)),
                "Saved grip filters and vertex cache contain every active early part", "active parts=" + earlyFilters.Length);
            foreach (bool opened in new[] { false, true })
            {
                inspection.SetWindowAngle(opened ? 45f : 0f); yield return Wait(.15f);
                foreach (var point in hotspots)
                {
                    bool contact = surface.TryGetContact(grab, point.transform.position, out var position, out var normal);
                    Require(contact && !surface.LastContactUsedColliderFallback && surface.LastContactRenderer &&
                        binding.EarlyVisual.renderers.Contains(surface.LastContactRenderer) && surface.LastTestedTriangleCount > 0 && normal.sqrMagnitude > .99f,
                        "Traditional grip resolves actual early triangles / " + opened + "/" + point.Identifier,
                        "renderer=" + (surface.LastContactRenderer ? surface.LastContactRenderer.name : "none") + "; fallback=" + surface.LastContactUsedColliderFallback + "; triangles=" + surface.LastTestedTriangleCount);
                    Require(Vector3.Distance(position, point.transform.position) < .08f,
                        "Traditional contact remains close to the shown early part / " + opened + "/" + point.Identifier, "distance=" + Vector3.Distance(position, point.transform.position));
                }
            }
            inspection.SetWindowAngle(0f); yield return Wait(.15f);
        }

        static void CaptureModel(string file)
        {
            Vector3 centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            Vector3 position = centre + inspection.transform.TransformDirection(new Vector3(-.38f, .18f, -1.0f));
            Quaternion rotation = Quaternion.LookRotation(centre - position, Vector3.up);
            Museum.Diegetic.Editor.HelmetDiegeticPreview.CaptureView(Path.Combine(Output, file), position, rotation, 45f, 4f / 3f, .01f);
            report.captures.Add(new Capture { file = file, purpose = "Diagnostic monocular model view; not headset readability.", position = position, rotation = rotation, fieldOfView = 45f });
        }

        static IEnumerator HeldWindowDefersCaption(HelmetHotspot selected)
        {
            inspection.SetWindowAngle(0); yield return Wait(.2f);
            var collider = window.colliders[0]; Vector3 outward = inspection.transform.rotation * Vector3.back;
            Vector3 touch = MuseumPhysicalHandsTestPose.WindowTouch(collider, rightController, Quaternion.identity, outward, collider.bounds.center + outward * .009f);
            Vector3 position = touch + outward * .5f; Quaternion rotation = Quaternion.LookRotation(touch - position, Vector3.up);
            QueueController(rightDevice, position, rotation, false); yield return Wait(.35f);
            QueueController(rightDevice, position, rotation, true); yield return Wait(.25f);
            Require(window.isSelected && inspection.IsWindowHeld && window.interactorsSelecting[0] is XRRayInteractor,
                "Production ray holds the original window", "No select events are invoked directly.");
            era.ShowEra(2); yield return Wait(.35f);
            Require(era.CurrentIndex == 2 && binding.AppliedEraIndex == 1 && !binding.IsAdditionalVisualActive && inspection.IsWindowHeld,
                "Held window defers actual geometry while accepting era request", "requested=" + era.CurrentIndex + "; applied=" + binding.AppliedEraIndex);
            CheckText(content.Instructions, MarkVCaption, "Deferred request retains Mark V caption");
            language.SetLanguage(MuseumLanguage.Chinese); yield return Wait(.25f);
            CheckText(content.Instructions, MarkVCaption, "Language switch while held follows actual Mark V geometry");
            Require(tour.VisitedCount == 3, "Pending request preserves observations", "count=" + tour.VisitedCount);
            yield return Wait(.3f); // A long hold avoids treating the release as a short window click.
            QueueController(rightDevice, position, rotation, false); yield return Wait(.4f); ParkHands(); yield return Wait(.2f);
            Require(!inspection.IsWindowHeld && binding.AppliedEraIndex == 2, "Window release applies pending era", "applied=" + binding.AppliedEraIndex);
            CheckEra(2, language.Current, selected);
            CaptureLabels("deferred-release-Chinese-caption.png", new[] { content.Instructions }, "Caption after releasing pending era request.");
        }

        static string Caption(int index) => index == 0 ? EarlyCaption : index == 1 ? MarkVCaption : SuperLiteCaption;
        static string EarlyTitle(string id) => id == "front-window" ? "Front viewport" : id == "bonnet" ? "Bonnet shell" : "Breastplate";
        static string EarlyModelDetail(string id) => id == "front-window" ? HelmetEraVisitorContent.EarlyModelFrontDetails :
            id == "bonnet" ? HelmetEraVisitorContent.EarlyModelShellDetails : HelmetEraVisitorContent.EarlyModelBreastplateDetails;
        static string SuperLiteTitle(string id) => id == "front-window" ? "Front window" : id == "bonnet" ? "Helmet shell" : "Neck seal";
        static string SuperLiteDetail(string id) => id == "front-window" ? "Window and rim. Side opening is a museum demonstration."
            : id == "bonnet" ? "Inspect the upper shell of this lightweight helmet study." : "Inspect the neck seal and surrounding clamp below the shell.";
        static float ExplanationElapsed()
        {
            var field = typeof(HelmetHotspotTour).GetField("explanationElapsed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Read-only explanation timer evidence is unavailable.");
            return (float)field.GetValue(tour);
        }
        static void CheckText(TMP_Text label, string source, string name)
        {
            var entry = table.Entries.FirstOrDefault(e => e.English == source);
            Require(entry.English == source && !string.IsNullOrWhiteSpace(entry.Chinese) && !string.IsNullOrWhiteSpace(entry.TraditionalChinese) &&
                entry.Chinese != source && entry.TraditionalChinese != source,
                "Saved full trilingual source exists / " + name, source);
            var localized = label.GetComponent<LocalizedSourceText>();
            Require(localized && localized.SourceEnglish == source && label.text == entry.For(language.Current),
                "Actual dynamic source and exact translation / " + name,
                "source=" + (localized ? localized.SourceEnglish : "missing") + "; text=" + label.text);
            CheckLayout(label, name);
        }
        static void CheckLayout(TMP_Text label, string name)
        {
            label.ForceMeshUpdate(true, true);
            string plain = Regex.Replace(label.text, "<[^>]*>", "");
            string missing = !label.font ? "NO_FONT" : new string(plain.Where(c => !char.IsWhiteSpace(c)).Distinct().Where(c => !label.font.HasCharacter(c, false, false)).ToArray());
            Require(missing.Length == 0 && !label.isTextOverflowing, "Saved glyphs and layout fit / " + name,
                "missing=" + missing + "; overflowing=" + label.isTextOverflowing + "; fontSize=" + label.fontSize);
        }

        static void SetupInput()
        {
            var simulators = UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>();
            Require(simulators.Length > 0, "Official simulator is present", "Disable only its automated device-state writer.");
            foreach (var simulator in simulators) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedInput = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard); if (addedMouse) InputSystem.EnableDevice(mouse);
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Left Controller");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            hmd = InputSystem.AddDevice<XRSimulatedHMD>();
            var camera = Camera.main; var parent = camera.transform.parent;
            Vector3 position = parent ? parent.InverseTransformPoint(camera.transform.position) : camera.transform.position;
            Quaternion rotation = parent ? Quaternion.Inverse(parent.rotation) * camera.transform.rotation : camera.transform.rotation;
            var state = new XRSimulatedHMDState { isTracked = true, trackingState = 3, devicePosition = position, centerEyePosition = position,
                leftEyePosition = position + Vector3.left * .032f, rightEyePosition = position + Vector3.right * .032f,
                deviceRotation = rotation, centerEyeRotation = rotation, leftEyeRotation = rotation, rightEyeRotation = rotation };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(hmd, state)); ParkHands();
        }
        static void QueueController(XRSimulatedController device, Vector3 position, Quaternion rotation, bool grip)
        {
            var parent = (device == leftDevice ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(position), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation,
                grip = grip ? 1f : 0f }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        { QueueController(rightDevice, new Vector3(.8f, .8f, 1f), Quaternion.identity, false); QueueController(leftDevice, new Vector3(-.8f, .8f, 1f), Quaternion.identity, false); }
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        static void CaptureLabels(string file, IEnumerable<TMP_Text> labels, string purpose)
        {
            var subjects = labels.ToArray(); Require(subjects.Length > 0, "Capture contains text / " + file, purpose);
            foreach (var label in subjects) label.ForceMeshUpdate(true, true);
            var bounds = subjects[0].GetComponent<Renderer>().bounds;
            foreach (var label in subjects.Skip(1)) bounds.Encapsulate(label.GetComponent<Renderer>().bounds);
            float distance = Mathf.Max(.35f, (bounds.extents.magnitude + .06f) / Mathf.Tan(24f * Mathf.Deg2Rad));
            Vector3 position = bounds.center - subjects[0].transform.forward * distance;
            Quaternion rotation = Quaternion.LookRotation(bounds.center - position, subjects[0].transform.up);
            var temporary = new GameObject("Temporary visitor era diagnostic camera") { hideFlags = HideFlags.HideAndDontSave };
            var previous = RenderTexture.active; var target = RenderTexture.GetTemporary(1440, 1080, 24, RenderTextureFormat.ARGB32); Texture2D pixels = null;
            try
            {
                var camera = temporary.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false; camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.targetTexture = null; camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = 48; camera.aspect = 4f / 3f;
                var additional = temporary.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); additional.allowXRRendering = false; additional.renderPostProcessing = false;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); pixels.Apply(); File.WriteAllBytes(Path.Combine(Output, file), pixels.EncodeToPNG());
                report.captures.Add(new Capture { file = file, purpose = purpose, position = position, rotation = rotation, fieldOfView = 48 });
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(temporary); }
        }
        static void Require(bool passed, string name, string detail)
        { report.checks.Add(new Check { name = name, detail = detail, passed = passed }); if (!passed) throw new InvalidOperationException(name + ": " + detail); }
        static void OnLog(string message, string stack, LogType type)
        {
            if (report == null) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + message + "\n" + stack);
            else if (type == LogType.Warning) report.warnings.Add(message);
        }
        static void Finish(string failure, bool exitPlay = true)
        {
            if (finishing) return; finishing = true;
            Application.logMessageReceived -= OnLog; InputSystem.onBeforeUpdate -= FlushInput; Routines.Clear(); PendingInput.Clear();
            report.failure = failure ?? "";
            foreach (var file in report.files) report.checks.Add(new Check { name = "Validator preserved saved source / " + file.path, passed = Hash(file.path) == file.sha256, detail = Hash(file.path) });
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, hmd, keyboard, addedMouse ? mouse : null }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedInput) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; }
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.status = string.IsNullOrEmpty(failure) && report.errors.Count == 0 && report.checks.All(c => c.passed) ? "passed" : "failed";
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            SessionState.SetInt(Prefix + "Exit", report.status == "passed" ? 0 : 1);
            Debug.Log("VISITOR_ERA_QA_" + report.status.ToUpperInvariant() + " " + Path.Combine(Output, "report.json") + (failure == null ? "" : "\n" + failure));
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
                    if (!process.WaitForExit(5000)) { process.Kill(); return "UNAVAILABLE: git timeout"; }
                    if (!stdout.Wait(1000) || !stderr.Wait(1000)) return "UNAVAILABLE: incomplete output";
                    return process.ExitCode == 0 ? stdout.Result.Trim() : "UNAVAILABLE: " + stderr.Result.Trim();
                }
            }
            catch (Exception ex) { return "UNAVAILABLE: " + ex.Message; }
        }
    }
}
#endif
