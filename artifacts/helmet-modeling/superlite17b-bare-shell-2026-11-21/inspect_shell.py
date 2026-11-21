"""Inspect a saved Blender shell and optionally render a four-view contact sheet.

blender -b --python-exit-code 1 -P inspect_shell.py -- --model shell.blend
        --report readback.json [--preview preview.png]
"""
import argparse
from collections import Counter
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


NAME = "SL17B_YellowShell_Study"
WIDTH, HEIGHT = 720, 528
VIEWS = (
    ("threequarter", "前斜视", (-0.72, -1.0, 0.43)),
    ("side", "侧视", (-1.0, 0.0, 0.0)),
    ("front", "正视", (0.0, -1.0, 0.0)),
    ("under", "底后视 · 颈口与内壁", (-0.24, 0.44, -1.0)),
)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def cavity_probes(tree, lower, upper):
    """Require sizeable, straight unobstructed segments inside an otherwise enclosed space."""
    from mathutils import Vector

    span = upper - lower
    found = []
    for x_fraction in (0.40, 0.50, 0.60):
        for z_fraction in (0.53, 0.62, 0.44, 0.68, 0.37):
            for y_fraction in (0.47, 0.39, 0.54, 0.33, 0.61):
                point = lower + Vector((x_fraction*span.x, y_fraction*span.y, z_fraction*span.z))
                # A witness must be inside the body envelope and sheltered by both sides,
                # the crown, and the rear. This rules out rays passing outside the shell.
                enclosed = all(tree.ray_cast(point, direction, 1.1*span.length)[0] is not None
                               for direction in (Vector((1, 0, 0)), Vector((-1, 0, 0)),
                                                 Vector((0, 1, 0)), Vector((0, 0, 1))))
                clear = all(tree.ray_cast(point, direction, 1.1*span.length)[0] is None
                            for direction in (Vector((0, -1, 0)), Vector((0, 0, -1))))
                if clear and enclosed:
                    found.append([round(float(v), 8) for v in point])
                    break
            else:
                continue
            break
    return found


