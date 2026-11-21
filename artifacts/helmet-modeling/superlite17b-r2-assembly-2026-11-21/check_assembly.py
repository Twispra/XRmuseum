"""Verify the editable 500-010 exhibit assembly from two independently opened files.

blender -b --python-exit-code 1 -P check_assembly.py -- --shell R2.blend
        --model assembled.blend --report fresh-report.json
"""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


SHELL = "SL17B_YellowShell_Study"
VISOR = "Visor_520-054"
APPROVED = "800f5175684cf9a31f83344bb03c16af2a079f7e77391a27ca1b17bdbccf777e"
MARKINGS = {"SB_Model designation", "REG_Purge marking"}
MIN_DOUBLE_AREA_M2 = 1e-12
EXPECTED = {
    "window_and_lifting_handle": [VISOR,"FP_PortRetainer_560-070","FP_Lens_520-004","FP_PortSeal_510-260",
                                  "FP_BrassHandle_560-014","FP_NoseGuide_550-116","FP_NoseBlockKnob_550-062"],
    "regulator_and_water_dump": ["REG_Body_505-027","REG_Chrome cover","REG_Purge button","EXH_Water dump body_550-063"],
    "starboard_sideblock_and_bent_tube": ["SB_Chromed main casting_505-024","SB_Steady flow_520-524",
                                          "SB_Emergency valve_520-525","SB_Nonreturn_555-195.valve",
                                          "TUBE_Bent supply_555-155","TUBE_Regulator end nut","TUBE_Side block end nut"],
    "quad_exhaust": ["EXH_Quad main body_510-760","EXH_Whisker_510-786","EXH_Whisker_510-787",
                     "FP_KidneyPlate_540-015_Port","FP_KidneyPlate_540-015_Starboard"],
    "weights_and_neck": ["Continuous rear weight | 560-005","Port cast weight | 560-019","Starboard cast weight | 560-023",
                         "Bottom O-ring | 510-446","Stainless split neck clamp | 505-055","Drawstring neck dam | 510-533",
                         "Urethane yoke | 520-117","Rear hinge mounting tab | 540-157","Flanged alignment sleeve | 550-339",
                         "Rear yoke transverse hinge bolt | 530-034","Pull-pin latch catch | 560-051",
                         "Yoke strap | 505-134","Internal chin strap | 505-130"],
    "cushion_air_train_and_oral_nasal": ["Cushion_Right_ear_pocket","Cushion_Left_ear_pocket","Cushion_center_top",
                                        "Cushion_rear_neck_roll","LVON_510-724_hollow_mask","AirTrain_545-016_perforated_arc"],
    "two_post_communications": ["CommsPost_1_515-035","CommsPost_2_515-035","Comms_unused_MWPC_550-043_plug",
                               "Microphone_515-029","Earphone_Left_speaker_face","Earphone_Right_speaker_face"],
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read_column(items, name, size, dtype):
    data=np.empty(len(items)*size,dtype=dtype)
    items.foreach_get(name,data)
    return data.reshape(-1,size)


def scalar(value):
    if isinstance(value,bpy.types.ID):
        return [value.bl_rna.identifier,value.name_full]
    if value is None or isinstance(value,(bool,int,float,str)):
        return value
    return list(value)


def graph_state(mat):
    nodes=[]
    for node in sorted(mat.node_tree.nodes,key=lambda item:item.name):
        props={name:scalar(getattr(node,name)) for name in
               ("layer_name","blend_type","data_type","interpolation_type","clamp","use_clamp","operation")
               if hasattr(node,name)}
        nodes.append([node.name,node.bl_idname,props,
                      [[s.identifier,scalar(getattr(s,"default_value",None))] for s in node.inputs],
                      [[s.identifier,scalar(getattr(s,"default_value",None))] for s in node.outputs]])
    links=sorted([ln.from_node.name,ln.from_socket.identifier,ln.to_node.name,ln.to_socket.identifier]
                 for ln in mat.node_tree.links)
    return [mat.name,tuple(mat.diffuse_color),mat.roughness,mat.metallic,mat.surface_render_method,nodes,links]


def shell_signature(obj):
    mesh=obj.data
    local=read_column(mesh.vertices,"co",3,np.float32)
    transform=np.asarray(obj.matrix_world,dtype=np.float64)
    world=local.astype(np.float64) @ transform[:3,:3].T+transform[:3,3]
    layer=mesh.color_attributes["Inside_wall"]
    sharp=mesh.attributes["sharp_edge"]
    # Include weights with their mesh vertex indices, not merely the group labels.
    groups=[(group.name,[(v.index,w.weight) for v in mesh.vertices for w in v.groups if w.group==group.index])
            for group in obj.vertex_groups]
    state={
        "local_vertices":sha(local.tobytes()),"world_vertices":sha(world.tobytes()),
        "matrix_world":transform.tolist(),"vertex_count":len(mesh.vertices),
        "edges":sha(read_column(mesh.edges,"vertices",2,np.int32).tobytes()),
        "corner_vertices":sha(read_column(mesh.loops,"vertex_index",1,np.int32).tobytes()),
        "corner_edges":sha(read_column(mesh.loops,"edge_index",1,np.int32).tobytes()),
        "face_sizes":sha(read_column(mesh.polygons,"loop_total",1,np.int32).tobytes()),
        "uv":{uv.name:sha(read_column(uv.data,"uv",2,np.float32).tobytes()) for uv in mesh.uv_layers},
        "inner_outer_finish":[layer.domain,layer.data_type,sha(read_column(layer.data,"color",4,np.float32).tobytes())],
        "face_smoothing":sha(read_column(mesh.polygons,"use_smooth",1,np.bool_).tobytes()),
        "sharp_edges":sha(read_column(sharp.data,"value",1,np.bool_).tobytes()),
        "face_materials":sha(read_column(mesh.polygons,"material_index",1,np.int32).tobytes()),
        "shader_graphs":sha(json.dumps([graph_state(m) for m in mesh.materials],sort_keys=True).encode()),
        "named_landmarks_and_weights":sha(json.dumps(groups).encode()),
        "modifier_stack":[[m.name,m.type] for m in obj.modifiers],
    }
    return state


def object_geometry(obj, depsgraph):
    evaluated=obj.evaluated_get(depsgraph)
    mesh=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=depsgraph)
    bm=bmesh.new()
    try:
        mesh.calc_loop_triangles()
        bm.from_mesh(mesh)
        bm.transform(evaluated.matrix_world)
        nonmanifold_edges=sum(not e.is_manifold for e in bm.edges)
        n_boundary=sum(e.is_boundary for e in bm.edges)
        world=[evaluated.matrix_world @ v.co for v in mesh.vertices]
        face_tris=[tuple(t.vertices) for t in mesh.loop_triangles]
        polygon_ids=[t.polygon_index for t in mesh.loop_triangles]
        tri_sets=[set(ids) for ids in face_tris]
        edge_pairs=read_column(mesh.edges,"vertices",2,np.int32)
        points=np.array([tuple(v) for v in world],dtype=np.float64).reshape(-1,3)
        finite=bool(len(points) and np.isfinite(points).all())
        uv_finite=all(np.isfinite(read_column(u.data,"uv",2,np.float32)).all() for u in mesh.uv_layers)
        zero_edges=0
        bad_tris=[]
        intersections=set()
        if finite and face_tris:
            edge_vec=points[edge_pairs[:,0]]-points[edge_pairs[:,1]]
            zero_edges=int(np.sum(np.einsum("ij,ij->i",edge_vec,edge_vec)==0))
            tri=points[np.asarray(face_tris,dtype=np.int32)]
            area2=np.linalg.norm(np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]),axis=1)
            bad_tris=np.flatnonzero(area2<MIN_DOUBLE_AREA_M2).tolist()
            tree=BVHTree.FromPolygons(world,face_tris,all_triangles=True)
            for a,b in tree.overlap(tree):
                if a<b and polygon_ids[a]!=polygon_ids[b] and tri_sets[a].isdisjoint(tri_sets[b]):
                    intersections.add(tuple(sorted((polygon_ids[a],polygon_ids[b]))))
        return {
            "type":obj.type,"collections":sorted(c.name for c in obj.users_collection),
            "role":"printed marking carrier" if obj.name in MARKINGS else "physical geometry",
            "vertices":len(points),"edges":len(mesh.edges),"polygons":len(mesh.polygons),"triangles":len(face_tris),
            "finite_world_vertices":finite,"zero_length_edges":zero_edges,
            "degenerate_triangles":len(bad_tris),"sample_degenerate_triangle_indices":bad_tris[:6],
            "nonmanifold_edges":nonmanifold_edges,"boundary_edges":n_boundary,
            "signed_volume_m3":bm.calc_volume(signed=True),
            "nonmanifold_vertex_fans":sum(not v.is_manifold for v in bm.verts),
            "inconsistent_winding_edges":sum(e.is_manifold and not e.is_contiguous for e in bm.edges),
            "self_intersecting_polygon_pairs":len(intersections),
            "sample_intersecting_polygon_pairs":sorted(intersections)[:6],"finite_uvs":bool(uv_finite),
            "uv_layers":[layer.name for layer in mesh.uv_layers],
            "materials":[m.name if m else None for m in mesh.materials],
        }
    finally:
        bm.free()
        evaluated.to_mesh_clear()


