"""Build one original SuperLite 17B shell study in an empty Blender scene.

The section coordinates below are visual estimates in mm, not factory dimensions.
Run Blender in background with --python-exit-code 1 -P build_shell.py -- new.blend.
"""
import math
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector


# Half sections go from the middle of the brow over the starboard side to the
# middle of the chin. The opposite half shares the crown and chin tangents.
OPENING_XZ = [(0,245),(37,241),(69,228),(91,207),(101,181),
              (101,155),(87,132),(54,130),(0,137)]
BODY = [
    [(0,-99,246),(46,-95,242),(84,-88,227),(107,-81,206),(118,-83,180),
     (116,-92,150),(99,-108,120),(60,-123,116),(0,-135,120)],
    [(0,-71,268),(52,-70,258),(89,-64,236),(114,-60,210),(125,-64,179),
     (122,-77,145),(103,-103,111),(61,-128,108),(0,-146,113)],
    [(0,-22,286),(57,-23,276),(98,-24,251),(122,-26,216),(131,-31,177),
     (126,-48,142),(106,-86,104),(62,-125,97),(0,-150,100)],
    [(0,27,290),(60,23,279),(101,17,253),(126,7,216),(134,-4,176),
     (127,-26,136),(107,-73,98),(63,-115,86),(0,-149,88)],
    [(0,73,278),(58,66,268),(100,52,245),(126,32,210),(134,18,172),
     (128,-7,134),(107,-59,92),(62,-106,75),(0,-140,77)],
    [(0,113,251),(53,105,245),(95,87,228),(123,63,200),(132,38,165),
     (129,7,125),(107,-47,84),(63,-102,67),(0,-143,67)],
    [(0,137,211),(49,129,207),(91,111,193),(119,81,173),(130,44,146),
     (125,11,112),(104,-42,77),(63,-96,56),(0,-135,54)],
]
N = 160
ANGLES = np.arange(N) * (2*math.pi/N)


def cubic(p0, p1, p2, p3, t):
    """Uniform interpolating cubic between p1 and p2."""
    return p1 + 0.5*t*((p2-p0) + t*((2*p0-5*p1+4*p2-p3) + t*(-p0+3*p1-3*p2+p3)))


def section(half):
    half = np.asarray(half, dtype=float)
    mirror = half[-2:0:-1].copy()
    mirror[:,0] *= -1
    controls = np.vstack((half, mirror))
    result = []
    for angle in ANGLES:
        u = angle*len(controls)/(2*math.pi)
        k = int(u)
        result.append(cubic(*(controls[(k+j) % len(controls)] for j in (-1,0,1,2)), u-k))
    return np.asarray(result)


def face_plane(x, z):
    # A shallow wrap into the temples and a lower edge forward of the brow.
    return -133 + 0.17*(z-190) + 16*(x/106)**2


def window_ring(offset, advance):
    xz = section(OPENING_XZ)
    xz[:,0] *= .95
    xz[:,1] = 187 + .91*(xz[:,1]-185)
    tangent = np.roll(xz, -1, axis=0)-np.roll(xz, 1, axis=0)
    normal = np.column_stack((-tangent[:,1], tangent[:,0]))
    normal /= np.linalg.norm(normal, axis=1)[:,None]
    border = offset + .25*max(0,offset-3)
    x, z = (xz + border*normal).T
    lower_corners = np.exp(-((z-138)/18)**2) * np.clip((abs(x)-44)/40,0,1)
    x += np.sign(x)*max(0,offset-3)*.35*lower_corners
    # The starboard mounting cheek has a clipped outer corner in the diagram.
    cheek = np.exp(-((z-151)/23)**4)*np.clip((-x-67)/28, 0, 1)
    x -= max(0, offset-3)*.25*cheek
    return np.stack((x, face_plane(x,z) - advance, z),axis=1)


def neck_ring(outset, lift):
    # Continue each last longitudinal rail down to the rim at the same
    # plan-view bearing; twisting the broad cheeks around the collar folds them.
    last = section(BODY[-1])
    bearing = np.arctan2(last[:,0]/116, (last[:,1]-8)/132)
    s,c = np.sin(bearing),np.cos(bearing)
    x = (116+outset)*np.sign(s)*abs(s)**.88*(1+.018*c)
    y = 8 + (np.where(c>0,128.,125.)+outset)*np.sign(c)*abs(c)**.90
    return np.column_stack((x,y,72+.42*y+lift))


