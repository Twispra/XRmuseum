using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Hotspots
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable), typeof(Collider))]
    public sealed class HelmetHotspot : MonoBehaviour
    {
        [SerializeField] HelmetHotspotTour tour;
        [SerializeField] string identifier;
        [SerializeField] string title;
        [SerializeField, TextArea] string description;
        [SerializeField] Renderer targetRenderer;
        [SerializeField] Color availableColor = new Color(0.12f, 0.8f, 0.9f, 0.75f);
        [SerializeField] Color hoverColor = new Color(1f, 0.68f, 0.15f, 1f);
        [SerializeField] Color visitedColor = new Color(0.25f, 0.95f, 0.5f, 0.85f);

        MaterialPropertyBlock propertyBlock;
        XRSimpleInteractable interactable;
        Func<IXRInteractable, Vector3, DistanceInfo> previousDistance;

        public bool IsVisited => tour != null && tour.HasVisited(identifier);

        public void Configure(HelmetHotspotTour targetTour, string id, string heading, string details, Renderer visual)
        {
            if (tour != null)
                tour.ResetPerformed -= ResetState;

            tour = targetTour;
            identifier = id;
            title = heading;
            description = details;
            targetRenderer = visual;

            if (tour != null && isActiveAndEnabled)
                tour.ResetPerformed += ResetState;

            ResetState();
        }

        public void Activate()
        {
            if (tour == null)
                return;

            tour.Visit(identifier, title, description);
            ApplyColor(IsVisited ? visitedColor : availableColor);
        }

        public void SetMarkerVisible(bool visible)
        {
            if (targetRenderer != null)
                targetRenderer.enabled = visible;
        }

        void OnEnable()
        {
            propertyBlock = new MaterialPropertyBlock();
            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable.getDistanceOverride != HotspotDistance)
                previousDistance = interactable.getDistanceOverride;
            // XRI's default collider distance omits triggers, placing these small
            // markers behind the solid helmet in a direct interactor's target list.
            interactable.getDistanceOverride = HotspotDistance;
            if (tour != null)
            {
                tour.ResetPerformed -= ResetState;
                tour.ResetPerformed += ResetState;
            }
            interactable.selectEntered.AddListener(OnSelected);
            interactable.activated.AddListener(OnActivated);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            ApplyColor(IsVisited ? visitedColor : availableColor);
        }

        void OnDisable()
        {
            if (tour != null)
                tour.ResetPerformed -= ResetState;
            if (interactable == null)
                return;

            if (interactable.getDistanceOverride == HotspotDistance)
                interactable.getDistanceOverride = previousDistance;

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.activated.RemoveListener(OnActivated);
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        void OnDestroy()
        {
            if (tour != null)
                tour.ResetPerformed -= ResetState;
        }

        void OnSelected(SelectEnterEventArgs args)
        {
            Activate();
        }

        void OnActivated(ActivateEventArgs args)
        {
            Activate();
        }

        void OnMouseDown()
        {
            Activate();
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            ApplyColor(hoverColor);
        }

        void OnHoverExited(HoverExitEventArgs args)
        {
            ApplyColor(IsVisited ? visitedColor : availableColor);
        }

        void ResetState()
        {
            ApplyColor(IsVisited ? visitedColor : availableColor);
        }

        DistanceInfo HotspotDistance(IXRInteractable target, Vector3 position)
        {
            var nearest = new DistanceInfo { distanceSqr = float.MaxValue, point = transform.position };
            foreach (var collider in interactable.colliders)
            {
                if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                var point = collider.ClosestPoint(position);
                var distance = (point - position).sqrMagnitude;
                if (distance < nearest.distanceSqr)
                    nearest = new DistanceInfo { point = point, distanceSqr = distance, collider = collider };
            }
            return nearest;
        }

        void ApplyColor(Color color)
        {
            if (targetRenderer == null)
                return;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
