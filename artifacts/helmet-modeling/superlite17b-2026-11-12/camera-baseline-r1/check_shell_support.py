"""Check whether annotated weight-edge camera rays intersect the saved shell."""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ap=argparse.ArgumentParser();ap.add_argument('--cameras',required=True);ap.add_argument('--output',required=True)
args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
here=Path(__file__).resolve().parent;root=here.parents[3]
source=root/'artifacts/helmet-modeling/superlite17b-2026-11-12/baseline-v4/superlite17b-shell-v4.blend'
expected='6e85f7f048b3a6b8706428854ab8934099da25ff21d427e87fde9de930b1c8a2'
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
bpy.ops.wm.open_mainfile(filepath=str(source))
shell=bpy.data.objects['Shell_Yellow_520-065_ESTIMATED']
mesh=shell.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh();mesh.calc_loop_triangles()
bvh=BVHTree.FromPolygons([shell.matrix_world@v.co for v in mesh.vertices],[t.vertices[:] for t in mesh.loop_triangles],all_triangles=True)
ann=json.loads((here/'annotations-side.json').read_text(encoding='utf-8'))
curves=[r for r in ann['visible_contours'] if r['id'].startswith('rear_weight_')]
fit=json.loads(Path(args.cameras).read_text())['views']['side'];primary=fit['primary']
profiles=[c for c in fit['candidates'] if c['pass']=='a' and c['objective_cost']<=primary['objective_cost']*1.25]
rows=[]
for c in profiles:
    right,up,back=[Vector(v) for v in c['camera_basis_right_up_back']]
    target=Vector(c['target_world']);p=c['parameters'];camera=target+back*math.exp(p[3]);f=c['focal_px']
    traces=[]
    for curve in curves:
        samples=[]
        for u,v in curve['pass_a_xy_px']:
            direction=(-back+right*((u-p[4])/f)-up*((v-p[5])/f)).normalized()
            hit,normal,face,distance=bvh.ray_cast(camera,direction,100)
            samples.append({'pixel':[u,v],'intersects_saved_shell':hit is not None,'hit_world':list(hit) if hit is not None else None})
        traces.append({'id':curve['id'],'hits':sum(s['intersects_saved_shell'] for s in samples),'samples':samples})
    rows.append({'focal_width_factor':c['focal_width_factor'],'curves':traces})
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
out=Path(args.output).resolve();out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps({'source_sha256':expected,'profile_selection':'pass A, cost <=1.25 primary; non-probabilistic sensitivity set','profiles':rows,
    'meaning':'Failure to hit the existing shell shows that a simple surface-projected weight edit cannot reproduce that observed boundary under that candidate camera. It does not prove the true shell is wrong or require the real weight to contact shell at every outline pixel. Camera/configuration/overhang remain possible explanations.',
    'geometry_modified':False},indent=2)+'\n',encoding='utf-8')
print('V4_SHELL_SUPPORT_CHECK_COMPLETE source unchanged')
