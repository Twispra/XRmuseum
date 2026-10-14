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
        Rigidbody body;
        SphereCollider palm;
        BoxCollider envelope;
        XRGrabInteractable held;
        Vector3 heldGripLocal;
        Quaternion heldRotationLocal;
        Vector3 originPosition;
        Quaternion originRotation;
        bool originReady, filterRegistered, releaseRequired, previousTracking;
        bool queriesSuspended, directWasEnabled, pokeWasEnabled;
        bool? trackingOverride;
        const float Skin = .002f;

        public Transform TrackingTarget => trackingTarget;
        public XRBaseController InputController => inputController;
        public XRDirectInteractor DirectInteractor => directInteractor;
        public Rigidbody Body { get { EnsureBody(); return body; } }
        public SphereCollider PalmCollider { get { EnsureBody(); return palm; } }
        public Collider CollisionCollider { get { EnsureBody(); return envelope; } }
        public float CollisionRadius => collisionRadius;
        public bool IsBlocked { get; private set; }
        public float TrackingError => trackingTarget ? Vector3.Distance(trackingTarget.position, body ? body.position : transform.position) : 0f;
        public bool IsTrackingValid => trackingOverride ?? (trackingTarget && (!inputController ||
            (inputController.isActiveAndEnabled && inputController.currentControllerState != null &&
             inputController.currentControllerState.isTracked &&
             (inputController.currentControllerState.inputTrackingState & (InputTrackingState.Position | InputTrackingState.Rotation)) ==
             (InputTrackingState.Position | InputTrackingState.Rotation))));
        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        void Awake() => EnsureBody();
        void OnEnable() { EnsureBody(); Register(); Application.onBeforeRender += BeforeRender; }
        void OnDisable()
        {
            ForceRelease();
            Unregister();
            Application.onBeforeRender -= BeforeRender;
            ResumeQueries(); previousTracking = false; originReady = false;
            if (palm) palm.enabled = false;
            if (envelope) envelope.enabled = false;
            if (visual) visual.gameObject.SetActive(false);
        }
        void OnDestroy() => RestoreCollisions();

        public void Configure(Transform target, XRBaseController controller, XRDirectInteractor direct, MuseumHandVisual handVisual, bool left)
        {
            Unregister();
            trackingTarget = target; inputController = controller; directInteractor = direct; visual = handVisual; this.left = left;
            var origin = controller ? controller.GetComponentInParent<XROrigin>() : null;
            trackingOrigin = origin ? origin.transform : null;
            EnsureBody();
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
        void Update() => SynchronizeQueries();
        [BeforeRenderOrder(99)]
        void BeforeRender() => SynchronizeQueries();
        public void SynchronizeQueries()
        {
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
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            palm.radius = collisionRadius; palm.center = Vector3.zero; palm.isTrigger = false;
            float grip = inputController && inputController.currentControllerState != null ? inputController.currentControllerState.selectInteractionState.value : 0;
            if (held) grip = Mathf.Max(grip, .72f);
            float side = left ? 1f : -1f;
            envelope.size = Vector3.Lerp(new Vector3(.15f,.09f,.20f), new Vector3(.12f,.105f,.12f), Mathf.Clamp01(grip));
            envelope.center = Vector3.Lerp(new Vector3(side*.018f,-.01f,.026f), new Vector3(side*.012f,-.025f,-.006f), Mathf.Clamp01(grip));
            envelope.isTrigger = false;
        }
        void Register()
        {
            if (!directInteractor || filterRegistered) return;
            directInteractor.selectFilters.Add(this);
            directInteractor.selectEntered.AddListener(Selected);
            directInteractor.selectExited.AddListener(Released);
            filterRegistered = true;
        }
        void Unregister()
        {
            if (!directInteractor || !filterRegistered) return;
            directInteractor.selectFilters.Remove(this);
            directInteractor.selectEntered.RemoveListener(Selected);
            directInteractor.selectExited.RemoveListener(Released);
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
                if (previousTracking || (directInteractor && directInteractor.hasSelection)) ForceRelease();
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
            ResumeQueries();
            if (visual) visual.gameObject.SetActive(true);
            var state = inputController ? inputController.currentControllerState : null;
            if (state == null || !state.selectInteractionState.active) releaseRequired = false;
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
                visual.transform.SetPositionAndRotation(held.transform.TransformPoint(heldGripLocal), held.transform.rotation * heldRotationLocal);
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
            foreach (var collider in held.GetComponentsInChildren<Collider>())
            {
                if (!collider || collider.isTrigger || collider == palm) continue;
                ignored[collider] = Physics.GetIgnoreCollision(palm, collider);
                envelopeIgnored[collider] = Physics.GetIgnoreCollision(envelope, collider);
                Physics.IgnoreCollision(palm, collider, true);
                Physics.IgnoreCollision(envelope, collider, true);
            }
        }
        void Released(SelectExitEventArgs args) { held = null; RestoreCollisions(); }
        void RestoreCollisions()
        {
            if (palm) foreach (var pair in ignored) if (pair.Key) Physics.IgnoreCollision(palm, pair.Key, pair.Value);
            if (envelope) foreach (var pair in envelopeIgnored) if (pair.Key) Physics.IgnoreCollision(envelope, pair.Key, pair.Value);
            ignored.Clear(); envelopeIgnored.Clear();
        }
        void SuspendQueries()
        {
            if (!queriesSuspended)
            { directWasEnabled = directInteractor && directInteractor.enabled; pokeWasEnabled = pokeInteractor && pokeInteractor.enabled; queriesSuspended = true; }
            if (directInteractor) directInteractor.enabled = false;
            if (pokeInteractor) pokeInteractor.enabled = false;
        }
        void ResumeQueries()
        {
            if (!queriesSuspended) return;
            queriesSuspended = false;
            if (directInteractor) directInteractor.enabled = directWasEnabled;
            if (pokeInteractor) pokeInteractor.enabled = pokeWasEnabled;
        }
        public void ForceRelease()
        {
            releaseRequired = true;
            if (directInteractor && directInteractor.interactionManager)
            {
                var selected = directInteractor.interactablesSelected.ToArray();
                var throwStates = new Dictionary<XRGrabInteractable, bool>();
                foreach (var item in selected) if (item is XRGrabInteractable grab) { throwStates[grab] = grab.throwOnDetach; grab.throwOnDetach = false; }
                try
                {
                    // A safety cancellation must not become a short-click on a window or a fling.
                    directInteractor.interactionManager.CancelInteractorSelection((IXRSelectInteractor)directInteractor);
                    foreach (var pair in throwStates) if (pair.Key && !pair.Key.isSelected)
                    {
                        pair.Key.ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase.Late);
                        var rb = pair.Key.GetComponent<Rigidbody>();
                        if (rb && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    }
                }
                finally { foreach (var pair in throwStates) if (pair.Key) pair.Key.throwOnDetach = pair.Value; }
            }
            held = null; RestoreCollisions();
        }
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (!IsTrackingValid || releaseRequired) return false;
            foreach (var selector in interactable.interactorsSelecting) if (selector == interactor) return true;
            // A trigger volume may overlap a thin wall; reject candidates hidden behind solid geometry.
            var candidate = interactable as XRBaseInteractable;
            if (!candidate) return true;
            Vector3 nearest = candidate.transform.position; float best = float.MaxValue;
            foreach (var collider in candidate.colliders)
            {
                if (!collider || !collider.enabled) continue;
                Vector3 point = collider.ClosestPoint(transform.position);
                float distance = (point - transform.position).sqrMagnitude;
                if (distance < best) { nearest = point; best = distance; }
            }
            if (Vector3.Distance(envelope.ClosestPoint(nearest), nearest) > .025f) return false;
            Vector3 delta = nearest - transform.position;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = Physics.RaycastNonAlloc(transform.position, delta.normalized, hits, delta.magnitude, obstructionMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            var assemblyBody = candidate.GetComponentInParent<Rigidbody>();
            for (int i = 0; i < count; i++)
                if (Obstacle(hits[i].collider) && !hits[i].collider.transform.IsChildOf(candidate.transform) &&
                    (!assemblyBody || hits[i].collider.attachedRigidbody != assemblyBody)) return false;
            return true;
        }
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w) && Quaternion.Dot(q, q) > .000001f;
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
