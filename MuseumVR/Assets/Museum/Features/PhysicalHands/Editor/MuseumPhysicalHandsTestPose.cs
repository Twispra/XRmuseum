#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

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
            Vector3 outward, Vector3 legacyPosition, Vector3? surfaceAim = null)
        {
            var hand = Hand(controller);
            if (!hand || !target || outward.sqrMagnitude < .000001f) return legacyPosition;
            outward.Normalize();
            Quaternion targetRotation = Quaternion.Inverse(controller.transform.rotation) * hand.TrackingTarget.rotation;
            Quaternion palmRotation = controllerRotation * targetRotation;
            Vector3 point = target.ClosestPoint((surfaceAim ?? target.bounds.center) + outward * (target.bounds.extents.magnitude + 1f));
            float support = EnvelopeSupport(hand, palmRotation, -outward);
            Vector3 palmPosition = point + outward * (support + .004f);
            // QueueStateEvent positions the tracked controller, not the independent palm target.
            return ControllerPosition(controller, palmPosition, controllerRotation);
        }

        public static Vector3 WindowTouch(Collider target, XRBaseController controller, Quaternion rotation,
            Vector3 outward, Vector3 legacyPosition)
        {
            if (!Hand(controller)) return legacyPosition;
            var box = target as BoxCollider;
            // The saved front discovery marker sits left of the window. Touch the
            // right inner edge of the same face, rather than asking its shared
            // centre to stand for two different visitor intentions.
            Vector3 aim = box ? box.transform.TransformPoint(box.center + Vector3.right * box.size.x * .45f) : target.bounds.center;
            return OutsideTouch(target, controller, rotation, outward, legacyPosition, aim);
        }

        public static string SelectionDiagnostics(XRBaseController controller, IXRSelectInteractable target)
        {
            var hand = Hand(controller);
            if (!hand || !hand.DirectInteractor) return "physical-hand=legacy";
            var direct = hand.DirectInteractor;
            var candidates = new List<IXRInteractable>();
            direct.GetValidTargets(candidates);
            var state = controller.currentControllerState;
            bool handFilter = target != null && ((IXRSelectFilter)hand).Process(direct, target);
            bool possible = target != null && direct.interactionManager && direct.interactionManager.IsSelectPossible(direct, target);
            return "target=" + hand.TrackingTarget.position + "; proxy=" + hand.Body.position +
                   "; query=" + direct.transform.position + "; error=" + hand.TrackingError +
                   "; tracked=" + hand.IsTrackingValid + "; blocked=" + hand.IsBlocked +
                   "; grip=" + (state == null ? "missing" : state.selectInteractionState.value.ToString("F3")) +
                   "; selectActive=" + direct.isSelectActive + "; handFilter=" + handFilter + "; selectPossible=" + possible +
                   "; group=" + direct.containingGroup?.activeInteractor?.transform.name +
                   "; candidates=" + string.Join(",", candidates.Select(t => t.transform.name)) +
                   "; selected=" + string.Join(",", direct.interactablesSelected.Select(t => t.transform.name));
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
            // Every near fixture approaches with Grip up. The prior interaction
            // may still leave the visible/physical glove closed when this pose
            // is calculated; always reserve the production open envelope.
            Vector3 center = Vector3.Scale(hand.OpenEnvelopeCenter, scale);
            Vector3 half = Vector3.Scale(hand.OpenEnvelopeSize * .5f, scale);
            return Vector3.Dot(rotation * center, direction) +
                   Mathf.Abs(Vector3.Dot(rotation * (Vector3.right * half.x), direction)) +
                   Mathf.Abs(Vector3.Dot(rotation * (Vector3.up * half.y), direction)) +
                   Mathf.Abs(Vector3.Dot(rotation * (Vector3.forward * half.z), direction));
        }
    }
}
#endif
