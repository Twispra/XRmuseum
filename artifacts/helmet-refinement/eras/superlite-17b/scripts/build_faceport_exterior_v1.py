"""Build two independent, provisional SL17B exterior study parts in Blender 4.5.

Use a NEW empty output directory only. The script refuses to overwrite any asset.
Reference/parameter decisions live in references/; geometry here is not measured.
"""

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if len(args) != 1:
    raise SystemExit("usage: blender -b -P build_faceport_exterior_v1.py -- NEW_OUTPUT_DIR")
ROOT = os.path.abspath(args[0])
for folder in ("sources/parts", "exports", "previews", "textures"):
    os.makedirs(os.path.join(ROOT, folder), exist_ok=True)

OUTPUTS = [
    "sources/parts/SL17B_FacePortAssembly_v1.blend",
    "sources/parts/SL17B_SideBlockControls_v1.blend",
    "exports/SL17B_FacePortAssembly_v1.glb",
    "exports/SL17B_SideBlockControls_v1.glb",
    "previews/SL17B_FacePort_front_v1.png",
    "previews/SL17B_FacePort_threequarter_v1.png",
    "previews/SL17B_SideBlock_front_v1.png",
    "previews/SL17B_SideBlock_threequarter_v1.png",
]
for rel in OUTPUTS:
    if os.path.exists(os.path.join(ROOT, rel)):
        raise SystemExit("Refusing to overwrite existing work: " + rel)


def image_material(name, color, metallic, roughness, base_image=None, rough_image=None,
                   transmission=0.0, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, alpha)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Alpha"].default_value = alpha
    bsdf.inputs["Transmission Weight"].default_value = transmission
    if base_image:
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.name = "Original_BaseColor"
        tex.image = bpy.data.images.load(os.path.join(ROOT, "textures", base_image), check_existing=True)
        tex.image.pack()
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if rough_image:
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.name = "Original_Roughness"
        tex.image = bpy.data.images.load(os.path.join(ROOT, "textures", rough_image), check_existing=True)
        tex.image.colorspace_settings.name = "Non-Color"
        tex.image.pack()
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Roughness"])
    if alpha < 1:
        mat.surface_render_method = "DITHERED"
    return mat


def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.world = bpy.data.worlds.new("REVIEW_ONLY_World")
    scene.world.color = (0.09, 0.09, 0.09)
    return scene


def assign(obj, mat, bevel=0):
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new("Soft manufactured edges (visual only)", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.affect = "EDGES"
        obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")
    return obj


def uv_all(obj):
    if obj.type != "MESH":
        return
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(island_margin=0.025)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)


