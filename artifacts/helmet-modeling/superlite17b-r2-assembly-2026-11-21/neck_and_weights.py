"""Build the 17B neck assembly and modelled ballast on the approved shell.

References: KMDSI 241108001 (items 11, 12, 14, 36, 65 and 66),
250918001 (505-008 exploded view), 260519003 (17BNK-4, 6, 8, 17), and
260519005 (17BHDL-2/3). Coordinates below are visual estimates in mm.
"""
import bisect
import math

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


TAU = 2 * math.pi
X = Vector((1, 0, 0))
Y = Vector((0, 1, 0))
Z = Vector((0, 0, 1))


def shade(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.metallic, mat.roughness = metallic, roughness
    mat.use_nodes = True
    node = mat.node_tree.nodes["Principled BSDF"]
    node.inputs["Base Color"].default_value = (*color, 1)
    node.inputs["Metallic"].default_value = metallic
    node.inputs["Roughness"].default_value = roughness
    return mat


def axis_frame(normal):
    normal = normal.normalized()
    across = (X if abs(normal.x) < .8 else Y)
    across = (across - normal * across.dot(normal)).normalized()
    return across, normal.cross(across)


class NeckAssembly:
    def __init__(self, shell):
        self.collection = bpy.data.collections.new("17B | Neck, restraint and ballast")
        bpy.context.scene.collection.children.link(self.collection)
        self.brass = shade("17B | Polished cast brass", (.79, .49, .16), .90, .225)
        self.steel = shade("17B | Neck hardware, satin stainless", (.63, .69, .72), .92, .23)
        self.dark = shade("17B | Charcoal moulded urethane", (.019, .022, .024), .03, .31)
        self.rubber = shade("17B | Black neoprene", (.012, .016, .020), .00, .53)
        self.blue = shade("17B | Blue inner neck fabric", (.028, .060, .166), .00, .62)
        self.strap = shade("17B | Woven restraint tape", (.021, .024, .027), .00, .77)
        for mat in (self.rubber, self.blue, self.strap):
            nodes = mat.node_tree.nodes
            noise = nodes.new("ShaderNodeTexNoise")
            noise.inputs["Scale"].default_value = 310
            noise.inputs["Detail"].default_value = 2.0
            bump = nodes.new("ShaderNodeBump")
            bump.inputs["Strength"].default_value = .12
            bump.inputs["Distance"].default_value = .00035
            mat.node_tree.links.new(noise.outputs["Fac"], bump.inputs["Height"])
            mat.node_tree.links.new(bump.outputs["Normal"], nodes["Principled BSDF"].inputs["Normal"])

        mesh = shell.data
        mesh.calc_loop_triangles()
        vertices = [1000 * (shell.matrix_world @ v.co) for v in mesh.vertices]
        self.tree = BVHTree.FromPolygons(vertices, [p.vertices[:] for p in mesh.loop_triangles], all_triangles=True)
        group = shell.vertex_groups["Neck_Groove"].index
        rim = [vertices[v.index] for v in mesh.vertices if any(g.group == group and g.weight > .99 for g in v.groups)]
        if len(rim) != 160:
            raise ValueError("Expected the approved shell's 160-point neck groove")
        self.center = (Vector(tuple(min(p[i] for p in rim) for i in range(3))) +
                       Vector(tuple(max(p[i] for p in rim) for i in range(3)))) * .5
        self.rim = sorted(rim, key=self.angle)
        self.angles = [self.angle(p) for p in self.rim]
        # Neck plane measured from three non-collinear saved rim vertices.
        self.n = (self.rim[52]-self.rim[0]).cross(self.rim[105]-self.rim[0]).normalized()
        if self.n.z < 0:
            self.n.negate()
        self.up_slope = self.n.cross(X).normalized()

    def angle(self, point):
        return math.atan2(point.x-self.center.x, point.y-self.center.y) % TAU

    def rim_point(self, angle):
        angle %= TAU
        right = bisect.bisect_right(self.angles, angle) % len(self.rim)
        left = (right-1) % len(self.rim)
        span = (self.angles[right]-self.angles[left]) % TAU
        fraction = ((angle-self.angles[left]) % TAU) / span
        return self.rim[left].lerp(self.rim[right], fraction)

    def on_rim(self, angle, offset=0, height=0):
        tangent = (self.rim_point(angle+.012)-self.rim_point(angle-.012)).normalized()
        outward = self.n.cross(tangent).normalized()
        return self.rim_point(angle) + offset*outward + height*self.n

    def mesh(self, name, verts, faces, materials, face_materials=None, smooth=True):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([Vector(p)*.001 for p in verts], [], faces)
        mesh.update()
        for material in materials:
            mesh.materials.append(material)
        for face in mesh.polygons:
            face.use_smooth = smooth
            if face_materials is not None:
                face.material_index = face_materials[face.index]
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        # Normal-directed box projection; each of the six projections occupies
        # a separate tile. The asset's procedural finishes also work without images.
        coords=[v.co for v in mesh.vertices]
        low=[min(p[i] for p in coords) for i in range(3)]
        extent=[max(max(p[i] for p in coords)-low[i],1e-8) for i in range(3)]
        uvs=mesh.uv_layers.new(name="PartsUV")
        for face in mesh.polygons:
            axis=max(range(3),key=lambda i: abs(face.normal[i]))
            reverse=face.normal[axis]<0
            i,j=(axis+1)%3,(axis+2)%3
            for index in face.loop_indices:
                co=coords[mesh.loops[index].vertex_index]
                u=(co[i]-low[i])/extent[i]
                v=(co[j]-low[j])/extent[j]
                uvs.data[index].uv=((axis+.01+.98*(1-u if reverse else u))/3,
                                    (int(reverse)+.01+.98*v)/2)
        obj = bpy.data.objects.new(name, mesh)
        self.collection.objects.link(obj)
        obj["geometry_basis"] = "KMDSI part relationships; R2 surface fit, proportions estimated."
        return obj

    def loft(self, name, rings, mat, closed_path=True, cross_closed=True, row_mats=None):
        count = len(rings[0])
        faces, colors = [], []
        for i in range(len(rings) if cross_closed else len(rings)-1):
            following = (i+1) % len(rings)
            for j in range(count if closed_path else count-1):
                k = (j+1) % count
                faces.append((i*count+j, i*count+k, following*count+k, following*count+j))
                colors.append(row_mats[i] if row_mats else 0)
        if not closed_path and cross_closed:
            for j in (0, count-1):
                faces.append(tuple(i*count+j for i in range(len(rings))))
                colors.append(0)
        return self.mesh(name,[p for ring in rings for p in ring],faces,mat,colors)

    def round_part(self, name, center, normal, profile, mat, segments=40, hollow=False):
        normal = normal.normalized()
        a,b = axis_frame(normal)
        rings = [[center+normal*depth+radius*(a*math.cos(j*TAU/segments)+b*math.sin(j*TAU/segments))
                  for j in range(segments)] for depth,radius in profile]
        if hollow:
            return self.loft(name, rings, [mat])
        verts = [p for ring in rings for p in ring]
        faces = []
        for i in range(len(rings)-1):
            for j in range(segments):
                k=(j+1)%segments
                faces.append((i*segments+j,i*segments+k,(i+1)*segments+k,(i+1)*segments+j))
        faces.extend([tuple(reversed(range(segments))),tuple(range(len(verts)-segments,len(verts)))])
        obj=self.mesh(name,verts,faces,[mat])
        for face in obj.data.polygons:
            if len(face.vertices)>4:
                face.use_smooth=False
        return obj

    def rod(self, name, start, end, radius, mat, segments=28):
        v=end-start
        d=v.length
        bevel=min(.50, radius*.25, d*.2)
        return self.round_part(name,start,v,[(0,radius*.91),(bevel,radius),
                               (d-bevel,radius),(d,radius*.91)],mat,segments)

    def neck(self):
        angles=self.angles
        # Annular cross section is seated in the saved groove.
        oprofile=[(2.02+1.24*math.cos(k*TAU/12),.55+1.13*math.sin(k*TAU/12)) for k in range(12)]
        self.loft("Bottom O-ring | 510-446",[[self.on_rim(a,r,h) for a in angles] for r,h in oprofile],[self.rubber])

        def sleeve_ring(t,h,d):
            points=[]
            def smooth(x):
                u=max(0,min(1,x))
                return u*u*(3-2*u)
            # Slack drawstring dam as displayed off a diver. The fold sags
            # about one cm past the rigid yoke, then rises back to the neck.
            # Keep the fitted bead and the free aperture independently open.
            fold=smooth((t-.20)/.41)*(1-.70*smooth((t-.76)/.24))
            for a in angles:
                tip=self.center+X*(61*math.sin(a))+self.up_slope*(69*math.cos(a))
                tip+=self.n*(1.1*math.cos(3*a)+.8*math.sin(5*a))
                slack=21.5*fold*(.90+.10*math.cos(a))
                points.append(self.on_rim(a,d,0).lerp(tip,t)+self.n*(h-slack))
            return points
        # Clamp is visible at its forward lever. Over the rest of the band
        # the folded large end of the neck dam covers the steel strip.
        def hem(r,h):
            points=[]
            for a in angles:
                front=max(0.,-math.cos(a))
                cover=1-min(1.,max(0.,(front-.68)/.24))
                points.append(self.on_rim(a,r+cover*2.35,h))
            return points
        outside=[hem(3.55,4.4),hem(4.25,4.0),hem(4.55,2.4),hem(4.55,-.2),
                 hem(4.25,-6.7),hem(3.5,-8.6),hem(1.5,-9.6),
                 sleeve_ring(.18,-12.0,0),sleeve_ring(.39,-13.4,0),sleeve_ring(.62,-10.0,0),
                 sleeve_ring(.80,-4.0,0),sleeve_ring(.95,9.0,0),sleeve_ring(1,17.0,0)]
        inner=[sleeve_ring(.970,17,0),sleeve_ring(.925,9,0),sleeve_ring(.785,-2.5,0),
               sleeve_ring(.615,-8.5,0),sleeve_ring(.39,-11.9,0),sleeve_ring(.18,-10.3,0),
               hem(.0,-8.0),hem(3.30,-5.1),hem(3.30,1.5),hem(3.05,3.3)]
        mats=[0]*len(outside+inner)
        for i in range(8,18): mats[i]=1
        self.loft("Drawstring neck dam | 510-533",outside+inner,[self.rubber,self.blue],row_mats=mats)

        clamp_profile=[(3.7,2.5),(4.4,3.1),(5.1,2.7),(5.3,1.8),(5.3,-5.8),(4.8,-6.9),(3.6,-6.7)]
        clamp_angles=[math.radians(-175+350*i/168) for i in range(169)]
        self.loft("Stainless split neck clamp | 505-055",
                  [[self.on_rim(a,r,h) for a in clamp_angles] for r,h in clamp_profile],[self.steel],False)

        yoke_angles=[math.radians(-143+286*i/148) for i in range(149)]
        # Broad horseshoe, with rounded inner bearing and front opening.
        profile=[(2.8,-10.7),(4.6,-11.8),(4.9,-14.2),(4.0,-17.8),(1.0,-19.5),
                 (-7.2,-20.0),(-17.6,-18.4),(-22.2,-15.2),(-22.0,-12.9),(-18.2,-11.0),(-7,-10.0)]
        self.loft("Urethane yoke | 520-117",[[self.on_rim(a,r,h) for a in yoke_angles] for r,h in profile],
                  [self.dark],False)
        self.yoke_front=yoke_angles[-1]

    def shell_point(self, angle_degrees, above, offset=0):
        a=math.radians(angle_degrees)
        radial=Vector((math.sin(a),math.cos(a),0))
        # Sample at constant elevation above the inclined neck plane.
        radial.z=-(radial.x*self.n.x+radial.y*self.n.y)/self.n.z
        start=self.center+above*Z+radial*240
        hit,normal,_,_=self.tree.ray_cast(start,-radial.normalized(),180)
        at_bore=(hit is not None and hit.y>90 and abs(abs(hit.x)-38)<5.0 and abs(hit.z-170)<4.6)
        if hit is None or normal.dot(radial)<.25 or at_bore:
            # At a small exposed mounting hole use adjacent intact gelcoat.
            samples=[]
            for dz in (-7.,7.):
                q,n,_,_=self.tree.ray_cast(start+Z*dz,-radial.normalized(),180)
                if q is not None and n.dot(radial)>.25:
                    samples.append((q-Z*dz,n))
            if not samples:
                raise ValueError(f"No external shell at {angle_degrees:.2f} deg/{above:.2f} mm")
            hit=sum((p for p,_ in samples),Vector())/len(samples)
            normal=sum((n for _,n in samples),Vector()).normalized()
        return hit+radial.normalized()*offset,normal

    def weights(self):
        # A gently crowned face and a rounded cast edge on a single rear weight.
        nu,nv=80,16
        layers=[]
        for outer in (False,True):
            points=[]
            for i in range(nu+1):
                u=i/nu
                for j in range(nv+1):
                    v=j/nv
                    span=69.0-2.1*(abs(2*v-1)**9)
                    angle=(2*u-1)*span
                    middle=math.cos(math.radians(angle))
                    low=10.5+2.3*middle
                    high=62.0+11.0*middle
                    h=low+(high-low)*v
                    margin=min(u*220,(1-u)*220,v*55,(1-v)*55)
                    shoulder=math.sin(.5*math.pi*min(1,margin/5.0))
                    offset=.36 if not outer else 2.5+3.7*shoulder+.6*math.sin(math.pi*v)
                    points.append(self.shell_point(angle,h,offset)[0])
            layers.extend(points)
        row=nv+1
        layer=(nu+1)*row
        faces=[]
        for extra in (0,layer):
            for i in range(nu):
                for j in range(nv):
                    k=extra+i*row+j
                    faces.append((k,k+row,k+row+1,k+1))
        border=[i*row for i in range(nu+1)]+[nu*row+j for j in range(1,nv+1)]
        border += [i*row+nv for i in range(nu-1,-1,-1)]+[j for j in range(nv-1,0,-1)]
        faces.extend((v,border[(i+1)%len(border)],border[(i+1)%len(border)]+layer,v+layer)
                     for i,v in enumerate(border))
        self.mesh("Continuous rear weight | 560-005",layers,faces,[self.brass])

        # The port shoulder wraps the blank side of the shell. The smaller
        # starboard plate leaves the side-block and inlet train exposed.
        self.side_weight("Port cast weight | 560-019", 1,
            [(102,12),(101,17),(102,26),(105,37),(104,54),(105,65),(111,68),
             (120,66),(127,56),(132,41),(139,24),(145,17),(145,12),(139,10),(119,9)],(119,33))
        self.side_weight("Starboard cast weight | 560-023",-1,
            [(93,14),(94,28),(94,46),(97,56),(104,58),(111,56),(115,51),
             (112,39),(113,26),(111,17),(107,11),(99,10)],(103,33))

        # Existing rear shell holes provide the hidden retaining screw seats.
        shell=bpy.data.objects["SL17B_YellowShell_Study"]
        mesh=shell.data
        uv=mesh.uv_layers["ShellUV"].data
        sockets={3:set(),4:set()}
        for p in mesh.polygons:
            first=uv[p.loop_start].uv
            if first.y<.887: continue
            coords=[uv[i].uv[:] for i in p.loop_indices]
            if min(q[1] for q in coords)<.887:continue
            key=int(21*sum(q[0] for q in coords)/len(coords))
            if key in sockets:sockets[key].update(p.vertices)
        for island,ids in sockets.items():
            if not ids:raise ValueError("Rear weight mounting bore island missing")
            verts=[1000*(shell.matrix_world @ mesh.vertices[i].co) for i in ids]
            center=sum(verts,Vector())/len(verts)
            direction=Vector((center.x/144,1,-.04)).normalized()
            inner=min(verts,key=lambda p:p.dot(direction)).dot(direction)
            seat=center+direction*(inner-center.dot(direction))
            self.round_part(f"Rear weight interior screw {island-2} | 530-032",seat,direction,
                            [(-2.4,2.8),(-1.7,4.0),(-.45,4.0),(0,3.7),(.01,1.9),(7.0,1.9)],self.steel)

    def side_weight(self,name,sign,outline,center):
        controls=[Vector(p) for p in outline]
        contour=[]
        for i,p in enumerate(controls):
            a,b,c,d=[controls[(i+k)%len(controls)] for k in (-1,0,1,2)]
            for j in range(5):
                t=j/5
                contour.append(((1-t)**3*a+(3*t**3-6*t*t+4)*b+
                                (-3*t**3+3*t*t+3*t+1)*c+t**3*d)/6)
        c=Vector(center)
        rings=[]
        # Cap both sides from small nonzero rings and a single central vertex.
        for r,offset in [(.08,.7),(.16,.7),(.25,.7),(.34,.7),(.43,.7),(.52,.7),(.61,.7),(.70,.7),
                         (.79,.7),(.88,.7),(.96,.7),(1,.78),(1,1.8),(.98,3.7),(.93,5.4),
                         (.80,6.4),(.59,7.2),(.34,7.8),(.14,8.0)]:
            ring=[]
            for p in contour:
                a,h=c.lerp(p,r)
                ring.append(self.shell_point(sign*a,h,offset)[0])
            rings.append(ring)
        n=len(contour)
        verts=[p for ring in rings for p in ring]
        faces=[]
        for i in range(len(rings)-1):
            for j in range(n):
                k=(j+1)%n
                faces.append((i*n+j,i*n+k,(i+1)*n+k,(i+1)*n+j))
        for row,offset in ((0,.7),(len(rings)-1,8.1)):
            index=len(verts)
            verts.append(self.shell_point(sign*c.x,c.y,offset)[0])
            faces.extend((index,row*n+j,row*n+(j+1)%n) for j in range(n))
        self.mesh(name,verts,faces,[self.brass])

    def rear_link(self):
        # Horizontal yoke hinge bolt below the alignment sleeve. The latter
        # mounts into the *weight*, on an independent fore-aft axis.
        rear=self.on_rim(0)
        pin=rear+Y*8-self.n*7.5
        foot=self.on_rim(0,-6,-17)
        self.rod("Rear yoke transverse hinge bolt | 530-034",pin-X*17,pin+X*17,3.4,self.steel,40)
        self.round_part("Hinge bolt retaining head",pin-X*17,-X,[(0,4.9),(1.0,5.4),(2.4,5.1)],self.steel)
        self.round_part("Hinge bolt lock nut | 530-028",pin+X*16,X,[(0,5.1),(3.1,5.1),(3.6,4.4)],self.steel,6)
        seat,axis=self.shell_point(0,22.5,6.0)
        self.round_part("Flanged alignment sleeve | 550-339",seat,axis,
                        [(0,4.7),(.5,4.7),(6.5,4.7),(7.2,7.6),(8.9,7.6),
                         (9.4,7.0),(9.4,2.6),(0,2.6)],self.steel,hollow=True)
        self.round_part("Alignment screw | 530-090",seat,axis,
                        [(-2.6,2.4),(8.7,2.4),(9.4,4.2),(10.4,4.2),(11.1,3.6)],self.steel)

        def plate_with_eye(name,high,ax,low,upper_width,lower_width,eye,thickness,mat):
            # Continuous eye plate bends down from its round attachment into
            # its lower foot. An actual bore leaves the mounting pin visible.
            ax=ax.normalized()
            long=low-high
            down=(long-ax*long.dot(ax)).normalized()
            right=down.cross(ax).normalized()
            rings=[]
            for outside,depth in [(False,-.5),(True,-.5),(True,.5),(False,.5)]:
                ring=[]
                for j in range(80):
                    a=TAU*j/80
                    dx,dy=math.sin(a),math.cos(a)
                    if outside:
                        part=dy*dy*(3-2*dy) if dy>0 else 0
                        width=upper_width+(lower_width-upper_width)*part
                        radial_dist=(1/((abs(dx)/width)**3+(abs(dy)/upper_width)**3))**(1/3)
                        p=high+right*(dx*radial_dist)+down*(dy*radial_dist)+part*long
                    else:
                        p=high+eye*(right*dx+down*dy)
                    ring.append(p+ax*(depth*thickness))
                rings.append(ring)
            return self.loft(name,rings,[mat])
        # Two brass arms and one steel hanging tab, with separate pin and sleeve eyes.
        for sign in (-1,1):
            plate_with_eye("Rear yoke hinge arm "+str(sign)+" | 560-026",pin+sign*11*X,X,
                           foot+sign*11*X,6.2,6.7,3.45,4.2,self.brass)
        self.rod("Rear yoke hinge crossfoot | 560-026",foot-13*X,foot+13*X,4.8,self.brass)
        tab_top=seat+axis*4.5
        plate_with_eye("Rear hinge mounting tab | 540-157",tab_top,axis,pin+Z*2,
                       8.5,10.4,4.85,2.4,self.steel)
        self.rod("Rear clamp hinge barrel",pin-8.5*X,pin+8.5*X,5.0,self.steel)
        for sign in (-1,1):
            pos=self.on_rim(0,4,-3.5)+sign*6.6*X
            self.round_part(f"Rear tab fastener {sign} | 530-025",pos,Y,
                            [(-1,2.5),(.5,2.5),(1.1,2.1)],self.steel,28)

    def front_closure(self):
        def band_segment(name,a0,a1,profile,mat):
            angles=[math.radians(a0+(a1-a0)*i/36) for i in range(37)]
            self.loft(name,[[self.on_rim(a,r,h) for a in angles] for r,h in profile],[mat],False)
        # Lever along the *front* clamp, engaging the pull pin on one yoke end.
        band_segment("Over-centre clamp lever | 505-055",146,191,
                     [(5,-.8),(8.5,.9),(9.6,-.4),(9.9,-4.2),(8.8,-5.5),(5,-4.7)],self.steel)
        band_segment("Adjustable clamp clevis | 505-055",184,207,
                     [(5,1.5),(7.8,1.3),(8.9,-.2),(8.7,-5.8),(6.6,-6.7),(5,-5.2)],self.steel)
        for degrees in (149,186,204):
            a=math.radians(degrees)
            p=self.on_rim(a,9,-2.2)
            axis=(self.on_rim(a,10)-self.on_rim(a,9)).normalized()
            self.round_part(f"Clamp lever pivot {degrees}",p,axis,[(-2.2,2.3),(1.3,2.3),(1.6,2.0)],self.steel)
        a=self.yoke_front
        p=self.on_rim(a,3,-14.2)
        outward=(self.on_rim(a,4)-self.on_rim(a,3)).normalized()
        self.round_part("Pull-pin latch catch | 560-051",p,outward,
                        [(-4.0,5.0),(-3,6.6),(2.3,6.6),(4.0,5.8),(6,4.7)],self.brass)
        self.round_part("Yoke pull pin and knob | 550-255",p,outward,
                        [(4,2.3),(9,2.3),(10,5.1),(13,5.3),(14,4.6)],self.brass)
        start=self.on_rim(math.radians(146),8,-2.3)
        self.rod("Bail engaging yoke catch",start,p+outward*2,2.0,self.steel)
        # A short black webbing retainer around the yoke, with the smaller
        # guide loop at the rear hinge. Its belly follows the yoke underside.
        for name,a0,a1 in [("Yoke strap | 505-134",.34,2.18),("Yoke strap guide | 505-138",-.82,-.13)]:
            angles=[a0+(a1-a0)*i/52 for i in range(53)]
            profile=[(-1.2,-19.3),(-.8,-20.4),(-2,-21.0),(-12,-20.9),(-13,-20.3),(-12,-19.1)]
            self.loft(name,[[self.on_rim(a,r,h) for a in angles] for r,h in profile],[self.strap],False)
            for index,a in enumerate((a0,a1)):
                cover=[(5.2,-10.0),(6.1,-12.2),(5.2,-18.5),(3,-20.9),
                       (-12.7,-21.4),(-20.5,-19),(-23.5,-15.4),(-22.6,-12.5),(-19,-11),(-12,-10.0)]
                path=[a-.04+i*.08/5 for i in range(6)]
                self.loft(f"{name} | keeper {index+1}",
                          [[self.on_rim(t,r,h) for t in path] for r,h in cover],[self.strap],False)

    def chin_strap(self):
        # 17BNK-3: each side-weight screw secures the chin-strap snap tab
        # against the inner shell. Place these two fittings behind the two
        # fitted side weights, independently of the external yoke retainer.
        anchors=[]
        for side,angle in (("Starboard",-103),("Port",119)):
            outer,_=self.shell_point(angle,47)
            facing=-X if angle>0 else X
            down=(-Z+Y*.08).normalized()
            across=down.cross(facing).normalized()
            def inside_wall(point,clearance):
                hit,_,_,_=self.tree.ray_cast(Vector((0,point.y,point.z)),-facing,210)
                if hit is None:raise ValueError("Could not seat the internal snap tab")
                return hit+facing*clearance
            screw=inside_wall(outer,1.6)
            snap=inside_wall(screw+down*10,7.0)
            # A radiused tab joins the shell-side screw to its inboard snap.
            rows=[]
            for i in range(20):
                length=-4.2+18.4*i/19
                extra=max(-length,length-10,0)
                width=math.sqrt(4.4**2-extra**2)
                center=screw+down*length
                along=max(0,min(1,length/10))
                lift=1.6+5.4*along
                # Form both faces to the actual inner cheek. The free end
                # folds away just enough to make room for the snap backing.
                cross=[(-width,-.4),(-.55*width,-.65),(0,-.65),(.55*width,-.65),(width,-.4),
                       (width,.4),(.55*width,.65),(0,.65),(-.55*width,.65),(-width,.4)]
                rows.append([inside_wall(center+across*w,lift+d) for w,d in cross])
            rails=[[row[k] for row in rows] for k in range(len(rows[0]))]
            self.loft(side+" inner chin strap snap tab | 545-027",rails,[self.steel],False)
            self.round_part(side+" weight and chin-tab retaining screw",screw,facing,
                            [(-7.7,1.85),(.4,1.85),(.6,3.25),(1.8,3.25),(2.25,2.7)],self.steel,32)
            self.round_part(side+" chin strap snap",snap,facing,
                            [(.55,3.7),(1.3,4.15),(2.7,4.15),(3.2,3.7)],self.steel,36)
            anchors.append(snap+facing*1.7)
        left,right=anchors
        centerline=[]
        segments=72
        for i in range(segments+1):
            t=i/segments
            fraction=.5*t+.25*(1-math.cos(math.pi*t))
            p=left.lerp(right,fraction)
            # Lead inboard from the two buttons before the tape dips under
            # the chin; the outer cheeks curl inward below these anchors.
            sag=math.sin(math.pi*t)**1.8
            p.y-=17*sag
            p.z-=21*sag
            centerline.append(p)
        def frame(i):
            tangent=(centerline[min(segments,i+1)]-centerline[max(0,i-1)]).normalized()
            width=(Y-tangent*Y.dot(tangent)).normalized()
            return width,tangent.cross(width).normalized(),tangent
        profile=[(-5.4,-.75),(5.4,-.75),(6,-.38),(6,.38),(5.4,.75),(-5.4,.75),(-6,.38),(-6,-.38)]
        cross=[]
        for j,p in enumerate(centerline):
            width,normal,_=frame(j)
            end_taper=.62+.38*min(1,min(j,segments-j)/12)
            cross.append([p+width*w*end_taper+normal*h for w,h in profile])
        # Each row below is a longitudinal rail; both webbing ends are capped.
        rails=[[ring[k] for ring in cross] for k in range(len(profile))]
        self.loft("Internal chin strap | 505-130",rails,[self.strap],False)
        k=51
        mid=centerline[k]
        width,normal,tangent=frame(k)
        # The loop and centre bar make the adjuster legible from the neck opening.
        def rounded_frame(outer_radius,length,width_size,rise):
            loop=[]
            for a in range(4):
                angle=a*math.pi*.5
                sx=1 if a in (0,3) else -1
                sy=1 if a in (0,1) else -1
                for j in range(6):
                    t=angle+math.pi*.5*j/5
                    x=sx*(length-outer_radius)+outer_radius*math.cos(t)
                    y=sy*(width_size-outer_radius)+outer_radius*math.sin(t)
                    loop.append(mid+tangent*x+width*y+normal*rise)
            return loop
        loops=[rounded_frame(r,l,w,h) for r,l,w,h in
               [(1.5,7.0,8.3,-.9),(1.8,7.4,8.7,-.4),(1.8,7.4,8.7,1.6),
                (1.3,6.8,8.1,2.1),(.85,4.9,6.2,2.1),(.85,4.9,6.2,-.9)]]
        self.loft("Internal chin strap length adjuster",loops,[self.dark])
        self.rod("Chin adjuster centre bar",mid-width*7+normal*1.5,mid+width*7+normal*1.5,1.2,self.dark,20)


def build(shell):
    """Add only the detachable neck, closure and weights. Shell stays read-only."""
    assembly=NeckAssembly(shell)
    assembly.neck()
    assembly.weights()
    assembly.rear_link()
    assembly.front_closure()
    assembly.chin_strap()
    return list(assembly.collection.objects)
