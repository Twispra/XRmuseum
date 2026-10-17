"""Build a separate, photo-referenced surface-detail study from PR #28's GLB.

This is a display model, not a measured reconstruction. The first four team
photos are visual references only and are neither embedded nor retextured here.

Run with a Python environment containing trimesh, numpy and Pillow:
  python refine_photo_helmet.py --source path/to/photo-helmet-study.glb
"""

from __future__ import annotations

import argparse
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


AGED_BRONZE = pbr("V2_AgedBronze_Edge", (29, 21, 14, 255), .45, .72)
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


def ellipsoid(scene: trimesh.Scene, name: str, scales: tuple[float, float, float],
              xyz: tuple[float, float, float], material: PBRMaterial) -> None:
    mesh = trimesh.creation.icosphere(subdivisions=3, radius=1)
    mesh.apply_scale(scales)
    add(scene, name, mesh, material, xyz)


def elliptical_rim(scene: trimesh.Scene, name: str, centre: tuple[float, float, float],
                   radii: tuple[float, float], tube: float, material: PBRMaterial) -> None:
    # A closed tube rather than a texture-painted outline, so it remains editable.
    points = np.linspace(0, 2 * np.pi, 97)
    verts, faces = [], []
    for theta in points[:-1]:
        x = radii[0] * np.cos(theta)
        y = radii[1] * np.sin(theta)
        tangent = np.array([-radii[0] * np.sin(theta), radii[1] * np.cos(theta), 0.])
        tangent /= np.linalg.norm(tangent)
        radial = np.array([tangent[1], -tangent[0], 0.])
        for phi in np.linspace(0, 2 * np.pi, 9)[:-1]:
            verts.append([x + tube * np.cos(phi) * radial[0],
                          y + tube * np.cos(phi) * radial[1], tube * np.sin(phi)])
    for i in range(96):
        for j in range(8):
            a, b = i * 8 + j, i * 8 + (j + 1) % 8
            c, d = ((i + 1) % 96) * 8 + (j + 1) % 8, ((i + 1) % 96) * 8 + j
            faces.extend(((a, b, c), (a, c, d)))
    add(scene, name, trimesh.Trimesh(vertices=verts, faces=faces, process=False), material, centre)


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
            image = np.clip(image * np.array([.59, .55, .47]) + [12, 10, 8], 0, 255).astype(np.uint8)
            material.baseColorTexture = Image.fromarray(image, "RGB")
            material.roughnessFactor = .66
        elif name == "CastBrass_Interpretive":
            image = np.asarray(material.baseColorTexture.convert("RGB"), dtype=np.float32)
            image = np.clip(image * np.array([.66, .61, .49]) + [8, 7, 4], 0, 255).astype(np.uint8)
            material.baseColorTexture = Image.fromarray(image, "RGB")
            material.roughnessFactor = .60
        elif name == "PolishedEdge_Interpretive":
            material.baseColorFactor = np.array([126, 94, 53, 255], dtype=np.uint8)
            material.roughnessFactor = .52
        elif name == "Glass_VisualAssumption":
            material.baseColorFactor = np.array([31, 45, 49, 255], dtype=np.uint8)
            material.roughnessFactor = .21
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


def build(source: Path) -> dict:
    if not source.is_file() or source.stat().st_size < 100_000:
        raise SystemExit(f"Expanded v1 GLB required (not an LFS pointer): {source}")
    scene = trimesh.load(source, force="scene")
    assert len(scene.geometry) > 90, "Unexpected v1 asset"
    original_nodes = len(scene.graph.nodes_geometry)
    tone_original_materials(scene)

    # Photo 01: darker recessed front seat, concentric cast lip and visible seal.
    front = (0., .235)
    annulus(scene, "V2_FrontPatinatedFaceplate", .086, .101, .003,
            (*front, .184), AGED_BRONZE)
    annulus(scene, "V2_FrontInnerSeal", .078, .084, .002,
            (*front, .1845), GASKET)
    annulus(scene, "V2_FrontWornInnerLip", .0835, .0865, .002,
            (*front, .187), WORN_BRASS)
    # Inset darkness is a visual reading of the photographed glass, not a claim
    # about its actual transparency or physical construction.
    for name, x, y, radius in [
        ("centre", 0., .235, .031),
        ("upper_left", -.046, .2736, .016),
        ("right", .0564, .2555, .016),
        ("lower", -.0104, .1759, .016),
    ]:
        add(scene, "V2_PortholeDarkBacking_" + name,
            trimesh.creation.cylinder(radius=radius, height=.0007, sections=64),
            GLASS_DARK, (x, y, .185))

    # Photo 01: the centre plaque is an oval with a raised metal border. The
    # writing remains blank because it is not legible enough to transcribe.
    elliptical_rim(scene, "V2_BlankNameplateRaisedBorder", (0., .058, .193),
                   (.041, .017), .0023, WORN_BRASS)
    ellipsoid(scene, "V2_BlankNameplateDarkInset", (.037, .014, .001),
              (0., .058, .191), RECESS)

    # Photos 01-04: larger collar fasteners have visibly more relief than v1's
    # uniform little bolts. Positions remain estimates because of perspective.
    for label, x, z in [("left", -.165, .101), ("centre", 0., .221), ("right", .165, .101)]:
        annulus(scene, "V2_CollarWasher_" + label, .005, .0125, .0032,
                (x, .058, z), AGED_BRONZE, 48)
        mesh = trimesh.creation.cylinder(radius=.0077, height=.008, sections=6)
        add(scene, "V2_CollarHexFastener_" + label, mesh, WORN_BRASS,
            (x, .058, z + .0055))

    # Retain the original rear fitting and side grilles: the photos show their
    # silhouette but do not establish unseen mechanisms or exact dimensions.
    export_path = OUT / "exports" / "photo-helmet-refined-v2.glb"
    export_path.parent.mkdir(parents=True, exist_ok=True)
    scene.export(export_path)
    readback = trimesh.load(export_path, force="scene")
    bounds = readback.bounds
    report = {
        "source": "PR #28 photo-helmet-study.glb; user/team reference photos 01-04 not included",
        "output": str(export_path.relative_to(OUT)),
        "original_geometry_nodes": original_nodes,
        "refined_geometry_nodes": len(readback.graph.nodes_geometry),
        "triangles": int(sum(len(g.faces) for g in readback.geometry.values())),
        "bounds_m_gltf_y_up": np.round(bounds, 6).tolist(),
        "all_finite": bool(all(np.isfinite(g.vertices).all() for g in readback.geometry.values())),
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
