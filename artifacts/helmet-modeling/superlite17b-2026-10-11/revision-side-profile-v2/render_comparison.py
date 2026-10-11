"""Render actual saved before/after models from an identical camera; no saves."""
import bpy,sys
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
out=Path(args[2]).resolve();out.mkdir(parents=True,exist_ok=True)
for source,label in zip(args[:2],['before','after']):
    bpy.ops.wm.open_mainfile(filepath=str(Path(source).resolve()))
    s=bpy.context.scene;c=s.camera
    c.location=(-.903,.4042,.387)
    c.rotation_euler=(Vector((0,.01548,.1634))-c.location).to_track_quat('-Z','Y').to_euler()
    c.data.type='ORTHO';c.data.ortho_scale=.4386
    s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True
    s.render.resolution_x=900;s.render.resolution_y=900;s.render.resolution_percentage=100
    s.render.image_settings.file_format='PNG';s.render.filepath=str(out/(label+'.png'))
    bpy.ops.render.render(write_still=True)
    print('COMPARISON_RENDER',label,flush=True)
