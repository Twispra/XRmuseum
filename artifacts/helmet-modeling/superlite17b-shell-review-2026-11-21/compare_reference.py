"""Read two saved shells and compare exposed side profiles with the user's first render.

The annotated positions are in the original 1209 by 963 screenshot. No screenshot
pixels are copied into the output. Run with Python, NumPy, Pillow and Blender.
"""
import argparse
import hashlib
import json
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np


REFERENCE_SHA = "b441c347cba5c6fbf2d77a14324aea20937d9bf9078aa117ad292b4fec3a75cd"
BASE_SHA = "09766af684372e1a47bc5ecb810e2d6f9d30cc54fca3f51912768e733575ce09"
# Exposed yellow outline; stop before the carrying handle blends into the roof.
# Gate each column around the traced edge, avoiding hardware above or below it.
BROW = [(477,189),(480,192),(485,195),(490,201),(495,212),(500,226),(505,239),
        (510,248),(515,257),(520,263),(525,267),(530,267),(535,264),(540,261)]
CROWN = [(550,253),(560,243),(570,234),(585,223),(600,216),(615,210),(635,205),
         (650,202),(670,198),(690,196),(710,195),(730,196),(750,198),
         (770,200),(790,203),(810,208),(825,212),(840,218)]
NECK = [(380,782),(400,782),(425,780),(450,778),(475,775),(500,772),(525,768),
        (550,763),(575,758),(600,753),(625,747),(650,742),(675,736),(700,730),
        (725,724),(750,717),(775,709),(800,702),(825,694),(850,685),(875,675),
        (900,666),(925,656),(950,647),(975,637),(1000,623)]
WINDOW_SIDE = [(297,450),(325,402),(349,365),(373,328),(398,286),(422,250),
               (444,216),(462,194)]
COWL_RETURN = [(523,265),(516,299),(509,337),(502,370),(494,400),
               (484,438),(471,475),(460,512),(446,552)]
PAD_VISIBLE = [(569,529),(587,496),(612,477),(648,464),(697,463),(743,482),
               (765,517),(777,558),(784,612),(781,653),(772,678),(751,693),
               (718,703),(681,710),(644,714),(619,714)]
# Approximate side/rear orthographic registration. Coordinates use an origin
# 150 mm above Blender world zero. The same transform is applied to both files.
POSE = [97.64, 8.93, 8.53, 2426.42, 680.90, 524.02]
WORLD_ORIGIN = np.array([0.,0.,.150])


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def export_mesh(source, dest):
    import bpy

    bpy.ops.wm.open_mainfile(filepath=str(source))
    if len(bpy.context.scene.objects) != 1:
        raise ValueError("Expected the isolated shell only")
    obj = next(iter(bpy.context.scene.objects))
    if obj.type != "MESH":
        raise ValueError("Expected a saved shell mesh")
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    try:
        vertices = np.array([tuple(evaluated.matrix_world @ v.co) for v in mesh.vertices])
        edges = np.array([tuple(edge.vertices) for edge in mesh.edges])
        groups = {}
        for name in ("Neck_Groove", "Face_Opening"):
            index = obj.vertex_groups[name].index
            ids = [v.index for v in mesh.vertices if any(g.group == index and g.weight > .99 for g in v.groups)]
            groups[name] = edges[np.isin(edges, ids).all(axis=1)]
            if len(groups[name]) != len(ids) or not len(ids):
                raise ValueError(f"Expected closed tagged loop: {name}")
        mesh.calc_loop_triangles()
        triangles = np.array([tuple(t.vertices) for t in mesh.loop_triangles],dtype=np.int32)
        np.savez(dest,vertices=vertices,triangles=triangles,**groups)
    finally:
        evaluated.to_mesh_clear()


def basis(pose):
    az, elev, roll = np.radians(pose[:3])
    back = [np.sin(az)*np.cos(elev),-np.cos(az)*np.cos(elev),np.sin(elev)]
    right = [np.cos(az),np.sin(az),0.]
    image = np.stack([right,-np.cross(back,right)])
    return np.array([[np.cos(roll),-np.sin(roll)],[np.sin(roll),np.cos(roll)]]) @ image


def project(points, pose):
    return (points - WORLD_ORIGIN) @ basis(pose).T * pose[3] + pose[4:6]


def boundary(poly, xs, side):
    """First/last crossing of vertical sample lines with triangle silhouette edges."""
    x0, y0 = poly[:,0,:].T
    x1, y1 = poly[:,1,:].T
    values = []
    for x in xs:
        valid = (((x0<=x) & (x1>x)) | ((x1<=x) & (x0>x)))
        if not np.any(valid):
            values.append(np.nan)
            continue
        ys = y0[valid] + (x - x0[valid])*(y1[valid]-y0[valid])/(x1[valid]-x0[valid])
        values.append(np.min(ys) if side == "top" else np.max(ys))
    return np.asarray(values)


