using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Eras
{
    /// <summary>Reachable buttons that step the exhibit through the era catalogue, matching the exhibit's one-handed language.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class HelmetEraButton : MonoBehaviour
    {
        // 追加取值即可；已有枚举值必须保持稳定，避免旧 Prefab 的序列化数据错位。
        public enum StepDirection { Next, Previous }

        [SerializeField] HelmetEraSwitcher switcher;
        [SerializeField] StepDirection direction = StepDirection.Next;
        [SerializeField] Renderer feedbackRenderer;
        [SerializeField] Material pressedMaterial;
        [SerializeField, Min(0f)] float pressFeedbackDuration = 0.12f;

        XRSimpleInteractable interactable;
        Material originalMaterial;
        bool feedbackActive;
        float feedbackUntil = -1f;

        void OnEnable()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable != null)
                interactable.selectEntered.AddListener(OnPressed);
            if (feedbackRenderer != null && originalMaterial == null)
                originalMaterial = feedbackRenderer.sharedMaterial;
        }

        void OnDisable()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnPressed);
            ClearFeedback();
        }

        void Update()
        {
            if (feedbackActive && Time.unscaledTime >= feedbackUntil)
                ClearFeedback();
        }

        void OnPressed(SelectEnterEventArgs args)
        {
            if (switcher == null || !switcher.isActiveAndEnabled)
                return;

            if (direction == StepDirection.Next) switcher.ShowNextEra();
            else switcher.ShowPreviousEra();

            ShowPressFeedback();
        }

        /// <summary>Steps the catalogue without a hand, so a desktop preview or the self-check can drive it.</summary>
        public void TriggerProgrammatically()
        {
            if (switcher == null) return;
            if (direction == StepDirection.Next) switcher.ShowNextEra();
            else switcher.ShowPreviousEra();
        }

        void ShowPressFeedback()
        {
            if (feedbackRenderer != null && pressedMaterial != null)
            {
                feedbackRenderer.sharedMaterial = pressedMaterial;
                feedbackActive = true;
                feedbackUntil = Time.unscaledTime + pressFeedbackDuration;
            }
        }

        void ClearFeedback()
        {
            if (feedbackRenderer != null && originalMaterial != null)
                feedbackRenderer.sharedMaterial = originalMaterial;
            feedbackActive = false;
            feedbackUntil = -1f;
        }

        public HelmetEraSwitcher Switcher => switcher;
        public StepDirection Direction => direction;

        /// <summary>Wires the button from an Editor builder or the scene setup script.</summary>
        public void Configure(HelmetEraSwitcher target, StepDirection stepDirection, Renderer feedback, Material pressed)
        {
            switcher = target;
            direction = stepDirection;
            feedbackRenderer = feedback;
            pressedMaterial = pressed;
        }
    }
}
