"""Export a standalone helmet GLB and check it in an empty Blender scene.

blender -b --python-exit-code 1 -P export_review.py -- --model in.blend
        --output review.glb --report glb-readback.json
Use --verify existing.glb in place of --output to check a published file in place.
"""
import argparse
import hashlib
import json
import shutil
import struct
import sys
import tempfile
from collections import deque
from pathlib import Path

import bpy
import numpy as np
from mathutils.kdtree import KDTree


SHELL = "SL17B_YellowShell_Study"
LENS = "FP_Lens_520-004"
GEOMETRY_TOLERANCE_M = 1e-6
UV_TOLERANCE = 1e-6


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fingerprint_triangles(points, triangles):
    # Independent of reordering at material boundaries and UV/normal seams.
    v = np.rint(points[triangles]*1e7).astype(np.int64)
    order = np.lexsort((v[:,:,2],v[:,:,1],v[:,:,0]), axis=1)
    ordered = np.take_along_axis(v,order[:,:,None],axis=1).reshape(-1,9)
    return hashlib.sha256(ordered[np.lexsort(ordered.T[::-1])].tobytes()).hexdigest()


def geometry(obj, groups=None):
    mesh=obj.data
    mesh.calc_loop_triangles()
    points=np.array([tuple(obj.matrix_world @ v.co) for v in mesh.vertices])
    if not len(points) or not np.isfinite(points).all():
        raise ValueError(f"Invalid world geometry on {obj.name}")
    indices=np.asarray([tri.vertices[:] for tri in mesh.loop_triangles],dtype=np.int32)
    slots=np.array([tri.material_index for tri in mesh.loop_triangles])
    if groups is not None:
        slots=groups[[tri.polygon_index for tri in mesh.loop_triangles]]
    materials={m.name for m in mesh.materials if m}
    loops = np.asarray([tri.loops[:] for tri in mesh.loop_triangles], dtype=np.int32)
    channels = [points[indices]]
    for layer in mesh.uv_layers:
        uv = np.empty((len(mesh.loops), 2), dtype=np.float32)
        layer.uv.foreach_get("vector", uv.ravel())
        if not np.isfinite(uv).all():
            raise ValueError(f"Nonfinite UVs in {obj.name}/{layer.name}")
        channels.append(uv[loops])
    corners = np.concatenate(channels, axis=2)
    out={"points": points,"ntri": len(indices),"materials": materials,
         "bounds": np.stack((points.min(axis=0),points.max(axis=0))),
         "uv": len(mesh.uv_layers),
         "triangles_by_material": {
             mesh.materials[int(slot)].name: corners[slots == slot] for slot in np.unique(slots)}}
    if obj.name==SHELL:
        out["shell_groups"]={mesh.materials[int(slot)].name:{
            "triangles": int(np.count_nonzero(slots==slot)),
            "triangle_world_sha256_1e_7m": fingerprint_triangles(points,indices[slots==slot])}
            for slot in np.unique(slots)}
    return out


def nearest_max(points, other):
    tree=KDTree(len(other))
    for i,p in enumerate(other):
        tree.insert(p,i)
    tree.balance()
    return max(tree.find(p)[2] for p in points)


def match_candidates(options):
    """Match each source face to a distinct output face, including coincident faces."""
    owners, assignment = {}, {}
    for root in range(len(options)):
        queue = deque([root])
        parent = {root: None}
        while queue:
            face = queue.popleft()
            for candidate in options[face]:
                owner = owners.get(candidate)
                if owner is None:
                    while face is not None:
                        previous = assignment.get(face)
                        owners[candidate] = face
                        assignment[face] = candidate
                        face, candidate = parent[face], previous
                    break
                if owner not in parent:
                    parent[owner] = face
                    queue.append(owner)
            else:
                continue
            break
        else:
            return None
    return assignment


