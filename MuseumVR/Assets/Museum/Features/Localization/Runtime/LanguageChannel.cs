using System;
using UnityEngine;

namespace Museum.Localization
{
    /// <summary>
    /// 语言的唯一权威来源，并负责广播变化。
    ///
    /// 用资产（ScriptableObject）而不是场景里的单例，是为了让「谁都能读、谁都能切」
    /// 而不需要互相引用。文字对象订阅事件即可，语言按钮只负责调用 SetLanguage。
    /// 这样以后加语言、加面板都不需要改已有代码。
    /// </summary>
    [CreateAssetMenu(menuName = "Museum/Localization/Language Channel", fileName = "LanguageChannel")]
    public sealed class LanguageChannel : ScriptableObject
    {
        [Tooltip("进入 Play 时的初始语言。")]
        [SerializeField] MuseumLanguage initial = MuseumLanguage.English;

        // 运行期状态不写回资产，避免把「上次跑测试时切成了英文」提交上去。
        [NonSerialized] MuseumLanguage current;
        [NonSerialized] bool initialised;

        /// <summary>语言变化时触发。订阅者必须在 OnDisable 里退订。</summary>
        public event Action<MuseumLanguage> LanguageChanged;

        public MuseumLanguage Current
        {
            get
            {
                EnsureInitialised();
                return current;
            }
        }

        public MuseumLanguage Initial => initial;

        public void SetLanguage(MuseumLanguage language)
        {
            if (!Enum.IsDefined(typeof(MuseumLanguage), language)) return;
            EnsureInitialised();
            if (current == language) return;

            current = language;
            LanguageChanged?.Invoke(current);
        }

        public void Toggle()
        {
            SetLanguage(Current == MuseumLanguage.English ? MuseumLanguage.Chinese
                : Current == MuseumLanguage.Chinese ? MuseumLanguage.TraditionalChinese : MuseumLanguage.English);
        }

        void EnsureInitialised()
        {
            if (initialised) return;
            current = initial;
            initialised = true;
        }

        void OnDisable()
        {
            // 编辑器里退出 Play 时 ScriptableObject 实例会保留，
            // 不清空事件的话下次进入 Play 会带着上一轮已销毁对象的订阅。
            LanguageChanged = null;
            initialised = false;
        }
    }
}
