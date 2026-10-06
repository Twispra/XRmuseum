using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Museum.Inspection;
using Museum.Localization;
using Command = Museum.Exploration.HelmetExplorationButton.Action;

namespace Museum.Exploration
{
    /// <summary>Progressive pages on the existing personal palette and its hover-frozen placement.</summary>
    [DisallowMultipleComponent]
    public sealed class HelmetExplorationPanel : MonoBehaviour
    {
        [SerializeField] HelmetExplorationController exploration;
        [SerializeField] HelmetInspectionPalette palette;
        [SerializeField] GameObject page;
        [SerializeField] GameObject tabs;
        [SerializeField] GameObject[] originalContents = new GameObject[0];
        [SerializeField] TextMeshPro title, body, progress, footer;
        [SerializeField] bool progressiveLayout;
        [SerializeField] GameObject[] backgroundLabels = new GameObject[0];
        // Captured before the editor upgrade, so feature disable restores the original tools
        // even after the upgraded scene has been saved and loaded again.
        [SerializeField] SavedElement[] originalLayout = new SavedElement[0];
        bool subscribed, availabilityKnown, lastAvailable, lastCutaway, lastReduceMotion;
        bool moreExpanded, inspectionToolsExpanded, readingFromExploration, lastSound;
        Reading reading;
        HelmetExplorationController.Page lastPage;
        ButtonSlot[] buttons;
        ButtonSlot[] inspectionButtons;
        TextMeshPro inspectionTitle, inspectionSubtitle, inspectionStatus;
        const float Row = .065f;
        const float Top = .405f;
        enum Reading { None, Help, Details }

        static readonly string[] InspectionNames = {
            "WINDOW", "SECTION", "FLOAT", "RESET", "TURN LEFT", "TURN RIGHT", "TILT UP", "TILT DOWN",
            "EDIT CUT", "ALIGN VIEW", "DEEPER", "SHALLOWER", "TOP CUT", "FLIP CUT"
        };
        static readonly string[] InspectionCaptions = {
            "Window", "Section", "Float", "Reset", "Turn left", "Turn right", "Tilt up", "Tilt down",
            "Edit cut", "Align view", "Deeper", "Shallower", "Top cut", "Flip cut"
        };

        sealed class ButtonSlot
        {
            public GameObject button;
            public TextMeshPro label;
            public bool wanted;
            public void Show(bool visible)
            {
                if (button) button.SetActive(visible);
                if (label) label.gameObject.SetActive(visible);
            }
            public void Place(string caption, float x, float y, float width = .222f)
            {
                wanted = true;
                if (button)
                {
                    button.transform.localPosition = new Vector3(x, y, -.027f);
                    var size = button.transform.localScale; size.x = width; button.transform.localScale = size;
                }
                if (!label) return;
                MuseumText.Set(label, caption);
                label.transform.localPosition = new Vector3(x, y, -.043f);
                label.rectTransform.sizeDelta = new Vector2(width - .011f, .049f);
                SetFont(label, .4f, false);
                label.enableAutoSizing = true;
                label.fontSizeMin = .28f; label.fontSizeMax = .4f;
            }
        }

        [Serializable]
        sealed class SavedElement
        {
            public GameObject item;
            public Vector3 position, scale;
            public bool active;
            public TextMeshPro label;
            public Vector2 size, pivot;
            public string text;
            public float fontSize, fontMin, fontMax;
            public bool autoSize, wrap;
            public TextAlignmentOptions alignment;
            public SavedElement(Transform value)
            {
                item = value.gameObject; position = value.localPosition; scale = value.localScale;
                active = item.activeSelf; label = item.GetComponent<TextMeshPro>();
                if (!label) return;
                size = label.rectTransform.sizeDelta; pivot = label.rectTransform.pivot; text = label.text;
                fontSize = label.fontSize; fontMin = label.fontSizeMin; fontMax = label.fontSizeMax;
                autoSize = label.enableAutoSizing; wrap = label.enableWordWrapping; alignment = label.alignment;
            }
            public void Restore()
            {
                if (!item) return;
                item.transform.localPosition = position; item.transform.localScale = scale; item.SetActive(active);
                if (!label) return;
                label.rectTransform.sizeDelta = size; label.rectTransform.pivot = pivot; MuseumText.Set(label, text);
                label.fontSize = fontSize; label.fontSizeMin = fontMin; label.fontSizeMax = fontMax;
                label.enableAutoSizing = autoSize; label.enableWordWrapping = wrap; label.alignment = alignment;
            }
        }