def compare_triangles(source, actual, label):
    """Match oriented faces by their three world positions and every corresponding UV."""
    if source.shape != actual.shape:
        raise ValueError(f"Triangle or UV channel count changed on {label}")
    positions = actual[:, :, :3]
    tree = KDTree(len(actual))
    for index, center in enumerate(positions.mean(axis=1)):
        tree.insert(center, index)
    tree.balance()
    source_ids, actual_ids = [], []
    for index, center in enumerate(source[:, :, :3].mean(axis=1)):
        # Allow for the KDTree's float32 rounding only in this broad-phase query.
        nearby = tree.find_range(center, 2 * GEOMETRY_TOLERANCE_M)
        source_ids.extend([index] * len(nearby))
        actual_ids.extend(item[1] for item in nearby)
    if not actual_ids:
        raise ValueError(f"No corresponding triangle positions on {label}")
    left = source[source_ids]
    right = actual[actual_ids]
    def normals(faces):
        xyz = faces[:, :, :3]
        return np.cross(xyz[:, 1] - xyz[:, 0], xyz[:, 2] - xyz[:, 0])
    same_winding = np.einsum("ij,ij->i", normals(left), normals(right)) > 0
    best = np.full(len(left), np.inf)
    for offset in range(3):
        # Changing the starting corner preserves winding. Reversing two corners does not.
        delta = left - np.roll(right, offset, axis=1)
        distances = np.linalg.norm(delta[:, :, :3], axis=2)
        same_positions = np.all(distances <= GEOMETRY_TOLERANCE_M, axis=1)
        uv_error = np.abs(delta[:, :, 3:]).max(axis=(1, 2), initial=0.)
        best = np.minimum(best, np.where(same_positions & same_winding, uv_error, np.inf))
    valid = best <= UV_TOLERANCE
    rows = np.asarray(source_ids)[valid]
    cols = np.asarray(actual_ids)[valid]
    errors = best[valid]
    if len(np.unique(rows)) != len(source) or len(np.unique(cols)) != len(actual):
        raise ValueError(f"Oriented triangle or corner UV mismatch on {label}")
    if len(rows) == len(source):
        return float(errors.max())
    # Close or duplicate triangles can have several valid partners. Require a bijection.
    options = [{} for _ in source]
    for row, col, error in zip(rows, cols, errors):
        options[row][col] = error
    matches = match_candidates(options)
    if matches is None:
        raise ValueError(f"Oriented triangle multiplicity changed on {label}")
    return float(max(options[row][col] for row, col in matches.items()))


