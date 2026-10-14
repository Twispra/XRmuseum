#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhysicalHands.EditorTools
{
    /// <summary>Input-only QA poses: approach a real surface with the saved glove envelope.</summary>
    public static class MuseumPhysicalHandsTestPose
    {
        public static XRBaseController Controller(bool left)
        {
            string name = left ? "Left Controller" : "Right Controller";
            foreach (var controller in Object.FindObjectsOfType<ActionBasedController>())
                if (controller.name == name) return controller;
            return null;
        }

        public static MuseumPhysicalHand Hand(XRBaseController controller)
        {
            if (!controller) return null;
            foreach (var hand in Object.FindObjectsOfType<MuseumPhysicalHand>())
                if (hand.isActiveAndEnabled && hand.InputController == controller && hand.TrackingTarget) return hand;
            return null;
        }

        public static IEnumerator WaitForPalm(XRBaseController controller, string phase)
        {
            var hand = Hand(controller);
            if (!hand) yield break;
            // Let the caller's queued raw controller state reach the production
            // actions before measuring the collision-constrained palm target.
            yield return null;
            yield return null;
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (!hand.IsTrackingValid || hand.TrackingError >= .008f)
            {
                if (Time.realtimeSinceStartup >= deadline)
                    throw new System.InvalidOperationException("Physical palm did not arrive before near grip / " + phase +
                        "; controller=" + controller.name + "; tracked=" + hand.IsTrackingValid +
                        "; error=" + hand.TrackingError.ToString("F4") + "; blocked=" + hand.IsBlocked +
                        "; target=" + hand.TrackingTarget.position + "; proxy=" + hand.Body.position);
                yield return null;
            }
        }

        public static Vector3 OutsideTouch(Collider target, XRBaseController controller, Quaternion controllerRotation,
            Vector3 outward, Vector3 legacyPosition)
        {
            var hand = Hand(controller);
            if (!hand || !target || outward.sqrMagnitude < .000001f) return legacyPosition;
            outward.Normalize();
            Quaternion targetRotation = Quaternion.Inverse(controller.transform.rotation) * hand.TrackingTarget.rotation;
            Quaternion palmRotation = controllerRotation * targetRotation;
            Vector3 point = target.ClosestPoint(target.bounds.center + outward * (target.bounds.extents.magnitude + 1f));
            float support = EnvelopeSupport(hand, palmRotation, -outward);
            Vector3 palmPosition = point + outward * (support + .004f);
            // QueueStateEvent positions the tracked controller, not the independent palm target.
            return ControllerPosition(controller, palmPosition, controllerRotation);
        }

        public static Vector3 TrackingPosition(XRBaseController controller, Vector3 controllerPosition, Quaternion controllerRotation)
        {
            return controllerPosition + TrackingOffset(controller, controllerRotation);
        }

        public static Vector3 ControllerPosition(XRBaseController controller, Vector3 palmPosition, Quaternion controllerRotation)
        {
            return palmPosition - TrackingOffset(controller, controllerRotation);
        }

        static Vector3 TrackingOffset(XRBaseController controller, Quaternion rotation)
        {
            var hand = Hand(controller);
            if (!hand) return Vector3.zero;
            Vector3 offset = controller.transform.InverseTransformPoint(hand.TrackingTarget.position);
            return rotation * Vector3.Scale(offset, controller.transform.lossyScale);
        }

        static float EnvelopeSupport(MuseumPhysicalHand hand, Quaternion rotation, Vector3 direction)
        {
            var box = hand.CollisionCollider as BoxCollider;
            if (!box) return hand.CollisionRadius;
            Vector3 scale = box.transform.lossyScale;
            Vector3 center = Vector3.Scale(box.center, scale);
            Vector3 half = Vector3.Scale(box.size * .5f, scale);
            return Vector3.Dot(rotation * center, direction) +
                   Mathf.Abs(Vector3.Dot(rotation * (Vector3.right * half.x), direction)) +
                   Mathf.Abs(Vector3.Dot(rotation * (Vector3.up * half.y), direction)) +
                   Mathf.Abs(Vector3.Dot(rotation * (Vector3.forward * half.z), direction));
        }
    }
}
#endif
