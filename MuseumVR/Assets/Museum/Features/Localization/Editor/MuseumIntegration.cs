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

        /// <summary>要挂上 LocalizedText 的既有文字（物体名 → key）。</summary>
        static readonly KeyValuePair<string, string>[] Signage =
        {
            new KeyValuePair<string, string>("Museum Title", "museum.sign.title"),
            new KeyValuePair<string, string>("Museum Subtitle", "museum.sign.subtitle"),
            new KeyValuePair<string, string>("Welcome", "museum.sign.welcome"),
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

            // ---- 2) 给既有标识挂 LocalizedText（只加组件，不改任何其它属性）----
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

                    // 别人已经挂过别的 key：不覆盖，只提醒。这是别人的内容决定，不该由本工具改。
                    report.Add($"⚠ 「{pair.Key}」已被绑定到「{existingComponent.Key}」，未覆盖。");
                    continue;
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

            foreach (var pair in Signage)
            {
                var target = FindByName(scene, pair.Key);
                if (target == null) continue;

                var localized = target.GetComponent<LocalizedText>();
                if (localized == null) continue;

                // 只移除本工具加的那三个 key，不动别人挂的组件。
                if (localized.Key != pair.Value)
                {
                    report.Add($"「{pair.Key}」上绑的是「{localized.Key}」，不是本工具加的，保留。");
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(localized);
                report.Add($"已从「{pair.Key}」移除 LocalizedText。");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[MuseumIntegration] 已撤销接入。\n  " + string.Join("\n  ", report));
        }

        // ------------------------------------------------------------------ 工具

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
