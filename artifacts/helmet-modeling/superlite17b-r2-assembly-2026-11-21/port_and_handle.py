"""Fit the external face port and brass handle to the saved R2 shell, without editing it.

Coordinates inside this module are millimetres; Blender output is in metres.
See KM 17B drawing 241108001, FCPRT-2/3, 17BHDL-1/2 and the manufacturer's
front and handle photographs. All thicknesses and hardware profiles are estimates.
"""
import math
import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def unit(v):
    return v / np.linalg.norm(v, axis=-1, keepdims=True)


def material(name, rgb, metallic=0., roughness=.35):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1.)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def mesh_object(name, pts, faces, mat):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata((np.asarray(pts)*.001).tolist(), [], faces)
    mesh.materials.append(mat)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    for face in mesh.polygons:
        face.use_smooth = True
    mesh.set_sharp_from_angle(angle=math.radians(39))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def project_uvs(obj):
    """Per-face dominant-axis UVs on the finished material/boolean surfaces."""
    mesh = obj.data
    uv = mesh.uv_layers.new(name="DisplayUV")
    p = np.asarray([v.co[:] for v in mesh.vertices])
    scaled = (p-p.min(axis=0))/np.maximum(np.ptp(p, axis=0), 1e-10)
    data = np.empty((len(mesh.loops),2), dtype=np.float32)
    for face in mesh.polygons:
        axes = ((1,2),(0,2),(0,1))[np.argmax(np.abs(face.normal[:]))]
        data[list(face.loop_indices)] = scaled[list(face.vertices)][:,axes]
    uv.data.foreach_set("uv", data.ravel())


def strip(name, rows, mat, closed=True):
    rows = np.array(rows)
    n = rows.shape[1]
    faces = []
    for r in range(len(rows) if closed else len(rows)-1):
        k = (r+1) % len(rows)
        faces.extend((r*n+i,r*n+(i+1)%n,k*n+(i+1)%n,k*n+i) for i in range(n))
    pts=rows.reshape(-1,3).tolist()
    if not closed:
        for r in (0,len(rows)-1):
            center=len(pts)
            pts.append(rows[r].mean(axis=0))
            faces.extend((r*n+j,r*n+(j+1)%n,center) for j in range(n))
    return mesh_object(name, pts, faces, mat)


def radial_basis(axis, count):
    across = unit(np.cross(axis, (0.,0.,1.) if abs(axis[2])<.85 else (0.,1.,0.)))
    up = np.cross(axis,across)
    angle = np.arange(count)*math.tau/count
    return np.cos(angle)[:,None]*across+np.sin(angle)[:,None]*up


def turned(name, center, axis, profile, mat, segments=48):
    normal = unit(np.asarray(axis))
    rays = radial_basis(normal,segments)
    return strip(name,[center+normal*t+r*rays for t,r in profile],mat,False)


def tree(obj):
    obj.data.calc_loop_triangles()
    return BVHTree.FromPolygons([v.co[:] for v in obj.data.vertices],
                                [t.vertices[:] for t in obj.data.loop_triangles],all_triangles=True)


def cast(bvh, point, direction, length=100.):
    hit, normal, _, _ = bvh.ray_cast(Vector(point)*.001, Vector(direction), length*.001)
    return (None,None) if hit is None else (np.array(hit)*1000,np.array(normal))


def front_hit(bvh, center, normal):
    hit, _ = cast(bvh,center+35*normal,-normal,60)
    if hit is None:
        raise ValueError("Hardware center misses its support")
    return hit


