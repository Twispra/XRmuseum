using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace Museum.PhysicalHands
{
    /// <summary>A swept glove envelope. Tracking remains on the controller; XRI alone moves held objects.</summary>
    [DefaultExecutionOrder(-106), DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class MuseumPhysicalHand : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] Transform trackingTarget;
        [SerializeField] XRBaseController inputController;
        [SerializeField] XRDirectInteractor directInteractor;
        [SerializeField] MuseumHandVisual visual;
        XRPokeInteractor pokeInteractor;
        XRRayInteractor[] rayInteractors = new XRRayInteractor[0];
        readonly Dictionary<XRRayInteractor, bool> rayEnabled = new Dictionary<XRRayInteractor, bool>();
        readonly HashSet<XRRayInteractor> raysWaitingForRelease = new HashSet<XRRayInteractor>();
        Vector3 pokeOffset;
        Quaternion pokeRotation;
        [SerializeField] Transform trackingOrigin;
        [SerializeField] bool left;
        [SerializeField, Min(.01f)] float collisionRadius = .035f;
        [SerializeField, Min(.1f)] float maximumSpeed = 6f;
        [SerializeField, Min(.1f)] float releaseDistance = .65f;
        [SerializeField] LayerMask obstructionMask = ~0;
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[64];
        readonly Dictionary<Collider, bool> ignored = new Dictionary<Collider, bool>();
        readonly Dictionary<Collider, bool> envelopeIgnored = new Dictionary<Collider, bool>();
        readonly Dictionary<Collider, bool> selfCollisionStates = new Dictionary<Collider, bool>();
        CharacterController selfCharacterController;
        Rigidbody body;
        SphereCollider palm;
        BoxCollider envelope;
        XRGrabInteractable held;
        Vector3 heldGripLocal;
        Vector3 heldVisualLocal, heldVisualStartLocal, heldSurfaceLocal;
        Quaternion heldRotationLocal;
        Quaternion heldVisualStartRotation;
        float visualGripStarted;
        bool hasSurfaceGrip;
        Vector3 originPosition;
        Quaternion originRotation;
        bool originReady, filterRegistered, releaseRequired, previousTracking;
        bool queriesSuspended, directWasEnabled, pokeWasEnabled;
        bool? trackingOverride;
        const float Skin = .002f;
        static readonly Vector3 OpenSize = new Vector3(.15f, .09f, .20f);
        Vector3 OpenCenter => new Vector3((left ? 1f : -1f) * .018f, -.01f, .026f);

        public Transform TrackingTarget => trackingTarget;
        public XRBaseController InputController => inputController;
        public XRDirectInteractor DirectInteractor => directInteractor;
        public Rigidbody Body { get { EnsureBody(); return body; } }
        public SphereCollider PalmCollider { get { EnsureBody(); return palm; } }
        public Collider CollisionCollider { get { EnsureBody(); return envelope; } }
        public float CollisionRadius => collisionRadius;
        public Vector3 OpenEnvelopeSize => OpenSize;
        public Vector3 OpenEnvelopeCenter => OpenCenter;
        public float OpenSelectionRadius
        {
            get
            {
                Vector3 half = OpenSize * .5f;
                Vector3 center = OpenCenter;
                return new Vector3(half.x + Mathf.Abs(center.x), half.y + Mathf.Abs(center.y),
                    half.z + Mathf.Abs(center.z)).magnitude + .06f;
            }
        }
        public bool IsBlocked { get; private set; }
        public bool HasSurfaceGrip => held && hasSurfaceGrip;
        public Vector3 SurfaceContactPoint => held ? held.transform.TransformPoint(heldSurfaceLocal) : transform.position;
        public float TrackingError => trackingTarget ? Vector3.Distance(trackingTarget.position, body ? body.position : transform.position) : 0f;
        public bool IsTrackingValid => trackingOverride ?? (trackingTarget && (!inputController ||
            (inputController.isActiveAndEnabled && inputController.currentControllerState != null &&
             inputController.currentControllerState.isTracked &&
             (inputController.currentControllerState.inputTrackingState & (InputTrackingState.Position | InputTrackingState.Rotation)) ==
             (InputTrackingState.Position | InputTrackingState.Rotation))));
        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        void Awake() => EnsureBody();
        void OnEnable() { EnsureBody(); MaintainSelfCollisionIsolation(); Register(); Application.onBeforeRender += BeforeRender; }
        void OnDisable()
        {
            ForceRelease();
            Unregister();
            Application.onBeforeRender -= BeforeRender;
            ResumeQueries(); previousTracking = false; originReady = false;
            RestoreSelfCollisions();
            if (palm) palm.enabled = false;
            if (envelope) envelope.enabled = false;
            if (visual) visual.gameObject.SetActive(false);
        }
        void OnDestroy() { RestoreCollisions(); RestoreSelfCollisions(true); }

        public void Configure(Transform target, XRBaseController controller, XRDirectInteractor direct, MuseumHandVisual handVisual, bool left)
        {
            RestoreSelfCollisions(true);
            Unregister();
            trackingTarget = target; inputController = controller; directInteractor = direct; visual = handVisual; this.left = left;
            var origin = controller ? controller.GetComponentInParent<XROrigin>() : null;
            trackingOrigin = origin ? origin.transform : null;
            EnsureBody();
            BindSelfCollisions(controller ? controller.GetComponentInParent<XROrigin>(true) : null);
            if (target && !Application.isPlaying) { transform.SetPositionAndRotation(target.position, target.rotation); body.position = target.position; body.rotation = target.rotation; }
            if (direct) direct.xrController = controller;
            Register();
        }
        // A deterministic no-device fixture can supply validity; null restores production tracking checks.
        public void SetTrackingOverride(bool? valid) => trackingOverride = valid;
        public void ConfigurePoke(XRPokeInteractor poke)
        {
            pokeInteractor = poke;
            if (!poke || !trackingTarget) return;
            pokeOffset = trackingTarget.InverseTransformPoint(poke.transform.position);
            pokeRotation = Quaternion.Inverse(trackingTarget.rotation) * poke.transform.rotation;
        }
        public void ConfigureRays(XRRayInteractor[] rays)
        {
            bool suspended = queriesSuspended;
            Unregister();
            // Relinquish the old set completely before assigning another set of owned queries.
            if (suspended) ResumeQueries();
            var owned = new List<XRRayInteractor>();
            if (rays != null) foreach (var ray in rays)
            {
                if (!ray || owned.Contains(ray)) continue;
                var controller = ray.xrController ? ray.xrController : ray.GetComponentInParent<XRBaseController>();
                // The saved teleport ray has its own nontracking controller beneath
                // this hand's tracked controller. Hierarchy still establishes ownership.
                if (inputController && controller != inputController && !ray.transform.IsChildOf(inputController.transform)) continue;
                owned.Add(ray);
            }
            foreach (var oldRay in rayInteractors) if (!oldRay || !owned.Contains(oldRay)) raysWaitingForRelease.Remove(oldRay);
            rayInteractors = owned.ToArray();
            Register();
            if (suspended) SuspendQueries();
        }
        void Update() => SynchronizeQueries();
        [BeforeRenderOrder(99)]
        void BeforeRender() => SynchronizeQueries();
        public void SynchronizeQueries()
        {
            MaintainSelfCollisionIsolation();
            ObserveRayReleases();
            // Keep authored interactor/group references and hierarchy while moving only their query poses.
            // The separate target never reads these transforms, so there is no tracking feedback loop.
            if (!previousTracking || !body) return;
            if (directInteractor) directInteractor.transform.SetPositionAndRotation(body.position, body.rotation);
            if (pokeInteractor) pokeInteractor.transform.SetPositionAndRotation(body.position + body.rotation * pokeOffset, body.rotation * pokeRotation);
        }

        void EnsureBody()
        {
            if (!body) body = GetComponent<Rigidbody>();
            if (!palm) palm = GetComponent<SphereCollider>();
            if (!envelope) envelope = GetComponent<BoxCollider>();
            if (!envelope) envelope = gameObject.AddComponent<BoxCollider>();
            if (!body || !palm) return;
            body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // This driver already sweeps translations and rotations. Speculative CCD
            // can create predictive contacts that fling nearby dynamic exhibits.
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            palm.radius = collisionRadius; palm.center = Vector3.zero; palm.isTrigger = false;
            palm.contactOffset = .001f; envelope.contactOffset = .001f;
            float grip = inputController && inputController.currentControllerState != null ? inputController.currentControllerState.selectInteractionState.value : 0;
            if (held) grip = Mathf.Max(grip, .72f);
            // Closing follows the visible fingers; opening expands the envelope immediately.
            if (visual) grip = Mathf.Min(grip, visual.GripAmount);
            float side = left ? 1f : -1f;
            envelope.size = Vector3.Lerp(OpenSize, new Vector3(.12f,.105f,.12f), Mathf.Clamp01(grip));
            envelope.center = Vector3.Lerp(OpenCenter, new Vector3(side*.012f,-.025f,-.006f), Mathf.Clamp01(grip));
            envelope.isTrigger = false;
        }
        void BindSelfCollisions(XROrigin origin)
        {
            selfCharacterController = origin && origin.Origin ? origin.Origin.GetComponent<CharacterController>() : null;
            if (!selfCharacterController && origin) selfCharacterController = origin.GetComponent<CharacterController>();
            if (!selfCharacterController) return;
            if (palm) selfCollisionStates[palm] = Physics.GetIgnoreCollision(palm, selfCharacterController);
            if (envelope) selfCollisionStates[envelope] = Physics.GetIgnoreCollision(envelope, selfCharacterController);
            MaintainSelfCollisionIsolation();
        }
        void MaintainSelfCollisionIsolation()
        {
            if (!isActiveAndEnabled || !selfCharacterController || !selfCharacterController.enabled ||
                !selfCharacterController.gameObject.activeInHierarchy) return;
            // Obstacle() excludes the tracking rig only from our software sweeps.
            // CharacterController.Move also needs native pair isolation, or its
            // capsule can hit a world-space hand that is still catching up with locomotion.
            // Keep these saved states separate from the temporary held-object pairs.
            foreach (var pair in selfCollisionStates)
                if (pair.Key && pair.Key.enabled && pair.Key.gameObject.activeInHierarchy &&
                    !Physics.GetIgnoreCollision(pair.Key, selfCharacterController))
                    Physics.IgnoreCollision(pair.Key, selfCharacterController, true);
        }
        void RestoreSelfCollisions(bool forget = false)
        {
            if (selfCharacterController) foreach (var pair in selfCollisionStates)
                if (pair.Key) Physics.IgnoreCollision(pair.Key, selfCharacterController, pair.Value);
            if (!forget) return;
            selfCollisionStates.Clear(); selfCharacterController = null;
        }
        void Register()
        {
            if (filterRegistered) return;
            if (directInteractor)
            {
                directInteractor.selectFilters.Add(this);
                directInteractor.selectEntered.AddListener(Selected);
                directInteractor.selectExited.AddListener(Released);
            }
            foreach (var ray in rayInteractors) if (ray) ray.selectFilters.Add(this);
            filterRegistered = true;
        }
        void Unregister()
        {
            if (!filterRegistered) return;
            if (directInteractor)
            {
                directInteractor.selectFilters.Remove(this);
                directInteractor.selectEntered.RemoveListener(Selected);
                directInteractor.selectExited.RemoveListener(Released);
            }
            foreach (var ray in rayInteractors) if (ray) ray.selectFilters.Remove(this);
            filterRegistered = false;
        }
        void FixedUpdate() => StepPhysics(Time.fixedDeltaTime);
        public void StepPhysics(float dt)
        {
            EnsureBody();
            if (!trackingTarget || !body || dt <= 0 || float.IsNaN(dt)) return;
            bool valid = IsTrackingValid;
            if (!valid)
            {
                if (previousTracking || HasOwnedSelection()) ForceRelease();
                palm.enabled = false; envelope.enabled = false;
                SuspendQueries();
                if (visual) visual.gameObject.SetActive(false);
                previousTracking = false; IsBlocked = false; return;
            }
            if (!Finite(trackingTarget.position) || !Finite(trackingTarget.rotation)) return;
            bool relocated = false;
            if (trackingOrigin)
            {
                relocated = originReady && (Vector3.Distance(originPosition, trackingOrigin.position) > .4f || Quaternion.Angle(originRotation, trackingOrigin.rotation) >= 44.9f);
                originPosition = trackingOrigin.position; originRotation = trackingOrigin.rotation; originReady = true;
            }
            if (relocated || !previousTracking)
            {
                ForceRelease();
                // Only a tracking recovery/locomotion jump may reanchor, and only to a clear palm pose.
                if (ClearAt(trackingTarget.position))
                {
                    body.position = trackingTarget.position; body.rotation = trackingTarget.rotation;
                    transform.SetPositionAndRotation(body.position, body.rotation);
                }
            }
            previousTracking = true; palm.enabled = true; envelope.enabled = true;
            MaintainSelfCollisionIsolation();
            ResumeQueries();
            if (visual) visual.gameObject.SetActive(true);
            var state = inputController ? inputController.currentControllerState : null;
            if (state == null || !state.selectInteractionState.active) releaseRequired = false;
            ObserveRayReleases();
            if (held && (TrackingError > releaseDistance || Vector3.Distance(held.transform.TransformPoint(heldGripLocal), trackingTarget.position) > releaseDistance)) ForceRelease();

            Vector3 from = Depenetrate(body.position);
            Vector3 remaining = Vector3.ClampMagnitude(trackingTarget.position - from, maximumSpeed * dt);
            IsBlocked = false;
            for (int slide = 0; slide < 3 && remaining.sqrMagnitude > .0000001f; slide++)
            {
                float length = remaining.magnitude;
                Vector3 direction = remaining / length;
                int count = Physics.BoxCastNonAlloc(from + body.rotation * envelope.center, envelope.size * .5f, direction, hits, body.rotation, length + Skin, obstructionMask, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) { IsBlocked = true; break; }
                float distance = length + Skin; Vector3 normal = Vector3.zero;
                for (int i = 0; i < count; i++)
                    if (Obstacle(hits[i].collider) && hits[i].distance < distance) { distance = hits[i].distance; normal = hits[i].normal; }
                if (normal == Vector3.zero) { from += remaining; remaining = Vector3.zero; break; }
                IsBlocked = true;
                float travel = Mathf.Clamp(distance - Skin, 0, length);
                from += direction * travel;
                remaining = Vector3.ProjectOnPlane(remaining - direction * travel, normal);
            }
            body.MovePosition(from);
            Quaternion startRotation = body.rotation;
            Quaternion desiredRotation = Quaternion.RotateTowards(startRotation, trackingTarget.rotation, 1080f * dt);
            // A clear endpoint can still sweep through a thin obstacle. Check the whole arc;
            // the 2 mm skin covers the glove corner's motion between samples at most 1 degree apart.
            int rotationSteps = Mathf.Max(1, Mathf.CeilToInt(Quaternion.Angle(startRotation, desiredRotation)));
            float lastClear = 0f;
            for (int step = 1; step <= rotationSteps; step++)
            {
                float next = step / (float)rotationSteps;
                if (ClearPose(from, Quaternion.Slerp(startRotation, desiredRotation, next)))
                { lastClear = next; continue; }
                IsBlocked = true;
                float lo = lastClear, hi = next;
                for (int trial = 0; trial < 6; trial++)
                { float t = (lo + hi) * .5f; if (ClearPose(from, Quaternion.Slerp(startRotation, desiredRotation, t))) lo = t; else hi = t; }
                desiredRotation = Quaternion.Slerp(startRotation, desiredRotation, lo);
                break;
            }
            body.MoveRotation(desiredRotation);
            SynchronizeQueries();
        }
        Vector3 Depenetrate(Vector3 position)
        {
            for (int iteration = 0; iteration < 3; iteration++)
            {
                int count = Physics.OverlapBoxNonAlloc(position + body.rotation * envelope.center, envelope.size * .5f, overlaps, body.rotation, obstructionMask, QueryTriggerInteraction.Ignore);
                bool changed = false;
                for (int i = 0; i < count; i++)
                    if (Obstacle(overlaps[i]) && Physics.ComputePenetration(envelope, position, body.rotation, overlaps[i], overlaps[i].transform.position, overlaps[i].transform.rotation, out var direction, out var distance))
                    { position += direction * (distance + Skin); changed = true; }
                if (!changed) break;
            }
            return position;
        }
        bool ClearAt(Vector3 position)
        {
            Quaternion rotation = trackingTarget ? trackingTarget.rotation : body.rotation;
            return ClearPose(position, rotation);
        }
        bool ClearPose(Vector3 position, Quaternion rotation)
        {
            int count = Physics.OverlapBoxNonAlloc(position + rotation * envelope.center, envelope.size * .5f + Vector3.one * Skin, overlaps, rotation, obstructionMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (Obstacle(overlaps[i])) return false;
            return true;
        }
        bool Obstacle(Collider collider)
        {
            if (!collider || !collider.enabled || collider.isTrigger || collider.transform.IsChildOf(transform)) return false;
            if (trackingOrigin && collider.transform.IsChildOf(trackingOrigin)) return false;
            if (held && collider.transform.IsChildOf(held.transform)) return false;
            return true;
        }
        void LateUpdate()
        {
            if (!visual || !previousTracking) return;
            // During a hold, draw the hand at the actual grip, including the object's collision lag.
            if (held && directInteractor && held.IsSelected(directInteractor))
            {
                float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - visualGripStarted) / .14f));
                visual.transform.SetPositionAndRotation(
                    held.transform.TransformPoint(Vector3.Lerp(heldVisualStartLocal, heldVisualLocal, settle)),
                    held.transform.rotation * Quaternion.Slerp(heldVisualStartRotation, heldRotationLocal, settle));
                IsBlocked |= Vector3.Distance(visual.transform.position, trackingTarget.position) > .08f;
            }
            else visual.transform.SetPositionAndRotation(transform.position, transform.rotation);
            var state = inputController ? inputController.currentControllerState : null;
            visual.SetPose(state == null ? 0 : state.selectInteractionState.value, state == null ? 0 : state.activateInteractionState.value, held != null);
        }
        void Selected(SelectEnterEventArgs args)
        {
            RestoreCollisions();
            held = args.interactableObject as XRGrabInteractable;
            if (!held) return;
            heldGripLocal = held.transform.InverseTransformPoint(transform.position);
            heldRotationLocal = Quaternion.Inverse(held.transform.rotation) * transform.rotation;
            heldVisualStartLocal = heldVisualLocal = heldGripLocal;
            heldVisualStartRotation = heldRotationLocal;
            visualGripStarted = Time.unscaledTime; hasSurfaceGrip = false;
            var surface = held.GetComponent<MuseumGripSurface>();
            if (surface && surface.TryGetContact(held, transform.position, out var contact, out var normal))
            {
                Vector3 fingers = Vector3.ProjectOnPlane(transform.forward, normal);
                if (fingers.sqrMagnitude < .0001f) fingers = Vector3.ProjectOnPlane(Vector3.up, normal);
                if (fingers.sqrMagnitude < .0001f) fingers = Vector3.ProjectOnPlane(Vector3.right, normal);
                Quaternion pose = Quaternion.LookRotation(fingers.normalized, normal);
                heldSurfaceLocal = held.transform.InverseTransformPoint(contact);
                heldVisualLocal = held.transform.InverseTransformPoint(contact - pose * MuseumHandGrabTransformer.PalmContactLocal);
                heldRotationLocal = Quaternion.Inverse(held.transform.rotation) * pose;
                hasSurfaceGrip = true;
            }
            foreach (var collider in held.GetComponentsInChildren<Collider>())
            {
                if (!collider || collider.isTrigger || collider == palm) continue;
                ignored[collider] = Physics.GetIgnoreCollision(palm, collider);
                envelopeIgnored[collider] = Physics.GetIgnoreCollision(envelope, collider);
                Physics.IgnoreCollision(palm, collider, true);
                Physics.IgnoreCollision(envelope, collider, true);
            }
        }
        void Released(SelectExitEventArgs args) { held = null; hasSurfaceGrip = false; RestoreCollisions(); }
        void RestoreCollisions()
        {
            if (palm) foreach (var pair in ignored) if (pair.Key) Physics.IgnoreCollision(palm, pair.Key, pair.Value);
            if (envelope) foreach (var pair in envelopeIgnored) if (pair.Key) Physics.IgnoreCollision(envelope, pair.Key, pair.Value);
            ignored.Clear(); envelopeIgnored.Clear();
        }
        void SuspendQueries()
        {
            if (!queriesSuspended)
            {
                directWasEnabled = directInteractor && directInteractor.enabled; pokeWasEnabled = pokeInteractor && pokeInteractor.enabled;
                rayEnabled.Clear();
                foreach (var ray in rayInteractors) if (ray) rayEnabled[ray] = ray.enabled;
                queriesSuspended = true;
            }
            if (directInteractor) directInteractor.enabled = false;
            if (pokeInteractor) pokeInteractor.enabled = false;
            foreach (var ray in rayInteractors) if (ray) ray.enabled = false;
        }
        void ResumeQueries()
        {
            if (!queriesSuspended) return;
            queriesSuspended = false;
            if (directInteractor) directInteractor.enabled = directWasEnabled;
            if (pokeInteractor) pokeInteractor.enabled = pokeWasEnabled;
            foreach (var pair in rayEnabled) if (pair.Key) pair.Key.enabled = pair.Value;
            rayEnabled.Clear();
        }
        bool HasOwnedSelection()
        {
            if (directInteractor && directInteractor.hasSelection) return true;
            foreach (var ray in rayInteractors) if (ray && ray.hasSelection) return true;
            return false;
        }
        void ObserveRayReleases()
        {
            foreach (var ray in rayInteractors) ObserveRayRelease(ray);
        }
        void ObserveRayRelease(XRRayInteractor ray)
        {
            if (!ray || !raysWaitingForRelease.Contains(ray) || !ray.gameObject.activeInHierarchy) return;
            var controller = ray.xrController ? ray.xrController : ray.GetComponentInParent<XRBaseController>(true);
            // An inactive teleport controller retains its old state. Only an active
            // source can demonstrate release; its latch never blocks the other queries.
            if (!controller || !controller.isActiveAndEnabled) return;
            var state = controller.currentControllerState;
            if (state != null && !state.selectInteractionState.active) raysWaitingForRelease.Remove(ray);
        }
        public void ForceRelease()
        {
            releaseRequired = true;
            var selectors = new List<XRBaseControllerInteractor>();
            if (directInteractor) selectors.Add(directInteractor);
            foreach (var ray in rayInteractors) if (ray)
            {
                selectors.Add(ray);
                var controller = ray.xrController ? ray.xrController : ray.GetComponentInParent<XRBaseController>(true);
                var inputState = inputController ? inputController.currentControllerState : null;
                bool sharesHeldInput = controller == inputController && inputController && inputController.isActiveAndEnabled &&
                    inputState != null && inputState.selectInteractionState.active;
                // Do not consume the first future input of an unused inactive ray.
                // A sleeping ordinary Ray still shares a live held Grip; a nested
                // inactive controller's cached state cannot establish that condition.
                // A previously latched ray stays latched even if its GameObject later sleeps.
                if (ray.gameObject.activeInHierarchy || ray.hasSelection || sharesHeldInput) raysWaitingForRelease.Add(ray);
            }
            var throwStates = new Dictionary<XRGrabInteractable, bool>();
            foreach (var selector in selectors)
            foreach (var item in selector.interactablesSelected)
            {
                if (item is XRGrabInteractable grab && !throwStates.ContainsKey(grab))
                { throwStates[grab] = grab.throwOnDetach; grab.throwOnDetach = false; }
            }
            try
            {
                // A safety cancellation must not become a short-click on a window or a fling.
                foreach (var selector in selectors) if (selector.interactionManager)
                    selector.interactionManager.CancelInteractorSelection((IXRSelectInteractor)selector);
                foreach (var pair in throwStates) if (pair.Key && !pair.Key.isSelected)
                {
                    pair.Key.ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase.Late);
                    var rb = pair.Key.GetComponent<Rigidbody>();
                    if (rb && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                }
            }
            finally { foreach (var pair in throwStates) if (pair.Key) pair.Key.throwOnDetach = pair.Value; }
            held = null; RestoreCollisions();
        }
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (!IsTrackingValid) return false;
            // Rays retain their authored distance/target rules; only physical-hand safety is shared.
            if (!object.ReferenceEquals(interactor, directInteractor))
            {
                var ray = interactor as XRRayInteractor;
                ObserveRayRelease(ray);
                return !ray || !raysWaitingForRelease.Contains(ray);
            }
            if (releaseRequired) return false;
            foreach (var selector in interactable.interactorsSelecting) if (selector == interactor) return true;
            // A trigger volume may overlap a thin wall; reject candidates hidden behind solid geometry.
            var candidate = interactable as XRBaseInteractable;
            if (!candidate) return true;
            if (PreferHeldExhibit(candidate, interactor)) return false;
            TryNearestPoint(candidate, transform.position, out var nearest);
            return WithinOpenReach(nearest, candidate is XRGrabInteractable ? .06f : .025f) && HasLineOfSight(candidate, nearest);
        }
        public bool IsDirectContact(XRBaseInteractable candidate)
        {
            return candidate && IsTrackingValid && TryNearestPoint(candidate, transform.position, out var nearest) &&
                WithinOpenReach(nearest) && HasLineOfSight(candidate, nearest);
        }
        bool PreferHeldExhibit(XRBaseInteractable candidate, IXRSelectInteractor interactor)
        {
            if (!object.ReferenceEquals(interactor, directInteractor) ||
                (!(candidate is Museum.Exploration.HelmetExplorationHotspot) && !candidate.GetComponent<Museum.Hotspots.HelmetHotspot>())) return false;
            var exhibit = candidate.GetComponentInParent<XRGrabInteractable>();
            if (!exhibit || !exhibit.isActiveAndEnabled || !exhibit.isSelected || !exhibit.IsSelectableBy(interactor)) return false;
            bool anotherHand = false;
            foreach (var selector in exhibit.interactorsSelecting)
            {
                if (selector == interactor) continue;
                var selectorController = (selector as XRBaseControllerInteractor)?.xrController;
                if (!selectorController) selectorController = selector.transform.GetComponentInParent<XRBaseController>();
                if (!inputController || selectorController != inputController) { anotherHand = true; break; }
            }
            if (!anotherHand) return false;
            var querySphere = directInteractor.GetComponent<SphereCollider>();
            if (!querySphere || !querySphere.isTrigger || exhibit.colliders.Count == 0) return false;
            Vector3 queryCenter = querySphere.transform.TransformPoint(querySphere.center);
            float queryRadius = querySphere.radius * Mathf.Abs(querySphere.transform.lossyScale.x);
            if (!TryNearestPoint(exhibit, queryCenter, out var rootNearest, directInteractor.physicsLayerMask)) return false;
            // A support hand touching an already held exhibit means "take hold", not "open a clue".
            // Only nearby Direct handovers win; unheld/farther clues and Window/Section/Ray keep their rules.
            return (rootNearest - queryCenter).sqrMagnitude <= queryRadius * queryRadius &&
                WithinOpenReach(rootNearest) && HasLineOfSight(exhibit, rootNearest);
        }
        static bool TryNearestPoint(XRBaseInteractable candidate, Vector3 origin, out Vector3 nearest, int layerMask = ~0)
        {
            nearest = candidate.transform.position; float best = float.MaxValue;
            foreach (var collider in candidate.colliders)
            {
                if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                    (layerMask & (1 << collider.gameObject.layer)) == 0) continue;
                Vector3 point = collider.ClosestPoint(origin);
                float distance = (point - origin).sqrMagnitude;
                if (distance < best) { nearest = point; best = distance; }
            }
            return best < float.MaxValue;
        }
        bool WithinOpenReach(Vector3 nearest, float reach = .025f)
        {
            // Grip shrinks the physical glove, but must not remove the chance to grab a surface
            // just reached with an open hand. Selection reach therefore uses the fixed open box.
            Vector3 localReach = Quaternion.Inverse(body.rotation) * (nearest - body.position) - OpenCenter;
            Vector3 halfReach = OpenSize * .5f;
            Vector3 outsideReach = new Vector3(Mathf.Max(Mathf.Abs(localReach.x) - halfReach.x, 0f),
                Mathf.Max(Mathf.Abs(localReach.y) - halfReach.y, 0f), Mathf.Max(Mathf.Abs(localReach.z) - halfReach.z, 0f));
            return outsideReach.sqrMagnitude <= reach * reach;
        }
        bool HasLineOfSight(XRBaseInteractable candidate, Vector3 nearest)
        {
            Vector3 delta = nearest - transform.position;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = Physics.RaycastNonAlloc(transform.position, delta.normalized, hits, delta.magnitude, obstructionMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            var assemblyBody = candidate.GetComponentInParent<Rigidbody>();
            for (int i = 0; i < count; i++)
                if (Obstacle(hits[i].collider) && !candidate.colliders.Contains(hits[i].collider) &&
                    !hits[i].collider.transform.IsChildOf(candidate.transform) &&
                    (!assemblyBody || hits[i].collider.attachedRigidbody != assemblyBody)) return false;
            return true;
        }
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w) && Quaternion.Dot(q, q) > .000001f;
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
