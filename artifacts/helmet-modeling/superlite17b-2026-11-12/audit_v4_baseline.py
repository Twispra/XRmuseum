import bpy,json,hashlib,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
BASE=Path(__file__).resolve().parent/'baseline-v4'
FILE=BASE/'superlite17b-shell-v4.blend'
EXPECTED='6e85f7f048b3a6b8706428854ab8934099da25ff21d427e87fde9de930b1c8a2'
assert hashlib.sha256(FILE.read_bytes()).hexdigest()==EXPECTED
bpy.ops.wm.open_mainfile(filepath=str(FILE))
model=[o for o in bpy.context.scene.objects if o.type=='MESH' and not any(s in o.name.lower() for s in ['studio','stage','ground','floor','backdrop'])]
rows=[]
def geometry_hash(mesh):
 h=hashlib.sha256()
 for v in mesh.vertices:h.update(struct.pack('<3f',*v.co))
 for p in mesh.polygons:
  h.update(struct.pack('<I',len(p.vertices)));h.update(struct.pack('<'+'I'*len(p.vertices),*p.vertices))
 return h.hexdigest()
for o in model:
 o.data.calc_loop_triangles()
 evaluated=o.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
 mesh.calc_loop_triangles()
 rows.append({'name':o.name,'vertices':len(o.data.vertices),'triangles':len(o.data.loop_triangles),'mesh_coordinates_topology_sha256':geometry_hash(o.data),'evaluated_coordinates_topology_sha256':geometry_hash(mesh),'evaluated_triangles':len(mesh.loop_triangles),'modifiers':[{'name':m.name,'type':m.type,'show_viewport':m.show_viewport,'show_render':m.show_render} for m in o.modifiers],'shape_keys':[{'name':k.name,'value':k.value} for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [],'hide_render':o.hide_render,'matrix_world':[[round(x,9) for x in row] for row in o.matrix_world],'material_names':[s.material.name if s.material else None for s in o.material_slots]})
 evaluated.to_mesh_clear()
assert len(rows)==144,len(rows)
assert sum(r['triangles'] for r in rows)==61400
assert hashlib.sha256(FILE.read_bytes()).hexdigest()==EXPECTED
result={'blender':bpy.app.version_string,'source_sha256':EXPECTED,'read_only':True,'meshes':len(rows),'triangles':sum(r['triangles'] for r in rows),'evaluated_triangles':sum(r['evaluated_triangles'] for r in rows),'frame':bpy.context.scene.frame_current,'geometry_modified':False,'objects':rows,'limits':'Raw/evaluated geometry at the saved frame, modifier visibility and shape-key values are recorded. Material names do not prove unchanged material parameters. Not a rerun of the historical 178 checks or measured-object accuracy / Unity/device validation.'}
(BASE/'readback.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('V4_BASELINE_READBACK_OK 144 meshes / 61400 triangles; saved source unchanged')
