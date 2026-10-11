"""Read back the actual saved .blend; do not regenerate or modify it."""
import bpy
import bmesh
import json
import math
import hashlib
import sys
from pathlib import Path
from mathutils import Vector

args=sys.argv[sys.argv.index('--')+1:]
source=Path(args[0]).resolve();out=Path(args[1]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(source))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.name.startswith('STUDIO_')]
errors=[];rows=[];allverts=[]
for o in sorted(objects,key=lambda o:o.name):
    me=o.data;me.calc_loop_triangles()
    world=[o.matrix_world@v.co for v in me.vertices];allverts+=world
    bad=sum(not all(math.isfinite(c) for c in v) for v in world)
    deg=sum(t.area<1e-12 for t in me.loop_triangles)
    bm=bmesh.new();bm.from_mesh(me)
    boundary=sum(e.is_boundary for e in bm.edges)
    nonmanifold=sum(not e.is_manifold for e in bm.edges)
    bm.free()
    geometry=[tuple(round(float(c),7) for c in v) for v in world]
    # Blender may serialize faces in a different array order after a render.
    # Compare identical coordinates and oriented polygon topology, ignoring only
    # face order and cyclic starting corner; reversed winding still differs.
    def canonical_face(poly):
        p=tuple(poly.vertices)
        return min(p[i:]+p[:i] for i in range(len(p)))
    topology=sorted(canonical_face(p) for p in me.polygons)
    fingerprint=hashlib.sha256(json.dumps([geometry,topology]).encode()).hexdigest()
    row=dict(name=o.name,vertices=len(me.vertices),triangles=len(me.loop_triangles),
             uv=bool(me.uv_layers),invalid_vertices=bad,degenerate_triangles=deg,
             boundary_edges=boundary,nonmanifold_edges=nonmanifold,geometry_sha256=fingerprint)
    rows.append(row)
    if bad or deg or not me.uv_layers or o.modifiers:errors.append('mesh quality: '+o.name)
triangles=sum(x['triangles'] for x in rows)
if triangles>60000:errors.append('triangle budget')
if len([o for o in objects if o.name.startswith('Screws_Port_')])!=11:errors.append('retainer screw count')
if len([o for o in objects if o.name.startswith('Whisker_Fastener_')])!=4:errors.append('whisker screw count')
if bpy.context.scene.unit_settings.scale_length!=1:errors.append('units')
gb=bpy.data.materials['M_Polycarbonate_Clear'].node_tree.nodes['Principled BSDF']
if gb.inputs['Transmission Weight'].default_value<.99:errors.append('lens transmission')
if bpy.context.scene.get('measured_dimensions_available') is not False:errors.append('measurement disclosure')
shell=next(x for x in rows if x['name'].startswith('Shell_Yellow'))
if shell['nonmanifold_edges']!=0:errors.append('shell not closed after wall construction')
for name in ['Handle_Brass_560-014','SideBlock_Chrome_505-024','BentTube_Chrome_555-155',
             'Glass_Port_520-004','OralNasal_Mask_510-747','AirTrain_Defog_545-016',
             'CheckValve_SideBlock_Adapter','Emergency_InletStem']:
    if name not in {o.name for o in objects}:errors.append('missing part: '+name)
bounds=[max(v[i] for v in allverts)-min(v[i] for v in allverts) for i in range(3)]
report=dict(source_file=source.name,blender=bpy.app.version_string,passed=not errors,errors=errors,
            source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
            build_script_sha256=hashlib.sha256((Path(__file__).parent/'build_helmet.py').read_bytes()).hexdigest(),
            geometry_hash_method='world coordinates at 1e-7 m + sorted oriented polygon cycles',
            units='metres',forward='-Y',up='+Z',mesh_count=len(rows),triangle_count=triangles,
            generated_bounds_m=bounds,measured_real_dimensions=False,
            retainer_screws=11,whisker_fasteners=4,objects=rows)
if len(args)>2:
    other=json.loads(Path(args[2]).read_text(encoding='utf-8'))
    same=[(r['name'],r['geometry_sha256']) for r in rows]==[(r['name'],r['geometry_sha256']) for r in other['objects']]
    report['rebuild_geometry_identical']=same
    if not same:report['passed']=False;report['errors'].append('rebuild differs')
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('READBACK',json.dumps({k:v for k,v in report.items() if k!='objects'}),flush=True)
if not report['passed']:raise RuntimeError('Model validation failed')
