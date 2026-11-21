"""Exercise the published GLB readback against positive and negative temporary copies.

blender -b --python-exit-code 1 -P check_export_regressions.py --
        --model helmet.blend --glb helmet.glb --report fresh.json
"""
import argparse
import json
from pathlib import Path
import struct
import sys
import tempfile

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_review import LENS, digest, run


def change_texture_binding(document, material, case, binary_chunk, dest):
    info = material["pbrMetallicRoughness"]["baseColorTexture"]
    transform = {"texCoord": 0}
    if case == "mirrored_texture_transform":
        transform.update(offset=[1., 0.], scale=[-1., 1.])
    elif case == "wrong_texture_channel":
        transform["texCoord"] = 1
    elif case == "identity_texture_transform":
        transform.update(offset=[0., 0.], scale=[1., 1.], rotation=0.)
    else:
        raise ValueError(case)
    extension = "KHR_texture_transform"
    info.setdefault("extensions", {})[extension] = transform
    used = document.setdefault("extensionsUsed", [])
    if extension not in used:
        used.append(extension)
    payload = json.dumps(document, separators=(",", ":")).encode("utf-8")
    payload += b" " * (-len(payload) % 4)
    with dest.open("xb") as stream:
        stream.write(struct.pack("<4sIII4s", b"glTF", 2, 20 + len(payload) + len(binary_chunk), len(payload), b"JSON"))
        stream.write(payload)
        stream.write(binary_chunk)
    return material["name"]


def modify_glb(source, dest, case, name):
    data = bytearray(source.read_bytes())
    json_size = struct.unpack_from("<I", data, 12)[0]
    document = json.loads(data[20:20 + json_size])
    bin_start = 20 + json_size + 8
    node = next(node for node in document["nodes"] if node["name"] == name)
    primitives = document["meshes"][node["mesh"]]["primitives"]
    if len(primitives) != 1 or primitives[0].get("mode", 4) != 4:
        raise ValueError(f"Expected one triangle primitive on {name}")
    primitive = primitives[0]
    if case in ("mirrored_texture_transform", "wrong_texture_channel", "identity_texture_transform"):
        return change_texture_binding(document, document["materials"][primitive["material"]],
                                      case, data[bin_start - 8:], dest)
    uv_case = case == "mirrored_marking"
    index = primitive["attributes"]["TEXCOORD_0"] if uv_case else primitive["indices"]
    accessor = document["accessors"][index]
    view = document["bufferViews"][accessor["bufferView"]]
    if view["buffer"] != 0 or "sparse" in accessor:
        raise ValueError("The regression fixture needs a regular GLB binary accessor")
    offset = bin_start + view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    count = accessor["count"]
    if uv_case:
        if accessor["type"] != "VEC2" or accessor["componentType"] != 5126:
            raise ValueError("Expected float32 texture coordinates on the marking")
        uv = np.ndarray((count, 2), "<f4", data, offset, strides=(view.get("byteStride", 8), 4))
        uv[:, 0] = 1 - uv[:, 0]
        affected = count
    else:
        if accessor["type"] != "SCALAR" or count % 3 or "byteStride" in view:
            raise ValueError("Expected packed triangle indices on the lens")
        dtype = {5121: "u1", 5123: "<u2", 5125: "<u4"}[accessor["componentType"]]
        faces = np.frombuffer(data, dtype=dtype, count=count, offset=offset).reshape(-1, 3)
        if case == "flipped_lens":
            faces[:] = faces[:, [0, 2, 1]]
        elif case == "reordered_lens":
            faces[:] = np.roll(faces[::-1], 1, axis=1)
        else:
            raise ValueError(f"Unknown face change {case}")
        affected = len(faces)
    with dest.open("xb") as stream:
        stream.write(data)
    return affected


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ("model", "glb", "report"):
        parser.add_argument("--" + flag, required=True, type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if args.report.exists():
        raise FileExistsError(args.report)
    baseline = run(args.model, args.glb, verify_existing=True)
    cases = {}
    with tempfile.TemporaryDirectory(prefix="17b-glb-regression-") as scratch:
        for case, name in (("reordered_lens", LENS), ("mirrored_marking", "REG_Purge marking"),
                           ("flipped_lens", LENS), ("identity_texture_transform", "REG_Purge marking"),
                           ("mirrored_texture_transform", "REG_Purge marking"),
                           ("wrong_texture_channel", "REG_Purge marking")):
            changed = Path(scratch) / (case + ".glb")
            affected = modify_glb(args.glb, changed, case, name)
            if case in ("reordered_lens", "identity_texture_transform"):
                result = run(args.model, changed, verify_existing=True)
                field = "reordered_triangles" if case == "reordered_lens" else "material"
                cases[case] = {"accepted": True, field: affected,
                               "verified_triangles": result["oriented_triangles_verified_by_material"]}
                continue
            expected_error = f"Oriented triangle or corner UV mismatch on {name}/"
            field = "changed_uvs" if case == "mirrored_marking" else "reversed_triangles"
            if case == "mirrored_texture_transform":
                expected_error, field = f"Base-color texture transform mismatch on {affected}", "material"
            elif case == "wrong_texture_channel":
                expected_error, field = f"Base-color texture UV mismatch on {affected}", "material"
            try:
                run(args.model, changed, verify_existing=True)
            except ValueError as error:
                if not str(error).startswith(expected_error):
                    raise
                cases[case] = {"rejected": True, field: affected, "reason": str(error)}
            else:
                raise AssertionError(f"Readback accepted {case}")
    if digest(args.glb) != baseline["glb_sha256"] or digest(args.model) != baseline["source_sha256"]:
        raise ValueError("A regression input changed")
    result = {"source_sha256": baseline["source_sha256"], "glb_sha256": baseline["glb_sha256"],
              "blender": baseline["blender"], "published_file_accepted": True, "cases": cases}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    with args.report.open("x", encoding="utf-8") as stream:
        print(json.dumps(result, indent=2, ensure_ascii=False), file=stream)
    print(json.dumps(cases, ensure_ascii=False))


if __name__ == "__main__":
    main()
