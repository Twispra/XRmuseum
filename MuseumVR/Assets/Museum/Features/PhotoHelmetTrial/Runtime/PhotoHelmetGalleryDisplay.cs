using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhotoHelmetTrial
{
    /// <summary>Independent photo-study exhibit; it never changes the three-era helmet catalogue.</summary>
    public sealed class PhotoHelmetGalleryDisplay : MonoBehaviour
    {
        [SerializeField] Transform model;
        [SerializeField] Quaternion frontRotation = Quaternion.identity;
        [SerializeField, Range(10f, 90f)] float turnStepDegrees = 30f;

        public Transform Model => model;
        public Quaternion FrontRotation => frontRotation;

        public void Configure(Transform target, float stepDegrees = 30f)
        {
            model = target;
            frontRotation = target ? target.localRotation : Quaternion.identity;
            turnStepDegrees = Mathf.Clamp(stepDegrees, 10f, 90f);
        }

        public void TurnLeft() => Turn(turnStepDegrees);
        public void TurnRight() => Turn(-turnStepDegrees);

        public void ResetDisplay()
        {
            if (!model) return;
            model.localRotation = frontRotation;
            var preview = model.GetComponent<PhotoHelmetSectionPreview>();
            if (Application.isPlaying && preview) preview.ResetPreview();
        }

        void Turn(float degrees)
        {
            if (!model) return;
            model.localRotation = Quaternion.AngleAxis(degrees, Vector3.up) * model.localRotation;
        }

        void OnApplicationQuit() => UnregisterButtons();

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            foreach (var button in GetComponentsInChildren<XRSimpleInteractable>(true))
                if (button) button.enabled = true;
        }

        void OnDisable()
        {
            if (Application.isPlaying) UnregisterButtons();
        }

        void UnregisterButtons()
        {
            foreach (var button in GetComponentsInChildren<XRSimpleInteractable>(true))
                if (button) button.enabled = false;
        }
    }
}
