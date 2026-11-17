"""Read the complete saved backup in a background Blender without saving it."""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import bpy

parser = argparse.ArgumentParser()
parser.add_argument('--repo', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = args.repo / 'artifacts/helmet-modeling/superlite17b-2026-11-12'
paths = [root / 'sources/superlite17b-shell-study-r2.blend',
         root / 'shell-rebuild-r3/backups/backup_r1_current.blend',
         root / 'shell-rebuild-r3/work/work_r3.blend']
expected = 'a595dc7f9979d21c95419fcc80fb2619099afcaf2e9735e8676c47037d1aa894'
assert all(hashlib.sha256(p.read_bytes()).hexdigest() == expected for p in paths)
bpy.ops.wm.open_mainfile(filepath=str(paths[1]))
scene = bpy.context.scene
names = {row['name'] for row in json.loads((root / 'baseline-v4/readback.json').read_text())['objects']}
names -= {'Weights_Brass_Rear_560-005_-1', 'Weights_Brass_Rear_560-005_1', 'Hinge_NeckClamp_Rear'}
names |= {obj.name for obj in scene.objects if any(c.name.startswith('Rear_Connection_Study') for c in obj.users_collection)}
model = [obj for obj in scene.objects if obj.type == 'MESH' and obj.name in names]
for obj in model:
    obj.data.calc_loop_triangles()
result = {'blender': bpy.app.version_string, 'model_meshes': len(model), 'scene_objects': len(scene.objects),
          'model_triangles': sum(len(obj.data.loop_triangles) for obj in model), 'materials': len(bpy.data.materials),
          'cameras': [obj.name for obj in scene.objects if obj.type == 'CAMERA'],
          'lights': [obj.name for obj in scene.objects if obj.type == 'LIGHT'],
          'source_backup_work_sha256': expected, 'saved_files_unchanged': True,
          'scope': 'Saved-file inventory; no render, reconstruction, physical accuracy or equipment acceptance.'}
assert result['model_meshes'] == 146 and result['materials'] == 12
assert len(result['cameras']) == 1 and len(result['lights']) == 4 and result['scene_objects'] == 152
assert all(hashlib.sha256(p.read_bytes()).hexdigest() == expected for p in paths)
args.output.parent.mkdir(parents=True, exist_ok=True)
with args.output.open('x', encoding='utf-8') as stream:
    stream.write(json.dumps(result, indent=2) + '\n')
print('R3_INVENTORY_OK 146 model meshes; 152 scene objects; files unchanged')
