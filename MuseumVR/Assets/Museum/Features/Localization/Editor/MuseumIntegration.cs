using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Museum.Localization.EditorTools
{
    /// <summary>
    /// 把「语言选择」接进主场景 <c>MuseumStart.unity</c>。
    ///
    /// 设计原则（因为主场景是全组共享、需要串行写入的文件）：
    /// <list type="bullet">
    /// <item>**只增不改**：所有改动集中在一个新增根物体上；对既有物体只**挂组件**，
    /// 不动它们的名字、位置、旋转、缩放和文字内容。</item>
    /// <item>**执行前自动备份**主场景到仓库外的 <c>_local-backup/</c>，随时可以还原。</item>
    /// <item>**可一键撤销**，见 <see cref="RemoveFromMuseumStart"/>。</item>
    /// <item>**Play 模式拒绝执行**：Unity 在播放状态会拒绝场景保存，产物会不完整而无明显报错。</item>
    /// </list>
    ///
    /// 挂 <c>LocalizedText</c> 的那三条文案，英文必须与场景里现有文字**逐字一致**，
    /// 否则挂上的瞬间就等于改掉了展陈文案。
    /// </summary>
    public static class MuseumIntegration
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string PrefabPath = "Assets/Museum/Features/Localization/Prefabs/LanguageShowcase.prefab";
        const string TablePath = "Assets/Museum/Features/Localization/Tables/LocalizationTable.asset";
        const string ChannelPath = "Assets/Museum/Features/Localization/Tables/LanguageChannel.asset";

        const string RootName = "Language Selection";

        /// <summary>
        /// 语言选择面板的位置。参观者出生点在 (0, 0, 2.2) 且朝 +Z，
        /// 入口标识墙在 z = 8.82；放在右前方 4 米处 —— 一眼看得到，又不挡住居中的馆名。
        /// 觉得位置不合适，接入后在 Hierarchy 里直接拖这个物体即可。
        /// </summary>
        static readonly Vector3 Placement = new Vector3(2.6f, 0f, 6.2f);

        /// <summary>本工具添加的绑定，key 一律以这个前缀开头（用于识别与清理）。</summary>
        const string SignKeyPrefix = "museum.sign.";

        /// <summary>
        /// 要挂上 LocalizedText 的既有文字（物体名 → key）。
        ///
        /// ⚠️ 必须是**游客真正看得见**的标识。第一版绑的是 Museum Title / Museum Subtitle /
        /// Welcome，但那三个是展厅改造后保留的**隐藏旧对象**（`MeshRenderer m_Enabled = 0`，
        /// 被 #9 的按需显隐逻辑关掉了）—— 挂上组件不会产生任何可见变化，游客看到的仍是英文
        /// （Issue #17，组长实测复现）。现在绑的是 MaritimeGallery 里真正在渲染的入口标识。
        ///
        /// 这几个物体位于 `MaritimeGallery.prefab` 内。**不要改那个预制体**：
        /// 在场景里给预制体实例的子物体加组件，Unity 会记为**场景 override**，
        /// 因此本工具只写 `MuseumStart.unity`，符合 Issue 限定的修改范围。
        /// </summary>
        static readonly KeyValuePair<string, string>[] Signage =
        {
            new KeyValuePair<string, string>("Gallery masthead", "museum.sign.masthead"),
            new KeyValuePair<string, string>("Gallery subtitle", "museum.sign.gallery"),
            new KeyValuePair<string, string>("Arrival brand", "museum.sign.gallery"),
            new KeyValuePair<string, string>("Arrival introduction", "museum.sign.intro"),
        };

        // ------------------------------------------------------------------ 接入

        [MenuItem("Museum/Localization/Integrate Into MuseumStart", priority = 30)]
        public static void IntegrateIntoMuseumStart()
        {
            if (RefuseWhilePlaying()) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(TablePath);
            var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(ChannelPath);
            if (prefab == null || table == null || channel == null)
            {
                Debug.LogError(
                    "[MuseumIntegration] 缺少模块资产。请先执行 " +
                    "Museum → Localization → Create Everything and Demo Scene。");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var backup = BackupScene();
            if (backup == null) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var report = new List<string>();

            // ---- 1) 新增语言选择面板（唯一的"增"）----
            var existing = FindRoot(scene, RootName);
            if (existing != null)
            {
                report.Add($"已存在「{RootName}」，跳过新增（如需重来请先执行 Remove From MuseumStart）。");
            }
            else
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = RootName;
                instance.transform.position = Placement;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                report.Add($"新增「{RootName}」于 {Placement}（面向 -Z，正对参观者来向）。");
            }

            // ---- 2) 先清掉已经过时的旧绑定 ----
            // 换过绑定目标（或改过 key）之后，旧绑定会残留在物体上，而它的 key 已经不在文案表里，
            // 于是那个物体上会一直显示红色 [[museum.sign.xxx]] 标记（Issue #17 的连带问题）。
            // 只清理**本工具加过的、且当前不再有效**的绑定；别人挂的组件一律不动。
            var validKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in Signage) validKeys.Add(pair.Value);
            var stale = StripStaleBindings(scene, validKeys);
            if (stale.Count > 0)
                report.Add($"清理过时绑定 {stale.Count} 处：{string.Join("; ", stale)}");

            // ---- 3) 给既有标识挂 LocalizedText（只加组件，不改任何其它属性）----
            foreach (var pair in Signage)
            {
                var target = FindByName(scene, pair.Key);
                if (target == null)
                {
                    report.Add($"⚠ 场景里找不到物体「{pair.Key}」，跳过（主场景结构可能变了）。");
                    continue;
                }

                if (target.GetComponent<TMP_Text>() == null)
                {
                    report.Add($"⚠ 「{pair.Key}」上没有 TMP 文字组件，跳过。");
                    continue;
                }

                var existingComponent = target.GetComponent<LocalizedText>();
                if (existingComponent != null)
                {
                    if (existingComponent.Key == pair.Value)
                    {
                        // 已经绑好：按**当前语言**刷新一次文字。
                        // 场景里保存的文字应当与初始语言一致 —— 例如初始语言从中文改成英文后，
                        // 场景里不该还留着上一轮写入的中文。
                        existingComponent.Refresh();
                        EditorUtility.SetDirty(target);
                        report.Add($"「{pair.Key}」已绑定 {pair.Value}，已按当前语言刷新文字。");
                        continue;
                    }

                    if (!IsOurs(existingComponent.Key))
                    {
                        // 别人挂的组件：不覆盖，只提醒。这是别人的内容决定，不该由本工具改。
                        report.Add($"⚠ 「{pair.Key}」已被绑定到「{existingComponent.Key}」，未覆盖。");
                        continue;
                    }

                    // 是本工具加的、但 key 换了：拆掉重挂。
                    UnityEngine.Object.DestroyImmediate(existingComponent);
                    report.Add($"「{pair.Key}」的旧 key 已替换。");
                }

                var localized = target.AddComponent<LocalizedText>();
                localized.Configure(table, channel, pair.Value);
                EditorUtility.SetDirty(target);
                report.Add($"「{pair.Key}」→ 绑定 {pair.Value}（只加了组件，文字与位置未改动）。");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[MuseumIntegration] 接入完成。\n  " + string.Join("\n  ", report) +
                      $"\n\n主场景备份：{backup}" +
                      "\n想撤销：Museum → Localization → Remove From MuseumStart");

            Selection.activeGameObject = FindRoot(scene, RootName);
            EditorGUIUtility.PingObject(Selection.activeGameObject);
        }

        // ------------------------------------------------------------------ 撤销

        [MenuItem("Museum/Localization/Remove From MuseumStart", priority = 31)]
        public static void RemoveFromMuseumStart()
        {
            if (RefuseWhilePlaying()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var report = new List<string>();

            var root = FindRoot(scene, RootName);
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                report.Add($"已删除「{RootName}」。");
            }
            else
            {
                report.Add($"场景里没有「{RootName}」。");
            }

            // 移除本工具加过的绑定：用 key 前缀识别，**不用**当前的 Signage 列表 ——
            // 换过绑定目标之后，旧目标上的绑定会残留在场景里，而它的 key 已不在文案表中，
            // 于是物体上会显示红色 [[museum.sign.title]] 标记（Issue #17 的连带问题）。
            var removed = StripOurBindings(scene);
            if (removed.Count == 0) report.Add("场景里没有本工具加过的绑定。");
            foreach (var item in removed) report.Add($"已移除绑定：{item}");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[MuseumIntegration] 已撤销接入。\n  " + string.Join("\n  ", report));
        }

        // ------------------------------------------------------------------ 工具

        /// <summary>这个 key 是不是本工具加的（用前缀识别，避免误动别人的组件）。</summary>
        static bool IsOurs(string key)
        {
            return !string.IsNullOrEmpty(key) && key.StartsWith(SignKeyPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// 找出并移除「本工具加过、但当前已不再有效」的绑定，返回被移除物体的描述。
        /// 别人挂的组件（key 不以本工具前缀开头）一律保留。
        /// </summary>
        static List<string> StripStaleBindings(
            UnityEngine.SceneManagement.Scene scene, HashSet<string> validKeys)
        {
            var removed = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                // GetComponentsInChildren 返回的是快照，循环中销毁组件是安全的。
                foreach (var localized in root.GetComponentsInChildren<LocalizedText>(true))
                {
                    if (localized == null || !IsOurs(localized.Key)) continue;
                    if (validKeys.Contains(localized.Key)) continue;

                    removed.Add($"{localized.gameObject.name}（{localized.Key}）");
                    UnityEngine.Object.DestroyImmediate(localized);
                }
            }

            return removed;
        }

        static bool RefuseWhilePlaying()
        {
            if (!Application.isPlaying) return false;
            Debug.LogError(
                "[MuseumIntegration] 请先退出 Play 模式（点 ▶ 停止运行）再执行本菜单。\n" +
                "Unity 在播放状态会拒绝场景保存，产物会不完整而界面上不会明显报错。");
            return true;
        }

        /// <summary>备份主场景到仓库外的 _local-backup/，返回备份路径；失败返回 null。</summary>
        static string BackupScene()
        {
            try
            {
                if (!File.Exists(ScenePath))
                {
                    Debug.LogError($"[MuseumIntegration] 找不到 {ScenePath}。");
                    return null;
                }

                var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
                var folder = Path.Combine(repoRoot, "_local-backup");
                Directory.CreateDirectory(folder);

                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var target = Path.Combine(folder, $"MuseumStart-{stamp}.unity");
                File.Copy(ScenePath, target, true);

                Debug.Log($"[MuseumIntegration] 已备份主场景：{target}");
                return target;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MuseumIntegration] 备份失败，为安全起见中止接入：{exception.Message}");
                return null;
            }
        }

        static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            return null;
        }

        /// <summary>按名字找物体（含未激活的子孙）。同名时取第一个，并在日志里说明。</summary>
        static GameObject FindByName(UnityEngine.SceneManagement.Scene scene, string name)
        {
            GameObject first = null;
            int count = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                var all = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (t.name != name) continue;
                    count++;
                    if (first == null) first = t.gameObject;
                }
            }

            if (count > 1)
            {
                Debug.LogWarning(
                    $"[MuseumIntegration] 场景里有 {count} 个叫「{name}」的物体，取了第一个。" +
                    "请确认挂对了对象。");
            }

            return first;
        }
    }
}
