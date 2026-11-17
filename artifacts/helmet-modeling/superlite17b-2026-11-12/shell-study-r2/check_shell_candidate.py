"""Compare actual saved candidate against retained r1, including collision deltas."""
import argparse,hashlib,json,math,struct,sys
from pathlib import Path
import bpy,bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ap=argparse.ArgumentParser();ap.add_argument('--candidate',required=True);ap.add_argument('--parameters',required=True);ap.add_argument('--choice',required=True);ap.add_argument('--output',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);here=Path(__file__).resolve().parent;base=here.parent/'sources/superlite17b-rear-study-r1.blend';candidate=Path(a.candidate).resolve();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
bh=hashlib.sha256(base.read_bytes()).hexdigest();ch=hashlib.sha256(candidate.read_bytes()).hexdigest();name='Shell_Yellow_520-065_ESTIMATED'
params=json.loads(Path(a.parameters).read_text(encoding='utf-8'));choice=next(c for c in params['candidates'] if c['name']==a.choice)
def finger(o):
 h=hashlib.sha256()
 for v in o.data.vertices:h.update(struct.pack('<3f',*v.co))
 for p in o.data.polygons:h.update(struct.pack('<I',len(p.vertices)));h.update(struct.pack('<'+'I'*len(p.vertices),*p.vertices))
 for row in o.matrix_world:h.update(struct.pack('<4f',*row))
 for uv in o.data.uv_layers:
  for q in uv.data:h.update(struct.pack('<2f',*q.uv))
 for slot in o.material_slots:
  if not slot.material:continue
  m=slot.material;h.update(m.name.encode());h.update(str(tuple(m.diffuse_color)).encode())
  if m.use_nodes:
   for node in m.node_tree.nodes:
    h.update((node.name+node.bl_idname).encode())
    for socket in node.inputs:
     if hasattr(socket,'default_value'):h.update(str(socket.default_value[:] if hasattr(socket.default_value,'__len__') and not isinstance(socket.default_value,str) else socket.default_value).encode())
 return h.hexdigest()
def shell_unchanged_attributes(o):
 h=hashlib.sha256()
 for p in o.data.polygons:h.update(str((tuple(p.vertices),p.use_smooth,p.material_index)).encode())
 for row in o.matrix_world:h.update(struct.pack('<4f',*row))
 for uv in o.data.uv_layers:
  for q in uv.data:h.update(struct.pack('<2f',*q.uv))
 for slot in o.material_slots:
  m=slot.material
  if not m:continue
  h.update((m.name+str(tuple(m.diffuse_color))).encode())
  if m.use_nodes:
   for node in m.node_tree.nodes:
    h.update((node.name+node.bl_idname).encode())
    for socket in node.inputs:
     if hasattr(socket,'default_value'):h.update(str(socket.default_value[:] if hasattr(socket.default_value,'__len__') and not isinstance(socket.default_value,str) else socket.default_value).encode())
   for link in m.node_tree.links:h.update(str((link.from_node.name,link.from_socket.identifier,link.to_node.name,link.to_socket.identifier)).encode())
 return h.hexdigest()
def tree(points,tri):return BVHTree.FromPolygons(points,tri,all_triangles=True)
def self_pairs(b,tri):return {tuple(sorted((i,j))) for i,j in b.overlap(b) if i!=j and not set(tri[i])&set(tri[j])}
bpy.ops.wm.open_mainfile(filepath=str(base));inventory=json.loads((here.parent/'baseline-v4/readback.json').read_text(encoding='utf-8'));original={r['name'] for r in inventory['objects']}-{'Weights_Brass_Rear_560-005_-1','Weights_Brass_Rear_560-005_1','Hinge_NeckClamp_Rear'}
original|={o.name for o in bpy.context.scene.objects if any(c.name.startswith('Rear_Connection_Study') for c in o.users_collection)};fixed=sorted(original-{name});assert len(fixed)==145
before={n:finger(bpy.data.objects[n]) for n in fixed};shell=bpy.data.objects[name];shell_attributes=shell_unchanged_attributes(shell);shell.data.calc_loop_triangles();old=[shell.matrix_world@v.co for v in shell.data.vertices];tri=[tuple(t.vertices) for t in shell.data.loop_triangles];baseline_tree=tree(old,tri);old_self=self_pairs(baseline_tree,tri)
fixed_geo={};old_overlap={}
for n in fixed:
 o=bpy.data.objects[n];o.data.calc_loop_triangles();v=[o.matrix_world@x.co for x in o.data.vertices];t=[tuple(p.vertices) for p in o.data.loop_triangles];fixed_geo[n]=(v,t);old_overlap[n]=set(baseline_tree.overlap(tree(v,t)))
