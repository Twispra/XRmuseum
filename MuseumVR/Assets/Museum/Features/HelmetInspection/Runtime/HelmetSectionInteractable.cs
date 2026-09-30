using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Inspection
{
    /// <summary>A non-physical cutting guide driven by relative hand motion, also with distant rays.</summary>
    [DisallowMultipleComponent]
    public sealed class HelmetSectionInteractable : XRSimpleInteractable
    {
        [SerializeField] HelmetInspectionController controller;
        [SerializeField] Transform outline;
        [SerializeField] Transform handle;
        readonly List<IXRSelectInteractor> cancelledInteractors = new List<IXRSelectInteractor>();
        IXRSelectInteractor activeInteractor;
        Renderer[] guideRenderers;
        Vector3 initialHandPosition, initialPlanePoint, initialHandlePosition;
        Quaternion initialHandRotation, initialPlaneRotation;
        Vector3 visualOffsetInPlane;
        Vector3 guideNormalLocal, guideRightLocal;
        bool guideFrameReady;
        bool visualOffsetCaptured, cancelling, showTool;
        public HelmetInspectionController Controller => controller;
        public bool ShowTool
        {
            get => showTool;
            set
            {
                if (!value) CancelInteraction();
                showTool = value;
                SetVisible(value && controller != null && controller.isActiveAndEnabled && controller.IsCutaway);
                if (showTool && controller != null) UpdateGuidePose();
            }
        }
        protected override void Awake()
        {
            base.Awake();
            EnsureController();
            selectMode = InteractableSelectMode.Single;
            CacheVisuals();
        }
        protected override void OnEnable()
        {
            EnsureController();
            getDistanceOverride = GuideDistance;
            base.OnEnable();
            CacheVisuals();
            SetVisible(showTool && controller != null && controller.IsCutaway);
        }
        protected override void OnDisable()
        {
            CancelInteraction();
            showTool = false;
            SetVisible(false);
            base.OnDisable();
            getDistanceOverride = null;
        }
        public void Configure(HelmetInspectionController inspectionController)
        {
            if (controller != inspectionController) guideFrameReady = false;
            controller = inspectionController;
            if (controller != null) controller.RegisterSection(this);
            selectMode = InteractableSelectMode.Single;
            visualOffsetCaptured = false;
            CacheVisuals();
        }
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            return showTool && controller != null && controller.CanSelectSection(interactor) && !cancelling &&
                   CanSelectOtherPart(interactor) && base.IsSelectableBy(interactor);
        }
        public override bool IsHoverableBy(IXRHoverInteractor interactor)
        {
            return showTool && controller != null && controller.isActiveAndEnabled && controller.IsCutaway && base.IsHoverableBy(interactor);
        }
        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
            cancelledInteractors.RemoveAll(HasReleasedOrDisappeared);
            if (!showTool || controller == null || !controller.isActiveAndEnabled || !controller.IsCutaway)
            {
                CancelInteraction();
                SetVisible(false);
                return;
            }
            SetVisible(true);
            if (activeInteractor != null && TryGetHandPose(activeInteractor, out var position, out var rotation))
            {
                Quaternion delta = rotation * Quaternion.Inverse(initialHandRotation);
                // Keep the initial handle-to-plane offset: a ray grab never snaps the guide onto the hand.
                Vector3 anchor = initialHandlePosition + (position - initialHandPosition);
                Vector3 point = anchor + delta * (initialPlanePoint - initialHandlePosition);
                Quaternion rotationInWorld = delta * initialPlaneRotation;
                controller.SetSectionFromWorldPose(point, rotationInWorld);
                // A normal alone loses wrist roll. Keep the whole hand-driven frame for release/regrab.
                RememberGuideFrame(rotationInWorld);
            }
            UpdateGuidePose();
        }
        void LateUpdate()
        {
            // Follow the helmet after XRI moves it. Do not chase the visitor's head or gaze.
            bool visible = showTool && controller != null && controller.isActiveAndEnabled && controller.IsCutaway;
            SetVisible(visible);
            if (visible) UpdateGuidePose();
        }
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            if (controller == null || !controller.CanSelectSection(args.interactorObject) ||
                !TryGetHandPose(args.interactorObject, out initialHandPosition, out initialHandRotation))
            {
                CancelInteraction();
                return;
            }
            activeInteractor = args.interactorObject;
            UpdateGuidePose();
            initialPlanePoint = controller.transform.TransformPoint(controller.SectionPointLocal);
            initialPlaneRotation = PlaneRotation();
            initialHandlePosition = handle != null ? handle.position : transform.position;
            controller.BeginSectionInteraction();
        }
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            activeInteractor = null;
            base.OnSelectExited(args);
            if (controller != null) controller.EndSectionInteraction();
        }
        public void CancelInteraction()
        {
            if (cancelling) return;
            cancelling = true;
            try
            {
                foreach (var interactor in interactorsSelecting.ToArray())
                {
                    if (!cancelledInteractors.Contains(interactor)) cancelledInteractors.Add(interactor);
                    if (interactionManager != null) interactionManager.SelectExit(interactor, this);
                }
                activeInteractor = null;
                if (controller != null) controller.EndSectionInteraction();
            }
            finally { cancelling = false; }
        }
        internal bool CanSelectOtherPart(IXRSelectInteractor interactor)
        {
            cancelledInteractors.RemoveAll(HasReleasedOrDisappeared);
            foreach (var cancelled in cancelledInteractors)
            {
                if (cancelled == interactor) return false;
                // Direct and ray interactors on one physical hand share the same grip.
                var hand = cancelled.transform.GetComponentInParent<XRBaseController>();
                if (hand != null && interactor != null && interactor.transform.GetComponentInParent<XRBaseController>() == hand) return false;
            }
            return true;
        }
        void EnsureController()
        {
            if (controller == null) controller = GetComponentInParent<HelmetInspectionController>();
            if (controller != null) controller.RegisterSection(this);
        }
        void CacheVisuals()
        {
            guideRenderers = GetComponentsInChildren<Renderer>(true);
            if (handle == null && colliders.Count > 0 && colliders[0] != null) handle = colliders[0].transform;
            if (controller != null && !visualOffsetCaptured)
            {
                // Support either a root at the plane centre or a root placed on the guide's rim.
                Vector3 centre = controller.transform.TransformPoint(controller.SectionPointLocal);
                visualOffsetInPlane = Quaternion.Inverse(PlaneRotation()) * (transform.position - centre);
                visualOffsetCaptured = true;
            }
        }
        Quaternion PlaneRotation()
        {
            Vector3 normal = controller.SectionNormalLocal;
            if (!guideFrameReady)
            {
                // Seed once from the authored/current tool, rather than selecting a new up axis every frame.
                guideRightLocal = Tangent(normal, controller.transform.worldToLocalMatrix.MultiplyVector(transform.right));
                guideFrameReady = true;
            }
            else
            {
                Vector3 axis = Vector3.Cross(guideNormalLocal, normal);
                float sine = axis.magnitude;
                float cosine = Mathf.Clamp(Vector3.Dot(guideNormalLocal, normal), -1f, 1f);
                if (sine > .0000001f)
                    guideRightLocal = Quaternion.AngleAxis(Mathf.Atan2(sine, cosine) * Mathf.Rad2Deg, axis / sine) * guideRightLocal;
                // At an exact 180-degree flip, keep the old right as the rotation axis and knob side.
                guideRightLocal = Tangent(normal, guideRightLocal);
            }
            guideNormalLocal = normal;
            Vector4 plane = controller.WorldSectionPlane;
            Vector3 forward = new Vector3(plane.x, plane.y, plane.z).normalized;
            // Tangents transform as vectors; the controller transforms normals by inverse transpose.
            Vector3 right = Tangent(forward, controller.transform.localToWorldMatrix.MultiplyVector(guideRightLocal));
            return Quaternion.LookRotation(forward, Vector3.Cross(forward, right));
        }
        void RememberGuideFrame(Quaternion rotationInWorld)
        {
            guideNormalLocal = controller.SectionNormalLocal;
            guideRightLocal = Tangent(guideNormalLocal,
                controller.transform.worldToLocalMatrix.MultiplyVector(rotationInWorld * Vector3.right));
            guideFrameReady = true;
        }
        static Vector3 Tangent(Vector3 normal, Vector3 right)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(right, normal);
            if (tangent.sqrMagnitude < .00000001f)
            {
                // Only an uninitialised/degenerate frame needs a seed. Valid transported frames keep their side.
                Vector3 seed = Mathf.Abs(normal.x) < Mathf.Abs(normal.y) ? Vector3.right : Vector3.up;
                if (Mathf.Abs(normal.z) < Mathf.Abs(Vector3.Dot(normal, seed))) seed = Vector3.forward;
                tangent = Vector3.ProjectOnPlane(seed, normal);
            }
            return tangent.normalized;
        }
        void UpdateGuidePose()
        {
            if (!visualOffsetCaptured) CacheVisuals();
            Quaternion rotation = PlaneRotation();
            Vector3 point = controller.transform.TransformPoint(controller.SectionPointLocal);
            transform.SetPositionAndRotation(point + rotation * visualOffsetInPlane, rotation);
        }
        void SetVisible(bool visible)
        {
            // Remain enabled to observe grip release after a forced cancellation.
            if (guideRenderers == null) CacheVisuals();
            foreach (var renderer in guideRenderers)
            {
                if (renderer == null) continue;
                renderer.enabled = visible;
                // Pure visual children can sleep; keep this interactable's root alive for grip locks.
                if (renderer.transform != transform && renderer.transform.IsChildOf(transform) && renderer.gameObject.activeSelf != visible)
                    renderer.gameObject.SetActive(visible);
            }
            foreach (var collider in colliders) if (collider != null) collider.enabled = visible;
        }
        DistanceInfo GuideDistance(IXRInteractable interactable, Vector3 position)
        {
            var result = new DistanceInfo { distanceSqr = float.MaxValue, point = transform.position };
            foreach (var collider in colliders)
            {
                if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                Vector3 point = collider.ClosestPoint(position);
                float distance = (position - point).sqrMagnitude;
                if (distance < result.distanceSqr) result = new DistanceInfo { point = point, distanceSqr = distance, collider = collider };
            }
            return result;
        }
        bool TryGetHandPose(IXRSelectInteractor interactor, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (interactor == null || (interactor is Object obj && obj == null)) return false;
            // A ray's attach transform can depend on hit depth. Use its tracked controller transform.
            Transform source = interactor.transform;
            if (source == null) return false;
            position = source.position;
            rotation = source.rotation;
            return true;
        }
        static bool HasReleasedOrDisappeared(IXRSelectInteractor interactor)
        {
            return interactor == null || (interactor is Object obj && obj == null) || !interactor.isSelectActive;
        }
    }
}
