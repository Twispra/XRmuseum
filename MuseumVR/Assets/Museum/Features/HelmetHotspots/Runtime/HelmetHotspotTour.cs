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

        readonly HashSet<string> visited = new HashSet<string>();
        readonly Dictionary<XRRayInteractor, QueryTriggerInteraction> rayQuerySettings = new Dictionary<XRRayInteractor, QueryTriggerInteraction>();
        readonly Dictionary<XRRayInteractor, bool> rayHoverActivationSettings = new Dictionary<XRRayInteractor, bool>();
        readonly Dictionary<XRDirectInteractor, QueryTriggerInteraction> directQuerySettings = new Dictionary<XRDirectInteractor, QueryTriggerInteraction>();

        public event Action Completed;
        public event Action ResetPerformed;
        public int VisitedCount => visited.Count;
        public int RequiredHotspots => requiredHotspots;
        public bool IsComplete => VisitedCount >= requiredHotspots;

        public void Configure(int required, TMP_Text information, TMP_Text progress, GameObject completion)
        {
            requiredHotspots = Mathf.Max(1, required);
            informationText = information;
            progressText = progress;
            completionFeedback = completion;
            RefreshProgress();
        }

        public bool Visit(string identifier, string title, string description)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            bool wasComplete = IsComplete;
            bool added = visited.Add(identifier);

            if (informationText != null)
                informationText.text = string.IsNullOrWhiteSpace(title) ? description : title + "\n" + description;

            RefreshProgress();

            if (!wasComplete && IsComplete)
                Completed?.Invoke();

            return added;
        }

        public void ResetTour()
        {
            visited.Clear();
            if (informationText != null)
                informationText.text = "Select a highlighted point to inspect the helmet.";
            RefreshProgress();
            ResetPerformed?.Invoke();
        }

        void OnEnable()
        {
            if (enableTriggerQueries)
                EnableCompatibleInteractors();
            RefreshProgress();
        }

        void OnDisable()
        {
            RestoreInteractorQueries();
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

        void RefreshProgress()
        {
            if (progressText != null)
                progressText.text = Mathf.Min(VisitedCount, requiredHotspots) + " / " + requiredHotspots + " inspected";
            if (completionFeedback != null)
                completionFeedback.SetActive(IsComplete);
        }
    }
}