def body_sections():
    # Knit the window shoulder into the dome, continue over the occiput, and
    # blend into the sloping neck. Each section runs all the way around the shell.
    shoulders = [section(row) for row in BODY]
    for p, minimum in zip(shoulders, (80,73,65,55,44,33,22)):
        clearance = p[:,2] - (81 + .425*p[:,1])
        # Smoothly reserve clearance for the neck returns at the sides.
        p[:,2] += 4*np.logaddexp(0, (minimum-clearance)/4)
    controls = [window_ring(12.7,-3.8), *shoulders,
                neck_ring(2.6,13+3*np.cos(ANGLES)), neck_ring(0.4,7)]
    rings = []
    for i in range(len(controls)-1):
        a, b = controls[i:i+2]
        before = controls[i-1] if i else 2*a-b
        after = controls[i+2] if i+2 < len(controls) else 2*b-a
        for t in np.arange(5)/5:
            p = cubic(before,a,b,after,t)
            # Rounded moulded cheek landing remains after the separate metal
            # weight is lifted off. Keep the bare rear crown uninterrupted.
            x,y,z = p.T
            side = np.clip((abs(x)-86)/32, 0, 1)
            radius = ((y-12)/39)**2+((z-143-.16*y)/34)**2
            amplitude = np.where(x<0, 10.5, 6.)
            p[:,0] += np.sign(x)*amplitude*np.exp(-1.4*radius**3)*side
            # Taper the full aft quarter into the vertical rear wall, leaving
            # the lower lip and the front flange independent of this fairing.
            aft = np.exp(-((y-94)/64)**2 - ((z-202)/58)**2)
            sidewall = np.clip(abs(x)/92,0,1)**2
            p[:,0] -= np.sign(x)*12*sidewall*aft
            p[:,1] -= 8*sidewall*aft
            # Behind the lower sill the cheeks narrow toward the two levels
            # of the chin. The regulator bore has its own local landing.
            fade = np.clip((8.0-i-t)/1.5,0,1)
            chin = np.exp(-((z-89)/34)**4 - ((y+126)/63)**4)*fade**2*(3-2*fade)
            cheeks = np.clip(abs(x)/55,0,1)
            p[:,0] -= np.sign(x)*6.0*cheeks*chin
            p[:,1] += (6+7*cheeks)*chin
            pad = np.exp(-1.25*((x/28)**2+((z-100)/18.5)**2)**2)
            pad *= np.clip((-y-90)/32,0,1)
            p[:,1] += pad*((-146.5 + .12*(z-100))-y)
            sill = np.exp(-((abs(x)-78)/32)**2 - ((z-119)/17)**2 - ((y+123)/40)**2)
            p[:,1] -= 5*sill
            p[:,0] += np.sign(x)*3*sill
            rings.append(p)
    rings.append(controls[-1])
    return rings


