using System.Collections.Generic;
using Museum.Inspection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Diegetic
{
    /// <summary>
    /// A position-driven hold gesture. The station owns physics and mutual exclusion;
    /// this component only rotates a pinned exhibit or translates its existing cut.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelmetGestureInteractable : XRSimpleInteractable
    {
        public enum Mode { Rotate, SectionTranslate }

        [SerializeField] HelmetInspectionController inspection;
        [SerializeField] Mode mode;
        [SerializeField] Transform viewer;
        Transform equipmentRoot;
        Vector3 equipmentCentreLocal;
        [SerializeField, Min(1f)] float rotationDegreesPerMetre = 300f;
        [SerializeField] Transform sectionRail;
        [SerializeField, Min(.01f)] float sectionRailHalfTravel = .15f;

        // A bounded cursor keeps the virtual ball useful during long drags. Its
        // position chooses the rotation axis, while hand distance controls angle;
        // moving past the rim therefore never stalls a continuous turn.
        const float MaximumTrackballOffset = 1.25f;
        const float RotationStartDistance = .004f;
        const float SectionManipulationDistance = .002f;

        readonly List<IXRSelectInteractor> releaseGuards = new List<IXRSelectInteractor>();
        IXRSelectInteractor activeInteractor;
        Rigidbody exhibitBody;
        Vector3 previousHandPosition, initialHandPosition;
        Vector3 fixedWorldCentre, fixedCentreLocal, initialSectionHandLocal;
        Vector3 initialSectionNormalLocal;
        Vector3 capturedRailAxis;
        Vector2 trackballPoint;
        Quaternion fixedViewRotation;
        float initialSectionOffset;
        float capturedRailHalfTravel, capturedSectionRange;
        int observedResetCount;
        bool dragging, cancelling, manipulating, usesRail;

        public bool IsDragging => dragging;
        public bool IsManipulating => dragging && manipulating;
        public Mode GestureMode => mode;
        public Transform TargetRoot => equipmentRoot ? equipmentRoot : inspection ? inspection.transform : null;
        public Transform SectionRail => sectionRail;
        public float SectionRailHalfTravel => sectionRailHalfTravel;
        public Vector3 SectionRailAxisWorld => sectionRail != null
            ? sectionRail.TransformVector(Vector3.right).normalized : Vector3.zero;
        public float SectionRailHalfTravelWorld => sectionRail != null
            ? sectionRail.TransformVector(Vector3.right * sectionRailHalfTravel).magnitude : 0f;

        protected override void Awake()
        {
            base.Awake();
            selectMode = InteractableSelectMode.Single;
            CacheExhibitBody();
        }

        protected override void OnEnable()
        {
            getDistanceOverride = GestureDistance;
            base.OnEnable();
            observedResetCount = inspection != null ? inspection.ResetCount : 0;
        }

        protected override void OnDisable()
        {
            // Explicit exit first: disabling a held control must never resume that
            // same still-pressed hand when the station later shows it again.
            CancelGesture();
            base.OnDisable();
            getDistanceOverride = null;
        }

        public void Configure(HelmetInspectionController controller, Mode gestureMode, Transform view)
        {
            CancelGesture();
            inspection = controller;
            equipmentRoot = null;
            exhibitBody = inspection != null ? inspection.GetComponent<Rigidbody>() : null;
            mode = gestureMode;
            viewer = view;
            selectMode = InteractableSelectMode.Single;
            observedResetCount = inspection != null ? inspection.ResetCount : 0;
        }

        /// <summary>Reuse the helmet's view-relative rolling gesture for an animated apparatus' outer pivot.</summary>
        public void ConfigureEquipment(Transform root, Vector3 centreLocal, Transform view)
        {
            CancelGesture();
            inspection = null; exhibitBody = null;
            equipmentRoot = root; equipmentCentreLocal = centreLocal;
            mode = Mode.Rotate; viewer = view; selectMode = InteractableSelectMode.Single;
        }

        public void SetEquipmentCentre(Vector3 centreLocal)
        { if (!dragging && Finite(centreLocal)) equipmentCentreLocal = centreLocal; }

        /// <summary>
        /// The rail is stationary relative to the display, not the helmet. Its
        /// local X direction is the visible slider direction and halfTravel is
        /// measured in the rail's local space, so parent scale is respected.
        /// </summary>
        public void ConfigureSectionRail(Transform rail, float halfTravel)
        {
            CancelGesture();
            sectionRail = rail;
            sectionRailHalfTravel = Finite(halfTravel) ? Mathf.Max(.01f, halfTravel) : .15f;
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            releaseGuards.RemoveAll(HasReleasedOrDisappeared);
            if (!CanOperate() || cancelling || interactor == null) return false;
            foreach (var guarded in releaseGuards)
                if (SameHand(guarded, interactor)) return false;
            return base.IsSelectableBy(interactor);
        }

        public override bool IsHoverableBy(IXRHoverInteractor interactor)
        {
            return CanOperate() && base.IsHoverableBy(interactor);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            activeInteractor = args.interactorObject;
            Quaternion viewRotation = viewer != null ? viewer.rotation : Quaternion.identity;
            if (!TryHandPosition(activeInteractor, out var position) || !Begin(position, viewRotation, Vector2.zero))
                CancelGesture();
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            if (activeInteractor == args.interactorObject)
            {
                activeInteractor = null;
                dragging = false;
                manipulating = false;
            }
            base.OnSelectExited(args);
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
            releaseGuards.RemoveAll(HasReleasedOrDisappeared);
            if ((!equipmentRoot && (inspection == null || observedResetCount != inspection.ResetCount)) || !CanOperate())
            {
                CancelGesture();
                observedResetCount = inspection != null ? inspection.ResetCount : 0;
                return;
            }
            if (activeInteractor == null) return;
            if (!TryHandPosition(activeInteractor, out var position))
            {
                CancelGesture();
                return;
            }
            Apply(position);
        }

        /// <summary>Forced exits preserve a release-before-reselect guard across disable/enable.</summary>
        public void CancelGesture()
        {
            if (cancelling) return;
            cancelling = true;
            try
            {
                foreach (var interactor in interactorsSelecting.ToArray())
                {
                    if (!releaseGuards.Contains(interactor)) releaseGuards.Add(interactor);
                    if (interactionManager != null) interactionManager.SelectExit(interactor, this);
                }
                if (activeInteractor != null && !releaseGuards.Contains(activeInteractor))
                    releaseGuards.Add(activeInteractor);
                activeInteractor = null;
                dragging = false;
                manipulating = false;
            }
            finally { cancelling = false; }
        }

        /// <summary>Exercises the production gesture calculation without manufacturing an XR device.</summary>
        public void BeginForValidation(Vector3 handPosition, Quaternion viewRotation)
        {
            CancelGesture();
            Begin(handPosition, viewRotation, Vector2.zero);
        }

        /// <summary>
        /// Starts the same rolling-ball calculation at a normalized view-plane
        /// point. This overload lets validation isolate a drag around the rim.
        /// The production gesture always begins at the neutral centre, so the
        /// selected point on the body cannot bias the first drag direction.
        /// </summary>
        public void BeginForValidation(Vector3 handPosition, Quaternion viewRotation, Vector2 normalizedTrackballPoint)
        {
            CancelGesture();
            Begin(handPosition, viewRotation, normalizedTrackballPoint);
        }

        public void ApplyForValidation(Vector3 handPosition) => Apply(handPosition);
        public void EndForValidation() => CancelGesture();

        bool CanOperate()
        {
            if (equipmentRoot) return isActiveAndEnabled && equipmentRoot.gameObject.activeInHierarchy;
            return isActiveAndEnabled && inspection != null && inspection.isActiveAndEnabled &&
                   !inspection.IsReturning && !inspection.IsHeld && !inspection.IsWindowHeld &&
                   !inspection.IsSectionHeld && (mode != Mode.SectionTranslate || inspection.IsCutaway);
        }

        bool Begin(Vector3 handPosition, Quaternion viewRotation, Vector2 normalizedTrackballPoint)
        {
            if (!CanOperate() || !Finite(handPosition) || !Finite(viewRotation) ||
                !Finite(normalizedTrackballPoint)) return false;
            CacheExhibitBody();
            initialHandPosition = previousHandPosition = handPosition;
            fixedViewRotation = viewRotation.normalized;
            trackballPoint = Vector2.ClampMagnitude(normalizedTrackballPoint, MaximumTrackballOffset);
            fixedCentreLocal = equipmentRoot ? equipmentCentreLocal : inspection.SectionCenterLocal;
            fixedWorldCentre = TargetRoot.TransformPoint(fixedCentreLocal);
            if (equipmentRoot)
            {
                if (!Finite(fixedCentreLocal) || !Finite(fixedWorldCentre) || !Finite(TargetRoot.rotation)) return false;
                manipulating = false; dragging = true; return true;
            }
            initialSectionNormalLocal = inspection.SectionNormalLocal;
            initialSectionOffset = inspection.SectionOffset;
            initialSectionHandLocal = inspection.transform.InverseTransformPoint(initialHandPosition);
            capturedRailAxis = SectionRailAxisWorld;
            capturedRailHalfTravel = SectionRailHalfTravelWorld;
            capturedSectionRange = inspection.SectionRange;
            usesRail = sectionRail != null && Finite(capturedRailAxis) &&
                       capturedRailAxis.sqrMagnitude > .5f && Finite(capturedRailHalfTravel) &&
                       capturedRailHalfTravel > .000001f && Finite(capturedSectionRange);
            if (!Finite(fixedCentreLocal) || !Finite(fixedWorldCentre) ||
                !Finite(initialSectionNormalLocal) || !Finite(initialSectionOffset) ||
                !Finite(initialSectionHandLocal) || !Finite(inspection.transform.rotation)) return false;
            observedResetCount = inspection.ResetCount;
            manipulating = false;
            dragging = true;
            return true;
        }

        void Apply(Vector3 handPosition)
        {
            if (!dragging) return;
            if (!CanOperate() || (!equipmentRoot && inspection.ResetCount != observedResetCount))
            {
                CancelGesture();
                return;
            }
            if (!Finite(handPosition)) return;
            Transform root = TargetRoot;
            if (!Finite(root.position) || !Finite(root.rotation) || !Finite(root.lossyScale)) return;
            if (mode == Mode.SectionTranslate)
            {
                if (usesRail)
                {
                    // The visible rail controls the cut even after the helmet
                    // has been turned. Movement along either perpendicular axis
                    // does nothing, and the original cut normal stays fixed.
                    float distanceAlongRail = Vector3.Dot(handPosition - initialHandPosition, capturedRailAxis);
                    float railOffset = initialSectionOffset + distanceAlongRail / capturedRailHalfTravel * capturedSectionRange;
                    if (Finite(distanceAlongRail) && Finite(railOffset))
                    {
                        manipulating |= Mathf.Abs(distanceAlongRail) > SectionManipulationDistance;
                        inspection.SetSectionPlane(initialSectionNormalLocal, railOffset);
                    }
                    return;
                }
                // Isolated callers without a rail retain the normal-space
                // projection. Wrist rotation never changes the cutting plane.
                Vector3 handLocal = inspection.transform.InverseTransformPoint(handPosition);
                float delta = Vector3.Dot(handLocal - initialSectionHandLocal, initialSectionNormalLocal);
                float offset = initialSectionOffset + delta;
                if (Finite(handLocal) && Finite(offset))
                {
                    manipulating |= inspection.transform.TransformVector(initialSectionNormalLocal * delta).magnitude > SectionManipulationDistance;
                    inspection.SetSectionPlane(initialSectionNormalLocal, offset);
                }
                return;
            }

            if (!manipulating)
            {
                Vector3 displacement = Quaternion.Inverse(fixedViewRotation) * (handPosition - initialHandPosition);
                if (!Finite(displacement)) return;
                var planarDisplacement = new Vector2(displacement.x, displacement.y);
                float planarDistance = planarDisplacement.magnitude;
                if (!Finite(planarDistance) || planarDistance <= RotationStartDistance) return;
                // Consume the small initial movement instead of releasing all of
                // it at once when the grab threshold is crossed. Reaching toward
                // the object alone cannot arm a rotation gesture.
                Vector2 threshold = planarDisplacement / planarDistance * RotationStartDistance;
                previousHandPosition = initialHandPosition + fixedViewRotation * new Vector3(threshold.x, threshold.y, displacement.z);
                manipulating = true;
            }

            Vector3 movement = Quaternion.Inverse(fixedViewRotation) * (handPosition - previousHandPosition);
            if (!Finite(movement) || !Finite(rotationDegreesPerMetre)) return;
            Vector2 planarMovement = new Vector2(movement.x, movement.y);
            float distance = planarMovement.magnitude;
            if (!Finite(distance)) return;
            if (distance < .000001f)
            {
                // Depth is deliberately ignored. Reaching toward the exhibit
                // must not introduce an unexpected twist around the view axis.
                previousHandPosition = handPosition;
                return;
            }
            float gain = Mathf.Clamp(rotationDegreesPerMetre, 1f, 1440f);
            float radius = Mathf.Clamp(Mathf.Rad2Deg / gain, .08f, .45f);
            Vector2 nextPoint = Vector2.ClampMagnitude(trackballPoint + planarMovement / radius, MaximumTrackballOffset);
            Vector3 nearSurface = ProjectTrackball((trackballPoint + nextPoint) * .5f);
            Vector3 localAxis = Vector3.Cross(nearSurface, new Vector3(planarMovement.x, planarMovement.y, 0f));
            float axisMagnitude = localAxis.magnitude;
            float angle = distance * gain;
            if (!Finite(nextPoint) || !Finite(localAxis) || !Finite(axisMagnitude) || !Finite(angle) || axisMagnitude < .0000001f) return;

            // In the frozen user view the near surface faces -Z. Cross(-Z,+X)
            // points down, so dragging right carries that surface to the right;
            // dragging up similarly lifts it. A curved planar drag introduces
            // roll through the ball tangent, rather than an unrelated depth axis.
            Vector3 worldAxis = fixedViewRotation * (localAxis / axisMagnitude);
            Quaternion deltaRotation = Quaternion.AngleAxis(angle, worldAxis);
            Quaternion rawRotation = deltaRotation * root.rotation;
            if (!Finite(deltaRotation) || !Finite(rawRotation)) return;
            Quaternion rotation = rawRotation.normalized;
            if (!Finite(rotation)) return;

            // Keep the actual mesh centre stationary even when its root is at the
            // breastplate. This changes root position only to compensate rotation.
            Quaternion previousRotation = root.rotation;
            root.rotation = rotation;
            Vector3 centreAfterRotation = root.TransformPoint(fixedCentreLocal);
            Vector3 position = root.position + fixedWorldCentre - centreAfterRotation;
            if (!Finite(centreAfterRotation) || !Finite(position))
            {
                root.rotation = previousRotation;
                return;
            }
            root.position = position;
            // XRI may query colliders again before the next FixedUpdate. Update
            // the pinned body's pose too, then flush child hotspot transforms.
            if (exhibitBody != null)
            {
                exhibitBody.position = position;
                exhibitBody.rotation = rotation;
            }
            Physics.SyncTransforms();
            trackballPoint = nextPoint;
            previousHandPosition = handPosition;
        }

        static Vector3 ProjectTrackball(Vector2 point)
        {
            // Sphere near its centre, smoothly joined hyperbola near the rim.
            // Keeping a non-zero near-facing component avoids an ambiguous axis
            // at the silhouette while still allowing deliberate circular rolls.
            float squaredRadius = point.sqrMagnitude;
            float depth = squaredRadius <= .5f
                ? Mathf.Sqrt(Mathf.Max(0f, 1f - squaredRadius))
                : .5f / Mathf.Sqrt(squaredRadius);
            return new Vector3(point.x, point.y, -depth).normalized;
        }

        void CacheExhibitBody()
        {
            if (exhibitBody == null && inspection != null)
                exhibitBody = inspection.GetComponent<Rigidbody>();
        }

        DistanceInfo GestureDistance(IXRInteractable interactable, Vector3 position)
        {
            var result = new DistanceInfo { distanceSqr = float.MaxValue, point = transform.position };
            if (!Finite(position)) return result;
            foreach (var candidate in colliders)
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;
                Vector3 point = candidate.ClosestPoint(position);
                float distance = (position - point).sqrMagnitude;
                if (!Finite(point) || !Finite(distance)) continue;
                if (distance < result.distanceSqr)
                    result = new DistanceInfo { point = point, distanceSqr = distance, collider = candidate };
            }
            return result;
        }

        static bool TryHandPosition(IXRSelectInteractor interactor, out Vector3 position)
        {
            position = Vector3.zero;
            if (HasDisappeared(interactor)) return false;
            // Ray attach transforms can move when hit distance changes; use the
            // tracked controller, including the explicit physical-hand proxy link.
            var controller = (interactor as XRBaseControllerInteractor)?.xrController;
            if (controller == null && interactor.transform != null)
                controller = interactor.transform.GetComponentInParent<XRBaseController>();
            Transform source = controller != null ? controller.transform : interactor.transform;
            if (source == null) return false;
            position = source.position;
            return Finite(position);
        }

        static bool SameHand(IXRSelectInteractor first, IXRSelectInteractor second)
        {
            if (HasDisappeared(first) || HasDisappeared(second)) return false;
            if (first == second) return true;
            var firstController = (first as XRBaseControllerInteractor)?.xrController;
            var secondController = (second as XRBaseControllerInteractor)?.xrController;
            if (firstController == null && first.transform != null)
                firstController = first.transform.GetComponentInParent<XRBaseController>();
            if (secondController == null && second.transform != null)
                secondController = second.transform.GetComponentInParent<XRBaseController>();
            return firstController != null && firstController == secondController;
        }

        static bool HasDisappeared(IXRSelectInteractor interactor) =>
            interactor == null || (interactor is Object unityObject && unityObject == null);
        static bool HasReleasedOrDisappeared(IXRSelectInteractor interactor) =>
            HasDisappeared(interactor) || !interactor.isSelectActive;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) &&
            Finite(value.w) && Finite(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w) &&
            value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > .000001f;
    }
}