def shell_landmarks(shell):
    mesh = shell.data
    pts = np.asarray([v.co[:] for v in mesh.vertices])*1000
    uv = np.asarray([v.uv[:] for v in mesh.uv_layers["ShellUV"].data])
    ids = np.asarray([v.vertex_index for v in mesh.loops])
    # First 11 native strip coordinates of the R2 front landing. Each contains
    # one undisturbed sample per original meridian; the bottom of a blind bore
    # deliberately never contributes to the estimated surface between bores.
    vcoords = [.006,.0080889,.0098626,.0113284,.0132896,.0169727,
               .0191436,.0206854,.0313081,.0344955,.0368416]
    rows = []
    for v in vcoords:
        selection = np.flatnonzero(abs(uv[:,1]-v)<.00000012)
        samples=[]
        for u in np.linspace(.005,.995,161)[:-1]:
            match = selection[abs(uv[selection,0]-u)<.000001]
            if not len(match):
                raise ValueError("Missing R2 window strip sample")
            samples.append(pts[ids[match[0]]])
        rows.append(np.array(samples))
    aperture_ids = {v.index for v in mesh.vertices if
                    any(g.group==shell.vertex_groups["Face_Opening"].index and g.weight>.99 for g in v.groups)}
    if set(map(tuple,rows[0]))!=set(map(tuple,pts[sorted(aperture_ids)])):
        raise ValueError("Front row no longer matches the approved aperture")
    sockets={}
    for face in mesh.polygons:
        if len(face.vertices)!=28:
            continue
        patch=uv[list(face.loop_indices)]
        code=int(21*patch[:,0].mean())
        if 5<=code<=20 and np.min(patch[:,1])>.952:
            sockets[code]=(pts[list(face.vertices)].mean(axis=0),unit(np.asarray(face.normal[:])))
    if set(sockets)!=set(range(5,21)):
        raise ValueError("Expected sixteen actual R2 socket floors")
    return rows,sockets


def subtract_drills(obj, sites, mat):
    # Disjoint coaxial tools. The same axes drive the screws and bearing pads.
    vertices,faces=[],[]
    for center,axis,radius in sites:
        count=40
        phase=math.tau*.317/count
        raw=radial_basis(axis,count)
        rays=raw*math.cos(phase)+np.cross(axis,raw)*math.sin(phase)
        start=len(vertices)
        for t in (-12,18):
            vertices.extend(center+axis*t+radius*rays)
        faces.extend((start+i,start+(i+1)%count,start+count+(i+1)%count,start+count+i)
                     for i in range(count))
        faces.extend([tuple(start+i for i in range(count)),tuple(start+count+i for i in range(count))])
    # Fix the diagonals of curved quads before the Boolean tessellates them.
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    tool=mesh_object("Temporary_Port_Drills",vertices,faces,mat)
    mod=obj.modifiers.new("Mounting bores","BOOLEAN")
    mod.operation="DIFFERENCE"
    mod.solver="EXACT"
    mod.object=tool
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    data=tool.data
    bpy.data.objects.remove(tool,do_unlink=True)
    bpy.data.meshes.remove(data)
    obj.data.calc_loop_triangles()
    narrow={tri.polygon_index for tri in obj.data.loop_triangles if tri.area<5e-13}
    curved_tabs = obj.name.startswith(("FP_KidneyPlate","FP_WhiskerMountFlange"))
    if narrow or curved_tabs:
        bm=bmesh.new()
        bm.from_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        cut_faces=([f for f in bm.faces if len(f.verts)>3] if curved_tabs else
                   [bm.faces[i] for i in narrow])
        bmesh.ops.triangulate(bm,faces=cut_faces,ngon_method="BEAUTY")
        bm.to_mesh(obj.data)
        bm.free()
    obj.data.set_sharp_from_angle(angle=math.radians(39))


def screw(name, seat, axis, mat, slot=False, radius=4., stem=3.):
    # Real drive recess and raised, rounded head, all one closed mesh.
    count=64
    phase=np.arange(count)*math.tau/count
    rays=radial_basis(axis,count)
    az=np.maximum(abs(np.cos(phase)),1.e-8)
    el=np.maximum(abs(np.sin(phase)),1.e-8)
    opening=np.minimum(radius*.69/az,radius*.125/el)
    if not slot:
        opening=np.maximum(opening,np.minimum(radius*.125/az,radius*.69/el))
    outer=[(-stem,1.45),(-.40,1.6),(-.02,radius*.72),(.08,radius*.92),
           (.46,radius),(.88,radius*.94),(1.12,radius*.76)]
    rows=[seat+axis*h+r*rays for h,r in outer]
    rows.extend([seat+axis*1.22+opening[:,None]*rays,
                 seat+axis*.32+opening[:,None]*.77*rays])
    return strip(name,rows,mat,False)


