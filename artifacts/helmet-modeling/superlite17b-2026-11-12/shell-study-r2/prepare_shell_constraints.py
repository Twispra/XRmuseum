"""Read retained r1, export shell data and fixed-part clearance constraints."""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ap=argparse.ArgumentParser();ap.add_argument('--output',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);here=Path(__file__).resolve().parent;source=here.parent/'sources/superlite17b-rear-study-r1.blend'
expected='f4c3241da1bd541d723d5dcf9cdc9be6a144ca3acdca1911b73002838840ce92';assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
bpy.ops.wm.open_mainfile(filepath=str(source));shell=bpy.data.objects['Shell_Yellow_520-065_ESTIMATED'];shell.data.calc_loop_triangles()
points=np.array([list(shell.matrix_world@v.co) for v in shell.data.vertices]);triangles=np.array([list(t.vertices) for t in shell.data.loop_triangles],dtype=np.int32)
assert len(points)==5880
original=json.loads((here.parent/'baseline-v4/readback.json').read_text());fixed={r['name'] for r in original['objects']} - {'Shell_Yellow_520-065_ESTIMATED','Weights_Brass_Rear_560-005_-1','Weights_Brass_Rear_560-005_1','Hinge_NeckClamp_Rear'}
fixed|={o.name for o in bpy.context.scene.objects if any(c.name.startswith('Rear_Connection_Study') for c in o.users_collection)}
assert len(fixed)==145
v=[];t=[]
for name in sorted(fixed):
    o=bpy.data.objects[name];o.data.calc_loop_triangles();offset=len(v)
    v.extend(o.matrix_world@x.co for x in o.data.vertices);t.extend(tuple(offset+i for i in face.vertices) for face in o.data.loop_triangles)
tree=BVHTree.FromPolygons(v,t,all_triangles=True)
distances=np.array([tree.find_nearest(Vector(p))[3] for p in points[:2254]])
front_y=points[:2254,1].min();front=np.where(points[:2254,1]<front_y+.0005)[0]
# The 196-point cut rim is identified from saved mesh connectivity to the
# appended collar, not from guessed coordinates or a machine-local metadata file.
rimset=set()
for edge in shell.data.edges:
    i,j=edge.vertices
    if i<2254 and j>=4508:rimset.add(i)
    if j<2254 and i>=4508:rimset.add(j)
rimids=np.array(sorted(rimset),dtype=np.int32);rim=points[rimids]
assert len(rimids)==196
rimdist=np.sqrt(np.min(np.sum((points[:2254,None,:]-rim[None,:,:])**2,axis=2),axis=1))
out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
np.savez_compressed(out/'shell-data.npz',points=points,triangles=triangles,fixed_part_distances=distances,front_ids=front,rim_ids=rimids,rim_distances=rimdist)
(out/'constraints.json').write_text(json.dumps({'source_sha256':expected,'shell_name':shell.name,'vertices':len(points),'triangles':len(triangles),'paired_outer_count':2254,'paired_inner_start':2254,'collar_start':4508,'locked_other_parts':sorted(fixed),'front_ids':front.tolist(),'neck_rim_ids':rimids.tolist(),'fixed_component_clearance_stats_m':{'min':float(distances.min()),'median':float(np.median(distances))},'limitation':'Distances constrain an unchanged estimated assembly; not real helmet fit or measurements. All numeric deformation choices remain visual estimates.'},indent=2)+'\n',encoding='utf-8')
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
print('SHELL_R2_CONSTRAINTS_READY 145 fixed objects / 196 rim points / source unchanged')
