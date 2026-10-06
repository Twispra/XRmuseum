using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Museum.Localization.EditorTools
{
    /// <summary>
    /// 本地化系统的离线校验。**不打开主场景、不改动任何产品文件。**
    ///
    /// 最要紧的一项是**缺字检查**：项目里只有英文字体，中文一旦缺字形就会显示成方块，
    /// 而方块在编辑器里和「设计上就有一块色块」几乎无法区分。这里逐字符核对，
    /// 并在动态图集模式下顺带把用到的字烤进图集。
    /// </summary>
    public static class LocalizationValidation
    {
        const string Root = "Assets/Museum/Features/Localization";
        const string FontAssetPath = Root + "/Fonts/NotoSansSC-MuseumSubset SDF.asset";
        const string TablePath = Root + "/Tables/LocalizationTable.asset";
        const string ChannelPath = Root + "/Tables/LanguageChannel.asset";
        const string ShowcasePath = Root + "/Prefabs/LanguageShowcase.prefab";

        sealed class Report
        {
            public readonly List<string> failures = new List<string>();
            public readonly List<string> notes = new List<string>();
            public int passed;

            public void Ok(string what)
            {
                passed++;
                notes.Add("通过  " + what);
            }

            public void Fail(string what)
            {
                failures.Add(what);
            }
        }

        [MenuItem("Museum/Localization/Validate Localization System", priority = 20)]
        public static void ValidateFromMenu()
        {
            var report = RunValidation();
            Debug.Log(report.ToConsole());
            Debug.Log($"报告已写入：{report.outputPath}");
        }

        /// <summary>
        /// 批处理入口，供无人值守运行：
        /// <c>Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod Museum.Localization.EditorTools.LocalizationValidation.RunBatch</c>
        /// 全部通过时退出码为 0。菜单里不会调用它，所以不会误关编辑器。
        /// </summary>
        public static void RunBatch()
        {
            int failures;
            try
            {
                var report = RunValidation();
                Debug.Log(report.ToConsole());
                failures = report.failures.Length;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LocalizationValidation] 校验异常：{exception}");
                failures = 1;
            }

            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static ValidationReport RunValidation()
        {
            var check = new Report();

            // 1) 资产是否齐全
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                check.Fail($"缺少字体资产：{FontAssetPath}");
            }
            else
            {
                int atlasCount = font.atlasTextures != null ? font.atlasTextures.Length : 0;
                int atlasW = atlasCount > 0 && font.atlasTextures[0] != null ? font.atlasTextures[0].width : 0;
                int atlasH = atlasCount > 0 && font.atlasTextures[0] != null ? font.atlasTextures[0].height : 0;
                int glyphs = font.characterTable != null ? font.characterTable.Count : 0;

                // 图集缺失是「假通过」的根源：内存里 tryAddCharacter 能成功，
                // 但磁盘上的资产没有图集，运行时一个字也烤不进去，文字会变成方块或实心矩形。
                if (atlasCount == 0 || atlasW == 0 || atlasH == 0)
                {
                    check.Fail("字体资产没有可用图集 —— 运行时会渲染成方块或实心矩形。" +
                               "请先备份并检查保存的字体与图集；按 Fonts/README.md 修复，不要删除资产或重建 meta。");
                }
                else
                {
                    check.Ok($"字体资产存在（图集 {atlasCount} 张，{atlasW}×{atlasH}，已烤 {glyphs} 个字形）");
                }
            }

            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(TablePath);
            if (table == null) check.Fail($"缺少文案表：{TablePath}");
            else check.Ok($"文案表存在（{table.Entries.Count} 条）");

            var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(ChannelPath);
            if (channel == null) check.Fail($"缺少语言通道：{ChannelPath}");
            else check.Ok("语言通道存在");

            if (font == null || table == null || channel == null)
                return Finish(check, null, null, null);

            // 2) 表本身
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in table.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    check.Fail("表里存在空 key 的条目");
                    continue;
                }

                if (!keys.Add(entry.Key))
                    check.Fail($"key 重复：{entry.Key}");

                if (string.IsNullOrWhiteSpace(entry.Chinese))
                    check.Fail($"缺中文：{entry.Key}");

                if (string.IsNullOrWhiteSpace(entry.TraditionalChinese)) check.Fail($"缺繁体中文：{entry.Key}");
                if (string.IsNullOrWhiteSpace(entry.English))
                    check.Fail($"缺英文：{entry.Key}");
            }

            check.Ok($"key 唯一性与双语完整性（{keys.Count} 个 key）");

            // 3) 缺字检查 —— 最关键的一项
            //
            // 只核对**真正会被绘制**的字符，来源有三类：
            //   ① 文案表里每条的中文与英文 —— 运行时会原样显示；
            //   ② 缺 key 时显示的 [[key]] 标记 —— 标记里的 key 本身也会被画出来；
            //   ③ 语言按钮上那两个固定标签（走 NativeName()，不经过文案表，最容易漏配字体）。
            //
            // 刻意**不再硬编码标点**：这里曾经追加过 `[[ ]]。，、`，
            // 其中顿号 U+3001 既不在已保存的字体图集里、当前文案也根本不使用它，
            // 于是已保存资产上会报一条假失败（Issue #16）。
            // 要支持某个标点，应当把它加进字符清单并重烤字体，而不是让校验器凭空要求它。
            var missing = new List<char>();
            var seen = new HashSet<char>();
            var probe = new StringBuilder();
            foreach (var entry in table.Entries)
            {
                probe.Append(entry.Chinese).Append(entry.English).Append(entry.TraditionalChinese);
                probe.Append(entry.Key);
            }

            probe.Append("[]");   // [[key]] 标记的方括号
            foreach (var language in (MuseumLanguage[])System.Enum.GetValues(typeof(MuseumLanguage)))
            {
                probe.Append(language.NativeName());
            }


            foreach (var character in probe.ToString())
            {
                if (char.IsWhiteSpace(character)) continue;
                if (!seen.Add(character)) continue;
                // tryAddCharacter: 动态图集下顺带把字烤进去，同时也回答了「能不能渲染」。
                if (!font.HasCharacter(character, false, true)) missing.Add(character);
            }

            if (missing.Count == 0)
                check.Ok($"字体字形覆盖（核对了 {seen.Count} 个不同字符）");
            else
                check.Fail($"字体缺少 {missing.Count} 个字形：" +
                           string.Join("，", missing.ConvertAll(c => $"'{c}'(U+{(int)c:X4})").ToArray()));

            // 4) 展示 Prefab 的接线
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShowcasePath);
            if (prefab == null)
            {
                check.Fail($"缺少展示 Prefab：{ShowcasePath}");
                return Finish(check, table, channel, null);
            }

            check.Ok("展示 Prefab 存在");

            // 4b) Prefab 里**所有** TMP 文字都要有字形，不只是挂了 LocalizedText 的那些。
            // 按钮标签这类固定文字不走本地化组件，最容易漏配字体——实机验证时就漏过一次，
            // 结果是「中文」两个字显示成方块。
            var allTexts = prefab.GetComponentsInChildren<TMP_Text>(true);
            var prefabMissing = new List<string>();
            foreach (var label in allTexts)
            {
                if (string.IsNullOrEmpty(label.text)) continue;
                foreach (var character in label.text)
                {
                    if (char.IsWhiteSpace(character)) continue;
                    if (font.HasCharacter(character, false, true)) continue;
                    var entry = $"{label.gameObject.name} 缺字形 '{character}'(U+{(int)character:X4})";
                    if (!prefabMissing.Contains(entry)) prefabMissing.Add(entry);
                }
            }

            if (prefabMissing.Count == 0)
                check.Ok($"展示 Prefab 内 {allTexts.Length} 处文字的缺字检查");
            else
                foreach (var problem in prefabMissing) check.Fail(problem);

            var localized = prefab.GetComponentsInChildren<LocalizedText>(true);
            if (localized.Length == 0)
            {
                check.Fail("展示 Prefab 里没有任何 LocalizedText");
            }
            else
            {
                foreach (var text in localized)
                {
                    var so = new SerializedObject(text);
                    var key = so.FindProperty("key").stringValue;
                    var tableRef = so.FindProperty("table").objectReferenceValue;
                    var channelRef = so.FindProperty("channel").objectReferenceValue;

                    if (string.IsNullOrEmpty(key)) check.Fail($"{text.name} 没有绑定 key");
                    else if (!keys.Contains(key)) check.Fail($"{text.name} 绑定的 key 不在表里：{key}");

                    if (tableRef == null) check.Fail($"{text.name} 没指定文案表");
                    if (channelRef == null) check.Fail($"{text.name} 没指定语言通道");
                }

                check.Ok($"展示 Prefab 的 {localized.Length} 处文字接线");
            }

            var buttons = prefab.GetComponentsInChildren<LanguageButton>(true);
            if (buttons.Length == 0) check.Fail("展示 Prefab 里没有语言按钮");
            else
            {
                foreach (var button in buttons)
                {
                    var so = new SerializedObject(button);
                    if (so.FindProperty("channel").objectReferenceValue == null)
                        check.Fail($"{button.name} 没指定语言通道");
                }
                check.Ok($"展示 Prefab 的 {buttons.Length} 个语言按钮接线");
            }

            if (prefab.GetComponentInChildren<LanguagePlayModePreview>(true) == null)
                check.Fail("展示 Prefab 上没有键盘预览组件（无头显成员会无法验证）");
            else
                check.Ok("键盘预览组件存在");

            // 5) 初始语言在每种语言下都要有文案
            foreach (MuseumLanguage language in Enum.GetValues(typeof(MuseumLanguage)))
            {
                int empty = 0;
                foreach (var entry in table.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.For(language))) empty++;
                }
                if (empty > 0) check.Fail($"{language} 下有 {empty} 条文案为空");
            }
            check.Ok("两种语言的文案完整度");

            return Finish(check, table, channel, prefab);
        }

        static ValidationReport Finish(Report check, LocalizationTable table, LanguageChannel channel, GameObject prefab)
        {
            var report = new ValidationReport
            {
                startedUtc = DateTime.UtcNow.ToString("o"),
                passed = check.passed,
                failures = check.failures.ToArray(),
                notes = check.notes.ToArray(),
                tableEntries = table != null ? table.Entries.Count : 0,
                initialLanguage = channel != null ? channel.Initial.ToString() : "?",
                showcasePresent = prefab != null,
            };
            report.finishedUtc = DateTime.UtcNow.ToString("o");
            report.outputPath = WriteReport(report);
            return report;
        }

        static string WriteReport(ValidationReport report)
        {
            // 证据写到仓库根的 artifacts/ 下，与 HelmetEras 模块保持同一约定。
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'");
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var folder = Path.Combine(repoRoot, "artifacts", "localization", "qa", "runs", stamp + "-validation");
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, "report.json");
            File.WriteAllText(path, report.ToJson(), new UTF8Encoding(false));
            return path;
        }

        sealed class ValidationReport
        {
            public string startedUtc;
            public string finishedUtc;
            public int passed;
            public string[] failures = Array.Empty<string>();
            public string[] notes = Array.Empty<string>();
            public int tableEntries;
            public string initialLanguage = "?";
            public bool showcasePresent;
            public string outputPath = "";

            public string ToConsole()
            {
                var builder = new StringBuilder();
                builder.AppendLine("[本地化校验] 通过 " + passed + " 项，失败 " + failures.Length + " 项");
                foreach (var note in notes) builder.AppendLine("  " + note);
                foreach (var failure in failures) builder.AppendLine("  失败  " + failure);
                if (failures.Length == 0) builder.AppendLine("  结论：链路可用（缺字与接线均已核对）");
                return builder.ToString();
            }

            public string ToJson()
            {
                var builder = new StringBuilder();
                builder.AppendLine("{");
                builder.AppendLine($"  \"startedUtc\": \"{startedUtc}\",");
                builder.AppendLine($"  \"finishedUtc\": \"{finishedUtc}\",");
                builder.AppendLine($"  \"passed\": {passed},");
                builder.AppendLine($"  \"failed\": {failures.Length},");
                builder.AppendLine($"  \"tableEntries\": {tableEntries},");
                builder.AppendLine($"  \"initialLanguage\": \"{initialLanguage}\",");
                builder.AppendLine($"  \"showcasePresent\": {(showcasePresent ? "true" : "false")},");
                builder.AppendLine("  \"checks\": [");
                for (int i = 0; i < notes.Length; i++)
                    builder.AppendLine($"    {{ \"ok\": true, \"detail\": \"{Escape(notes[i])}\" }}{(i == notes.Length - 1 && failures.Length == 0 ? "" : ",")}");
                for (int i = 0; i < failures.Length; i++)
                    builder.AppendLine($"    {{ \"ok\": false, \"detail\": \"{Escape(failures[i])}\" }}{(i == failures.Length - 1 ? "" : ",")}");
                builder.AppendLine("  ]");
                builder.AppendLine("}");
                return builder.ToString();
            }

            static string Escape(string value)
            {
                return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            }
        }
    }
}
