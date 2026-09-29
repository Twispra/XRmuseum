#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Museum.Inspection.Editor
{
    public static class HelmetInspectionCapture
    {
        [MenuItem("Museum/Inspection/Capture saved demo views (read only)")]
        public static void Capture()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before capturing.");
            if(!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
            EditorSceneManager.OpenScene(HelmetInspectionBuilder.ScenePath);
            var exhibit=UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            var root=new GameObject("Temporary inspection capture"){hideFlags=HideFlags.HideAndDontSave};
            var camera=root.AddComponent<Camera>(); camera.enabled=false;
            camera.nearClipPlane=.01f; camera.farClipPlane=20; camera.fieldOfView=37; camera.aspect=1.2f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.07f,.085f);
            camera.stereoTargetEye=StereoTargetEyeMask.None;
            var data=root.AddComponent<UniversalAdditionalCameraData>();data.allowXRRendering=false;
            foreach(var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>())text.ForceMeshUpdate(true);
            string path=Path.GetFullPath("../artifacts/helmet-interaction/qa/runs/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-inspection-views");
            Directory.CreateDirectory(path);
            try
            {
                Position(camera,exhibit,-23,11);
                Render(camera,Path.Combine(path,"01-closed.jpg"));
                exhibit.SetWindowOpen(true,true);
                Render(camera,Path.Combine(path,"02-open.jpg"));
                exhibit.SetCutaway(true);
                Position(camera,exhibit,-67,13);
                Render(camera,Path.Combine(path,"03-half-section.jpg"));
                exhibit.SetWindowOpen(false,true);
                Position(camera,exhibit,-100,8);
                Render(camera,Path.Combine(path,"04-inside-shell.jpg"));
                File.WriteAllText(Path.Combine(path,"README.txt"),"Actual Unity URP renders of the saved isolated scene, with temporary inspection state changes.\nThese are edit-mode visual previews, not headset validation or captured user input.\nAngle limits and highlighted section edges are interpretation aids, not historical measurements.\nNo scene was saved by this capture.\nUnity "+Application.unityVersion);
                Debug.Log("INSPECTION_CAPTURE_OK "+path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void Position(Camera camera,HelmetInspectionController exhibit,float yaw,float elevation)
        {var q=Quaternion.Euler(elevation,yaw,0);var target=exhibit.transform.position+Vector3.up*.275f;camera.transform.SetPositionAndRotation(target+q*new Vector3(0,0,-1.02f),q);}
        static void Render(Camera camera,string path)
        {
            var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(1080,900,24,RenderTextureFormat.ARGB32);
            Texture2D pixels=null;
            try
            {RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;pixels=new Texture2D(1080,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1080,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToJPG(93));}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);if(pixels)UnityEngine.Object.DestroyImmediate(pixels);}
        }
    }
}
#endif
