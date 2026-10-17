#if UNITY_EDITOR
using System;
using System.Linq;
using Museum.Eras;
using Museum.Experience;
using Museum.Inspection;
using Museum.Localization;
using Museum.Localization.EditorTools;
using Museum.Refined;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Museum.EarlyHelmet.EditorTools
{
    /// <summary>One-time, explicit install of the sourced index-0 visual. Never runs on project open.</summary>
    public static class EarlyHelmetIntegration
    {
        const string Root = "Assets/Museum/Features/EarlyHelmet";
        const string Model = Root + "/Models/early-1881-47-runtime.obj";
        const string Prefab = "Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab";
        const string Scene = "Assets/Museum/Scenes/MuseumStart.unity";

        public static void InstallBatch()
        {
            try { Install(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        [MenuItem("Museum/Early Helmet/Install 1881-47 at era 0")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play before editing the saved prefab.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (!source) throw new InvalidOperationException("Early OBJ has not imported. Check the model and its .meta file.");
            var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Unity model importer is missing.");
            importer.isReadable = true;
            importer.globalScale = 1f;
            importer.importAnimation = false;
            importer.preserveHierarchy = true;
            importer.SaveAndReimport();

            var originalGuid = AssetDatabase.AssetPathToGUID(Prefab);
            var helmet = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var binding = helmet.GetComponent<RefinedHelmetBinding>();
                var inspection = helmet.GetComponent<HelmetInspectionController>();
                if (!binding || !inspection || binding.AdditionalVisual == null || binding.EarlyVisual != null)
                    throw new InvalidOperationException("Expected installed Mark V and SuperLite, without an early replacement.");
                var previousBounds = CombinedBounds(binding.RefinedRenderers, helmet.transform);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "Early 1839 helmet 1881-47 visual";
                model.transform.SetParent(helmet.transform, false);
                // OBJ stores the editable source's Z-up, -Y-front coordinates.
                // The saved gallery views the exhibit along local -Z. Rotate the
                // source's -Y front toward that visitor view after Z-up import.
                model.transform.localRotation = Quaternion.AngleAxis(180f, Vector3.up) *
                    Quaternion.Euler(-90f, 0f, 0f);
                var imported = model.GetComponentsInChildren<MeshRenderer>(true);
                if (imported.Length != 56 || !imported.Any(r => r.name == "FrontPort_Glass") ||
                    !imported.Any(r => r.name == "Breastplate_DishedCopper"))
                    throw new InvalidOperationException("Imported object list differs from the reviewed 56-part export: " +
                        imported.Length + " renderers: " + string.Join(", ", imported.Select(r => r.name)));
                foreach (var renderer in imported)
                    renderer.sharedMaterial = MaterialFor(renderer.name);

                var rawBounds = CombinedBounds(imported.Cast<Renderer>().ToArray(), helmet.transform);
                model.transform.localPosition += new Vector3(previousBounds.center.x - rawBounds.center.x,
                    previousBounds.min.y - rawBounds.min.y, previousBounds.center.z - rawBounds.center.z);

                // The source object has no documented opening hinge. Only the XR
                // virtual inspection panel moves; no hinge mesh is added to the model.
                var front = imported.Where(r => r.name.StartsWith("FrontPort_", StringComparison.Ordinal)).ToArray();
                var glassOriginal = front.Single(r => r.name == "FrontPort_Glass");
                var frontBounds = LocalBounds(glassOriginal, helmet.transform);
                var hinge = new Vector3(frontBounds.max.x + .006f, frontBounds.center.y, frontBounds.center.z);
                var oldHinge = inspection.WindowPivot.localPosition;
                inspection.WindowPivot.localPosition = hinge;
                var door = new GameObject("Early virtual front inspection panel").transform;
                door.SetParent(inspection.WindowPivot, false);
                foreach (var renderer in front)
                {
                    var copy = UnityEngine.Object.Instantiate(renderer.gameObject);
                    copy.name = renderer.name;
                    copy.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                    copy.transform.localScale = renderer.transform.lossyScale;
                    copy.transform.SetParent(door, true);
                    renderer.gameObject.SetActive(false);
                }
                var moving = door.GetComponentsInChildren<MeshRenderer>(true);
                var still = imported.Except(front).Cast<Renderer>().ToArray();
                var all = still.Concat(moving.Cast<Renderer>()).ToArray();
                var movingGlass = moving.Single(r => r.name == "FrontPort_Glass");
                var trigger = LocalBounds(movingGlass, inspection.WindowPivot);
                inspection.WindowPivot.localPosition = oldHinge;

                EnsureFolder(Root, "Materials");
                EnsureFolder(Root, "Data");
                var data = ScriptableObject.CreateInstance<HelmetSectionMeshData>();
                data.entries = all.Where(r => r.sharedMaterial && r.sharedMaterial.shader.name == "Museum/Inspection Shell")
                    .Select(r => r.GetComponent<MeshFilter>().sharedMesh).Distinct()
                    .Select(mesh => new HelmetSectionMeshData.Entry
                    { source = mesh, vertices = mesh.vertices, triangles = mesh.triangles }).ToArray();
                AssetDatabase.CreateAsset(data, Root + "/Data/Early1881SectionMeshes.asset");

                var air = new GameObject("Early AIR teaching endpoint").transform;
                air.SetParent(model.transform, false);
                air.localPosition = new Vector3(-.12f, .115f, .245f);
                var voice = new GameObject("Early VOICE teaching endpoint").transform;
                voice.SetParent(model.transform, false);
                voice.localPosition = new Vector3(.12f, .115f, .245f);
                var visual = new RefinedHelmetBinding.EraVisual
                {
                    eraIndex = 0, renderers = all, window = door, glass = movingGlass,
                    shell = still.Single(r => r.name == "Bonnet_RoundedCopper"),
                    air = still.Single(r => r.name == "Bonnet_RoundedCopper"),
                    airOutlet = air, telephoneOutlet = voice, telephone = Array.Empty<Renderer>(),
                    hinge = hinge, triggerCenter = trigger.center,
                    triggerSize = trigger.size + Vector3.one * .012f, sectionData = data
                };
                binding.ConfigureEarlyEra(visual);
                PrefabUtility.SaveAsPrefabAsset(helmet, Prefab);
                Debug.Log($"EARLY_HELMET_INSTALLED guid={AssetDatabase.AssetPathToGUID(Model)} renderers={all.Length} triangles=" +
                    all.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3));
            }
            finally { PrefabUtility.UnloadPrefabContents(helmet); }
            if (AssetDatabase.AssetPathToGUID(Prefab) != originalGuid)
                throw new InvalidOperationException("Existing prefab GUID changed; abort review.");
            var scene = EditorSceneManager.OpenScene(Scene);
            var saved = UnityEngine.Object.FindObjectOfType<RefinedHelmetBinding>();
            if (!saved || saved.EarlyVisual == null || saved.EarlyVisual.eraIndex != 0)
                throw new InvalidOperationException("MuseumStart does not resolve the installed index-0 visual.");
            EditorSceneManager.SaveScene(scene);
            SyncContent();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Museum/Early Helmet/Sync 1881-47 text and profile")]
        public static void SyncContent()
        {
            // This single-surface photo study has no measured interior wall.
            // Render either visible side in the same copper tone, including
            // Unity's imported OBJ backfaces, instead of the shared shader's
            // default teal interior colour used by the hollow Mark V model.
            foreach (var name in new[] { "AgedCopper", "Bronze", "WornCopper", "Seam" })
            {
                var surface = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Early" + name + ".mat");
                if (!surface) throw new InvalidOperationException("Missing early model material: " + name);
                surface.SetColor("_InsideColor", surface.GetColor("_BaseColor"));
                surface.SetFloat("_SpecularScale", .25f);
                EditorUtility.SetDirty(surface);
            }
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            if (!table || !table.FontAsset) throw new InvalidOperationException("Saved trilingual table/font is missing.");
            TrilingualMuseumSetup.ApplyTable(table, table.FontAsset);
            var profile = AssetDatabase.LoadAssetAtPath<HelmetEraProfile>(
                "Assets/Museum/Features/HelmetEras/Profiles/Era-1839-Standard-Dress.asset");
            if (!profile) throw new InvalidOperationException("Saved 1839 profile is missing.");
            var data = new SerializedObject(profile);
            data.FindProperty("displayName").stringValue = "1839 早期封闭式头盔 · 1881-47";
            data.FindProperty("summary").stringValue = "依据英国科学博物馆集团馆藏 1881-47 的照片制作：圆顶帽体、三处带护栏观察窗与宽大的胸肩板。不可见的背面和部件尺寸是建模估算。";
            data.FindProperty("servicePeriod").stringValue = "1839 年制（馆藏年代；使用年限未详）";
            data.FindProperty("operatingDepth").stringValue = "该馆藏页未注明作业深度";
            data.FindProperty("factSource").stringValue = "Science Museum Group Collection 1881-47: https://collection.sciencemuseumgroup.org.uk/objects/co40614/first-closed-diving-helmet-and-breastplate；外形参照其公开照片，不复制照片。馆藏给出整体 465×460×417 mm，未标明各轴，模型轴向及局部尺寸为估算。";
            var details = data.FindProperty("details");
            if (details.arraySize < 3) throw new InvalidOperationException("1839 profile lost its three explanation rows.");
            details.GetArrayElementAtIndex(0).FindPropertyRelative("text").stringValue =
                "馆藏照片可见三处带护栏的观察窗；游戏内前面板的开启是虚拟剖析功能，并非有史料证实的原物铰链。";
            details.GetArrayElementAtIndex(1).FindPropertyRelative("text").stringValue =
                "馆藏记录未给出此件供气接头的准确位置；场景 AIR 标记只是教学交互锚点。";
            details.GetArrayElementAtIndex(2).FindPropertyRelative("text").stringValue =
                "圆顶帽体与胸肩板的整体轮廓参照馆藏照片；背面、紧固件位置和各部件尺寸属于建模估算。";
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("EARLY_HELMET_CONTENT_SYNCED source=SMG-1881-47 languages=3");
        }

        [MenuItem("Museum/Early Helmet/Correct installed 1881-47 visitor-facing orientation")]
        public static void CorrectInstalledFacing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play before correcting the saved prefab.");
            var helmet = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var binding = helmet.GetComponent<RefinedHelmetBinding>();
                var inspection = helmet.GetComponent<HelmetInspectionController>();
                var model = helmet.transform.Find("Early 1839 helmet 1881-47 visual");
                var early = binding ? binding.EarlyVisual : null;
                if (!model || !inspection || early == null || !early.window ||
                    Quaternion.Angle(model.localRotation, Quaternion.Euler(-90f, 0f, 0f)) > .1f)
                    throw new InvalidOperationException("Expected the first installed, reverse-facing 1881-47 visual only.");
                model.localRotation = Quaternion.AngleAxis(180f, Vector3.up) * model.localRotation;
                var sourceFront = model.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => r.name.StartsWith("FrontPort_", StringComparison.Ordinal)).ToArray();
                if (sourceFront.Length != 10 || sourceFront.Any(r => r.gameObject.activeSelf))
                    throw new InvalidOperationException("Saved inactive source front pieces differ from the first install.");
                var door = early.window;
                foreach (Transform child in door.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                var originalHinge = inspection.WindowPivot.localPosition;
                var frontBounds = LocalBounds(sourceFront.Single(r => r.name == "FrontPort_Glass"), helmet.transform);
                early.hinge = new Vector3(frontBounds.max.x + .006f, frontBounds.center.y, frontBounds.center.z);
                inspection.WindowPivot.localPosition = early.hinge;
                foreach (var renderer in sourceFront)
                {
                    var copy = UnityEngine.Object.Instantiate(renderer.gameObject);
                    copy.name = renderer.name;
                    copy.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                    copy.transform.localScale = renderer.transform.lossyScale;
                    copy.transform.SetParent(door, true);
                    copy.SetActive(true);
                }
                var moving = door.GetComponentsInChildren<MeshRenderer>(true);
                var still = model.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => !r.name.StartsWith("FrontPort_", StringComparison.Ordinal)).Cast<Renderer>();
                early.renderers = still.Concat(moving.Cast<Renderer>()).ToArray();
                early.glass = moving.Single(r => r.name == "FrontPort_Glass");
                var trigger = LocalBounds(early.glass, inspection.WindowPivot);
                early.triggerCenter = trigger.center;
                early.triggerSize = trigger.size + Vector3.one * .012f;
                inspection.WindowPivot.localPosition = originalHinge;
                binding.ConfigureEarlyEra(early);
                PrefabUtility.SaveAsPrefabAsset(helmet, Prefab);
                Debug.Log("EARLY_HELMET_FACING_CORRECTED front=-Z renderers=" + early.renderers.Length);
            }
            finally { PrefabUtility.UnloadPrefabContents(helmet); }
            var scene = EditorSceneManager.OpenScene(Scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Material MaterialFor(string name)
        {
            var type = name.Contains("_Glass") ? "Glass" :
                name.Contains("Shadow") || name.Contains("RaisedSeat") ? "Seam" :
                name.Contains("Bar_") || name.Contains("InnerBead") || name.Contains("Fastener") ? "Bronze" :
                name.Contains("Rim") || name.Contains("Knob") ? "WornCopper" : "AgedCopper";
            var path = Root + "/Materials/Early" + type + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            EnsureFolder(Root, "Materials");
            var shader = Shader.Find(type == "Glass" ? "Museum/Inspection Glass" : "Museum/Inspection Shell");
            if (!shader) throw new InvalidOperationException("Museum inspection shader missing: " + type);
            material = new Material(shader) { name = "Early" + type, enableInstancing = true };
            Color color;
            switch (type)
            {
                case "Glass": color = new Color(.16f,.26f,.28f,.18f); break;
                case "Seam": color = new Color(.13f,.11f,.09f,1f); break;
                case "Bronze": color = new Color(.42f,.25f,.18f,1f); break;
                case "WornCopper": color = new Color(.59f,.37f,.24f,1f); break;
                default: color = new Color(.35f,.21f,.16f,1f); break;
            }
            material.SetColor("_BaseColor", color);
            if (type != "Glass")
            {
                material.SetColor("_InsideColor", color);
                material.SetFloat("_Metallic", .68f);
                material.SetFloat("_Roughness", .48f);
                material.SetFloat("_SpecularScale", .25f);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void EnsureFolder(string parent, string child)
        { if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child); }
        static Bounds LocalBounds(Renderer renderer, Transform parent)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            var matrix = parent.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(mesh.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }
        static Bounds CombinedBounds(Renderer[] renderers, Transform parent)
        {
            var b = LocalBounds(renderers[0], parent);
            foreach (var r in renderers.Skip(1)) b.Encapsulate(LocalBounds(r, parent));
            return b;
        }
    }
}
#endif
