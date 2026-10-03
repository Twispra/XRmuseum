using System;
using UnityEngine;

namespace Museum.Eras
{
    /// <summary>Which surface of the helmet a material belongs to. Shared by profiles and renderer bindings.</summary>
    public enum HelmetMaterialSlot
    {
        Shell,
        Window,
        Fitting,
        Breastplate,
    }

    /// <summary>One describable age of diving helmet. Data lives here so new eras need no code change.</summary>
    [CreateAssetMenu(menuName = "Museum/Helmet Era Profile", fileName = "HelmetEra")]
    public sealed class HelmetEraProfile : ScriptableObject
    {
        [Serializable]
        public struct DetailLine
        {
            [Tooltip("左侧短标签，例如 AIR、WINDOW、WEIGHT。")]
            public string label;

            [TextArea(1, 3)]
            [Tooltip("该条目的说明正文。")]
            public string text;
        }

        [Header("身份")]
        [Tooltip("切换时显示给观众的年代名称，例如 Mark V / SuperLite 17B。")]
        [SerializeField] string displayName = "未命名年代";

        [Tooltip("排序权重。列表与按钮顺序按此升序排列，便于把早期型号排在前面。")]
        [SerializeField] int sortOrder;

        [TextArea(2, 4)]
        [Tooltip("一句话介绍，显示在年代名称下方。")]
        [SerializeField] string summary;

        [Header("年代事实")]
        [Tooltip("使用年代或服役区间，例如 1916-1984。这里记录的是团队采用的公开资料口径，不是本组测量结果。")]
        [SerializeField] string servicePeriod;

        [Tooltip("典型作业深度或气源类型，例如 Standard air / 60 m。留空表示该年代资料未确认。")]
        [SerializeField] string operatingDepth;

        [Tooltip("资料口径，说明上面两项取自哪一类公开来源，避免把原型参数写成历史事实。")]
        [SerializeField] string factSource;

        [Header("外观")]
        [Tooltip("替换壳体的材质。留空表示保持当前材质不变。")]
        [SerializeField] Material shellMaterial;

        [Tooltip("替换前窗玻璃的材质。留空表示保持当前材质不变。")]
        [SerializeField] Material windowMaterial;

        [Tooltip("替换配件的金属材质。留空表示保持当前材质不变。")]
        [SerializeField] Material fittingMaterial;

        [Tooltip("替换胸板的材质。留空表示保持当前材质不变。")]
        [SerializeField] Material breastplateMaterial;

        [Header("切换表现")]
        [Tooltip("切换时是否让模型短暂淡出，以掩盖材质替换的跳变。")]
        [SerializeField] bool fadeDuringSwap = true;

        [Header("说明条目")]
        [Tooltip("按顺序显示的说明条目。可留空，届时只显示 summary。")]
        [SerializeField] DetailLine[] details = Array.Empty<DetailLine>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int SortOrder => sortOrder;
        public string Summary => summary;
        public string ServicePeriod => servicePeriod;
        public string OperatingDepth => operatingDepth;
        public string FactSource => factSource;
        public bool FadeDuringSwap => fadeDuringSwap;
        public DetailLine[] Details => details ?? Array.Empty<DetailLine>();

        /// <summary>Material for one surface, or null when this era keeps whatever is already there.</summary>
        public Material MaterialFor(HelmetMaterialSlot slot)
        {
            return slot switch
            {
                HelmetMaterialSlot.Shell => shellMaterial,
                HelmetMaterialSlot.Window => windowMaterial,
                HelmetMaterialSlot.Fitting => fittingMaterial,
                HelmetMaterialSlot.Breastplate => breastplateMaterial,
                _ => null
            };
        }

        /// <summary>True when at least one slot points at a material, so validation can tell "未配置" from "故意保持默认".</summary>
        public bool OverridesAnyMaterial =>
            shellMaterial != null || windowMaterial != null ||
            fittingMaterial != null || breastplateMaterial != null;

        /// <summary>Names of the slots this era actually fills. Used by the report to show coverage.</summary>
        public string FilledSlots()
        {
            var filled = new System.Collections.Generic.List<string>();
            foreach (HelmetMaterialSlot slot in System.Enum.GetValues(typeof(HelmetMaterialSlot)))
                if (MaterialFor(slot) != null) filled.Add(slot.ToString());
            return filled.Count == 0 ? "（无，保持模型原始材质）" : string.Join(", ", filled);
        }

        /// <summary>True when the era carries enough wording to be presented to a visitor.</summary>
        public bool HasVisitorFacingText => !string.IsNullOrWhiteSpace(summary);
    }
}
