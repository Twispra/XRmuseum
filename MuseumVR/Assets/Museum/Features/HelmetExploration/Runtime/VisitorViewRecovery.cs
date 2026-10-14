using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Museum.Exploration
{
    /// <summary>Returns the tracked camera to the saved gallery start without changing exhibit state.</summary>
    public sealed class VisitorViewRecovery : MonoBehaviour
    {
        XROrigin origin;
        Museum.Inspection.HelmetInspectionPalette palette;
        Vector3 homeCameraPosition;
        Vector3 homeForward;

        public bool IsReady => origin && origin.Camera && homeForward.sqrMagnitude > .5f;

        public void Configure(XROrigin xrOrigin, Museum.Inspection.HelmetInspectionPalette follower)
        {
            if (!xrOrigin || !xrOrigin.Camera || origin) return;
            origin = xrOrigin;
            palette = follower;
            var baseTransform = origin.Origin ? origin.Origin.transform : origin.transform;
            homeCameraPosition = baseTransform.position + Vector3.up * origin.CameraYOffset;
            homeForward = Vector3.ProjectOnPlane(baseTransform.forward, Vector3.up).normalized;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
                ResetView();
        }

        public bool ResetView()
        {
            if (!IsReady) return false;
            if (!origin.MatchOriginUpCameraForward(Vector3.up, homeForward)) return false;
            if (!origin.MoveCameraToWorldLocation(homeCameraPosition)) return false;
            if (palette) palette.RecenterNow();
            return true;
        }
    }
}
