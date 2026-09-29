using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Inspection
{
    /// <summary>Optional reachable buttons for users who prefer a click over a constrained drag.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class HelmetInspectionControls : MonoBehaviour
    {
        // Append values: existing prefab enum values must remain stable.
        public enum Action { OpenWindow, Cutaway, RotateLeft, RotateRight, Reset,
            Float, TiltUp, TiltDown, Side, Top, Front, DepthIn, DepthOut, Flip, Align, EditCut, CenterCut }

        [SerializeField] HelmetInspectionController controller;
        [SerializeField] Action action;
        XRSimpleInteractable interactable;

        void OnEnable()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable != null)
                interactable.selectEntered.AddListener(OnPressed);
        }

        void OnDisable()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnPressed);
        }

        public void Configure(HelmetInspectionController inspectionController, Action buttonAction)
        {
            controller = inspectionController;
            action = buttonAction;
        }

        void OnPressed(SelectEnterEventArgs args)
        {
            if (controller == null || !controller.isActiveAndEnabled)
                return;
            switch (action)
            {
                case Action.OpenWindow: controller.ToggleWindow(); break;
                case Action.Cutaway: controller.ToggleCutaway(); break;
                case Action.RotateLeft: controller.RotateDisplay(-30f); break;
                case Action.RotateRight: controller.RotateDisplay(30f); break;
                case Action.Reset: controller.ResetInspection(); break;
                case Action.Float: controller.ToggleFloating(); break;
                case Action.TiltUp: controller.RotateDisplayAround(ViewRight(), -20f); break;
                case Action.TiltDown: controller.RotateDisplayAround(ViewRight(), 20f); break;
                case Action.Side: controller.SetSectionPreset(0); break;
                case Action.Top: controller.SetSectionPreset(1); break;
                case Action.Front: controller.SetSectionPreset(2); break;
                case Action.DepthIn: controller.AdjustSectionDepth(-.15f); break;
                case Action.DepthOut: controller.AdjustSectionDepth(.15f); break;
                case Action.Flip: controller.FlipSection(); break;
                case Action.Align: if (Camera.main) controller.AlignSectionToViewer(Camera.main.transform); break;
                case Action.CenterCut: controller.ResetSection(); break;
                case Action.EditCut:
                    var tool = controller.GetComponentInChildren<HelmetSectionInteractable>(true);
                    if (tool) tool.ShowTool = !tool.ShowTool;
                    break;
            }
        }

        static Vector3 ViewRight() => Camera.main ? Camera.main.transform.right : Vector3.right;
    }
}
