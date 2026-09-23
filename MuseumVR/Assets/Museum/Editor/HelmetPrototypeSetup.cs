using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Editor
{
    /// <summary>Rebuilds only the helmet prefab and its station in the existing gallery.</summary>
    public static class HelmetPrototypeSetup
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string ModelPath = "Assets/Museum/Models/Helmet/helmet-graybox-v02.fbx";
        const string PrefabPath = "Assets/Museum/Prefabs/HelmetExhibit.prefab";
        static string RepoRoot => Directory.GetParent(Application.dataPath).Parent.FullName;

        [MenuItem("Museum/Build Helmet Interaction Prototype")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.Combine(RepoRoot, "artifacts/helmet-interaction/qa"));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Helmet FBX failed to import.");
            var clay = Material("Helmet Clay", new Color(.38f, .48f, .49f), .08f, .25f);
            var trim = Material("Helmet Trim", new Color(.58f, .66f, .65f), .18f, .35f);
            var recess = Material("Helmet Recess", new Color(.055f, .085f, .095f), .05f, .25f);
            var accent = Material("Helmet Reset Brass", new Color(.67f, .40f, .14f), .6f, .4f);
            var plaqueMat = Material("Helmet Plaque", new Color(.018f, .048f, .055f), .1f, .25f);
            var friction = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/Museum/Materials/Helmet Contact.physicMaterial");
            if (friction == null)
            {
                friction = new PhysicMaterial("Helmet Contact");
                AssetDatabase.CreateAsset(friction, "Assets/Museum/Materials/Helmet Contact.physicMaterial");
            }
            friction.dynamicFriction = .55f;
            friction.staticFriction = .65f;
            friction.bounciness = 0f;
            friction.frictionCombine = PhysicMaterialCombine.Average;
            friction.bounceCombine = PhysicMaterialCombine.Minimum;
            EditorUtility.SetDirty(friction);

            var root = new GameObject("HelmetExhibit");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Helmet Visual";
            visual.transform.SetParent(root.transform, false);
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
            {
                string n = renderer.name;
                renderer.sharedMaterial = n.Contains("Glass") || n.Contains("Fastener") || n.Contains("Connector")
                    ? recess : n.Contains("Rim") || n.Contains("Guard") || n.Contains("Ring") || n.Contains("Edge") ? trim : clay;
            }
            var bonnet = FindRenderer(visual, "GB_Bonnet_Hollow");
            var front = FindRenderer(visual, "GB_Front_PortRim");
            if (front.bounds.center.z > bonnet.bounds.center.z)
                visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var envelope = BoundsOf(visual);
            visual.transform.localPosition -= Vector3.up * envelope.min.y;
            envelope = BoundsOf(visual);
            if (Mathf.Abs(envelope.size.x - .3175f) > .002f ||
                Mathf.Abs(envelope.size.y - .4953f) > .002f ||
                Mathf.Abs(envelope.size.z - .4191f) > .002f)
                throw new InvalidOperationException("Unexpected imported helmet dimensions: " + envelope.size);

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1.5f; // Interaction tuning, not a claim about the historical helmet's mass.
            body.drag = .08f;
            body.angularDrag = .7f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            body.maxAngularVelocity = 12f;
            var colliders = new GameObject("Compound Colliders");
            colliders.transform.SetParent(root.transform, false);
            var head = bonnet.bounds;
            var headGo = new GameObject("Bonnet Capsule");
            headGo.transform.SetParent(colliders.transform, false);
            var capsule = headGo.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = head.center;
            capsule.radius = Mathf.Max(head.size.x, head.size.z) * .48f;
            capsule.height = head.size.y;
            capsule.sharedMaterial = friction;
            AddBox(colliders.transform, "Breastplate Base", new Vector3(0f, .025f, envelope.center.z),
                new Vector3(.29f, .05f, .39f), friction);
            AddBox(colliders.transform, "Neck Support", new Vector3(0f, .10f, .01f),
                new Vector3(.24f, .12f, .24f), friction);
            AddBox(colliders.transform, "Front Window", front.bounds.center,
                new Vector3(front.bounds.size.x, front.bounds.size.y, Mathf.Max(.028f, front.bounds.size.z)), friction);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.interactionLayers = 1;
            grab.selectMode = InteractableSelectMode.Single;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.useDynamicAttach = true;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            grab.snapToColliderVolume = true;
            grab.attachEaseInTime = .12f;
            grab.trackScale = false;
            grab.throwOnDetach = true;
            grab.throwVelocityScale = 1f;
            grab.throwAngularVelocityScale = 1f;
            grab.retainTransformParent = false;
            grab.colliders.Clear();
            grab.colliders.AddRange(root.GetComponentsInChildren<Collider>());
            var reset = root.AddComponent<HelmetExhibitReset>();
            var resetData = new SerializedObject(reset);
            resetData.FindProperty("hasConfiguredHome").boolValue = false;
            resetData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            // The original grab-test prefab remains available as a reference asset.
            var oldCube = GameObject.Find("Grab Test Object");
            if (oldCube != null) UnityEngine.Object.DestroyImmediate(oldCube);
            var oldStation = GameObject.Find("Helmet Interaction Station");
            if (oldStation != null) UnityEngine.Object.DestroyImmediate(oldStation);
            // Grabbing unparents the helmet at runtime; remove a prior edit-time instance if needed.
            foreach (var previous in UnityEngine.Object.FindObjectsOfType<HelmetExhibitReset>())
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var station = new GameObject("Helmet Interaction Station");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(station.transform, false);
            var display = UnityEngine.Object.FindObjectsOfType<BoxCollider>()
                .Where(c => c.name == "Display Top")
                .OrderBy(c => Mathf.Abs(c.transform.position.x)).First();
            float tableY = display.bounds.max.y;
            instance.transform.position = new Vector3(0f, tableY + .012f, 3.5f);
            reset = instance.GetComponent<HelmetExhibitReset>();
            reset.ConfigureHomePose(instance.transform.position, instance.transform.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(reset);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

            var resetButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
            resetButton.name = "Helmet Reset Button";
            resetButton.transform.SetParent(station.transform, false);
            resetButton.transform.position = new Vector3(.52f, tableY + .055f, 3.02f);
            resetButton.transform.localScale = new Vector3(.22f, .09f, .14f);
            resetButton.GetComponent<MeshRenderer>().sharedMaterial = accent;
            var interactable = resetButton.AddComponent<XRSimpleInteractable>();
            interactable.interactionLayers = 1;
            interactable.colliders.Add(resetButton.GetComponent<Collider>());
            UnityEventTools.AddVoidPersistentListener(interactable.selectEntered, reset.ResetToDisplay);

            Text(station.transform, "Reset Button Label", "RESET", new Vector3(.52f, tableY+.059f, 2.944f),
                new Vector2(.21f,.075f), 1.6f, Color.white);
            var plaque = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plaque.name = "Helmet Instructions Backing";
            plaque.transform.SetParent(station.transform, false);
            plaque.transform.position = new Vector3(0f, 1.005f, 2.752f);
            plaque.transform.localScale = new Vector3(1.18f, .21f, .02f);
            plaque.GetComponent<MeshRenderer>().sharedMaterial = plaqueMat;
            UnityEngine.Object.DestroyImmediate(plaque.GetComponent<Collider>());
            Text(station.transform, "Helmet Instructions", "DIVING HELMET  /  SCALE PROTOTYPE\nGrip to pick up. Release to place.\nPoint + grip RESET to return to display.",
                new Vector3(0f,1.005f,2.738f),new Vector2(1.12f,.19f),1.5f,new Color(.90f,.94f,.93f));

            // Start within comfortable viewing distance; movement/teleport remains available.
            var rig = GameObject.Find("XR Origin (XR Rig)");
            if (rig != null)
            {
                rig.transform.position = new Vector3(0f,0f,2.2f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rig.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            var report = new SetupReport {
                model = ModelPath, prefab = PrefabPath, scene = ScenePath,
                sizeMetres = envelope.size, colliderCount = instance.GetComponentsInChildren<Collider>().Length,
                rendererCount = instance.GetComponentsInChildren<MeshRenderer>().Length,
                triangles = instance.GetComponentsInChildren<MeshFilter>().Sum(m => (long)m.sharedMesh.triangles.Length/3),
                displayTopY = tableY, homePosition = instance.transform.position,
                missingScripts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))
            };
            if (report.missingScripts != 0) throw new InvalidOperationException("Missing scripts in scene.");
            File.WriteAllText(Path.Combine(RepoRoot,"artifacts/helmet-interaction/qa/setup.json"),JsonUtility.ToJson(report,true));
            Debug.Log("HELMET_SETUP_OK " + JsonUtility.ToJson(report));
        }

        static MeshRenderer FindRenderer(GameObject root, string name) => root.GetComponentsInChildren<MeshRenderer>().First(r => r.name == name);
        static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        static void AddBox(Transform parent, string name, Vector3 center, Vector3 size, PhysicMaterial material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent,false);
            var collider = child.AddComponent<BoxCollider>();
            collider.center = center; collider.size = size; collider.sharedMaterial = material;
        }
        static Material Material(string name, Color color, float metallic, float smoothness)
        {
            string path = "Assets/Museum/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat,path);
            }
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Smoothness",smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }
        static void Text(Transform parent, string name, string content, Vector3 position, Vector2 size, float fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent,false);
            go.transform.position = position;
            var text = go.AddComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content; text.fontSize = fontSize; text.color = color;
            text.alignment = TextAlignmentOptions.Center; text.enableWordWrapping = false;
            text.rectTransform.sizeDelta = size;
            FitLabel(text, fontSize);
        }
        static void FitLabel(TMP_Text text, float maximum)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = .03f;
            text.fontSizeMax = maximum;
            text.ForceMeshUpdate(true);
        }
        public static void FitLabelsAndPreview()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
            {
                if (text.name != "Reset Button Label" && text.name != "Helmet Instructions") continue;
                FitLabel(text, text.name == "Reset Button Label" ? 1.6f : 1.5f);
                EditorUtility.SetDirty(text);
                Debug.Log("HELMET_LABEL " + text.name + " size=" + text.fontSize + " bounds=" + text.textBounds.size);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            HelmetPrototypePreview.Capture();
        }
        [Serializable] sealed class SetupReport
        {
            public string model, prefab, scene;
            public Vector3 sizeMetres, homePosition;
            public int colliderCount, rendererCount, missingScripts;
            public long triangles;
            public float displayTopY;
        }
    }
}
