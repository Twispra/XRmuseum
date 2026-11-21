using Museum.Hotspots;
using Museum.Localization;
using Museum.PhotoHelmetTrial;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
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

        GameObject visitRecap;
        HelmetHotspotTour recapTour;
        TextMeshPro recapProgress, recapConnections;
        readonly TextMeshPro[] recapParts = new TextMeshPro[3];
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

            BuildVisitRecap();
        }

        void BuildVisitRecap()
        {
            visitRecap = new GameObject("Visit recap");
            var root = visitRecap.transform;
            root.SetParent(pedestalUI, false);
            root.localPosition = new Vector3(.16f, -.071f, -.065f);

            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "Recap backing";
            backing.transform.SetParent(root, false);
            backing.transform.localPosition = new Vector3(0, -.167f, .012f);
            backing.transform.localScale = new Vector3(.77f, .334f, .006f);
            var collider = backing.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = backing.GetComponent<Renderer>();
            renderer.sharedMaterial = uiMaterial;
            // Draw behind every label, even when its center is closer to the viewer.
            renderer.sortingOrder = -1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", new Color(.025f, .045f, .055f, 1f));
            renderer.SetPropertyBlock(properties);
            SetFrontSurface(renderer);

            var title = Label(root, "Recap title", new Vector3(0, -.039f, 0), new Vector2(.70f, .043f), .32f);
            MuseumText.Set(title, "Your visit");
            recapProgress = Label(root, "Recap inspected count", new Vector3(-.19f, -.098f, 0), new Vector2(.32f, .039f), .26f);
            MuseumText.Set(recapProgress, "");
            for (int i = 0; i < recapParts.Length; i++)
            {
                recapParts[i] = Label(root, "Recap part " + (i + 1), new Vector3(-.19f, -.143f - i * .035f, 0),
                    new Vector2(.32f, .035f), .25f);
                MuseumText.Set(recapParts[i], "", headingOnly: true);
            }

            recapConnections = Label(root, "Recap connections", new Vector3(.175f, -.158f, 0), new Vector2(.39f, .156f), .26f);
            recapConnections.alignment = TextAlignmentOptions.TopLeft;
            MuseumText.Set(recapConnections, exploration.ConnectionRecap);
            var warning = Label(root, "Visit handoff confirmation", new Vector3(0, -.283f, 0), new Vector2(.69f, .065f), .25f);
            MuseumText.Set(warning, "Starting the next visit clears this visit's discoveries.");
            visitRecap.SetActive(false);
        }

        void SetVisitRecapOpen(bool open)
        {
            if (recapTour) recapTour.ObservationsChanged -= RefreshVisitRecap;
            recapTour = open ? exploration.VisitTour : null;
            if (recapTour) recapTour.ObservationsChanged += RefreshVisitRecap;
            if (open) RefreshVisitRecap();
            visitRecap.SetActive(open);
        }

        void RefreshVisitRecap()
        {
            recapProgress.gameObject.SetActive(recapTour != null);
            if (recapTour) MuseumText.Set(recapProgress, recapTour.ProgressEnglish);
            MuseumText.Set(recapConnections, exploration.ConnectionRecap);

            int shown = 0;
            if (recapTour)
                foreach (var hotspot in recapTour.GetComponentsInChildren<HelmetHotspot>(true))
                {
                    if (!recapTour.TryGetFirstObservation(hotspot.Identifier, out var source)) continue;
                    var row = recapParts[shown++];
                    MuseumText.Set(row, source, headingOnly: true);
                    row.gameObject.SetActive(true);
                    if (shown == recapParts.Length) break;
                }
            for (int i = shown; i < recapParts.Length; i++)
                recapParts[i].gameObject.SetActive(false);
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
            SetVisitRecapOpen(open);
            eraTitle.gameObject.SetActive(!open);
            instruction.gameObject.SetActive(!open);
            sectionDock.gameObject.SetActive(!open && !SettingsVisible);
            RefreshConnectionControls();
        }
    }
}
