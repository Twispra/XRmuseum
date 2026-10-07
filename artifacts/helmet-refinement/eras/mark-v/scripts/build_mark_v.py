"""Editable Mark V Mod.1 exterior study, Blender 4.5 LTS.
Original geometry/textures. Evidence and limitations: ../references/research.zh-CN.md.
Run: blender -b --python scripts/build_mark_v.py -- --output NEW_EMPTY_DIRECTORY
Never run against an existing source file; artist changes are not regenerated.
"""
import bpy, bmesh, math, json, sys, hashlib
from pathlib import Path
from mathutils import Vector
import numpy as np

args=sys.argv[sys.argv.index('--')+1:]
OUT=Path(args[args.index('--output')+1]).resolve()
if (OUT/'sources/mark-v-mod1.blend').exists():
    raise RuntimeError('Existing editable source protected. Generate into a NEW directory and review manually.')
for sub in ['sources/parts','exports','textures','previews','qa']:(OUT/sub).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.name='Mark V Mod.1 - evidence-led exterior study'
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene['reference']='Hampton Roads Naval Museum DESCO specimen tag DATE 12 1 44; exterior study, not scan'
scene['scale_status']='Approximate museum envelope; selected published nominal interfaces. All other dimensions estimated.'
scene['axis_convention']='Z up, -Y front, +X wearer left / front-view right; rear-view left telephone, right air'
parts=bpy.data.collections.new('01_MARK_V_EDITABLE');scene.collection.children.link(parts)
studio=bpy.data.collections.new('90_REVIEW_STUDIO_NOT_EXPORTED');scene.collection.children.link(studio)

def move(ob,collection=parts):
    for c in list(ob.users_collection):c.objects.unlink(ob)
    collection.objects.link(ob);return ob
def empty(name,parent=None):
    ob=bpy.data.objects.new(name,None);parts.objects.link(ob);ob.parent=parent;ob.empty_display_size=.012;return ob
root=empty('MarkVMod1_ROOT');root['historical_accuracy']='Referenced reconstruction; not measured replica'
groups={n:empty(n,root) for n in ['ShellAssembly','FrontSeatAssembly','FrontWindowPivot','SideWindowsAssembly','TopWindowAssembly','CorseletAssembly','AirConnectionAssembly','TelephoneAssembly','ExhaustAssembly','SpitcockAssembly','NeckLockAssembly','InteriorInterpretiveAssembly']}
root['evidence_file']='references/research.zh-CN.md';root['unity_integrated']=False

def mat(name,color,metal=0,rough=.4):
    m=bpy.data.materials.new(name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;m.diffuse_color=(*color,1);return m
def noise(size,seed):
    rng=np.random.default_rng(seed);yy,xx=np.mgrid[:size,:size];field=np.zeros((size,size),dtype=np.float32)
    for f,w in [(6,1),(17,.55),(47,.25),(131,.12)]:
        g=rng.random((f,f)).astype(np.float32);u=xx/size*f;v=yy/size*f;i=u.astype(int);j=v.astype(int);a=u-i;b=v-j
        a=a*a*(3-2*a);b=b*b*(3-2*b)
        field+=w*((1-a)*(1-b)*g[j%f,i%f]+a*(1-b)*g[j%f,(i+1)%f]+(1-a)*b*g[(j+1)%f,i%f]+a*b*g[(j+1)%f,(i+1)%f])
    return (field-field.min())/(field.max()-field.min()),rng
def surface(name,low,high,patina,seed,roughness=.4):
    size=2048;n,rng=noise(size,seed);fine=rng.random(n.shape).astype(np.float32)
    v=np.clip(.20+.72*n,0,1);rgb=np.array(low)[None,None,:]*(1-v[:,:,None])+np.array(high)[None,None,:]*v[:,:,None]
    oxidation=np.clip((n-.68)*3.1,0,.42);rgb=rgb*(1-oxidation[:,:,None])+np.array(patina)[None,None,:]*oxidation[:,:,None]
    # Original fine scratches and occasional nicks; not copied from the specimen.
    scratch=np.zeros((size,size),dtype=np.float32)
    for i in range(210):
        x,y=rng.integers(0,size,2);length=int(rng.integers(12,105));dx=float(rng.uniform(-.12,.12))
        for k in range(length):scratch[(y+k)%size,(x+int(k*dx))%size]=rng.uniform(.15,.42)
    rgb*=.94+.11*fine[:,:,None];rgb+=scratch[:,:,None]*.10
    h=.38*n+.07*fine-.06*scratch;gy,gx=np.gradient(h)
    normal=np.stack((-gx*2.5,-gy*2.5,np.ones_like(n)),axis=2);normal/=np.linalg.norm(normal,axis=2)[:,:,None];normal=normal*.5+.5
    m=mat(name,low,.90,roughness);nt=m.node_tree;p=nt.nodes.get('Principled BSDF')
    for kind,val in [('BaseColor',rgb),('Roughness',np.repeat((roughness+.18*n+.035*fine-.06*scratch)[:,:,None],3,axis=2)),('Normal',normal)]:
        rgba=np.ones((size,size,4),dtype=np.float32);rgba[:,:,:3]=np.clip(val,0,1)
        im=bpy.data.images.new(name+'_'+kind,width=size,height=size,alpha=False);im.colorspace_settings.name='sRGB' if kind=='BaseColor' else 'Non-Color'
        im.pixels.foreach_set(rgba.ravel());im.filepath_raw=str(OUT/'textures'/(name+'_'+kind+'.png'));im.file_format='PNG';im.save();im.pack()
        t=nt.nodes.new('ShaderNodeTexImage');t.image=im
        if kind=='Normal':
            normal_node=nt.nodes.new('ShaderNodeNormalMap');normal_node.inputs['Strength'].default_value=.32
            nt.links.new(t.outputs['Color'],normal_node.inputs['Color']);nt.links.new(normal_node.outputs['Normal'],p.inputs['Normal'])
        else:nt.links.new(t.outputs['Color'],p.inputs['Base Color' if kind=='BaseColor' else 'Roughness'])
    return m
tin=surface('AgedTin_OriginalInterpretation',(.085,.102,.090),(.255,.285,.248),(.040,.115,.072),5424107,.44)
bronze=surface('CastBronze_OriginalInterpretation',(.12,.072,.033),(.35,.24,.115),(.065,.13,.09),5424108,.34)
edge=mat('HandledBronze_EstimatedWear',(.37,.24,.105),.9,.28)
interior=mat('InteriorTin_Approximate',(.40,.44,.43),.82,.46)
leather=mat('LeatherSeal_Approximate',(.035,.022,.015),0,.83)
rubber=mat('FaceSeal_Rubber_Approximate',(.017,.023,.022),0,.80)
glass=mat('HistoricalGlass_VisualPropertiesAssumed',(.84,.92,.93),0,.08)
p=glass.node_tree.nodes.get('Principled BSDF');p.inputs['Transmission Weight'].default_value=.97;p.inputs['IOR'].default_value=1.50
dark=mat('UnresolvedTelephoneInterior',(.016,.022,.019),.2,.64)
slate=mat('StudioSlate',(.026,.035,.044),.2,.43)

def finish(ob,name,material,parent):
    bpy.context.view_layer.update()
    world=ob.matrix_world.copy();move(ob);ob.name=name;ob.parent=parent;ob.matrix_world=world
    if material:ob.data.materials.append(material)
    if ob.type=='MESH':
        for p in ob.data.polygons:p.use_smooth=True
    ob['evidence']='Visible structure referenced; exact geometry estimated. See research and component matrix.'
    return ob
def mesh(name,v,f,material,parent):
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(v,[],f);me.update();bm=bmesh.new();bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
    ob=bpy.data.objects.new(name,me);parts.objects.link(ob);ob.parent=parent
    if material:me.materials.append(material)
    for p in me.polygons:p.use_smooth=True
    ob['evidence']='Geometric approximation; see research and component matrix';return ob
def bevel(ob,width=.0007,segments=3):
    m=ob.modifiers.new('Adjustable small edge radius','BEVEL');m.width=width;m.segments=segments;m.limit_method='ANGLE';return ob
def cylinder(name,r,d,loc,direction=(0,0,1),material=bronze,parent=root,vertices=64):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d,location=loc)
    ob=bpy.context.object;ob.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler();return finish(ob,name,material,parent)