def construct():
    # Rings are joined without overlaps. The inner mould is deliberately
    # simpler than the gelcoat side, with no reverse impression of the grooves.
    front = [window_ring(r,d) for r,d in [(0,-1),(.65,.2),(1.7,.6),(2.5,.3),
             (3.1,-.65),(4.8,-.65),(5.4,.55),(6.1,.9),(11,.9),(12.4,.5),(13.0,-.6)]]
    body = body_sections()
    collar = [neck_ring(r,z) for r,z in [(0,4.5),(.9,3.8),(1.5,2.8),(1.5,1.9),
              (.65,1.2),(-.65,.6),(-.75,-.8),(.75,-1.5),(1.25,-2.2),
              (1.2,-3.0),(.45,-3.6),(-.3,-3.9)]]
    inner_neck = [neck_ring(-3.7,-3.9),neck_ring(-4.25,-3.3),neck_ring(-4.3,-1),neck_ring(-4.2,4)]
    centre = np.array((0.,7.,161.))
    inner_body = []
    for i,p in enumerate(body):
        across = np.roll(p,-1,axis=0)-np.roll(p,1,axis=0)
        along = body[min(i+1,len(body)-1)]-body[max(i-1,0)]
        outward = np.cross(across,along)
        outward /= np.linalg.norm(outward,axis=1)[:,None]
        inset = p - 3.0*outward
        neck_inset = (p-centre)*np.array((.963,.964,.972))+centre
        end_blend = np.clip((i-8)/8,0,1)
        inner_body.append((1-end_blend)*inset + end_blend*neck_inset)
    inside_front = [window_ring(7.7,-4.4),window_ring(4,-4.8),window_ring(.9,-4.8),
                    window_ring(0,-4.3)]
    rings = front + body + collar + inner_neck + inner_body[::-1] + inside_front
    vertices = np.concatenate(rings)*.001
    faces = []
    for row in range(len(rings)):
        a,b = row*N, ((row+1)%len(rings))*N
        for j in range(N):
            k=(j+1)%N
            faces.append((a+j,a+k,b+k,b+j))
    mesh=bpy.data.meshes.new("Shell_sections")
    mesh.from_pydata(vertices.tolist(),[],faces)
    mesh.update()
    # Continuous UV strips follow the actual section spacing. The small top
    # band of the atlas is reserved for the aperture walls and socket floors.
    lengths = np.linalg.norm(np.roll(rings,-1,axis=0)-rings,axis=2).mean(axis=1)
    v_breaks = .006 + .867*np.r_[0,np.cumsum(lengths)]/lengths.sum()
    u_breaks = np.linspace(.005,.995,N+1)
    shell_uv = mesh.uv_layers.new(name="ShellUV")
    for face in mesh.polygons:
        i,j=divmod(face.index,N)
        coords=[(u_breaks[j],v_breaks[i]),(u_breaks[j+1],v_breaks[i]),
                (u_breaks[j+1],v_breaks[i+1]),(u_breaks[j],v_breaks[i+1])]
        for loop,uv in zip(face.loop_indices,coords):
            shell_uv.data[loop].uv=uv
    # One moulded part, with a glossy gelcoat on its exterior and a dull
    # interior finish. A face-corner attribute marks only the inside wall.
    finish = mesh.color_attributes.new(name="Inside_wall",type="BYTE_COLOR",domain="CORNER")
    inner_start = len(front)+len(body)+len(collar)-1
    for polygon in mesh.polygons:
        inside = int(inner_start <= polygon.index//N < len(rings)-1)
        for index in polygon.loop_indices:
            finish.data[index].color = (inside,inside,inside,1)
    obj=bpy.data.objects.new("SL17B_YellowShell_Study",mesh)
    bpy.context.collection.objects.link(obj)
    def tag(name, row):
        group=obj.vertex_groups.new(name=name)
        group.add(list(range(row*N,(row+1)*N)),1,"REPLACE")
    tag("Face_Opening",0)
    tag("Neck_Opening",len(front)+len(body)+len(collar))
    tag("Window_Moulding",len(front)-2)
    tag("Neck_Groove",len(front)+len(body)+6)
    bm=bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    # The service bores cross curved quads. Give the Boolean solver the same
    # planar faces that will be rendered; retain quads elsewhere for editing.
    chin_faces = []
    for face in bm.faces:
        p=face.calc_center_median()
        if abs(p.x) < .030 and p.y < -.115 and .046 < p.z < .122:
            chin_faces.append(face)
    bmesh.ops.triangulate(bm,faces=chin_faces)
    bm.to_mesh(mesh)
    bm.free()
    for face in mesh.polygons:
        face.use_smooth=True
    obj["source"] = "Original section study, Kirby Morgan 17B shell 520-065, diagram 241108001 item 13"
    obj["scale_basis"] = "Exhibit-scale estimates; shell section curves and bore sizes are not measured."
    return obj


def recesses(obj):
    """Trim the service openings and blind insert seats from the mould itself."""
    cutters = []
    def cutter(origin, inward, profile, segments):
        direction=Vector(inward).normalized()
        across=direction.cross(Vector((1,0,0)) if abs(direction.x)<.8 else Vector((0,0,1))).normalized()
        up=direction.cross(across)
        verts=[]
        for depth,radius in profile:
            for j in range(segments):
                angle=2*math.pi*(j+.37)/segments
                verts.append((Vector(origin)+depth*direction+
                              radius*(math.cos(angle)*across+math.sin(angle)*up))*.001)
        faces=[tuple(reversed(range(segments)))]
        for k in range(len(profile)-1):
            for j in range(segments):
                a=k*segments+j
                b=k*segments+(j+1)%segments
                faces.append((a,b,b+segments,a+segments))
        faces.append(tuple(range(len(verts)-segments,len(verts))))
        cuts_uv=[[(.25+.17*math.cos(2*math.pi*j/segments),.84+.14*math.sin(2*math.pi*j/segments))
                  for j in reversed(range(segments))]]
        for k in range(len(profile)-1):
            bottom=.04+.60*k/(len(profile)-1)
            top=.04+.60*(k+1)/(len(profile)-1)
            for j in range(segments):
                a,b=.04+.92*j/segments,.04+.92*(j+1)/segments
                cuts_uv.append([(a,bottom),(b,bottom),(b,top),(a,top)])
        cuts_uv.append([(.72+.17*math.cos(2*math.pi*j/segments),.84+.14*math.sin(2*math.pi*j/segments))
                        for j in range(segments)])
        cutters.append((verts,faces,cuts_uv))

    # Front apertures face the observer on the two distinct lower mouldings.
    cutter((0,-155,100),(0,1,-.08),[(-10,13.8),(4,13.8),(6.2,13.0),(35,13.)],64)
    cutter((0,-144,64),(0,1,.18),[(-8,8.8),(2,8.8),(4,8.3),(27,8.3)],48)
    # The other observed holes are represented without fittings or threads.
    cutter((-113,-83,105),(.83,.52,0),[(-24,3.7),(30,3.7)],32)
    for x in (-38,38):
        cutter((x,130,170),(-x/144,-1,.04),[(-24,2.1),(22,2.1)],32)
    # Front insert seats are shallow recesses; the backing wall stays intact.
    face_points = window_ring(8.45,.9)
    sites = [0,9,19,30,42,55,65,73,80,87,95,105,118,130,141,151]
    for index in sites:
        x,y,z=face_points[index]
        inward=Vector((-32*x/(106**2),1,-.17)).normalized()
        cutter((x,y,z),inward,[(-5,2.35),(-.05,2.35),(.5,1.85),(2.15,1.85)],28)
    # The cutters are disjoint temporary solids, evaluated once and discarded.
    vertices, faces=[],[]
    uv_values=[]
    for island,(verts, polygons, uvs) in enumerate(cutters):
        start=len(vertices)
        vertices.extend(verts)
        faces.extend(tuple(start+i for i in face) for face in polygons)
        uv_values.extend(((island+u)/len(cutters),.89+.1*v) for face in uvs for u,v in face)
    mesh=bpy.data.meshes.new("Temporary cutters")
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    uv=mesh.uv_layers.new(name="ShellUV")
    uv.data.foreach_set("uv",np.asarray(uv_values,dtype=np.float32).ravel())
    tool=bpy.data.objects.new("Temporary cutters",mesh)
    bpy.context.collection.objects.link(tool)
    for f in mesh.polygons:
        f.use_smooth=len(f.vertices)==4
    mod=obj.modifiers.new("Shell apertures","BOOLEAN")
    mod.operation="DIFFERENCE"
    mod.solver="EXACT"
    mod.object=tool
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(tool,do_unlink=True)
    bpy.data.meshes.remove(mesh)


def main():
    output=Path(sys.argv[sys.argv.index("--")+1]).resolve()
    if output.exists() or output.suffix.lower()!=".blend":
        raise ValueError("Select a new .blend path.")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    obj=construct()
    recesses(obj)
    obj.data.set_sharp_from_angle(angle=math.radians(48))
    material=bpy.data.materials.new("17B • Yellow gelcoat")
    material.diffuse_color=(.99,.62,.008,1)
    material.use_nodes=True
    shader=material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value=material.diffuse_color
    shader.inputs["Roughness"].default_value=.29
    shader.inputs["Coat Weight"].default_value=.30
    shader.inputs["Coat Roughness"].default_value=.22
    nodes=material.node_tree.nodes
    links=material.node_tree.links
    finish=nodes.new("ShaderNodeVertexColor")
    finish.layer_name="Inside_wall"
    finish.location=(-600,120)
    color=nodes.new("ShaderNodeMixRGB")
    color.label="Gelcoat / inside wall"
    color.inputs[1].default_value=material.diffuse_color
    color.inputs[2].default_value=(.094,.078,.059,1)
    color.location=(-340,180)
    links.new(finish.outputs["Color"],color.inputs[0])
    links.new(color.outputs[0],shader.inputs["Base Color"])
    roughness=nodes.new("ShaderNodeMapRange")
    roughness.inputs["To Min"].default_value=.29
    roughness.inputs["To Max"].default_value=.62
    roughness.location=(-340,-30)
    links.new(finish.outputs["Color"],roughness.inputs["Value"])
    links.new(roughness.outputs["Result"],shader.inputs["Roughness"])
    obj.data.materials.append(material)
    bpy.context.scene.unit_settings.system="METRIC"
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                space=area.spaces.active
                space.region_3d.view_rotation=Vector((.63,.87,-.30)).to_track_quat("-Z","Y")
                space.region_3d.view_distance=.57
                space.region_3d.view_location=(0,0,.16)
                space.clip_start=.001
                space.overlay.show_overlays=False
                space.shading.color_type="MATERIAL"
                space.shading.light="STUDIO"
                space.shading.show_shadows=True
                space.shading.show_cavity=True
                space.shading.cavity_type="WORLD"
    bpy.context.preferences.filepaths.save_version=0
    output.parent.mkdir(exist_ok=True,parents=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output),compress=True)


if __name__=="__main__":
    main()
