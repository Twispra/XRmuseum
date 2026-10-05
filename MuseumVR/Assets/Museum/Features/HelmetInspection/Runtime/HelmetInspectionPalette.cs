using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Inspection
{
    /// <summary>A world-space, one-hand palette. It stays still while the visitor aims or manipulates.</summary>
    public sealed class HelmetInspectionPalette : MonoBehaviour
    {
        [SerializeField] HelmetInspectionController controller;
        [SerializeField] Transform viewer;
        [SerializeField] GameObject sectionOptions;
        [SerializeField] TextMeshPro status;
        [SerializeField] Vector3 viewerOffset = new Vector3(-.38f, .02f, .78f);
        [SerializeField] bool followEnabled = true;
        // Optional pages share this same placement, hover freeze and obstacle query.
        // Zero padding preserves the original prefab's layout.
        [SerializeField, Min(0f)] float additionalTopPadding;
        bool alternatePageVisible;
        bool contentBoundsOverride;
        float contentBottom, contentTop;
        XRBaseInteractable[] controls;
        Transform backPlate;
        Vector3 anchorPosition;
        Quaternion anchorYaw;
        Vector3 destination;
        Quaternion destinationRotation;
        bool placed;
        bool relocating;
        bool layoutExpanded;
        bool layoutDirty;
        float outsideSince = -1;
        int observedReset;

        public Transform Viewer { get => viewer; set { viewer = value; placed = false; } }
        public bool FollowEnabled { get => followEnabled; set => followEnabled = value; }
        public bool IsFrozen { get; private set; }
        public HelmetInspectionController Controller => controller;

        // Optional presentation owns only the visible content bounds, never XR placement/input.
        public void ConfigureContentBounds(float bottom, float top)
        {
            bottom = Mathf.Clamp(bottom, -.65f, .35f);
            top = Mathf.Clamp(top, bottom + .08f, .65f);
            if (contentBoundsOverride && Mathf.Approximately(contentBottom, bottom) && Mathf.Approximately(contentTop, top)) return;
            contentBoundsOverride = true;
            contentBottom = bottom; contentTop = top;
            layoutDirty = true;
        }

        public void ClearContentBounds()
        {
            if (!contentBoundsOverride) return;
            contentBoundsOverride = false;
            layoutDirty = true;
        }

        public void ConfigureAdditionalPage(float topPadding)
        {
            additionalTopPadding = Mathf.Clamp(topPadding, 0f, .2f);
            controls = GetComponentsInChildren<XRBaseInteractable>(true);
            layoutDirty = true;
        }

        public void SetAlternatePageVisible(bool visible)
        {
            if (alternatePageVisible == visible) return;
            alternatePageVisible = visible;
            layoutDirty = true;
        }

        void PanelBounds(out Vector3 center, out Vector3 size)
        {
            if (contentBoundsOverride)
            {
                center = new Vector3(0f, (contentTop + contentBottom) * .5f, 0f);
                size = new Vector3(.48f, contentTop - contentBottom, .025f);
                return;
            }
            bool expanded = alternatePageVisible || (controller && controller.IsCutaway);
            float bottom = expanded ? -.32f : -.12f;
            float top = .32f + additionalTopPadding;
            center = new Vector3(0f, (top + bottom) * .5f, 0f);
            size = new Vector3(.48f, top - bottom, .025f);
        }

        public void Configure(HelmetInspectionController target, Transform camera, GameObject options, TextMeshPro statusText)
        {
            controller = target; viewer = camera; sectionOptions = options; status = statusText;
            controls = GetComponentsInChildren<XRBaseInteractable>(true);
            backPlate = transform.Find("Palette back");
        }

        void Start()
        {
            controls = GetComponentsInChildren<XRBaseInteractable>(true);
            backPlate = transform.Find("Palette back");
            if (!viewer && Camera.main) viewer = Camera.main.transform;
            observedReset = controller ? controller.ResetCount : 0;
            layoutExpanded = controller && controller.IsCutaway;
            RecenterNow();
        }

        void LateUpdate()
        {
            if (!viewer || !controller) return;
            bool selected = controller.IsHeld || controller.IsWindowHeld || controller.IsSectionHeld;
            bool hovered = false;
            foreach (var control in controls)
                if (control && control.isActiveAndEnabled) { selected |= control.isSelected; hovered |= control.isHovered; }
            bool lost = Vector3.Distance(viewer.position, transform.position) > 1.5f || Vector3.Dot(viewer.forward, transform.position - viewer.position) < -.15f;
            IsFrozen = selected || (hovered && !lost);
            if (layoutExpanded != controller.IsCutaway)
            {
                layoutExpanded = controller.IsCutaway;
                layoutDirty = true;
            }
            bool showSectionOptions = controller.IsCutaway && !alternatePageVisible;
            if (sectionOptions && sectionOptions.activeSelf != showSectionOptions)
                sectionOptions.SetActive(showSectionOptions);
            if (backPlate)
            {
                PanelBounds(out var center, out var size);
                backPlate.localPosition = center;
                backPlate.localScale = size;
            }
            if (status && !contentBoundsOverride)
            {
                status.transform.localPosition = new Vector3(0,controller.IsCutaway ? -.275f : -.075f,-.025f);
                status.text = controller.IsCutaway
                    ? (controller.IsFloating ? "FLOATING  /  SECTION ON" : "SECTION ON") + "\nEDIT CUT: grip the cyan handle, move + twist"
                    : "GRIP TO SELECT  /  ONE HAND\nFLOAT: pick up, turn freely, release to keep pose";
            }
            if (observedReset != controller.ResetCount)
            {
                observedReset = controller.ResetCount;
                placed = false;
            }
            if (!followEnabled || IsFrozen) { outsideSince = -1; return; }
            if (!placed) { RecenterNow(); return; }
            if (layoutDirty)
            {
                anchorPosition = viewer.position; anchorYaw = Yaw();
                ChooseDestination(); relocating = true; layoutDirty = false;
            }
            if (relocating)
            {
                float step = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, destination, step);
                transform.rotation = Quaternion.Slerp(transform.rotation, destinationRotation, step);
                Physics.SyncTransforms();
                if (Vector3.Distance(transform.position, destination) < .002f && Quaternion.Angle(transform.rotation, destinationRotation) < .5f)
                    relocating = false;
                return;
            }
            var yaw = Yaw();
            // Looking at a side button must not make it move away. Turn only after a large heading change.
            bool outside = Vector3.Distance(viewer.position, anchorPosition) > .25f || Quaternion.Angle(yaw, anchorYaw) > 75f;
            if (!outside) { outsideSince = -1; return; }
            if (outsideSince < 0) outsideSince = Time.unscaledTime;
            if (Time.unscaledTime - outsideSince > .65f)
            { anchorPosition = viewer.position; anchorYaw = yaw; ChooseDestination(); relocating = true; outsideSince = -1; }
        }

        public void RecenterNow()
        {
            if (!viewer) return;
            anchorPosition = viewer.position; anchorYaw = Yaw();
            ChooseDestination();
            transform.SetPositionAndRotation(destination, destinationRotation);
            Physics.SyncTransforms();
            outsideSince = -1; placed = true; relocating = false; layoutDirty = false;
        }

        void ChooseDestination()
        {
            // A seated visitor or someone beside the plinth must not have controls placed inside its collider.
            // Progressive pages stay clear of the plinth and farther away, keeping their
            // header inside the forward view. Legacy pages keep their original placement.
            var preferred = contentBoundsOverride ? new Vector3(-.42f, .08f, .99f) : viewerOffset;
            var opposite = contentBoundsOverride ? new Vector3(.42f, .08f, .99f) : new Vector3(.38f,.02f,.78f);
            var offsets = new[] { preferred, opposite,
                new Vector3(-.38f,.18f,.85f),new Vector3(.38f,.18f,.85f),
                new Vector3(-.65f,-.16f,.35f),new Vector3(.65f,-.16f,.35f),
                new Vector3(-.85f,-.12f,.25f),new Vector3(.85f,-.12f,.25f),
                new Vector3(-.38f,-.12f,.20f),new Vector3(.38f,-.12f,.20f),
                new Vector3(-.48f,.25f,.6f),new Vector3(.48f,.25f,.6f) };
            foreach (var offset in offsets)
            {
                var position = anchorPosition + anchorYaw * offset;
                var forward = Vector3.ProjectOnPlane(position - anchorPosition, Vector3.up).normalized;
                var rotation = Quaternion.LookRotation(forward, Vector3.up);
                // Hidden section rows must not push the compact menu above the visitor's view.
                PanelBounds(out var localCenter, out var size);
                var center = position + rotation * localCenter;
                var halfSize = size * .5f + new Vector3(.02f, .02f, .0375f);
                bool blocked = false;
                foreach (var hit in Physics.OverlapBox(center, halfSize, rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(viewer.root)) continue;
                    // A portable panel must not occupy the exhibit being inspected.
                    if (hit.attachedRigidbody && (!controller || !hit.transform.IsChildOf(controller.transform))) continue;
                    blocked = true; break;
                }
                if (blocked) continue;
                destination = position; destinationRotation = rotation; return;
            }
            // No arbitrary teleport across the room: retain the previous known position when locally crowded.
            destination = placed ? transform.position : anchorPosition + anchorYaw * viewerOffset;
            destinationRotation = placed ? transform.rotation : anchorYaw;
        }

        Quaternion Yaw()
        {
            var direction = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            return direction.sqrMagnitude > .01f ? Quaternion.LookRotation(direction, Vector3.up) : (placed ? anchorYaw : Quaternion.identity);
        }
    }
}
