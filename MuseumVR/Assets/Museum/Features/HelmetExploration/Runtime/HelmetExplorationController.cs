using System;
using Museum.Inspection;
using UnityEngine;

namespace Museum.Exploration
{
    /// <summary>Optional exhibit interpretation. It never gates the physical inspection tools.</summary>
    [DisallowMultipleComponent]
    public sealed class HelmetExplorationController : MonoBehaviour
    {
        public enum Clue { Air, Voice }
        public enum Page { Welcome, Air, Voice, Hint, Review }

        [SerializeField] HelmetInspectionController inspection;
        [SerializeField] HelmetConnectionDemonstration demonstration;
        HelmetConnectionDemonstration subscribedDemonstration;
        int observedReset;
        bool hasSelectedClue;

        public event Action Changed;
        public event Action<Clue> ClueOpened;
        public Page CurrentPage { get; private set; } = Page.Welcome;
        public Clue SelectedClue { get; private set; }
        public bool AirSeen { get; private set; }
        public bool VoiceSeen { get; private set; }
        public int HintRequests { get; private set; }
        public bool HasSelectedClue => hasSelectedClue;
        public bool IsPlaying => demonstration && demonstration.IsPlaying;
        public bool IsDiagramVisible => demonstration && demonstration.IsVisible;

        public string Title
        {
            get
            {
                switch (CurrentPage)
                {
                    case Page.Air: return "Air from the surface";
                    case Page.Voice: return "A two-way connection";
                    case Page.Hint: return "A hint";
                    case Page.Review: return "Your visit";
                    default: return "Choose a connection";
                }
            }
        }

        public string Body
        {
            get
            {
                switch (CurrentPage)
                {
                    case Page.Air:
                        return "A surface pump sends air through a hose to the helmet.";
                    case Page.Voice:
                        return "Diver and surface team exchange messages through a cable.";
                    case Page.Hint:
                        if (!AirSeen) return "Find the rear connector. Use Back to choose Air.";
                        if (!VoiceSeen) return "How could a diver ask for help? Use Back to choose Voice.";
                        return "You have seen both connections. How does the surface team support the diver?";
                    case Page.Review:
                        if (!AirSeen && !VoiceSeen)
                            return "Air: not seen yet\nVoice: not seen yet";
                        if (AirSeen && VoiceSeen)
                            return "Air: seen - air arrives from the surface.\nVoice: seen - messages travel both ways.";
                        return AirSeen ? "Air: seen - air arrives from the surface.\nVoice: not seen yet"
                            : "Air: not seen yet\nVoice: seen - messages travel both ways.";
                    default:
                        return "";
                }
            }
        }

        public string Footer => IsPlaying
            ? "Demo playing"
            : IsDiagramVisible ? "Static model view" : "";

        public void Configure(HelmetInspectionController target, HelmetConnectionDemonstration animation)
        {
            Unsubscribe();
            inspection = target;
            demonstration = animation;
            observedReset = inspection ? inspection.ResetCount : 0;
            if (isActiveAndEnabled) Subscribe();
            NotifyChanged();
        }

        void OnEnable()
        {
            observedReset = inspection ? inspection.ResetCount : 0;
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
            if (demonstration) demonstration.Stop();
        }

        void Update()
        {
            if (!inspection || observedReset == inspection.ResetCount) return;
            observedReset = inspection.ResetCount;
            // Returning the physical exhibit does not erase a visitor's discoveries.
            Stop();
        }

        public void SelectClue(Clue clue)
        {
            if (!isActiveAndEnabled || (clue != Clue.Air && clue != Clue.Voice)) return;
            bool firstView = clue == Clue.Air ? !AirSeen : !VoiceSeen;
            if (demonstration) demonstration.Stop();
            SelectedClue = clue;
            hasSelectedClue = true;
            CurrentPage = clue == Clue.Air ? Page.Air : Page.Voice;
            if (clue == Clue.Air) AirSeen = true; else VoiceSeen = true;
            if (firstView && demonstration) demonstration.Play(clue);
            NotifyChanged();
            ClueOpened?.Invoke(clue);
        }

        public void RequestHint()
        {
            if (!isActiveAndEnabled) return;
            StopDemonstration();
            HintRequests++;
            CurrentPage = Page.Hint;
            NotifyChanged();
        }

        public void ShowReview()
        {
            if (!isActiveAndEnabled) return;
            StopDemonstration();
            CurrentPage = Page.Review;
            NotifyChanged();
        }

        public void ShowWelcome()
        {
            if (!isActiveAndEnabled) return;
            StopDemonstration();
            CurrentPage = Page.Welcome;
            NotifyChanged();
        }

        public void Replay()
        {
            if (!isActiveAndEnabled || !hasSelectedClue) return;
            CurrentPage = SelectedClue == Clue.Air ? Page.Air : Page.Voice;
            if (demonstration) demonstration.Play(SelectedClue);
            NotifyChanged();
        }

        public void Stop()
        {
            StopDemonstration();
            NotifyChanged();
        }

        public void RestartVisit()
        {
            if (!isActiveAndEnabled) return;
            StopDemonstration();
            AirSeen = false;
            VoiceSeen = false;
            HintRequests = 0;
            hasSelectedClue = false;
            SelectedClue = Clue.Air;
            CurrentPage = Page.Welcome;
            NotifyChanged();
        }

        void StopDemonstration() { if (demonstration) demonstration.Stop(); }
        void NotifyChanged() => Changed?.Invoke();
        void Subscribe()
        {
            if (!demonstration || subscribedDemonstration == demonstration) return;
            subscribedDemonstration = demonstration;
            subscribedDemonstration.PlaybackChanged += NotifyChanged;
        }
        void Unsubscribe()
        {
            if (subscribedDemonstration) subscribedDemonstration.PlaybackChanged -= NotifyChanged;
            subscribedDemonstration = null;
        }
    }
}