def sphere(name,loc,scale,material=bronze,parent=root):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=40,ring_count=20,radius=1,location=loc)
    ob=bpy.context.object;ob.scale=scale;return finish(ob,name,material,parent)
def ring(name,outer,inner,depth,loc,direction=(0,0,1),material=bronze,parent=root,n=96):
    v=[];f=[]
    for z,r in [(-depth/2,outer),(depth/2,outer),(-depth/2,inner),(depth/2,inner)]:v.extend((r*math.cos(i*2*math.pi/n),r*math.sin(i*2*math.pi/n),z) for i in range(n))
    for i in range(n):
        j=(i+1)%n;f.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
    ob=mesh(name,v,f,material,parent);ob.location=loc;ob.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler();return bevel(ob)
def torus(name,major,minor,loc,direction=(0,0,1),material=bronze,parent=root):
    bpy.ops.mesh.primitive_torus_add(major_segments=72,minor_segments=10,major_radius=major,minor_radius=minor,location=loc)
    ob=bpy.context.object;ob.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler();return finish(ob,name,material,parent)
def rod(name,a,b,r=.003,material=bronze,parent=root,vertices=24):
    a=Vector(a);b=Vector(b);return bevel(cylinder(name,r,(b-a).length,(a+b)/2,b-a,material,parent,vertices),.0003,2)
def curve(name,points,r=.003,material=bronze,parent=root,resolution=3):
    points=list(points);cyclic=(Vector(points[0])-Vector(points[-1])).length<1e-8
    if cyclic:points=points[:-1]
    d=bpy.data.curves.new(name+'_Curve','CURVE');d.dimensions='3D';d.resolution_u=6;d.bevel_depth=r;d.bevel_resolution=resolution;d.use_fill_caps=True
    s=d.splines.new('POLY');s.points.add(len(points)-1)
    s.use_cyclic_u=cyclic
    for p,co in zip(s.points,points):p.co=(*co,1)
    ob=bpy.data.objects.new(name,d);parts.objects.link(ob);ob.parent=parent;d.materials.append(material);return ob
def cut(ob,cutter,label):
    bpy.context.view_layer.objects.active=ob;m=ob.modifiers.new(label,'BOOLEAN');m.operation='DIFFERENCE';m.solver='EXACT';m.object=cutter
    bpy.ops.object.modifier_move_to_index(modifier=m.name,index=0)
    bpy.ops.object.modifier_apply(modifier=m.name);bpy.data.objects.remove(cutter,do_unlink=True)
def screw(name,loc,direction=(0,0,1),parent=root,r=.0025):
    n=Vector(direction).normalized();p=Vector(loc)
    ob=cylinder(name,r,.0025,p,n,edge,parent,32);bevel(ob,.0003,2)
    # Real recessed slot, not black paint over the fastener.
    bpy.ops.mesh.primitive_cube_add(size=1,location=p+n*.00115);tool=bpy.context.object
    tool.scale=(r*1.7,.00065,.0012);tool.rotation_euler=n.to_track_quat('Z','Y').to_euler();cut(ob,tool,'Recessed slot')
    return ob
def thread(name,loc,r,pitch,length,parent,material=edge):
    count=int(length/pitch*48);pts=[]
    for i in range(count+1):
        a=i*2*math.pi/48;pts.append((loc[0]+r*math.cos(a),loc[1]+r*math.sin(a),loc[2]+pitch*i/48))
    ob=curve(name,pts,min(pitch*.20,.00048),material,parent,2);ob['nominal_pitch_m']=pitch;ob['thread_profile']='Visual rounded ridge, not a manufactured thread tolerance';return ob

