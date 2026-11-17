"""Actual saved-file comparison and triangle-surface checks for rear study."""
import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ap=argparse.ArgumentParser();ap.add_argument('--study',required=True);ap.add_argument('--output',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);here=Path(__file__).resolve().parent
source=here/'baseline-v4/superlite17b-shell-v4.blend';study=Path(a.study).resolve();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
basehash=hashlib.sha256(source.read_bytes()).hexdigest();studyhash=hashlib.sha256(study.read_bytes()).hexdigest()
removed={'Weights_Brass_Rear_560-005_-1','Weights_Brass_Rear_560-005_1','Hinge_NeckClamp_Rear'}
newnames={'RearWeight_560-005_Continuous_RESEARCH','AlignmentSleeve_550-339_FLANGED_ESTIMATED','RearHingeTab_540-157_SINGLE_PIECE_ESTIMATED','AlignmentScrew_530-090_VISUAL_ESTIMATED','AlignmentScrew_530-090_Head_ESTIMATED'}
original=json.loads((source.parent/'readback.json').read_text());names={o['name'] for o in original['objects']}
def fingerprint(o):
    h=hashlib.sha256()
    for v in o.data.vertices:h.update(struct.pack('<3f',*v.co))
    for p in o.data.polygons:
        h.update(struct.pack('<I',len(p.vertices)));h.update(struct.pack('<'+'I'*len(p.vertices),*p.vertices))
    for row in o.matrix_world:h.update(struct.pack('<4f',*row))
    for uv in o.data.uv_layers:
        for d in uv.data:h.update(struct.pack('<2f',*d.uv))
    return h.hexdigest()
def materials(o):
    rows=[]
    for slot in o.material_slots:
        m=slot.material
        if not m:rows.append(None);continue
        nodes=[]
        if m.use_nodes:
            for node in m.node_tree.nodes:
                inputs=[]
                for socket in node.inputs:
                    if hasattr(socket,'default_value'):
                        value=socket.default_value
                        inputs.append([socket.identifier,list(value) if hasattr(value,'__len__') and not isinstance(value,str) else value])
                nodes.append([node.name,node.bl_idname,inputs])
        rows.append([m.name,list(m.diffuse_color),m.use_nodes,nodes])
    return rows
bpy.ops.wm.open_mainfile(filepath=str(source));before={n:fingerprint(bpy.data.objects[n]) for n in names};before_materials={n:materials(bpy.data.objects[n]) for n in names}
bpy.ops.wm.open_mainfile(filepath=str(study))
unchanged={n:fingerprint(bpy.data.objects[n])==before[n] for n in names-removed}
assert all(unchanged.values()) and len(unchanged)==141
assert all(materials(bpy.data.objects[n])==before_materials[n] for n in names-removed)
assert not any(n in bpy.data.objects for n in removed)
checks={};trees={};geometries={}
for n in newnames|{'Shell_Yellow_520-065_ESTIMATED','Hinge_Pin','NeckClamp_505-055','NeckDam_Neoprene_510-533','Neck_Yoke_520-117'}:
    o=bpy.data.objects[n];m=o.data;m.calc_loop_triangles()
    v=[o.matrix_world@x.co for x in m.vertices];tri=[tuple(t.vertices) for t in m.loop_triangles]
    trees[n]=BVHTree.FromPolygons(v,tri,all_triangles=True);geometries[n]=(v,tri)
    if n not in newnames:continue
    bm=bmesh.new();bm.from_mesh(m)
    nonmanifold=sum(not e.is_manifold for e in bm.edges);components=0;unseen=set(bm.verts)
    while unseen:
        components+=1;stack=[unseen.pop()]
        while stack:
            x=stack.pop()
            for e in x.link_edges:
                other=e.other_vert(x)
                if other in unseen:unseen.remove(other);stack.append(other)
    bm.free()
    # Ignore triangles sharing any topological vertex when checking self overlap.
    pairs=trees[n].overlap(trees[n]);nonadj=[]
    for i,j in pairs:
        if i>=j or set(tri[i])&set(tri[j]):continue
        nonadj.append([i,j])
    checks[n]={'vertices':len(m.vertices),'triangles':len(m.loop_triangles),'nonmanifold_edges':nonmanifold,'connected_components':components,
               'nonadjacent_self_overlap_pairs':len(nonadj),'zero_area_triangles':sum(t.area<1e-12 for t in m.loop_triangles),'uv_layers':len(m.uv_layers),'finite_coordinates':all(math.isfinite(x) for p in v for x in p)}
    assert checks[n]['finite_coordinates'] and len(m.uv_layers)>0
    assert nonmanifold==0 and components==1 and not nonadj and checks[n]['zero_area_triangles']==0,(n,checks[n])
