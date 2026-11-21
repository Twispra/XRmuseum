using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace Museum.Navigation
{
    /// <summary>Confirm on release while still pointing; a combined grip/trigger press is one intent.</summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class GalleryNavigationButton : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        GalleryNavigator navigator;
        XRSimpleInteractable target;
        IXRInteractor hand, releaseCandidate;
        bool selecting, activating, cancelled;
        public GalleryNavigator.Exhibit Destination { get; private set; }
        public int Confirmations { get; private set; }
        public XRSimpleInteractable Target => target;
        bool IXRSelectFilter.canProcess => true;
        bool IXRHoverFilter.canProcess => true;
        bool IXRHoverFilter.Process(IXRHoverInteractor interactor, IXRHoverInteractable _) => interactor is XRRayInteractor;
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable _) =>
            interactor is XRRayInteractor && navigator && navigator.IsReady && !navigator.IsTransitioning && !cancelled;

        public void Configure(GalleryNavigator owner, GalleryNavigator.Exhibit destination)
        { navigator = owner; Destination = destination; }

        void OnEnable()
        {
            target = GetComponent<XRSimpleInteractable>();
            target.selectEntered.AddListener(OnSelected);
            target.selectExited.AddListener(OnReleased);
            target.activated.AddListener(OnActivated);
            target.deactivated.AddListener(OnDeactivated);
            target.hoverExited.AddListener(OnHoverExited);
            target.selectFilters.Add(this);
            target.hoverFilters.Add(this);
        }
        void OnDisable()
        {
            target.selectEntered.RemoveListener(OnSelected);
            target.selectExited.RemoveListener(OnReleased);
            target.activated.RemoveListener(OnActivated);
            target.deactivated.RemoveListener(OnDeactivated);
            target.hoverExited.RemoveListener(OnHoverExited);
            target.selectFilters.Remove(this);
            target.hoverFilters.Remove(this);
            ResetPress();
            releaseCandidate = null;
        }

        bool Begin(IXRInteractor interactor)
        {
            if (!navigator || !navigator.IsReady || navigator.IsTransitioning || cancelled) return false;
            if (hand != null && !SameHand(hand, interactor)) return false;
            if (SameHand(releaseCandidate, interactor)) releaseCandidate = null;
            hand = interactor;
            return true;
        }
        void OnSelected(SelectEnterEventArgs args)
        { if (Begin(args.interactorObject)) selecting = true; }
        void OnActivated(ActivateEventArgs args)
        { if (Begin(args.interactorObject)) activating = true; }
        void OnReleased(SelectExitEventArgs args)
        {
            if (!SameHand(hand, args.interactorObject)) return;
            selecting = false;
            cancelled |= args.isCanceled;
            EndIfReleased();
        }
        void OnDeactivated(DeactivateEventArgs args)
        {
            if (!SameHand(hand, args.interactorObject)) return;
            activating = false;
            EndIfReleased();
        }
        void OnHoverExited(HoverExitEventArgs args)
        {
            if (SameHand(releaseCandidate, args.interactorObject)) releaseCandidate = null;
            if (SameHand(hand, args.interactorObject) && (selecting || activating)) cancelled = true;
        }
        void EndIfReleased()
        {
            if (selecting || activating) return;
            // XRI can send selectExited before updating hover for the same device state.
            // Keep the releasing interactor until Update can verify the final hover state.
            if (!cancelled) releaseCandidate = hand;
            ResetPress();
        }
        bool StillPointing(IXRInteractor interactor) =>
            interactor != null && target.interactorsHovering.Any(i => SameHand(i, interactor));
        void ResetPress() { hand = null; selecting = activating = cancelled = false; }

        void Update()
        {
            if (hand != null && (selecting || activating))
            {
                if (!StillPointing(hand)) cancelled = true;
                if (hand is XRBaseControllerInteractor controller && controller.xrController &&
                    !controller.xrController.selectInteractionState.active && !controller.xrController.activateInteractionState.active)
                    ResetPress(); // Input disappeared without an exit event: cancel, never confirm.
            }
            var releasedHand = releaseCandidate;
            releaseCandidate = null;
            if (!StillPointing(releasedHand)) return;
            if (releasedHand is XRBaseControllerInteractor source && source.xrController &&
                (source.xrController.selectInteractionState.active || source.xrController.activateInteractionState.active)) return;
            if (navigator && navigator.Request(Destination)) Confirmations++;
        }

        static bool SameHand(IXRInteractor first, IXRInteractor second)
        {
            if (first == null || second == null) return false;
            if (ReferenceEquals(first, second)) return true;
            return first is XRBaseControllerInteractor a && second is XRBaseControllerInteractor b &&
                a.xrController && a.xrController == b.xrController;
        }
    }
}
