#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Museum.Editor
{
    /// <summary>
    /// Renders the saved scene directly to textures without Play Mode or a Game view.
    /// Batch entry: Museum.Editor.HelmetPrototypePreview.Capture (compatible with -quit).
    /// Requires a graphics device: do not pass -nographics.
    /// </summary>
    public static class HelmetPrototypePreview
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const int Width = 960;
        const int Height = 720;

        [Serializable] class ViewRecord
        {
            public string path, description;
            public Vector3 cameraPosition, cameraEulerAngles;
            public float fieldOfViewDegrees, aspect, minLuminance, maxLuminance, meanLuminance;
            public int width, height;
            public long bytes;
        }

        [Serializable] class CaptureRecord
        {
            public string utc, scene, unityVersion, graphicsDevice, pipeline, status = "running", error = "";
            public string renderMethod = "RenderPipeline.SubmitRenderRequest / UniversalRenderPipeline.SingleCameraRequest -> temporary RenderTexture -> RGB JPEG";
            public string note = "Player-start view uses the saved Main Camera world pose and field of view. Actual headset tracking can change eye height and orientation. Close-up is a separate inspection camera. Scene content, lighting and materials are unchanged; no scene or asset is saved.";
            public List<ViewRecord> views = new List<ViewRecord>();
        }

        public static void Capture()
        {
            var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-preview-" + Guid.NewGuid().ToString("N");
            CaptureToDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/helmet-interaction/qa/runs", runId, "preview")));
        }

        // The acceptance runner supplies its unique run directory. Standalone captures also
        // get a new directory; historical previews are never the implicit output target.
        public static void CaptureToDirectory(string outputDirectory)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Capture must be invoked from Edit Mode.");
            if (!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save or discard the active scene before running this read-only preview export.");

            var directory = Path.GetFullPath(outputDirectory);
            foreach (var fileName in new[] { "capture.json", "01-player-start.jpg", "02-helmet-and-reset.jpg" })
                if (File.Exists(Path.Combine(directory, fileName)))
                    throw new InvalidOperationException("Preview evidence already exists in this directory; choose a new output directory: " + directory);
            Directory.CreateDirectory(directory);
            var report = new CaptureRecord
            {
                utc = DateTime.UtcNow.ToString("o"), scene = ScenePath, unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "none"
            };
            GameObject cameraObject = null;
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("A graphics device is required for actual scene rendering. Omit -nographics.");
                EditorSceneManager.OpenScene(ScenePath);
                var source = Camera.main;
                if (!source) throw new InvalidOperationException("MuseumStart has no tagged Main Camera.");
                var exhibit = GameObject.Find("HelmetExhibit");
                var button = GameObject.Find("Helmet Reset Button");
                if (!exhibit || !button) throw new InvalidOperationException("HelmetExhibit and Helmet Reset Button must exist in the saved scene.");
                if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                    throw new InvalidOperationException("This preview exporter requires the project's Universal Render Pipeline.");

                // TextMeshPro meshes normally refresh on an editor update; force them before
                // this synchronous batch render so freshly loaded labels are included.
                foreach (var label in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
                    label.ForceMeshUpdate(true);

                cameraObject = new GameObject("Temporary Helmet Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
                var camera = cameraObject.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.enabled = false;
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = (float)Width / Height;
                camera.allowDynamicResolution = false;
                camera.targetTexture = null;
                var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                var sourceData = source.GetComponent<UniversalAdditionalCameraData>();
                if (sourceData) EditorUtility.CopySerialized(sourceData, data);
                data.renderType = CameraRenderType.Base;
                data.allowXRRendering = false;
                data.cameraStack?.Clear();

                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                report.views.Add(Render(camera, Path.Combine(directory, "01-player-start.jpg"),
                    "Initial player view: saved Main Camera world position, rotation and field of view."));

                var renderers = exhibit.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("HelmetExhibit has no active renderers.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                bounds.Encapsulate(button.GetComponent<Renderer>().bounds);
                var target = bounds.center + Vector3.down * .05f;
                var position = target + new Vector3(.92f, .52f, -1.42f);
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
                camera.fieldOfView = 43f;
                report.views.Add(Render(camera, Path.Combine(directory, "02-helmet-and-reset.jpg"),
                    "Three-quarter inspection view including helmet, display surface and the RESET button."));
                report.status = "passed";
                Debug.Log("HELMET_PREVIEW_OK " + directory);
            }
            catch (Exception exception)
            {
                report.status = "failed";
                report.error = exception.ToString();
                throw;
            }
            finally
            {
                if (cameraObject) UnityEngine.Object.DestroyImmediate(cameraObject);
                File.WriteAllText(Path.Combine(directory, "capture.json"), JsonUtility.ToJson(report, true));
                // Intentionally no SaveScene, SaveAssets, or changes to project rendering settings.
            }
        }

        static ViewRecord Render(Camera camera, string path, string description)
        {
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            Texture2D pixels = null;
            try
            {
                // URP 14.0.12 supports SingleCameraRequest directly (UniversalRenderPipeline.cs
                // IsRenderRequestSupported and ProcessRenderRequests). No deprecated Camera.Render
                // or empty Game-view screen capture is used.
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply(false, false);
                var colors = pixels.GetPixels32();
                float minimum = 1, maximum = 0, sum = 0;
                int samples = 0;
                for (int i = 0; i < colors.Length; i += 23)
                {
                    var color = colors[i];
                    var luminance = (color.r * .2126f + color.g * .7152f + color.b * .0722f) / 255f;
                    minimum = Mathf.Min(minimum, luminance);
                    maximum = Mathf.Max(maximum, luminance);
                    sum += luminance;
                    samples++;
                }
                if (maximum - minimum < .04f || sum / samples < .005f)
                    throw new InvalidOperationException("Rendered image is blank or effectively black: " + path);
                var bytes = pixels.EncodeToJPG(92);
                File.WriteAllBytes(path, bytes);
                return new ViewRecord
                {
                    path = path, description = description, cameraPosition = camera.transform.position,
                    cameraEulerAngles = camera.transform.eulerAngles, fieldOfViewDegrees = camera.fieldOfView,
                    aspect = camera.aspect, width = Width, height = Height, bytes = bytes.Length,
                    minLuminance = minimum, maxLuminance = maximum, meanLuminance = sum / samples
                };
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
#endif
