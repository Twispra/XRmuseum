#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Museum.Refined;
using Museum.Inspection;
using Museum.Experience;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Museum.SuperLiteModels.EditorTools
{
    public static class SuperLiteModelSetup
    {
        public const string Root = "Assets/Museum/Features/SuperLiteModels";
        public const string HelmetPrefab = "Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab";
        public const string MainScene = "Assets/Museum/Scenes/MuseumStart.unity";
        const string ModelPath = Root + "/Models/superlite17b-v2-runtime.fbx";
        [Serializable] sealed class MaterialInfo { public string name; public float[] base_color_linear; public float metallic, roughness, transmission; }
        [Serializable] sealed class ExportInfo { public bool passed; public string export_sha256; public MaterialInfo[] materials; }
        [Serializable] sealed class PlacementInfo { public float root_world_y, plinth_world_y, saved_home_y, display_clearance_m; }
        [Serializable] sealed class InstallReport
        {
            public string unity, prefab, sourceExportSha256, modelGuid, prefabGuid;
            public int renderers, triangles, movingWindowRenderers, sectionDataMeshCount;
            public Vector3 displayBoundsCenter, displayBoundsSize, hinge, windowTriggerCenter, windowTriggerSize;
            public Vector3 sourceUpInUnity, sourceFrontInUnity;
            public float priorModelBottom, installedModelBottom;
            public string limitations = "Display dimensions, telephone endpoint and virtual panel articulation are assumptions. Existing scene and interaction identities retained; real headset/build/performance not tested by installation.";
        }
        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static float SupportBottom()
        {
            EditorSceneManager.OpenScene(MainScene);
            var exhibit = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            var info = JsonUtility.FromJson<PlacementInfo>(File.ReadAllText(Path.Combine(Repo, "artifacts/superlite17b-unity-integration/qa/placement-calibration.json")));
            var support = GameObject.Find("Plinth").GetComponent<Renderer>().bounds.max.y;
            if (Mathf.Abs(support - info.plinth_world_y) > .001f || Mathf.Abs(exhibit.GetComponent<Museum.HelmetExhibitReset>().HomePose.position.y - info.saved_home_y) > .001f ||
                Vector3.Distance(exhibit.transform.lossyScale, Vector3.one) > .001f || Quaternion.Angle(exhibit.transform.rotation, Quaternion.identity) > .1f)
                throw new InvalidOperationException("Scene placement changed; recalibrate against the current runtime before installation.");
            // The retained dynamic root settles 12 mm below its serialized home pose.
            // Use the observed runtime pose, with a separate provenance record, without altering physics.
            return support + info.display_clearance_m - info.root_world_y;
        }
        // An explicit placement correction for this newly installed v2, not a regeneration or guard bypass.
        public static void AlignSupportBatch()
        {
            try
            {
                var bottom = SupportBottom();
                var helmet = PrefabUtility.LoadPrefabContents(HelmetPrefab);
                try
                {
                    var binder = helmet.GetComponent<RefinedHelmetBinding>();
                    var visual = binder.AdditionalVisual;
                    if (visual == null || visual.eraIndex != 2) throw new InvalidOperationException("Reviewed v2 must already be installed.");
                    var model = Find(helmet.transform, "Refined SuperLite 17B Visual");
                    var staticParts = visual.renderers.Where(r => !r.transform.IsChildOf(visual.window)).ToArray();
                    var currentBottom = CombinedBounds(staticParts, helmet.transform).min.y;
                    float delta = bottom - currentBottom;
                    model.localPosition += Vector3.up * delta;
                    visual.hinge += Vector3.up * delta;
                    visual.sectionData.entries = SectionEntries(visual.renderers);
                    EditorUtility.SetDirty(visual.sectionData);
                    binder.ConfigureAdditionalEra(visual);
                    PrefabUtility.SaveAsPrefabAsset(helmet, HelmetPrefab);
                    var path = Path.Combine(Repo, "artifacts/superlite17b-unity-integration/qa/install-readback.json");
                    var report = JsonUtility.FromJson<InstallReport>(File.ReadAllText(path));
                    report.displayBoundsCenter += Vector3.up * delta; report.hinge = visual.hinge; report.installedModelBottom = bottom;
                    report.sectionDataMeshCount = visual.sectionData.entries.Length;
                    File.WriteAllText(path, JsonUtility.ToJson(report, true));
                    Debug.Log("SUPERLITE_SUPPORT_ALIGNED bottom=" + bottom + "; delta=" + delta);
                }
                finally { PrefabUtility.UnloadPrefabContents(helmet); }
                AssetDatabase.SaveAssets(); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static void InstallBatch()
        {
            try { Install(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        [MenuItem("Museum/SuperLite Models/Install reviewed v2")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save existing scene edits before installation.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HelmetPrefab);
            if (!prefab || !prefab.GetComponent<RefinedHelmetBinding>()) throw new InvalidOperationException("Existing Mark V binding required.");
            if (prefab.GetComponent<RefinedHelmetBinding>().AdditionalVisual != null) throw new InvalidOperationException("Already installed; refusing to overwrite subsequent edits.");
            var info = JsonUtility.FromJson<ExportInfo>(File.ReadAllText(Path.Combine(Repo, "artifacts/superlite17b-unity-integration/exports/runtime-readback.json")));
            if (!info.passed) throw new InvalidOperationException("Export readback must pass first.");
            float supportBottom = SupportBottom();
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Model missing; expand LFS and import the actual FBX first.");
            importer.isReadable = true; importer.globalScale = 1; importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            foreach (var folder in new[] { "Materials", "Data" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder);
            var backup = Path.Combine(Repo, "tmp/superlite-integration-backups", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(backup); File.Copy(Path.GetFullPath(HelmetPrefab), Path.Combine(backup, "InspectableHelmet.prefab"));
            File.Copy(Path.GetFullPath(HelmetPrefab + ".meta"), Path.Combine(backup, "InspectableHelmet.prefab.meta"));
            var helmet = PrefabUtility.LoadPrefabContents(HelmetPrefab);
            try
            {
                var binder = helmet.GetComponent<RefinedHelmetBinding>();
                var inspection = helmet.GetComponent<HelmetInspectionController>();
                var priorBounds = CombinedBounds(binder.RefinedRenderers, helmet.transform);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "Refined SuperLite 17B Visual"; visual.transform.SetParent(helmet.transform, false);
                var origin = visual.transform.position;
                var rawUp = visual.transform.InverseTransformVector(Find(visual.transform, "Anchor_Up").position - origin).normalized;
                var rawFront = visual.transform.InverseTransformVector(Find(visual.transform, "Anchor_Front").position - origin).normalized;
                visual.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(-rawFront, rawUp));
                var renderers = visual.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray();
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => BuildMaterial(info.materials.Single(p => p.name == m.name))).ToArray();
                }
                var bounds = CombinedBounds(renderers, helmet.transform);
                // Keep the reviewed model's scale, and place its lowest point 3 mm above the saved plinth.
                visual.transform.localPosition += new Vector3(-bounds.center.x, supportBottom - bounds.min.y, -bounds.center.z);
                var importedDoor = Find(visual.transform, "SL17B_InspectionWindow");
                var glass = renderers.Single(r => r.name == "Glass_Port_520-004");
                var frontBounds = LocalBounds(glass, helmet.transform);
                // The real 17B port is bolted. This virtual side pivot preserves the museum's inspection action.
                var hingePosition = new Vector3(frontBounds.max.x, frontBounds.center.y, frontBounds.center.z + .005f);
                var oldHinge = inspection.WindowPivot.localPosition;
                inspection.WindowPivot.localPosition = hingePosition;
                var door = UnityEngine.Object.Instantiate(importedDoor.gameObject).transform;
                door.name = "SuperLite inspection front panel";
                door.SetPositionAndRotation(importedDoor.position, importedDoor.rotation);
                door.localScale = importedDoor.lossyScale;
                door.SetParent(inspection.WindowPivot, true);
                // Duplicated imported meshes remain independent of the source model instance.
                importedDoor.gameObject.SetActive(false);
                var moving = door.GetComponentsInChildren<Renderer>(true);
                renderers = renderers.Where(r => !r.transform.IsChildOf(importedDoor)).Concat(moving).ToArray();
                var windowBounds = LocalBounds(moving.Single(r => r.name == "Glass_Port_520-004"), inspection.WindowPivot);
                inspection.WindowPivot.localPosition = oldHinge;
                var data = ScriptableObject.CreateInstance<HelmetSectionMeshData>();
                data.entries = SectionEntries(renderers);
                AssetDatabase.CreateAsset(data, Root + "/Data/SuperLiteSectionMeshes.asset");
                var additional = new RefinedHelmetBinding.EraVisual
                {
                    eraIndex = 2, renderers = renderers, window = door, glass = moving.Single(r => r.name == "Glass_Port_520-004"),
                    shell = renderers.Single(r => r.name.StartsWith("Shell_Yellow")), air = renderers.Single(r => r.name == "CheckValve_Brass_555-195"),
                    airOutlet = Find(visual.transform, "Anchor_AirOutlet"), telephoneOutlet = Find(visual.transform, "Anchor_TelephoneOutlet"),
                    telephone = renderers.Where(r => r.name.StartsWith("Hinge_") || r.name.StartsWith("Neck_Yoke_")).ToArray(),
                    hinge = hingePosition, triggerCenter = windowBounds.center, triggerSize = windowBounds.size + Vector3.one * .012f, sectionData = data
                };
                // The retained educational voice path uses a separate anchor; no physical posts are invented.
                binder.ConfigureAdditionalEra(additional);
                var modelBounds = CombinedBounds(renderers, helmet.transform);
                // Moving meshes have a different current hinge in the default Mark V pose; measure the actual SuperLite pose.
                inspection.WindowPivot.localPosition = hingePosition;
                modelBounds = CombinedBounds(renderers, helmet.transform);
                inspection.WindowPivot.localPosition = oldHinge;
                binder.RefreshVisualBinding();
                PrefabUtility.SaveAsPrefabAsset(helmet, HelmetPrefab);
                var result = new InstallReport
                {
                    unity = Application.unityVersion, prefab = HelmetPrefab, sourceExportSha256 = info.export_sha256,
                    modelGuid = AssetDatabase.AssetPathToGUID(ModelPath), prefabGuid = AssetDatabase.AssetPathToGUID(HelmetPrefab),
                    renderers = renderers.Length, triangles = renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3),
                    movingWindowRenderers = moving.Length, displayBoundsCenter = modelBounds.center, displayBoundsSize = modelBounds.size,
                    sectionDataMeshCount = data.entries.Length,
                    hinge = hingePosition, windowTriggerCenter = windowBounds.center, windowTriggerSize = additional.triggerSize,
                    sourceUpInUnity = visual.transform.localRotation * rawUp, sourceFrontInUnity = visual.transform.localRotation * rawFront,
                    priorModelBottom = priorBounds.min.y, installedModelBottom = modelBounds.min.y
                };
                var output = Path.Combine(Repo, "artifacts/superlite17b-unity-integration/qa/install-readback.json");
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(result, true));
                Debug.Log("SUPERLITE_INSTALL_OK " + JsonUtility.ToJson(result));
            }
            finally { PrefabUtility.UnloadPrefabContents(helmet); }
            AssetDatabase.SaveAssets();
        }
        static Material BuildMaterial(MaterialInfo info)
        {
            var path = Root + "/Materials/" + info.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing) return existing;
            bool glass = info.transmission > .99f;
            var shader = Shader.Find(glass ? "Museum/Inspection Glass" : "Museum/Inspection Shell");
            if (!shader) throw new InvalidOperationException("Required inspection shader missing.");
            var material = new Material(shader) { name = info.name, enableInstancing = true };
            var c = info.base_color_linear;
            material.SetColor("_BaseColor", glass ? new Color(.16f, .23f, .24f, .10f) : new Color(c[0], c[1], c[2], 1));
            if (!glass) { material.SetFloat("_Metallic", info.metallic); material.SetFloat("_Roughness", info.roughness); }
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static HelmetSectionMeshData.Entry[] SectionEntries(Renderer[] renderers) => renderers
            .Where(r => r.sharedMaterial && r.sharedMaterial.shader.name == "Museum/Inspection Shell")
            .Select(r => r.GetComponent<MeshFilter>().sharedMesh).Distinct()
            .Select(m => new HelmetSectionMeshData.Entry { source = m, vertices = m.vertices, triangles = m.triangles }).ToArray();
        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        public static Bounds LocalBounds(Renderer renderer, Transform parent)
        {
            var b = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            var matrix = parent.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }
        static Bounds CombinedBounds(Renderer[] renderers, Transform parent)
        { var b = LocalBounds(renderers[0], parent); foreach (var r in renderers.Skip(1)) b.Encapsulate(LocalBounds(r, parent)); return b; }
    }
}
#endif
