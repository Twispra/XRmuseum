"""Fixed baseline cameras, real new file and exclusive output directory."""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy
from mathutils import Matrix,Vector

ap=argparse.ArgumentParser();ap.add_argument('--model',required=True);ap.add_argument('--output',required=True);ap.add_argument('--mode',choices=['masks','beauty'],required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
here=Path(__file__).resolve().parent;root=here.parents[3];source=Path(a.model).resolve();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=False)
frozen=json.loads((root/'artifacts/helmet-modeling/superlite17b-2026-11-12/shell-rebuild-r3/baseline/baseline-cameras-and-inventory.json').read_text(encoding='utf-8'))
sha=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene;camera=scene.camera
model=[o for o in scene.objects if o.type=='MESH' and not any(w in o.name.lower() for w in ['studio','stage','ground','backdrop'])]
assert len(model)==146
for o in scene.objects:
 if o.type=='MESH':o.hide_render=o not in model
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1 if a.mode=='masks' else 12;scene.cycles.use_denoising=a.mode=='beauty';scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=a.mode=='masks'
if a.mode=='masks':
 mats={}
 for key,color in [('shell',(1,1,1)),('other',(0,0,0))]:
  m=bpy.data.materials.new('R3Mask_'+key);m.use_nodes=True;m.node_tree.nodes.clear();e=m.node_tree.nodes.new('ShaderNodeEmission');e.inputs['Color'].default_value=(*color,1);o=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(e.outputs[0],o.inputs['Surface']);mats[key]=m
 for o in model:o.data.materials.clear();o.data.materials.append(mats['shell' if o.name=='Shell_Yellow_520-065_ESTIMATED' else 'other'])
 scene.view_settings.view_transform='Raw';scene.view_settings.exposure=0;scene.view_settings.gamma=1;scene.use_nodes=False
manifest=[]
for key,view in frozen['fixed_views'].items():
 if key in ['side','34']:
  c=view['configuration'];W,H=c['image_size'];p=c['parameters'];f=c['focal_px'];right,up,back=[Vector(v) for v in c['camera_basis_right_up_back']]
  camera.data.type='PERSP';camera.data.sensor_fit='HORIZONTAL';camera.data.sensor_width=36.;camera.data.lens=f*36/W;camera.data.shift_x=(W*.5-p[4])/W;camera.data.shift_y=(p[5]-H*.5)/W
  camera.location=Vector(c['target_world'])+back*math.exp(p[3]);camera.rotation_euler=Matrix((right,up,back)).transposed().to_euler()
 else:
  W,H=1200,1200;camera.data.type='ORTHO';camera.data.shift_x=camera.data.shift_y=0;camera.data.ortho_scale=view['ortho_scale'];camera.location=view['position'];camera.rotation_euler=(Vector(view['target'])-camera.location).to_track_quat('-Z','Y').to_euler()
 camera.data.clip_start=.001;camera.data.clip_end=100;scene.render.resolution_x=W;scene.render.resolution_y=H
 coverages=['visible','full'] if a.mode=='masks' else ['beauty']
 for coverage in coverages:
  for o in model:o.hide_render=coverage=='full' and o.name!='Shell_Yellow_520-065_ESTIMATED'
  scene.render.filepath=str(out/(key+'-'+coverage+'.png'));bpy.ops.render.render(write_still=True)
  manifest.append({'view':key,'coverage':coverage,'model_sha256':sha,'same_baseline_camera':True,'pixels':[W,H]})
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
(out/'manifest.json').write_text(json.dumps({'renders':manifest,'source_unchanged':True,'no_real_frontal_reference':True},indent=2)+'\n',encoding='utf-8')
print('R3_ITERATION_RENDERED '+a.mode+' unchanged file')