# Smooth spun dome, hollow and with physical apertures. +X is wearer-left.
profile=[(.200,.115),(.215,.123),(.250,.137),(.305,.143),(.370,.144),(.405,.139),(.436,.126),(.462,.103),(.483,.076),(.499,.040),(.507,.0003)]
def smooth_profile(data,steps=6):
    result=[]
    for j in range(len(data)-1):
        p0=np.array(data[max(0,j-1)]);p1=np.array(data[j]);p2=np.array(data[j+1]);p3=np.array(data[min(len(data)-1,j+2)])
        for k in range(steps):
            t=k/steps;v=.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t);result.append(v)
    result.append(np.array(data[-1]));return result
prof=smooth_profile(profile);N=128;v=[];f=[]
for layer in [0,1]:
    for z,r in prof:
        rad=max(.0001,r-(.0026 if layer else 0))
        for i in range(N):
            a=2*math.pi*i/N;v.append((rad*.97*math.cos(a),rad*math.sin(a),z-(.0026 if layer else 0)))
C=len(prof)*N
for layer in [0,1]:
    for j in range(len(prof)-1):
        for i in range(N):
            a=layer*C+j*N+i;b=layer*C+j*N+(i+1)%N;f.append((a,b,b+N,a+N) if layer==0 else (a+N,b+N,b,a))
for i in range(N):j=(i+1)%N;f.append((i,C+i,C+j,j))
f.extend([tuple((len(prof)-1)*N+i for i in range(N)),tuple(C+(len(prof)-1)*N+i for i in reversed(range(N)))])
body=mesh('Bonnet_HollowCopper_TinFinish',v,f,tin,groups['ShellAssembly']);body['wall_thickness_m']=.0026;body['thickness_status']='Assumption, not a published measurement'
front_z=.347
for name,loc,direction,r,d,scale in [
    ('Front',(0,-.128,front_z),(0,-1,0),.060,.17,(1,1,1)),
    ('WearerLeft',(.126,.001,.336),(1,0,0),.048,.15,(1,1,1)),
    ('WearerRight',(-.126,.001,.336),(-1,0,0),.048,.15,(1,1,1)),
    ('Top',(0,-.085,.461),(0,-.58,.815),.048,.18,(1.42,.88,1))]:
    tool=cylinder('TEMP_'+name,r,d,loc,direction,None,root,96);tool.scale=scale;cut(body,tool,'Physical aperture '+name)
bevel(body,.00038,2)

# Interrupted neck connection: published nominal opening, simplified thread seats.
opening=(8+9/16)*.0254/2;nominal_neck=(9+13/64)*.0254/2
neck=groups['NeckLockAssembly']
ring('LowerNeckRing_NominalOpening',nominal_neck+.004,opening,.018,(0,0,.191),material=bronze,parent=neck)
ring('UpperNeckRing_Female_Approximate',.125,opening,.014,(0,0,.207),material=bronze,parent=neck)
torus('NeckJoint_RolledLip',.123,.002,(0,0,.212),material=edge,parent=neck)
ring('NeckGasket_Leather_Interpretive',.121,opening,.0035,(0,0,.198),material=leather,parent=neck)
for j in range(4):
    for level in range(2):
        pts=[]
        for k in range(37):
            a=j*math.pi/2+math.pi/4*k/36;pts.append((nominal_neck*math.cos(a),nominal_neck*math.sin(a),.192+level*.0254/6+.0254/6*k/288))
        curve('InterruptedThread_%d_%d_SeatApproximation'%(j,level),pts,.0006,edge,neck,2)

# Deep narrow front/back saddle, closed underside and four independently editable brails.
chest=groups['CorseletAssembly'];NN=128
def platepoint(a,t,offset=0):
    xi=opening*math.cos(a);yi=opening*math.sin(a);zi=.184
    xo=.153*math.cos(a);yo=.184*math.sin(a);zo=.129-.111*abs(math.sin(a))**1.5
    return (xi+(xo-xi)*t,yi+(yo-yi)*t,zi+(zo-zi)*(t**.86)+offset)
def plate_mesh(name,a0,a1,t0,t1,top,thick,material):
    steps=max(16,int((a1-a0)/(2*math.pi)*NN));radial=8;vv=[];ff=[]
    for layer in [0,1]:
        for k in range(radial+1):
            t=t0+(t1-t0)*k/radial
            for i in range(steps+1):vv.append(platepoint(a0+(a1-a0)*i/steps,t,top-layer*thick))
    row=steps+1;cnt=(radial+1)*row
    for layer in [0,1]:
        for k in range(radial):
            for i in range(steps):
                q=layer*cnt+k*row+i;face=(q,q+1,q+1+row,q+row);ff.append(face if layer==0 else tuple(reversed(face)))
    # For complete ring, first/last columns coincide and are welded below.
    if abs(a1-a0-2*math.pi)>.001:
        for k in range(radial):
            for i in [0,steps]:q=k*row+i;ff.append((q,q+row,q+row+cnt,q+cnt))
    for i in range(steps):
        for k in [0,radial]:q=k*row+i;ff.append((q,q+cnt,q+cnt+1,q+1))
    ob=mesh(name,vv,ff,material,chest)
    if abs(a1-a0-2*math.pi)<.001:
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.00001)
        # Degenerate closure quads along welded seam can be deleted safely.
        bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001);bm.to_mesh(ob.data);bm.free()
    return bevel(ob,.0007,2)
