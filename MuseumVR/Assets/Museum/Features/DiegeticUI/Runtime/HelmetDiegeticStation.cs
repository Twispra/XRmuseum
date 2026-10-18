using System;
using System.Collections.Generic;
using System.Linq;
using Museum.Eras;
using Museum.Exploration;
using Museum.Inspection;
using Museum.Localization;
using Museum.Refined;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace Museum.Diegetic
{
    /// <summary>Optional station presentation; existing specimen, clipping, language and demo engines remain authoritative.</summary>
    [DefaultExecutionOrder(210), DisallowMultipleComponent]
    public sealed partial class HelmetDiegeticStation : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        public enum Command { Rotate, Window, Section, Slide, Air, Voice, Reset, PreviousEra, NextEra,
            Hints, Settings, About, Chinese, Traditional, English, Sound, Motion, AlwaysHints, ResetView, CloseDemo,
            HeightUp, HeightDown, HeightReset }
        [SerializeField] HelmetInspectionController inspection;
        [SerializeField] HelmetEraSwitcher eras;
        [SerializeField] HelmetExplorationController exploration;
        [SerializeField] HelmetConnectionDemonstration demonstration;
        [SerializeField] Material uiMaterial;
        [SerializeField] Transform viewer;
        [SerializeField,Range(.05f,.5f)] float idleOpacity=.22f;
        [SerializeField,Range(.05f,.5f)] float handRevealDistance=.18f;
        [SerializeField,Range(0,3)] float fadeOutDelay=.65f;
        [SerializeField,Range(.02f,.10f)] float directContactDistance=.055f;
        [SerializeField,Range(.15f,1f)] float nearHintDistance=.45f;
        [SerializeField,Range(8f,30f)] float maximumHintAngle=18f;
        [SerializeField,Range(1f,30f)] float rotationLearningAngle=5f;
        readonly Dictionary<Command, HelmetPhysicalControl> controls = new Dictionary<Command, HelmetPhysicalControl>();
        readonly Dictionary<GameObject,bool> previousVisibility = new Dictionary<GameObject,bool>();
        readonly List<XRBaseInteractable> guardedTargets = new List<XRBaseInteractable>();
        readonly HashSet<HelmetPhysicalControl> additionalControls = new HashSet<HelmetPhysicalControl>();
        IEnumerable<HelmetPhysicalControl> AllControls => controls.Values.Concat(additionalControls.Where(c => c));
        public bool HasActiveGesture => (RotationGesture && RotationGesture.IsDragging) ||
            (SectionGesture && SectionGesture.IsDragging) || equipmentControls.Any(c => c.Gesture && c.Gesture.IsDragging);

        /// <summary>Enroll runtime extension buttons in the station's same-hand, contact and recovery guards.</summary>
        public void RegisterControl(HelmetPhysicalControl control)
        {
            if (!control || controls.Values.Contains(control) || !additionalControls.Add(control)) return;
            control.PressAbandonedWhileHeld += GuardAbandonedPress;
            Guard(control.Target);
        }
        public void UnregisterControl(HelmetPhysicalControl control)
        {
            if (!control || !additionalControls.Remove(control)) return;
            if (control.Target)
                foreach (var hand in control.Target.interactorsSelecting.ToArray()) GuardAbandonedPress(hand);
            control.CancelPress();
            control.PressAbandonedWhileHeld -= GuardAbandonedPress;
            if (control.Target)
            {
                control.Target.selectFilters.Remove(this);
                control.Target.hoverFilters.Remove(this);
                guardedTargets.Remove(control.Target);
            }
        }
        readonly Dictionary<Behaviour,bool> previousEnabled = new Dictionary<Behaviour,bool>();
        readonly List<IXRSelectInteractor> cancelledHands=new List<IXRSelectInteractor>();
        Transform exhibitUI, pedestalUI, sectionDock, sectionRail;
        LineRenderer railLine, gripRing;
        LineRenderer[] railStops;
        Renderer hoveredShell;
        float previousShellHover;
        Vector3 gripPointLocal;
        Quaternion bodyGripStart;
        bool bodyWasSelected;
        const float RailHalfTravel=.15f;
        GameObject options, about;
        TextMeshPro aboutText, eraTitle, instruction;
        XRBaseController[] hands;
        XRGrabInteractable legacyGrab;
        Rigidbody body;
        RefinedHelmetBinding binding;
        VisitorViewRecovery recovery;
        public VisitorViewHeight ViewHeight { get; private set; }
        Transform knowledgeCard, originalCardParent;
        Vector3 originalCardPosition;
        Quaternion originalCardRotation;
        Quaternion homeRotation, resetFrom;
        Vector3 homePosition, fixedCenter;
        bool ready, resetting, oldKinematic, oldGravity, wasCut;
        int observedEra=-1, observedReset;
        float resetStarted, pulseUntil;
        MeshRenderer[] capRenderers=Array.Empty<MeshRenderer>();
        MaterialPropertyBlock capBlock;
        readonly Color teal = new Color(.2f,.88f,.82f), amber=new Color(1,.68f,.32f);
        public bool AlwaysShowHints { get; private set; }
        public bool HintsPinned { get; private set; }
        public bool IsReady => ready;
        public bool SettingsVisible => options && options.activeSelf;
        public bool AboutVisible => about && about.activeSelf;
        public HelmetGestureInteractable RotationGesture { get; private set; }
        public HelmetGestureInteractable SectionGesture { get; private set; }
        public HelmetRotationHint BodyRotationHint { get; private set; }
        public IReadOnlyList<HelmetRotationHint> EquipmentRotationHints => equipmentControls.Select(c=>c.Hint).Where(h=>h).ToArray();
        public bool RotationHelpLearned { get; private set; }
        public bool RotationHintsCondensed => RotationHelpLearned && !AlwaysShowHints;
        public float NearHintDistance => nearHintDistance;
        public float MaximumHintAngle => maximumHintAngle;
        public HelmetInspectionController Inspection => inspection;
        public HelmetConnectionDemonstration Demonstration => demonstration;
        public Transform SectionRail => sectionRail;
        public float SectionRailHalfTravel => RailHalfTravel;
        public IReadOnlyDictionary<Command,HelmetPhysicalControl> Controls => controls;
        bool IXRSelectFilter.canProcess => ready;
        bool IXRHoverFilter.canProcess => ready;
        public void Configure(HelmetInspectionController target, HelmetEraSwitcher selector,
            HelmetExplorationController clues, HelmetConnectionDemonstration demo, Material material, Transform camera)
        { inspection=target; eras=selector; exploration=clues; demonstration=demo; uiMaterial=material; viewer=camera; }

        void Start() => Initialize();
        public void Initialize()
        {
            if(ready || !inspection || !eras || !exploration || !demonstration || !uiMaterial)return;
            capBlock=new MaterialPropertyBlock();
            if(!viewer && Camera.main)viewer=Camera.main.transform;
            legacyGrab=inspection.GetComponent<XRGrabInteractable>();body=inspection.GetComponent<Rigidbody>();
            binding=inspection.GetComponent<RefinedHelmetBinding>();
            homePosition=inspection.GetComponent<HelmetExhibitReset>().HomePose.position;
            homeRotation=inspection.GetComponent<HelmetExhibitReset>().HomePose.rotation;
            oldKinematic=body.isKinematic;oldGravity=body.useGravity;
            hands=FindObjectsOfType<XRBaseController>(true);
            // Only old control presentation is retired; the exploration/demo engine remains active.
            foreach(var palette in FindObjectsOfType<HelmetInspectionPalette>(true))HideLegacy(palette.gameObject);
            foreach(var clue in inspection.GetComponentsInChildren<HelmetExplorationHotspot>(true))SetLegacyEnabled(clue,false);
            foreach(var button in FindObjectsOfType<HelmetEraButton>(true))HideLegacy(button.gameObject);
            var oldReset=GameObject.Find("Helmet Reset Button");if(oldReset)HideLegacy(oldReset);
            var oldResetLabel=GameObject.Find("Reset Button Label");if(oldResetLabel)HideLegacy(oldResetLabel);
            SetLegacyEnabled(legacyGrab,false);
            foreach(var window in inspection.GetComponentsInChildren<HelmetWindowInteractable>(true))SetLegacyEnabled(window,false);
            foreach(var guide in inspection.GetComponentsInChildren<HelmetSectionInteractable>(true))SetLegacyEnabled(guide,false);
            body.isKinematic=true;body.useGravity=false;
            exhibitUI=new GameObject("Exhibit operation markers").transform;exhibitUI.SetParent(transform,false);
            pedestalUI=new GameObject("Pedestal edge controls").transform;pedestalUI.SetParent(transform,false);
            var plinth=FindObjectsOfType<MeshRenderer>().Where(r=>r.name=="Plinth")
                .OrderBy(r=>Mathf.Abs(r.bounds.center.x-homePosition.x)).FirstOrDefault();
            // Mount outside the actual front edge, rather than burying buttons below the top mesh.
            pedestalUI.position=plinth?new Vector3(homePosition.x,plinth.bounds.max.y+.16f,plinth.bounds.min.z-.10f)
                :new Vector3(homePosition.x,homePosition.y+.04f,homePosition.z-.48f);
            pedestalUI.rotation=Quaternion.Euler(20,0,0);
            BuildPedestalControls();BuildExhibitControls();
            var tour=inspection.GetComponentInChildren<Museum.Hotspots.HelmetHotspotTour>(true);
            var information=tour?tour.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="Information"):null;
            if(information)
            {
                knowledgeCard=information.transform.parent;originalCardParent=knowledgeCard.parent;
                originalCardPosition=knowledgeCard.localPosition;originalCardRotation=knowledgeCard.localRotation;
                PlaceKnowledgeCard();
            }
            foreach(var target in inspection.GetComponentsInChildren<XRBaseInteractable>(true))Guard(target);
            foreach(var control in controls.Values)Guard(control.Target);
            ViewHeight=GetComponent<VisitorViewHeight>();if(!ViewHeight)ViewHeight=gameObject.AddComponent<VisitorViewHeight>();
            ViewHeight.enabled=true;ViewHeight.Configure(FindObjectOfType<XROrigin>());
            recovery=gameObject.AddComponent<VisitorViewRecovery>();recovery.Configure(FindObjectOfType<XROrigin>(),null);
            demonstration.PlaybackChanged+=RefreshEquipmentInteractions;
            gameObject.AddComponent<HelmetViewRecoveryGuard>().Configure(this);
            ready=true;observedReset=inspection.ResetCount;observedEra=eras.CurrentIndex;
            fixedCenter=inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            RefreshLabels();PositionExhibitControls();
        }
        void PlaceKnowledgeCard()
        { if(knowledgeCard){knowledgeCard.SetParent(pedestalUI,false);knowledgeCard.localPosition=new Vector3(.64f,.015f,-.03f);knowledgeCard.localRotation=Quaternion.identity;} }
        void HideLegacy(GameObject obj)
        { if(!previousVisibility.ContainsKey(obj))previousVisibility[obj]=obj.activeSelf;obj.SetActive(false); }
        void SetLegacyEnabled(Behaviour obj,bool value)
        { if(!obj)return;if(!previousEnabled.ContainsKey(obj))previousEnabled[obj]=obj.enabled;obj.enabled=value; }
        void Guard(XRBaseInteractable target)
        {
            if(!target || guardedTargets.Contains(target))return;
            target.selectFilters.Add(this);
            if(IsStationTarget(target))target.hoverFilters.Add(this);
            guardedTargets.Add(target);
        }
        void GuardAbandonedPress(IXRSelectInteractor hand)
        {if(hand!=null && hand.isSelectActive && !cancelledHands.Contains(hand))cancelledHands.Add(hand);}
        bool IXRHoverFilter.Process(IXRHoverInteractor interactor,IXRHoverInteractable target)
        {
            if(!isActiveAndEnabled)return true;
            if(interactor is XRDirectInteractor && !DirectContact(interactor,target as XRBaseInteractable))return false;
            if(interactor is XRRayInteractor ray)
            {
                if(interactor is IXRSelectInteractor selected && target is XRBaseInteractable held && held.IsSelected(selected))return true;
                return FrontRayTarget(ray,target as XRBaseInteractable);
            }
            if(ReferenceEquals(target,RotationGesture) && interactor is IXRSelectInteractor selector && !RotationGesture.IsSelected(selector))
                foreach(var other in guardedTargets)
                    if(other && other!=RotationGesture && other.isActiveAndEnabled && TouchesTarget(selector,other))return false;
            return true;
        }
        static bool FrontRayTarget(XRRayInteractor ray,XRBaseInteractable target)
        {
            if(!ray || !target || !ray.TryGetCurrent3DRaycastHit(out var hit))return false;
            return target.colliders.Contains(hit.collider);
        }
        bool DirectContact(IXRInteractor interactor,XRBaseInteractable target)
        {
            if(!target)return false;
            if(interactor is IXRSelectInteractor selector && target.IsSelected(selector))return true;
            return HelmetDirectContact.Touches(interactor,target,directContactDistance);
        }
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor,IXRSelectInteractable target)
        {
            PruneReleasedHands();
            foreach(var previous in cancelledHands)if(SameHand(previous,interactor))return false;
            if(!isActiveAndEnabled)return true;
            if(interactor is XRDirectInteractor && IsStationTarget(target as XRBaseInteractable) &&
               !DirectContact(interactor,target as XRBaseInteractable))return false;
            if(resetting || inspection.IsReturning)return false;
            if(target is HelmetGestureInteractable &&
               !(target is XRBaseInteractable current && current.IsSelected(interactor)) &&
               AllControls.Any(c=>!ReferenceEquals(c.Target,target) && c.IsPressed))return false;
            if(RotationGesture && RotationGesture.IsDragging && !ReferenceEquals(target,RotationGesture))return false;
            if(SectionGesture && SectionGesture.IsDragging && !ReferenceEquals(target,SectionGesture))return false;
            if(equipmentControls.Any(c=>c.Gesture && c.Gesture.IsDragging && !ReferenceEquals(target,c.Gesture)))return false;
            if(interactor is XRRayInteractor ray && IsStationTarget(target as XRBaseInteractable) &&
               !(target is XRBaseInteractable held && held.IsSelected(interactor)) && !FrontRayTarget(ray,target as XRBaseInteractable))return false;
            if(ReferenceEquals(target,RotationGesture))
            {
                if(RotationGesture.IsSelected(interactor))return true;
                if(interactor is XRRayInteractor bodyRay)return FrontRayTarget(bodyRay,RotationGesture);
                // A grip over an existing knowledge hotspot or a control always belongs to that target.
                foreach(var other in guardedTargets)
                    if(other && other!=RotationGesture && other.isActiveAndEnabled &&
                       (other.isSelected || TouchesTarget(interactor,other)))return false;
            }
            return true;
        }
        static bool TouchesTarget(IXRSelectInteractor hand,XRBaseInteractable target)
        {
            if(hand is XRDirectInteractor)return HelmetDirectContact.Touches(hand,target,.025f);
            Vector3 point=hand.transform.position;float tolerance=.025f;
            if(hand is XRRayInteractor ray)
            {
                if(!ray.TryGetCurrent3DRaycastHit(out var hit))return false;
                point=hit.point;tolerance=.003f;
            }
            foreach(var collider in target.colliders)
                if(collider && collider.enabled && collider.gameObject.activeInHierarchy &&
                   (collider.ClosestPoint(point)-point).sqrMagnitude<tolerance*tolerance)return true;
            return false;
        }
        public bool HandNear(Vector3 point,float radius)
        {
            if(hands==null)return false;
            foreach(var hand in hands)if(hand && hand.isActiveAndEnabled && (hand.transform.position-point).sqrMagnitude<radius*radius)return true;
            return false;
        }
        public bool HandNear(Collider surface,float radius)
        {
            if(!surface || hands==null)return false;
            foreach(var hand in hands)
                if(hand && hand.isActiveAndEnabled &&
                   (surface.ClosestPoint(hand.transform.position)-hand.transform.position).sqrMagnitude<radius*radius)return true;
            return false;
        }
        public void Execute(Command command)
        {
            if(!ready || resetting || inspection.IsReturning)return;
            bool cancels=command==Command.Reset || command==Command.NextEra || command==Command.PreviousEra ||
                command==Command.ResetView || command==Command.CloseDemo || command==Command.HeightUp ||
                command==Command.HeightDown || command==Command.HeightReset;
            if((RotationGesture.IsDragging || SectionGesture.IsDragging || equipmentControls.Any(c=>c.Gesture.IsDragging)) && !cancels)return;
            if(cancels)CancelGestures();
            switch(command)
            {
                case Command.Window: inspection.ToggleWindow();break;
                case Command.Section:
                    CancelGestures();inspection.ToggleCutaway();
                    if(inspection.IsCutaway)pulseUntil=Time.unscaledTime+2f;break;
                case Command.Air: ToggleDemo(HelmetExplorationController.Clue.Air);break;
                case Command.Voice: ToggleDemo(HelmetExplorationController.Clue.Voice);break;
                case Command.Reset: ResetHelmet();break;
                case Command.NextEra: ChangeEra(1);break;
                case Command.PreviousEra: ChangeEra(-1);break;
                case Command.Hints: HintsPinned=!HintsPinned;AlwaysShowHints=HintsPinned;break;
                case Command.Settings: options.SetActive(!options.activeSelf);about.SetActive(false);break;
                case Command.About: about.SetActive(!about.activeSelf);options.SetActive(false);break;
                case Command.Chinese: MuseumText.Channel?.SetLanguage(MuseumLanguage.Chinese);break;
                case Command.Traditional: MuseumText.Channel?.SetLanguage(MuseumLanguage.TraditionalChinese);break;
                case Command.English: MuseumText.Channel?.SetLanguage(MuseumLanguage.English);break;
                case Command.Sound: inspection.SoundEnabled=!inspection.SoundEnabled;break;
                case Command.Motion: inspection.ReduceMotion=!inspection.ReduceMotion;break;
                case Command.AlwaysHints: AlwaysShowHints=!AlwaysShowHints;HintsPinned=AlwaysShowHints;break;
                case Command.ResetView: recovery.ResetView();break;
                case Command.CloseDemo: exploration.Stop();break;
                case Command.HeightUp: ViewHeight.Raise();break;
                case Command.HeightDown: ViewHeight.Lower();break;
                case Command.HeightReset: ViewHeight.ResetHeight();break;
            }
            RefreshLabels();
        }
        void ToggleDemo(HelmetExplorationController.Clue clue)
        {
            if(demonstration.IsVisible && demonstration.CurrentClue==clue)exploration.Stop();
            else { exploration.SelectClue(clue);if(!demonstration.IsVisible || demonstration.CurrentClue!=clue)exploration.Replay(); }
        }
        public void ResetHelmet()
        {
            CancelGestures();exploration.Stop();resetFrom=inspection.transform.rotation;
            inspection.ResetInspection();observedReset=inspection.ResetCount;
            fixedCenter=inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            resetting=!inspection.ReduceMotion;resetStarted=Time.unscaledTime;
            if(resetting)SetRootRotation(resetFrom);
        }
        void ChangeEra(int step)
        {
            CancelGestures();exploration.Stop();inspection.ResetInspection();observedReset=inspection.ResetCount;
            eras.ShowEra((eras.CurrentIndex+step+eras.AvailableEraCount)%eras.AvailableEraCount);
            if(binding)binding.RefreshVisualBinding();
            observedEra=eras.CurrentIndex;
            fixedCenter=inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            RefreshLabels();
        }
        void CancelGestures()
        {
            foreach(var control in AllControls)
            {
                foreach(var hand in control.Target.interactorsSelecting.ToArray())GuardAbandonedPress(hand);
                control.CancelPress();
            }
            foreach(var gesture in new[]{RotationGesture,SectionGesture}.Concat(equipmentControls.Select(c=>c.Gesture)))
            {
                if(!gesture)continue;
                foreach(var hand in gesture.interactorsSelecting.ToArray())if(!cancelledHands.Contains(hand))cancelledHands.Add(hand);
                gesture.CancelGesture();
            }
            foreach(var control in AllControls)
                foreach(var hand in control.Target.interactorsSelecting.ToArray())
                    if(control.Target.interactionManager)control.Target.interactionManager.SelectExit(hand,(IXRSelectInteractable)control.Target);
        }
        public void CancelInteractionForRecovery()=>CancelGestures();
        public void PruneReleasedHands()=>cancelledHands.RemoveAll(i=>i==null || (i is UnityEngine.Object o && !o) || !i.isSelectActive);
        static bool SameHand(IXRSelectInteractor first,IXRSelectInteractor second)
        {
            if(first==second)return true;if(first==null || second==null)return false;
            var a=(first as XRBaseControllerInteractor)?.xrController ?? first.transform.GetComponentInParent<XRBaseController>();
            var b=(second as XRBaseControllerInteractor)?.xrController ?? second.transform.GetComponentInParent<XRBaseController>();
            return a && a==b;
        }
        void LateUpdate()
        {
            if(!ready)return;
            PruneReleasedHands();
            RefreshEquipmentInteractions();UpdateEquipmentFeedback();
            if(eras.CurrentIndex!=observedEra)
            {
                CancelGestures();exploration.Stop();inspection.ResetInspection();observedReset=inspection.ResetCount;
                observedEra=eras.CurrentIndex;fixedCenter=inspection.transform.TransformPoint(inspection.SectionCenterLocal);RefreshLabels();
            }
            if(observedReset!=inspection.ResetCount)
            { CancelGestures();exploration.Stop();observedReset=inspection.ResetCount; }
            if(resetting)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-resetStarted)/.3f);
                SetRootRotation(Quaternion.Slerp(resetFrom,homeRotation,t*t*(3-2*t)));if(t>=1)resetting=false;
            }
            body.isKinematic=true;body.useGravity=false;
            bool cut=inspection.IsCutaway;
            if(cut && !wasCut)
            {
                pulseUntil=Time.unscaledTime+2;
                capRenderers=inspection.GetComponentsInChildren<MeshRenderer>(false).Where(r=>r.name.StartsWith("Section face - ")).ToArray();
            }
            wasCut=cut;
            controls[Command.Slide].gameObject.SetActive(cut);
            sectionRail.gameObject.SetActive(cut);
            sectionDock.gameObject.SetActive(!SettingsVisible);
            controls[Command.CloseDemo].gameObject.SetActive(demonstration.IsVisible);
            controls[Command.Air].SetEmphasis(demonstration.IsVisible && demonstration.CurrentClue==HelmetExplorationController.Clue.Air);
            controls[Command.Voice].SetEmphasis(demonstration.IsVisible && demonstration.CurrentClue==HelmetExplorationController.Clue.Voice);
            controls[Command.Slide].SetEmphasis(SectionGesture.IsDragging || Time.unscaledTime<pulseUntil);
            inspection.SetWindowHover(controls[Command.Window].Target.isHovered || controls[Command.Window].Target.isSelected);
            PositionExhibitControls();UpdateRailFeedback();UpdateBodyFeedback();PulseSection();RefreshLabels();
        }
        void SetRootRotation(Quaternion rotation)
        {
            inspection.transform.rotation=rotation;
            inspection.transform.position=fixedCenter-inspection.transform.TransformVector(inspection.SectionCenterLocal);
            body.position=inspection.transform.position;body.rotation=rotation;Physics.SyncTransforms();
        }
        void PositionExhibitControls()
        {
            Vector3 center=inspection.SectionCenterLocal,extent=inspection.SectionExtentLocal;
            SetMarker(Command.Window,binding && binding.ActiveGlass?binding.ActiveGlass.bounds.center:inspection.WindowPivot.position);
            // This rail and switch stay on the plinth. Their direction never follows the rotating specimen.
            var slider=controls[Command.Slide];
            float ratio=inspection.SectionRange>.00001f?inspection.SectionOffset/inspection.SectionRange:0f;
            slider.transform.localPosition=new Vector3(Mathf.Clamp(ratio,-1,1)*RailHalfTravel,0,0);
            SetMarker(Command.Air,binding && binding.AirOutlet?binding.AirOutlet.position+inspection.transform.up*.08f:inspection.transform.TransformPoint(center+new Vector3(-extent.x,0,extent.z+.07f)));
            SetMarker(Command.Voice,binding && binding.TelephoneOutlet?binding.TelephoneOutlet.position+inspection.transform.up*.08f:inspection.transform.TransformPoint(center+new Vector3(extent.x,0,extent.z+.07f)));
        }
        void UpdateRailFeedback()
        {
            var control=controls[Command.Slide];
            float alpha=control.Opacity;
            var color=teal;color.a=alpha;
            SetLineColor(railLine,color);
            float ratio=inspection.SectionRange>.00001f?inspection.SectionOffset/inspection.SectionRange:0f;
            for(int i=0;i<railStops.Length;i++)
            {var tint=i==0 && ratio<=-.999f || i==1 && ratio>=.999f?amber:teal;tint.a=alpha;SetLineColor(railStops[i],tint);}
        }
        void UpdateBodyFeedback()
        {
            var control=controls[Command.Rotate];
            bool selected=RotationGesture.isSelected;
            bool hovered=RotationGesture.isHovered;
            Vector3 point=inspection.transform.TransformPoint(inspection.SectionCenterLocal);
            var hand=selected?RotationGesture.interactorsSelecting.FirstOrDefault() as IXRInteractor:
                RotationGesture.interactorsHovering.FirstOrDefault() as IXRInteractor;
            if(hand is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out var hit) &&
               RotationGesture.colliders.Contains(hit.collider))point=hit.point;
            else if(hand!=null)
            {
                float distance=float.MaxValue;
                foreach(var collider in RotationGesture.colliders)
                {if(!collider || !collider.enabled)continue;var near=collider.ClosestPoint(hand.transform.position);float d=(near-hand.transform.position).sqrMagnitude;if(d<distance){distance=d;point=near;}}
            }
            if(selected && !bodyWasSelected)
            {gripPointLocal=inspection.transform.InverseTransformPoint(point);bodyGripStart=inspection.transform.rotation;}
            if(selected)point=inspection.transform.TransformPoint(gripPointLocal);
            if(selected && RotationGesture.IsManipulating && Quaternion.Angle(bodyGripStart,inspection.transform.rotation)>=rotationLearningAngle)
                RotationHelpLearned=true;
            bodyWasSelected=selected;
            SetMarker(Command.Rotate,point);
            gripRing.enabled=selected || hovered;
            gripRing.widthMultiplier=selected?.0028f:.0013f;
            var tint=selected?amber:teal;tint.a=1;SetLineColor(gripRing,tint);
            var shell=binding?binding.ActiveShell:null;
            if(BodyRotationHint)
            {
                var bounds=shell?shell.bounds:new Bounds(inspection.transform.TransformPoint(inspection.SectionCenterLocal),Vector3.one*.4f);
                // Include the current inspection geometry, especially the wider
                // breastplate, instead of treating the bonnet alone as the outline.
                var center=inspection.SectionCenterLocal;var extent=inspection.SectionExtentLocal;
                for(int corner=0;corner<8;corner++)
                    bounds.Encapsulate(inspection.transform.TransformPoint(center+Vector3.Scale(extent,
                        new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1))));
                BodyRotationHint.UpdatePresentation(viewer,bounds,point,control.HasVisualIntent,selected,
                    control.Opacity,nearHintDistance,maximumHintAngle,tint);
                gripRing.transform.localScale=Vector3.one*BodyRotationHint.GripScale;
                gripRing.widthMultiplier=(selected?.0028f:.0013f)*BodyRotationHint.GripScale;
            }
            if(hoveredShell!=shell){RestoreShellFeedback();hoveredShell=shell;if(shell){shell.GetPropertyBlock(capBlock);previousShellHover=capBlock.GetFloat("_Hover");}}
            if(hoveredShell){hoveredShell.GetPropertyBlock(capBlock);capBlock.SetFloat("_Hover",selected?.45f:hovered?.13f:previousShellHover);hoveredShell.SetPropertyBlock(capBlock);}
        }
        void RestoreShellFeedback()
        {if(hoveredShell){hoveredShell.GetPropertyBlock(capBlock);capBlock.SetFloat("_Hover",previousShellHover);hoveredShell.SetPropertyBlock(capBlock);}hoveredShell=null;}
        void SetLineColor(LineRenderer line,Color color)
        {if(!line)return;line.GetPropertyBlock(capBlock);capBlock.SetColor("_BaseColor",color);line.SetPropertyBlock(capBlock);}
        void SetMarker(Command command,Vector3 point)
        {
            var control=controls[command];control.transform.position=point;
            if(viewer)
            { Vector3 direction=point-viewer.position;if(direction.sqrMagnitude>.0001f)control.transform.rotation=Quaternion.LookRotation(direction,Vector3.up); }
        }
        void PulseSection()
        {
            bool pulse=inspection.IsCutaway && !inspection.ReduceMotion && Time.unscaledTime<pulseUntil;
            float amount=SectionGesture && SectionGesture.IsDragging?.25f:pulse?(.5f+.5f*Mathf.Sin(Time.unscaledTime*Mathf.PI*2))*.3f:0;
            foreach(var renderer in capRenderers)
            {
                if(!renderer || !renderer.sharedMaterial || !renderer.sharedMaterial.HasProperty("_BaseColor"))continue;
                renderer.GetPropertyBlock(capBlock);
                capBlock.SetColor("_BaseColor",Color.Lerp(renderer.sharedMaterial.GetColor("_BaseColor"),teal,amount));renderer.SetPropertyBlock(capBlock);
            }
        }
        void RefreshLabels()
        {
            if(controls.Count==0)return;
            controls[Command.Window].SetTitle(inspection.WindowTargetOpen?"Click to close window":"Click to open window");
            controls[Command.Rotate].SetTitle(RotationHintText(RotationGesture.isSelected,RotationGesture.IsManipulating));
            controls[Command.Section].SetTitle(inspection.IsCutaway?"Close section":"Section");
            controls[Command.Air].SetTitle(demonstration.IsVisible && demonstration.CurrentClue==HelmetExplorationController.Clue.Air?"Close air demo":"Air");
            controls[Command.Voice].SetTitle(demonstration.IsVisible && demonstration.CurrentClue==HelmetExplorationController.Clue.Voice?"Close voice demo":"Voice");
            controls[Command.Hints].SetTitle(AlwaysShowHints?"Hide hints":"Show hints");
            controls[Command.AlwaysHints].SetTitle(AlwaysShowHints?"Hints: on":"Hints: off");
            controls[Command.Sound].SetTitle(inspection.SoundEnabled?"Sound: on":"Sound: off");
            controls[Command.Motion].SetTitle(inspection.ReduceMotion?"Motion: off":"Motion: on");
            controls[Command.HeightUp].SetTitle(ViewHeight!=null && !ViewHeight.CanRaise?"Height limit":"View higher");
            controls[Command.HeightDown].SetTitle(ViewHeight!=null && !ViewHeight.CanLower?"Height limit":"View lower");
            string era=eras.CurrentIndex==0?"1839 / Standard dress":eras.CurrentIndex==2?"SuperLite 17B":"Mark V";
            if(eraTitle.GetComponent<LocalizedSourceText>()?.SourceEnglish!=era)MuseumText.Set(eraTitle,era);
        }
        string RotationHintText(bool selected,bool manipulating) => selected
            ?manipulating?"Release to stop":RotationHintsCondensed?"Move to rotate":"Move hand to rotate"
            :RotationHintsCondensed?"Hold to rotate":"Hold + move to rotate";
        HelmetRotationHint MakeRotationHint(Transform parent,string name,TMP_Text caption,int side,Transform targetRoot)
        {
            var anchor=new GameObject(name).transform;anchor.SetParent(parent,false);
            var hint=anchor.gameObject.AddComponent<HelmetRotationHint>();
            var leader=MakeLine(anchor,"Rotation hint leader",.0008f);
            hint.Configure(caption,leader,side,targetRoot);return hint;
        }
        void BuildExhibitControls()
        {
            MakeWindowControl();
            sectionDock=new GameObject("Fixed section operation dock").transform;sectionDock.SetParent(pedestalUI,false);sectionDock.localPosition=new Vector3(-.50f,.16f,-.005f);
            var section=MakeControl(sectionDock,Command.Section,"Section","",teal);section.transform.localPosition=new Vector3(0,.10f,0);
            MakeControl(exhibitUI,Command.Air,"Air","Click to show or hide",teal);
            MakeControl(exhibitUI,Command.Voice,"Voice","Click to show or hide",amber);
            var rotate=MakeControl(exhibitUI,Command.Rotate,"Hold + move to rotate","",teal,true);
            RotationGesture=(HelmetGestureInteractable)rotate.Target;
            RotationGesture.Configure(inspection,HelmetGestureInteractable.Mode.Rotate,viewer);
            var bodyManager=RotationGesture.interactionManager;
            if(bodyManager)bodyManager.UnregisterInteractable((IXRInteractable)RotationGesture);
            foreach(var collider in RotationGesture.colliders)if(collider)collider.enabled=false;
            RotationGesture.colliders.Clear();
            foreach(var collider in legacyGrab.colliders)
                if(collider && !controls[Command.Window].Target.colliders.Contains(collider) && !RotationGesture.colliders.Contains(collider))RotationGesture.colliders.Add(collider);
            // Re-register after adding the existing helmet collision surfaces.
            if(bodyManager)bodyManager.RegisterInteractable((IXRInteractable)RotationGesture);
            rotate.ConfigureContextualOnly();
            rotate.Caption.transform.localPosition=new Vector3(0,.065f,-.025f);rotate.Caption.rectTransform.sizeDelta=new Vector2(.40f,.055f);
            rotate.UseExternalCaptionPlacement();
            BodyRotationHint=MakeRotationHint(rotate.transform,"Body rotation caption anchor",rotate.Caption,-1,inspection.transform);
            gripRing=MakeLine(rotate.transform,"Body grip feedback",.0013f);
            gripRing.loop=true;gripRing.positionCount=40;
            for(int i=0;i<40;i++){float a=i/40f*Mathf.PI*2;gripRing.SetPosition(i,new Vector3(Mathf.Cos(a)*.014f,Mathf.Sin(a)*.014f,-.022f));}gripRing.enabled=false;
            BuildSectionRail();
            var slide=MakeControl(sectionRail,Command.Slide,"Slide section","Hold and drag along the rail",teal,true);
            SectionGesture=(HelmetGestureInteractable)slide.Target;SectionGesture.Configure(inspection,HelmetGestureInteractable.Mode.SectionTranslate,viewer);
            SectionGesture.ConfigureSectionRail(sectionRail,RailHalfTravel);
            slide.Hint.transform.localPosition=new Vector3(0,-.12f,-.016f);slide.Hint.rectTransform.sizeDelta=new Vector2(.43f,.035f);
            slide.gameObject.SetActive(false);
            sectionRail.gameObject.SetActive(false);
        }
        void BuildSectionRail()
        {
            sectionRail=new GameObject("Fixed section slide rail").transform;sectionRail.SetParent(sectionDock,false);sectionRail.localPosition=new Vector3(0,-.10f,0);
            railLine=MakeLine(sectionRail,"Section travel axis",.002f);railLine.positionCount=2;
            railLine.SetPositions(new[]{new Vector3(-RailHalfTravel,0,-.012f),new Vector3(RailHalfTravel,0,-.012f)});
            railStops=new LineRenderer[2];
            for(int i=0;i<2;i++){float x=(i==0?-1:1)*RailHalfTravel;var stop=MakeLine(sectionRail,"Section rail end "+i,.003f);stop.positionCount=2;stop.SetPositions(new[]{new Vector3(x,-.018f,-.012f),new Vector3(x,.018f,-.012f)});railStops[i]=stop;}
            var zero=MakeLine(sectionRail,"Section rail centre",.001f);zero.positionCount=2;zero.SetPositions(new[]{new Vector3(0,-.012f,-.012f),new Vector3(0,.012f,-.012f)});
        }
        LineRenderer MakeLine(Transform parent,string name,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);line.sharedMaterial=uiMaterial;
            line.useWorldSpace=false;line.widthMultiplier=width;line.numCapVertices=3;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        void MakeWindowControl()
        {
            var control=MakeControl(exhibitUI,Command.Window,"Click to open window","",teal);
            var target=control.Target;
            // Reuse the articulated hit box maintained by RefinedHelmetBinding for every era.
            // It follows the actual glass when opened; no invisible floating button is left behind.
            var manager=target.interactionManager;
            if(manager)manager.UnregisterInteractable((IXRInteractable)target);
            foreach(var hit in target.colliders)if(hit)hit.enabled=false;
            target.colliders.Clear();
            var window=inspection.GetComponentInChildren<HelmetWindowInteractable>(true);
            if(window)foreach(var hit in window.colliders)if(hit)target.colliders.Add(hit);
            if(target.colliders.Count==0)throw new InvalidOperationException("The actual window hit surface is missing.");
            if(manager)manager.RegisterInteractable((IXRInteractable)target);
            control.Caption.rectTransform.localPosition=new Vector3(0,.10f,-.025f);
            control.Caption.rectTransform.sizeDelta=new Vector2(.36f,.045f);
            control.ConfigureContextualOnly();
        }
        void BuildPedestalControls()
        {
            Command[] row={Command.PreviousEra,Command.NextEra,Command.Reset,Command.Hints,Command.Settings,Command.About};
            string[] titles={"Previous era","Next era","Reset","Show hints","Settings","About"};
            for(int i=0;i<row.Length;i++)
            { var c=MakeControl(pedestalUI,row[i],titles[i],"Click to select",teal);c.transform.localPosition=new Vector3((i%3-1)*.19f,-(i/3)*.10f,0); }
            var close=MakeControl(pedestalUI,Command.CloseDemo,"Close demo","",teal);close.transform.localPosition=new Vector3(.47f,.26f,-.005f);close.gameObject.SetActive(false);
            eraTitle=Label(pedestalUI,"Era caption",new Vector3(0,.10f,-.025f),new Vector2(.65f,.065f),.35f);
            instruction=Label(pedestalUI,"Station invitation",new Vector3(0,.17f,-.025f),new Vector2(.72f,.075f),.31f);
            MuseumText.Set(instruction,"Could a diver work alone in this helmet?");
            options=new GameObject("Fixed settings");options.transform.SetParent(pedestalUI,false);options.transform.localPosition=new Vector3(-.64f,.07f,-.015f);
            Command[] settings={Command.Chinese,Command.Traditional,Command.English,Command.HeightDown,Command.HeightReset,Command.HeightUp,Command.Sound,Command.Motion,Command.AlwaysHints,Command.ResetView};
            string[] labels={"简体中文","繁體中文","English","View lower","Reset height","View higher","Sound: on","Motion: on","Hints: off","Reset View"};
            for(int i=0;i<settings.Length;i++)
            {var c=MakeControl(options.transform,settings[i],labels[i],"",teal);c.transform.localPosition=new Vector3((i%3-1)*.19f,-(i/3)*.10f,0);}
            options.SetActive(false);
            about=new GameObject("Fixed exhibit information");about.transform.SetParent(pedestalUI,false);about.transform.localPosition=new Vector3(.64f,-.30f,-.02f);
            aboutText=Label(about.transform,"Model boundary",Vector3.zero,new Vector2(.65f,.32f),.30f);
            MuseumText.Set(aboutText,"Equipment types, simplified.\nNot an exact matched rig.\nNo helmet interior is reconstructed.");
            about.SetActive(false);
        }
        HelmetPhysicalControl MakeControl(Transform parent,Command command,string title,string hint,Color color,bool gesture=false)
        {
            var go=new GameObject("Physical UI - "+command);go.transform.SetParent(parent,false);
            go.SetActive(false); // Register XRI only after its trigger collider list is complete.
            var face=GameObject.CreatePrimitive(PrimitiveType.Cube);face.name="Inset target";face.transform.SetParent(go.transform,false);
            face.transform.localScale=new Vector3(.168f,.074f,.012f);var collider=face.GetComponent<BoxCollider>();collider.isTrigger=true;
            var renderer=face.GetComponent<Renderer>();renderer.sharedMaterial=uiMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            XRBaseInteractable target=gesture?(XRBaseInteractable)go.AddComponent<HelmetGestureInteractable>():go.AddComponent<XRSimpleInteractable>();
            target.colliders.Add(collider);target.interactionLayers=1;
            var rim=new GameObject("Target outline").AddComponent<LineRenderer>();rim.transform.SetParent(go.transform,false);rim.sharedMaterial=uiMaterial;
            rim.useWorldSpace=false;rim.loop=true;rim.positionCount=8;rim.widthMultiplier=.0015f;rim.numCornerVertices=3;
            rim.shadowCastingMode=ShadowCastingMode.Off;rim.receiveShadows=false;
            rim.SetPositions(new[]{new Vector3(-.075f,-.037f,-.008f),new Vector3(.075f,-.037f,-.008f),new Vector3(.084f,-.029f,-.008f),new Vector3(.084f,.029f,-.008f),new Vector3(.075f,.037f,-.008f),new Vector3(-.075f,.037f,-.008f),new Vector3(-.084f,.029f,-.008f),new Vector3(-.084f,-.029f,-.008f)});
            var text=Label(go.transform,"Action",new Vector3(0,0,-.015f),new Vector2(.156f,.071f),.26f);
            text.enableAutoSizing=true;text.fontSizeMin=.23f;text.fontSizeMax=.26f;
            var detail=Label(go.transform,"Action hint",new Vector3(0,.062f,-.015f),new Vector2(.29f,.035f),.24f);
            var control=go.AddComponent<HelmetPhysicalControl>();control.Configure(this,target,renderer,rim,text,detail,title,hint,color,gesture?null:(Action)(()=>Execute(command)));
            control.ConfigureVisibility(idleOpacity,handRevealDistance,fadeOutDelay);
            control.PressAbandonedWhileHeld+=GuardAbandonedPress;
            if(command==Command.Section || command==Command.Slide)
            {
                face.transform.localScale=new Vector3(.118f,.118f,.012f);
                rim.SetPositions(new[]{new Vector3(-.049f,-.059f,-.008f),new Vector3(.049f,-.059f,-.008f),new Vector3(.059f,-.049f,-.008f),new Vector3(.059f,.049f,-.008f),new Vector3(.049f,.059f,-.008f),new Vector3(-.049f,.059f,-.008f),new Vector3(-.059f,.049f,-.008f),new Vector3(-.059f,-.049f,-.008f)});
                text.transform.localPosition=new Vector3(0,-.084f,-.016f);
                text.rectTransform.sizeDelta=new Vector2(.26f,.037f);
                detail.transform.localPosition=new Vector3(0,.090f,-.016f);
                var kind=command==Command.Section?HelmetControlSymbol.Kind.Section:HelmetControlSymbol.Kind.Slide;
                control.ConfigureSymbol(HelmetControlSymbol.Build(go.transform,uiMaterial,kind));
            }
            controls.Add(command,control);go.SetActive(true);return control;
        }
        static TextMeshPro Label(Transform parent,string name,Vector3 position,Vector2 size,float font)
        {
            var label=new GameObject(name).AddComponent<TextMeshPro>();label.transform.SetParent(parent,false);label.transform.localPosition=position;
            label.font=MuseumText.Table?MuseumText.Table.FontAsset:TMP_Settings.defaultFontAsset;
            label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=size;label.fontSize=font;
            label.enableAutoSizing=false;label.enableWordWrapping=true;label.color=Color.white;return label;
        }
        void OnDisable()
        {
            if(!ready)return;CancelGestures();demonstration.Stop();
            UpdateEquipmentFeedback();
            RestoreShellFeedback();
            // Keep the release guard on legacy targets when this optional module is disabled.
            // The inactive filter passes all other input and can be removed on destruction.
            if(exhibitUI)exhibitUI.gameObject.SetActive(false);if(pedestalUI)pedestalUI.gameObject.SetActive(false);
            foreach(var item in previousEnabled)if(item.Key)item.Key.enabled=item.Value;
            foreach(var item in previousVisibility)if(item.Key)item.Key.SetActive(item.Value);
            if(body){body.isKinematic=oldKinematic;body.useGravity=oldGravity;}
            if(recovery)recovery.enabled=false;
            if(ViewHeight)ViewHeight.enabled=false;
            if(knowledgeCard)
            {knowledgeCard.SetParent(originalCardParent,false);knowledgeCard.localPosition=originalCardPosition;knowledgeCard.localRotation=originalCardRotation;}
        }
        void OnDestroy()
        { if(demonstration)demonstration.PlaybackChanged-=RefreshEquipmentInteractions;
          foreach(var target in guardedTargets)if(target){target.selectFilters.Remove(this);target.hoverFilters.Remove(this);} }
        void OnEnable()
        {
            if(!ready)return;
            PruneReleasedHands();
            foreach(var item in previousVisibility)if(item.Key)item.Key.SetActive(false);
            foreach(var item in previousEnabled)if(item.Key)item.Key.enabled=false;
            foreach(var target in inspection.GetComponentsInChildren<XRBaseInteractable>(true))Guard(target);
            foreach(var control in controls.Values)Guard(control.Target);
            exhibitUI.gameObject.SetActive(true);pedestalUI.gameObject.SetActive(true);
            PlaceKnowledgeCard();
            body.isKinematic=true;body.useGravity=false;if(recovery)recovery.enabled=true;
            if(ViewHeight)ViewHeight.enabled=true;
        }
    }
}
