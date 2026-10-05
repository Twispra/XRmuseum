using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Museum.Localization.EditorTools
{
    /// <summary>
    /// 一键建出本地化所需的数据与演示 Prefab。
    ///
    /// 与 HelmetEras 同样的隔离策略：**不修改 MuseumStart.unity**，
    /// 演示 Prefab 自带文字板与两个按钮，接入主场景由整合人决定。
    /// </summary>
    public static class LocalizationSetup
    {
        const string Root = "Assets/Museum/Features/Localization";
        const string TableFolder = Root + "/Tables";
        const string PrefabFolder = Root + "/Prefabs";
        const string MaterialFolder = Root + "/Materials";
        const string FontFolder = Root + "/Fonts";
        const string SceneFolder = Root + "/Scenes";
        const string SceneName = "LanguageDemo";

        /// <summary>
        /// 展厅的默认界面语言。
        ///
        /// 刻意设成**英文**：这样接入主场景后「展厅的默认外观与接入之前完全一致」，
        /// 访客主动选择中文才会切换。目前可本地化的只有入口三处标识，
        /// 其余 37 处文字仍是英文 —— 若默认中文，展厅会呈现"半中半英"的状态。
        /// 等全馆文案补齐中文之后，改成 <c>MuseumLanguage.Chinese</c> 更合适。
        /// </summary>
        const MuseumLanguage DefaultMuseumLanguage = MuseumLanguage.English;

        // 源字体候选，按顺序取第一个存在的。
        //
        // 第一项是**系统原始字体**（SIL OFL 1.1），第二项是用 fontTools 从它生成的子集。
        // 子集字体在 Python/PIL(FreeType) 下渲染完全正常，但 **Unity TMP 一个字都烤不出来**
        // （图集恒为 0×0，已烤字形数 0），说明问题出在「变量字体 → 静态实例 + 子集化」这一步
        // 产出的 glyf 表上。因此默认先用原始字体验证链路，确认后再决定最终提交哪一个。
        const string OriginalFontPath = FontFolder + "/NotoSansSC-MuseumSource.ttf";
        const string SubsetFontPath = FontFolder + "/NotoSansSC-MuseumSubset.ttf";

        static string ResolveSourceFontPath()
        {
            if (System.IO.File.Exists(OriginalFontPath)) return OriginalFontPath;
            return SubsetFontPath;
        }
        const string FontAssetPath = FontFolder + "/NotoSansSC-MuseumSubset SDF.asset";
        const string ChannelPath = TableFolder + "/LanguageChannel.asset";
        const string TablePath = TableFolder + "/LocalizationTable.asset";
        const string ShowcaseName = "LanguageShowcase";

        // ---------------------------------------------------------------- 菜单

        /// <summary>
        /// 一步到位：生成资产与展示 Prefab，再生成可直接播放的演示场景。
        /// 评审的人只需要执行这一个菜单、然后打开生成的场景按 ▶。
        /// </summary>
        [MenuItem("Museum/Localization/Create Everything and Demo Scene", priority = 9)]
        public static void CreateEverything()
        {
            CreateAll();
            CreateDemoScene();
        }

        [MenuItem("Museum/Localization/Create Language Assets and Showcase", priority = 10)]
        public static void CreateAll()
        {
            if (RefuseWhilePlaying()) return;
            EnsureFolders();

            // 先刷新一次：外面直接改过磁盘上的文件时（例如手工删除了坏掉的字体资产），
            // 不刷新会拿到 Unity 缓存里的旧对象，导致引用指向已经不存在的资产。
            AssetDatabase.Refresh();

            var font = CreateFontAsset();
            var channel = CreateChannel();
            var table = CreateTable(font);
            var materials = CreateMaterials();
            var prefabPath = BuildShowcase(table, channel, materials);

            var report = new StringBuilder();
            report.AppendLine("本地化系统资产已创建");
            report.AppendLine($"  字体资产   {FontAssetPath}{(font == null ? "  ← 创建失败，见上方报错" : string.Empty)}");
            report.AppendLine($"  语言通道   {ChannelPath}");
            report.AppendLine($"  文案表     {TablePath}（{table.Entries.Count} 条）");
            report.AppendLine($"  展示 Prefab {prefabPath}");
            report.AppendLine();
            report.AppendLine("该 Prefab 独立于 MuseumStart，不会影响其他成员正在编辑的场景。");
            report.AppendLine("把它拖进任意测试场景即可预览；接入主场景由整合人决定。");
            report.AppendLine();
            report.AppendLine("没有头显时的预览方式：进入 Play 后按 L 循环切换，数字键 1 / 2 直接选中文 / 英文。");
            Debug.Log(report.ToString());

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        [MenuItem("Museum/Localization/Create Chinese Font Asset Only", priority = 11)]
        public static void CreateFontOnly()
        {
            EnsureFolders();
            var font = CreateFontAsset();
            Debug.Log(font == null
                ? "[LocalizationSetup] 字体资产创建失败，请查看上方的具体报错。"
                : $"[LocalizationSetup] 字体资产就绪：{FontAssetPath}");
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[] { Root, TableFolder, PrefabFolder, MaterialFolder, FontFolder })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                var parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
                var leaf = System.IO.Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        /// <summary>
        /// Play 模式下拒绝执行。
        ///
        /// Unity 在播放状态会拒绝 TMP 字体生成与场景生成相关的调用（抛
        /// <c>InvalidOperationException: This cannot be used during play mode.</c>），
        /// 结果是**资产只生成了一半、场景没更新**，而界面上看不出明显错误，极易误判。
        /// 这一道护栏就是为了不让这种情况再发生。
        /// </summary>
        static bool RefuseWhilePlaying()
        {
            if (!Application.isPlaying) return false;

            Debug.LogError(
                "[LocalizationSetup] 请先退出 Play 模式（点 ▶ 停止运行）再执行本菜单。\n" +
                "原因：Unity 在播放状态下会拒绝 TMP 字体生成与场景保存，产物会不完整，" +
                "而界面上不会明显报错。退出 Play 后重新执行即可。");
            return true;
        }

        // ---------------------------------------------------------------- 字体

        /// <summary>
        /// 从仓库里的子集字体生成 TMP 字体资产。
        ///
        /// 源字体取自系统安装的 Noto Sans SC（SIL OFL 1.1），
        /// 先用 fontTools 实例化成静态 Regular 并子集化（见 Fonts/subset-noto-sans-sc.py），
        /// 只把子集结果提交进仓库——**不提交 17 MB 的原始变量字体**。
        /// </summary>
        static TMP_FontAsset CreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null && HasUsableAtlas(existing))
            {
                return existing;
            }

            if (existing != null)
            {
                // 资产在，但图集是空的（曾经生成失败过）。直接重建，避免留下坏资产。
                Debug.LogWarning($"[LocalizationSetup] {FontAssetPath} 存在但没有可用图集，将重新生成。");
                AssetDatabase.DeleteAsset(FontAssetPath);
            }

            var sourceFontPath = ResolveSourceFontPath();
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[LocalizationSetup] 找不到字体文件 {sourceFontPath}。" +
                               "请确认已把字体放进 Fonts/ 目录（见该目录下 README）。");
                return null;
            }

            Debug.Log($"[LocalizationSetup] 使用源字体：{sourceFontPath}");

            TMP_FontAsset fontAsset;
            try
            {
                // 用 **Static** 模式在生成时就把字烤进图集。
                //
                // 换过两次 Dynamic 都不行：
                //   单参数重载（默认 1024×1024 / 采样 90）放不下汉字 → TMP 另开第二个图集，
                //   而材质仍指第一张 → 实心矩形；
                //   8 参数 + Dynamic 则**根本不创建图集**（延迟分配），保存出来只有 6 KB、
                //   图集尺寸 0×0 → 采样到空数据 → 还是实心矩形。
                // Static 模式会真正建好图集并把字写进去，代价是以后新增文案要重跑本菜单。
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    source,
                    60,      // samplingPointSize
                    5,       // atlasPadding
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                    2048,    // atlasWidth
                    2048,    // atlasHeight
                    AtlasPopulationMode.Static,
                    true);   // enableMultiAtlasSupport
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[LocalizationSetup] TMP_FontAsset.CreateFontAsset 失败：{exception.Message}");
                return null;
            }

            if (fontAsset == null)
            {
                Debug.LogError("[LocalizationSetup] TMP_FontAsset.CreateFontAsset 返回空。");
                return null;
            }

            fontAsset.name = "NotoSansSC-MuseumSubset SDF";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.isMultiAtlasTexturesEnabled = true;

            // Static 模式必须显式指定要烤哪些字，否则图集里一个字形都没有。
            var required = CollectRequiredCharacters();
            if (!fontAsset.TryAddCharacters(required, out string missingCharacters))
            {
                Debug.LogWarning($"[LocalizationSetup] 有 {missingCharacters.Length} 个字符没能烤进字体图集：" +
                                 $"'{missingCharacters}'。这些字会显示成方块。");
            }

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // **所有**图集纹理都要作为子资产保存，不能只存第 0 个。
            for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
            {
                var texture = fontAsset.atlasTextures[i];
                if (texture == null) continue;
                texture.name = "Atlas " + i;
                AssetDatabase.AddObjectToAsset(texture, fontAsset);
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = "NotoSansSC-MuseumSubset Material";
                if (fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
                    fontAsset.material.SetTexture("_MainTex", fontAsset.atlasTextures[0]);
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);

            var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (saved == null || !HasUsableAtlas(saved))
            {
                Debug.LogError(
                    "[LocalizationSetup] 脚本生成的字体资产没有可用图集。\n" +
                    "改用 Unity 官方工具手工生成一次即可（之后本菜单会复用）：\n" +
                    "  Window → TextMeshPro → Font Asset Creator\n" +
                    $"    Source Font File : {sourceFontPath}\n" +
                    "    Sampling Point Size : Custom 110     Padding : 6\n" +
                    "    Atlas Resolution : 2048 x 2048\n" +
                    "    Render Mode : SDFAA     Atlas Population Mode : Dynamic\n" +
                    "    Character Set : Characters from File（或用 Custom Characters 填入常用汉字）\n" +
                    "    → Generate Font Atlas → Save as... 存到 Fonts/ 目录，文件名保持 " +
                    $"{System.IO.Path.GetFileName(FontAssetPath)}");
            }

            return saved;
        }

        /// <summary>本模块所有会被渲染的文字，去重后拼成一串，供烤图集使用。</summary>
        static string CollectRequiredCharacters()
        {
            var set = new HashSet<char>();

            void Add(string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                foreach (var character in text)
                {
                    if (char.IsWhiteSpace(character)) continue; // 空白不需要字形
                    set.Add(character);
                }
            }

            foreach (var seed in Seeds)
            {
                Add(seed.zh);
                Add(seed.en);
            }

            // 语言按钮标签用各自的原生名称，不随语言切换
            Add(MuseumLanguage.Chinese.NativeName());
            Add(MuseumLanguage.English.NativeName());

            // 缺 key 时的标记 [[key]]，以及兜底用的 ASCII
            Add("[[ ]]0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:;()%-/+=?!'\"@#&*");

            var builder = new StringBuilder(set.Count);
            foreach (var character in set) builder.Append(character);
            return builder.ToString();
        }

        static bool HasUsableAtlas(TMP_FontAsset fontAsset)
        {
            return fontAsset != null
                   && fontAsset.atlasTextures != null
                   && fontAsset.atlasTextures.Length > 0
                   && fontAsset.atlasTextures[0] != null
                   && fontAsset.atlasTextures[0].width > 0
                   && fontAsset.atlasTextures[0].height > 0;
        }

        // ---------------------------------------------------------------- 资产

        static LanguageChannel CreateChannel()
        {
            var channel = AssetDatabase.LoadAssetAtPath<LanguageChannel>(ChannelPath);

            if (channel == null)
            {
                channel = ScriptableObject.CreateInstance<LanguageChannel>();
                AssetDatabase.CreateAsset(channel, ChannelPath);
            }

            // 初始语言也一并拉回统一默认值。通道是生成出来的资产，
            // 初始语言属于代码级决定，这样「重新执行菜单」的结果是可预期的，
            // 不会因为某次手动改过而留下一个没人记得的状态。
            var serialized = new SerializedObject(channel);
            var property = serialized.FindProperty("initial");
            if (property != null && property.enumValueIndex != (int)DefaultMuseumLanguage)
            {
                property.enumValueIndex = (int)DefaultMuseumLanguage;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(channel);
                Debug.Log($"[LocalizationSetup] 语言通道的初始语言已设为 {DefaultMuseumLanguage}。");
            }

            AssetDatabase.SaveAssets();
            return channel;
        }

        static LocalizationTable CreateTable(TMP_FontAsset font)
        {
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LocalizationTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            var so = new SerializedObject(table);

            so.FindProperty("fontAsset").objectReferenceValue = font;

            var entries = so.FindProperty("entries");
            entries.arraySize = Seeds.Length;
            for (int i = 0; i < Seeds.Length; i++)
            {
                var element = entries.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").stringValue = Seeds[i].key;
                element.FindPropertyRelative("chinese").stringValue = Seeds[i].zh;
                element.FindPropertyRelative("english").stringValue = Seeds[i].en;
                element.FindPropertyRelative("note").stringValue = Seeds[i].note;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return table;
        }

        static Dictionary<string, Material> CreateMaterials()
        {
            var result = new Dictionary<string, Material>();
            result["Board"] = EnsureMaterial("Language-Board", new Color(0.10f, 0.12f, 0.15f), 0.0f, 0.25f);
            result["Button"] = EnsureMaterial("Language-Button", new Color(0.16f, 0.42f, 0.52f), 0.0f, 0.45f);
            result["ButtonPressed"] = EnsureMaterial("Language-ButtonPressed", new Color(0.90f, 0.68f, 0.20f), 0.0f, 0.60f);
            return result;
        }

        static Material EnsureMaterial(string materialName, Color color, float metallic, float smoothness)
        {
            var path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- Prefab

        static string BuildShowcase(LocalizationTable table, LanguageChannel channel,
            Dictionary<string, Material> materials)
        {
            var path = $"{PrefabFolder}/{ShowcaseName}.prefab";
            var root = new GameObject(ShowcaseName);

            try
            {
                // 版面朝向 -Z（新建场景的 Main Camera 默认朝 +Z 看）。
                //
                // ⚠️ 字号换算（踩过两次坑，记牢）：
                // 3D `TextMeshPro` 的 `fontSize` 单位是**点**，不是世界单位。
                // 物体缩放为 1 时，**世界高度 ≈ fontSize × 0.1**。
                // 第一版我按「世界单位」直接写了 0.22，结果只有 0.022 米高——小了 10 倍，
                // 在画面里就是看不清的小点（曾被误判成「方块字」）。
                // 所以下面的字号都按「目标世界高度 × 10」填写。
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "Board";
                board.transform.SetParent(root.transform, false);
                board.transform.localPosition = new Vector3(0f, 1.45f, 0.60f);
                board.transform.localScale = new Vector3(2.10f, 1.55f, 0.03f);
                Object.DestroyImmediate(board.GetComponent<Collider>());
                board.GetComponent<Renderer>().sharedMaterial = materials["Board"];

                // 板面前表面在 z = 0.585，文字放在它前面；四层由下往上排开，互不重叠。
                // 字号按「目标世界高度 × 10」写（3D TMP 的 fontSize 单位是点，见文件头注释）。
                //   标题 fontSize 2.0 → 0.20 单位/字，9 个汉字 ≈ 1.80 宽（rect 给 1.95，一行放得下）
                //   正文 fontSize 1.0 → 0.10 单位/字，中英文各约 2 行 ≈ 0.24 高（rect 给 0.45）
                AddLocalizedText(root.transform, table, channel, "Title", "museum.title",
                    new Vector3(0f, 1.98f, 0.575f), 2.0f, new Vector2(1.95f, 0.30f));
                AddLocalizedText(root.transform, table, channel, "Body", "welcome.body",
                    new Vector3(0f, 1.52f, 0.575f), 1.0f, new Vector2(1.95f, 0.45f));
                AddLocalizedText(root.transform, table, channel, "Prompt", "language.prompt",
                    new Vector3(0f, 1.19f, 0.575f), 1.2f, new Vector2(1.95f, 0.22f));

                CreateLanguageButton(root.transform, table, channel, materials, "Language Button - Chinese",
                    LanguageButton.Choice.Chinese, "中文", new Vector3(-0.44f, 0.92f, 0.545f));
                CreateLanguageButton(root.transform, table, channel, materials, "Language Button - English",
                    LanguageButton.Choice.English, "English", new Vector3(0.44f, 0.92f, 0.545f));

                root.AddComponent<LanguagePlayModePreview>().Configure(channel);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                return AssetDatabase.GetAssetPath(prefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void AddLocalizedText(Transform parent, LocalizationTable table, LanguageChannel channel,
            string name, string key, Vector3 localPosition, float fontSize, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var tmp = go.AddComponent<TextMeshPro>();
            // 生成时就把中文字体设好：LocalizedText 运行期还会再设一次，
            // 但编辑状态下的预览和校验工具都要靠这里。
            if (table != null && table.FontAsset != null) tmp.font = table.FontAsset;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.rectTransform.sizeDelta = size;
            tmp.text = key; // 先放 key，运行时会被 LocalizedText 覆盖

            go.AddComponent<LocalizedText>().Configure(table, channel, key);
        }

        static void CreateLanguageButton(Transform parent, LocalizationTable table, LanguageChannel channel,
            Dictionary<string, Material> materials, string name, LanguageButton.Choice choice,
            string nativeLabel, Vector3 localPosition)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(0.52f, 0.20f, 0.04f);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = materials["Button"];

            // 标签挂在根节点而不是按钮下面：按钮是非等比缩放，挂上去文字会被一起压扁。
            // 用该语言的原生名称，所以它不随语言切换变化，不需要 LocalizedText。
            var labelGo = new GameObject(name + " Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = localPosition + new Vector3(0f, 0f, -0.035f);
            var label = labelGo.AddComponent<TextMeshPro>();
            // 必须显式套上中文字体：「中文」这两个字不在 LiberationSans 里，
            // 不设字体就会显示成方块（这是实机验证时抓到的）。
            if (table != null && table.FontAsset != null) label.font = table.FontAsset;
            // 字号同样按「世界高度 × 10」写：目标约 0.105 米高
            label.fontSize = 1.05f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(0.6f, 0.25f);
            label.text = nativeLabel;

            // XRSimpleInteractable 会自动把同一个 GameObject 上的碰撞体登记进
            // 自己的 colliders 列表；这里再手工 Add 一次会触发
            // 「A collider used by an interactable object is already registered」警告。
            go.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>();

            go.AddComponent<LanguageButton>().Configure(channel, choice, renderer,
                materials["ButtonPressed"], label);
        }

        // ---------------------------------------------------------------- 演示场景

        /// <summary>
        /// 生成一个「打开就能按 ▶」的演示场景。
        ///
        /// 目的是把「拖预制体、摆相机、调位置」这些手工步骤也自动化掉——
        /// 评审的人只需要打开场景按下播放，不必自己搭。
        /// 场景是本模块自己的资产，**不碰 MuseumStart.unity**。
        /// </summary>
        [MenuItem("Museum/Localization/Create Demo Scene", priority = 12)]
        public static void CreateDemoScene()
        {
            if (RefuseWhilePlaying()) return;

            var prefabPath = $"{PrefabFolder}/{ShowcaseName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[LocalizationSetup] 找不到 {prefabPath}，请先执行 Create Language Assets and Showcase。");
                return;
            }

            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder(Root, "Scenes");

            // 生成演示场景会切换当前打开的场景。先给一次保存机会，
            // 否则用户手头未保存的改动会被无声丢弃。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[LocalizationSetup] 已取消，演示场景未生成。");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var showcase = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (showcase != null) showcase.transform.position = Vector3.zero;

            var camera = Object.FindObjectOfType<Camera>();
            if (camera != null)
            {
                // 正对展示板；板心在 y = 1.45、板前表面在 z ≈ 0.585。
                // 相机放得比较近（离板约 2.2 米），保证在默认窗口尺寸下文字也够大够清楚。
                // 背景色比展示板亮一档，否则深色板子会糊在深色背景里看不见边界。
                camera.transform.position = new Vector3(0f, 1.45f, -1.65f);
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.17f, 0.18f, 0.21f);
            }
            else
            {
                Debug.LogWarning("[LocalizationSetup] 演示场景里没有相机，打开后需要自己加一个。");
            }

            var scenePath = $"{SceneFolder}/{SceneName}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[LocalizationSetup] 演示场景已生成：{scenePath}\n" +
                      "打开它直接按 ▶ 即可，按 1 / 2 切换语言、L 循环切换。");
        }

        // ---------------------------------------------------------------- 批处理入口

        /// <summary>
        /// 无人值守入口，供命令行调用：
        /// <c>Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod Museum.Localization.EditorTools.LocalizationSetup.BatchCreateAll</c>
        /// 一次跑完：生成资产 → 生成展示 Prefab → 生成演示场景 → 自动校验。
        /// 校验全通过时退出码为 0。
        /// </summary>
        public static void BatchCreateAll()
        {
            try
            {
                CreateAll();
                CreateDemoScene();
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[LocalizationSetup] 批处理生成失败：{exception}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // 内部会在批处理模式下按校验结果设置退出码
            LocalizationValidation.RunBatch();
        }


        struct Seed
        {
            public string key;
            public string zh;
            public string en;
            public string note;

            public Seed(string key, string zh, string en, string note)
            {
                this.key = key;
                this.zh = zh;
                this.en = en;
                this.note = note;
            }
        }

        /// <summary>
        /// 起步文案。覆盖用户旅程的开头与无障碍说明，**不是最终展陈文案**——
        /// 展陈内容应由 B 角色在试点评价后替换，这里只保证切换链路可用、可演示。
        /// </summary>
        static readonly Seed[] Seeds =
        {
            new Seed("museum.title",
                "无障碍博物馆体验",
                "Accessible Museum Experience",
                "演示板的标题。"),
            new Seed("welcome.body",
                "欢迎。这段体验可以坐着完成，只需单手操作。你可以随时回到这里切换语言。",
                "Welcome. This experience works seated and one-handed. You can change the language here at any time.",
                "欢迎说明，同时点出两项无障碍承诺。英文字数刻意压到三行以内，避免与下方提示重叠。"),
            new Seed("language.prompt",
                "请选择界面语言",
                "Choose your interface language",
                "语言选择提示。"),

            // ---- 主场景入口标识的英译中 ----
            // ⚠️ 目标必须是**游客真正看得见**的标识。
            // 第一版绑的是 Museum Title / Museum Subtitle / Welcome，但那三个是展厅改造后
            // **保留下来的隐藏旧对象**（MeshRenderer m_Enabled = 0，被 #9 的按需显隐逻辑关掉了），
            // 只挂组件根本不会让游客看到任何变化（Issue #17）。
            // 现在改绑 MaritimeGallery 预制体里真正在渲染的入口标识。
            //
            // 英文一律**逐字照抄**场景/预制体里的原文，否则挂上组件的瞬间就等于改掉了展陈文案。
            new Seed("museum.sign.masthead",
                "海面之下",
                "BENEATH THE SURFACE",
                "展馆主标题（Gallery masthead，字号 5，实际可见）。"),
            new Seed("museum.sign.gallery",
                "潜水展厅",
                "THE DIVING GALLERY",
                "展厅名。Gallery subtitle 与 Arrival brand 两个物体共用这一条。"),
            new Seed("museum.sign.intro",
                "一件展品，多种看法。",
                "One object. Many ways to explore.",
                "入口欢迎语（Arrival introduction，实际可见）。"),
            new Seed("controls.point",
                "用手柄指向按钮，按住握把键确认。",
                "Point with your controller and hold the grip button to confirm.",
                "操作提示，与主场景既有按钮的交互方式一致。"),
            new Seed("journey.begin",
                "开始参观",
                "Begin",
                "旅程第一步。"),
            new Seed("journey.select_object",
                "选择一件展品",
                "Choose an object",
                "旅程第二步。"),
            new Seed("journey.explore",
                "自由观察",
                "Explore freely",
                "旅程第三步。"),
            new Seed("journey.explain",
                "细节说明",
                "Details",
                "旅程第四步。"),
            new Seed("journey.reflect",
                "回顾你看到的",
                "Review what you found",
                "旅程第五步。"),
            new Seed("journey.exit",
                "结束参观",
                "Finish",
                "旅程第六步。"),
            new Seed("journey.reset",
                "复位，交给下一位访客",
                "Reset for the next visitor",
                "工作人员操作，对应客户对「讲不清或复位不了」的担心。"),
            new Seed("sources.title",
                "资料与来源",
                "Sources",
                "来源面板标题。"),
            new Seed("sources.placeholder",
                "此件为示意重建，不是历史复原。",
                "This is a schematic reconstruction, not a historical restoration.",
                "占位与重建说明，对应客户对史料可信度的要求。"),
            new Seed("access.seated",
                "可以坐着使用",
                "Seated use",
                "无障碍说明一。"),
            new Seed("access.one_hand",
                "支持单手操作",
                "One-handed use",
                "无障碍说明二。"),
            new Seed("access.text_size",
                "文字大小可调",
                "Adjustable text size",
                "无障碍说明三。"),
            new Seed("settings.language",
                "语言",
                "Language",
                "设置项名称，供后续设置面板复用。"),
        };
    }
}
