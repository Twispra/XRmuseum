"""Sampled surface-intersection audit, not engineering fit or continuous physics.
Blender -b --python check_window_clearance.py -- --output ASSET_DIRECTORY
"""
import bpy,math,json,sys,hashlib
from pathlib import Path
from mathutils.bvhtree import BVHTree
args=sys.argv[sys.argv.index('--')+1:];out=Path(args[args.index('--output')+1]).resolve()
source=out/'sources/mark-v-mod1.blend';bpy.ops.wm.open_mainfile(filepath=str(source))
parts=bpy.data.collections['01_MARK_V_EDITABLE'];door=bpy.data.objects['FrontWindowPivot'];toggle=bpy.data.objects['DoorTogglePivot']
def tree(ob):
    deps=bpy.context.evaluated_depsgraph_get();ev=ob.evaluated_get(deps);me=ev.to_mesh();me.calc_loop_triangles()
    t=BVHTree.FromPolygons([ob.matrix_world@v.co for v in me.vertices],[tuple(p.vertices) for p in me.loop_triangles],all_triangles=True,epsilon=1e-8)
    ev.to_mesh_clear();return t
obstacles=[bpy.data.objects['Bonnet_HollowCopper_TinFinish'],*[ob for ob in parts.objects if ob.type=='MESH' and ob.name.startswith('TelephoneCup_')]]
obstacle_trees={ob.name:tree(ob) for ob in obstacles}
moving=[ob for ob in door.children_recursive if ob.type=='MESH'];samples=[]
toggle.rotation_euler.z=math.radians(-70)
for deg in range(0,101,5):
    door.rotation_euler.z=math.radians(deg);bpy.context.view_layer.update();intersections=[]
    for ob in moving:
        t=tree(ob)
        for name,other in obstacle_trees.items():
            pairs=t.overlap(other)
            if pairs:intersections.append({'moving':ob.name,'obstacle':name,'triangle_pairs':len(pairs)})
    samples.append({'door_angle_deg':deg,'surface_intersections':intersections})
door.rotation_euler.z=0;toggle.rotation_euler.z=0;bpy.context.view_layer.update()
report={'blender':bpy.app.version_string,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'obstacles':list(obstacle_trees),'moving_meshes':len(moving),'poses':samples,'passed':all(not s['surface_intersections'] for s in samples),'scope':'21 poses at five-degree intervals, moving front-window surfaces versus shell and telephone cup only','excluded':['hinge/seat/gasket intentional mating contact','toggle/fork loosening and thread fit','intersections between samples','solid containment and engineering tolerance','Unity collider behaviour','real artifact stop angle'],'angle_status':'100 degrees is an illustrative review angle, not measured from artifact'}
(out/'qa/window-clearance.json').write_text(json.dumps(report,indent=2),encoding='utf-8',newline='\n')
print('WINDOW_CLEARANCE',report['passed'],[(s['door_angle_deg'],s['surface_intersections']) for s in samples if s['surface_intersections']],flush=True)
if not report['passed']:raise RuntimeError('Sampled window/shell or cup intersections detected')
