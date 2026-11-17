using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.Localization;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Museum.RightExhibit.Editor
{
    [InitializeOnLoad]
    public static class RightExhibitPanelValidation
    {
        const string Running = "Museum.RightExhibit.Validation";
        const string ExitCode = Running + ".ExitCode";
        const float ReadingFov = 60f;
        const double ExitSettleSeconds = 20;
        static double exitNotBefore;
        static readonly Vector3[] SeatedEyes =
        {
            new Vector3(3.10f, 1.20f, 1.90f),
            new Vector3(3.10f, 1.55f, 2.20f),
            new Vector3(2.85f, 1.20f, 1.90f),
            new Vector3(3.35f, 1.55f, 1.90f),
        };

        static RightExhibitPanelValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(RightExhibitPanelTools.ScenePath);
            SessionState.SetBool(Running, true);
            SessionState.SetInt(ExitCode, 1);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareBatchInput()
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += ValidateInPlayMode;
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                exitNotBefore = EditorApplication.timeSinceStartup + ExitSettleSeconds;
                EditorApplication.update += ExitBatchWhenSettled;
            }
        }

        static void ExitBatchWhenSettled()
        {
            if (EditorApplication.timeSinceStartup < exitNotBefore || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= ExitBatchWhenSettled;
            int code = SessionState.GetInt(ExitCode, 1);
            SessionState.EraseBool(Running);
            SessionState.EraseInt(ExitCode);
            EditorApplication.Exit(code);
        }

        static void ValidateInPlayMode()
        {
            var checks = new List<string>();
            var scene = SceneManager.GetActiveScene();
            try
            {
                var panel = RightExhibitPanelTools.MountPreview(scene);
                var labels = panel.GetComponentsInChildren<TextMeshPro>();
                var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                    RightExhibitPanelTools.TableFolder + "LocalizationTable.asset");
                var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(
                    RightExhibitPanelTools.TableFolder + "LanguageChannel.asset");

                Check(checks, MuseumText.Channel == channel, "Uses the saved main scene's language channel");
                Check(checks, labels.Length == 9, "Nine visitor labels");
                Check(checks, panel.GetComponentsInChildren<Collider>(true).Length == 0, "No added colliders");
                Check(checks, RightExhibitPanelTools.MountPreview(scene) == panel, "Preview reuses the mounted panel");
                CheckArrow(checks, panel.transform.Find("air route"), "Right", "Air");
                CheckArrow(checks, panel.transform.Find("voice route"), "Right", "Voice");
                CheckArrow(checks, panel.transform.Find("voice route"), "Left", "Voice");
                CheckArrow(checks, panel.transform.Find("Toward main exhibit"), "Left", "Wayfinding");
                Check(checks, !panel.transform.Find("air route/Left tip top"), "Air has no reverse arrow");

                Physics.SyncTransforms();
                var backing = panel.transform.Find("Backing").GetComponent<Renderer>().bounds;
                Check(checks, backing.min.x > 2.42f && backing.max.x < 3.78f &&
                    backing.min.y > .20f && backing.max.y < 1.10f && backing.min.z > 2.78f,
                    "Panel fits the right plinth frontage, under the tabletop and behind the walking edge");
                Check(checks, Physics.OverlapBox(backing.center, backing.extents, Quaternion.identity,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Length == 0,
                    "Panel does not intersect solid exhibit geometry");
                foreach (var eye in SeatedEyes)
                {
                    Check(checks, !Physics.Linecast(new Vector3(eye.x, .8f, 1f), new Vector3(eye.x, .8f, eye.z),
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), "Clear approach at x=" + eye.x);
                }

                var camera = new GameObject("Right exhibit diagnostic camera").AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = ReadingFov;
                camera.aspect = 4f / 3f;
                camera.nearClipPlane = .03f;
                camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
                var xr = UnityEngine.Object.FindObjectOfType<XROrigin>();
                Check(checks, xr && xr.Camera, "Saved XR camera is available");

                foreach (MuseumLanguage language in Enum.GetValues(typeof(MuseumLanguage)))
                {
                    channel.SetLanguage(language);
                    camera.transform.SetPositionAndRotation(SeatedEyes[0], Quaternion.LookRotation(backing.center - SeatedEyes[0]));
                    Capture(camera, language);
                    foreach (var label in labels)
                    {
                        string key = label.GetComponent<LocalizedText>().Key;
                        label.ForceMeshUpdate(true);
                        Check(checks, table.TryGet(key, language, out var expected) && label.text == expected,
                            language + " text " + key);
                        Check(checks, !label.isTextOverflowing && label.textInfo.lineCount == 1 &&
                            label.textBounds.size.y >= .021f, language + " single-line visible glyph height " + key +
                                $": height={label.textBounds.size.y:F3}, width={label.textBounds.size.x:F3}, " +
                                $"lines={label.textInfo.lineCount}, overflow={label.isTextOverflowing}");
                        Check(checks, label.font == table.FontAsset && expected.All(c => char.IsWhiteSpace(c) ||
                            label.font.HasCharacter(c, false, false)), language + " baked glyphs " + key);
                    }
                    CheckLabelSpacing(checks, panel.transform, labels, language);
                    foreach (var eye in SeatedEyes)
                    {
                        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(backing.center - eye));
                        foreach (var label in labels)
                            CheckSight(checks, camera, label, language);
                    }
                }

                Debug.Log("RIGHT EXHIBIT PASS: " + checks.Count + " checks");
                WriteReport(checks);
                SessionState.SetInt(ExitCode, 0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteReport(checks, ex.Message);
            }
            finally
            {
                EditorApplication.ExitPlaymode();
            }
        }

        static void CheckArrow(List<string> checks, Transform route, string direction, string name)
        {
            var top = route.Find(direction + " tip top");
            var bottom = route.Find(direction + " tip bottom");
            bool left = direction == "Left";
            Check(checks, top && bottom && top.localPosition.y > 0 && bottom.localPosition.y < 0 &&
                (left ? top.localPosition.x < 0 && bottom.localPosition.x < 0 :
                    top.localPosition.x > 0 && bottom.localPosition.x > 0), name + " " + direction + " arrowhead");
        }

        static void CheckLabelSpacing(List<string> checks, Transform root, TextMeshPro[] labels, MuseumLanguage language)
        {
            var boxes = labels.Select(label =>
            {
                var bounds = label.textBounds;
                var min = root.InverseTransformPoint(label.transform.TransformPoint(bounds.min));
                var max = root.InverseTransformPoint(label.transform.TransformPoint(bounds.max));
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }).ToArray();
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                Check(checks, box.xMin > -.60f && box.xMax < .60f && box.yMin > -.312f && box.yMax < .312f,
                    language + " text fits board " + labels[i].name + " " + box);
                for (int j = i + 1; j < boxes.Length; j++)
                {
                    Check(checks, !box.Overlaps(boxes[j]), language + " no overlap: " + labels[i].name + " / " + labels[j].name);
                }
            }
        }

        static void CheckSight(List<string> checks, Camera camera, TextMeshPro label, MuseumLanguage language)
        {
            var bounds = label.textBounds;
            var eye = camera.transform.position;
            var margin = new Vector2(.02f, .03f);
            foreach (float x in new[] { bounds.min.x, bounds.max.x })
            foreach (float y in new[] { bounds.min.y, bounds.max.y })
            {
                var corner = label.transform.TransformPoint(new Vector3(x, y, bounds.center.z));
                var projected = camera.WorldToViewportPoint(corner);
                string sample = language + " " + label.name + " from " + eye;
                Check(checks, projected.z > 0 && projected.x > margin.x && projected.x < 1 - margin.x &&
                    projected.y > margin.y && projected.y < 1 - margin.y, "Inside view: " + sample);
                Check(checks, !Physics.Linecast(eye, corner, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore), "Unobstructed: " + sample);
            }
        }

        static string OutputFolder()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-rightExhibitOutput");
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        static void Capture(Camera camera, MuseumLanguage language)
        {
            string folder = OutputFolder();
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1000, 750, 24);
            var image = new Texture2D(1000, 750, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                File.WriteAllBytes(Path.Combine(folder, language + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(image);
            }
        }

        static void WriteReport(List<string> checks, string failure = null)
        {
            string folder = OutputFolder();
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            var lines = new List<string> { failure == null ? "PASS" : "FAIL: " + failure,
                Application.unityVersion + " / " + checks.Count + " checks" };
            lines.AddRange(checks);
            File.WriteAllLines(Path.Combine(folder, "right-exhibit-report.txt"), lines);
        }

        static void Check(List<string> checks, bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add(name);
        }
    }
}
