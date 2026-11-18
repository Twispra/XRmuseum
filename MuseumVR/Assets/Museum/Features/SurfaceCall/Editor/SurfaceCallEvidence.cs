using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.Diegetic;
using Museum.Diegetic.Editor;
using Museum.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using Object = UnityEngine.Object;

namespace Museum.SurfaceLine.Editor
{
    /// <summary>Read-only Play Mode observation with tracked Input System devices. All exhibit actions use the saved XRI ray.</summary>
    [InitializeOnLoad]
    public static class SurfaceCallEvidence
    {
        const string Active = "SurfaceCall.Evidence";
        const string Sample = "Assets/Museum/Features/SurfaceCall/SurfaceCall.unity";
        static IEnumerator run;
        static readonly Queue<Action> input = new Queue<Action>();
        static readonly List<string> log = new List<string>();
        static string folder;
        static int frame, exitCode;
        static Keyboard keyboard;
        static Mouse mouse;
        static XRSimulatedHMD head;
        static XRSimulatedController hand;
        static ActionBasedController controller;
        static XRRayInteractor ray;
        static SurfaceCall call;
        static HelmetDiegeticStation station;
        static string movieFrames;
        static bool recording, sentGripButton;
        static int recorded;
        static float nextCapture;
        static readonly List<string> movieTimes = new List<string>();
        static bool batchEnding;
        static double exitAt;
        static float deadline;

        static SurfaceCallEvidence()
        {
            EditorApplication.playModeStateChanged += Mode;
            EditorApplication.update += Tick;
        }

        public static void CaptureBaseline() => Begin("Assets/Museum/Scenes/MuseumStart.unity");
        public static void CaptureSample() => Begin(Sample);

        static void Begin(string scene)
        {
            // Open a saved scene only in the expressly launched batch editor.
            if (!Application.isBatchMode) throw new InvalidOperationException("Launch in a separate batch Editor.");
            var args=Environment.GetCommandLineArgs();
            int i=Array.IndexOf(args,"-surfaceOutput");
            if(i<0 || i+1==args.Length) throw new ArgumentException("Pass an unused directory with -surfaceOutput");
            var path=Path.GetFullPath(args[i+1]);
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any()) throw new IOException("Output exists: "+path);
            Directory.CreateDirectory(path);
            SessionState.SetString(Active+".output",path);
            EditorSceneManager.OpenScene(scene);
            SessionState.SetBool(Active,true);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SimulatorBindings()
        {
            if (!SessionState.GetBool(Active,false)) return;
            keyboard=InputSystem.AddDevice<Keyboard>();
            if(Mouse.current==null) mouse=InputSystem.AddDevice<Mouse>();
        }

