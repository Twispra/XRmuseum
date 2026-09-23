"""Inspect the loaded prototype. Writes small JSON reports, never modifies meshes."""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector

ROOT = Path('E:/XRmuseum/artifacts/helmet-prototype')
scene = bpy.context.scene
depsgraph = bpy.context.evaluated_depsgraph_get()

def mesh_stats(ob, evaluated=False):
    source = ob.evaluated_get(depsgraph) if evaluated else ob
    me = source.to_mesh() if evaluated else source.data
    bm = bmesh.new()
    bm.from_mesh(me)
    me.calc_loop_triangles()
    result = {
        'vertices': len(me.vertices), 'triangles': len(me.loop_triangles),
        'boundary_edges': sum(e.is_boundary for e in bm.edges),
        'nonmanifold_edges': sum(not e.is_manifold for e in bm.edges),
        'inconsistent_edges': sum(e.is_manifold and not e.is_contiguous for e in bm.edges),
        'degenerate_faces': sum(f.calc_area() < 1e-14 for f in bm.faces),
        'signed_volume_m3': bm.calc_volume(signed=True),
    }
    bm.free()
    if evaluated:
        source.to_mesh_clear()
    return result

def aabb(objects):
    points = []
    for ob in objects:
        if ob.type != 'MESH':
            continue
        ev = ob.evaluated_get(depsgraph)
        points.extend(ev.matrix_world @ Vector(v) for v in ev.bound_box)
    lo = [min(p[k] for p in points) for k in range(3)]
    hi = [max(p[k] for p in points) for k in range(3)]
    return {'min_m': lo, 'max_m': hi, 'size_mm': [(hi[k]-lo[k])*1000 for k in range(3)]}

report = {'file': bpy.data.filepath, 'blender_version': bpy.app.version_string,
          'scene': scene.name, 'units': scene.unit_settings.system,
          'metres_per_unit': scene.unit_settings.scale_length, 'collections': {}}
for name in ('01_GRAYBOX_RealScale', '02_PORTHOLE_QualitySample'):
    objects = list(bpy.data.collections[name].objects)
    details = {}
    for ob in objects:
        if ob.type == 'MESH':
            details[ob.name] = {'base': mesh_stats(ob), 'evaluated': mesh_stats(ob, True)}
    report['collections'][name] = {
        'mesh_count': len(details), 'bounds': aabb(objects), 'objects': details,
        'evaluated_triangles': sum(d['evaluated']['triangles'] for d in details.values()),
        'negative_volume_objects': [n for n,d in details.items() if d['evaluated']['signed_volume_m3'] < -1e-14],
        'nonmanifold_objects': [n for n,d in details.items() if d['evaluated']['nonmanifold_edges']],
        'degenerate_objects': [n for n,d in details.items() if d['evaluated']['degenerate_faces']],
    }
hinge = bpy.data.objects['DETAIL_Window_HINGE_PIVOT']
report['hinge'] = {'local_axis': 'Z', 'angle_deg': math.degrees(hinge.rotation_euler.z),
                   'moving_children': len(hinge.children), 'pivot_local_m': list(hinge.location)}
target = ROOT/'qa'/('audit-' + Path(bpy.data.filepath).stem + '.json')
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'report': str(target), 'collections': {n:{k:v for k,v in c.items() if k!='objects'}
                  for n,c in report['collections'].items()}, 'hinge':report['hinge']}, ensure_ascii=False))
