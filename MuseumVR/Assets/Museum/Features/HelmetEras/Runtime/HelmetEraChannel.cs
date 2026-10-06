using System;
using UnityEngine;

namespace Museum.Eras
{
    /// <summary>
    /// Decoupled broadcast of "which era is showing".
    /// Listeners hold one asset reference instead of hunting for the switcher component,
    /// so the era system stays reusable when the greybox model is replaced.
    /// </summary>
    [CreateAssetMenu(menuName = "Museum/Helmet Era Channel", fileName = "HelmetEraChannel")]
    public sealed class HelmetEraChannel : ScriptableObject
    {
        /// <summary>Raised after a switch completes. Argument is the era now on display, never null.</summary>
        public event Action<HelmetEraProfile> EraChanged;

        /// <summary>Raised when a switch is requested but cannot be honoured, with the reason.</summary>
        public event Action<string> SwitchRejected;

        [Tooltip("最近一次成功切换的年代。仅用于调试与晚订阅者补读当前状态。")]
        [SerializeField] HelmetEraProfile current;

        public HelmetEraProfile Current => current;

        public void RaiseEraChanged(HelmetEraProfile era)
        {
            if (era == null) return;
            current = era;
            RaiseSafely(EraChanged, era, nameof(EraChanged));
        }

        public void RaiseSwitchRejected(string reason)
        {
            RaiseSafely(SwitchRejected, reason, nameof(SwitchRejected));
        }

        /// <summary>
        /// Invokes each listener in isolation so one throwing subscriber cannot stop the rest.
        /// 订阅者通常在场景对象上；如果某个对象已被销毁但委托还没摘掉，这里会捕获异常并跳过，
        /// 避免一个坏订阅者让整条广播链断掉。
        /// </summary>
        static void RaiseSafely<T>(Action<T> handlers, T argument, string label)
        {
            if (handlers == null) return;

            // 复制一份再遍历：订阅者可能在回调里退订，直接遍历原委托会漏掉后续项。
            var snapshot = handlers.GetInvocationList();
            foreach (var handler in snapshot)
            {
                try
                {
                    ((Action<T>)handler).Invoke(argument);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[HelmetEraChannel] 订阅者在 {label} 回调中抛出异常，已跳过该订阅者：{exception}",
                        null);
                }
            }
        }

        void OnDisable()
        {
            // 只有真正离开播放、编辑器重载或退出时才需要清空，避免悬空委托。
            //
            // 注意：ScriptableObject 的 OnDisable 在编辑器里还会因为「资产被重新导入」
            // 或「脚本重编译」而触发。如果此时正处于播放状态，无条件清空会把正在运行的
            // 订阅者全部摘掉，表现为「按钮按了没反应，但控制台也不报错」。
            // 因此这里先判断是否仍在播放：仍在播放就保留订阅，交给真正的退出流程处理。
            if (Application.isPlaying) return;

            EraChanged = null;
            SwitchRejected = null;
        }
    }
}
