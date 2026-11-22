"""Derive the third-era FBX from PR #94's approved, triangulated GLB.

Blender 4.5.14: blender -b -P export_r2_runtime.py -- --source <GLB> --out <new directory>
The two PNGs are extracted verbatim for use by Unity's inspection shader.
"""
import argparse
import hashlib
import json
import re
import struct
import sys
from collections import Counter
from pathlib import Path

import bpy
import numpy as np
from mathutils.kdtree import KDTree


SOURCE_SHA256 = "82de680095b45aa24b45b523510c244488394d7f84c6f32b9674a6295885e4bb"
GLASS = "FP_Lens_520-004"
SHELL = "SL17B_YellowShell_Study"
PORT = {"FP_PortSeal_510-260", "FP_PortRetainer_560-070", GLASS}
AIR = "SB_Nonreturn_555-195_valve"
POSTS = ("CommsPost_1_515-035", "CommsPost_2_515-035")
WINDOW_GROUP = "SL17B_InspectionWindow"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def unity_name(name):
    return re.sub(r"[^A-Za-z0-9_-]+", "_", name).strip("_")


def read_materials(source, output):
    json_size = struct.unpack_from("<I", source, 12)[0]
    gltf = json.loads(source[20:20 + json_size])
    binary = memoryview(source)[28 + json_size:]
    image_paths = []
    for image in gltf["images"]:
        view = gltf["bufferViews"][image["bufferView"]]
        start = view.get("byteOffset", 0)
        data = binary[start:start + view["byteLength"]]
        if image["mimeType"] != "image/png" or bytes(data[:8]) != b"\x89PNG\r\n\x1a\n":
            raise ValueError("Unexpected image format")
        filename = "r2-" + unity_name(image["name"]) + ".png"
        (output / filename).write_bytes(data)
        image_paths.append(filename)
    result = []
    for material in gltf["materials"]:
        pbr = material["pbrMetallicRoughness"]
        texture = pbr.get("baseColorTexture")
        source_name = material["name"]
        extensions = material.get("extensions", {})
        result.append({
            "name": "R2_" + unity_name(source_name),
            "source_name": source_name,
            "base_color_linear": pbr.get("baseColorFactor", [1, 1, 1, 1]),
            "metallic": pbr.get("metallicFactor", 1.0),
            "roughness": pbr.get("roughnessFactor", 1.0),
            "transmission": extensions.get("KHR_materials_transmission", {}).get("transmissionFactor", 0.0),
            "texture": image_paths[gltf["textures"][texture["index"]]["source"]] if texture else "",
        })
    return result, image_paths


def world_points(obj):
    points = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
    obj.data.vertices.foreach_get("co", points)
    matrix = np.asarray(obj.matrix_world)
    return points.reshape(-1, 3) @ matrix[:3, :3].T + matrix[:3, 3]


