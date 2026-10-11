#!/usr/bin/env python3
"""可访问性事实提取与可复现核对（只读，纯标准库）。

用途
----
从 Unity 工程的资产文件里**直接读出**与可访问性相关的客观数据，写到新的输出文件，
让任何组员都能在自己 clone 的仓库里重跑一次、逐项核对 docs/accessibility.zh-CN.md 的说法。

它**不猜测**、也**不评价**任何无法从文件里读出的东西（真实视高、视野、实际观感、
参与者反馈等一律不产出），只回答"资产里写了什么"。

用法
----
    python audit_accessibility.py --out <新的运行目录>/report.json
    python audit_accessibility.py --repo D:\\some\\clone --out custom.json
    python audit_accessibility.py --package-cache <Library/PackageCache> --out custom.json

约定
----
- 只读资产；必须显式指定新的输出路径；已有输出一律拒绝覆盖。
- --package-cache 可额外核对当前 manifest 版本的 URP shader 与 XRI 源码；没有包缓存时标为未核对。
- 退出码：0 = 正常（含"发现待处理项"，这不是失败）；1 = 输入缺失/无法读取（需要人看）。
- 无第三方依赖（不要求 PyYAML / numpy）。

颜色空间说明（重要）
--------------------
材质的 Color 属性以 sRGB 提供，在 Linear 工程传入 shader 时才转换为线性值。
脚本以材质基色与 TMP 序列化文字色均按 sRGB 的条件估算配色对比度。
这不是最终像素对比度：TMP shader、光照、雾、后处理、透明度与遮挡仍需渲染检查。
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

# --------------------------------------------------------------------------------------
# 基本工具
# --------------------------------------------------------------------------------------

COLOR_PAT = (
    r"\{r:\s*(-?[\d.eE+-]+),\s*g:\s*(-?[\d.eE+-]+),\s*b:\s*(-?[\d.eE+-]+)"
    r"(?:,\s*a:\s*(-?[\d.eE+-]+))?\}"
)
COLOR_RE = re.compile(COLOR_PAT)
VEC3_RE = r"\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)\}"
VEC2_RE = r"\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)\}"
NUM = r"(-?[\d.]+)f?"
DOC_SPLIT = re.compile(r"(?m)^--- !u!")
# 文档头形如 "1 &161485901626453979"（类号 + fileID 锚点），fileID 需要用 search 而非 match
FID_RE = re.compile(r"&(-?\d+)")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def srgb_to_linear(c: float) -> float:
    return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4


def relative_luminance(rgb, treat_as_srgb: bool) -> float:
    vals = [srgb_to_linear(c) if treat_as_srgb else c for c in rgb]
    return 0.2126 * vals[0] + 0.7152 * vals[1] + 0.0722 * vals[2]


def contrast_ratio(l1: float, l2: float) -> float:
    hi, lo = max(l1, l2), min(l1, l2)
    return (hi + 0.05) / (lo + 0.05)


def rgb_to_hex(rgb) -> str:
    return "#" + "".join(f"{max(0, min(255, round(c * 255))):02X}" for c in rgb)


def parse_docs(text: str):
    """把一个 .prefab/.unity 拆成 [(head, body), ...]。"""
    out = []
    for part in DOC_SPLIT.split(text)[1:]:
        head, _, body = part.partition("\n")
        out.append((head.strip(), body))
    return out


# --------------------------------------------------------------------------------------
# Unity YAML 轻量解析
# --------------------------------------------------------------------------------------

def _fid(head: str):
    m = FID_RE.search(head)
    return m.group(1) if m else None


def build_name_map(docs):
    """fileID(GameObject) -> 名称。"""
    names = {}
    for head, body in docs:
        fid = _fid(head)
        if fid and body.lstrip().startswith("GameObject:"):
            nm = re.search(r"(?m)^  m_Name:\s*(.*)$", body)
            if nm:
                names[fid] = nm.group(1).strip()
    return names


def tmp_components(path: Path):
    """产出该文件里所有 TMP 文本组件的样式信息。"""
    docs = parse_docs(read_text(path))
    names = build_name_map(docs)
    out = []
    for _, body in docs:
        if "m_fontColor" not in body:
            continue
        size = re.search(r"(?m)^  m_fontSize:\s*" + NUM, body)
        smin = re.search(r"(?m)^  m_fontSizeMin:\s*" + NUM, body)
        smax = re.search(r"(?m)^  m_fontSizeMax:\s*" + NUM, body)
        auto = re.search(r"(?m)^  m_enableAutoSizing:\s*(\d)", body)
        tail = body.split("m_fontColor:", 1)[1][:200] if "m_fontColor:" in body else ""
        color = COLOR_RE.search(tail)
        raw = re.search(r"(?m)^  m_text:\s*(.*)$", body)
        go = re.search(r"(?m)^  m_GameObject:\s*\{fileID:\s*(-?\d+)", body)
        pos_node = path
        out.append(
            {
                "file": str(pos_node).replace("\\", "/"),
                "object": names.get(go.group(1)) if go else None,
                "fontSize": float(size.group(1)) if size else None,
                "fontSizeMin": float(smin.group(1)) if smin else None,
                "fontSizeMax": float(smax.group(1)) if smax else None,
                "autoSizing": bool(int(auto.group(1))) if auto else None,
                "fontColor": [float(color.group(i)) for i in (1, 2, 3)] if color else None,
                "fontAlpha": float(color.group(4)) if color and color.group(4) else 1.0,
                "textSample": (raw.group(1).strip()[:60] if raw else None),
            }
        )
    return out


def scene_style_overrides(path: Path):
    text = read_text(path)
    pat = re.compile(
        r"propertyPath:\s*(m_fontSize|m_fontSizeMax|m_fontSizeMin|m_text)\s*\n\s*value:\s*(.*)"
    )
    return [
        {"file": str(path).replace("\\", "/"), "property": k, "value": v.strip()}
        for k, v in pat.findall(text)
    ]


def package_source(repo: Path, package_cache, package: str, relative_path: str):
    """只读取 manifest 指定版本的包源码；缺缓存时保留明确的未核对标记。"""
    manifest = repo / "MuseumVR/Packages/manifest.json"
    version = json.loads(read_text(manifest))["dependencies"].get(package)
    result = {"package": package, "version": version,
              "file": f"Packages/{package}/{relative_path}", "available": False}
    if package_cache is None or version is None:
        return result, None
    source = package_cache / f"{package}@{version}" / relative_path
    if not source.is_file():
        return result, None
    result.update(available=True, sha256=hashlib.sha256(source.read_bytes()).hexdigest())
    return result, read_text(source)


def material_colors(path: Path, repo: Path, package_cache=None):
    text = read_text(path)
    base = None
    m = re.search(r"(?m)^    - _BaseColor:\s*" + COLOR_PAT, text)
    if m:
        base = [float(m.group(i)) for i in (1, 2, 3)]
    base_alpha = float(m.group(4)) if m and m.group(4) else None
    surface = re.search(r"(?m)^    - _Surface:\s*(\d)", text)
    queue = re.search(r"(?m)^  m_CustomRenderQueue:\s*(-?\d+)", text)
    shader = re.search(r"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", text)
    shader_guid = shader.group(1) if shader else None
    shader_evidence = None
    # Resolve only by the actual .meta GUID in the declared package version, never by material name.
    for candidate in ("Unlit", "Lit"):
        evidence, src = package_source(repo, package_cache, "com.unity.render-pipelines.universal",
                                       f"Shaders/{candidate}.shader")
        _, meta = package_source(repo, package_cache, "com.unity.render-pipelines.universal",
                                 f"Shaders/{candidate}.shader.meta")
        if src and meta and f"guid: {shader_guid}" in meta:
            shader_name = re.search(r'^Shader\s+"([^"]+)"', src)
            blend = re.search(r"(?m)^\s*Blend\s*\[.*", src)
            evidence.update(name=shader_name.group(1) if shader_name else None,
                            blendDeclaration=blend.group(0).strip() if blend else None,
                            baseColorIsColorProperty=bool(re.search(r'_BaseColor\([^\n]*,\s*Color\)', src)),
                            baseMapDefaultWhite=bool(re.search(r'_BaseMap\([^\n]*=\s*"white"', src)))
            if candidate == "Unlit":
                fragment_evidence, fragment = package_source(
                    repo, package_cache, "com.unity.render-pipelines.universal", "Shaders/UnlitForwardPass.hlsl")
                alpha = re.search(r"half alpha = ([^;]+);", fragment or "")
                fragment_evidence["alphaExpression"] = alpha.group(1) if alpha else None
                evidence["fragmentSource"] = fragment_evidence
            shader_evidence = evidence
            break
    floats = {}
    for key in ("_Surface", "_Blend", "_SrcBlend", "_DstBlend", "_ZWrite"):
        match = re.search(r"(?m)^    - " + key + r":\s*" + NUM, text)
        floats[key] = float(match.group(1)) if match else None
    base_map = re.search(r"- _BaseMap:\s*\n\s*m_Texture: (\{[^\n]+\})", text)
    return {
        "file": str(path).replace("\\", "/"),
        "name": path.stem,
        "baseColor": base,
        "baseColorHex": rgb_to_hex(base) if base else None,
        "baseAlpha": base_alpha,
        "shaderGuid": shader_guid,
        "shaderEvidence": shader_evidence or {"available": False, "note": "未从包源码解析 shader"},
        "baseMapTexture": base_map.group(1) if base_map else None,
        "blendState": floats,
        "transparentSurface": bool(surface and int(surface.group(1)) == 1),
        "renderQueue": int(queue.group(1)) if queue else None,
    }


def cumulative_scale(prefab: Path, target_name: str):
    """沿 Transform 父链累乘 localScale，得到该对象相对 prefab 根的物理缩放。

    对 1x1 的 Unity 内置 Quad，累乘后的 (x, y) 就是它的物理尺寸（米）。
    对 TMP 的 RectTransform，同样给出 sizeDelta 的物理尺寸。
    """
    docs = parse_docs(read_text(prefab))
    names = build_name_map(docs)
    tfs = {}
    for head, body in docs:
        fid = _fid(head)
        if not fid:
            continue
        if body.lstrip().startswith(("Transform:", "RectTransform:")):
            go = re.search(r"(?m)^  m_GameObject:\s*\{fileID:\s*(-?\d+)", body)
            fa = re.search(r"(?m)^  m_Father:\s*\{fileID:\s*(-?\d+)", body)
            sc = re.search(r"(?m)^  m_LocalScale:\s*" + VEC3_RE, body)
            sd = re.search(r"(?m)^  m_SizeDelta:\s*" + VEC2_RE, body)
            lp = re.search(r"(?m)^  m_LocalPosition:\s*" + VEC3_RE, body)
            tfs[fid] = {
                "go": go.group(1) if go else None,
                "father": fa.group(1) if fa else None,
                "scale": [float(sc.group(i)) for i in (1, 2, 3)] if sc else [1.0, 1.0, 1.0],
                "sizeDelta": [float(sd.group(i)) for i in (1, 2)] if sd else None,
                "localPos": [float(lp.group(i)) for i in (1, 2, 3)] if lp else None,
            }
    target = next((fid for fid, tf in tfs.items() if names.get(tf["go"]) == target_name), None)
    if target is None:
        return None
    cum = [1.0, 1.0, 1.0]
    chain = []
    detail = []
    fid = target
    while fid is not None and fid in tfs:
        tf = tfs[fid]
        cum = [c * s for c, s in zip(cum, tf["scale"])]
        node = names.get(tf["go"]) or f"<{tf['go']}>"
        chain.append(node)
        detail.append(
            {
                "object": node,
                "localScale": [round(v, 6) for v in tf["scale"]],
                "localPosition": [round(v, 6) for v in tf["localPos"]] if tf["localPos"] else None,
            }
        )
        fid = tf["father"]
    chain.reverse()
    detail.reverse()
    res = {
        "object": target_name,
        "chain": chain,
        "chainDetail": detail,
        "cumulativeScale": [round(c, 6) for c in cum],
        "localPosition": tfs[target]["localPos"],
    }
    sd = tfs[target]["sizeDelta"]
    if sd:
        res["sizeDeltaLocal"] = sd
        res["physicalSizeMeters"] = {
            "x": round(sd[0] * abs(cum[0]), 4),
            "y": round(sd[1] * abs(cum[1]), 4),
        }
    return res


# --------------------------------------------------------------------------------------
# 主流程
# --------------------------------------------------------------------------------------

def audit(repo: Path, package_cache=None):
    obs = []

    def rec(name, value, note=""):
        obs.append({"item": name, "value": value, "note": note})

    museum = repo / "MuseumVR/Assets/Museum"
    product_prefabs = sorted(museum.rglob("*.prefab"))
    product_scenes = sorted(museum.rglob("*.unity"))
    materials = sorted(museum.rglob("*.mat"))
    samples = sorted((repo / "MuseumVR/Assets/Samples").rglob("*.prefab")) + sorted(
        (repo / "MuseumVR/Assets/Samples").rglob("*.unity")
    )

    rec(
        "扫描范围",
        {
            "productPrefabs": len(product_prefabs),
            "productScenes": len(product_scenes),
            "productMaterials": len(materials),
            "thirdPartySamples": len(samples),
            "note": "Samples/ 是 XR Interaction Toolkit 官方样例，单独统计、不计入产品结论",
        },
    )

    # ---- 1. 全量 TMP 字号 / 颜色 ------------------------------------------------------
    prod_texts = []
    for p in product_prefabs + product_scenes:
        prod_texts.extend(tmp_components(p))

    sized = [t for t in prod_texts if t["fontSize"]]
    if sized:
        rec(
            "产品侧 TMP 文本组件数",
            {
                "components": len(prod_texts),
                "withFontSize": len(sized),
                "autoSizing": sum(1 for t in prod_texts if t["autoSizing"]),
                "semiTransparentText": sum(1 for t in prod_texts if t["fontAlpha"] < 1),
            },
        )

    # 按文件给出字号分布：比一个全局 min/max 更有解释力，也能看出"各文件基线不同"
    by_file = {}
    for t in sized:
        key = t["file"].split("Assets/")[-1]
        b = by_file.setdefault(key, {"count": 0, "sizes": set()})
        b["count"] += 1
        b["sizes"].add(round(t["fontSize"], 4))
    dist = {
        k: {"count": v["count"], "min": min(v["sizes"]), "max": max(v["sizes"]),
            "distinct": sorted(v["sizes"])}
        for k, v in sorted(by_file.items())
    }
    if dist:
        rec(
            "字号分布（按文件）",
            dist,
            "各文件的字号区间互不相同 → 工程内没有统一的排版基线；"
            "仓库也没有任何一处记录『最小可读字号』，因此无法判断自动缩放压到下限时是否仍可读。"
            "注意：*.unity 只内联场景自身的 TMP 组件，prefab 实例展开的组件不在其中"
            "（其实际字号以源 prefab + 场景覆盖为准）",
        )

    auto = [t for t in prod_texts if t["autoSizing"] and t["fontSizeMin"] and t["fontSizeMax"]]
    if auto:
        ranked = sorted(auto, key=lambda t: t["fontSizeMax"] / t["fontSizeMin"], reverse=True)

        def _brief(t):
            return {
                "object": t["object"],
                "file": t["file"].split("Assets/")[-1],
                "range": [t["fontSizeMin"], t["fontSizeMax"]],
                "shrinkRatio": round(t["fontSizeMax"] / t["fontSizeMin"], 1),
            }

        rec(
            "自动缩放压缩比最大的几个文本组件",
            {
                "autoSizingComponents": len(auto),
                "top5": [_brief(t) for t in ranked[:5]],
            },
            "自动缩放会在文案变长时把字号压到 fontSizeMin 为止；"
            "同一组件内压缩比越大，长文案（如繁体/英文整句）被压得越小",
        )

    # 关键项：下限设得极低 = 实际上没有可读性保护
    tiny = [t for t in prod_texts if t["fontSizeMin"] is not None and t["fontSizeMin"] <= 0.05]
    if tiny:
        tally = {}
        for t in tiny:
            tally[round(t["fontSizeMin"], 4)] = tally.get(round(t["fontSizeMin"], 4), 0) + 1
        rec(
            "自动缩放下限低于 0.05 的文本组件",
            {
                "count": len(tiny),
                "ofAutoSizingComponents": len(auto),
                "distribution": tally,
                "examples": [_brief(t) for t in sorted(tiny, key=lambda t: t["fontSizeMax"] / t["fontSizeMin"], reverse=True)[:4]],
            },
            "下限 0.02 意味着当文案超出文本框时，字号可以被压到原下限的 1/40 以下 —— "
            "资产层面等于没有可读性保护。是否真的会被压到这一步，取决于最长文案，需在编辑器里逐条核对",
        )

    overrides = []
    for p in product_scenes:
        overrides.extend(scene_style_overrides(p))
    font_ov = [o for o in overrides if o["property"].startswith("m_fontSize")]
    rec(
        "主场景中的样式覆盖",
        {
            "fontSizeOverrideCount": len(font_ov),
            "fontSizeOverrideValues": sorted({o["value"] for o in font_ov}, key=float),
            "textOverrideCount": sum(1 for o in overrides if o["property"] == "m_text"),
        },
        "主场景里实际显示的字号由这些覆盖决定，与源 prefab 默认值不同；"
        "m_text 覆盖处写死的英文字符串同时充当三语表的查找 key",
    )

    # ---- 2. 物理尺寸（由 Transform 父链累乘得出，可复算） ------------------------------
    prefab_by_stem = {p.stem: p for p in product_prefabs}
    sizes = []
    for stem, target in (
        ("HelmetHotspotTour", "Panel Backing"),
        ("HelmetHotspotTour", "Information"),
        ("LanguageShowcase", "Board"),
        ("LanguageShowcase", "Language Button - English"),
    ):
        p = prefab_by_stem.get(stem)
        if not p:
            continue
        r = cumulative_scale(p, target)
        if r:
            sizes.append({"prefab": stem, **r})
    if sizes:
        rec(
            "面板的物理尺寸（沿 Transform 父链累乘 localScale）",
            sizes,
            "Quad 的物理尺寸 = 累乘后的 scale；TMP 的 = sizeDelta × 累乘 scale。"
            "这是资产层面的确定值，可复算",
        )

    # ---- 3. 对比度 -------------------------------------------------------------------
    mats = {m.stem: material_colors(m, repo, package_cache) for m in materials}
    panel = mats.get("HotspotPanel")
    board = mats.get("Language-Board")
    if panel:
        rec(
            "热点说明面板背景（HotspotPanel.mat）",
            {k: panel[k] for k in ("baseColorHex", "baseAlpha", "shaderGuid", "shaderEvidence",
                                  "baseMapTexture", "blendState", "transparentSurface", "renderQueue")},
            "Transparent 表示渲染模式，不等于当前透景。需联合核对 shader、纹理 alpha、"
            "baseAlpha、blendState 和运行时 opacity；不能只凭 _Surface 判断。"
            "hold 与 fade 两阶段的最终像素对比度均未验证",
        )
    if board:
        rec(
            "语言选择板背景（Language-Board.mat）",
            {k: board[k] for k in ("baseColorHex", "baseAlpha", "shaderGuid", "shaderEvidence",
                                  "baseMapTexture", "blendState", "transparentSurface", "renderQueue")},
            "不透明表示不会 Alpha 混合后景；若 shader 是 Lit，最终颜色仍受光照等影响。"
            "不能把文字对材质基色的配色估算当成已通过的最终对比度",
        )

    def pair_report(label, text_rgb, bg_rgb):
        ratio = contrast_ratio(relative_luminance(text_rgb, True), relative_luminance(bg_rgb, True))
        return {
            "label": label,
            "textHex": rgb_to_hex(text_rgb),
            "backgroundHex": rgb_to_hex(bg_rgb),
            "conditionalSrgbPaletteRatio": round(ratio, 2),
            "assumption": "材质 Color 和 TMP 序列化文字色均按 sRGB 的配色估算；不代表最终像素",
            "renderedContrastVerified": False,
        }

    hotspot_prefab = prefab_by_stem.get("HelmetHotspotTour")
    if hotspot_prefab and panel and panel["baseColor"]:
        comps = tmp_components(hotspot_prefab)
        uniq = sorted({tuple(c["fontColor"]) for c in comps if c["fontColor"]})
        rec(
            "热点面板文字 / 面板背景 的对比度",
            [pair_report(f"文字色 {i+1}", list(c), panel["baseColor"]) for i, c in enumerate(uniq)],
            "仅估算文字色与材质基色；hold 时 alpha=1，fade 时透明度降低，"
            "两阶段的最终像素对比度都未在本轮渲染验证",
        )

    lang_prefab = prefab_by_stem.get("LanguageShowcase")
    if lang_prefab and board and board["baseColor"]:
        comps = tmp_components(lang_prefab)
        uniq = sorted({tuple(c["fontColor"]) for c in comps if c["fontColor"]})
        rec(
            "语言选择板文字 / 板面背景 的对比度",
            [pair_report(f"文字色 {i+1}", list(c), board["baseColor"]) for i, c in enumerate(uniq)],
            "板面是 URP Lit，光照可能改变实际颜色；语言按钮另有 Language-Button 材质。"
            "此处只估算文字色对 Language-Board 基色，不判定整个语言入口对比度通过",
        )

    marker = {}
    hotspot_cs = museum / "Features/HelmetHotspots/Runtime/HelmetHotspot.cs"
    if hotspot_cs.exists():
        src = read_text(hotspot_cs)
        for field in ("availableColor", "hoverColor", "visitedColor"):
            m = re.search(
                field + r"\s*=\s*new Color\(\s*" + NUM + r"\s*,\s*" + NUM + r"\s*,\s*"
                + NUM + r"\s*,\s*" + NUM + r"\s*\)",
                src,
            )
            if m:
                marker[field] = {
                    "hex": rgb_to_hex([float(m.group(i)) for i in (1, 2, 3)]),
                    "alpha": float(m.group(4)),
                }
        edge = re.search(r"(?m)^\s*public bool IsVisited", src)
        marker["_hasIsVisitedState"] = bool(edge)
    if marker:
        rec(
            "热点标记的状态配色（HelmetHotspot.cs）",
            marker,
            "三种状态（可选/悬停/已访问）**只用颜色区分**，其中两态还带透明度；"
            "对抗色觉障碍与低视力使用者属风险点（WCAG 1.4.1 不以颜色为唯一信息载体）",
        )

    # ---- 4. 时间限制 ------------------------------------------------------------------
    tour_cs = museum / "Features/HelmetHotspots/Runtime/HelmetHotspotTour.cs"
    if tour_cs.exists():
        s = read_text(tour_cs)
        hold = re.search(r"explanationHoldSeconds\s*=\s*" + NUM, s)
        fade = re.search(r"explanationFadeSeconds\s*=\s*" + NUM, s)
        hover = re.search(r"allowHoveredActivate\s*=\s*(true|false)", s)
        trig = re.search(r"enableTriggerQueries\s*=\s*(true|false)", s)
        restart = "RestartExplanationLifetime" in s
        rec(
            "说明面板自动淡出的默认时长",
            {
                "holdSeconds": float(hold.group(1)) if hold else None,
                "fadeSeconds": float(fade.group(1)) if fade else None,
                "recallPathExists": restart,
                "fadeScalesLabelAlpha": "color.a *= ExplanationOpacity" in s,
                "fadeScalesBackingAlpha": "explanationRendererColors[i]" in s and "color.a *= ExplanationOpacity" in s,
                "restartRestoresFullOpacity": bool(re.search(r"void RestartExplanationLifetime\(\).*?SetExplanationOpacity\(1f\)", s, re.S)),
            },
            "阅读窗口 = hold + fade；代码里重新选中热点会重启计时（RestartExplanationLifetime），"
            "因此存在召回路径 —— 这是正面的，但窗口长度本身是否足够只能由真实使用者判断",
        )
        activate_evidence, controller_src = package_source(
            repo, package_cache, "com.unity.xr.interaction.toolkit",
            "Runtime/Interaction/Interactors/XRBaseControllerInteractor.cs")
        activate_condition = re.search(r"public virtual bool shouldActivate\s*=>\s*([^;]+);", controller_src or "")
        activate_evidence["shouldActivateCondition"] = " ".join(activate_condition.group(1).split()) if activate_condition else None
        hotspot_src = read_text(hotspot_cs) if hotspot_cs.exists() else ""
        hover_body = re.search(r"void OnHoverEntered\([^)]*\)\s*\{([^}]+)\}", hotspot_src)
        rec(
            "交互输入开关",
            {
                "enableTriggerQueries": trig.group(1) if trig else None,
                "allowHoveredActivate": hover.group(1) if hover else None,
                "activateInputSource": activate_evidence,
                "hoverHandler": hover_body.group(1).strip() if hover_body else None,
            },
            "allowHoveredActivate 允许在未 Select、已 Hover 时把 Activate 输入发给目标；"
            "它不产生输入，仍需 Activate（本工程为扳机）或 Select（握持）动作。"
            "热点 hover 回调只换颜色，不构成免按键、dwell 或凝视激活路径",
        )

    # ---- 5. 运动舒适 ------------------------------------------------------------------
    switcher = museum / "Features/HelmetEras/Runtime/HelmetEraSwitcher.cs"
    if switcher.exists():
        s = read_text(switcher)
        bright = re.search(r"MinFadeBrightness\s*=\s*" + NUM, s)
        cam = [k for k in ("Teleport", "Camera.main", "MoveTowards", "SetPositionAndRotation")
               if k in s]
        rec(
            "年代切换的运动方式（HelmetEraSwitcher.cs）",
            {
                "minFadeBrightness": float(bright.group(1)) if bright else None,
                "cameraMotionApisFound": cam or "无",
            },
            "淡变修改绑定渲染器的材质颜色，未在此脚本找到所列相机运动 API；"
            "minFadeBrightness 是对象颜色乘数下限，不是全画面亮度或舒适性实测",
        )

    # ---- 6. 复位（课程明确要求） ------------------------------------------------------
    scene = museum / "Scenes/MuseumStart.unity"
    if scene.exists():
        s = read_text(scene)
        home = re.findall(r"propertyPath: (homePosition\.\w+|homeRotation\.\w|hasConfiguredHome)\s*\n\s*value:\s*([-\d.]+)", s)
        rec(
            "复位（回到起始位姿）的资产证据",
            {
                "homeOverrides": home,
                "configuredFlagPresent": any(k == "hasConfiguredHome" and v not in ("0",) for k, v in home),
                "resetScriptExists": (museum / "Scripts/HelmetExhibitReset.cs").exists(),
            },
            "存在已配置的复位位姿覆盖与独立复位脚本 → 『复位』这条官方要求在资产层面有对应实现",
        )

    # ---- 6.5 主场景里的 prefab 实例位置（决定入口/面板落在场景的哪里） -----------------
    scene = museum / "Scenes/MuseumStart.unity"
    if scene.exists():
        docs = parse_docs(read_text(scene))
        known = {
            "a2f0d4237079f5a4194e3a4b3567ce82": "HelmetHotspotTour",
            "47e8abe331f261142b8c8e553a3b32f2": "LanguageShowcase",
            "f6336ac4ac8b4d34bc5072418cdc62a0": "XR Origin (XR Rig)",
            "7eef595f081bc0a4fabb2153e06a553d": "HelmetExhibit",
            "241077d17842c134f866ee4c6c579aca": "Maritime Gallery Environment",
        }
        inst = []
        for _, body in docs:
            if "PrefabInstance:" not in body or "m_SourcePrefab:" not in body:
                continue
            src = re.search(r"m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]{32})", body)
            if not src or src.group(1) not in known:
                continue
            tp = re.search(r"m_TransformParent: \{fileID: (-?\d+)", body)
            # 覆盖列表是按目标对象分组的：必须先按 target fileID 归拢，否则会把不同对象的坐标混在一起
            targets = {}
            for fid, pp, val in re.findall(
                r"- target: \{fileID: (-?\d+)[^\}]*\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)",
                body,
            ):
                targets.setdefault(fid, {})[pp] = val.strip()
            # 根对象的名字挂在 stripped GameObject 上，坐标挂在 stripped Transform 上，
            # 两者的 fileID 不同 → 分别取。
            scene_name = next((t["m_Name"] for t in targets.values() if "m_Name" in t), None)
            pos = {}
            pos_fid = None
            best = max(
                targets.items(),
                key=lambda kv: sum(1 for k in kv[1] if k.startswith("m_LocalPosition")),
                default=(None, {}),
            )
            for key, val in best[1].items():
                am = re.match(r"m_LocalPosition\.(\w)$", key)
                if am:
                    try:
                        pos[am.group(1)] = float(val)
                    except ValueError:
                        pass
            if pos:
                pos_fid = best[0]
            inst.append(
                {
                    "prefab": known[src.group(1)],
                    "sceneName": scene_name,
                    "transformObjectFileID": pos_fid,
                    "parentIsSceneRoot": bool(tp and tp.group(1) == "0"),
                    "localPositionOverrides": pos or None,
                }
            )
        rec(
            "主场景中的 prefab 实例位置",
            inst,
            "坐标只取该实例根对象的 m_LocalPosition 覆盖。被别的实例嵌套的 prefab"
            "（parentIsSceneRoot=false）其绝对位置还要叠加父级实例，因此这些数值只用于判断相对关系，"
            "绝对视距/视高仍需打开场景或实机确认",
        )

    # ---- 7. 语言可及性 ----------------------------------------------------------------
    trans = museum / "Features/Localization/Tables/MuseumTranslations.json"
    if trans.exists():
        raw = read_text(trans)
        keys = [
            "Select a highlighted point to inspect the helmet.",
            "Exploration complete",
            "Choose your interface language",
            "Inside the helmet",
            "Beneath the surface",
            "Connected to the surface",
            "The diving gallery",
        ]
        rec(
            "运行时字符串是否都在三语表里",
            [{"key": k, "present": k in raw} for k in keys],
            "热点提示语/完成语在代码里是英文常量，但经 HelmetHotspotLocalization 桥接后"
            "以英文为 key 查表；本项确认这些 key 均已存在，故不构成缺陷",
        )

    # ---- 8. 文档与流程缺口 ------------------------------------------------------------
    a11y_hits = []
    docs_dir = repo / "docs"
    if docs_dir.exists():
        for p in docs_dir.rglob("*.md"):
            if re.search(r"accessib|a11y|无障碍|可访问", read_text(p), re.I):
                a11y_hits.append(str(p.relative_to(repo)).replace("\\", "/"))
    rec(
        "docs/ 中提到可访问性的文件",
        {"count": len(a11y_hits), "files": a11y_hits},
        "本项只做关键词命中统计，不判断命中处是『设计基线』还是『未做该项声明』——"
        "上下文需要人工阅读；脚本不代替该判断。",
    )

    wf = repo / ".github/workflows"
    rec(
        "CI 工作流",
        {
            "workflowsDirExists": wf.exists(),
            "files": sorted(p.name for p in wf.glob("*")) if wf.exists() else [],
        },
        "没有 CI 意味着本报告这类核对只能靠人手动重跑",
    )

    return {
        "scope": "只读扫描仓库资产文件；不运行 Unity、不做仿真或推测",
        "observations": obs,
        "unverified": [
            "真实视高/眼高与坐姿可达范围：需要头显；坐姿人体测量分布也不在仓库里",
            "文字的实际角尺寸与毫米高度：需要 Unity 打开工程或实机测量（本环境不运行 Unity、本机无头显）",
            "文字的最终像素对比度：包含 Lit 光照、TMP shader 与热点背板 hold/fade 两阶段，需渲染或实机取样",
            "色觉障碍下的真实可辨性：需色彩仿真或真人测试，本脚本不做仿真",
            "参与者可用性研究：属 Phase 3；仓库明示『自动 XR 测试不等于参与者研究』",
        ],
    }


def main() -> int:
    default_repo = Path(__file__).resolve().parents[5]
    ap = argparse.ArgumentParser(description="可访问性事实提取（只读）")
    ap.add_argument("--repo", default=str(default_repo), help="仓库根目录")
    ap.add_argument("--out", required=True, help="新的报告输出路径（拒绝覆盖任何已有文件）")
    ap.add_argument("--package-cache", default=None, help="可选：当前工程的 Library/PackageCache，用于核对 shader/XRI 源码")
    args = ap.parse_args()

    repo = Path(args.repo)
    if not (repo / "MuseumVR").exists():
        print(f"[错误] 找不到 Unity 工程：{repo / 'MuseumVR'}", file=sys.stderr)
        return 1

    package_cache = Path(args.package_cache) if args.package_cache else repo / "MuseumVR/Library/PackageCache"
    result = audit(repo, package_cache)
    result["repo"] = str(repo)
    result["generatedBy"] = (
        "artifacts/accessibility/qa/runs/20261010-accessibility-audit/audit_accessibility.py"
    )

    def git_value(*parts):
        proc = subprocess.run(["git", "-C", str(repo), *parts], capture_output=True, text=True, encoding="utf-8", errors="replace")
        return proc.stdout.strip() if proc.returncode == 0 else None

    result["provenance"] = {
        "gitHead": git_value("rev-parse", "HEAD"),
        "museumTree": git_value("rev-parse", "HEAD:MuseumVR/Assets/Museum"),
        "dirtyPaths": git_value("status", "--short"),
        "scriptSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "python": sys.version.split()[0],
        "packageCache": str(package_cache),
    }
    out = Path(args.out)
    if out.exists():
        print(f"[错误] 拒绝覆盖已有报告：{out}；请指定新的 --out 路径", file=sys.stderr)
        return 1
    out.parent.mkdir(parents=True, exist_ok=True)
    # 必须显式 newline="\n"：Windows 上 write_text 默认会把 \n 翻成 \r\n，
    # 而仓库 .gitattributes 规定 *.json text eol=lf —— 否则每次重跑都会产生换行噪声 diff。
    with out.open("x", encoding="utf-8", newline="\n") as stream:
        stream.write(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(f"[写出] {out}")
    for o in result["observations"]:
        print(f"  - {o['item']}: {json.dumps(o['value'], ensure_ascii=False)[:120]}")
    print(f"[未验证项] {len(result['unverified'])} 条，见 report.json")
    return 0


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(errors="backslashreplace")
    raise SystemExit(main())
