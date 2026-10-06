using System;
using Museum.Inspection;
using Museum.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Museum.Exploration
{
    /// <summary>Illustrative surface equipment and staged connections, not an exact historical reconstruction.</summary>
    [DisallowMultipleComponent]
    public sealed class HelmetConnectionDemonstration : MonoBehaviour
    {
        public const float Duration = 16f;
        const int PathSamples = 41;
        [SerializeField] HelmetInspectionController inspection;
        [SerializeField] Renderer airConnector;
        [SerializeField] Museum.Experience.HelmetRearConnections rearConnections;
        [SerializeField] Material airMaterial;
        [SerializeField] Material voiceMaterial;
        [SerializeField] Material inkMaterial;
        GameObject visuals;
        LineRenderer route, routeAccent, arrow, returnArrow;
        readonly LineRenderer[] waveforms = new LineRenderer[3];
        Transform pulse;
        TextMeshPro sourceText, destinationText, caption;
        HelmetDemonstrationModels models;
        Vector3 airAnchorLocal, pathStart, pathControlA, pathControlB, pathEnd;
        Vector3 diagramRight = Vector3.right, diagramForward = Vector3.forward;
        readonly Vector3[] pathPoints = new Vector3[PathSamples];
        float startedAt;
        Transform viewer;
        bool anchorReady;
        MaterialPropertyBlock attentionBlock;
        static readonly int FocusId=Shader.PropertyToID("_Focus");
        public float SourceEmphasis { get; private set; }
        public float DestinationEmphasis { get; private set; }
        public bool HasReachedDestination { get; private set; }

        public event Action PlaybackChanged;
        public bool IsPlaying { get; private set; }
        public bool IsVisible { get; private set; }
        public HelmetExplorationController.Clue CurrentClue { get; private set; }
        public string AnimationStage { get; private set; } = "Demo hidden";
        public int PhaseIndex { get; private set; }
        public float NormalizedProgress => IsVisible ? Mathf.Clamp01((Time.unscaledTime - startedAt) / Duration) : 0f;
        public Vector3 AirTargetWorld => rearConnections && rearConnections.AirOutlet ? rearConnections.AirOutlet.position : inspection && anchorReady
            ? inspection.transform.TransformPoint(airAnchorLocal) : Vector3.zero;
        public Vector3 AirEndpointWorld => route && route.positionCount > 0 && CurrentClue == HelmetExplorationController.Clue.Air
            ? route.GetPosition(route.positionCount - 1) : AirTargetWorld;
        public Vector3 VoiceTargetWorld => rearConnections && rearConnections.TelephoneOutlet ? rearConnections.TelephoneOutlet.position : models != null ? models.TransducerInlet.position : Vector3.zero;
        public Vector3 VoiceEndpointWorld => route && route.positionCount > 0 && CurrentClue == HelmetExplorationController.Clue.Voice
            ? route.GetPosition(route.positionCount - 1) : VoiceTargetWorld;
        public void ConfigureRearConnections(Museum.Experience.HelmetRearConnections connections)
        { Stop(); rearConnections=connections; }

        public void Configure(HelmetInspectionController target, Renderer connector, Material airMat, Material voiceMat, Material inkMat)
        {
            Stop();
            if (visuals) ReleaseVisuals();
            inspection = target; airConnector = connector;
            rearConnections = target ? target.GetComponent<Museum.Experience.HelmetRearConnections>() : null;
            airMaterial = airMat; voiceMaterial = voiceMat; inkMaterial = inkMat;
            anchorReady = false; CaptureAnchor();
        }

        void Awake() => CaptureAnchor();
        void OnDisable() => Stop();
        void OnDestroy() => ReleaseVisuals();

        public void Play(HelmetExplorationController.Clue clue)
        {
            Stop();
            if (!isActiveAndEnabled || !inspection || !inspection.isActiveAndEnabled ||
                !airMaterial || !voiceMaterial || !inkMaterial ||
                (clue != HelmetExplorationController.Clue.Air && clue != HelmetExplorationController.Clue.Voice)) return;
            CaptureAnchor();
            if (clue == HelmetExplorationController.Clue.Air && !anchorReady) return;
            EnsureVisuals();
            CurrentClue = clue; startedAt = Time.unscaledTime;
            IsPlaying = !inspection.ReduceMotion; IsVisible = true;
            visuals.SetActive(true); models.SetMode(clue == HelmetExplorationController.Clue.Air);
            UpdateVisuals(0f);
            PlaybackChanged?.Invoke();
        }

        public void Stop()
        {
            bool changed = IsPlaying || IsVisible;
            IsPlaying = false; IsVisible = false;
            AnimationStage = "Demo hidden";
            SourceEmphasis=DestinationEmphasis=0;HasReachedDestination=false;SetConnectorFocus(0);
            if(rearConnections)rearConnections.ClearVoice();
            if (visuals) visuals.SetActive(false);
            if (changed) PlaybackChanged?.Invoke();
        }

        void LateUpdate()
        {
            if (!IsVisible) return;
            if (!inspection || !inspection.isActiveAndEnabled) { Stop(); return; }
            float elapsed = Time.unscaledTime - startedAt;
            bool finished = IsPlaying && (elapsed >= Duration || inspection.ReduceMotion);
            if (finished) IsPlaying = false;
            int priorPhase = PhaseIndex;
            UpdateVisuals(Mathf.Min(elapsed, Duration));
            if (finished || priorPhase != PhaseIndex) PlaybackChanged?.Invoke();
        }

        void CaptureAnchor()
        {
            if (anchorReady || !inspection || !airConnector) return;
            // Mesh vertices are baked; the fitting's Transform origin is not its location.
            airAnchorLocal = inspection.transform.InverseTransformPoint(airConnector.bounds.center);
            anchorReady = true;
        }

        void EnsureVisuals()
        {
            if (visuals) return;
            visuals = new GameObject("Connection schematic visuals");
            visuals.transform.SetParent(transform, false);
            route = MakeLine(visuals.transform, "Connection route", .015f, inkMaterial);
            route.numCapVertices = 4;
            routeAccent = MakeLine(visuals.transform, "Hose direction guide", .003f, airMaterial);
            arrow = MakeLine(visuals.transform, "Direction arrow", .007f, airMaterial);
            returnArrow = MakeLine(visuals.transform, "Return direction arrow", .007f, voiceMaterial);
            pulse = new GameObject("Travelling signal").transform;
            pulse.SetParent(visuals.transform, false);
            // The supplied helmet material already references a working, shaded shader.
            // Clones in the model factory explicitly disable section/hover properties.
            var template = airConnector && airConnector.sharedMaterial ? airConnector.sharedMaterial : inkMaterial;
            models = new HelmetDemonstrationModels(visuals.transform, pulse, template, airMaterial, voiceMaterial);
            for(int i=0;i<waveforms.Length;i++)
            {
                waveforms[i] = MakeLine(pulse, "Voice waveform " + i, .006f, voiceMaterial);
                waveforms[i].positionCount = 31;
            }
            sourceText = MakeText("Source label", new Vector2(.23f,.045f), .42f);
            destinationText = MakeText("Destination label", new Vector2(.23f,.045f), .42f);
            caption = MakeText("Schematic caption", new Vector2(.52f,.073f), .48f);
            if (Camera.main) viewer = Camera.main.transform;
            visuals.SetActive(false);
        }

        void UpdateVisuals(float elapsed)
        {
            bool air = CurrentClue == HelmetExplorationController.Clue.Air;
            bool staticDiagram = inspection.ReduceMotion || !IsPlaying;
            if (!viewer && Camera.main) viewer = Camera.main.transform;
            diagramRight = viewer ? Vector3.ProjectOnPlane(viewer.right, Vector3.up).normalized : Vector3.right;
            if (diagramRight.sqrMagnitude < .1f) diagramRight = Vector3.right;
            diagramForward = Vector3.Cross(diagramRight, Vector3.up).normalized;
            Quaternion facing = Quaternion.LookRotation(diagramForward, Vector3.up);
            Vector3 centre = inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            models.Position(centre, facing);
            // The real helmet fitting is the receiving end. Keep the old detached schematic only as a fallback.
            models.DiverTransducer.gameObject.SetActive(!rearConnections);
            SetPhase(air, elapsed, staticDiagram);

            if (air)
            {
                pathStart = models.AirOutlet.position; pathEnd = AirTargetWorld;
                pathControlA = pathStart - diagramRight * .13f + Vector3.up * .07f - diagramForward * .06f;
                pathControlB = pathEnd + diagramRight * .06f + Vector3.up * .13f;
            }
            else
            {
                pathStart = models.TelephoneOutlet.position; pathEnd = VoiceTargetWorld;
                pathControlA = pathStart + diagramRight * .065f - Vector3.up * .11f - diagramForward * .045f;
                pathControlB = rearConnections ? pathEnd + inspection.transform.forward*.065f+Vector3.up*.025f
                    : pathEnd - diagramRight * .04f - Vector3.up * .11f - diagramForward * .045f;
            }
            route.startWidth = route.endWidth = air ? .015f : .009f;
            route.positionCount = PathSamples; routeAccent.positionCount = PathSamples;
            routeAccent.sharedMaterial = air ? airMaterial : voiceMaterial;
            for(int i=0;i<PathSamples;i++)
            {
                float t=i/(float)(PathSamples-1); pathPoints[i]=PointOnPath(t);
                route.SetPosition(i,pathPoints[i]);
                // A fine contrasting seam helps the cable remain readable against the equipment.
                routeAccent.SetPosition(i,pathPoints[i]-diagramForward*.008f);
            }

            bool airFlow = air && !staticDiagram && PhaseIndex == 1;
            bool sending = !air && !staticDiagram && PhaseIndex == 1;
            bool replying = !air && !staticDiagram && PhaseIndex == 3;
            UpdateAttention(air,elapsed,staticDiagram);
            models.AnimatePump(elapsed, airFlow);
            // Local speech happens at emission; after arrival it no longer competes with the receiving end.
            models.AnimateVoice(elapsed, sending && elapsed-2f<1.8f, replying && elapsed-9f<1.8f, !staticDiagram);
            models.AnimateReception(elapsed, replying && HasReachedDestination, sending && HasReachedDestination);
            pulse.gameObject.SetActive(airFlow || sending || replying);
            if (air) UpdateAirPackets(elapsed - 2f, airFlow);
            else UpdateVoicePackets(elapsed, replying, sending || replying);
            if(air || (!sending && !replying)) foreach(var waveform in waveforms) waveform.gameObject.SetActive(false);

            arrow.sharedMaterial = air ? airMaterial : voiceMaterial;
            arrow.gameObject.SetActive(true);
            bool showBoth = !air && (staticDiagram || PhaseIndex == 0 || PhaseIndex == 2 || PhaseIndex == 4);
            returnArrow.gameObject.SetActive(showBoth);
            SetArrow(arrow, .73f, replying ? -1f : 1f);
            if(showBoth) SetArrow(returnArrow, .28f, -1f);

            MuseumText.Set(sourceText, air ? "Air pump" : "Surface telephone");
            MuseumText.Set(destinationText, air ? "Air inlet" : rearConnections ? "Telephone connection" : "Diver unit");
            sourceText.transform.position = (air ? models.Pump.position : models.SurfaceTelephone.position) + Vector3.up * .31f;
            destinationText.transform.position = air || rearConnections ? pathEnd + Vector3.up*.09f : models.DiverTransducer.position + Vector3.up*.24f;
            MuseumText.Set(caption, AnimationStage + "\n<size=75%>Illustrative model</size>");
            caption.transform.position = centre + diagramRight * .43f + Vector3.up * .43f;
            FaceViewer(sourceText.transform); FaceViewer(destinationText.transform); FaceViewer(caption.transform);
        }

        void UpdateAttention(bool air,float elapsed,bool stationary)
        {
            float phaseTime=elapsed-(air?2:PhaseIndex==3?9:2);
            float arrival=air?.25f+1f/.22f:3.7f;
            HasReachedDestination=!stationary && (air?PhaseIndex==1:PhaseIndex==1 || PhaseIndex==3) && phaseTime>=arrival;
            float source=.12f,destination=.12f;
            if(stationary || (air?PhaseIndex==2:PhaseIndex==4)){source=destination=.25f;}
            else if(PhaseIndex==0){source=.6f;destination=.12f;}
            else if(!air && PhaseIndex==2){source=.12f;destination=.55f;}
            else
            {
                // The sender acts first; receiving attention follows the actual path traversal.
                float sender=HasReachedDestination?.25f:.7f;
                float receiver=HasReachedDestination?.75f:.08f;
                if(!air && PhaseIndex==3){source=receiver;destination=sender;}
                else{source=sender;destination=receiver;}
            }
            SourceEmphasis=source;DestinationEmphasis=destination;
            models.SetAttention(air,source,destination);SetConnectorFocus(air?destination:0);
            if(rearConnections)
            {
                bool responding=!air && (stationary || PhaseIndex==2 || (PhaseIndex==1 && HasReachedDestination) || (PhaseIndex==3 && phaseTime<1.8f));
                rearConnections.ShowVoice(air?0:destination,responding,!stationary,phaseTime);
            }
            sourceText.color=Color.Lerp(new Color(.68f,.76f,.78f),Color.white,source);
            destinationText.color=Color.Lerp(new Color(.68f,.76f,.78f),Color.white,destination);
        }
        void SetConnectorFocus(float value)
        {
            if(!airConnector || !airConnector.sharedMaterial || !airConnector.sharedMaterial.HasProperty(FocusId))return;
            if(attentionBlock==null)attentionBlock=new MaterialPropertyBlock();
            airConnector.GetPropertyBlock(attentionBlock);attentionBlock.SetFloat(FocusId,value);airConnector.SetPropertyBlock(attentionBlock);
        }

        void SetPhase(bool air,float elapsed,bool staticDiagram)
        {
            if (staticDiagram)
            {
                PhaseIndex = air ? 2 : 4;
                AnimationStage = air ? "Pump > helmet / static view" : "Two-way voice / static view";
                return;
            }
            if (air)
            {
                PhaseIndex = elapsed < 2f ? 0 : elapsed < 13f ? 1 : 2;
                AnimationStage = PhaseIndex == 0 ? "Pump and hose"
                    : PhaseIndex == 1 ? "Air: pump > helmet"
                    : "Air supplied from the surface";
            }
            else
            {
                PhaseIndex = elapsed < 2f ? 0 : elapsed < 7f ? 1 : elapsed < 9f ? 2 : elapsed < 14f ? 3 : 4;
                switch(PhaseIndex)
                {
                    case 0: AnimationStage="Surface and diver";break;
                    case 1: AnimationStage="Message: surface > diver";break;
                    case 2: AnimationStage="Waiting for a reply";break;
                    case 3: AnimationStage="Reply: diver > surface";break;
                    default: AnimationStage="A two-way connection";break;
                }
            }
        }

        void UpdateAirPackets(float seconds,bool moving)
        {
            for(int i=0;i<models.AirPackets.Length;i++)
            {
                var packet=models.AirPackets[i];packet.gameObject.SetActive(moving);
                if(!moving) continue;
                // Stagger emission from the pump, instead of seeding packets along the entire hose at start.
                float emittedAt=seconds-i*.55f;
                packet.gameObject.SetActive(emittedAt>=.25f);
                if(emittedAt<.25f)continue;
                float t=Mathf.Repeat((emittedAt-.25f)*.22f,1f);
                packet.SetPositionAndRotation(PointOnPath(t)-diagramForward*.016f,
                    Quaternion.LookRotation(PathDirection(t),Vector3.up));
            }
        }

        void UpdateVoicePackets(float elapsed,bool reply,bool moving)
        {
            if(!moving) return;
                float phaseTime=elapsed-(reply?9f:2f);
            for(int wave=0;wave<waveforms.Length;wave++)
            {
                bool active=phaseTime>=wave*.65f && phaseTime<=wave*.65f+4.1f;
                waveforms[wave].gameObject.SetActive(active);
                if(!active)continue;
                // Each train crosses once per speaking phase. No end-to-start jump can read as a reply.
                float progress=Mathf.Clamp01((phaseTime-wave*.65f)/3.7f);
                float center=reply?1f-progress:progress;
                for(int i=0;i<waveforms[wave].positionCount;i++)
                {
                    float u=i/(float)(waveforms[wave].positionCount-1);
                    float t=Mathf.Clamp01(center+(u-.5f)*.22f);
                    float envelope=Mathf.Sin(u*Mathf.PI);
                    float amplitude=Mathf.Sin(u*Mathf.PI*8)*.014f*envelope;
                    waveforms[wave].SetPosition(i,PointOnPath(t)+Vector3.up*amplitude-diagramForward*.014f);
                }
            }
        }

        Vector3 PointOnPath(float t)
        {
            float u=1f-t;
            return u*u*u*pathStart+3*u*u*t*pathControlA+3*u*t*t*pathControlB+t*t*t*pathEnd;
        }
        Vector3 PathDirection(float t)
        {
            Vector3 direction=PointOnPath(Mathf.Min(1f,t+.012f))-PointOnPath(Mathf.Max(0f,t-.012f));
            return direction.sqrMagnitude>.000001f?direction.normalized:diagramRight;
        }
        void SetArrow(LineRenderer line,float t,float sign)
        {
            Vector3 direction=PathDirection(t)*sign;
            Vector3 across=Vector3.Cross(direction,diagramForward).normalized;
            if(across.sqrMagnitude<.001f) across=Vector3.up;
            Vector3 tip=PointOnPath(t)-diagramForward*.018f;
            line.positionCount=3;
            line.SetPosition(0,tip-direction*.028f+across*.015f);
            line.SetPosition(1,tip);
            line.SetPosition(2,tip-direction*.028f-across*.015f);
        }

        static LineRenderer MakeLine(Transform parent,string name,float width,Material material)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);
            line.useWorldSpace=true;line.sharedMaterial=material;line.startWidth=width;line.endWidth=width;
            line.numCornerVertices=3;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        TextMeshPro MakeText(string name,Vector2 size,float font)
        {
            var text=new GameObject(name).AddComponent<TextMeshPro>();text.transform.SetParent(visuals.transform,false);
            text.font=TMP_Settings.defaultFontAsset;text.color=Color.white;text.alignment=TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta=size;text.enableWordWrapping=false;
            text.enableAutoSizing=true;text.fontSizeMin=.02f;text.fontSizeMax=font;
            text.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Opaque text backing";
            backing.transform.SetParent(text.transform,false);backing.transform.localPosition=Vector3.forward*.012f;
            backing.transform.localScale=new Vector3(size.x+.024f,size.y+.014f,.006f);
            var hit=backing.GetComponent<Collider>();hit.enabled=false;DestroyOwned(hit);
            var renderer=backing.GetComponent<Renderer>();renderer.sharedMaterial=inkMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return text;
        }
        void FaceViewer(Transform label)
        {
            if(!viewer)return;Vector3 forward=label.position-viewer.position;
            if(forward.sqrMagnitude>.0001f)label.rotation=Quaternion.LookRotation(forward,Vector3.up);
        }
        void ReleaseVisuals()
        {
            IsPlaying=false;IsVisible=false;
            if(visuals)DestroyOwned(visuals);
            visuals=null;models?.Dispose();models=null;
        }
        static void DestroyOwned(UnityEngine.Object target)
        {if(!target)return;if(Application.isPlaying)Destroy(target);else DestroyImmediate(target);}
    }
}