        public bool IsExploring { get; private set; }
        public void ConfigureBackgroundLabels(GameObject[] labels) { backgroundLabels = labels; RefreshView(); }
        void ShowBackground(bool visible)
        {
            foreach (var label in backgroundLabels) if (label) label.SetActive(visible);
        }
        public bool IsReading => reading != Reading.None;
        public bool ProgressiveLayoutEnabled => progressiveLayout;
        public bool MoreExpanded => moreExpanded;
        public bool InspectionToolsExpanded => inspectionToolsExpanded;
        public HelmetExplorationController Exploration => exploration;
        bool Available => isActiveAndEnabled && exploration && exploration.isActiveAndEnabled;
        bool UseProgressiveLayout => progressiveLayout && Available;

        public void Configure(HelmetExplorationController target, HelmetInspectionPalette follower,
            GameObject pageRoot, GameObject tabRoot, GameObject[] original,
            TextMeshPro titleText, TextMeshPro bodyText, TextMeshPro progressText, TextMeshPro footerText)
        {
            Unsubscribe();
            exploration = target; palette = follower; page = pageRoot; tabs = tabRoot;
            originalContents = original; title = titleText; body = bodyText;
            progress = progressText; footer = footerText;
            buttons = null; inspectionButtons = null;
            if (palette) palette.ConfigureAdditionalPage(.085f);
            if (isActiveAndEnabled) Subscribe();
            ShowInspection();
            availabilityKnown = false; UpdateAvailability();
        }

        public void EnableProgressiveLayout()
        {
            if (originalLayout.Length == 0)
            {
                var saved = new List<SavedElement>();
                foreach (var original in originalContents)
                    if (original)
                        foreach (var child in original.GetComponentsInChildren<Transform>(true))
                            saved.Add(new SavedElement(child));
                originalLayout = saved.ToArray();
            }
            progressiveLayout = true;
            buttons = null; inspectionButtons = null;
            CacheControls();
            if (palette) palette.ConfigureAdditionalPage(.085f);
            RefreshView();
        }

        LanguageChannel subscribedLanguage;
        void OnEnable()
        {
            subscribedLanguage = MuseumText.Channel;
            if (subscribedLanguage) subscribedLanguage.LanguageChanged += OnLanguageChanged;
            Subscribe(); availabilityKnown = false; UpdateAvailability();
        }
        void Start() { ShowInspection(); }
        void Update()
        {
            UpdateAvailability();
            if (!UseProgressiveLayout || !palette || !palette.Controller) return;
            var controller = palette.Controller;
            if (lastCutaway == controller.IsCutaway && lastReduceMotion == controller.ReduceMotion && lastSound == controller.SoundEnabled) return;
            lastCutaway = controller.IsCutaway; lastReduceMotion = controller.ReduceMotion; lastSound=controller.SoundEnabled;
            RefreshView();
        }
        void UpdateAvailability()
        {
            bool available = Available;
            if (availabilityKnown && lastAvailable == available) return;
            availabilityKnown = true; lastAvailable = available;
            if (tabs) tabs.SetActive(available);
            if (palette) palette.ConfigureAdditionalPage(available ? .085f : 0f);
            ShowInspection();
        }
        void OnDisable()
        {
            if (subscribedLanguage) subscribedLanguage.LanguageChanged -= OnLanguageChanged;
            subscribedLanguage = null;
            Unsubscribe(); ShowInspection();
            if (tabs) tabs.SetActive(false);
            if (palette) { palette.ClearContentBounds(); palette.ConfigureAdditionalPage(0f); }
            if (exploration) exploration.Stop();
        }
        void Subscribe()
        {
            if (!exploration || subscribed) return;
            exploration.Changed += RefreshView; exploration.ClueOpened += OnClue; subscribed = true;
        }
        void Unsubscribe()
        {
            if (exploration && subscribed)
            { exploration.Changed -= RefreshView; exploration.ClueOpened -= OnClue; }
            subscribed = false;
        }
        void OnLanguageChanged(MuseumLanguage _) { RefreshView(); }
        void OnClue(HelmetExplorationController.Clue clue) { ShowExploration(); }

