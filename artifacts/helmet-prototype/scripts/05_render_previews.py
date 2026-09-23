"""Render the Blender-MCP-built scene without blocking the interactive Blender UI."""
import bpy, math, sys
from pathlib import Path
root=Path('E:/XRmuseum/artifacts/helmet-prototype')
scene=bpy.data.scenes['XRmuseum_Helmet_Study_v01']
bpy.context.window.scene=scene
scene.render.engine='CYCLES'
try:
    pref=bpy.context.preferences.addons['cycles'].preferences
    supported=[i.identifier for i in pref.bl_rna.properties['compute_device_type'].enum_items]
    # Dynamic backends can be omitted by RNA; assignment is checked by Blender.
    try: pref.compute_device_type='OPTIX'
    except (TypeError,ValueError): pref.compute_device_type='CUDA'
    pref.get_devices()
    gpu=False
    for d in pref.devices:
        d.use=d.type!='CPU';gpu|=d.use
    if gpu: scene.cycles.device='GPU'
    print('RENDER DEVICES',[(d.name,d.type,d.use) for d in pref.devices],flush=True)
except Exception as e:
    print('GPU fallback',str(e),flush=True)
scene.cycles.samples=64
scene.cycles.use_denoising=True
scene.render.resolution_percentage=100
hinge=bpy.data.objects['DETAIL_Window_HINGE_PIVOT']
jobs=[('CAM_01_Overview','01_overview.png',1800,1200,0),
      ('CAM_02_WindowCloseup','02_window_closeup.png',1600,1600,0),
      ('CAM_03_WindowOpen','03_window_open.png',1600,1600,75),
      ('CAM_04_Graybox','04_graybox.png',1400,1600,0)]
requested=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for cam,name,w,h,angle in jobs:
    if requested and name not in requested: continue
    scene.camera=bpy.data.objects[cam]
    hinge.rotation_euler.z=math.radians(angle)
    scene.render.resolution_x=w;scene.render.resolution_y=h
    scene.render.filepath=str(root/'renders'/name)
    bpy.ops.render.render(write_still=True)
    print('FINISHED',name,flush=True)
