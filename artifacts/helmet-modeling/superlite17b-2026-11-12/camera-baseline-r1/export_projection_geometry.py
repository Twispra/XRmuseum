"""Read the frozen v4 source and export evaluated geometry for photo matching."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = Path(__file__).resolve().parents[4]
source = root / 'artifacts/helmet-modeling/superlite17b-2026-11-12/baseline-v4/superlite17b-shell-v4.blend'
expected = '6e85f7f048b3a6b8706428854ab8934099da25ff21d427e87fde9de930b1c8a2'
assert hashlib.sha256(source.read_bytes()).hexdigest() == expected
bpy.ops.wm.open_mainfile(filepath=str(source))
inventory = json.loads((source.parent / 'readback.json').read_text(encoding='utf-8'))
names = [r['name'] for r in inventory['objects']]
arrays, parts = {}, []
depsgraph = bpy.context.evaluated_depsgraph_get()
for i, name in enumerate(names):
    obj = bpy.data.objects[name]
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    vertices = np.array([list(obj.matrix_world @ v.co) for v in mesh.vertices], dtype=np.float64)
    triangles = np.array([list(t.vertices) for t in mesh.loop_triangles], dtype=np.int32)
    arrays[f'v{i}'] = vertices
    arrays[f't{i}'] = triangles
    parts.append({'index': i, 'name': name, 'vertices': len(vertices), 'triangles': len(triangles),
                  'mesh_vertex_centroid_world': vertices.mean(axis=0).tolist(),
                  'bbox_center_world': ((vertices.min(axis=0) + vertices.max(axis=0)) / 2).tolist()})
    evaluated.to_mesh_clear()
assert len(parts) == 144
assert sum(p['triangles'] for p in parts) == 61400
out = Path(args.output)
out.mkdir(parents=True, exist_ok=True)
np.savez_compressed(out / 'projection-geometry.npz', **arrays)
(out / 'projection-parts.json').write_text(json.dumps({'source_sha256': expected, 'parts': parts,
    'centroid_definition': 'Named mesh vertex centroid; an approximate model feature, not a surveyed physical point.',
    'units': 'Existing estimated model world metres; not independently calibrated.'}, indent=2) + '\n', encoding='utf-8')
assert hashlib.sha256(source.read_bytes()).hexdigest() == expected
print('V4_PROJECTION_EXPORT_OK 144 meshes / 61400 triangles / source unchanged')
