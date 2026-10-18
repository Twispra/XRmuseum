#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.PhotoHelmetTrial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Museum.PhotoHelmetTrial.EditorTools
{
    /// <summary>Reads the saved assets; never runs the comparison scene generator.</summary>
    [InitializeOnLoad]
    public static class PhotoHelmetComparisonValidation
    {
        const string StateKey = "Museum.PhotoHelmetQA.State";
        const string OutputKey = "Museum.PhotoHelmetQA.Output";
        const string ResultKey = "Museum.PhotoHelmetQA.Result";
        const string ScenePath = "Assets/Museum/Features/PhotoHelmetTrial/Scenes/PhotoHelmetComparison.unity";
        const string Root = "Assets/Museum/Features/PhotoHelmetTrial/";
        static Report report;
        static IEnumerator routine;
        static Keyboard keyboard;
        static KeyboardState? pendingKeys;
        static int lastFrame = -1;
        static double deadline;
        static double exitAt;
        static InputSettings.BackgroundBehavior oldBackground;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;

        [Serializable] class Check { public string name, detail; public bool passed; }
        [Serializable] class Report
        {
            public string unityVersion, sourceCommit, cacheProvenance, scene = ScenePath;
            public string input = "Synthetic KeyboardState through Input System -> saved PhotoHelmetComparisonControls.Update";
            public string limitation = "Desktop comparison only; no headset, build, main-scene journey or historical validation.";
            public List<Check> checks = new List<Check>();
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        static PhotoHelmetComparisonValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
        }

        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start this runner in Edit Mode.");
            var output = Environment.GetEnvironmentVariable("MUSEUM_QA_OUTPUT");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set a new MUSEUM_QA_OUTPUT directory.");
            output = Path.GetFullPath(output);
            if (File.Exists(Path.Combine(output, "report.json")))
                throw new InvalidOperationException("Refusing to overwrite an existing report.");
            Directory.CreateDirectory(output);
            SessionState.SetString(OutputKey, output);
            SessionState.SetString(StateKey, "starting");
            report = new Report { unityVersion = Application.unityVersion,
                sourceCommit = Environment.GetEnvironmentVariable("MUSEUM_QA_COMMIT") ?? "see process manifest",
                cacheProvenance = Environment.GetEnvironmentVariable("MUSEUM_QA_CACHE") ?? "project-specific Library; see process manifest" };
            Application.logMessageReceived += OnLog;
            try
            {
                ValidateImport("details", 107248, 10);
                ValidateImport("shell", 10056, 1);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PhotoHelmetStudy.prefab");
                CheckThat(prefab, "saved prefab exists", "No regeneration was performed.");
                CheckMeshes(prefab, "saved photo prefab", 117304);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
                    CheckThat(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0,
                        "no missing script: " + go.name, "saved comparison scene");
                WriteReport();
                SessionState.SetString(StateKey, "play");
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Fail(error); }
        }

        static void ValidateImport(string part, int expectedTriangles, int materialSlots)
        {
            var path = Root + "Models/photo-helmet-refined-v2-" + part + ".obj";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CheckThat(asset, part + " imported", path);
            var renderers = asset.GetComponentsInChildren<MeshRenderer>(true);
            CheckThat(renderers.Length == 1 && renderers[0].sharedMaterials.Length == materialSlots &&
                renderers[0].sharedMaterials.All(m => m), part + " material slots intact", materialSlots.ToString());
            CheckMeshes(asset, part + " imported OBJ", expectedTriangles);
        }

        static void CheckMeshes(GameObject asset, string label, int expectedTriangles)
        {
            var filters = asset.GetComponentsInChildren<MeshFilter>(true);
            CheckThat(filters.Length > 0 && filters.All(f => f.sharedMesh), label + " meshes exist", filters.Length.ToString());
            int triangles = filters.Sum(f => Enumerable.Range(0, f.sharedMesh.subMeshCount)
                .Sum(i => checked((int)f.sharedMesh.GetIndexCount(i)) / 3));
            CheckThat(triangles == expectedTriangles, label + " full triangle count", triangles + " / " + expectedTriangles);
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                // UV count must match the imported vertex stream even for the solid-color pieces.
                if (!Application.isPlaying)
                    CheckThat(mesh.uv.Length == mesh.vertexCount, label + " complete UV stream", mesh.vertexCount.ToString());
                CheckThat(mesh.bounds.size.sqrMagnitude > .001f, label + " nonempty bounds", mesh.bounds.ToString());
            }
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(StateKey, "") == "play")
            {
                report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
                Application.logMessageReceived += OnLog;
                oldBackground = InputSystem.settings.backgroundBehavior;
                oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.onBeforeUpdate += BeforeInput;
                routine = Exercise();
                deadline = EditorApplication.timeSinceStartup + 180;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(StateKey, "") == "exit")
            {
                report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
                Application.logMessageReceived += OnLog;
                exitAt = EditorApplication.timeSinceStartup + 20;
            }
        }

        static void BeforeInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || !pendingKeys.HasValue || keyboard == null) return;
            InputSystem.QueueStateEvent(keyboard, pendingKeys.Value);
            pendingKeys = null;
        }

        static void Tick()
        {
            if (SessionState.GetString(StateKey, "") == "exit" && !EditorApplication.isPlayingOrWillChangePlaymode && exitAt > 0)
            {
                if (EditorApplication.timeSinceStartup < exitAt || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                WriteReport();
                SessionState.SetString(StateKey, "");
                EditorApplication.Exit(SessionState.GetInt(ResultKey, 1) == 0 && report.errors.Count == 0 ? 0 : 1);
                return;
            }
            if (routine == null || !EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Photo comparison Play validation timed out.");
                if (!routine.MoveNext()) Finish(0);
            }
            catch (Exception error) { Fail(error); }
        }

        static IEnumerator Exercise()
        {
            for (int i = 0; i < 12; i++) yield return null;
            var controls = UnityEngine.Object.FindObjectOfType<PhotoHelmetComparisonControls>();
            CheckThat(controls, "saved comparison controls active", "Production component");
            var so = new SerializedObject(controls);
            var current = so.FindProperty("currentModel").objectReferenceValue as Transform;
            var photo = so.FindProperty("photoModel").objectReferenceValue as Transform;
            var section = so.FindProperty("photoSection").objectReferenceValue as PhotoHelmetSectionPreview;
            CheckThat(current && photo && section, "saved controls bound", "Both models and the photo section component");
            CheckMeshes(photo.gameObject, "Play photo model", 117304);
            var currentStart = current.rotation;
            var photoStart = photo.rotation;
            Capture("01-front");

            pendingKeys = new KeyboardState(Key.D);
            for (int i = 0; i < 30; i++) yield return null;
            pendingKeys = new KeyboardState();
            for (int i = 0; i < 3; i++) yield return null;
            CheckThat(Quaternion.Angle(photoStart, photo.rotation) > 1f, "D rotates model", Quaternion.Angle(photoStart, photo.rotation).ToString("F2"));
            CheckThat(Quaternion.Angle(current.rotation * Quaternion.Inverse(currentStart), photo.rotation * Quaternion.Inverse(photoStart)) < .1f,
                "both models rotate together", "world-space rotation deltas");
            Capture("02-rotated");
            pendingKeys = new KeyboardState(Key.R);
            for (int i = 0; i < 3; i++) yield return null;
            pendingKeys = new KeyboardState();
            for (int i = 0; i < 3; i++) yield return null;
            CheckThat(Quaternion.Angle(photoStart, photo.rotation) < .1f && Quaternion.Angle(currentStart, current.rotation) < .1f,
                "R restores both orientations", "keyboard path");

            pendingKeys = new KeyboardState(Key.S);
            for (int i = 0; i < 3; i++) yield return null;
            pendingKeys = new KeyboardState();
            for (int i = 0; i < 3; i++) yield return null;
            CheckThat(section.IsCutaway && section.CapTriangles > 0, "S creates shell cap", section.CapTriangles + " triangles");
            Capture("03-section");
            pendingKeys = new KeyboardState(Key.E);
            for (int i = 0; i < 3; i++) yield return null;
            pendingKeys = new KeyboardState();
            for (int i = 0; i < 3; i++) yield return null;
            CheckThat(Mathf.Abs(section.Depth - .1f) < .001f && section.CapTriangles > 0,
                "E adjusts depth and rebuilds cap", section.Depth + "; " + section.CapTriangles + " triangles");
            Capture("04-depth");

            pendingKeys = new KeyboardState(Key.D);
            for (int i = 0; i < 30; i++) yield return null;
            pendingKeys = new KeyboardState();
            for (int i = 0; i < 3; i++) yield return null;
            var cap = photo.Find("Photo shell section face - interpretive");
            CheckThat(cap && cap.gameObject.activeSelf, "cap remains visible after rotation", "generated shell section");
            var block = new MaterialPropertyBlock();
            var surface = photo.GetComponentsInChildren<MeshRenderer>().First(r => r.transform != cap);
            surface.GetPropertyBlock(block);
            var plane = block.GetVector("_CutPlane");
            var normal = new Vector3(plane.x, plane.y, plane.z);
            CheckThat(Vector3.Dot(normal, photo.right) > .999f, "cut plane follows rotated model", normal.ToString());
            var maxError = cap.GetComponent<MeshFilter>().sharedMesh.vertices.Max(v => Mathf.Abs(Vector3.Dot(normal, cap.TransformPoint(v)) + plane.w));
            CheckThat(maxError < .0001f, "cap stays on world cut plane", maxError.ToString("G4") + " m");
            Capture("05-rotated-section");

            pendingKeys = new KeyboardState(Key.R);
            for (int i = 0; i < 3; i++) yield return null;
            pendingKeys = new KeyboardState();
            for (int i = 0; i < 3; i++) yield return null;
            CheckThat(!section.IsCutaway && Mathf.Abs(section.Depth) < .001f && !cap.gameObject.activeSelf &&
                Quaternion.Angle(photoStart, photo.rotation) < .1f &&
                photo.GetComponentsInChildren<MeshRenderer>().All(r => !r.sharedMaterials.Any(m => m.name.StartsWith("Cut_"))),
                "R restores full display and disables cap", "orientation, depth and display materials");
            Capture("06-reset");
        }

        static void Capture(string name)
        {
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            if (!camera) throw new InvalidOperationException("Saved comparison camera missing.");
            var target = RenderTexture.GetTemporary(1280, 900, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels = new Texture2D(1280, 900, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(SessionState.GetString(OutputKey, ""), name + ".jpg"), pixels.EncodeToJPG(92));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        static void CheckThat(bool passed, string name, string detail)
        {
            report.checks.Add(new Check { name = name, detail = detail, passed = passed });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
        static string ReportPath => Path.Combine(SessionState.GetString(OutputKey, ""), "report.json");
        static void WriteReport() => File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true) + "\n");
        static void OnLog(string message, string stack, LogType type)
        {
            if (report == null) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message);
            else if (type == LogType.Warning) report.warnings.Add(message);
            else return;
            // Preserve shutdown errors across the domain reload when leaving Play Mode.
            if (File.Exists(ReportPath)) WriteReport();
        }
        static void Fail(Exception error)
        {
            report.errors.Add(error.ToString());
            Finish(1);
        }
        static void Finish(int result)
        {
            routine = null;
            if (keyboard != null)
            {
                InputSystem.onBeforeUpdate -= BeforeInput;
                InputSystem.RemoveDevice(keyboard);
                keyboard = null;
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            }
            WriteReport();
            SessionState.SetInt(ResultKey, result);
            SessionState.SetString(StateKey, "exit");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else exitAt = EditorApplication.timeSinceStartup + 20;
        }
    }
}
#endif
