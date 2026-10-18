#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.PhotoHelmetTrial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Museum.PhotoHelmetTrial.EditorTools
{
    /// <summary>Isolated, repeatable visual comparison; never edits MuseumStart or its prefabs.</summary>
    public static class PhotoHelmetComparisonSetup
    {
        private const string Root = "Assets/Museum/Features/PhotoHelmetTrial";
        private const string CurrentPrefab = "Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab";
        private const string PhotoDetails = Root + "/Models/photo-helmet-refined-v2-details.obj";
        private const string PhotoShell = Root + "/Models/photo-helmet-refined-v2-shell.obj";
        private const string PhotoPrefabPath = Root + "/Prefabs/PhotoHelmetStudy.prefab";
        private const string ScenePath = Root + "/Scenes/PhotoHelmetComparison.unity";
        private const float DisplayHeight = 0.45f;

        [MenuItem("Museum/Photo helmet/Build comparison scene")]
        public static void Build()
        {
            var currentAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CurrentPrefab);
            var importer = AssetImporter.GetAtPath(PhotoShell) as ModelImporter;
            if (importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var detailsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PhotoDetails);
            var shellAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PhotoShell);
            if (!currentAsset || !detailsAsset || !shellAsset)
                throw new InvalidOperationException("Current Mark V prefab or separated photo OBJ assets are missing.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var currentRoot = new GameObject("Current integrated Mark V");
            var photoRoot = new GameObject("Photo study V2 - section preview");

            var currentInstance = (GameObject)PrefabUtility.InstantiatePrefab(currentAsset);
            var markV = currentInstance.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Refined Mark V Visual");
            if (!markV)
                throw new InvalidOperationException("Saved current prefab has no Refined Mark V Visual.");
            var currentVisual = UnityEngine.Object.Instantiate(markV.gameObject, currentRoot.transform, false);
            currentVisual.name = "Saved Mark V visual";
            var closedWindow = currentVisual.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "FrontWindowPivot");
            if (closedWindow) closedWindow.gameObject.SetActive(true);
            UnityEngine.Object.DestroyImmediate(currentInstance);

            var details = (GameObject)PrefabUtility.InstantiatePrefab(detailsAsset);
            details.transform.SetParent(photoRoot.transform, false);
            details.name = "Photo details OBJ";
            var shellVisual = (GameObject)PrefabUtility.InstantiatePrefab(shellAsset);
            shellVisual.transform.SetParent(photoRoot.transform, false);
            shellVisual.name = "Photo hollow shell OBJ";
            var bindings = ApplyPhotoMaterials(photoRoot);
            var shell = shellVisual.GetComponentsInChildren<MeshFilter>(true)
                .SingleOrDefault(f => f.sharedMesh);
            if (!shell || !shell.sharedMesh || !shell.sharedMesh.isReadable)
                throw new InvalidOperationException("The photo study has no readable hollow shell for a structural section preview.");
            var cap = AssetDatabase.LoadAssetAtPath<Material>("Assets/Museum/Features/HelmetExperience/Materials/SectionFace.mat");
            if (!cap) throw new InvalidOperationException("The existing SectionFace material is missing.");
            photoRoot.AddComponent<PhotoHelmetSectionPreview>()
                .Configure(bindings, shell, cap);

            float markHeight = NormalizeToHeight(currentRoot.transform, DisplayHeight);
            float photoHeight = NormalizeToHeight(photoRoot.transform, DisplayHeight);
            currentRoot.transform.position += new Vector3(-0.40f, 0, 0);
            photoRoot.transform.rotation = Quaternion.Euler(0, 180, 0);

            Directory.CreateDirectory(Root + "/Prefabs");
            var prefab = PrefabUtility.SaveAsPrefabAsset(photoRoot, PhotoPrefabPath);
            if (!prefab) throw new InvalidOperationException("Could not save photo helmet prefab.");
            photoRoot.transform.position += new Vector3(0.40f, 0, 0);

            var lightObject = new GameObject("Comparison directional light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(40, -30, 0);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            var fillObject = new GameObject("Comparison front fill");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 2.5f;
            fill.range = 3f;
            fillObject.transform.position = new Vector3(0, 0.65f, -0.9f);

            var cameraObject = new GameObject("Comparison camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.20f);
            camera.fieldOfView = 37;
            cameraObject.transform.position = new Vector3(0, 0.30f, -1.75f);
            cameraObject.transform.LookAt(new Vector3(0, 0.22f, 0));
            cameraObject.AddComponent<PhotoHelmetComparisonControls>()
                .Configure(currentRoot.transform, photoRoot.transform, photoRoot.GetComponent<PhotoHelmetSectionPreview>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = photoRoot;
            Debug.Log($"PHOTO_HELMET_COMPARE scene={ScenePath} currentRawHeight={markHeight:F4}m photoRawHeight={photoHeight:F4}m targetHeight={DisplayHeight:F2}m; photo-shell section preview is available, but no hinge or product interaction is claimed.");
        }

        private static float NormalizeToHeight(Transform root, float targetHeight)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException(root.name + " has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float rawHeight = bounds.size.y;
            if (rawHeight < 0.01f || rawHeight > 10f)
                throw new InvalidOperationException(root.name + " has unexpected raw height " + rawHeight);
            root.localScale = Vector3.one * (targetHeight / rawHeight);
            var scaledRenderers = root.GetComponentsInChildren<Renderer>(true);
            var scaledBounds = scaledRenderers[0].bounds;
            foreach (var renderer in scaledRenderers.Skip(1)) scaledBounds.Encapsulate(renderer.bounds);
            root.position = new Vector3(0, -scaledBounds.min.y, 0);
            return rawHeight;
        }

        private static PhotoHelmetSectionPreview.SurfaceBinding[] ApplyPhotoMaterials(GameObject root)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var cutShader = Shader.Find("Museum/Inspection Shell");
            if (!shader || !cutShader) throw new InvalidOperationException("A display or section shader is missing.");
            var materials = new Dictionary<string, Material>();
            var cutVariants = new Dictionary<string, Material>();
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.sharedMaterials.Length > 0).ToArray();
            var bindings = new PhotoHelmetSectionPreview.SurfaceBinding[renderers.Length];
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                var source = renderer.sharedMaterials;
                var display = new Material[source.Length];
                var cut = new Material[source.Length];
                for (int slot = 0; slot < source.Length; slot++)
                {
                    string name = source[slot] ? source[slot].name : "Unknown";
                    if (!materials.TryGetValue(name, out var material))
                    {
                        string path = Root + "/Materials/" + name + ".mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material)
                        {
                            material = new Material(shader) { name = name };
                            AssetDatabase.CreateAsset(material, path);
                        }
                        ConfigureMaterial(material, name);
                        materials.Add(name, material);
                    }
                    if (!cutVariants.TryGetValue(name, out var cutMaterial))
                    {
                        string path = Root + "/Materials/Cut_" + name + ".mat";
                        cutMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!cutMaterial)
                        {
                            cutMaterial = new Material(cutShader) { name = "Cut_" + name };
                            AssetDatabase.CreateAsset(cutMaterial, path);
                        }
                        ConfigureMaterial(cutMaterial, name);
                        cutVariants.Add(name, cutMaterial);
                    }
                    display[slot] = material;
                    cut[slot] = cutMaterial;
                }
                renderer.sharedMaterials = display;
                bindings[rendererIndex] = new PhotoHelmetSectionPreview.SurfaceBinding
                    { surface = renderer, display = display, cut = cut };
            }
            AssetDatabase.SaveAssets();
            return bindings;
        }

        private static void ConfigureMaterial(Material material, string name)
        {
            Color color = name switch
            {
                "V2_DarkGlass_VisualAssumption" => new Color(0.024f, 0.039f, 0.043f),
                "V2_WornBrass_Highlight" => new Color(0.522f, 0.404f, 0.239f),
                "V2_RubberGasket_VisualAssumption" => new Color(0.071f, 0.078f, 0.071f),
                "V2_AgedBronze_Edge" => new Color(0.176f, 0.125f, 0.078f),
                "PolishedEdge_Interpretive" => new Color(0.494f, 0.369f, 0.208f),
                "V2_Recess_VisualAssumption" => new Color(0.039f, 0.051f, 0.047f),
                "V2_FrontWindowWornMetal_VisualAssumption" => new Color(0.392f, 0.369f, 0.306f),
                "Gasket_Interpretive" => new Color(0.024f, 0.027f, 0.024f),
                "Glass_VisualAssumption" => new Color(0.275f, 0.263f, 0.212f),
                _ => Color.white
            };
            string textureName = name == "CastBrass_Interpretive" ? "CastBrass_Interpretive" :
                                 name == "WeatheredShell_Interpretive" ? "WeatheredShell_Interpretive" : null;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (textureName != null)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Models/" + textureName + ".png");
                if (!texture) throw new InvalidOperationException("Missing photo texture " + textureName);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            }
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", name.Contains("Glass") || name.Contains("Gasket") ? 0f : 0.15f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", name.Contains("Glass") ? 0.75f : 0.25f);
            if (material.HasProperty("_Roughness")) material.SetFloat("_Roughness", name.Contains("Glass") ? 0.25f : 0.75f);
            if (material.HasProperty("_TextureStrength")) material.SetFloat("_TextureStrength", textureName != null ? 1f : 0f);
        }
    }
}
#endif
