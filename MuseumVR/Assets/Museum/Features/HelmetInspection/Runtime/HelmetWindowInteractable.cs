using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Inspection
{
    /// <summary>A constrained front window: grip and move around the hinge, or click to open/close.</summary>
    [DisallowMultipleComponent]
    public sealed class HelmetWindowInteractable : XRSimpleInteractable
    {
        [SerializeField] HelmetInspectionController controller;
        [SerializeField, Min(0f)] float clickTravelLimit = 0.025f;
        [SerializeField, Min(0f)] float clickAngleLimit = 5f;
        [SerializeField, Min(0f)] float clickTimeLimit = 1f;

        readonly List<IXRSelectInteractor> cancelledInteractors = new List<IXRSelectInteractor>();
        IXRSelectInteractor activeInteractor;
        float previousHandAngle;
        float selectedAt;
        float totalAngularTravel;
        float maximumHandTravel;
        Vector3 initialHandPosition;
        bool hasHandAngle;
        bool cancelling;
        bool tapOnly;

        protected override void Awake()
        {
            base.Awake();
            EnsureController();
            selectMode = InteractableSelectMode.Single;
        }

        protected override void OnEnable()
        {
            EnsureController();
            // XRI 2.6's default distance utilities omit triggers. This window deliberately
            // uses a trigger to avoid adding a moving solid collider to the helmet body.
            getDistanceOverride = WindowDistance;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            CancelInteraction();
            if (controller != null)
                controller.SetWindowHover(false);
            base.OnDisable();
            getDistanceOverride = null;
        }

        DistanceInfo WindowDistance(IXRInteractable interactable, Vector3 position)
        {
            var result = new DistanceInfo { distanceSqr = float.MaxValue, point = transform.position };
            foreach (var collider in colliders)
            {
                if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                Vector3 point = collider.ClosestPoint(position);
                float distance = (position - point).sqrMagnitude;
                if (distance < result.distanceSqr)
                    result = new DistanceInfo { point = point, distanceSqr = distance, collider = collider };
            }
            return result;
        }

        public void Configure(HelmetInspectionController inspectionController)
        {
            controller = inspectionController;
            if (controller != null)
                controller.RegisterWindow(this);
            selectMode = InteractableSelectMode.Single;
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            return controller != null && controller.CanSelectWindow(interactor) &&
                   !cancelling && !cancelledInteractors.Contains(interactor) && base.IsSelectableBy(interactor);
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
                return;

            cancelledInteractors.RemoveAll(HasReleasedOrDisappeared);
            if (controller == null || !controller.isActiveAndEnabled)
            {
                CancelInteraction();
                return;
            }

            // A distant ray uses click-to-toggle; its controller position is not on the window arc.
            if (!tapOnly && activeInteractor != null && TryGetHandPosition(activeInteractor, out var localPosition))
            {
                maximumHandTravel = Mathf.Max(maximumHandTravel, Vector3.Distance(localPosition, initialHandPosition));
                if (TryGetHandAngle(localPosition, out var handAngle))
                {
                    if (hasHandAngle)
                    {
                        var delta = Mathf.DeltaAngle(previousHandAngle, handAngle);
                        totalAngularTravel += Mathf.Abs(delta);
                        controller.SetWindowAngle(controller.WindowAngle + delta / controller.OpenAngleSign);
                    }
                    previousHandAngle = handAngle;
                    hasHandAngle = true;
                }
                else
                    hasHandAngle = false;
            }
            controller.SetWindowHover(isHovered || isSelected);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            if (controller == null || !controller.CanSelectWindow(args.interactorObject))
            {
                CancelInteraction();
                return;
            }
            activeInteractor = args.interactorObject;
            tapOnly = activeInteractor is XRRayInteractor;
            selectedAt = Time.unscaledTime;
            totalAngularTravel = 0f;
            maximumHandTravel = 0f;
            hasHandAngle = TryGetHandPosition(activeInteractor, out initialHandPosition) &&
                           TryGetHandAngle(initialHandPosition, out previousHandAngle);
            controller.BeginWindowInteraction();
            controller.SetWindowHover(true);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            bool clicked = !cancelling && !args.isCanceled && activeInteractor == args.interactorObject &&
                           Time.unscaledTime - selectedAt <= clickTimeLimit &&
                           maximumHandTravel <= clickTravelLimit && totalAngularTravel <= clickAngleLimit;
            activeInteractor = null;
            hasHandAngle = false;
            base.OnSelectExited(args);
            if (controller != null)
            {
                controller.EndWindowInteraction();
                if (clicked && controller.isActiveAndEnabled)
                    controller.ToggleWindow();
                controller.SetWindowHover(isHovered);
            }
        }

        /// <summary>Cancel safely during reset or disable, without turning the release into a click.</summary>
        public void CancelInteraction()
        {
            if (cancelling)
                return;
            cancelling = true;
            try
            {
                var selectors = interactorsSelecting.ToArray();
                foreach (var interactor in selectors)
                {
                    if (!cancelledInteractors.Contains(interactor))
                        cancelledInteractors.Add(interactor);
                    if (interactionManager != null)
                        interactionManager.SelectExit(interactor, this);
                }
                activeInteractor = null;
                hasHandAngle = false;
                if (controller != null)
                {
                    controller.EndWindowInteraction();
                    controller.SetWindowHover(false);
                }
            }
            finally
            {
                cancelling = false;
            }
        }

        void EnsureController()
        {
            if (controller == null)
                controller = GetComponentInParent<HelmetInspectionController>();
            if (controller != null)
                controller.RegisterWindow(this);
        }

        internal bool CanSelectWholeHelmet(IXRSelectInteractor interactor)
        {
            // A hand cancelled from the window must not immediately catch the whole helmet on reset.
            cancelledInteractors.RemoveAll(HasReleasedOrDisappeared);
            return !cancelledInteractors.Contains(interactor);
        }

        bool TryGetHandPosition(IXRSelectInteractor interactor, out Vector3 localPosition)
        {
            localPosition = Vector3.zero;
            if (interactor == null || (interactor is Object unityObject && unityObject == null) ||
                controller == null || controller.WindowPivot == null)
                return false;
            var attach = interactor.GetAttachTransform(this);
            if (attach == null)
                return false;
            var pivot = controller.WindowPivot;
            localPosition = pivot.parent != null
                ? pivot.parent.InverseTransformPoint(attach.position) - pivot.localPosition
                : attach.position - pivot.position;
            return true;
        }

        static bool TryGetHandAngle(Vector3 localPosition, out float degrees)
        {
            degrees = Mathf.Atan2(localPosition.x, localPosition.z) * Mathf.Rad2Deg;
            // At the hinge axis there is no stable angular measurement.
            return localPosition.x * localPosition.x + localPosition.z * localPosition.z >= 0.0001f;
        }

        static bool HasReleasedOrDisappeared(IXRSelectInteractor interactor)
        {
            return interactor == null || (interactor is Object unityObject && unityObject == null) ||
                   !interactor.isSelectActive;
        }
    }
}
