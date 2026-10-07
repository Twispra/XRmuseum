"""Blender/FBX/GLB asset readback, topology and known structural relationships.
Does not claim historical survey accuracy, Unity integration or device performance.
"""
import bpy, bmesh, math, json, sys, hashlib, struct
from collections import Counter
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];out=Path(args[args.index('--output')+1]).resolve()
report={'blender':bpy.app.version_string,'scope':'Asset source/export readback and specified geometric checks','unity_tested':False,'device_tested':False,'historical_survey_verified':False,'checks':[]}
def inspect(objects):
    bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();points=[];triangles=0;names=[];invalid=0;uv_missing=[];normals_invalid=0;topology=[]
    for ob in objects:
        if ob.type!='MESH':continue
        ev=ob.evaluated_get(deps);me=ev.to_mesh();me.calc_loop_triangles();triangles+=len(me.loop_triangles);names.append(ob.name)
        if not me.uv_layers:uv_missing.append(ob.name)
        if me.uv_layers:
            invalid+=sum(not all(math.isfinite(x) for x in p.uv) for p in me.uv_layers.active.data)
        for v in me.vertices:
            p=ob.matrix_world@v.co;invalid+=not all(math.isfinite(x) for x in p);points.append(p)
        normals_invalid+=sum(not all(math.isfinite(x) for x in p.normal) or p.normal.length<.9 for p in me.polygons)
        bm=bmesh.new();bm.from_mesh(me)
        topology.append({'name':ob.name,'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'loose_vertices':sum(not v.link_edges for v in bm.verts),'zero_area_faces':sum(f.calc_area()<1e-14 for f in bm.faces)})
        bm.free();ev.to_mesh_clear()
    assert points and triangles>0 and invalid==0,'Empty or non-finite mesh/UV'
    assert not uv_missing,'UV missing'
    return {'mesh_parts':len(names),'triangles':triangles,'invalid_coordinates':invalid,'missing_uv_parts':uv_missing,'invalid_normals':normals_invalid,'topology_issues':[t for t in topology if t['nonmanifold_edges'] or t['loose_vertices'] or t['zero_area_faces']],
            'bounds':{'min':[min(p[i] for p in points) for i in range(3)],'max':[max(p[i] for p in points) for i in range(3)]}}
bpy.ops.wm.open_mainfile(filepath=str(out/'sources/mark-v-mod1.blend'))
parts=list(bpy.data.collections['01_MARK_V_EDITABLE'].objects);source=inspect(parts);source['name']='editable assembly'
source['packed_maps']=sum(bool(im.packed_file) for im in bpy.data.images)
assert source['packed_maps']==6
assert source['invalid_normals']==0
source['envelope_m']=[source['bounds']['max'][i]-source['bounds']['min'][i] for i in range(3)]
body=bpy.data.objects['Bonnet_HollowCopper_TinFinish'];shell_issue=next((t for t in source['topology_issues'] if t['name']==body.name),None)
def triangle_records(ob):
    ob.data.calc_loop_triangles();records={}
    for t in ob.data.loop_triangles:
        key=tuple(sorted(tuple(round(x,7) for x in ob.matrix_world@ob.data.vertices[v].co) for v in t.vertices))
        records.setdefault(key,[]).append(t.area)
    return records
shell_triangles=triangle_records(body)
print('SOURCE_TOPOLOGY_DIAGNOSTIC',source['topology_issues'],flush=True)
assert shell_issue is None,'Hollow shell topology invalid'
if bpy.context.scene.get('refinement_revision')=='R2':
    assert not source['topology_issues'],'R2 contains open, loose or degenerate source geometry'
# Ray traverses the open front aperture into the cavity and reaches only the far shell.
origin=Vector((0,-.25,.347));hit,location,normal,index=body.ray_cast(body.matrix_world.inverted()@origin,Vector((0,1,0)))
assert hit and (body.matrix_world@location).y>.10,'Front aperture remains blocked by shell'
source['front_shell_aperture_clear']=True
for sign in [-1,1]:
    hit,location,normal,index=body.ray_cast(Vector((sign*.25,0,.336)),Vector((-sign,0,0)))
    assert not hit or sign*location.x<-.10,'Side aperture remains blocked'
source['side_shell_apertures_clear']=True
top_origin=Vector((0,-.085,.462))+Vector((0,-.58,.815))*.15
hit,location,normal,index=body.ray_cast(top_origin,Vector((0,.58,-.815)))
assert not hit or (location-top_origin).length>.20,'Top aperture remains blocked'
source['top_shell_aperture_clear']=True
# Published current nominal opening, not proof of this specimen's measured diameter.
neck=bpy.data.objects['LowerNeckRing_NominalOpening'];radii=[math.hypot(v.co.x,v.co.y) for v in neck.data.vertices]
assert abs(min(radii)*2-(8+9/16)*.0254)<1e-6
assert bpy.data.objects['Anchor_TelephoneOutlet'].location.x>0 and bpy.data.objects['Anchor_AirOutlet'].location.x<0
source['rear_connection_sides_checked']=True
for name,count in [('Brail_',4),('BrailFastener_',12),('SideGlass_',2)]:
    if name=='BrailFastener_':actual=sum(ob.name.startswith(name) and ob.name.endswith('_CastWingnut') for ob in parts)
    elif name=='Brail_':actual=sum(ob.name.startswith(name) and 'ClampingStrap' in ob.name for ob in parts)
    else:actual=sum(ob.name.startswith(name) for ob in parts)
    assert actual==count,(name,actual)
assert sum(ob.name=='TopGlass' or ob.name.startswith('SideGlass_') or ob.name.startswith('FrontGlass_') for ob in parts)==4
source['four_brails_twelve_wingnuts_four_glass_windows']=True
door=bpy.data.objects['FrontWindowPivot'];glass=bpy.data.objects['FrontGlass_12point7mm_SpecimenApproximation']
closed=glass.matrix_world.translation.copy();door.rotation_euler.z=math.radians(100);bpy.context.view_layer.update();opened=glass.matrix_world.translation.copy()
assert (closed-opened).length>.10,'Front pivot does not move the window'
assert opened.x>.08,'Door rotates to the incorrect side'
door.rotation_euler.z=0;bpy.context.view_layer.update();source['window_pivot_checked']=True
source['source_sha256']=hashlib.sha256((out/'sources/mark-v-mod1.blend').read_bytes()).hexdigest();report['checks'].append(source)
front=inspect([door,*door.children])
for stem in ['mark-v-mod1','mark-v-front-window']:
    expected=source if stem=='mark-v-mod1' else front
    for ext in ['fbx','glb']:
        bpy.ops.wm.read_factory_settings(use_empty=True);path=out/'exports'/(stem+'.'+ext)
        if ext=='fbx':bpy.ops.import_scene.fbx(filepath=str(path))
        else:bpy.ops.import_scene.gltf(filepath=str(path))
        check=inspect(list(bpy.context.scene.objects));check['name']=path.name
        delta=max(abs(check['bounds'][key][i]-expected['bounds'][key][i]) for key in ['min','max'] for i in range(3))
        assert delta<.0001,(path.name,delta)
        if ext=='glb':
            raw=path.read_bytes();json_length=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+json_length])
            raw_triangles=sum(doc['accessors'][p['indices']]['count']//3 for m in doc['meshes'] for p in m['primitives'])
            assert raw_triangles==expected['triangles'],(path.name,'exported triangles differ')
            check['encoded_triangles']=raw_triangles
        if check['triangles']!=expected['triangles']:
            # Blender's glTF readback validates away two sub-micrometre Boolean slivers.
            # The GLB itself retains both. Verify the exact geometric residual rather
            # than weakening the triangle-count check for the assembly in general.
            assert stem=='mark-v-mod1' and ext=='glb'
            imported_shell=triangle_records(bpy.data.objects['Bonnet_HollowCopper_TinFinish'])
            original_counts=Counter({k:len(v) for k,v in shell_triangles.items()});imported_counts=Counter({k:len(v) for k,v in imported_shell.items()})
            assert not imported_counts-original_counts,'Readback introduced unknown shell triangles'
            residual=original_counts-imported_counts
            missing=[{'vertices_m':k,'area_m2':shell_triangles[k][0]} for k,n in residual.items() for _ in range(n)]
            assert len(missing)==expected['triangles']-check['triangles'] and len(missing)<=2
            assert all(t['area_m2']<1e-11 for t in missing),'Readback lost a visible surface'
            check['blender_import_validation_removed_slivers']=missing
        assert check['mesh_parts']==expected['mesh_parts'],(path.name,'parts lost')
        assert check['invalid_normals']==0
        check['bounds_max_delta_m']=delta;check['sha256']=hashlib.sha256(path.read_bytes()).hexdigest();report['checks'].append(check)
partreports=[]
for path in sorted((out/'sources/parts').glob('*.blend')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(path),link=False) as (a,b):b.objects=list(a.objects)
    objects=[ob for ob in b.objects if ob is not None]
    for ob in objects:bpy.context.scene.collection.objects.link(ob)
    result=inspect(objects);result['file']=path.name;assert result['triangles']>0
    texture_paths=[]
    for im in bpy.data.images:
        if im.source!='FILE':continue
        resolved=Path(bpy.path.abspath(im.filepath))
        if not resolved.is_file() and im.filepath.startswith('//'):resolved=(path.parent/im.filepath[2:]).resolve()
        assert im.packed_file or resolved.is_file(),(path.name,'external texture missing',im.filepath)
        texture_paths.append(resolved.name)
    result['texture_files_present']=texture_paths
    result['sha256']=hashlib.sha256(path.read_bytes()).hexdigest();partreports.append(result)
report['independent_part_sources']=partreports
report['generator_sha256']=hashlib.sha256(Path(__file__).with_name('build_mark_v.py').read_bytes()).hexdigest()
assert report['generator_sha256']==json.loads((out/'qa/geometry-report.json').read_text(encoding='utf-8'))['generator_sha256'],'Generator differs from the measured asset version'
geometry=json.loads((out/'qa/geometry-report.json').read_text(encoding='utf-8'))
if geometry.get('revision')=='R2':
    report['revision_script_sha256']=hashlib.sha256(Path(__file__).with_name('refine_mark_v_r2.py').read_bytes()).hexdigest()
    assert report['revision_script_sha256']==geometry['revision_script_sha256'],'Revision script differs from measured asset version'
    report['input_source_sha256']=geometry['input_source_sha256']
report['checker_sha256']=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
report['passed']=True
(out/'qa/export-readback.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
print('MARK_V_ASSET_CHECKS_PASSED',source['mesh_parts'],source['triangles'],source['envelope_m'],source['topology_issues'],flush=True)
