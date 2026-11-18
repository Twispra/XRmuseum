using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.Diegetic;
using Museum.Diegetic.Editor;
using Museum.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Museum.DuskWorkshop.Editor
{
    // Temporary review fixture. All positions are fixed Unity world coordinates in metres.
    [InitializeOnLoad]
    public static class DuskWorkshopCapture
    {
        const string SessionKey = "Museum.DuskWorkshop.Capture";
        static double resumeAt, exitAt, deadline;
        static int step;
        static CaptureRecord record;
        static string Folder => SessionState.GetString(SessionKey + ".output", "");
        static int ExitCode => SessionState.GetInt(SessionKey + ".exit", 1);

        [Serializable]
        sealed class View
        {
            public string id;
            public Vector3 eye, lookAt;
            public float verticalFov, aspect;
            public View(string name, Vector3 origin, Vector3 target, float fov, float ratio)
            {
                id = name;
                eye = origin;
                lookAt = target;
                verticalFov = fov;
                aspect = ratio;
            }
        }

        [Serializable]
        sealed class CaptureRecord
        {
            public string editor, scene, graphicsDevice, graphicsApi, renderPipeline, language, method, result;
            public View[] views;
        }

        // The overview, seated and entrance prescriptions match the November route evidence.
        // Hero and detail views are fixed identically for the baseline and the candidate.
        static readonly View[] Views = {
            new View("01-workshop", new Vector3(2.7f, 1.68f, -3.60f), new Vector3(-.9f, 1.50f, 3.4f), 62f, 1.8f),
            new View("02-overview", new Vector3(0, 2.85f, -.55f), new Vector3(0, 1.15f, 3.6f), 68f, 1.65f),
            new View("03-center-seated", new Vector3(.01f, 1.25f, 2.25f), new Vector3(0, 1.24f, 3.55f), 55f, 1.6f),
            new View("04-spawn", new Vector3(0, 1.36f, 2.2f), new Vector3(0, 1.36f, 3.2f), 55f, 1.6f),
            new View("05-window", new Vector3(-2.2f, 1.56f, -.85f), new Vector3(-5.82f, 2.25f, 2.6f), 58f, 1.65f),
            new View("06-bench-corner", new Vector3(1.32f, .80f, 2.06f), new Vector3(.48f, .58f, 2.95f), 47f, 1.60f),
            new View("07-helmet", new Vector3(.35f, 1.37f, 2.79f), new Vector3(.01f, 1.25f, 3.50f), 43f, 1.6f),
            new View("08-right", new Vector3(3, 1.36f, 2.33f), new Vector3(3.1f, 1.38f, 3.53f), 55f, 1.6f),
            new View("09-left", new Vector3(-3, 1.36f, 2.33f), new Vector3(-3.1f, 1.38f, 3.53f), 55f, 1.6f)
        };

        static DuskWorkshopCapture()
        {
            EditorApplication.playModeStateChanged += OnMode;
            EditorApplication.update += OnUpdate;
        }

        // Use -batchmode -force-d3d11, with GPU, and omit -quit (the fixture exits on completion).
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate batch Editor.");
            string scene = Arg("-duskScene");
            string output = Path.GetFullPath(Arg("-duskOutput"));
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new capture directory: " + output);
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            SessionState.SetString(SessionKey + ".output", output);
            SessionState.SetInt(SessionKey + ".exit", 1);
            SessionState.SetBool(SessionKey, true);
            EditorApplication.EnterPlaymode();
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("Missing " + name);
            return args[i + 1];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SetUpEditorInput()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
        }

        static void OnMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                record = new CaptureRecord {
                    editor = Application.unityVersion,
                    scene = SceneManager.GetActiveScene().path,
                    graphicsDevice = SystemInfo.graphicsDeviceName,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                    renderPipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "Built-in",
                    language = "zh-Hans",
                    method = "Play Mode, offscreen mono URP camera, 1600 pixels wide. FOV in vertical degrees. World-space poses in metres. Diagnostic views, without a headset.",
                    views = Views
                };
                step = -1;
                resumeAt = EditorApplication.timeSinceStartup + .8;
                deadline = EditorApplication.timeSinceStartup + 120;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(SessionKey, false);
                exitAt = EditorApplication.timeSinceStartup + 1.5;
            }
        }

        static void OnUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (exitAt > 0 && now >= exitAt)
            {
                exitAt = 0;
                EditorApplication.Exit(ExitCode);
                return;
            }
            if (record == null || !EditorApplication.isPlaying || now < resumeAt) return;
            try
            {
                if (now > deadline) throw new TimeoutException("Timed out waiting for the exhibit and URP.");
                if (step < 0)
                {
                    var station = UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
                    if (!station || !station.IsReady || !Camera.main) return;
                    ShaderUtil.allowAsyncCompilation = false;
                    AudioListener.pause = true;
                    MuseumText.Channel.SetLanguage(MuseumLanguage.Chinese);
                    Take(Views[0], ".shader-warmup");
                    File.Delete(Path.Combine(Folder, ".shader-warmup.png"));
                    step = 0;
                    resumeAt = now + .75;
                    return;
                }
                if (step < Views.Length)
                {
                    Take(Views[step], Views[step].id);
                    step++;
                    resumeAt = now + .08;
                    return;
                }
                Finish(null);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish(ex);
            }
        }

        static void Take(View view, string name)
        {
            var q = Quaternion.LookRotation(view.lookAt - view.eye, Vector3.up);
            HelmetDiegeticPreview.CaptureView(Path.Combine(Folder, name + ".png"), view.eye, q, view.verticalFov, view.aspect);
        }

        static void Finish(Exception failure)
        {
            record.result = failure == null ? "complete" : failure.ToString();
            File.WriteAllText(Path.Combine(Folder, "capture.json"), JsonUtility.ToJson(record, true));
            record = null;
            SessionState.SetInt(SessionKey + ".exit", failure == null ? 0 : 1);
            EditorApplication.ExitPlaymode();
        }
    }
}
