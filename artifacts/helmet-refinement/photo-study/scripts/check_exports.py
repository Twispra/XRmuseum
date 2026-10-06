"""Blender readback checks. Does not assert Unity, historical or device validity."""
import bpy, bmesh, json, math, sys, hashlib
from pathlib import Path
from mathutils import Vector

args=sys.argv[sys.argv.index('--')+1:]
out=Path(args[args.index('--output')+1]).resolve()
report={'scope':'Blender editable source and FBX/GLB readback only','unity_tested':False,'device_tested':False,'historical_accuracy_verified':False,'checks':[]}

def inspect(objects):
    deps=bpy.context.evaluated_depsgraph_get();points=[];triangles=0;names=[];invalid=0;uv_missing=[]
    for ob in objects:
        if ob.type not in {'MESH','CURVE'}:continue
        ev=ob.evaluated_get(deps);me=ev.to_mesh();me.calc_loop_triangles()
        triangles+=len(me.loop_triangles);names.append(ob.name)
        if not me.uv_layers:uv_missing.append(ob.name)
        for vertex in me.vertices:
            p=ob.matrix_world@vertex.co
            if not all(math.isfinite(x) for x in p):invalid+=1
            points.append(p)
        ev.to_mesh_clear()
    assert points and triangles>0 and invalid==0, 'Empty or nonfinite geometry'
    return {'mesh_parts':len(names),'triangles':triangles,'nonfinite_vertices':invalid,'missing_uv_parts':uv_missing,
        'bounds':{'min':[min(p[i] for p in points) for i in range(3)],'max':[max(p[i] for p in points) for i in range(3)]}}

bpy.ops.wm.open_mainfile(filepath=str(out/'sources/photo-helmet-study.blend'))
parts=list(bpy.data.collections['01_HELMET_EDITABLE_PARTS'].objects)
source=inspect(parts);source['name']='editable source';source['packed_images']=sum(bool(im.packed_file) for im in bpy.data.images)
assert source['packed_images']>=6
for name in ['FrontWindowAssembly','SideWindowsAssembly','CorseletAssembly','RearFittings_UnverifiedFunctions']:
    assert bpy.data.objects.get(name) is not None
shell=bpy.data.objects['Shell_Hollow_WallThicknessAssumed'];me=shell.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
bm=bmesh.new();bm.from_mesh(me);source['shell_nonmanifold_edges']=sum(not e.is_manifold for e in bm.edges);bm.free()
assert source['shell_nonmanifold_edges']==0, 'Shell contains an open or nonmanifold edge'
report['checks'].append(source)
front=inspect([ob for ob in parts if ob.parent and ob.parent.name=='FrontWindowAssembly'])
for stem in ['photo-helmet-study','front-window-study']:
    expected=source if stem.startswith('photo') else front
    for extension in ['fbx','glb']:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path=out/'exports'/f'{stem}.{extension}'
        if extension=='fbx':bpy.ops.import_scene.fbx(filepath=str(path))
        else:bpy.ops.import_scene.gltf(filepath=str(path))
        check=inspect(list(bpy.context.scene.objects));check['name']=path.name
        delta=max(abs(check['bounds'][key][i]-expected['bounds'][key][i]) for key in ['min','max'] for i in range(3))
        check['bounds_max_delta_m']=delta;check['sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
        assert delta<0.0001, f'Unexpected dimensions in {path.name}: {delta}'
        assert check['triangles']==expected['triangles'], f'Triangle count changed in {path.name}'
        assert not check['missing_uv_parts'], 'Exported parts missing UV'
        report['checks'].append(check)
report['passed']=True
report['generator_sha256']=hashlib.sha256((out/'scripts/build_photo_study.py').read_bytes()).hexdigest()
(out/'qa/export-readback.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('EXPORT_READBACK_PASSED', source['triangles'],flush=True)