def split_shell_finishes(obj):
    """Encode the binary paint/inside attribute as two standard glTF materials."""
    mesh=obj.data
    attribute=mesh.color_attributes.get("Inside_wall")
    if not attribute or attribute.domain!="CORNER" or len(mesh.materials)!=1:
        raise ValueError("Unrecognized shell finish encoding")
    exterior=mesh.materials[0]
    bsdf=exterior.node_tree.nodes["Principled BSDF"]
    mix=bsdf.inputs["Base Color"].links[0].from_node
    rough=bsdf.inputs["Roughness"].links[0].from_node
    if (mix.type!="MIX_RGB" or rough.type!="MAP_RANGE" or
            mix.inputs[0].links[0].from_node.layer_name!=attribute.name or
            rough.inputs["Value"].links[0].from_node.layer_name!=attribute.name):
        raise ValueError("The inside-wall shader graph has changed")
    finishes=[(tuple(mix.inputs[i].default_value), rough.inputs[key].default_value)
              for i,key in ((1,"To Min"),(2,"To Max"))]
    colors=np.zeros(len(attribute.data)*4,dtype=np.float32)
    attribute.data.foreach_get("color",colors)
    bits=colors.reshape(-1,4)[:,0]
    if not np.isin(bits,[0,1]).all():
        raise ValueError("The shell has a continuous finish gradient")
    face_ids=np.empty(len(mesh.polygons),dtype=np.int32)
    for p in mesh.polygons:
        values=bits[p.loop_start:p.loop_start+p.loop_total]
        if not np.all(values==values[0]):
            raise ValueError("A shell polygon crosses the finish boundary")
        face_ids[p.index]=int(values[0])
    if set(face_ids)!={0,1}:
        raise ValueError("Both shell finishes are required")
    interior=exterior.copy()
    interior.name="17B • Inside shell finish"
    mesh.materials.append(interior)
    for mat,(rgba,roughness) in zip((exterior,interior),finishes):
        sockets=mat.node_tree.nodes["Principled BSDF"].inputs
        for key,val in (("Base Color",rgba),("Roughness",roughness)):
            for link in list(sockets[key].links):
                mat.node_tree.links.remove(link)
            sockets[key].default_value=val
        mat.diffuse_color=rgba
    # Record the triangle partition independently using the source face
    # attribute, before assigning face material indices or deleting the attribute.
    expected=geometry(obj,face_ids)["shell_groups"]
    mesh.polygons.foreach_set("material_index",face_ids)
    mesh.color_attributes.remove(attribute)
    mesh.update()
    return {"basis":"Saved Inside_wall face-corner attribute: 0 exterior, 1 interior",
            "source_polygon_counts":{m.name:int(np.count_nonzero(face_ids==i))
                                     for i,m in enumerate((exterior,interior))},
            "expected_triangle_partition":expected,
            "exterior_and_interior_pbr":[{"material":m.name,"linear_rgb":list(rgba[:3]),"roughness":r}
                       for m,(rgba,r) in zip((exterior,interior),finishes)]}


def image_signature(image):
    pixels=np.empty(len(image.pixels),dtype=np.float32)
    image.pixels.foreach_get(pixels)
    if image.size[0]==0 or not len(pixels) or not np.isfinite(pixels).all():
        raise ValueError(f"Unreadable image: {image.name}")
    values=np.rint(np.clip(pixels,0,1)*65535).astype("<u2")
    return {"dimensions":list(image.size),"decoded_linear_rgba_sha256_uint16":hashlib.sha256(values).hexdigest()}


def source_texture_uv(model, material, texture):
    """Identify the rendered UV set for this direct flat base-color texture."""
    uv_name = ""
    vector = texture.inputs["Vector"]
    if texture.projection != "FLAT":
        raise ValueError(f"Unsupported base-color projection on {material.name}")
    if vector.is_linked:
        link = vector.links[0]
        node = link.from_node
        if node.type == "UVMAP" and not node.from_instancer:
            uv_name = node.uv_map
        elif not (node.type == "TEX_COORD" and link.from_socket.name == "UV" and not node.from_instancer):
            raise ValueError(f"Unsupported source base-color coordinate graph on {material.name}")
    indices = set()
    for obj in model:
        if material not in obj.data.materials[:]:
            continue
        layers = obj.data.uv_layers
        index = layers.find(uv_name) if uv_name else next(
            (i for i, layer in enumerate(layers) if layer.active_render), -1)
        if index < 0:
            raise ValueError(f"Missing source UV set for {obj.name}/{material.name}")
        indices.add(index)
    if len(indices) != 1:
        raise ValueError(f"Inconsistent source UV sets for shared material {material.name}")
    return indices.pop()


def check_texture_uv(info, expected_uv, material):
    """A glTF texture can select and transform UVs after the mesh has been exported."""
    transform = info.get("extensions", {}).get("KHR_texture_transform", {})
    channel = transform.get("texCoord", info.get("texCoord", 0))
    if type(channel) is not int or channel != expected_uv:
        raise ValueError(f"Base-color texture UV mismatch on {material}: expected TEXCOORD_{expected_uv}, got {channel}")
    offset = np.asarray(transform.get("offset", [0., 0.]))
    scale = np.asarray(transform.get("scale", [1., 1.]))
    rotation = transform.get("rotation", 0.)
    if offset.shape != (2,) or scale.shape != (2,) or not np.isscalar(rotation):
        raise ValueError(f"Invalid base-color texture transform on {material}")
    cosine, sine = np.cos(rotation), np.sin(rotation)
    linear = np.array([[cosine, -sine], [sine, cosine]]) * scale
    if not np.allclose(offset, 0., rtol=0., atol=1e-8) or not np.allclose(linear, np.eye(2), rtol=0., atol=1e-8):
        raise ValueError(f"Base-color texture transform mismatch on {material}")


