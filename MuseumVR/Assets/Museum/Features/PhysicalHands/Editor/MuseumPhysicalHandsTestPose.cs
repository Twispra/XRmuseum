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

        public static IEnumerator Approach(XRBaseController controller, XRGrabInteractable exhibit,
            System.Action<Vector3, Quaternion, bool> queue, string phase)
        {
            var hand = Hand(controller);
            if (!hand) yield break;
            Vector3 desiredControllerPosition = controller.transform.position;
            Quaternion desiredControllerRotation = controller.transform.rotation;
            Vector3 target = TrackingPosition(controller, desiredControllerPosition, desiredControllerRotation);
            Quaternion localRotation = Quaternion.Inverse(controller.transform.rotation) * hand.TrackingTarget.rotation;
            Quaternion targetRotation = desiredControllerRotation * localRotation;
            Quaternion currentRotation = hand.Body.rotation;
            Vector3 from = hand.Body.position;
            var solids = exhibit ? exhibit.colliders.Where(c => c && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy).ToArray() : new Collider[0];
            if (solids.Length == 0 || (Quaternion.Angle(currentRotation, targetRotation) < .25f &&
                !CrossesExhibit(hand, from, target, currentRotation, solids)))
            {
                queue(desiredControllerPosition, desiredControllerRotation, false);
                yield return WaitForPalm(controller, phase);
                yield break;
            }

            var bounds = solids[0].bounds;
            foreach (var solid in solids.Skip(1)) bounds.Encapsulate(solid.bounds);
            var box = hand.CollisionCollider as BoxCollider;
            Vector3 scale = box ? box.transform.lossyScale : Vector3.one;
            float radius = Vector3.Scale(hand.OpenEnvelopeSize * .5f, scale).magnitude +
                           Vector3.Scale(hand.OpenEnvelopeCenter, scale).magnitude + .03f;
            bounds.Expand(radius * 2f);
            Vector3 withdraw = OutsideBounds(bounds, from, SurfaceOutward(solids, from, bounds.center));
            Vector3 entry = OutsideBounds(bounds, target, SurfaceOutward(solids, target, bounds.center));
            float height = Mathf.Max(bounds.max.y + .08f, withdraw.y, entry.y);
            Vector3 aboveWithdraw = new Vector3(withdraw.x, height, withdraw.z);
            Vector3 aboveEntry = new Vector3(entry.x, height, entry.z);
            // First clear the entire open glove without changing its current rotation.
            // Turn only above the expanded exhibit, then descend on its target side.
            yield return Waypoint(controller, withdraw, currentRotation, queue, phase + " / withdraw");
            yield return Waypoint(controller, aboveWithdraw, currentRotation, queue, phase + " / lift clear");
            yield return Waypoint(controller, aboveWithdraw, targetRotation, queue, phase + " / turn clear");
            yield return Waypoint(controller, aboveEntry, targetRotation, queue, phase + " / travel outside");
            yield return Waypoint(controller, entry, targetRotation, queue, phase + " / descend outside");
            queue(desiredControllerPosition, desiredControllerRotation, false);
            yield return WaitForPalm(controller, phase + " / final approach");
        }

        static bool CrossesExhibit(MuseumPhysicalHand hand, Vector3 from, Vector3 target, Quaternion rotation, Collider[] solids)
        {
            Vector3 delta = target - from;
            if (delta.sqrMagnitude < .000001f) return false;
            Vector3 scale = hand.CollisionCollider.transform.lossyScale;
            Vector3 center = rotation * Vector3.Scale(hand.OpenEnvelopeCenter, scale);
            Vector3 half = Vector3.Scale(hand.OpenEnvelopeSize * .5f, scale);
            var hits = Physics.BoxCastAll(from + center, half, delta.normalized, rotation, delta.magnitude + .002f, ~0, QueryTriggerInteraction.Ignore);
            return hits.Any(hit => solids.Contains(hit.collider));
        }

        static Vector3 SurfaceOutward(Collider[] solids, Vector3 position, Vector3 fallbackCenter)
        {
            Vector3 direction = position - fallbackCenter;
            float nearest = float.MaxValue;
            foreach (var solid in solids)
            {
                Vector3 delta = position - solid.ClosestPoint(position);
                if (delta.sqrMagnitude > .000001f && delta.sqrMagnitude < nearest)
                { nearest = delta.sqrMagnitude; direction = delta; }
            }
            return direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.back;
        }

        static Vector3 OutsideBounds(Bounds bounds, Vector3 position, Vector3 direction)
        {
            if (!bounds.Contains(position)) return position + direction * .08f;
            float exit = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                float component = direction[axis];
                if (Mathf.Abs(component) < .00001f) continue;
                float distance = ((component > 0 ? bounds.max[axis] : bounds.min[axis]) - position[axis]) / component;
                if (distance >= 0) exit = Mathf.Min(exit, distance);
            }
            return position + direction * (exit + .08f);
        }

        static IEnumerator Waypoint(XRBaseController controller, Vector3 position, Quaternion palmRotation,
            System.Action<Vector3, Quaternion, bool> queue, string phase)
        {
            var hand = Hand(controller);
            Quaternion localRotation = Quaternion.Inverse(controller.transform.rotation) * hand.TrackingTarget.rotation;
            Quaternion rawRotation = palmRotation * Quaternion.Inverse(localRotation);
            queue(ControllerPosition(controller, position, rawRotation), rawRotation, false);
            yield return WaitForPalm(controller, phase);
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (Quaternion.Angle(hand.Body.rotation, palmRotation) >= .25f)
            {
                if (Time.realtimeSinceStartup >= deadline)
                    throw new System.InvalidOperationException("Physical palm could not turn in the clear route / " + phase +
                        "; rotation error=" + Quaternion.Angle(hand.Body.rotation, palmRotation) + "; " + SelectionDiagnostics(controller, null));
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

        public static Vector3 ControlTouch(Collider target, XRBaseController controller, Quaternion rotation,
            Vector3 outward, Vector3 legacyPosition, XRGrabInteractable exhibit)
        {
            if (!Hand(controller) || !exhibit || !(target is BoxCollider box)) return OutsideTouch(target, controller, rotation, outward, legacyPosition);
            float side = Mathf.Sign(Vector3.Dot(target.bounds.center - exhibit.transform.position, box.transform.right));
            Vector3 aim = box.transform.TransformPoint(box.center + Vector3.right * box.size.x * .45f * side);
            // Use the control's own face away from the exhibit, where the visitor
            // can distinguish its button from the neighbouring discovery marker.
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