def silhouette_edges(points, triangles, pose):
    xy = project(points,pose)
    a,b,c=xy[triangles].transpose(1,0,2)
    ab, ac = b-a, c-a
    facing = ab[:,0]*ac[:,1] - ab[:,1]*ac[:,0] > 0
    directed = np.concatenate([triangles[:,[0,1]],triangles[:,[1,2]],triangles[:,[2,0]]])
    labels = np.sort(directed,axis=1)
    order = np.lexsort((labels[:,1],labels[:,0]))
    ordered = labels[order]
    if not np.array_equal(ordered[::2],ordered[1::2]):
        raise ValueError("Expected two triangles per undirected edge")
    sign = np.tile(facing,3)[order]
    return xy[ordered[::2][sign[::2]!=sign[1::2]]]


def trace(path, guide):
    from PIL import Image

    with Image.open(path) as raw:
        if raw.size != (1209,963):
            raise ValueError("Unexpected first screenshot dimensions")
        rgb = np.asarray(raw.convert("RGB"),dtype=int)
    red,green,blue = rgb.transpose(2,0,1)
    yellow = (red>175)&(green>125)&(red-blue>61)&(green-blue>46)
    samples=[]
    for x,y in guide:
        entries = np.where(yellow[1:,x] & ~yellow[:-1,x])[0]+1
        found = entries[np.argmin(abs(entries-y))]
        if abs(found-y)>8:
            raise ValueError(f"Unmatched silhouette sample near {(x,y)}")
        samples.append((x,int(found)))
    return np.asarray(samples)


def metrics(mesh, pose, traces):
    edges = silhouette_edges(mesh["vertices"],mesh["triangles"],pose)
    result={}
    for label, observed in traces.items():
        predicted = boundary(edges,observed[:,0],"top")
        if not np.isfinite(predicted).all():
            raise ValueError(f"Mesh does not cover reference interval {label}")
        dy = predicted-observed[:,1]
        result[label] = {"reference_xy_px":observed.tolist(),
                         "model_y_at_same_x_px":predicted.tolist(),
                         "mean_signed_model_minus_reference_y_px":float(np.mean(dy)),
                         "mean_absolute_vertical_distance_px":float(np.abs(dy).mean()),
                         "max_absolute_vertical_distance_px":float(np.abs(dy).max())}
    xy = project(mesh["vertices"],pose)
    near = mesh["Neck_Groove"]
    near = xy[near[mesh["vertices"][near,0].mean(axis=1)>0]]
    observed_neck = np.asarray(NECK)
    a, b = near[:,0], near[:,1]
    delta = b-a
    fraction = np.clip(np.einsum("ijk,jk->ij",observed_neck[:,None]-a,delta)
                       /np.maximum((delta*delta).sum(axis=1),1e-16),0,1)
    residual = observed_neck[:,None] - (a+fraction[:,:,None]*delta)
    nearest = np.linalg.norm(residual,axis=2).min(axis=1)
    result["registration_neck_arc"]={"mean_nearest_distance_px":float(nearest.mean()),
                                      "max_nearest_distance_px":float(nearest.max())}
    front = np.asarray(WINDOW_SIDE[:5])
    model_x = boundary(xy[mesh["Face_Opening"]][:,:,::-1],front[:,1],"top")
    def tilt(xs):
        if np.isfinite(xs).all():
            return float(np.rad2deg(np.arctan(-np.polyfit(front[:,1],xs,1)[0])))
        return None
    result["front_oblique_edge"]={"observed_outer_trim_direction_deg_from_vertical":tilt(front[:,0]),
                                  "aperture_foremost_edge_direction_deg_from_vertical":tilt(model_x),
                                  "image_y_interval_px":[int(front[:,1].min()),int(front[:,1].max())],
                                  "limit":"The visible yellow/metal boundary and the modeled aperture are adjacent features. Their projected directions provide an approximate comparison, not a measured plane angle."}
    # The screenshot exposes the back of the brow, its saddle, then the crown.
    # Locate these three landmarks afresh in each mesh; this is independent of
    # the vertical-at-fixed-column distances above.
    xs=np.arange(400,845)
    ys=boundary(edges,xs,"top")
    minima=1+np.flatnonzero((ys[:-2] > ys[1:-1]) & (ys[1:-1] <= ys[2:]))
    maxima=1+np.flatnonzero((ys[:-2] < ys[1:-1]) & (ys[1:-1] >= ys[2:]))
    pairs=[(i,j) for i in minima if xs[i]<550 for j in maxima if i<j and xs[j]<640]
    if pairs:
        crest,saddle=max(pairs,key=lambda ij:ys[ij[1]]-ys[ij[0]])
        crown=saddle+np.nanargmin(ys[saddle:])
        result["landmarks"]={
            name:[int(xs[k]),float(ys[k])] for name,k in
            (("brow_peak",crest),("saddle",saddle),("crown_peak",crown))}
        result["landmarks"]["saddle_depth_below_brow_px"]=float(ys[saddle]-ys[crest])
        result["landmarks"]["brow_minus_crown_image_y_px"]=float(ys[crest]-ys[crown])
    else:
        result["landmarks"]={"brow_peak":None,"saddle":None,"note":"Distinct brow and saddle not resolved."}
    # This line is for displaying the continuous saved geometry. Metrics above
    # use only the reference columns where the gelcoat edge could be identified.
    return result, (xs,ys)


