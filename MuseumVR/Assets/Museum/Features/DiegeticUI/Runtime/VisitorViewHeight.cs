using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Museum.Diegetic
{
    /// <summary>
    /// Adds a per-session viewing-height preference after XROrigin's tracking-origin offset.
    /// Tracked camera/controller poses, the physical floor and the exhibit stay unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(190)]
    public sealed class VisitorViewHeight : MonoBehaviour
    {
        // Prototype adjustment limits, not measured visitor heights or an accessibility standard.
        public const float StepMetres = .05f;
        public const float MinimumOffsetMetres = -.5f;
        public const float MaximumOffsetMetres = .5f;

        [SerializeField] XROrigin origin;
        Transform trackingParent;
        Transform preferenceOffset;
        readonly Dictionary<Transform, int> originalSiblingIndices = new Dictionary<Transform, int>();
        float offsetMetres;

        public float OffsetMetres => offsetMetres;
        public bool IsReady => isActiveAndEnabled && origin && origin.Camera && trackingParent &&
            preferenceOffset && preferenceOffset.parent == trackingParent;
        public bool CanRaise => IsReady && offsetMetres < MaximumOffsetMetres - .0001f;
        public bool CanLower => IsReady && offsetMetres > MinimumOffsetMetres + .0001f;

        public void Configure(XROrigin xrOrigin)
        {
            if (!xrOrigin) return;
            if (origin != xrOrigin)
            {
                RemoveOffset();
                origin = xrOrigin;
            }
            if (isActiveAndEnabled) EnsureOffset();
        }

        public bool Raise() => Adjust(StepMetres);
        public bool Lower() => Adjust(-StepMetres);

        /// <summary>Applies a bounded world-up offset without writing a tracked local pose.</summary>
        public bool Adjust(float metres)
        {
            if (float.IsNaN(metres) || float.IsInfinity(metres) || !EnsureOffset()) return false;
            // Round accumulated button steps so reset and limits have deterministic values.
            var next = Mathf.Clamp(Mathf.Round((offsetMetres + metres) * 10000f) / 10000f,
                MinimumOffsetMetres, MaximumOffsetMetres);
            if (Mathf.Abs(next - offsetMetres) < .00001f) return false;
            offsetMetres = next;
            ApplyOffset();
            Physics.SyncTransforms();
            return true;
        }

        /// <summary>Returns to the current tracked height, not a hard-coded adult eye height.</summary>
        public bool ResetHeight()
        {
            if (!EnsureOffset()) return false;
            var changed = Mathf.Abs(offsetMetres) > .00001f;
            offsetMetres = 0;
            ApplyOffset();
            Physics.SyncTransforms();
            return changed;
        }

        void OnEnable() => EnsureOffset();

        void LateUpdate()
        {
            // XROrigin may become ready after this component. Its tracking callbacks own the
            // floor-offset object's Y; the dedicated child remains independent of those writes.
            if (EnsureOffset()) ApplyOffset();
        }

        void OnDisable() => RemoveOffset();
        void OnDestroy() => RemoveOffset();

        bool EnsureOffset()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !origin || !origin.Camera ||
                !origin.CameraFloorOffsetObject) return false;
            var parent = origin.CameraFloorOffsetObject.transform;
            if (preferenceOffset && trackingParent == parent && preferenceOffset.parent == parent)
                return true;
            RemoveOffset();
            // Only wrap the configured tracking subtree. Never wrap a separate camera or
            // substitute the XROrigin root, which can also own locomotion/world-space content.
            if (parent == origin.transform || (origin.Origin && parent == origin.Origin.transform) ||
                parent == origin.Camera.transform || !origin.Camera.transform.IsChildOf(parent))
                return false;
            foreach (var other in FindObjectsOfType<VisitorViewHeight>())
                if (other != this && other.origin == origin && other.IsReady) return false;

            var children = new List<Transform>();
            for (var index = 0; index < parent.childCount; index++) children.Add(parent.GetChild(index));
            if (children.Count == 0) return false;
            trackingParent = parent;
            preferenceOffset = new GameObject("Visitor view height offset").transform;
            preferenceOffset.SetParent(parent, false);
            preferenceOffset.localPosition = Vector3.zero;
            preferenceOffset.localRotation = Quaternion.identity;
            preferenceOffset.localScale = Vector3.one;
            originalSiblingIndices.Clear();
            for (var index = 0; index < children.Count; index++)
                originalSiblingIndices[children[index]] = index;
            foreach (var child in children)
            {
                // The wrapper is identity, so keeping local poses also keeps world poses.
                // TrackedPoseDriver can continue writing exactly the same local pose values.
                child.SetParent(preferenceOffset, false);
            }
            ApplyOffset();
            return true;
        }

        void ApplyOffset()
        {
            if (!preferenceOffset || !trackingParent) return;
            // Metres always mean world-up metres, including after locomotion/recentering or
            // a scaled rig. The floor offset's own local Y remains owned by XROrigin.
            preferenceOffset.localPosition = trackingParent.InverseTransformVector(Vector3.up * offsetMetres);
        }

        void RemoveOffset()
        {
            if (!preferenceOffset)
            {
                trackingParent = null;
                originalSiblingIndices.Clear();
                return;
            }
            var wrapper = preferenceOffset;
            // Subtract only our contribution before unwrapping. Current tracked poses and
            // any keyboard/locomotion changes made meanwhile are deliberately retained.
            wrapper.localPosition = Vector3.zero;
            var parent = trackingParent ? trackingParent : wrapper.parent;
            var children = new List<Transform>();
            for (var index = 0; index < wrapper.childCount; index++) children.Add(wrapper.GetChild(index));
            foreach (var child in children)
            {
                child.SetParent(parent, true);
                if (parent && originalSiblingIndices.TryGetValue(child, out var sibling))
                    child.SetSiblingIndex(Mathf.Min(sibling, parent.childCount - 1));
            }
            preferenceOffset = null;
            trackingParent = null;
            originalSiblingIndices.Clear();
            if (Application.isPlaying) Destroy(wrapper.gameObject);
            else DestroyImmediate(wrapper.gameObject);
        }
    }
}
