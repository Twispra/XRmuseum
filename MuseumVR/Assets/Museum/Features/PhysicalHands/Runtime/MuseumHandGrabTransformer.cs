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
        Pose objectStart, firstStart, secondStart;
        Vector3 scale;
        int count;
        Quaternion lastTwoHandDelta = Quaternion.identity;
        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;
        public override void OnGrab(XRGrabInteractable grab) => Capture(grab);
        public override void OnGrabCountChanged(XRGrabInteractable grab, Pose targetPose, Vector3 localScale) => Capture(grab);
        void Capture(XRGrabInteractable grab)
        {
            count = grab.interactorsSelecting.Count;
            lastTwoHandDelta = Quaternion.identity;
            objectStart = new Pose(grab.transform.position, grab.transform.rotation); scale = grab.transform.localScale;
            if (count > 0) firstStart = Hand(grab, 0);
            if (count > 1) secondStart = Hand(grab, 1);
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
            // A bounded target keeps collision pursuit from building an unbounded velocity spike.
            targetPose.position = grab.transform.position + Vector3.ClampMagnitude(desired - grab.transform.position, maximumPositionError);
            targetPose.rotation = Quaternion.RotateTowards(grab.transform.rotation, delta * objectStart.rotation, maximumRotationError);
            localScale = scale;
        }
    }
}
