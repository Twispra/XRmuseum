using UnityEngine;

namespace Museum.Localization
{
    /// <summary>
    /// 没有头显时的键盘预览。
    ///
    /// 组内有人没有 Quest，搭一套 XR Origin + 模拟器只为看一眼文字切换并不划算。
    /// 键盘路径与按钮走的是同一个语言通道，所以它能验证「切换 → 刷新」这条链路；
    /// 它验证不了射线交互本身，那需要真机。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Museum/Localization/Language Play Mode Preview")]
    public sealed class LanguagePlayModePreview : MonoBehaviour
    {
        [Tooltip("要驱动的语言通道。")]
        [SerializeField] LanguageChannel channel;

        [Tooltip("开启键盘预览。正式演示时可关闭，避免误触。")]
        [SerializeField] bool enableKeyboardPreview = true;

        [Tooltip("循环切换中英。")]
        [SerializeField] KeyCode toggleKey = KeyCode.L;

        [Tooltip("数字键 1 / 2 直接选中文 / 英文，方便截图对比同一个界面。")]
        [SerializeField] bool enableNumberKeys = true;

        public LanguageChannel Channel => channel;

        /// <summary>由 Editor 生成器接手装配。</summary>
        public void Configure(LanguageChannel sourceChannel, bool keyboard = true)
        {
            channel = sourceChannel;
            enableKeyboardPreview = keyboard;
        }

        void Update()
        {
            if (!enableKeyboardPreview || channel == null) return;

            if (Input.GetKeyDown(toggleKey))
            {
                channel.Toggle();
                return;
            }

            if (!enableNumberKeys) return;

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                channel.SetLanguage(MuseumLanguage.Chinese);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                channel.SetLanguage(MuseumLanguage.English);
            }
        }
    }
}