        static void Mode(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Active,false)) return;
            if(mode==PlayModeStateChange.EnteredPlayMode)
            {
                folder=SessionState.GetString(Active+".output","");
                InputSystem.onBeforeUpdate+=Flush;
                frame=-1;deadline=Time.realtimeSinceStartup+130;run=Visit();
            }
            if(mode==PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Active,false);
                batchEnding=true; exitAt=EditorApplication.timeSinceStartup+5; exitCode=SessionState.GetInt(Active+".exit",1);
            }
        }

        static void Tick()
        {
            if(batchEnding && EditorApplication.timeSinceStartup>=exitAt){batchEnding=false;EditorApplication.Exit(exitCode);return;}
            if(run==null || !EditorApplication.isPlaying || frame==Time.frameCount)return;
            frame=Time.frameCount;
            try
            {
                if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Visit timed out.");
                if (!run.MoveNext()) Finish(null);
                else if (recording && movieFrames != null && Time.unscaledTime >= nextCapture)
                {
                    var eye = Camera.main.transform;
                    string name = recorded++.ToString("D4") + ".png";
                    HelmetDiegeticPreview.CaptureView(Path.Combine(movieFrames,name),eye.position,eye.rotation,55,1.6f);
                    movieTimes.Add(name+" "+Time.unscaledTime.ToString("F5",System.Globalization.CultureInfo.InvariantCulture));
                    nextCapture=Time.unscaledTime+1f/8f;
                }
            }
            catch(Exception e){Finish(e);}
        }

        static void Flush()
        {
            if(InputState.currentUpdateType==InputUpdateType.Dynamic)
                while(input.Count!=0) input.Dequeue()();
        }

        static void Finish(Exception failure)
        {
            if(movieFrames!=null && movieTimes.Count>0) File.WriteAllLines(Path.Combine(movieFrames,"timestamps.txt"),movieTimes);
            run=null; InputSystem.onBeforeUpdate-=Flush; input.Clear();
            if(failure!=null){log.Add("FAILED "+failure);Debug.LogException(failure);}
            else log.Add("PASS");
            File.WriteAllLines(Path.Combine(folder,"observation.txt"),log);
            foreach(var device in new InputDevice[]{hand,head,keyboard,mouse}) if(device!=null && device.added) InputSystem.RemoveDevice(device);
            SessionState.SetInt(Active+".exit",failure!=null?1:0);
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator Visit()
        {
            for(int i=0;i<16;i++)yield return null;
            if(InputSystem.devices.OfType<XRHMD>().Any(d=>!(d is XRSimulatedHMD)))throw new InvalidOperationException("Hardware HMD connected.");
            foreach(var sim in Object.FindObjectsOfType<XRDeviceSimulator>())sim.enabled=false;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            station=Object.FindObjectOfType<HelmetDiegeticStation>();
            call=Object.FindObjectOfType<SurfaceCall>();
            controller=Object.FindObjectsOfType<ActionBasedController>(true).Single(c=>c.name=="Right Controller");
            hand=InputSystem.AddDevice<XRSimulatedController>();InputSystem.SetDeviceUsage(hand,UnityEngine.InputSystem.CommonUsages.RightHand);
            head=InputSystem.AddDevice<XRSimulatedHMD>();
            InputSystem.EnableDevice(keyboard);
            ray=controller.GetComponentsInChildren<XRRayInteractor>(true).First(r=>!r.name.ToLowerInvariant().Contains("teleport"));
            station.Inspection.SoundEnabled=false; station.Inspection.ReduceMotion=false;
            MuseumText.Channel.SetLanguage(MuseumLanguage.English);
            log.Add(Application.unityVersion+"  "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+"  "+SystemInfo.graphicsDeviceName);
            log.Add("Mono 1600x1000, FOV 55 vertical; tracked HMD and right grip; sound disabled; fixed eye positions in world metres.");
            var home=new Vector3(0,1.36f,2.2f);
            Head(home,Quaternion.identity); Hand(new Vector3(.38f,.84f,2.43f),Quaternion.LookRotation(Vector3.down),false);
            for(int i=0;i<14;i++)yield return null;
            // Allow the Editor to finish compiling the material variants used by the mono view.
            ShaderUtil.allowAsyncCompilation=false;
            var warm = Path.Combine(folder,"warmup.png");
            HelmetDiegeticPreview.CaptureView(warm,Camera.main.transform.position,Camera.main.transform.rotation,55,1.6f);
            File.Delete(warm);
            float settled=Time.realtimeSinceStartup+1;
            while(Time.realtimeSinceStartup<settled) yield return null;
            Shot("01-spawn");
            MuseumText.Channel.SetLanguage(MuseumLanguage.Chinese);
            for(int i=0;i<4;i++)yield return null;
            Shot("02-spawn-zh");
            var seat=new Vector3(.01f,1.25f,2.25f);
            var seatRotation=Quaternion.LookRotation(new Vector3(0,1.24f,3.55f)-seat);
            Head(seat,seatRotation);for(int i=0;i<6;i++)yield return null;
            Shot("03-center-seated");
            var side=new Vector3(-3.00f,1.36f,2.33f);
            Head(side,Quaternion.LookRotation(new Vector3(-3.1f,1.38f,3.53f)-side));
            for(int i=0;i<6;i++)yield return null;
            Shot("04-left-exhibit");
            var starboard=new Vector3(3.00f,1.36f,2.33f);
            Head(starboard,Quaternion.LookRotation(new Vector3(3.1f,1.38f,3.53f)-starboard));
            for(int i=0;i<6;i++)yield return null;
            Shot("04-right-exhibit");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=Sample)yield break;
            Assert(call && call.Control && call.surfaceFlag,"saved sample and registered calling key exist");
            Assert(!GameObject.Find("Central question"), "one opening question; old back-wall question is inactive");
            Assert(!station.Inspection.SoundEnabled,"silent inspection");
            Head(seat,seatRotation);for(int i=0;i<8;i++)yield return null;
            MuseumText.Channel.SetLanguage(MuseumLanguage.TraditionalChinese);CheckText();Shot("05-idle-zh-Hant");
            MuseumText.Channel.SetLanguage(MuseumLanguage.Chinese);CheckText();
            var renderers=call.restingTelephone.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;foreach(var surface in renderers) bounds.Encapsulate(surface.bounds);
            log.Add("Resting phone "+bounds.ToString("F4"));
            PhonePose(call.restingTelephone.transform);
            call.key.selectEntered.AddListener(e=>log.Add("XRI selectEntered | "+e.interactorObject.GetType().Name+" | "+e.interactorObject.transform.name));
            call.key.selectExited.AddListener(e=>log.Add("XRI selectExited | "+e.interactorObject.GetType().Name+" | canceled="+e.isCanceled));
            int confirms=call.Control.ConfirmedCount;
            var point=call.key.colliders[0].bounds.center;
            Vector3 wrist=new Vector3(.28f,1.13f,2.54f);
            var aim=Quaternion.LookRotation(point-wrist);
            // An abandoned squeeze must leave the unanswered telephone on its stand.
            Hand(wrist,aim,false); for(int i=0;i<9;i++)yield return null;
            Hand(wrist,aim,true); for(int i=0;i<7;i++)yield return null;
            Assert(call.Control.IsPressed,"cancel fixture reached key; "+Hit());
            Hand(wrist,Quaternion.LookRotation(Vector3.up),true);for(int i=0;i<8;i++)yield return null;
            Hand(wrist,Quaternion.LookRotation(Vector3.up),false);for(int i=0;i<6;i++)yield return null;
            Assert(!call.Control.IsPressed && call.Control.CancelledCount>0 && call.Control.ConfirmedCount==confirms && !station.Demonstration.IsVisible,"moving off while held cancels without a call");
            var args=Environment.GetCommandLineArgs();
            int outIndex=Array.IndexOf(args,"-surfaceFrames");
            if(outIndex>=0)
            {
                movieFrames=Path.GetFullPath(args[outIndex+1]);
                if (Directory.Exists(movieFrames) && Directory.EnumerateFileSystemEntries(movieFrames).Any()) throw new IOException("Use an empty frames directory.");
                Directory.CreateDirectory(movieFrames);
            }
            recording=true;
            float pause=Time.unscaledTime+1f;
            while(Time.unscaledTime<pause)yield return null;
            Shot("06-before");
            Hand(wrist,aim,false);
            pause=Time.unscaledTime+.65f;while(Time.unscaledTime<pause) yield return null;
            Assert(call.key.isHovered,"hover from saved ray; hit="+Hit());
            Shot("07-aim");
            Hand(wrist,aim,true);pause=Time.unscaledTime+.75f;while(Time.unscaledTime<pause)yield return null;
            Assert(sentGripButton && call.Control.IsPressed && call.key.isSelected && call.key.GetOldestInteractorSelecting() is XRRayInteractor && !station.Demonstration.IsVisible && call.Control.ConfirmedCount==confirms,"grip held; no call yet");
            Shot("08-hold");
            Hand(wrist,aim,false);for(int i=0;i<5;i++)yield return null;
            var demo=station.Demonstration;
            Assert(call.Control.ConfirmedCount==confirms+1 && demo.IsVisible && demo.IsPlaying,"grip released exactly once; telephone active");
            Shot("09-release");
            while(demo.NormalizedProgress<.214f)yield return null;
            Assert(demo.PhaseIndex==1 && call.surfaceFlag.activeInHierarchy,"surface sending; "+demo.AnimationStage);
            PhonePose(demo.RefinedEquipment.SurfaceTelephone);
            Assert(!demo.RefinedEquipment.IsAudioPlaying,"visual call while equipment audio remains silent");
            log.Add("Surface cable socket "+demo.VoiceSourceWorld.ToString("F4")+"; helmet socket "+demo.VoiceTargetWorld.ToString("F4")+"; drawn endpoint "+demo.VoiceEndpointWorld.ToString("F4"));
            Assert(Vector3.Distance(demo.VoiceEndpointWorld,demo.VoiceTargetWorld)<.002f,"drawn route meets helmet fitting");
            CheckLanguages("10-surface",true);
            while(demo.PhaseIndex<2)yield return null;
            Shot("11-wait");
            while(demo.NormalizedProgress<.641f)yield return null;
            Assert(demo.PhaseIndex==3 && !call.surfaceFlag.activeSelf && call.words.text==MuseumText.Translate(SurfaceCall.Incoming),"diver replying; "+demo.AnimationStage);
            CheckLanguages("12-diver",true);
            while(demo.IsPlaying)yield return null;
            Assert(demo.IsVisible && demo.PhaseIndex==4 && call.Control.ConfirmedCount==confirms+1,"line remains for inspection");
            CheckLanguages("13-discovery",true);
            pause=Time.unscaledTime+1.2f;while(Time.unscaledTime<pause)yield return null;
            recording=false;
            // A second physical release in reduced motion must show an immediately readable result.
            station.Inspection.ReduceMotion=true;
            Hand(wrist,aim,true);for(int i=0;i<6;i++)yield return null;
            Hand(wrist,aim,false);for(int i=0;i<6;i++)yield return null;
            Assert(call.Control.ConfirmedCount==confirms+2 && demo.IsVisible && !demo.IsPlaying,"replay by grip, static mode");
            CheckLanguages("14-static",false);
        }

        static void PhonePose(Transform model)
        {
            var parts=model.GetComponentsInChildren<Renderer>();
            Vector3 Center(string n)=>parts.First(r=>r.name==n).bounds.center;
            var up=(Center("Cell positive stud")-Center("Dry cell")).normalized;
            var front=Vector3.ProjectOnPlane(Center("Switch gland")-Center("Ash battery box base"),up).normalized;
            Assert(Vector3.Dot(up,Vector3.up)>.98f && Vector3.Dot(front,Vector3.back)>.95f,
                "battery upright and telephone face towards visitor: up "+up.ToString("F3")+" face "+front.ToString("F3"));
        }

        static void Head(Vector3 world,Quaternion rot)
        {
            var parent=Camera.main.transform.parent;var local=parent.InverseTransformPoint(world);var q=Quaternion.Inverse(parent.rotation)*rot;
            var state=new XRSimulatedHMDState {isTracked=true,trackingState=3,devicePosition=local,centerEyePosition=local,
                leftEyePosition=local+q*Vector3.left*.032f,rightEyePosition=local+q*Vector3.right*.032f,deviceRotation=q,centerEyeRotation=q,leftEyeRotation=q,rightEyeRotation=q};
            input.Enqueue(()=>InputSystem.QueueStateEvent(head,state));
        }
        static void Hand(Vector3 world,Quaternion rot,bool grip)
        {
            var parent=controller.transform.parent;
            var state=new XRSimulatedControllerState {isTracked=true,trackingState=3,batteryLevel=1,devicePosition=parent.InverseTransformPoint(world),
                deviceRotation=Quaternion.Inverse(parent.rotation)*rot,grip=grip?1:0}.WithButton(ControllerButton.GripButton,grip);
            input.Enqueue(()=>
            {
                InputSystem.QueueStateEvent(hand,state);
                sentGripButton=grip;
                log.Add("InputSystem.QueueStateEvent on dynamic update | device="+hand.deviceId+" | right gripButton="+grip+
                    " | world wrist="+world.ToString("F3")+" | rotation="+rot.ToString("F4"));
            });
        }
        static string Hit()=>ray.TryGetCurrent3DRaycastHit(out var hit)?hit.collider.transform.parent.name+"/"+hit.collider.name+" @"+hit.point.ToString("F3"):"none";
        static void Assert(bool ok,string fact)
        {
            log.Add((ok?"OK ":"FAIL ")+fact);
            if(!ok)throw new InvalidOperationException(fact);
        }
        static void CheckText()
        {
            foreach(var text in call.surfaceFlag.GetComponentsInChildren<LocalizedText>(true))
            {
                if(!call.surfaceFlag.activeInHierarchy) continue;
                var label=text.GetComponent<TMP_Text>();label.ForceMeshUpdate();
                if(label.text!=MuseumText.Table.Get(text.Key,MuseumText.Channel.Current) || label.isTextOverflowing)
                    throw new InvalidOperationException("Endpoint label "+text.Key);
                foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c)))
                    if(!label.font.characterLookupTable.ContainsKey(c))throw new InvalidOperationException("Endpoint glyph "+c);
            }
            foreach(var label in new[]{call.words,call.speaker,call.keyLegend})
            {
                label.ForceMeshUpdate();
                var binding=label.GetComponent<LocalizedSourceText>();
                if(!binding || label.isTextOverflowing || label.textBounds.size.x>label.rectTransform.rect.width+.004f || label.textBounds.size.y>label.rectTransform.rect.height+.004f)
                    throw new InvalidOperationException("Label overflow/binding: "+label.name+" "+label.text+" "+label.textBounds);
                foreach(char ch in label.text.Where(c=>!char.IsWhiteSpace(c)))
                    if(!label.font.characterLookupTable.ContainsKey(ch)) throw new InvalidOperationException("Missing saved glyph "+ch);
            }
        }
        static void CheckLanguages(string id,bool allShots)
        {
            foreach(var lang in new[]{MuseumLanguage.Chinese,MuseumLanguage.TraditionalChinese,MuseumLanguage.English})
            {
                MuseumText.Channel.SetLanguage(lang);CheckText();
                if(allShots || lang==MuseumLanguage.Chinese) Shot(id+"-"+lang);
                log.Add(lang+" | "+call.speaker.text+" | "+call.words.text.Replace("\n"," / ")+" | "+call.keyLegend.text);
            }
            MuseumText.Channel.SetLanguage(MuseumLanguage.Chinese);
        }
        static void Shot(string name)
        {
            var eye=Camera.main.transform;
            HelmetDiegeticPreview.CaptureView(Path.Combine(folder,name+".png"),eye.position,eye.rotation,55,1.6f);
            string extra=call ? " sentGripButton="+sentGripButton+" xriSelected="+call.key.isSelected+" pressed="+call.Control.IsPressed+" confirmed="+call.Control.ConfirmedCount+
                " ray="+Hit()+" demo="+station.Demonstration.IsVisible+" phase="+station.Demonstration.PhaseIndex+
                " progress="+station.Demonstration.NormalizedProgress.ToString("F3")+" speaker="+call.speaker.text+" words="+call.words.text.Replace("\n"," / "):"";
            log.Add(name+" frame="+Time.frameCount+" time="+Time.unscaledTime.ToString("F3")+" eye="+eye.position.ToString("F4")+" rot="+eye.rotation.ToString("F4")+extra);
        }
    }
}
