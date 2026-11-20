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
        IXRInteractor hand;
        bool selecting, activating, cancelled, pending;
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
            ResetPress(); pending = false;
        }

        bool Begin(IXRInteractor interactor)
        {
            if (!navigator || !navigator.IsReady || navigator.IsTransitioning || cancelled) return false;
            if (hand != null && !SameHand(hand, interactor)) return false;
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
        { if (hand != null && SameHand(hand, args.interactorObject) && (selecting || activating)) cancelled = true; }
        void EndIfReleased()
        {
            if (selecting || activating) return;
            pending = !cancelled && StillPointing();
            ResetPress();
        }
        bool StillPointing() => hand != null && target.interactorsHovering.Any(i => SameHand(i, hand));
        void ResetPress() { hand = null; selecting = activating = cancelled = false; }

        void Update()
        {
            if (hand != null && (selecting || activating))
            {
                if (!StillPointing()) cancelled = true;
                if (hand is XRBaseControllerInteractor controller && controller.xrController &&
                    !controller.xrController.selectInteractionState.active && !controller.xrController.activateInteractionState.active)
                    ResetPress(); // Input disappeared without an exit event: cancel, never confirm.
            }
            if (!pending) return;
            pending = false;
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
