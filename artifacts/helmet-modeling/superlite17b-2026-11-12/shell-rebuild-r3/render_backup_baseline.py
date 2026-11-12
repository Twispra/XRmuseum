"""Step 0: inspect complete backup and render frozen baseline views, never save it."""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy
from mathutils import Matrix,Vector

ap=argparse.ArgumentParser();ap.add_argument('--output',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
here=Path(__file__).resolve().parent;source=here/'backups/backup_r1_current.blend';work=here/'work/work_r3.blend';out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
expected='a595dc7f9979d21c95419fcc80fb2619099afcaf2e9735e8676c47037d1aa894'
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected and hashlib.sha256(work.read_bytes()).hexdigest()==expected
bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
inventory=json.loads((here.parent/'baseline-v4/readback.json').read_text(encoding='utf-8'));names={r['name'] for r in inventory['objects']}-{'Weights_Brass_Rear_560-005_-1','Weights_Brass_Rear_560-005_1','Hinge_NeckClamp_Rear'}
names|={o.name for o in scene.objects if any(c.name.startswith('Rear_Connection_Study') for c in o.users_collection)}
model=[o for o in scene.objects if o.type=='MESH' and o.name in names];assert len(model)==146
datablocks={'model_mesh_objects':len(model),'all_objects':len(scene.objects),'materials':len(bpy.data.materials),'cameras':[o.name for o in scene.objects if o.type=='CAMERA'],'lights':[{'name':o.name,'type':o.data.type,'energy':o.data.energy} for o in scene.objects if o.type=='LIGHT'],'scene_render_engine_saved':scene.render.engine,'camera_saved':scene.camera.name if scene.camera else None}
fit=json.loads((here.parent/'camera-baseline-r1/results/camera-fit.json').read_text(encoding='utf-8'));camera=scene.camera
for obj in scene.objects:
 if obj.type=='MESH':obj.hide_render=obj not in model
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=14;scene.cycles.use_denoising=True
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=False;scene.render.resolution_percentage=100
views={}
def set_view(key):
 if key in ['side','34']:
  c=fit['views']['side' if key=='side' else 'front']['primary'];W,H=c['image_size'];p=c['parameters'];f=c['focal_px'];right,up,back=[Vector(v) for v in c['camera_basis_right_up_back']]
  camera.data.type='PERSP';camera.data.sensor_fit='HORIZONTAL';camera.data.sensor_width=36.;camera.data.lens=f*36/W;camera.data.shift_x=(W*.5-p[4])/W;camera.data.shift_y=(p[5]-H*.5)/W
  camera.location=Vector(c['target_world'])+back*math.exp(p[3]);camera.rotation_euler=Matrix((right,up,back)).transposed().to_euler()
  views[key]={'camera_kind':'Existing fixed virtual pose matched to unchanged accessories/frame; uncertain physical camera.','configuration':c,'reference':'B' if key=='side' else 'A'}
 else:
  W,H=1200,1200;camera.data.type='ORTHO';camera.data.shift_x=camera.data.shift_y=0;camera.data.ortho_scale=.39
  camera.location=(0,-1.,.19);target=Vector((0,.015,.18));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
  views[key]={'camera_kind':'Orthographic model diagnostic front, no real front photograph supplied.','position':list(camera.location),'target':list(target),'ortho_scale':.39,'reference':None}
 camera.data.clip_start=.001;camera.data.clip_end=100;scene.render.resolution_x=W;scene.render.resolution_y=H;bpy.context.view_layer.update()
for key in ['side','34','front']:
 set_view(key);scene.render.filepath=str(out/('base_'+key+'.png'));bpy.ops.render.render(write_still=True)
# Exact geometry coverage: mask full shell separately from actually visible shell.
materials={}
for key,color in [('shell',(1.,1.,1.)),('occluder',(0.,0.,0.))]:
 m=bpy.data.materials.new('R3Coverage_'+key);m.use_nodes=True;m.node_tree.nodes.clear();e=m.node_tree.nodes.new('ShaderNodeEmission');e.inputs['Color'].default_value=(*color,1);o=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(e.outputs[0],o.inputs['Surface']);materials[key]=m
for obj in model:obj.data.materials.clear();obj.data.materials.append(materials['shell' if obj.name=='Shell_Yellow_520-065_ESTIMATED' else 'occluder'])
scene.view_settings.view_transform='Raw';scene.view_settings.exposure=0;scene.view_settings.gamma=1;scene.use_nodes=False
scene.render.film_transparent=True;scene.cycles.samples=1;scene.cycles.use_denoising=False
for key in ['side','34','front']:
 set_view(key)
 for coverage in ['visible','full']:
  for obj in model:obj.hide_render=coverage=='full' and obj.name!='Shell_Yellow_520-065_ESTIMATED'
  scene.render.filepath=str(out/('base_'+key+'_shell_'+coverage+'.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected and hashlib.sha256(work.read_bytes()).hexdigest()==expected
(out/'baseline-cameras-and-inventory.json').write_text(json.dumps({'backup_sha256':expected,'backup_unchanged':True,'work_unchanged':True,'complete_backup_inventory':datablocks,'fixed_views':views,'limits':'Baseline poses are estimates; no true front photo supplied. Full shell mask includes model-hidden surfaces while reference visible mask does not; they must not be compared as identical definitions.'},indent=2)+'\n',encoding='utf-8')
print('R3_BACKUP_BASELINE_RENDER_COMPLETE 146 model parts; full backup and work unchanged')
