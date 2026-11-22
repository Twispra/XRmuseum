using System;
using System.Collections.Generic;
using System.Linq;
using Museum.Diegetic;
using Museum.Exploration;
using Museum.Inspection;
using Museum.Localization;
using Museum.PhysicalHands;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Navigation
{
    /// <summary>Three authored viewing places, with no changes to tracked local poses or exhibit progress.</summary>
    [DefaultExecutionOrder(350)]
    [DisallowMultipleComponent]
    public sealed class GalleryNavigator : MonoBehaviour
    {
        public enum Exhibit { Left, Main, Right }
        const float FadeSeconds = .16f;
        static readonly string[] ExhibitPrefixes = { "Exhibit 01 - ", "Exhibit 02 - ", "Exhibit 03 - " };
        static readonly string[] ButtonKeys = { "navigation.left", "navigation.main", "navigation.right" };
        static readonly float[] ViewingDistances = { .95f, .75f, 1.45f };
        readonly Dictionary<LocomotionProvider, bool> locomotionStates = new Dictionary<LocomotionProvider, bool>();
        readonly List<GalleryNavigationButton> buttons = new List<GalleryNavigationButton>();
        XROrigin origin;
        HelmetInspectionController inspection;
        HelmetDiegeticStation station;
        Transform exhibits;
        HelmetInspectionPalette palette;
        GameObject panel, fade;
        Material fadeMaterial;
        LocalizedText status;
        float floorY, phaseTime, noticeUntil;
        bool relocated, ready;
        Exhibit requested;

        public bool IsReady => ready && origin && origin.Camera && isActiveAndEnabled;
        public bool IsTransitioning { get; private set; }
        public float FadeOpacity { get; private set; }
        public int CompletedNavigations { get; private set; }
        public Exhibit? CurrentExhibit { get; private set; }
        public IReadOnlyList<GalleryNavigationButton> Buttons => buttons;
        public HelmetInspectionPalette Palette => palette;
        public XROrigin Origin => origin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSceneSubscription() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInScene(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode _) => EnsureInScene(scene);
        static void EnsureInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.GetComponentInChildren<GalleryNavigator>(true))) return;
            var gallery = roots.SingleOrDefault(r => r.name == "Exhibit Placeholders");
            if (!gallery || Enumerable.Range(0, 3).Any(i => !FindExhibit(gallery.transform, i))) return;
            var target = new GameObject("Gallery quick navigation");
            SceneManager.MoveGameObjectToScene(target, scene);
            target.AddComponent<GalleryNavigator>();
        }

        void OnEnable()
        {
            VisitorViewRecovery.ViewResetRequested += OnViewReset;
            if (ready && panel) panel.SetActive(true);
        }

        void Start() => Initialize();
        void Initialize()
        {
            // Station.Start retires the old personal palette before this later Start creates a new, independent one.
            var roots = gameObject.scene.GetRootGameObjects();
            origin = roots.SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).FirstOrDefault();
            inspection = roots.SelectMany(r => r.GetComponentsInChildren<HelmetInspectionController>(true)).FirstOrDefault();
            station = roots.SelectMany(r => r.GetComponentsInChildren<HelmetDiegeticStation>(true)).FirstOrDefault();
            exhibits = roots.Single(r => r.name == "Exhibit Placeholders").transform;
            if (!origin || !origin.Camera || !inspection || !MuseumText.Table || !MuseumText.Channel) return;
            floorY = BaseTransform(origin).position.y;
            var source = roots.SelectMany(r => r.GetComponentsInChildren<HelmetInspectionPalette>(true)).FirstOrDefault();
            var template = source ? source.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Exploration HELP") : null;
            var back = source ? source.transform.Find("Palette back") : null;
            if (!template || !back) return;
            var shader = Resources.Load<Shader>("MuseumNavigationFade");
            if (!shader) { Debug.LogError("Missing MuseumNavigationFade shader."); return; }

            panel = new GameObject("Exhibit navigation panel");
            panel.transform.SetParent(transform, false);
            Plate(panel.transform, "Palette back", Vector3.zero, new Vector3(.48f, .22f, .012f),
                back.GetComponent<Renderer>().sharedMaterial, new Color(.025f, .045f, .055f), false);
            Label("Navigation heading", "navigation.title", new Vector3(0, .078f, -.018f), new Vector2(.44f, .04f), .32f);
            status = Label("Navigation status", "navigation.hint", new Vector3(0, -.078f, -.018f), new Vector2(.44f, .04f), .25f);
            for (int i = 0; i < 3; i++)
            {
                var button = Plate(panel.transform, "Navigate " + (Exhibit)i, new Vector3((i - 1) * .153f, 0, -.015f),
                    new Vector3(.143f, .085f, .012f), template.sharedMaterial, new Color(.13f, .30f, .34f), true);
                var interaction = button.gameObject.AddComponent<XRSimpleInteractable>();
                interaction.interactionLayers = template.GetComponent<XRSimpleInteractable>().interactionLayers;
                interaction.selectMode = InteractableSelectMode.Single;
                var control = button.gameObject.AddComponent<GalleryNavigationButton>();
                control.Configure(this, (Exhibit)i);
                buttons.Add(control);
                Label("Navigate " + (Exhibit)i + " label", ButtonKeys[i], new Vector3((i - 1) * .153f, 0, -.026f),
                    new Vector2(.132f, .079f), .29f);
            }
            palette = panel.AddComponent<HelmetInspectionPalette>();
            palette.Configure(inspection, origin.Camera.transform, null, null);
            // The Mark V telephone and its calling key occupy the visitor's lower-right sightline.
            bool hasSurfaceCall = roots.Any(root => root.GetComponentInChildren<Museum.SurfaceLine.SurfaceCall>(true));
            palette.ConfigureViewerOffset(hasSurfaceCall ? new Vector3(-.45f, -.27f, .88f) : new Vector3(.35f, -.23f, .88f));
            palette.ConfigureContentBounds(-.11f, .11f);
            palette.RecenterNow();

            fadeMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 5000 };
            fade = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fade.name = "Navigation eye fade";
            fade.layer = 2; // Ignore Raycast, never an interaction surface.
            fade.transform.SetParent(origin.Camera.transform, false);
            fade.transform.localPosition = new Vector3(0, 0, Mathf.Max(.05f, origin.Camera.nearClipPlane + .02f));
            fade.transform.localScale = new Vector3(10, 10, 1);
            Destroy(fade.GetComponent<Collider>());
            var renderer = fade.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = fadeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            SetFade(0);
            ready = true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            gameObject.AddComponent<DesktopExhibitVisit>();
