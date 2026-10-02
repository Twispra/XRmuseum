using TMPro;
using UnityEngine;
using Museum.Inspection;

namespace Museum.Exploration
{
    /// <summary>Two pages on the existing personal palette; no second following panel.</summary>
    [DisallowMultipleComponent]
    public sealed class HelmetExplorationPanel : MonoBehaviour
    {
        [SerializeField] HelmetExplorationController exploration;
        [SerializeField] HelmetInspectionPalette palette;
        [SerializeField] GameObject page;
        [SerializeField] GameObject tabs;
        [SerializeField] GameObject[] originalContents = new GameObject[0];
        [SerializeField] TextMeshPro title, body, progress, footer;
        bool subscribed;
        bool availabilityKnown, lastAvailable;

        public bool IsExploring { get; private set; }
        public HelmetExplorationController Exploration => exploration;

        public void Configure(HelmetExplorationController target, HelmetInspectionPalette follower,
            GameObject pageRoot, GameObject tabRoot, GameObject[] original,
            TextMeshPro titleText, TextMeshPro bodyText, TextMeshPro progressText, TextMeshPro footerText)
        {
            Unsubscribe();
            exploration = target; palette = follower; page = pageRoot; tabs = tabRoot;
            originalContents = original; title = titleText; body = bodyText;
            progress = progressText; footer = footerText;
            if (palette) palette.ConfigureAdditionalPage(.085f);
            if (isActiveAndEnabled) Subscribe();
            ShowInspection();
            UpdateAvailability();
        }

        void OnEnable()
        {
            Subscribe();
            availabilityKnown = false;
            UpdateAvailability();
        }
        void Start() { ShowInspection(); }
        void Update() { UpdateAvailability(); }
        void UpdateAvailability()
        {
            bool available = isActiveAndEnabled && exploration && exploration.isActiveAndEnabled;
            if (availabilityKnown && lastAvailable == available) return;
            availabilityKnown = true; lastAvailable = available;
            if (!available) ShowInspection();
            if (tabs) tabs.SetActive(available);
            if (palette) palette.ConfigureAdditionalPage(available ? .085f : 0f);
        }
        void OnDisable()
        {
            Unsubscribe();
            ShowInspection();
            if (tabs) tabs.SetActive(false);
            if (palette) palette.ConfigureAdditionalPage(0f);
            if (exploration) exploration.Stop();
        }
        void Subscribe()
        {
            if (!exploration || subscribed) return;
            exploration.Changed += RefreshView;
            exploration.ClueOpened += OnClue;
            subscribed = true;
        }
        void Unsubscribe()
        {
            if (exploration && subscribed)
            {
                exploration.Changed -= RefreshView;
                exploration.ClueOpened -= OnClue;
            }
            subscribed = false;
        }
        void OnClue(HelmetExplorationController.Clue clue) { ShowExploration(); }

        public void ShowExploration()
        {
            if (!isActiveAndEnabled || !exploration || !exploration.isActiveAndEnabled) return;
            IsExploring = true;
            SetOriginal(false);
            if (page) page.SetActive(true);
            if (palette) palette.SetAlternatePageVisible(true);
            RefreshView();
        }
        public void ShowInspection()
        {
            IsExploring = false;
            if (page) page.SetActive(false);
            SetOriginal(true);
            if (palette) palette.SetAlternatePageVisible(false);
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
        public void RefreshView()
        {
            if (!exploration) return;
            if (title) title.text = exploration.Title;
            if (body) body.text = exploration.Body;
            if (progress) progress.text = "AIR: " + (exploration.AirSeen ? "viewed" : "unexplored") +
                "   /   VOICE: " + (exploration.VoiceSeen ? "viewed" : "unexplored");
            if (footer) footer.text = exploration.Footer;
            FitText(title, .8f, true);
            FitText(body, .52f, true);
            FitText(progress, .44f, false);
            FitText(footer, .44f, true);
        }
        static void FitText(TextMeshPro text, float maximum, bool wrap)
        {
            if (!text) return;
            // TMP uses world-space font units here. A large minimum prevents autosizing
            // and lets text spill into neighbouring controls even though the rect is correct.
            text.enableAutoSizing = true; text.fontSizeMin = .02f; text.fontSizeMax = maximum;
            text.enableWordWrapping = wrap;
            text.ForceMeshUpdate(true);
        }
    }
}
