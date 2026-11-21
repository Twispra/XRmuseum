"""Export a standalone helmet GLB and check it in an empty Blender scene.

blender -b --python-exit-code 1 -P export_review.py -- --model in.blend
        --output review.glb --report glb-readback.json
"""
import argparse
import hashlib
import json
import shutil
import struct
import sys
import tempfile
from pathlib import Path

import bpy
import numpy as np
from mathutils.kdtree import KDTree


SHELL = "SL17B_YellowShell_Study"
LENS = "FP_Lens_520-004"
GEOMETRY_TOLERANCE_M = 1e-6


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
    out={"points": points,"ntri": len(indices),"materials": materials,
         "bounds": np.stack((points.min(axis=0),points.max(axis=0))),
         "uv": bool(mesh.uv_layers)}
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


def material_values(model):
    values={}
    images={}
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
            rgb=[1.,1.,1.]
        values[mat.name]=[*rgb,sockets["Alpha"].default_value,
                          sockets["Roughness"].default_value,sockets["Metallic"].default_value]
        if sockets["Normal"].is_linked:
            if sockets["Normal"].links[0].from_node.type!="BUMP":
                raise ValueError(f"Unexpected normal graph on {mat.name}")
            mat.node_tree.links.remove(sockets["Normal"].links[0])
            omitted_bumps.append(mat.name)
    return values,images,sorted(omitted_bumps)


def validate_gltf(path, before, pbr_source, images, lens_material, optics):
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
            idx=pbr["baseColorTexture"]["index"]
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
            if before[node["name"]]["uv"]:
                required.add("TEXCOORD_0")
            if not required.issubset(channels):
                raise ValueError(f"Lost vertex channel on {node['name']}")
            if node["name"]==SHELL and "COLOR_0" in channels:
                raise ValueError("Shell color attribute would be applied twice")
    return lens_output


def run(model_path, glb):
    original_sha=digest(model_path)
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
    pbr_source,images,skipped=material_values(model)
    before={obj.name:geometry(obj) for obj in model}
    lens_material=bpy.data.objects[LENS].data.materials[0]
    lens_name=lens_material.name
    lens_bsdf=lens_material.node_tree.nodes["Principled BSDF"].inputs
    optics=[lens_bsdf[k].default_value for k in ("Transmission Weight","IOR")]
    if optics[0]<=0 or optics[1]<=1:
        raise ValueError("The source lens must be transmissive")
    bpy.ops.export_scene.gltf(filepath=str(glb),export_format="GLB",export_yup=True,
                              export_normals=True,export_texcoords=True,export_animations=False,
                              export_cameras=False,export_lights=False)
    actual_optics=validate_gltf(glb,before,pbr_source,images,lens_name,optics)
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
    triangles=0
    for name,old in before.items():
        new=after[name]
        if old["ntri"]!=new["ntri"] or old["materials"]!=new["materials"]:
            raise ValueError(f"Changed triangle count or material assignments on {name}")
        if old["uv"] and not new["uv"]:
            raise ValueError(f"Missing source UV channel on {name}")
        bounds_error=float(np.max(np.abs(old["bounds"]-new["bounds"])))
        spacing=max(nearest_max(old["points"],new["points"]),nearest_max(new["points"],old["points"]))
        if max(bounds_error,spacing)>GEOMETRY_TOLERANCE_M:
            raise ValueError(f"World positions changed on {name}: {spacing}, {bounds_error}")
        maximum_bounds=max(maximum_bounds,bounds_error)
        maximum_position=max(maximum_position,spacing)
        triangles+=new["ntri"]
    if after[SHELL]["shell_groups"]!=shell_report["expected_triangle_partition"]:
        raise ValueError("Painted exterior and inside-wall triangle partitions differ")
    if original_sha!=digest(model_path):
        raise ValueError("Editable source changed during export")
    return {
        "source_file":model_path.name,"source_sha256":original_sha,
        "glb_sha256":digest(glb),"glb_bytes":glb.stat().st_size,"blender":bpy.app.version_string,
        "empty_scene_import":True,"mesh_names_verified":len(after),"triangles_verified":triangles,
        "all_source_meshes_recovered":True,"all_source_uv_channels_recovered":True,
        "source_meshes_without_uv":sorted(n for n,obs in before.items() if not obs["uv"]),
        "bounds_max_axis_difference_m":maximum_bounds,"max_bidirectional_position_distance_m":maximum_position,
        "geometry_tolerance_m":GEOMETRY_TOLERANCE_M,"materials_verified":sorted(pbr_source),
        "lens":{"material":lens_name,"transmission":actual_optics[0],"ior":actual_optics[1]},
        "shell_finishes":shell_report,"shell_face_partition_verified_on_readback":True,
        "packed_base_color_textures_verified_after_import":images,
        "bevels_applied_in_export_copy":baked,"procedural_bump_not_baked":skipped,
        "limits":"Display export; viewing software controls refraction and lighting. Procedural bump remains in .blend."
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for flag in ("model","output","report"):
        parser.add_argument("--"+flag,required=True,type=Path)
    a=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    source,dest,report=(p.resolve() for p in (a.model,a.output,a.report))
    if (source.suffix.lower(),dest.suffix.lower(),report.suffix.lower())!=(".blend",".glb",".json"):
        raise ValueError("Expected .blend, .glb and .json paths")
    if dest.exists() or report.exists():
        raise FileExistsError("Select fresh GLB and QA paths")
    with tempfile.TemporaryDirectory(prefix="sl17b-r2-export-") as temp:
        glb=Path(temp)/"local.glb"
        result=run(source,glb)
        result["glb_file"]=dest.name
        dest.parent.mkdir(parents=True,exist_ok=True)
        report.parent.mkdir(parents=True,exist_ok=True)
        written=[]
        try:
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
