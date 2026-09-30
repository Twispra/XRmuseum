#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Inspection.Editor
{
    /// <summary>Upgrades only this feature's saved prefab and its controls in the existing main gallery.</summary>
    public static class HelmetAdvancedInspectionSetup
    {
        const string Root = HelmetInspectionBuilder.Root;
        [MenuItem("Museum/Inspection/Upgrade main gallery inspection tools")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            if (!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save your current scene before upgrading.");
            string backup = Path.GetFullPath("../tmp/advanced-inspection-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(backup);
            File.Copy(HelmetMainIntegration.ScenePath, Path.Combine(backup, "MuseumStart.before.unity"));
            File.Copy(HelmetInspectionBuilder.PrefabPath, Path.Combine(backup, "InspectableHelmet.before.prefab"));
            var cyan = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/SectionTool.mat");
            if (!cyan)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) throw new InvalidOperationException("URP Unlit unavailable.");
                cyan = new Material(shader) { name = "SectionTool" };
                cyan.SetColor("_BaseColor", new Color(.10f, .88f, .94f));
                AssetDatabase.CreateAsset(cyan, Root + "/Materials/SectionTool.mat");
            }
            var prefab = PrefabUtility.LoadPrefabContents(HelmetInspectionBuilder.PrefabPath);
            try
            {
                var controller = prefab.GetComponent<HelmetInspectionController>();
                if (!controller) throw new InvalidOperationException("Expected inspection prefab.");
                var old = prefab.transform.Find("Section Plane Tool");
                if (!old)
                {
                    var toolObject = new GameObject("Section Plane Tool");
                    toolObject.transform.SetParent(prefab.transform, false);
                    toolObject.transform.localPosition = controller.SectionPointLocal;
                    toolObject.transform.localRotation = Quaternion.LookRotation(controller.SectionNormalLocal, Vector3.up);
                    var tool = toolObject.AddComponent<HelmetSectionInteractable>();
                    var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    knob.name = "Grip and twist"; knob.transform.SetParent(toolObject.transform, false);
                    knob.transform.localPosition = new Vector3(.28f, 0, 0); knob.transform.localScale = Vector3.one * .085f;
                    knob.GetComponent<Renderer>().sharedMaterial = cyan;
                    var collider = knob.GetComponent<SphereCollider>(); collider.isTrigger = true;
                    tool.colliders.Clear(); tool.colliders.Add(collider); tool.interactionLayers = 1;
                    var outline = new GameObject("Cut plane outline").AddComponent<LineRenderer>();
                    outline.transform.SetParent(toolObject.transform, false); outline.useWorldSpace = false;
                    outline.sharedMaterial = cyan; outline.startWidth = .0025f; outline.endWidth = .0025f;
                    outline.positionCount = 5;
                    outline.SetPositions(new[] {new Vector3(-.24f,-.27f,0),new Vector3(.24f,-.27f,0),new Vector3(.24f,.27f,0),new Vector3(-.24f,.27f,0),new Vector3(-.24f,-.27f,0)});
                    tool.Configure(controller);
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, HelmetInspectionBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var scene = EditorSceneManager.OpenScene(HelmetMainIntegration.ScenePath);
            var inspection = UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            if (!inspection || !Camera.main) throw new InvalidOperationException("Expected integrated main scene.");
            var originalCameraPosition = Camera.main.transform.position;
            var originalCameraRotation = Camera.main.transform.rotation;
            var station = GameObject.Find("Helmet Interaction Station");
            var oldControls = GameObject.Find("Helmet Inspection Controls");
            if (oldControls) UnityEngine.Object.DestroyImmediate(oldControls);
            var previous = GameObject.Find("Personal Inspection Palette");
            if (previous) UnityEngine.Object.DestroyImmediate(previous);
            var paletteRoot = new GameObject("Personal Inspection Palette");
            paletteRoot.transform.SetParent(station.transform, false);
            var palette = paletteRoot.AddComponent<HelmetInspectionPalette>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Controls.mat");
            var background = AssetDatabase.LoadAssetAtPath<Material>("Assets/Museum/Materials/Helmet Plaque.mat");
            var title = Label(paletteRoot.transform, "INSPECTION", new Vector3(0,.29f,-.025f),new Vector2(.46f,.05f),1.1f);
            Label(paletteRoot.transform,"Follows your position / stays still while aiming",new Vector3(0,.25f,-.025f),new Vector2(.46f,.028f),.48f);
            var plate=Box(paletteRoot.transform,"Palette back",Vector3.zero,new Vector3(.48f,.64f,.025f),background);
            UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());
            Button(paletteRoot.transform,inspection,"WINDOW",-.12f,.19f,HelmetInspectionControls.Action.OpenWindow,material);
            Button(paletteRoot.transform,inspection,"SECTION",.12f,.19f,HelmetInspectionControls.Action.Cutaway,material);
            Button(paletteRoot.transform,inspection,"FLOAT",-.12f,.125f,HelmetInspectionControls.Action.Float,material);
            Button(paletteRoot.transform,inspection,"RESET",.12f,.125f,HelmetInspectionControls.Action.Reset,material);
            Button(paletteRoot.transform,inspection,"TURN LEFT",-.12f,.06f,HelmetInspectionControls.Action.RotateLeft,material);
            Button(paletteRoot.transform,inspection,"TURN RIGHT",.12f,.06f,HelmetInspectionControls.Action.RotateRight,material);
            Button(paletteRoot.transform,inspection,"TILT UP",-.12f,-.005f,HelmetInspectionControls.Action.TiltUp,material);
            Button(paletteRoot.transform,inspection,"TILT DOWN",.12f,-.005f,HelmetInspectionControls.Action.TiltDown,material);
            var options=new GameObject("Section options");options.transform.SetParent(paletteRoot.transform,false);
            Button(options.transform,inspection,"EDIT CUT",-.12f,-.07f,HelmetInspectionControls.Action.EditCut,material);
            Button(options.transform,inspection,"ALIGN VIEW",.12f,-.07f,HelmetInspectionControls.Action.Align,material);
            Button(options.transform,inspection,"DEEPER",-.12f,-.135f,HelmetInspectionControls.Action.DepthIn,material);
            Button(options.transform,inspection,"SHALLOWER",.12f,-.135f,HelmetInspectionControls.Action.DepthOut,material);
            Button(options.transform,inspection,"TOP CUT",-.12f,-.20f,HelmetInspectionControls.Action.Top,material);
            Button(options.transform,inspection,"FLIP CUT",.12f,-.20f,HelmetInspectionControls.Action.Flip,material);
            var status=Label(paletteRoot.transform,"GRIP TO SELECT",new Vector3(0,-.275f,-.025f),new Vector2(.46f,.065f),.56f);
            palette.Configure(inspection,Camera.main.transform,options,status);palette.RecenterNow();
            options.SetActive(false);
            foreach(var renderer in paletteRoot.GetComponentsInChildren<Renderer>(true))
            {renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
            var instructions=GameObject.Find("Helmet Instructions")?.GetComponent<TextMeshPro>();
            if(instructions) instructions.text="PICK UP AND TURN THE HELMET\nFLOAT keeps it nearby. SECTION opens the shell.\nEDIT CUT: grip the cyan handle, move and twist.";
            if(Camera.main.transform.position!=originalCameraPosition || Camera.main.transform.rotation!=originalCameraRotation)
                throw new InvalidOperationException("Unexpected camera change.");
            if(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0))
                throw new InvalidOperationException("Missing scripts in main scene.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("ADVANCED_INSPECTION_UPGRADE_OK");
        }

        static void Button(Transform parent,HelmetInspectionController controller,string label,float x,float y,HelmetInspectionControls.Action action,Material material)
        {
            var go=Box(parent,"Inspection "+label,new Vector3(x,y,-.027f),new Vector3(.222f,.055f,.025f),material);
            var interactable=go.AddComponent<XRSimpleInteractable>();interactable.colliders.Add(go.GetComponent<Collider>());interactable.interactionLayers=1;
            go.AddComponent<HelmetInspectionControls>().Configure(controller,action);
            Label(parent,label,new Vector3(x,y,-.042f),new Vector2(.212f,.05f),.85f);
        }
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static TextMeshPro Label(Transform parent,string value,Vector3 position,Vector2 size,float font)
        {
            var t=new GameObject(value+" label").AddComponent<TextMeshPro>();t.transform.SetParent(parent,false);t.transform.localPosition=position;
            t.font=TMP_Settings.defaultFontAsset;t.text=value;t.color=Color.white;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=size;
            t.enableWordWrapping=false;t.enableAutoSizing=true;t.fontSizeMin=.02f;t.fontSizeMax=font;t.ForceMeshUpdate(true);return t;
        }
    }
}
#endif
