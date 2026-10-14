using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Exploration
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class HelmetExplorationButton : MonoBehaviour
    {
        // Append only: existing scene assets serialize these numeric values.
        public enum Action { Explore, Inspect, Air, Voice, Hint, Review, Replay, Stop, Back, NewVisit,
            More, Help, Details, CloseReading, InspectMore, ReduceMotion, Sound, CenterView }
        [SerializeField] HelmetExplorationController controller;
        [SerializeField] HelmetExplorationPanel panel;
        [SerializeField] Action action;
        XRSimpleInteractable interactable;
        Renderer surface;
        MaterialPropertyBlock block;
        bool pending;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public Action Command => action;

        public void Configure(HelmetExplorationController target, HelmetExplorationPanel view, Action command)
        { controller = target; panel = view; action = command; }
        void OnEnable()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            surface = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            interactable.selectEntered.AddListener(Pressed);
        }
        void OnDisable()
        {
            pending = false;
            if (interactable) interactable.selectEntered.RemoveListener(Pressed);
            if (surface) surface.SetPropertyBlock(null);
        }
        void Pressed(SelectEnterEventArgs args) { pending = true; }
        void Update()
        {
            if (surface && surface.sharedMaterial && surface.sharedMaterial.HasProperty(BaseColor))
            {
                var color = surface.sharedMaterial.GetColor(BaseColor);
                if (panel && panel.IsCommandSelected(action)) color = Color.Lerp(color, Color.white, .22f);
                block.SetColor(BaseColor, interactable.isHovered || interactable.isSelected ? Color.Lerp(color, Color.white, .35f) : color);
                surface.SetPropertyBlock(block);
            }
            if (!pending) return;
            pending = false; // Dispatch outside XRI's select callback before a page hides any controls.
            if (!panel) return;
            // Returning to the original tools must work even when interpretation is disabled.
            if (action == Action.Inspect) { panel.ShowInspection(); return; }
            if (action == Action.CenterView) { panel.ResetView(); return; }
            if (!controller || !controller.isActiveAndEnabled) return;
            switch (action)
            {
                case Action.Explore: panel.ShowExploration(); break;
                case Action.Inspect: panel.ShowInspection(); break;
                case Action.Air: controller.SelectClue(HelmetExplorationController.Clue.Air); break;
                case Action.Voice: controller.SelectClue(HelmetExplorationController.Clue.Voice); break;
                case Action.Hint: controller.RequestHint(); break;
                case Action.Review: controller.ShowReview(); break;
                case Action.Replay: controller.Replay(); break;
                case Action.Stop: controller.Stop(); break;
                case Action.Back: controller.ShowWelcome(); break;
                case Action.NewVisit: controller.RestartVisit(); break;
                case Action.More: panel.ToggleMore(); break;
                case Action.Help: panel.ShowHelp(); break;
                case Action.Details: panel.ShowDetails(); break;
                case Action.CloseReading: panel.CloseReading(); break;
                case Action.InspectMore: panel.ToggleInspectionTools(); break;
                case Action.ReduceMotion: panel.ToggleReduceMotion(); break;
                case Action.Sound: panel.ToggleSound(); break;
            }
            // Controller changes notify the panel; panel commands refresh themselves.
        }
    }
}
