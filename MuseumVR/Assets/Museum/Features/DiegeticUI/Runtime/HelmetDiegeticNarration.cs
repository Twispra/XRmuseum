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
        const string OrientationGuide = "Aim at the shell. Hold Grip and move the controller to turn the helmet. Release to stop. Aim at the front window, then press and release Grip to open it.";
        const string AirGuide = "In this demo, the team at the surface pumps air through a hose to the helmet.";
        const string VoiceGuide = "In this demo, a cable lets the diver and the team at the surface talk to each other.";

        HelmetPhysicalControl narrationControl;
        TextMeshPro narrationCaption;
        AudioSource narrationSource;
        LanguageChannel narrationLanguage;
        GuideSegment activeGuide;
        int narrationEra, narrationReset;
        float narrationEndsAt;
        bool narrationActive;

        void BuildNarration()
        {
            narrationControl = MakeControl(pedestalUI, Command.Narration, PlayGuide, "", teal);
            narrationControl.transform.localPosition = new Vector3(.39f, -.10f, 0);
            narrationCaption = Label(pedestalUI, "Guide subtitle", new Vector3(.16f, -.245f, -.065f), new Vector2(.74f, .16f), .27f);
            narrationCaption.enableAutoSizing = true;
            narrationCaption.fontSizeMin = .22f;
            narrationCaption.fontSizeMax = .27f;
            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "Subtitle backing";
            backing.transform.SetParent(narrationCaption.transform, false);
            backing.transform.localPosition = new Vector3(0, 0, .012f);
            backing.transform.localScale = new Vector3(.77f, .18f, .006f);
            backing.GetComponent<Collider>().enabled = false;
            Destroy(backing.GetComponent<Collider>());
            var renderer = backing.GetComponent<Renderer>();
            renderer.sharedMaterial = uiMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", new Color(.025f, .045f, .055f, 1f));
            renderer.SetPropertyBlock(properties);
            narrationCaption.gameObject.SetActive(false);

            var player = new GameObject("Helmet narration playback");
            player.transform.SetParent(pedestalUI, false);
            narrationSource = player.AddComponent<AudioSource>();
            narrationSource.playOnAwake = false;
            narrationSource.spatialBlend = 0f;
            narrationSource.volume = .75f;
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
            narrationEndsAt = Time.unscaledTime + (clip ? Mathf.Max(6f, clip.length) : 8f);
            MuseumText.Set(narrationCaption, caption);
            narrationCaption.gameObject.SetActive(true);
            narrationControl.SetTitle(StopGuide);
            narrationControl.SetEmphasis(true);
            if (!inspection.SoundEnabled || !clip) return;
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
            if (!narrationActive) return;
            if (Time.unscaledTime >= narrationEndsAt)
            {
                StopNarration();
                return;
            }
            if (!inspection.SoundEnabled || (narrationSource.clip && !narrationSource.isPlaying))
                StopNarrationAudio();
        }

        void StopNarrationAudio()
        {
            if (!narrationSource || !narrationSource.clip) return;
            narrationSource.Stop();
            narrationSource.clip = null;
            if (demonstration) demonstration.SetNarrationAudioSuppressed(false);
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
