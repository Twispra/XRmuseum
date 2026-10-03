using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Museum.Eras
{
    /// <summary>
    /// Applies an era profile's materials to the exhibit's renderers.
    /// The original materials are captured on first use and restored for any slot a profile leaves empty,
    /// so switching back and forth never accumulates drift.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelmetEraSwitcher : MonoBehaviour
    {
        [System.Serializable]
        public struct SlotBinding
        {
            [Tooltip("要改写材质的渲染器。留空会被校验工具标为未配置。")]
            public Renderer target;

            [Tooltip("该渲染器对应头盔的哪一块表面。")]
            public HelmetMaterialSlot slot;

            [Tooltip("要改写的子材质序号。单材质网格填 0；多材质网格请对照渲染器上的材质列表填写。")]
            [Min(0)] public int subMaterialIndex;

            [Tooltip("当资料没有指定该槽位材质时，是否退回模型原始材质。关闭表示保持上一次的材质。")]
            public bool restoreOriginalWhenEmpty;
        }

        [Header("数据")]
        [Tooltip("年代图鉴。为空时切换器不会做任何事，并在控制台给出一次提示。")]
        [SerializeField] HelmetEraCatalogue catalogue;

        [Tooltip("广播当前年代的通道。留空表示不需要通知其他系统。")]
        [SerializeField] HelmetEraChannel channel;

        [Header("绑定")]
        [Tooltip("渲染器与资料槽位的对应关系。同一渲染器可以被多个年代共用。")]
        [SerializeField] SlotBinding[] bindings = System.Array.Empty<SlotBinding>();

        [Header("表现")]
        [Tooltip("淡出淡入的时长，单位秒。默认 0 = 立即替换（最稳）；调大后可掩盖材质替换的跳变。")]
        [SerializeField, Min(0f)] float transitionDuration;

        [Tooltip("进入 Play 后是否自动套用图鉴的默认年代。")]
        [SerializeField] bool applyDefaultOnStart = true;

        int currentIndex = -1;
        bool initialised;
        bool warnedMissingCatalogue;
        bool switching;
        Coroutine switchRoutine;

        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        // 针对具体渲染器的诊断只报一次，避免在每帧路径里刷屏。
        readonly HashSet<Renderer> warnedRenderers = new HashSet<Renderer>();
        // 缓存每个渲染器可用的淡入淡出属性；0 表示已确认不可用。
        // 与 analyzedFadeMaterials 配对：只有材质没变时缓存才有效。
        readonly Dictionary<Renderer, int> fadePropertyCache = new Dictionary<Renderer, int>();
        readonly Dictionary<Renderer, Material> analyzedFadeMaterials = new Dictionary<Renderer, Material>();
        // 记录哪些渲染器当前被属性块压着颜色。淡变结束后必须把覆盖清掉，
        // 否则材质换了、覆盖还在，外观会一直停在淡变时的颜色上。
        readonly Dictionary<Renderer, bool> overrideActive = new Dictionary<Renderer, bool>();
        // 切换器级别的配置问题各自去重，互不压制。
        bool warnedEmptyBindings;
        bool warnedNullBindingTarget;

        public HelmetEraCatalogue Catalogue => catalogue;
        public int CurrentIndex => currentIndex;
        public bool IsSwitching => switching;

        /// <summary>The era currently on display, or null before the first successful switch.</summary>
        public HelmetEraProfile CurrentEra
        {
            get
            {
                if (catalogue == null || currentIndex < 0) return null;
                return catalogue[currentIndex];
            }
        }

        /// <summary>Number of eras this switcher can actually reach. Zero when no catalogue is assigned.</summary>
        public int AvailableEraCount => catalogue ? catalogue.Count : 0;

        void Start()
        {
            if (!applyDefaultOnStart) return;
            var index = catalogue ? catalogue.DefaultIndex : -1;
            if (index >= 0) ShowEra(index);
        }

        void OnDisable()
        {
            if (switchRoutine != null)
            {
                StopCoroutine(switchRoutine);
                switchRoutine = null;
            }
            switching = false;
        }

        /// <summary>Advances to the next era, wrapping around at the end.</summary>
        [ContextMenu("预览/切换到下一个年代")]
        public void ShowNextEra()
        {
            if (!EnsureCatalogue()) return;
            ShowEra(catalogue.NextIndex(currentIndex));
        }

        /// <summary>Steps back to the previous era, wrapping around at the start.</summary>
        [ContextMenu("预览/切换到上一个年代")]
        public void ShowPreviousEra()
        {
            if (!EnsureCatalogue()) return;
            ShowEra(catalogue.PreviousIndex(currentIndex));
        }

        /// <summary>Restores the first era. Handy when comparing screenshots.</summary>
        [ContextMenu("预览/回到第一个年代")]
        public void ShowFirstEra()
        {
            ShowEra(0);
        }

        /// <summary>Shows a specific era by display index. Out-of-range indices are rejected and reported.</summary>
        public void ShowEra(int index)
        {
            if (!EnsureCatalogue()) return;

            var era = catalogue[index];
            if (era == null)
            {
                channel?.RaiseSwitchRejected($"年代序号 {index} 超出图鉴范围（共 {catalogue.Count} 项）。");
                return;
            }

            if (index == currentIndex && initialised)
            {
                // 重复选中同一项时仍然补发广播，让切换后新订阅的面板拿到当前状态。
                channel?.RaiseEraChanged(era);
                return;
            }

            currentIndex = index;
            initialised = true;

            if (transitionDuration <= 0f || !era.FadeDuringSwap || !isActiveAndEnabled)
            {
                ApplyMaterials(era);
                channel?.RaiseEraChanged(era);
                return;
            }

            if (switchRoutine != null) StopCoroutine(switchRoutine);
            switchRoutine = StartCoroutine(SwapWithFade(era));
        }

        /// <summary>Builds the material list this switcher would apply for an era. Exposed for validation tooling.</summary>
        public List<Material> PreviewMaterialsFor(HelmetEraProfile era)
        {
            var planned = new List<Material>();
            if (era == null || bindings == null) return planned;

            foreach (var binding in bindings)
            {
                if (binding.target == null) continue;
                planned.Add(era.MaterialFor(binding.slot));
            }
            return planned;
        }

        IEnumerator SwapWithFade(HelmetEraProfile era)
        {
            switching = true;

            yield return FadeAll(1f, 0f, transitionDuration * 0.5f);

            ApplyMaterials(era);

            // 等一帧让渲染器拿到新材质，避免淡入时仍画出旧外观。
            yield return null;

            yield return FadeAll(0f, 1f, transitionDuration * 0.5f);

            switching = false;
            switchRoutine = null;
            channel?.RaiseEraChanged(era);
        }

        IEnumerator FadeAll(float from, float to, float duration)
        {
            if (duration <= 0f) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetRenderersVisible(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetRenderersVisible(to);
        }

        /// <summary>
        /// Dims the bound renderers by scaling **their own** base colour, then clears the override.
        ///
        /// 早期实现把 _BaseColor 硬写成白色、只调 alpha。但 URP 的不透明材质会忽略 alpha，
        /// 结果只是把物体刷成纯白；属性块又一直留着不清，导致切换后外观永远是白的，
        /// 三个年代看起来完全一样。现在改为以材质自身颜色为基准做明度变化，
        /// 并在完全显示时彻底清掉覆盖，让材质本身的外观回来。
        /// </summary>
        void SetRenderersVisible(float alpha)
        {
            if (bindings == null) return;
            float clamped = Mathf.Clamp01(alpha);

            foreach (var binding in bindings)
            {
                var renderer = binding.target;
                if (renderer == null) continue;

                // 完全不透明：把属性块覆盖清掉，材质原本的颜色才会显示出来。
                if (clamped >= 1f)
                {
                    if (overrideActive.TryGetValue(renderer, out bool active) && active)
                    {
                        renderer.SetPropertyBlock(null);
                        overrideActive[renderer] = false;
                    }
                    continue;
                }

                int propertyId = ResolveFadeProperty(renderer);
                if (propertyId == 0)
                {
                    // 该渲染器的材质没有可用的颜色属性，直接跳过淡变，
                    // 而不是静默失败——ResolveFadeProperty 已经给出过一次提示。
                    continue;
                }

                var baseColor = ReadBaseColor(renderer, binding, propertyId);

                // 不要压到全黑，否则是一次刺眼的黑闪。
                var dimmed = baseColor * Mathf.Lerp(MinFadeBrightness, 1f, clamped);
                dimmed.a = baseColor.a;

                // 用属性块而不是实例化材质，避免在播放期间泄漏材质实例。
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor(propertyId, dimmed);
                renderer.SetPropertyBlock(block);
                overrideActive[renderer] = true;
            }
        }

        /// <summary>Reads the base colour of the material this binding actually drives.</summary>
        Color ReadBaseColor(Renderer renderer, SlotBinding binding, int propertyId)
        {
            var materials = renderer.sharedMaterials;
            Material material = null;
            if (binding.subMaterialIndex >= 0 && binding.subMaterialIndex < materials.Length)
                material = materials[binding.subMaterialIndex];

            if (material != null && material.HasProperty(propertyId))
                return material.GetColor(propertyId);

            return Color.white;
        }

        // 淡变时压到的最低亮度。0 会变成黑闪，所以留一点。
        const float MinFadeBrightness = 0.35f;

        /// <summary>Removes a renderer's colour override so the material's own look shows through.</summary>
        void ClearOverride(Renderer renderer)
        {
            if (renderer == null) return;
            if (overrideActive.TryGetValue(renderer, out bool active) && active)
                renderer.SetPropertyBlock(null);
            overrideActive[renderer] = false;
        }

        void ClearAllOverrides()
        {
            if (bindings == null) return;
            foreach (var binding in bindings)
                ClearOverride(binding.target);
        }

        /// <summary>
        /// Finds a colour property the renderer's material actually exposes.
        /// URP Lit 用 _BaseColor，内置 Standard 用 _Color；两者都没有时说明该材质
        /// 根本不支持透明度，此时淡入淡出会完全无效，因此明确报一次警而不是静默跳过。
        /// Returns 0 when no supported property exists.
        /// </summary>
        int ResolveFadeProperty(Renderer renderer)
        {
            // 缓存以「当前材质」为键的一部分：材质被替换后（正是本系统的核心行为），
            // 旧材质能否淡变的结论可能不再成立，必须重新判断。
            var material = renderer.sharedMaterial;
            if (material != null
                && analyzedFadeMaterials.TryGetValue(renderer, out var analyzed)
                && ReferenceEquals(analyzed, material)
                && fadePropertyCache.TryGetValue(renderer, out int cached))
            {
                return cached;
            }

            int resolved = 0;
            if (material != null)
            {
                if (material.HasProperty(BaseColorId)) resolved = BaseColorId;
                else if (material.HasProperty(LegacyColorId)) resolved = LegacyColorId;
            }

            if (resolved == 0)
            {
                WarnOnce(renderer,
                    $"渲染器 {renderer.name} 使用的材质没有 _BaseColor 或 _Color 属性，" +
                    "切换时将直接跳变而不做淡入淡出。若需要淡变，请改用支持该属性的着色器，" +
                    "或把该年代资料的 fadeDuringSwap 关掉以避免误解。");
            }

            analyzedFadeMaterials[renderer] = material;
            fadePropertyCache[renderer] = resolved;
            return resolved;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        void ApplyMaterials(HelmetEraProfile era)
        {
            if (bindings == null) return;

            if (bindings.Length == 0)
            {
                if (!warnedEmptyBindings)
                {
                    warnedEmptyBindings = true;
                    Debug.LogWarning(
                        "[HelmetEraSwitcher] 没有配置任何渲染器绑定，切换年代不会改变外观。请在绑定列表里填入渲染器。",
                        this);
                }
                return;
            }

            foreach (var binding in bindings)
            {
                var renderer = binding.target;
                if (renderer == null)
                {
                    if (!warnedNullBindingTarget)
                    {
                        warnedNullBindingTarget = true;
                        Debug.LogWarning(
                            "[HelmetEraSwitcher] 绑定列表里存在未指定渲染器的条目，该条目已跳过。",
                            this);
                    }
                    continue;
                }
                CaptureOriginal(renderer);
                // 换材质之前先把淡变留下的颜色覆盖清掉，否则新材质会被旧覆盖压住。
                ClearOverride(renderer);

                var replacement = era ? era.MaterialFor(binding.slot) : null;
                if (replacement == null && binding.restoreOriginalWhenEmpty)
                    RestoreOriginal(renderer, binding);
                else if (replacement != null)
                    AssignSlotMaterial(renderer, binding, replacement);
            }
        }

        void CaptureOriginal(Renderer renderer)
        {
            if (originalMaterials.ContainsKey(renderer)) return;
            originalMaterials[renderer] = renderer.sharedMaterials;
        }

        void RestoreOriginal(Renderer renderer, SlotBinding binding)
        {
            if (!originalMaterials.TryGetValue(renderer, out var originals) || originals == null) return;
            var current = renderer.sharedMaterials;
            if (!ResolveSubMaterialIndex(binding, current.Length, renderer, out int index)) return;
            // 原始材质列表可能比当前短（材质在播放中被替换过），此时无处可还原。
            if (index >= originals.Length)
            {
                WarnOnce(renderer,
                    $"渲染器 {renderer.name} 记录到的原始材质只有 {originals.Length} 个，" +
                    $"不足以还原子材质 {index}。已跳过还原。");
                return;
            }
            current[index] = originals[index];
            renderer.sharedMaterials = current;
        }

        /// <summary>
        /// Resolves which sub-material a binding targets and reports whether it is usable.
        /// 早期版本按槽位猜测序号（窗口恒为 0 等），换掉灰模后猜错也不会报错。
        /// 现在序号由绑定方填写，这里只负责越界检查并给出一次明确提示。
        /// </summary>
        bool ResolveSubMaterialIndex(SlotBinding binding, int materialCount, Renderer renderer, out int index)
        {
            index = binding.subMaterialIndex;
            if (materialCount <= 0)
            {
                WarnOnce(renderer, $"渲染器 {renderer.name} 上没有材质，已跳过。");
                return false;
            }

            if (index < 0 || index >= materialCount)
            {
                WarnOnce(renderer,
                    $"绑定到 {renderer.name}（槽位 {binding.slot}）的子材质序号 {index} 越界；" +
                    $"该渲染器共有 {materialCount} 个子材质。已跳过，请检查绑定配置。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Emits at most one diagnostic per renderer so a per-frame path cannot flood the console.
        /// 切换器级别的配置问题走各自的去重标记，不经过这里。
        /// </summary>
        void WarnOnce(Renderer renderer, string message)
        {
            if (renderer == null)
            {
                // 走到这里说明调用方漏了去重标记，直接报警以免问题被吞掉。
                Debug.LogWarning($"[HelmetEraSwitcher] {message}", this);
                return;
            }

            if (!warnedRenderers.Add(renderer)) return;
            Debug.LogWarning($"[HelmetEraSwitcher] {message}", this);
        }

        void AssignSlotMaterial(Renderer renderer, SlotBinding binding, Material material)
        {
            var materials = renderer.sharedMaterials;
            if (!ResolveSubMaterialIndex(binding, materials.Length, renderer, out int index)) return;
            materials[index] = material;
            renderer.sharedMaterials = materials;
        }

        /// <summary>Assigns the catalogue at runtime, e.g. from an Editor builder script.</summary>
        public void Configure(HelmetEraCatalogue targetCatalogue, HelmetEraChannel targetChannel)
        {
            catalogue = targetCatalogue;
            channel = targetChannel;
            warnedMissingCatalogue = false;
        }

        /// <summary>Assigns the renderer bindings at runtime, e.g. from an Editor builder script.</summary>
        public void ConfigureBindings(SlotBinding[] newBindings)
        {
            bindings = newBindings ?? System.Array.Empty<SlotBinding>();
            originalMaterials.Clear();
            // 绑定变了，之前对「能否淡变」的判断也可能失效，一并重建。
            fadePropertyCache.Clear();
            analyzedFadeMaterials.Clear();
            warnedRenderers.Clear();
            warnedEmptyBindings = false;
            warnedNullBindingTarget = false;
            // 换绑定前先把旧的属性块覆盖清干净，避免残留的颜色压在新绑定上。
            ClearAllOverrides();
            overrideActive.Clear();
        }

        /// <summary>Reads back the current bindings so a validator can report on them.</summary>
        public IReadOnlyList<SlotBinding> Bindings =>
            bindings ?? (IReadOnlyList<SlotBinding>)System.Array.Empty<SlotBinding>();

        bool EnsureCatalogue()
        {
            if (catalogue != null) return true;
            if (!warnedMissingCatalogue)
            {
                warnedMissingCatalogue = true;
                var reason = $"{name} 未指定年代图鉴，切换请求已忽略。";
                // 这里不用 WarnOnce：缺图鉴是致命配置错误，用独立的去重标记，
                // 避免和「绑定为空」等其它切换器级提示互相压制。
                Debug.LogWarning($"[HelmetEraSwitcher] {reason}", this);
                channel?.RaiseSwitchRejected(reason);
            }
            return false;
        }
    }
}
