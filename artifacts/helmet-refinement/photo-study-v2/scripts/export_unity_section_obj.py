"""Export the photo GLB as separate hollow-shell and detail meshes for Unity.

Run: python export_unity_section_obj.py SOURCE.glb UNITY_MODELS_DIRECTORY
Only format conversion is performed; the source geometry and transforms are retained.
"""

from pathlib import Path
import sys

import numpy as np
import trimesh
from trimesh.exchange.obj import export_obj


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("Expected SOURCE.glb UNITY_MODELS_DIRECTORY")
    source = Path(sys.argv[1])
    target = Path(sys.argv[2])
    if not source.is_file() or source.stat().st_size < 100_000:
        raise SystemExit("Expanded GLB is required")
    scene = trimesh.load(source, force="scene")
    if len(scene.graph.nodes_geometry) < 80:
        raise SystemExit("Unexpected photo source hierarchy")
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
    for name, data in full_aux.items():
        (target / name).write_bytes(data)
    for name, piece in (("shell", shell), ("details", details)):
        obj, _ = export_obj(piece, include_normals=True, include_texture=True, return_texture=True)
        (target / f"photo-helmet-refined-v2-{name}.obj").write_text(obj)
    print(f"UNITY_PHOTO_SPLIT source_nodes={len(scene.graph.nodes_geometry)} shell_faces={sum(len(m.faces) for m in shell.geometry.values())} details_faces={sum(len(m.faces) for m in details.geometry.values())} bounds=unchanged")


if __name__ == "__main__":
    main()
