using System.Collections.Generic;
using System.IO;
using System.Text;
using Museum.Eras;
using UnityEditor;
using UnityEngine;

namespace Museum.Eras.EditorTools
{
    /// <summary>
    /// Creates the era assets and a self-contained showcase prefab.
    /// Everything is written under Features/HelmetEras, so running this never touches the shared MuseumStart scene.
    /// </summary>
    public static class HelmetEraSetup
    {
        const string Root = "Assets/Museum/Features/HelmetEras";
        const string ProfileFolder = Root + "/Profiles";
        const string PrefabFolder = Root + "/Prefabs";
        const string MaterialFolder = Root + "/Materials";
        const string ShowcaseName = "HelmetEraShowcase";

        [MenuItem("Museum/Helmet Eras/Create Era Assets and Showcase", priority = 10)]
        public static void CreateAll()
        {
            EnsureFolders();

            // 必须先做材质：年代资料要引用它们，否则切换年代时外观不会有任何变化。
            var materials = CreateEraMaterials();

            var profiles = CreateEraProfiles(materials);
            var catalogue = CreateCatalogue(profiles);
            var channel = CreateChannel();

            var prefabPath = BuildShowcase(catalogue, channel, materials);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var report = new StringBuilder();
            report.AppendLine("年代系统资产已创建：");
            report.AppendLine($"  图鉴      {AssetDatabase.GetAssetPath(catalogue)}  （{profiles.Count} 个年代）");
            report.AppendLine($"  通道      {AssetDatabase.GetAssetPath(channel)}");
            report.AppendLine($"  展示 Prefab {prefabPath}");
            report.AppendLine();
            report.AppendLine("该 Prefab 独立于 MuseumStart，不会影响其他成员正在编辑的场景。");
            report.AppendLine("把它拖进任意测试场景即可预览；接入主场景由整合人决定。");
            report.AppendLine();
            report.AppendLine("没有头显时的预览方式：进入 Play 后按 N / P 前后切换，数字键 1-3 直接跳到某个年代。");
            Debug.Log(report.ToString());

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[] { Root, ProfileFolder, PrefabFolder, MaterialFolder })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                var leaf = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        /// <summary>Profiles carry the public-source wording already recorded in docs/helmet-prototype-reference.md.</summary>
        static List<HelmetEraProfile> CreateEraProfiles(Dictionary<string, Material> materials)
        {
            var specs = new[]
            {
                new EraSeed
                {
                    file = "Era-1839-Standard-Dress",
                    assetName = "1839 Standard Dress",
                    display = "1839 标准潜水服头盔",
                    sort = 10,
                    materialTag = "1839",
                    summary = "最早的实用型封闭头盔，靠软管从水面供气。这一代奠定了「坚硬壳体加前置观察窗」的基本形态。",
                    period = "1839 年起（早期商业潜水）",
                    depth = "由水面软管连续供气，作业深度受限",
                    source = "团队采用的公开史料口径，不是本组测量，具体尺寸与年代边界未在仓库中逐项核实。",
                    details = new[]
                    {
                        new DetailSeed("WINDOW", "单面小型前窗，视野窄，玻璃仅覆盖正前方。"),
                        new DetailSeed("AIR", "连续软管送气，潜水员不携带自主气源。"),
                        new DetailSeed("SHELL", "铜制球壳与软质潜水服缝合，颈部用螺栓压紧。"),
                    },
                },
                new EraSeed
                {
                    file = "Era-MarkV-Standard-Air",
                    assetName = "Mark V Standard Air",
                    display = "Mark V 标准空气头盔",
                    sort = 20,
                    materialTag = "MarkV",
                    summary = "本项目原型的主要参照对象。四窗布局扩大了视野，前窗可开合，配件为铜合金铸件。",
                    period = "1916 至 1984（美国海军制式）",
                    depth = "标准空气式，按公开资料以数十米量级计",
                    source = "DESCO 制造商说明与美海军水下博物馆公开资料；外包尺寸锚定于一件公开拍卖记录的 0.4953 x 0.3175 x 0.4191 米。",
                    details = new[]
                    {
                        new DetailSeed("WINDOW", "前方圆形铰接视窗、两侧圆窗与顶部椭圆窗，共四窗。"),
                        new DetailSeed("AIR", "从后方看，左侧鹅颈接头接电话，右侧接供气软管；排气阀是另一部件。"),
                        new DetailSeed("SHELL", "旋压铜壳，配件为铜合金铸件，窗片历史版本为玻璃。"),
                    },
                },
                new EraSeed
                {
                    file = "Era-SuperLite-17B",
                    assetName = "SuperLite 17B",
                    display = "SuperLite 17B 轻量化头盔",
                    sort = 30,
                    materialTag = "SuperLite",
                    summary = "为混合气潜水设计的轻量化头盔，用颈部密封环取代传统胸板，是商业饱和潜水的代表型号。",
                    period = "1970 年代起（商业饱和潜水）",
                    depth = "配合混合气作业，深度显著高于空气式装备",
                    source = "团队采用的公开资料口径，本组未取得制造图纸，具体参数未核实。",
                    details = new[]
                    {
                        new DetailSeed("WINDOW", "单一大型前窗，视野比四窗布局更连续。"),
                        new DetailSeed("AIR", "可连接混合气供给，带应急气源接口。"),
                        new DetailSeed("SHELL", "轻量化壳体，颈部密封环直接与潜水服连接。"),
                    },
                },
            };

            var created = new List<HelmetEraProfile>();
            foreach (var spec in specs)
            {
                var path = $"{ProfileFolder}/{spec.file}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<HelmetEraProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<HelmetEraProfile>();
                    AssetDatabase.CreateAsset(profile, path);
                }

                // 通过 SerializedObject 写入，保持字段私有并让 Inspector 立即刷新。
                var so = new SerializedObject(profile);
                so.FindProperty("displayName").stringValue = spec.display;
                so.FindProperty("sortOrder").intValue = spec.sort;
                so.FindProperty("summary").stringValue = spec.summary;
                so.FindProperty("servicePeriod").stringValue = spec.period;
                so.FindProperty("operatingDepth").stringValue = spec.depth;
                so.FindProperty("factSource").stringValue = spec.source;
                so.FindProperty("fadeDuringSwap").boolValue = true;

                // 三块表面各自套上该年代的占位材质。
                // 少了这一步，切换年代只会换文字说明，外观毫无变化——这正是实机验证时暴露的问题。
                AssignMaterial(so, "shellMaterial", materials, spec.materialTag, "Shell");
                AssignMaterial(so, "windowMaterial", materials, spec.materialTag, "Window");
                AssignMaterial(so, "fittingMaterial", materials, spec.materialTag, "Fitting");

                var details = so.FindProperty("details");
                details.arraySize = spec.details.Length;
                for (int i = 0; i < spec.details.Length; i++)
                {
                    var element = details.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("label").stringValue = spec.details[i].label;
                    element.FindPropertyRelative("text").stringValue = spec.details[i].text;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);

                created.Add(profile);
            }
            return created;
        }

