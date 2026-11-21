"""Render both saved shell meshes with the same orthographic cameras and studio.

blender -b -P render_review.py -- --base old.blend --candidate new.blend --output review.png
"""
import argparse
import hashlib
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys
from tempfile import TemporaryDirectory


WIDTH, HEIGHT = 704, 450
# Camera directions are given in the model's world frame (-Y is forward).
# The first two show the side and rear quarter appearing in references 1 and 4.
VIEWS = (
    ("side", "侧面 · 窗肩、圆顶与侧壁凸台", (1.0, .13, .18), 9),
    ("rear", "后斜视 · 后脑与颈口上沿", (.77, .94, .48), 6),
    ("detail", "另一侧近景 · 窗座到侧壁的过渡", (-1.0, -.43, .24), -4),
)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fixed_cameras(base):
    """Frame the full views on the base. Keep these exact transforms for both columns."""
    import bpy
    from mathutils import Quaternion, Vector

    bpy.ops.wm.open_mainfile(filepath=str(base))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if len(meshes) != 1:
        raise ValueError("Expected one shell mesh in the reference scene.")
    obj = meshes[0]
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    center = Vector((0., 0., .157))
    cameras = {}
    for key, _, direction, roll in VIEWS:
        d = Vector(direction).normalized()
        rotation = (-d).to_track_quat("-Z", "Y") @ Quaternion((0,0,1),math.radians(roll))
        if key == "detail":
            target, scale = Vector((-.095, -.038, .149)), .297
        else:
            right, up = rotation @ Vector((1,0,0)), rotation @ Vector((0,1,0))
            x, y = [p.dot(right) for p in points], [p.dot(up) for p in points]
            target = (center + right*((min(x)+max(x))/2-center.dot(right))
                      + up*((min(y)+max(y))/2-center.dot(up)))
            scale = max(max(x)-min(x), (max(y)-min(y))*WIDTH/HEIGHT)*1.12
        cameras[key] = (tuple(target+1.1*d), tuple(rotation), scale)
    return cameras


