"""Build the two metre-scale static fittings. Run with Blender --background --factory-startup --python this_file.py.
The exporter writes FBX files beside this script. Geometry is authored in Blender's Z-up frame.
"""
import math
from collections import defaultdict
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

OUTPUT = Path(__file__).resolve().parent
COLORS = {
    "DuskTimber": ((0.24, 0.108, 0.045), 0.0, 0.53),
    "DuskIron": ((0.062, 0.073, 0.074), 0.66, 0.47),
    "DuskBronze": ((0.33, 0.204, 0.096), 0.72, 0.36),
    "DuskStone": ((0.32, 0.284, 0.241), 0.04, 0.78),
}
MATS = {}


class Fittings:
    def __init__(self):
        self.groups = defaultdict(lambda: [[], []])

    def solid(self, verts, polys, finish, fillet):
        points, faces = self.groups[(finish, fillet)]
        start = len(points)
        points.extend(verts)
        faces.extend([tuple(start + i for i in face) for face in polys])

    def prism(self, outline, depth, finish, fillet, plane="XZ"):
        # Extrude a front outline into a closed solid.
        map_coord = {"XZ": lambda a, b, d: (a, d, b),
                     "YZ": lambda a, b, d: (d, a, b),
                     "XY": lambda a, b, d: (a, b, d)}[plane]
        n = len(outline)
        verts = [map_coord(a, b, d) for d in depth for a, b in outline]
        polys = [list(reversed(range(n))), list(range(n, 2*n))]
        polys.extend([(i, (i+1) % n, n+(i+1) % n, n+i) for i in range(n)])
        self.solid(verts, polys, finish, fillet)

    def box(self, center, dims, finish, fillet):
        x, y, z = center
        w, d, h = [v/2 for v in dims]
        self.prism([(x-w,z-h),(x+w,z-h),(x+w,z+h),(x-w,z+h)], (y-d,y+d), finish, fillet)

    def turned(self, start, end, profile, finish, segments=14, fillet=0):
        a, b = Vector(start), Vector(end)
        v = b-a
        normal = v.normalized()
        u = normal.cross(Vector((0, 0, 1)))
        if u.length < 0.01:
            u = normal.cross(Vector((0, 1, 0)))
        u.normalize()
        w = normal.cross(u)
        verts = []
        for fraction, radius in profile:
            for i in range(segments):
                angle = math.tau * i / segments
                verts.append(a + fraction*v + radius*(math.cos(angle)*u+math.sin(angle)*w))
        polys = [list(reversed(range(segments)))]
        for ring in range(len(profile)-1):
            for i in range(segments):
                j = (i+1) % segments
                polys.append((ring*segments+i,ring*segments+j,(ring+1)*segments+j,(ring+1)*segments+i))
        top = (len(profile)-1)*segments
        polys.append([top+i for i in range(segments)])
        self.solid(verts,polys,finish,fillet)

    def rivet(self, surface, direction, radius, finish="DuskBronze"):
        a = Vector(surface)
        b = a + Vector(direction)*radius*.59
        self.turned(a,b,[(0,radius),(.21,radius),(.65,radius*.87),(.9,radius*.60),(1,radius*.27)],finish,14)


def materials():
    for key,(rgb,metal,rough) in COLORS.items():
        m = bpy.data.materials.new(key)
        m.diffuse_color = (*rgb,1)
        m.metallic,m.roughness = metal,rough
        m.use_nodes = True
        bsdf = next(n for n in m.node_tree.nodes if n.type=="BSDF_PRINCIPLED")
        for key2,val in [('Base Color',(*rgb,1)),('Metallic',metal),('Roughness',rough)]:
            bsdf.inputs[key2].default_value = val
        MATS[key] = m


