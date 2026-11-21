using System;
using Museum.Localization;
using Museum.Localization.EditorTools;
using UnityEditor;

namespace Museum.Navigation.Editor
{
    public static class GalleryNavigationSetup
    {
        public static void PrepareNavigationText()
        {
            TrilingualFontBaker.RunBatch();
            SyncTranslations();
        }

        // Only synchronize the authorized text asset. Do not rebuild scenes or fonts.
        public static void SyncTranslations()
        {
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset");
            if (!table || !table.FontAsset) throw new InvalidOperationException("Saved localization table/font missing.");
            var serialized = new SerializedObject(table);
            var entries = serialized.FindProperty("entries");
            foreach (var copy in TrilingualMuseumSetup.ReadCopy().entries)
            {
                if (!copy.key.StartsWith("navigation.", StringComparison.Ordinal)) continue;
                int index = -1;
                for (int i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue == copy.key) { index = i; break; }
                if (index < 0) { index = entries.arraySize; entries.arraySize++; }
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("key").stringValue = copy.key;
                entry.FindPropertyRelative("english").stringValue = copy.english;
                entry.FindPropertyRelative("chinese").stringValue = copy.chinese;
                entry.FindPropertyRelative("traditionalChinese").stringValue = copy.traditionalChinese;
                entry.FindPropertyRelative("note").stringValue = copy.note;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var entry in table.Entries)
                if (entry.Key.StartsWith("navigation.", StringComparison.Ordinal))
                    foreach (MuseumLanguage language in Enum.GetValues(typeof(MuseumLanguage)))
                        foreach (var character in entry.For(language))
                            if (!char.IsWhiteSpace(character) && !table.FontAsset.HasCharacter(character, false, false))
                                throw new InvalidOperationException("Unbaked navigation glyph: " + character);
            AssetDatabase.SaveAssetIfDirty(table);
        }
    }
}