def visor_fit(objects, graph):
    """Inspect the saved insert in world space, including the actual lens and inner shell."""
    insert = bpy.context.scene.objects.get(VISOR)
    if insert is None:
        return {"passed": False, "missing": VISOR}

    def world_tree(obj):
        evaluated = obj.evaluated_get(graph)
        mesh = evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            points = [evaluated.matrix_world @ v.co for v in mesh.vertices]
            triangles = [tuple(t.vertices) for t in mesh.loop_triangles]
            return BVHTree.FromPolygons(points, triangles, all_triangles=True), np.array(points)
        finally:
            evaluated.to_mesh_clear()

    tree, points = world_tree(insert)
    ceiling, _ = world_tree(bpy.data.objects[SHELL])
    low, high = points.min(axis=0), points.max(axis=0)
    collisions = []
    for obj in objects:
        if obj.type != "MESH" or obj == insert:
            continue
        evaluated = obj.evaluated_get(graph)
        bounds = np.array([evaluated.matrix_world @ Vector(c) for c in evaluated.bound_box])
        if np.all(bounds.max(axis=0) >= low) and np.all(bounds.min(axis=0) <= high):
            other = ceiling if obj.name == SHELL else world_tree(obj)[0]
            if tree.overlap(other):
                collisions.append(obj.name)
    # Look up from each visor vertex to the inside ceiling. The rear bearing
    # area must have a seat on both sides of the centreline.
    seated = []
    below_ceiling = True
    for x, y, z in points:
        hit, normal, _, distance = ceiling.ray_cast((x, y, z), (0, 0, 1), .08)
        if hit is None or normal.z >= 0:
            below_ceiling = False
        elif distance <= .00025 and y > -.055:
            seated.append(x)
    # Sample sight lines from two nominal eye points to the central lens.
    # These fixed display-scale positions are not a wearer field-of-view test.
    _, glass = world_tree(bpy.data.objects["FP_Lens_520-004"])
    targets = glass[(abs(glass[:, 0]) < .072) & (glass[:, 2] > .165) & (glass[:, 2] < .225)][::8]
    blocked = 0
    for eye_x in (-.032, .032):
        eye = Vector((eye_x, -.025, .193))
        for target in targets:
            ray = Vector(target)-eye
            if tree.ray_cast(eye, ray, ray.length)[0] is not None:
                blocked += 1
    front = points[points[:, 1] < -.080]
    rear = points[points[:, 1] > -.050]
    fan_shape = bool(len(front) and len(rear) and np.ptp(front[:, 0]) > .105
                     and np.ptp(front[:, 0]) > 1.5*np.ptp(rear[:, 0]))
    heights, thicknesses = [], []
    for x in (-.055, 0., .055):
        up = Vector((0, 0, 1))
        lower = tree.ray_cast((x, -.090, .150), up, .15)[0]
        if lower is not None:
            upper = tree.ray_cast(lower+up*.00001, up, .01)[0]
            if upper is not None:
                heights.append(lower.z)
                thicknesses.append(upper.z-lower.z)
    curved_sheet = (len(heights) == 3 and heights[1] > max(heights[0], heights[2])+.004
                    and all(.0003 < t < .003 for t in thicknesses))
    seat_span = float(np.ptp(seated)) if seated else 0.
    finish = insert.active_material
    output = next((n for n in finish.node_tree.nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output),
                  None) if finish and finish.use_nodes else None
    surface = output.inputs["Surface"].links if output else []
    shader = surface[0].from_node if surface else None
    opaque = False
    if shader and shader.type == "BSDF_PRINCIPLED":
        alpha, transmission, color = [shader.inputs[k] for k in ("Alpha", "Transmission Weight", "Base Color")]
        opaque = (not any(s.is_linked for s in (alpha, transmission, color))
                  and alpha.default_value >= .99 and transmission.default_value <= .01
                  and max(color.default_value[:3]) <= .15)
    tests = {
        "dark_opaque_finish": opaque,
        "curved_forehead_insert": bool(fan_shape and curved_sheet and low[2] > .202 and high[2]-low[2] > .025),
        "wholly_inside_brow": bool(below_ceiling and high[1] < 0 and low[1] > glass[:, 1].max()),
        "rear_tongue_seated": bool(seated and min(seated) < -.008 and max(seated) > .008),
        "no_inter_part_triangle_crossings": not collisions,
        "clear_central_sight_lines": len(targets) >= 12 and blocked == 0,
    }
    return {"passed": all(tests.values()), "checks": tests, "world_bounds_m": [low.tolist(), high.tolist()],
            "brow_section_lower_heights_m": heights, "brow_section_vertical_thicknesses_m": thicknesses,
            "rear_seat_width_within_0_25_mm_m": seat_span, "intersecting_objects": collisions,
            "diagnostic_eye_to_lens_rays": 2*len(targets), "rays_blocked_by_visor": blocked}


