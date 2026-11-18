"""Matched r1/r2 views and opaque masks; no source or manufacturer media saved."""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy
from mathutils import Matrix,Vector

ap=argparse.ArgumentParser();ap.add_argument('--candidate',required=True);ap.add_argument('--output',required=True);ap.add_argument('--mode',choices=['masks','beauty'],required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);here=Path(__file__).resolve().parent;base=here.parent/'sources/superlite17b-rear-study-r1.blend';candidate=Path(a.candidate).resolve();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
fit=json.loads((here.parent/'camera-baseline-r1/results/camera-fit.json').read_text(encoding='utf-8'));inventory=json.loads((here.parent/'baseline-v4/readback.json').read_text(encoding='utf-8'))
names={r['name'] for r in inventory['objects']}-{'Weights_Brass_Rear_560-005_-1','Weights_Brass_Rear_560-005_1','Hinge_NeckClamp_Rear'}
report=[]
for version,path in [('before',base),('after',candidate)]:
 sha=hashlib.sha256(path.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(path));scene=bpy.context.scene;camera=scene.camera
 model=[o for o in scene.objects if o.type=='MESH' and (o.name in names or any(c.name.startswith('Rear_Connection_Study') for c in o.users_collection))];assert len(model)==146
 for o in scene.objects:
  if o.type=='MESH':o.hide_render=o not in model
 scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1 if a.mode=='masks' else 16;scene.cycles.use_denoising=a.mode=='beauty'
 scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=a.mode=='masks'
 if a.mode=='masks':
  palette={'shell':(0,1,0),'frame':(0,0,1),'glass':(.5,.5,.5),'other':(.05,.05,.05)};mats={}
  for name,color in palette.items():
   m=bpy.data.materials.new('Mask_'+name);m.use_nodes=True;m.node_tree.nodes.clear();e=m.node_tree.nodes.new('ShaderNodeEmission');e.inputs['Color'].default_value=(*color,1);o=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(e.outputs[0],o.inputs['Surface']);mats[name]=m
  for o in model:
   key='shell' if o.name.startswith('Shell_Yellow') else 'frame' if o.name=='Ring_PortChrome_560-070' else 'glass' if o.name=='Glass_Port_520-004' else 'other'
   o.data.materials.clear();o.data.materials.append(mats[key])
  scene.view_settings.view_transform='Raw';scene.view_settings.exposure=0;scene.view_settings.gamma=1;scene.use_nodes=False
 for key,data in fit['views'].items():
  c=data['primary'];W,H=c['image_size'];p=c['parameters'];f=c['focal_px'];right,up,back=[Vector(v) for v in c['camera_basis_right_up_back']]
  camera.data.type='PERSP';camera.data.sensor_fit='HORIZONTAL';camera.data.sensor_width=36.;camera.data.lens=f*36/W
  camera.location=Vector(c['target_world'])+back*math.exp(p[3]);camera.rotation_euler=Matrix((right,up,back)).transposed().to_euler();camera.data.shift_x=(W*.5-p[4])/W;camera.data.shift_y=(p[5]-H*.5)/W
  camera.data.clip_start=.001;camera.data.clip_end=100
  factor=1. if a.mode=='masks' else .6
  scene.render.resolution_x=round(W*factor);scene.render.resolution_y=round(H*factor)
  scene.render.filepath=str(out/(version+'-'+key+'-'+a.mode+'.png'));bpy.ops.render.render(write_still=True)
  report.append({'version':version,'view':key,'mode':a.mode,'source_sha256':sha,'same_frozen_camera':True,'image_size':[scene.render.resolution_x,scene.render.resolution_y]})
 if a.mode=='beauty':
  camera.data.type='ORTHO';camera.data.shift_x=camera.data.shift_y=0;camera.data.ortho_scale=.39
  camera.location=(-1.,.05,.34);target=Vector((0,.015,.17));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
  scene.render.resolution_x=1100;scene.render.resolution_y=900;scene.render.filepath=str(out/(version+'-side-ortho.png'));bpy.ops.render.render(write_still=True)
  report.append({'version':version,'view':'side-ortho','mode':a.mode,'source_sha256':sha,'camera_position':list(camera.location),'target':list(target),'ortho_scale':.39})
 assert hashlib.sha256(path.read_bytes()).hexdigest()==sha
(out/('render-manifest-'+a.mode+'.json')).write_text(json.dumps({'renders':report,'source_saved_during_render':False,'limits':'Fixed virtual cameras; no physical-camera calibration or photometric comparison implied.'},indent=2)+'\n',encoding='utf-8')
print('SHELL_COMPARISON_RENDER_OK '+a.mode+' source files unchanged')
