"""Render opaque geometry-coverage masks at fitted cameras without saving .blend."""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from bpy_extras.object_utils import world_to_camera_view

ap = argparse.ArgumentParser()
ap.add_argument('--cameras', required=True)
ap.add_argument('--output', required=True)
args = ap.parse_args(sys.argv[sys.argv.index('--')+1:])
root = Path(__file__).resolve().parents[4]
source = root/'artifacts/helmet-modeling/superlite17b-2026-11-12/baseline-v4/superlite17b-shell-v4.blend'
expected = '6e85f7f048b3a6b8706428854ab8934099da25ff21d427e87fde9de930b1c8a2'
assert hashlib.sha256(source.read_bytes()).hexdigest() == expected
fits = json.loads(Path(args.cameras).read_text())
bpy.ops.wm.open_mainfile(filepath=str(source))
scene = bpy.context.scene
inventory = json.loads((source.parent/'readback.json').read_text())
names = {r['name'] for r in inventory['objects']}
palette = {'shell': (0.,1.,0.), 'frame': (0.,0.,1.), 'glass': (.5,.5,.5),
           'rear_weight': (1.,0.,0.), 'handle': (1.,1.,0.), 'neck': (0.,1.,1.), 'other': (.05,.05,.05)}
def group(name):
    if name.startswith('Shell_Yellow'): return 'shell'
    if name == 'Ring_PortChrome_560-070': return 'frame'
    if name == 'Glass_Port_520-004': return 'glass'
    if name.startswith('Weights_Brass_Rear'): return 'rear_weight'
    if name.startswith('Handle_'): return 'handle'
    if name.startswith(('Neck','Bottom_O_Ring')): return 'neck'
    return 'other'
materials = {}
for name, color in palette.items():
    m = bpy.data.materials.new('Coverage_'+name); m.use_nodes = True
    m.node_tree.nodes.clear()
    emission = m.node_tree.nodes.new('ShaderNodeEmission'); emission.inputs['Color'].default_value = (*color,1.)
    output = m.node_tree.nodes.new('ShaderNodeOutputMaterial')
    m.node_tree.links.new(emission.outputs['Emission'], output.inputs['Surface'])
    materials[name] = m
for obj in scene.objects:
    if obj.type != 'MESH': continue
    obj.hide_render = obj.name not in names
    if obj.name in names:
        obj.data.materials.clear(); obj.data.materials.append(materials[group(obj.name)])
scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'; scene.cycles.samples = 1
scene.cycles.use_denoising = False
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'; scene.render.image_settings.color_mode = 'RGBA'
scene.render.resolution_percentage = 100
scene.render.pixel_aspect_x = scene.render.pixel_aspect_y = 1.
scene.view_settings.view_transform = 'Raw'; scene.view_settings.exposure = 0; scene.view_settings.gamma = 1
scene.use_nodes = False
camera = scene.camera
camera.data.type = 'PERSP'; camera.data.sensor_fit = 'HORIZONTAL'; camera.data.sensor_width = 36.
out = Path(args.output).resolve(); out.mkdir(parents=True, exist_ok=True)
checks = []
for key, data in fits['views'].items():
    c = data['primary']; width, height = c['image_size']; p = c['parameters']; focal = c['focal_px']
    right, up, back = [Vector(v) for v in c['camera_basis_right_up_back']]
    target = Vector(c['target_world']); distance = math.exp(p[3])
    camera.location = target + back*distance
    camera.rotation_euler = Matrix((right,up,back)).transposed().to_euler()
    camera.data.lens = focal*36./width
    camera.data.shift_x = (width*.5-p[4])/width
    camera.data.shift_y = (p[5]-height*.5)/width
    camera.data.clip_start = .001; camera.data.clip_end = 100
    scene.render.resolution_x = width; scene.render.resolution_y = height
    bpy.context.view_layer.update()
    errors = []
    for point in [target, Vector((-.1,-.12,.24)), Vector((.08,.08,.12))]:
        q = point-target; depth = distance-q.dot(back)
        expected_xy = (p[4]+focal*q.dot(right)/depth, p[5]-focal*q.dot(up)/depth)
        uv = world_to_camera_view(scene,camera,point)
        actual = (uv.x*width, (1-uv.y)*height)
        errors.append(math.dist(expected_xy,actual))
    assert max(errors) < .002, (key,errors)
    scene.render.filepath = str(out/(key+'-coverage.png'))
    bpy.ops.render.render(write_still=True)
    checks.append({'view':key,'projection_check_errors_px':errors,'virtual_lens_mm_36mm_sensor':camera.data.lens,
                   'image_size':[width,height],'camera_is_not_recovered_physical_intrinsics':True})
assert hashlib.sha256(source.read_bytes()).hexdigest() == expected
(out/'render-checks.json').write_text(json.dumps({'source_sha256':expected,'palette_linear_rgb':palette,
    'views':checks,'semantics':'Opaque geometry coverage: glass temporarily opaque, flat emission IDs, no photometric or transparency comparison. Source not saved.',
    'source_unchanged':True},indent=2)+'\n',encoding='utf-8')
print('V4_COVERAGE_RENDER_OK; numerical and Blender projections agree; source unchanged')
