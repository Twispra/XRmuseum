using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhotoHelmetTrial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class PhotoHelmetGalleryButton : MonoBehaviour
    {
        public enum Action { TurnLeft, Reset, TurnRight }

        [SerializeField] PhotoHelmetGalleryDisplay display;
        [SerializeField] Action action;
        [SerializeField] Renderer feedbackRenderer;
        [SerializeField] Material pressedMaterial;

        XRSimpleInteractable interactable;
        Material originalMaterial;
        float feedbackUntil;

        public PhotoHelmetGalleryDisplay Display => display;
        public Action ButtonAction => action;

        public void Configure(PhotoHelmetGalleryDisplay target, Action buttonAction,
            Renderer renderer, Material pressed)
        {
            display = target;
            action = buttonAction;
            feedbackRenderer = renderer;
            pressedMaterial = pressed;
            originalMaterial = renderer ? renderer.sharedMaterial : null;
        }

        void OnEnable()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable) interactable.selectEntered.AddListener(OnSelected);
            if (feedbackRenderer) originalMaterial = feedbackRenderer.sharedMaterial;
        }

        void OnDisable()
        {
            if (interactable) interactable.selectEntered.RemoveListener(OnSelected);
            RestoreFeedback();
        }

        void Update()
        {
            if (feedbackUntil > 0f && Time.unscaledTime >= feedbackUntil)
                RestoreFeedback();
        }

        void OnSelected(SelectEnterEventArgs _) => TriggerProgrammatically();

        public void TriggerProgrammatically()
        {
            if (!display || !display.isActiveAndEnabled) return;
            switch (action)
            {
                case Action.TurnLeft: display.TurnLeft(); break;
                case Action.Reset: display.ResetDisplay(); break;
                case Action.TurnRight: display.TurnRight(); break;
            }
            if (!feedbackRenderer || !pressedMaterial) return;
            feedbackRenderer.sharedMaterial = pressedMaterial;
            feedbackUntil = Time.unscaledTime + 0.12f;
        }

        void RestoreFeedback()
        {
            if (feedbackRenderer && originalMaterial)
                feedbackRenderer.sharedMaterial = originalMaterial;
            feedbackUntil = 0f;
        }
    }
}
