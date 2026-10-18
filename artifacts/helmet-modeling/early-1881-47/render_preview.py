"""CPU-only orthographic QA previews of the exported geometry (not a Unity test)."""
import numpy as np
import trimesh
from PIL import Image, ImageDraw
from pathlib import Path

here = Path(__file__).resolve().parent
output = here / "delivery"
model = trimesh.load(output / "early-1881-47.glb", force="scene")

def render(name, eye):
    size = 920
    image = Image.new("RGB", (size, size), (26, 30, 35))
    draw = ImageDraw.Draw(image)
    eye = np.array(eye, dtype=float)
    target = np.array((0, 0, .25))
    forward = target - eye
    forward /= np.linalg.norm(forward)
    right = np.cross(forward, (0, 0, 1))
    right /= np.linalg.norm(right)
    up = np.cross(right, forward)
    # Light the front of the object; the previews are evidence of geometry,
    # not a physically calibrated rendering of the museum photograph.
    light = np.array((-.4, -.65, .72))
    light /= np.linalg.norm(light)
    pending = []
    for mesh in model.dump(concatenate=False):
        rgb = np.array(mesh.visual.material.baseColorFactor[:3], dtype=float)
        tris = mesh.vertices[mesh.faces]
        normals = mesh.face_normals
        for tri, normal in zip(tris, normals):
            center = np.mean(tri, axis=0)
            depth = np.dot(center-eye, forward)
            p = [tuple((size/2 + np.dot(v-target, right)*1350,
                        size/2 - np.dot(v-target, up)*1350)) for v in tri]
            shade = .90 + .90*max(0, np.dot(normal, light))
            color = tuple(int(np.clip(v*shade, 0, 255)) for v in rgb)
            pending.append((depth, p, color))
    for _, polygon, color in sorted(pending, key=lambda item: item[0], reverse=True):
        draw.polygon(polygon, fill=color)
    image.save(output / f"{name}.png")

render("preview-front", (0, -1.5, .32))
render("preview-three-quarter", (.7, -1.5, .50))
