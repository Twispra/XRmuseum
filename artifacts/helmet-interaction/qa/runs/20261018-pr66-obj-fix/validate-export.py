"""Reproduce PR #66's negative control and verify the repaired saved exports.

Run from the repository root with NumPy/Pillow installed. trimesh is needed
only for the small mixed-UV fixture and the source-scene material guard.
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True

import numpy as np
from PIL import Image
import trimesh
from trimesh.exchange.obj import export_obj
from trimesh.visual.material import PBRMaterial
from trimesh.visual.texture import TextureVisuals

ROOT = Path(__file__).resolve().parents[5]
SCRIPT = ROOT / "artifacts/helmet-refinement/photo-study-v2/scripts/export_unity_section_obj.py"
MODELS = ROOT / "MuseumVR/Assets/Museum/Features/PhotoHelmetTrial/Models"
BASE = "da52091d1c0ad46abb717a89e10a3afd1facce3a"
spec = importlib.util.spec_from_file_location("photo_export", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def git(*args):
    return subprocess.check_output(["git", "-C", str(ROOT), *args])


common = Path(git("rev-parse", "--git-common-dir").decode().strip())
if not common.is_absolute():
    common = ROOT / common


def old_bytes(path):
    data = git("show", f"{BASE}:{path}")
    if data.startswith(b"version https://git-lfs"):
        sha = re.search(rb"oid sha256:(\w+)", data)[1].decode()
        data = (common / "lfs/objects" / sha[:2] / sha[2:4] / sha).read_bytes()
        assert hashlib.sha256(data).hexdigest() == sha
    return data


report = {"base": BASE, "testedSourceCommit": git("rev-parse", "HEAD").decode().strip(),
          "python": sys.version, "trimesh": trimesh.__version__, "checks": []}


def check(name, passed, detail):
    report["checks"].append({"name": name, "passed": bool(passed), "detail": detail})
    assert passed, name


old_details = old_bytes((MODELS / "photo-helmet-refined-v2-details.obj").relative_to(ROOT).as_posix()).decode()
try:
    module.validate_obj_indices(old_details)
except ValueError as error:
    check("negative control rejects the original saved OBJ", "18159 faces" in str(error), str(error))
else:
    raise AssertionError("Original invalid OBJ was accepted")


def split_obj(text):
    records = {key: [] for key in ("v", "vn", "f", "o", "usemtl")}
    groups = {}
    group = None
    for line in text.splitlines():
        parts = line.split()
        if not parts:
            continue
        kind = parts[0]
        if kind in records:
            # UV references intentionally change; vertex/normal topology does not.
            records[kind].append([(p.split('/')[0], p.split('/')[-1]) for p in parts[1:]]
                                 if kind == "f" else parts[1:])
        if kind == "o":
            group = parts[1]
            groups[group] = {"uv": [], "vertices": 0}
        if kind == "v":
            groups[group]["vertices"] += 1
        if kind == "vt":
            groups[group]["uv"].append(tuple(float(x) for x in parts[1:]))
    return records, groups


for part, expected in (("details", 107248), ("shell", 10056)):
    path = MODELS / f"photo-helmet-refined-v2-{part}.obj"
    original = old_bytes(path.relative_to(ROOT).as_posix()).decode()
    current = path.read_text(encoding="utf-8")
    counts = module.validate_obj_indices(current)
    check(part + " all three index streams valid", counts["v"] == counts["vt"] == counts["vn"], counts)
    check(part + " triangle count preserved", counts["f"] == expected, expected)
    a, ag = split_obj(original)
    b, bg = split_obj(current)
    check(part + " vertex coordinates preserved", a["v"] == b["v"], counts["v"])
    check(part + " numerical normals preserved", np.array_equal(np.array(a["vn"], float), np.array(b["vn"], float)), counts["vn"])
    check(part + " vertex and normal face topology preserved", a["f"] == b["f"], counts["f"])
    check(part + " object order and material bindings preserved", a["o"] == b["o"] and a["usemtl"] == b["usemtl"], len(ag))
    for name, old in ag.items():
        new = bg[name]
        if old["uv"]:
            assert old["uv"] == new["uv"], name + " changed existing UVs"
        else:
            assert new["uv"] == [(0., 0.)] * old["vertices"], name + " incorrect neutral UVs"
    check(part + " existing UVs preserved and missing UVs neutral", True, len(ag))

for path in [*MODELS.glob("*.png"), MODELS / "material.mtl", *MODELS.glob("*.meta"),
             ROOT / "MuseumVR/Assets/Museum/Features/PhotoHelmetTrial/Prefabs/PhotoHelmetStudy.prefab",
             ROOT / "MuseumVR/Assets/Museum/Features/PhotoHelmetTrial/Scenes/PhotoHelmetComparison.unity",
             ROOT / "artifacts/helmet-refinement/photo-study-v2/exports/photo-helmet-refined-v2.glb"]:
    old = old_bytes(path.relative_to(ROOT).as_posix())
    current = path.read_bytes()
    if path.suffix in (".mtl", ".meta", ".prefab", ".unity"):
        old = old.replace(b"\r\n", b"\n")
        current = current.replace(b"\r\n", b"\n")
    check("preserved " + path.name, old == current, hashlib.sha256(current).hexdigest())

source = trimesh.load(ROOT / "artifacts/helmet-refinement/photo-study-v2/exports/photo-helmet-refined-v2.glb", force="scene")
check("source has exactly 11606 vertices needing neutral UVs", module.fill_missing_uvs(source) == 11606, 11606)

vertices = [[0, 0, 0], [1, 0, 0], [0, 1, 0]]
uv = np.array([[0., 0.], [1., 0.], [0., 1.]])
plain = trimesh.Trimesh(vertices=vertices, faces=[[0, 1, 2]], process=False,
                        vertex_normals=[[0, 0, 1]] * 3,
                        visual=TextureVisuals(uv=None, material=PBRMaterial()))
textured = plain.copy()
textured.visual = TextureVisuals(uv=uv.copy(), material=PBRMaterial(baseColorTexture=Image.new("RGB", (2, 2))))
fixture = trimesh.Scene()
fixture.add_geometry(plain, node_name="plain", geom_name="plain")
fixture.add_geometry(textured, node_name="textured", geom_name="textured")
raw, _ = export_obj(fixture, include_normals=True, include_texture=True, return_texture=True)
try:
    module.validate_obj_indices(raw)
except ValueError:
    check("mixed UV fixture reproduces exporter failure", True, "untextured mesh before textured mesh")
else:
    raise AssertionError("Fixture did not reproduce mixed-UV exporter defect")
module.fill_missing_uvs(fixture)
fixed, _ = export_obj(fixture, include_normals=True, include_texture=True, return_texture=True)
check("mixed UV fixture repaired without changing original UVs", module.validate_obj_indices(fixed)["vt"] == 6 and
      np.array_equal(textured.visual.uv, uv), "6 valid vertex/UV/normal references")

for slot in ("baseColorTexture", "metallicRoughnessTexture", "normalTexture", "occlusionTexture", "emissiveTexture"):
    material = PBRMaterial(**{slot: Image.new("RGB", (2, 2))})
    bad = plain.copy()
    bad.visual = TextureVisuals(uv=None, material=material)
    try:
        module.fill_missing_uvs(trimesh.Scene(bad))
    except ValueError:
        check("missing UV with " + slot + " rejected", True, "no invented texture mapping")
    else:
        raise AssertionError(slot + " missing-UV guard failed")

out = Path(__file__).with_name("static-validation.json")
out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"PASS {len(report['checks'])} checks; {out}")