def inspect(model):
    import bpy
    import bmesh
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree

    graph = bpy.context.evaluated_depsgraph_get()
    evaluated = model.evaluated_get(graph)
    mesh = evaluated.to_mesh()
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        bm.transform(evaluated.matrix_world)
        bm.normal_update()
        seen = set()
        components = 0
        for start in bm.verts:
            if start in seen:
                continue
            seen.add(start)
            pending = [start]
            components += 1
            while pending:
                for edge in pending.pop().link_edges:
                    for neighbour in edge.verts:
                        if neighbour not in seen:
                            seen.add(neighbour)
                            pending.append(neighbour)
        edge_errors = sum(not edge.is_manifold for edge in bm.edges)
        winding_errors = sum(edge.is_manifold and not edge.is_contiguous for edge in bm.edges)
        vertex_fan_errors = sum(not vertex.is_manifold for vertex in bm.verts)
        volume = bm.calc_volume(signed=True)
        mesh.calc_loop_triangles()
        points = [evaluated.matrix_world @ v.co for v in mesh.vertices]
        triangles = [tuple(t.vertices) for t in mesh.loop_triangles]
        polygon_ids = [t.polygon_index for t in mesh.loop_triangles]
        tri_nodes = list(map(set, triangles))
        bvh = BVHTree.FromPolygons(points, triangles, all_triangles=True)
        inter = set()
        for a, b in bvh.overlap(bvh):
            if a < b and polygon_ids[a] != polygon_ids[b] and tri_nodes[a].isdisjoint(tri_nodes[b]):
                inter.add(tuple(sorted((polygon_ids[a], polygon_ids[b]))))
        low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
        high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
        degenerate = sum((points[b] - points[a]).cross(points[c] - points[a]).length < 1e-12
                         for a, b, c in triangles)
        chi = len(mesh.vertices) - len(mesh.edges) + len(mesh.polygons)
        genus = (2-chi)//2 if (components == 1 and not edge_errors and not winding_errors
                              and not vertex_fan_errors and (2-chi) % 2 == 0) else None
        probes = cavity_probes(bvh, low, high)
        scene_objects = {o.name: o.type for o in bpy.context.scene.objects}
        file_objects = {o.name: o.type for o in bpy.data.objects}
        image_assets = [{"name": im.name, "source": im.source, "packed": bool(im.packed_file)}
                        for im in bpy.data.images if im.type not in {"RENDER_RESULT", "COMPOSITING"}]
        linked_libraries = [lib.name for lib in bpy.data.libraries]
        uv_layer = mesh.uv_layers.get("ShellUV")
        uv_points = [tuple(c.uv) for c in uv_layer.data] if uv_layer else []
        uv_valid = bool(uv_points) and all(math.isfinite(v) and 0 <= v <= 1 for uv in uv_points for v in uv)
        materials = [m for m in mesh.materials if m]
        gelcoat = materials[0] if len(materials) == 1 else None
        shader = (next((n for n in gelcoat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
                  if gelcoat and gelcoat.use_nodes else None)
        display_rgb = list(gelcoat.diffuse_color[:3]) if gelcoat else None
        base_socket = shader.inputs["Base Color"] if shader else None
        upstream = base_socket.links[0].from_node if base_socket and base_socket.is_linked else None
        color = list(base_socket.default_value[:3]) if base_socket and not upstream else display_rgb
        attributes = {}
        for attr in mesh.attributes:
            if attr.name.startswith(".") or attr.domain not in {"CORNER", "FACE", "POINT"}:
                continue
            if attr.data_type in {"BOOLEAN", "INT", "FLOAT"}:
                values = Counter(round(float(element.value), 5) for element in attr.data)
            elif attr.data_type in {"BYTE_COLOR", "FLOAT_COLOR"}:
                values = Counter(tuple(round(c, 5) for c in element.color[:3]) for element in attr.data)
            else:
                continue
            attributes[attr.name] = {"domain": attr.domain, "data_type": attr.data_type,
                                     "values_and_counts_top4": values.most_common(4)}
        groups = {}
        for group in model.vertex_groups:
            ids = [v.index for v in model.data.vertices if any(x.group == group.index and x.weight > 0 for x in v.groups)]
            if ids:
                positions = [model.matrix_world @ model.data.vertices[i].co for i in ids]
                groups[group.name] = {"vertices": len(ids), "mean_xyz_m": list(sum(positions, Vector())/len(ids))}
        checks = {
            "only_one_object_in_file_and_scene": file_objects == scene_objects == {NAME: "MESH"},
            "no_linked_libraries_or_reference_images": not linked_libraries and not image_assets,
            "ShellUV_finite_in_unit_square_on_all_corners": uv_valid and len(uv_points) == len(mesh.loops),
            "one_yellow_material_on_shell": bool(color and color[0] > 0.1 and color[1] > 0.07 and
                                                 min(color[0], color[1]) > 2*color[2] and
                                                 all(p.material_index == 0 for p in mesh.polygons)),
            "solid_connected_surface": components == 1 and edge_errors == 0 and vertex_fan_errors == 0,
            "consistent_outward_winding": winding_errors == 0 and volume > 0,
            "finite_nondegenerate_triangles": all(math.isfinite(v) for p in points for v in p) and not degenerate,
            "no_nonadjacent_triangle_intersections": not inter,
            "faceport_and_neck_open_to_common_cavity": len(probes) == 3 and genus is not None and genus >= 1,
        }
        return {
            "checks": checks, "passed": all(checks.values()),
            "file_objects": file_objects, "materials": [m.name for m in materials],
            "image_assets": image_assets, "linked_libraries": linked_libraries,
            "ShellUV": {"corner_count": len(uv_points), "mesh_loop_count": len(mesh.loops),
                        "uv_min": [min(v) for v in zip(*uv_points)], "uv_max": [max(v) for v in zip(*uv_points)]},
            "material_display_rgb": display_rgb, "unlinked_shader_base_rgb": color if not upstream else None,
            "base_color_driven_by": upstream.name if upstream else None, "surface_attributes": attributes,
            "blender": bpy.app.version_string, "units": bpy.context.scene.unit_settings.system,
            "base_vertices": len(model.data.vertices), "base_polygons": len(model.data.polygons),
            "modifier_stack": [m.type for m in model.modifiers],
            "evaluated_vertices": len(points), "evaluated_polygons": len(mesh.polygons),
            "evaluated_edges": len(mesh.edges), "evaluated_triangles": len(triangles),
            "dimensions_xyz_m": list(high - low), "bounds_min_xyz_m": list(low), "bounds_max_xyz_m": list(high),
            "connected_components": components, "nonmanifold_edges": edge_errors,
            "nonmanifold_vertex_fans": vertex_fan_errors, "inconsistent_winding_edges": winding_errors,
            "signed_wall_volume_m3": volume, "degenerate_triangles": degenerate,
            "euler_characteristic": chi, "genus_of_closed_wall_surface": genus,
            "named_vertex_groups": groups,
            "nonadjacent_intersecting_polygon_pairs": len(inter), "first_intersection_pairs": sorted(inter)[:20],
            "cavity_witness_points_xyz_m": probes,
            "cavity_test": "Each witness has clear paths out toward -Y and -Z and hits the wall toward +Y, +Z and both sides.",
            "topology_note": "Genus counts surface handles. The front and neck openings together already create one handle.",
        }, points
    finally:
        bm.free()
        evaluated.to_mesh_clear()


def render_panels(points, directory):
    import bpy
    from mathutils import Vector

    scene = bpy.context.scene
    xmin, ymin, zmin = (min(p[i] for p in points) for i in range(3))
    xmax, ymax, zmax = (max(p[i] for p in points) for i in range(3))
    center = Vector(((xmin+xmax)/2, (ymin+ymax)/2, (zmin+zmax)/2))
    size = max(xmax-xmin, ymax-ymin, zmax-zmin)
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.cycles.seed = 23
    scene.render.resolution_x, scene.render.resolution_y = WIDTH, HEIGHT
    scene.render.resolution_percentage = 100
    scene.render.pixel_aspect_x = scene.render.pixel_aspect_y = 1
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Khronos PBR Neutral"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = -2.7
    world = bpy.data.worlds.new("Preview studio")
    world.use_nodes = True
    world.node_tree.nodes.get("Background").inputs["Color"].default_value = (0.64, 0.68, 0.75, 1)
    world.node_tree.nodes.get("Background").inputs["Strength"].default_value = 0.22
    scene.world = world
    for label, relative_position, power, relative_width, tint in (
        ("Softbox", (1.4,-1.8,2.4), 75, 1.9, (1,.93,.81)),
        ("Fill", (-2,-.7,.7), 48, 1.9, (.82,.89,1)),
        ("Rim", (.2,1.7,1.9), 85, 1.55, (1,.99,.90)),
        ("Bounce", (0,-1,-1.5), 15, 1.25, (.93,.95,1)),
    ):
        data = bpy.data.lights.new(label, "AREA")
        light = bpy.data.objects.new(label, data)
        scene.collection.objects.link(light)
        light.location = center + size*Vector(relative_position)
        light.rotation_euler = (center-light.location).to_track_quat("-Z", "Y").to_euler()
        data.energy = power*(size/.32)**2
        data.shape = "DISK"
        data.size = size*relative_width
        data.color = tint
    camera_data = bpy.data.cameras.new("Preview camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("Preview camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    cameras = []
    for key, _, direction in VIEWS:
        d = Vector(direction).normalized()
        rotation = (-d).to_track_quat("-Z", "Y")
        right, up = rotation @ Vector((1,0,0)), rotation @ Vector((0,1,0))
        x = [p.dot(right) for p in points]
        y = [p.dot(up) for p in points]
        target = (center + right*((max(x)+min(x))/2-center.dot(right))
                  + up*((max(y)+min(y))/2-center.dot(up)))
        fitted_width = max(max(x)-min(x), (max(y)-min(y))*WIDTH/HEIGHT)
        cameras.append((key, target+size*3*d, rotation, fitted_width))
    # The four orthographic panels share a scale and centre on each projection.
    camera_data.ortho_scale = max(view[3] for view in cameras)*1.12
    for key, location, rotation, _ in cameras:
        camera.location, camera.rotation_euler = location, rotation.to_euler()
        scene.render.filepath = str(directory / f"{key}.png")
        bpy.ops.render.render(write_still=True)


def compose(directory, output):
    from PIL import Image, ImageDraw, ImageFont

    font_file = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts/msyh.ttc"
    if not font_file.exists():
        raise RuntimeError("Preview labels require Microsoft YaHei or an equivalent CJK font.")
    heading = ImageFont.truetype(str(font_file), 28)
    label = ImageFont.truetype(str(font_file), 20)
    gap, margin, caption = 12, 22, 36
    header = 62
    canvas = Image.new("RGB", (2*WIDTH+gap+2*margin, header+2*(HEIGHT+caption)+gap+margin), "#f1f2ef")
    draw = ImageDraw.Draw(canvas)
    draw.text((margin, 12), "SuperLite 17B   /   黄色裸壳", fill="#20282c", font=heading)
    for i, (key, title, _) in enumerate(VIEWS):
        left, top = margin+(i%2)*(WIDTH+gap), header+(i//2)*(HEIGHT+caption+gap)
        with Image.open(directory / f"{key}.png") as source:
            rgba = source.convert("RGBA")
            if not rgba.getchannel("A").getbbox():
                raise RuntimeError("Empty rendered panel: "+key)
            panel = Image.new("RGB", rgba.size, "#d1d7d7")
            panel.paste(rgba, mask=rgba.getchannel("A"))
        canvas.paste(panel, (left,top))
        draw.text((left+4, top+HEIGHT+3), title, fill="#283336", font=label)
    with output.open("xb") as stream:
        canvas.save(stream, format="PNG", optimize=True)


def main():
    if len(sys.argv) == 4 and sys.argv[1] == "--compose":
        compose(Path(sys.argv[2]), Path(sys.argv[3]))
        return
    import bpy

    parser = argparse.ArgumentParser()
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    model, report = args.model.resolve(strict=True), args.report.resolve()
    preview = args.preview.resolve() if args.preview else None
    for path in filter(None, (report, preview)):
        if path.exists():
            raise FileExistsError(f"Choose a new output path: {path}")
        path.parent.mkdir(parents=True, exist_ok=True)
    digest = sha256(model)
    bpy.ops.wm.open_mainfile(filepath=str(model))
    result, points = inspect(bpy.data.objects[NAME])
    result["source_file"], result["source_sha256"] = model.name, digest
    if preview:
        host_python = os.environ.get("PYTHON_FOR_PILLOW") or shutil.which("python") or shutil.which("python3")
        if not host_python:
            raise RuntimeError("Set PYTHON_FOR_PILLOW to a Python executable with Pillow.")
        with tempfile.TemporaryDirectory(prefix="km17b-shell-") as temp:
            render_panels(points, Path(temp))
            subprocess.run([host_python, __file__, "--compose", temp, str(preview)], check=True)
        result["preview_sha256"] = sha256(preview)
    result["input_unchanged_after_readback"] = sha256(model) == digest
    with report.open("x", encoding="utf-8") as out:
        json.dump(result, out, indent=2, ensure_ascii=False)
        out.write("\n")
    print(json.dumps(result["checks"]), flush=True)
    if not result["passed"] or not result["input_unchanged_after_readback"]:
        raise ValueError("Geometry inspection failed. Read the report for details.")


if __name__ == "__main__":
    main()
