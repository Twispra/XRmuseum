"""Background-only FBX export and round-trip geometry/hinge checks."""
import bpy, json, math
from pathlib import Path
from mathutils import Vector
ROOT = Path('E:/XRmuseum/artifacts/helmet-prototype')
assert bpy.app.background, 'Run in an isolated background process.'
assert bpy.context.scene.get('recovery_v02_applied')

def bounds(objects):
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for ob in objects:
        if ob.type == 'MESH':
            ev = ob.evaluated_get(dg)
            pts += [ev.matrix_world @ Vector(v) for v in ev.bound_box]
    low = [min(p[k] for p in pts) for k in range(3)]
    high = [max(p[k] for p in pts) for k in range(3)]
    return {'min_m':low, 'max_m':high, 'size_m':[high[k]-low[k] for k in range(3)]}

def select(objects):
    for ob in bpy.context.scene.objects:
        ob.select_set(False)
    for ob in objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

params = bpy.ops.export_scene.fbx.get_rna_type().properties
for key, value in {'axis_forward':'-Z', 'axis_up':'Y', 'apply_scale_options':'FBX_SCALE_UNITS'}.items():
    assert value in {x.identifier for x in params[key].enum_items}
assert {'MESH','EMPTY'} <= {x.identifier for x in params['object_types'].enum_items}

jobs = [('01_GRAYBOX_RealScale','Helmet_Graybox_ROOT','helmet-graybox-v02.fbx'),
        ('02_PORTHOLE_QualitySample','Porthole_Sample_ROOT','porthole-sample-v02.fbx')]
report = {'scope':'FBX geometry and hierarchy only; no Unity runtime/material validation', 'exports':[]}
for coll_name,root_name,filename in jobs:
    objects = list(bpy.data.collections[coll_name].objects)
    root = bpy.data.objects[root_name]
    old_location = root.location.copy()
    root.location = (0,0,0)
    bpy.context.view_layer.update()
    source = bounds(objects)
    select(objects)
    path = ROOT/'exports'/filename
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},
        use_mesh_modifiers=True,bake_anim=False,add_leaf_bones=False,
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,use_custom_props=True)
    root.location = old_location
    bpy.context.view_layer.update()
    report['exports'].append({'file':str(path),'source_bounds':source,
                             'source_mesh_count':sum(o.type=='MESH' for o in objects)})

# A clean Blender scene tests what was actually written to each FBX.
for item in report['exports']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=item['file'])
    objects = list(bpy.context.scene.objects)
    got = bounds(objects)
    error = max(abs(got['size_m'][i]-item['source_bounds']['size_m'][i]) for i in range(3))
    count = sum(o.type=='MESH' for o in objects)
    item['roundtrip_bounds'] = got
    item['roundtrip_mesh_count'] = count
    item['max_size_error_mm'] = error*1000
    item['excluded_studio'] = not any(o.type in {'CAMERA','LIGHT'} or o.name.startswith('STUDIO_') for o in objects)
    item['pass'] = error < .0001 and count == item['source_mesh_count'] and item['excluded_studio']
    hinge = bpy.data.objects.get('DETAIL_Window_HINGE_PIVOT')
    if hinge:
        checks = []
        glass = bpy.data.objects['DETAIL_OpticalGlass_4mm']
        center = sum((v.co for v in glass.data.vertices), Vector()) / len(glass.data.vertices)
        for angle in (0,75,100):
            hinge.rotation_euler.z = math.radians(angle)
            bpy.context.view_layer.update()
            checks.append({'degrees':angle, 'glass_center_world_m':list(glass.matrix_world @ center),
                           'hinge_world_m':list(hinge.matrix_world.translation),
                           'moving_children':len(hinge.children)})
        item['hinge_checks'] = checks
        item['hinge_pass'] = len(hinge.children)==52 and (Vector(checks[2]['glass_center_world_m'])-Vector(checks[0]['glass_center_world_m'])).length > .1
        item['pass'] = item['pass'] and item['hinge_pass']

(ROOT/'qa'/'export-verification-v02.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
assert all(x['pass'] for x in report['exports']), 'Export check failed; inspect QA JSON.'
print('EXPORT_VERIFIED ' + json.dumps([{k:v for k,v in x.items() if k in {'file','pass','max_size_error_mm','roundtrip_mesh_count','hinge_pass'}} for x in report['exports']]),flush=True)
