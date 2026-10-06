using UnityEngine;

namespace Museum.Localization
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MuseumLocalizationRoot : MonoBehaviour
    {
        [SerializeField] LocalizationTable table;
        [SerializeField] LanguageChannel channel;
        void OnEnable() => MuseumText.Configure(table, channel);
        void OnDisable() { if (MuseumText.Channel == channel) MuseumText.Configure(null, null); }
        public void Configure(LocalizationTable sourceTable, LanguageChannel sourceChannel)
        { table=sourceTable; channel=sourceChannel; if(isActiveAndEnabled) MuseumText.Configure(table,channel); }
    }
}
