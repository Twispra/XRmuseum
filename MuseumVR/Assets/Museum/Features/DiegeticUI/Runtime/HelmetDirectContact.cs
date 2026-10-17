using Museum.PhysicalHands;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Diegetic
{
    /// <summary>Use the registered glove's surface reach, rather than its broad candidate sphere.</summary>
    internal static class HelmetDirectContact
    {
        internal static bool Touches(IXRInteractor interactor, XRBaseInteractable target, float legacyDistance)
        {
            if (interactor == null || !target) return false;
            if (interactor is XRDirectInteractor direct)
            {
                // The hand already registers on its own Direct interactor. This also follows
                // rig enable/disable without scene searches or a stale controller-to-hand cache.
                for (int i = 0; i < direct.selectFilters.count; i++)
                    if (direct.selectFilters.GetAt(i) is MuseumPhysicalHand hand && hand && hand.DirectInteractor == direct)
                        return hand.isActiveAndEnabled && hand.IsDirectContact(target);
            }

            // Preserve the authored small contact radius for Direct interactors without a glove.
            float squared = legacyDistance * legacyDistance;
            foreach (var collider in target.colliders)
                if (collider && collider.enabled && collider.gameObject.activeInHierarchy &&
                    (collider.ClosestPoint(interactor.transform.position) - interactor.transform.position).sqrMagnitude <= squared)
                    return true;
            return false;
        }
    }
}
