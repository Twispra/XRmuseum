#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Museum.Eras;
using Museum.Inspection;
using Museum.Refined;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

namespace Museum.XRTracking.EditorTools
{
    /// <summary>Saved display-height and public reset regression; never writes scene or prefab assets.</summary>
    [InitializeOnLoad]
    public static class HelmetDisplayHeightValidation
    {
        const string Prefix = "Museum.DisplayHeightQA.", ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const float HomeY = .9719999f;
        static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
        static Report report;
        static Keyboard addedKeyboard;
        static Mouse addedMouse;
        static bool finishing;
        static int lastFrame = -1, pendingExit = -1;
        static double started, exitAt;
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Output => SessionState.GetString(Prefix + "Output", "");
        [Serializable] sealed class Check { public string kind, name, detail; public bool passed; }
        [Serializable] sealed class Source { public string path, sha256; }
        [Serializable] sealed class Capture { public string file, era, method = "synthetic standing-eye fixture copied from production camera; monocular"; public Vector3 position, shellMin, shellMax; public Quaternion rotation; public float fieldOfView, aspect; }
        [Serializable] sealed class Report
        {
            public string status = "running", failure = "", utc, commit, branch, dirty, unity, environment;
            public string limits = "Public ResetToDisplay/RequestResetToDisplay/ShowEra functional checks with temporary rigidbody pose fixtures. No physical grab input, headset comfort, Virtual Desktop, floor calibration, executable build or participant study. Standing captures use a temporary camera at 1.6m with production horizontal pose/orientation/FOV; persistent XR camera is not moved. Visibility checks are geometric, not a human readability judgment.";
            public Vector3 savedOriginPosition, savedOriginScale, savedHelmetScale;
            public Quaternion savedOriginRotation;
            public float durationSeconds;
            public List<Check> checks = new List<Check>();
            public List<Source> sources = new List<Source>();
            public List<Capture> captures = new List<Capture>();
            public List<string> errors = new List<string>(), warnings = new List<string>();
        }
        static HelmetDisplayHeightValidation() { EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick; }
        // Omit -quit: the validator writes evidence and exits after Play Mode completes.
        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run display height validation from Edit Mode.");
            started = EditorApplication.timeSinceStartup; finishing = false;
            report = new Report { utc = DateTime.UtcNow.ToString("o"), commit = Git("rev-parse HEAD"), branch = Git("branch --show-current"), dirty = Git("status --porcelain=v1 --untracked-files=all"), unity = Application.unityVersion,
                environment = SystemInfo.operatingSystem + "; " + SystemInfo.processorType + "; " + SystemInfo.graphicsDeviceName };
            string output = Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa/runs", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-display-height-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output); SessionState.SetString(Prefix + "Output", output); SessionState.SetBool(Prefix + "Active", true); SessionState.SetInt(Prefix + "Exit", -1);
            try
            {
                EditorSceneManager.OpenScene(ScenePath);
                var paths = AssetDatabase.GetDependencies(ScenePath, true).Where(p => p.EndsWith(".prefab")).Concat(new[] { ScenePath, "Assets/Museum/Scripts/HelmetExhibitReset.cs", "Assets/Museum/Features/HelmetEras/Runtime/HelmetEraSwitcher.cs", "Assets/Museum/Features/RefinedModels/Runtime/RefinedHelmetBinding.cs", "Assets/Museum/Features/XRTracking/Editor/HelmetDisplayHeightValidation.cs" });
                foreach (string path in paths.SelectMany(p => new[] { p, p + ".meta" }).Distinct().OrderBy(p => p)) report.sources.Add(new Source { path = path, sha256 = Hash(path) });
                SavedChecks(); SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report)); EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Finish(ex.ToString(), false); SessionState.SetBool(Prefix + "Active", false); pendingExit = 1; exitAt = EditorApplication.timeSinceStartup + 1; }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareDesktopInput()
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (Keyboard.current == null) addedKeyboard = InputSystem.AddDevice<Keyboard>("Display height QA keyboard");
            if (Mouse.current == null) addedMouse = InputSystem.AddDevice<Mouse>("Display height QA mouse");
        }
        static void SavedChecks()
        {
            Transform central = GameObject.Find("Exhibit 02 - Placeholder").transform;
            var plinth = central.GetComponentsInChildren<Renderer>().Single(r => r.name == "Plinth");
            var top = central.GetComponentsInChildren<Renderer>().Single(r => r.name == "Display Top");
            Require(Near(central.localPosition.y, -.2f) && Near(plinth.bounds.min.y, 0) && Near(plinth.bounds.max.y, .9f) && Near(top.bounds.max.y, .96f), "saved structure", "central display base stays at floor and top is 0.96m", "plinth=" + plinth.bounds + "; top=" + top.bounds);
            var sides = UnityEngine.Object.FindObjectsOfType<Renderer>().Where(r => r.name == "Display Top" && !r.transform.IsChildOf(central)).ToArray();
            Require(sides.Length == 2 && sides.All(r => Near(r.bounds.max.y, 1.16f)), "saved structure", "two side display tops remain at 1.16m");
            Require(Near(GameObject.Find("Helmet Interaction Station").transform.localPosition.y, -.2f) && Near(GameObject.Find("Helmet Era Controls").transform.localPosition.y, -.2f), "saved structure", "helmet station and era controls are lowered together by 0.20m");
            var reset = UnityEngine.Object.FindObjectOfType<HelmetExhibitReset>();
            Require(reset && new SerializedObject(reset).FindProperty("hasConfiguredHome").boolValue && Near(reset.HomePose.position.y, HomeY) && Vector3.Distance(reset.HomePose.position, reset.transform.position) < .0001f, "saved reset", "explicit world home matches lowered saved helmet", reset ? "home=" + reset.HomePose.position + "; root=" + reset.transform.position : "missing reset");
            var rig = UnityEngine.Object.FindObjectOfType<XROrigin>(); Transform origin = rig.Origin.transform;
            report.savedOriginPosition = origin.position; report.savedOriginRotation = origin.rotation; report.savedOriginScale = origin.lossyScale; report.savedHelmetScale = reset.transform.lossyScale;
            Require(Near(origin.position.y, 0) && Vector3.Distance(origin.lossyScale, Vector3.one) < .0001f && rig.RequestedTrackingOriginMode == XROrigin.TrackingOriginMode.Floor, "saved XR", "rig stays at y=0, scale=1 and requests Floor tracking");
            var floor = GameObject.Find("Floor - Teleport Area");
            Require(floor && Near(floor.GetComponent<Renderer>().bounds.max.y, 0) && Near(floor.transform.position.y, -.1f), "saved floor", "floor top remains at world y=0");
        }
        static void Mode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}")); started = EditorApplication.timeSinceStartup; finishing = false; lastFrame = -1;
                Application.logMessageReceived += Log; Routines.Clear(); Routines.Push(Validate());
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && !finishing) Finish("Play Mode exited before completion.", false);
            else if (state == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Prefix + "Active", false); pendingExit = SessionState.GetInt(Prefix + "Exit", 1); if (pendingExit < 0) pendingExit = 1; exitAt = EditorApplication.timeSinceStartup + 20; }
        }
        static void Tick()
        {
            if (pendingExit >= 0 && EditorApplication.timeSinceStartup >= exitAt) { int code = pendingExit; pendingExit = -1; EditorApplication.Exit(code); return; }
            if (!EditorApplication.isPlaying || finishing || Routines.Count == 0 || lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 120) throw new TimeoutException("Display height QA exceeded 120 seconds.");
                while (Routines.Count > 0) { var current = Routines.Peek(); if (!current.MoveNext()) { Routines.Pop(); continue; } if (current.Current is IEnumerator nested) { Routines.Push(nested); continue; } return; } Finish(null);
            }
            catch (Exception ex) { Finish(ex.ToString()); }
        }
        static IEnumerator Validate()
        {
            yield return Wait(1f);
            var reset = UnityEngine.Object.FindObjectOfType<HelmetExhibitReset>(); var body = reset.GetComponent<Rigidbody>(); var grab = reset.GetComponent<XRGrabInteractable>();
            var inspection = reset.GetComponent<HelmetInspectionController>(); var eras = reset.GetComponent<HelmetEraSwitcher>(); var binding = reset.GetComponent<RefinedHelmetBinding>();
            Require(body && grab && inspection && eras && binding && eras.AvailableEraCount == 3, "runtime structure", "same exhibit retains physics, grab and all three era visuals");
            Require(Near(reset.HomePose.position.y, HomeY) && AtHome(reset), "runtime physics", "initial helmet settles within 12mm of the lowered configured home", "root=" + reset.transform.position + "; home=" + reset.HomePose.position);
            Displace(body, reset.HomePose); reset.ResetToDisplay();
            Require(Vector3.Distance(reset.transform.position, reset.HomePose.position) < .0001f, "public reset API", "ResetToDisplay returns immediately to the new home");
            yield return Wait(.5f); Require(AtHome(reset), "runtime physics", "immediate reset remains supported on the lowered tabletop");
            bool reduced = inspection.ReduceMotion; inspection.ReduceMotion = false; Displace(body, reset.HomePose); Vector3 from = body.position; reset.RequestResetToDisplay();
            Require(reset.IsReturning && Vector3.Distance(body.position, from) < .001f, "public reset API", "RequestResetToDisplay starts a smooth return from the pose fixture");
            yield return Wait(.1f); Require(reset.IsReturning && Vector3.Distance(body.position, from) > .01f && Vector3.Distance(body.position, reset.HomePose.position) > .01f, "public reset API", "smooth return has an intermediate pose");
            yield return Wait(.5f); inspection.ReduceMotion = reduced; Require(!reset.IsReturning && AtHome(reset), "public reset API + physics", "smooth return finishes and remains at the new home");
            int originalEra = eras.CurrentIndex;
            for (int i = 0; i < 3; i++)
            {
                eras.ShowEra(i); yield return Wait(.5f); reset.ResetToDisplay(); yield return Wait(.25f);
                var shell = binding.ActiveShell;
                Require(eras.CurrentIndex == i && !eras.IsSwitching && shell && shell.enabled && shell.gameObject.activeInHierarchy && shell.bounds.size.sqrMagnitude > .01f, "public era API", "era " + i + " displays an active helmet shell", eras.CurrentEra.DisplayName);
                Require(reset.GetComponent<XRGrabInteractable>() == grab && Vector3.Distance(reset.transform.lossyScale, report.savedHelmetScale) < .0001f && AtHome(reset), "runtime model + physics", "era " + i + " preserves grab, model scale and lowered home");
                CaptureStanding(eras.CurrentEra.DisplayName, i, shell);
            }
            eras.ShowEra(originalEra); reset.ResetToDisplay();
            var origin = UnityEngine.Object.FindObjectOfType<XROrigin>().Origin.transform;
            Require(Vector3.Distance(origin.position, report.savedOriginPosition) < .0001f && Quaternion.Angle(origin.rotation, report.savedOriginRotation) < .01f && Vector3.Distance(origin.lossyScale, report.savedOriginScale) < .0001f, "runtime XR", "display changes and reset APIs leave rig origin unchanged");
        }
        static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
        static bool AtHome(HelmetExhibitReset reset) => Vector3.Distance(reset.transform.position, reset.HomePose.position) < .012f && Quaternion.Angle(reset.transform.rotation, reset.HomePose.rotation) < 1.5f;
        static void Displace(Rigidbody body, Pose home) { body.position = home.position + new Vector3(.35f, .25f, -.2f); body.rotation = Quaternion.Euler(15, 35, 4); body.transform.SetPositionAndRotation(body.position, body.rotation); if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; } Physics.SyncTransforms(); }
        static void CaptureStanding(string era, int index, Renderer shell)
        {
            var source = Camera.main; var temporary = new GameObject("Temporary standing eye-height QA view") { hideFlags = HideFlags.HideAndDontSave };
            var target = RenderTexture.GetTemporary(1440, 1080, 24, RenderTextureFormat.ARGB32); var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                var camera = temporary.AddComponent<Camera>(); camera.CopyFrom(source); camera.enabled = false; camera.targetTexture = null; camera.stereoTargetEye = StereoTargetEyeMask.None; camera.aspect = 4f / 3f;
                Vector3 position = source.transform.position; position.y = 1.6f; camera.transform.SetPositionAndRotation(position, source.transform.rotation);
                var data = temporary.AddComponent<UniversalAdditionalCameraData>(); data.allowXRRendering = false; data.renderPostProcessing = false;
                Require(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), shell.bounds), "synthetic standing view", "era " + index + " shell intersects the 1.6m standing camera frustum");
                foreach (var text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) text.ForceMeshUpdate(true);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); RenderTexture.active = target;
                pixels = new Texture2D(1440, 1080, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); pixels.Apply();
                string file = "standing-1.6m-era-" + index + ".jpg"; File.WriteAllBytes(Path.Combine(Output, file), pixels.EncodeToJPG(92));
                report.captures.Add(new Capture { file = file, era = era, position = camera.transform.position, rotation = camera.transform.rotation, fieldOfView = camera.fieldOfView, aspect = camera.aspect, shellMin = shell.bounds.min, shellMax = shell.bounds.max });
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (pixels) UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(temporary); }
        }
        static void Require(bool passed, string kind, string name, string detail = "") { report.checks.Add(new Check { kind = kind, name = name, passed = passed, detail = detail }); if (!passed) throw new InvalidOperationException(name + ": " + detail); }
        static void Log(string message, string stack, LogType type) { if (type == LogType.Warning) report.warnings.Add(message); else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + message + "\n" + stack); }
        static void Finish(string failure, bool exitPlay = true)
        {
            if (finishing) return; finishing = true; Routines.Clear(); Application.logMessageReceived -= Log;
            if (addedKeyboard != null && addedKeyboard.added) InputSystem.RemoveDevice(addedKeyboard); if (addedMouse != null && addedMouse.added) InputSystem.RemoveDevice(addedMouse);
            foreach (var source in report.sources) report.checks.Add(new Check { kind = "source", name = "no incidental scene/meta/prefab/source save: " + source.path, passed = Hash(source.path) == source.sha256 });
            report.failure = failure ?? ""; report.durationSeconds = (float)(EditorApplication.timeSinceStartup - started); report.status = failure == null && report.errors.Count == 0 && report.checks.All(c => c.passed) ? "passed" : "failed";
            File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false)); SessionState.SetInt(Prefix + "Exit", report.status == "passed" ? 0 : 1);
            Debug.Log("DISPLAY_HEIGHT_QA_" + report.status.ToUpperInvariant() + " " + Path.Combine(Output, "report.json")); if (exitPlay) EditorApplication.ExitPlaymode();
        }
        static string Hash(string path) { if (!File.Exists(path)) return "MISSING"; using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static string Git(string arguments)
        {
            try { using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments) { WorkingDirectory = RepoRoot, UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true })) { var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); if (!process.WaitForExit(5000)) { process.Kill(); return "UNAVAILABLE: git timeout"; } return process.ExitCode == 0 ? stdout.Result.Trim() : "UNAVAILABLE: " + stderr.Result.Trim(); } }
            catch (Exception ex) { return "UNAVAILABLE: " + ex.Message; }
        }
    }
}
#endif