def draw_diagram(path, traces, baseline, candidate, curves):
    from PIL import Image,ImageDraw,ImageFont

    img=Image.new("RGB",(1060,455),"#F5F4EF"); d=ImageDraw.Draw(img)
    try:
        font=ImageFont.truetype("msyh.ttc",20)
        title=ImageFont.truetype("msyhbd.ttc",27)
        header="参考图 1 · 窗眉、凹口与冠顶"
        items=("参考图可见黄边","上一版裸壳","本次修订")
    except OSError:
        font=title=ImageFont.load_default()
        header="Reference 1 / brow, saddle and crown"
        items=("Visible in reference","Previous shell","Revised shell")
    d.text((35,18),header,font=title,fill="#253438")
    def screen(x,y):
        return ((float(x)-425)*2.38+30,(float(y)-150)*2.38+60)
    def line(xs,ys,color):
        d.line([screen(x,y) for x,y in zip(xs,ys) if 425<=x<=840 and np.isfinite(y)],
               fill=color,width=3)
    for label,color in (("base","#9B9692"),("candidate","#D05F25")):
        line(*curves[label],color)
    for points in traces.values():
        line(points[:,0],points[:,1],"#08767D")
    for values,color in ((baseline["landmarks"],"#9B9692"),(candidate["landmarks"],"#D05F25"),
                          ({"brow_peak":[475,188],"saddle":[527,267],"crown_peak":[707,196]},"#08767D")):
        for name in ("brow_peak","saddle","crown_peak"):
            point=values.get(name)
            if point is not None:
                x,y=screen(*point)
                d.ellipse((x-4,y-4,x+4,y+4),fill=color)
    for i,(name,c) in enumerate(zip(items,("#08767D","#9B9692","#D05F25"))):
        x=35+i*310;d.line([(x,408),(x+35,408)],fill=c,width=4);d.text((x+46,394),name,font=font,fill="#253438")
    with path.open("xb") as f: img.save(f,format="PNG")


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for arg in ("reference","base","candidate","blender","report"):
        parser.add_argument("--"+arg,type=Path,required=True)
    parser.add_argument("--diagram",type=Path)
    args=parser.parse_args()
    if sha(args.reference)!=REFERENCE_SHA or sha(args.base)!=BASE_SHA:
        raise ValueError("The reference or the baseline differs from the registered input")
    for path in (args.report,args.diagram):
        if path and path.exists(): raise FileExistsError(path)
        if path: path.parent.mkdir(parents=True,exist_ok=True)
    traces={"brow":trace(args.reference,BROW),"crown":trace(args.reference,CROWN)}
    measures,curves={},{}
    with tempfile.TemporaryDirectory() as temp:
        for label,path in (("base",args.base),("candidate",args.candidate)):
            dest=Path(temp)/(label+".npz")
            subprocess.run([str(args.blender),"-b","--python-exit-code","1","-P",__file__,"--",
                            "--extract",str(path.resolve()),str(dest)],check=True,
                           stdout=subprocess.DEVNULL)
            with np.load(dest) as mesh: measures[label],curves[label]=metrics(mesh,POSE,traces)
    result={"reference_sha256":REFERENCE_SHA,"base_sha256":BASE_SHA,
            "candidate_sha256":sha(args.candidate),"image_size_px":[1209,963],
            "pose_azimuth_from_front_towards_positive_X_elevation_roll_scale_px_per_m_tx_ty":POSE,
            "pose_world_origin_m":WORLD_ORIGIN.tolist(),"measurements":measures,
            "registration":"Fixed from the previous bare shell before candidate scoring. Approximate orthographic registration uses the visible positive-X neck groove near arc, rough front/rear neck extents (partly hidden), crown-apex location and weak side/rear-view prior. The crown apex was a registration anchor. The brow and saddle were not fit targets. Remaining camera and type/configuration uncertainty can shift all distances.",
            "registration_neck_trace_xy_px":NECK,
            "reference_landmarks_approx_xy_px":{"brow_peak":[475,188],"saddle":[527,267],"crown_peak":[707,196]},
            "visible_side_moulding_trace_xy_px":WINDOW_SIDE,
            "cowl_return_highlight_trace_approx_xy_px":COWL_RETURN,
            "visible_pad_trace_approx_xy_px":PAD_VISIBLE,
            "scope":"Source shows assembled hardware; only the upper unobstructed shell segments are scored. Internal moulding and pad lines are descriptive. Distances are signed vertically with image Y increasing down, under one approximate uncalibrated orthographic pose."}
    if args.diagram:
        draw_diagram(args.diagram,traces,measures["base"],measures["candidate"],curves)
        result["diagram_sha256"]=sha(args.diagram)
    with args.report.open("x",encoding="utf-8") as f:
        json.dump(result,f,indent=2);f.write("\n")


if __name__=="__main__":
    if "--" in sys.argv and "--extract" in sys.argv:
        export_mesh(Path(sys.argv[-2]),Path(sys.argv[-1]))
    else:
        main()
