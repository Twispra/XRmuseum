using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Museum.Exploration
{
    /// <summary>Returns the tracked camera to the saved gallery start without changing exhibit state.</summary>
    public sealed class VisitorViewRecovery : MonoBehaviour
    {
        public static event Action<XROrigin> ViewResetRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearResetObservers() => ViewResetRequested = null;

        XROrigin origin;
        Museum.Inspection.HelmetInspectionPalette palette;
        Vector3 homeOriginPosition;
        Vector3 homeForward;

        public bool IsReady => origin && origin.Camera && homeForward.sqrMagnitude > .5f;

        public void Configure(XROrigin xrOrigin, Museum.Inspection.HelmetInspectionPalette follower)
        {
            if (!xrOrigin || !xrOrigin.Camera || origin) return;
            origin = xrOrigin;
            palette = follower;
            var baseTransform = origin.Origin ? origin.Origin.transform : origin.transform;
            homeOriginPosition = baseTransform.position;
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
            ViewResetRequested?.Invoke(origin);
            if (!origin.MatchOriginUpCameraForward(Vector3.up, homeForward)) return false;
            var baseTransform = origin.Origin ? origin.Origin.transform : origin.transform;
            // Restore the original floor while retaining the current tracked eye height,
            // including crouching and the Device-mode camera offset. Use world offsets
            // so a scaled rig also keeps its tracked pose relative to that floor.
            var cameraOffset = Vector3.ProjectOnPlane(origin.Camera.transform.position - baseTransform.position, Vector3.up);
            baseTransform.position = homeOriginPosition - cameraOffset;
            if (palette) palette.RecenterNow();
            return true;
        }
    }
}
