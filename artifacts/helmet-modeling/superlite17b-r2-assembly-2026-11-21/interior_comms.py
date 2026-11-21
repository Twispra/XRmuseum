"""Exhibit-scale 17B cushion, LVON, air train and two-post communications.

build(shell) adds meshes only. Inputs to the modelling helpers are millimetres.
Sources: 17BHC-1, 17COM-2/5/10/11, bulletin 10/2024, blowapart 241108001.
"""
import math

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


TAU = 2*math.pi


def material(name, rgb, roughness, metallic=0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    for key, value in (("Base Color", mat.diffuse_color), ("Roughness", roughness),
                       ("Metallic", metallic)):
        bsdf.inputs[key].default_value = value
    return mat


def frame(axis, hint=(1, 0, 0)):
    normal = Vector(axis).normalized()
    hint = Vector(hint)
    if abs(normal.dot(hint.normalized())) > .9:
        hint = Vector((0, 1, 0)) if abs(normal.y)<.9 else Vector((0, 0, 1))
    u = (hint - normal*normal.dot(hint)).normalized()
    return u, normal.cross(u), normal


def circle(center, axis, radius, count=32):
    u, v, _ = frame(axis)
    return [Vector(center)+radius*(u*math.cos(TAU*i/count)+v*math.sin(TAU*i/count))
            for i in range(count)]


class Interior:
    def __init__(self, shell):
        self.shell = shell
        self.tree = BVHTree.FromObject(shell, bpy.context.evaluated_depsgraph_get())
        self.collection = bpy.data.collections.new("17B • Cushion, airway and communications")
        bpy.context.scene.collection.children.link(self.collection)
        self.black = material("17B • Inner rubber", (.021,.025,.030), .51)
        self.hard = material("17B • Post insulation", (.033,.035,.038), .32)
        self.mic = material("17B • Microphone rubber", (.032,.035,.039), .63)
        self.cloth = material("17B • Head cushion blue nylon", (.028,.062,.24), .79)
        self.edge = material("17B • Cushion binding", (.012,.018,.036), .81)
        self.diaphragm = material("17B • Earphone face", (.24,.29,.30), .52,.28)
        self.brass = material("17B • Comms brass", (.49,.31,.11), .32,.83)
        self.jacket = material("17B • Comms cable jacket", (.065,.074,.081), .62)
        self.red = material("17B • Red terminal sleeve", (.36,.032,.023), .56)
        self.white = material("17B • White terminal sleeve", (.47,.48,.46), .63)
        self.anchors = {}

    def cast(self, origin, direction):
        start = Vector(origin)*.001
        hit, n, _, _ = self.tree.ray_cast(start, Vector(direction).normalized(), .6)
        return (hit*1000, n) if hit is not None else (None, None)

    def loft(self, name, rings, mats, bands=None, closed=False):
        """Each ring has the same winding and sample count. Link a single solid."""
        count = len(rings[0])
        vertices = [Vector(p)*.001 for ring in rings for p in ring]
        faces, uv_faces, slots = [], [], []
        sections = len(rings) if closed else len(rings)-1
        for k in range(sections):
            next_k = (k+1) % len(rings)
            for i in range(count):
                j=(i+1)%count
                faces.append((k*count+i,k*count+j,next_k*count+j,next_k*count+i))
                uv_faces.append(((i/count,k/sections),((i+1)/count,k/sections),
                                 ((i+1)/count,(k+1)/sections),(i/count,(k+1)/sections)))
                slots.append(bands[k] if bands else 0)
        if not closed:
            for k, order in ((0,list(reversed(range(count)))),(len(rings)-1,list(range(count)))):
                faces.append(tuple(k*count+i for i in order))
                slots.append(0 if not bands else bands[0 if k==0 else -1])
                uv_faces.append([(.5+.48*math.cos(TAU*i/count),.5+.48*math.sin(TAU*i/count))
                                 for i in order])
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata(vertices,[],faces)
        mesh.update()
        for mat in mats:
            mesh.materials.append(mat)
        uv=mesh.uv_layers.new(name="UVMap")
        for face, coords, slot in zip(mesh.polygons,uv_faces,slots):
            face.use_smooth=len(face.vertices)==4
            face.material_index=slot
            for loop, coord in zip(face.loop_indices,coords):
                uv.data[loop].uv=coord
        bm=bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        obj=bpy.data.objects.new(name,mesh)
        self.collection.objects.link(obj)
        return obj

    def turned(self,name,center,axis,profile,mat,count=32,closed=False):
        normal=Vector(axis).normalized()
        rings=[circle(Vector(center)+normal*t,normal,r,count) for t,r in profile]
        return self.loft(name,rings,[mat],closed=closed)

    def tube(self,name,points,radius,mat,segments=12):
        pts=[Vector(p) for p in points]
        rings=[]
        u,_,_=frame(pts[1]-pts[0])
        for i,p in enumerate(pts):
            n=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized()
            u=(u-n*u.dot(n)).normalized()
            v=n.cross(u)
            rings.append([p+radius*(u*math.cos(TAU*j/segments)+v*math.sin(TAU*j/segments))
                          for j in range(segments)])
        return self.loft(name,rings,[mat])

    def cable(self,name,knots,radius,mat):
        knots=[Vector(p) for p in knots]
        samples=[]
        for i,(a,b) in enumerate(zip(knots,knots[1:])):
            previous=knots[i-1] if i else 2*a-b
            after=knots[i+2] if i+2<len(knots) else 2*b-a
            for step in range(6):
                t=step/6
                samples.append(a+.5*t*((b-previous)+t*((2*previous-5*a+4*b-after)
                               +t*(-previous+3*a-3*b+after))))
        return self.tube(name,[*samples,knots[-1]],radius,mat)

    def pockets_and_phones(self):
        # The two foam side pieces have earphone apertures (17BHC-1).
        for sign,side in ((-1,"Right"),(1,"Left")):
            rings=[]
            front=[(t,"front") for t in (0,.12,.34,.55,.75,.91,.97)]
            back=[(t,"back") for t in (.985,.90,.80,.70,.60,.50,.40,.30,.20,.10,0)]
            for fraction,face in [*front,(1,"welt"),*back]:
                ring=[]
                for j in range(80):
                    angle=TAU*j/80
                    c,s=math.cos(angle),math.sin(angle)
                    out_y=7+69*math.copysign(abs(c)**.82,c)+8*s
                    out_z=181+48*math.copysign(abs(s)**.86,s)+.13*(out_y-7)
                    y=(1-fraction)*(8+18*c)+fraction*out_y
                    z=(1-fraction)*(182+18*s)+fraction*out_z
                    hit,_=self.cast((0,y,z),(sign,0,0))
                    if hit is None:
                        raise RuntimeError("Cushion outline lies outside the shell")
                    outer=abs(hit.x)-4
                    inside=min(101-.0016*(y-8)**2-.0023*(z-182)**2,outer-5)
                    inside-=7*math.exp(-((y+42)/37)**2-((z-198)/46)**2)
                    amount={"front":.0,"welt":.48,"back":1.0}[face]
                    inside+=2.8*fraction**5
                    ring.append((sign*((1-amount)*inside+amount*outer),y,z))
                rings.append(ring)
            self.loft(f"Cushion_{side}_ear_pocket",rings,[self.cloth,self.edge],
                      [0]*len(front)+[1]*(1+len(back)),closed=True)
            center=Vector((sign*101.5,8,182))
            toward_face=Vector((-sign,0,0))
            self.turned(f"Earphone_{side}_covered_housing",center,toward_face,
                        [(-5,14.3),(-4.5,17.6),(-1.0,17.8),(.3,17.6),(1.2,16.4),
                         (1.5,14.7)],self.black,56)
            grille=self.turned(f"Earphone_{side}_speaker_face",center,toward_face,
                               [(1.501,14.7),(1.65,14.7),(1.95,13.9)],self.diaphragm,56)
            self.perforate(grille,center+toward_face*1.71,toward_face,1.05,1.8,
                           [(k*3.7,l*3.7) for k in range(-3,4)
                            for l in range(-3,4) if (k*k+l*l)<12])
            self.anchors[side]=center+Vector((0,-13,-12))

    def perforate(self,obj,center,normal,radius,depth,locations):
        """Cut the actual speaker screen or shallow microphone recesses."""
        u,v,n=frame(normal)
        verts=[]; faces=[]
        for x,y in locations:
            base=len(verts)
            angle=.047 if obj.name.startswith("Microphone") else 0
            x,y=x*math.cos(angle)-y*math.sin(angle), x*math.sin(angle)+y*math.cos(angle)
            for offset in (-depth/2,depth/2):
                verts.extend(p*.001 for p in circle(center+x*u+y*v+n*offset,n,radius,12))
            faces.append(tuple(base+i for i in reversed(range(12))))
            faces.append(tuple(base+12+i for i in range(12)))
            for i in range(12):
                j=(i+1)%12
                faces.append((base+i,base+j,base+j+12,base+i+12))
        mesh=bpy.data.meshes.new("Temporary grille cutters")
        mesh.from_pydata(verts,[],faces)
        mesh.update()
        uv=mesh.uv_layers.new(name="UVMap")
        for face in mesh.polygons:
            if len(face.vertices)==4:
                coords=((0,0),(1,0),(1,1),(0,1))
            else:
                coords=[(.5+.45*math.cos(TAU*j/12),.5+.45*math.sin(TAU*j/12)) for j in range(12)]
            for loop,coord in zip(face.loop_indices,coords):
                uv.data[loop].uv=coord
        cutter=bpy.data.objects.new("Temporary grille cutters",mesh)
        self.collection.objects.link(cutter)
        self.subtract(obj,cutter)

    def crown_and_roll(self):
        rings=[]
        for r,depth in ((.02,17),(.40,18),(.80,16),(.97,10),(1,6),(.98,2.7),(.7,2.7),(.02,2.7)):
            ring=[]
            for j in range(72):
                a=TAU*j/72
                x=77*r*math.cos(a)
                y=18+82*r*math.sin(a)
                hit,_=self.cast((x,y,176),(0,0,1))
                if hit is None: raise RuntimeError("No roof above crown pad")
                ring.append((x,y,hit.z-depth))
            rings.append(ring)
        self.loft("Cushion_center_top",rings,[self.cloth,self.edge],[0,0,0,1,1,1,1])
        rings=[]
        for j in range(53):
            t=j/52
            angle=math.radians(-66+132*t)
            radial=Vector((math.sin(angle),math.cos(angle),0))
            z=142+17*math.cos(angle)
            hit,_=self.cast((0,0,z),radial)
            if hit is None: raise RuntimeError("Missing rear neck-roll wall")
            center=hit-radial*17
            end=.30+.70*min(1,t/.12,(1-t)/.12)
            rings.append([center+radial*(12*end*math.cos(TAU*k/32))
                          +Vector((0,0,12*end*math.sin(TAU*k/32))) for k in range(32)])
        self.loft("Cushion_rear_neck_roll",rings,[self.cloth])

    def oral_nasal(self):
        # Two communicating open ends: regulator throat and rear face seal.
        # A rounded triangular face seal gives the nose prominence above the sill.
        rings=[]
        sections=[i/16 for i in range(17)]
        course=[(t,0) for t in sections]+[(t,1) for t in reversed(sections)]
        for t,inside in course:
            ring=[]
            for j in range(80):
                a=TAU*j/80
                x,z=math.sin(a),math.cos(a)
                throat=Vector((13.1*x,-135.5,100+13.1*z))
                seal=Vector((48*x*(1-.39*max(z,0)),-53-12*max(z,0)+4*max(-z,0),
                             139+(53 if z>0 else 44)*z))
                p=throat.lerp(seal,t)
                p.x += 5*math.sin(math.pi*t)*x
                p.z += 10*math.sin(math.pi*t)*max(z,0)**2
                p.y += 13*math.sin(math.pi*t)**.7*max(z,0)**2
                if inside:
                    p.x-=2.1*x
                    p.z-=2.1*z
                ring.append(p)
            rings.append(ring)
        mask=self.loft("LVON_510-724_hollow_mask",rings,[self.black],closed=True)
        # Mic in the mask's side pocket, with the face toward the mouth.
        mic_axis=Vector((1,-.1,0)).normalized()
        mike=self.turned("Microphone_515-029",(-39,-74,133),mic_axis,
                         [(-1,8.0),(-.4,10.1),(1,10.4),(3.5,9.1)],self.mic,40)
        self.perforate(mike,Vector((-39,-74,133))+mic_axis*3.4,mic_axis,.77,1.6,
                       [(u*2.7,v*2.7) for u in range(-2,3)
                        for v in range(-2,3) if u*u+v*v<=5])
        self.anchors["Mic"]=Vector((-42,-76,129))
        self.nose_block(mask)

    def nose_block(self,mask):
        # Use the same measured R2 seat axis as the external guide at socket 13.
        # Only the inner section is modelled here; leave the protected shell intact.
        from port_and_handle import shell_landmarks
        _,sites=shell_landmarks(self.shell)
        floor,outward=(Vector(v) for v in sites[13])
        start,_=self.cast(floor-25*outward,outward)
        if start is None: raise RuntimeError("Missing nose-guide backing wall")
        start-=.15*outward
        mask_tree=BVHTree.FromObject(mask,bpy.context.evaluated_depsgraph_get())
        entry,_,_,_=mask_tree.ray_cast(start*.001,-outward,.080)
        if entry is None: raise RuntimeError("Nose rod does not meet the oral nasal")
        self.bore(mask,entry*1000-2*outward,outward,16,3.15)
        tip=Vector((0,-75,146))
        self.cable("NoseBlock_inner_545-015",[start,start-15*outward,start-37*outward,
                   start-48*outward,Vector((0,-95,117)),Vector((0,-84,134)),tip],2.7,self.brass)
        rings=[]
        for z,rx,ry in ((-1.5,10.8,4.4),(-.8,12.7,5.2),(.4,13.5,5.7),
                        (2.4,13.2,5.5),(3.1,12.3,4.95)):
            ring=[]
            for j in range(48):
                a=TAU*j/48
                x=rx*math.cos(a)
                y=ry*math.sin(a)+.012*x*x
                ring.append(tip+Vector((x,y,z+.08*y+.007*x*x)))
            rings.append(ring)
        self.loft("NoseBlock_inner_pad_510-575",rings,[self.black])
        self.anchors["NoseGuideInside"]=start
        self.anchors["NosePad"]=tip

    def bore(self,obj,center,axis,length,radius):
        cutter=self.turned("Temporary passage",center,axis,[(-length*.5,radius),(length*.5,radius)],self.hard,20)
        self.subtract(obj,cutter)

    def subtract(self,obj,cutter):
        mod=obj.modifiers.new("Open cross-hole","BOOLEAN")
        mod.operation="DIFFERENCE"
        mod.solver="EXACT"
        mod.object=cutter
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mesh=cutter.data
        bpy.data.objects.remove(cutter,do_unlink=True)
        bpy.data.meshes.remove(mesh)

    def binding_posts(self):
        for number,y in enumerate((-87,-59),1):
            z=110 + (y+73)*.2
            root,n=self.cast((-240,y,z),(1,0,0))
            if root is None or root.x > -40: raise RuntimeError("Invalid communications post seat")
            self.turned(f"CommsPost_{number}_base",root,n,
                        [(-1,6.6),(.4,6.9),(1.7,6.7),(2.3,5.5)],self.hard)
            stud=self.turned(f"CommsPost_{number}_515-035",root,n,
                             [(2,4.8),(2.6,5.4),(7.6,5.4),(8.1,4.7),
                              (8.6,4.9),(12.3,4.9),(13.3,4.1)],self.hard)
            earward=Vector((0,1,.60))
            earward=(earward-n*n.dot(earward)).normalized()
            self.bore(stud,root+8.9*n,earward,16,1.25)
            inner,ni=self.cast((0,y,z),(-1,0,0))
            self.turned(f"CommsPost_{number}_inner_washer",inner,ni,
                        [(-.35,4.6),(.9,4.6)],self.brass)
            self.turned(f"CommsPost_{number}_inner_nut",inner,ni,
                        [(.8,3.65),(3.6,3.65),(4.1,3.1)],self.brass,6)
            self.anchors[f"Terminal{number}"]=inner+3.8*ni
            self.anchors[f"Post{number}"]=root
            self.turned(f"Comms_terminal_lug_{number}",inner,ni,
                        [(.86,1.65),(.86,4.0),(1.25,4.0),(1.25,1.65)],self.brass,24,True)
        # Recover the sides of the existing small lateral aperture, then fit
        # the corresponding optional-connector closure onto its mouth.
        samples=[]
        for j in range(16):
            a=TAU*j/16
            hit,n=self.cast((-240,-72+8*math.cos(a),105+8*math.sin(a)),(1,0,0))
            if hit is not None and hit.x < -50: samples.append((hit,n))
        root=sum((p for p,_ in samples),Vector())/len(samples)
        n=sum((n for _,n in samples),Vector()).normalized()
        self.turned("Comms_unused_MWPC_550-043_plug",root,n,
                    [(-.8,6.2),(.3,6.3),(3.1,6.3),(3.5,5.8)],self.hard,6)
        self.anchors["Plug"]=root
        p1,p2=self.anchors["Terminal1"],self.anchors["Terminal2"]
        # The four-contact terminal strip is optional in 17COM-4. Use the
        # documented direct-to-post wiring arrangement of this two-wire study.
        ends=[Vector((-94,-57,128)),Vector((-95,-47,129)),Vector((-73,-66,114))]
        self.cable("Comms_right_ear_internal_lead",
                   [self.anchors["Right"],(-108,-26,158),(-100,-51,141),ends[0]],1.55,self.jacket)
        self.cable("Comms_left_ear_internal_lead",
                   [self.anchors["Left"],(107,31,167),(88,67,154),(46,98,146),
                    (0,103,146),(-62,89,149),(-102,42,151),(-110,1,149),
                    (-103,-36,142),ends[1]],1.55,self.jacket)
        self.cable("Comms_microphone_internal_lead",
                   [self.anchors["Mic"],(-53,-62,118),ends[2]],1.4,self.jacket)
        for i,(name,start) in enumerate(zip(("Right","Left","Microphone"),ends)):
            # Distinct conductors from each cable jacket end at opposite post
            # contacts; the exact internal dress is an exhibit estimate.
            for number,(terminal,colour) in enumerate(((p1,self.red),
                    (p2,self.white if i==2 else self.black)),1):
                finish=terminal+Vector((-.8,i-1,.45*i))
                midpoint=start.lerp(finish,.52)+Vector((3-i*1.2,2*number,2+i))
                self.cable(f"Comms_{name}_conductor_{number}",
                           [start,midpoint,finish],.67,colour)

    def air_train(self):
        group=self.shell.vertex_groups["Face_Opening"].index
        rim=[v.co*1000 for v in self.shell.data.vertices
             if any(g.group==group and g.weight>.99 for g in v.groups)]
        if len(rim)!=160: raise ValueError("Unexpected R2 opening")
        pts=[]
        for j in range(-44,31):
            p=rim[j%160]
            radial=Vector((-p.x/99,0,(190-p.z)/65)).normalized()
            t=max(0,1-(j+44)/13)
            rearward=6.0*t*t*(3-2*t)
            pts.append(p+Vector((0,7+rearward,0))+3.7*radial)
        # The feed block seats on the actual inner cheek, aft of the lens
        # edge. A short elbow joins the curved perforated diffuser.
        wall,normal=self.cast((0,pts[0].y,pts[0].z-12.5),(-1,0,0))
        lead=[wall+4.4*normal,wall+9*normal,
              pts[0]+Vector((-6.5,.5,-5)),pts[0]+Vector((-2.8,.2,-1.8))]
        train=self.tube("AirTrain_545-016_perforated_arc",lead+pts,3.2,self.black,16)
        for j in (18,24,30,36,42,48,54,60,66):
            self.bore(train,pts[j]+Vector((0,-2.9,0)),(0,1,.15),3.6,1.0)
        self.turned("AirTrain_starboard_feed_shoulder",wall,normal,
                    [(-.45,5.8),(.65,6.2),(3.8,6.0),(5.2,4.3)],self.hard,32)
        self.anchors["AirTrainInnerSeat"]=wall


def build(shell):
    """Append the interior and 500-010 post configuration to the accepted shell."""
    interior=Interior(shell)
    interior.pockets_and_phones()
    interior.crown_and_roll()
    interior.oral_nasal()
    interior.binding_posts()
    interior.air_train()
    for obj in interior.collection.objects:
        obj["assembly"]="17B interior and two-post communications study"
        obj["placement"]="Anatomical and local fitting estimate from manufacturer illustrations"
    return {name: list(point*.001) for name,point in interior.anchors.items()}