        public void ShowExploration()
        {
            if (!Available) return;
            reading = Reading.None; IsExploring = true;
            if (palette) palette.SetAlternatePageVisible(true);
            SetOriginal(false);
            if (page) page.SetActive(true);
            RefreshView();
        }
        public void ShowInspection()
        {
            reading = Reading.None; IsExploring = false;
            if (page) page.SetActive(false);
            if (palette) palette.SetAlternatePageVisible(false);
            if (!UseProgressiveLayout)
            {
                ShowBackground(true);
                foreach (var element in originalLayout) element.Restore();
                SetOriginal(true);
                HideAddedControls();
                if (palette) palette.ClearContentBounds();
                return;
            }
            SetOriginal(true); RefreshView();
        }
        public void ToggleMore() { moreExpanded = !moreExpanded; RefreshView(); }
        public void ToggleInspectionTools() { inspectionToolsExpanded = !inspectionToolsExpanded; RefreshView(); }
        public void ShowHelp() { OpenReading(Reading.Help); }
        public void ShowDetails()
        {
            if (exploration && exploration.HasSelectedClue) OpenReading(Reading.Details);
        }
        void OpenReading(Reading value)
        {
            if (!UseProgressiveLayout) return;
            if (!IsReading) readingFromExploration = IsExploring;
            reading = value;
            if (palette) palette.SetAlternatePageVisible(true);
            SetOriginal(false);
            if (page) page.SetActive(true);
            RefreshView();
        }
        public void CloseReading()
        {
            if (!IsReading) return;
            bool restoreExploration = readingFromExploration;
            reading = Reading.None;
            // Restoring the page never restarts or rewinds a demonstration.
            if (restoreExploration) ShowExploration(); else ShowInspection();
        }
        public void ToggleReduceMotion()
        {
            if (!palette || !palette.Controller) return;
            palette.Controller.ReduceMotion = !palette.Controller.ReduceMotion;
            lastReduceMotion = palette.Controller.ReduceMotion;
            RefreshView();
        }
        public void ToggleSound()
        {
            if(!palette || !palette.Controller)return;
            palette.Controller.SoundEnabled=!palette.Controller.SoundEnabled;
            lastSound=palette.Controller.SoundEnabled;RefreshView();
        }
        public bool IsCommandSelected(Command command)
        {
            if (!UseProgressiveLayout) return false;
            switch (command)
            {
                case Command.Inspect: return !IsReading && !IsExploring;
                case Command.Explore: return !IsReading && IsExploring;
                case Command.Help: return reading == Reading.Help;
                case Command.Details: return reading == Reading.Details;
                case Command.Air: return exploration.HasSelectedClue && exploration.SelectedClue == HelmetExplorationController.Clue.Air;
                case Command.Voice: return exploration.HasSelectedClue && exploration.SelectedClue == HelmetExplorationController.Clue.Voice;
                case Command.More: return moreExpanded;
                case Command.InspectMore: return inspectionToolsExpanded;
                default: return false;
            }
        }

