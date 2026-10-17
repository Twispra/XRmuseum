using System;
using System.Collections.Generic;
using System.Linq;
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
        public const string EarlyInstructions = "Diving helmet\n<size=62%>Early (1839) - placeholder study</size>";
        public const string EarlyPlaceholderDetails = "Placeholder; not a historical replica.";
        public const string EarlyModelInstructions = "Diving helmet\n<size=62%>1839 closed-helmet study · SMG 1881-47</size>";
        public const string EarlyModelFrontDetails = "Three guarded viewports are visible in the museum photograph. Opening this panel is a virtual inspection action, not a documented hinge.";
        public const string EarlyModelShellDetails = "Rounded copper-alloy bonnet based on Science Museum Group object 1881-47. Exact rear profile and part dimensions are estimates.";
        public const string EarlyModelBreastplateDetails = "Broad breastplate based on object 1881-47. Rear fastener positions and component measurements are interpretive.";
        public const string FrontWindowTitle = "Front window";
        public const string FrontWindowDetails = "Window and rim. Side opening is a museum demonstration.";
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
        sealed class SurfaceAttachment
        {
            public HelmetHotspot hotspot;
            public Renderer surface;
            public Vector3 localPoint;
        }
        SurfaceAttachment[] earlyAttachments = Array.Empty<SurfaceAttachment>();
        string originalInstructions;
        bool captured;
        LocalizedText originalKeyBinding;
        bool originalKeyBindingEnabled;
        int appliedEra = int.MinValue;

        public bool IsSuperLiteContentActive { get; private set; }
        public bool IsEarlyModelContentActive { get; private set; }
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
            appliedEra = int.MinValue;
        }

        void OnEnable()
        {
            if (captured && originalKeyBinding) originalKeyBinding.enabled = false;
            appliedEra = int.MinValue;
        }

        void LateUpdate()
        {
            if (!binding) return;
            if (!captured) CaptureOriginal();
            // A requested era can be deferred while a window or section tool is held.
            // The visitor copy must change with the applied geometry, not the request.
            int next = binding.AppliedEraIndex;
            if (next < 0) return;
            if (next != appliedEra)
            {
                RestoreOriginal();
                appliedEra = next;
                IsSuperLiteContentActive = next == 2 && binding.IsAdditionalVisualActive;
                if (IsSuperLiteContentActive)
                {
                    foreach (var attachment in attachments)
                        if (attachment != null && attachment.hotspot)
                            attachment.hotspot.SetContent(attachment.heading, attachment.details);
                    WriteInstructions(SuperLiteInstructions);
                }
                else if (next == 0 && binding.EarlyVisual != null)
                {
                    IsEarlyModelContentActive = true;
                    WriteInstructions(EarlyModelInstructions);
                    var early = binding.EarlyVisual;
                    var breastplate = early.renderers.First(r => r.name == "Breastplate_DishedCopper");
                    var points = new List<SurfaceAttachment>();
                    foreach (var original in originals)
                    {
                        if (!original.hotspot) continue;
                        switch (original.hotspot.Identifier)
                        {
                            case "front-window":
                                original.hotspot.SetContent("Front viewport", EarlyModelFrontDetails);
                                points.Add(Attach(original.hotspot, early.glass,
                                    early.glass.bounds.center + binding.transform.forward * .038f));
                                break;
                            case "bonnet":
                                original.hotspot.SetContent("Bonnet shell", EarlyModelShellDetails);
                                points.Add(Attach(original.hotspot, early.shell,
                                    early.shell.bounds.center + binding.transform.right * (early.shell.bounds.extents.x + .025f)));
                                break;
                            case "breastplate":
                                original.hotspot.SetContent("Breastplate", EarlyModelBreastplateDetails);
                                points.Add(Attach(original.hotspot, breastplate,
                                    breastplate.bounds.center + binding.transform.forward * (breastplate.bounds.extents.z + .025f)));
                                break;
                        }
                    }
                    earlyAttachments = points.ToArray();
                }
                else if (!binding.IsRefinedActive)
                {
                    WriteInstructions(EarlyInstructions);
                    foreach (var original in originals)
                        if (original.hotspot)
                            original.hotspot.SetContent(original.title, EarlyPlaceholderDetails);
                }
            }
            if (IsEarlyModelContentActive)
                foreach (var attachment in earlyAttachments)
                    if (attachment.hotspot && attachment.surface)
                        attachment.hotspot.transform.position = attachment.surface.transform.TransformPoint(attachment.localPoint);
            if (!IsSuperLiteContentActive) return;
            foreach (var attachment in attachments)
                if (attachment != null && attachment.hotspot && attachment.surface)
                    attachment.hotspot.transform.position = attachment.surface.transform.TransformPoint(attachment.localPoint);
        }

        static SurfaceAttachment Attach(HelmetHotspot hotspot, Renderer surface, Vector3 worldPoint)
        {
            hotspot.transform.position = worldPoint;
            return new SurfaceAttachment
            {
                hotspot = hotspot, surface = surface,
                localPoint = surface.transform.InverseTransformPoint(worldPoint)
            };
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
            IsEarlyModelContentActive = false;
            earlyAttachments = Array.Empty<SurfaceAttachment>();
        }

        void OnDisable()
        {
            if (!captured) return;
            RestoreOriginal();
            if (originalKeyBinding) originalKeyBinding.enabled = originalKeyBindingEnabled;
        }
    }
}
