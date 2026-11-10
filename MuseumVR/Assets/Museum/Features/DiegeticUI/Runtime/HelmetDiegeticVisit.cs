using Museum.Localization;
using Museum.PhotoHelmetTrial;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Diegetic
{
    public sealed partial class HelmetDiegeticStation
    {
        const string VisitInvitation = "Could a diver work alone in this helmet?";
        const string VisitMovementHelp = "Left stick moves. Center and hold its click, then tilt to turn.\nRight stick: up to teleport, sideways to turn.";

        static readonly Command[] VisitDeckCommands =
        {
            Command.PreviousEra, Command.NextEra, Command.Reset, Command.Hints,
            Command.Settings, Command.About, Command.Narration, Command.FinishVisit
        };

        TextMeshPro visitHandoffMessage;
        bool showingMovementHelp;

        public bool IsVisitHandoffOpen { get; private set; }

        void BuildVisitControls()
        {
            var finish = MakeControl(pedestalUI, Command.FinishVisit, "Finish visit", "", teal);
            finish.transform.localPosition = new Vector3(RightButtonColumn, 0, 0);

            var keep = MakeControl(pedestalUI, Command.ContinueVisit, "Continue visit", "", teal);
            keep.transform.localPosition = new Vector3(-.105f, 0, 0);
            keep.SetEmphasis(true);
            keep.gameObject.SetActive(false);

            var next = MakeControl(pedestalUI, Command.StartNextVisit, "Start next visit", "", amber);
            next.transform.localPosition = new Vector3(.105f, 0, 0);
            next.SetEmphasis(true);
            next.gameObject.SetActive(false);

            visitHandoffMessage = Label(pedestalUI, "Visit handoff confirmation",
                new Vector3(0, -.11f, -.015f), new Vector2(.62f, .10f), .29f);
            MuseumText.Set(visitHandoffMessage, "Starting the next visit clears this visit's discoveries.");
            visitHandoffMessage.gameObject.SetActive(false);
        }

        void RefreshVisitMovementHelp()
        {
            if (showingMovementHelp == AlwaysShowHints) return;
            showingMovementHelp = AlwaysShowHints;
            MuseumText.Set(instruction, showingMovementHelp ? VisitMovementHelp : VisitInvitation);
        }

        static bool IsVisitChoice(Command command) =>
            command == Command.ContinueVisit || command == Command.StartNextVisit;

        bool IsVisitChoiceTarget(IXRInteractable target) =>
            ReferenceEquals(target, controls[Command.ContinueVisit].Target) ||
            ReferenceEquals(target, controls[Command.StartNextVisit].Target);

        void FinishVisit()
        {
            if (IsVisitHandoffOpen) return;
            CancelGestures();
            StopNarration();
            exploration.Stop();
            options.SetActive(false);
            about.SetActive(false);
            SetVisitHandoff(true);
        }

        void ContinueVisit()
        {
            if (!IsVisitHandoffOpen) return;
            CancelGestures();
            SetVisitHandoff(false);
        }

        void StartNextVisit()
        {
            if (!IsVisitHandoffOpen) return;
            // ApplyEra cancels station, panel and left-study presses before the rig moves.
            ApplyEra(MarkVEraIndex);
            exploration.RestartVisit();
            RotationHelpLearned = false;
            foreach (var display in FindObjectsOfType<PhotoHelmetGalleryDisplay>())
                if (display.gameObject.scene == gameObject.scene) display.ResetDisplay();
            recovery.ResetView();
            SetVisitHandoff(false);
        }

        void SetVisitHandoff(bool open)
        {
            IsVisitHandoffOpen = open;
            foreach (var command in VisitDeckCommands)
                controls[command].gameObject.SetActive(!open);
            controls[Command.ContinueVisit].gameObject.SetActive(open);
            controls[Command.StartNextVisit].gameObject.SetActive(open);
            visitHandoffMessage.gameObject.SetActive(open);
            eraTitle.gameObject.SetActive(!open);
            instruction.gameObject.SetActive(!open);
            sectionDock.gameObject.SetActive(!open && !SettingsVisible);
            RefreshConnectionControls();
        }
    }
}
