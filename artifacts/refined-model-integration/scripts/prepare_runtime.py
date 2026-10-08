"""Prepare runtime FBX copies without changing the editable source assets.

Run in Blender 4.5 LTS, e.g.::

  blender -b --python prepare_runtime.py -- --repository <checkout>

Only artifacts/refined-model-integration/exports and qa are written. By default
existing outputs are refused; use a new --output directory for a repeat run.
--mark-v-source can refer to another checkout of the same reviewed source.
Mark V retains the complete hollow shell, breastplate, glass, window seat and
door casting; high-density non-role details are decimated in memory. Every
object name, parent and animation curve is retained. Equipment is exported
without geometry changes to retain all source mechanical animation paths.
These are runtime derivatives, not improved historical measurements or a VR
performance certification. Unity import, interaction and appearance are tested
separately by the integration task.
"""

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy


PROTECTED_MARK_V = {
    "Bonnet_HollowCopper_TinFinish",
    "Corselet_CopperSaddle_WithUnderside",
    "FrontSeat_CastFrame",
    "Door_CastFrame",
}
SAMPLE_SECONDS = (0.0, 0.5, 1.0, 1.5, 2.0)
MINIMUM_FINE_TRIANGLES = 1200
MINIMUM_REDUCED_TRIANGLES = 200
TARGET_MARK_V_TRIANGLES = 140000


def sha256(path):
    with path.open("rb") as stream:
        digest = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def relative(path, repository):
    try:
        return path.relative_to(repository).as_posix()
    except ValueError:
        # Source in a sibling checkout: portable suffix plus hash is the identity.
        marker = path.parts.index("artifacts") if "artifacts" in path.parts else -1
        return "/".join(path.parts[marker:]) if marker >= 0 else path.name


def family(root):
    return [root, *root.children_recursive]


def triangles(mesh):
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def topology(mesh):
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        return {
            "nonmanifold_edges": sum(not edge.is_manifold for edge in bm.edges),
            "loose_vertices": sum(not vertex.link_edges for vertex in bm.verts),
            "zero_area_faces": sum(face.calc_area() < 1e-14 for face in bm.faces),
        }
    finally:
        bm.free()


def path_of(obj, root):
    names = [obj.name]
    current = obj
    while current != root and current.parent:
        current = current.parent
        names.append(current.name)
    return "/".join(reversed(names))


def load_source(source, root_name, animated):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = (1, 61) if animated else (1, 1)
    with bpy.data.libraries.load(str(source), link=False) as (available, requested):
        requested.objects = available.objects
    for obj in requested.objects:
        if obj is not None:
            scene.collection.objects.link(obj)
    root = bpy.data.objects.get(root_name)
    if root is None:
        raise RuntimeError(f"Source is missing {root_name}: {source}")
    original_location = list(root.location)
    root.location = (0, 0, 0)
    scene.frame_set(1)
    bpy.context.view_layer.update()
    return root, original_location


def snapshot(root):
    objects = family(root)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    bpy.context.view_layer.update()
    coords = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    points = [list(point) for point in coords]
    return {
        "object_count": len(objects),
        "mesh_count": len(meshes),
        "triangles": sum(triangles(obj.data) for obj in meshes),
        "bounds_blender_m": [[min(p[i] for p in points) for i in range(3)],
                             [max(p[i] for p in points) for i in range(3)]],
        "materials": sorted({mat.name for obj in meshes for mat in obj.data.materials if mat}),
        "node_paths": sorted(path_of(obj, root) for obj in objects),
        "anchors_blender_m": {
            obj.name: list(root.matrix_world.inverted() @ obj.matrix_world.translation)
            for obj in objects if obj.name.startswith("Anchor_")
        },
        "animated_nodes": sorted(obj.name for obj in objects if obj.animation_data and obj.animation_data.action),
    }


def capture_poses(root, seconds=SAMPLE_SECONDS):
    animated = [obj for obj in family(root) if obj.animation_data and obj.animation_data.action]
    anchors = [obj for obj in family(root) if obj.name.startswith("Anchor_")]
    result = {}
    for time in seconds:
        bpy.context.scene.frame_set(round(1 + time * 30))
        bpy.context.view_layer.update()
        result[str(time)] = {
            obj.name: [float(v) for row in obj.matrix_world for v in row]
            for obj in [*animated, *anchors]
        }
    bpy.context.scene.frame_set(1)
    return result


def bake_source_modifiers(root):
    # Source QA counts evaluated meshes. Bake the same evaluated geometry before
    # measuring/reducing, without saving changes to the source .blend.
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in family(root):
        if obj.type != "MESH" or not obj.modifiers:
            continue
        geometry = bpy.data.meshes.new_from_object(
            obj.evaluated_get(depsgraph), preserve_all_data_layers=True, depsgraph=depsgraph)
        obj.modifiers.clear()
        obj.data = geometry
    bpy.context.view_layer.update()