def window(rows, mats, shell_tree, sites):
    lip,outer=rows[0],rows[-1]
    tangent=np.roll(lip,-1,axis=0)-np.roll(lip,1,axis=0)
    radial=unit(outer-lip)
    normal=unit(np.cross(tangent,radial))
    if normal[:,1].mean()>0:
        normal*=-1
    radial=unit(radial-normal*(radial*normal).sum(axis=1)[:,None])
    # A rolled bright rim and a continuous underside following the R2 landing.
    # Work out the crown needed to clear the raised front landing as it
    # undulates between the aperture and the outer edge, particularly at the
    # chin. Front-side relief stays smooth along the casting.
    projected=(outer-lip)[:,[0,2]]
    relief=np.zeros(len(lip))
    for row in rows:
        t=np.sum((row-lip)[:,[0,2]]*projected,axis=1)/np.sum(projected**2,axis=1)
        relief=np.maximum(relief,lip[:,1]+t*(outer[:,1]-lip[:,1])-row[:,1])
    forward=np.array((0.,-1.,0.))
    rim_width=unit(np.column_stack((projected[:,0],np.zeros(len(lip)),projected[:,1])))
    # Radial ordering gives the pane, seal and rolled retaining edge separate
    # surfaces. The hidden back toe of the seal returns to the shell lip.
    gasket_profile=[(-3.2,.25),(-3.2,2.08),(-2.96,2.49),(-2.48,2.63),
                    (-1.82,2.64),(-1.56,2.47),(-.65,2.02),(0.,1.7),
                   (.40,1.26),(.38,.98),(0.,.49),(-1.7,.20)]
    strip("FP_PortSeal_510-260",[lip+rim_width*r+h*forward for r,h in gasket_profile],mats["rubber"])
    front_inner=lip-1.12*rim_width+forward*(3.9+.60*relief[:,None])
    front_outer=outer-.52*rim_width+forward*(3.7+.60*relief[:,None])
    front=[lip-1.45*rim_width+2.65*forward,lip-1.55*rim_width+3.25*forward,front_inner]
    for t in (.18,.4,.65,.85):
        front.append(front_inner*(1-t)+front_outer*t+forward*(.72*math.sin(math.pi*t)))
    front.extend([front_outer,outer+.35*rim_width+forward*(3.22+.55*relief[:,None]),
                  outer+.75*rim_width+forward*(2.65+.45*relief[:,None]),
                  outer+.75*rim_width+.8*forward,outer+.30*rim_width+.22*forward])
    # The mounting holes interrupt the original strip between rows 7 and 8.
    # Let the casting bridge the bores with a small local rear relief instead
    # of allowing a large chord to cut through a lip of the original socket.
    back=[]
    for i in range(len(rows)-1,0,-1):
        for f in ([0.,.2,.4,.6,.8] if i==8 else [0.]):
            row=rows[i]*(1-f)+rows[i-1]*f
            fitted=row.copy()
            for j,p in enumerate(row):
                hit,_=cast(shell_tree,(p[0],-250,p[2]),(0,1,0),250)
                proximity=min(np.linalg.norm(np.cross(p-o,axis)) for o,axis in sites.values())
                lower_return=.52*np.clip((158-p[2])/18,0,1)
                margin=.12+lower_return+1.65*max(0.,1-(proximity/8.5)**2)**2
                fitted[j,1]=(min(p[1],hit[1]) if hit is not None else p[1])-margin
            back.append(fitted)
    # The gasket and lens underlap the bright lip without crossing its face.
    frame=strip("FP_PortRetainer_560-070",front+back+[lip+forward*1.95],mats["chrome"])
    frame["basis"]="17B #241108001, loc 33; shell front strips; thickness estimated"
    # A transparent meniscus in two optically parallel skins. Its perimeter
    # follows the actual curved opening, including the raised lower centre.
    edge=lip-3.24*rim_width+forward*2.06
    center_xz=(edge[:,[0,2]].min(axis=0)+edge[:,[0,2]].max(axis=0))/2
    def design(xz):
        x,z=(xz-center_xz).T
        return np.column_stack((np.ones(len(x)),x,z,x*x,x*z))
    co=np.linalg.lstsq(design(edge[:,[0,2]]),edge[:,1],rcond=None)[0]
    edge_residual=edge[:,1]-design(edge[:,[0,2]])@co
    front_rows=[]
    for t in np.linspace(.045,1,18):
        xz=center_xz+(edge[:,[0,2]]-center_xz)*t
        y=design(xz)@co+edge_residual*(t**2)-2*(1-t*t)
        front_rows.append(np.column_stack((xz[:,0],y,xz[:,1])))
    lens=strip("FP_Lens_520-004",front_rows+[row+np.array((0.,2.45,0.)) for row in front_rows[::-1]],
               mats["lens"],False)
    lens["basis"]="Clear polycarbonate 520-004; edge from R2; optical curvature and thickness estimated"
    return frame


