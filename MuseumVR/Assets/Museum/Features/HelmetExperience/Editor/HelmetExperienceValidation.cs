#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Museum.Inspection;
using Museum.Exploration;
using Museum.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Museum.Experience.EditorTools
{
    [InitializeOnLoad]
    public static class HelmetExperienceValidation
    {
        const string Prefix="Museum.ExperienceQA.";
        static IEnumerator routine;
        static double started,exitAt;
        static int code=-1,lastFrame=-1;
        static Report report;
        static string output;
        [Serializable] class Check {public string name,detail;public bool passed;}
        [Serializable] class Fingerprint {public string path,sha256;}
        [Serializable] class Report
        {
            public string status="running",unity,baseCommit,dirty,method="New behavior checked through explicit component APIs in saved MuseumStart. Separate existing XR regression validates production input. Temporary diagnostic camera screenshots, no scene saves or headset/user study.",failure="";
            public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
            public List<Fingerprint> files=new List<Fingerprint>();
        }
        static HelmetExperienceValidation(){EditorApplication.playModeStateChanged+=Mode;EditorApplication.update+=Tick;}
        public static void RunBatch()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Run from Edit Mode.");
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/helmet-experience/qa/runs",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
            Directory.CreateDirectory(output);SessionState.SetString(Prefix+"Output",output);SessionState.SetBool(Prefix+"Active",true);
            EditorSceneManager.OpenScene("Assets/Museum/Scenes/MuseumStart.unity");EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Input(){if(!SessionState.GetBool(Prefix+"Active",false))return;InputSystem.AddDevice<Keyboard>("Experience QA Keyboard");if(Mouse.current==null)InputSystem.AddDevice<Mouse>();}
        static void Mode(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Prefix+"Active",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                output=SessionState.GetString(Prefix+"Output","");report=new Report{unity=Application.unityVersion,baseCommit=Git("rev-parse HEAD"),dirty=Git("status --short --untracked-files=no")};
                foreach(var path in Directory.GetFiles(Application.dataPath+"/Museum","*",SearchOption.AllDirectories)
                    .Where(p=>p.EndsWith(".cs") || p.EndsWith(".shader") || p.EndsWith(".asset") || p.EndsWith(".mat") || p.EndsWith(".prefab") || p.EndsWith(".unity") || p.EndsWith(".ttf")))
                    report.files.Add(new Fingerprint{path=path.Substring(Application.dataPath.Length+1).Replace('\\','/'),sha256=Hash(path)});
                Application.logMessageReceived+=Log;started=EditorApplication.timeSinceStartup;routine=Run();
            }
            if(state==PlayModeStateChange.EnteredEditMode && code>=0)exitAt=EditorApplication.timeSinceStartup+20;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Prefix+"Active",false))return;
            if(exitAt>0 && EditorApplication.timeSinceStartup>=exitAt){SessionState.SetBool(Prefix+"Active",false);EditorApplication.Exit(code);return;}
            if(routine==null || !EditorApplication.isPlaying || lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            try{if(EditorApplication.timeSinceStartup-started>180)throw new TimeoutException("Experience QA timed out.");if(!routine.MoveNext())Finish(null);}
            catch(Exception e){Finish(e);}
        }
        static void CheckThat(bool ok,string name,string detail="")
        {report.checks.Add(new Check{name=name,passed=ok,detail=detail});if(!ok)throw new InvalidOperationException(name+": "+detail);}
        static void Finish(Exception error)
        {
            routine=null;
            foreach(var f in report.files)if(Hash(Application.dataPath+"/"+f.path)!=f.sha256)report.errors.Add("File changed during QA: "+f.path);
            if(error!=null)report.failure=error.ToString();code=error==null && report.errors.Count==0?0:1;report.status=code==0?"passed":"failed";
            Application.logMessageReceived-=Log;File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("EXPERIENCE_QA_"+report.status.ToUpperInvariant()+" "+output);EditorApplication.ExitPlaymode();
        }
        static void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)report.errors.Add(message+"\n"+stack);}
        static IEnumerator Run()
        {
            float until=Time.realtimeSinceStartup+.5f;while(Time.realtimeSinceStartup<until)yield return null;
            var inspection=UnityEngine.Object.FindObjectOfType<HelmetInspectionController>();
            var reset=inspection.GetComponent<HelmetExhibitReset>();var body=inspection.GetComponent<Rigidbody>();
            var surface=inspection.GetComponent<HelmetSectionSurface>();var feedback=inspection.GetComponent<HelmetMechanicalFeedback>();
            var panel=UnityEngine.Object.FindObjectOfType<HelmetExplorationPanel>();
            var demo=UnityEngine.Object.FindObjectOfType<HelmetConnectionDemonstration>();
            CheckThat(surface && feedback,"saved prefab has section and feedback components");
            var rear=inspection.GetComponent<HelmetRearConnections>();
            CheckThat(rear && rear.TelephoneOutlet && rear.AirOutlet,"saved helmet has independent air and telephone outlet anchors");
            Vector3 airLocal=inspection.transform.InverseTransformPoint(rear.AirOutlet.position),phoneLocal=inspection.transform.InverseTransformPoint(rear.TelephoneOutlet.position);
            CheckThat(airLocal.x<-.03f && phoneLocal.x>.03f && airLocal.z>0 && phoneLocal.z>0,"from rear: phone left and air right", "air="+airLocal+"; phone="+phoneLocal);
            CheckThat(rear.TelephoneSurfaces.All(r=>r && r.GetComponent<MeshFilter>().sharedMesh) && !rear.GetComponentsInChildren<Collider>(true).Any(c=>c.name.Contains("Telephone")),"telephone addition has saved mesh and does not add colliders");
            Capture(inspection,"00-rear-two-fittings",inspection.transform.forward*.75f+Vector3.up*.13f);
            var glass=inspection.GetComponentsInChildren<Renderer>().Single(r=>r.name=="GB_Front_GlassPlaceholder");
            CheckThat(glass.sharedMaterial.shader.name=="Museum/Inspection Glass" && glass.sharedMaterial.renderQueue>=3000,"transparent glass saved",glass.sharedMaterial.shader.name);
            CheckHollowGeometry();
            inspection.ResetInspection();inspection.ToggleFloating();
            Capture(inspection,"01-window-closed",new Vector3(-.45f,.12f,-.90f));
            // Synchronous rendering can compile shaders and stall this frame. Start
            // the time-based action on a fresh frame so the capture's elapsed time
            // is not counted as window animation time before its first update.
            yield return null;
            inspection.SetWindowOpen(true);float last=inspection.WindowAngle;
            until=Time.realtimeSinceStartup+.22f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(inspection.WindowAngle>last && inspection.WindowAngle<HelmetInspectionController.MaximumWindowAngle,"window has an intermediate opening pose",inspection.WindowAngle.ToString());
            Capture(inspection,"02-window-opening",new Vector3(-.45f,.12f,-.90f));
            until=Time.realtimeSinceStartup+.6f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(Mathf.Abs(inspection.WindowAngle-95)<.01f && feedback.CompletionSounds>0,"opening reaches limit with completion feedback","angle="+inspection.WindowAngle+"; completion sounds="+feedback.CompletionSounds);
            Capture(inspection,"03-window-open",new Vector3(-.45f,.12f,-.90f));
            feedback.SoundEnabled=false;inspection.SetWindowOpen(false);
            until=Time.realtimeSinceStartup+.8f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(inspection.WindowAngle<.01f && !feedback.HingePlaying,"mute stops sound and preserves window movement");
            feedback.SoundEnabled=true;
            foreach(var normal in new[]{Vector3.right,Vector3.up,new Vector3(.4f,.3f,-.8f).normalized})
            {
                inspection.SetCutaway(true);inspection.SetSectionPlane(normal,0);surface.Refresh();
                CheckThat(surface.TriangleCount>0,"nonempty section face / "+normal,surface.TriangleCount+" triangles; rejected open bands="+surface.RejectedOpenBands);
                foreach(var filter in inspection.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Section face -") && f.gameObject.activeInHierarchy))
                    CheckThat(filter.sharedMesh.vertices.All(v=>Mathf.Abs(Vector3.Dot(normal,v-inspection.SectionPointLocal))<.0001f),"section faces stay on requested local plane / "+filter.name);
                Capture(inspection,"04-section-"+normal.ToString().Replace(" ","").Replace(",","_").Replace("(","").Replace(")",""),normal*.95f+Vector3.up*.1f);
            }
            int rebuild=surface.RebuildCount;inspection.transform.rotation*=Quaternion.Euler(9,32,2);surface.Refresh();
            CheckThat(surface.RebuildCount==rebuild,"whole-helmet motion reuses existing section mesh");
            var shell=inspection.GetComponentsInChildren<Renderer>().Single(r=>r.name=="GB_Bonnet_Hollow");
            var block=new MaterialPropertyBlock();shell.GetPropertyBlock(block);
            Vector3 stored=block.GetVector("_ShellCenter");
            CheckThat(Vector3.Distance(stored,shell.GetComponent<MeshFilter>().sharedMesh.bounds.center)<.00001f,"inner-surface shading center stays object-local when moved");
            inspection.SetWindowAngle(35);surface.Refresh();CheckThat(surface.RebuildCount>rebuild,"moving articulated parts updates section faces");
            inspection.SetCutaway(false);surface.Refresh();CheckThat(!inspection.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.name.StartsWith("Section face -") && r.gameObject.activeInHierarchy),"closing section hides all cap geometry");
            inspection.ResetInspection();
            // The section captures above can also stall this frame before a
            // time-based return starts. Refresh its clock without changing the
            // intermediate-pose assertion or the product's return duration.
            yield return null;
            var home=reset.HomePose;body.isKinematic=true;body.useGravity=false;
            body.position=home.position+new Vector3(.5f,.2f,-.25f);body.rotation=Quaternion.Euler(25,70,5);inspection.transform.SetPositionAndRotation(body.position,body.rotation);
            Vector3 from=body.position;reset.RequestResetToDisplay();
            CheckThat(reset.IsReturning && Vector3.Distance(inspection.transform.position,from)<.001f,"manual return starts at existing pose without initial teleport");
            until=Time.realtimeSinceStartup+.12f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(reset.IsReturning && Vector3.Distance(body.position,from)>.01f && Vector3.Distance(body.position,home.position)>.01f,"return has an intermediate pose");
            Capture(inspection,"05-return-midway",new Vector3(-.55f,.25f,-1.1f));
            until=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(!reset.IsReturning && Vector3.Distance(body.position,home.position)<.02f && Quaternion.Angle(body.rotation,home.rotation)<1,"return finishes at exact home with original physics");
            inspection.ReduceMotion=true;body.isKinematic=true;body.position=home.position+Vector3.up*.2f;inspection.transform.position=body.position;
            reset.RequestResetToDisplay();CheckThat(!reset.IsReturning && Vector3.Distance(body.position,home.position)<.001f,"reduced motion returns immediately");
            inspection.ReduceMotion=false;inspection.ResetInspection();
            panel.ShowHelp();panel.ToggleSound();CheckThat(!feedback.SoundEnabled,"help mutes synthesized feedback");panel.ToggleSound();CheckThat(feedback.SoundEnabled,"help restores synthesized feedback");
            Capture(inspection,"06-help-controls",new Vector3(-.45f,.12f,-.90f));panel.CloseReading();panel.ShowExploration();
            // Let the original exploration controller consume the preceding reset before starting a new clue.
            until=Time.realtimeSinceStartup+.2f;while(Time.realtimeSinceStartup<until)yield return null;
            demo.Play(HelmetExplorationController.Clue.Air);until=Time.realtimeSinceStartup+2.6f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(Vector3.Distance(demo.AirEndpointWorld,rear.AirOutlet.position)<.0001f && rear.VoiceFocus==0 && !rear.VoiceResponseVisible,"air goes only to air outlet and clears telephone feedback");
            CheckThat(demo.SourceEmphasis>demo.DestinationEmphasis && !demo.HasReachedDestination,"air source emphasized before arrival","source="+demo.SourceEmphasis+"; destination="+demo.DestinationEmphasis+"; visible="+demo.IsVisible);
            CaptureDemo(demo,"07-air-sender");until=Time.realtimeSinceStartup+4.4f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(demo.DestinationEmphasis>demo.SourceEmphasis && demo.HasReachedDestination,"air receiving connector emphasized after traversal");CaptureDemo(demo,"08-air-receiver");
            demo.Stop();CheckThat(demo.SourceEmphasis==0 && demo.DestinationEmphasis==0,"stop clears attention");
            demo.Play(HelmetExplorationController.Clue.Voice);until=Time.realtimeSinceStartup+2.5f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(Vector3.Distance(demo.VoiceEndpointWorld,rear.TelephoneOutlet.position)<.0001f && Vector3.Distance(demo.VoiceEndpointWorld,rear.AirOutlet.position)>.08f,"voice cable terminates at telephone fitting, separate from air");
            CheckThat(!rear.VoiceResponseVisible,"telephone arrival feedback stays off before message arrives");
            Vector3 originalPosition=inspection.transform.position;Quaternion originalRotation=inspection.transform.rotation;
            inspection.transform.SetPositionAndRotation(originalPosition+new Vector3(.08f,.03f,.04f),originalRotation*Quaternion.Euler(10,35,0));
            yield return null;yield return null;
            CheckThat(Vector3.Distance(demo.VoiceEndpointWorld,rear.TelephoneOutlet.position)<.0001f,"telephone cable follows moved and rotated helmet");
            inspection.transform.SetPositionAndRotation(originalPosition,originalRotation);yield return null;
            CheckThat(demo.SourceEmphasis>demo.DestinationEmphasis,"outgoing message starts at surface");CaptureDemo(demo,"09-voice-surface");
            until=Time.realtimeSinceStartup+3.7f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(demo.DestinationEmphasis>demo.SourceEmphasis && demo.HasReachedDestination,"outgoing message arrival emphasizes diver");CaptureDemo(demo,"10-voice-diver-received");
            CheckThat(rear.VoiceResponseVisible && rear.VoiceFocus>.65f,"arrival highlights and animates actual telephone fitting");
            var fittingBlock=new MaterialPropertyBlock();rear.TelephoneSurfaces[0].GetPropertyBlock(fittingBlock);
            CheckThat(fittingBlock.GetFloat("_Focus")>.65f,"telephone material receives staged emphasis");
            Capture(inspection,"10b-rear-telephone-received",inspection.transform.forward*.8f+Vector3.up*.13f);
            CheckThat(!demo.GetComponentsInChildren<Renderer>(true).Any(r=>r.name.StartsWith("Surface voice wave ") && r.gameObject.activeInHierarchy),"sender speech waves stop before receiver emphasis");
            until=Time.realtimeSinceStartup+3.3f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(demo.PhaseIndex==3 && demo.DestinationEmphasis>demo.SourceEmphasis,"reply starts at diver");CaptureDemo(demo,"11-voice-diver-reply");
            CheckThat(rear.VoiceResponseVisible && rear.VoiceFocus>.65f,"reply begins at the helmet telephone fitting");
            until=Time.realtimeSinceStartup+3.8f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(demo.PhaseIndex==3 && demo.SourceEmphasis>demo.DestinationEmphasis,"reply arrival emphasizes surface");CaptureDemo(demo,"12-voice-surface-received");
            CheckThat(!rear.VoiceResponseVisible && rear.VoiceFocus<.3f,"reply arrival releases telephone emphasis toward surface");
            CheckThat(!demo.GetComponentsInChildren<Renderer>(true).Any(r=>r.name.StartsWith("Diver voice wave ") && r.gameObject.activeInHierarchy),"reply speech waves stop before receiving surface emphasis");
            demo.Stop();CheckThat(rear.VoiceFocus==0 && !rear.VoiceResponseVisible,"stop clears actual telephone material and response");
            inspection.ReduceMotion=true;demo.Play(HelmetExplorationController.Clue.Voice);
            CheckThat(!demo.IsPlaying && !demo.HasReachedDestination && Mathf.Abs(demo.SourceEmphasis-demo.DestinationEmphasis)<.001f,"reduced motion shows stable equal emphasis");
            var indicator=rear.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Telephone receive indicator");Vector3 scale=indicator.localScale;
            until=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<until)yield return null;
            CheckThat(indicator.localScale==scale && rear.VoiceResponseVisible,"reduced motion keeps telephone response still");
            inspection.SetCutaway(true);inspection.SetSectionPlane(Vector3.right,0);surface.Refresh();yield return null;yield return null;
            CheckThat(!rear.VoiceResponseVisible,"removed telephone side hides its response ring");
            inspection.SetSectionPlane(Vector3.forward,phoneLocal.z-inspection.SectionCenterLocal.z-.02f);surface.Refresh();
            CheckThat(inspection.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.name=="Section face - Telephone elbow and cable union" && r.gameObject.activeInHierarchy),"new hollow telephone fitting participates in section surfaces");
            Capture(inspection,"13-rear-telephone-section",inspection.transform.forward*.75f+Vector3.up*.13f);
            demo.Stop();inspection.ReduceMotion=false;inspection.ResetInspection();yield return null;yield return null;
            CheckThat(Vector3.Distance(rear.TelephoneOutlet.position,inspection.transform.TransformPoint(phoneLocal))<.0001f,"reset keeps saved telephone anchor attached");
        }
        static void CheckHollowGeometry()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            AddCube(1);AddCube(.5f);
            var result=new List<Vector3>();var indices=new List<int>();SectionCapGeometry.Build(vertices.ToArray(),triangles.ToArray(),Matrix4x4.identity,Vector3.forward,Vector3.zero,result,indices);
            float area=0;bool covered=false;
            for(int i=0;i<indices.Count;i+=3){Vector3 a=result[indices[i]],b=result[indices[i+1]],c=result[indices[i+2]];area+=Vector3.Cross(b-a,c-a).magnitude*.5f;covered|=Inside(Vector3.zero,a,b,c);}
            CheckThat(Mathf.Abs(area-3)<.001f && !covered,"nested contours preserve cavity", "hollow square section area="+area+"; center filled="+covered);
            void AddCube(float size)
            {
                int start=vertices.Count;for(int i=0;i<8;i++)vertices.Add(new Vector3((i&1)==0?-size:size,(i&2)==0?-size:size,(i&4)==0?-size:size));
                foreach(int index in new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5})triangles.Add(start+index);
            }
        }
        static bool Inside(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {float s1=Vector3.Cross(b-a,p-a).z,s2=Vector3.Cross(c-b,p-b).z,s3=Vector3.Cross(a-c,p-c).z;return (s1>=0 && s2>=0 && s3>=0)||(s1<=0 && s2<=0 && s3<=0);}
        static void Capture(HelmetInspectionController inspection,string name,Vector3 offset)
        {Vector3 center=inspection.transform.TransformPoint(inspection.SectionCenterLocal);Render(name,center+offset,center);}
        static void CaptureDemo(HelmetConnectionDemonstration demo,string name)
        {
            var renderers=demo.GetComponentsInChildren<Renderer>().Where(r=>r.gameObject.activeInHierarchy).ToArray();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            Vector3 direction=Camera.main?Vector3.ProjectOnPlane(Camera.main.transform.position-bounds.center,Vector3.up).normalized:Vector3.back;
            Render(name,bounds.center+direction*1.35f+Vector3.up*.15f,bounds.center);
        }
        static void Render(string name,Vector3 position,Vector3 center)
        {
            var go=new GameObject("Temporary experience QA camera");var camera=go.AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(center);camera.fieldOfView=48;camera.nearClipPlane=.02f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.055f,.075f);
            var target=RenderTexture.GetTemporary(1280,900,24);var prior=RenderTexture.active;var pixels=new Texture2D(1280,900,TextureFormat.RGB24,false);
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());}
            finally{RenderTexture.active=prior;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);}
        }
        static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
        static string Git(string arguments)
        {var info=new System.Diagnostics.ProcessStartInfo("git",arguments){WorkingDirectory=Path.GetFullPath(Application.dataPath+"/../.."),UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true};using(var p=System.Diagnostics.Process.Start(info)){string value=p.StandardOutput.ReadToEnd();p.WaitForExit();return value.Trim();}}
    }
}
#endif
