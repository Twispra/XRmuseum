"""Create ONLY a lightweight review context from the fixed Mark V R2 GLB.
Never modifies the upstream source. New output file only; not a Unity replacement.
"""
import bpy,sys,json,hashlib
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];src=Path(args[args.index('--source')+1]);out=Path(args[args.index('--output')+1])
if hashlib.sha256(src.read_bytes()).hexdigest()!='630fcd394b115ca9e4f6e41e373adc69e6e7096e3e1eb22122888d883738d64e':raise RuntimeError('Expected fixed Mark V R2 input; update provenance explicitly for another version')
if out.exists():raise RuntimeError('Context output already exists; refuse overwrite')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(src))
for ob in bpy.data.objects:
 if ob.type=='MESH' and len(ob.data.polygons)>160:
  bpy.context.view_layer.objects.active=ob;q=ob.modifiers.new('ReviewContextReduction','DECIMATE');q.ratio=.30;bpy.ops.object.modifier_apply(modifier=q.name)
for im in bpy.data.images:
 if im.type=='IMAGE' and im.size[0]>512:im.scale(512,512)
out.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.export_scene.gltf(filepath=str(out),export_format='GLB',export_yup=True,export_extras=True,export_animations=False)
report={'upstream_commit':'674d5a69014ea407e0e9a061d91cdc46e5af36f1','upstream_pr':32,'upstream_path':'artifacts/helmet-refinement/eras/mark-v/exports/mark-v-mod1.glb','upstream_sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'modifications':'decimate complex parts to 30%, resize images to512px; review context only','triangles':sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in bpy.data.objects if o.type=='MESH'),'limitations':'Not accurate collision geometry, not reviewed LOD, not part of runtime or original Mark V asset'}
(out.parent.parent/'qa/context-provenance.json').write_text(json.dumps(report,indent=2),encoding='utf-8',newline='\n');print('CONTEXT',json.dumps(report))