def assemble(frame,name):
    finished = []
    for (finish,radius),(verts,faces) in frame.groups.items():
        mesh = bpy.data.meshes.new(finish)
        mesh.from_pydata(verts,[],faces)
        mesh.update()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(finish,mesh)
        bpy.context.collection.objects.link(obj)
        mesh.materials.append(MATS[finish])
        bpy.context.view_layer.objects.active = obj
        if radius > 0:
            mod=obj.modifiers.new('Soft cast and cut arrises','BEVEL')
            mod.width=radius
            mod.segments=3
            mod.limit_method='ANGLE'
            mod.angle_limit=math.radians(25)
            mod.harden_normals=True
            bpy.ops.object.modifier_apply(modifier=mod.name)
        # Box projection in object metres. Horizontal U remains continuous across the visible
        # boards and rotates naturally around the side of the case.
        uv=mesh.uv_layers.new(name='SurfaceMeters')
        for polygon in mesh.polygons:
            nx,ny,nz=(abs(v) for v in polygon.normal)
            for li in polygon.loop_indices:
                x,y,z=mesh.vertices[mesh.loops[li].vertex_index].co
                if nz>=nx and nz>=ny: u,v=x,y
                elif nx>ny: u,v=y,z
                else: u,v=x,z
                if finish=='DuskTimber':
                    u += (math.floor((polygon.center.z+0.004)/0.18)*0.413) % 1.7
                uv.data[li].uv=(u/1.46+.4,v/1.46+.21)
        finished.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in finished: obj.select_set(True)
    bpy.context.view_layer.objects.active = finished[0]
    bpy.ops.object.join()
    obj=bpy.context.active_object
    obj.name=name
    obj.data.name=name
    # Distinct manufacturing radii were built in separate batches; collapse repeated
    # material indices to one slot per finish on each exported mesh.
    unique=list(dict.fromkeys(slot.name for slot in obj.data.materials))
    old=[slot.name for slot in obj.data.materials]
    old_faces=[p.material_index for p in obj.data.polygons]
    obj.data.materials.clear()
    for slot in unique: obj.data.materials.append(MATS[slot])
    for p,idx in zip(obj.data.polygons,old_faces): p.material_index=unique.index(old[idx])
    file=OUTPUT/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(file),use_selection=True,axis_forward='-Z',axis_up='Y',
                             bake_space_transform=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
                             object_types={'MESH'},mesh_smooth_type='FACE',add_leaf_bones=False,
                             bake_anim=False,path_mode='AUTO',use_mesh_modifiers=True)
    obj.data.calc_loop_triangles()
    print('CRAFT',name,'bounds/blender=',tuple(round(x,4) for x in obj.dimensions),
          'triangles=',len(obj.data.loop_triangles),'finishes=',unique)
    bpy.data.objects.remove(obj,do_unlink=True)