def run():
    parser=argparse.ArgumentParser(description=__doc__)
    for flag in ("shell","model","report"):
        parser.add_argument("--"+flag,required=True,type=Path)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    shell,model,report=(getattr(args,k).resolve() for k in ("shell","model","report"))
    if report.exists() or report in (shell,model):
        raise FileExistsError("Choose a new report path")
    hashes=[sha(p.read_bytes()) for p in (shell,model)]
    if hashes[0]!=APPROVED:raise ValueError("Incorrect approved R2 input")
    bpy.ops.wm.open_mainfile(filepath=str(shell))
    original=shell_signature(bpy.data.objects[SHELL])
    bpy.ops.wm.open_mainfile(filepath=str(model))
    scene=bpy.context.scene
    saved=shell_signature(bpy.data.objects[SHELL])
    differences=[name for name in original if original[name]!=saved[name]]
    objects=sorted(scene.objects,key=lambda o:o.name)
    graph=bpy.context.evaluated_depsgraph_get()
    records={o.name:object_geometry(o,graph) for o in objects if o.type in {"MESH","FONT","CURVE"}}
    not_model_objects=[o.name for o in objects if o.name not in records]
    missing={group:[n for n in names if n not in records] for group,names in EXPECTED.items()}
    group_counts=Counter(c for item in records.values() for c in item["collections"])
    raw_problems={}
    for name,row in records.items():
        checks=("zero_length_edges","degenerate_triangles","nonmanifold_edges","nonmanifold_vertex_fans",
                "inconsistent_winding_edges","self_intersecting_polygon_pairs")
        problems={metric:row[metric] for metric in checks if row[metric]}
        if not row["finite_world_vertices"] or not row["triangles"]:problems["empty_or_nonfinite"]=True
        if not row["uv_layers"]:problems["missing_uv"]=True
        if not row["finite_uvs"]:problems["nonfinite_uv"]=True
        if not row["materials"] or None in row["materials"]:problems["missing_material"]=True
        if row["signed_volume_m3"]<=0:problems["nonpositive_world_volume"]=True
        if problems:raw_problems[name]=problems
    physical_problems={n:r for n,r in raw_problems.items() if n not in MARKINGS}
    # Both the two thin marking carriers and the other model parts get the
    # same geometry tests. Also identify the packed print used by each carrier.
    print_problems={n:r for n,r in raw_problems.items() if n in MARKINGS}
    print_images={}
    for name in sorted(MARKINGS & records.keys()):
        material=bpy.data.objects[name].data.materials[0]
        texture_inputs=[link.from_node.image for link in material.node_tree.links
                        if link.from_node.type=="TEX_IMAGE" and link.to_node.type=="BSDF_PRINCIPLED"
                        and link.to_socket.name=="Base Color" and link.from_node.image]
        print_images[name]=[{"image":im.name,"size":list(im.size),"packed":bool(im.packed_file),
                             "sha256":sha(im.packed_file.data) if im.packed_file else None}
                            for im in texture_inputs]
    glass=bpy.data.objects["FP_Lens_520-004"]
    lens_mat=glass.data.materials[0]
    output=next(n for n in lens_mat.node_tree.nodes if n.type=="OUTPUT_MATERIAL" and n.is_active_output)
    shader=output.inputs["Surface"].links[0].from_node
    if shader.type!="BSDF_PRINCIPLED":
        raise ValueError("Expected the lens's transmitting shader to feed the active material output")
    optics={k:float(shader.inputs[k].default_value) for k in ("Transmission Weight","IOR","Roughness")}
    assets=bpy.utils.blend_paths(absolute=False,packed=False)
    extra_file_objects=sorted(set(o.name for o in bpy.data.objects)-set(o.name for o in objects))
    screw_counts={p:sum(n.startswith(p) for n in records) for p in
                  ("FP_RetainerScrew_","FP_WhiskerScrew_","FP_PortPlug_530-052_","FP_WhiskerSpacer_550-061_")}
    hidden=[o.name for o in objects if o.hide_render or o.hide_viewport]
    visor = visor_fit(objects, graph)
    tests={
        "opaque_visor_fitted_inside_brow":visor["passed"],
        "approved_shell_identical":not differences,
        "all_expected_subsystems_present":not any(missing.values()),
        "retainer_whisker_spacer_and_plug_counts":list(screw_counts.values())==[11,4,2,4],
        "positive_lens_transmission_and_polycarbonate_ior":optics["Transmission Weight"]>.95 and abs(optics["IOR"]-1.586)<.01,
        "all_physical_parts_have_finite_closed_consistent_geometry":not physical_problems,
        "marking_carriers_closed_with_embedded_prints":not print_problems and set(print_images)==MARKINGS
            and all(imgs and all(i["packed"] for i in imgs) for imgs in print_images.values()),
        "only_display_model_objects_in_file":not not_model_objects and not extra_file_objects and not hidden,
        "no_external_libraries_or_unpacked_assets":not bpy.data.libraries and not any(assets),
        "saved_source_files_unchanged":hashes==[sha(p.read_bytes()) for p in (shell,model)],
    }
    data={"blender":bpy.app.version_string,"shell_file":shell.name,"shell_sha256":hashes[0],
          "assembly_file":model.name,"assembly_sha256":hashes[1],"passed":all(tests.values()),"checks":tests,
          "counts":{"objects":len(objects),"evaluated_geometry_objects":len(records),
                    "native_types":dict(Counter(o.type for o in objects)),"collections":dict(group_counts),
                    "evaluated_world_triangles":sum(r["triangles"] for r in records.values()),
                    "assigned_materials":len(set(m for r in records.values() for m in r["materials"]))},
          "shell_signature_before":original,"shell_signature_after":saved,"shell_signature_differences":differences,
          "subsystems":EXPECTED,"missing_parts":missing,"faceport_fastener_counts":screw_counts,
          "visor_fit":visor,
          "lens_material":lens_mat.name,"lens_principled_settings":optics,"external_assets":list(assets),
          "unused_file_objects":extra_file_objects,"unexpected_scene_objects":not_model_objects,"hidden_objects":hidden,
          "per_object_problems_including_lettering":raw_problems,"physical_geometry_problems":physical_problems,
          "marking_problems":print_problems,"marking_images":print_images,"per_object_geometry":records,
          "method":"Per object: evaluated mesh in saved world coordinates; Blender triangle BVH compares distinct polygons "
                   "with no shared triangle vertex. Winding and edge/manifold tests use BMesh. Degenerate testing uses "
                   "double-area < 1e-12 square metres. Surface lettering is reported with its exact native type and counts. "
                   "The shell is fingerprinted twice, using saved topology, world/local positions, all UVs, inside/outside "
                   "colour, sharpness/smoothing, landmark groups, material indices and shader graph.",
          "scope":"Geometry, presence and material readback. This does not measure mechanical tolerances or gas/seal "
                  "performance, prove all inter-part contacts, or test Unity/VR."}
    report.parent.mkdir(parents=True,exist_ok=True)
    with report.open("x",encoding="utf-8") as output:
        json.dump(data,output,indent=2,ensure_ascii=False)
        output.write("\n")
    print("ASSEMBLY_READBACK",json.dumps(tests),flush=True)
    if not data["passed"]:raise ValueError("Readback failed; see report")


if __name__=="__main__":
    run()
