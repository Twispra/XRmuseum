#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Museum.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Museum.Diegetic.Editor
{
    [InitializeOnLoad]
    public static class HelmetDiegeticPreview
    {
        const string Key="DiegeticPreview";
        static HelmetDiegeticStation station;
        static double next,exit;
        static int step;
        static string Folder=>SessionState.GetString(Key+"Folder","");
        static HelmetDiegeticPreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Prepare(){if(!SessionState.GetBool(Key,false))return;if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();if(Mouse.current==null)InputSystem.AddDevice<Mouse>();}
        public static void RunBatch(){SessionState.SetString(Key+"Folder",Path.GetFullPath(Path.Combine(Application.dataPath,"../../../tmp/height-demo-20261017/preview-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"))));Directory.CreateDirectory(Folder);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Museum/Scenes/MuseumStart.unity");EditorApplication.EnterPlaymode();}
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){step=0;next=EditorApplication.timeSinceStartup+3;}
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);exit=EditorApplication.timeSinceStartup+2;}
        }
        static void Tick()
        {
            if(exit>0 && EditorApplication.timeSinceStartup>exit){EditorApplication.Exit(0);return;}
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+2;
            try
            {
                if(!station)station=UnityEngine.Object.FindObjectOfType<HelmetDiegeticStation>();
                if(!station || !station.IsReady)throw new InvalidOperationException("Station not ready.");
                switch(step++)
                {
                    case 0:Capture("01-default",new Vector3(.15f,.4f,-1.8f));station.Execute(HelmetDiegeticStation.Command.Hints);break;
                    case 1:Capture("02-hints",new Vector3(.15f,.4f,-1.8f));station.Execute(HelmetDiegeticStation.Command.Section);break;
                    case 2:Capture("03-section",new Vector3(-.08f,.28f,-1.4f));station.Execute(HelmetDiegeticStation.Command.Air);break;
                    case 3:Capture("04-air",new Vector3(.45f,.40f,-1.2f));station.Execute(HelmetDiegeticStation.Command.Voice);break;
                    case 4:Capture("05-voice",new Vector3(.65f,.35f,.95f));station.Execute(HelmetDiegeticStation.Command.Reset);break;
                    case 5:station.Execute(HelmetDiegeticStation.Command.Settings);MuseumText.Channel.SetLanguage(MuseumLanguage.Chinese);break;
                    case 6:Capture("06-settings-cn",new Vector3(0,.4f,-2.1f));MuseumText.Channel.SetLanguage(MuseumLanguage.TraditionalChinese);break;
                    case 7:Capture("07-settings-tc",new Vector3(0,.4f,-2.1f));station.Execute(HelmetDiegeticStation.Command.NextEra);break;
                    case 8:Capture("08-superlite",new Vector3(.15f,.38f,-1.05f));station.Execute(HelmetDiegeticStation.Command.PreviousEra);MuseumText.Channel.SetLanguage(MuseumLanguage.Chinese);break;
                    case 9:station.Execute(HelmetDiegeticStation.Command.Settings);Capture("09-body-only-controls",new Vector3(0,.25f,-1.5f));station.Execute(HelmetDiegeticStation.Command.Window);break;
                    case 10:Capture("10-window-open-no-button",new Vector3(.22f,.13f,-.9f));station.Execute(HelmetDiegeticStation.Command.Window);station.Execute(HelmetDiegeticStation.Command.Section);break;
                    case 11:Capture("11-fixed-section-rail",new Vector3(0,.18f,-.85f),true);station.SectionGesture.BeginForValidation(Vector3.zero,Quaternion.identity);station.SectionGesture.ApplyForValidation(station.SectionRail.right*.10f);station.SectionGesture.EndForValidation();break;
                    case 12:Capture("12-rail-moved",new Vector3(0,.18f,-.85f),true);station.Execute(HelmetDiegeticStation.Command.Reset);break;
                    case 13:station.Execute(HelmetDiegeticStation.Command.Air);break;
                    case 14:
                        Capture("13-pump-fixed-beside-helmet",new Vector3(.45f,.25f,-1.1f));
                        var equipment=station.Demonstration.EquipmentExhibits.First(e=>e.IsVisible);
                        var gesture=equipment.Root.GetComponentInChildren<HelmetGestureInteractable>();
                        gesture.BeginForValidation(Vector3.zero,Camera.main.transform.rotation);
                        gesture.ApplyForValidation(Camera.main.transform.right*.18f+Camera.main.transform.up*.08f);
                        gesture.EndForValidation();break;
                    case 15:Capture("14-pump-rotated-in-place",new Vector3(.45f,.25f,-1.1f));station.Execute(HelmetDiegeticStation.Command.CloseDemo);station.Execute(HelmetDiegeticStation.Command.Settings);break;
                    case 16:Capture("15-height-settings-cn",new Vector3(0,.25f,-.85f),false,true);MuseumText.Channel.SetLanguage(MuseumLanguage.TraditionalChinese);break;
                    case 17:Capture("16-height-settings-tc",new Vector3(0,.25f,-.85f),false,true);MuseumText.Channel.SetLanguage(MuseumLanguage.English);break;
                    case 18:Capture("17-height-settings-en",new Vector3(0,.25f,-.85f),false,true);Debug.Log("DIEGETIC_PREVIEW_DONE "+Folder);EditorApplication.ExitPlaymode();break;
                }
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.ExitPlaymode();}
        }
        static void Capture(string name,Vector3 offset,bool railView=false,bool settingsView=false)
        {
            Vector3 center=station.Inspection.transform.TransformPoint(station.Inspection.SectionCenterLocal);
            if(railView)center=station.SectionRail.position+Vector3.up*.05f;
            if(settingsView)center=station.Controls[HelmetDiegeticStation.Command.HeightReset].transform.position;
            var go=new GameObject("Read-only preview camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=52;
            camera.transform.position=center+offset;camera.transform.LookAt(center+Vector3.down*.1f);
            camera.nearClipPlane=.02f;camera.farClipPlane=50;camera.clearFlags=CameraClearFlags.Skybox;
            var data=camera.gameObject.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;data.allowXRRendering=false;
            var target=new RenderTexture(1600,1200,24);camera.targetTexture=target;camera.stereoTargetEye=StereoTargetEyeMask.None;
            camera.aspect=1600f/1200f;camera.rect=new Rect(0,0,1,1);
            camera.projectionMatrix=Matrix4x4.Perspective(52,camera.aspect,.02f,50);
            camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(Folder,name+".png"),image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
#endif
