#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Museum.Exploration;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Museum.Localization.EditorTools
{
    /// <summary>Validate saved assets and their real main-scene input/lifecycle wiring. Never generate or save product assets.</summary>
    [InitializeOnLoad]
    public static class LocalizationIntegrationValidation
    {
        const string Prefix = "Museum.LocalizationIntegrationQA.";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string Root = "Assets/Museum/Features/Localization";
        static readonly string[] Names = { "Gallery masthead", "Gallery subtitle", "Arrival brand", "Arrival introduction" };
        static readonly string[] Keys = { "museum.sign.masthead", "museum.sign.gallery", "museum.sign.arrival_brand", "museum.sign.intro" };
        static readonly string[] English = { "Beneath the surface", "THE DIVING GALLERY", "The diving gallery", "One object. Many ways to explore." };
        static readonly string[] Chinese = { "海面之下", "潜水展厅", "潜水展厅", "一件展品，多种看法。" };
        static readonly bool[] Active = { true, false, true, false };
        static readonly string[] OldNames = { "Museum Title", "Museum Subtitle", "Welcome" };
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static readonly Queue<Action> PendingInput = new Queue<Action>();
        static Report report;
        static LocalizationTable table;
        static LanguageChannel channel;
        static TMP_Text[] signs;
        static LanguageButton chineseButton, englishButton;
        static HelmetExplorationController exploration;
        static HelmetConnectionDemonstration demonstration;
        static XRSimulatedController rightDevice, leftDevice;
        static XRSimulatedHMD hmd;
        static ActionBasedController rightController, leftController;
        static Keyboard keyboard;
        static Mouse mouse;
        static bool addedMouse, changedSettings, finishing;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static double started, exitAt;
        static int lastFrame = -1, pendingExit = -1;
        static View primary, mastheadView, arrivalView;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => Path.Combine(RepoRoot, "artifacts/localization/qa/runs", SessionState.GetString(Prefix + "RunId", "missing-run-id"));

        [Serializable] sealed class Check { public string kind, name, detail; public bool passed; }
        [Serializable] sealed class Fingerprint { public string path, sha256; }
        [Serializable] sealed class View { public string name; public Vector3 position; public Quaternion rotation; public float fieldOfView, aspect; }
        [Serializable] sealed class Sample { public string phase, name, text, font; public bool activeSelf, activeInHierarchy, rendererEnabled, overflow; }
        [Serializable] sealed class Capture { public string file; public View view; }
        [Serializable] sealed class Report
        {
            public string startedUtc, finishedUtc, commit, branch, dirty, unity, status = "running", failure = "";
            public string inputMethod = "Queued synthetic XR controller/HMD states in the normal Dynamic input update; production Input Actions, ray interactors and saved LanguageButton selectEntered handlers. No SelectEnter/SelectExit or TriggerProgrammatically calls. The existing simulator device writer is temporarily disabled.";
            public string limitations = "XR controller poses are automated fixtures, not physical headset/controller, reachability or OS keyboard/mouse simulator testing. Demonstration start/stop uses the public exploration API and is labelled separately. Hidden labels are enabled only in a temporary Play Mode lifecycle fixture, then restored. PNGs are monocular captures: production view and separately labelled fixed diagnostic close-ups. No comfort, user study, historical accuracy or executable build claim.";
            public List<Check> checks = new List<Check>();
            public List<Fingerprint> files = new List<Fingerprint>();
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
            public List<Sample> samples = new List<Sample>();
            public List<Capture> captures = new List<Capture>();
        }

        static LocalizationIntegrationValidation()
        { EditorApplication.playModeStateChanged += OnPlayMode; EditorApplication.update += Tick; }
        [MenuItem("Museum/Localization/Validate saved main scene (Play Mode)", priority = 21)]
        public static void RunInEditor() => Begin(false);
        // Omit -quit and -nographics: this method controls completion and captures rendered PNGs.
        public static void RunBatch() => Begin(true);

        static void Begin(bool batch)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Save or close dirty scenes explicitly; validation never saves them.");
            string runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-integration-" + Guid.NewGuid().ToString("N");
            SessionState.SetString(Prefix + "RunId", runId);
            SessionState.SetBool(Prefix + "Batch", batch);
            Directory.CreateDirectory(Output);
            report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"),
                branch = Git("branch --show-current"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion };
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var paths = new HashSet<string>(AssetDatabase.GetDependencies(ScenePath, true).Where(File.Exists), StringComparer.Ordinal);
                foreach (string path in Directory.GetFiles(Root, "*", SearchOption.AllDirectories)) paths.Add(path.Replace('\\', '/'));
                foreach (string path in new[] { ScenePath, "Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectSettings.asset" }) paths.Add(path);
                foreach (string path in paths.OrderBy(path => path, StringComparer.Ordinal)) report.files.Add(new Fingerprint { path = path, sha256 = Hash(path) });
                ValidateSaved();
                SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
                SessionState.SetBool(Prefix + "Active", true);
                SessionState.SetInt(Prefix + "ExitCode", -1);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                report.failure = exception.ToString(); report.status = "failed"; WriteReport();
                SessionState.SetBool(Prefix + "Active", false);
                if (batch) EditorApplication.Exit(1); else throw;
            }
        }

        static void ValidateSaved()
        {
            table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(Root + "/Tables/LocalizationTable.asset");
            channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(Root + "/Tables/LanguageChannel.asset");
            Require(table && channel && table.FontAsset, "saved assets", "table, channel and font exist", "No assets are generated.");
            Require(channel.Initial == MuseumLanguage.English, "saved assets", "English initial language", channel.Initial.ToString());
            Require(table.Entries.Select(entry => entry.Key).Distinct().Count() == table.Entries.Count, "saved assets", "unique keys", "entries=" + table.Entries.Count);
            for (int index = 0; index < Keys.Length; index++)
                Require(table.ContainsKey(Keys[index]) && table.Get(Keys[index], MuseumLanguage.English) == English[index] &&
                    table.Get(Keys[index], MuseumLanguage.Chinese) == Chinese[index], "saved assets", "exact accepted bilingual text: " + Keys[index],
                    "EN=" + table.Get(Keys[index], MuseumLanguage.English) + "; ZH=" + table.Get(Keys[index], MuseumLanguage.Chinese));
            string glyphs = string.Concat(table.Entries.Select(entry => entry.Key + entry.Chinese + entry.English)) + "[]" + MuseumLanguage.Chinese.NativeName() + MuseumLanguage.English.NativeName();
            var missing = glyphs.Where(character => !char.IsWhiteSpace(character)).Distinct().Where(character => !table.FontAsset.HasCharacter(character, false, false)).ToArray();
            Require(missing.Length == 0, "saved font", "all current table and native-label glyphs already stored", string.Join(", ", missing.Select(c => "U+" + ((int)c).ToString("X4"))));
            Require(table.FontAsset.atlasTextures != null && table.FontAsset.atlasTextures.Length > 0 &&
                table.FontAsset.atlasTextures.All(texture => texture && texture.width > 0 && texture.height > 0),
                "saved font", "saved atlas textures exist", "No tryAddCharacter, dynamic baking or fallback lookup is used by this check.");
            signs = Names.Select(name => Find(name).GetComponent<TMP_Text>()).ToArray();
            for (int index = 0; index < Names.Length; index++)
            {
                var label = signs[index];
                var binding = label ? label.GetComponent<LocalizedText>() : null;
                Require(label && binding && binding.Key == Keys[index], "saved binding", Names[index], binding ? binding.Key : "missing");
                var so = new SerializedObject(binding);
                Require(so.FindProperty("table").objectReferenceValue == table && so.FindProperty("channel").objectReferenceValue == channel &&
                    so.FindProperty("applyTableFont").boolValue, "saved binding", "references and bilingual font enabled: " + Names[index], Keys[index]);
                Require(label.text == English[index] && label.font == table.FontAsset, "saved binding", "saved English and font: " + Names[index], label.text);
                Require(label.gameObject.activeSelf == Active[index] && label.GetComponent<Renderer>().enabled,
                    "saved visibility", "main layout retained: " + Names[index], "active=" + label.gameObject.activeSelf);
            }
            foreach (string name in OldNames)
            {
                var old = Find(name);
                Require(!old.GetComponent<LocalizedText>() && !old.GetComponent<Renderer>().enabled,
                    "saved visibility", "old hidden label stays unbound: " + name, "Legacy Renderer stays disabled.");
            }
            var panel = Find("Language Selection");
            Require(Vector3.Distance(panel.position, new Vector3(2.6f, 0f, 6.2f)) < .0001f &&
                Quaternion.Angle(panel.rotation, Quaternion.identity) < .001f && Vector3.Distance(panel.localScale, Vector3.one) < .0001f,
                "saved layout", "existing language panel placement preserved", panel.position.ToString("F4"));
            var components = panel.GetComponentsInChildren<LanguageButton>(true);
            Require(components.Count(button => button.Action == LanguageButton.Choice.Chinese) == 1 &&
                components.Count(button => button.Action == LanguageButton.Choice.English) == 1,
                "saved binding", "both language buttons are uniquely wired", "buttons=" + components.Length);
            foreach (var button in components)
                Require(new SerializedObject(button).FindProperty("channel").objectReferenceValue == channel,
                    "saved binding", "button channel: " + button.name, button.Action.ToString());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>("Localization integration QA keyboard");
            mouse = Mouse.current; addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>("Localization integration QA mouse");
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                started = EditorApplication.timeSinceStartup; lastFrame = -1; finishing = false;
                Application.logMessageReceived += OnLog; InputSystem.onBeforeUpdate += FlushInput;
                Routines.Clear(); PendingInput.Clear(); Routines.Push(ValidateRuntime());
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && !finishing && report != null) Finish("Play Mode ended early.", false);
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                int code = SessionState.GetInt(Prefix + "ExitCode", 1);
                SessionState.SetBool(Prefix + "Active", false);
                if (SessionState.GetBool(Prefix + "Batch", false)) { pendingExit = code < 0 ? 1 : code; exitAt = EditorApplication.timeSinceStartup + 20; }
            }
        }
        static void Tick()
        {
            if (pendingExit >= 0 && EditorApplication.timeSinceStartup >= exitAt) { int code = pendingExit; pendingExit = -1; EditorApplication.Exit(code); return; }
            if (!SessionState.GetBool(Prefix + "Active", false) || !EditorApplication.isPlaying || report == null || finishing) return;
            if (EditorApplication.timeSinceStartup - started > 150) { Finish("Runtime checks exceeded 150 seconds."); return; }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
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
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        static IEnumerator ValidateRuntime()
        {
            yield return Wait(2f);
            Require(SceneManager.GetActiveScene().path == ScenePath && Camera.main, "runtime structure", "saved main scene and camera", SceneManager.GetActiveScene().path);
            table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(Root + "/Tables/LocalizationTable.asset");
            channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(Root + "/Tables/LanguageChannel.asset");
            signs = Names.Select(name => Find(name).GetComponent<TMP_Text>()).ToArray();
            var languageButtons = Find("Language Selection").GetComponentsInChildren<LanguageButton>(true);
            chineseButton = languageButtons.Single(button => button.Action == LanguageButton.Choice.Chinese);
            englishButton = languageButtons.Single(button => button.Action == LanguageButton.Choice.English);
            exploration = UnityEngine.Object.FindObjectOfType<HelmetExplorationController>();
            demonstration = UnityEngine.Object.FindObjectOfType<HelmetConnectionDemonstration>();
            var progressive = UnityEngine.Object.FindObjectOfType<HelmetExplorationPanel>();
            Require(exploration && demonstration && progressive && progressive.ProgressiveLayoutEnabled,
                "runtime structure", "PR9 progressive exploration remains installed", "Uses saved production components.");
            Require(channel.Current == MuseumLanguage.English && !demonstration.IsVisible, "runtime state", "initial English without a diagram", channel.Current.ToString());
            SetupInput(); yield return Wait(.6f);
            primary = new View { name = "production camera pose", position = Camera.main.transform.position, rotation = Camera.main.transform.rotation, fieldOfView = Camera.main.fieldOfView, aspect = 4f / 3f };
            mastheadView = DiagnosticView(signs[0]); arrivalView = DiagnosticView(signs[2]);
            CheckVisible("initial-en", MuseumLanguage.English, false); CaptureAll("initial-en");
            yield return Press(chineseButton, false);
            CheckVisible("selected-zh", MuseumLanguage.Chinese, false); CaptureAll("selected-zh");

            // Separate lifecycle fixture: verify hidden translations only when a future flow enables them.
            foreach (int index in new[] { 1, 3 })
            {
                Require(!signs[index].gameObject.activeSelf, "runtime lifecycle", "hidden before fixture: " + Names[index], "Test-only activation; never saved.");
                signs[index].gameObject.SetActive(true);
                yield return Wait(.1f);
                CheckText(index, MuseumLanguage.Chinese, "temporary-hidden-label-activation");
                signs[index].gameObject.SetActive(false);
            }
            CheckVisible("hidden-fixtures-restored", MuseumLanguage.Chinese, false);
            exploration.SelectClue(HelmetExplorationController.Clue.Air); yield return Wait(.2f);
            Require(demonstration.IsPlaying && demonstration.IsVisible, "public exploration API", "Air diagram started", "No claim that this step used a physical/controller button.");
            CheckVisible("diagram-zh", MuseumLanguage.Chinese, true); CaptureAll("diagram-zh");
            yield return Press(englishButton, true);
            CheckVisible("switch-en-during-diagram", MuseumLanguage.English, true);
            exploration.Stop(); yield return Wait(.2f);
            CheckVisible("restored-en", MuseumLanguage.English, false); CaptureAll("restored-en");
            exploration.Replay(); yield return Wait(.2f);
            yield return Press(chineseButton, true);
            CheckVisible("switch-zh-during-diagram", MuseumLanguage.Chinese, true);
            exploration.Stop(); yield return Wait(.2f);
            CheckVisible("restored-zh", MuseumLanguage.Chinese, false); CaptureAll("restored-zh");
            yield return Press(englishButton, false);
            foreach (int index in new[] { 1, 3 })
            {
                signs[index].gameObject.SetActive(true); yield return Wait(.1f);
                CheckText(index, MuseumLanguage.English, "hidden-label-enable-after-language-change");
                signs[index].gameObject.SetActive(false);
            }
            CheckVisible("final-en", MuseumLanguage.English, false);
            Require(report.errors.Count == 0, "logs", "no Unity errors", string.Join("\n", report.errors));
        }

        static void CheckVisible(string phase, MuseumLanguage language, bool diagramVisible)
        {
            Require(channel.Current == language, "XR input result", "channel: " + phase, channel.Current.ToString());
            Require(demonstration.IsVisible == diagramVisible, "progressive visibility", "diagram visibility: " + phase, demonstration.IsVisible.ToString());
            for (int index = 0; index < signs.Length; index++)
            {
                bool expected = Active[index] && (index != 0 || !diagramVisible);
                var renderer = signs[index].GetComponent<Renderer>();
                Require(signs[index].gameObject.activeSelf == expected && signs[index].gameObject.activeInHierarchy == expected && renderer.enabled,
                    "progressive visibility", phase + "/" + Names[index], "expected active=" + expected + "; actual=" + signs[index].gameObject.activeInHierarchy + "; renderer=" + renderer.enabled);
                if (expected) CheckText(index, language, phase);
                SampleText(phase, signs[index]);
            }
            Require(Find("Central question").gameObject.activeSelf == !diagramVisible, "progressive visibility", "original central question follows diagram: " + phase, "PR9 policy retained.");
            foreach (string name in OldNames)
                Require(!Find(name).GetComponent<Renderer>().enabled, "legacy visibility", phase + "/" + name, "Original hidden renderers stay disabled.");
        }
        static void CheckText(int index, MuseumLanguage language, string phase)
        {
            var label = signs[index]; label.ForceMeshUpdate(true, true);
            string expected = language == MuseumLanguage.English ? English[index] : Chinese[index];
            Require(label.text == expected && label.font == table.FontAsset, "rendered text", phase + "/" + Names[index], "expected=" + expected + "; actual=" + label.text + "; font=" + label.font.name);
            Require(!label.isTextOverflowing, "text layout", "no text overflow: " + phase + "/" + Names[index], "Containment only; screenshots need visual review.");
            foreach (char character in expected.Where(c => !char.IsWhiteSpace(c)).Distinct())
                Require(label.font.HasCharacter(character, false, false), "rendered text", "stored glyph " + Names[index] + "/U+" + ((int)character).ToString("X4"), label.font.name);
        }
        static void SampleText(string phase, TMP_Text label)
        { report.samples.Add(new Sample { phase = phase, name = label.name, text = label.text, font = label.font ? label.font.name : "none", activeSelf = label.gameObject.activeSelf, activeInHierarchy = label.gameObject.activeInHierarchy, rendererEnabled = label.GetComponent<Renderer>().enabled, overflow = label.isTextOverflowing }); }
        static Transform Find(string name)
        {
            var matches = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Where(item => item.name == name).ToArray();
            Require(matches.Length == 1, "structure", "unique object: " + name, "count=" + matches.Length);
            return matches[0];
        }

        static void SetupInput()
        {
            var simulators = UnityEngine.Object.FindObjectsOfType<XRDeviceSimulator>();
            Require(simulators.Length > 0, "input fixture", "official simulator exists", "Only its automated state writer is disabled during this run.");
            foreach (var simulator in simulators) simulator.enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior; oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; changedSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard); if (addedMouse) InputSystem.EnableDevice(mouse);
            rightController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(item => item.name == "Right Controller");
            leftController = UnityEngine.Object.FindObjectsOfType<ActionBasedController>(true).Single(item => item.name == "Left Controller");
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
        static IEnumerator Press(LanguageButton languageButton, bool left)
        {
            var button = languageButton.GetComponent<XRSimpleInteractable>();
            Require(button && button.isActiveAndEnabled, "XR input", "saved language button is selectable: " + languageButton.name, languageButton.Action.ToString());
            var collider = button.colliders.FirstOrDefault(item => item && item.enabled) ?? button.GetComponent<Collider>();
            Require(collider && collider.enabled, "XR input", "language button collider: " + languageButton.name, "Production collider.");
            int events = 0; string interactor = "none";
            UnityEngine.Events.UnityAction<SelectEnterEventArgs> listener = args => { events++; interactor = args.interactorObject.GetType().Name; };
            button.selectEntered.AddListener(listener);
            try
            {
                var device = left ? leftDevice : rightDevice;
                Vector3 position = collider.bounds.center - button.transform.forward * .55f;
                Quaternion rotation = Quaternion.LookRotation(collider.bounds.center - position, Vector3.up);
                QueueController(device, position, rotation, false); yield return Wait(.25f);
                Require(button.isHovered, "XR input", "ray hovers language button: " + languageButton.name, left ? "left" : "right");
                QueueController(device, position, rotation, true); yield return Wait(.25f);
                Require(events == 1 && interactor == nameof(XRRayInteractor), "XR input", "grip selects language button exactly once: " + languageButton.name, "events=" + events + "; interactor=" + interactor);
                QueueController(device, position, rotation, false); yield return Wait(.2f);
                ParkHands(); yield return Wait(.15f);
            }
            finally { if (button) button.selectEntered.RemoveListener(listener); }
        }
        static void QueueController(XRSimulatedController device, Vector3 world, Quaternion rotation, bool grip)
        {
            var parent = (device == leftDevice ? leftController : rightController).transform.parent;
            var state = new XRSimulatedControllerState { isTracked = true, trackingState = 3, batteryLevel = 1,
                devicePosition = parent.InverseTransformPoint(world), deviceRotation = Quaternion.Inverse(parent.rotation) * rotation,
                grip = grip ? 1f : 0f }.WithButton(ControllerButton.GripButton, grip);
            PendingInput.Enqueue(() => InputSystem.QueueStateEvent(device, state));
        }
        static void ParkHands()
        { QueueController(rightDevice, new Vector3(.7f, .8f, 1f), Quaternion.identity, false); QueueController(leftDevice, new Vector3(-.7f, .8f, 1f), Quaternion.identity, false); }
        static void FlushInput() { if (InputState.currentUpdateType == InputUpdateType.Dynamic) while (PendingInput.Count > 0) PendingInput.Dequeue()(); }
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        static View DiagnosticView(TMP_Text text)
        {
            text.ForceMeshUpdate(true, true);
            var bounds = text.GetComponent<Renderer>().bounds;
            float distance = Mathf.Max(.5f, (bounds.extents.magnitude + .05f) / Mathf.Tan(24f * Mathf.Deg2Rad));
            Vector3 position = bounds.center - text.transform.forward * distance;
            return new View { name = "fixed diagnostic close-up: " + text.name, position = position,
                rotation = Quaternion.LookRotation(bounds.center - position, text.transform.up), fieldOfView = 48f, aspect = 4f / 3f };
        }
        static void CaptureAll(string phase)
        {
            Require(Vector3.Distance(Camera.main.transform.position, primary.position) < .0001f && Quaternion.Angle(Camera.main.transform.rotation, primary.rotation) < .01f,
                "QA camera", "unchanged production viewpoint: " + phase, "Input devices retain one camera pose throughout.");
            CapturePng(phase + "-production.png", primary);
            CapturePng(phase + "-masthead-closeup.png", mastheadView);
            CapturePng(phase + "-arrival-closeup.png", arrivalView);
        }
        static void CapturePng(string filename, View view)
        {
            var temporary = new GameObject("Temporary Localization QA Camera") { hideFlags = HideFlags.HideAndDontSave };
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1440, 1080, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                var camera = temporary.AddComponent<Camera>(); camera.CopyFrom(Camera.main);
                camera.enabled = false; camera.stereoTargetEye = StereoTargetEyeMask.None; camera.targetTexture = null;
                var additional = temporary.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                additional.allowXRRendering = false; additional.renderPostProcessing = false;
                camera.transform.SetPositionAndRotation(view.position, view.rotation); camera.fieldOfView = view.fieldOfView; camera.aspect = view.aspect;
                foreach (var label in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) label.ForceMeshUpdate(true);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, filename), pixels.EncodeToPNG());
                report.captures.Add(new Capture { file = filename, view = view });
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(temporary); }
        }

        static void Require(bool condition, string kind, string name, string detail)
        { report.checks.Add(new Check { kind = kind, name = name, detail = detail, passed = condition }); if (!condition) throw new InvalidOperationException(name + ": " + detail); }
        static void OnLog(string message, string stack, LogType type)
        {
            if (report == null) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) report.errors.Add(type + ": " + message + "\n" + stack);
            else if (type == LogType.Warning) report.warnings.Add(message);
        }
        static void Finish(string failure, bool exitPlayMode = true)
        {
            if (finishing) return;
            finishing = true; Application.logMessageReceived -= OnLog; InputSystem.onBeforeUpdate -= FlushInput;
            PendingInput.Clear(); Routines.Clear(); report.failure = failure ?? "";
            foreach (var file in report.files)
                report.checks.Add(new Check { kind = "source integrity", name = "validator left saved file unchanged: " + file.path, passed = Hash(file.path) == file.sha256, detail = Hash(file.path) });
            report.status = failure == null && report.errors.Count == 0 && report.checks.All(check => check.passed) ? "passed" : "failed";
            foreach (var device in new InputDevice[] { rightDevice, leftDevice, hmd, keyboard, addedMouse ? mouse : null })
                if (device != null && device.added) InputSystem.RemoveDevice(device);
            if (changedSettings) { InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput; }
            WriteReport(); SessionState.SetInt(Prefix + "ExitCode", report.status == "passed" ? 0 : 1);
            Debug.Log("LOCALIZATION_INTEGRATION_QA_" + report.status.ToUpperInvariant() + " " + Path.Combine(Output, "report.json") + (failure == null ? "" : "\n" + failure));
            if (exitPlayMode) EditorApplication.ExitPlaymode();
        }
        static void WriteReport()
        { report.finishedUtc = DateTime.UtcNow.ToString("o"); Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false)); }
        static string Hash(string path)
        { if (!File.Exists(path)) return "MISSING"; using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static string Git(string arguments)
        {
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", arguments) { WorkingDirectory = RepoRoot, UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
                start.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(5000)) { process.Kill(); return "UNAVAILABLE: git timeout"; }
                    if (!stdout.Wait(1000) || !stderr.Wait(1000)) return "UNAVAILABLE: incomplete output";
                    return process.ExitCode == 0 ? stdout.Result.Trim() : "UNAVAILABLE: " + stderr.Result.Trim();
                }
            }
            catch (Exception exception) { return "UNAVAILABLE: " + exception.Message; }
        }
    }
}
#endif
