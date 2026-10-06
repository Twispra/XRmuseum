using TMPro;
using UnityEngine;

namespace Museum.Localization
{
    /// <summary>Explicit write path for runtime captions; the English source remains available after switching languages.</summary>
    public static class MuseumText
    {
        public static LocalizationTable Table { get; private set; }
        public static LanguageChannel Channel { get; private set; }
        public static void Configure(LocalizationTable table, LanguageChannel channel) { Table = table; Channel = channel; }
        public static string Translate(string english)
        {
            if (!Table || !Channel || string.IsNullOrEmpty(english)) return english ?? "";
            return Table.TranslateEnglish(english, Channel.Current);
        }
        public static void Set(TMP_Text label, string english)
        {
            if (!label) return;
            if (!Table || !Channel) { label.text = english ?? ""; return; }
            var binding = label.GetComponent<LocalizedSourceText>();
            if (!binding) binding = label.gameObject.AddComponent<LocalizedSourceText>();
            binding.Configure(Table, Channel, english ?? "");
        }
    }
}
