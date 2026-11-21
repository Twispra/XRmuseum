"""Inspect a saved 17B assembly in Blender. Inputs stay read-only.

blender -b --python-exit-code 1 -P check_interfaces.py -- --model helmet.blend --report new.json
"""
import argparse
import hashlib
import itertools
import json
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils.bvhtree import BVHTree


SHELL = "SL17B_YellowShell_Study"
TUBE = "TUBE_Bent supply_555-155"
FRAME = "FP_PortRetainer_560-070"
LENS = "FP_Lens_520-004"
MIC = "Microphone_515-029"
MIC_LEAD = "Comms_microphone_internal_lead"
DEFROSTER = ("AirTrain_545-016_perforated_arc", "AirTrain_starboard_feed_shoulder")
SIDES = {"Port": "EXH_Whisker_510-787", "Starboard": "EXH_Whisker_510-786"}
FERRULES = ("TUBE_Side block ferrule", "TUBE_Regulator ferrule")
SHELL_SEATS = {
    "REG_Mount_550-038": "regulator mounting shoulder at existing service bore",
    "REG_Shell gasket": "regulator gasket at forward shell seat",
    "REG_Body_505-027": "lower inlet collar at regulator service bore",
    "EXH_Water dump body_550-063": "water dump body through lower service bore",
    "SB_Mounting boss": "side-block boss on shell mounting surface",
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def ordered(a, b):
    return tuple(sorted((a,b)))


class Geometry:
    def __init__(self, obj, graph):
        evaluated = obj.evaluated_get(graph)
        mesh = evaluated.to_mesh()
        try:
            matrix = np.asarray(evaluated.matrix_world)
            local = np.asarray([v.co[:] for v in mesh.vertices])
            self.points = local @ matrix[:3,:3].T + matrix[:3,3]
            mesh.calc_loop_triangles()
            self.triangles = np.asarray([tri.vertices[:] for tri in mesh.loop_triangles], dtype=int)
            self.tree = BVHTree.FromPolygons(self.points.tolist(), self.triangles.tolist(), all_triangles=True)
            self.large_faces = [tuple(p.vertices) for p in mesh.polygons if len(p.vertices)>4] if obj.name in (TUBE, MIC_LEAD) else []
        finally:
            evaluated.to_mesh_clear()


class Readback:
    def __init__(self):
        graph = bpy.context.evaluated_depsgraph_get()
        self.geometries = {o.name:Geometry(o,graph) for o in bpy.context.scene.objects if o.type=="MESH"}
        self.pair_counts = {}

    def count(self, a, b):
        key=ordered(a,b)
        if key not in self.pair_counts:
            self.pair_counts[key]=len(self.geometries[a].tree.overlap(self.geometries[b].tree))
        return self.pair_counts[key]

    def sampled_spacing(self, a, b):
        # A vertex-to-opposing-surface distance, useful at joints. At actual
        # triangle crossings the mesh spacing is exactly zero by the BVH test.
        if self.count(a,b):
            return 0.
        first,second = self.geometries[a], self.geometries[b]
        return 1000*min(min(second.tree.find_nearest(v.tolist())[3] for v in first.points),
                        min(first.tree.find_nearest(v.tolist())[3] for v in second.points))

    def joint(self, a, b):
        return {"pair":ordered(a,b), "intersecting_triangle_pairs":self.count(a,b),
                "minimum_sampled_spacing_mm":self.sampled_spacing(a,b)}

    def microphone_joint(self):
        housing = self.geometries[MIC].tree
        lead = self.geometries[MIC_LEAD]
        if len(lead.large_faces) != 2:
            raise ValueError("Expected two closed microphone lead end caps")
        cap_depths = []
        for cap in lead.large_faces:
            depths = []
            for vertex in lead.points[list(cap)]:
                point, normal, _, _ = housing.find_nearest(vertex.tolist())
                depths.append(float(np.dot(np.asarray(point) - vertex, normal)) * 1000)
            cap_depths.append([min(depths), max(depths)])
        cap_depths.sort()
        result = self.joint(MIC, MIC_LEAD)
        result["end_cap_depth_ranges_mm_positive_inside"] = cap_depths
        # A crossing midway along a loose cable must not qualify as a seated end.
        result["one_end_seated"] = (result["intersecting_triangle_pairs"] > 0 and
                                    cap_depths[0][1] < 0 < cap_depths[1][0])
        return result

    def plate_visibility(self, side, wing):
        plate = self.geometries["FP_KidneyPlate_540-015_"+side]
        fan = self.geometries[wing]
        corners = plate.points[plate.triangles]
        normals = np.cross(corners[:,1]-corners[:,0],corners[:,2]-corners[:,0])
        area = np.linalg.norm(normals,axis=1)/2
        facing = normals[:,1] < -1.3*area
        centers = corners[facing].mean(axis=1)
        origin_y = min(plate.points[:,1].min(),fan.points[:,1].min())-.01
        depth = []
        for v in centers:
            hit = fan.tree.ray_cast((v[0],origin_y,v[2]),(0,1,0),.35)[0]
            depth.append(0. if hit is None else max(0.,1000*(v[1]-hit.y)))
        depth=np.asarray(depth)
        return {"outward_face_samples":len(centers), "covered_outward_area_fraction":
                float(area[facing][depth>.05].sum()/area[facing].sum()),
                "max_wing_overhang_mm":float(depth.max())}

    def tube_visibility(self):
        tube=self.geometries[TUBE]
        caps=tube.large_faces
        if len(caps)!=2 or len(caps[0])!=len(caps[1]):
            raise ValueError("Saved bent tube does not have two identifiable end caps")
        n=len(caps[0])
        if len(tube.points)%n or {frozenset(ids) for ids in caps} != {
                frozenset(range(n)),frozenset(range(len(tube.points)-n,len(tube.points)))}:
            raise ValueError("Cannot recover consecutive tube cross-sections from saved indexing")
        rings=tube.points.reshape(-1,n,3)
        centers=rings.mean(axis=1)
        radii=np.linalg.norm(rings-centers[:,None,:],axis=2).mean(axis=1)
        observer=np.array([-.68,-.73,.26])
        observer/=np.linalg.norm(observer)
        obstructed=0
        clearance=[]
        for center,radius in zip(centers,radii):
            frontmost=0.
            for name in SIDES.values():
                tree=self.geometries[name].tree
                hit=tree.ray_cast((center+.6*observer).tolist(),(-observer).tolist(),.6)[0]
                if hit is not None:
                    frontmost=max(frontmost,float((np.asarray(hit)-center) @ observer))
                clearance.append(1000*(tree.find_nearest(center.tolist())[3]-radius))
            obstructed+=int(frontmost>radius)
        return {"cross_sections":len(rings),"station_centres_hidden_behind_wing_beyond_tube_radius":obstructed,
                "observer_unit_vector":observer.tolist(),"end_cap_centres_world_m":centers[[0,-1]].tolist(),
                "minimum_centre_distance_to_wing_less_tube_radius_mm":float(min(clearance))}


def inspect(model, out):
    if out.exists():
        raise FileExistsError(f"Choose a fresh output path: {out}")
    digest=sha(model)
    bpy.ops.wm.open_mainfile(filepath=str(model))
    read=Readback()
    names=set(read.geometries)
    air={n for n in names if n.startswith(("EXH_","SB_","REG_","TUBE_"))}
    window={n for n in names if n.startswith("FP_")}
    comms={n for n in names if n.startswith("Comms")}
    lower={o.name for o in bpy.context.scene.objects if o.type=="MESH" and any(
           "yoke" in c.name.lower() or "neck" in c.name.lower() or "weights" in c.name.lower()
           for c in o.users_collection)}
    required={SHELL,TUBE,FRAME,LENS,MIC,MIC_LEAD,*SIDES.values(),*FERRULES,*DEFROSTER,*SHELL_SEATS,
              "CommsPost_1_base","CommsPost_2_base","Comms_unused_MWPC_550-043_plug"}
    if not required<=names or not lower:
        raise ValueError("Expected assembly meshes or the lower hardware collection are absent")
    allowed={ordered(SHELL,part):reason for part,reason in SHELL_SEATS.items()}
    allowed[ordered(MIC, MIC_LEAD)] = "microphone lead seated in the microphone housing"
    allowed.update({ordered(TUBE,n):"supply tube seated inside its terminal ferrule" for n in FERRULES})
    for side,wing in SIDES.items():
        allowed[ordered("FP_WhiskerMountFlange_"+side,wing)]="junction between the two meshes of the same rubber wing"
    # Record each unordered object pair only once, even if a check (for
    # example, tube versus a wing) is also used by a targeted metric below.
    scanned={ordered(a,b) for a in air for b in {SHELL}|window|comms|lower if a!=b}
    scanned.add(ordered(MIC, MIC_LEAD))
    scanned.update(ordered(a,b) for a,b in itertools.product(DEFROSTER,window))
    scanned.update(ordered(a,b) for a,b in itertools.product(comms,window|lower) if a!=b)
    # The controls, ferrules and nuts have much larger diameters than the
    # supply tube. Test their full surfaces, including against the throat
    # cuffs and centre chamber, rather than relying on the tube clearance.
    lateral={n for n in air if n.startswith(("REG_Adjustment","REG_Knurled","REG_Inlet","TUBE_Regulator"))}
    exhaust={n for n in air if n.startswith("EXH_")}
    scanned.update(ordered(a,b) for a,b in itertools.product(lateral,exhaust))
    scanned.update(ordered(TUBE,b) for b in air if b!=TUBE)
    scanned.update(ordered(a,b) for a,b in itertools.product(
        (n for n in air if n.startswith("SB_")),
        (n for n in air if n.startswith(("REG_","EXH_")))))
    contacts=[]
    for a,b in sorted(scanned):
        count=read.count(a,b)
        if count:
            contacts.append({"pair":[a,b],"intersecting_triangle_pairs":count,
                             "expected_reason":allowed.get((a,b))})
    unexpected=[row for row in contacts if row["expected_reason"] is None]
    joints=[read.joint(TUBE,part) for part in FERRULES]
    sides={}
    for side,wing in SIDES.items():
        plate="FP_KidneyPlate_540-015_"+side
        flange="FP_WhiskerMountFlange_"+side
        sides[side]={"plate_view":read.plate_visibility(side,wing),
                     "rubber_joint":read.joint(flange,wing),"metal_to_rubber":read.joint(plate,flange),
                     "wing_to_tube":read.joint(wing,TUBE)}
    air_train={n:{"lens":read.count(n,LENS),"gasket":read.count(n,"FP_PortSeal_510-260"),
                  "retainer":read.count(n,FRAME)} for n in DEFROSTER}
    posts=[read.joint(SHELL,n) for n in ("CommsPost_1_base","CommsPost_2_base","Comms_unused_MWPC_550-043_plug")]
    tube=read.tube_visibility()
    microphone = read.microphone_joint()
    checks={
        "no_unexpected_breathing_window_lower_or_communications_intersections":not unexpected,
        "bent_tube_connected_to_both_separate_ferrules":all(j["minimum_sampled_spacing_mm"]<.15 for j in joints),
        "bent_tube_clear_of_both_whiskers":all(d["wing_to_tube"]["intersecting_triangle_pairs"]==0 for d in sides.values()),
        "tube_cross_sections_exposed_to_observer_beyond_wings":tube["station_centres_hidden_behind_wing_beyond_tube_radius"]==0,
        "both_rubber_wings_meet_corresponding_bolted_flange":all(d["rubber_joint"]["minimum_sampled_spacing_mm"]<.15 for d in sides.values()),
        "each_flange_behind_its_plate":all(d["metal_to_rubber"]["intersecting_triangle_pairs"]==0 and
            d["metal_to_rubber"]["minimum_sampled_spacing_mm"]<.15 and d["plate_view"]["covered_outward_area_fraction"]==0 for d in sides.values()),
        "air_train_and_feed_clear_of_lens_seal_and_retainer":not any(n for row in air_train.values() for n in row.values()),
        "communications_post_bases_and_alternative_port_plug_at_shell":all(j["minimum_sampled_spacing_mm"]<.25 for j in posts),
        "microphone_lead_end_seated_in_housing":microphone["one_end_seated"],
        "source_bytes_unchanged":sha(model)==digest}
    result={"blender":bpy.app.version_string,"input_sha256":digest,"checks":checks,"passed":all(checks.values()),
            "scope_unique_object_pairs":len(scanned),"inspected_pairing":
            "All REG/SB/TUBE/EXH meshes versus approved shell, FP meshes, neck/yoke/weight collections and Comms meshes; Comms versus FP and lower hardware; microphone lead versus microphone housing; defog tube/feed versus FP; bent tube versus all other breathing hardware; lateral regulator controls and lower tube connector versus Quad exhaust parts; side block versus all regulators and exhaust parts.",
            "surface_contacts":contacts,"unexpected_contacts":unexpected,"tube":tube,"ferrule_joints":joints,
            "wing_interfaces":sides,"air_train_contacts":air_train,"comms_mount_seats":posts,
            "microphone_lead_joint":microphone,
            "notes":"World-space evaluated triangle BVH. Counts are triangle pairs, not distinct defects. A crossing gives zero surface distance; otherwise distance is the smaller sampled vertex-to-opposing-surface distance. A fully enclosed smaller component does not need to produce surface crossings. Kidney coverage uses an orthographic front ray at each outward triangle centroid, area weighted, with 0.05 mm tolerance. Tube visibility is specific to the reported front-starboard direction. These are display assembly measurements."}
    out.parent.mkdir(parents=True,exist_ok=True)
    with out.open("x",encoding="utf-8") as stream:
        json.dump(result,stream,indent=2,ensure_ascii=False)
        stream.write("\n")
    print(json.dumps(checks),flush=True)
    if not result["passed"]:
        raise ValueError(f"Interface checks failed; inspect {out.name}")


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model",required=True,type=Path)
    parser.add_argument("--report",required=True,type=Path)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    inspect(args.model.resolve(),args.report.resolve())