intersections=[]
for n in newnames:
    for fixed in ['Shell_Yellow_520-065_ESTIMATED','NeckDam_Neoprene_510-533','Neck_Yoke_520-117']:
        pairs=trees[n].overlap(trees[fixed]);intersections.append({'new_part':n,'fixed_part':fixed,'surface_triangle_overlap_pairs':len(pairs)})
        assert not pairs,(n,fixed,len(pairs))
allow=[]
for n in newnames:
    for fixed in ['Hinge_Pin','NeckClamp_505-055']:
        pairs=trees[n].overlap(trees[fixed]);allow.append({'new_part':n,'background_mount':fixed,'surface_overlap_pairs':len(pairs),'contact_status':'Research mounting contacts need visual review; not claimed manufacturing fit.'})
retained_surface_scan=[]
for fixed in names-removed:
    if fixed not in trees:
        o=bpy.data.objects[fixed];o.data.calc_loop_triangles()
        trees[fixed]=BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[t.vertices[:] for t in o.data.loop_triangles],all_triangles=True)
    for n in newnames:
        overlap=trees[n].overlap(trees[fixed])
        if overlap:retained_surface_scan.append({'new_part':n,'retained_part':fixed,'surface_overlap_pairs':len(overlap)})
assert not retained_surface_scan,retained_surface_scan
new_pair_scan=[]
ordered=sorted(newnames)
for i,n in enumerate(ordered):
    for other in ordered[i+1:]:
        overlap=trees[n].overlap(trees[other])
        same_fastener={n,other}=={'AlignmentScrew_530-090_VISUAL_ESTIMATED','AlignmentScrew_530-090_Head_ESTIMATED'}
        new_pair_scan.append({'a':n,'b':other,'surface_overlap_pairs':len(overlap),'allowed_same_visual_fastener_head_shaft_join':same_fastener})
        assert not overlap or same_fastener,(n,other,len(overlap))
tab_points=geometries['RearHingeTab_540-157_SINGLE_PIECE_ESTIMATED'][0];pin_points=geometries['Hinge_Pin'][0]
tab_pin_y_gap=min(p.y for p in tab_points)-max(p.y for p in pin_points)
assert hashlib.sha256(source.read_bytes()).hexdigest()==basehash
assert hashlib.sha256(study.read_bytes()).hexdigest()==studyhash
(out/'report.json').write_text(json.dumps({'baseline_sha256':basehash,'study_sha256':studyhash,'preserved_original_part_count':141,'all_preserved_fingerprints_identical':True,'preserved_material_node_defaults_identical':True,'fingerprint_includes':['raw vertices','topology','matrix_world','UV coordinates'],'new_parts':checks,'protected_surface_intersections':intersections,'mount_background_contacts':allow,'all_141_retained_parts_surface_overlap_scan':retained_surface_scan,'new_vs_retained_checks':len(newnames)*141,'new_pair_surface_scan':new_pair_scan,'uncompleted_lower_tab_pin_y_gap_model_m':tab_pin_y_gap,'source_files_unchanged':True,'limits':'Triangle-level model checks, not CAD fit, containment/real load/seal validation or photo accuracy improvement. No contact is not proof of correct attachment.'},indent=2)+'\n',encoding='utf-8')
print('REAR_STUDY_GEOMETRY_CHECK_OK 141 original parts unchanged; new meshes closed and no protected surface overlaps')
