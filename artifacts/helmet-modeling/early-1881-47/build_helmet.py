"""Rebuild the interpretive 1839 helmet study from editable geometry definitions.

Use Python 3 with numpy and trimesh. No museum photograph is embedded.
The .obj is Unity's native import, and the .glb is the exchange copy.
"""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

import numpy as np
import trimesh

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
OUT = HERE / "delivery"
UNITY = REPO / "MuseumVR/Assets/Museum/Features/EarlyHelmet/Models"
OUT.mkdir(parents=True, exist_ok=True)
UNITY.mkdir(parents=True, exist_ok=True)

# Color is an approximation of the single museum image, not material analysis.
COLORS = {
    "aged_copper": [91, 54, 42, 255],
    "dark_bronze": [106, 65, 45, 255],
    "worn_copper": [151, 96, 62, 255],
    "glass": [41, 67, 71, 120],
    "seam": [34, 29, 25, 255],
}
MATERIALS = {
    name: trimesh.visual.material.SimpleMaterial(name=name, diffuse=rgba)
    for name, rgba in COLORS.items()
}
scene = trimesh.Scene()
metadata = {}


def put(name, mesh, material, evidence):
    assert name not in metadata, name
    assert np.isfinite(mesh.vertices).all(), name
    mesh.visual = trimesh.visual.TextureVisuals(material=MATERIALS[material])
    scene.add_geometry(mesh, geom_name=name, node_name=name)
    metadata[name] = {"material": material, "evidence": evidence}


def lathe(name, profile, material, evidence, xscale=1.0, yscale=1.0):
    sides = 64
    a = np.arange(sides) * 2 * np.pi / sides
    verts = np.array([(r * math.cos(t) * xscale,
                       r * math.sin(t) * yscale, z)
                      for z, r in profile for t in a])
    faces = []
    for i in range(len(profile) - 1):
        for j in range(sides):
            k = (j + 1) % sides
            faces.extend(((i * sides + j, i * sides + k, (i + 1) * sides + k),
                          (i * sides + j, (i + 1) * sides + k, (i + 1) * sides + j)))
    center_bottom = len(verts)
    center_top = len(verts) + 1
    verts = np.vstack((verts, [0, 0, profile[0][0]], [0, 0, profile[-1][0]]))
    for j in range(sides):
        k = (j + 1) % sides
        faces.extend(((center_bottom, k, j),
                      (center_top, (len(profile)-1)*sides+j,
                       (len(profile)-1)*sides+k)))
    mesh = trimesh.Trimesh(vertices=verts, faces=faces, process=False)
    put(name, mesh, material, evidence)


def torus(name, center, major, minor, material, evidence,
          normal=(0, 0, 1), xscale=1, yscale=1):
    a = np.arange(64) * 2 * np.pi / 64
    b = np.arange(10) * 2 * np.pi / 10
    verts = np.array([((major + minor * math.cos(t)) * math.cos(u) * xscale,
                       (major + minor * math.cos(t)) * math.sin(u) * yscale,
                       minor * math.sin(t)) for u in a for t in b])
    faces = []
    for i in range(64):
        for j in range(10):
            i2, j2 = (i + 1) % 64, (j + 1) % 10
            faces.extend(((i*10+j, i2*10+j, i2*10+j2),
                          (i*10+j, i2*10+j2, i*10+j2)))
    mesh = trimesh.Trimesh(vertices=verts, faces=faces, process=False)
    mesh.apply_transform(trimesh.geometry.align_vectors([0, 0, 1], normal))
    mesh.apply_translation(center)
    put(name, mesh, material, evidence)


def cylinder(name, center, radius, depth, material, evidence,
             direction=(0, 0, 1), sections=24):
    mesh = trimesh.creation.cylinder(radius=radius, height=depth, sections=sections)
    mesh.apply_transform(trimesh.geometry.align_vectors([0, 0, 1], direction))
    mesh.apply_translation(center)
    put(name, mesh, material, evidence)


def rod(name, a, b, radius, material, evidence):
    a, b = np.array(a), np.array(b)
    cylinder(name, (a+b)/2, radius, np.linalg.norm(b-a), material,
             evidence, b-a, sections=12)


OBS = "observed in SMG 1881-47 front image; exact geometry estimated"
EST = "interpretive completion: museum does not supply this measurement/view"
lathe("Breastplate_DishedCopper", [(.022,.130),(.030,.173),(.048,.197),(.080,.216),
                                   (.109,.211),(.137,.190),(.164,.160),(.181,.148)],
      "aged_copper", OBS, yscale=.73)
torus("Breastplate_OuterRolledEdge", (0,0,.055), .190, .008,
      "worn_copper", OBS, xscale=1.08, yscale=.79)
torus("Breastplate_InnerLip", (0,0,.175), .148, .006,
      "dark_bronze", OBS, yscale=.76)
lathe("Bonnet_RoundedCopper", [(.177,.136),(.196,.150),(.224,.156),(.266,.158),
                               (.310,.154),(.349,.140),(.383,.115),(.413,.077),
                               (.434,.035),(.439,.006)],
      "aged_copper", OBS, yscale=.89)
torus("Bonnet_BaseRolledSeam", (0,0,.189), .148, .006,
      "worn_copper", OBS, yscale=.87)
torus("Bonnet_LowerShadowSeam", (0,0,.181), .146, .003,
      "seam", EST, yscale=.86)


