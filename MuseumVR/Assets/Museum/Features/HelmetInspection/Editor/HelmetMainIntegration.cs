#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Inspection.Editor
{
    /// <summary>One-time integration into the existing gallery; never rebuilds the gallery or XR rig.</summary>
    public static class HelmetMainIntegration
    {
        public const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";

        [MenuItem("Museum/Inspection/Integrate into MuseumStart (first time only)")]
        public static void Integrate()
        {
            RequireEditMode();
            var scene=EditorSceneManager.OpenScene(ScenePath);
            if(UnityEngine.Object.FindObjectsOfType<HelmetInspectionController>(true).Length!=0 || GameObject.Find("Helmet Inspection Controls"))
                throw new InvalidOperationException("MuseumStart already has inspection integration. Open it; do not re-run this tool.");
            var oldHelmet=GameObject.Find("HelmetExhibit");
            var oldReset=oldHelmet ? oldHelmet.GetComponent<HelmetExhibitReset>() : null;
            var resetButton=GameObject.Find("Helmet Reset Button")?.GetComponent<XRSimpleInteractable>();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HelmetInspectionBuilder.PrefabPath);
            var station=GameObject.Find("Helmet Interaction Station");
            if(!oldReset || !resetButton || !prefab || !station || !Camera.main)
                throw new InvalidOperationException("Expected saved gallery/helmet/reset/camera not found. No scene was saved.");
            var oldHome=oldReset.HomePose;
            var rootNames=scene.GetRootGameObjects().Select(r=>r.name).OrderBy(n=>n).ToArray();
            Vector3 cameraPosition=Camera.main.transform.position;
            Quaternion cameraRotation=Camera.main.transform.rotation;
            int[] resetListeners=Enumerable.Range(0,resetButton.selectEntered.GetPersistentEventCount())
                .Where(i=>resetButton.selectEntered.GetPersistentTarget(i)==oldReset && resetButton.selectEntered.GetPersistentMethodName(i)=="ResetToDisplay").ToArray();
            if(resetListeners.Length!=1) throw new InvalidOperationException("Unexpected RESET event wiring; integration refused before editing.");
            string backup=Path.GetFullPath("../tmp/main-integration-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath,Path.Combine(backup,"MuseumStart.before.unity"));

            var helmet=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            helmet.name=oldHelmet.name;
            helmet.transform.SetParent(oldHelmet.transform.parent,false);
            helmet.transform.SetPositionAndRotation(oldHelmet.transform.position,oldHelmet.transform.rotation);
            helmet.transform.localScale=oldHelmet.transform.localScale;
            var reset=helmet.GetComponent<HelmetExhibitReset>();
            reset.ConfigureHomePose(oldHome.position,oldHome.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(reset);
            PrefabUtility.RecordPrefabInstancePropertyModifications(helmet.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(helmet);
            UnityEventTools.RegisterVoidPersistentListener(resetButton.selectEntered,resetListeners[0],reset.ResetToDisplay);
            EditorUtility.SetDirty(resetButton);
            UnityEngine.Object.DestroyImmediate(oldHelmet);

            var inspection=helmet.GetComponent<HelmetInspectionController>();
            var controls=new GameObject("Helmet Inspection Controls");
            controls.transform.SetParent(station.transform,false);
            var material=AssetDatabase.LoadAssetAtPath<Material>(HelmetInspectionBuilder.Root+"/Materials/Controls.mat");
            var backingMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Museum/Materials/Helmet Plaque.mat");
            var backing=Cube(controls.transform,"Inspection panel",new Vector3(-.44f,1.4f,3.23f),new Vector3(.59f,.55f,.035f),backingMaterial);
            UnityEngine.Object.DestroyImmediate(backing.GetComponent<Collider>());
            Label(controls.transform,"Inspection panel title","INSPECT",new Vector3(-.44f,1.62f,3.205f),new Vector2(.5f,.06f),1.2f);
            AddButton(controls.transform,inspection,"WINDOW",new Vector3(-.44f,1.515f,3.19f),new Vector3(.49f,.10f,.06f),HelmetInspectionControls.Action.OpenWindow,material);
            AddButton(controls.transform,inspection,"SECTION",new Vector3(-.44f,1.37f,3.19f),new Vector3(.49f,.10f,.06f),HelmetInspectionControls.Action.Cutaway,material);
            AddButton(controls.transform,inspection,"TURN LEFT",new Vector3(-.575f,1.225f,3.19f),new Vector3(.22f,.09f,.06f),HelmetInspectionControls.Action.RotateLeft,material);
            AddButton(controls.transform,inspection,"TURN RIGHT",new Vector3(-.305f,1.225f,3.19f),new Vector3(.22f,.09f,.06f),HelmetInspectionControls.Action.RotateRight,material);
            foreach(var ray in UnityEngine.Object.FindObjectsOfType<XRRayInteractor>(true))
                if(!ray.name.Contains("Teleport"))
                {ray.raycastTriggerInteraction=QueryTriggerInteraction.Collide;PrefabUtility.RecordPrefabInstancePropertyModifications(ray);}
            foreach(var direct in UnityEngine.Object.FindObjectsOfType<XRDirectInteractor>(true))
            {direct.physicsTriggerInteraction=QueryTriggerInteraction.Collide;PrefabUtility.RecordPrefabInstancePropertyModifications(direct);}
            var instructions=GameObject.Find("Helmet Instructions")?.GetComponent<TextMeshPro>();
            if(instructions)
            {
                instructions.text="GRIP THE HELMET TO PICK IT UP\nPoint + grip WINDOW / SECTION / TURN.\nGrip the window to open. RESET restores everything.";
                instructions.enableWordWrapping=false;instructions.enableAutoSizing=true;instructions.fontSizeMin=.02f;instructions.fontSizeMax=1.5f;instructions.ForceMeshUpdate(true);
                EditorUtility.SetDirty(instructions);
            }
            if(!rootNames.SequenceEqual(scene.GetRootGameObjects().Select(r=>r.name).OrderBy(n=>n)))
                throw new InvalidOperationException("Unexpected gallery root changes; refusing to save.");
            if(Vector3.Distance(cameraPosition,Camera.main.transform.position)>.0001f || Quaternion.Angle(cameraRotation,Camera.main.transform.rotation)>.001f)
                throw new InvalidOperationException("XR camera moved during integration; refusing to save.");
            if(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0))
                throw new InvalidOperationException("Missing scripts; refusing to save.");
            if(UnityEngine.Object.FindObjectsOfType<HelmetInspectionDesktopPreview>(true).Length!=0)
                throw new InvalidOperationException("Desktop orbit preview must not be present in the gallery.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("HELMET_MAIN_INTEGRATION_OK: existing gallery, XR camera, home pose and RESET retained; local branch only.");
        }

        [MenuItem("Museum/Inspection/Open MuseumStart (XR controls)")]
        public static void OpenMain()
        {
            RequireEditMode();EditorSceneManager.OpenScene(ScenePath);
            var gameView=Type.GetType("UnityEditor.GameView,UnityEditor");
            if(gameView!=null)EditorWindow.GetWindow(gameView).Focus();
        }
        static void RequireEditMode()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save your current scene before opening/integrating MuseumStart.");
        }
        static void AddButton(Transform parent,HelmetInspectionController inspection,string name,Vector3 position,Vector3 scale,HelmetInspectionControls.Action action,Material material)
        {
            var go=Cube(parent,"Inspection "+name,position,scale,material);
            var interactable=go.AddComponent<XRSimpleInteractable>();interactable.colliders.Add(go.GetComponent<Collider>());interactable.interactionLayers=1;
            go.AddComponent<HelmetInspectionControls>().Configure(inspection,action);
            Label(parent,name+" label",name,position+Vector3.back*.035f,new Vector2(scale.x-.018f,scale.y-.01f),1.1f);
        }
        static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            if(!material)throw new InvalidOperationException("Missing material: "+name);
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static void Label(Transform parent,string name,string value,Vector3 position,Vector2 size,float maxFont)
        {
            var label=new GameObject(name).AddComponent<TextMeshPro>();label.transform.SetParent(parent,false);label.transform.position=position;
            label.font=TMP_Settings.defaultFontAsset;label.text=value;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta=size;label.enableWordWrapping=false;label.enableAutoSizing=true;label.fontSizeMin=.02f;label.fontSizeMax=maxFont;label.ForceMeshUpdate(true);
        }
    }
}
#endif
