using System;
using UnityEngine;
using UnityEngine.Rendering;
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
        LineRenderer hoverOutline;
        LineRenderer visitedCheck;
        bool markerVisible;

        public bool IsVisited => tour != null && tour.HasVisited(identifier);
        public string Identifier => identifier;
        public string Title => title;
        public string Description => description;

        // A visual-era change keeps the same discovery identity and completion state.
        public void SetContent(string heading, string details)
        {
            title = heading;
            description = details;
            if (tour != null)
                tour.RefreshSelectedContent(identifier, title, description);
        }

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

            markerVisible = targetRenderer != null && targetRenderer.enabled;
            EnsureStateCues();
            ResetState();
        }

        public void Activate()
        {
            if (tour == null)
                return;

            tour.Visit(identifier, title, description);
            ApplyVisualState();
        }

        public void SetMarkerVisible(bool visible)
        {
            markerVisible = visible;
            if (targetRenderer != null)
                targetRenderer.enabled = visible;
            ApplyVisualState();
        }

        void OnEnable()
        {
            propertyBlock = new MaterialPropertyBlock();
            interactable = GetComponent<XRSimpleInteractable>();
            markerVisible = targetRenderer != null && targetRenderer.enabled;
            EnsureStateCues();
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
            ApplyVisualState();
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
            SetCueActive(hoverOutline, false);
            SetCueActive(visitedCheck, false);
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
            ApplyVisualState();
        }

        void OnHoverExited(HoverExitEventArgs args)
        {
            ApplyVisualState();
        }

        void ResetState()
        {
            ApplyVisualState();
        }

        void LateUpdate()
        {
            if ((hoverOutline != null && hoverOutline.gameObject.activeSelf) ||
                (visitedCheck != null && visitedCheck.gameObject.activeSelf))
                UpdateCueGeometry();
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

        void ApplyVisualState()
        {
            // XRI updates the aggregate hover state before these callbacks. One
            // interactor exiting must not clear another interactor's hover cue.
            bool isHovered = isActiveAndEnabled && interactable != null && interactable.isHovered;
            Color color = isHovered ? hoverColor : IsVisited ? visitedColor : availableColor;
            ApplyColor(color);
            EnsureStateCues();
            SetCueActive(hoverOutline, markerVisible && isHovered);
            SetCueActive(visitedCheck, markerVisible && IsVisited);
            ApplyCueColor(hoverOutline, hoverColor);
            ApplyCueColor(visitedCheck, visitedColor);
            UpdateCueGeometry();
        }

        void EnsureStateCues()
        {
            if (targetRenderer == null)
                return;

            var stateCues = transform.Find("State Cues");
            if (stateCues == null)
            {
                var cueObject = new GameObject("State Cues");
                cueObject.layer = gameObject.layer;
                stateCues = cueObject.transform;
                stateCues.SetParent(transform, false);
            }

            hoverOutline = GetOrCreateLine(stateCues, "Hover Outline", true, 32);
            visitedCheck = GetOrCreateLine(stateCues, "Visited Check", false, 3);
        }

        LineRenderer GetOrCreateLine(Transform parent, string objectName, bool loop, int positionCount)
        {
            var child = parent.Find(objectName);
            if (child == null)
            {
                var childObject = new GameObject(objectName);
                childObject.layer = gameObject.layer;
                child = childObject.transform;
                child.SetParent(parent, false);
            }

            var line = child.GetComponent<LineRenderer>();
            if (line == null)
                line = child.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = positionCount;
            line.sharedMaterial = targetRenderer.sharedMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.gameObject.SetActive(false);
            return line;
        }

        void UpdateCueGeometry()
        {
            if (targetRenderer == null || hoverOutline == null || visitedCheck == null)
                return;

            float markerSize = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            float radius = markerSize * 0.72f;
            var camera = Camera.main;
            Vector3 forward = camera != null ? (camera.transform.position - transform.position).normalized : -transform.forward;
            Vector3 up = camera != null ? camera.transform.up : transform.up;
            Vector3 right = camera != null ? camera.transform.right : transform.right;
            Vector3 center = transform.position + forward * markerSize * 0.56f;

            hoverOutline.widthMultiplier = markerSize * 0.13f;
            for (int i = 0; i < hoverOutline.positionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / hoverOutline.positionCount;
                hoverOutline.SetPosition(i, center + right * Mathf.Cos(angle) * radius + up * Mathf.Sin(angle) * radius);
            }

            visitedCheck.widthMultiplier = markerSize * 0.18f;
            visitedCheck.SetPosition(0, center - right * radius * 0.65f);
            visitedCheck.SetPosition(1, center - right * radius * 0.15f - up * radius * 0.5f);
            visitedCheck.SetPosition(2, center + right * radius * 0.75f + up * radius * 0.55f);
        }

        void ApplyCueColor(Renderer cue, Color color)
        {
            if (cue == null)
                return;
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            cue.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            cue.SetPropertyBlock(propertyBlock);
        }

        static void SetCueActive(Renderer cue, bool active)
        {
            if (cue != null)
                cue.gameObject.SetActive(active);
        }
    }
}
