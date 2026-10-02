#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Museum.Inspection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Object = UnityEngine.Object;

namespace Museum.MaritimeGallery.Editor
{
    /// <summary>Read-only saved-scene checks and editor renders. Never saves or rebuilds a scene.</summary>
    public static class MaritimeGalleryValidation
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string PrefabPath = "Assets/Museum/Features/MaritimeGallery/Prefabs/MaritimeGallery.prefab";
        const string EnvironmentName = "Maritime Gallery Environment";
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        static Report report;
        static string runDirectory;

        [Serializable] public sealed class Entry { public string key, value; }
        [Serializable] public sealed class CheckResult { public string name, detail; public bool passed; }
        [Serializable] public sealed class Capture
        {
            public string file, purpose;
            public Vector3 position, euler;
            public float fieldOfView;
        }
        [Serializable] public sealed class Inventory
        {
            public int gameObjects, renderers, activeRenderers, meshRenderers, missingMeshes;
            public int lights, activeLights, shadowLights, colliders, rigidbodies, xrInteractables;
            public long triangles;
        }
        [Serializable] public sealed class Snapshot
        {
            public string scene, sceneGuid, cameraKey, helmetKey, helmetPrefabGuid, resetKey, floorKey;
            public Vector3 cameraPosition, cameraEuler, homePosition;
            public Quaternion homeRotation;
            public List<Entry> protectedComponents = new List<Entry>();
            public List<Entry> colliderStates = new List<Entry>();
            public List<Entry> materialReferences = new List<Entry>();
        }
        [Serializable] public sealed class Report
        {
            public string status = "running", mode, utc, unity, platform, graphicsDevice, graphicsApi;
            public string commit, branch, dirty, sceneHashBefore, sceneHashAfter, baselineSnapshot, error;
            public string limitation = "Static saved-scene inspection and temporary monocular Unity URP editor renders only. No Play Mode, XR input, actual headset, stereo comfort, accessibility with users, frame-time measurement, or executable build was tested. Triangle/light counts are inventory, not measured performance.";
            public Inventory inventory;
            public List<Entry> sourceHashes = new List<Entry>();
            public List<CheckResult> checks = new List<CheckResult>();
            public List<Capture> captures = new List<Capture>();
            public List<string> errors = new List<string>();
        }

        public static void CaptureBaselineBatch() { Run(true); }
        public static void ValidateBatch() { Run(false); }

