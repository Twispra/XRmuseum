#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Museum.Eras;
using Museum.Diegetic;
using Museum.Diegetic.Editor;
using Museum.Exploration;
using Museum.Hotspots;
using Museum.Inspection;
using Museum.Localization;
using Museum.Refined;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.OperatorPanel.Editor
{
    /// <summary>Tests the saved product scene. Never installs, generates or saves product assets.</summary>
    [InitializeOnLoad]
    public static class OperatorPanelInteractionValidation
    {
        const string Prefix = "Museum.OperatorPanelQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static HelmetDiegeticStation station;
        static HelmetInspectionController inspection;
        static HelmetEraSwitcher eras;
        static RefinedHelmetBinding binding;
        static HelmetConnectionDemonstration demo;
        static ActionBasedController rightController, leftController;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD head;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedMouse, changedSettings, finishing;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static double started, exitAt;
        static int lastFrame = -1, pendingExit = -1;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", SessionState.GetString(Prefix + "Run", "missing"));

        [Serializable] sealed class Check { public string name, detail, layer; public bool passed; }
        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class HotspotRecord { public string id; public Vector3 position; public bool enabled; }
        [Serializable] sealed class Report
        {
            public string utc, finishedUtc, commit, dirty, unity, failure;
            public string method = "Saved MuseumStart Play Mode. Shared baseline runner lifecycle and queued synthetic controller/HMD/keyboard states, ordinary Dynamic Input System updates through production Input Actions and XR ray selection. API drag fixtures identified separately. No direct SelectEnter/Exit, InputSystem.Update, scene saves or generation.";
            public string limitations = "No real headset/controller, comfort, reachability study, user test or executable build validation. RenderTexture images are diagnostic views of the product scene, not headset screenshots. Direct physical hand interaction is not independently validated by this runner.";
            public List<Check> checks = new List<Check>();
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<HotspotRecord> originalHotspots = new List<HotspotRecord>();
            public List<string> errors = new List<string>();
            public List<string> diagnosticCaptures = new List<string>();
        }

        static OperatorPanelInteractionValidation()
        { EditorApplication.playModeStateChanged += OnPlayMode; EditorApplication.update += Tick; }
        [MenuItem("Museum/Operator Panel/Validate saved main interactions")]
        public static void RunInEditor() => Begin();
        public static void RunBatch() => Begin();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            // The official simulator resolves keyboard/mouse bindings in Start.
            keyboard = InputSystem.AddDevice<Keyboard>("Diegetic QA keyboard");
            mouse = Mouse.current; addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>("Diegetic QA mouse");
        }
        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validation.");
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetString(Prefix + "Run", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-operator-panel-20261018-" + Guid.NewGuid().ToString("N"));
            report = new Report { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion };
            foreach (string path in new[] { ScenePath, "Packages/manifest.json", "Packages/packages-lock.json" }
                .Concat(Directory.GetFiles("Assets/Museum/Features/DiegeticUI", "*", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/Museum/Features/OperatorPanel", "*", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("ProjectSettings", "*", SearchOption.AllDirectories))
                .Concat(new[] { "Assets/Museum/Features/HelmetInspection/Runtime/HelmetInspectionController.cs", "Assets/Museum/Features/HelmetExploration/Runtime/HelmetConnectionDemonstration.cs",
                    "Assets/Museum/Features/RefinedModels/Runtime/RefinedConnectionEquipment.cs", "Assets/Museum/Features/VisitorFlow/Runtime/VisitorViewRecovery.cs",
                    "Assets/Museum/Features/Localization/Tables/MuseumTranslations.json", "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset",
                    "Assets/Museum/Features/Localization/Fonts/NotoSansSC-MuseumSubset SDF.asset", "Assets/Museum/Features/Localization/Fonts/characters-required.txt" }).Where(File.Exists).OrderBy(p => p))
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                    report.files.Add(new Fingerprint { path = path.Replace('\\', '/'), sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() });
            var savedInspection = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            if (savedInspection)
                foreach (var hotspot in savedInspection.GetComponentsInChildren<HelmetHotspot>(true))
                    report.originalHotspots.Add(new HotspotRecord { id = hotspot.Identifier, position = savedInspection.transform.InverseTransformPoint(hotspot.transform.position), enabled = hotspot.enabled });
            SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
            SessionState.SetBool(Prefix + "Active", true);
            EditorApplication.EnterPlaymode();
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
            if (EditorApplication.timeSinceStartup - started > 300) { Finish("Validation exceeded 300 seconds."); return; }
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
            if (InputSystem.devices.OfType<XRHMD>().Any(d => !(d is XRSimulatedHMD))) throw new InvalidOperationException("Real HMD found; refusing synthetic input.");
            foreach (var simulator in UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>()) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
            CheckResult(station && station.IsReady, "Saved scene starts installed diegetic station", station ? "Ready=" + station.IsReady : "Station missing");
            if (!station || !station.IsReady) throw new InvalidOperationException("Diegetic station was not ready in the saved product scene.");
            CheckResult(!station.RotationHelpLearned && !station.RotationHintsCondensed,
                "New visitor starts with complete rotation guidance", "Learned=" + station.RotationHelpLearned + "; Condensed=" + station.RotationHintsCondensed);
            inspection = station.Inspection; demo = station.Demonstration;
            eras = inspection.GetComponent<HelmetEraSwitcher>(); binding = inspection.GetComponent<RefinedHelmetBinding>();
            CheckResult(station.Controls.Count == Enum.GetValues(typeof(HelmetDiegeticStation.Command)).Length, "All commands retain registered interaction metadata", "Count=" + station.Controls.Count);
            CheckResult(!UnityEngine.Object.FindObjectsOfType<HelmetInspectionPalette>(true).Any(p => p.gameObject.activeInHierarchy), "No old following palette remains active", "Retired presentation, existing content engines retained.");
            var hotspots = inspection.GetComponentsInChildren<HelmetHotspot>(true);
            CheckResult(hotspots.Length == 3 && hotspots.All(h => h.enabled), "Three original knowledge hotspots remain enabled", string.Join(",", hotspots.Select(h => h.Identifier)));
            foreach (var original in report.originalHotspots)
            {
                var hotspot = hotspots.SingleOrDefault(h => h.Identifier == original.id);
                CheckResult(hotspot && Vector3.Distance(inspection.transform.InverseTransformPoint(hotspot.transform.position), original.position) < .0001f,
                    "Knowledge hotspot position preserved: " + original.id, hotspot ? hotspot.transform.localPosition.ToString("F4") : "Missing");
            }
            CheckResult(!inspection.GetComponent<XRGrabInteractable>().enabled && inspection.GetComponent<Rigidbody>().isKinematic, "Exhibit cannot be freely translated or dropped", "Legacy grab disabled; rigidbody pinned.");
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(c => c.name == "Left Controller");
            rightDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(rightDevice, UnityEngine.InputSystem.CommonUsages.RightHand);
            leftDevice = InputSystem.AddDevice<XRSimulatedController>(); InputSystem.SetDeviceUsage(leftDevice, UnityEngine.InputSystem.CommonUsages.LeftHand);
            head = InputSystem.AddDevice<XRSimulatedHMD>(); InputSystem.EnableDevice(keyboard); if (addedMouse) InputSystem.EnableDevice(mouse);
            QueueHead(); ParkHands(); yield return Wait(.5f);
            MuseumText.Channel.SetLanguage(MuseumLanguage.English);
            // Explicitly exercise the normal animation mode first.
            inspection.ReduceMotion = false;
            if (station.AlwaysShowHints) station.Execute(HelmetDiegeticStation.Command.Hints);
            yield return Wait(1.1f);


            var panel = station.GetComponent<OperatorPanelController>();
            CheckResult(panel && panel.EntryReady, "Controller and unchanged GUID import into saved scene", panel ? "EntryReady="+panel.EntryReady : "Missing script/component");
            if (!panel || !panel.EntryReady) throw new InvalidOperationException("Panel not ready.");
            var pedestal=station.transform.Find("Pedestal edge controls");
            var settings=pedestal.Find("Fixed settings"); var about=pedestal.Find("Fixed exhibit information");
            var entry=panel.Entry;
            var structure=OperatorPanelValidation.Validate();
            CheckResult(structure.FailedCount==0,"Installed scene and saved glyph validation passes in Play Mode", "Passed="+structure.PassedCount+" Failed="+structure.FailedCount);
            CheckResult(Enumerable.Range(0,entry.Target.selectFilters.count).Any(i=>ReferenceEquals(entry.Target.selectFilters.GetAt(i),station)) &&
                Enumerable.Range(0,entry.Target.hoverFilters.count).Any(i=>ReferenceEquals(entry.Target.hoverFilters.GetAt(i),station)), "Dynamic entry inherits station select and hover protection", "Filters registered before enabling entry");
            station.Execute(HelmetDiegeticStation.Command.Section);
            station.Execute(HelmetDiegeticStation.Command.Window);
            station.Execute(HelmetDiegeticStation.Command.Voice); yield return Wait(.8f);
            inspection.GetComponentsInChildren<HelmetHotspot>(true).First().Activate(); yield return Wait(.2f);
            var before=Snapshot();
            yield return Click(entry,"summon");
            CheckResult(station.SettingsVisible,"Entry opens settings on release","Settings="+station.SettingsVisible);
            CheckEnvelope(settings,"initial summon");
            CheckResult(before==Snapshot(),"Summon preserves specimen, preferences and active demonstration",before+" => "+Snapshot());
            var fixedPosition=settings.position; var fixedRotation=settings.rotation;
            QueueHead(new Vector3(.18f,.05f,.14f),Quaternion.Euler(0,30,0)); yield return Wait(.3f);
            CheckResult(Vector3.Distance(fixedPosition,settings.position)<.0001f && Quaternion.Angle(fixedRotation,settings.rotation)<.001f,"Head motion does not move visible panel",settings.position.ToString("F4"));
            station.Execute(HelmetDiegeticStation.Command.HeightUp); yield return Wait(.25f);
            CheckResult(Vector3.Distance(fixedPosition,settings.position)<.0001f,"Height preference does not move visible panel",settings.position.ToString("F4"));
            var oldAbout=about.position;
            yield return Click(station.Controls[HelmetDiegeticStation.Command.About],"settings to about");
            CheckResult(station.AboutVisible && !station.SettingsVisible && Vector3.Distance(oldAbout,about.position)>.1f && Quaternion.Angle(about.rotation,Quaternion.Euler(0,Camera.main.transform.eulerAngles.y,0))<.01f,
                "One-visible to one-visible transition places newly shown About at current viewing yaw",about.position.ToString("F4"));
            QueueHead(new Vector3(-.25f,0,.10f),Quaternion.Euler(0,-25,0)); yield return Wait(.3f);
            yield return Click(station.Controls[HelmetDiegeticStation.Command.Settings],"about to settings");
            CheckResult(station.SettingsVisible && !station.AboutVisible && Vector3.Distance(fixedPosition,settings.position)>.1f && Quaternion.Angle(settings.rotation,Quaternion.Euler(0,Camera.main.transform.eulerAngles.y,0))<.01f,
                "About to Settings also places at new view instead of retaining stale pose",settings.position.ToString("F4"));
            CaptureEye("01-settings");
            before=Snapshot(); yield return Click(entry,"hide");
            CheckResult(!panel.PanelShown && !settings.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled&&c.gameObject.activeInHierarchy),"Hidden settings has no active ray-blocking collider","Hidden="+!panel.PanelShown);
            CheckResult(before==Snapshot(),"Hide preserves specimen, preferences and active demonstration",before+" => "+Snapshot());
            yield return Click(entry,"resummon");
            CheckResult(panel.PanelShown,"Hidden panel remains reachable through fixed entry","Shown="+panel.PanelShown);
            yield return Click(entry,"hide before guard fixtures");
            foreach(float pitch in new[]{-20f,20f,30f})
            {
                QueueHead(Vector3.zero,Quaternion.Euler(pitch,0,0));yield return Wait(.25f);
                yield return Click(entry,"pitched-eye summon "+pitch);
                CheckResult(panel.PanelShown && Mathf.Abs(Mathf.DeltaAngle(settings.eulerAngles.x,0))<.01f,"Pitched eye can summon a yaw-only panel: "+pitch,"Shown="+panel.PanelShown+" PanelEuler="+settings.eulerAngles);
                if(!panel.PanelShown) DumpRejectedPlacement(panel,settings,pitch);
                CheckEnvelope(settings,"eye pitch "+pitch);CaptureEye("pitch-"+pitch);
                if(panel.PanelShown)yield return Click(entry,"pitched-eye hide "+pitch);
            }
            QueueHead();yield return Wait(.2f);
            foreach(var gesture in new[]{station.RotationGesture,station.SectionGesture})
            {
                gesture.BeginForValidation(Vector3.zero,Quaternion.identity);
                int confirmations=entry.ConfirmedCount;
                panel.Toggle();
                yield return Attempt(entry,true); yield return Attempt(entry,false);
                CheckResult(gesture.IsDragging && !panel.PanelShown && entry.ConfirmedCount==confirmations,"Dragging rejects entry through API and production ray: "+gesture.GestureMode,"Confirmations="+entry.ConfirmedCount);
                gesture.EndForValidation(); ParkHands(); yield return Wait(.2f);
            }
            var equipmentGesture=UnityEngine.Object.FindObjectsOfType<HelmetGestureInteractable>().First(g=>g!=station.RotationGesture&&g!=station.SectionGesture&&g.isActiveAndEnabled);
            equipmentGesture.BeginForValidation(Vector3.zero,Quaternion.identity);
            CheckResult(equipmentGesture.IsDragging,"Equipment drag fixture begins on active demonstration","Dragging="+equipmentGesture.IsDragging);
            int equipmentCount=entry.ConfirmedCount;
            panel.Toggle(); yield return Attempt(entry,true); yield return Attempt(entry,false);
            CheckResult(equipmentGesture.IsDragging && !panel.PanelShown && entry.ConfirmedCount==equipmentCount,"Equipment drag also rejects dynamic entry through station protection","Confirmations="+entry.ConfirmedCount);
            equipmentGesture.EndForValidation(); ParkHands(); yield return Wait(.3f);
            yield return Attempt(entry,false); yield return Attempt(entry,true);
            CheckResult(entry.IsPressed,"Production ray holds dynamic entry before abandonment","Pressed="+entry.IsPressed);
            QueueController(rightDevice,new Vector3(2,1,-1),Quaternion.LookRotation(Vector3.back),true); yield return Wait(.25f);
            var rightRay=rightController.GetComponentsInChildren<XRRayInteractor>(true).First();
            CheckResult(!entry.IsPressed && !((UnityEngine.XR.Interaction.Toolkit.Filtering.IXRSelectFilter)station).Process(rightRay,station.RotationGesture),"Abandoned dynamic press propagates release guard across targets","EntryPressed="+entry.IsPressed);
            BodyRayPose(out var bodyPose,out var bodyRotation);
            QueueController(rightDevice,bodyPose,bodyRotation,true); yield return Wait(.3f);
            CheckResult(!station.RotationGesture.IsDragging,"Still-held cancelled entry cannot become model rotation","Dragging="+station.RotationGesture.IsDragging);
            QueueController(rightDevice,bodyPose,bodyRotation,false); yield return Wait(.2f);
            QueueController(rightDevice,bodyPose,bodyRotation,true); yield return Wait(.25f);
            CheckResult(station.RotationGesture.IsDragging,"Fresh production grip rotates after release guard clears","Dragging="+station.RotationGesture.IsDragging);
            QueueController(rightDevice,bodyPose,bodyRotation,false); ParkHands(); yield return Wait(.3f);
            yield return Attempt(entry,false); yield return Attempt(entry,true);
            PendingInput.Enqueue(()=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F11))); yield return Wait(.3f);
            CheckResult(!entry.IsPressed && !entry.Target.isSelected && !panel.PanelShown,"F11 cancels dynamic entry without confirming or opening panel","Pressed="+entry.IsPressed+" Selected="+entry.Target.isSelected);
            PendingInput.Enqueue(()=>InputSystem.QueueStateEvent(keyboard,new KeyboardState())); ParkHands(); yield return Wait(.3f);
            station.Execute(HelmetDiegeticStation.Command.CloseDemo); station.Execute(HelmetDiegeticStation.Command.HeightReset); yield return Wait(.3f);
            // Former collision case: group centre 0.50 m to the helmet's right, then a -0.42 m settings offset.
            var helmet=inspection.transform.position; var origin=Camera.main.transform.parent;
            QueueHead(origin.InverseTransformPoint(new Vector3(helmet.x+.5f,1.65f,helmet.z-.65f)),Quaternion.Inverse(origin.rotation)); yield return Wait(.3f);
            yield return Click(entry,"right-side clearance");
            CheckEnvelope(settings,"right-side settings"); CaptureEye("02-side-clearance");
            foreach(var language in Enum.GetValues(typeof(MuseumLanguage)).Cast<MuseumLanguage>())
            {
                MuseumText.Channel.SetLanguage(language); yield return Wait(.2f);
                foreach(var text in settings.GetComponentsInChildren<TMP_Text>(false).Concat(entry.GetComponentsInChildren<TMP_Text>(false)))
                {
                    text.ForceMeshUpdate(true,true);
                    CheckResult(!text.isTextOverflowing,language+" visible text fits "+text.name,text.text);
                    CheckResult(text.text.Where(c=>!char.IsWhiteSpace(c)&&!char.IsControl(c)).All(c=>text.font.HasCharacter(c,true,false)),language+" uses saved glyphs "+text.name,text.text);
                }
                CheckResult(entry.Caption.text==MuseumText.Translate(OperatorPanelController.HideTitle),language+" entry follows current language",entry.Caption.text);
                CaptureEye("language-"+language);
            }
            yield return Click(station.Controls[HelmetDiegeticStation.Command.About],"localized about");
            CheckEnvelope(about,"about envelope");
            foreach(var language in Enum.GetValues(typeof(MuseumLanguage)).Cast<MuseumLanguage>())
            {
                MuseumText.Channel.SetLanguage(language); yield return Wait(.15f);
                foreach(var text in about.GetComponentsInChildren<TMP_Text>(false))
                {text.ForceMeshUpdate(true,true);CheckResult(!text.isTextOverflowing && text.text.Where(c=>!char.IsWhiteSpace(c)&&!char.IsControl(c)).All(c=>text.font.HasCharacter(c,true,false)),language+" About fits with saved glyphs",text.text);}
            }
            yield return Click(entry,"hide about");
        }
        static string Snapshot()
        {
            var tour=inspection.GetComponentInChildren<HelmetHotspotTour>(true);
            var info=(TMP_Text)new SerializedObject(tour).FindProperty("informationText").objectReferenceValue;
            return inspection.transform.position+"|"+inspection.transform.rotation+"|"+inspection.WindowTargetOpen+"|"+inspection.IsCutaway+"|"+eras.CurrentIndex+"|"+demo.IsVisible+"|"+demo.CurrentClue+"|"+MuseumText.Channel.Current+"|"+inspection.SoundEnabled+"|"+inspection.ReduceMotion+"|"+station.AlwaysShowHints+"|"+station.ViewHeight.OffsetMetres+"|"+info.gameObject.activeInHierarchy+"|"+string.Join(",",inspection.GetComponentsInChildren<HelmetHotspot>(true).Select(h=>tour.HasVisited(h.Identifier)));
        }
        [Serializable] sealed class ProbeObstacle { public string name; public Bounds bounds; }
        [Serializable] sealed class ProbeCandidate
        {
            public Vector3 groupPosition; public Bounds envelope; public float cost, eyeDistance;
            public string[] intersections, occluders; public bool helmetCentreClear;
        }
        [Serializable] sealed class PlacementProbe
        {
            public Vector3 eye, eyeEuler, preferredGroup; public Bounds panelLocal;
            public int candidates, rejectedDistance, rejectedViewport, rejectedOverlap, rejectedSight, rejectedCentre, clear;
            public List<ProbeObstacle> obstacles=new List<ProbeObstacle>();
            public List<ProbeCandidate> closestInViewport=new List<ProbeCandidate>();
            public List<ProbeCandidate> closestWithoutOverlap=new List<ProbeCandidate>();
        }
        static IEnumerable<Vector3> ProbeCorners(Bounds bounds)
        {
            for(int i=0;i<8;i++)yield return bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
        }
        static void DumpRejectedPlacement(OperatorPanelController controller,Transform panel,float pitch)
        {
            var view=Camera.main;var so=new SerializedObject(controller);
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
            var local=(Bounds)typeof(OperatorPanelController).GetMethod("LocalEnvelope",flags).Invoke(null,new object[]{panel});
            var worldMethod=typeof(OperatorPanelController).GetMethod("WorldEnvelope",flags);
            var forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            var facing=Quaternion.LookRotation(forward,Vector3.up);var offset=so.FindProperty("settingsOffset").vector3Value;
            var center=view.transform.position+view.transform.forward*so.FindProperty("summonDistance").floatValue-Vector3.up*so.FindProperty("summonBelowEye").floatValue-facing*(offset+local.center);
            var probe=new PlacementProbe{eye=view.transform.position,eyeEuler=view.transform.eulerAngles,preferredGroup=center,panelLocal=local};
            var surfaces=UnityEngine.Object.FindObjectsOfType<Renderer>().Where(r=>r.name=="Plinth")
                .Concat(inspection.GetComponentsInChildren<Renderer>(false))
                .Concat(demo.EquipmentExhibits.Where(e=>e.IsVisible).SelectMany(e=>e.Renderers))
                .Concat(station.transform.Find("Pedestal edge controls").GetComponentsInChildren<Renderer>(false).Where(r=>!r.transform.IsChildOf(panel)));
            foreach(var surface in surfaces.Where(r=>r&&r.enabled&&r.gameObject.activeInHierarchy).Distinct())
            {var bounds=surface.bounds;bounds.Expand(.06f);probe.obstacles.Add(new ProbeObstacle{name=surface.transform.parent.name+"/"+surface.name,bounds=bounds});}
            var candidates=new List<Vector3>{center};
            for(int depth=-2;depth<=12;depth++)for(int side=-8;side<=8;side++)for(int height=-4;height<=4;height++)candidates.Add(center+forward*(depth*.10f)+right*(side*.15f)+Vector3.up*(height*.10f));
            for(int depth=-5;depth<=4;depth++)for(int side=-4;side<=4;side++)for(int height=-4;height<=4;height++)candidates.Add(center+forward*(depth*.05f)+right*(side*.05f)+Vector3.up*(height*.05f));
            foreach(var candidate in candidates.OrderBy(p=>(p-center).sqrMagnitude))
            {
                probe.candidates++;var bounds=(Bounds)worldMethod.Invoke(null,new object[]{local,candidate+facing*offset,facing});
                float distance=Vector3.Distance(view.transform.position,bounds.center);
                if(distance<.35f||distance>1.6f){probe.rejectedDistance++;continue;}
                if(ProbeCorners(bounds).Select(p=>view.WorldToViewportPoint(p,Camera.MonoOrStereoscopicEye.Mono)).Any(p=>p.z<=view.nearClipPlane||p.x<.03f||p.x>.97f||p.y<.03f||p.y>.97f)){probe.rejectedViewport++;continue;}
                var intersections=probe.obstacles.Where(o=>bounds.Intersects(o.bounds)).Select(o=>o.name).ToArray();
                var occluders=new List<string>();
                foreach(var point in ProbeCorners(bounds).Concat(new[]{bounds.center}))
                {var direction=point-view.transform.position;var ray=new Ray(view.transform.position,direction.normalized);foreach(var obstacle in probe.obstacles)if(obstacle.bounds.IntersectRay(ray,out var hitDistance)&&hitDistance<direction.magnitude-.02f)occluders.Add(obstacle.name+"; point="+point.ToString("F3")+"; hit="+hitDistance.ToString("F3"));}
                var helmet=inspection.transform.position;bool centreClear=new Vector2(bounds.center.x-helmet.x,bounds.center.z-helmet.z).magnitude>=so.FindProperty("minimumHelmetClearance").floatValue;
                var record=new ProbeCandidate{groupPosition=candidate,envelope=bounds,cost=(candidate-center).magnitude,eyeDistance=distance,intersections=intersections,occluders=occluders.Distinct().Take(12).ToArray(),helmetCentreClear=centreClear};
                if(probe.closestInViewport.Count<30)probe.closestInViewport.Add(record);
                if(intersections.Length>0){probe.rejectedOverlap++;continue;}
                if(probe.closestWithoutOverlap.Count<30)probe.closestWithoutOverlap.Add(record);
                if(occluders.Count>0){probe.rejectedSight++;continue;}
                if(!centreClear){probe.rejectedCentre++;continue;}probe.clear++;
            }
            Directory.CreateDirectory(Output);var name="placement-probe-"+pitch+".json";File.WriteAllText(Path.Combine(Output,name),JsonUtility.ToJson(probe,true));
            CheckResult(false,"Rejected-placement diagnostic "+pitch,"File="+name+"; eye="+probe.eye+"; distance="+probe.rejectedDistance+" viewport="+probe.rejectedViewport+" overlap="+probe.rejectedOverlap+" sight="+probe.rejectedSight+" centre="+probe.rejectedCentre+" clear="+probe.clear);
        }
        static IEnumerator Attempt(HelmetPhysicalControl control,bool grip)
        {
            var collider=control.Target.colliders.First(c=>c&&c.enabled&&c.gameObject.activeInHierarchy);
            var point=collider.bounds.center;var pose=point-control.transform.forward*.48f;
            QueueController(rightDevice,pose,Quaternion.LookRotation(point-pose,Vector3.up),grip);yield return Wait(.25f);
        }
        static IEnumerator Click(HelmetPhysicalControl control,string fixture)
        {
            int count=control.ConfirmedCount;
            yield return Attempt(control,false);
            CheckResult(control.Target.isHovered,"Production ray hover: "+fixture,"Hovered="+control.Target.isHovered,"production-input");
            yield return Attempt(control,true);
            CheckResult(control.IsPressed && control.Target.interactorsSelecting.Any(i=>i is XRRayInteractor),"Production ray selects: "+fixture,"Pressed="+control.IsPressed,"production-input");
            yield return Attempt(control,false);
            CheckResult(control.ConfirmedCount==count+1,"Production release confirms once: "+fixture,"Count="+control.ConfirmedCount,"production-input");
            ParkHands();yield return Wait(.15f);
        }
        static void CheckEnvelope(Transform panel,string fixture)
        {
            var renderers=panel.GetComponentsInChildren<Renderer>(false).Where(r=>r.enabled).ToArray();
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            var corners=Enumerable.Range(0,8).Select(i=>bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))
                .Select(c=>Camera.main.WorldToViewportPoint(c,Camera.MonoOrStereoscopicEye.Mono)).ToArray();
            var distance=Vector3.Distance(Camera.main.transform.position,bounds.center);
            CheckResult(distance<=1.6f && corners.All(p=>p.z>Camera.main.nearClipPlane && p.x>=.03f && p.x<=.97f && p.y>=.03f && p.y<=.97f),"Whole panel fits current eye viewport at near distance: "+fixture,
                "Distance="+distance.ToString("F4")+"; Viewport corners="+string.Join(",",corners.Select(p=>p.ToString("F4"))));
            var helmet=inspection.transform.position;
            CheckResult(new Vector2(bounds.center.x-helmet.x,bounds.center.z-helmet.z).magnitude>=.55f,"Final panel centre keeps helmet clearance: "+fixture,bounds.ToString());
            var obstacles=station.Inspection.GetComponentsInChildren<Renderer>(false).Where(r=>r.enabled)
                .Concat(station.Controls.Values.Where(c=>!c.transform.IsChildOf(panel)&&c.gameObject.activeInHierarchy).SelectMany(c=>c.GetComponentsInChildren<Renderer>(false)).Where(r=>r.enabled))
                .Concat(UnityEngine.Object.FindObjectsOfType<Renderer>().Where(r=>r.enabled&&r.name=="Plinth")).ToArray();
            CheckResult(!obstacles.Any(r=>bounds.Intersects(r.bounds)),"Final envelope avoids specimen and fixed controls: "+fixture,bounds.ToString());
            var worldPoints=Enumerable.Range(0,8).Select(i=>bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))).Concat(new[]{bounds.center});
            CheckResult(worldPoints.All(point=>{var direction=point-Camera.main.transform.position;var ray=new Ray(Camera.main.transform.position,direction.normalized);return !obstacles.Any(r=>r.bounds.IntersectRay(ray,out var distance)&&distance<direction.magnitude-.02f);}),
                "Eye sees entire panel without plinth or specimen occlusion: "+fixture,"Eye="+Camera.main.transform.position+"; "+bounds);
        }
        static void CaptureEye(string name)
        {
            foreach(var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>())text.ForceMeshUpdate(true);
            var view=Camera.main;Directory.CreateDirectory(Output);
            HelmetDiegeticPreview.CaptureView(Path.Combine(Output,name+".png"),view.transform.position,view.transform.rotation,view.fieldOfView,view.aspect,view.nearClipPlane);
            report.diagnosticCaptures.Add(name+".png");
        }
        static void BodyRayPose(out Vector3 pose, out Quaternion rotation)
        {
            // This fixture deliberately targets a registered shell surface, never
            // the removed rotation tile. Candidate rays query the real colliders;
            // the actual hover and select are still driven by production input.
            var gesture = station.RotationGesture;
            var bounds = binding.ActiveShell.bounds;
            Vector3 centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            var directions = new[] { new Vector3(-.7f, .45f, -1), new Vector3(.7f, .45f, -1),
                new Vector3(-1, .6f, -.15f), new Vector3(1, .6f, -.15f), new Vector3(-.7f, .45f, 1), new Vector3(.7f, .45f, 1), Vector3.up };
            foreach (var direction in directions)
                foreach (float height in new[] { .55f, .25f, .75f, 0f })
                {
                    Vector3 aim = bounds.center + Vector3.up * bounds.extents.y * height;
                    var outside = aim + inspection.transform.TransformDirection(direction.normalized) * (bounds.extents.magnitude + .4f);
                    var ray = new Ray(outside, (aim - outside).normalized);
                    foreach (var hit in Physics.RaycastAll(ray, Vector3.Distance(outside, aim) + .04f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
                    {
                        if (!hit.collider || hit.collider.GetComponentInParent<XRBaseInteractor>()) continue;
                        IXRInteractable owner;
                        if (gesture.interactionManager.TryGetInteractableForCollider(hit.collider, out owner) && ReferenceEquals(owner, gesture))
                        {
                            pose = outside;
                            rotation = Quaternion.LookRotation(hit.point - outside, Vector3.up);
                            // Looking almost straight down needs a nonparallel up.
                            if (Mathf.Abs(Vector3.Dot(ray.direction, Vector3.up)) > .98f)
                                rotation = Quaternion.LookRotation(hit.point - outside, Vector3.forward);
                            return;
                        }
                        // A knowledge dot, window, another control or solid
                        // environment surface in front makes this ray unsuitable.
                        break;
                    }
                }
            throw new InvalidOperationException("No unobstructed registered helmet-body ray was found; do not substitute the removed rotation button. Body=" + bounds + "; Centre=" + centre);
        }
        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip)
        {
            var parent = (device == leftDevice ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(world), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation, grip = grip ? 1 : 0 }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        { QueueController(rightDevice, new Vector3(2, .45f, -.5f), Quaternion.identity, false); QueueController(leftDevice, new Vector3(-2, .45f, -.5f), Quaternion.identity, false); }
        static void QueueHead() => QueueHead(Vector3.zero, Quaternion.identity);
        static void QueueHead(Vector3 position, Quaternion rotation)
        {
            var state = new XRSimulatedHMDState { isTracked = true, trackingState = 3, devicePosition = position, centerEyePosition = position,
                leftEyePosition = position + rotation * Vector3.left * .032f, rightEyePosition = position + rotation * Vector3.right * .032f, deviceRotation = rotation,
                centerEyeRotation = rotation, leftEyeRotation = rotation, rightEyeRotation = rotation };
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(head, state));
        }
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        static void CheckResult(bool passed, string name, string detail, string layer = "public-api") => report.checks.Add(new Check { passed = passed, name = name, detail = detail, layer = layer });
        static void OnLog(string condition, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(condition + "\n" + stack); }
        static void Finish(string failure)
        {
            if (finishing) return; finishing = true; report.failure = failure; report.finishedUtc = DateTime.UtcNow.ToString("o");
            Application.logMessageReceived -= OnLog; InputSystem.onBeforeUpdate -= FlushInput; PendingInput.Clear();
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, head, keyboard, addedMouse ? mouse : null }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedSettings) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; changedSettings = false; }
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
            bool passed = failure == null && report.errors.Count == 0 && report.checks.All(c => c.passed);
            SessionState.SetInt(Prefix + "Exit", passed ? 0 : 1);
            Debug.Log("Diegetic UI QA: " + (passed ? "PASS" : "FAIL") + "; checks=" + report.checks.Count + "; output=" + Output);
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