def material_values(model):
    values={}
    images={}
    texture_uvs = {}
    omitted_bumps=[]
    for mat in {m for obj in model for m in obj.data.materials if m}:
        sockets=mat.node_tree.nodes["Principled BSDF"].inputs
        for name in ("Roughness","Metallic","Alpha","Transmission Weight","IOR"):
            if sockets[name].is_linked:
                raise ValueError(f"Unsupported linked input {mat.name}: {name}")
        base=sockets["Base Color"]
        rgb=list(base.default_value[:3])
        if base.is_linked:
            tex=base.links[0].from_node
            if tex.type!="TEX_IMAGE" or tex.image is None or not tex.image.packed_file:
                raise ValueError(f"Expected a direct packed base-color texture on {mat.name}")
            images[mat.name]=image_signature(tex.image)
            texture_uvs[mat.name] = source_texture_uv(model, mat, tex)
            rgb=[1.,1.,1.]
        values[mat.name]=[*rgb,sockets["Alpha"].default_value,
                          sockets["Roughness"].default_value,sockets["Metallic"].default_value]
        if sockets["Normal"].is_linked:
            if sockets["Normal"].links[0].from_node.type!="BUMP":
                raise ValueError(f"Unexpected normal graph on {mat.name}")
            mat.node_tree.links.remove(sockets["Normal"].links[0])
            omitted_bumps.append(mat.name)
    return values,images,texture_uvs,sorted(omitted_bumps)


def validate_gltf(path, before, pbr_source, images, texture_uvs, lens_material, optics):
    data=path.read_bytes()
    magic,version,size,n,chunk=struct.unpack_from("<4sIII4s",data)
    if (magic,version,size,chunk)!=(b"glTF",2,len(data),b"JSON"):
        raise ValueError("Invalid GLB file header")
    doc=json.loads(data[20:20+n])
    materials={m["name"]:m for m in doc["materials"]}
    if set(materials)!=set(pbr_source):
        raise ValueError("Exported material inventory differs")
    for name,expected in pbr_source.items():
        pbr=materials[name]["pbrMetallicRoughness"]
        actual=[*pbr.get("baseColorFactor",[1]*4),pbr.get("roughnessFactor",1),pbr.get("metallicFactor",1)]
        if not np.allclose(expected,actual,rtol=0,atol=1e-6):
            raise ValueError(f"PBR mismatch on {name}: {actual} versus {expected}")
        if name in images:
            info = pbr["baseColorTexture"]
            check_texture_uv(info, texture_uvs[name], name)
            idx=info["index"]
            texture=doc["images"][doc["textures"][idx]["source"]]
            if "bufferView" not in texture or texture.get("uri"):
                raise ValueError(f"Unembedded texture for {name}")
    ext=materials[lens_material]["extensions"]
    lens_output=[ext["KHR_materials_transmission"]["transmissionFactor"],ext["KHR_materials_ior"]["ior"]]
    if not np.allclose(lens_output,optics,rtol=0,atol=1e-6):
        raise ValueError(f"Lens optics differ: {lens_output} vs {optics}")
    if set(before)!={o["name"] for o in doc["nodes"]} or doc.get("cameras") or doc.get("animations"):
        raise ValueError("Unexpected node or animation in review asset")
    for node in doc["nodes"]:
        mesh=doc["meshes"][node["mesh"]]
        for primitive in mesh["primitives"]:
            channels=set(primitive["attributes"])
            required={"POSITION","NORMAL"}
            required.update(f"TEXCOORD_{i}" for i in range(before[node["name"]]["uv"]))
            if not required.issubset(channels):
                raise ValueError(f"Lost vertex channel on {node['name']}")
            if node["name"]==SHELL and "COLOR_0" in channels:
                raise ValueError("Shell color attribute would be applied twice")
    return lens_output