        static void Run(bool baseline)
        {
            bool failed = false;
            report = new Report
            {
                mode = baseline ? "baseline" : "validation",
                utc = DateTime.UtcNow.ToString("o", Invariant),
                unity = Application.unityVersion,
                platform = Application.platform.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString()
            };
            runDirectory = Path.Combine(RepositoryRoot, "artifacts/maritime-gallery/qa/runs",
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", Invariant) + "-" + report.mode + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runDirectory);
            Application.logMessageReceived += OnLog;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "edit mode", "This validator does not enter Play Mode.");
                Require(Application.isBatchMode || !SceneManager.GetActiveScene().isDirty,
                    "no unsaved interactive scene", "Interactive use refuses to discard an unsaved scene.");
                report.commit = Git("rev-parse HEAD");
                report.branch = Git("branch --show-current");
                report.dirty = Git("status --short");
                report.sceneHashBefore = Hash(ProjectFile(ScenePath));
                RecordSourceHashes();
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
                var objects = SceneObjects(scene);
                CheckCore(objects);
                var snapshot = TakeSnapshot(objects);
                File.WriteAllText(Path.Combine(runDirectory, "snapshot.json"), JsonUtility.ToJson(snapshot, true), Encoding.UTF8);
                if (baseline)
                {
                    Require(!objects.Any(o => o.name == EnvironmentName), "baseline precedes environment integration", "Capture the saved original gallery before adding the new environment root.");
                }
                else
                {
                    string marker = Path.Combine(RepositoryRoot, "tmp/maritime-baseline-path.txt");
                    Require(File.Exists(marker), "baseline marker exists", marker);
                    string baselinePath = File.ReadAllText(marker).Trim().TrimStart('\uFEFF');
                    Require(File.Exists(baselinePath), "baseline snapshot exists", baselinePath);
                    report.baselineSnapshot = baselinePath;
                    var previous = JsonUtility.FromJson<Snapshot>(File.ReadAllText(baselinePath));
                    Require(previous != null && previous.scene == ScenePath, "baseline is the main gallery", baselinePath);
                    CompareBaseline(previous, snapshot);
                    CheckEnvironment(objects, previous);
                }
                foreach (var label in objects.SelectMany(o => o.GetComponents<TMP_Text>())) label.ForceMeshUpdate(true);
                CheckRenderAssets(objects);
                report.inventory = Count(objects);
                CaptureViews(objects);
                report.sceneHashAfter = Hash(ProjectFile(ScenePath));
                Require(report.sceneHashAfter == report.sceneHashBefore, "saved scene unchanged by validator", report.sceneHashAfter);
                Require(report.errors.Count == 0, "no Unity error logs", string.Join("\n", report.errors.ToArray()));
                report.status = "passed";
                if (baseline)
                {
                    string marker = Path.Combine(RepositoryRoot, "tmp/maritime-baseline-path.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(marker));
                    File.WriteAllText(marker, Path.Combine(runDirectory, "snapshot.json"), new UTF8Encoding(false));
                    report.baselineSnapshot = Path.Combine(runDirectory, "snapshot.json");
                }
            }
            catch (Exception exception)
            {
                failed = true;
                report.status = "failed";
                report.error = exception.ToString();
                Debug.LogError("MARITIME_GALLERY_" + report.mode.ToUpperInvariant() + "_FAILED " + exception);
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                try
                {
                    report.sceneHashAfter = Hash(ProjectFile(ScenePath));
                    if (!string.IsNullOrEmpty(report.sceneHashBefore) && report.sceneHashAfter != report.sceneHashBefore)
                    {
                        failed = true; report.status = "failed";
                        report.error += "\nSaved scene bytes changed while validation was running.";
                    }
                    File.WriteAllText(Path.Combine(runDirectory, "report.json"), JsonUtility.ToJson(report, true), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(runDirectory, "README.txt"), Summary(), Encoding.UTF8);
                    Debug.Log("MARITIME_GALLERY_" + report.mode.ToUpperInvariant() + "_" + report.status.ToUpperInvariant() + " " + runDirectory);
                }
                catch (Exception exception) { failed = true; Debug.LogError("Cannot write maritime report: " + exception); }
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        static void CheckCore(GameObject[] objects)
        {
            Require(objects.All(o => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(o) == 0),
                "no missing scripts", "All saved scene objects, including inactive objects.");
            var controllers = objects.SelectMany(o => o.GetComponents<HelmetInspectionController>()).ToArray();
            Require(controllers.Length == 1, "one helmet inspection controller", "Count=" + controllers.Length);
            var helmet = controllers[0];
            var reset = helmet.GetComponent<HelmetExhibitReset>();
            Require(reset && helmet.GetComponent<Rigidbody>() && helmet.GetComponent<XRGrabInteractable>(),
                "helmet physical and interaction components", Hierarchy(helmet.transform));
            var rigs = objects.Where(o => o.name == "XR Origin (XR Rig)").ToArray();
            Require(rigs.Length == 1 && Camera.main && Camera.main.transform.IsChildOf(rigs[0].transform),
                "original XR main camera", "Exactly one original named rig owns Camera.main.");
            Require(objects.SelectMany(o => o.GetComponents<Camera>()).Count(c => c.CompareTag("MainCamera")) == 1,
                "one main camera", "No environment camera replaces the production XR view.");
            Require(!objects.SelectMany(o => o.GetComponents<HelmetInspectionDesktopPreview>()).Any(),
                "no desktop orbit controller", "The production main gallery remains the XR scene.");
            var button = objects.SingleOrDefault(o => o.name == "Helmet Reset Button");
            var interactable = button ? button.GetComponent<XRSimpleInteractable>() : null;
            Require(interactable, "original physical RESET exists", "Helmet Reset Button / XRSimpleInteractable");
            int correctListeners = 0;
            for (int i = 0; i < interactable.selectEntered.GetPersistentEventCount(); i++)
                if (interactable.selectEntered.GetPersistentTarget(i) == reset && interactable.selectEntered.GetPersistentMethodName(i) == "ResetToDisplay") correctListeners++;
            Require(correctListeners == 1, "original RESET targets current helmet", "Exactly one persistent ResetToDisplay listener targets its HelmetExhibitReset.");
            var floor = objects.SingleOrDefault(o => o.name == "Floor - Teleport Area");
            Require(floor && floor.activeInHierarchy && floor.GetComponent<TeleportationArea>() && floor.GetComponent<Collider>() && floor.GetComponent<Collider>().enabled,
                "original teleport floor active", "Existing Floor - Teleport Area remains active with enabled collider and TeleportationArea.");
            var area = floor.GetComponent<TeleportationArea>();
            Require(area.enabled && area.interactionLayers.value == unchecked((int)0x80000000),
                "teleport interaction layer retained", "Teleport bit31 only; actual=" + area.interactionLayers.value);
            var rays = new[] { new Vector3(0, 2, 2.2f), new Vector3(-1.6f, 2, 2.2f), new Vector3(1.6f, 2, 2.2f),
                new Vector3(0, 2, 5), new Vector3(0, 2, -2), new Vector3(-4.5f, 2, 6), new Vector3(4.5f, 2, 6) };
            foreach (var origin in rays)
            {
                // The visitor's existing capsule is not an environment obstruction.
                // Keep every other solid collider in the query so new decor cannot mask a floor blocker.
                var hits = Physics.RaycastAll(origin, Vector3.down, 3, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.collider.transform.IsChildOf(rigs[0].transform)).OrderBy(h => h.distance).ToArray();
                bool found = hits.Length > 0;
                RaycastHit hit = found ? hits[0] : default(RaycastHit);
                Require(found && hit.collider.gameObject == floor, "floor ray remains unobstructed " + V(origin),
                    found ? Hierarchy(hit.collider.transform) + " at " + V(hit.point) : "No floor hit.");
            }
        }

        static Snapshot TakeSnapshot(GameObject[] objects)
        {
            var helmet = objects.SelectMany(o => o.GetComponents<HelmetInspectionController>()).Single();
            var reset = helmet.GetComponent<HelmetExhibitReset>();
            var floor = objects.Single(o => o.name == "Floor - Teleport Area");
            var snapshot = new Snapshot
            {
                scene = ScenePath, sceneGuid = AssetDatabase.AssetPathToGUID(ScenePath),
                cameraKey = Key(Camera.main), cameraPosition = Camera.main.transform.position, cameraEuler = Camera.main.transform.eulerAngles,
                helmetKey = Key(helmet), helmetPrefabGuid = AssetDatabase.AssetPathToGUID(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(helmet.gameObject)),
                resetKey = Key(reset), floorKey = Key(floor.GetComponent<TeleportationArea>()),
                homePosition = reset.HomePose.position, homeRotation = reset.HomePose.rotation
            };
            foreach (var go in objects.Where(o => !UnderEnvironment(o.transform)))
            {
                // Keep all original transforms and physical/input components. Renderer styling and old sign visibility may change.
                foreach (var component in go.GetComponents<Component>())
                {
                    if (!component) continue;
                    string ns = component.GetType().Namespace ?? "";
                    bool protectedType = component is Transform || component is Collider || component is Rigidbody || component is Camera ||
                        ns.StartsWith("UnityEngine.XR.Interaction.Toolkit", StringComparison.Ordinal) || ns.StartsWith("Unity.XR.CoreUtils", StringComparison.Ordinal) ||
                        ns.StartsWith("UnityEngine.InputSystem", StringComparison.Ordinal) || ns.StartsWith("Museum.Inspection", StringComparison.Ordinal) || component is HelmetExhibitReset;
                    if (protectedType) snapshot.protectedComponents.Add(new Entry { key = Key(component), value = Signature(component) });
                    var collider = component as Collider;
                    if (collider) snapshot.colliderStates.Add(new Entry { key = Key(collider), value = ColliderState(collider) });
                }
                foreach (var renderer in go.GetComponents<Renderer>())
                {
                    string materials = string.Join(" | ", renderer.sharedMaterials.Select(Reference).ToArray());
                    snapshot.materialReferences.Add(new Entry { key = Key(renderer), value = materials });
                    if (go.transform.IsChildOf(helmet.transform))
                        snapshot.protectedComponents.Add(new Entry { key = Key(renderer) + "/helmet-materials", value = materials });
                }
            }
            snapshot.protectedComponents = snapshot.protectedComponents.OrderBy(e => e.key, StringComparer.Ordinal).ToList();
            snapshot.colliderStates = snapshot.colliderStates.OrderBy(e => e.key, StringComparer.Ordinal).ToList();
            return snapshot;
        }

        static void CompareBaseline(Snapshot previous, Snapshot current)
        {
            Require(previous.sceneGuid == current.sceneGuid, "scene GUID preserved", current.sceneGuid);
            Require(previous.cameraKey == current.cameraKey && Vector3.Distance(previous.cameraPosition, current.cameraPosition) < .0001f &&
                Quaternion.Angle(Quaternion.Euler(previous.cameraEuler), Quaternion.Euler(current.cameraEuler)) < .001f,
                "saved XR camera pose preserved", V(current.cameraPosition) + " / " + V(current.cameraEuler));
            Require(previous.helmetKey == current.helmetKey && previous.helmetPrefabGuid == current.helmetPrefabGuid && previous.resetKey == current.resetKey,
                "helmet prefab and reset identity preserved", current.helmetPrefabGuid);
            Require(Vector3.Distance(previous.homePosition, current.homePosition) < .0001f && Quaternion.Angle(previous.homeRotation, current.homeRotation) < .001f,
                "helmet home pose preserved", V(current.homePosition));
            Require(previous.floorKey == current.floorKey, "original teleport component preserved", current.floorKey);
            CompareEntries(previous.protectedComponents, current.protectedComponents, "original transforms, XR, input, helmet and physical settings preserved");
            var expectedColliderStates = previous.colliderStates.Select(e => new Entry { key = e.key, value = e.value }).ToList();
            foreach (var entry in expectedColliderStates.Where(e => IsRetiredPlaceholderCollider(e.key)))
                entry.value = entry.value.Replace("active=True;", "active=False;");
            CompareEntries(expectedColliderStates, current.colliderStates, "original colliders preserved with two intentional placeholder deactivations");
            Check(true, "decorative material replacements permitted", "Baseline and current material GUIDs are recorded. Only protected helmet materials must be identical; old gallery Renderer colors and enabled states may change.");
            Check(true, "intentional visual differences", "The two Replace With Museum Exhibit objects are retained but deactivated, including their primitive colliders. Old welcome/exhibit-number Renderers may be hidden. Wall/floor/plinth materials and original light/RenderSettings color and intensity may change. The comparison does not claim unchanged scene bytes relative to baseline.");
        }

        static void CompareEntries(List<Entry> before, List<Entry> after, string name)
        {
            var map = after.ToDictionary(e => e.key, e => e.value, StringComparer.Ordinal);
            var failures = new List<string>();
            foreach (var item in before)
            {
                string current;
                if (!map.TryGetValue(item.key, out current)) failures.Add("Missing " + item.key);
                else if (StableSignature(current) != StableSignature(item.value)) failures.Add("Changed " + item.key + "\nBefore: " + item.value + "\nAfter: " + current);
            }
            // No additional production input or physical component is allowed outside the new environment prefab.
            var oldKeys = new HashSet<string>(before.Select(e => e.key), StringComparer.Ordinal);
            failures.AddRange(after.Where(e => !oldKeys.Contains(e.key)).Select(e => "Unexpected original-hierarchy component " + e.key));
            Require(failures.Count == 0, name, failures.Count == 0 ? before.Count + " entries match baseline; transient ObjectReference .m_FileID/.m_PathID internals excluded, stable parent GUID/hierarchy references compared." : string.Join("\n", failures.ToArray()));
        }

        static string StableSignature(string value)
        {
            // Older baseline snapshots contain transient Editor instance IDs below ObjectReference values.
            // Do not rewrite that evidence; compare both sides with only those redundant internals removed.
            return string.Join("\n", value.Split('\n').Where(line =>
            {
                int equals = line.IndexOf('=');
                string path = equals >= 0 ? line.Substring(0, equals) : line;
                return !path.EndsWith(".m_FileID", StringComparison.Ordinal) && !path.EndsWith(".m_PathID", StringComparison.Ordinal);
            }).ToArray());
        }

        static void CheckEnvironment(GameObject[] objects, Snapshot previous)
        {
            var roots = objects.Where(o => o.name == EnvironmentName).ToArray();
            Require(roots.Length == 1 && roots[0].activeInHierarchy && roots[0].transform.parent == null,
                "one independent environment root", EnvironmentName);
            var root = roots[0];
            Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == PrefabPath,
                "environment is the dedicated prefab", PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root));
            Require(root.GetComponentsInChildren<XRBaseInteractable>(true).Length == 0 && root.GetComponentsInChildren<XRBaseInteractor>(true).Length == 0,
                "environment adds no XR interaction", "No new select/hover targets or interactors.");
            Require(root.GetComponentsInChildren<Rigidbody>(true).Length == 0, "environment adds no rigidbody", "Decorative objects cannot disturb exhibit physics.");
            Require(root.GetComponentsInChildren<Camera>(true).Length == 0, "environment adds no camera", "All captures use temporary untagged cameras.");
            var retired = objects.Where(o => o.name == "Replace With Museum Exhibit").ToArray();
            Require(retired.Length == 2 && retired.All(o => !o.activeSelf && o.GetComponent<Collider>()),
                "two original placeholders retained and deactivated", "Their existing Renderer and primitive Collider components remain present; their shapes are compared to baseline.");
            var originalKeys = new HashSet<string>(previous.colliderStates.Select(e => e.key), StringComparer.Ordinal);
            var added = objects.SelectMany(o => o.GetComponents<Collider>()).Where(c => !originalKeys.Contains(Key(c))).ToArray();
            var safeRegion = new Bounds(new Vector3(0, 1.6f, 3.45f), new Vector3(4.8f, 3.2f, 4.5f));
            foreach (var collider in added)
            {
                Require(collider.transform.IsChildOf(root.transform), "new collider belongs to environment", Key(collider));
                if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                var bounds = collider.bounds;
                Require(!bounds.Intersects(safeRegion), "new collider leaves helmet and menu zone clear", Key(collider) + " bounds=" + bounds);
                bool frontBoundary = Mathf.Abs(bounds.center.z + 4.98f) <= .25f && bounds.max.z <= -4.65f;
                bool roofBoundary = bounds.min.y >= 3.8f && Mathf.Abs(bounds.center.y - 4.2f) <= .5f;
                Require(frontBoundary || roofBoundary, "new collider confined to front wall or roof", Key(collider) + " bounds=" + bounds);
            }
            Check(true, "central seated and single-hand space preserved", "No new enabled collider intersects X[-2.4,2.4], Y[0,3.2], Z[1.2,5.7]. Actual menu/input behavior requires the existing Play Mode regression suite.");
            var environmentRenderers = root.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToArray();
            bool roof = environmentRenderers.Any(r => r.bounds.size.x >= 9 && r.bounds.size.z >= 10 && r.bounds.min.y >= 3.8f && r.bounds.max.y <= 4.8f);
            bool front = environmentRenderers.Any(r => r.bounds.size.x >= 8 && r.bounds.size.y >= 2.5f && Mathf.Abs(r.bounds.center.z + 4.98f) <= .4f);
            Require(roof, "visible ceiling closes upward view", "A wide roof Renderer covers the room around Y=4.2.");
            Require(front, "visible front boundary closes backward view", "A wide front-wall Renderer covers the room around Z=-4.98.");
        }

