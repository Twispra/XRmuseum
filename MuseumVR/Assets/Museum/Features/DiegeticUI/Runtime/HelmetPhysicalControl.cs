using System;
using System.Collections.Generic;
using Museum.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace Museum.Diegetic
{
    /// <summary>Visual intent feedback never changes the target's hit area.</summary>
    public sealed class HelmetPhysicalControl : MonoBehaviour, IXRSelectFilter
    {
        public enum FeedbackState { Resting, Hovered, Pressed, Confirmed }
        [SerializeField, Range(0, 1)] float restingOpacity = .22f;
        [SerializeField, Min(.01f)] float proximity = .18f;
        [SerializeField, Min(0)] float fadeDelay = .65f;
        HelmetDiegeticStation station;
        XRBaseInteractable target;
        Renderer face, originalFace;
        LineRenderer rim;
        LineRenderer[] symbolLines = Array.Empty<LineRenderer>();
        TextMeshPro caption, hint;
        string source, description;
        Action pressed;
        MaterialPropertyBlock block;
        bool pending, emphasis, held;
        float lastIntent = -100, opacity;
        float confirmedUntil = -100, visualTravel, rimWidth;
        IXRSelectInteractor pressInteractor;
        readonly List<IXRSelectInteractor> blockedHands = new List<IXRSelectInteractor>();
        Color tint;
        bool contextualOnly, symbol, externalCaptionPlacement;
        public float Opacity => opacity;
        public XRBaseInteractable Target => target;
        public TMP_Text Caption => caption;
        public TMP_Text Hint => hint;
        public bool ContextualOnly => contextualOnly;
        public bool HasSymbol => symbol;
        public bool IsEmphasized => emphasis;
        public bool IsPressed => held;
        public bool ConfirmationVisible => Time.unscaledTime < confirmedUntil;
        public int ConfirmedCount { get; private set; }
        public int CancelledCount { get; private set; }
        public IXRSelectInteractor PressInteractor => pressInteractor;
        public FeedbackState State { get; private set; }
        public bool HasVisualIntent { get; private set; }
        public event Action<IXRSelectInteractor> PressAbandonedWhileHeld;
        bool IXRSelectFilter.canProcess => true;
        public void ConfigureVisibility(float idle,float radius,float delay)
        {restingOpacity=Mathf.Clamp(idle,.05f,.5f);proximity=Mathf.Clamp(radius,.05f,.5f);fadeDelay=Mathf.Clamp(delay,0,3);opacity=restingOpacity;}
        public void Configure(HelmetDiegeticStation owner, XRBaseInteractable interaction, Renderer surface,
            LineRenderer outline, TextMeshPro label, TextMeshPro explanation, string title, string help, Color color, Action action)
        {
            if(target)
            {
                target.selectEntered.RemoveListener(OnSelected);target.selectExited.RemoveListener(OnReleased);
                target.selectFilters.Remove(this);
            }
            station=owner; target=interaction; originalFace=surface; rim=outline; caption=label; hint=explanation;
            source=title; description=help; tint=color; pressed=action; block=new MaterialPropertyBlock();
            BuildVisualFace();rimWidth=rim?rim.widthMultiplier:.0015f;
            MuseumText.Set(caption, source); MuseumText.Set(hint, description);
            target.selectEntered.AddListener(OnSelected);target.selectExited.AddListener(OnReleased);target.selectFilters.Add(this);
            opacity=restingOpacity; ApplyVisuals();
            target.getDistanceOverride=TargetDistance;
        }
        void BuildVisualFace()
        {
            // The source cube owns the hit collider. Only this mesh-only copy travels on press.
            if(!originalFace)return;
            var mesh=originalFace.GetComponent<MeshFilter>();
            if(!mesh || !mesh.sharedMesh){face=originalFace;return;}
            var visual=new GameObject("Button travel (visual only)");visual.transform.SetParent(transform,false);
            visual.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
            face=visual.AddComponent<MeshRenderer>();face.sharedMaterials=originalFace.sharedMaterials;
            face.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;face.receiveShadows=false;
            originalFace.enabled=false;
            SyncFaceTransform(0);
        }
        public void SetTitle(string value) { if(source==value)return; source=value; MuseumText.Set(caption,value); }
        public void SetHint(string value) { if(description==value)return; description=value; MuseumText.Set(hint,value); }
        public void ConfigureSymbol(LineRenderer[] lines)
        { symbol=true;symbolLines=lines??Array.Empty<LineRenderer>();ApplyVisuals(); }
        public void ConfigureContextualOnly()
        {
            contextualOnly=true;opacity=0;restingOpacity=0;
            if(face)face.enabled=false;if(rim)rim.enabled=false;
            foreach(var line in symbolLines)if(line)line.enabled=false;
            ApplyVisuals();
        }
        public void SetEmphasis(bool value) { emphasis=value; }
        public void UseExternalCaptionPlacement() => externalCaptionPlacement=true;
        void OnSelected(SelectEnterEventArgs args)
        {
            PruneReleasedHands();
            if(held || IsBlocked(args.interactorObject))return;
            pressInteractor=args.interactorObject;held=true;confirmedUntil=-100;
        }
        void OnReleased(SelectExitEventArgs args)
        {
            if(!held || !ReferenceEquals(pressInteractor,args.interactorObject))return;
            var hand=pressInteractor;
            held=false;pressInteractor=null;
            if(pressed==null)return; // Continuous gestures already apply while held.
            bool stillHeld=IsHandAlive(hand) && hand.isSelectActive;
            bool accepted=!args.isCanceled && isActiveAndEnabled && target && target.isActiveAndEnabled &&
                !stillHeld && ReleasedOnThisTarget(hand);
            if(accepted)
            {
                pending=true;ConfirmedCount++;confirmedUntil=Time.unscaledTime+.32f;
            }
            else
            {
                CancelledCount++;
                if(stillHeld)BlockUntilReleased(hand);
            }
        }
        public void CancelPress()
        {
            bool wasPending=pending;
            pending=false;
            if(held)
            {
                var hand=pressInteractor;
                held=false;pressInteractor=null;CancelledCount++;
                if(IsHandAlive(hand) && hand.isSelectActive)BlockUntilReleased(hand);
                confirmedUntil=-100;
            }
            else if(wasPending)confirmedUntil=-100;
        }
        bool ReleasedOnThisTarget(IXRSelectInteractor hand)
        {
            if(!IsHandAlive(hand) || !target)return false;
            if(hand is XRRayInteractor ray)
            {
                if(!ray.TryGetCurrent3DRaycastHit(out var hit))return false;
                foreach(var collider in target.colliders)
                    if(collider && collider.enabled && collider.gameObject.activeInHierarchy && hit.collider==collider)return true;
                return false;
            }
            // Direct interaction uses the physical hand, never the ray attach point retained at a distance.
            Vector3 position=hand.transform.position;
            foreach(var collider in target.colliders)
                if(collider && collider.enabled && collider.gameObject.activeInHierarchy &&
                   (collider.ClosestPoint(position)-position).sqrMagnitude<=.06f*.06f)return true;
            return false;
        }
        void BlockUntilReleased(IXRSelectInteractor hand)
        {
            if(!IsBlocked(hand))blockedHands.Add(hand);
            PressAbandonedWhileHeld?.Invoke(hand);
        }
        void PruneReleasedHands()=>blockedHands.RemoveAll(hand=>!IsHandAlive(hand) || !hand.isSelectActive);
        bool IsBlocked(IXRSelectInteractor hand)
        {
            foreach(var blocked in blockedHands)if(SameHand(blocked,hand))return true;
            return false;
        }
        static bool IsHandAlive(IXRSelectInteractor hand)=>hand!=null && (!(hand is UnityEngine.Object o) || o);
        static bool SameHand(IXRSelectInteractor first,IXRSelectInteractor second)
        {
            if(ReferenceEquals(first,second))return true;
            if(!IsHandAlive(first) || !IsHandAlive(second))return false;
            var a=(first as XRBaseControllerInteractor)?.xrController ?? first.transform.GetComponentInParent<XRBaseController>();
            var b=(second as XRBaseControllerInteractor)?.xrController ?? second.transform.GetComponentInParent<XRBaseController>();
            return a && a==b;
        }
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor,IXRSelectInteractable interactable)
        {PruneReleasedHands();return !IsBlocked(interactor);}
        void LateUpdate()
        {
            if (!station || !target) return;
            PruneReleasedHands();
            if(!station.isActiveAndEnabled){CancelPress();return;}
            if(pressed!=null && held && IsHandAlive(pressInteractor) && pressInteractor.isSelectActive &&
                !ReleasedOnThisTarget(pressInteractor))CancelPress();
            if(pending) { pending=false;if(target.isActiveAndEnabled)pressed?.Invoke(); }
            bool nearby=false;
            foreach(var hit in target.colliders)
                if(hit && hit.enabled && hit.gameObject.activeInHierarchy && station.HandNear(hit,proximity)){nearby=true;break;}
            bool intent=target.isHovered || target.isSelected || ConfirmationVisible || nearby || (!contextualOnly && (emphasis || station.AlwaysShowHints));
            HasVisualIntent=intent;
            if(intent)lastIntent=Time.unscaledTime;
            bool visible=intent || Time.unscaledTime-lastIntent<fadeDelay;
            opacity=Mathf.MoveTowards(opacity,visible?1:restingOpacity,Time.unscaledDeltaTime*4);
            ApplyVisuals();
        }
        void ApplyVisuals()
        {
            bool hover=target && target.isHovered;
            State=held?FeedbackState.Pressed:ConfirmationVisible?FeedbackState.Confirmed:hover?FeedbackState.Hovered:FeedbackState.Resting;
            Color feedback=State==FeedbackState.Confirmed?new Color(.45f,1f,.66f):
                State==FeedbackState.Pressed?Color.Lerp(tint,Color.white,.60f):hover?Color.Lerp(tint,Color.white,.25f):tint;
            float travel=held ? .004f : 0;
            float deltaTravel=travel-visualTravel;
            SyncFaceTransform(travel);
            // Never move the target or the source face: their colliders remain fixed through every state.
            if(rim){rim.transform.localPosition+=Vector3.forward*deltaTravel;rim.widthMultiplier=rimWidth*(held?2.2f:ConfirmationVisible?2.5f:hover?1.6f:1);}
            foreach(var line in symbolLines)if(line)line.transform.localPosition+=Vector3.forward*deltaTravel;
            if(caption && !externalCaptionPlacement)caption.transform.localPosition+=Vector3.forward*deltaTravel;
            if(hint)hint.transform.localPosition+=Vector3.forward*deltaTravel;
            visualTravel=travel;
            if(face)
            {
                face.GetPropertyBlock(block);
                Color surface=Color.Lerp(new Color(.015f,.05f,.065f),feedback,held ? .20f : ConfirmationVisible ? .14f : .02f);
                surface.a=opacity*.86f;block.SetColor("_BaseColor",surface);face.SetPropertyBlock(block);
            }
            if(rim)
            {
                var c=feedback;c.a=opacity;rim.startColor=rim.endColor=Color.white;
                rim.GetPropertyBlock(block);block.SetColor("_BaseColor",c);rim.SetPropertyBlock(block);
            }
            foreach(var line in symbolLines)
            {
                if(!line)continue;
                var color=feedback;color.a=opacity;
                line.GetPropertyBlock(block);block.SetColor("_BaseColor",color);line.SetPropertyBlock(block);
            }
            float textOpacity=symbol?Mathf.InverseLerp(restingOpacity,.85f,opacity):opacity;
            if(caption)caption.color=new Color(feedback.r,feedback.g,feedback.b,textOpacity);
            if(hint)hint.color=new Color(feedback.r,feedback.g,feedback.b,Mathf.InverseLerp(restingOpacity,.85f,opacity));
        }
        void SyncFaceTransform(float travel)
        {
            if(!face || !originalFace || face==originalFace)return;
            face.transform.position=originalFace.transform.position+transform.forward*travel;
            face.transform.rotation=originalFace.transform.rotation;
            face.transform.localScale=originalFace.transform.localScale;
        }
        DistanceInfo TargetDistance(IXRInteractable interactable,Vector3 position)
        {
            var result=new DistanceInfo {distanceSqr=float.MaxValue,point=transform.position};
            foreach(var hit in target.colliders)
            {
                if(!hit || !hit.enabled || !hit.gameObject.activeInHierarchy)continue;
                Vector3 point=hit.ClosestPoint(position);float distance=(position-point).sqrMagnitude;
                if(distance<result.distanceSqr)result=new DistanceInfo{distanceSqr=distance,point=point,collider=hit};
            }
            return result;
        }
        void OnDisable() { CancelPress();confirmedUntil=-100;HasVisualIntent=false; }
        void OnDestroy()
        {
            if(target)
            {
                target.selectEntered.RemoveListener(OnSelected);target.selectExited.RemoveListener(OnReleased);
                target.selectFilters.Remove(this);
            }
        }
    }
}
