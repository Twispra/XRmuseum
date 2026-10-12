#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Museum.Refined;
using Museum.Hotspots;
using Museum.Inspection;
using Museum.Localization;
using Museum.Localization.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-time PR #44 repair in the isolated checkout; does not generate any scene or model.
public static class Pr44SavedAssetRepair
{
    const string PrefabPath = "Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab";
    const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
    [Serializable] sealed class AttachmentRecord { public string identifier, surface; public Vector3 surfaceLocal; }
    [Serializable] sealed class Report { public string unity, output, caption, prefabGuid, sceneGuid; public AttachmentRecord[] attachments; public bool completed; }
    public static void RunBatch()
    {
        string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        string output = Path.Combine(repo, "tmp/pr44-asset-repair", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            if (Directory.Exists(output)) throw new InvalidOperationException("Preserve earlier repair evidence.");
            Directory.CreateDirectory(output);
            foreach (string asset in new[] { PrefabPath, ScenePath, "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset" })
                File.Copy(Path.Combine(repo, "MuseumVR", asset), Path.Combine(output, Path.GetFileName(asset)));
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            string required = File.ReadAllText(Path.Combine(Application.dataPath, "Museum/Features/Localization/Fonts/characters-required.txt")).TrimEnd();
            if (required.Any(c => !table.FontAsset.HasCharacter(c))) TrilingualFontBaker.RunBatch();
            TrilingualMuseumSetup.ApplyTable(table, table.FontAsset);
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (prefab.GetComponent<HelmetEraVisitorContent>()) throw new InvalidOperationException("Visitor content is already installed; preserve subsequent edits.");
                var binding = prefab.GetComponent<RefinedHelmetBinding>();
                var visual = binding.AdditionalVisual;
                if (visual == null || visual.eraIndex != 2 || visual.telephone.Length != 0) throw new InvalidOperationException("Reviewed SuperLite and empty phone surfaces required.");
                prefab.AddComponent<HelmetEraVisitorContent>().Configure(binding, null, null, Array.Empty<HelmetEraVisitorContent.HotspotAttachment>());
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var content = UnityEngine.Object.FindObjectOfType<HelmetEraVisitorContent>();
            var caption = GameObject.Find("Helmet Instructions").GetComponent<TMP_Text>();
            if (!caption.gameObject.activeInHierarchy || !caption.GetComponent<LocalizedSourceText>()) throw new InvalidOperationException("Saved localized exhibit caption required.");
            var sceneBinding = content.GetComponent<RefinedHelmetBinding>();
            var sceneVisual = sceneBinding.AdditionalVisual;
            var tour = content.GetComponentInChildren<HelmetHotspotTour>(true);
            if (!tour) throw new InvalidOperationException("Saved scene hotspot instance required.");
            var inspection = content.GetComponent<HelmetInspectionController>();
            Vector3 previousHinge = inspection.WindowPivot.localPosition;
            inspection.WindowPivot.localPosition = sceneVisual.hinge;
            var neck = sceneVisual.renderers.Single(r => r.name == "NeckDam_Neoprene_510-533");
            var surfaces = new[] { sceneVisual.glass, sceneVisual.shell, neck };
            var ids = new[] { "front-window", "bonnet", "breastplate" };
            var headings = new[] { HelmetEraVisitorContent.FrontWindowTitle, HelmetEraVisitorContent.ShellTitle, HelmetEraVisitorContent.NeckTitle };
            var details = new[] { HelmetEraVisitorContent.FrontWindowDetails, HelmetEraVisitorContent.ShellDetails, HelmetEraVisitorContent.NeckDetails };
            var attachments = new HelmetEraVisitorContent.HotspotAttachment[3];
            var records = new AttachmentRecord[3];
            for (int i = 0; i < 3; i++)
            {
                var hotspot = tour.GetComponentsInChildren<HelmetHotspot>(true).Single(h => h.Identifier == ids[i]);
                var bounds = surfaces[i].bounds;
                Vector3 target = i == 1 ? new Vector3(bounds.max.x, bounds.max.y - .05f, bounds.center.z) : new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
                var vertices = surfaces[i].GetComponent<MeshFilter>().sharedMesh.vertices;
                Vector3 point = vertices.OrderBy(v => (surfaces[i].transform.TransformPoint(v) - target).sqrMagnitude).First();
                attachments[i] = new HelmetEraVisitorContent.HotspotAttachment { hotspot = hotspot, surface = surfaces[i], localPoint = point, heading = headings[i], details = details[i] };
                records[i] = new AttachmentRecord { identifier = ids[i], surface = surfaces[i].name, surfaceLocal = point };
            }
            inspection.WindowPivot.localPosition = previousHinge;
            content.Configure(sceneBinding, tour, caption, attachments);
            PrefabUtility.RecordPrefabInstancePropertyModifications(content);
            EditorUtility.SetDirty(content);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report { unity = Application.unityVersion, output = output, caption = caption.name,
                prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath), sceneGuid = AssetDatabase.AssetPathToGUID(ScenePath), attachments = records, completed = true }, true));
            Debug.Log("PR44_SAVED_ASSET_REPAIR " + output); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
#endif
