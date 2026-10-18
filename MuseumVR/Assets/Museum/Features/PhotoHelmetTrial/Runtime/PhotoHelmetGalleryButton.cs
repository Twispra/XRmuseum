using Museum.Diegetic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace Museum.PhotoHelmetTrial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class PhotoHelmetGalleryButton : MonoBehaviour, IXRSelectFilter
    {
        public enum Action { TurnLeft, Reset, TurnRight }

        [SerializeField] PhotoHelmetGalleryDisplay display;
        [SerializeField] Action action;
        [SerializeField] Renderer feedbackRenderer;
        [SerializeField] Material pressedMaterial;

        XRSimpleInteractable interactable;
        HelmetDiegeticStation station;
        GameObject confirmationRoot;
        bool registered, availabilityFilterInstalled, originalFaceEnabled;

        public PhotoHelmetGalleryDisplay Display => display;
        public Action ButtonAction => action;
        public HelmetPhysicalControl PhysicalControl { get; private set; }
        bool Ready => isActiveAndEnabled && display && display.isActiveAndEnabled &&
            station && station.isActiveAndEnabled && station.IsReady && registered &&
            PhysicalControl && PhysicalControl.isActiveAndEnabled;
        bool IXRSelectFilter.canProcess => true;
        bool IXRSelectFilter.Process(IXRSelectInteractor hand, IXRSelectInteractable target) =>
            Ready && !station.HasActiveGesture;

        public void Configure(PhotoHelmetGalleryDisplay target, Action buttonAction,
            Renderer renderer, Material pressed)
        {
            display = target;
            action = buttonAction;
            feedbackRenderer = renderer;
            pressedMaterial = pressed;
            if (Application.isPlaying && isActiveAndEnabled) RefreshBinding();
        }

        void Awake() => InstallAvailabilityFilter();

        void InstallAvailabilityFilter()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (!Application.isPlaying) return;
            // This filter stays installed while this component is disabled, so a saved
            // XRSimpleInteractable cannot accept an unguarded press during late setup.
            if (!availabilityFilterInstalled)
            {
                interactable.selectFilters.Add(this);
                availabilityFilterInstalled = true;
            }
        }

        void OnEnable()
        {
            InstallAvailabilityFilter();
            if (!Application.isPlaying) return;
            RefreshBinding();
        }

        void OnDisable()
        {
            if (Application.isPlaying) SuspendControl();
        }

        void Update()
        {
            RefreshBinding();
        }

        void RefreshBinding()
        {
            if (!station)
            {
                // A destroyed/replaced station must not leave callbacks or a distance
                // override referring to its old physical control.
                if (confirmationRoot) ReleaseControl();
                station = FindObjectOfType<HelmetDiegeticStation>(true);
            }
            if (!display || !display.isActiveAndEnabled || !station || !station.IsReady || !station.isActiveAndEnabled)
            {
                SuspendControl();
                return;
            }
            if (!PhysicalControl)
            {
                // The saved button cube is scaled. Put its mesh-only travel copy on
                // an unscaled sibling so the shared control does not apply that scale twice.
                confirmationRoot = new GameObject("Photo helmet confirmation - " + action);
                confirmationRoot.transform.SetParent(transform.parent, false);
                confirmationRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
                originalFaceEnabled = feedbackRenderer && feedbackRenderer.enabled;
                PhysicalControl = confirmationRoot.AddComponent<HelmetPhysicalControl>();
                var tint = pressedMaterial && pressedMaterial.HasProperty("_BaseColor")
                    ? pressedMaterial.GetColor("_BaseColor") : new Color(.16f, .63f, .64f);
                // Existing sibling labels keep their saved LocalizedText binding.
                PhysicalControl.Configure(station, interactable, feedbackRenderer,
                    null, null, null, null, null, tint, TriggerProgrammatically);
            }
            confirmationRoot.SetActive(true);
            if (feedbackRenderer) feedbackRenderer.enabled = false;
            if (!registered)
            {
                station.RegisterControl(PhysicalControl);
                registered = true;
            }
        }

        void SuspendControl()
        {
            // Cancel before removing the filter or ending XRI selection. The common
            // control propagates its held-press guard to the station and the same hand.
            if (registered && station && PhysicalControl) station.UnregisterControl(PhysicalControl);
            registered = false;
            if (PhysicalControl) PhysicalControl.CancelPress();
            if (interactable && interactable.isSelected && interactable.interactionManager)
                interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)interactable);
            if (confirmationRoot) confirmationRoot.SetActive(false);
            if (PhysicalControl && feedbackRenderer) feedbackRenderer.enabled = originalFaceEnabled;
        }

        public void TriggerProgrammatically()
        {
            if (!display || !display.isActiveAndEnabled) return;
            if (Application.isPlaying && (!Ready || station.HasActiveGesture)) return;
            switch (action)
            {
                case Action.TurnLeft: display.TurnLeft(); break;
                case Action.Reset: display.ResetDisplay(); break;
                case Action.TurnRight: display.TurnRight(); break;
            }
        }

        void ReleaseControl()
        {
            SuspendControl();
            if (interactable) interactable.getDistanceOverride = null;
            if (confirmationRoot) Destroy(confirmationRoot);
            confirmationRoot = null;
            PhysicalControl = null;
        }

        void OnDestroy()
        {
            ReleaseControl();
            if (interactable && availabilityFilterInstalled) interactable.selectFilters.Remove(this);
        }
    }
}
