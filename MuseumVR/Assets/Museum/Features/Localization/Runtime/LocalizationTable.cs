using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Museum.Localization
{
    /// <summary>
    /// 一段可切换语言的界面文字。
    ///
    /// 用字符串 key 而不是「按对象逐个填两段文字」，是因为展品会增删、面板会被复用到别的展项上；
    /// key 让内容（翻译）与摆放（场景里的文字对象）彼此独立。
    /// </summary>
    [CreateAssetMenu(menuName = "Museum/Localization/Localization Table", fileName = "LocalizationTable")]
    public sealed class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] string key;
            [SerializeField] string chinese;
            [SerializeField] string english;
            [SerializeField] string traditionalChinese;
            [SerializeField] string note;

            public string Key => key;
            public string Chinese => chinese;
            public string English => english;
            public string TraditionalChinese => traditionalChinese;
            public string Note => note;

            /// <summary>取该语言的文案。未知语言返回空串而不是乱猜。</summary>
            public string For(MuseumLanguage language)
            {
                return language switch
                {
                    MuseumLanguage.Chinese => chinese,
                    MuseumLanguage.English => english,
                    MuseumLanguage.TraditionalChinese => traditionalChinese,
                    _ => string.Empty,
                };
            }
        }

        [Tooltip("所有可切换的界面文字。key 建议用英文小写加下划线，例如 welcome_title。")]
        [SerializeField] List<Entry> entries = new List<Entry>();

        [Tooltip("显示中文所需的字体。留空则沿用文字对象原本的字体（中文会缺字）。")]
        [SerializeField] TMP_FontAsset fontAsset;

        // 运行时查找表。用 [NonSerialized] 保证不写回资产文件。
        [NonSerialized] Dictionary<string, int> index;
        [NonSerialized] Dictionary<string, int> englishIndex;

        public TMP_FontAsset FontAsset => fontAsset;
        public IReadOnlyList<Entry> Entries => entries;

        void OnEnable()
        {
            // 编辑器里改过资产后要重建索引，否则会用到旧的 key 列表。
            index = null; englishIndex = null;
        }

        void OnValidate() { index = null; englishIndex = null; }

        public bool ContainsEnglish(string source)
        {
            EnsureIndex();
            return !string.IsNullOrEmpty(source) && englishIndex.ContainsKey(source);
        }

        /// <summary>
        /// 取文案。**找不到 key 时返回醒目的红色标记而不是空串**——
        /// 静默返回空会让「漏了一条翻译」看起来像「设计上就没有这段字」。
        /// 标记刻意只用 ASCII 字符，保证任何字体子集里都有字形。
        /// </summary>
        public string Get(string key, MuseumLanguage language)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (!TryGet(key, language, out var text)) return $"<color=#FF5555>[[{key}]]</color>";
            return text;
        }

        public bool TryGet(string key, MuseumLanguage language, out string text)
        {
            text = string.Empty;
            if (string.IsNullOrEmpty(key)) return false;

            EnsureIndex();
            if (!index.TryGetValue(key, out int slot)) return false;
            text = entries[slot].For(language);
            return !string.IsNullOrEmpty(text);
        }

        public string TranslateEnglish(string english, MuseumLanguage language)
        {
            if (string.IsNullOrEmpty(english) || language == MuseumLanguage.English) return english ?? "";
            EnsureIndex();
            if (englishIndex.TryGetValue(english, out int slot))
            {
                var translated = entries[slot].For(language);
                return string.IsNullOrEmpty(translated) ? english : translated;
            }
            // Numeric exhibit IDs and historical proper names remain literal; known sentences use full entries.
            return english;
        }

        /// <summary>key 是否登记在表里（不要求该语言一定有译文）。</summary>
        public bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            EnsureIndex();
            return index.ContainsKey(key);
        }

        void EnsureIndex()
        {
            if (index != null) return;

            index = new Dictionary<string, int>(entries.Count, StringComparer.Ordinal);
            englishIndex = new Dictionary<string, int>(entries.Count, StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                var entryKey = entries[i].Key;
                if (string.IsNullOrEmpty(entryKey)) continue;
                if (index.ContainsKey(entryKey)) continue; // 重复 key 由校验工具报出，这里保留第一条
                index.Add(entryKey, i);
                if (!string.IsNullOrEmpty(entries[i].English) && !englishIndex.ContainsKey(entries[i].English))
                    englishIndex.Add(entries[i].English, i);
            }
        }

        /// <summary>把表里所有文案拼成一个字符串，供缺字检查使用。</summary>
        public string CollectAllCharacters()
        {
            var builder = new System.Text.StringBuilder();
            foreach (var entry in entries)
            {
                builder.Append(entry.Chinese);
                builder.Append(entry.English);
                builder.Append(entry.TraditionalChinese);
            }
            return builder.ToString();
        }
    }
}