bpy.ops.wm.open_mainfile(filepath=str(candidate));assert all(finger(bpy.data.objects[n])==before[n] for n in fixed)
shell=bpy.data.objects[name];new=[shell.matrix_world@v.co for v in shell.data.vertices];assert len(new)==len(old)==5880
assert shell_unchanged_attributes(shell)==shell_attributes
deltas=[q-p for p,q in zip(old,new)];fixed_errors=[deltas[i].length for i in choice['fixed_ids']]+[deltas[i+2254].length for i in choice['fixed_ids']]+[deltas[i].length for i in range(4508,5880)]
assert max(fixed_errors)<1e-8
newtree=tree(new,tri);new_self=self_pairs(newtree,tri);added_self=new_self-old_self
shell.data.calc_loop_triangles();render_tri=[tuple(t.vertices) for t in shell.data.loop_triangles]
render_tree=tree(new,render_tri);render_self=self_pairs(render_tree,render_tri)
def canonical_pairs(pairs,faces):return {tuple(sorted((tuple(sorted(faces[i])),tuple(sorted(faces[j]))))) for i,j in pairs}
actual_added_self=canonical_pairs(render_self,render_tri)-canonical_pairs(old_self,tri)
normal_flips=[];zero=0;area_ratios=[]
for i,(aa,bb,cc) in enumerate(tri):
 n0=(old[bb]-old[aa]).cross(old[cc]-old[aa]);n1=(new[bb]-new[aa]).cross(new[cc]-new[aa])
 if n1.length<2e-12:zero+=1
 if n0.dot(n1)<0:normal_flips.append(i)
 if n0.length>1e-12:area_ratios.append(n1.length/n0.length)
intersections=[];added_contacts=[]
for n,(v,t) in fixed_geo.items():
 fixedtree=tree(v,t);pairs=set(newtree.overlap(fixedtree));added=pairs-old_overlap[n]
 actual_pairs=set(render_tree.overlap(fixedtree))
 oldcanonical={(tuple(sorted(tri[i])),j) for i,j in old_overlap[n]}
 actual_added={(tuple(sorted(render_tri[i])),j) for i,j in actual_pairs}-oldcanonical
 if pairs or old_overlap[n]:intersections.append({'fixed_part':n,'before_pairs':len(old_overlap[n]),'after_pairs':len(pairs),'new_specific_pairs':len(added)})
 if added or actual_added:added_contacts.append({'part':n,'count':len(added),'first_pairs':sorted(added)[:10],'actual_render_added_canonical_pairs':len(actual_added)})
wall_change=max(((new[i+2254]-new[i])-(old[i+2254]-old[i])).length for i in range(2254))
innertri=[t for t in tri if all(2254<=i<4508 for i in t)]
before_inner=tree(old,innertri);after_inner=tree(new,innertri)
old_sep=[before_inner.find_nearest(p)[3] for p in old[:2254]];new_sep=[after_inner.find_nearest(p)[3] for p in new[:2254]]
new_close=[i for i,(d0,d1) in enumerate(zip(old_sep,new_sep)) if d1<.0005 and d1<d0-1e-6]
bm=bmesh.new();bm.from_mesh(shell.data);nonmanifold=sum(not e.is_manifold for e in bm.edges);bm.free()
report={'baseline_sha256':bh,'candidate_sha256':ch,'choice':a.choice,'fixed_other_parts':145,'fixed_fingerprints_geometry_transform_uv_materials_identical':True,'shell_topology_uv_transform_materials_unchanged':True,'vertices':5880,'same_baseline_triangle_list_used':True,'triangles':len(tri),'actual_render_triangulation_different_entries':sum(a!=b for a,b in zip(tri,render_tri)),'actual_render_self_pairs':len(render_self),'actual_render_new_canonical_self_pairs':len(actual_added_self),'exact_protected_outer_count':len(choice['fixed_ids']),'protected_vertex_max_error_model_m':max(fixed_errors),'max_displacement_model_m':max(d.length for d in deltas),'paired_wall_vector_max_change_model_m':wall_change,'outer_vertex_to_inner_surface_minimum_before_after_model_m':[min(old_sep),min(new_sep)],'new_vertex_samples_below_half_mm_clearance':new_close,'nonmanifold_edges':nonmanifold,'zero_area_triangles':zero,'reversed_triangle_normal_indices':normal_flips,'area_ratio_range':[min(area_ratios),max(area_ratios)],'baseline_nonadjacent_self_overlap_pairs':len(old_self),'candidate_nonadjacent_self_overlap_pairs':len(new_self),'baseline_specific_self_pairs':sorted(old_self),'added_specific_self_pairs':sorted(added_self),'fixed_parts_surface_intersection_deltas':intersections,'new_specific_contact_pairs':added_contacts,'source_files_unchanged':hashlib.sha256(base.read_bytes()).hexdigest()==bh and hashlib.sha256(candidate.read_bytes()).hexdigest()==ch,'limits':'BVH pairs are geometric candidates, not physical fit, thickness or safety acceptance. Existing baseline overlaps are not proven harmless. Vertex-to-inner distances are samples, not a full minimum-thickness proof. Photo cameras/scale remain uncertain.'}
passed=not added_self and not actual_added_self and not added_contacts and not new_close and not normal_flips and zero==0 and nonmanifold==0 and wall_change<1e-7
report['geometry_gate_passed']=passed
report['changed_outer_vertices_exact_nonzero']=sum(d.length>0 for d in deltas[:2254])
report['changed_outer_vertices_above_1e8_model_m']=sum(d.length>1e-8 for d in deltas[:2254])
(out/'report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('SHELL_ONLY_GEOMETRY_GATE '+str(passed)+' baseline/self '+str(len(old_self))+'/'+str(len(new_self))+' added_contacts '+str(sum(x['count'] for x in added_contacts)),flush=True)
assert passed,'Shell candidate failed unchanged-boundary / new-intersection gate; see report.'
