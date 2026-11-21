"""Reshape the saved single shell in Blender 4.5. All shape controls are in mm."""
import argparse
import hashlib
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


BASE_SHA = "09766af684372e1a47bc5ecb810e2d6f9d30cc54fca3f51912768e733575ce09"
SHELL = "SL17B_YellowShell_Study"


def ease(value, start, end):
    t = np.clip((value-start)/(end-start), 0, 1)
    return t**3 * (10 - 15*t + 6*t*t)


def refine_side_quads(mesh):
    """Add local support for the rounded shoulders of the two moulded lands."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    eligible = set()
    for v in bm.verts:
        x,y,z = v.co*1000
        if abs(x)>91 and -66 < y < 90 and 65 < z < 228 and z-72-.42*y > 4:
            eligible.add(v)
    edges=[edge for edge in bm.edges if all(v in eligible for v in edge.verts)]
    boundary={face for edge in edges for face in edge.link_faces
              if len(face.verts)==4 and any(v not in eligible for v in face.verts)}
    bmesh.ops.subdivide_edges(bm, edges=edges, cuts=1, use_grid_fill=True)
    # A half-divided perimeter quad is an n-gon with collinear boundary
    # points. Fan from its centre rather than creating zero-area corner ears.
    transition=[face for face in boundary if face.is_valid and len(face.verts)>4]
    if transition:
        bmesh.ops.poke(bm,faces=transition,center_mode="MEAN_WEIGHTED")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def plane_y(x, z):
    return -133 + .17*(z-190) + 16*(x/106)**2


def rounded_rectangle(u, v, half_width, half_height, radius):
    x = np.abs(u)-half_width+radius
    y = np.abs(v)-half_height+radius
    return np.hypot(np.maximum(x,0),np.maximum(y,0)) + np.minimum(np.maximum(x,y),0) - radius


def fit_side_lands(points, tree):
    x,y,z=points.T
    side=np.sign(x)
    height=z-72-.42*y
    # Fade into the original sidewall before reaching the continuous neck groove.
    active=(np.abs(x)>92)&(y>-68)&(y<92)&(height>3)&(height<151)
    moved=np.zeros(len(points))
    indices=np.flatnonzero(active)
    for i in indices:
        sign=side[i]
        u=y[i]-12 + .06*(z[i]-133)
        v=z[i]-133 - .19*(y[i]-12)
        w,h,r = (37.5,43.0,20.0) if sign>0 else (34.0,41.0,18.0)
        distance = rounded_rectangle(u,v,w,h,r)
        # A broad fillet, particularly at the lower return into the skirt.
        blend = 1-ease(distance,-8.0,10.0+3.0*ease(-v,15,36))
        edge_fade=ease(height[i],3,16)
        # Back out the first study's oval relief in the transition to the land.
        old_radius = ((y[i]-12)/39)**2 + ((z[i]-143-.16*y[i])/34)**2
        old_relief = (6.0 if sign>0 else 10.5)*np.exp(-1.4*old_radius**3)
        if blend < 1e-8 and old_relief < 1e-6:
            continue
        hit,_,_,_=tree.ray_cast((sign*.25,y[i]*.001,z[i]*.001),(-sign,0,0),.22)
        if hit is None:
            raise RuntimeError("No outer shell at side-wall sample")
        original_surface=abs(hit.x)*1000
        # Displace both walls by their outer-surface correction. This retains
        # the underlying cavity instead of collapsing both onto the land plane.
        top = (143.8 if sign>0 else 141.7) + .18*v + .012*u - .0016*u*u - .00086*v*v
        moved[i]=sign*edge_fade*(blend*(top-original_surface)-(1-blend)*old_relief)
    return moved


def distance_to_window(points, lip):
    """Nearest 2D rim sample (rim spacing is about 3 mm)."""
    distance=np.full(len(points),np.inf)
    for rim_x,_,rim_z in lip:
        distance=np.minimum(distance,np.hypot(points[:,0]-rim_x,points[:,2]-rim_z))
    return distance


def revise(points, tree, lip_vertices):
    x,y,z=points.T
    depth=y-plane_y(x,z)
    above_seat=ease(z-72-.42*y,12,59)
    face=(1-ease(depth,6,138))*ease(z,112,136)*above_seat
    # Shear the aperture and its recesses along Y first, then raise the crest
    # in the resulting coordinates. The long return preserves wall ordering.
    result=points.copy()
    result[:,1] += .245*(z-205)*face
    brow=ease(z,156,245)*above_seat
    cheek=ease(np.abs(x),52,100)*ease(z,111,158)*above_seat
    behind_rim=ease(depth,3,23)*(1-ease(depth,29,122))
    result[:,1] += (5*brow+8*cheek)*behind_rim
    cowl_depth=result[:,1]-plane_y(x,z)-.245*(z-205)
    lip=1-ease(cowl_depth,4,28)
    valley=np.exp(-((cowl_depth-33)/14)**2)
    result[:,2] += brow*(14.5*lip-5.7*valley)
    # The sloping strip fans out toward the lower cheek. A single rolling
    # edge returns its broad face into the shell below the crown.
    shoulder_depth = 27 + 19*(1-ease(z,139,249))
    shoulder_rise = 4.8 + 4.1*ease(cowl_depth,6,shoulder_depth-6)
    shoulder = shoulder_rise*(1-ease(cowl_depth,shoulder_depth-7,shoulder_depth+7))
    return_curve = 1.5*np.exp(-((cowl_depth-shoulder_depth-10)/9)**2)
    result[:,0] += np.sign(x)*cheek*(shoulder-return_curve)
    # Raise the moulded eyebrow more than the actual pane aperture. With a
    # common deformation the opening grew considerably in the archived bare-
    # shell three-quarter view. Ease this correction away before the outer lip.
    margin=distance_to_window(points,lip_vertices)
    aperture=(1-ease(depth,4,34))*(1-ease(margin,2,19))
    result[:,0] -= .041*x*aperture
    result[:,2] += aperture*(3.8*(1-ease(z,143,178))-10*ease(z,185,240))
    result[:,1] += 4.5*aperture*(1-ease(z,190,240))
    # Carry the broad crown toward the brow; retain the rounded back visible
    # above the separate metal weight in the supplied views.
    crown=ease(z,205,282)*ease(depth,45,95)
    result[:,1] -= 6.5*crown*np.exp(-((y-27)/78)**2)
    result[:,2] -= 3*crown*np.exp(-((y-16)/95)**2)
    result[:,0] += fit_side_lands(points,tree)
    return result


def preserve_socket_shapes(mesh, original, revised):
    """Move each existing blind bore rigidly into the re-formed window seat."""
    # The baseline's upper UV strip contains five service holes, followed by
    # one labelled-by-position island for each of its 16 insert recesses.
    uvs=np.array([corner.uv[:] for corner in mesh.uv_layers["ShellUV"].data])
    sockets={island:set() for island in range(5,21)}
    for face in mesh.polygons:
        uv=uvs[list(face.loop_indices)]
        if uv[:,1].min()>.887:
            island=int(21*uv[:,0].mean())
            if island in sockets:
                sockets[island].update(face.vertices[:])
    for indices in sockets.values():
        if len(indices)!=86:
            raise ValueError("Could not identify the complete saved socket")
        ids=sorted(indices)
        old,new=original[ids],revised[ids]
        center=old.mean(axis=0)
        destination=new.mean(axis=0)
        u,_,vt=np.linalg.svd((old-center).T @ (new-destination))
        rotation=u @ np.diag([1,1,np.linalg.det(u@vt)]) @ vt
        # Move the shared bore rim, wall and floor. The collar's outer
        # silhouette belongs to the continuous moulding, even near a bore.
        revised[ids]=(old-center)@rotation+destination


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base",required=True,type=Path)
    parser.add_argument("--output",required=True,type=Path)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    source,output=args.base.resolve(),args.output.resolve()
    if hashlib.sha256(source.read_bytes()).hexdigest()!=BASE_SHA:
        raise ValueError("Input does not match the saved first shell study")
    if output.exists() or output.suffix.lower()!=".blend":
        raise ValueError("Select a new .blend filename")
    bpy.ops.wm.open_mainfile(filepath=str(source))
    obj=bpy.data.objects[SHELL]
    if len(bpy.data.objects)!=1 or obj.type!="MESH":
        raise ValueError("Expected the one-object bare-shell scene")
    mesh=obj.data
    refine_side_quads(mesh)
    mesh.calc_loop_triangles()
    tree=BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices],
                              [tri.vertices[:] for tri in mesh.loop_triangles],all_triangles=True)
    points=np.array([v.co[:] for v in mesh.vertices])*1000
    lip_group=obj.vertex_groups["Face_Opening"].index
    lip_ids=[v.index for v in mesh.vertices if any(g.group==lip_group and g.weight>.99 for g in v.groups)]
    if len(lip_ids)!=160:
        raise ValueError("The saved aperture loop has changed")
    coordinates=revise(points,tree,points[lip_ids])
    preserve_socket_shapes(mesh,points,coordinates)
    mesh.vertices.foreach_set("co",np.asarray(coordinates*.001,dtype=np.float32).ravel())
    mesh.update()
    obj["revision"]="User-supplied four views and SuperLite 17 turntable / bare shell review 2"
    obj["input_blend_sha256"]=BASE_SHA
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                space=area.spaces.active
                space.region_3d.view_rotation=Vector((-.98,.18,-.18)).to_track_quat("-Z","Y")
                space.region_3d.view_location=(0,0,.16)
                space.region_3d.view_distance=.545
    bpy.context.preferences.filepaths.save_version=0
    output.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output),compress=True)


if __name__=="__main__":
    main()
