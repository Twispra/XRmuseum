using TMPro;
using UnityEngine;

namespace Museum.Diegetic
{
    /// <summary>
    /// Visual-only rotation caption and leader. Its label anchor is latched for one
    /// continuous interaction; neither the hint nor its size owns an input collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelmetRotationHint : MonoBehaviour
    {
        TMP_Text caption;
        LineRenderer leader;
        MaterialPropertyBlock block;
        Vector3 captionScale;
        Vector3 anchorPosition;
        Vector3 originalFacing;
        Bounds targetBounds;
        int preferredSide;
        bool anchored, fallbackSuppressed;

        public TMP_Text Caption => caption;
        public Transform TargetRoot { get; private set; }
        public LineRenderer Leader => leader;
        public bool IsNear { get; private set; }
        public bool IsAnchored => anchored;
        public Vector3 AnchorPosition => anchorPosition;
        public Bounds TargetBounds => targetBounds;
        public float GripScale { get; private set; } = 1f;
        public float AngularWidthDegrees { get; private set; }
        public bool CaptionSuppressed { get; private set; }
        public bool UsesViewportFallback { get; private set; }
        public bool InitialPlacementWithinSafeView { get; private set; }
        public const float SafeViewportInset = .05f;

        public void Configure(TMP_Text label, LineRenderer line, int side, Transform targetRoot)
        {
            caption = label; leader = line; preferredSide = side < 0 ? -1 : 1;
            TargetRoot = targetRoot;
            block = new MaterialPropertyBlock();
            if (caption)
            {
                captionScale = caption.transform.localScale;
                caption.transform.SetParent(transform, false);
                caption.transform.localPosition = Vector3.zero;
                caption.transform.localRotation = Quaternion.identity;
            }
            if (leader)
            {
                leader.useWorldSpace = true;
                leader.positionCount = 2;
                leader.enabled = false;
            }
        }

        public void UpdatePresentation(Transform view, Bounds bounds, Vector3 gripPoint,
            bool intent, bool selected, float opacity, float nearDistance, float maximumAngle, Color tint)
        {
            if (!caption || !view || !Finite(gripPoint) || !Finite(bounds.center) || !Finite(bounds.extents))
            { Hide(); return; }
            float gripDistance = Vector3.Distance(view.position, gripPoint);
            nearDistance = Mathf.Clamp(nearDistance, .15f, 1f);
            // A small exit band prevents the near-distance boundary from pulsing the dot.
            IsNear = gripDistance < nearDistance + (IsNear ? .05f : 0f);
            maximumAngle = Mathf.Clamp(maximumAngle, 8f, 30f);
            GetSafeView(view, out float horizontalTangent, out float verticalTangent, out float nearClip);
            // Also fit unusually narrow camera views; 18 degrees remains an upper
            // bound, never a reason to crop text from the actual rendering camera.
            maximumAngle = Mathf.Min(maximumAngle, Mathf.Atan(horizontalTangent) * Mathf.Rad2Deg * 1.5f);
            GripScale = IsNear
                ? Mathf.Clamp(gripDistance * Mathf.Tan(.75f * Mathf.Deg2Rad) / .014f, .12f, 1f)
                : 1f;

            if (intent && !anchored)
            {
                targetBounds = bounds;
                Vector3 right = view.right, up = view.up, forward = view.forward;
                float halfWidth = ProjectedExtent(bounds.extents, right);
                float halfHeight = ProjectedExtent(bounds.extents, up);
                float height = Mathf.Clamp(Vector3.Dot(gripPoint - bounds.center, up), -halfHeight * .65f, halfHeight * .65f);
                float depth = Vector3.Dot(gripPoint - bounds.center, forward) - .025f;
                Vector3 edge = bounds.center + right * preferredSide * (halfWidth + .025f)
                    + up * height + forward * depth;
                float anticipatedWidth = .40f;
                // Fit the whole caption beyond the edge using its eventual angular
                // size, rather than letting half the text cover the selected detail.
                for (int iteration = 0; iteration < 6; iteration++)
                {
                    anchorPosition = edge + right * preferredSide * anticipatedWidth * .5f;
                    anticipatedWidth = Mathf.Min(.40f, 2f * Vector3.Distance(view.position, anchorPosition)
                        * Mathf.Tan(maximumAngle * .5f * Mathf.Deg2Rad));
                }
                anchorPosition = edge + right * preferredSide * anticipatedWidth * .5f;
                fallbackSuppressed = false;
                UsesViewportFallback = !FitsSafeView(view, anchorPosition, maximumAngle, horizontalTangent, verticalTangent, nearClip);
                if (UsesViewportFallback)
                {
                    // The helmet can fill the view at close range. In that case a
                    // readable corner hint is preferable to insisting on an offscreen
                    // silhouette edge. Choose once, then retain this world-space pose.
                    float targetDepth = Vector3.Dot(gripPoint - view.position, view.forward);
                    float depthInView = Mathf.Max(nearClip * 1.1f, Mathf.Min(.40f, Mathf.Max(.055f, targetDepth * .78f)));
                    var candidates = new[]
                    {
                        new Vector2(preferredSide * .65f, .68f), new Vector2(0, .78f),
                        new Vector2(-preferredSide * .65f, .68f), new Vector2(preferredSide * .70f, 0),
                        new Vector2(-preferredSide * .70f, 0), new Vector2(preferredSide * .65f, -.68f),
                        new Vector2(0, -.78f), new Vector2(-preferredSide * .65f, -.68f),
                        new Vector2(0, .45f), new Vector2(0, -.45f)
                    };
                    bool found = false;
                    foreach (var candidate in candidates)
                    {
                        Vector3 point = view.position + view.forward * depthInView
                            + view.right * (candidate.x * horizontalTangent * depthInView)
                            + view.up * (candidate.y * verticalTangent * depthInView);
                        if (!FitsSafeView(view, point, maximumAngle, horizontalTangent, verticalTangent, nearClip, targetDepth)) continue;
                        CaptionGeometry(view, point, maximumAngle, out var rotation, out float candidateWidth, out float textHeight);
                        if (CoversFocusAt(view.position, gripPoint, point, rotation, candidateWidth, textHeight) ||
                            CoversFocusAt(view.position, bounds.center, point, rotation, candidateWidth, textHeight)) continue;
                        anchorPosition = point; found = true; break;
                    }
                    // A narrow projection still gets a bounded, central-top anchor;
                    // the ordinary focus-protection rule can suppress it if necessary.
                    if (!found)
                    {
                        anchorPosition = view.position + view.forward * depthInView
                            + view.up * (verticalTangent * depthInView * .65f);
                        fallbackSuppressed = true;
                    }
                }
                InitialPlacementWithinSafeView = FitsSafeView(view, anchorPosition, maximumAngle,
                    horizontalTangent, verticalTangent, nearClip);
                originalFacing = (anchorPosition - view.position).normalized;
                anchored = true;
            }
            if (!intent && !selected && opacity <= .005f) anchored = false;
            if (!anchored)
            {
                if (leader) leader.enabled = false;
                caption.enabled = false;
                return;
            }

            // The world position stays fixed even if the model rotates, its grip point
            // or a parent moves, or the visitor crosses the near-distance threshold.
            transform.position = anchorPosition;
            Vector3 towardLabel = anchorPosition - view.position;
            float distance = towardLabel.magnitude;
            if (distance > .0001f) transform.rotation = Quaternion.LookRotation(towardLabel, Vector3.up);
            caption.transform.localPosition = Vector3.zero;
            caption.transform.localRotation = Quaternion.identity;
            caption.transform.localScale = captionScale;
            float baseWidth = caption.transform.TransformVector(Vector3.right * caption.rectTransform.rect.width).magnitude;
            float allowedWidth = 2f * distance * Mathf.Tan(maximumAngle * .5f * Mathf.Deg2Rad);
            float scale = baseWidth > .00001f ? Mathf.Clamp01(allowedWidth / baseWidth) : 1f;
            caption.transform.localScale = captionScale * Mathf.Max(.0001f, scale);
            float width = caption.transform.TransformVector(Vector3.right * caption.rectTransform.rect.width).magnitude;
            AngularWidthDegrees = distance > .0001f ? 2f * Mathf.Atan(width / (2f * distance)) * Mathf.Rad2Deg : 0f;
            // Keep the latched anchor when a turned part grows into its space or
            // the visitor crosses it. Hide the caption instead of chasing the head,
            // flipping across their view or covering the detail they selected.
            float captionHeight = caption.transform.TransformVector(Vector3.up * caption.rectTransform.rect.height).magnitude;
            CaptionSuppressed = fallbackSuppressed || distance < .04f || Vector3.Dot(towardLabel.normalized, originalFacing) <= .05f ||
                CoversFocusAt(view.position, gripPoint, anchorPosition, transform.rotation, width, captionHeight) ||
                CoversFocusAt(view.position, bounds.center, anchorPosition, transform.rotation, width, captionHeight);
            caption.enabled = !CaptionSuppressed;

            if (leader)
            {
                leader.enabled = IsNear && opacity > .005f && caption.enabled;
                if (leader.enabled)
                {
                    Vector3 end = anchorPosition - transform.right * preferredSide * width * .5f;
                    Vector3 direction = (end - gripPoint).normalized;
                    leader.SetPosition(0, gripPoint + direction * (.014f * GripScale + .003f));
                    leader.SetPosition(1, end);
                    leader.widthMultiplier = Mathf.Min(.0012f, Mathf.Max(.00035f, gripDistance * .002f));
                    leader.GetPropertyBlock(block); tint.a = opacity * .55f;
                    block.SetColor("_BaseColor", tint); leader.SetPropertyBlock(block);
                }
            }
        }

        public void Hide()
        {
            anchored = false; IsNear = false; GripScale = 1f; AngularWidthDegrees = 0; CaptionSuppressed = false;
            UsesViewportFallback = false; InitialPlacementWithinSafeView = false; fallbackSuppressed = false;
            if (leader) leader.enabled = false;
            if (caption) caption.enabled = false;
        }

        void OnDisable() => Hide();

        static float ProjectedExtent(Vector3 extents, Vector3 axis) =>
            Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;
        void GetSafeView(Transform view, out float horizontalTangent, out float verticalTangent, out float nearClip)
        {
            var camera = view.GetComponent<Camera>();
            float verticalFov = camera ? Mathf.Clamp(camera.fieldOfView, 1f, 80f) : 60f;
            float aspect = camera ? Mathf.Clamp(camera.aspect, .1f, 4f) : 4f / 3f;
            float tangent = Mathf.Tan(verticalFov * .5f * Mathf.Deg2Rad);
            horizontalTangent = Mathf.Min(tangent * aspect, 1f) * (1f - 2f * SafeViewportInset);
            verticalTangent = tangent * (1f - 2f * SafeViewportInset);
            nearClip = camera ? Mathf.Max(.001f, camera.nearClipPlane) : .01f;
        }
        void CaptionGeometry(Transform view, Vector3 point, float maximumAngle, out Quaternion rotation, out float width, out float height)
        {
            var direction = point - view.position;
            rotation = direction.sqrMagnitude > .0000001f ? Quaternion.LookRotation(direction, Vector3.up) : view.rotation;
            float baseWidth = caption.rectTransform.rect.width * Mathf.Abs(captionScale.x * transform.lossyScale.x);
            width = Mathf.Min(baseWidth, 2f * direction.magnitude * Mathf.Tan(maximumAngle * .5f * Mathf.Deg2Rad));
            height = width * caption.rectTransform.rect.height / Mathf.Max(.0001f, caption.rectTransform.rect.width);
        }
        bool FitsSafeView(Transform view, Vector3 point, float maximumAngle, float horizontalTangent, float verticalTangent,
            float nearClip, float maximumDepth = float.PositiveInfinity)
        {
            CaptionGeometry(view, point, maximumAngle, out var rotation, out float width, out float height);
            var inverse = Quaternion.Inverse(view.rotation);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 offset = rotation * new Vector3((corner & 1) == 0 ? -width * .5f : width * .5f,
                    (corner & 2) == 0 ? -height * .5f : height * .5f, 0);
                Vector3 local = inverse * (point + offset - view.position);
                if (local.z <= nearClip || local.z >= maximumDepth - .001f || Mathf.Abs(local.x) > horizontalTangent * local.z ||
                    Mathf.Abs(local.y) > verticalTangent * local.z) return false;
            }
            return true;
        }
        static bool CoversFocusAt(Vector3 eye, Vector3 focus, Vector3 anchor, Quaternion rotation, float width, float height)
        {
            Vector3 ray = focus - eye;
            float forward = Vector3.Dot(ray, rotation * Vector3.forward);
            if (forward <= .0001f) return false;
            float planeDistance = (anchor - eye).magnitude;
            Vector3 onCaption = eye + ray * (planeDistance / forward) - anchor;
            return Mathf.Abs(Vector3.Dot(onCaption, rotation * Vector3.right)) < width * .5f + .003f &&
                Mathf.Abs(Vector3.Dot(onCaption, rotation * Vector3.up)) < height * .5f + .003f;
        }
        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