#endif
        }

        public bool TryGetDestination(Exhibit exhibit, out Vector3 floor, out Vector3 forward)
        {
            floor = forward = Vector3.zero;
            int index = (int)exhibit;
            if (!exhibits || index < 0 || index >= 3) return false;
            var root = FindExhibit(exhibits, index);
            var plinth = root ? root.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Plinth") : null;
            if (!plinth) return false;
            forward = Vector3.ProjectOnPlane(exhibits.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) return false;
            var bounds = plinth.bounds;
            var absoluteForward = new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z));
            floor = bounds.center - forward * (Vector3.Dot(bounds.extents, absoluteForward) + ViewingDistances[index]);
            floor.y = floorY;
            return Finite(floor);
        }

        public bool Request(Exhibit exhibit)
        {
            if (!IsReady || IsTransitioning || !TryGetDestination(exhibit, out var floor, out _)) return false;
            if (HasBlockingInteraction()) return Notice("navigation.finish_action");
            if (!DestinationClear(floor)) return Notice("navigation.blocked");
            requested = exhibit;
            IsTransitioning = true;
            relocated = false;
            phaseTime = 0;
            foreach (var provider in origin.GetComponentsInChildren<LocomotionProvider>(true))
            { locomotionStates[provider] = provider.enabled; provider.enabled = false; }
            if (station && station.isActiveAndEnabled) station.PrepareForGalleryNavigation();
            else
            {
                var clues = gameObject.scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<HelmetExplorationController>(true)).FirstOrDefault();
                if (clues) clues.Stop();
            }
            status.SetKey("navigation.moving");
            return true;
        }

        bool Notice(string key)
        { status.SetKey(key); noticeUntil = Time.unscaledTime + 2.5f; return false; }

        bool HasBlockingInteraction() =>
            !inspection || inspection.IsHeld || inspection.IsWindowHeld || inspection.IsSectionHeld || inspection.IsReturning ||
            (station && (station.IsVisitHandoffOpen || station.HasActiveGesture));

        void Update()
        {
            if (!ready) { if (origin && origin.Camera) Initialize(); return; }
            if (!IsTransitioning)
            {
                if (noticeUntil > 0 && Time.unscaledTime >= noticeUntil)
                { noticeUntil = 0; status.SetKey("navigation.hint"); }
                return;
            }
            phaseTime += Time.unscaledDeltaTime;
            if (!relocated)
            {
                SetFade(Mathf.Clamp01(phaseTime / FadeSeconds));
                if (phaseTime < FadeSeconds) return;
                if (HasBlockingInteraction())
                { CancelTransition(); Notice("navigation.finish_action"); return; }
                if (!TryGetDestination(requested, out var floor, out var forward) || !DestinationClear(floor))
                { CancelTransition(); Notice("navigation.blocked"); return; }
                if (!PlaceTrackedView(origin, floor, forward)) { CancelTransition(); return; }
                CurrentExhibit = requested;
                CompletedNavigations++;
                palette.RecenterNow();
                Physics.SyncTransforms();
                relocated = true;
                phaseTime = 0;
                return; // Keep a fully covered frame at the new position before revealing it.
            }
            SetFade(1 - Mathf.Clamp01(phaseTime / FadeSeconds));
            if (phaseTime >= FadeSeconds) CancelTransition();
        }

        public static bool PlaceTrackedView(XROrigin target, Vector3 floor, Vector3 forward)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (!target || !target.Camera || !Finite(floor) || !Finite(forward) || forward.sqrMagnitude < .01f) return false;
            if (!target.MatchOriginUpCameraForward(Vector3.up, forward.normalized)) return false;
            var baseTransform = BaseTransform(target);
            var horizontalOffset = Vector3.ProjectOnPlane(target.Camera.transform.position - baseTransform.position, Vector3.up);
            baseTransform.position = floor - horizontalOffset;
            return true;
        }

        bool DestinationClear(Vector3 floor)
        {
            const float radius = .22f;
            float height = Mathf.Max(1.1f, origin.Camera.transform.position.y - BaseTransform(origin).position.y);
            foreach (var hit in Physics.OverlapCapsule(floor + Vector3.up * (radius + .04f),
                floor + Vector3.up * Mathf.Max(radius + .04f, height - radius), radius, ~0, QueryTriggerInteraction.Ignore))
                if (!IsVisitorCollider(hit)) return false;
            return Physics.RaycastAll(floor + Vector3.up * .25f, Vector3.down, .5f, ~0, QueryTriggerInteraction.Ignore)
                .Any(h => !IsVisitorCollider(h.collider) &&
                    h.normal.y > .75f && Mathf.Abs(h.point.y - floor.y) < .12f);
        }

        bool IsVisitorCollider(Collider collider)
        {
            if (collider.transform.IsChildOf(origin.transform) || collider.transform.IsChildOf(transform)) return true;
            // The physical hands may have their own scene root outside the tracked rig hierarchy.
            var hand = collider.GetComponentInParent<MuseumPhysicalHand>();
            return hand && hand.InputController && hand.InputController.transform.IsChildOf(origin.transform);
        }

        void OnViewReset(XROrigin target)
        {
            if (target != origin) return;
            CancelTransition();
            CurrentExhibit = null;
        }

        public void CancelTransition()
        {
            IsTransitioning = false;
            phaseTime = 0;
            SetFade(0);
            foreach (var entry in locomotionStates) if (entry.Key) entry.Key.enabled = entry.Value;
            locomotionStates.Clear();
            if (status) status.SetKey("navigation.hint");
        }

        void SetFade(float opacity)
        {
            FadeOpacity = opacity;
            if (fadeMaterial) fadeMaterial.SetFloat("_Opacity", opacity);
            if (fade) fade.GetComponent<Renderer>().enabled = opacity > 0;
        }

        void OnDisable()
        {
            VisitorViewRecovery.ViewResetRequested -= OnViewReset;
            CancelTransition();
            if (panel) panel.SetActive(false);
        }
        void OnDestroy()
        {
            if (fade) Destroy(fade);
            if (fadeMaterial) Destroy(fadeMaterial);
        }

        LocalizedText Label(string name, string key, Vector3 position, Vector2 size, float fontSize)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(panel.transform, false);
            obj.transform.localPosition = position;
            var label = obj.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = size;
            label.font = MuseumText.Table.FontAsset;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.enableWordWrapping = true;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Overflow;
            obj.SetActive(false);
            var localized = obj.AddComponent<LocalizedText>();
            localized.Configure(MuseumText.Table, MuseumText.Channel, key);
            obj.SetActive(true);
            return localized;
        }

        static Transform Plate(Transform parent, string name, Vector3 position, Vector3 size, Material material, Color color, bool collider)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", color); renderer.SetPropertyBlock(block);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (!collider) { obj.GetComponent<Collider>().enabled = false; Destroy(obj.GetComponent<Collider>()); }
            return obj.transform;
        }

        static Transform FindExhibit(Transform gallery, int index) => gallery.Cast<Transform>()
            .FirstOrDefault(t => t.name.StartsWith(ExhibitPrefixes[index], StringComparison.Ordinal));

        static Transform BaseTransform(XROrigin target) => target.Origin ? target.Origin.transform : target.transform;
        static bool Finite(Vector3 value) => !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
            float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
    }
}