def clean_normals(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()


def box(name, loc, scale, mat, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign(obj, mat, bevel)
    uv_all(obj)
    return obj


def cylinder(name, loc, radius, depth, mat, axis=(0, -1, 0), vertices=48, bevel=0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = Vector(axis).to_track_quat("Z", "Y").to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    assign(obj, mat, bevel)
    uv_all(obj)
    return obj


def perimeter(theta, rx, rz, power=2.45):
    c, s = math.cos(theta), math.sin(theta)
    return (rx * math.copysign(abs(c) ** (2 / power), c),
            rz * math.copysign(abs(s) ** (2 / power), s))


def annulus(name, outer, inner, front_y, back_y, mat, n=96):
    vertices = []
    for rx, rz, y in ((outer[0], outer[1], front_y),
                      (inner[0], inner[1], front_y),
                      (outer[0], outer[1], back_y),
                      (inner[0], inner[1], back_y)):
        for i in range(n):
            x, z = perimeter(i * 2 * math.pi / n, rx, rz)
            vertices.append((x, y, z))
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces += [(i, j, n+j, n+i),
                  (2*n+i, 3*n+i, 3*n+j, 2*n+j),
                  (i, 2*n+i, 2*n+j, j),
                  (n+i, n+j, 3*n+j, 3*n+i)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    clean_normals(obj)
    assign(obj, mat)
    uv_all(obj)
    return obj


def lens(name, rx, rz, front_y, back_y, mat, n=96):
    vertices = []
    for y in (front_y, back_y):
        for i in range(n):
            x, z = perimeter(i * 2 * math.pi / n, rx, rz)
            vertices.append((x, y, z))
    faces = [tuple(range(n-1, -1, -1)), tuple(range(n, 2*n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n+j, n+i))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    clean_normals(obj)
    assign(obj, mat, bevel=0.001)
    uv_all(obj)
    return obj


def setup_studio(scene, location, look_at, ortho=0.48):
    bpy.ops.object.camera_add(location=location)
    cam = bpy.context.object
    cam.name = "REVIEW_ONLY_Camera"
    cam.rotation_euler = (Vector(look_at) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    scene.camera = cam
    for name, loc, power, size in [
        ("Key", (-0.28, -0.28, 0.33), 12, 0.35),
        ("Fill", (0.28, -0.20, 0.10), 7, 0.28),
        ("Rim", (0.02, 0.23, 0.24), 9, 0.23),
    ]:
        bpy.ops.object.light_add(type="AREA", location=loc)
        light = bpy.context.object
        light.name = "REVIEW_ONLY_" + name
        light.data.energy = power
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (Vector(look_at) - light.location).to_track_quat("-Z", "Y").to_euler()
    return cam


def save_and_export(scene, stem, cams):
    model_meshes = [o for o in scene.objects if o.type == "MESH" and not o.name.startswith("REVIEW_ONLY")]
    for obj in model_meshes:
        if not obj.data.uv_layers:
            uv_all(obj)
    source = os.path.join(ROOT, "sources", "parts", stem + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=source)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in model_meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = model_meshes[0]
    bpy.ops.export_scene.gltf(filepath=os.path.join(ROOT, "exports", stem + ".glb"),
                              export_format="GLB", use_selection=True,
                              export_yup=True, export_apply=True)
    bpy.ops.object.select_all(action="DESELECT")
    for suffix, position, target, ortho in cams:
        cam = scene.camera
        cam.location = position
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = ortho
        scene.render.filepath = os.path.join(ROOT, "previews", suffix + ".png")
        bpy.ops.render.render(write_still=True)
    print("ASSET", stem, "meshes", len(model_meshes), "triangles",
          sum(len(p.vertices)-2 for o in model_meshes for p in o.data.polygons))


def faceport():
    scene = setup()
    chrome = image_material("Polished retainer - interpretation", (0.74, .79, .84), .88, .27,
                            rough_image="SL17B_Exterior_Roughness_v1.png")
    black = image_material("Polyurethane dark - interpretation", (.05,.06,.065), .0, .58,
                           base_image="SL17B_Exterior_BaseColor_v1.png")
    seal = image_material("Black elastomer seal - interpretation", (.018,.021,.024), 0, .78)
    glass = image_material("Polycarbonate lens - interpretive tint", (.075,.12,.15), 0, .08,
                           transmission=.65, alpha=.42)
    annulus("Port_Retainer_560-070_ESTIMATED", (.157,.109), (.133,.0845), -.011, .001, chrome)
    annulus("ViewPort_Seal_510-260_ESTIMATED", (.138,.090), (.131,.082), .002, .006, seal)
    lens("FacePort_520-004_ESTIMATED", .136, .087, -.004, .000, glass)
    # Evenly distributed screw heads are an appearance study, not a hole template.
    for i in range(14):
        t = 2*math.pi*(i+.5)/14
        x,z = perimeter(t,.146,.098)
        cylinder("Retainer_Fastener_%02d_ESTIMATED"% (i+1), (x,-.015,z), .0043,.004,
                 chrome, bevel=.0006, vertices=24)
    cylinder("NoseBlock_ExternalBoss_ESTIMATED", (0,-.020,-.110), .014,.014, chrome, bevel=.001)
    cylinder("NoseBlock_Knob_550-062_ESTIMATED", (0,-.034,-.110), .011,.014, black,
             bevel=.0007, vertices=32)
    # A simple upper visor lip gives reviewers a separate visible p/n 520-054.
    annulus("UpperVisor_520-054_VISUAL_APPROX", (.161,.113), (.153,.107), .001,.011, black)
    box("REVIEW_ONLY_DarkCardBehindClearLens", (0,.023,0), (.36,.002,.26), seal)
    setup_studio(scene, (0,-.48,.10), (0,0,0), .43)
    save_and_export(scene, "SL17B_FacePortAssembly_v1", [
        ("SL17B_FacePort_front_v1", (0,-.48,.015), (0,0,0), .43),
        ("SL17B_FacePort_threequarter_v1", (.28,-.42,.17), (0,0,0), .47)
    ])


def sideblock():
    scene = setup()
    brass = image_material("Brass housing - interpretation", (.47,.31,.11), .83, .32,
                           rough_image="SL17B_Exterior_Roughness_v1.png")
    chrome = image_material("Connector steel - interpretation", (.64,.68,.70), .83, .27,
                            rough_image="SL17B_Exterior_Roughness_v1.png")
    black = image_material("Polyurethane control knob - interpretation", (.05,.06,.065), 0, .55,
                           base_image="SL17B_Exterior_BaseColor_v1.png")
    box("SideBlock_505-024_OuterBody_ESTIMATED", (0,0,0), (.160,.059,.082), brass, .011)
    # Two externally visible controls and two generic connector stubs; no gas path.
    for i,x in enumerate((-.051,.052),1):
        cylinder("Control_%d_Base_ESTIMATED"%i, (x,-.039,.008), .026,.019, chrome,
                 bevel=.002, vertices=48)
        cylinder("Control_%d_Knob_POLYURETHANE_ESTIMATED"%i, (x,-.057,.008), .023,.025,
                 black, bevel=.003, vertices=32)
        for k in range(16):
            t = 2*math.pi*k/16
            xx = x + .022*math.cos(t)
            zz = .008 + .022*math.sin(t)
            cylinder("Control_%d_Grip_%02d_ESTIMATED"%(i,k+1), (xx,-.062,zz),
                     .0014,.014, black, bevel=0, vertices=8)
    for side,x,axis in (("L",-.092,(-1,0,0)),("R",.092,(1,0,0))):
        cylinder("Connector_%s_ExternalStub_ESTIMATED"%side, (x,.002,-.009), .016,.033,
                 chrome, axis=axis, bevel=.002, vertices=32)
    setup_studio(scene, (.15,-.35,.17), (0,0,0), .38)
    save_and_export(scene, "SL17B_SideBlockControls_v1", [
        ("SL17B_SideBlock_front_v1", (0,-.42,.01), (0,0,0), .36),
        ("SL17B_SideBlock_threequarter_v1", (.21,-.34,.16), (0,0,0), .39)
    ])


faceport()
sideblock()
