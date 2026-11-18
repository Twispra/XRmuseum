"""Matched before/after source-model views; no manufacturer media."""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import bpy
from mathutils import Vector

ap=argparse.ArgumentParser();ap.add_argument('--study',required=True);ap.add_argument('--output',required=True);ap.add_argument('--only-view')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);here=Path(__file__).resolve().parent
base=here/'baseline-v4/superlite17b-shell-v4.blend';study=Path(a.study).resolve();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
baseline_names={r['name'] for r in json.loads((base.parent/'readback.json').read_text())['objects']}
views={'rear':((.43,.90,.38),(0,.04,.18),.40),'rear_detail':((.14,.70,.28),(0,.145,.209),.145),'side_detail':((.65,.40,.26),(0,.15,.205),.12)}
if a.only_view:views={a.only_view:views[a.only_view]}
report=[]
for key,path in [('before',base),('after',study)]:
    sha=hashlib.sha256(path.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(path));scene=bpy.context.scene
    for o in scene.objects:
        if o.type=='MESH':o.hide_render=o.name not in baseline_names and not any(c.name.startswith('Rear_Connection_Study') for c in o.users_collection)
    camera=scene.camera;camera.data.type='ORTHO'
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=20;scene.cycles.use_denoising=True
    scene.render.resolution_x=1000;scene.render.resolution_y=900;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
    for view,(pos,target,scale) in views.items():
        camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale
        scene.render.filepath=str(out/(key+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
        report.append({'version':key,'view':view,'source_sha256':sha,'camera_position':pos,'target':target,'ortho_scale':scale})
    assert hashlib.sha256(path.read_bytes()).hexdigest()==sha
(out/('render-manifest-'+a.only_view+'.json' if a.only_view else 'render-manifest.json')).write_text(json.dumps({'views':report,'geometry_saved_during_render':False,'limits':'Matched source renders only, not real-object photographic alignment or Unity/device evidence.'},indent=2)+'\n',encoding='utf-8')
print('REAR_STUDY_MATCHED_VIEWS_COMPLETE sources unchanged')
