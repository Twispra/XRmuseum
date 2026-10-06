using Museum.Hotspots;
using TMPro;
using UnityEngine;

namespace Museum.Localization
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HelmetHotspotTour))]
    public sealed class HelmetHotspotLocalization : MonoBehaviour
    {
        [SerializeField] LocalizationTable table;
        [SerializeField] LanguageChannel channel;
        HelmetHotspotTour tour;

        public void Configure(LocalizationTable sourceTable, LanguageChannel sourceChannel)
        {
            table = sourceTable;
            channel = sourceChannel;
        }

        void OnEnable()
        {
            tour = GetComponent<HelmetHotspotTour>();
            tour.TextChanged += OnTextChanged;
            tour.RefreshText();
        }

        void OnDisable()
        {
            if (tour) tour.TextChanged -= OnTextChanged;
            tour = null;
        }

        void OnTextChanged(TMP_Text label, string english)
        {
            if (!label) return;
            if (MuseumText.Table && MuseumText.Channel)
            {
                MuseumText.Set(label, english);
                return;
            }

            // Saved references also support opening the independent prefab without a scene root.
            // With no localization source, retain the tour's English fallback and any saved source binding.
            if (!table || !channel) return;
            var binding = label.GetComponent<LocalizedSourceText>();
            if (!binding) binding = label.gameObject.AddComponent<LocalizedSourceText>();
            binding.Configure(table, channel, english);
        }
    }
}
