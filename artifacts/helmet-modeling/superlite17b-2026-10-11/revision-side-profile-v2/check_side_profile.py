"""Inspect saved profile changes, without writing the .blend."""
import bpy,json,sys,math,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
args=sys.argv[sys.argv.index('--')+1:]
source=Path(args[0]).resolve();out=Path(args[1]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(source))
def points(o):return [o.matrix_world@v.co for v in o.data.vertices]
neck=points(bpy.data.objects['NeckClamp_505-055'])
ymean=sum(v.y for v in neck)/len(neck);zmean=sum(v.z for v in neck)/len(neck)
slope=sum((v.y-ymean)*(v.z-zmean) for v in neck)/sum((v.y-ymean)**2 for v in neck)
intercept=zmean-slope*ymean
residual=[v.z-slope*v.y for v in neck]
shell=bpy.data.objects['Shell_Yellow_520-065_ESTIMATED']
shellpts=points(shell)
bvh=BVHTree.FromObject(shell,bpy.context.evaluated_depsgraph_get())
plates=[]
for o in bpy.context.scene.objects:
 if o.type=='MESH' and o.name.startswith('Weights_Brass_Rear_560-005_'):
  dist=[bvh.find_nearest(v)[3] for v in points(o)]
  plates.append(dict(name=o.name,max_shell_distance_m=max(dist),min_shell_distance_m=min(dist)))
handle=points(bpy.data.objects['Handle_Brass_560-014'])
gap=max(v.z for v in handle)-max(v.z for v in shellpts)
checks=dict(neck_angle_matches_v2=abs(slope-.58)<.01,
            neck_vertical_band_thickness_under_14mm=max(residual)-min(residual)<.014,
            two_shallow_rear_plates=len(plates)==2 and all(p['max_shell_distance_m']<.007 for p in plates),
            handle_height_above_shell_under_35mm=0<gap<.035,
            rear_retainer_straps_present=all('Strap_YokeRetainer_'+str(i) in bpy.data.objects for i in [-1,1]))
report=dict(source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
            passed=all(checks.values()),checks=checks,neck_slope=slope,
            neck_angle_deg=math.degrees(math.atan(slope)),neck_plane_intercept_m=intercept,
            neck_vertical_band_thickness_m=max(residual)-min(residual),
            handle_height_above_shell_m=gap,rear_plates=plates,
            limitation='Tests verify intended modelling corrections, not measured real dimensions or photo accuracy.')
out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('PROFILE',json.dumps(report),flush=True)
if not report['passed']:raise RuntimeError('Profile checks failed')