def render_column(path, prefix, cameras, directory):
    import bpy
    from mathutils import Quaternion, Vector

    bpy.ops.wm.open_mainfile(filepath=str(path))
    scene = bpy.context.scene
    if len(scene.objects) != 1 or scene.objects[0].type != "MESH":
        raise ValueError("The review scene must contain one shell object.")
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 28
    scene.cycles.use_denoising = True
    scene.cycles.seed = 37
    scene.render.resolution_x, scene.render.resolution_y = WIDTH, HEIGHT
    scene.render.resolution_percentage = 100
    scene.render.pixel_aspect_x = scene.render.pixel_aspect_y = 1
    scene.render.film_transparent = True
    scene.render.use_border = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Khronos PBR Neutral"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = -2.85
    world = bpy.data.worlds.new("Review illumination")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.76,.79,.84,1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .42
    scene.world = world

    focus = Vector((0.,.005,.158))
    for name, location, power, width, height, tint in (
        ("Key", (.30,-.49,.72), 68, .39,.52, (1,.96,.90)),
        ("Fill", (-.47,-.31,.39), 36, .54,.39, (.89,.94,1)),
        ("Side bounce", (.63,-.07,.22), 14, .56,.38, (1,.99,.95)),
        ("Rear strip", (.34,.48,.58), 55, .23,.56, (1,.97,.91)),
        ("Underfill", (-.12,-.18,-.30), 8, .42,.33, (.94,.96,1)),
    ):
        data = bpy.data.lights.new(name,"AREA")
        light = bpy.data.objects.new(name,data)
        scene.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (focus-light.location).to_track_quat("-Z","Y").to_euler()
        data.shape = "RECTANGLE"
        data.size, data.size_y = width, height
        data.energy, data.color = power, tint

    camera_data = bpy.data.cameras.new("Review camera")
    camera_data.type, camera_data.clip_start = "ORTHO", .001
    camera = bpy.data.objects.new("Review camera",camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for key, _, _, _ in VIEWS:
        position, orientation, scale = cameras[key]
        camera.location = position
        camera.rotation_euler = Quaternion(orientation).to_euler()
        camera_data.ortho_scale = scale
        scene.render.filepath = str(directory / f"{prefix}-{key}.png")
        bpy.ops.render.render(write_still=True)


def compose(directory, output):
    from PIL import Image, ImageDraw, ImageFont

    fonts = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    normal, bold = fonts/"msyh.ttc", fonts/"msyhbd.ttc"
    if not normal.is_file() or not bold.is_file():
        raise FileNotFoundError("Microsoft YaHei is required for the Chinese labels.")
    heading = ImageFont.truetype(str(bold), 33)
    column_font = ImageFont.truetype(str(bold), 24)
    row_font = ImageFont.truetype(str(normal), 22)
    ink = "#202a2c"
    margin, gutter, header, label, row_gap = 28, 20, 106, 36, 14
    canvas = Image.new("RGB", (2*margin+2*WIDTH+gutter,
                       header+len(VIEWS)*(HEIGHT+label+row_gap)+margin-row_gap), "#f5f5f1")
    draw = ImageDraw.Draw(canvas)
    draw.text((margin,12), "SuperLite 17B　黄色头壳", fill=ink, font=heading)
    for idx, label_text in enumerate(("上一版", "本次修订")):
        x = margin+idx*(WIDTH+gutter)
        draw.text((x,66), label_text, fill=ink, font=column_font)
        draw.line((x,103,x+WIDTH,103), fill="#b3b9b6", width=1)
    for row, (key, name, _, _) in enumerate(VIEWS):
        top = header + row*(HEIGHT+label+row_gap)
        draw.text((margin,top+3), name, fill=ink, font=row_font)
        for column, prefix in enumerate(("base", "candidate")):
            with Image.open(directory/f"{prefix}-{key}.png") as src:
                rgba = src.convert("RGBA")
            alpha = rgba.getchannel("A")
            box = alpha.getbbox()
            if not box or (key != "detail" and
                           min(box[0],box[1],WIDTH-box[2],HEIGHT-box[3]) < 3):
                raise ValueError(f"Empty or clipped full-shell view in {prefix}-{key}.")
            panel = Image.new("RGB", rgba.size, "#d8dddd")
            panel.paste(rgba, mask=alpha)
            canvas.paste(panel, (margin+column*(WIDTH+gutter), top+label))
    with output.open("xb") as stream:
        canvas.save(stream, format="PNG", optimize=True)


def main():
    if len(sys.argv) == 4 and sys.argv[1] == "--compose":
        compose(Path(sys.argv[2]),Path(sys.argv[3]))
        return
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ("base", "candidate", "output"):
        parser.add_argument("--"+key,required=True,type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    base, candidate = args.base.resolve(strict=True), args.candidate.resolve(strict=True)
    output = args.output.resolve()
    if output.exists() or output.suffix.lower() != ".png":
        parser.error("Choose a new PNG output filename.")
    output.parent.mkdir(exist_ok=True,parents=True)
    before = {path:sha(path) for path in (base,candidate)}
    python = os.environ.get("PYTHON_FOR_PILLOW") or shutil.which("python") or shutil.which("python3")
    if not python:
        raise RuntimeError("Set PYTHON_FOR_PILLOW to an interpreter containing Pillow.")
    cameras = fixed_cameras(base)
    with TemporaryDirectory(prefix="sl17b-shell-review-") as temp:
        folder = Path(temp)
        for key, path in (("base",base),("candidate",candidate)):
            render_column(path,key,cameras,folder)
        subprocess.run([python,__file__,"--compose",temp,str(output)],check=True)
    if any(sha(path) != digest for path,digest in before.items()):
        raise RuntimeError("A source changed during readback.")
    print(f"Saved comparative shell review: {output}",flush=True)


if __name__ == "__main__":
    main()
