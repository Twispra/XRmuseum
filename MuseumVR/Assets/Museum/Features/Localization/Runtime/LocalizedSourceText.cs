using TMPro;
using UnityEngine;

namespace Museum.Localization
{
    /// <summary>A label with a changing source (page, animation phase or state), rather than a fixed table key.</summary>
    [DisallowMultipleComponent]
    public sealed class LocalizedSourceText : MonoBehaviour
    {
        [SerializeField] LocalizationTable table;
        [SerializeField] LanguageChannel channel;
        [SerializeField, TextArea] string sourceEnglish;
        [SerializeField] bool firstLineOnly;
        TMP_Text label;
        LanguageChannel subscribed;
        string rendered;
        public string SourceEnglish => sourceEnglish;
        void OnEnable() { Subscribe(); Refresh(); }
        void OnDisable() { Unsubscribe(); }
        public void Configure(LocalizationTable sourceTable, LanguageChannel sourceChannel, string english, bool headingOnly = false)
        {
            if (subscribed != sourceChannel) Unsubscribe();
            table = sourceTable; channel = sourceChannel; sourceEnglish = english; firstLineOnly = headingOnly;
            if (isActiveAndEnabled) Subscribe();
            Refresh();
        }
        void Subscribe()
        {
            if (!channel || subscribed == channel) return;
            subscribed = channel; subscribed.LanguageChanged += OnLanguageChanged;
        }
        void Unsubscribe() { if (subscribed) subscribed.LanguageChanged -= OnLanguageChanged; subscribed = null; }
        void OnLanguageChanged(MuseumLanguage _) => Refresh();
        public string TextFor(MuseumLanguage language) =>
            DisplayText(table ? table.TranslateEnglish(sourceEnglish, language) : sourceEnglish, firstLineOnly);

        internal static string DisplayText(string source, bool headingOnly)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            int newline = headingOnly ? source.IndexOf('\n') : -1;
            return newline < 0 ? source : source.Substring(0, newline).TrimEnd();
        }

        public void Refresh()
        {
            if (!label) label = GetComponent<TMP_Text>();
            if (!label || !table || !channel) return;
            var next = TextFor(channel.Current);
            if (table.FontAsset && label.font != table.FontAsset) label.font = table.FontAsset;
            if (next == rendered && label.text == next) return;
            rendered = next; label.text = next;
            // Layout owners compute their bounds after this write; no per-frame scanning or forced visibility.
        }
    }
}
