using TMPro;
using UnityEngine;

namespace Museum.Localization
{
    /// <summary>
    /// 把一个文字对象绑定到表里的某个 key，并在语言变化时自动刷新。
    ///
    /// 故意做成「一个文字对象 = 一个 key」，而不是「一个面板 = 一串文字」：
    /// 展项会增删、面板会在不同展品间复用，逐对象绑定改动最小、冲突最少。
    /// 也故意同时兼容 TMP 的两种文字组件（世界空间的 TextMeshPro 与 UI 的 TextMeshProUGUI）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Museum/Localization/Localized Text")]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Header("数据来源")]
        [Tooltip("文案表。缺了它这个对象不会显示任何文字。")]
        [SerializeField] LocalizationTable table;

        [Tooltip("语言通道。缺了它文字不会随语言变化刷新。")]
        [SerializeField] LanguageChannel channel;

        [Header("这一段是什么")]
        [Tooltip("对应表里的 key。找不到时会在物体上显示红色标记，便于一眼发现漏翻。")]
        [SerializeField] string key = "";

        [Header("字体")]
        [Tooltip("勾选时自动套用表里指定的中文字体。英文为主的文字对象可以取消勾选。")]
        [SerializeField] bool applyTableFont = true;

        [Tooltip("key 缺失时在控制台提示。只在开发期需要，可关掉。")]
        [SerializeField] bool logMissingKeys = true;

        TMP_Text label;
        bool warned;

        /// <summary>当前绑定的 key。</summary>
        public string Key => key;

        void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            if (!Validate(true)) return;
            channel.LanguageChanged += ApplyLanguage;
            Apply(channel.Current);
        }

        void OnDisable()
        {
            if (channel != null) channel.LanguageChanged -= ApplyLanguage;
        }

        /// <summary>由 Editor 生成器或整合人调用，一次把三个引用都接好。</summary>
        public void Configure(LocalizationTable sourceTable, LanguageChannel sourceChannel, string textKey)
        {
            table = sourceTable;
            channel = sourceChannel;
            key = textKey;
            warned = false;
            Apply(channel != null ? channel.Current : MuseumLanguage.Chinese);
        }

        /// <summary>换一个 key，并立即刷新。</summary>
        public void SetKey(string textKey)
        {
            key = textKey;
            warned = false;
            Apply(channel != null ? channel.Current : MuseumLanguage.Chinese);
        }

        /// <summary>按当前语言重新取一次文案。表在运行期被改动时用得上。</summary>
        public void Refresh()
        {
            Apply(channel != null ? channel.Current : MuseumLanguage.Chinese);
        }

        void ApplyLanguage(MuseumLanguage language)
        {
            Apply(language);
        }

        void Apply(MuseumLanguage language)
        {
            if (!Validate(false)) return;

            if (applyTableFont && table.FontAsset != null && label.font != table.FontAsset)
                label.font = table.FontAsset;

            if (!table.ContainsKey(key))
            {
                // 换成醒目的标记：静默显示空串会让人以为「这段字本来就没有」。
                // 标记只用 ASCII，保证任何字体子集里都有字形。
                label.text = $"<color=#FF5555>[[{key}]]</color>";
                label.ForceMeshUpdate();
                WarnOnce($"key 不在表里：「{key}」（物体 {name}）");
                return;
            }

            label.text = table.Get(key, language);
            // 动态字体图集是懒生成的；不主动刷新的话首帧可能是空的。
            label.ForceMeshUpdate();
        }

        bool Validate(bool log)
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (label == null)
            {
                if (log) Debug.LogError($"[LocalizedText] {name} 上没有任何 TMP 文字组件（TextMeshPro 或 TextMeshProUGUI）。", this);
                return false;
            }

            if (table == null)
            {
                if (log) Debug.LogError($"[LocalizedText] {name} 没有指定 LocalizationTable。", this);
                return false;
            }

            if (channel == null)
            {
                if (log) Debug.LogError($"[LocalizedText] {name} 没有指定 LanguageChannel。", this);
                return false;
            }

            return true;
        }

        void WarnOnce(string message)
        {
            if (!logMissingKeys || warned) return;
            warned = true;
            Debug.LogWarning($"[LocalizedText] {message}", this);
        }
    }
}