def mesh_snapshot(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    tris = mesh.loop_triangles
    if len(mesh.uv_layers) != 1:
        raise ValueError(f"Expected one source UV channel on {obj.name}")
    faces = np.empty(len(tris) * 3, dtype=np.int32)
    loops = np.empty_like(faces)
    slots = np.empty(len(tris), dtype=np.int32)
    tris.foreach_get("vertices", faces)
    tris.foreach_get("loops", loops)
    tris.foreach_get("material_index", slots)
    uv = np.empty(len(mesh.loops) * 2, dtype=np.float32)
    mesh.uv_layers[0].uv.foreach_get("vector", uv)
    points = world_points(obj)
    corners = np.concatenate((points[faces.reshape(-1, 3)], uv.reshape(-1, 2)[loops.reshape(-1, 3)]), axis=2)
    if not np.isfinite(corners).all():
        raise ValueError(f"Invalid world coordinates or UVs: {obj.name}")
    return {mesh.materials[i].name: corners[slots == i] for i in np.unique(slots)}


def verify_faces(source, loaded):
    """Check the oriented positions and first UV set of every material's triangles."""
    if source.shape != loaded.shape:
        raise ValueError("Face count changed")
    tolerance = 2e-6
    def equal(a, b):
        return np.any([np.max(np.abs(a - np.roll(b, shift, axis=-2)), axis=(-2, -1)) <= tolerance
                       for shift in range(3)], axis=0)
    if equal(source, loaded).all():
        return
    centers = loaded[:, :, :3].mean(axis=1)
    tree = KDTree(len(centers))
    for i, center in enumerate(centers):
        tree.insert(center, i)
    tree.balance()
    used = set()
    for a in source:
        candidates = [i for _, i, _ in tree.find_range(a[:, :3].mean(axis=0), tolerance * 2) if i not in used]
        if not candidates:
            raise ValueError("Missing source triangle")
        matches = np.flatnonzero(equal(a, loaded[candidates]))
        if not len(matches):
            raise ValueError("World position, UV or winding changed")
        used.add(candidates[matches[0]])


def empty(name, parent=None, position=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = position
    return obj


def part_center(obj):
    points = world_points(obj)
    return (points.min(axis=0) + points.max(axis=0)) / 2


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    data = args.source.read_bytes()
    if digest(data) != SOURCE_SHA256:
        raise ValueError("Expected the PR #94 500-010 delivery GLB (LFS object 82de6800)")
    args.out.mkdir(parents=True, exist_ok=True)
    if any(args.out.iterdir()):
        raise ValueError("Use a fresh output directory")
    materials, textures = read_materials(data, args.out)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(args.source), import_pack_images=True, merge_vertices=False)
    for filename in textures:
        image_name = Path(filename).stem.removeprefix("r2-")
        bpy.data.images[image_name].filepath_raw = str(args.out / filename)
    for material in materials:
        bpy.data.materials[material["source_name"]].name = material["name"]
    parts = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    names = {obj: unity_name(obj.name) for obj in parts}
    if len(parts) != 173 or len(set(names.values())) != len(names):
        raise ValueError("Unexpected mesh inventory or ambiguous Unity name")
    root = empty("SL17B_R2_RuntimeRoot")
    door = empty(WINDOW_GROUP, root)
    for obj, name in names.items():
        world = obj.matrix_world.copy()
        obj.name = name
        obj.parent = door if name in PORT or name.startswith(("FP_RetainerScrew_", "FP_PortPlug_")) else root
        obj.matrix_world = world
    if len(door.children) != 16:
        raise ValueError("Unexpected inspection-panel assembly")
    bpy.context.view_layer.update()
    empty("Anchor_Up", root, (0, 0, .1))
    empty("Anchor_Front", root, (0, -.1, 0))
    empty("Anchor_AirOutlet", root, part_center(bpy.data.objects[AIR]))
    empty("Anchor_TelephoneOutlet", root, np.mean([part_center(bpy.data.objects[k]) for k in POSTS], axis=0))
    bpy.context.view_layer.update()
    original = {obj.name: mesh_snapshot(obj) for obj in parts}
    window_parts = sorted(obj.name for obj in door.children)
    full = np.vstack([world_points(obj) for obj in parts])
    bounds = [full.min(axis=0).tolist(), full.max(axis=0).tolist()]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root, *root.children_recursive]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    fbx = args.out / "superlite17b-r2-runtime.fbx"
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={"EMPTY", "MESH"},
                             axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False,
                             mesh_smooth_type="OFF", use_custom_props=False, path_mode="STRIP")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx), use_anim=False)
    bpy.context.view_layer.update()
    loaded = {o.name: o for o in bpy.context.scene.objects if o.type == "MESH"}
    if loaded.keys() != original.keys() or sorted(o.name for o in bpy.data.objects[WINDOW_GROUP].children) != window_parts:
        raise ValueError("Runtime mesh hierarchy changed on readback")
    counts = Counter()
    parts_report = []
    for name, source in original.items():
        actual = mesh_snapshot(loaded[name])
        if source.keys() != actual.keys():
            raise ValueError(f"Material assignment changed: {name}")
        for material, faces in source.items():
            try:
                verify_faces(faces, actual[material])
            except ValueError as error:
                raise ValueError(f"{name}/{material}: {error}") from error
            counts[material] += len(faces)
        parts_report.append({
            "name": name,
            "materials": [m.name for m in loaded[name].data.materials],
            "triangles": sum(len(faces) for faces in source.values()),
            "moves_with_window": name in window_parts,
        })
    if counts.total() != 265244:
        raise ValueError("Wrong number of triangles")
    report = {
        "passed": True, "blender": bpy.app.version_string,
        "source_commit": "8a7d319f407fea858b1f177c00b3279954e1543d", "source_sha256": SOURCE_SHA256,
        "export_sha256": digest(fbx.read_bytes()), "meshes": len(loaded), "triangles": counts.total(),
        "bounds_blender_m": bounds,
        "size_unity_m": [bounds[1][i] - bounds[0][i] for i in [0, 2, 1]],
        "window_parts": window_parts, "materials": materials,
        "parts": sorted(parts_report, key=lambda part: part["name"]),
        "triangles_by_material": dict(counts), "verified_oriented_uv_corners": counts.total() * 3,
        "textures_sha256": {name: digest((args.out / name).read_bytes()) for name in textures},
    }
    (args.out / "r2-runtime-readback.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"R2 FBX verified: {len(loaded)} meshes, {counts.total()} triangles, {len(materials)} materials.")


if __name__ == "__main__":
    main()
