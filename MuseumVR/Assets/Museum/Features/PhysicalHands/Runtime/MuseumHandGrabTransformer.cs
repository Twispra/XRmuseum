using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Museum.PhysicalHands
{
    /// <summary>Rebases on every hand transition; never changes exhibit scale or drives the Rigidbody itself.</summary>
    [DisallowMultipleComponent]
    public sealed class MuseumHandGrabTransformer : XRBaseGrabTransformer
    {
        [SerializeField, Min(.01f)] float maximumPositionError = .10f;
        [SerializeField, Range(1f, 90f)] float maximumRotationError = 20f;
        [SerializeField, Range(0f, .16f)] float maximumSurfacePull = .12f;
        [SerializeField, Range(.08f, .4f)] float surfacePullDuration = .18f;
        Pose objectStart, firstStart, secondStart;
        Vector3 scale;
        Vector3 surfacePullLocal;
        float surfacePullStarted;
        IXRSelectInteractor primary;
        int count;
        Quaternion lastTwoHandDelta = Quaternion.identity;
        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;
        public static readonly Vector3 PalmContactLocal = new Vector3(0f, -.019f, .018f);
        public Vector3 SurfacePullLocal => surfacePullLocal;
        public override void OnGrab(XRGrabInteractable grab) => Capture(grab, true);
        public override void OnGrabCountChanged(XRGrabInteractable grab, Pose targetPose, Vector3 localScale)
        {
            if (count != grab.interactorsSelecting.Count || (count > 0 && primary != grab.interactorsSelecting[0])) Capture(grab, false);
        }
        void Capture(XRGrabInteractable grab, bool initial = false)
        {
            count = grab.interactorsSelecting.Count;
            primary = count > 0 ? grab.interactorsSelecting[0] : null;
            surfacePullLocal = Vector3.zero;
            lastTwoHandDelta = Quaternion.identity;
            objectStart = new Pose(grab.transform.position, grab.transform.rotation); scale = grab.transform.localScale;
            if (count > 0) firstStart = Hand(grab, 0);
            if (count > 1) secondStart = Hand(grab, 1);
            // Only the first near Direct grip receives a small positional settle.
            // Handoffs rebase at the actual object pose; Rays keep their original behavior.
            var surface = grab.GetComponent<MuseumGripSurface>();
            if (initial && count == 1 && surface && PhysicalHand(primary) &&
                surface.TryGetContact(grab, firstStart.position, out var contact, out _))
            {
                Vector3 palm = firstStart.position + firstStart.rotation * PalmContactLocal;
                surfacePullLocal = Quaternion.Inverse(firstStart.rotation) * Vector3.ClampMagnitude(palm - contact, maximumSurfacePull);
                surfacePullStarted = Time.unscaledTime;
            }
        }
        public static MuseumPhysicalHand PhysicalHand(IXRSelectInteractor selector)
        {
            if (!(selector is XRDirectInteractor direct)) return null;
            for (int i = 0; i < direct.selectFilters.count; i++)
                if (direct.selectFilters.GetAt(i) is MuseumPhysicalHand hand && hand.isActiveAndEnabled) return hand;
            return null;
        }
        static Pose Hand(XRGrabInteractable grab, int index)
        {
            Transform attach = grab.interactorsSelecting[index].GetAttachTransform(grab);
            return new Pose(attach.position, attach.rotation);
        }
        public override void Process(XRGrabInteractable grab, XRInteractionUpdateOrder.UpdatePhase phase, ref Pose targetPose, ref Vector3 localScale)
        {
            if (!grab.isSelected) return;
            if (count != grab.interactorsSelecting.Count) Capture(grab);
            Pose first = Hand(grab, 0);
            Quaternion delta = first.rotation * Quaternion.Inverse(firstStart.rotation);
            Vector3 startAnchor = firstStart.position, anchor = first.position;
            if (count > 1)
            {
                Pose second = Hand(grab, 1);
                Vector3 oldSpan = secondStart.position - firstStart.position, span = second.position - first.position;
                if (oldSpan.sqrMagnitude > .0004f && span.sqrMagnitude > .0004f)
                {
                    Quaternion swing = Quaternion.FromToRotation(oldSpan, span);
                    Quaternion wrist = first.rotation * Quaternion.Inverse(swing * firstStart.rotation);
                    Vector3 axis = span.normalized;
                    float projection = Vector3.Dot(new Vector3(wrist.x, wrist.y, wrist.z), axis);
                    Quaternion twist = new Quaternion(axis.x * projection, axis.y * projection, axis.z * projection, wrist.w);
                    delta = (Quaternion.Dot(twist, twist) > .000001f ? twist.normalized : Quaternion.identity) * swing;
                    lastTwoHandDelta = delta;
                }
                else delta = lastTwoHandDelta;
                startAnchor = (firstStart.position + secondStart.position) * .5f;
                anchor = (first.position + second.position) * .5f;
            }
            Vector3 desired = anchor + delta * (objectStart.position - startAnchor);
            if (count == 1)
            {
                float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - surfacePullStarted) / Mathf.Max(.08f, surfacePullDuration)));
                desired += first.rotation * (surfacePullLocal * settle);
            }
            // A bounded target keeps collision pursuit from building an unbounded velocity spike.
            targetPose.position = grab.transform.position + Vector3.ClampMagnitude(desired - grab.transform.position, maximumPositionError);
            targetPose.rotation = Quaternion.RotateTowards(grab.transform.rotation, delta * objectStart.rotation, maximumRotationError);
            localScale = scale;
        }
    }
}