plate=plate_mesh('Corselet_CopperSaddle_WithUnderside',0,2*math.pi,0,1,0,.0034,tin)
plate_mesh('DressCollar_DarkGasket_Approximate',0,2*math.pi,.86,1.005,.004,.003,leather)
for j in range(4):plate_mesh('Brail_%02d_CurvedClampingStrap'%(j+1),j*math.pi/2+.009,(j+1)*math.pi/2-.009,.86,1.01,.009,.006,bronze)
curve('Corselet_RolledOuterEdge',[platepoint(2*math.pi*i/128,1.01,.004) for i in range(129)],.0022,edge,chest)
def nut(name,loc,normal,large=False,parent=chest):
    n=Vector(normal).normalized();p=Vector(loc)
    ring(name+'_Washer',.011 if large else .009,.0061,.0015,p,n,edge,parent,48)
    cylinder(name+'_Stud',.0058,.024,p+n*.010,n,edge,parent,32)
    # Nominal stud 1/2-12 NS, visible round ridge without fit/tolerance simulation.
    th=thread(name+'_StudThread_12TPI',(0,0,0),.00635,.0254/12,.017,parent)
    th.location=p+n*.008;th.rotation_euler=n.to_track_quat('Z','Y').to_euler()
    core=ring(name+'_WingnutCore',.010 if large else .0085,.0065,.009,p+n*.006,n,bronze,parent,48)
    u=n.to_track_quat('Z','Y')@Vector((1,0,0));vv=[];ff=[]
    shape=[(-.019,.011),(-.018,.018),(-.013,.019),(-.008,.011),(-.007,.001),(.007,.001),(.008,.011),(.013,.019),(.018,.018),(.019,.011),(.011,.006),(-.011,.006)]
    if not large:shape=[(x*.85,z*.85) for x,z in shape]
    w=n.cross(u).normalized()
    for dep in [-.0023,.0023]:vv.extend(p+u*x+n*z+w*dep for x,z in shape)
    k=len(shape);ff=[tuple(range(k)),tuple(reversed(range(k,2*k)))]
    ff.extend((i,(i+1)%k,(i+1)%k+k,i+k) for i in range(k));bevel(mesh(name+'_CastWingnut',vv,ff,bronze,parent),.0013,3)
    if large:ring(name+'_FlangedBase',.011,.0065,.002,p+n*.003,n,bronze,parent,48)
for j in range(12):
    a=j*math.pi/6;pos=platepoint(a,.955,.012)
    inner=Vector(platepoint(a,.92));outer=Vector(platepoint(a,.99));tangent=Vector(platepoint(a+.01,.955))-Vector(platepoint(a-.01,.955));n=tangent.cross(outer-inner).normalized()
    if n.z<0:n=-n
    nut('BrailFastener_%02d'%(j+1),pos,n,j%3==0)
# Eye pads with rings; rope is separate and not fabricated into the helmet.
for sign in [-1,1]:
    pos=Vector(platepoint(-math.pi/2+sign*.52,.52,.006));n=Vector((0,-.8,.6))
    sphere('EyePadBase_'+str(sign),pos,(.020,.009,.010),bronze,chest)
    t=torus('FrontEyelet_'+str(sign),.012,.003,pos+Vector((0,-.006,.008)),(0,-1,0),edge,chest)
    for j in [-1,1]:screw('EyePadRivet_'+str(sign)+'_'+str(j),pos+Vector((j*.012,-.003,0)),n,chest,.0028)
# No false serial/date engraving. Oval dimensions approximate, original tag text omitted.
pos=Vector(platepoint(-math.pi/2,.62,.004));vv=[];ff=[];u=Vector((1,0,0));w=Vector((0,.65,.76));n=u.cross(w).normalized()
for dep in [-.0008,.0008]:
    for j in range(64):
        a=j*2*math.pi/64;vv.append(pos+u*(.031*math.cos(a))+w*(.011*math.sin(a)*abs(math.sin(a)))+n*dep)
ff=[tuple(reversed(range(64))),tuple(range(64,128))]
ff.extend((i,(i+1)%64,(i+1)%64+64,i+64) for i in range(64))
ob=bevel(mesh('Nameplate_WartimeFootball_Untranscribed',vv,ff,bronze,chest),.0005,2)
for sign in [-1,1]:screw('NameplateRivet_'+str(sign),Vector(pos)+Vector((sign*.026,-.001,0)),(0,-.8,.6),chest,.0018)

# Front seat, glass, seated seal and diamond guard, all with actual aperture clearance.
seat=groups['FrontSeatAssembly'];door=groups['FrontWindowPivot'];normal=(0,-1,0)
ring('FrontSeat_CastFrame',.086,.060,.024,(0,-.139,front_z),normal,bronze,seat)
ring('FaceplateSeal_Rubber',.073,.0595,.004,(0,-.153,front_z),normal,rubber,seat)
ring('Door_CastFrame',.080,.059,.014,(0,-.162,front_z),normal,bronze,door)
torus('Door_HandledOuterLip',.077,.0024,(0,-.172,front_z),normal,edge,door)
ring('Door_InternalFollowerRing',.064,.058,.006,(0,-.158,front_z),normal,edge,door)
g=cylinder('FrontGlass_12point7mm_SpecimenApproximation',.0594,.0127,(0,-.157,front_z),normal,glass,door,96)
g['glass_thickness_basis']='Museum says approximately half an inch; not a measured engineering tolerance';bevel(g,.0007,3)
# Four diagonal curved strips form a central diamond, leaving the centre open.
def guard_strip(name,pts,width,depth,parent,direction=(0,-1,0)):
    vv=[];ff=[]
    for k,point in enumerate(pts):
        tangent=Vector(pts[min(k+1,len(pts)-1)])-Vector(pts[max(k-1,0)]);n=Vector(direction).normalized();u=tangent.cross(n).normalized()
        vv.extend(Vector(point)+su*u*width/2+sn*n*depth/2 for su,sn in [(-1,-1),(1,-1),(1,1),(-1,1)])
    for k in range(len(pts)-1):
        for j in range(4):a=k*4+j;b=k*4+(j+1)%4;ff.append((a,b,b+4,a+4))
    ff.extend([(3,2,1,0),tuple((len(pts)-1)*4+j for j in range(4))]);return bevel(mesh(name,vv,ff,bronze,parent),.0010,3)
for slope in [-1,1]:
    for intercept in [-.057,.057]:
        # Intersection of z=slope*x+intercept with circle radius .073.
        roots=np.roots([2,2*slope*intercept,intercept*intercept-.073**2]);lo,hi=sorted(roots);pts=[]
        for k in range(17):
            x=lo+(hi-lo)*k/16;z=slope*x+intercept;bow=.004*(1-(x*x+z*z)/.073**2);pts.append((float(x),-.180-bow,front_z+float(z)))
        guard_strip('DiamondGuard_%d_%s'%(slope,str(intercept)),pts,.007,.005,door)