def arch(shell_tree, retainer_tree, sites, brass):
    centers=[front_hit(retainer_tree,*sites[k]) for k in (20,5,6)]
    front_center=centers[1]
    # Broad front foot folds over the front seat, narrows at the centre of
    # the grip, then returns to a narrow footprint on the shell's rear crown.
    # Profile columns are underside y/z, half width, stock thickness, crossfall.
    y0,z0=front_center[1:]
    controls=np.asarray([
        (y0-1.2,z0-6.4,37.4,3.9,3.8),(y0-1.3,z0-4.6,40.6,4.15,4.0),
        (y0+.4,z0+2.0,40.6,4.5,4.1),(y0+2.6,z0+10,38.6,4.9,3.7),
        (-104,284,31,5.1,2.9),(-87,302,22.5,5.5,1.6),
        (-59,320,16,5.8,.8),(-27,330,13.8,5.8,.75),
        (0,331,13.1,5.8,.65),(33,321,12.4,5.5,.6),
        (62,303,11.6,5.,.55),(84,283,11,4.7,.5),
        (96,267,10.8,4.1,.5),(104,257,10.9,3.6,.5),
        (110,251.7,10.1,2.7,.4),(114,247.9,7.6,1.3,.3)],dtype=float)
    def profile(t):
        i=min(int(t),len(controls)-2);u=t-i
        a,b=controls[i:i+2]
        p=controls[i-1] if i else 2*a-b
        q=controls[i+2] if i+2<len(controls) else 2*b-a
        return a+.5*u*((b-p)+u*((2*p-5*a+4*b-q)+u*(-p+3*a-3*b+q)))
    widths=np.linspace(-1,1,23)
    lower,upper=[],[]
    for t in np.linspace(0,len(controls)-1,101):
        y,z,w,thickness,fall=profile(t)
        dy,dz=profile(min(len(controls)-1,t+.015))[:2]-profile(max(0,t-.015))[:2]
        n=unit(np.array((0.,-dz,dy)))
        # At the flared three-screw face the stock is extruded forward. The
        # arch picks up the longitudinal surface normal above this landing.
        blend=np.clip(t-2.7,0,1)
        n=unit((1-blend)*np.array((0.,-1.,0.))+blend*n)
        bottom=[]
        for u in widths:
            x=w*u
            p=np.array((x,y,z-fall*u*u))
            if t<=3.4:
                surface,_=cast(retainer_tree,(x,-230,p[2]),(0,1,0),170)
                if surface is not None:
                    mix=np.clip(3.4-t,0,1)
                    p[1]=(1-mix)*p[1]+mix*(surface[1]-.08)
            if t>=12:
                surface,_=cast(shell_tree,(x,y,420),(0,0,-1),260)
                mix=np.clip((t-12)/1.65,0,1)
                if surface is not None:
                    p[2]=(1-mix)*p[2]+mix*(surface[2]+.05)
            bottom.append(p)
        bottom=np.array(bottom)
        top=bottom+(thickness*(1-.18*widths**6))[:,None]*n
        lower.append(bottom);upper.append(top)
    # Explicit end caps as quads avoid a long nonplanar face across the foot.
    nt,nw=len(lower),len(widths)
    p=np.concatenate((np.array(lower).reshape(-1,3),np.array(upper).reshape(-1,3)))
    faces=[]
    layer=nt*nw
    for base in (0,layer):
        for i in range(nt-1):
            for j in range(nw-1):
                k=base+i*nw+j
                faces.append((k,k+1,k+nw+1,k+nw))
    for j in range(nw-1):
        for k in (j,(nt-1)*nw+j):
            faces.append((k,k+1,k+1+layer,k+layer))
    for i in range(nt-1):
        for j in (0,nw-1):
            k=i*nw+j
            faces.append((k,k+nw,k+nw+layer,k+layer))
    obj=mesh_object("FP_BrassHandle_560-014",p,faces,brass)
    edge=obj.modifiers.new("Rounded outer casting edges","BEVEL")
    edge.width=.0007;edge.segments=3;edge.limit_method="ANGLE";edge.angle_limit=.76
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=edge.name)
    obj["basis"]="17BHDL-1/2: three shared front retainer screws; rear shell fixing"
    return obj