def is_protected(obj):
    # Glass retainers are metal. Protect actual glass by material identity, not
    # by the substring Glass, which would incorrectly match a metal retainer.
    return obj.name in PROTECTED_MARK_V or any(
        mat and mat.name == "HistoricalGlass_VisualPropertiesAssumed" for mat in obj.data.materials)


def reduce_mark_v(root):
    meshes = [obj for obj in family(root) if obj.type == "MESH"]
    eligible = [obj for obj in meshes if not is_protected(obj) and triangles(obj.data) >= MINIMUM_FINE_TRIANGLES]
    fixed = sum(triangles(obj.data) for obj in meshes if obj not in eligible)
    fine = sum(triangles(obj.data) for obj in eligible)
    ratio = min(1.0, max(0.10, (TARGET_MARK_V_TRIANGLES - fixed) / max(fine, 1)))
    changes = []
    for obj in eligible:
        before_count = triangles(obj.data)
        before_topology = topology(obj.data)
        original_mesh = obj.data.copy()
        original_uvs = len(obj.data.uv_layers)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("Runtime detail reduction", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = min(1.0, max(ratio, MINIMUM_REDUCED_TRIANGLES / before_count))
        modifier.use_collapse_triangulate = True
        requested_ratio = modifier.ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        after_topology = topology(obj.data)
        reason = None
        if len(obj.data.uv_layers) != original_uvs:
            reason = "UV-layer count changed"
        elif any(after_topology[key] > before_topology[key] for key in before_topology):
            reason = "Topology issue count increased"
        if reason:
            rejected = obj.data
            obj.data = original_mesh
            bpy.data.meshes.remove(rejected)
        else:
            bpy.data.meshes.remove(original_mesh)
        changes.append({
            "name": obj.name, "parent": obj.parent.name if obj.parent else None,
            "triangles_before": before_count, "triangles_after": triangles(obj.data),
            "requested_ratio": requested_ratio, "rejected_reason": reason,
            "topology_before": before_topology, "topology_after": topology(obj.data),
        })
    return {"target_triangles": TARGET_MARK_V_TRIANGLES, "computed_ratio": ratio,
            "protected_role_names": sorted(PROTECTED_MARK_V),
            "glass_material_protected": "HistoricalGlass_VisualPropertiesAssumed",
            "small_detail_threshold_triangles": MINIMUM_FINE_TRIANGLES,
            "changes": changes, "merge_performed": False}


def export(root, output, animated):
    bpy.context.scene.frame_set(1)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in family(root):
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(output), use_selection=True, object_types={"EMPTY", "MESH"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, add_leaf_bones=False,
        use_mesh_modifiers=True, mesh_smooth_type="OFF", use_custom_props=True,
        bake_anim=animated, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0, bake_anim_step=1.0,
        path_mode="COPY", embed_textures=True)


def readback(output, root_name, baseline, source_poses, animated):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(output), use_anim=animated)
    root = bpy.data.objects.get(root_name)
    if root is None:
        raise RuntimeError(f"FBX readback missing root {root_name}")
    bpy.context.scene.frame_set(1)
    actual = snapshot(root)
    if actual["node_paths"] != baseline["node_paths"]:
        raise RuntimeError(f"FBX changed node paths for {root_name}")
    if actual["triangles"] != baseline["triangles"]:
        raise RuntimeError(f"FBX triangle count differs for {root_name}")
    bound_error = max(abs(a-b) for left, right in zip(actual["bounds_blender_m"], baseline["bounds_blender_m"])
                      for a, b in zip(left, right))
    if bound_error > 0.0002:
        raise RuntimeError(f"FBX bounds changed {bound_error}m for {root_name}")
    origin = min((action.frame_range[0] for action in bpy.data.actions), default=1)
    matrix_errors = []
    for seconds, pose in source_poses.items():
        bpy.context.scene.frame_set(round(origin + float(seconds) * bpy.context.scene.render.fps))
        bpy.context.view_layer.update()
        for name, expected in pose.items():
            obj = bpy.data.objects.get(name)
            if obj is None:
                raise RuntimeError(f"FBX missing animated/anchor node {name}")
            imported = [float(v) for row in obj.matrix_world for v in row]
            matrix_errors.append(max(abs(a-b) for a, b in zip(imported, expected)))
    maximum_matrix_error = max(matrix_errors, default=0)
    if maximum_matrix_error > 0.002:
        raise RuntimeError(f"FBX animation/anchor matrix mismatch {maximum_matrix_error} for {root_name}")
    return {"status": "PASS", "mesh_count": actual["mesh_count"], "triangles": actual["triangles"],
            "bounds_max_error_m": bound_error, "node_paths_equal": True,
            "actions_read": len(bpy.data.actions), "sample_seconds": list(SAMPLE_SECONDS),
            "max_animation_or_anchor_matrix_error": maximum_matrix_error,
            "packed_images_read": sum(bool(image.packed_file) for image in bpy.data.images)}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--mark-v-source", type=Path)
    parser.add_argument("--asset", choices=("all", "mark-v", "pump", "surface-telephone", "diver-telephone"), default="all")
    args = parser.parse_args(argv)
    repository = args.repository.resolve()
    output = (args.output or repository / "artifacts/refined-model-integration").resolve()
    mark_source = (args.mark_v_source or repository / "artifacts/helmet-refinement/eras/mark-v/sources/mark-v-mod1.blend").resolve()
    part_sources = repository / "artifacts/connection-demo-refinement/sources/parts"
    specs = [
        ("mark-v", mark_source, "MarkVMod1_ROOT", "mark-v-runtime.fbx", False),
        ("pump", part_sources / "Pump_ROOT.blend", "Pump_ROOT", "mark-iii-pump-runtime.fbx", True),
        ("surface-telephone", part_sources / "SurfaceTelephone_ROOT.blend", "SurfaceTelephone_ROOT", "surface-telephone-runtime.fbx", True),
        ("diver-telephone", part_sources / "DiverTelephone_ROOT.blend", "DiverTelephone_ROOT", "diver-telephone-runtime.fbx", True),
    ]
    chosen = [spec for spec in specs if args.asset == "all" or spec[0] == args.asset]
    # Refuse accidental overwrite before making any output.
    for name, source, root_name, filename, animated in chosen:
        if not source.is_file():
            raise FileNotFoundError(source)
        for target in (output / "exports" / filename, output / "qa" / (name + "-runtime.json")):
            if target.exists():
                raise FileExistsError(f"Generated output exists; choose a fresh --output directory: {target}")
    (output / "exports").mkdir(parents=True, exist_ok=True)
    (output / "qa").mkdir(parents=True, exist_ok=True)
    reports = []
    for name, source, root_name, filename, animated in chosen:
        digest = sha256(source)
        root, original_location = load_source(source, root_name, animated)
        bake_source_modifiers(root)
        original = snapshot(root)
        source_poses = capture_poses(root)
        reduction = reduce_mark_v(root) if name == "mark-v" else {
            "merge_performed": False, "geometry_changed": False,
            "reason": "Preserve all source node paths, animated meshes and mechanical-loop sampling."}
        after = snapshot(root)
        if original["node_paths"] != after["node_paths"] or original["materials"] != after["materials"]:
            raise RuntimeError(f"Runtime preparation changed hierarchy/material roles: {name}")
        if capture_poses(root) != source_poses:
            raise RuntimeError(f"Runtime preparation changed animation/anchor samples: {name}")
        target = output / "exports" / filename
        export(root, target, animated)
        imported = readback(target, root_name, after, source_poses, animated)
        if sha256(source) != digest:
            raise RuntimeError(f"Source file changed during preparation: {source}")
        report = {
            "status": "PASS", "asset": name, "blender": bpy.app.version_string,
            "script_sha256": sha256(Path(__file__).resolve()),
            "source": relative(source, repository), "source_sha256": digest, "source_unchanged": True,
            "original_root_location_blender_m": original_location,
            "export": "exports/" + filename, "export_sha256": sha256(target), "export_bytes": target.stat().st_size,
            "before": original, "after": after, "reduction": reduction, "readback": imported,
            "coordinates": "Source Blender Z up / -Y front. FBX Y up, forward=-Z export option. Unity importer orientation must be checked separately.",
            "limitations": ["No Unity or VR-device test in this script.",
                            "No LOD, collision-mesh or draw-call optimization.",
                            "Five-time-point mechanical/anchor export checks are sampled, not exhaustive continuous validation.",
                            "Texture embedding is retained; final Unity materials are assigned by integration."]}
        destination = output / "qa" / (name + "-runtime.json")
        destination.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        reports.append({"asset": name, "triangles_before": original["triangles"],
                        "triangles_after": after["triangles"], "mesh_count": after["mesh_count"],
                        "export_bytes": target.stat().st_size, "readback": imported["status"]})
        print("RUNTIME_ASSET_READY " + json.dumps(reports[-1]), flush=True)
    (output / "qa" / ("runtime-preparation" + ("" if args.asset == "all" else "-" + args.asset) + ".json")).write_text(
        json.dumps({"status": "PASS", "assets": reports}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("RUNTIME_PREPARATION_PASS", flush=True)


if __name__ == "__main__":
    main()
