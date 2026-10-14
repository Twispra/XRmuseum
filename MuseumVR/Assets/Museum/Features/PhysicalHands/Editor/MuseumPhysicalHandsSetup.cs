#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Museum.Inspection;
using Museum.PhysicalHands.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhysicalHands.EditorTools
{
    /// <summary>Scoped hand installation. Existing helmet/model generators are never invoked.</summary>
    public static class MuseumPhysicalHandsSetup
    {
        const string Root = "Assets/Museum/Features/PhysicalHands";
        const string Main = "Assets/Museum/Scenes/MuseumStart.unity";
        const string Helmet = "Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab";
        const string LeftPath = Root + "/Prefabs/LeftMuseumHand.prefab", RightPath = Root + "/Prefabs/RightMuseumHand.prefab";
        public const string Lab = Root + "/Scenes/PhysicalHandsLab.unity";
        public static void BuildAndIntegrateBatch()
        {
            try { Install(); EditorApplication.delayCall += () => EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        [MenuItem("Museum/Physical Hands/Install Saved Hand Module")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install from Edit Mode.");
            string diskRoot = Path.Combine(Application.dataPath, "Museum/Features/PhysicalHands");
            Directory.CreateDirectory(Path.Combine(diskRoot,"Materials")); Directory.CreateDirectory(Path.Combine(diskRoot,"Prefabs")); Directory.CreateDirectory(Path.Combine(diskRoot,"Scenes"));
            AssetDatabase.Refresh();
            var glove = Material("Museum Glove", new Color(.72f, .79f, .82f), .25f);
            var cuff = Material("Museum Navy Cuff", new Color(.035f, .105f, .17f), .18f);
            var left = HandPrefab(true, glove, cuff); var right = HandPrefab(false, glove, cuff);
            ConfigureHelmet();
            var scene = EditorSceneManager.OpenScene(Main, OpenSceneMode.Single);
            // Opening/unloading prefab contents may unload unused native assets; reacquire after scene switches.
            left = AssetDatabase.LoadAssetAtPath<GameObject>(LeftPath).GetComponent<MuseumPhysicalHand>();
            right = AssetDatabase.LoadAssetAtPath<GameObject>(RightPath).GetComponent<MuseumPhysicalHand>();
            InstallRig(scene, left, right);
            EditorSceneManager.SaveScene(scene);
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Lab))
            {
                // The lab is a separate saved copy of the current integration, with simple collision fixtures.
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Palm Collision Test Wall";
                wall.transform.position = new Vector3(-1.5f, 1.2f, 2.3f); wall.transform.localScale = new Vector3(.10f, 1.6f, 1.8f);
                wall.GetComponent<Renderer>().sharedMaterial = cuff;
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.name = "Physical Hands Grab Test Block";
                block.transform.position = new Vector3(1.5f, 1f, 2.3f); block.transform.localScale = Vector3.one * .15f;
                block.GetComponent<Renderer>().sharedMaterial = glove;
                var rb = block.AddComponent<Rigidbody>(); rb.mass = .25f; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var grab = block.AddComponent<XRGrabInteractable>(); ConfigureGrab(grab);
                EditorSceneManager.SaveScene(scene, Lab);
            }
            else
            {
                var labScene = EditorSceneManager.OpenScene(Lab, OpenSceneMode.Single);
                left = AssetDatabase.LoadAssetAtPath<GameObject>(LeftPath).GetComponent<MuseumPhysicalHand>();
                right = AssetDatabase.LoadAssetAtPath<GameObject>(RightPath).GetComponent<MuseumPhysicalHand>();
                InstallRig(labScene, left, right);
                EditorSceneManager.SaveScene(labScene);
            }
            AssetDatabase.SaveAssets();
            string output = Environment.GetEnvironmentVariable("MUSEUM_QA_OUTPUT");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output); CaptureVisuals(left, right, output);
                File.WriteAllText(Path.Combine(output, "installation.txt"), "Installed saved left/right palm prefabs, opt-in collaboration and both main/lab scenes. Unity " + Application.unityVersion + ". Static captures are artwork QA, not headset evidence.");
            }
            Debug.Log("MUSEUM_PHYSICAL_HANDS_INSTALL_PASSED");
        }
        static Material Material(string name, Color color, float smoothness)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static MuseumPhysicalHand HandPrefab(bool left, Material glove, Material cuff)
        {
            string path = Root + "/Prefabs/" + (left ? "LeftMuseumHand" : "RightMuseumHand") + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing.GetComponent<MuseumPhysicalHand>();
            var root = new GameObject(left ? "Left Museum Hand" : "Right Museum Hand");
            try
            {
                var physical = root.AddComponent<MuseumPhysicalHand>();
                var visual = MuseumHandVisualFactory.Create(root.transform, left, glove, cuff);
                physical.Configure(null, null, null, visual, left);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<MuseumPhysicalHand>();
        }
        static void ConfigureHelmet()
        {
            var root = PrefabUtility.LoadPrefabContents(Helmet);
            try
            {
                ConfigureGrab(root.GetComponent<XRGrabInteractable>());
                root.GetComponent<HelmetInspectionController>().ConfigureHandCollaboration(true);
                PrefabUtility.SaveAsPrefabAsset(root, Helmet);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void ConfigureGrab(XRGrabInteractable grab)
        {
            var transformer = grab.GetComponent<MuseumHandGrabTransformer>();
            if (!transformer) transformer = grab.gameObject.AddComponent<MuseumHandGrabTransformer>();
            grab.selectMode = InteractableSelectMode.Multiple; grab.trackScale = false;
            grab.addDefaultGrabTransformers = false;
            grab.startingSingleGrabTransformers.Clear(); grab.startingSingleGrabTransformers.Add(transformer);
            grab.startingMultipleGrabTransformers.Clear(); grab.startingMultipleGrabTransformers.Add(transformer);
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.useDynamicAttach = true; grab.matchAttachPosition = true; grab.matchAttachRotation = true;
            grab.reinitializeDynamicAttachEverySingleGrab = true;
        }
        static void InstallRig(Scene scene, MuseumPhysicalHand left, MuseumPhysicalHand right)
        {
            var controllers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ActionBasedController>(true)).ToArray();
            var leftInput = controllers.Single(c => c.name == "Left Controller");
            var rightInput = controllers.Single(c => c.name == "Right Controller");
            var rig = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MuseumPhysicalHandsRig>(true)).FirstOrDefault();
            if (!rig) rig = new GameObject("Museum Physical Hands").AddComponent<MuseumPhysicalHandsRig>();
            rig.Configure(leftInput, rightInput, left, right); EditorUtility.SetDirty(rig);
        }
        static void CaptureVisuals(MuseumPhysicalHand left, MuseumPhysicalHand right, string output)
        {
            left = AssetDatabase.LoadAssetAtPath<GameObject>(LeftPath).GetComponent<MuseumPhysicalHand>();
            right = AssetDatabase.LoadAssetAtPath<GameObject>(RightPath).GetComponent<MuseumPhysicalHand>();
            var root = new GameObject("Temporary Hand Artwork QA");
            var cameraObject = new GameObject("Temporary Hand Artwork Camera");
            var lightObject = new GameObject("Temporary Hand Artwork Light");
            var texture = new RenderTexture(1200, 800, 24);
            var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                root.transform.position = new Vector3(100, 100, 100);
                for (int row = 0; row < 2; row++) for (int side = 0; side < 2; side++)
                {
                    var hand = UnityEngine.Object.Instantiate(side == 0 ? left : right, root.transform);
                    hand.transform.localPosition = new Vector3(side == 0 ? -.11f : .11f, 0, row == 0 ? .16f : -.16f);
                    hand.GetComponentInChildren<MuseumHandVisual>().ApplyPoseImmediate(row, 0, row == 1);
                }
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = .28f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f,.07f,.09f); camera.targetTexture = texture;
                camera.transform.position = root.transform.position + new Vector3(0,.7f,-.10f); camera.transform.LookAt(root.transform.position + new Vector3(0,0,.035f));
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f;
                light.transform.rotation = Quaternion.Euler(55,-25,0); light.shadows = LightShadows.None;
                camera.Render(); RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,1200,800),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output,"gloves-open-and-held.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }
    }
}
#endif
