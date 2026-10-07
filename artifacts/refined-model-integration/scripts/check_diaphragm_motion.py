"""Read-only Blender source/FBX motion comparison with noncommensurate phases.

Run in Blender 4.5 LTS:
  blender -b --factory-startup --python check_diaphragm_motion.py -- --repository <checkout>

Only the fresh JSON output is written; .blend and FBX inputs are never saved.
Use --output <fresh-json-path> to preserve an earlier report when repeating.
This checks illustrative animation curves, not historical dimensions, acoustic
physics, Unity import compression, headset visibility or user comfort.
"""

import argparse
import hashlib
import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy


PHASE_SECONDS = (0.037, 0.091, 0.153, 0.219, 0.307, 0.463, 0.731, 0.947,
                 1.037, 1.091, 1.153, 1.219, 1.307, 1.463, 1.731, 1.947)
FULL_CYCLE_SECONDS = tuple(index / 120 for index in range(241))
SAMPLE_SECONDS = tuple(sorted(set((*FULL_CYCLE_SECONDS, *PHASE_SECONDS))))
MINIMUM_MOTION_M = 0.0001
MAXIMUM_POSITION_ERROR_M = 0.00001


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def portable(path, repository):
    try:
        return path.relative_to(repository).as_posix()
    except ValueError:
        return path.name


def set_seconds(scene, frame_origin, seconds):
    frame = frame_origin + seconds * scene.render.fps / scene.render.fps_base
    floor = math.floor(frame)
    scene.frame_set(floor, subframe=frame - floor)
    bpy.context.view_layer.update()