def run(model_path, glb, *, verify_existing=False):
    original_sha=digest(model_path)
    input_glb_sha = digest(glb) if verify_existing else None
    bpy.ops.wm.open_mainfile(filepath=str(model_path))
    model=list(bpy.context.scene.objects)
    if any(o.type!="MESH" for o in model):
        raise ValueError("Expected the saved, helmet-only assembly")
    names={o.name for o in model}
    if not {SHELL,LENS}.issubset(names):
        raise ValueError("Shell or lens missing")
    baked={}
    for obj in model:
        if len(obj.modifiers):
            if obj.name==SHELL or any(m.type!="BEVEL" or not m.show_viewport or not m.show_render
                                      for m in obj.modifiers):
                raise ValueError(f"Unsupported modifier on {obj.name}")
            baked[obj.name]=[m.name for m in obj.modifiers]
            bpy.context.view_layer.objects.active=obj
            for mod in list(obj.modifiers):
                bpy.ops.object.modifier_apply(modifier=mod.name)
    shell_report=split_shell_finishes(bpy.data.objects[SHELL])
    pbr_source,images,texture_uvs,skipped=material_values(model)
    before={obj.name:geometry(obj) for obj in model}
    lens_material=bpy.data.objects[LENS].data.materials[0]
    lens_name=lens_material.name
    lens_bsdf=lens_material.node_tree.nodes["Principled BSDF"].inputs
    optics=[lens_bsdf[k].default_value for k in ("Transmission Weight","IOR")]
    if optics[0]<=0 or optics[1]<=1:
        raise ValueError("The source lens must be transmissive")
    if not verify_existing:
        bpy.ops.export_scene.gltf(filepath=str(glb),export_format="GLB",export_yup=True,
                                  export_normals=True,export_texcoords=True,export_animations=False,
                                  export_cameras=False,export_lights=False)
    actual_optics=validate_gltf(glb,before,pbr_source,images,texture_uvs,lens_name,optics)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb))
    after={o.name:geometry(o) for o in bpy.context.scene.objects if o.type=="MESH"}
    if set(before)!=set(after) or len(after)!=len(bpy.context.scene.objects):
        raise ValueError("Reimported node inventory differs")
    for material,expected in images.items():
        socket=bpy.data.materials[material].node_tree.nodes["Principled BSDF"].inputs["Base Color"]
        tex=socket.links[0].from_node if socket.is_linked else None
        if not tex or tex.type!="TEX_IMAGE" or image_signature(tex.image)!=expected:
            raise ValueError(f"Texture pixels changed in {material}")
    maximum_bounds=maximum_position=0.0
    maximum_uv = 0.0
    uv_corners = 0
    triangles=0
    for name,old in before.items():
        new=after[name]
        if old["ntri"]!=new["ntri"] or old["materials"]!=new["materials"]:
            raise ValueError(f"Changed triangle count or material assignments on {name}")
        expected_groups = old["triangles_by_material"]
        actual_groups = new["triangles_by_material"]
        if old["uv"] != new["uv"] or expected_groups.keys() != actual_groups.keys():
            raise ValueError(f"UV channel count or face material groups changed on {name}")
        for material, expected in expected_groups.items():
            error = compare_triangles(expected, actual_groups[material], f"{name}/{material}")
            maximum_uv = max(maximum_uv, error)
        uv_corners += 3 * old["ntri"] * old["uv"]
        bounds_error=float(np.max(np.abs(old["bounds"]-new["bounds"])))
        spacing=max(nearest_max(old["points"],new["points"]),nearest_max(new["points"],old["points"]))
        if max(bounds_error,spacing)>GEOMETRY_TOLERANCE_M:
            raise ValueError(f"World positions changed on {name}: {spacing}, {bounds_error}")
        maximum_bounds=max(maximum_bounds,bounds_error)
        maximum_position=max(maximum_position,spacing)
        triangles+=new["ntri"]
    if after[SHELL]["shell_groups"]!=shell_report["expected_triangle_partition"]:
        raise ValueError("Painted exterior and inside-wall triangle partitions differ")
    glb_sha = digest(glb)
    if original_sha != digest(model_path) or (verify_existing and input_glb_sha != glb_sha):
        raise ValueError("An input file changed during verification")
    return {
        "source_file":model_path.name,"source_sha256":original_sha,
        "glb_sha256":glb_sha,"glb_bytes":glb.stat().st_size,"blender":bpy.app.version_string,
        "readback_mode":"existing_file" if verify_existing else "new_export",
        "empty_scene_import":True,"mesh_names_verified":len(after),"triangles_verified":triangles,
        "all_source_meshes_recovered":True,"all_source_uv_channels_recovered":True,
        "oriented_triangles_verified_by_material":triangles,"corner_uv_pairs_verified":uv_corners,
        "max_matched_corner_uv_component_error":maximum_uv,"corner_uv_tolerance":UV_TOLERANCE,
        "source_meshes_without_uv":sorted(n for n,obs in before.items() if not obs["uv"]),
        "bounds_max_axis_difference_m":maximum_bounds,"max_bidirectional_position_distance_m":maximum_position,
        "geometry_tolerance_m":GEOMETRY_TOLERANCE_M,"materials_verified":sorted(pbr_source),
        "lens":{"material":lens_name,"transmission":actual_optics[0],"ior":actual_optics[1]},
        "shell_finishes":shell_report,"shell_face_partition_verified_on_readback":True,
        "packed_base_color_textures_verified_after_import":images,
        "base_color_identity_transform_verified_on_texcoord":texture_uvs,
        "bevels_applied_in_export_copy":baked,"procedural_bump_not_baked":skipped,
        "limits":"Display export; viewing software controls refraction and lighting. Procedural bump remains in .blend."
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for flag in ("model","report"):
        parser.add_argument("--"+flag,required=True,type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--output", type=Path, help="Create and verify a new GLB.")
    mode.add_argument("--verify", type=Path, help="Verify an existing GLB without exporting.")
    a=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    source,dest,report=(p.resolve() for p in (a.model,a.output or a.verify,a.report))
    if (source.suffix.lower(),dest.suffix.lower(),report.suffix.lower())!=(".blend",".glb",".json"):
        raise ValueError("Expected .blend, .glb and .json paths")
    if (a.output and dest.exists()) or report.exists():
        raise FileExistsError("Select fresh GLB and QA paths")
    with tempfile.TemporaryDirectory(prefix="sl17b-r2-export-") as temp:
        glb = Path(temp)/"local.glb" if a.output else dest
        result=run(source,glb,verify_existing=a.verify is not None)
        result["glb_file"]=dest.name
        dest.parent.mkdir(parents=True,exist_ok=True)
        report.parent.mkdir(parents=True,exist_ok=True)
        written=[]
        try:
            if a.output:
                with dest.open("xb") as target, glb.open("rb") as data:
                    written.append(dest)
                    shutil.copyfileobj(data,target)
            with report.open("x",encoding="utf-8") as handle:
                written.append(report)
                print(json.dumps(result,indent=2,ensure_ascii=False),file=handle)
        except BaseException:
            for path in written:
                path.unlink()
            raise
    print("GLB reimport verified",result["mesh_names_verified"],result["triangles_verified"],result["glb_sha256"])


if __name__=="__main__":
    main()
