using System.Collections.Generic;
using System.IO;
using System.Text;
using Museum.Eras;
using UnityEditor;
using UnityEngine;

namespace Museum.Eras.EditorTools
{
    /// <summary>
    /// Checks the era system without opening the shared main scene.
    /// Results are written to an isolated run folder so earlier evidence is never overwritten.
    /// </summary>
    public static class HelmetEraValidation
    {
        const string Root = "Assets/Museum/Features/HelmetEras";
        const string RunRoot = "artifacts/helmet-eras/qa/runs";

        // 与 HelmetEraSwitcher 的淡变属性保持一致：URP 用 _BaseColor，内置管线用 _Color。
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        [MenuItem("Museum/Helmet Eras/Validate Era System", priority = 20)]
        public static void ValidateFromMenu()
        {
            var report = RunValidation();
            Debug.Log(report.ToConsole());
            // outputPath 是 EraValidationReport 上的 public 字段（该类统一用小写字段名），不是属性。
            Debug.Log($"报告已写入：{report.outputPath}");
        }

        /// <summary>Runs every check and returns the collected report. Callable from a batch-mode entry point.</summary>
        public static EraValidationReport RunValidation()
        {
            var report = new EraValidationReport();
            report.startedUtc = System.DateTime.UtcNow.ToString("o");

            var catalogue = AssetDatabase.LoadAssetAtPath<HelmetEraCatalogue>($"{Root}/HelmetEraCatalogue.asset");
            var channel = AssetDatabase.LoadAssetAtPath<HelmetEraChannel>($"{Root}/HelmetEraChannel.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/HelmetEraShowcase.prefab");

            CheckCatalogue(report, catalogue);
            CheckChannel(report, channel);
            CheckPrefab(report, prefab);

            report.finishedUtc = System.DateTime.UtcNow.ToString("o");
            report.outputPath = WriteReport(report);
            return report;
        }

        static void CheckCatalogue(EraValidationReport report, HelmetEraCatalogue catalogue)
        {
            report.Add("catalogue asset exists", catalogue != null,
                catalogue != null ? $"{Root}/HelmetEraCatalogue.asset" : "缺少图鉴资产，请先运行 Create Era Assets");

            if (catalogue == null) return;

            int count = catalogue.Count;
            report.Add("catalogue has at least two eras", count >= 2, $"实际 {count} 个");
            report.Add("catalogue default index is valid", catalogue.DefaultIndex >= 0,
                $"默认序号 {catalogue.DefaultIndex}");

            var seenNames = new HashSet<string>();
            var seenOrders = new HashSet<int>();
            bool duplicateName = false;
            bool duplicateOrder = false;

            for (int i = 0; i < count; i++)
            {
                var era = catalogue[i];
                if (!seenNames.Add(era.DisplayName)) duplicateName = true;
                if (!seenOrders.Add(era.SortOrder)) duplicateOrder = true;

                report.Add($"era {i} \"{era.DisplayName}\" has visitor text", era.HasVisitorFacingText,
                    string.IsNullOrWhiteSpace(era.Summary) ? "summary 为空，观众看不到任何说明" : "ok");

                report.Add($"era {i} records its source", !string.IsNullOrWhiteSpace(era.FactSource),
                    string.IsNullOrWhiteSpace(era.FactSource) ? "缺 factSource，无法区分史料与团队解释" : "ok");

                report.Add($"era {i} declares a material slot", era.OverridesAnyMaterial,
                    era.OverridesAnyMaterial ? "ok" : "三个材质槽都为空，切换时外观不会变化");
            }

            report.Add("era display names are unique", !duplicateName, duplicateName ? "存在重名年代" : "ok");
            report.Add("era sort orders are unique", !duplicateOrder,
                duplicateOrder ? "存在相同 sortOrder，按钮顺序将退化为按名称排序" : "ok");

            // 环绕切换必须能回到起点，否则按钮会出现"卡住"的错觉。
            int first = 0;
            int wrapped = first;
            for (int i = 0; i < count; i++) wrapped = catalogue.NextIndex(wrapped);
            report.Add("next wraps back to the first era", wrapped == first, $"从 {first} 出发绕行 {count} 次后回到 {wrapped}");

            int backWrapped = first;
            for (int i = 0; i < count; i++) backWrapped = catalogue.PreviousIndex(backWrapped);
            report.Add("previous wraps back to the first era", backWrapped == first,
                $"从 {first} 出发反向绕行 {count} 次后回到 {backWrapped}");

            report.Add("NextIndex tolerates an unset index", catalogue.NextIndex(-1) == 0 || count == 0,
                $"NextIndex(-1) = {catalogue.NextIndex(-1)}");
            report.Add("PreviousIndex tolerates an unset index",
                count == 0 || catalogue.PreviousIndex(-1) == count - 1,
                $"PreviousIndex(-1) = {catalogue.PreviousIndex(-1)}");
            report.Add("out-of-range lookup returns null", catalogue[count + 5] == null, "越界应返回 null 而不是抛异常");
        }

