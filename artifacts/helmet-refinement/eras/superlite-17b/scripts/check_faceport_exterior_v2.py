"""Read Blender sources and GLB exports back for basic independent-part QA."""

import bpy
import json
import os
import struct
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if len(args) != 1:
    raise SystemExit("usage: blender -b -P check_faceport_exterior_v2.py -- ASSET_DIR")
root = os.path.abspath(args[0])
result = {"blender": bpy.app.version_string, "parts": {}}

for stem in ("SL17B_FacePortAssembly_v2", "SL17B_SideBlockControls_v2"):
    source = os.path.join(root, "sources", "parts", stem + ".blend")
    export = os.path.join(root, "exports", stem + ".glb")
    bpy.ops.wm.open_mainfile(filepath=source)
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and
              not o.name.startswith("REVIEW_ONLY")]
    source_names = {o.name for o in meshes}
    source_uv_missing = [o.name for o in meshes if not o.data.uv_layers]
    source_triangles = sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)
    source_bounds = [tuple(round(v, 5) for v in
                     (min((o.matrix_world @ vert.co)[a] for o in meshes for vert in o.data.vertices),
                      max((o.matrix_world @ vert.co)[a] for o in meshes for vert in o.data.vertices)))
                     for a in range(3)]
    packed_images = sorted(i.name for i in bpy.data.images if i.packed_file)
    retainer_depth = None
    retainer_fastener_count = None
    distinct_sideblock_controls = None
    if stem == "SL17B_FacePortAssembly_v2":
        retainer = next(o for o in meshes if o.name.startswith("Port_Retainer_"))
        ys = [(retainer.matrix_world @ v.co).y for v in retainer.data.vertices]
        retainer_depth = round(max(ys) - min(ys), 6)
        retainer_fastener_count = sum(n.startswith("Retainer_Fastener_") for n in source_names)
    else:
        distinct_sideblock_controls = (any("520-524" in n for n in source_names) and
                                       any("520-525" in n for n in source_names))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=export)
    imported = [o for o in bpy.data.objects if o.type == "MESH"]
    export_names = {o.name for o in imported}
    export_uv_missing = [o.name for o in imported if not o.data.uv_layers]
    export_triangles = sum(len(p.vertices) - 2 for o in imported for p in o.data.polygons)
    with open(export, "rb") as f:
        glb = f.read()
    if glb[:4] != b"glTF":
        raise SystemExit("Invalid GLB header: " + export)
    json_size = struct.unpack_from("<I", glb, 12)[0]
    gltf = json.loads(glb[20:20 + json_size])
    transparent = [m.get("name", "") for m in gltf.get("materials", [])
                   if m.get("alphaMode") == "BLEND"]
    all_ok = (len(meshes) == len(imported) and
              not source_uv_missing and not export_uv_missing and
              not any(n.startswith("REVIEW_ONLY") for n in export_names) and
              source_names == export_names and
              (stem != "SL17B_FacePortAssembly_v2" or
               (transparent == ["Polycarbonate lens - interpretive tint"] and
                retainer_depth == 0.012 and
                retainer_fastener_count == 11 and
                not any("UpperVisor" in n for n in source_names))))
    all_ok = all_ok and (stem != "SL17B_SideBlockControls_v2" or distinct_sideblock_controls)
    result["parts"][stem] = {
        "passed": all_ok,
        "source_meshes": len(meshes),
        "glb_meshes": len(imported),
        "source_triangles_without_modifiers": source_triangles,
        "glb_triangles_after_modifiers": export_triangles,
        "source_local_bounds_m_provisional": source_bounds,
        "source_uv_missing": source_uv_missing,
        "glb_uv_missing": export_uv_missing,
        "packed_images": packed_images,
        "transparent_glb_materials": transparent,
        "names_match": source_names == export_names,
        "review_objects_excluded": not any(n.startswith("REVIEW_ONLY") for n in export_names)
        ,"retainer_depth_m": retainer_depth,
        "retainer_fastener_count": retainer_fastener_count,
        "distinct_sideblock_controls": distinct_sideblock_controls
    }

result["passed"] = all(item["passed"] for item in result["parts"].values())
out = os.path.join(root, "qa", "faceport-exterior-v2-readback.json")
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, "w", encoding="utf-8") as f:
    json.dump(result, f, ensure_ascii=False, indent=2)
print("QA_REPORT", out, "PASS" if result["passed"] else "FAIL")
if not result["passed"]:
    raise SystemExit(1)