def port(prefix, center, normal, radius, evidence):
    c = np.array(center, dtype=float)
    n = np.array(normal, dtype=float)
    n /= np.linalg.norm(n)
    horizontal = np.array((1,0,0) if abs(n[0]) < .5 else (0,1,0))
    vertical = np.array((0,0,1))
    torus(prefix+"_RaisedSeat", c-n*.004, radius+.012, .007,
          "seam", evidence, n)
    torus(prefix+"_CopperRim", c+n*.001, radius+.009, .007,
          "worn_copper", evidence, n)
    cylinder(prefix+"_Glass", c+n*.003, radius, .002,
             "glass", evidence, n, sections=48)
    torus(prefix+"_InnerBead", c+n*.006, radius-.003, .0035,
          "dark_bronze", evidence, n)
    for i, offset in enumerate((-.019,.019), 1):
        half = math.sqrt(max(.0001, radius**2-offset**2))*.91
        a = c+n*.011+horizontal*offset
        rod(prefix+f"_VerticalBar_{i}", a-vertical*half, a+vertical*half,
            .003, "dark_bronze", evidence)
        b = c+n*.013+vertical*offset
        rod(prefix+f"_HorizontalBar_{i}", b-horizontal*half, b+horizontal*half,
            .003, "dark_bronze", evidence)
    for sign in (-1,1):
        p = c+n*.011+vertical*(radius+.015)*sign
        cylinder(prefix+f"_RimFastener_{sign}", p, .006, .004,
                 "dark_bronze", evidence, n, sections=16)


# Three grille-covered observation openings are visible in the reference. A
# side survey does not exist; their radial placement is an explicit estimate.
port("FrontPort", (0,-.151,.300), (0,-1,0), .061, OBS)
port("LeftPort", (-.147,-.008,.290), (-1,-.05,0), .041, OBS)
port("RightPort", (.147,-.008,.290), (1,-.05,0), .041, OBS)

for i, deg in enumerate(range(0,360,45), 1):
    angle = math.radians(deg)
    x, y = .184*math.sin(angle), -.137*math.cos(angle)
    z = .072 if math.cos(angle) >= 0 else .092
    direction = (math.sin(angle),-math.cos(angle),.22)
    cylinder(f"Breastplate_Fastener_{i:02d}", (x,y,z), .008, .010,
             "dark_bronze", OBS if math.cos(angle) >= 0 else EST, direction, 16)
    rod(f"Breastplate_FastenerPin_{i:02d}", (x,y,z-.010), (x,y,z+.011),
        .0025, "worn_copper", EST)
for side, side_name in ((-1,"Left"),(1,"Right")):
    x = side*.170
    cylinder(f"Shoulder_ClampBase_{side_name}", (x,-.067,.145), .014, .006,
             "dark_bronze", OBS, (side*.45,-.85,.25))
    cylinder(f"Shoulder_ClampKnob_{side_name}", (x+side*.005,-.075,.151), .007, .016,
             "worn_copper", OBS, (side*.45,-.85,.25), 16)

# The museum lists three overall dimensions without axis labels. The mapping
# X=460, Y=417, Z=465 mm is a display estimate, not an archival measurement.
raw_size = scene.extents.copy()
target_size = np.array((.460, .417, .465))
scale = target_size / raw_size
scene.apply_transform(np.diag((*scale, 1.0)))

obj, support = trimesh.exchange.obj.export_obj(scene, include_normals=True,
                                                 return_texture=True,
                                                 mtl_name="early-1881-47-runtime.mtl")
# Unity's OBJ importer honors explicit g records; trimesh emits only o records.
# Retain both so each inspectable physical part remains a separate Renderer.
obj = "\n".join(line + "\ng " + line[2:] if line.startswith("o ") else line
                for line in obj.split("\n"))
obj_path = UNITY / "early-1881-47-runtime.obj"
obj_path.write_text(obj, encoding="utf8")
for name, data in support.items():
    (UNITY / name).write_bytes(data)
glb_path = OUT / "early-1881-47.glb"
glb_path.write_bytes(scene.export(file_type="glb"))

reimport = trimesh.load(glb_path, force="scene")
assert len(reimport.geometry) == len(metadata)
assert np.isfinite(reimport.bounds).all()
assert np.allclose(reimport.bounds, scene.bounds, atol=1e-5)
triangles = sum(len(m.faces) for m in reimport.geometry.values())
report = {
    "source_object": "Science Museum Group Collection 1881-47 (1839)",
    "source_url": "https://collection.sciencemuseumgroup.org.uk/objects/co40614/first-closed-diving-helmet-and-breastplate",
    "source_image": "one front/oblique licensed image; not embedded or copied",
    "museum_overall_mm_unassigned_axes": [465,460,417],
    "model_axes": "X=460 mm width, Y=417 mm depth, Z=465 mm height; this mapping and component sizes are estimates",
    "parts": len(metadata), "triangles": triangles,
    "bounds_m_xyz": scene.bounds.tolist(),
    "glb_readback_bounds_m_xyz": reimport.bounds.tolist(),
    "part_evidence": metadata,
    "limitations": ["only one museum view", "rear and exact side profile interpretive",
                    "no verified window hinge", "air/telephone endpoints are virtual teaching anchors"],
    "model_action": "front port can be moved in Unity as a virtual inspection panel, never a claimed period hinge",
    "source_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    "obj_sha256": hashlib.sha256(obj_path.read_bytes()).hexdigest(),
    "glb_sha256": hashlib.sha256(glb_path.read_bytes()).hexdigest(),
}
(OUT / "geometry-summary.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf8")
print(f"EARLY_MODEL_OK parts={len(metadata)} triangles={triangles} bounds={scene.bounds.tolist()}")