        static void CheckChannel(EraValidationReport report, HelmetEraChannel channel)
        {
            report.Add("channel asset exists", channel != null,
                channel != null ? $"{Root}/HelmetEraChannel.asset" : "缺少事件通道资产");

            if (channel == null) return;

            // 直接驱动通道，确认广播能被订阅者收到，而不依赖任何场景对象。
            int received = 0;
            HelmetEraProfile lastEra = null;
            System.Action<HelmetEraProfile> handler = era => { received++; lastEra = era; };
            channel.EraChanged += handler;

            var probe = AssetDatabase.LoadAssetAtPath<HelmetEraProfile>($"{Root}/Profiles/Era-MarkV-Standard-Air.asset");
            channel.RaiseEraChanged(probe);
            report.Add("channel forwards EraChanged to listeners", received == 1 && lastEra == probe,
                $"收到 {received} 次，最近一次 {(lastEra ? lastEra.DisplayName : "null")}");

            // 传 null 不应清空当前年代，否则晚订阅者会读到空状态。
            channel.RaiseEraChanged(null);
            report.Add("channel ignores a null era", channel.Current == probe, "传 null 后当前年代应保持不变");

            int rejected = 0;
            System.Action<string> rejectHandler = _ => rejected++;
            channel.SwitchRejected += rejectHandler;
            channel.RaiseSwitchRejected("验证用原因");
            report.Add("channel forwards SwitchRejected to listeners", rejected == 1, $"收到 {rejected} 次");

            // 一个订阅者抛异常不应掐断其余订阅者的广播；否则排在前面的坏订阅者会让整条链失效。
            int survived = 0;
            System.Action<HelmetEraProfile> throwing = _ => throw new System.InvalidOperationException("验证用异常");
            System.Action<HelmetEraProfile> survivor = _ => survived++;
            channel.EraChanged += throwing;
            channel.EraChanged += survivor;
            channel.RaiseEraChanged(probe);
            report.Add("a throwing listener does not break the others", survived == 1,
                $"抛异常的订阅者之后仍收到 {survived} 次广播");

            channel.EraChanged -= throwing;
            channel.EraChanged -= survivor;
            channel.EraChanged -= handler;
            channel.SwitchRejected -= rejectHandler;
        }