def kidney_plate(frame_tree, pads, axes, row, side, mat, rubber):
    a,b=pads
    spacing=np.linalg.norm(b-a)
    # Follow the lower rim's actual curved corner instead of spanning the
    # two bolts with a flat chord across the lens. The rubber wing fits below.
    ia=int(np.argmin(np.linalg.norm(row-a,axis=1)))
    ib=int(np.argmin(np.linalg.norm(row-b,axis=1)))
    indices=np.arange(len(row))
    def rim(t):
        u=ia+t*(ib-ia)
        return np.array([np.interp(u,indices,row[:,j]) for j in range(3)])
    pa,pb=rim(0),rim(1)
    width=5.8
    lower,upper,axes_by_row=[],[],[]
    for t in np.linspace(-.998*width/spacing,1+.998*width/spacing,28):
        end_distance=max(-t,t-1,0)*spacing
        radius=math.sqrt(width**2-end_distance**2)
        guide=rim(t)+(1-t)*(a-pa)+t*(b-pb)
        direction=unit(axes[0]*(1-t)+axes[1]*t)
        axes_by_row.append(direction)
        cross=unit(np.cross(direction,rim(t+.01)-rim(t-.01)))
        bottom=[];top=[]
        for u in np.linspace(-1,1,7):
            surface=front_hit(frame_tree,guide+u*radius*cross,direction)
            bottom.append(surface+2.7*direction)
            top.append(surface+(4.5-.30*u**4)*direction)
        lower.append(bottom);upper.append(top)
    nl,nw=len(lower),len(lower[0]); layer=nl*nw
    faces=[]
    for base in (0,layer):
        for i in range(nl-1):
            for j in range(nw-1):
                k=base+i*nw+j
                faces.append((k,k+1,k+nw+1,k+nw))
    for i in (0,nl-1):
        for j in range(nw-1):
            k=i*nw+j;faces.append((k,k+1,k+1+layer,k+layer))
    for i in range(nl-1):
        for j in (0,nw-1):
            k=i*nw+j;faces.append((k,k+nw,k+nw+layer,k+layer))
    plate=mesh_object("FP_KidneyPlate_540-015_"+side,
                      np.concatenate((np.reshape(lower,(-1,3)),np.reshape(upper,(-1,3)))),faces,mat)
    # This bolted mounting flange is part of the same moulded exhaust wing.
    # Its front skin is the actual metal undersurface, separated by 0.035 mm
    # for display. Both skins and the wing's future joint share 28 stations.
    directions=np.array(axes_by_row)[:,None,:]
    flange_front=np.array(lower)-.035*directions
    flange_back=flange_front-2.4*directions
    flange=mesh_object("FP_WhiskerMountFlange_"+side,
                       np.concatenate((flange_back.reshape(-1,3),flange_front.reshape(-1,3))),faces,rubber)
    flange["basis"]="Moulded whisker flange held under the 540-015 kidney plate; display thickness estimated"
    ends=np.where(flange_front[:,0,2]<flange_front[:,-1,2],0,nw-1)
    along=np.arange(nl)
    return plate,flange,flange_front[along,ends],flange_back[along,ends]


