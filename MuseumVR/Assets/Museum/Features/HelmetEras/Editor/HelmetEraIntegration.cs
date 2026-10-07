using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Museum.Eras.EditorTools
{
    /// <summary>
    /// 把「年代切换」装备到主场景的中央展品头盔（HelmetExhibit）上。
    ///
    /// 产品决定（2026-10-06 与组长/成员确认的方向）：不做独立的年代展台小部件 ——
    /// 展厅中间的这**一件**展品就是年代切换的载体，与 Group Component「一个简化 Mark V」的承诺一致。
    ///
    /// 材质兼容性（PR #27 复审修正）：
    /// - 壳体/金属件年代材质使用 <c>Museum/Inspection Shell</c>（剖切/悬停属性载体）；
    /// - **窗口年代材质使用 <c>Museum/Inspection Glass</c>**（PR #24 引入的透明玻璃 shader，
    ///   同样支持剖切/悬停）—— 不能用不透明的 Shell 覆盖玻璃；
    /// - Verify Section Compatibility 按绑定槽位分别核对这两种 shader。
    ///
    /// 按钮位置（PR #27 复审修正）：第一版 (±0.45, 1.06, 2.95) 完全落在 Plinth
    /// （x±0.675、y0..1.1、z2.825..4.175）内部，射线先命中 Plinth，按钮无法点击。
    /// 现改为**展台顶面上方** y=1.155（按钮底 1.12 > 1.1），z=3.05、x=±0.30，
    /// 位于参观者正前方可直视直点的位置。
    ///
    /// 安全约定（主场景是全组共享、串行写入的文件）：
    /// <list type="bullet">
    /// <item>对头盔**只加组件**，不动它的位置、抓取、剖切、rear connections 与现有交互。</item>
    /// <item>执行前自动备份主场景到仓库外的 <c>_local-backup/</c>。</item>
    /// <item>可一键撤销：删除按钮根物体、摘掉加在头盔上的组件。</item>
    /// <item>Play 模式拒绝执行。</item>
    /// </list>
    /// </summary>
    public static class HelmetEraIntegration
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumStart.unity";
        const string CataloguePath = "Assets/Museum/Features/HelmetEras/HelmetEraCatalogue.asset";
        const string ChannelPath = "Assets/Museum/Features/HelmetEras/HelmetEraChannel.asset";
        const string BrassMaterialPath = "Assets/Museum/Features/HelmetInspection/Materials/Brass.mat";

        /// <summary>中央头盔在场景里的可能名字：队友把实例改名为 HelmetExhibit（2026-10-06 实测），
        /// 预制体本名仍是 InspectableHelmet —— 两个都认，忽略大小写与空格，递归查找。</summary>
        static readonly string[] HelmetRootNames = { "InspectableHelmet", "HelmetExhibit" };

        const string ControlsRootName = "Helmet Era Controls";
        const string LegacyStationName = "Helmet Era Station";

        // ------------------------------------------------------------------ 接入

        [MenuItem("Museum/HelmetEras/Integrate Era Switching Into MuseumStart", priority = 40)]
        public static void IntegrateEraSwitchingIntoMuseumStart()
        {
            if (RefuseWhilePlaying()) return;

            var catalogue = AssetDatabase.LoadAssetAtPath<HelmetEraCatalogue>(CataloguePath);
            var channel = AssetDatabase.LoadAssetAtPath<HelmetEraChannel>(ChannelPath);
            var brass = AssetDatabase.LoadAssetAtPath<Material>(BrassMaterialPath);
            if (catalogue == null || channel == null)
            {
                Debug.LogError("[HelmetEraIntegration] 缺少年代图鉴或事件通道资产（HelmetEras 模块根目录）。");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var backup = BackupScene();
            if (backup == null) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var report = new List<string>();

            // ---- 1) 找到中央头盔 ----
            var helmet = FindHelmetRoot();
            if (helmet == null)
            {
                Debug.LogError("[HelmetEraIntegration] 场景里找不到中央头盔（候选名：" +
                               string.Join(" / ", HelmetRootNames) + "）。主场景结构可能变了。");
                return;
            }

            // ---- 2) 挂切换器与键盘预览（只加组件，不动其它任何东西）----
            var switcher = helmet.GetComponent<HelmetEraSwitcher>();
            if (switcher == null)
            {
                switcher = Undo.AddComponent<HelmetEraSwitcher>(helmet);
                report.Add("已给「" + helmet.name + "」挂上 HelmetEraSwitcher。");
            }
            else
            {
                report.Add("头盔上已有 HelmetEraSwitcher，刷新其配置。");
            }
            switcher.Configure(catalogue, channel);
            EditorUtility.SetDirty(switcher);

            var preview = helmet.GetComponent<HelmetEraPlayModePreview>();
            if (preview == null)
            {
                preview = Undo.AddComponent<HelmetEraPlayModePreview>(helmet);
                report.Add("已挂上 HelmetEraPlayModePreview（编辑器内用 F9 / F10 切年代预览；数字键已按复审要求停用）。");
            }

            // ---- 3) 按部位名自动建立渲染器绑定 ----
            var bindings = new List<HelmetEraSwitcher.SlotBinding>();
            var unmatched = new List<string>();
            foreach (var renderer in helmet.GetComponentsInChildren<Renderer>(true))
            {
                if (IsToolingVisual(renderer.transform)) continue;   // 剖切工具/标注，不属于头盔本体
                if (TryClassify(renderer.gameObject.name, out var slot))
                {
                    bindings.Add(new HelmetEraSwitcher.SlotBinding
                    {
                        target = renderer,
                        slot = slot,
                        subMaterialIndex = 0,
                        restoreOriginalWhenEmpty = true,
                    });
                }
                else
                {
                    unmatched.Add(renderer.gameObject.name);
                }
            }
            switcher.ConfigureBindings(bindings.ToArray());
            EditorUtility.SetDirty(switcher);
            report.Add($"渲染器绑定 {bindings.Count} 条：玻璃→Window（Inspection Glass），" +
                       "帽壳/颈圈→Shell（Inspection Shell），其余金属件→Fitting。");
            if (unmatched.Count > 0)
                report.Add($"未分类（保持原材质，共 {unmatched.Count} 个，前几个）："
                           + string.Join("、", unmatched.GetRange(0, Mathf.Min(5, unmatched.Count)).ToArray()));

            // ---- 4) 参观者用的切换按钮（展台顶面上方，射线可按；复审 P1 修正）----
            var controls = GameObject.Find(ControlsRootName);
            if (controls == null)
            {
                controls = new GameObject(ControlsRootName);
                Undo.RegisterCreatedObjectUndo(controls, "Create Helmet Era Controls");

                CreateButton(controls.transform, "Era Button - Previous", new Vector3(-0.30f, 1.155f, 3.05f),
                             brass, switcher, HelmetEraButton.StepDirection.Previous, report);
                CreateButton(controls.transform, "Era Button - Next", new Vector3(0.30f, 1.155f, 3.05f),
                             brass, switcher, HelmetEraButton.StepDirection.Next, report);
                report.Add("已创建两个射线按钮（左=上一个年代，右=下一个年代），位于展台顶面上方 " +
                           "(±0.30, 1.155, 3.05)，不再被 Plinth 挡住。");
            }
            else
            {
                report.Add("「" + ControlsRootName + "」已存在，跳过按钮创建。");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[HelmetEraIntegration] 接入完成：\n- " + string.Join("\n- ", report.ToArray()));
        }

        // ------------------------------------------------------------------ 撤销

        [MenuItem("Museum/HelmetEras/Remove Era Switching From MuseumStart", priority = 41)]
        public static void RemoveEraSwitchingFromMuseumStart()
        {
            if (RefuseWhilePlaying()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var report = new List<string>();

            var controls = GameObject.Find(ControlsRootName);
            if (controls != null)
            {
                Undo.DestroyObjectImmediate(controls);
                report.Add("已删除「" + ControlsRootName + "」。");
            }

            var helmet = FindHelmetRoot();
            if (helmet != null)
            {
                foreach (var type in new[] { typeof(HelmetEraSwitcher), typeof(HelmetEraPlayModePreview) })
                {
                    var component = helmet.GetComponent(type);
                    if (component != null)
                    {
                        Undo.DestroyObjectImmediate(component);
                        report.Add("已从头盔摘下 " + type.Name + "。");
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HelmetEraIntegration] 撤销完成：\n- " + string.Join("\n- ", report.ToArray()));
        }

        // ------------------------------------------------------------------ 验证

        static readonly int CutEnabledId = Shader.PropertyToID("_CutEnabled");
        static readonly int CutPlaneId = Shader.PropertyToID("_CutPlane");
        static readonly int HoverId = Shader.PropertyToID("_Hover");
        const string SectionShaderName = "Museum/Inspection Shell";
        const string GlassShaderName = "Museum/Inspection Glass";

        /// <summary>
        /// 自动验证「换年代材质之后，检视剖切链路是否完好」（复审第 2 条修正版）。
        ///
        /// 做法：依次套用三个年代，然后核对检视控制器引用的每一个剖切/高亮渲染器：
        /// 按其在切换器绑定里的槽位分别核对 —— Window 渲染器必须使用
        /// Museum/Inspection Glass（透明玻璃），其余必须使用 Museum/Inspection Shell；
        /// 两者都必须带 _CutEnabled / _CutPlane / _Hover。测完恢复原材质。
        /// </summary>
        [MenuItem("Museum/HelmetEras/Verify Section Compatibility", priority = 42)]
        public static void VerifySectionCompatibility()
        {
            if (RefuseWhilePlaying()) return;

            var helmet = FindHelmetRoot();
            if (helmet == null)
            {
                Debug.LogError("[HelmetEraIntegration] 找不到中央头盔，请先运行 Integrate。");
                return;
            }
            var switcher = helmet.GetComponent<HelmetEraSwitcher>();
            if (switcher == null || switcher.Catalogue == null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[HelmetEraIntegration] 诊断（请整段截图发给 AI）：");
                sb.AppendLine("  活动场景 = " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                sb.AppendLine("  找到的头盔 = " + helmet.name + "  (instanceID " + helmet.GetInstanceID() + ")");
                sb.AppendLine("  头盔是否为预制体实例: " + (PrefabUtility.IsPartOfPrefabInstance(helmet)));
                sb.AppendLine("  GetComponent<HelmetEraSwitcher>() = " + (switcher == null ? "null" : "非空"));
                if (switcher != null)
                {
                    var so2 = new SerializedObject(switcher);
                    var catProp = so2.FindProperty("catalogue");
                    sb.AppendLine("  switcher.Catalogue = " + (switcher.Catalogue == null ? "null" : switcher.Catalogue.name));
                    sb.AppendLine("  序列化 catalogue 字段 = " + (catProp != null && catProp.objectReferenceValue != null
                        ? catProp.objectReferenceValue.name
                        : "null"));
                }
                var allSwitchers = helmet.GetComponentsInChildren<HelmetEraSwitcher>(true);
                sb.AppendLine("  头盔子树内 Switcher（含未激活）数量 = " + allSwitchers.Length);
                Debug.LogError(sb.ToString());
                return;
            }
            var controllerType = System.Type.GetType(
                "Museum.Inspection.HelmetInspectionController, Assembly-CSharp")
                ?? typeof(HelmetEraSwitcher).Assembly.GetType("Museum.Inspection.HelmetInspectionController");
            UnityEngine.Object controller = controllerType != null
                ? helmet.GetComponent(controllerType)
                : null;
            if (controller == null)
            {
                Debug.LogError("[HelmetEraIntegration] 头盔上找不到 HelmetInspectionController —— 检视模块可能变了。");
                return;
            }

            // 记录原材质，测完恢复
            var original = new Dictionary<Renderer, Material[]>();
            foreach (var r in helmet.GetComponentsInChildren<Renderer>(true))
                original[r] = r.sharedMaterials;

            int pass = 0, fail = 0;
            var failures = new List<string>();

            for (var eraIndex = 0; eraIndex < switcher.AvailableEraCount; eraIndex++)
            {
                var era = switcher.Catalogue[eraIndex];
                if (era == null) continue;
                switcher.ShowEra(eraIndex);   // 编辑器内瞬时切换（duration 默认 0，不走协程）

                var so = new SerializedObject(controller);
                foreach (var fieldName in new[] { "clippedRenderers", "highlightedRenderers" })
                {
                    var prop = so.FindProperty(fieldName);
                    if (prop == null || !prop.isArray) continue;
                    for (var i = 0; i < prop.arraySize; i++)
                    {
                        var r = prop.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                        if (r == null) continue;
                        var mat = r.sharedMaterial;
                        if (mat == null || mat.shader == null)
                        {
                            fail++;
                            failures.Add($"{era.DisplayName} / {fieldName}[{i}] {r.name} → 材质为空");
                            continue;
                        }

                        // 按绑定槽位判定期望 shader：Window→Glass，其余→Shell；
                        // 不在绑定里的渲染器只要求带剖切属性（宽松）。
                        string expected = null;
                        foreach (var b in switcher.Bindings)
                        {
                            if (b.target == r) { expected = b.slot == HelmetMaterialSlot.Window
                                ? GlassShaderName : SectionShaderName; break; }
                        }
                        var ok = mat.HasProperty(CutEnabledId)
                            && mat.HasProperty(CutPlaneId)
                            && mat.HasProperty(HoverId)
                            && (expected == null || mat.shader.name == expected);
                        if (ok) pass++;
                        else
                        {
                            fail++;
                            failures.Add($"{era.DisplayName} / {fieldName}[{i}] {r.name} → " +
                                         $"shader = {mat.shader.name}（期望 {(expected ?? "带剖切属性的任意 shader")}）");
                        }
                    }
                }
            }

            // 恢复原材质（引用相等写回，不会弄脏场景）
            foreach (var kv in original)
                if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;

            if (fail == 0)
                Debug.Log($"[HelmetEraIntegration] ✅ 剖切兼容性验证通过：三个年代 × {pass} 个剖切/高亮渲染器，" +
                          "Window 渲染器均为 Museum/Inspection Glass、其余均为 Museum/Inspection Shell，" +
                          "且全部带 _CutEnabled/_CutPlane/_Hover。属性块写入路径成立。");
            else
                Debug.LogError($"[HelmetEraIntegration] ❌ 剖切兼容性验证失败 {fail} 项（通过 {pass}）：\n- "
                               + string.Join("\n- ", failures.ToArray()));
        }

        // ------------------------------------------------------------------ 工具

        /// <summary>按名字找中央头盔：忽略大小写与空格，先查根物体再递归整棵层级（队友可能移动过它）。</summary>
        static GameObject FindHelmetRoot()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = FindInHierarchy(root.transform);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        static Transform FindInHierarchy(Transform node)
        {
            var compact = node.name.Replace(" ", "");
            foreach (var candidate in HelmetRootNames)
            {
                if (compact.Equals(candidate, System.StringComparison.OrdinalIgnoreCase))
                    return node;
            }
            for (var i = 0; i < node.childCount; i++)
            {
                var hit = FindInHierarchy(node.GetChild(i));
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>剖切工具与标注不是头盔本体，不参与年代换色。</summary>
        static bool IsToolingVisual(Transform node)
        {
            while (node != null)
            {
                if (node.name.Contains("Section Plane") || node.name == "InterpretiveLabel")
                    return true;
                node = node.parent;
            }
            return false;
        }

        /// <summary>按部件名的关键词把渲染器归入年代档案的材质槽位。</summary>
        static bool TryClassify(string name, out HelmetMaterialSlot slot)
        {
            var n = name.ToLowerInvariant();
            if (n.Contains("glass")) { slot = HelmetMaterialSlot.Window; return true; }
            if (n.Contains("bonnet") || n.Contains("corselet") || n.Contains("shoulderplate"))
            {
                slot = HelmetMaterialSlot.Shell;
                return true;
            }
            if (n.Contains("portrim") || n.Contains("neckfastener") || n.Contains("airconnector")
                || n.Contains("hinge") || n.Contains("frontlatch") || n.Contains("weightattachmenteye")
                || n.Contains("diamondguard") || n.Contains("windowframe"))
            {
                slot = HelmetMaterialSlot.Fitting;
                return true;
            }
            slot = default;
            return false;
        }

        /// <summary>创建一个可射线按压的年代切换按钮（用内置 Cube，自带网格与碰撞体）。</summary>
        static void CreateButton(Transform parent, string name, Vector3 position, Material material,
                                 HelmetEraSwitcher switcher, HelmetEraButton.StepDirection direction,
                                 List<string> report)
        {
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(parent, false);
            button.transform.position = position;
            button.transform.localScale = new Vector3(0.16f, 0.07f, 0.04f);

            button.AddComponent<XRSimpleInteractable>();
            var renderer = button.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;

            var eraButton = button.AddComponent<HelmetEraButton>();
            eraButton.Configure(switcher, direction, renderer, null);
            report.Add("  按钮 " + name + " @ " + position);
        }

        static bool RefuseWhilePlaying()
        {
            if (!Application.isPlaying) return false;
            Debug.LogError(
                "[HelmetEraIntegration] 当前处于 Play 模式，Unity 会拒绝场景保存，" +
                "产物会不完整且无明显报错。请先退出 Play 模式再执行。");
            return true;
        }

        static string BackupScene()
        {
            var source = Path.GetFullPath(Path.Combine(Application.dataPath,
                ScenePath.Replace("Assets/", string.Empty)));
            if (!File.Exists(source))
            {
                Debug.LogError($"[HelmetEraIntegration] 找不到主场景文件：{source}");
                return null;
            }

            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var backupDir = Path.Combine(repoRoot, "_local-backup");
            Directory.CreateDirectory(backupDir);

            var stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backup = Path.Combine(backupDir, $"MuseumStart-{stamp}.unity");
            File.Copy(source, backup, overwrite: false);
            Debug.Log($"[HelmetEraIntegration] 主场景已备份到 {backup}");
            return backup;
        }
    }
}