def load_source(path, root_name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene.render.fps = 30
    with bpy.data.libraries.load(str(path), link=False) as (available, requested):
        requested.objects = available.objects
    for obj in requested.objects:
        if obj is not None:
            scene.collection.objects.link(obj)
    root = bpy.data.objects.get(root_name)
    if root is None:
        raise RuntimeError(f"Missing source root {root_name}")
    root.location = (0, 0, 0)
    return root, 1.0


def load_fbx(path, root_name, node_name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 30
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
    root = bpy.data.objects.get(root_name)
    node = bpy.data.objects.get(node_name)
    if root is None or node is None or node.animation_data is None or node.animation_data.action is None:
        raise RuntimeError(f"FBX lacks required root/animated diaphragm {root_name}/{node_name}")
    root.location = (0, 0, 0)
    # FBX import has a frame offset; compare seconds since this node's own take start.
    return root, float(node.animation_data.action.frame_range[0])


def trace(root, node_name, origin):
    node = bpy.data.objects.get(node_name)
    if node is None or node.animation_data is None or node.animation_data.action is None:
        raise RuntimeError(f"Missing animated diaphragm {node_name}")
    scene = bpy.context.scene
    values = []
    for seconds in SAMPLE_SECONDS:
        set_seconds(scene, origin, seconds)
        values.append({
            "seconds": seconds,
            "local_m": list(node.matrix_basis.translation),
            "root_relative_world_m": list(root.matrix_world.inverted() @ node.matrix_world.translation),
            "world_m": list(node.matrix_world.translation),
        })
    return values, {
        "frame_origin": origin,
        "action_frame_range": list(node.animation_data.action.frame_range),
        "frames_per_second": scene.render.fps / scene.render.fps_base,
    }


def motion_stats(values, key):
    points = [row[key] for row in values]
    spans = [max(point[axis] for point in points) - min(point[axis] for point in points) for axis in range(3)]
    peak_to_peak = max(math.dist(left, right) for i, left in enumerate(points) for right in points[i + 1:])
    return {"axis_ranges_m": spans, "sampled_peak_to_peak_m": peak_to_peak,
            "max_displacement_from_start_m": max(math.dist(points[0], point) for point in points),
            "motion_exceeds_0_1_mm": peak_to_peak > MINIMUM_MOTION_M}


def compare_asset(repository, label, root_name, node_name, fbx_name):
    source = repository / "artifacts/connection-demo-refinement/sources/parts" / (root_name + ".blend")
    runtime = repository / "MuseumVR/Assets/Museum/Features/RefinedModels/Models" / fbx_name
    export = repository / "artifacts/refined-model-integration/exports" / fbx_name
    hashes_before = {str(path): digest(path) for path in (source, runtime, export)}
    if hashes_before[str(runtime)] != hashes_before[str(export)]:
        raise RuntimeError(f"Unity runtime copy differs from prepared FBX: {fbx_name}")
    root, origin = load_source(source, root_name)
    original, source_time = trace(root, node_name, origin)
    root, origin = load_fbx(runtime, root_name, node_name)
    imported, imported_time = trace(root, node_name, origin)
    keys = ("local_m", "root_relative_world_m", "world_m")
    errors = {key: max(math.dist(left[key], right[key]) for left, right in zip(original, imported)) for key in keys}
    source_motion = {key: motion_stats(original, key) for key in keys}
    imported_motion = {key: motion_stats(imported, key) for key in keys}
    amplitude_errors = {key: abs(source_motion[key]["sampled_peak_to_peak_m"] - imported_motion[key]["sampled_peak_to_peak_m"]) for key in keys}
    unchanged = all(digest(path) == hashes_before[str(path)] for path in (source, runtime, export))
    pass_checks = {
        "source_local_and_world_motion_present": all(value["motion_exceeds_0_1_mm"] for value in source_motion.values()),
        "fbx_local_and_world_motion_present": all(value["motion_exceeds_0_1_mm"] for value in imported_motion.values()),
        "sampled_position_matches_within_10_um": max(errors.values()) < MAXIMUM_POSITION_ERROR_M,
        "sampled_motion_range_matches_within_10_um": max(amplitude_errors.values()) < MAXIMUM_POSITION_ERROR_M,
        "inputs_unchanged": unchanged,
        "runtime_copy_matches_prepared_export": True,
    }
    phase_samples = [{"seconds": left["seconds"], "source": {key: left[key] for key in keys},
                      "fbx": {key: right[key] for key in keys}}
                     for left, right in zip(original, imported) if left["seconds"] in PHASE_SECONDS]
    return {
        "status": "PASS" if all(pass_checks.values()) else "FAIL", "asset": label,
        "root": root_name, "animated_node": node_name,
        "inputs": [{"path": portable(path, repository), "sha256": hashes_before[str(path)], "bytes": path.stat().st_size}
                   for path in (source, runtime, export)],
        "source_time": source_time, "fbx_time": imported_time,
        "source_motion": source_motion, "fbx_motion": imported_motion,
        "max_sampled_position_error_m": errors,
        "sampled_motion_range_error_m": amplitude_errors,
        "checks": pass_checks, "noncommensurate_phase_samples": phase_samples,
    }


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--output", type=Path)
    args = parser.parse_args(argv)
    repository = args.repository.resolve()
    output = (args.output or repository / "artifacts/refined-model-integration/qa/diaphragm-motion-readback.json").resolve()
    if output.exists():
        raise FileExistsError(f"Keep existing evidence; choose a fresh --output path: {output}")
    results = [compare_asset(repository, "surface-telephone", "SurfaceTelephone_ROOT", "SurfaceDiaphragm", "surface-telephone-runtime.fbx"),
               compare_asset(repository, "diver-telephone", "DiverTelephone_ROOT", "DiverDiaphragm", "diver-telephone-runtime.fbx")]
    report = {
        "status": "PASS" if all(result["status"] == "PASS" for result in results) else "FAIL",
        "created_utc": datetime.now(timezone.utc).isoformat(), "blender": bpy.app.version_string,
        "script": portable(Path(__file__).resolve(), repository), "script_sha256": digest(Path(__file__).resolve()),
        "sampling": {"cycle_seconds": 2, "dense_samples": 241, "dense_step_seconds": 1 / 120,
                     "noncommensurate_phase_seconds": list(PHASE_SECONDS), "total_samples_per_trace": len(SAMPLE_SECONDS),
                     "fractional_frames": True, "comparison_frame_origin": "source frame 1 vs FBX node action first frame"},
        "thresholds": {"minimum_sampled_peak_to_peak_motion_m": MINIMUM_MOTION_M,
                       "maximum_sampled_position_or_range_error_m": MAXIMUM_POSITION_ERROR_M},
        "assets": results,
        "limitations": ["Dense finite samples, not proof of continuous agreement at every instant.",
                        "Positions are Blender meter-space readback; Unity import/compression needs separate actual Unity testing.",
                        "Authored diaphragm displacement is an illustrative teaching motion, not a measured historical mechanism or precision engineering tolerance.",
                        "This does not test headset performance, perception, acoustics or comfort."]}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("DIAPHRAGM_MOTION_READBACK_" + report["status"])
    for asset in results:
        print(asset["asset"], "world peak-to-peak", asset["fbx_motion"]["world_m"]["sampled_peak_to_peak_m"],
              "max error", max(asset["max_sampled_position_error_m"].values()))
    if report["status"] != "PASS":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
