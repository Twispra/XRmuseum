using Museum.Diegetic;
using Museum.SurfaceLine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Museum.Navigation
{
    /// <summary>Keyboard access to the two exhibits in a Windows window without a headset.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(360)]
    public sealed class DesktopExhibitVisit : MonoBehaviour
    {
        GalleryNavigator gallery;
        SurfaceCall telephone;
        Transform routeBoard;
        HelmetPhysicalControl pressedKey;
        Quaternion visitorRotation;
        float visitorFov;
        int framedNavigation;
        bool desktopView, paletteWasVisible;

        void Start()
        {
            gallery = GetComponent<GalleryNavigator>();
            telephone = FindObjectOfType<SurfaceCall>();
            var board = GameObject.Find("Right Exhibit Panel");
            if (board) routeBoard = board.transform;
        }

        void Update()
        {
            if (!gallery || !gallery.IsReady || Keyboard.current == null || XRSettings.isDeviceActive)
            {
                RestoreView();
                return;
            }
            if (!Application.isFocused) { CancelKey(); return; }
            var keyboard = Keyboard.current;
            if (keyboard.escapeKey.wasPressedThisFrame ||
                (desktopView && !gallery.IsTransitioning && !gallery.CurrentExhibit.HasValue))
            { RestoreView(); return; }
            if (keyboard.digit2Key.wasPressedThisFrame) Travel(GalleryNavigator.Exhibit.Main);
            else if (keyboard.digit3Key.wasPressedThisFrame) Travel(GalleryNavigator.Exhibit.Right);
            if (!desktopView) return;

            if (gallery.CompletedNavigations != framedNavigation)
            {
                framedNavigation = gallery.CompletedNavigations;
                FrameExhibit();
            }
            bool atPhone = gallery.CurrentExhibit == GalleryNavigator.Exhibit.Main && !gallery.IsTransitioning &&
                telephone && telephone.isActiveAndEnabled && telephone.Control &&
                telephone.installation.activeInHierarchy && telephone.key.isActiveAndEnabled;
            if (!atPhone) { CancelKey(); return; }
            if (keyboard.cKey.wasPressedThisFrame && telephone.Control.BeginKeyboardPress())
                pressedKey = telephone.Control;
            if (pressedKey && keyboard.cKey.wasReleasedThisFrame)
            {
                pressedKey.EndKeyboardPress(true);
                pressedKey = null;
            }
        }

        void Travel(GalleryNavigator.Exhibit destination)
        {
            CancelKey();
            if (!gallery.Request(destination)) return;
            if (!desktopView)
            {
                var eye = gallery.Origin.Camera;
                visitorRotation = eye.transform.localRotation;
                visitorFov = eye.fieldOfView;
                paletteWasVisible = gallery.Palette.gameObject.activeSelf;
                gallery.Palette.gameObject.SetActive(false);
                desktopView = true;
            }
            framedNavigation = gallery.CompletedNavigations;
        }

        void FrameExhibit()
        {
            var eye = gallery.Origin.Camera;
            if (gallery.CurrentExhibit == GalleryNavigator.Exhibit.Right && routeBoard)
            {
                var backing = routeBoard.Find("Backing");
                if (!backing || !backing.TryGetComponent<Renderer>(out var renderer)) return;
                var bounds = renderer.bounds;
                Vector3 aim = bounds.center - eye.transform.position;
                eye.transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
                // Fit both endpoints in wide and narrow windows, with a margin around the arrows.
                float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / eye.aspect);
                eye.fieldOfView = Mathf.Clamp(2 * Mathf.Atan2(halfHeight * 1.3f, aim.magnitude) * Mathf.Rad2Deg, 24, 90);
            }
            else if (gallery.CurrentExhibit == GalleryNavigator.Exhibit.Main && telephone)
            {
                Vector3 text = telephone.words.transform.position;
                Vector3 key = telephone.key.transform.position;
                Vector3 target = (key + text + telephone.surfaceFlag.transform.position) / 3f;
                eye.transform.rotation = Quaternion.LookRotation(target - eye.transform.position, Vector3.up);
                eye.fieldOfView = visitorFov;
            }
        }

        void CancelKey()
        {
            if (pressedKey) pressedKey.EndKeyboardPress(false);
            pressedKey = null;
        }

        void RestoreView()
        {
            CancelKey();
            if (!desktopView) return;
            desktopView = false;
            if (!gallery || !gallery.Origin || !gallery.Origin.Camera) return;
            var eye = gallery.Origin.Camera;
            eye.transform.localRotation = visitorRotation;
            eye.fieldOfView = visitorFov;
            if (gallery.Palette) gallery.Palette.gameObject.SetActive(paletteWasVisible);
        }

        void OnApplicationFocus(bool focused) { if (!focused) CancelKey(); }
        void OnDisable() => RestoreView();
    }
}
