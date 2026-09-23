"""Run in a separate Blender process; render full images and small JPEG review frames."""
import bpy, math, sys
from pathlib import Path
ROOT = Path('E:/XRmuseum/artifacts/helmet-prototype')
scene = bpy.data.scenes['XRmuseum_Helmet_Study_v01']
bpy.context.window.scene = scene
assert scene.get('recovery_v02_applied'), 'Load helmet-study-v02.blend first.'
try:
    scene.render.engine = 'CYCLES'
except TypeError:
    raise RuntimeError('Cycles is required for the glass appearance sample.')
pref = bpy.context.preferences.addons['cycles'].preferences
try:
    pref.compute_device_type = 'OPTIX'
except TypeError:
    pref.compute_device_type = 'CUDA'
pref.get_devices()
for device in pref.devices:
    device.use = device.type != 'CPU'
scene.cycles.device = 'GPU' if any(d.use for d in pref.devices) else 'CPU'
scene.cycles.use_denoising = True
hinge = bpy.data.objects['DETAIL_Window_HINGE_PIVOT']
jobs = [
    ('CAM_01_Overview', '01_overview', 1800, 1200, 0),
    ('CAM_02_WindowCloseup', '02_window_closeup', 1600, 1600, 0),
    ('CAM_03_WindowOpen', '03_window_open', 1600, 1600, 75),
    ('CAM_04_Graybox', '04_graybox', 1400, 1600, 0),
]
formats = {i.identifier for i in scene.render.image_settings.bl_rna.properties['file_format'].enum_items}
assert {'PNG', 'JPEG'} <= formats
for camera, stem, width, height, angle in jobs:
    scene.camera = bpy.data.objects[camera]
    hinge.rotation_euler.z = math.radians(angle)
    for preview in (True, False):
        directory = ROOT/'renders'/'v02'/('preview' if preview else 'full')
        directory.mkdir(parents=True, exist_ok=True)
        scene.render.resolution_x = width
        scene.render.resolution_y = height
        scene.render.resolution_percentage = 50 if preview else 100
        scene.cycles.samples = 32 if preview else 64
        scene.render.image_settings.file_format = 'JPEG' if preview else 'PNG'
        scene.render.image_settings.quality = 82
        scene.render.filepath = str(directory/(stem + ('.jpg' if preview else '.png')))
        bpy.ops.render.render(write_still=True)
        print('DELIVERED ' + scene.render.filepath, flush=True)
