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

        [Tooltip("下一个年代。")]
        [SerializeField] KeyCode nextKey = KeyCode.N;

        [Tooltip("上一个年代。")]
        [SerializeField] KeyCode previousKey = KeyCode.P;

        [Tooltip("数字键 1-9 直接跳到对应年代序号。")]
        [SerializeField] bool enableNumberKeys = true;

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