def build(shell):
    """Create the port and handle; return wing attachment sites in world metres."""
    original=set(bpy.data.objects)
    mats={
        "chrome":material("Port • polished chrome",(.75,.80,.84),.96,.20),
        "steel":material("Port • machined fasteners",(.57,.63,.68),.94,.27),
        "brass":material("Handle • polished warm brass",(.78,.515,.19),.90,.235),
        "rubber":material("Port • graphite elastomer",(.013,.017,.019),0,.57),
        "lens":material("Port • clear polycarbonate",(.965,.991,1.0),0,.073)}
    lens_mat=mats["lens"]
    shader=lens_mat.node_tree.nodes["Principled BSDF"]
    shader.inputs["Transmission Weight"].default_value=1.0
    shader.inputs["IOR"].default_value=1.586
    lens_mat.diffuse_color=(.57,.72,.78,.24)
    rows,sites=shell_landmarks(shell)
    shell_tree=tree(shell)
    frame=window(rows,mats,shell_tree,sites)
    frame_tree=tree(frame)
    seats={n:front_hit(frame_tree,*site) for n,site in sites.items()}
    handle=arch(shell_tree,frame_tree,sites,mats["brass"])
    handle_tree=tree(handle)
    # The rear handle fastener is visible from inside the helmet in 17BHDL-1.
    # Mount its head and washer against the actual inner wall. The hidden
    # threaded bore is not cut into the approved research shell.
    outer_rear,_=cast(shell_tree,(0,105,430),(0,0,-1),240)
    inner_rear,inner_normal=cast(shell_tree,outer_rear-np.array((0.,0.,.08)),(0,0,-1),24)
    if inner_rear is None or inner_normal[2]>=0:
        raise ValueError("Rear handle internal seat missing")
    rays=radial_basis(inner_normal,40)
    washer_profile=[(.03,2.1),(.03,5.0),(.65,5.0),(.65,2.1)]
    strip("FP_HandleRearWasher_530-540",
          [inner_rear+h*inner_normal+r*rays for h,r in washer_profile],mats["steel"])
    screw("FP_HandleRearScrew_530-070",inner_rear+.66*inner_normal,inner_normal,mats["steel"],stem=.63)
    shared={5,6,20};wing={10,11,15,16};nose=13
    mounts={}
    for k in shared:
        mounts[k]=front_hit(handle_tree,*sites[k])
    # Two slotted retainers visible between the upper lateral cross screws
    # on the manufacturer's frontal photo. They are separate from the 16
    # underlying R2 shell recesses, and terminate in the retainer.
    plugs=[]
    for pair in ((7,8),(18,19)):
        a,b=[sites[k] for k in pair]
        center=(a[0]+b[0])*.5
        axis=unit(a[1]+b[1])
        center[0]+=np.sign(center[0])*2.
        plugs.append((front_hit(frame_tree,center,axis),axis))
    drills=[(c,n,4.6 if k==nose else 2.05) for k,(c,n) in sites.items()]
    subtract_drills(frame,drills+[(p,n,1.9) for p,n in plugs],mats["chrome"])
    subtract_drills(handle,[(*sites[k],2.05) for k in shared],mats["brass"])
    for k in sorted(set(sites)-wing-{nose}):
        seat=mounts.get(k,seats[k])
        base,axis=sites[k]
        screw(f"FP_RetainerScrew_{k:02d}",seat,axis,mats["steel"],
              stem=max(2.,float((seat-base)@axis)-.14))
    for j,(p,axis) in enumerate(plugs,1):
        screw(f"FP_PortPlug_530-052_{j}",p,axis,mats["steel"],True,3.65,2.)
    wing_seats,wing_lower_edges,wing_lower_back_edges={},{},{}
    for sign,pair in ((1,(10,11)),(-1,(16,15))):
        side="Port" if sign>0 else "Starboard"
        axes=[sites[k][1] for k in pair]
        plate,flange,front_edge,back_edge=kidney_plate(
            frame_tree,[seats[k] for k in pair],axes,(rows[7]+rows[8])*.5,side,mats["chrome"],mats["rubber"])
        wing_lower_edges[sign]=(front_edge*.001).tolist()
        wing_lower_back_edges[sign]=(back_edge*.001).tolist()
        plate_tree=tree(plate)
        wing_seats[sign]=[((seats[k]+2.7*sites[k][1])*.001).tolist() for k in pair]
        pads=[front_hit(plate_tree,*sites[k]) for k in pair]
        subtract_drills(plate,[(*sites[k],2.05) for k in pair],mats["chrome"])
        subtract_drills(flange,[(*sites[k],3.05) for k in pair],mats["rubber"])
        for k,p in zip(pair,pads):
            base,axis=sites[k]
            # FCPRT-2 / assembly #57: a sleeve through each rubber tab lies
            # between the retaining ring and the back of the kidney plate.
            radial=radial_basis(axis,36)
            sleeve=[]
            for offset,radius in ((.05,2.77),(.17,2.92),(2.51,2.92),(2.65,2.77),
                                  (2.65,1.73),(.05,1.73)):
                sleeve.append([front_hit(frame_tree,seats[k]+radius*r,axis)+offset*axis for r in radial])
            strip(f"FP_WhiskerSpacer_550-061_{k:02d}",sleeve,mats["steel"],True)
            screw(f"FP_WhiskerScrew_530-045_{k}",p,axis,mats["steel"],stem=(p-base)@axis-.18)
    axis=sites[nose][1]
    center=seats[nose]
    turned("FP_NoseGuide_550-116",center,axis,[(-2,4.35),(1,4.35),(1.4,5.7),(3.3,5.7)],
           mats["chrome"])
    turned("FP_NosePackingNut_555-180",center,axis,[(3.3,5.4),(3.8,6.),(7.,6.),(7.4,5.4)],
           mats["steel"],6)
    turned("FP_NoseBlockStem_545-015",center,axis,[(7.4,2.85),(16.5,2.85)],mats["chrome"])
    count=144; rays=radial_basis(axis,count)
    rings=[]
    for j,(h,r) in enumerate([(13.,3.3),(13.6,4.4),(15.,4.7),(18.,4.7),(21.,4.7),(23.,4.55),(23.7,3.95)]):
        knurl = .40*((np.arange(count)+j)%2) if 1<j<6 else 0.
        rings.append(center+h*axis+(r+np.broadcast_to(knurl,count))[:,None]*rays)
    strip("FP_NoseBlockKnob_550-062",rings,mats["steel"],False)
    for obj in set(bpy.data.objects)-original:
        project_uvs(obj)
    free_span={}
    for y in (-45.,0.,45.):
        top,_=cast(handle_tree,(0.,y,420.),(0,0,-1),230)
        underside,_=cast(handle_tree,top-np.array((0.,0.,.03)),(0,0,-1),30)
        crown,_=cast(shell_tree,(0.,y,420.),(0,0,-1),230)
        free_span[str(int(y))]=float(underside[2]-crown[2])
    return {"kidney_pads":wing_seats,"wing_lower_edges":wing_lower_edges,
            "wing_lower_back_edges":wing_lower_back_edges,
            "socket_axes":{k:sites[k][1].tolist() for k in sorted(wing)},"lens":"FP_Lens_520-004",
            "socket_sites":{k:(p*.001).tolist() for k,p in seats.items()},
            "handle_midspan_vertical_clearance_mm":free_span,
            "retainer_screws":11,"whisker_screws":4}
