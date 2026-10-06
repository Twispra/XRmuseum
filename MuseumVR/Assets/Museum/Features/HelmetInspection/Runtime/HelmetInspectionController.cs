using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace Museum.Inspection
{
    /// <summary>Articulated front window and an adjustable, helmet-local inspection plane.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HelmetExhibitReset), typeof(XRGrabInteractable), typeof(Rigidbody))]
    public sealed class HelmetInspectionController : MonoBehaviour, IXRSelectFilter
    {
        public const float MaximumWindowAngle = 95f;
        [SerializeField] Transform windowPivot;
        [SerializeField] Renderer[] clippedRenderers = new Renderer[0];
        [SerializeField] Renderer[] highlightedRenderers = new Renderer[0];
        [SerializeField] float openAngleSign = -1f;
        [SerializeField, Min(1f)] float windowSpeed = 150f;
        [SerializeField] bool reduceMotion;
        static readonly int CutEnabledId = Shader.PropertyToID("_CutEnabled");
        static readonly int CutPlaneId = Shader.PropertyToID("_CutPlane");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        MaterialPropertyBlock propertyBlock;
        HelmetExhibitReset exhibitReset;
        XRGrabInteractable grab;
        Rigidbody body;
        HelmetWindowInteractable windowInteraction;
        HelmetSectionInteractable sectionInteraction;
        Vector3 sectionNormalLocal = Vector3.right;
        Vector3 sectionCenterLocal;
        Vector3 sectionExtentLocal = Vector3.one * .2f;
        float sectionOffset, targetWindowAngle;
        float windowFrom, windowStartedAt, windowDuration;
        Museum.Experience.HelmetMechanicalFeedback feedback;
        int observedResetCount;
        bool clipDirty = true, boundsReady, filterRegistered;
        bool interactionBodyPinned, pinnedWasKinematic, pinnedUsedGravity, windowPinOwner, sectionPinOwner;
        bool initialPhysicsCaptured, initialIsKinematic, initialUseGravity, previousThrowOnDetach;

        public float WindowAngle { get; private set; }
        public bool IsCutaway { get; private set; }
        public bool IsFloating { get; private set; }
        public bool IsHeld => grab != null && grab.isSelected;
        public bool IsWindowHeld => windowInteraction != null && windowInteraction.isSelected;
        public bool IsSectionHeld => sectionInteraction != null && sectionInteraction.isSelected;
        public bool IsReturning => exhibitReset != null && exhibitReset.IsReturning;
        public bool SoundEnabled
        {
            get { EnsureReferences(); return feedback != null && feedback.SoundEnabled; }
            set { EnsureReferences(); if(feedback != null)feedback.SoundEnabled=value; }
        }
        public Transform WindowPivot => windowPivot;
        public bool ReduceMotion { get => reduceMotion; set => reduceMotion = value; }
        public int ResetCount => exhibitReset != null ? exhibitReset.ResetCount : 0;
        public Vector3 SectionNormalLocal => sectionNormalLocal;
        public Vector3 SectionCenterLocal { get { EnsureSectionBounds(); return sectionCenterLocal; } }
        public Vector3 SectionExtentLocal { get { EnsureSectionBounds(); return sectionExtentLocal; } }
        public Vector3 SectionPointLocal => SectionCenterLocal + sectionNormalLocal * sectionOffset;
        public float SectionOffset => sectionOffset;
        // Leave some shell at both ends. This is a UI limit, not a historical dimension.
        public float SectionRange => .85f * ProjectedExtent(sectionNormalLocal);
        public Vector4 WorldSectionPlane
        {
            get
            {
                Vector3 normal = transform.worldToLocalMatrix.transpose.MultiplyVector(sectionNormalLocal).normalized;
                Vector3 point = transform.TransformPoint(SectionPointLocal);
                return new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
            }
        }
        internal float OpenAngleSign => openAngleSign < 0f ? -1f : 1f;
        // Keep release-before-regrab protection while this component is disabled.
        bool IXRSelectFilter.canProcess => grab != null;

        void Awake() { EnsureReferences(); ResetSectionGeometry(); }
        void OnEnable()
        {
            EnsureReferences();
            if (!filterRegistered) { grab.selectFilters.Add(this); filterRegistered = true; }
            observedResetCount = exhibitReset.ResetCount;
            clipDirty = true;
        }
        void OnDisable()
        {
            CancelChildInteractions();
            if (grab != null && grab.isSelected && exhibitReset != null) exhibitReset.ResetToDisplay();
            RestoreInspectionState();
        }
        void OnDestroy()
        {
            if (grab != null && filterRegistered) grab.selectFilters.Remove(this);
        }
        void LateUpdate()
        {
            if (exhibitReset != null && observedResetCount != exhibitReset.ResetCount)
            {
                observedResetCount = exhibitReset.ResetCount;
                CancelChildInteractions();
                RestoreInspectionState();
            }
            if (!IsWindowHeld && !Mathf.Approximately(WindowAngle, targetWindowAngle))
            {
                float t=windowDuration>0 ? Mathf.Clamp01((Time.unscaledTime-windowStartedAt)/windowDuration):1;
                ApplyWindowAngle(reduceMotion ? targetWindowAngle : Mathf.Lerp(windowFrom,targetWindowAngle,t*t*(3-2*t)),reduceMotion);
            }
            // XRI owns physics while grabbed. Once released, the inspection pose stays put.
            if (IsFloating && !IsHeld && !interactionBodyPinned && !IsReturning) ApplyFloatingPhysics();
            if (IsCutaway || clipDirty) ApplyCutawayProperties();
        }

        /// <summary>Closed pivot rotation is identity; its local Y is the hinge axis.</summary>
        public void Configure(Transform pivot, Renderer[] clipped, Renderer[] highlighted, float openAngleSign = -1f)
        {
            EnsureReferences();
            windowPivot = pivot;
            clippedRenderers = clipped ?? new Renderer[0];
            highlightedRenderers = highlighted ?? new Renderer[0];
            this.openAngleSign = openAngleSign < 0f ? -1f : 1f;
            boundsReady = false;
            ResetSectionGeometry();
            ApplyWindowAngle(WindowAngle);
            ApplyCutawayProperties();
            SetWindowHover(false);
        }
        public void ToggleWindow() => SetWindowOpen(targetWindowAngle < MaximumWindowAngle * .5f);
        public void SetWindowOpen(bool open, bool immediate = false)
        {
            if (!isActiveAndEnabled || IsWindowHeld || IsReturning) return;
            windowFrom=WindowAngle;windowStartedAt=Time.unscaledTime;
            targetWindowAngle = open ? MaximumWindowAngle : 0f;
            windowDuration=Mathf.Max(.05f,Mathf.Abs(targetWindowAngle-windowFrom)/windowSpeed);
            if (immediate || reduceMotion || !Application.isPlaying) ApplyWindowAngle(targetWindowAngle,true);
        }
        public void SetWindowAngle(float degrees)
        {
            if (!isActiveAndEnabled || IsReturning || !IsFinite(degrees)) return;
            targetWindowAngle = Mathf.Clamp(degrees, 0f, MaximumWindowAngle);
            ApplyWindowAngle(targetWindowAngle);
        }
        public void ToggleCutaway() => SetCutaway(!IsCutaway);
        public void SetCutaway(bool enabled)
        {
            if (!isActiveAndEnabled || IsReturning) return;
            if (!enabled && sectionInteraction != null) sectionInteraction.ShowTool = false;
            IsCutaway = enabled;
            ApplyCutawayProperties();
        }
        /// <summary>Offset is measured from mesh-bounds centre along the local unit normal.</summary>
        public void SetSectionPlane(Vector3 localNormal, float localOffset)
        {
            if (!isActiveAndEnabled || IsReturning || !IsFinite(localNormal) || !IsFinite(localOffset) || localNormal.sqrMagnitude < .000001f) return;
            sectionNormalLocal = localNormal.normalized;
            sectionOffset = Mathf.Clamp(localOffset, -SectionRange, SectionRange);
            ApplyCutawayProperties();
        }
        /// <summary>The tool's forward axis is its world-space normal; position is a point on it.</summary>
        public void SetSectionFromWorldPose(Vector3 worldPoint, Quaternion worldRotation)
        {
            if (!IsFinite(worldPoint) || !IsFinite(worldRotation)) return;
            Vector3 worldNormal = worldRotation.normalized * Vector3.forward;
            // Invert the normal transform, preserving non-uniform ancestor scales.
            Vector3 localNormal = transform.localToWorldMatrix.transpose.MultiplyVector(worldNormal).normalized;
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            SetSectionPlane(localNormal, Vector3.Dot(localNormal, localPoint - SectionCenterLocal));
        }
        public void SetSectionPreset(int index)
        {
            if (!isActiveAndEnabled || IsSectionHeld) return;
            Vector3 normal = index == 1 ? Vector3.up : index == 2 ? Vector3.back : Vector3.right;
            SetSectionPlane(normal, index == 0 ? -SectionCenterLocal.x : 0f);
        }
        public void AlignSectionToViewer(Transform viewer)
        {
            if (viewer == null || !isActiveAndEnabled || IsSectionHeld) return;
            Vector3 centre = transform.TransformPoint(SectionCenterLocal);
            Vector3 normal = viewer.position - centre;
            if (normal.sqrMagnitude < .0001f) normal = -viewer.forward;
            normal.Normalize();
            SetSectionFromWorldPose(centre, Quaternion.LookRotation(normal, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > .98f ? Vector3.forward : Vector3.up));
        }
        public void FlipSection()
        {
            if (!IsSectionHeld) SetSectionPlane(-sectionNormalLocal, -sectionOffset);
        }
        public void AdjustSectionDepth(float normalizedDelta)
        {
            if (IsFinite(normalizedDelta) && !IsSectionHeld)
                SetSectionPlane(sectionNormalLocal, sectionOffset + Mathf.Clamp(normalizedDelta, -2f, 2f) * SectionRange);
        }
        public void ResetSection()
        {
            if (!isActiveAndEnabled) return;
            if (sectionInteraction != null) sectionInteraction.ShowTool = false;
            ResetSectionGeometry();
            ApplyCutawayProperties();
        }
        public void ToggleFloating()
        {
            if (!isActiveAndEnabled || IsReturning || IsHeld || IsWindowHeld || IsSectionHeld) return;
            EnsureReferences();
            SetFloating(!IsFloating);
        }
        /// <summary>Legacy yaw step retains the original dynamic-body behaviour.</summary>
        public void RotateDisplay(float degrees) => RotateRoot(Vector3.up, degrees, false);
        /// <summary>Any-axis rotation suspends the body so tilted poses do not fall over.</summary>
        public void RotateDisplayAround(Vector3 worldAxis, float degrees) => RotateRoot(worldAxis, degrees, true);
        void RotateRoot(Vector3 worldAxis, float degrees, bool suspend)
        {
            if (!isActiveAndEnabled || IsReturning || IsHeld || IsWindowHeld || IsSectionHeld || !IsFinite(degrees) || !IsFinite(worldAxis) || worldAxis.sqrMagnitude < .000001f) return;
            EnsureReferences();
            if (suspend && !IsFloating) SetFloating(true);
            Quaternion delta = Quaternion.AngleAxis(degrees, worldAxis.normalized);
            Quaternion rotation = delta * transform.rotation;
            Vector3 centre = transform.TransformPoint(SectionCenterLocal);
            // Tilt about the object's centre rather than the collar origin.
            Vector3 position = suspend ? centre + delta * (transform.position - centre) : transform.position;
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            ClearVelocity();
            Physics.SyncTransforms();
            clipDirty = true;
        }
        public void ResetInspection()
        {
            EnsureReferences();
            CancelChildInteractions();
            exhibitReset.ResetToDisplay();
            observedResetCount = exhibitReset.ResetCount;
            RestoreInspectionState();
        }
        /// <summary>Visitor-requested return. The immediate ResetInspection API remains available for recovery/fixtures.</summary>
        public void ReturnInspectionToDisplay()
        {
            EnsureReferences();
            if(IsReturning)return;
            CancelChildInteractions();RestoreInspectionState();
            exhibitReset.RequestResetToDisplay();observedResetCount=exhibitReset.ResetCount;
        }
        internal void RegisterWindow(HelmetWindowInteractable interactable) => windowInteraction = interactable;
        internal void RegisterSection(HelmetSectionInteractable interactable) => sectionInteraction = interactable;
        internal bool CanSelectWindow(IXRSelectInteractor interactor)
        {
            return CanSelectChild(interactor) && !IsSectionHeld && (sectionInteraction == null || sectionInteraction.CanSelectOtherPart(interactor));
        }
        internal bool CanSelectSection(IXRSelectInteractor interactor)
        {
            return IsCutaway && CanSelectChild(interactor) && !IsWindowHeld && (windowInteraction == null || windowInteraction.CanSelectWholeHelmet(interactor));
        }
        bool CanSelectChild(IXRSelectInteractor interactor)
        {
            if (!isActiveAndEnabled || interactor == null || (interactor is Object obj && obj == null)) return false;
            EnsureReferences();
            if (grab == null || exhibitReset == null || IsHeld) return false;
            var resetFilter = (IXRSelectFilter)exhibitReset;
            return !resetFilter.canProcess || resetFilter.Process(interactor, grab);
        }
        internal void BeginWindowInteraction() { targetWindowAngle = WindowAngle; windowPinOwner = true; PinBody(); }
        internal void EndWindowInteraction() { windowPinOwner = false; if (!sectionPinOwner) UnpinBody(); }
        internal void BeginSectionInteraction() { sectionPinOwner = true; PinBody(); }
        internal void EndSectionInteraction() { sectionPinOwner = false; if (!windowPinOwner) UnpinBody(); }
        void PinBody()
        {
            EnsureReferences();
            if (interactionBodyPinned) return;
            pinnedWasKinematic = body.isKinematic;
            pinnedUsedGravity = body.useGravity;
            ClearVelocity();
            body.isKinematic = true;
            body.useGravity = false;
            interactionBodyPinned = true;
        }
        void UnpinBody()
        {
            if (!interactionBodyPinned) return;
            interactionBodyPinned = false;
            if (body == null) return;
            body.isKinematic = IsFloating || pinnedWasKinematic;
            body.useGravity = !IsFloating && pinnedUsedGravity;
            ClearVelocity();
        }
        internal void SetWindowHover(bool hovered)
        {
            EnsurePropertyBlock();
            foreach (var renderer in highlightedRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(HoverId, hovered && isActiveAndEnabled ? 1f : 0f);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if ((windowInteraction != null && !windowInteraction.CanSelectWholeHelmet(interactor)) || (sectionInteraction != null && !sectionInteraction.CanSelectOtherPart(interactor))) return false;
            if (!isActiveAndEnabled) return true;
            // A grip aimed at a handle must not silently select the entire exhibit.
            if (grab != null && !grab.IsSelected(interactor) && interactor is IXRHoverInteractor hover &&
                ((windowInteraction != null && windowInteraction.IsHovered(hover)) ||
                 (sectionInteraction != null && IsCutaway && sectionInteraction.ShowTool && sectionInteraction.IsHovered(hover)))) return false;
            return !IsWindowHeld && !IsSectionHeld && !interactionBodyPinned;
        }
        void EnsureReferences()
        {
            if (exhibitReset == null) exhibitReset = GetComponent<HelmetExhibitReset>();
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
            if (body == null) body = GetComponent<Rigidbody>();
            if (feedback == null) feedback = GetComponent<Museum.Experience.HelmetMechanicalFeedback>();
            if (!initialPhysicsCaptured && body != null && grab != null)
            {
                initialIsKinematic = body.isKinematic;
                initialUseGravity = body.useGravity;
                previousThrowOnDetach = grab.throwOnDetach;
                initialPhysicsCaptured = true;
            }
            EnsurePropertyBlock();
        }
        void EnsurePropertyBlock() { if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock(); }
        void EnsureSectionBounds()
        {
            if (boundsReady) return;
            bool found = false;
            var bounds = new Bounds();
            var rearConnections=GetComponent<Museum.Experience.HelmetRearConnections>();
            foreach (var renderer in clippedRenderers)
            {
                if (renderer == null) continue;
                // Keep the original inspection centre/range stable when adding illustrative rear fittings.
                if(rearConnections && rearConnections.IsTelephoneSurface(renderer))continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Bounds meshBounds = filter.sharedMesh.bounds;
                Matrix4x4 local = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    point = local.MultiplyPoint3x4(point);
                    if (!IsFinite(point)) continue;
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            sectionCenterLocal = found ? bounds.center : Vector3.zero;
            sectionExtentLocal = found ? Vector3.Max(bounds.extents, Vector3.one * .01f) : Vector3.one * .2f;
            boundsReady = true;
        }
        float ProjectedExtent(Vector3 normal)
        {
            EnsureSectionBounds();
            return Mathf.Abs(normal.x) * sectionExtentLocal.x + Mathf.Abs(normal.y) * sectionExtentLocal.y + Mathf.Abs(normal.z) * sectionExtentLocal.z;
        }
        void ResetSectionGeometry()
        {
            EnsureSectionBounds();
            sectionNormalLocal = Vector3.right;
            // Retain the original X=0 plane for the existing specimen and controls.
            sectionOffset = Mathf.Clamp(-sectionCenterLocal.x, -SectionRange, SectionRange);
        }
        void ClearVelocity()
        {
            if (body != null && !body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.Sleep(); }
        }
        void SetFloating(bool enabled)
        {
            if (IsFloating == enabled) return;
            if (enabled)
            {
                previousThrowOnDetach = grab.throwOnDetach;
                grab.throwOnDetach = false;
                IsFloating = true;
                ApplyFloatingPhysics();
            }
            else
            {
                IsFloating = false;
                grab.throwOnDetach = previousThrowOnDetach;
                body.isKinematic = initialIsKinematic;
                body.useGravity = initialUseGravity;
                ClearVelocity();
            }
        }
        void ApplyFloatingPhysics() { ClearVelocity(); body.isKinematic = true; body.useGravity = false; }
        void CancelChildInteractions()
        {
            if (windowInteraction != null) windowInteraction.CancelInteraction();
            if (sectionInteraction != null) sectionInteraction.CancelInteraction();
            UnpinBody();
        }
        void RestoreInspectionState()
        {
            if (IsFloating) SetFloating(false);
            targetWindowAngle = 0f;
            ApplyWindowAngle(0f,true);
            IsCutaway = false;
            if (sectionInteraction != null) sectionInteraction.ShowTool = false;
            ResetSectionGeometry();
            ApplyCutawayProperties();
            SetWindowHover(false);
            if(exhibitReset != null && exhibitReset.IsReturning) exhibitReset.MaintainReturnPhysics();
        }
        void ApplyWindowAngle(float degrees,bool silent=false)
        {
            WindowAngle = degrees;
            if (windowPivot != null) windowPivot.localRotation = Quaternion.AngleAxis(degrees * OpenAngleSign, Vector3.up);
            if(feedback != null)feedback.WindowChanged(degrees,silent);
        }
        void ApplyCutawayProperties()
        {
            EnsurePropertyBlock();
            Vector4 plane = WorldSectionPlane;
            ApplyPlaneTo(clippedRenderers, plane);
            // The articulated front window uses the same plane, so looking from the front
            // does not leave an opaque, detached-looking plate across the opened section.
            ApplyPlaneTo(highlightedRenderers, plane);
            clipDirty = false;
        }
        void ApplyPlaneTo(Renderer[] renderers, Vector4 plane)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(CutEnabledId, IsCutaway ? 1f : 0f);
                propertyBlock.SetVector(CutPlaneId, plane);
                bool hollow=renderer.name=="GB_Bonnet_Hollow" || renderer.name=="GB_Corselet_ShoulderPlate";
                propertyBlock.SetFloat("_ShellInterior",hollow?1:0);
                if(hollow)
                {
                    var mesh=renderer.GetComponent<MeshFilter>();
                    propertyBlock.SetVector("_ShellCenter",mesh && mesh.sharedMesh?mesh.sharedMesh.bounds.center:Vector3.zero);
                }
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w) && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > .000001f;
    }
}
