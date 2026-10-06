using Museum.Inspection;
using UnityEngine;

namespace Museum.Experience
{
    /// <summary>Quiet, synthesized feedback, not recordings of a historical helmet.</summary>
    [DisallowMultipleComponent]
    public sealed class HelmetMechanicalFeedback : MonoBehaviour
    {
        [SerializeField] bool soundEnabled=true;
        [SerializeField, Range(0,1)] float volume=.15f;
        AudioSource hinge,contact;
        AudioClip hingeClip,openClip,closeClip,returnClip;
        HelmetInspectionController controller;
        float previousAngle,lastMovingAt;
        bool wasMoving;
        public bool SoundEnabled { get=>soundEnabled; set { soundEnabled=value;if(!value)StopSounds(); } }
        public bool HingePlaying => hinge && hinge.isPlaying;
        public int CompletionSounds { get; private set; }
        public void WindowChanged(float angle,bool silent)
        {
            if(!Application.isPlaying)return;
            if(silent){previousAngle=angle;wasMoving=false;StopSounds();return;}
            if(Mathf.Abs(angle-previousAngle)>.015f)
            {
                EnsureAudio();wasMoving=true;lastMovingAt=Time.unscaledTime;
                if(soundEnabled && !hinge.isPlaying)hinge.Play();
            }
            // Ease-out may leave only a tiny final angle delta; endpoint feedback must not depend on speed.
            if(wasMoving && (angle<=.02f || angle>=HelmetInspectionController.MaximumWindowAngle-.02f))
            {
                if(soundEnabled){EnsureAudio();contact.PlayOneShot(angle<=.02f?closeClip:openClip,volume);CompletionSounds++;}
                if(hinge)hinge.Stop();wasMoving=false;
            }
            previousAngle=angle;
        }
        public void ReturnCompleted()
        {
            if(!Application.isPlaying || !soundEnabled)return;
            EnsureAudio();contact.PlayOneShot(returnClip,volume*.65f);CompletionSounds++;
        }
        void Awake(){controller=GetComponent<HelmetInspectionController>();previousAngle=controller?controller.WindowAngle:0;}
        void Update(){if(hinge && hinge.isPlaying && (!soundEnabled || Time.unscaledTime-lastMovingAt>.075f))hinge.Stop();}
        void OnDisable()=>StopSounds();
        void StopSounds(){if(hinge)hinge.Stop();if(contact)contact.Stop();}
        void EnsureAudio()
        {
            if(hinge)return;
            var parent=controller && controller.WindowPivot?controller.WindowPivot:transform;
            var go=new GameObject("Quiet mechanical feedback");go.transform.SetParent(parent,false);
            hinge=go.AddComponent<AudioSource>();contact=go.AddComponent<AudioSource>();
            foreach(var source in new[]{hinge,contact})
            {source.playOnAwake=false;source.spatialBlend=1;source.minDistance=.35f;source.maxDistance=4;source.rolloffMode=AudioRolloffMode.Linear;source.dopplerLevel=0;}
            hingeClip=Synth("Illustrative hinge",.34f,0);openClip=Synth("Illustrative open stop",.11f,1);
            closeClip=Synth("Illustrative closing contact",.15f,2);returnClip=Synth("Illustrative return contact",.12f,3);
            hinge.clip=hingeClip;hinge.loop=true;hinge.volume=volume*.45f;contact.volume=1;
        }
        public static AudioClip Synth(string name,float duration,int kind)
        {
            const int rate=22050;var samples=new float[Mathf.CeilToInt(rate*duration)];var noise=new System.Random(2016+kind);
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                float envelope=kind==0?Mathf.Sin(Mathf.PI*i/(samples.Length-1)):Mathf.Exp(-t*(kind==2?30:45));
                float signal=kind==0?(float)(noise.NextDouble()*2-1)*.12f+Mathf.Sin(t*2*Mathf.PI*130)*.06f
                    :Mathf.Sin(t*2*Mathf.PI*(kind==2?360:kind==3?520:680))*.24f+(float)(noise.NextDouble()*2-1)*.10f;
                samples[i]=signal*envelope;
            }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        void OnDestroy(){if(hinge)Destroy(hinge.gameObject);foreach(var clip in new[]{hingeClip,openClip,closeClip,returnClip})if(clip)Destroy(clip);}
    }
}
