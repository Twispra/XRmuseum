#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Museum.Inspection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

namespace Museum.Inspection.Editor
{
    /// <summary>First-time builder for isolated outputs. Refuses to replace saved work.</summary>
    public static class HelmetInspectionBuilder
    {
        public const string Root = "Assets/Museum/Features/HelmetInspection";
        public const string ScenePath = Root+"/Scenes/HelmetInspectionDemo.unity";
        public const string PrefabPath = Root+"/Prefabs/InspectableHelmet.prefab";
        [MenuItem("Museum/Inspection/Open saved demo")]
        public static void OpenSavedDemo()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            var gameView=Type.GetType("UnityEditor.GameView,UnityEditor");
            if(gameView!=null) EditorWindow.GetWindow(gameView).Focus();
        }
        [MenuItem("Museum/Inspection/Create isolated demo (first time only)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save your current scene first. This tool will open a different scene.");
            foreach(var path in new[]{ ScenePath, PrefabPath, Root+"/Materials/Copper.mat" })
                if(File.Exists(path) || File.Exists(path+".meta")) throw new InvalidOperationException("Existing inspection output: "+path+". Open the saved scene; do not rebuild it.");
            foreach(var dir in new[]{"Scenes","Prefabs","Materials"}) Directory.CreateDirectory(Root+"/"+dir);
            AssetDatabase.Refresh();
            var shader=Shader.Find("Museum/Inspection Shell");
            if(!shader || !shader.isSupported) throw new InvalidOperationException("Inspection shader is unavailable or unsupported.");
            var copper = MakeMaterial("Copper",shader,new Color(.55f,.26f,.115f));
            var brass = MakeMaterial("Brass",shader,new Color(.79f,.57f,.24f));
            var dark = MakeMaterial("Window",shader,new Color(.06f,.15f,.18f));
            var shellInside = new Color(.30f,.40f,.39f);
            foreach(var material in new[]{copper,brass,dark}) material.SetColor("_InsideColor",shellInside);
            var scene = EditorSceneManager.OpenScene("Assets/Museum/Scenes/MuseumStart.unity");
            // Retain the existing rig, manager and action assets verbatim in a new scene.
            foreach(var root in scene.GetRootGameObjects())
                if(!root.GetComponentInChildren<XROrigin>(true) && !root.GetComponentInChildren<XRInteractionManager>(true) && root.name!="XR Input Actions")
                    UnityEngine.Object.DestroyImmediate(root);
            ConfigureInspectionRays();
            RenderSettings.ambientLight = new Color(.32f,.38f,.41f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var light = new GameObject("Inspection daylight").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.3f; light.transform.rotation=Quaternion.Euler(42,-28,0);
            var background = MakeLit("Studio",new Color(.035f,.07f,.085f),0f,.2f);
            var surface = MakeLit("Plinth",new Color(.09f,.17f,.19f),.15f,.4f);
            var controlMat = MakeLit("Controls",new Color(.07f,.30f,.33f),.05f,.35f);
            Cube("Floor",new Vector3(0,-.1f,3),new Vector3(12,.2f,12),background);
            Cube("Background",new Vector3(0,2,5.5f),new Vector3(12,6,.12f),background);
            Cube("Inspection plinth",new Vector3(0,.57f,3.5f),new Vector3(1.12f,1.14f,.8f),surface);
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Museum/Prefabs/HelmetExhibit.prefab");
            var helmet=(GameObject)PrefabUtility.InstantiatePrefab(original);
            PrefabUtility.UnpackPrefabInstance(helmet,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            helmet.name="InspectableHelmet";
            helmet.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var renderers=helmet.GetComponentsInChildren<Renderer>();
            Renderer Find(string name) => renderers.First(r=>r.name==name);
            var front=Find("GB_Front_PortRim"); var glass=Find("GB_Front_GlassPlaceholder"); var hinge=Find("GB_Hinge");
            foreach(var renderer in renderers)
            {
                bool trim = renderer.name.Contains("Rim") || renderer.name.Contains("Guard") || renderer.name.Contains("Ring") || renderer.name.Contains("Fastener");
                renderer.sharedMaterial = renderer.name.Contains("Glass") ? dark : trim ? brass : copper;
            }
            var pivot=new GameObject("Front Window Hinge").transform;
            pivot.SetParent(helmet.transform,false);
            pivot.position=new Vector3(hinge.bounds.center.x,glass.bounds.center.y,glass.bounds.center.z);
            var windowFrame=UnityEngine.Object.Instantiate(front.gameObject,front.transform.parent);
            windowFrame.name="Moving front window frame";
            windowFrame.transform.position+=Vector3.back*.009f;
            var moving=renderers.Where(r=>r.name=="GB_Front_GlassPlaceholder" || r.name.Contains("GB_FrontGuard")).Select(r=>r.transform).ToList();
            // Guard object names differ between early exports; inspect the actual front rods as a fallback.
            if(moving.Count<2)
                moving.AddRange(renderers.Where(r=>r.name.Contains("Guard") && r.bounds.center.z < front.bounds.center.z+.025f).Select(r=>r.transform));
            moving.Add(windowFrame.transform);
            foreach(var part in moving.Distinct()) part.SetParent(pivot,true);
            var window= pivot.gameObject.AddComponent<HelmetWindowInteractable>();
            var hit=pivot.gameObject.AddComponent<BoxCollider>(); hit.isTrigger=true;
            var closedBounds=glass.bounds;
            hit.center=pivot.InverseTransformPoint(closedBounds.center+Vector3.back*.03f);
            hit.size=new Vector3(closedBounds.size.x*.98f,closedBounds.size.y*.98f,.028f);
            window.colliders.Clear(); window.colliders.Add(hit); window.interactionLayers=1;
            var controller=helmet.AddComponent<HelmetInspectionController>();
            var clipped=helmet.GetComponentsInChildren<Renderer>().Where(r=>!r.transform.IsChildOf(pivot)).ToArray();
            controller.Configure(pivot,clipped,pivot.GetComponentsInChildren<Renderer>(),-Mathf.Sign(hinge.bounds.center.x));
            window.Configure(controller);
            PrefabUtility.SaveAsPrefabAsset(helmet,PrefabPath);
            UnityEngine.Object.DestroyImmediate(helmet);
            helmet=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            helmet.transform.position=new Vector3(0,1.157f,3.5f);
            var reset=helmet.GetComponent<HelmetExhibitReset>(); reset.ConfigureHomePose(helmet.transform.position,helmet.transform.rotation);
            controller=helmet.GetComponent<HelmetInspectionController>();
            var station = new GameObject("Inspection controls");
            // Vertical front-facing controls remain reachable by either ray; no walking is needed.
            AddButton(station.transform,controller,"WINDOW",new Vector3(-.34f,1.045f,3.045f),0,controlMat);
            AddButton(station.transform,controller,"SECTION",new Vector3(0,1.045f,3.045f),1,controlMat);
            AddButton(station.transform,controller,"RESET",new Vector3(.34f,1.045f,3.045f),4,controlMat);
            AddButton(station.transform,controller,"TURN LEFT",new Vector3(-.19f,.89f,3.045f),2,controlMat);
            AddButton(station.transform,controller,"TURN RIGHT",new Vector3(.19f,.89f,3.045f),3,controlMat);
            Label("Title","INSIDE THE DIVING HELMET",new Vector3(0,1.98f,3.64f),new Vector2(2,.2f),3.1f,Color.white);
            Label("Task","Open the window. Reveal the shell. Look inside.",new Vector3(0,1.84f,3.63f),new Vector2(2,.15f),1.65f,new Color(.52f,.84f,.85f));
            Label("Note","INTERPRETIVE MODEL  /  INTERNAL EQUIPMENT NOT RECONSTRUCTED",new Vector3(0,.71f,3.035f),new Vector2(1.15f,.11f),.82f,Color.white);
            var preview=new GameObject("Desktop Preview - disable this object for XR validation");
            var camera=preview.AddComponent<Camera>(); camera.enabled=false; camera.fieldOfView=38; camera.nearClipPlane=.01f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.07f,.085f);
            camera.stereoTargetEye=StereoTargetEyeMask.None;
            camera.rect=new Rect(.25f,0,.75f,1);
            preview.AddComponent<HelmetInspectionDesktopPreview>().Configure(controller,camera);
            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../tmp");
            File.WriteAllText("../tmp/inspection-geometry.txt",string.Join("\n",helmet.GetComponentsInChildren<Renderer>().Select(r=>r.name+" "+r.bounds.ToString("F4"))));
            Debug.Log("INSPECTION_BUILD_OK "+ScenePath);
        }
        static void AddButton(Transform parent,HelmetInspectionController controller,string text,Vector3 position,int action,Material material)
        {
            var go=Cube("Inspection "+text,position,new Vector3(.28f,.105f,.065f),material);
            go.transform.SetParent(parent,true);
            var interactable=go.AddComponent<XRSimpleInteractable>(); interactable.colliders.Add(go.GetComponent<Collider>()); interactable.interactionLayers=1;
            // The enum's order is part of this isolated builder/runtime interface.
            var control=go.AddComponent<HelmetInspectionControls>();
            control.Configure(controller,(HelmetInspectionControls.Action)action);
            Label(text+" label",text,position+Vector3.back*.035f,new Vector2(.26f,.09f),1.1f,Color.white).transform.SetParent(go.transform,true);
        }
        static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material)
        { var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.position=position; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material; return go; }
        static TextMeshPro Label(string name,string content,Vector3 position,Vector2 size,float font,Color color)
        {
            var text=new GameObject(name).AddComponent<TextMeshPro>(); text.transform.position=position;
            text.text=content; text.font=TMP_Settings.defaultFontAsset; text.fontSize=font; text.color=color;
            text.alignment=TextAlignmentOptions.Center; text.rectTransform.sizeDelta=size;
            text.enableWordWrapping=false; text.enableAutoSizing=true; text.fontSizeMin=.02f; text.fontSizeMax=font;
            text.ForceMeshUpdate(true); return text;
        }
        public static void RefineDemoLabels()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            ConfigureInspectionRays();
            foreach(var text in UnityEngine.Object.FindObjectsOfType<TextMeshPro>())
            {text.enableWordWrapping=false;text.enableAutoSizing=true;text.fontSizeMin=.02f;text.fontSizeMax=text.fontSize;text.ForceMeshUpdate(true);EditorUtility.SetDirty(text);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        static void ConfigureInspectionRays()
        {
            // The window uses its own trigger so it cannot add unwanted physical collision forces.
            // Scope this override to this copied scene; never modify the shared XRI sample prefab.
            foreach(var ray in UnityEngine.Object.FindObjectsOfType<XRRayInteractor>(true))
                if(!ray.name.Contains("Teleport"))
                {ray.raycastTriggerInteraction=QueryTriggerInteraction.Collide;PrefabUtility.RecordPrefabInstancePropertyModifications(ray);}
            foreach(var direct in UnityEngine.Object.FindObjectsOfType<XRDirectInteractor>(true))
            {direct.physicsTriggerInteraction=QueryTriggerInteraction.Collide;PrefabUtility.RecordPrefabInstancePropertyModifications(direct);}
        }
        static Material MakeMaterial(string name,Shader shader,Color color)
        { var mat=new Material(shader){name=name}; mat.SetColor("_BaseColor",color); AssetDatabase.CreateAsset(mat,Root+"/Materials/"+name+".mat"); return mat; }
        static Material MakeLit(string name,Color color,float metallic,float smoothness)
        { var shader=Shader.Find("Universal Render Pipeline/Lit"); if(!shader)throw new InvalidOperationException("URP/Lit unavailable"); var mat=MakeMaterial(name,shader,color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smoothness);return mat; }
    }
}
#endif
