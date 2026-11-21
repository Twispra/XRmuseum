"""Visible 17B breathing hardware for the approved R2 exhibit shell.

Coordinates and radii in this module are estimated millimetres. Assembled
relationships follow KM's 17B BlowApart, SuperFlow, Bent Tube and Quad manuals.
"""
import math
import shutil
import subprocess
import tempfile
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


def finish(name, rgb, metal=0, roughness=.35):
    m=bpy.data.materials.new(name)
    m.diffuse_color=(*rgb,1)
    m.use_nodes=True
    bs=m.node_tree.nodes.get("Principled BSDF")
    bs.inputs["Base Color"].default_value=m.diffuse_color
    bs.inputs["Metallic"].default_value=metal
    bs.inputs["Roughness"].default_value=roughness
    return m


def unit(v):
    return Vector(v).normalized()


def basis(direction):
    axis=unit(direction)
    ref=(0,0,1) if abs(axis.z)<.9 else (1,0,0)
    u=unit(axis.cross(Vector(ref)))
    return axis,u,axis.cross(u)


class Hardware:
    def __init__(self, shell):
        self.tree=BVHTree.FromObject(shell,bpy.context.evaluated_depsgraph_get())
        self.chrome=finish("Air • polished chrome",(.64,.69,.73),.95,.18)
        self.steel=finish("Air • machined steel",(.39,.44,.47),.91,.27)
        self.brass=finish("Air • brass",(.69,.42,.108),.84,.28)
        self.black=finish("Air • moulded rubber",(.014,.017,.019),.03,.46)
        self.grip=finish("Air • black handwheel",(.026,.029,.031),.04,.34)
        self.recess=finish("Air • dark recess",(.004,.006,.007),0,.64)
        self.blue=finish("Air • blue transport caps",(.023,.22,.54),.04,.38)
        self.collection=None

    def group(self,name):
        self.collection=bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(self.collection)

    def mesh(self,name,verts,faces,mat,smooth=True,uv=None):
        points=np.asarray(verts,dtype=float)*.001
        center=(points.max(axis=0)+points.min(axis=0))/2
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata((points-center).tolist(),[],faces)
        mesh.update()
        mesh.materials.append(mat)
        # All exportable meshes also have UVs. Stock without a wrapped atlas
        # uses a box projection, suitable for the single solid finish.
        layer=mesh.uv_layers.new(name="UVMap")
        if uv is None:
            lo=points.min(axis=0)
            norm=(points-lo)/np.maximum(points.max(axis=0)-lo,1e-8)
            for p in mesh.polygons:
                axes=np.delete(np.arange(3),int(np.argmax(np.abs(p.normal[:]))))
                for j in p.loop_indices:
                    layer.data[j].uv=norm[mesh.loops[j].vertex_index,axes]
        else:
            layer.data.foreach_set("uv",np.asarray(uv,dtype=np.float32).ravel())
        bm=bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        for p in mesh.polygons:
            p.use_smooth=smooth and len(p.vertices)<=4
        mesh.set_sharp_from_angle(angle=math.radians(44))
        obj=bpy.data.objects.new(name,mesh)
        self.collection.objects.link(obj)
        obj.location=center
        return obj

    def rings(self,name,rings,material,closed=False,smooth=True):
        n=len(rings[0]); rows=len(rings)
        faces=[];uv=[]
        spans=rows if closed else rows-1
        for j in range(spans):
            nxt=(j+1)%rows
            for k in range(n):
                kk=(k+1)%n
                faces.append((j*n+k,j*n+kk,nxt*n+kk,nxt*n+k))
                uv.extend(((k/n,j/spans),((k+1)/n,j/spans),((k+1)/n,(j+1)/spans),(k/n,(j+1)/spans)))
        if not closed:
            faces.extend((tuple(reversed(range(n))),tuple(range((rows-1)*n,rows*n))))
            for seq in (reversed(range(n)),range(n)):
                uv.extend((.5+.48*math.cos(k*math.tau/n),.5+.48*math.sin(k*math.tau/n)) for k in seq)
        return self.mesh(name,[p for r in rings for p in r],faces,material,smooth,uv)

    def turn(self,name,origin,direction,profile,material,n=64,relief=None):
        axis,u,v=basis(direction)
        org=Vector(origin);rings=[]
        for row,(d,r) in enumerate(profile):
            ring=[]
            for j in range(n):
                a=math.tau*j/n
                rad=r+(relief(row,a) if relief else 0)
                ring.append(org+d*axis+(math.cos(a)*u+math.sin(a)*v)*rad)
            rings.append(ring)
        return self.rings(name,rings,material,smooth=n>10)

    def fastener(self,name,origin,axis,radius,depth,mat):
        return self.turn(name,origin,axis,[(0,radius*.89),(.6,radius),(depth-.7,radius),(depth,radius*.89)],mat,6)

    def cuboid(self,name,center,axes,size,mat,bevel):
        ax,ay,az=map(unit,axes);center=Vector(center)
        verts=[center+x*size[0]/2*ax+y*size[1]/2*ay+z*size[2]/2*az
               for z in (-1,1) for y in (-1,1) for x in (-1,1)]
        faces=[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]
        ob=self.mesh(name,verts,faces,mat,False)
        mod=ob.modifiers.new("Cast edge radius","BEVEL")
        mod.width=bevel/1000;mod.segments=4
        mod.harden_normals=True
        return ob

    def slotted(self,name,origin,axis,r):
        axis,u,v=basis(axis); org=Vector(origin)
        verts=[];faces=[]
        for sign in (1,-1):
            a0=math.asin(.12)
            indices=[]
            for depth,radius in ((0,r),(.65,.96*r)):
                ring=[]
                for j in range(25):
                    a=a0+(math.pi-2*a0)*j/24
                    ring.append(len(verts))
                    verts.append(org+depth*axis+u*(radius*math.cos(a))+v*(sign*radius*math.sin(a)))
                indices.append(ring)
            low,high=indices
            faces+=[tuple(low),tuple(high)]
            for i in range(25):
                k=(i+1)%25;faces.append((low[i],low[k],high[k],high[i]))
        return self.mesh(name,verts,faces,self.steel,False)

    def handwheel(self,name,origin,direction,radius,length):
        axis=unit(direction); org=Vector(origin)
        self.turn(name+".stem",org,axis,[(-3,7),(1,8),(7,8)],self.chrome)
        self.fastener(name+".packing",org,axis,10.5,5,self.steel)
        profile=[(4,radius*.81),(5.2,radius*.94),(7,radius),(length-2.3,radius),
                 (length-.7,radius*.94),(length,radius*.84),(length,radius*.48)]
        def flute(row,angle):
            return -radius*.11*(.5+.5*math.cos(12*angle))**2 if 1<=row<=4 else 0
        self.turn(name,org,axis,profile,self.grip,96,flute)
        self.turn(name+".hub",org+axis*length,axis,[(-.6,.47*radius),(.4,.45*radius)],self.recess)
        self.slotted(name+".slotted keeper",org+axis*(length+.41),axis,radius*.36)

    def side_point(self,sign,y,z):
        hit,n,_,_=self.tree.ray_cast((.40*sign,y*.001,z*.001),(-sign,0,0))
        if hit is None:
            raise ValueError(f"Shell seat missing at {sign},{y},{z}")
        return hit*1000,n

    def tube(self,name,points,radius,mat):
        # Catmull–Rom through coupling seats, with tangent-continuous round bends.
        control=list(map(Vector,points))
        padded=[2*control[0]-control[1],*control,2*control[-1]-control[-2]]
        path=[]
        for i in range(len(control)-1):
            a,b,c,d=padded[i:i+4]
            for j in range(8):
                t=j/8
                path.append(b+.5*t*((c-a)+t*((2*a-5*b+4*c-d)+t*(-a+3*b-3*c+d))))
        path.append(control[-1])
        rings=[];u=None
        for i,p in enumerate(path):
            tangent=unit(path[min(i+1,len(path)-1)]-path[max(i-1,0)])
            u=basis(tangent)[1] if u is None else unit(u-tangent*u.dot(tangent))
            v=tangent.cross(u)
            rings.append([p+radius*(u*math.cos(a*math.tau/32)+v*math.sin(a*math.tau/32)) for a in range(32)])
        return self.rings(name,rings,mat)

    def regulator(self):
        self.group("03  •  SuperFlow B and water dump")
        seat=Vector((0,-143,99.4)); front=unit((0,-1,.08))
        self.turn("REG_Mount_550-038",seat,front,[(-8,11.9),(1,12),(2.5,17),(5,17)],self.steel)
        self.turn("REG_Shell gasket",seat,front,[(1,15),(2,17.6),(3.5,17.6)],self.black)
        seat+=Vector((0,-3,0))
        tilt=Matrix.Rotation(math.radians(16),3,"X")
        front=tilt @ front
        self.turn("REG_Body_505-027",seat,front,[(-1.0,13.0),(3,15),(5,19),(9,26),(15,33),
                  (21,38),(25,40.5),(28,41),(30,40)],self.chrome,96)
        self.turn("REG_Diaphragm band",seat,front,[(24,39.4),(25,41.4),(27,42),(30.3,41.8),(31.2,40.6)],self.steel,96)
        self.turn("REG_Chrome cover",seat,front,[(29,39.8),(30.3,41.2),(32.2,40.4),(35,37.8),
                  (38,32),(41.0,25),(41.8,20),(41.2,17.6)],self.chrome,96)
        self.turn("REG_Purge button",seat,front,[(40,18),(41.6,18.6),(42.5,17.6),(43,15)],self.grip,80)
        self.turn("REG_Purge trim",seat,front,[(41.8,17.8),(42.35,18.0),(42.7,17.6),(42.45,17.2)],self.steel,80)
        # Slightly below the circular cover, the left & right bores share a cast barrel.
        self.badge("REG_Purge marking",seat+front*43.03,front,unit((0,-front.z,front.y))*-1,29,29,"purge")
        # The transverse inlet and adjustment barrel sit in the forward
        # quadrant of the housing, in front of the rubber exhaust plenum.
        bar=seat+front*18+tilt@Vector((0,0,-11))+Vector((0,-12,0))
        self.turn("REG_Adjustment barrel",bar,(1,0,0),[(21,11),(31,12),(38,11),(53,9)],self.chrome)
        self.fastener("REG_Adjustment packing",bar+Vector((46,0,0)),(1,0,0),10.8,9,self.steel)
        profile=[(53,8),(54,10),(56,11.3)]
        profile += [(56.2+i*1.05,11.7) for i in range(16)]
        profile += [(73,11),(74.8,9.5),(75,8)]
        def knurl(row,a):
            return (.48 if row%2 else -.48)*math.cos(a*42) if 3<=row<19 else 0
        self.turn("REG_Knurled adjustment wheel",bar,(1,0,0),profile,self.steel,84,knurl)
        inlet=bar+Vector((-26,0,0)); ax=Vector((-1,0,0))
        self.turn("REG_Inlet nipple",inlet,ax,[(-3,10),(3,10),(9,9),(23,9)],self.chrome)
        self.fastener("REG_Inlet jam nut",inlet+ax*9,ax,11.5,7,self.steel)
        self.fastener("TUBE_Regulator end nut",inlet+ax*16,ax,12,14,self.chrome)
        lower=inlet+ax*31
        self.turn("TUBE_Regulator ferrule",lower,ax,[(-2,9.6),(2,9.5),(4,7),(6,5.3)],self.chrome)
        drain=Vector((0,-135,64))
        self.turn("EXH_Water dump body_550-063",drain,(0,-1,-.12),[(-6,7.6),(2,7.9),(4,13),(12,14)],self.black)
        self.turn("EXH_Water dump adapter_560-530",drain,(0,-1,-.12),[(3,13),(6,14.4),(11,14),(13,11)],self.chrome)
        return lower+ax*4,drain

    def badge(self,name,center,normal,up,width,height,style):
        # A thin closed marking carrier, with original typesetting and embedded
        # artwork. Avoid coincident free-standing text geometry.
        normal=unit(normal); right=unit(up.cross(normal));up=unit(normal.cross(right))
        circular=style=="purge"
        if circular:
            outline=[(math.cos(k*math.tau/72),math.sin(k*math.tau/72)) for k in range(72)]
        else:
            outline=[(-1,-1),(1,-1),(1,1),(-1,1)]
        rings=[[center+right*(x*width/2)+up*(y*height/2)+normal*h for x,y in outline] for h in (-.10,.02)]
        mat=finish("Air • "+style+" printed marking",(.22,.23,.23),.12 if circular else .55,.33)
        obj=self.rings(name,rings,mat,smooth=False)
        uv=obj.data.uv_layers.active
        for p in obj.data.polygons:
            for loop_id in p.loop_indices:
                v=obj.data.vertices[obj.data.loops[loop_id].vertex_index]
                world=(v.co+obj.location)*1000-center
                uv.data[loop_id].uv=(.5+world.dot(right)/width,.5+world.dot(up)/height)
        with tempfile.TemporaryDirectory(prefix="sl17b-mark-") as temp:
            path=Path(temp)/(style+".png")
            subprocess.run([shutil.which("python"),str(Path(__file__).with_name("markings.py")),style,str(path)],check=True)
            img=bpy.data.images.load(str(path));img.name="SL17B original print • "+style
            img.pack(); img.filepath="//"+style+".png"
        tex=mat.node_tree.nodes.new("ShaderNodeTexImage");tex.image=img;tex.extension="EXTEND"
        mat.node_tree.links.new(tex.outputs["Color"],mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])

    def sideblock(self):
        self.group("04  •  Side block and supply")
        # Starboard (-X) lies to the left in a frontal view of the exhibit.
        fwd=unit((.27,-.936,.226)); out=unit((-.957,-.29,-.055))
        out=unit(out-fwd*fwd.dot(out)); up=unit(fwd.cross(out))
        if up.z<0: up=-up
        rear=Vector((-161,-46,170)); axis=fwd
        # The standoff follows the curved sidewall under the valve body.
        shellseat,normal=self.side_point(-1,-68,174)
        hub=rear+fwd*23
        self.tube("SB_Mounting boss", [shellseat-.8*normal,hub],11,self.chrome)
        self.turn("SB_Chromed main casting_505-024",rear,axis,[(-5,9),(-4,12),(1,14.4),
                  (7,14.6),(59,13.4),(68,13.1),(71,10.5)],self.chrome,64)
        self.handwheel("SB_Steady flow_520-524",rear+fwd*66,fwd,17.4,27.5)
        block=rear-fwd*7-up*2.5
        self.cuboid("SB_Emergency valve body",block,(fwd,out,up),(23,23,21),self.brass,2.6)
        self.handwheel("SB_Emergency valve_520-525",block+out*9,out,15.2,22.5)
        for prefix,offset in (("SB_EGS",Vector((0,0,0))),("SB_Nonreturn_555-195",-up*27+fwd*3)):
            p=block+offset
            if offset.length:
                self.turn("SB_Nonreturn adapter",rear+fwd*5,-up,[(0,8.7),(21,9),(29,8)],self.brass)
            rearward=-fwd
            self.fastener(prefix+".seat",p,rearward,10.6,12,self.brass)
            self.turn(prefix+".valve",p,rearward,[(9,8),(13,8.7),(19,8.7),(21,7.7),(26,7.7)],self.brass)
            self.fastener(prefix+".flats",p+rearward*17,rearward,9.65,9,self.steel if not offset.length else self.brass)
            self.turn(prefix+".thread relief",p,rearward,[(25,7.5),(30,7.3),(32,6.3)],self.brass)
            self.turn(prefix+".display dust cap",p,rearward,[(29.5,6.7),(31,7.4),(43,7.25),(44.6,6.55)],self.blue,56)
        plate=rear+fwd*33+out*10.3-up*10
        self.cuboid("SB_Identification plate",plate,(fwd,out,up),(44,2.4,21),self.steel,.65)
        self.badge("SB_Model designation",plate+out*1.24,out,up,39,16,"sideblock")
        for d in (-18.4,18.4):
            self.turn("SB_Plate rivet "+str(d),plate+fwd*d+up*6+out, out,[(0,1.25),(.6,1.4),(.95,1.0)],self.chrome,24)
        outlet=rear+fwd*51-up*7
        down=unit((-.07,-.38,-.922))
        self.turn("SB_Bent tube socket",outlet,down,[(0,10),(9,10.3),(13,8.5),(18,8.5)],self.chrome)
        self.fastener("TUBE_Side block end nut",outlet+down*13,down,11.6,12,self.chrome)
        upper=outlet+down*25
        self.turn("TUBE_Side block ferrule",upper,down,[(-1,9),(2.5,8.3),(5.5,5.3)],self.chrome)
        return upper+down*4,down

    def wings(self,drain,port):
        self.group("05  •  Quad exhaust and whiskers")
        plenum=Vector((0,-153,65))
        self.turn("EXH_Quad main body_510-760",plenum,(1,0,0),
                  [(-40,11.5),(-35,14),(-22,16.2),(0,16.8),(22,16.2),(35,14),(40,11.5)],self.black)
        self.tube("EXH_Regulator exhaust neck",[(0,-161,89),(0,-157,76),plenum],12,self.black)
        self.tube("EXH_Dump to plenum",[drain+Vector((0,-9,-1)),plenum],9,self.black)
        for sign,part in ((-1,"510-786"),(1,"510-787")):
            pads=sorted([Vector(p)*1000 for p in port["kidney_pads"][sign]],key=lambda p:abs(p.x))
            outer=pads[1];inner=pads[0]
            tops=[(32*sign,-151,77),(45*sign,-154,80),(56*sign,-157,102),
                  inner+Vector((0,2,-5)),outer+Vector((0,2,-5)),
                  (119*sign,-116,129),(124*sign,-92,121),(124*sign,-81,121)]
            bottoms=[(32*sign,-151,53),(47*sign,-156,49),(61*sign,-160,46),
                     (78*sign,-159,50),(98*sign,-146,60),(114*sign,-124,70),
                     (121*sign,-96,80),(122*sign,-81,81)]
            tops=list(map(Vector,tops));bottoms=list(map(Vector,bottoms))
            widths=[12,13,12,10.5,9.9,9.2,10,10.5]
            # Intermediate sections smooth the long fan without blunting the lips.
            def sample(values,i,t):
                a=values[i-1] if i else values[0]*2-values[1]
                b,c=values[i:i+2]
                d=values[i+2] if i+2<len(values) else c*2-b
                return b+.5*t*((c-a)+t*((a*2-b*5+c*4-d)+t*(-a+b*3-c*3+d)))
            sections=[]
            for i in range(len(tops)-1):
                for j in range(4):
                    t=j/4
                    sections.append((sample(tops,i,t),sample(bottoms,i,t),sample(widths,i,t)))
            sections.append((tops[-1],bottoms[-1],widths[-1]))
            edge=np.array(port["wing_lower_edges"][sign])*1000
            edge=edge[np.argsort(abs(edge[:,0]))]
            xx=abs(edge[:,0])
            def ease(value,low,high):
                t=np.clip((value-low)/(high-low),0,1)
                return float(t*t*(3-2*t))
            for i,(top,base,w) in enumerate(sections):
                x=abs(top.x)
                follow=ease(x,xx[0]-8,xx[0]+1)*(1-ease(x,xx[-1]-1,xx[-1]+9))
                if follow:
                    # The soft upper edge meets the moulded tab trapped under
                    # the kidney plate. The metal face stays fully exposed.
                    tip=Vector((top.x,np.interp(x,xx,edge[:,1])+.8,
                                np.interp(x,xx,edge[:,2])+.9))
                    sections[i]=(top.lerp(tip,follow),base,w)
            def clear_tube(point):
                # A spatial offset shared by the inner and outer surfaces keeps
                # the duct wall continuous while making room for the supply tube.
                relief=ease(abs(point.x),38,58)*(1-ease(abs(point.x),92,117))
                x=abs(point.x)
                high=ease(point.z,106,120)
                outer=ease(x,91,107)*(1-ease(x,113,123))*high
                inner=ease(x,54,63)*(1-ease(x,69,76))*high
                return point+Vector((sign*5*outer,15*relief*(1-ease(point.z,100,124)),
                                     -4*outer-3*inner))
            profiles=[];inner_rings=[]
            for i,(top,base,width) in enumerate(sections):
                prev=sections[max(0,i-1)];nxt=sections[min(i+1,len(sections)-1)]
                travel=(nxt[0]+nxt[1]-prev[0]-prev[1])
                across=unit(travel.cross(top-base))
                ring=[]
                for k in range(44):
                    angle=math.tau*k/44
                    mix=(1+math.sin(angle))*.5
                    side=math.copysign(abs(math.cos(angle))**.80,math.cos(angle))
                    point=base.lerp(top,mix)+across*width*side*(1-.85*ease(mix,.72,1))
                    # Scooped cheek below the mounting lip leaves the bent
                    # supply tube visibly clear of the rubber deflector.
                    ring.append(point)
                center=(top+base)*.5
                profiles.append([clear_tube(p) for p in ring])
                inner_rings.append([clear_tube(center+(p-center)*.84) for p in ring])
            wing=self.rings("EXH_Whisker_"+part,profiles+inner_rings[::-1],self.black,closed=True)
            wing["display_note"]="Hollow swept rubber wing with an open aft exit and a shell-side mounting flange"
            stem=Vector((sign*37,-151,65));d=Vector((sign,0,0))
            self.turn("EXH_Whisker clamp "+part,stem,d,
                      [(-3,13.5),(-2.5,14.4),(2.5,14.4),(3,13.5)],self.grip,52)
            self.turn("EXH_Clamp screw "+part,stem+Vector((0,-13.5,0)),d,
                      [(-4,2.9),(-3,3.1),(2,3.1)],self.brass,24)


def build(shell,port):
    hardware=Hardware(shell)
    lower,drain=hardware.regulator()
    upper,down=hardware.sideblock()
    hardware.tube("TUBE_Bent supply_555-155",
                  [upper,upper+down*12,(-151,-134,117),(-140,-157,93),(-118,-173,80),
                   (-97,-174,79),lower+Vector((-15,0,0)),lower],5.1,hardware.chrome)
    hardware.wings(drain,port)
    return {"bent_tube_seats_mm":[list(upper),list(lower)]}
