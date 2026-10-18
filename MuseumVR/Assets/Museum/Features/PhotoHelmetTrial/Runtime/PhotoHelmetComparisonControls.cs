using UnityEngine;
using UnityEngine.InputSystem;

namespace Museum.PhotoHelmetTrial
{
    /// <summary>Desktop-only review controls for the isolated comparison scene.</summary>
    public sealed class PhotoHelmetComparisonControls : MonoBehaviour
    {
        [SerializeField] Transform currentModel;
        [SerializeField] Transform photoModel;
        [SerializeField] PhotoHelmetSectionPreview photoSection;

        Quaternion currentStart;
        Quaternion photoStart;
        float angle;

        public void Configure(Transform current, Transform photo, PhotoHelmetSectionPreview section)
        {
            currentModel = current;
            photoModel = photo;
            photoSection = section;
            CaptureStart();
        }

        void Awake() => CaptureStart();

        void CaptureStart()
        {
            if (!currentModel || !photoModel) return;
            currentStart = currentModel.rotation;
            photoStart = photoModel.rotation;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !currentModel || !photoModel) return;
            if (keyboard.rKey.wasPressedThisFrame)
            {
                angle = 0;
                if (photoSection) photoSection.ResetPreview();
            }
            if (photoSection && keyboard.sKey.wasPressedThisFrame) photoSection.ToggleCutaway();
            if (photoSection && photoSection.IsCutaway)
            {
                if (keyboard.qKey.wasPressedThisFrame) photoSection.AdjustDepth(-0.10f);
                if (keyboard.eKey.wasPressedThisFrame) photoSection.AdjustDepth(0.10f);
            }
            float direction = 0;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) direction -= 1;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) direction += 1;
            angle += direction * 60f * Time.deltaTime;
            var turn = Quaternion.Euler(0, angle, 0);
            currentModel.rotation = turn * currentStart;
            photoModel.rotation = turn * photoStart;
        }

        void OnGUI()
        {
            string section = photoSection && photoSection.IsCutaway
                ? "Section ON: depth " + photoSection.Depth.ToString("+0.00;-0.00;0.00") +
                  ", cap " + photoSection.CapTriangles + " tris"
                : "Section OFF";
            GUI.Box(new Rect(12, 12, 520, 78), "Mark V (left) / photo study (right)\nA/D rotate  |  R reset  |  S section  |  Q/E depth  |  " + section +
                "\nInterpretive hollow shell only; no verified hinge or XR link.");
        }
    }
}
