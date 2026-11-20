"""Saved-file inventory, explicit refit scope, new-surface defects, no blanket pass."""
import argparse,hashlib,json,math,struct,sys
from pathlib import Path
import bpy,bmesh
from mathutils.bvhtree import BVHTree
ALLOW=['Ring_PortChrome_560-070','Glass_Port_520-004','ViewPort_Seal_510-260','Handle_Brass_560-014','Handle_FrontFoot','Handle_RearFoot','Bottom_O_Ring_510-446_ESTIMATED','NeckClamp_505-055','NeckDam_Neoprene_510-533','Neck_Yoke_520-117','SideBlock_MountingFoot','RearWeight_560-005_Continuous_RESEARCH']

ap=argparse.ArgumentParser();ap.add_argument('--model',required=True);ap.add_argument('--output',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
here=Path(__file__).resolve().parent;root=here.parents[3];base=root/'artifacts/helmet-modeling/superlite17b-2026-11-12/shell-rebuild-r3/backups/backup_r1_current.blend';model=Path(a.model).resolve();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=False)
def fingerprint(o):
 h=hashlib.sha256()
 for v in o.data.vertices:h.update(struct.pack('<3f',*v.co))
 for p in o.data.polygons:h.update(str((tuple(p.vertices),p.material_index,p.use_smooth)).encode())
 for row in o.matrix_world:h.update(struct.pack('<4f',*row))
 for layer in o.data.uv_layers:
  for q in layer.data:h.update(struct.pack('<2f',*q.uv))
 for m in o.data.materials:
  if not m:continue
  h.update((m.name+str(tuple(m.diffuse_color))).encode())
  if m.use_nodes:
   for node in m.node_tree.nodes:
    h.update((node.name+node.bl_idname).encode())
    for s in node.inputs:
     if hasattr(s,'default_value'):h.update(str(s.default_value[:] if hasattr(s.default_value,'__len__') and not isinstance(s.default_value,str) else s.default_value).encode())
   for link in m.node_tree.links:h.update(str((link.from_node.name,link.from_socket.identifier,link.to_node.name,link.to_socket.identifier)).encode())
 return h.hexdigest()
def names():
 old=json.loads((root/'artifacts/helmet-modeling/superlite17b-2026-11-12/baseline-v4/readback.json').read_text(encoding='utf-8'))
 n={r['name'] for r in old['objects']}-{'Weights_Brass_Rear_560-005_-1','Weights_Brass_Rear_560-005_1','Hinge_NeckClamp_Rear'}
 n|={o.name for o in bpy.context.scene.objects if any(c.name.startswith('Rear_Connection_Study') for c in o.users_collection)};return n
def geometry(o):
 o.data.calc_loop_triangles();v=[o.matrix_world@x.co for x in o.data.vertices];t=[tuple(f.vertices) for f in o.data.loop_triangles];return v,t,BVHTree.FromPolygons(v,t,all_triangles=True)
bh=hashlib.sha256(base.read_bytes()).hexdigest();mh=hashlib.sha256(model.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(base));n=names();assert len(n)==146;before={k:fingerprint(bpy.data.objects[k]) for k in n}
bpy.ops.wm.open_mainfile(filepath=str(model));assert names()==n
changed=sorted(k for k in n if fingerprint(bpy.data.objects[k])!=before[k]);unexpected=set(changed)-set(ALLOW)-{'Shell_Yellow_520-065_ESTIMATED'};assert not unexpected
meshreports={};trees={};totaltri=0
for k in sorted(n):
 o=bpy.data.objects[k];v,t,b=geometry(o);trees[k]=b;totaltri+=len(t)
 if k not in changed:continue
 bm=bmesh.new();bm.from_mesh(o.data);nonmanifold=sum(not e.is_manifold for e in bm.edges);components=0;todo=set(bm.verts)
 while todo:
  components+=1;stack=[todo.pop()]
  while stack:
   x=stack.pop()
   for e in x.link_edges:
    q=e.other_vert(x)
    if q in todo:todo.remove(q);stack.append(q)
 bm.free();pairs={(min(i,j),max(i,j)) for i,j in b.overlap(b) if i!=j and not set(t[i])&set(t[j])}
 meshreports[k]={'vertices':len(v),'triangles':len(t),'nonmanifold_edges':nonmanifold,'connected_components':components,'zero_area_triangles':sum(f.area<1e-12 for f in o.data.loop_triangles),'nonadjacent_surface_overlap_candidates':len(pairs),'finite_coordinates':all(math.isfinite(c) for p in v for c in p),'uv_layers':len(o.data.uv_layers)}
shell=trees['Shell_Yellow_520-065_ESTIMATED'];contacts=[]
for k in sorted(n-{'Shell_Yellow_520-065_ESTIMATED'}):
 pairs=shell.overlap(trees[k])
 if pairs:contacts.append({'part':k,'triangle_surface_pairs':len(pairs),'part_authorized_for_refit':k in ALLOW})
assert hashlib.sha256(base.read_bytes()).hexdigest()==bh and hashlib.sha256(model.read_bytes()).hexdigest()==mh
report={'backup_sha256':bh,'model_sha256':mh,'model_objects':146,'model_triangles':totaltri,'changed_objects':changed,'explicit_allowed_contact_meshes':ALLOW,'locked_other_meshes':133,'locked_fingerprints_unchanged':True,'unexpected_changes':[],'changed_mesh_checks':meshreports,'shell_surface_contact_pairs':contacts,'backup_unchanged':True,'no_old_shell_geometry_used':True,'scope_check_completed':True,'changed_surface_defects_observed':any(q['nonadjacent_surface_overlap_candidates'] or q['nonmanifold_edges'] or q['zero_area_triangles'] for q in meshreports.values()),'complete_assembly_validated':False,'exit_code_semantics':'Exit0 means readback/scope report completed, NOT all geometry/image/assembly gates passed. Defects stay explicit in this report.','limits':'BVH overlap candidates require inspection; not manufacturing or seal acceptance. Closed thin-wall solid does not mean neck/window openings are filled. No object-scope pass implies image gates pass.'}
(out/'report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps({'changed':changed,'shell':meshreports['Shell_Yellow_520-065_ESTIMATED'],'contact_parts':len(contacts),'scope_preserved':True}),flush=True)
