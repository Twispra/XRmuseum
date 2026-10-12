"""Derive FBX from the reviewed saved v2; never rebuild or save the source."""
import bpy,sys,json,hashlib,math
from pathlib import Path
from mathutils import Vector

repo=Path(__file__).resolve().parents[3]
source=repo/'artifacts/helmet-modeling/superlite17b-2026-10-11/revision-side-profile-v2/delivery/superlite17b.blend'
out=Path(sys.argv[sys.argv.index('--')+1]).resolve() if '--' in sys.argv else repo/'artifacts/superlite17b-unity-integration/exports'
out.mkdir(parents=True,exist_ok=True)
fbx=out/'superlite17b-v2-runtime.fbx'
if fbx.exists():raise RuntimeError('Use a new output directory; refusing to overwrite an export.')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
expected='54a21895b807a19ccc0345a25101cb5369c0a9c976d175a40a58a528a5b8b905'
if sha(source)!=expected:raise RuntimeError('Reviewed source changed; update the scope before export.')
bpy.ops.wm.open_mainfile(filepath=str(source))
parts=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.name.startswith('STUDIO_')]
def empty(name,position=(0,0,0),parent=None):
    o=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(o);o.location=position;o.parent=parent;return o
def reparent(o,parent):
    world=o.matrix_world.copy();o.parent=parent;o.matrix_world=world
root=empty('SL17B_RuntimeRoot')
door=empty('SL17B_InspectionWindow',(0,0,0),root)
window_names={'Ring_PortChrome_560-070','ViewPort_Seal_510-260','Glass_Port_520-004'}
window_parts=[o for o in parts if o.name in window_names or o.name.startswith('Screws_Port_')]
assert len(parts)==143 and len(window_parts)==14
for o in parts:reparent(o,door if o in window_parts else root)
empty('Anchor_Up',(0,0,.1),root);empty('Anchor_Front',(0,-.1,0),root)
# Exterior endpoint assumptions; these anchors do not claim verified gas/communications routing.
empty('Anchor_AirOutlet',(-.153*.86,.052*.86,(.148+.022)*.86),root)
empty('Anchor_TelephoneOutlet',(.108*.86,.097*.86,.180*.86),root)
bpy.context.view_layer.update()
def snapshot():
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.name.startswith('STUDIO_')]
    positions=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    triangles=0
    for o in meshes:o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
    return {'meshes':len(meshes),'triangles':triangles,'bounds_m':[[min(v[i] for v in positions) for i in range(3)],[max(v[i] for v in positions) for i in range(3)]],'mesh_names':sorted(o.name for o in meshes),'all_uv':all(o.data.uv_layers for o in meshes),'finite_vertices':all(all(math.isfinite(c) for c in v) for v in positions)}
before=snapshot()
materials=[]
for m in sorted({m for o in parts for m in o.data.materials},key=lambda m:m.name):
    node=m.node_tree.nodes.get('Principled BSDF')
    materials.append({'name':m.name,'base_color_linear':list(node.inputs['Base Color'].default_value),'metallic':float(node.inputs['Metallic'].default_value),'roughness':float(node.inputs['Roughness'].default_value),'transmission':float(node.inputs['Transmission Weight'].default_value)})
bpy.ops.object.select_all(action='DESELECT')
for o in [root,*root.children_recursive]:o.hide_set(False);o.hide_viewport=False;o.hide_render=False;o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,use_mesh_modifiers=True,mesh_smooth_type='OFF',use_custom_props=True,bake_anim=False,path_mode='AUTO')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(fbx),use_anim=False)
bpy.context.view_layer.update()
after=snapshot()
errors=[]
for key in ['meshes','triangles','mesh_names','all_uv','finite_vertices']:
    if before[key]!=after[key]:errors.append('Readback differs: '+key)
bound_error=max(abs(before['bounds_m'][k][i]-after['bounds_m'][k][i]) for k in range(2) for i in range(3))
if bound_error>.00001:errors.append('Readback bounds exceed 10 micrometres')
window=bpy.data.objects.get('SL17B_InspectionWindow')
if not window or len(window.children)!=14:errors.append('Window group was not retained')
if any(o.name.startswith('STUDIO_') for o in bpy.context.scene.objects):errors.append('Studio exported')
report={'source':source.relative_to(repo).as_posix(),'source_sha256':expected,'export_sha256':sha(fbx),'blender':bpy.app.version_string,'passed':not errors,'errors':errors,'source_geometry':before,'fbx_readback':after,'bounds_max_error_m':bound_error,'materials':materials,'window_parts':sorted(o.name for o in window.children) if window else [],'limitations':'Dimension, inlet/telephone endpoint locations and virtual inspection articulation are display assumptions. Front-panel rotation is an educational inspection action, not a claim that the real bolted 17B lens opens this way. Unity reconstructs scalar materials; Blender procedural bump and coat are not baked.'}
(out/'runtime-readback.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SL17B_EXPORT',json.dumps({k:v for k,v in report.items() if k not in ['source_geometry','fbx_readback','materials','window_parts']}),flush=True)
if errors:raise RuntimeError('FBX readback failed')
assert sha(source)==expected
