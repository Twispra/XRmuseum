using System;
using System.Collections.Generic;
using UnityEngine;

namespace Museum.Eras
{
    /// <summary>Ordered catalogue of eras. Add a profile asset here and every switcher in the scene sees it.</summary>
    [CreateAssetMenu(menuName = "Museum/Helmet Era Catalogue", fileName = "HelmetEraCatalogue")]
    public sealed class HelmetEraCatalogue : ScriptableObject
    {
        [Tooltip("纳入图鉴的年代资料。留空的槽位会被忽略，方便先占位后补内容。")]
        [SerializeField] HelmetEraProfile[] eras = Array.Empty<HelmetEraProfile>();

        [Tooltip("进入场景时默认显示的年代序号。越界时自动退回到第一个有效年代。")]
        [SerializeField] int defaultIndex;

        readonly List<HelmetEraProfile> resolved = new List<HelmetEraProfile>();
        bool cached;

        /// <summary>Valid profiles in display order. Sorted by each profile's sort order, then by asset name.</summary>
        public IReadOnlyList<HelmetEraProfile> Eras
        {
            get
            {
                EnsureCache();
                return resolved;
            }
        }

        public int Count
        {
            get
            {
                EnsureCache();
                return resolved.Count;
            }
        }

        /// <summary>Index entered on scene start, clamped to the catalogue that actually resolved.</summary>
        public int DefaultIndex
        {
            get
            {
                EnsureCache();
                if (resolved.Count == 0) return -1;
                return Mathf.Clamp(defaultIndex, 0, resolved.Count - 1);
            }
        }

        public HelmetEraProfile this[int index]
        {
            get
            {
                EnsureCache();
                return index >= 0 && index < resolved.Count ? resolved[index] : null;
            }
        }

        /// <summary>Returns the next index, wrapping around. Returns -1 when the catalogue is empty.</summary>
        public int NextIndex(int current)
        {
            EnsureCache();
            if (resolved.Count == 0) return -1;
            if (current < 0 || current >= resolved.Count) return 0;
            return (current + 1) % resolved.Count;
        }

        /// <summary>Returns the previous index, wrapping around. Returns -1 when the catalogue is empty.</summary>
        public int PreviousIndex(int current)
        {
            EnsureCache();
            if (resolved.Count == 0) return -1;
            if (current <= 0 || current >= resolved.Count) return resolved.Count - 1;
            return current - 1;
        }

        /// <summary>Locates a profile's display index, or -1 when it is not part of this catalogue.</summary>
        public int IndexOf(HelmetEraProfile profile)
        {
            EnsureCache();
            return profile == null ? -1 : resolved.IndexOf(profile);
        }

        /// <summary>Drops the cache so an Editor script can re-read the asset after editing it.</summary>
        public void InvalidateCache()
        {
            cached = false;
            resolved.Clear();
        }

        void EnsureCache()
        {
            if (cached) return;
            resolved.Clear();
            if (eras != null)
            {
                foreach (var era in eras)
                {
                    if (era == null || resolved.Contains(era)) continue;
                    resolved.Add(era);
                }
            }
            // 排序按资产自带权重，同权重时按名称，保证六个按钮的顺序在任何机器上都一致。
            resolved.Sort(CompareByOrderThenName);
            cached = true;
        }

        static int CompareByOrderThenName(HelmetEraProfile a, HelmetEraProfile b)
        {
            int byOrder = a.SortOrder.CompareTo(b.SortOrder);
            if (byOrder != 0) return byOrder;
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        }

        void OnValidate()
        {
            InvalidateCache();
        }
    }
}