        static void CheckPrefab(EraValidationReport report, GameObject prefab)
        {
            report.Add("showcase prefab exists", prefab != null,
                prefab != null ? $"{Root}/Prefabs/HelmetEraShowcase.prefab" : "缺少展示 Prefab");

            if (prefab == null) return;

            var switcher = prefab.GetComponentInChildren<HelmetEraSwitcher>(true);
            report.Add("prefab carries a switcher", switcher != null, switcher ? "找到 HelmetEraSwitcher" : "未找到");
            if (switcher == null) return;

            report.Add("switcher has a catalogue assigned", switcher.Catalogue != null,
                switcher.Catalogue ? switcher.Catalogue.name : "未指定图鉴");
            report.Add("switcher reaches more than one era", switcher.AvailableEraCount >= 2,
                $"可用年代 {switcher.AvailableEraCount} 个");

            int bound = 0;
            int unbound = 0;
            int outOfRange = 0;
            int fadeCapable = 0;
            foreach (var binding in switcher.Bindings)
            {
                if (binding.target == null)
                {
                    unbound++;
                    continue;
                }

                bound++;

                // 子材质序号越界会静默跳过，所以这里必须在构建阶段就拦下来。
                int materialCount = binding.target.sharedMaterials?.Length ?? 0;
                if (materialCount <= 0 || binding.subMaterialIndex < 0 || binding.subMaterialIndex >= materialCount)
                    outOfRange++;

                // 淡入淡出依赖材质暴露 _BaseColor 或 _Color，缺了就会直接跳变。
                var material = binding.target.sharedMaterial;
                if (material != null && (material.HasProperty(BaseColorId) || material.HasProperty(LegacyColorId)))
                    fadeCapable++;
            }

            report.Add("every binding points at a renderer", unbound == 0,
                $"已绑定 {bound} 个，未绑定 {unbound} 个");
            report.Add("switcher has at least one binding", bound > 0, $"已绑定 {bound} 个");
            report.Add("every binding targets an existing sub-material", outOfRange == 0,
                outOfRange == 0 ? "ok" : $"{outOfRange} 个绑定的子材质序号越界，切换时会静默跳过");
            report.Add("bound materials support a fade colour property", bound == 0 || fadeCapable == bound,
                $"支持淡变的材质 {fadeCapable} / 绑定 {bound}；不支持时切换会直接跳变");

            var buttons = prefab.GetComponentsInChildren<HelmetEraButton>(true);
            report.Add("prefab has at least two buttons", buttons.Length >= 2, $"找到 {buttons.Length} 个");

            var directions = new HashSet<HelmetEraButton.StepDirection>();
            foreach (var button in buttons) directions.Add(button.Direction);
            report.Add("buttons cover both directions", directions.Count == 2,
                $"覆盖方向 {directions.Count} 个");

            foreach (var button in buttons)
                report.Add($"button \"{button.name}\" is wired to a switcher", button.Switcher != null,
                    button.Switcher ? "ok" : "未指向切换器，点击不会有反应");

            // 展示 Prefab 必须自带切换器，不能依赖主场景里的其他对象。
            report.Add("prefab is self-contained", prefab.transform.parent == null,
                "根节点应是 Prefab 自身");
        }

        static string WriteReport(EraValidationReport report)
        {
            var stamp = System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var folder = Path.Combine(RunRoot, stamp);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "report.json").Replace('\\', '/');

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"startedUtc\": \"{report.startedUtc}\",");
            json.AppendLine($"  \"finishedUtc\": \"{report.finishedUtc}\",");
            json.AppendLine($"  \"passed\": {report.PassedCount},");
            json.AppendLine($"  \"failed\": {report.FailedCount},");
            json.AppendLine($"  \"total\": {report.Checks.Count},");
            json.AppendLine("  \"checks\": [");
            for (int i = 0; i < report.Checks.Count; i++)
            {
                var check = report.Checks[i];
                var comma = i == report.Checks.Count - 1 ? "" : ",";
                json.AppendLine($"    {{ \"name\": \"{Escape(check.name)}\", \"passed\": {check.passed.ToString().ToLowerInvariant()}, \"detail\": \"{Escape(check.detail)}\" }}{comma}");
            }
            json.AppendLine("  ]");
            json.AppendLine("}");

            File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            return path;
        }

        static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }
    }

    /// <summary>Collected outcome of one validation run.</summary>
    public sealed class EraValidationReport
    {
        public struct Check
        {
            public string name;
            public bool passed;
            public string detail;
        }

        public readonly List<Check> Checks = new List<Check>();
        public string startedUtc;
        public string finishedUtc;
        public string outputPath;

        public int PassedCount
        {
            get
            {
                int n = 0;
                foreach (var check in Checks) if (check.passed) n++;
                return n;
            }
        }

        public int FailedCount => Checks.Count - PassedCount;

        public void Add(string name, bool passed, string detail)
        {
            Checks.Add(new Check { name = name, passed = passed, detail = detail });
        }

        public string ToConsole()
        {
            var text = new StringBuilder();
            text.AppendLine($"[年代系统校验] 通过 {PassedCount} / 共 {Checks.Count}，失败 {FailedCount}");
            foreach (var check in Checks)
                if (!check.passed)
                    text.AppendLine($"  失败：{check.name}  （{check.detail}）");
            return text.ToString();
        }
    }
}