for j in range(4):
    a=math.pi/4+j*math.pi/2;screw('DoorGuardScrew_'+str(j),(.073*math.cos(a),-.179,front_z+.073*math.sin(a)),normal,door,.003)
# Right in front view: fixed/moving alternating hinge knuckles, vertical removable pin.
for j in range(5):
    par=seat if j%2==0 else door
    cylinder('HingeKnuckle_'+str(j),.008,.014,(.087,-.146,front_z+(j-2)*.015),(0,0,1),bronze,par,48)
rod('HingePin_Removable',(.087,-.146,front_z-.044),(.087,-.146,front_z+.044),.0033,edge,seat)
sphere('HingePinTopButton',(.087,-.146,front_z+.046),(.0055,.0055,.0028),edge,seat)
for z in [-.030,.030]:
    rod('HingeLeafFixed_'+str(z),(.066,-.136,front_z+z),(.087,-.146,front_z+z),.005,bronze,seat)
    rod('HingeLeafDoor_'+str(z),(.070,-.162,front_z+z),(.087,-.146,front_z+z),.0045,bronze,door)
# Left in front view: toggle bolt and nut; separate lock element, no auto-unlocking fiction.
toggle=cylinder('DoorToggleBolt_13TPI_Nominal',.0056,.031,(-.089,-.157,front_z),(0,-1,0),edge,seat,32)
th=thread('DoorToggleThread_13TPI',(0,0,0),.00635,.0254/13,.024,seat);th.location=(-.089,-.143,front_z);th.rotation_euler=Vector((0,-1,0)).to_track_quat('Z','Y').to_euler()
ring('DoorToggleNutCore',.009,.0065,.009,(-.089,-.177,front_z),normal,bronze,seat)
for sign in [-1,1]:sphere('DoorToggleWing_'+str(sign),(-.089+sign*.014,-.181,front_z),(.011,.004,.0048),bronze,seat)
rod('DoorToggleFork_Upper',(-.082,-.166,front_z+.010),(-.097,-.166,front_z+.008),.0035,bronze,door)
rod('DoorToggleFork_Lower',(-.082,-.166,front_z-.010),(-.097,-.166,front_z-.008),.0035,bronze,door)

# Round side lights with bowed two-by-two guard strips.
side=groups['SideWindowsAssembly']
for sign in [-1,1]:
    direction=(sign,0,0);centre=(sign*.139,0,.337)
    ring('SideSeat_'+str(sign),.067,.048,.020,centre,direction,bronze,side)
    ring('SideGlassRetainer_'+str(sign),.054,.047,.005,(sign*.154,0,.337),direction,edge,side)
    bevel(cylinder('SideGlass_'+str(sign),.048,.0127,(sign*.147,0,.337),direction,glass,side,80),.0005,2)
    for offset in [-.017,.017]:
        extent=math.sqrt(.062**2-offset**2)
        for axis in ['v','h']:
            pts=[]
            for k in range(17):
                u=-extent+2*extent*k/16;bow=.010*(1-(u*u+offset*offset)/.062**2)
                pts.append((sign*(.165+bow),offset if axis=='v' else u,.337+(u if axis=='v' else offset)))
            guard_strip('SideGuard_'+str(sign)+axis+str(offset),pts,.007,.0045,side,(sign,0,0))
    for j in range(4):
        a=j*math.pi/2+math.pi/4;screw('SideGuardScrew_'+str(sign)+'_'+str(j),(sign*.160,.059*math.cos(a),.337+.059*math.sin(a)),direction,side,.0027)

# Top elliptical light, tilted forward. Its exact tilt/proportion is photographic estimate.
top=groups['TopWindowAssembly'];topcentre=Vector((0,-.085,.462));topdir=Vector((0,-.58,.815));q=topdir.to_track_quat('Z','Y')
for name,outer,inner,depth,offset,material in [('TopSeat',.066,.048,.018,0,bronze),('TopRetainer',.053,.047,.005,.015,edge)]:
    ob=ring(name,outer,inner,depth,topcentre+topdir*offset,topdir,material,top);ob.scale=(1.42,.88,1)
ob=cylinder('TopGlass',.048,.0127,topcentre+topdir*.009,topdir,glass,top,80);ob.scale=(1.42,.88,1)
for x in [-.024,.024]:
    extent=.056*math.sqrt(1-(x/.086)**2);pts=[]
    for k in range(17):
        y=-extent+2*extent*k/16;pts.append(topcentre+q@Vector((x,y,.024+.008*(1-(y/extent)**2))))
    guard_strip('TopGuard_V_'+str(x),pts,.006,.004,top,topdir)
for y in [-.025,0,.025]:
    extent=.086*math.sqrt(1-(y/.056)**2);pts=[]
    for k in range(17):
        x=-extent+2*extent*k/16;pts.append(topcentre+q@Vector((x,y,.024+.008*(1-(x/extent)**2))))
    guard_strip('TopGuard_H_'+str(y),pts,.006,.004,top,topdir)
for j in range(4):
    a=j*math.pi/2+math.pi/4;screw('TopGuardScrew_'+str(j),topcentre+q@Vector((.087*math.cos(a),.054*math.sin(a),.023)),topdir,top,.0027)

# Telephone cup is NOT an extra window. External outline only; innards are illustrative.
telephone=groups['TelephoneAssembly'];cupcentre=Vector((.080,-.090,.421));cupdir=Vector((.40,-.72,.55)).normalized()
ring('TelephoneCup_Mount',.036,.013,.016,cupcentre,cupdir,bronze,telephone)
cup=cylinder('TelephoneCup_ClosedOuterCover',.035,.016,cupcentre+cupdir*.012,cupdir,tin,telephone,80);bevel(cup,.003,4)
rod('TelephoneCup_UpperShoulder',(.061,-.068,.444),(.054,-.062,.454),.015,bronze,telephone)
for sign in [-1,1]:screw('TelephoneCupCoverScrew_'+str(sign),cupcentre+cupdir*.021+Vector((sign*.025,0,0)),cupdir,telephone,.0022)

