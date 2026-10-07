"""Read editable source and both exported formats in Blender. Write a fresh QA report.
Run: blender -b --python verify_equipment.py -- --root <asset-directory>
"""
import bpy,sys,math,json,hashlib,bmesh
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];root=Path(args[args.index('--root')+1]).resolve()
names={'Pump_ROOT':'mark-iii-pump','SurfaceTelephone_ROOT':'surface-telephone','DiverTelephone_ROOT':'diver-telephone'}
def bounds(objects):
 bpy.context.view_layer.update();v=[o.matrix_world@x.co for o in objects if o.type=='MESH' for x in o.data.vertices]
 return [[min(x[i] for x in v) for i in range(3)],[max(x[i] for x in v) for i in range(3)]]
def stats(objects):
 meshes=[o for o in objects if o.type=='MESH'];return {'meshes':len(meshes),'triangles':sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in meshes),'bounds_m':bounds(meshes)}
bpy.ops.wm.open_mainfile(filepath=str(root/'sources/equipment.blend'));scene=bpy.context.scene
for n in names:bpy.data.objects[n].location=(0,0,0)
scene.frame_set(1);baseline={n:stats(bpy.data.objects[n].children_recursive) for n in names}
assert all(im.packed_file for im in bpy.data.images if im.name.startswith('Original'))
trace=[];link_errors=[]
sample_seconds=[0,.5,1,1.5,2]
tracked={'Pump_ROOT':['PumpCrankPivot','PumpSlider0','PumpSlider1','PumpSling0','PumpSling1','HandQuillPivotL','HandQuillPivotR'],'SurfaceTelephone_ROOT':['SurfaceDiaphragm'],'DiverTelephone_ROOT':['DiverDiaphragm']}
pose_baseline={}
for sec in sample_seconds:
 scene.frame_set(round(1+sec*scene.render.fps));bpy.context.view_layer.update()
 pose_baseline[sec]={n:[v for row in bpy.data.objects[n].matrix_world for v in row] for nameset in tracked.values() for n in nameset}
for f in range(1,62):
 scene.frame_set(f);bpy.context.view_layer.update();t=(f-1)/60*math.tau
 row={'frame':f,'wheel_rad':bpy.data.objects['PumpCrankPivot'].rotation_euler.x}
 for i in range(2):
  s=bpy.data.objects['PumpSlider'+str(i)];link=bpy.data.objects['PumpSling'+str(i)];row['slider_z_'+str(i)]=s.location.z
  start=Vector(([-.12,.12][i],.08+.092075*math.cos(t+i*math.pi/2),.76+.092075*math.sin(t+i*math.pi/2)))
  assert abs((start-s.location).length-.29)<1e-5
  endpoint=link.matrix_world@Vector((0,0,.145));link_errors.append((endpoint-start).length)
 trace.append(row)
for i in range(2):assert abs(max(r['slider_z_'+str(i)] for r in trace)-min(r['slider_z_'+str(i)] for r in trace)-.18415)<1e-6
assert max(link_errors)<1e-5
assert abs(trace[-1]['wheel_rad']-math.tau)<1e-5
imports=[]
for name,stem in names.items():
 for ext in ['glb','fbx']:
  bpy.ops.wm.read_factory_settings(use_empty=True)
  p=root/'exports'/(stem+'.'+ext)
  if ext=='glb':
   bpy.ops.import_scene.gltf(filepath=str(p))
   # The GLB has one clip per animated part. Blender mutes all but its first
   # imported action by default. Enable every part together, as the viewer does.
   for o in bpy.context.scene.objects:
    if o.animation_data:
     o.animation_data.action=None
     for track in o.animation_data.nla_tracks:track.mute=False
  else:bpy.ops.import_scene.fbx(filepath=str(p))
  bpy.context.scene.frame_set(1);st=stats(bpy.context.scene.objects)
  assert st['triangles']==baseline[name]['triangles'],(stem,ext,st,baseline[name])
  error=max(abs(x-y) for a,b in zip(st['bounds_m'],baseline[name]['bounds_m']) for x,y in zip(a,b));assert error<.0002,(stem,ext,error)
  clips=len(bpy.data.actions);assert clips>=1
  pose_errors=[];frame_origin=min(a.frame_range[0] for a in bpy.data.actions)
  for sec in sample_seconds:
   bpy.context.scene.frame_set(round(frame_origin+sec*bpy.context.scene.render.fps));bpy.context.view_layer.update()
   for n in tracked[name]:
    o=bpy.data.objects[n];actual=[v for row in o.matrix_world for v in row];pose_errors.append(max(abs(x-y) for x,y in zip(actual,pose_baseline[sec][n])))
  assert max(pose_errors)<.002,(stem,ext,'pose mismatch',max(pose_errors))
  imports.append({'file':p.name,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),**st,'bounds_max_error_m':error,'actions_read':clips,'sample_seconds':sample_seconds,'import_frame_origin':frame_origin,'max_pose_matrix_error':max(pose_errors)})
report={'status':'PASS','blender':bpy.app.version_string,'source_sha256':hashlib.sha256((root/'sources/equipment.blend').read_bytes()).hexdigest(),'source':baseline,'imports':imports,'pump_trace':trace,'max_link_endpoint_error_m':max(link_errors),'checks':['61-frame source loop; exact stroke and connecting-rod endpoints','all original images packed','GLB and FBX neutral geometry readback triangle/bounds equivalence','five-time-point animated node matrices agree with source in both exports'],'limitations':['No Unity or headset import/run tested','Gauge and diaphragm motions are illustrative','Export poses sampled at five times, not continuous exhaustive verification']}
(root/'qa/export-readback.json').write_text(json.dumps(report,indent=2),encoding='utf-8',newline='\n');print('EQUIPMENT_READBACK_PASS')
