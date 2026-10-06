using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Localization
{
    /// <summary>
    /// 语言选择按钮。既可以由 XR 射线/近距离抓取触发，也可以被脚本直接调用。
    ///
    /// 场景里完全不需要有头显也能测试：键盘预览或无头显成员都可以调用
    /// <see cref="TriggerProgrammatically"/>，走的是同一条切换逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Museum/Localization/Language Button")]
    public sealed class LanguageButton : MonoBehaviour
    {
        public enum Choice
        {
            /// <summary>切到简体中文。</summary>
            Chinese = 0,

            /// <summary>切到英文。</summary>
            English = 1,

            /// <summary>在中英之间来回切，用于只有一个按钮的简化面板。</summary>
            Toggle = 2,
            TraditionalChinese = 3,
        }

        [Header("行为")]
        [SerializeField] LanguageChannel channel;
        [SerializeField] Choice choice = Choice.Chinese;

        [Header("可选反馈")]
        [Tooltip("按下时短暂替换材质的渲染器，用于给触觉缺席时的视觉反馈。")]
        [SerializeField] Renderer feedbackRenderer;
        [SerializeField] Material pressedMaterial;
        [SerializeField] float pressedDuration = 0.12f;

        [Header("可选标签")]
        [Tooltip("按钮上的文字。设为该语言的原生名称（中文 / English），不随语言切换变化。")]
        [SerializeField] TMP_Text label;

        XRSimpleInteractable interactable;
        Material originalMaterial;
        Coroutine flash;

        public Choice Action => choice;

        void Awake()
        {
            if (feedbackRenderer != null) originalMaterial = feedbackRenderer.sharedMaterial;

            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable == null) interactable = gameObject.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);

            ApplyLabel();
        }

        void OnDestroy()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(OnSelected);
        }

        /// <summary>接好引用、贴上标签文字。Editor 生成器与整合人都用这个入口。</summary>
        public void Configure(LanguageChannel sourceChannel, Choice action,
            Renderer renderer = null, Material pressed = null, TMP_Text buttonLabel = null)
        {
            channel = sourceChannel;
            choice = action;
            feedbackRenderer = renderer;
            pressedMaterial = pressed;
            label = buttonLabel;
            if (feedbackRenderer != null) originalMaterial = feedbackRenderer.sharedMaterial;
            ApplyLabel();
        }

        /// <summary>等同于按下按钮。键盘预览与自动测试都走这里。</summary>
        public void TriggerProgrammatically()
        {
            if (channel == null)
            {
                Debug.LogError($"[LanguageButton] {name} 没有指定 LanguageChannel。", this);
                return;
            }

            switch (choice)
            {
                case Choice.Chinese:
                    channel.SetLanguage(MuseumLanguage.Chinese);
                    break;
                case Choice.TraditionalChinese:
                    channel.SetLanguage(MuseumLanguage.TraditionalChinese);
                    break;
                case Choice.English:
                    channel.SetLanguage(MuseumLanguage.English);
                    break;
                default:
                    channel.Toggle();
                    break;
            }

            StartFlash();
        }

        void OnSelected(SelectEnterEventArgs args)
        {
            TriggerProgrammatically();
        }

        void ApplyLabel()
        {
            if (label == null) return;
            label.text = choice switch
            {
                Choice.Chinese => MuseumLanguage.Chinese.NativeName(),
                Choice.English => MuseumLanguage.English.NativeName(),
                Choice.TraditionalChinese => MuseumLanguage.TraditionalChinese.NativeName(),
                _ => "简 / 繁 / EN",
            };
            label.ForceMeshUpdate();
        }

        void StartFlash()
        {
            if (feedbackRenderer == null || pressedMaterial == null) return;
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            feedbackRenderer.sharedMaterial = pressedMaterial;
            yield return new WaitForSeconds(Mathf.Max(0.01f, pressedDuration));
            feedbackRenderer.sharedMaterial = originalMaterial;
            flash = null;
        }
    }
}
