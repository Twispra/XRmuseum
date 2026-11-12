import bpy, json, hashlib, math, sys
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
ROOT=Path(__file__).resolve().parents[4]
OUT=Path(__file__).resolve().parent
SOURCE=ROOT/'artifacts/helmet-modeling/superlite17b-2026-10-11/revision-side-profile-v2/delivery/superlite17b.blend'
EXPORT=ROOT/'artifacts/superlite17b-unity-integration/exports/superlite17b-v2-runtime.fbx'
UNITY=ROOT/'MuseumVR/Assets/Museum/Features/SuperLiteModels/Models/superlite17b-v2-runtime.fbx'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
before={p.relative_to(ROOT).as_posix():sha(p) for p in [SOURCE,EXPORT,UNITY]}
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene=bpy.context.scene
# Read the delivered model, never run its historical generator or save the .blend.
model=[o for o in scene.objects if o.type=='MESH' and not any(t in o.name.lower() for t in ['stage','ground','backdrop','floor'])]
# The delivery scene also contains a studio plane. Match the model manifest explicitly.
manifest=json.loads((SOURCE.parent/'geometry-summary.json').read_text(encoding='utf-8-sig'))
expected={o['name'] for o in manifest['objects']}
model=[o for o in scene.objects if o.type=='MESH' and o.name in expected]
assert len(model)==manifest['mesh_count']==143
for o in scene.objects:
 if o.type=='MESH': o.hide_render=o not in model
inventory=[]
for o in model:
 mesh=o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
 mesh.calc_loop_triangles()
 corners=[o.matrix_world@Vector(v) for v in o.bound_box]
 inventory.append({'name':o.name,'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'world_min_m':[round(min(v[i] for v in corners),6) for i in range(3)],'world_max_m':[round(max(v[i] for v in corners),6) for i in range(3)]})
 o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
assert sum(o['triangles'] for o in inventory)==50268
# Also inspect the delivered FBX in a separate, temporary scene; no import settings saved.

scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=24
scene.render.resolution_x=1100; scene.render.resolution_y=900; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
camera=scene.camera
assert camera is not None
camera.data.type='ORTHO'; camera.data.ortho_scale=.43
views={'rear':((0,.95,.31),(0,.025,.165)), 'side':((.95,.02,.29),(0,.005,.16)), 'rear_threequarter':((-.72,.78,.43),(0,.025,.165))}
labels={'A_hinge':'Hinge_NeckClamp_Rear','B_rear_weight':'Weights_Brass_Rear_560-005_-1','C_neck_yoke':'Neck_Yoke_520-117'}
projections={}
for name,(pos,target) in views.items():
 camera.location=pos; camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler(); scene.camera=camera
 bpy.context.view_layer.update()
 scene.render.filepath=str(OUT/(name+'.png')); bpy.ops.render.render(write_still=True)
 projections[name]={}
 for key,objname in labels.items():
  obj=bpy.data.objects.get(objname)
  if obj:
   center=sum((obj.matrix_world@Vector(v) for v in obj.bound_box),Vector())/8
   q=world_to_camera_view(scene,camera,center)
   projections[name][key]={'object':objname,'x_px':round(q.x*1100,1),'y_px':round((1-q.y)*900,1),'note':'Projected bounding-box center, not a physical attachment measurement.'}
assert all(sha(ROOT/k)==v for k,v in before.items())
# Import actual runtime FBX into a fresh scene to confirm object names and part inventory.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(EXPORT))
fbx_meshes=[o.name for o in bpy.context.scene.objects if o.type=='MESH']
fbx_parts={term:[n for n in fbx_meshes if term.lower() in n.lower()] for term in ['515-035','505-047','550-339','530-090','540-157','Hinge','560-005','Neck']}
assert len(fbx_meshes)==143
report={'base_commit':'adc06875103cc47a555dd251e3cf80d836a09558','blender':bpy.app.version_string,'operation':'Read saved v2, render temporary cameras, import delivered FBX; never save source','asset_sha256_before_after':before,'fbx_export_equals_unity':sha(EXPORT)==sha(UNITY),'mesh_count':len(model),'triangles':50268,'runtime_fbx_meshes':len(fbx_meshes),'runtime_named_part_matches':fbx_parts,'source_inventory':inventory,'annotation_projections':projections,'limits':['Blender source renders are not Unity screenshots or device evidence.','Names identify explicitly modeled parts; unmatched names alone do not prove geometric absence.','No historical dimensions, actual configuration or installation coordinates established.']}
(OUT/'model-readback.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('SUPERLITE_CONFIG_READBACK_OK source=143/50268 fbx=143 asset_hashes_unchanged=true')