def window():
    f=Fittings()
    # The rounded inner head sits below a square opening sized for the replacement wall.
    for side in (-1,1):
        for i in range(4):
            low=.115+i*.532
            high=min(2.26,low+.530)
            f.box((side*1.538,0,(low+high)/2),(.124,.296,high-low),'DuskStone',.0038)
        f.box((side*1.482,0,1.101),(.076,.186,1.97),'DuskIron',.009)
        for face in (-1,1):
            f.box((side*1.479,face*.091,1.081),(.099,.020,1.907),'DuskIron',.004)
            for h in (.245,.694,1.239,1.810):
                f.rivet((side*1.477,face*.102,h),(0,face,0),.019,'DuskBronze')
    f.box((0,0,.045),(3.20,.378,.090),'DuskStone',.012)
    f.box((0,0,.105),(3.15,.277,.052),'DuskStone',.005)
    f.box((0,0,.157),(2.988,.180,.060),'DuskIron',.006)
    f.box((0,0,.187),(2.90,.092,.024),'DuskBronze',.002)
    def arch(x): return 1.959+.155*(1-(x/1.5)**2)
    # Solid fan of twelve voussoirs, each with a small joint. Metal collar below is continuous.
    for i in range(12):
        l=-1.48+i*2.96/12+.0016
        r=-1.48+(i+1)*2.96/12-.0016
        f.prism([(l,arch(l)+.061),(r,arch(r)+.061),(r,2.26),(l,2.26)],(-.146,.146),'DuskStone',.0033)
    for ofs,th,dd,finish in [(0,.068,.158,'DuskIron'),(-.013,.021,.106,'DuskBronze')]:
        xs=[-1.511+3.022*i/24 for i in range(25)]
        shape=[(x,arch(x)+ofs) for x in xs]+[(x,arch(x)+ofs+th) for x in reversed(xs)]
        f.prism(shape,(-dd/2,dd/2),finish,.003)
    for x in (-.875,-.292,.292,.875):
        h=arch(x)-.181
        f.box((x,0,.181+h/2),(.029,.045,h),'DuskIron',.003)
        f.box((x,0,.181+h/2),(.012,.063,h),'DuskIron',.002)
    for h,wide in ((.763,.031),(1.364,.049)):
        f.box((0,0,h),(2.915,.048,wide),'DuskIron',.003)
        f.box((0,0,h),(2.915,.067,.014),'DuskIron',.0018)
    # Astragal at the meeting rail with a small counterweighted cam catch on each face.
    for face in (-1,1):
        f.box((0,face*.042,1.364),(.109,.021,.081),'DuskIron',.006)
        f.rivet((-.035,face*.054,1.364),(0,face,0),.009)
        f.rivet(( .035,face*.054,1.364),(0,face,0),.009)
        f.turned((0,face*.052,1.364),(.042,face*.085,1.414),
                 [(0,.016),(.22,.012),(.65,.011),(1,.016)],'DuskBronze',14,.0015)
    return f


def case():
    f=Fittings()
    # Keep the front field at depth .680 m: baseline label lettering sits at ~.687 m.
    # Left/back symmetric design works for room sides or flipped instances. Left and
    # right faces just clear the legacy navy panel insets (+/-.692 m).
    for side in (-1,1):
        for i in range(5):
            low=.109+i*.1445
            high=low+.1415
            f.box((0,side*.675,(low+high)/2),(1.328,.010,high-low),'DuskTimber',.0017)
            f.box((side*.693,0,(low+high)/2),(.012,1.346,high-low),'DuskTimber',.0017)
        for level,height in ((.104,.052),(.839,.034)):
            f.box((0,side*.680,level),(1.365,.017,height),'DuskIron',.0035)
            f.box((side*.700,0,level),(.018,1.373,height),'DuskIron',.0035)
        # Sections of clipped strap follow each corner. Fasteners remain clear of text.
        for sx in (-1,1):
            c=sx*.648
            shape=[(c-.026,.112),(c+.026,.112),(c+.026,.791),
                   (c+.020,.827),(c-.020,.827),(c-.026,.791)]
            f.prism(shape,tuple(sorted((side*.681,side*.690))),'DuskIron',.003)
            for level in (.149,.297,.746,.812):
                f.rivet((c,side*.691,level),(0,side,0),.0108)
            c=sx*.649
            shape=[(c-.028,.113),(c+.028,.113),(c+.028,.812),(c+.013,.828),(c-.028,.828)]
            f.prism(shape,tuple(sorted((side*.699,side*.708))),'DuskIron',.0028,'YZ')
            for level in (.149,.307,.746,.807):
                f.rivet((side*.709,c,level),(side,0,0),.0112)
        # Dark lower rail and bolted feet give the fronts a grounded shadow line.
        for x in (-.59,-.205,.205,.59):
            f.rivet((x,side*.690,.104),(0,side,0),.0075,'DuskIron')
        for y in (-.54,.54):
            f.box((side*.701,y,.043),(.038,.114,.073),'DuskIron',.0047)
    return f


if __name__=='__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials()
    assemble(window(),'Fenestration')
    assemble(case(),'BenchCasing')

