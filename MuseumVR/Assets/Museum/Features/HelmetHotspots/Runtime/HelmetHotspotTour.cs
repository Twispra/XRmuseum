using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Hotspots
{
    [DisallowMultipleComponent]
    public sealed class HelmetHotspotTour : MonoBehaviour
    {
        [SerializeField, Min(1)] int requiredHotspots = 3;
        [SerializeField] TMP_Text informationText;
        [SerializeField] TMP_Text progressText;
        [SerializeField] GameObject completionFeedback;
        [SerializeField] bool enableTriggerQueries = true;
        [SerializeField, Min(0f)] float explanationHoldSeconds = 30f;
        [SerializeField, Min(.01f)] float explanationFadeSeconds = 1f;

        readonly HashSet<string> visited = new HashSet<string>();
        readonly Dictionary<XRRayInteractor, QueryTriggerInteraction> rayQuerySettings = new Dictionary<XRRayInteractor, QueryTriggerInteraction>();
        readonly Dictionary<XRRayInteractor, bool> rayHoverActivationSettings = new Dictionary<XRRayInteractor, bool>();
        readonly Dictionary<XRDirectInteractor, QueryTriggerInteraction> directQuerySettings = new Dictionary<XRDirectInteractor, QueryTriggerInteraction>();

        public event Action Completed;
        public event Action ResetPerformed;
        // Optional adapters can translate the original English without coupling this
        // standalone assembly to the museum's localization implementation.
        public event Action<TMP_Text, string> TextChanged;
        const string Prompt = "Select a highlighted point to inspect the helmet.";
        string informationEnglish = Prompt;
        string selectedIdentifier;
        TMP_Text[] explanationLabels = Array.Empty<TMP_Text>();
        Color[] explanationLabelColors = Array.Empty<Color>();
        Renderer[] explanationRenderers = Array.Empty<Renderer>();
        Color[] explanationRendererColors = Array.Empty<Color>();
        XRGrabInteractable helmetGrab;
        Transform explanationHelmet;
        Vector3 explanationHelmetPosition;
        Quaternion explanationHelmetRotation;
        // Ignore sub-millimetre pose noise, but compare against the selection pose
        // rather than the previous frame so slow continuous movement still hides.
        const float MovementDistance = .002f;
        const float MovementAngle = .5f;
        bool explanationVisualsCached;
        float explanationElapsed;
        bool explanationCountdownActive;
        public int VisitedCount => visited.Count;
        public int RequiredHotspots => requiredHotspots;
        public bool IsComplete => VisitedCount >= requiredHotspots;
        public bool HasVisited(string identifier) => identifier != null && visited.Contains(identifier);
        public float ExplanationOpacity { get; private set; } = 1f;
        public bool HotspotsVisible { get; private set; }

        public void Configure(int required, TMP_Text information, TMP_Text progress, GameObject completion)
        {
            requiredHotspots = Mathf.Max(1, required);
            informationText = information;
            progressText = progress;
            completionFeedback = completion;
            explanationVisualsCached = false;
            CacheExplanationVisuals();
            SetExplanationOpacity(1f);
            RefreshText();
        }

        public bool Visit(string identifier, string title, string description)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            bool wasComplete = IsComplete;
            bool added = visited.Add(identifier);

            selectedIdentifier = identifier;
            informationEnglish = string.IsNullOrWhiteSpace(title) ? description : title + "\n" + description;
            RestartExplanationLifetime();
            RefreshText();

            if (!wasComplete && IsComplete)
                Completed?.Invoke();

            return added;
        }

        public void RefreshSelectedContent(string identifier, string title, string description)
        {
            if (string.IsNullOrEmpty(identifier) || selectedIdentifier != identifier)
                return;
            informationEnglish = string.IsNullOrWhiteSpace(title) ? description : title + "\n" + description;
            // Do not add a visit, restart the explanation timer, or change visibility.
            RefreshText();
        }

        public void ResetTour()
        {
            visited.Clear();
            selectedIdentifier = null;
            informationEnglish = Prompt;
            explanationCountdownActive = false;
            explanationElapsed = 0f;
            explanationHelmet = null;
            SetExplanationOpacity(1f);
            RefreshText();
            ResetPerformed?.Invoke();
        }

        void OnEnable()
        {
            BindHelmetGrab();
            if (enableTriggerQueries)
                EnableCompatibleInteractors();
            CacheExplanationVisuals();
            SetExplanationOpacity(ExplanationOpacity);
            SetHotspotsVisible(false);
            RefreshText();
        }

        void Update()
        {
            AdvanceExplanation(Time.unscaledDeltaTime);
            UpdateHotspotVisibilityFromRays();
        }

        void LateUpdate()
        {
            if (!explanationCountdownActive || explanationHelmet == null)
                return;
            // Turn/Tilt, physics and the smooth return can move the whole helmet
            // without a grab event. Sample after their Update work, before rendering.
            if ((explanationHelmet.position - explanationHelmetPosition).sqrMagnitude > MovementDistance * MovementDistance ||
                Quaternion.Angle(explanationHelmet.rotation, explanationHelmetRotation) > MovementAngle)
                HideExplanation();
        }

        void OnDisable()
        {
            UnbindHelmetGrab();
            RestoreInteractorQueries();
        }

        void BindHelmetGrab()
        {
            UnbindHelmetGrab();
            helmetGrab = GetComponentInParent<XRGrabInteractable>();
            if (helmetGrab != null)
                helmetGrab.selectEntered.AddListener(OnHelmetGrabbed);
        }

        void UnbindHelmetGrab()
        {
            if (helmetGrab != null)
                helmetGrab.selectEntered.RemoveListener(OnHelmetGrabbed);
            helmetGrab = null;
        }

        void OnHelmetGrabbed(SelectEnterEventArgs args)
        {
            if (selectedIdentifier == null)
                return;
            HideExplanation();
        }

        void HideExplanation()
        {
            explanationCountdownActive = false;
            explanationElapsed = 0f;
            SetExplanationOpacity(0f);
        }

        public void RestoreInteractorQueries()
        {
            foreach (var entry in rayQuerySettings)
                if (entry.Key != null && entry.Key.raycastTriggerInteraction == QueryTriggerInteraction.Collide)
                    entry.Key.raycastTriggerInteraction = entry.Value;
            foreach (var entry in rayHoverActivationSettings)
                if (entry.Key != null && entry.Key.allowHoveredActivate)
                    entry.Key.allowHoveredActivate = entry.Value;
            foreach (var entry in directQuerySettings)
                if (entry.Key != null && entry.Key.physicsTriggerInteraction == QueryTriggerInteraction.Collide)
                    entry.Key.physicsTriggerInteraction = entry.Value;
            rayQuerySettings.Clear();
            rayHoverActivationSettings.Clear();
            directQuerySettings.Clear();
        }

        public void EnableCompatibleInteractors()
        {
            foreach (var interactor in FindObjectsOfType<XRRayInteractor>(true))
            {
                if (!rayQuerySettings.ContainsKey(interactor))
                    rayQuerySettings[interactor] = interactor.raycastTriggerInteraction;
                if (!rayHoverActivationSettings.ContainsKey(interactor))
                    rayHoverActivationSettings[interactor] = interactor.allowHoveredActivate;
                interactor.raycastTriggerInteraction = QueryTriggerInteraction.Collide;
                interactor.allowHoveredActivate = true;
            }

            foreach (var interactor in FindObjectsOfType<XRDirectInteractor>(true))
            {
                if (!directQuerySettings.ContainsKey(interactor))
                    directQuerySettings[interactor] = interactor.physicsTriggerInteraction;
                interactor.physicsTriggerInteraction = QueryTriggerInteraction.Collide;
            }
        }

        public void AdvanceExplanation(float deltaSeconds)
        {
            if (!explanationCountdownActive || deltaSeconds <= 0f)
                return;

            explanationElapsed += deltaSeconds;
            float fadeProgress = Mathf.Max(0f, explanationElapsed - explanationHoldSeconds) / Mathf.Max(.01f, explanationFadeSeconds);
            SetExplanationOpacity(1f - Mathf.Clamp01(fadeProgress));
            if (ExplanationOpacity <= 0f)
                explanationCountdownActive = false;
        }

        public void SetHotspotsVisible(bool visible)
        {
            HotspotsVisible = visible;
            foreach (var hotspot in GetComponentsInChildren<HelmetHotspot>(true))
                hotspot.SetMarkerVisible(visible);
        }

        public bool IsHelmetTarget(Transform target)
        {
            if (target == null)
                return false;

            var hotspot = target.GetComponentInParent<HelmetHotspot>();
            if (hotspot != null && hotspot.transform.IsChildOf(transform))
                return true;

            var helmet = transform.parent;
            return helmet != null && (target == helmet || target.IsChildOf(helmet)) && !target.IsChildOf(transform);
        }

        void UpdateHotspotVisibilityFromRays()
        {
            bool visible = false;
            foreach (var ray in rayQuerySettings.Keys)
            {
                if (ray == null || !ray.isActiveAndEnabled || !ray.TryGetCurrent3DRaycastHit(out var hit) || !hit.collider)
                    continue;
                if (!IsHelmetTarget(hit.collider.transform))
                    continue;
                visible = true;
                break;
            }
            if (visible != HotspotsVisible)
                SetHotspotsVisible(visible);
        }

        void RestartExplanationLifetime()
        {
            var grab = GetComponentInParent<XRGrabInteractable>();
            explanationHelmet = grab != null ? grab.transform : null;
            if (explanationHelmet != null)
            {
                explanationHelmetPosition = explanationHelmet.position;
                explanationHelmetRotation = explanationHelmet.rotation;
            }
            explanationElapsed = 0f;
            explanationCountdownActive = true;
            SetExplanationOpacity(1f);
        }

        void CacheExplanationVisuals()
        {
            if (explanationVisualsCached)
                return;

            if (informationText == null || informationText.transform.parent == null)
            {
                explanationLabels = Array.Empty<TMP_Text>();
                explanationLabelColors = Array.Empty<Color>();
                explanationRenderers = Array.Empty<Renderer>();
                explanationRendererColors = Array.Empty<Color>();
                return;
            }

            var panel = informationText.transform.parent;
            explanationLabels = panel.GetComponentsInChildren<TMP_Text>(true);
            explanationLabelColors = new Color[explanationLabels.Length];
            for (int i = 0; i < explanationLabels.Length; i++)
                explanationLabelColors[i] = explanationLabels[i].color;

            var renderers = panel.GetComponentsInChildren<Renderer>(true);
            var backgrounds = new List<Renderer>();
            var colors = new List<Color>();
            foreach (var renderer in renderers)
            {
                if (renderer.GetComponent<TMP_Text>() != null || renderer.sharedMaterial == null)
                    continue;
                string property = renderer.sharedMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                if (!renderer.sharedMaterial.HasProperty(property))
                    continue;
                backgrounds.Add(renderer);
                colors.Add(renderer.sharedMaterial.GetColor(property));
            }
            explanationRenderers = backgrounds.ToArray();
            explanationRendererColors = colors.ToArray();
            explanationVisualsCached = true;
        }

        void SetExplanationOpacity(float opacity)
        {
            ExplanationOpacity = Mathf.Clamp01(opacity);
            for (int i = 0; i < explanationLabels.Length; i++)
            {
                if (!explanationLabels[i]) continue;
                var color = explanationLabelColors[i];
                color.a *= ExplanationOpacity;
                explanationLabels[i].color = color;
            }
            for (int i = 0; i < explanationRenderers.Length; i++)
            {
                var renderer = explanationRenderers[i];
                if (!renderer) continue;
                var color = explanationRendererColors[i];
                color.a *= ExplanationOpacity;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }

        public void RefreshText()
        {
            WriteText(informationText, informationEnglish);
            WriteText(progressText, Mathf.Min(VisitedCount, requiredHotspots) + " / " + requiredHotspots + " inspected");
            if (completionFeedback != null)
            {
                WriteText(completionFeedback.GetComponentInChildren<TMP_Text>(true), "Exploration complete");
                completionFeedback.SetActive(IsComplete);
            }
        }

        void WriteText(TMP_Text label, string english)
        {
            if (!label) return;
            label.text = english;
            TextChanged?.Invoke(label, english);
        }
    }
}
