using Museum.Localization;
using System.Collections.Generic;
using Museum.Inspection;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace Museum.Exploration
{
    /// <summary>A small optional air clue. Ordinary grabs elsewhere on the helmet remain unchanged.</summary>
    [DisallowMultipleComponent]
    public sealed class HelmetExplorationHotspot : XRSimpleInteractable, IXRSelectFilter
    {
        [SerializeField] HelmetExplorationController exploration;
        [SerializeField] HelmetInspectionController inspection;
        [SerializeField] Renderer airConnector;
        [SerializeField] Material highlightMaterial;
        [SerializeField] Transform viewer;
        [SerializeField] Vector3 localAnchor;
        [SerializeField] bool anchorReady;
        [SerializeField] GameObject marker;
        [SerializeField] TextMeshPro hoverText;
        XRGrabInteractable rootGrab;
        HelmetExhibitReset reset;
        readonly List<IXRSelectInteractor> cancelledInteractors = new List<IXRSelectInteractor>();
        bool filterRegistered, cancelling;
        int observedReset;

        bool Available => isActiveAndEnabled && exploration && exploration.isActiveAndEnabled &&
            inspection && inspection.isActiveAndEnabled;
        bool IXRSelectFilter.canProcess => Available;

        public void Configure(HelmetExplorationController controller, HelmetInspectionController target,
            Renderer connector, Material material, Transform camera)
        {
            RemoveRootFilter();
            exploration = controller; inspection = target; airConnector = connector;
            highlightMaterial = material; viewer = camera;
            anchorReady = false;
            EnsureReferences();
            EnsureTarget();
            if (isActiveAndEnabled)
            {
                AddRootFilter();
                // Configure may create a collider after XRI's first registration.
                if (Application.isPlaying && interactionManager)
                {
                    interactionManager.UnregisterInteractable((IXRInteractable)this);
                    interactionManager.RegisterInteractable((IXRInteractable)this);
                }
            }
        }

        public void RebindConnector(Renderer connector)
        {
            airConnector = connector; anchorReady = false;
            if (isActiveAndEnabled) EnsureTarget();
        }

        protected override void Awake()
        {
            base.Awake();
            selectMode = InteractableSelectMode.Single;
            EnsureReferences();
        }

        protected override void OnEnable()
        {
            EnsureReferences();
            getDistanceOverride = TriggerDistance;
            EnsureTarget();
            base.OnEnable();
            AddRootFilter();
            observedReset = inspection ? inspection.ResetCount : 0;
            SetTargetAvailable(Available);
        }

        protected override void OnDisable()
        {
            CancelSelection();
            RemoveRootFilter();
            SetTargetAvailable(false);
            if (hoverText) hoverText.gameObject.SetActive(false);
            base.OnDisable();
            getDistanceOverride = null;
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (!exploration || !exploration.isActiveAndEnabled || !inspection || !inspection.isActiveAndEnabled ||
                inspection.IsHeld || inspection.IsWindowHeld || inspection.IsSectionHeld || cancelling ||
                observedReset != inspection.ResetCount || cancelledInteractors.Contains(interactor)) return false;
            // Respect the original reset's release guard; never bypass it for a new child target.
            if (reset is IXRSelectFilter filter && filter.canProcess && !filter.Process(interactor, rootGrab)) return false;
            return base.IsSelectableBy(interactor);
        }

        public override bool IsHoverableBy(IXRHoverInteractor interactor) => Available && base.IsHoverableBy(interactor);

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
        {
            base.ProcessInteractable(phase);
            if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
            cancelledInteractors.RemoveAll(ReleasedOrGone);
            if (inspection && observedReset != inspection.ResetCount)
            {
                observedReset = inspection.ResetCount;
                CancelSelection();
            }
            bool available = Available;
            SetTargetAvailable(available);
            if (!available) CancelSelection();
            bool showHint = available && (isHovered || isSelected);
            if (hoverText)
            {
                hoverText.gameObject.SetActive(showHint);
                if (showHint && viewer)
                {
                    Vector3 forward = hoverText.transform.position - viewer.position;
                    if (forward.sqrMagnitude > .0001f) hoverText.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                }
            }
            if (marker) marker.transform.localScale = Vector3.one * (showHint ? .041f : .032f);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            if (exploration && exploration.isActiveAndEnabled)
                exploration.SelectClue(HelmetExplorationController.Clue.Air);
        }

        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (!Available) return true;
            cancelledInteractors.RemoveAll(ReleasedOrGone);
            if (cancelledInteractors.Contains(interactor)) return false;
            if (!rootGrab || rootGrab.IsSelected(interactor)) return true;
            var physical = Museum.PhysicalHands.MuseumHandGrabTransformer.PhysicalHand(interactor);
            if (physical && !physical.IsDirectContact(this)) return !isSelected;
            return !isSelected && !(interactor is IXRHoverInteractor hover && IsHovered(hover));
        }

        void EnsureReferences()
        {
            if (!inspection) inspection = GetComponentInParent<HelmetInspectionController>();
            if (inspection)
            {
                rootGrab = inspection.GetComponent<XRGrabInteractable>();
                reset = inspection.GetComponent<HelmetExhibitReset>();
            }
            if (!viewer && Camera.main) viewer = Camera.main.transform;
        }

        void EnsureTarget()
        {
            if (!inspection || !airConnector || !highlightMaterial) return;
            if (!anchorReady)
            {
                localAnchor = inspection.transform.InverseTransformPoint(airConnector.bounds.center);
                anchorReady = true;
            }
            transform.position = inspection.transform.TransformPoint(localAnchor);
            // Builder parents this object to the helmet; keep pose attached even if called before parenting.
            if (transform.parent != inspection.transform) transform.SetParent(inspection.transform, true);
            if (!marker)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Air clue marker";
                marker.transform.SetParent(transform, false);
                marker.transform.localScale = Vector3.one * .032f;
                marker.GetComponent<Renderer>().sharedMaterial = highlightMaterial;
                var markerCollider = marker.GetComponent<Collider>(); markerCollider.enabled = false;
                if (Application.isPlaying) Destroy(markerCollider); else DestroyImmediate(markerCollider);
            }
            var targetCollider = GetComponent<SphereCollider>();
            if (!targetCollider) targetCollider = gameObject.AddComponent<SphereCollider>();
            targetCollider.center = Vector3.zero; targetCollider.radius = .030f; targetCollider.isTrigger = true;
            colliders.Clear(); colliders.Add(targetCollider);
            interactionLayers = 1;
            if (!hoverText)
            {
                hoverText = new GameObject("Air clue action hint").AddComponent<TextMeshPro>();
                hoverText.transform.SetParent(transform, false);
                hoverText.transform.localPosition = Vector3.up * .085f;
                hoverText.font = TMP_Settings.defaultFontAsset;
                MuseumText.Set(hoverText, "AIR CONNECTOR\nGrip to explore");
                hoverText.color = Color.white;
                hoverText.alignment = TextAlignmentOptions.Center;
                hoverText.rectTransform.sizeDelta = new Vector2(.34f, .085f);
                hoverText.enableWordWrapping = false; hoverText.enableAutoSizing = true;
                hoverText.fontSizeMin = .5f; hoverText.fontSizeMax = 1.2f;
                hoverText.gameObject.SetActive(false);
            }
        }

        void AddRootFilter()
        {
            if (!rootGrab || filterRegistered) return;
            rootGrab.selectFilters.Add(this);
            filterRegistered = true;
        }

        void SetTargetAvailable(bool available)
        {
            if (marker && marker.activeSelf != available) marker.SetActive(available);
            foreach (var hit in colliders) if (hit && hit.enabled != available) hit.enabled = available;
        }

        void RemoveRootFilter()
        {
            if (rootGrab && filterRegistered) rootGrab.selectFilters.Remove(this);
            filterRegistered = false;
        }

        void CancelSelection()
        {
            if (cancelling) return;
            cancelling = true;
            try
            {
                var selectors = interactorsSelecting.ToArray();
                foreach (var interactor in selectors)
                {
                    if (!cancelledInteractors.Contains(interactor)) cancelledInteractors.Add(interactor);
                    if (interactionManager) interactionManager.SelectExit(interactor, this);
                }
            }
            finally { cancelling = false; }
        }

        DistanceInfo TriggerDistance(IXRInteractable interactable, Vector3 position)
        {
            var result = new DistanceInfo { distanceSqr = float.MaxValue, point = transform.position };
            foreach (var hit in colliders)
            {
                if (!hit || !hit.enabled || !hit.gameObject.activeInHierarchy) continue;
                Vector3 closest = hit.ClosestPoint(position);
                float distance = (closest - position).sqrMagnitude;
                if (distance < result.distanceSqr) result = new DistanceInfo { distanceSqr = distance, point = closest, collider = hit };
            }
            return result;
        }

        static bool ReleasedOrGone(IXRSelectInteractor interactor) => interactor == null ||
            (interactor is Object unityObject && unityObject == null) || !interactor.isSelectActive;
    }
}
