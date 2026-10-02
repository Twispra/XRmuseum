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
                    case Page.Air: return "WHERE DOES AIR COME FROM?";
                    case Page.Voice: return "WHERE DOES A MESSAGE GO?";
                    case Page.Hint: return "NEED A CLUE?";
                    case Page.Review: return "WHAT HAVE YOU FOUND?";
                    default: return "IS THE HELMET ENOUGH?";
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
                        return "A surface pump sends air through a hose.\nFollow the flow to the rear connector.\nModels and flow are simplified.";
                    case Page.Voice:
                        return "Watch the surface telephone send a message.\nThe diver replies along the cable.\nThese models show the principle, not exact equipment.";
                    case Page.Hint:
                        if (!AirSeen) return "Turn the helmet to find its rear connector.\nGrip the marker, or choose AIR here.\nYou do not need to walk.";
                        if (!VoiceSeen) return "How could a diver ask for help?\nChoose VOICE to follow a message and reply.\nKeep inspecting whenever you wish.";
                        return "You have viewed both connections.\nWhy is the helmet part of a larger system?\nChoose REVIEW to collect your findings.";
                    case Page.Review:
                        if (!AirSeen && !VoiceSeen)
                            return "No clues viewed yet.\nChoose AIR or VOICE in any order.\nKeep inspecting at your own pace.";
                        if (AirSeen && VoiceSeen)
                            return "AIR: a hose supplies air from the surface.\nVOICE: diver and surface team exchange messages.\nThe helmet is one part of the system.";
                        return (AirSeen ? "AIR: a hose supplies air from the surface.\nVOICE: not viewed yet."
                            : "VOICE: diver and surface team exchange messages.\nAIR: not viewed yet.")
                            + "\nKeep exploring when you wish.";
                    default:
                        return "Could you work underwater alone wearing this helmet?\nExplore its connections above water.\nChoose AIR or VOICE, or inspect it yourself.";
                }
            }
        }

        public string Footer => IsPlaying
            ? "Demo playing / STOP hides models"
            : IsDiagramVisible ? "Demo paused / REPLAY or STOP"
            : "Point + grip to choose / REPLAY is optional";

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
