using Museum.Exploration;
using Museum.Localization;
using TMPro;
using UnityEngine;

namespace Museum.Diegetic
{
    public sealed partial class HelmetDiegeticStation
    {
        enum GuideSegment { Orientation, Air, Voice }

        const string PlayGuide = "Play narration";
        const string StopGuide = "Stop narration";
        const string CloseGuide = "Close subtitles";
        const string OrientationGuide = "Aim at the shell. Hold Grip and move the controller to turn the helmet. Release to stop. Aim at the front window, then press and release Grip to open it.";
        const string AirGuide = "In this demo, the team at the surface pumps air through a hose to the helmet.";
        const string VoiceGuide = "In this demo, a cable lets the diver and the team at the surface talk to each other.";
        const string StandardSubtitleSize = "Subtitles\nStandard";
        const string LargeSubtitleSize = "Subtitles\nLarge";
        const float SubtitleWidth = .74f;
        const float StandardSubtitleHeight = .16f;
        const float StandardSubtitleFont = .27f;
        const float LargeSubtitleFont = .36f;
        static readonly Vector3 StandardSubtitlePosition = new Vector3(.16f, -.245f, -.065f);

        HelmetPhysicalControl narrationControl;
        TextMeshPro narrationCaption;
        Transform narrationBacking;
        bool largeSubtitles;
        AudioSource narrationSource;
        LanguageChannel narrationLanguage;
        GuideSegment activeGuide;
        int narrationEra, narrationReset;
        bool narrationActive;

        void BuildNarration()
        {
            narrationControl = MakeControl(pedestalUI, Command.Narration, PlayGuide, "", teal);
            narrationControl.transform.localPosition = new Vector3(RightButtonColumn, -.10f, 0);
            narrationCaption = Label(pedestalUI, "Guide subtitle", StandardSubtitlePosition,
                new Vector2(SubtitleWidth, StandardSubtitleHeight), StandardSubtitleFont);
            narrationCaption.fontSizeMin = .22f;
            narrationCaption.fontSizeMax = StandardSubtitleFont;
            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "Subtitle backing";
            narrationBacking = backing.transform;
            narrationBacking.SetParent(narrationCaption.transform, false);
            narrationBacking.localPosition = new Vector3(0, 0, .012f);
            backing.GetComponent<Collider>().enabled = false;
            Destroy(backing.GetComponent<Collider>());
            var renderer = backing.GetComponent<Renderer>();
            renderer.sharedMaterial = uiMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", new Color(.025f, .045f, .055f, 1f));
            renderer.SetPropertyBlock(properties);
            SetFrontSurface(renderer);
            MuseumText.Set(narrationCaption, OrientationGuide);
            LayoutNarrationSubtitle();
            narrationCaption.gameObject.SetActive(false);

            var player = new GameObject("Helmet narration playback");
            player.transform.SetParent(pedestalUI, false);
            narrationSource = player.AddComponent<AudioSource>();
            narrationSource.playOnAwake = false;
            narrationSource.spatialBlend = 0f;
            narrationSource.volume = .75f;
        }

        void ToggleSubtitleSize()
        {
            largeSubtitles = !largeSubtitles;
            var control = controls[Command.SubtitleSize];
            control.SetTitle(largeSubtitles ? LargeSubtitleSize : StandardSubtitleSize);
            control.SetEmphasis(largeSubtitles);
            if (narrationActive) LayoutNarrationSubtitle();
        }

        void LayoutNarrationSubtitle()
        {
            narrationCaption.enableAutoSizing = !largeSubtitles;
            narrationCaption.fontSize = largeSubtitles ? LargeSubtitleFont : StandardSubtitleFont;
            float height = StandardSubtitleHeight;
            if (largeSubtitles)
            {
                float preferredHeight = narrationCaption.GetPreferredValues(
                    narrationCaption.text, SubtitleWidth, Mathf.Infinity).y;
                height = Mathf.Max(height, preferredHeight + .02f);
            }

            // Keep the top edge below the controls as additional lines grow downward.
            narrationCaption.rectTransform.sizeDelta = new Vector2(SubtitleWidth, height);
            narrationCaption.transform.localPosition = StandardSubtitlePosition +
                Vector3.down * ((height - StandardSubtitleHeight) * .5f);
            narrationBacking.localScale = new Vector3(SubtitleWidth + .03f, height + .02f, .006f);
        }

        GuideSegment CurrentGuide()
        {
            if (!ConnectionDemosAvailable || !demonstration.IsVisible) return GuideSegment.Orientation;
            return demonstration.CurrentClue == HelmetExplorationController.Clue.Air ? GuideSegment.Air : GuideSegment.Voice;
        }

        void ToggleNarration()
        {
            if (!isActiveAndEnabled) return;
            if (narrationActive)
            {
                StopNarration();
                return;
            }

            activeGuide = CurrentGuide();
            narrationEra = AppliedEra;
            narrationReset = inspection.ResetCount;
            narrationLanguage = MuseumText.Channel;
            if (narrationLanguage) narrationLanguage.LanguageChanged += OnNarrationLanguageChanged;
            bool english = !narrationLanguage || narrationLanguage.Current == MuseumLanguage.English;
            string stem = activeGuide == GuideSegment.Air ? "air" : activeGuide == GuideSegment.Voice ? "voice" : "intro";
            string caption = activeGuide == GuideSegment.Air ? AirGuide : activeGuide == GuideSegment.Voice ? VoiceGuide : OrientationGuide;
            var clip = Resources.Load<AudioClip>("HelmetNarration/" + stem + (english ? "-en" : "-zh"));

            narrationActive = true;
            MuseumText.Set(narrationCaption, caption);
            narrationCaption.gameObject.SetActive(true);
            LayoutNarrationSubtitle();
            narrationControl.SetTitle(StopGuide);
            narrationControl.SetEmphasis(true);
            if (!inspection.SoundEnabled || !clip)
            {
                narrationControl.SetTitle(CloseGuide);
                return;
            }

            demonstration.SetNarrationAudioSuppressed(true);
            narrationSource.clip = clip;
            narrationSource.Play();
        }

        void OnNarrationLanguageChanged(MuseumLanguage _) => StopNarration();

        void CheckNarrationContext()
        {
            if (narrationActive && (activeGuide != CurrentGuide() || narrationEra != AppliedEra ||
                narrationReset != inspection.ResetCount || narrationLanguage != MuseumText.Channel))
                StopNarration();
        }

        void UpdateNarration()
        {
            CheckNarrationContext();
            if (narrationActive && narrationSource.clip && (!inspection.SoundEnabled || !narrationSource.isPlaying))
                StopNarrationAudio();
        }

        void StopNarrationAudio()
        {
            if (!narrationSource || !narrationSource.clip) return;
            narrationSource.Stop();
            narrationSource.clip = null;
            if (demonstration) demonstration.SetNarrationAudioSuppressed(false);
            if (narrationActive) narrationControl.SetTitle(CloseGuide);
        }

        void StopNarration()
        {
            if (!narrationActive) return;
            narrationActive = false;
            StopNarrationAudio();
            if (narrationLanguage) narrationLanguage.LanguageChanged -= OnNarrationLanguageChanged;
            narrationLanguage = null;
            narrationCaption.gameObject.SetActive(false);
            narrationControl.SetEmphasis(false);
            narrationControl.SetTitle(PlayGuide);
        }
    }
}
