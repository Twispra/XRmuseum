"""Export the photo GLB as separate hollow-shell and detail meshes for Unity.

Run: python export_unity_section_obj.py SOURCE.glb UNITY_MODELS_DIRECTORY
Check an existing export without third-party packages:
    python export_unity_section_obj.py --validate FILE.obj [FILE.obj ...]
Only format conversion is performed; the source geometry and transforms are retained.
"""

from collections import Counter
from pathlib import Path
import sys


def validate_obj_indices(text: str) -> dict[str, int]:
    """Reject dangling vertex, UV and normal references before publishing an OBJ.

    Positive OBJ indices address the complete file; negative indices address the
    entries preceding that face. Checking vertices alone misses corrupt UVs.
    """
    lines = [line.split() for line in text.splitlines() if line.strip()]
    totals = Counter(parts[0] for parts in lines)
    seen = Counter()
    bad_faces = []
    first_error = None
    for line_number, raw in enumerate(text.splitlines(), 1):
        parts = raw.split()
        if not parts or parts[0].startswith("#"):
            continue
        kind = parts[0]
        seen[kind] += 1
        if kind != "f":
            continue
        invalid = len(parts) != 4
        for reference in parts[1:]:
            indices = reference.split("/")
            if len(indices) > 3 or not indices[0]:
                invalid = True
                continue
            for index, entry_kind in zip(indices, ("v", "vt", "vn")):
                if not index:
                    continue
                try:
                    value = int(index)
                except ValueError:
                    invalid = True
                    continue
                if value == 0 or value > totals[entry_kind] or value < -seen[entry_kind]:
                    invalid = True
                    if first_error is None:
                        first_error = f"line {line_number}: {entry_kind} index {value}, total {totals[entry_kind]}"
        if invalid:
            bad_faces.append(line_number)
    if bad_faces:
        raise ValueError(f"Invalid OBJ: {len(bad_faces)} faces; {first_error or 'non-triangle or malformed face'}")
    return {key: totals[key] for key in ("v", "vt", "vn", "f")}


def fill_missing_uvs(scene) -> int:
    """Give untextured PBR meshes a neutral UV, preserving all existing UVs.

    trimesh's scene OBJ exporter offsets all face streams by the vertex count.
    A mesh without UVs otherwise advances the v/vn offset but writes no vt rows,
    so later textured meshes reference the wrong UVs and eventually go out of
    range. These materials have no image texture: (0, 0) changes no appearance.
    """
    import numpy as np
    from trimesh.visual.material import PBRMaterial

    added = 0
    for mesh in scene.geometry.values():
        if mesh.visual.kind != "texture":
            raise ValueError("Expected the GLB's PBR material bindings, not vertex colors")
        if mesh.visual.uv is None:
            material = mesh.visual.material
            if not isinstance(material, PBRMaterial) or any(
                getattr(material, slot) is not None
                for slot in ("baseColorTexture", "metallicRoughnessTexture", "normalTexture",
                             "occlusionTexture", "emissiveTexture")
            ):
                raise ValueError("Cannot invent UVs for a material containing an image texture")
            mesh.visual.uv = np.zeros((len(mesh.vertices), 2), dtype=np.float64)
            added += len(mesh.vertices)
        if mesh.visual.uv.shape != (len(mesh.vertices), 2) or not np.isfinite(mesh.visual.uv).all():
            raise ValueError("A source mesh has invalid UV coordinates")
    return added


def main() -> None:
    if len(sys.argv) >= 3 and sys.argv[1] == "--validate":
        for filename in sys.argv[2:]:
            print(filename, validate_obj_indices(Path(filename).read_text(encoding="utf-8")))
        return
    if len(sys.argv) != 3:
        raise SystemExit("Expected SOURCE.glb UNITY_MODELS_DIRECTORY")
    import numpy as np
    import trimesh
    from trimesh.exchange.obj import export_obj

    source = Path(sys.argv[1])
    target = Path(sys.argv[2])
    if not source.is_file() or source.stat().st_size < 100_000:
        raise SystemExit("Expanded GLB is required")
    scene = trimesh.load(source, force="scene")
    if len(scene.graph.nodes_geometry) < 80:
        raise SystemExit("Unexpected photo source hierarchy")
    added_uvs = fill_missing_uvs(scene)
    shell = trimesh.Scene()
    details = trimesh.Scene()
    shell_nodes = 0
    for node in scene.graph.nodes_geometry:
        transform, geometry_name = scene.graph[node]
        output = shell if node.startswith("Shell_Hollow_WallThicknessAssumed") else details
        output.add_geometry(scene.geometry[geometry_name], node_name=node,
                            geom_name=geometry_name, transform=transform)
        shell_nodes += output is shell
    if shell_nodes != 1:
        raise SystemExit(f"Expected one hollow shell, got {shell_nodes}")
    combined = np.vstack((shell.bounds, details.bounds))
    if not np.allclose(np.vstack((combined.min(axis=0), combined.max(axis=0))), scene.bounds, atol=1e-6):
        raise SystemExit("Separated meshes changed the source bounds")

    target.mkdir(parents=True, exist_ok=True)
    _, full_aux = export_obj(scene, include_normals=True, include_texture=True, return_texture=True)
    exports = []
    for name, piece in (("shell", shell), ("details", details)):
        obj, _ = export_obj(piece, include_normals=True, include_texture=True, return_texture=True)
        validate_obj_indices(obj)
        exports.append((name, obj))
    # Validate both pieces before changing any saved model or texture.
    for name, data in full_aux.items():
        (target / name).write_bytes(data)
    for name, obj in exports:
        (target / f"photo-helmet-refined-v2-{name}.obj").write_text(obj, encoding="utf-8", newline="\n")
    print(f"UNITY_PHOTO_SPLIT source_nodes={len(scene.graph.nodes_geometry)} shell_faces={sum(len(m.faces) for m in shell.geometry.values())} details_faces={sum(len(m.faces) for m in details.geometry.values())} neutral_uvs={added_uvs} bounds=unchanged")


if __name__ == "__main__":
    main()