        static HelmetEraCatalogue CreateCatalogue(List<HelmetEraProfile> profiles)
        {
            var path = $"{Root}/HelmetEraCatalogue.asset";
            var catalogue = AssetDatabase.LoadAssetAtPath<HelmetEraCatalogue>(path);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<HelmetEraCatalogue>();
                AssetDatabase.CreateAsset(catalogue, path);
            }

            var so = new SerializedObject(catalogue);
            var array = so.FindProperty("eras");
            array.arraySize = profiles.Count;
            for (int i = 0; i < profiles.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            so.FindProperty("defaultIndex").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalogue);
            return catalogue;
        }

        static HelmetEraChannel CreateChannel()
        {
            var path = $"{Root}/HelmetEraChannel.asset";
            var channel = AssetDatabase.LoadAssetAtPath<HelmetEraChannel>(path);
            if (channel == null)
            {
                channel = ScriptableObject.CreateInstance<HelmetEraChannel>();
                AssetDatabase.CreateAsset(channel, path);
            }
            EditorUtility.SetDirty(channel);
            return channel;
        }

        /// <summary>
        /// 每个年代三块表面的占位外观。
        /// 同一顶头盔在不同年代之间必须一眼能看出差别，所以三个年代各用一套明显不同的颜色，
        /// 而不是共用一组材质——共用的话切换就只是换文字，外观毫无变化。
        /// 这些颜色只是占位，**不是历史复原**，真实外观应由美术资源替换。
        /// </summary>
        static readonly EraLook[] EraLooks =
        {
            new EraLook("1839",
                shell: new Color(0.42f, 0.30f, 0.17f),
                window: new Color(0.22f, 0.28f, 0.26f),
                fitting: new Color(0.52f, 0.42f, 0.25f)),
            new EraLook("MarkV",
                shell: new Color(0.72f, 0.45f, 0.20f),
                window: new Color(0.35f, 0.55f, 0.60f),
                fitting: new Color(0.85f, 0.72f, 0.42f)),
            new EraLook("SuperLite",
                shell: new Color(0.60f, 0.63f, 0.68f),
                window: new Color(0.55f, 0.76f, 0.86f),
                fitting: new Color(0.78f, 0.80f, 0.82f)),
        };

        /// <summary>早期版本用过的人造材质名，已被「按年代生成」取代，顺手清掉避免留下孤立资产。</summary>
        static readonly string[] LegacyMaterialNames =
        {
            "EraShellWarmCopper", "EraWindowGlass", "EraFittingBright",
        };

        /// <summary>Creates one shell / window / fitting material per era so the switch is plainly visible.</summary>
        static Dictionary<string, Material> CreateEraMaterials()
        {
            foreach (var legacy in LegacyMaterialNames)
            {
                var legacyPath = $"{MaterialFolder}/{legacy}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(legacyPath) != null)
                    AssetDatabase.DeleteAsset(legacyPath);
            }

            var result = new Dictionary<string, Material>();
            foreach (var look in EraLooks)
            {
                result[$"{look.tag}-Shell"] = EnsureMaterial($"{look.tag}-Shell", look.shell, 0.35f, 0.55f);
                result[$"{look.tag}-Window"] = EnsureMaterial($"{look.tag}-Window", look.window, 0.10f, 0.90f);
                result[$"{look.tag}-Fitting"] = EnsureMaterial($"{look.tag}-Fitting", look.fitting, 0.55f, 0.70f);
            }
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

        /// <summary>Writes one material slot onto a profile, warning instead of silently leaving it empty.</summary>
        static void AssignMaterial(SerializedObject so, string property, Dictionary<string, Material> materials,
            string tag, string slot)
        {
            var key = $"{tag}-{slot}";
            if (!materials.TryGetValue(key, out var material) || material == null)
            {
                Debug.LogWarning($"[HelmetEraSetup] 缺少材质 {key}，{property} 将保持为空。");
                return;
            }
            so.FindProperty(property).objectReferenceValue = material;
        }

        /// <summary>
        /// Builds a greybox showcase: two era buttons, a plinth and a swappable body.
        /// No reference to MuseumStart, so it cannot collide with another member's scene work.
        /// </summary>
        static string BuildShowcase(HelmetEraCatalogue catalogue, HelmetEraChannel channel,
            Dictionary<string, Material> materials)
        {
            var path = $"{PrefabFolder}/{ShowcaseName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var root = new GameObject(ShowcaseName);
            try
            {
                var switcher = root.AddComponent<HelmetEraSwitcher>();
                switcher.Configure(catalogue, channel);

                // 没有头显的成员也能按键预览：N / P 前后切换，数字键 1-3 直接跳到某个年代。
                root.AddComponent<HelmetEraPlayModePreview>().Configure(switcher);

                var shell = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                shell.name = "Era Body - Shell";
                shell.transform.SetParent(root.transform, false);
                shell.transform.localScale = new Vector3(0.32f, 0.25f, 0.32f);
                shell.GetComponent<Renderer>().sharedMaterial = materials["MarkV-Shell"];

                var window = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                window.name = "Era Body - Window";
                window.transform.SetParent(root.transform, false);
                window.transform.localPosition = new Vector3(0f, 0.05f, 0.14f);
                window.transform.localScale = new Vector3(0.16f, 0.16f, 0.06f);
                window.GetComponent<Renderer>().sharedMaterial = materials["MarkV-Window"];

                var fitting = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fitting.name = "Era Body - Fitting";
                fitting.transform.SetParent(root.transform, false);
                fitting.transform.localPosition = new Vector3(0f, 0.28f, 0f);
                fitting.transform.localScale = new Vector3(0.08f, 0.04f, 0.08f);
                fitting.GetComponent<Renderer>().sharedMaterial = materials["MarkV-Fitting"];

                switcher.ConfigureBindings(new[]
                {
                    // 灰模每个部件都是单材质网格，因此子材质序号统一为 0。
                    // 换上真实头盔模型后，这里要按渲染器实际的材质列表逐个核对。
                    new HelmetEraSwitcher.SlotBinding
                    {
                        target = shell.GetComponent<Renderer>(),
                        slot = HelmetMaterialSlot.Shell,
                        subMaterialIndex = 0,
                        restoreOriginalWhenEmpty = false,
                    },
                    new HelmetEraSwitcher.SlotBinding
                    {
                        target = window.GetComponent<Renderer>(),
                        slot = HelmetMaterialSlot.Window,
                        subMaterialIndex = 0,
                        restoreOriginalWhenEmpty = false,
                    },
                    new HelmetEraSwitcher.SlotBinding
                    {
                        target = fitting.GetComponent<Renderer>(),
                        slot = HelmetMaterialSlot.Fitting,
                        subMaterialIndex = 0,
                        restoreOriginalWhenEmpty = false,
                    },
                });

                CreateButton(root.transform, switcher, "Era Button - Previous",
                    HelmetEraButton.StepDirection.Previous, new Vector3(-0.28f, 0f, 0f), materials);
                CreateButton(root.transform, switcher, "Era Button - Next",
                    HelmetEraButton.StepDirection.Next, new Vector3(0.28f, 0f, 0f), materials);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                return AssetDatabase.GetAssetPath(prefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void CreateButton(Transform parent, HelmetEraSwitcher switcher, string name,
            HelmetEraButton.StepDirection direction, Vector3 localPosition, Dictionary<string, Material> materials)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition + new Vector3(0f, -0.12f, 0.22f);
            go.transform.localScale = new Vector3(0.14f, 0.06f, 0.03f);

            var renderer = go.GetComponent<Renderer>();
            var interactable = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>();
            interactable.colliders.Add(go.GetComponent<Collider>());

            var button = go.AddComponent<HelmetEraButton>();
            button.Configure(switcher, direction, renderer, materials["MarkV-Fitting"]);
        }

        struct EraSeed
        {
            public string file;
            public string assetName;
            public string display;
            public int sort;
            /// <summary>对应 <see cref="EraLooks"/> 里的材质前缀，用于给该年代套上占位外观。</summary>
            public string materialTag;
            public string summary;
            public string period;
            public string depth;
            public string source;
            public DetailSeed[] details;
        }

        readonly struct DetailSeed
        {
            public readonly string label;
            public readonly string text;

            public DetailSeed(string label, string text)
            {
                this.label = label;
                this.text = text;
            }
        }

        /// <summary>一个年代的三块表面配色。tag 同时用于材质命名。</summary>
        readonly struct EraLook
        {
            public readonly string tag;
            public readonly Color shell;
            public readonly Color window;
            public readonly Color fitting;

            public EraLook(string tag, Color shell, Color window, Color fitting)
            {
                this.tag = tag;
                this.shell = shell;
                this.window = window;
                this.fitting = fitting;
            }
        }
    }
}