        void SetOriginal(bool active)
        {
            foreach (var item in originalContents)
            {
                if (!item) continue;
                bool show = active;
                if (item.name == "Section options") show &= palette && palette.Controller && palette.Controller.IsCutaway;
                item.SetActive(show);
            }
        }
        void CacheControls()
        {
            if (buttons != null) return;
            buttons = new ButtonSlot[Enum.GetValues(typeof(Command)).Length];
            foreach (var button in GetComponentsInChildren<HelmetExplorationButton>(true))
            {
                var labelTransform = button.transform.parent.Find(button.name + " label");
                buttons[(int)button.Command] = new ButtonSlot {
                    button = button.gameObject, label = labelTransform ? labelTransform.GetComponent<TextMeshPro>() : null
                };
            }
            inspectionButtons = new ButtonSlot[InspectionNames.Length];
            var section = transform.Find("Section options");
            for (int i = 0; i < inspectionButtons.Length; i++)
            {
                var parent = i < 8 ? transform : section;
                if (!parent) continue;
                var button = parent.Find("Inspection " + InspectionNames[i]);
                var label = parent.Find(InspectionNames[i] + " label");
                inspectionButtons[i] = new ButtonSlot {
                    button = button ? button.gameObject : null, label = label ? label.GetComponent<TextMeshPro>() : null
                };
            }
            inspectionTitle = FindText("INSPECTION label");
            inspectionSubtitle = FindText("Follows your position / stays still while aiming label");
            inspectionStatus = FindText("GRIP TO SELECT label");
        }
        TextMeshPro FindText(string name)
        {
            // The original subtitle has a literal '/' in its object name.
            // Transform.Find would interpret that character as a hierarchy path.
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == name) return child.GetComponent<TextMeshPro>();
            }
            return null;
        }
        void HideAddedControls()
        {
            if (!progressiveLayout) return;
            CacheControls();
            for (int i = (int)Command.More; i < buttons.Length; i++) buttons[i]?.Show(false);
        }
        void Place(Command command, string caption, float x, float y, float width = .222f)
        {
            var slot = buttons[(int)command];
            slot?.Place(caption, x, y, width);
            if (slot != null && slot.label)
                slot.label.fontStyle = IsCommandSelected(command) ? FontStyles.Underline : FontStyles.Normal;
        }

        public void RefreshView()
        {
            ShowBackground(!UseProgressiveLayout || !exploration.IsDiagramVisible);
            if (!exploration) return;
            if (!UseProgressiveLayout)
            {
                if (title) MuseumText.Set(title, exploration.Title);
                if (body) MuseumText.Set(body, exploration.Body);
                if (progress) MuseumText.Set(progress, "AIR: " + (exploration.AirSeen ? "seen" : "not seen") +
                    "   /   VOICE: " + (exploration.VoiceSeen ? "seen" : "not seen"));
                if (footer) MuseumText.Set(footer, exploration.Footer);
                FitLegacyText(title, .8f, true); FitLegacyText(body, .52f, true);
                FitLegacyText(progress, .44f, false); FitLegacyText(footer, .44f, true);
                return;
            }
            CacheControls();
            if (lastPage != exploration.CurrentPage) moreExpanded = false;
            lastPage = exploration.CurrentPage;
            foreach (var button in buttons) if (button != null) button.wanted = false;
            if (tabs) tabs.SetActive(true);
            Place(Command.Inspect, "Inspect", -.157f, .36f, .148f);
            Place(Command.Explore, "Explore", 0f, .36f, .148f);
            Place(Command.Help, "Help", .157f, .36f, .148f);
            if (IsReading) LayoutReading();
            else if (IsExploring) LayoutExploration();
            else LayoutInspection();
            // Apply visibility once. Unchanged controls retain XRI hover/selection and
            // continue freezing the existing palette while playback phases change.
            foreach (var button in buttons) if (button != null) button.Show(button.wanted);
        }
        void LayoutInspection()
        {
            // The existing Stop object lives on the shared page. Keep just that control
            // available during inspection while a model is visible, without opening a reading page.
            bool showHideDemo = exploration.IsDiagramVisible;
            if (page) page.SetActive(showHideDemo);
            if (title) title.gameObject.SetActive(false);
            if (body) body.gameObject.SetActive(false);
            if (progress) progress.gameObject.SetActive(false);
            if (footer) footer.gameObject.SetActive(false);
            if (inspectionSubtitle) inspectionSubtitle.gameObject.SetActive(false);
            if (inspectionTitle) LayoutText(inspectionTitle, "Inspect", .285f, .05f, .4f, false);
            foreach (var button in inspectionButtons) if (button != null) button.wanted = false;
            for (int i = 0; i < 4; i++) PlaceInspection(i, i % 2 == 0 ? -.12f : .12f, .21f - (i / 2) * Row);
            float y = .08f;
            bool cutaway = palette && palette.Controller && palette.Controller.IsCutaway;
            if (cutaway)
            { PlaceInspection(8, -.12f, y); PlaceInspection(9, .12f, y); y -= Row; }
            Place(Command.InspectMore, inspectionToolsExpanded ? "Fewer tools" : "More tools", 0, y); y -= Row;
            if (inspectionToolsExpanded)
            {
                for (int i = 4; i < 8; i += 2)
                { PlaceInspection(i, -.12f, y); PlaceInspection(i + 1, .12f, y); y -= Row; }
                if (cutaway)
                    for (int i = 10; i < 14; i += 2)
                    { PlaceInspection(i, -.12f, y); PlaceInspection(i + 1, .12f, y); y -= Row; }
            }
            if (showHideDemo)
            { Place(Command.Stop, "Hide demo", 0, y); y -= Row; }
            if (inspectionStatus) LayoutText(inspectionStatus, "Point + G to select", y + .017f, .03f, .22f, false);
            foreach (var button in inspectionButtons) if (button != null) button.Show(button.wanted);
            SetBounds(y - .016f);
        }
        void PlaceInspection(int index, float x, float y)
        { inspectionButtons[index]?.Place(InspectionCaptions[index], x, y); }

        void LayoutExploration()
        {
            if (progress) progress.gameObject.SetActive(false);
            LayoutText(title, exploration.Title, .285f, .05f, .4f, false);
            bool welcome = exploration.CurrentPage == HelmetExplorationController.Page.Welcome;
            bool clue = exploration.CurrentPage == HelmetExplorationController.Page.Air || exploration.CurrentPage == HelmetExplorationController.Page.Voice;
            float y;
            if (welcome)
            {
                if (body) body.gameObject.SetActive(false);
                Place(Command.Air, "Air", -.12f, .21f); Place(Command.Voice, "Voice", .12f, .21f);
                y = .145f;
            }
            else y = LayoutBody(exploration.Body, .242f) - .04f;
            if (clue)
            {
                if (exploration.IsPlaying)
                { Place(Command.Stop, "Hide demo", -.12f, y); Place(Command.Details, "Details", .12f, y); y -= Row; }
                else
                {
                    Place(Command.Replay, "Replay", -.12f, y);
                    if (exploration.IsDiagramVisible)
                    {
                        Place(Command.Stop, "Hide demo", .12f, y); y -= Row;
                        Place(Command.Details, "Details", 0, y);
                    }
                    else Place(Command.Details, "Details", .12f, y);
                    y -= Row;
                }
            }
            if (exploration.CurrentPage == HelmetExplorationController.Page.Review)
            { Place(Command.NewVisit, "New visit", 0, y); y -= Row; }
            Place(Command.More, moreExpanded ? "Less" : "More", welcome ? 0f : -.12f, y);
            if (!welcome) Place(Command.Back, "Back", .12f, y);
            y -= Row;
            if (moreExpanded)
            { Place(Command.Hint, "Hint", -.12f, y); Place(Command.Review, "Review", .12f, y); y -= Row; }
            bool showFooter = clue && !string.IsNullOrEmpty(exploration.Footer);
            if (footer)
            {
                footer.gameObject.SetActive(showFooter);
                if (showFooter) LayoutText(footer, exploration.Footer, y + .017f, .03f, .22f, false);
            }
            SetBounds(showFooter ? y - .016f : y + .021f);
        }
        void LayoutReading()
        {
            if (progress) progress.gameObject.SetActive(false);
            if (footer) footer.gameObject.SetActive(false);
            bool help = reading == Reading.Help;
            LayoutText(title, help ? "Help" : "About this model", .285f, .05f, .4f, false);
            string content = help
                ? "Click Game to focus.\nT / Y: choose a controller.\nPoint + G: select a button.\nHold G: grab; release to let go.\nHome: reset the helmet."
                : DetailsText();
            float y = LayoutBody(content, .242f) - .04f;
            if (help)
            {
                bool reduced = palette && palette.Controller && palette.Controller.ReduceMotion;
                Place(Command.ReduceMotion, reduced ? "Motion: off" : "Motion: on", -.12f, y);
                bool sound=palette && palette.Controller && palette.Controller.SoundEnabled;
                Place(Command.Sound, sound ? "Sound: on" : "Sound: off", .12f, y); y -= Row;
            }
            bool visible = exploration.IsDiagramVisible;
            Place(Command.CloseReading, "Return", visible ? -.12f : 0f, y);
            if (visible) Place(Command.Stop, "Hide demo", .12f, y);
            SetBounds(y - .044f);
        }
        string DetailsText()
        {
            const string boundary = "Equipment types, simplified.\nNot an exact matched rig.\nNo helmet interior is reconstructed.\n\n";
            return boundary + (exploration.SelectedClue == HelmetExplorationController.Clue.Air
                ? "Sources: Naval Undersea Museum, MK V\nRoyal Museums Greenwich, EQA0617\nScience Museum Group, 1963-71"
                : "Sources: Naval Undersea Museum, MK V\nRoyal Museums Greenwich, ZBA6671");
        }
        float LayoutBody(string value, float top)
        {
            if (!body) return top - .07f;
            body.gameObject.SetActive(true); MuseumText.Set(body, value);
            SetFont(body, .28f, true);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.rectTransform.pivot = new Vector2(.5f, .5f);
            body.rectTransform.sizeDelta = new Vector2(.436f, 1f);
            body.ForceMeshUpdate(true);
            float height = Mathf.Max(.05f, body.GetPreferredValues(body.text, .436f, Mathf.Infinity).y + .012f);
            body.rectTransform.sizeDelta = new Vector2(.436f, height);
            body.transform.localPosition = new Vector3(0, top - height * .5f, -.043f);
            return top - height;
        }
        static void LayoutText(TextMeshPro text, string value, float y, float height, float font, bool wrap)
        {
            if (!text) return;
            text.gameObject.SetActive(true); MuseumText.Set(text, value);
            text.rectTransform.pivot = new Vector2(.5f, .5f);
            text.rectTransform.sizeDelta = new Vector2(.436f, height);
            text.transform.localPosition = new Vector3(0, y, -.043f);
            text.alignment = TextAlignmentOptions.Center;
            SetFont(text, font, wrap);
        }
        void SetBounds(float bottom) { if (palette) palette.ConfigureContentBounds(bottom, Top); }
        static void SetFont(TextMeshPro text, float size, bool wrap)
        {
            text.enableAutoSizing = false; text.fontSize = size; text.enableWordWrapping = wrap;
        }
        static void FitLegacyText(TextMeshPro text, float maximum, bool wrap)
        {
            if (!text) return;
            text.enableAutoSizing = true; text.fontSizeMin = .02f; text.fontSizeMax = maximum;
            text.enableWordWrapping = wrap; text.ForceMeshUpdate(true);
        }
    }
}