# Hollow cast goose necks, separate air non-return valve and telephone coupling.
def tube(name,centres,outer,inner,parent,material=bronze,n=48):
    vv=[];ff=[];frames=[]
    for j,c in enumerate(centres):
        t=Vector(centres[min(j+1,len(centres)-1)])-Vector(centres[max(j-1,0)]);t.normalize()
        u=Vector((1,0,0));u=(u-t*u.dot(t)).normalized();w=t.cross(u).normalized();frames.append((u,w))
    for r in [outer,inner]:
        for c,(u,w) in zip(centres,frames):vv.extend(Vector(c)+r*(math.cos(i*2*math.pi/n)*u+math.sin(i*2*math.pi/n)*w) for i in range(n))
    cnt=n*len(centres)
    for layer in [0,1]:
        for j in range(len(centres)-1):
            for i in range(n):a=layer*cnt+j*n+i;b=layer*cnt+j*n+(i+1)%n;ff.append((a,b,b+n,a+n))
    for j in [0,len(centres)-1]:
        for i in range(n):a=j*n+i;b=j*n+(i+1)%n;ff.append((a,a+cnt,b+cnt,b))
    return mesh(name,vv,ff,material,parent)
air=groups['AirConnectionAssembly']
for sign,parent,label,outer,inner in [(-1,air,'Air',.0145,.009),(1,telephone,'Telephone',.0218,.010)]:
    mount=Vector((sign*.065,.119,.344));direction=Vector((0,1,.05))
    ring(label+'GooseneckMount',.029,.011,.008,mount,direction,bronze,parent)
    for j in range(3):
        a=j*2*math.pi/3;screw(label+'MountRivet_'+str(j),mount+Vector((.023*math.cos(a),.005,.023*math.sin(a))),direction,parent,.0025)
    pts=[]
    for j in range(25):
        a=j*math.pi/2/24;pts.append((sign*.065,.125+.049*math.sin(a),.344-.049*(1-math.cos(a))))
    pts.extend([(sign*.065,.174,.283)])
    tube(label+'Gooseneck_HollowCast',pts,outer,inner,parent)
    if label=='Air':
        ring('AirValve_UpperHex',.024,.010,.014,(sign*.065,.174,.280),material=bronze,parent=parent,n=6)
        ring('AirNonReturnValve_OldStyleExternalBody',.019,.008,.028,(sign*.065,.174,.260),material=bronze,parent=parent)
        ring('AirSubmarineThreadCore_1and1sixteenth',.0130,.008,.020,(sign*.065,.174,.236),material=edge,parent=parent)
        thread('AirOutletThread_17TPI',(sign*.065,.174,.226),1.0625*.0254/2,.0254/17,.019,parent)
    else:
        ring('TelephoneFemaleCoupling_HexBody',.028,.013,.021,(sign*.065,.174,.275),material=bronze,parent=parent,n=6)
        ring('TelephoneElbowThreadCore_1point715',.0208,.010,.019,(sign*.065,.174,.296),material=edge,parent=parent)
        thread('TelephoneElbowThread_11TPI',(sign*.065,.174,.287),1.715*.0254/2,.0254/11,.018,parent)
        cylinder('TelephonePlug_Recessed_Unresolved',.012,.003,(sign*.065,.174,.265),material=dark,parent=parent)

# Banana exhaust under wearer-right window. Bulb perforations are genuine geometry.
exhaust=groups['ExhaustAssembly'];valvecentre=Vector((-.096,-.096,.266));valvedir=Vector((-.74,-.66,.03))
ring('ExhaustValve_Seat',.025,.012,.018,valvecentre,valvedir,bronze,exhaust)
ring('ExhaustValve_Cover',.024,.008,.022,valvecentre+valvedir*.018,valvedir,bronze,exhaust)
ring('ExhaustValve_AdjusterBoss',.010,.0035,.011,valvecentre+valvedir*.039,valvedir,edge,exhaust)
starcentre=valvecentre+valvedir*.050
cylinder('ExhaustStarWheel_Hub',.010,.004,starcentre,valvedir,bronze,exhaust,48)
q=valvedir.to_track_quat('Z','Y')
for j in range(4):
    a=math.pi/4+j*math.pi/2;end=starcentre+q@Vector((.020*math.cos(a),.020*math.sin(a),0))
    rod('ExhaustStarWheel_Arm_'+str(j),starcentre,end,.006,bronze,exhaust)
path=[(-.100,-.100,.266),(-.128,-.066,.252),(-.145,-.018,.246),(-.141,.037,.246),(-.120,.095,.251)]
# Dense smooth interpolation of observed banana outline, section not surveyed.
centres=[]
for j in range(len(path)-1):
    for k in range(8):centres.append(tuple(np.array(path[j])*(1-k/8)+np.array(path[j+1])*k/8))
centres.append(path[-1]);tube('BananaExhaust_Hollow_SectionApproximate',centres,.016,.011,exhaust)
bulb=sphere('ExhaustPerforatedBulb',(-.120,.098,.251),(.023,.024,.023),bronze,exhaust)
inner=sphere('TEMP_InnerBulb',(-.120,.098,.251),(.019,.020,.019),None,root);cut(bulb,inner,'Hollow bulb cavity')
cutters=[]
outdir=Vector((-.6,.8,0)).normalized();q=outdir.to_track_quat('Z','Y')
for rr,count in [(.0,1),(.009,7),(.016,12),(.020,15)]:
    for j in range(count):
        a=2*math.pi*j/count;local=Vector((rr*math.cos(a),rr*math.sin(a),math.sqrt(max(0,.023**2-rr**2))))
        pos=Vector((-.120,.098,.251))+q@local;n=(q@local).normalized()
        cutters.append(cylinder('TEMP_Vent',.0014,.012,pos,n,None,root,16))
bpy.ops.object.select_all(action='DESELECT')
for ob in cutters:ob.select_set(True)
bpy.context.view_layer.objects.active=cutters[0];bpy.ops.object.join();cut(bulb,bpy.context.object,'Actual rear vent perforations')
rod('InternalChinValveStem_Interpretive',(-.093,-.093,.266),(-.058,-.060,.266),.0026,edge,groups['InteriorInterpretiveAssembly'])
sphere('ChinButton_Interpretive',(-.054,-.056,.266),(.012,.004,.009),bronze,groups['InteriorInterpretiveAssembly'])

