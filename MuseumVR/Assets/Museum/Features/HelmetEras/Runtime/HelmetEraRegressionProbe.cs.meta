using UnityEngine;

namespace Museum.Eras
{
    /// <summary>
    /// Headset-free preview of the era switch.
    /// Lets a reviewer step the catalogue from the keyboard, so a member without a Quest
    /// headset can still see the exhibit change instead of taking the code on trust.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelmetEraPlayModePreview : MonoBehaviour
    {
        [Tooltip("要驱动的切换器。留空时自动从自身及子物体里查找。")]
        [SerializeField] HelmetEraSwitcher switcher;

        [Tooltip("开启键盘预览。正式演示时可关闭，避免误触。")]
        [SerializeField] bool enableKeyboardPreview = true;

        [Tooltip("下一个年代。F9 —— 复审确认 N/P 与 XR 模拟器冲突，数字键被指认易误触，均已停用。")]
        [SerializeField] KeyCode nextKey = KeyCode.F9;

        [Tooltip("上一个年代。F10。")]
        [SerializeField] KeyCode previousKey = KeyCode.F10;

        [Tooltip("数字键 1-9 直接跳年代。默认关闭：数字键与 XR 模拟器的输入目标切换冲突（PR #27 复审第 6 条）。")]
        [SerializeField] bool enableNumberKeys = false;

        void Awake()
        {
            if (switcher == null) switcher = GetComponentInChildren<HelmetEraSwitcher>(true);
        }

        void Update()
        {
            if (!enableKeyboardPreview || switcher == null) return;

            if (Input.GetKeyDown(nextKey))
            {
                switcher.ShowNextEra();
                return;
            }

            if (Input.GetKeyDown(previousKey))
            {
                switcher.ShowPreviousEra();
                return;
            }

            if (!enableNumberKeys) return;

            // 数字键直接跳转，方便截图对比同一个年代。
            for (int i = 0; i < 9; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                switcher.ShowEra(i);
                return;
            }
        }

        /// <summary>Wires the preview from an Editor builder or the scene setup script.</summary>
        public void Configure(HelmetEraSwitcher target, bool keyboard = true)
        {
            switcher = target;
            enableKeyboardPreview = keyboard;
        }

        public HelmetEraSwitcher Switcher => switcher;
    }
}
