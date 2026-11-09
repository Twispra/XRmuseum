"""Read-only audit of the saved three-era asset chain in a repository checkout."""
import argparse
import hashlib
import json
import re
import struct
import subprocess
from pathlib import Path


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT).decode("utf-8").strip()


def fingerprint(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return {"path": path.relative_to(ROOT).as_posix(), "bytes": path.stat().st_size,
            "sha256": digest.hexdigest()}


def guid(path):
    meta = path.with_name(path.name + ".meta")
    return re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8"), re.M)[1]


def glb_info(path):
    with path.open("rb") as stream:
        magic, version, length, json_length, chunk_type = struct.unpack("<5I", stream.read(20))
        assert magic == 0x46546c67 and version == 2 and length == path.stat().st_size and chunk_type == 0x4e4f534a
        body = json.loads(stream.read(json_length))
    primitives = [primitive for mesh in body["meshes"] for primitive in mesh["primitives"]]
    assert all(p.get("mode", 4) == 4 for p in primitives)
    return {"meshCount": len(body["meshes"]), "triangles": sum(
        body["accessors"][p["indices"] if "indices" in p else p["attributes"]["POSITION"]]["count"] // 3
        for p in primitives)}


def obj_info(path):
    with path.open(encoding="utf-8") as stream:
        rows = list(stream)
    return {"objectCount": sum(row.startswith("o ") for row in rows),
            "triangles": sum(len(row.split()) - 3 for row in rows if row.startswith("f "))}


def renderer_groups(prefab):
    headers = list(re.finditer(r"^--- !u!(\d+) &(-?\d+).*$", prefab, re.M))
    blocks = {}
    for index, header in enumerate(headers):
        end = headers[index + 1].start() if index + 1 < len(headers) else len(prefab)
        blocks[header[2]] = (header[1], prefab[header.end():end])

    mesh_by_object = {}
    for kind, block in blocks.values():
        if kind == "33":
            owner = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", block, re.M)
            mesh = re.search(r"^  m_Mesh: .*guid: ([0-9a-f]{32})", block, re.M)
            if owner and mesh:
                mesh_by_object[owner[1]] = mesh[1]

    group_fields = {
        0: "  earlyVisual:\n    eraIndex: 0\n    renderers:\n",
        1: "  refinedRenderers:\n",
        2: "  additionalVisual:\n    eraIndex: 2\n    renderers:\n",
    }
    groups = {}
    for era, field in group_fields.items():
        rows = re.search(r"^" + re.escape(field) + r"((?: +\- \{fileID: -?\d+\}\n)+)", prefab, re.M)[1]
        renderer_ids = re.findall(r"fileID: (-?\d+)", rows)
        groups[era] = []
        for renderer_id in renderer_ids:
            kind, block = blocks[renderer_id]
            assert kind == "23", (era, renderer_id, kind)
            source = re.search(r"^  m_CorrespondingSourceObject: .*guid: ([0-9a-f]{32})", block, re.M)
            if source:
                groups[era].append(source[1])
            else:
                owner = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", block, re.M)
                groups[era].append(mesh_by_object[owner[1]])
    return groups


def record_era(index, label, source, export, unity, renderer_guids):
    paths = [ROOT / source, ROOT / export, ROOT / unity]
    asset_guid = guid(paths[-1])
    assert set(renderer_guids) == {asset_guid}, (index, unity, set(renderer_guids))
    data = {"index": index, "label": label, "source": fingerprint(paths[0]), "export": fingerprint(paths[1]),
            "unity": fingerprint(paths[2]), "unityGuid": asset_guid, "matchingPrefabRenderers": len(renderer_guids)}
    if paths[1].suffix == paths[2].suffix:
        data["exportMatchesUnity"] = data["export"]["sha256"] == data["unity"]["sha256"]
        assert data["exportMatchesUnity"], unity
    else:
        data["glbReadback"] = glb_info(paths[1])
        data["objReadback"] = obj_info(paths[2])
        assert data["glbReadback"]["triangles"] == data["objReadback"]["triangles"] == 32832
        assert data["glbReadback"]["meshCount"] == data["objReadback"]["objectCount"] == 56
    return data


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", default="HEAD")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    ROOT = Path.cwd()
    base = git("rev-parse", args.base)
    git("diff", "--exit-code", base, "--", "MuseumVR")
    prefix = "MuseumVR/Assets/Museum/Features/"
    prefab_path = ROOT / (prefix + "HelmetInspection/Prefabs/InspectableHelmet.prefab")
    scene_path = ROOT / "MuseumVR/Assets/Museum/Scenes/MuseumStart.unity"
    catalog_path = ROOT / (prefix + "HelmetEras/HelmetEraCatalogue.asset")
    prefab = prefab_path.read_text(encoding="utf-8")
    scene = scene_path.read_text(encoding="utf-8")
    prefab_guid = guid(prefab_path)
    assert re.search(r"m_SourcePrefab: .*guid: " + prefab_guid + r"\b", scene)
    assert re.search(r"^  defaultIndex: 1$", catalog_path.read_text(encoding="utf-8"), re.M)
    assert "  refinedEraIndex: 1\n  additionalVisual:\n    eraIndex: 2" in prefab
    groups = renderer_groups(prefab)
    eras = [
        record_era(0, "1839 SMG 1881-47", "artifacts/helmet-modeling/early-1881-47/build_helmet.py",
                   "artifacts/helmet-modeling/early-1881-47/delivery/early-1881-47.glb",
                   prefix + "EarlyHelmet/Models/early-1881-47-runtime.obj", groups[0]),
        record_era(1, "Mark V", "artifacts/helmet-refinement/eras/mark-v/sources/mark-v-mod1.blend",
                   "artifacts/refined-model-integration/exports/mark-v-runtime.fbx",
                   prefix + "RefinedModels/Models/mark-v-runtime.fbx", groups[1]),
        record_era(2, "SuperLite 17B", "artifacts/helmet-modeling/superlite17b-2026-10-11/revision-side-profile-v2/delivery/superlite17b.blend",
                   "artifacts/superlite17b-unity-integration/exports/superlite17b-v2-runtime.fbx",
                   prefix + "SuperLiteModels/Models/superlite17b-v2-runtime.fbx", groups[2]),
    ]
    lfs = []
    for line in git("lfs", "ls-files", "-l").splitlines():
        match = re.fullmatch(r"([0-9a-f]{64}) [*-] (.+)", line)
        assert match, line
        oid, path = match.groups()
        check = fingerprint(ROOT / path)
        assert oid == check["sha256"], path
        lfs.append(check)
    assert lfs
    prior_run = ROOT / "artifacts/helmet-interaction/qa/runs/20261018-pr67-review/positive-visitor/report.json"
    prior_process = prior_run.with_name("process.json")
    prior = json.loads(prior_run.read_text(encoding="utf-8-sig"))
    exit_state = json.loads(prior_process.read_text(encoding="utf-8-sig"))
    product_tree = git("rev-parse", f"{base}:MuseumVR")
    assert git("rev-parse", f"{prior['commit']}:MuseumVR") == product_tree
    assert prior["status"] == "passed" and all(check["passed"] for check in prior["checks"]) and not prior["errors"]
    assert exit_state["exitCode"] == 0 and not exit_state["forced"]
    output = {"base": base, "museumTree": product_tree, "method": "Working-file SHA-256 vs Git LFS OIDs; saved scene/prefab/meta, renderer-to-model references and direct GLB/OBJ parsing.",
              "limitations": "No runtime, Blender, headset, binocular, reach, performance or model historical reconstruction test was repeated.",
              "defaultEra": 1, "scene": fingerprint(scene_path), "catalog": fingerprint(catalog_path), "prefab": fingerprint(prefab_path),
              "prefabGuid": prefab_guid, "eras": eras, "lfs": {"total": len(lfs),
              "unityAssets": sum(item["path"].startswith("MuseumVR/") for item in lfs), "verified": lfs},
              "priorUnityEvidence": {"report": fingerprint(prior_run), "process": fingerprint(prior_process),
                                     "source": prior["commit"], "sameProductTree": True, "checks": len(prior["checks"]),
                                     "exitCode": exit_state["exitCode"]}}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes((json.dumps(output, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    print(f"Verified {len(lfs)} LFS working files and {sum(map(len, groups.values()))} saved renderer references; source {base[:8]}")
