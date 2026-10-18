using System.Collections.Generic;
using System.Linq;
using Museum.Exploration;
using Museum.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Diegetic
{
    public sealed partial class HelmetDiegeticStation
    {
        sealed class EquipmentControl
        {
            public HelmetConnectionDemonstration.EquipmentExhibit Exhibit;
            public HelmetGestureInteractable Gesture;
            public Transform Feedback;
            public TextMeshPro Caption;
            public LineRenderer Ring;
            public HelmetRotationHint Hint;
            public Vector3 GripLocal;
            public Quaternion GripStart;
            public bool WasSelected;
            public int HoverState=-1;
            public readonly Dictionary<Renderer,float> OriginalHover = new Dictionary<Renderer,float>();
        }
        readonly List<EquipmentControl> equipmentControls = new List<EquipmentControl>();

        bool IsStationTarget(XRBaseInteractable target) => target &&
            (AllControls.Any(c=>c.Target==target) || equipmentControls.Any(c=>c.Gesture==target));

        void RefreshEquipmentInteractions()
        {
            if(!ready || !demonstration)return;
            foreach(var exhibit in demonstration.EquipmentExhibits)
            {
                if(!exhibit.Root)continue;
                var control=equipmentControls.FirstOrDefault(c=>ReferenceEquals(c.Exhibit,exhibit));
                if(control==null)
                {
                    control=BuildEquipmentControl(exhibit);equipmentControls.Add(control);Guard(control.Gesture);
                }
                bool visible=isActiveAndEnabled && demonstration.IsVisible && exhibit.IsVisible;
                if(!visible && control.Gesture.IsDragging)
                {
                    foreach(var hand in control.Gesture.interactorsSelecting.ToArray())GuardAbandonedPress(hand);
                    control.Gesture.CancelGesture();
                }
                control.Gesture.enabled=visible;
                foreach(var collider in control.Gesture.colliders)if(collider)collider.enabled=visible;
                control.Gesture.SetEquipmentCentre(exhibit.CenterLocal);
                if(!visible){control.Feedback.gameObject.SetActive(false);RestoreEquipmentHover(control,0);control.WasSelected=false;}
            }
        }

        EquipmentControl BuildEquipmentControl(HelmetConnectionDemonstration.EquipmentExhibit exhibit)
        {
            var hit=new GameObject("Equipment surface hold - "+exhibit.Id);
            hit.SetActive(false);hit.transform.SetParent(exhibit.Root,false);
            var gesture=hit.AddComponent<HelmetGestureInteractable>();
            gesture.ConfigureEquipment(exhibit.Root,exhibit.CenterLocal,viewer);
            gesture.interactionLayers=1;
            var surfaces=exhibit.Renderers.Where(r=>r && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var largest=surfaces.OrderByDescending(r=>r.bounds.size.sqrMagnitude).FirstOrDefault();
            foreach(var surface in surfaces)
            {
                if(!surface)continue;
                var mesh=surface.GetComponent<MeshFilter>();
                if(!mesh || !mesh.sharedMesh)continue;
                var worldSize=surface.bounds.size;
                // Grab the principal surfaces, not hundreds of millimetre-scale
                // screws, grille holes and dial ticks. All visible detail stays.
                // 18 mm is a prototype hit-proxy threshold, not a reach standard.
                if(surface!=largest && Mathf.Max(worldSize.x,worldSize.y,worldSize.z)<.018f)continue;
                // Each modest proxy follows its own animated part. No enclosing box
                // spans the empty space between equipment, hoses or the helmet.
                var proxy=new GameObject("Equipment touch surface");proxy.transform.SetParent(surface.transform,false);
                var collider=proxy.AddComponent<BoxCollider>();collider.isTrigger=true;
                collider.center=mesh.sharedMesh.bounds.center;
                var size=mesh.sharedMesh.bounds.size;
                collider.size=new Vector3(Mathf.Max(.0001f,size.x),Mathf.Max(.0001f,size.y),Mathf.Max(.0001f,size.z));
                gesture.colliders.Add(collider);
            }
            var feedback=new GameObject("Equipment grip feedback - "+exhibit.Id).transform;
            feedback.SetParent(exhibitUI,false);
            var label=Label(feedback,"Equipment gesture hint",new Vector3(0,.065f,-.025f),new Vector2(.40f,.055f),.26f);
            MuseumText.Set(label,"Hold + move to rotate");
            var captionHint=MakeRotationHint(feedback,"Equipment rotation caption anchor - "+exhibit.Id,label,1,exhibit.Root);
            var ring=MakeLine(feedback,"Equipment grip ring",.0013f);ring.loop=true;ring.positionCount=40;
            for(int i=0;i<40;i++){float a=i/40f*Mathf.PI*2;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*.014f,Mathf.Sin(a)*.014f,-.022f));}
            var control=new EquipmentControl{Exhibit=exhibit,Gesture=gesture,Feedback=feedback,Caption=label,Ring=ring,Hint=captionHint};
            foreach(var surface in exhibit.Renderers)
            {
                if(!surface)continue;surface.GetPropertyBlock(capBlock);
                control.OriginalHover[surface]=capBlock.GetFloat("_Hover");
            }
            feedback.gameObject.SetActive(false);hit.SetActive(true);
            return control;
        }

        void UpdateEquipmentFeedback()
        {
            foreach(var control in equipmentControls)
            {
                if(!control.Gesture || !control.Exhibit.Root)continue;
                bool selected=control.Gesture.isSelected;
                bool hovered=control.Gesture.isHovered;
                bool visible=isActiveAndEnabled && demonstration.IsVisible && control.Exhibit.IsVisible && (selected || hovered);
                control.Feedback.gameObject.SetActive(visible);
                RestoreEquipmentHover(control,visible?(selected?2:1):0);
                if(!visible){control.WasSelected=false;continue;}
                var hand=selected?control.Gesture.interactorsSelecting.FirstOrDefault() as IXRInteractor:
                    control.Gesture.interactorsHovering.FirstOrDefault() as IXRInteractor;
                var root=control.Exhibit.Root;
                Vector3 point=root.TransformPoint(control.Exhibit.CenterLocal);
                if(hand is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out var hit) && control.Gesture.colliders.Contains(hit.collider))point=hit.point;
                else if(hand!=null)
                {
                    float distance=float.MaxValue;
                    foreach(var collider in control.Gesture.colliders)
                    {
                        if(!collider || !collider.enabled)continue;
                        var near=collider.ClosestPoint(hand.transform.position);float d=(near-hand.transform.position).sqrMagnitude;
                        if(d<distance){distance=d;point=near;}
                    }
                }
                if(selected && !control.WasSelected)
                {control.GripLocal=root.InverseTransformPoint(point);control.GripStart=root.rotation;}
                if(selected)point=root.TransformPoint(control.GripLocal);
                if(selected && control.Gesture.IsManipulating && Quaternion.Angle(control.GripStart,root.rotation)>=rotationLearningAngle)
                    RotationHelpLearned=true;
                control.WasSelected=selected;control.Feedback.position=point;
                if(viewer && (point-viewer.position).sqrMagnitude>.0001f)
                    control.Feedback.rotation=Quaternion.LookRotation(point-viewer.position,Vector3.up);
                control.Ring.widthMultiplier=selected?.0028f:.0013f;SetLineColor(control.Ring,selected?amber:teal);
                var bounds=control.Exhibit.Renderers.FirstOrDefault(r=>r && r.enabled && r.gameObject.activeInHierarchy);
                var combined=bounds?bounds.bounds:new Bounds(root.TransformPoint(control.Exhibit.CenterLocal),Vector3.one*.1f);
                if(!control.Hint.IsAnchored || selected)
                    foreach(var surface in control.Exhibit.Renderers)
                        if(surface && surface.enabled && surface.gameObject.activeInHierarchy)combined.Encapsulate(surface.bounds);
                else combined=control.Hint.TargetBounds;
                control.Hint.UpdatePresentation(viewer,combined,point,true,selected,1f,nearHintDistance,maximumHintAngle,selected?amber:teal);
                control.Ring.transform.localScale=Vector3.one*control.Hint.GripScale;
                control.Ring.widthMultiplier=(selected?.0028f:.0013f)*control.Hint.GripScale;
                string text=RotationHintText(selected,control.Gesture.IsManipulating);
                if(control.Caption.GetComponent<LocalizedSourceText>()?.SourceEnglish!=text)MuseumText.Set(control.Caption,text);
            }
        }

        void RestoreEquipmentHover(EquipmentControl control,int state)
        {
            if(control.HoverState==state)return;
            foreach(var pair in control.OriginalHover)
            {
                if(!pair.Key)continue;pair.Key.GetPropertyBlock(capBlock);
                capBlock.SetFloat("_Hover",state==2?.45f:state==1?.13f:pair.Value);pair.Key.SetPropertyBlock(capBlock);
            }
            control.HoverState=state;
        }
    }
}
