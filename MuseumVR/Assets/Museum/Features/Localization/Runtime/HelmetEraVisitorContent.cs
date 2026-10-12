using System;
using System.Collections.Generic;
using Museum.Hotspots;
using Museum.Refined;
using TMPro;
using UnityEngine;

namespace Museum.Localization
{
    /// <summary>Visitor copy and discovery points follow the visual era actually applied by the binding.</summary>
    [DefaultExecutionOrder(95), DisallowMultipleComponent]
    public sealed class HelmetEraVisitorContent : MonoBehaviour
    {
        public const string SuperLiteInstructions = "Diving helmet\n<size=62%>SuperLite 17B-inspired study</size>";
        public const string FrontWindowTitle = "Front window";
        public const string FrontWindowDetails = "Inspect the front viewing area and its rim. Side opening is an observation demonstration, not the real helmet's mechanism.";
        public const string ShellTitle = "Helmet shell";
        public const string ShellDetails = "Inspect the upper shell of this lightweight helmet study.";
        public const string NeckTitle = "Neck seal";
        public const string NeckDetails = "Inspect the neck seal and surrounding clamp below the shell.";

        [Serializable]
        public sealed class HotspotAttachment
        {
            public HelmetHotspot hotspot;
            public Renderer surface;
            // Position in the referenced mesh transform, stored when the saved asset is integrated.
            public Vector3 localPoint;
            public string heading;
            [TextArea] public string details;
        }

        [SerializeField] RefinedHelmetBinding binding;
        [SerializeField] HelmetHotspotTour tour;
        [SerializeField] TMP_Text instructions;
        [SerializeField] HotspotAttachment[] attachments = Array.Empty<HotspotAttachment>();

        sealed class OriginalHotspot
        {
            public HelmetHotspot hotspot;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public string title, description;
        }
        OriginalHotspot[] originals = Array.Empty<OriginalHotspot>();
        string originalInstructions;
        bool captured;
        LocalizedText originalKeyBinding;
        bool originalKeyBindingEnabled;

        public bool IsSuperLiteContentActive { get; private set; }
        public IReadOnlyList<HotspotAttachment> Attachments => attachments;
        public TMP_Text Instructions => instructions;

        public void Configure(RefinedHelmetBinding targetBinding, HelmetHotspotTour targetTour, TMP_Text caption,
            HotspotAttachment[] points)
        {
            if (captured) RestoreOriginal();
            if (originalKeyBinding) originalKeyBinding.enabled = originalKeyBindingEnabled;
            originalKeyBinding = null;
            originalInstructions = null;
            binding = targetBinding;
            tour = targetTour;
            instructions = caption;
            attachments = points ?? Array.Empty<HotspotAttachment>();
            captured = false;
        }

        void OnEnable()
        {
            if (captured && originalKeyBinding) originalKeyBinding.enabled = false;
        }

        void LateUpdate()
        {
            if (!binding) return;
            if (!captured) CaptureOriginal();
            // A requested era can be deferred while a window or section tool is held.
            // The visitor copy must change with the applied geometry, not the request.
            bool next = binding.IsAdditionalVisualActive;
            if (next != IsSuperLiteContentActive)
            {
                IsSuperLiteContentActive = next;
                if (next)
                {
                    foreach (var attachment in attachments)
                        if (attachment != null && attachment.hotspot)
                            attachment.hotspot.SetContent(attachment.heading, attachment.details);
                    WriteInstructions(SuperLiteInstructions);
                }
                else RestoreOriginal();
            }
            if (!IsSuperLiteContentActive) return;
            foreach (var attachment in attachments)
                if (attachment != null && attachment.hotspot && attachment.surface)
                    attachment.hotspot.transform.position = attachment.surface.transform.TransformPoint(attachment.localPoint);
        }

        void CaptureOriginal()
        {
            var saved = new List<OriginalHotspot>();
            foreach (var attachment in attachments)
            {
                if (attachment == null || !attachment.hotspot) continue;
                var point = attachment.hotspot;
                saved.Add(new OriginalHotspot
                {
                    hotspot = point, position = point.transform.localPosition,
                    rotation = point.transform.localRotation, scale = point.transform.localScale,
                    title = point.Title, description = point.Description
                });
            }
            originals = saved.ToArray();
            if (instructions)
            {
                var source = instructions.GetComponent<LocalizedSourceText>();
                originalKeyBinding = instructions.GetComponent<LocalizedText>();
                if (source) originalInstructions = source.SourceEnglish;
                else if (originalKeyBinding && MuseumText.Table &&
                    MuseumText.Table.TryGet(originalKeyBinding.Key, MuseumLanguage.English, out var english))
                    originalInstructions = english;
                // A translated label.text is never an English source.
                if (originalKeyBinding)
                {
                    originalKeyBindingEnabled = originalKeyBinding.enabled;
                    originalKeyBinding.enabled = false;
                }
            }
            captured = true;
        }

        void WriteInstructions(string english)
        {
            if (instructions && !string.IsNullOrEmpty(english)) MuseumText.Set(instructions, english);
        }

        void RestoreOriginal()
        {
            foreach (var original in originals)
            {
                if (!original.hotspot) continue;
                original.hotspot.transform.localPosition = original.position;
                original.hotspot.transform.localRotation = original.rotation;
                original.hotspot.transform.localScale = original.scale;
                original.hotspot.SetContent(original.title, original.description);
            }
            WriteInstructions(originalInstructions);
            IsSuperLiteContentActive = false;
        }

        void OnDisable()
        {
            if (!captured) return;
            RestoreOriginal();
            if (originalKeyBinding) originalKeyBinding.enabled = originalKeyBindingEnabled;
        }
    }
}