        static void CheckRenderAssets(GameObject[] objects)
        {
            var problems = new List<string>();
            foreach (var renderer in objects.SelectMany(o => o.GetComponents<Renderer>()))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!material) problems.Add(Key(renderer) + ": missing material");
                    else if (!material.shader || material.shader.name == "Hidden/InternalErrorShader" || !material.shader.isSupported)
                        problems.Add(Key(renderer) + ": missing/error/unsupported shader " + (material.shader ? material.shader.name : "null"));
                }
                var filter = renderer.GetComponent<MeshFilter>();
                var text = renderer.GetComponent<TMP_Text>();
                // TMP generates its mesh on activation; hidden original menu labels need a font, not a baked mesh.
                bool deferredText = text && !text.gameObject.activeInHierarchy;
                if (text && !text.font) problems.Add(Key(renderer) + ": missing text font");
                if (!deferredText && renderer is MeshRenderer && (!filter || !filter.sharedMesh || filter.sharedMesh.vertexCount == 0))
                    problems.Add(Key(renderer) + ": absent/empty visible mesh (check Git LFS hydration)");
            }
            Require(problems.Count == 0, "no missing render assets or error shaders", problems.Count == 0 ? "All scene renderers including inactive originals checked; actual pixels also require visual review." : string.Join("\n", problems.ToArray()));
        }

        static Inventory Count(GameObject[] objects)
        {
            var renderers = objects.SelectMany(o => o.GetComponents<Renderer>()).ToArray();
            var lights = objects.SelectMany(o => o.GetComponents<Light>()).ToArray();
            var inventory = new Inventory
            {
                gameObjects = objects.Length, renderers = renderers.Length,
                activeRenderers = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy),
                meshRenderers = renderers.Count(r => r is MeshRenderer),
                lights = lights.Length, activeLights = lights.Count(l => l.enabled && l.gameObject.activeInHierarchy),
                shadowLights = lights.Count(l => l.enabled && l.gameObject.activeInHierarchy && l.shadows != LightShadows.None),
                colliders = objects.Sum(o => o.GetComponents<Collider>().Length), rigidbodies = objects.Sum(o => o.GetComponents<Rigidbody>().Length),
                xrInteractables = objects.Sum(o => o.GetComponents<XRBaseInteractable>().Length)
            };
            foreach (var renderer in renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy))
            {
                Mesh mesh = null;
                var skinned = renderer as SkinnedMeshRenderer;
                if (skinned) mesh = skinned.sharedMesh;
                else { var filter = renderer.GetComponent<MeshFilter>(); if (filter) mesh = filter.sharedMesh; }
                if (!mesh) { if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) inventory.missingMeshes++; continue; }
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    if (mesh.GetTopology(submesh) == MeshTopology.Triangles) inventory.triangles += (long)mesh.GetIndexCount(submesh) / 3;
            }
            return inventory;
        }

        static void CaptureViews(GameObject[] objects)
        {
            var original = Camera.main;
            var temporary = new GameObject("Temporary Maritime Gallery Capture") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = temporary.AddComponent<Camera>();
                camera.CopyFrom(original); camera.enabled = false; camera.targetTexture = null; camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.aspect = 16f / 10f; camera.nearClipPlane = .05f; camera.farClipPlane = 100;
                var data = temporary.AddComponent<UniversalAdditionalCameraData>(); data.allowXRRendering = false;
                data.renderPostProcessing = false;
                var startup = original.transform.position;
                AddCapture(camera, "01-startup", "Saved XR startup pose, unchanged", startup, original.transform.rotation, 60);
                LookCapture(camera, "02-wide-front", "Wide gallery approach", new Vector3(0, 1.6f, -3.1f), new Vector3(0, 1.65f, 4.6f), 78);
                LookCapture(camera, "03-seated", "Seated eye height at the original exhibit", new Vector3(0, 1.2f, 2.15f), new Vector3(0, 1.45f, 4), 76);
                AddCapture(camera, "04-left", "Left direction from startup", startup, Quaternion.Euler(0, -90, 0), 82);
                AddCapture(camera, "05-right", "Right direction from startup", startup, Quaternion.Euler(0, 90, 0), 82);
                AddCapture(camera, "06-back", "Backward direction from startup", startup, Quaternion.Euler(0, 180, 0), 82);
                LookCapture(camera, "07-exhibit-from-rear", "Look back at all exhibits from the rear gallery", new Vector3(0, 1.6f, 7), new Vector3(0, 1.35f, 3.5f), 88);
                AddCapture(camera, "08-ceiling", "Upward view from startup", startup, Quaternion.Euler(-72, 0, 0), 84);
                AddCapture(camera, "09-floor", "Downward view from startup", startup, Quaternion.Euler(67, 0, 0), 84);
            }
            finally { Object.DestroyImmediate(temporary); }
        }

        static void LookCapture(Camera camera, string name, string purpose, Vector3 position, Vector3 target, float fov)
        { AddCapture(camera, name, purpose, position, Quaternion.LookRotation(target - position, Vector3.up), fov); }

        static void AddCapture(Camera camera, string name, string purpose, Vector3 position, Quaternion rotation, float fov)
        {
            camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
            const int width = 1440, height = 900;
            string file = name + ".png";
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(runDirectory, file), pixels.EncodeToPNG());
                report.captures.Add(new Capture { file = file, purpose = purpose, position = position, euler = rotation.eulerAngles, fieldOfView = fov });
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                if (pixels) Object.DestroyImmediate(pixels);
            }
        }

        static void RecordSourceHashes()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal)
            {
                ScenePath, "Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab",
                "Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectSettings.asset",
                "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset",
                "Assets/Museum/Settings/MuseumURP.asset", "Assets/Museum/Settings/MuseumRenderer.asset"
            };
            if (File.Exists(ProjectFile(PrefabPath))) paths.Add(PrefabPath);
            foreach (var path in AssetDatabase.GetDependencies(ScenePath, true)) if (File.Exists(ProjectFile(path))) paths.Add(path);
            string scripts = ProjectFile("Assets/Museum/Features/MaritimeGallery");
            if (Directory.Exists(scripts)) foreach (var path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
                paths.Add(path.Substring(ProjectRoot.Length + 1).Replace('\\', '/'));
            foreach (var path in paths.OrderBy(p => p, StringComparer.Ordinal))
                report.sourceHashes.Add(new Entry { key = path, value = File.Exists(ProjectFile(path)) ? Hash(ProjectFile(path)) : "MISSING" });
        }

        static string Signature(Component component)
        {
            var builder = new StringBuilder();
            using (var serialized = new SerializedObject(component))
            {
                var iterator = serialized.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyPath == "m_ObjectHideFlags") continue;
                    if (iterator.propertyType == SerializedPropertyType.Generic) continue;
                    // Root child arrays gain the one new prefab only in SceneRoots, never on an existing Transform.
                    builder.Append(iterator.propertyPath).Append('=').Append(PropertyValue(iterator)).Append('\n');
                }
            }
            return StableSignature(builder.ToString());
        }

        static string PropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: return property.longValue.ToString(Invariant);
                case SerializedPropertyType.Boolean: return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Float: return property.doubleValue.ToString("R", Invariant);
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Color: return property.colorValue.ToString("F9");
                case SerializedPropertyType.ObjectReference: return Reference(property.objectReferenceValue);
                case SerializedPropertyType.LayerMask: case SerializedPropertyType.Enum: case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character: case SerializedPropertyType.FixedBufferSize: return property.intValue.ToString(Invariant);
                case SerializedPropertyType.Vector2: return property.vector2Value.ToString("F9");
                case SerializedPropertyType.Vector3: return V(property.vector3Value);
                case SerializedPropertyType.Vector4: return property.vector4Value.ToString("F9");
                case SerializedPropertyType.Rect: return property.rectValue.ToString("F9");
                case SerializedPropertyType.Bounds: return property.boundsValue.ToString("F9");
                case SerializedPropertyType.Quaternion: return property.quaternionValue.ToString("F9");
                case SerializedPropertyType.Vector2Int: return property.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int: return property.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt: return property.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt: return property.boundsIntValue.ToString();
                case SerializedPropertyType.AnimationCurve:
                    var curve = property.animationCurveValue;
                    return curve.preWrapMode + "/" + curve.postWrapMode + "/" + string.Join(";", curve.keys.Select(k =>
                        k.time.ToString("R", Invariant) + "," + k.value.ToString("R", Invariant) + "," + k.inTangent.ToString("R", Invariant) + "," + k.outTangent.ToString("R", Invariant)).ToArray());
                default: return property.propertyType + ":" + property.type;
            }
        }

        static string ColliderState(Collider collider)
        {
            return "active=" + collider.gameObject.activeInHierarchy + ";enabled=" + collider.enabled + ";trigger=" + collider.isTrigger +
                ";layer=" + collider.gameObject.layer + ";position=" + V(collider.transform.position) + ";rotation=" + collider.transform.rotation.ToString("F9") +
                ";scale=" + V(collider.transform.lossyScale) + ";physicsMaterial=" + Reference(collider.sharedMaterial) + "\n" + Signature(collider);
        }

        static bool IsRetiredPlaceholderCollider(string key)
        {
            return key.StartsWith("Exhibit Placeholders/Exhibit 01 - Placeholder/Replace With Museum Exhibit/", StringComparison.Ordinal) ||
                key.StartsWith("Exhibit Placeholders/Exhibit 03 - Placeholder/Replace With Museum Exhibit/", StringComparison.Ordinal);
        }

        static string Key(Component component) { return Hierarchy(component.transform) + "/" + component.GetType().FullName + "#" + Array.IndexOf(component.GetComponents(component.GetType()), component); }
        static string Reference(Object value)
        {
            if (!value) return "null";
            string guid; long localId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out localId) && !string.IsNullOrEmpty(guid)) return guid + ":" + localId;
            var component = value as Component;
            if (component) return Key(component);
            var gameObject = value as GameObject;
            if (gameObject) return Hierarchy(gameObject.transform) + "/GameObject";
            return GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() + ":" + value.name;
        }
        static string Hierarchy(Transform transform) { return transform.parent ? Hierarchy(transform.parent) + "/" + transform.name : transform.name; }
        static bool UnderEnvironment(Transform transform)
        { for (var current = transform; current; current = current.parent) if (current.name == EnvironmentName) return true; return false; }
        static GameObject[] SceneObjects(Scene scene)
        { return scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray(); }
        static string V(Vector3 value) { return value.x.ToString("F9", Invariant) + "," + value.y.ToString("F9", Invariant) + "," + value.z.ToString("F9", Invariant); }
        static string ProjectRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
        static string RepositoryRoot { get { return Path.GetFullPath(Path.Combine(ProjectRoot, "..")); } }
        static string ProjectFile(string relative) { return Path.GetFullPath(Path.Combine(ProjectRoot, relative)); }
        static string Hash(string path)
        {
            if (!File.Exists(path)) return "MISSING";
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string Git(string arguments)
        {
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", arguments)
                { WorkingDirectory = RepositoryRoot, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return process.ExitCode == 0 ? output.Trim() : "UNAVAILABLE: " + error.Trim();
                }
            }
            catch (Exception exception) { return "UNAVAILABLE: " + exception.Message; }
        }
        static void Require(bool passed, string name, string detail)
        { Check(passed, name, detail); if (!passed) throw new InvalidOperationException(name + ": " + detail); }
        static void Check(bool passed, string name, string detail)
        { report.checks.Add(new CheckResult { name = name, passed = passed, detail = detail }); }
        static void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + message + "\n" + stack); }
        static string Summary()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Maritime gallery " + report.mode + ": " + report.status);
            builder.AppendLine("UTC: " + report.utc).AppendLine("Commit: " + report.commit).AppendLine("Branch: " + report.branch);
            builder.AppendLine("Unity: " + report.unity).AppendLine("Graphics: " + report.graphicsDevice + " / " + report.graphicsApi);
            builder.AppendLine("Dirty checkout at start:\n" + report.dirty).AppendLine(report.limitation);
            builder.AppendLine("Checks: " + report.checks.Count(c => c.passed) + "/" + report.checks.Count);
            if (!string.IsNullOrEmpty(report.baselineSnapshot)) builder.AppendLine("Baseline: " + report.baselineSnapshot);
            foreach (var check in report.checks) builder.AppendLine((check.passed ? "PASS " : "FAIL ") + check.name + ": " + check.detail);
            if (report.inventory != null) builder.AppendLine("Inventory: " + JsonUtility.ToJson(report.inventory));
            foreach (var capture in report.captures) builder.AppendLine(capture.file + ": " + capture.purpose);
            if (!string.IsNullOrEmpty(report.error)) builder.AppendLine(report.error);
            return builder.ToString();
        }
    }
}
#endif
