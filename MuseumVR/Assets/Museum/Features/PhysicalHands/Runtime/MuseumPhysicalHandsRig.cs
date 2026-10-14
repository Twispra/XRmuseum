using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.PhysicalHands
{
    /// <summary>Installs world-space palms without changing the saved XR camera, input actions or sample prefabs.</summary>
    [DefaultExecutionOrder(-29990), DisallowMultipleComponent]
    public sealed class MuseumPhysicalHandsRig : MonoBehaviour
    {
        [SerializeField] ActionBasedController leftController;
        [SerializeField] ActionBasedController rightController;
        [SerializeField] MuseumPhysicalHand leftPrefab;
        [SerializeField] MuseumPhysicalHand rightPrefab;
        [NonSerialized] HandBinding left, right;
        readonly System.Collections.Generic.Dictionary<XRBaseInteractable, XRBaseInteractable.DistanceCalculationMode> controlDistances =
            new System.Collections.Generic.Dictionary<XRBaseInteractable, XRBaseInteractable.DistanceCalculationMode>();
        public MuseumPhysicalHand LeftHand => left?.hand;
        public MuseumPhysicalHand RightHand => right?.hand;
        public ActionBasedController LeftController => leftController;
        public ActionBasedController RightController => rightController;
        sealed class HandBinding
        {
            public ActionBasedController controller;
            public MuseumPhysicalHand hand;
            public Transform target, directTransform, pokeTransform;
            public Vector3 directPosition, pokePosition;
            public Quaternion directRotation, pokeRotation;
            public bool hiddenModel;
            public bool directEnabled, pokeEnabled;
            public SphereCollider[] triggers;
            public float[] radii;
        }
        public void Configure(ActionBasedController leftInput, ActionBasedController rightInput, MuseumPhysicalHand leftHand, MuseumPhysicalHand rightHand)
        { leftController = leftInput; rightController = rightInput; leftPrefab = leftHand; rightPrefab = rightHand; }
        void Awake()
        {
            if (!leftController || !rightController || !leftPrefab || !rightPrefab)
            { Debug.LogError("Museum physical hands require both saved controller and palm prefab references.", this); enabled = false; return; }
            left = Install(leftController, leftPrefab, true); right = Install(rightController, rightPrefab, false);
            Activate(left, isActiveAndEnabled); Activate(right, isActiveAndEnabled);
        }
        HandBinding Install(ActionBasedController controller, MuseumPhysicalHand prefab, bool isLeft)
        {
            var direct = controller.GetComponentInChildren<XRDirectInteractor>(true);
            if (!direct) throw new InvalidOperationException("The museum hand requires the controller's existing Direct Interactor.");
            var poke = controller.GetComponentInChildren<XRPokeInteractor>(true);
            var result = new HandBinding { controller = controller, directTransform = direct.transform,
                directPosition = direct.transform.localPosition, directRotation = direct.transform.localRotation,
                pokeTransform = poke ? poke.transform : null, pokePosition = poke ? poke.transform.localPosition : Vector3.zero,
                pokeRotation = poke ? poke.transform.localRotation : Quaternion.identity, hiddenModel = controller.hideControllerModel,
                directEnabled = direct.enabled, pokeEnabled = poke && poke.enabled };
            result.target = new GameObject(isLeft ? "Left Palm Tracking Target" : "Right Palm Tracking Target").transform;
            result.target.SetParent(direct.transform.parent, false);
            result.target.localPosition = result.directPosition; result.target.localRotation = result.directRotation;
            result.hand = Instantiate(prefab, result.target.position, result.target.rotation, transform);
            result.hand.name = isLeft ? "Left Physical Hand" : "Right Physical Hand";
            var visual = result.hand.GetComponentInChildren<MuseumHandVisual>(true);
            result.hand.Configure(result.target, controller, direct, visual, isLeft);
            result.hand.ConfigurePoke(poke);
            result.hand.ConfigureRays(controller.GetComponentsInChildren<XRRayInteractor>(true));
            // The Direct trigger is a selection affordance, distinct from the smaller solid palm.
            result.triggers = direct.GetComponents<SphereCollider>(); result.radii = new float[result.triggers.Length];
            for (int i = 0; i < result.triggers.Length; i++) { result.radii[i] = result.triggers[i].radius; if (result.triggers[i].isTrigger) result.triggers[i].radius = result.hand.OpenSelectionRadius; }
            controller.hideControllerModel = true;
            return result;
        }
        void Start()
        {
            foreach (var control in FindObjectsOfType<XRBaseInteractable>(true))
            {
                if (!control.GetComponent<Museum.Inspection.HelmetInspectionControls>() &&
                    !control.GetComponent<Museum.Exploration.HelmetExplorationButton>()) continue;
                controlDistances[control] = control.distanceCalculationMode;
            }
            SetControlDistances(true);
            if (Debug.isDebugBuild)
            {
                var diagnostics = GetComponent<MuseumHandDeviceDiagnostics>();
                if (!diagnostics) diagnostics = gameObject.AddComponent<MuseumHandDeviceDiagnostics>();
                diagnostics.Configure(this, "physical-hands-device-feedback-20261014");
            }
        }
        void SetControlDistances(bool active)
        {
            // Flat controls rank by the touched surface, like the window and
            // discovery markers, rather than a centre far from that contact.
            foreach (var pair in controlDistances) if (pair.Key)
                pair.Key.distanceCalculationMode = active ? XRBaseInteractable.DistanceCalculationMode.ColliderVolume : pair.Value;
        }
        void OnEnable() { Activate(left, true); Activate(right, true); SetControlDistances(true); }
        void OnDisable() { Activate(left, false); Activate(right, false); SetControlDistances(false); }
        void LateUpdate()
        {
            // XRI selection exits can unhide a controller model even when its select-hide option is off.
            if (leftController) leftController.hideControllerModel = true;
            if (rightController) rightController.hideControllerModel = true;
        }
        static void Activate(HandBinding binding, bool active)
        {
            if (binding == null) return;
            if (binding.hand) binding.hand.gameObject.SetActive(active);
            if (binding.controller) binding.controller.hideControllerModel = active || binding.hiddenModel;
            if (binding.triggers != null) for (int i = 0; i < binding.triggers.Length; i++)
                if (binding.triggers[i]) binding.triggers[i].radius = active && binding.triggers[i].isTrigger && binding.hand ? binding.hand.OpenSelectionRadius : binding.radii[i];
            if (!active)
            {
                if (binding.directTransform) { binding.directTransform.localPosition = binding.directPosition; binding.directTransform.localRotation = binding.directRotation; }
                if (binding.pokeTransform) { binding.pokeTransform.localPosition = binding.pokePosition; binding.pokeTransform.localRotation = binding.pokeRotation; }
                if (binding.directTransform) binding.directTransform.GetComponent<XRDirectInteractor>().enabled = binding.directEnabled;
                if (binding.pokeTransform) binding.pokeTransform.GetComponent<XRPokeInteractor>().enabled = binding.pokeEnabled;
            }
        }
        void OnDestroy()
        {
            Activate(left, false); Activate(right, false);
            if (left?.target) Destroy(left.target.gameObject);
            if (right?.target) Destroy(right.target.gameObject);
            if (left?.hand) Destroy(left.hand.gameObject);
            if (right?.hand) Destroy(right.hand.gameObject);
        }
    }
}
