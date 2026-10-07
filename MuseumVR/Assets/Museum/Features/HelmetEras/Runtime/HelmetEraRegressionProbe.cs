using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Museum.Eras
{
    /// <summary>
    /// Issue #15 的定向回归探针（验收工具，验收通过后可删除或保留作回归）：
    /// 挂在场景里任意物体上即可，进入 Play 后自动跑两条时序缺陷的用例，Console 打印 PASS / FAIL。
    ///
    /// 用例 1 —— 淡变途中禁用再启用（Issue #15 第 1 条）：
    ///   旧行为：OnDisable 只停协程，材质停在旧年代、变暗覆盖残留，
    ///   重选同一年代又被「index 相同」提前返回，永远回不到一致状态。
    ///   期望：禁用瞬间被取消的切换收敛到目标年代（材质正确、覆盖清空），且重选同一年代不破坏一致性。
    ///
    /// 用例 2 —— 淡变途中发起新的瞬时切换（Issue #15 第 2 条）：
    ///   旧行为：停止旧协程的代码写在瞬时分支 return 之后，旧淡变稍后把材质改回上一个年代。
    ///   期望：新请求立即取消旧转换，等够旧淡变"本该完成"的时间后，材质仍稳定在新年代。
    ///
    /// 测试会临时把 transitionDuration 与 fadeDuringSwap 改成探针需要的值，
    /// 结束后全部恢复原值，并切回年代 0。
    /// </summary>
    public sealed class HelmetEraRegressionProbe : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return new WaitForSeconds(0.5f);

            var switcher = FindObjectOfType<HelmetEraSwitcher>();
            if (switcher == null)
            {
                Debug.LogError("[回归探针] ❌ 场景里没有 HelmetEraSwitcher。请把 HelmetEraShowcase 预制体拖进场景。");
                yield break;
            }
            if (switcher.Catalogue == null || switcher.AvailableEraCount < 2)
            {
                Debug.LogError("[回归探针] ❌ 图鉴缺失或年代少于 2 个，无法测试。");
                yield break;
            }

            var durationField = typeof(HelmetEraSwitcher).GetField(
                "transitionDuration", BindingFlags.NonPublic | BindingFlags.Instance);
            var fadeField = typeof(HelmetEraProfile).GetField(
                "fadeDuringSwap", BindingFlags.NonPublic | BindingFlags.Instance);
            if (durationField == null || fadeField == null)
            {
                Debug.LogError("[回归探针] ❌ 反射找不到私有字段，探针与切换器脚本版本不匹配。");
                yield break;
            }

            float originalDuration = (float)durationField.GetValue(switcher);
            var originalFade = new System.Collections.Generic.Dictionary<HelmetEraProfile, bool>();
            foreach (var era in EnumerateEras(switcher))
                originalFade[era] = era.FadeDuringSwap;

            // 保证淡变路径真的会走：把所有年代的 fadeDuringSwap 临时设为 true（测完恢复）。
            foreach (var era in EnumerateEras(switcher))
                fadeField.SetValue(era, true);

            int pass = 0, fail = 0;

            // ---------------------------------------------------------- 测试 1
            Debug.Log("[回归探针] ▶ 测试 1：淡变途中禁用再启用（Issue #15 第 1 条）");
            durationField.SetValue(switcher, 0.8f);

            switcher.ShowEra(0);
            yield return null;

            int startIdx = switcher.CurrentIndex;
            int fadeTarget1 = (startIdx + 1) % switcher.AvailableEraCount;
            var targetEra1 = switcher.Catalogue[fadeTarget1];

            switcher.ShowEra(fadeTarget1);
            yield return new WaitForSeconds(0.15f);   // 淡变进行到一半

            switcher.enabled = false;                 // 旧行为：这里只停协程，留下不一致状态
            yield return null;
            switcher.enabled = true;

            string why1;
            bool ok1 = MaterialsMatch(switcher, targetEra1, out why1) && NoDimOverride(switcher, out why1);
            if (ok1)
            {
                // 重选同一年代一次：旧行为会因「index 相同」提前返回而留下旧外观。
                switcher.ShowEra(fadeTarget1);
                yield return new WaitForSeconds(0.1f);
                ok1 = MaterialsMatch(switcher, targetEra1, out why1) && NoDimOverride(switcher, out why1);
            }
            LogResult(1, ok1, ok1 ? "禁用后收敛到目标年代，覆盖已清，重选同一年代一致" : why1,
                ref pass, ref fail);

            // ---------------------------------------------------------- 测试 2
            Debug.Log("[回归探针] ▶ 测试 2：淡变途中发起新的瞬时切换（Issue #15 第 2 条）");
            durationField.SetValue(switcher, 0.8f);
            yield return new WaitForSeconds(0.2f);

            int baseIdx = switcher.CurrentIndex;
            int fadeTarget2 = (baseIdx + 1) % switcher.AvailableEraCount;
            int instantTarget = (fadeTarget2 + 1) % switcher.AvailableEraCount;
            var instantEra = switcher.Catalogue[instantTarget];

            switcher.ShowEra(fadeTarget2);            // 开始一次 0.8 秒淡变
            yield return new WaitForSeconds(0.15f);   // 淡变进行到一半

            durationField.SetValue(switcher, 0f);     // 改成瞬时
            switcher.ShowEra(instantTarget);          // 旧行为：旧淡变未被取消，稍后覆盖材质
            yield return new WaitForSeconds(2.0f);    // 等够旧淡变"本该完成"的时间

            string why2;
            bool ok2 = switcher.CurrentIndex == instantTarget
                ? MaterialsMatch(switcher, instantEra, out why2)
                : SetReason(out why2, $"索引停在 {switcher.CurrentIndex}，应为 {instantTarget}");
            LogResult(2, ok2, ok2 ? "新瞬时切换立即生效，旧淡变未覆盖结果" : why2, ref pass, ref fail);

            // ---------------------------------------------------------- 恢复
            durationField.SetValue(switcher, originalDuration);
            foreach (var pair in originalFade)
                fadeField.SetValue(pair.Key, pair.Value);
            switcher.ShowEra(0);

            Debug.Log($"[回归探针] ===== 回归结束：通过 {pass} / 失败 {fail} =====");
            if (fail == 0)
                Debug.Log("[回归探针] ✅ 全部通过 —— Issue #15 的两条时序缺陷均已修复。请把 Console 截图发给 AI 汇总。");
            else
                Debug.LogError("[回归探针] ❌ 存在失败项 —— 请把 Console 截图发给 AI 分析。");
        }

        // ------------------------------------------------------------------ 检查

        static System.Collections.Generic.IEnumerable<HelmetEraProfile> EnumerateEras(HelmetEraSwitcher switcher)
        {
            for (int i = 0; i < switcher.AvailableEraCount; i++)
            {
                var era = switcher.Catalogue[i];
                if (era != null) yield return era;
            }
        }

        static bool SetReason(out string reason, string text)
        {
            reason = text;
            return false;
        }

        /// <summary>每个绑定渲染器的目标槽位材质是否与目标年代一致（年代留空的槽位跳过）。</summary>
        static bool MaterialsMatch(HelmetEraSwitcher switcher, HelmetEraProfile era, out string reason)
        {
            reason = "ok";
            if (era == null)
            {
                reason = "目标年代为空";
                return false;
            }

            foreach (var binding in switcher.Bindings)
            {
                if (binding.target == null) continue;

                var expected = era.MaterialFor(binding.slot);
                var current = binding.target.sharedMaterials;
                if (binding.subMaterialIndex < 0 || binding.subMaterialIndex >= current.Length)
                {
                    reason = $"{binding.target.name}: 子材质序号 {binding.subMaterialIndex} 越界";
                    return false;
                }

                var now = current[binding.subMaterialIndex];
                if (expected != null && now != expected)
                {
                    reason = $"{binding.target.name}[{binding.slot}] 期望「{expected.name}」实际「{(now ? now.name : "null")}」";
                    return false;
                }
            }
            return true;
        }

        /// <summary>绑定渲染器上是否还压着淡变留下的 MaterialPropertyBlock 覆盖。</summary>
        static bool NoDimOverride(HelmetEraSwitcher switcher, out string reason)
        {
            reason = "ok";
            var block = new MaterialPropertyBlock();
            foreach (var binding in switcher.Bindings)
            {
                if (binding.target == null) continue;
                block.Clear();
                binding.target.GetPropertyBlock(block);
                if (!block.isEmpty)
                {
                    reason = $"{binding.target.name} 仍有淡变留下的属性块覆盖";
                    return false;
                }
            }
            return true;
        }

        void LogResult(int number, bool ok, string detail, ref int pass, ref int fail)
        {
            if (ok)
            {
                pass++;
                Debug.Log($"[回归探针] ✅ 测试 {number} PASS —— {detail}");
            }
            else
            {
                fail++;
                Debug.LogError($"[回归探针] ❌ 测试 {number} FAIL —— {detail}");
            }
        }
    }
}
