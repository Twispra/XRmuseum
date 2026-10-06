using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Hotspots.Editor
{
    public static class HelmetHotspotBuilder
    {
        const string RootPath = "Assets/Museum/Features/HelmetHotspots";
        const string PrefabPath = RootPath + "/Prefabs/HelmetHotspotTour.prefab";
        const string MarkerMaterialPath = RootPath + "/Materials/HotspotMarker.mat";
        const string PanelMaterialPath = RootPath + "/Materials/HotspotPanel.mat";
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";

        [MenuItem("Museum/Hotspots/Create standalone helmet hotspots (first time only)")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("Helmet hotspot prefab already exists.");

            EnsureFolder(RootPath, "Materials");

            var markerMaterial = GetMaterial(MarkerMaterialPath, new Color(0.12f, 0.8f, 0.9f, 0.75f));
            var panelMaterial = GetMaterial(PanelMaterialPath, new Color(0.015f, 0.045f, 0.055f, 1f));
            var root = new GameObject("Helmet Hotspot Tour");

            try
            {
                var tour = root.AddComponent<HelmetHotspotTour>();
                var panel = CreatePanel(root.transform, panelMaterial, out var information, out var progress, out var completion);
                panel.transform.localPosition = new Vector3(0.43f, 0.29f, -0.08f);
                panel.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);

                tour.Configure(3, information, progress, completion);
                tour.ResetTour();

                CreateHotspot(root.transform, tour, markerMaterial, "front-window", "Front window", "Inspect the front viewing area and its surrounding rim.", new Vector3(0f, 0.3f, -0.18f));
                CreateHotspot(root.transform, tour, markerMaterial, "bonnet", "Bonnet shell", "Inspect the rounded upper shell and its overall form.", new Vector3(0.17f, 0.34f, 0f));
                CreateHotspot(root.transform, tour, markerMaterial, "breastplate", "Breastplate", "Inspect the broad lower section around the shoulders.", new Vector3(0f, 0.075f, -0.18f));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Museum/Hotspots/Integrate into main scene")]
        public static void IntegrateIntoMainScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Helmet hotspot prefab is missing.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var helmet = GameObject.Find("HelmetExhibit");
            if (helmet == null)
                throw new InvalidOperationException("HelmetExhibit is missing from MuseumStart.");
            if (helmet.transform.Find("HelmetHotspotTour") != null || helmet.transform.Find("Helmet Hotspot Tour") != null)
                return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(helmet.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static GameObject CreatePanel(Transform parent, Material material, out TextMeshPro information, out TextMeshPro progress, out GameObject completion)
        {
            var panel = new GameObject("Information Panel");
            panel.transform.SetParent(parent, false);

            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "Panel Backing";
            backing.transform.SetParent(panel.transform, false);
            backing.transform.localScale = new Vector3(0.4f, 0.25f, 0.015f);
            backing.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(backing.GetComponent<Collider>());

            information = CreateText(panel.transform, "Information", new Vector3(0f, 0.035f, -0.011f), new Vector2(0.35f, 0.14f), 1.2f, TextAlignmentOptions.TopLeft);
            progress = CreateText(panel.transform, "Progress", new Vector3(0f, -0.078f, -0.011f), new Vector2(0.35f, 0.045f), 0.8f, TextAlignmentOptions.Center);
            var completionText = CreateText(panel.transform, "Complete", new Vector3(0f, -0.105f, -0.012f), new Vector2(0.35f, 0.04f), 0.75f, TextAlignmentOptions.Center);
            completionText.text = "Exploration complete";
            completionText.color = new Color(0.3f, 1f, 0.55f, 1f);
            completion = completionText.gameObject;
            completion.SetActive(false);
            return panel;
        }

        static TextMeshPro CreateText(Transform parent, string name, Vector3 position, Vector2 size, float maximumSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            var text = textObject.AddComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.color = new Color(0.92f, 0.96f, 0.95f, 1f);
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.03f;
            text.fontSizeMax = maximumSize;
            text.rectTransform.sizeDelta = size;
            return text;
        }

        static void CreateHotspot(Transform parent, HelmetHotspotTour tour, Material material, string identifier, string title, string description, Vector3 position)
        {
            var hotspotObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hotspotObject.name = title + " Hotspot";
            hotspotObject.transform.SetParent(parent, false);
            hotspotObject.transform.localPosition = position;
            hotspotObject.transform.localScale = Vector3.one * 0.065f;

            var collider = hotspotObject.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            var renderer = hotspotObject.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            var interactable = hotspotObject.AddComponent<XRSimpleInteractable>();
            interactable.interactionLayers = 1;
            interactable.colliders.Clear();
            interactable.colliders.Add(collider);

            var hotspot = hotspotObject.AddComponent<HelmetHotspot>();
            hotspot.Configure(tour, identifier, title, description, renderer);
        }

        static Material GetMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    throw new InvalidOperationException("URP Unlit shader is unavailable.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