# Spitcock on opposite side of the front, distinct from air and telephone.
spit=groups['SpitcockAssembly'];spitcentre=Vector((.098,-.097,.267));spitdir=Vector((.74,-.67,.02))
ring('SpitcockSeat',.019,.006,.012,spitcentre,spitdir,bronze,spit)
cylinder('SpitcockBody',.013,.032,spitcentre+spitdir*.019,spitdir,bronze,spit,48)
torus('SpitcockCollar',.012,.002,spitcentre+spitdir*.030,spitdir,edge,spit)
lever=spitcentre+spitdir*.040
rod('SpitcockLever',lever,lever+Vector((.016,.004,.004)),.0038,edge,spit)
sphere('SpitcockLeverBall',lever+Vector((.019,.004,.004)),(.006,.006,.006),bronze,spit)

# Rear dumbbell lock, latch and separate small chain; decorative path is estimated.
rod('RearLock_Pivot',(-.013,.118,.225),(.013,.118,.225),.0048,bronze,neck)
sphere('RearLock_BallA',(-.015,.118,.225),(.007,.007,.007),bronze,neck)
sphere('RearLock_BallB',(.015,.118,.225),(.007,.007,.007),bronze,neck)
rod('RearLock_Lever',(0,.118,.225),(0,.130,.184),.0042,bronze,neck)
ring('RearLock_StopRecess',.010,.005,.006,(0,.128,.180),(0,1,.4),bronze,neck,48)
rod('RearLock_Latch',(-.010,.130,.181),(.010,.130,.181),.0023,edge,neck)
rod('RearLock_CotterPin',(-.015,.132,.182),(.016,.132,.182),.0010,edge,neck,16)
for j in range(15):
    t=j/14;loc=(-.016-.028*t,.132+.008*math.sin(math.pi*t),.182-.037*math.sin(math.pi*t))
    ob=torus('SafetyChain_Link_%02d'%j,.0023,.00055,loc,(1,0,0) if j%2 else (0,1,0),edge,neck);ob.scale.y=1.45

# Evidence supports the existence of air channels and telephone hardware, not exact 1944 routing.
inside=groups['InteriorInterpretiveAssembly']
curve('AirDuct_Interpretive_BackToCrown',[(-.061,.112,.344),(-.045,.110,.415),(0,.042,.485),(0,-.044,.469)],.008,interior,inside,3)
for sign in [-1,1]:curve('AirDuct_Interpretive_SideBranch_'+str(sign),[(0,.042,.478),(sign*.068,.035,.454),(sign*.116,.0,.398)],.0055,interior,inside,3)
curve('AirDuct_Interpretive_TopBranch',[(0,-.042,.469),(0,-.074,.447)],.0055,interior,inside,3)
cylinder('TelephoneTransducer_Interpretive_NoInventedCircuit',.026,.006,cupcentre-cupdir*.018,cupdir,dark,inside,64)
curve('TelephoneLead_Interpretive_Route',[(.062,.109,.341),(.091,.075,.401),(.105,-.011,.408),tuple(cupcentre-cupdir*.024)],.0017,rubber,inside,2)

# Anchors only; runtime scripts are deliberately not bound at the asset stage.
for name,loc in [('Anchor_AirOutlet',(-.065,.174,.226)),('Anchor_TelephoneOutlet',(.065,.174,.264)),('Anchor_ExhaustOutlet',(-.120,.117,.251)),('Anchor_WindowCentre',(0,-.157,front_z)),('Anchor_Neck',(0,0,.198))]:
    ob=empty(name,root);ob.location=loc;ob['meaning']='Future integration landmark; no runtime logic'
# Set a meaningful vertical front-window pivot while preserving closed mesh world transforms.
bpy.context.view_layer.update()
children=list(door.children);worlds=[ob.matrix_world.copy() for ob in children];door.location=(.087,-.146,front_z)
bpy.context.view_layer.update()
for ob,world in zip(children,worlds):ob.matrix_world=world
door['opening_axis_blender']='Z';door['review_open_angle_deg']=100;door['angle_status']='Review illustration assumption, not historical stop angle'

# Mesh UVs. Cylindrical shell mapping retains a continuous metal surface.
for ob in list(parts.objects):
    if ob.type=='CURVE':
        # Convert original swept details to editable meshes, keeping names and groups.
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH')
    if ob.type!='MESH':continue
    if ob==body:
        bpy.context.view_layer.objects.active=body
        for m in list(body.modifiers):bpy.ops.object.modifier_apply(modifier=m.name)
    bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    if not ob.data.uv_layers:ob.data.uv_layers.new(name='UVMap')
    print('UNWRAP',ob.name,flush=True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.020);bpy.ops.object.mode_set(mode='OBJECT')
uv=body.data.uv_layers.active
for poly in body.data.polygons:
    values=[]
    for index in poly.loop_indices:
        co=body.data.vertices[body.data.loops[index].vertex_index].co;values.append((index,math.atan2(co.x,-co.y)/(2*math.pi)+.5,(co.z-.195)/.315))
    cross=max(t[1] for t in values)-min(t[1] for t in values)>.5
    for index,u,v in values:uv.data[index].uv=(u+1 if cross and u<.5 else u,v)
# Continuous shared mapping across the curved corselet: avoids smart-UV colour jumps.
for ob in [plate,bpy.data.objects['DressCollar_DarkGasket_Approximate'],*[bpy.data.objects['Brail_%02d_CurvedClampingStrap'%(j+1)] for j in range(4)]]:
    uv=ob.data.uv_layers.active
    for poly in ob.data.polygons:
        values=[]
        for index in poly.loop_indices:
            co=ob.data.vertices[ob.data.loops[index].vertex_index].co
            values.append((index,math.atan2(co.x,-co.y)/(2*math.pi)+.5,co.z/.20))
        cross=max(t[1] for t in values)-min(t[1] for t in values)>.5
        for index,u,v in values:uv.data[index].uv=(u+1 if cross and u<.5 else u,v)

