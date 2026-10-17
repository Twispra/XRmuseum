"""Build a separate, photo-referenced surface-detail study from PR #28's GLB.

This is a display model, not a measured reconstruction. The first four team
photos are visual references only and are neither embedded nor retextured here.

Run with a Python environment containing trimesh, numpy and Pillow:
  python refine_photo_helmet.py --source path/to/photo-helmet-study.glb
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import trimesh
from PIL import Image
from trimesh.visual import TextureVisuals
from trimesh.visual.material import PBRMaterial


HERE = Path(__file__).resolve().parent
OUT = HERE.parent
DEFAULT_SOURCE = HERE.parents[1] / "photo-study" / "exports" / "photo-helmet-study.glb"


def pbr(name: str, rgba: tuple[int, int, int, int], metal: float, rough: float) -> PBRMaterial:
    return PBRMaterial(
        name=name,
        baseColorFactor=rgba,
        metallicFactor=metal,
        roughnessFactor=rough,
        doubleSided=True,
    )


AGED_BRONZE = pbr("V2_AgedBronze_Edge", (45, 32, 20, 255), .45, .72)
WORN_BRASS = pbr("V2_WornBrass_Highlight", (133, 103, 61, 255), .78, .49)
RECESS = pbr("V2_Recess_VisualAssumption", (10, 13, 12, 255), .12, .82)
GLASS_DARK = pbr("V2_DarkGlass_VisualAssumption", (6, 10, 11, 255), .0, .72)
GASKET = pbr("V2_RubberGasket_VisualAssumption", (18, 20, 18, 255), .0, .89)
MOTIF_METAL = pbr("V2_FrontWindowWornMetal_VisualAssumption", (100, 94, 78, 255), .68, .43)


def add(scene: trimesh.Scene, name: str, mesh: trimesh.Trimesh, material: PBRMaterial,
        xyz: tuple[float, float, float]) -> None:
    mesh.visual = TextureVisuals(material=material)
    matrix = trimesh.transformations.translation_matrix(xyz)
    scene.add_geometry(mesh, node_name=name, geom_name=name, transform=matrix)


def annulus(scene: trimesh.Scene, name: str, inner: float, outer: float, depth: float,
            xyz: tuple[float, float, float], material: PBRMaterial, sections: int = 96) -> None:
    mesh = trimesh.creation.annulus(r_min=inner, r_max=outer, height=depth, sections=sections)
    add(scene, name, mesh, material, xyz)


def reshape_observed_proportions(scene: trimesh.Scene) -> None:
    # The photo's front window is visibly narrower than the first study's.
    # The corselet also falls farther below the neck ring. These are visual
    # proportions, not measured dimensions; the first-study source is untouched.
    front_names = ("Front", "ClearMotif_", "FrameThumb", "RetainerSlot_")
    corselet_names = ("Corselet_", "PerimeterBolt_", "Nameplate_")
    front = np.eye(4)
    front[0, 0] = front[1, 1] = .86
    front[1, 3] = .235 * (1 - .86)
    corselet = np.eye(4)
    corselet[1, 1] = 2.1
    corselet[1, 3] = .09 * (1 - 2.1)
    for node in scene.graph.nodes_geometry:
        world = front if node.startswith(front_names) else (
            corselet if node.startswith(corselet_names) else None
        )
        if world is None:
            continue
        transform, name = scene.graph[node]
        scene.geometry[name].apply_transform(np.linalg.inv(transform) @ world @ transform)


def tone_original_materials(scene: trimesh.Scene) -> None:
    # Keep the original synthetic UV textures; adjust their tonal range, never use photos.
    seen: set[int] = set()
    for geometry in scene.geometry.values():
        material = getattr(geometry.visual, "material", None)
        if material is None or id(material) in seen:
            continue
        seen.add(id(material))
        name = material.name or ""
        if name == "WeatheredShell_Interpretive":
            image = np.asarray(material.baseColorTexture.convert("RGB"), dtype=np.float32)
            image = np.clip(image * np.array([.90, .83, .69]) + [10, 8, 5], 0, 255).astype(np.uint8)
            material.baseColorTexture = Image.fromarray(image, "RGB")
            material.roughnessFactor = .66
        elif name == "CastBrass_Interpretive":
            image = np.asarray(material.baseColorTexture.convert("RGB"), dtype=np.float32)
            image = np.clip(image * np.array([.85, .80, .70]) + [6, 5, 3], 0, 255).astype(np.uint8)
            material.baseColorTexture = Image.fromarray(image, "RGB")
            material.roughnessFactor = .60
        elif name == "PolishedEdge_Interpretive":
            material.baseColorFactor = np.array([126, 94, 53, 255], dtype=np.uint8)
            material.roughnessFactor = .52
        elif name == "Glass_VisualAssumption":
            material.baseColorFactor = np.array([70, 67, 54, 255], dtype=np.uint8)
            material.roughnessFactor = .36
            material.metallicFactor = .04
        elif name == "ClearFrontMotif_MaterialUnconfirmed":
            material.baseColorFactor = np.array([110, 121, 111, 255], dtype=np.uint8)
            material.roughnessFactor = .38
    for node in scene.graph.nodes_geometry:
        mesh = scene.geometry[scene.graph[node][1]]
        if node.startswith("FrontFrame_Removable"):
            mesh.visual = TextureVisuals(material=AGED_BRONZE)
        elif node.startswith("FrontGlass_PlaneThicknessAssumed"):
            mesh.visual = TextureVisuals(material=GLASS_DARK)
        elif node.startswith("ClearMotif_"):
            mesh.visual = TextureVisuals(material=MOTIF_METAL)
        elif node.startswith("Nameplate_Blank"):
            mesh.visual = TextureVisuals(material=RECESS)


def build(source: Path) -> dict:
    if not source.is_file() or source.stat().st_size < 100_000:
        raise SystemExit(f"Expanded v1 GLB required (not an LFS pointer): {source}")
    scene = trimesh.load(source, force="scene")
    assert len(scene.geometry) > 90, "Unexpected v1 asset"
    original_nodes = len(scene.graph.nodes_geometry)
    original_node_names = set(scene.graph.nodes_geometry)
    original_bounds = scene.bounds.copy()
    reshape_observed_proportions(scene)
    tone_original_materials(scene)

    # Photo 01: darker recessed front seat, concentric cast lip and visible seal.
    front = (0., .235)
    annulus(scene, "V2_FrontPatinatedFaceplate", .074, .087, .003,
            (*front, .184), AGED_BRONZE)
    annulus(scene, "V2_FrontInnerSeal", .067, .072, .002,
            (*front, .1845), GASKET)
    annulus(scene, "V2_FrontWornInnerLip", .071, .074, .002,
            (*front, .187), WORN_BRASS)
    # Inset darkness is a visual reading of the photographed glass, not a claim
    # about its actual transparency or physical construction.
    for name, x, y, radius in [
        ("centre", 0., .235, .0267),
        ("upper_left", -.0396, .2682, .0138),
        ("right", .0485, .2526, .0138),
        ("lower", -.0089, .1842, .0138),
    ]:
        add(scene, "V2_PortholeDarkBacking_" + name,
            trimesh.creation.cylinder(radius=radius, height=.0007, sections=64),
            GLASS_DARK, (x, y, .185))

    # Keep the existing nameplate and collar bolts. An earlier pass placed
    # duplicate details beyond the corselet surface; those are deliberately
    # removed rather than claiming a photo-accurate reconstruction.
    export_path = OUT / "exports" / "photo-helmet-refined-v2.glb"
    export_path.parent.mkdir(parents=True, exist_ok=True)
    scene.export(export_path)
    readback = trimesh.load(export_path, force="scene")
    bounds = readback.bounds
    preserved = original_node_names.issubset(set(readback.graph.nodes_geometry))
    finite = bool(all(np.isfinite(g.vertices).all() for g in readback.geometry.values()))
    xz_inside_original = bool(np.all(bounds[0, [0, 2]] >= original_bounds[0, [0, 2]] - .001)
                              and np.all(bounds[1, [0, 2]] <= original_bounds[1, [0, 2]] + .001))
    assert preserved and finite and xz_inside_original, "Export lost original nodes or exceeded checked bounds"
    report = {
        "source": "PR #28 photo-helmet-study.glb; user/team reference photos 01-04 not included",
        "output": str(export_path.relative_to(OUT)),
        "original_geometry_nodes": original_nodes,
        "refined_geometry_nodes": len(readback.graph.nodes_geometry),
        "triangles": int(sum(len(g.faces) for g in readback.geometry.values())),
        "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "export_sha256": hashlib.sha256(export_path.read_bytes()).hexdigest(),
        "front_window_scale_from_v1": .86,
        "corselet_vertical_scale_from_v1": 2.1,
        "original_nodes_preserved": preserved,
        "xz_inside_original_bounds": xz_inside_original,
        "bounds_m_gltf_y_up": np.round(bounds, 6).tolist(),
        "all_finite": finite,
        "photo_based_not_measured": True,
        "blender_source_generated": False,
        "unity_or_headset_tested": False,
    }
    qa_path = OUT / "qa" / "export-readback.json"
    qa_path.parent.mkdir(parents=True, exist_ok=True)
    qa_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    args = parser.parse_args()
    print(json.dumps(build(args.source), ensure_ascii=False, indent=2))
