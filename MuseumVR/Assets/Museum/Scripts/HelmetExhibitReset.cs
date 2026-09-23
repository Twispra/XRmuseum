using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace Museum
{
    /// <summary>Returns a dropped exhibit to its display, without interrupting normal grabs.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class HelmetExhibitReset : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] bool keyboardHomeReset = true;
        [SerializeField] float fallY = -2f;
        [SerializeField, Min(1f)] float maximumDistanceFromHome = 12f;
        [SerializeField, Min(0f)] float outOfBoundsDelay = 0.5f;
        [SerializeField, HideInInspector] bool hasConfiguredHome;
        [SerializeField, HideInInspector] Vector3 homePosition;
        [SerializeField, HideInInspector] Quaternion homeRotation = Quaternion.identity;

        readonly List<IXRSelectInteractor> releasedInteractors = new List<IXRSelectInteractor>();
        Rigidbody body;
        XRGrabInteractable grab;
        float outsideSince = -1f;
        float selectionBlockedUntil;
        bool resetting;

        public int ResetCount { get; private set; }
        public bool IsHeld => grab != null && grab.isSelected;
        public Pose HomePose => new Pose(homePosition, homeRotation);
        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        void Awake()
        {
            EnsureReferencesAndHome();
        }

        void OnEnable()
        {
            EnsureReferencesAndHome();
            grab.selectFilters.Add(this);
        }

        void OnDisable()
        {
            if (grab != null)
                grab.selectFilters.Remove(this);
            outsideSince = -1f;
        }

        void Update()
        {
            // A reset must not snap straight back to a hand whose grip is still held.
            releasedInteractors.RemoveAll(HasReleasedOrDisappeared);

            if (keyboardHomeReset && Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame)
            {
                ResetToDisplay();
                return;
            }

            if (IsHeld)
            {
                outsideSince = -1f;
                return;
            }

            var displacement = transform.position - homePosition;
            bool outside = transform.position.y < fallY ||
                           displacement.sqrMagnitude > maximumDistanceFromHome * maximumDistanceFromHome;
            if (!outside)
            {
                outsideSince = -1f;
                return;
            }

            if (outsideSince < 0f)
                outsideSince = Time.unscaledTime;
            if (Time.unscaledTime - outsideSince >= outOfBoundsDelay)
                ResetToDisplay();
        }

        /// <summary>Sets the display's world pose; safe to call when building a scene in the editor.</summary>
        public void ConfigureHomePose(Vector3 worldPosition, Quaternion worldRotation)
        {
            homePosition = worldPosition;
            homeRotation = worldRotation;
            hasConfiguredHome = true;
        }

        /// <summary>Releases every selecting hand, clears throw motion, and restores the display pose.</summary>
        [ContextMenu("Reset To Display")]
        public void ResetToDisplay()
        {
            if (resetting)
                return;

            EnsureReferencesAndHome();
            resetting = true;
            bool previousThrowOnDetach = grab.throwOnDetach;
            grab.throwOnDetach = false;
            selectionBlockedUntil = Time.unscaledTime + 0.15f;
            try
            {
                var selectors = grab.interactorsSelecting.ToArray();
                foreach (var interactor in selectors)
                {
                    if (!releasedInteractors.Contains(interactor))
                        releasedInteractors.Add(interactor);
                    grab.interactionManager.SelectExit(interactor, grab);
                }

                if (Application.isPlaying)
                {
                    // XRI 2.6 queues Detach until LateUpdate. Drain it while throwing is disabled,
                    // so its cached release velocity cannot overwrite the zero velocity below.
                    grab.ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase.Late);
                    grab.SetTargetPose(HomePose);
                }

                transform.SetPositionAndRotation(homePosition, homeRotation);
                body.position = homePosition;
                body.rotation = homeRotation;
                if (!body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.Sleep();
                }
                Physics.SyncTransforms();
                outsideSince = -1f;
                ResetCount++;
            }
            finally
            {
                grab.throwOnDetach = previousThrowOnDetach;
                resetting = false;
            }
        }

        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            return !resetting && Time.unscaledTime >= selectionBlockedUntil &&
                   !releasedInteractors.Contains(interactor);
        }

        static bool HasReleasedOrDisappeared(IXRSelectInteractor interactor)
        {
            return interactor == null || (interactor is Object unityObject && unityObject == null) ||
                   !interactor.isSelectActive;
        }

        void EnsureReferencesAndHome()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (grab == null)
                grab = GetComponent<XRGrabInteractable>();
            if (!hasConfiguredHome)
                ConfigureHomePose(transform.position, transform.rotation);
        }
    }
}