# Review studio, never exported with the helmet.
bpy.ops.mesh.primitive_cylinder_add(vertices=96,radius=.31,depth=.025,location=(0,0,-.007))
ob=bpy.context.object;move(ob,studio);ob.name='ReviewPlinth';ob.data.materials.append(slate);bevel(ob,.006,4)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.020));ob=bpy.context.object;move(ob,studio);ob.data.materials.append(slate)
world=bpy.data.worlds.new('StudioWorld');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.065,.082,1);world.node_tree.nodes['Background'].inputs[1].default_value=.35
def light(name,loc,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    ob=bpy.data.objects.new(name,d);studio.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector((0,0,.27))-ob.location).to_track_quat('-Z','Y').to_euler()
light('LargeKey',(-.7,-.7,1.1),100,.62,(1,.88,.73));light('CoolFill',(.65,-.2,.8),75,.52,(.75,.88,1))
light('RearRim',(.1,.75,.85),150,.5,(1,.91,.78));light('TopSoftbox',(-.1,.1,1.15),40,.25,(1,1,1))
d=bpy.data.cameras.new('ReviewCamera');camera=bpy.data.objects.new('ReviewCamera',d);studio.objects.link(camera);scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1440;scene.render.resolution_y=1440;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
def aim(loc,target,scale):
    camera.location=loc;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=scale
aim((.7,-1,.63),(0,0,.259),.66)

def export_copy(stem,subset):
    bpy.context.view_layer.update();mapping={};deps=bpy.context.evaluated_depsgraph_get()
    original_names={ob:ob.name for ob in subset if ob.type in {'MESH','EMPTY'}}
    for ob,name in original_names.items():ob.name='EXPORT_SOURCE_TMP_'+name
    for ob in subset:
        if ob.type not in {'MESH','EMPTY'}:continue
        if ob.type=='EMPTY':copy=bpy.data.objects.new(original_names[ob],None)
        else:copy=bpy.data.objects.new(original_names[ob],bpy.data.meshes.new_from_object(ob.evaluated_get(deps),depsgraph=deps))
        scene.collection.objects.link(copy);copy.matrix_world=ob.matrix_world.copy();mapping[ob]=copy
        for k in ob.keys():
            try:copy[k]=ob[k]
            except TypeError:pass
    for ob,copy in mapping.items():world=copy.matrix_world.copy();copy.parent=mapping.get(ob.parent);copy.matrix_world=world
    bpy.ops.object.select_all(action='DESELECT')
    for ob in mapping.values():ob.select_set(True)
    bpy.context.view_layer.objects.active=next(ob for ob in mapping.values() if ob.type=='MESH')
    bpy.ops.export_scene.fbx(filepath=str(OUT/'exports'/(stem+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='COPY',embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/'exports'/(stem+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_extras=True,export_animations=False)
    for ob in mapping.values():bpy.data.objects.remove(ob,do_unlink=True)
    for ob,name in original_names.items():ob.name=name

bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'sources/mark-v-mod1.blend'))
export_copy('mark-v-mod1',list(parts.objects))
export_copy('mark-v-front-window',[door,*door.children])
# Independent part sources: no studio and no duplicated whole-helmet master.
for im in bpy.data.images:
    if im.packed_file:im.unpack(method='REMOVE')
    im.filepath='//../../textures/'+Path(im.filepath).name
for name,group in groups.items():
    bpy.data.libraries.write(str(OUT/'sources/parts'/(name+'.blend')),{group,*group.children},fake_user=True,compress=True,path_remap='NONE')
for im in bpy.data.images:
    im.filepath=str(OUT/'textures'/Path(im.filepath).name);im.pack()
deps=bpy.context.evaluated_depsgraph_get();stats=[]
for ob in parts.objects:
    if ob.type!='MESH':continue
    ev=ob.evaluated_get(deps);me=ev.to_mesh();me.calc_loop_triangles()
    stats.append({'name':ob.name,'parent':ob.parent.name if ob.parent else None,'vertices':len(me.vertices),'triangles':len(me.loop_triangles)});ev.to_mesh_clear()
report={'blender':bpy.app.version_string,'identity':'DESCO Mark V Mod.1 standard air exterior study; 1944 museum specimen reference','meshes':len(stats),'triangles':sum(s['triangles'] for s in stats),'materials':len({m.name for ob in parts.objects if ob.type=='MESH' for m in ob.data.materials}),'parts':stats,'texture_origin':'original deterministic synthetic 2048px maps; not photograph/scan','unity_integrated':False,'device_tested':False,'historical_measurement_replica':False,'generator_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
(OUT/'qa/geometry-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
views=[('01-overview',(.70,-1,.63),(0,0,.259),.66),('02-front',(0,-1,.37),(0,-.008,.259),.64),('03-wearer-left',(1,0,.37),(0,0,.260),.65),('04-wearer-right',(-1,0,.36),(0,0,.260),.65),('05-rear',(-.5,.9,.47),(0,.025,.275),.66),('06-window-detail',(.18,-.65,.40),(0,-.135,.348),.29),('07-rear-connections',(-.28,.75,.40),(0,.165,.311),.28),('08-brail-detail',(.35,-.5,.26),(0,-.100,.145),.35)]
for name,loc,target,scale in views:
    aim(loc,target,scale);scene.render.filepath=str(OUT/'previews'/(name+'.png'));bpy.ops.render.render(write_still=True);print('RENDER_READY',name,flush=True)
door.rotation_euler.z=math.radians(100);bpy.context.view_layer.update();aim((.55,-.95,.50),(0,-.10,.343),.38)
scene.render.filepath=str(OUT/'previews/09-window-open.png');bpy.ops.render.render(write_still=True);print('RENDER_READY','09-window-open',flush=True)
# Interior reveal is a review illustration, not a physical disassembly or scanned interior.
door.rotation_euler.z=0
body.hide_render=True
aim((.62,-.95,.68),(0,0,.329),.54);scene.render.filepath=str(OUT/'previews/10-interior-interpretive.png');bpy.ops.render.render(write_still=True)
print('MARK_V_READY',report['meshes'],report['triangles'],flush=True)
