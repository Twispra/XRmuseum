"""Apply bounded geometry repairs to the loaded v01, preserving the original file."""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector

ROOT = Path('E:/XRmuseum/artifacts/helmet-prototype')
scene = bpy.context.scene
assert scene.name == 'XRmuseum_Helmet_Study_v01'
assert not scene.get('recovery_v02_applied'), 'Repairs already applied; do not repeat.'
changes = []

# Correct wholly inverted closed meshes, including the studio plinths.
for ob in scene.objects:
    if ob.type != 'MESH':
        continue
    bm = bmesh.new(); bm.from_mesh(ob.data)
    if bm.calc_volume(signed=True) < -1e-14 and all(e.is_manifold for e in bm.edges):
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
        bm.to_mesh(ob.data); ob.data.update()
        changes.append('Reversed inward faces: ' + ob.name)
    bm.free()

shell = bpy.data.objects['GB_Bonnet_Hollow']
bm = bmesh.new(); bm.from_mesh(shell.data)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-7)
boundary = [e for e in bm.edges if e.is_boundary]
if boundary:
    # The v01 audit locates these defects at the front cut, not at intended ports.
    assert sum(e.calc_length() for e in boundary) < .10, 'Unexpected large boundary; inspect manually.'
    bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(shell.data); bm.free(); shell.data.update()
changes.append('Welded boolean slivers and repaired small bonnet cut boundary.')

# Evaluate the existing bonnet bevel once and clean its tiny zero-area artifacts.
bpy.context.view_layer.update()
ev = shell.evaluated_get(bpy.context.evaluated_depsgraph_get())
new_mesh = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True,
                                        depsgraph=bpy.context.evaluated_depsgraph_get())
bm = bmesh.new(); bm.from_mesh(new_mesh)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-8)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(new_mesh); bm.free()
shell.data = new_mesh
shell.modifiers.clear()
changes.append('Baked and cleaned bonnet bevel for stable export.')

# Separate nominal frame bore from the narrower rear retaining-ring aperture.
root = bpy.data.objects['Porthole_Sample_ROOT']
root['window_frame_bore_m'] = .148
root['window_effective_aperture_m'] = .1436
if 'window_clear_aperture_m' in root:
    del root['window_clear_aperture_m']
root['quality_status'] = 'Blender appearance sample; procedural materials not baked for URP; no VR interaction implementation.'

scene['recovery_v02_applied'] = True
scene['recovery_notes'] = 'Normals and bonnet boolean slivers repaired; v01 source preserved. See qa/repair-v02.json.'
scene['prototype_limitations'] = 'Interpretive proportions; simplified top port and rear fittings. No pressure engineering, UV baking, LOD or VR interaction validation.'
bpy.data.objects['DETAIL_Window_HINGE_PIVOT'].rotation_euler.z = 0
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'helmet-study-v02.blend'))
(ROOT/'qa'/'repair-v02.json').write_text(json.dumps({'changes':changes},indent=2),encoding='utf-8')
print(json.dumps({'saved':bpy.data.filepath, 'repairs':changes}))
