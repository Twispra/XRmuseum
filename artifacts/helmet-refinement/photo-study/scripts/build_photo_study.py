"""Original photo-referenced geometry. Run in Blender 4.5 LTS, with -- --output DIR.

Dimensions are display assumptions, not measurements of the museum object.
No source photograph is used as a texture. No current Unity asset is overwritten.
"""
import bpy, bmesh, math, json, sys, hashlib
from pathlib import Path
from mathutils import Vector
import numpy as np

args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
OUT=(Path(args[args.index('--output')+1]) if '--output' in args else Path(__file__).resolve().parents[1]).resolve()
for sub in ['sources','exports','textures','previews','qa']:(OUT/sub).mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.name='Photo-referenced helmet study - identity unconfirmed'
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene['historical_status']='Photo-referenced external study; object identity and actual dimensions unconfirmed. Not identified as Mark V.'
scene['scale_status']='Nominal metre-scale display geometry only. All numeric dimensions are modelling assumptions.'
parts=bpy.data.collections.new('01_HELMET_EDITABLE_PARTS');scene.collection.children.link(parts)
studio=bpy.data.collections.new('02_STUDIO_NOT_FOR_EXPORT');scene.collection.children.link(studio)

def place(ob,coll=parts):
    for old in list(ob.users_collection):old.objects.unlink(ob)
    coll.objects.link(ob);return ob
def empty(name,parent=None):
    ob=bpy.data.objects.new(name,None);parts.objects.link(ob);ob.parent=parent;ob.empty_display_size=.02;return ob
root=empty('PhotoHelmetStudy_ROOT')
root['identity']='User photo01-04 object; exact model pending verification'
root['dimension_confidence']='Estimated viewing scale; not a surveyed or engineering replica'
front=empty('FrontWindowAssembly',root)
side=empty('SideWindowsAssembly',root)
chest=empty('CorseletAssembly',root)
rear=empty('RearFittings_UnverifiedFunctions',root)

def material(name,color,metal=0,rough=.4):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    mat.diffuse_color=(*color,1);return mat

def noise(size,seed):
    rng=np.random.default_rng(seed);field=np.zeros((size,size),dtype=np.float32)
    yy,xx=np.mgrid[:size,:size]
    for frequency,weight in [(5,1),(13,.48),(37,.20),(99,.09)]:
        grid=rng.random((frequency,frequency)).astype(np.float32)
        u=xx/size*frequency;v=yy/size*frequency
        i=np.floor(u).astype(int);j=np.floor(v).astype(int)
        a=u-i;b=v-j;a=a*a*(3-2*a);b=b*b*(3-2*b)
        field+=weight*((1-a)*(1-b)*grid[j%frequency,i%frequency]+a*(1-b)*grid[j%frequency,(i+1)%frequency]+(1-a)*b*grid[(j+1)%frequency,i%frequency]+a*b*grid[(j+1)%frequency,(i+1)%frequency])
    field=(field-field.min())/(field.max()-field.min())
    return field,rng

def weathered(name,base,bright,oxide,seed):
    mat=material(name,base,.82,.46);nt=mat.node_tree;p=nt.nodes.get('Principled BSDF')
    n,rng=noise(1024,seed);fine=rng.random(n.shape).astype(np.float32)
    t=np.clip((n-.24)*1.5,0,1)
    rgb=np.array(base)[None,None,:]*(1-t[:,:,None])+np.array(bright)[None,None,:]*t[:,:,None]
    spots=np.clip((n-.70)*4,0,.30)
    rgb=rgb*(1-spots[:,:,None])+np.array(oxide)[None,None,:]*spots[:,:,None]
    rgb*=.90+.18*fine[:,:,None]
    micro=.70*n+.07*fine
    gy,gx=np.gradient(micro);normal=np.stack((-gx*5,-gy*5,np.ones_like(n)),axis=2)
    normal/=np.linalg.norm(normal,axis=2)[:,:,None];normal=normal*.5+.5
    for map_name,values in [('BaseColor',rgb),('Roughness',np.repeat((.47+.25*n+.05*fine)[:,:,None],3,axis=2)),('Normal',normal)]:
        rgba=np.ones((1024,1024,4),dtype=np.float32);rgba[:,:,:3]=np.clip(values,0,1)
        im=bpy.data.images.new(name+'_'+map_name,width=1024,height=1024,alpha=False)
        im.colorspace_settings.name='sRGB' if map_name=='BaseColor' else 'Non-Color'
        im.pixels.foreach_set(rgba.ravel());im.filepath_raw=str(OUT/'textures'/(name+'_'+map_name+'.png'));im.file_format='PNG';im.save();im.pack()
        tex=nt.nodes.new('ShaderNodeTexImage');tex.image=im;tex.label='Original synthetic wear; not museum photographic texture'
        if map_name=='Normal':
            norm=nt.nodes.new('ShaderNodeNormalMap');norm.inputs['Strength'].default_value=.55
            nt.links.new(tex.outputs['Color'],norm.inputs['Color']);nt.links.new(norm.outputs['Normal'],p.inputs['Normal'])
        else:nt.links.new(tex.outputs['Color'],p.inputs['Base Color' if map_name=='BaseColor' else 'Roughness'])
    return mat

shellmat=weathered('WeatheredShell_Interpretive',(.17,.15,.11),(.45,.39,.24),(.08,.19,.13),5424)
brass=weathered('CastBrass_Interpretive',(.30,.19,.075),(.68,.47,.21),(.12,.22,.15),5425)
bright=material('PolishedEdge_Interpretive',(.53,.36,.16),.86,.37)
dark=material('Gasket_Interpretive',(.024,.027,.023),0,.85)
glass=material('Glass_VisualAssumption',(.56,.68,.70),0,.14)
gp=glass.node_tree.nodes.get('Principled BSDF');gp.inputs['Transmission Weight'].default_value=.94;gp.inputs['IOR'].default_value=1.48
acrylic=material('ClearFrontMotif_MaterialUnconfirmed',(.76,.81,.79),0,.10)
ap=acrylic.node_tree.nodes.get('Principled BSDF');ap.inputs['Transmission Weight'].default_value=.92;ap.inputs['IOR'].default_value=1.49
floor_mat=material('StudioSlate',(.024,.039,.052),.15,.42)

def finish(ob,name,mat,parent=root):
    place(ob);ob.name=name;ob.parent=parent
    if mat:ob.data.materials.append(mat)
    if ob.type=='MESH':
        for poly in ob.data.polygons:poly.use_smooth=True
    ob['source']='Photo observation; metric parameters estimated'
    return ob
def bevel(ob,width=.001,segments=3):
    m=ob.modifiers.new('Small edge radius - adjustable','BEVEL');m.width=width;m.segments=segments
    m.limit_method='ANGLE'
    return ob
def cylinder(name,radius,depth,loc,direction=(0,0,1),mat=brass,parent=root,verts=64):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=radius,depth=depth,location=loc)
    ob=bpy.context.object;ob.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler()
    return finish(ob,name,mat,parent)
def ring(name,outer,inner,depth,loc,direction=(0,0,1),mat=brass,parent=root,n=96):
    vertices=[];faces=[]
    for z,r in [(-depth/2,outer),(depth/2,outer),(-depth/2,inner),(depth/2,inner)]:
        vertices +=[(r*math.cos(i*2*math.pi/n),r*math.sin(i*2*math.pi/n),z) for i in range(n)]
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
    ob=mesh(name,vertices,faces,mat,parent);ob.location=loc;ob.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler()
    return bevel(ob,.0007,2)
def torus(name,major,minor,loc,direction=(0,0,1),mat=brass,parent=root):
    bpy.ops.mesh.primitive_torus_add(major_segments=80,minor_segments=12,major_radius=major,minor_radius=minor,location=loc)
    ob=bpy.context.object;ob.rotation_euler=Vector(direction).to_track_quat('Z','Y').to_euler()
    return finish(ob,name,mat,parent)
def mesh(name,vertices,faces,mat,parent=root):
    data=bpy.data.meshes.new(name+'_Mesh');data.from_pydata(vertices,[],faces);data.update()
    bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(data);bm.free()
    ob=bpy.data.objects.new(name,data);parts.objects.link(ob);ob.parent=parent
    if mat:data.materials.append(mat)
    for poly in data.polygons:poly.use_smooth=True
    return ob
def rod(name,a,b,radius,mat=brass,parent=root):
    a=Vector(a);b=Vector(b);return cylinder(name,radius,(b-a).length,(a+b)/2,b-a,mat,parent,32)
def curve(name,points,radius,mat=brass,parent=root):
    data=bpy.data.curves.new(name+'_Curve','CURVE');data.dimensions='3D';data.resolution_u=10
    data.bevel_depth=radius;data.bevel_resolution=3
    spline=data.splines.new('BEZIER');spline.bezier_points.add(len(points)-1)
    for b,p in zip(spline.bezier_points,points):b.co=p;b.handle_left_type='AUTO';b.handle_right_type='AUTO'
    ob=bpy.data.objects.new(name,data);parts.objects.link(ob);ob.parent=parent;data.materials.append(mat);return ob
def bolt(name,loc,direction=(0,0,1),radius=.009,parent=root):
    direction=Vector(direction).normalized();loc=Vector(loc)
    ring(name+'_Washer',radius*1.4,radius*.50,.0018,loc,direction,bright,parent,48)
    ob=cylinder(name+'_HexNut',radius,.009,loc+direction*.0045,direction,brass,parent,6);bevel(ob,.0007,2)
    cylinder(name+'_Stem',radius*.43,.015,loc+direction*.006,direction,bright,parent,24)

# Hollow dome: smoothly lofted exterior and a simplified interior of assumed thickness.
profile=[(.096,.111),(.110,.124),(.135,.135),(.170,.143),(.215,.147),(.260,.148),(.305,.145),(.342,.134),(.375,.110),(.400,.079),(.416,.043),(.423,.008)]
n=128;verts=[];faces=[]
for layer in (0,1):
    for z,r in profile:
        radius=r-(.0032 if layer else 0)
        for i in range(n):
            a=2*math.pi*i/n;verts.append((radius*.97*math.cos(a),radius*math.sin(a),z-(.002 if layer else 0)))
count=len(profile)*n
for layer in (0,1):
    for j in range(len(profile)-1):
        for i in range(n):
            a=layer*count+j*n+i;b=layer*count+j*n+(i+1)%n
            faces.append((a,b,b+n,a+n) if layer==0 else (a+n,b+n,b,a))
for j in (0,):
    for i in range(n):
        a=j*n+i;b=j*n+(i+1)%n;faces.append((a,a+count,b+count,b))
faces.append(tuple((len(profile)-1)*n+i for i in range(n)))
faces.append(tuple(count+(len(profile)-1)*n+i for i in reversed(range(n))))
body=mesh('Shell_Hollow_WallThicknessAssumed',verts,faces,shellmat)
body['wall_thickness_m']=.0032;body['wall_thickness_status']='modelling assumption, not measured'
for name,loc,direction,radius,depth,scale in [('Front', (0,-.134,.235),(0,-1,0),.081,.19,(1,1,1)),('Left',(-.132,.012,.221),(-1,0,0),.047,.15,(1.20,1,1)),('Right',(.132,.012,.221),(1,0,0),.047,.15,(1.20,1,1))]:
    cut=cylinder('TEMP_aperture_'+name,radius,depth,loc,direction,None);cut.scale=scale
    bpy.context.view_layer.objects.active=body;m=body.modifiers.new('Physical '+name+' aperture','BOOLEAN');m.operation='DIFFERENCE';m.solver='EXACT';m.object=cut
    bpy.ops.object.modifier_apply(modifier=m.name);bpy.data.objects.remove(cut,do_unlink=True)
bevel(body,.0005,2)
ring('NeckRing_Fixed',.126,.112,.015,(0,0,.101),mat=brass)
torus('NeckRing_BrightRim',.124,.002,(0,0,.110),mat=bright)
ring('NeckSeal_Illustrative',.124,.114,.005,(0,0,.090),mat=dark)

# Corselet shaped around the neck; underside and fastener spacing are provisional.
N=128;V=[];F=[]
for layer in (0,1):
    for inner in (False,True):
        for i in range(N):
            a=2*math.pi*i/N
            if inner:x=.115*math.cos(a);y=.115*math.sin(a);z=.089
            else:x=.205*math.cos(a);y=-.027+.204*math.sin(a);z=.045+.020*(.5+.5*math.sin(a))+.013*math.cos(a)**2
            V.append((x,y,z-layer*.005))
for i in range(N):
    j=(i+1)%N;F.extend([(i,j,N+j,N+i),(2*N+i,3*N+i,3*N+j,2*N+j),(i,2*N+i,2*N+j,j),(N+i,N+j,3*N+j,3*N+i)])
plate=mesh('Corselet_OuterAndInnerSurfaces_Assumed',V,F,brass,chest);bevel(plate,.0018,3)
edge=[]
for i in range(65):
    a=2*math.pi*i/64;edge.append((.205*math.cos(a),-.027+.204*math.sin(a),.045+.020*(.5+.5*math.sin(a))+.013*math.cos(a)**2))
curve('Corselet_RolledEdge',edge,.0032,bright,chest)
for i in range(12):
    a=2*math.pi*(i+.5)/12
    x=.188*math.cos(a);y=-.027+.184*math.sin(a);z=.045+.020*(.5+.5*math.sin(a))+.013*math.cos(a)**2+.003
    bolt('PerimeterBolt_%02d_LayoutAssumed'%(i+1),(x,y,z),parent=chest)
for sign in (-1,1):
    loc=(sign*.066,-.121,.067)
    cylinder('FrontPostBase_'+str(sign),.025,.003,loc,mat=brass,parent=chest)
    rod('FrontPost_'+str(sign),(loc[0],loc[1],loc[2]),(loc[0],loc[1]-.004,loc[2]+.029),.008,bright,chest)
    cylinder('FrontPostCap_'+str(sign),.011,.004,(loc[0],loc[1]-.004,loc[2]+.029),mat=brass,parent=chest)
# Blank oval plate: illegible nameplate wording is deliberately not reconstructed.
bpy.ops.mesh.primitive_uv_sphere_add(segments=48,ring_count=16,radius=1,location=(0,-.170,.058))
label=bpy.context.object;label.scale=(.039,.021,.0016);finish(label,'Nameplate_Blank_TextUnverified',dark,chest)

# Fixed front seat, removable plate and clear circular motif. No unverified hinge animation.
Z=.235
ring('FrontSeat_Fixed',.102,.0805,.021,(0,-.146,Z),(0,-1,0),brass,front)
ring('FrontSeal_Illustrative',.090,.078,.003,(0,-.159,Z),(0,-1,0),dark,front)
ring('FrontFrame_Removable',.095,.078,.012,(0,-.168,Z),(0,-1,0),bright,front)
cylinder('FrontGlass_PlaneThicknessAssumed',.079,.0025,(0,-.165,Z),(0,-1,0),glass,front,96)
ring('ClearMotif_Centre',.039,.032,.008,(0,-.181,Z),(0,-1,0),acrylic,front)
for i,a in enumerate([140,20,260]):
    angle=math.radians(a);x=.060*math.cos(angle);z=Z+.060*math.sin(angle)
    ring('ClearMotif_Satellite_'+str(i+1),.023,.017,.008,(x,-.181,z),(0,-1,0),acrylic,front)
    for da in (-19,19):
        phi=math.radians(a+da)
        rod('ClearMotif_Link_'+str(i+1)+'_'+str(da),(.039*math.cos(phi),-.181,Z+.039*math.sin(phi)),(.078*math.cos(phi),-.181,Z+.078*math.sin(phi)),.0025,acrylic,front)
for i,a in enumerate([90,210,330]):
    ang=math.radians(a);x=.084*math.cos(ang);z=Z+.084*math.sin(ang)
    cylinder('FrontRetainer_'+str(i+1),.0054,.010,(x,-.180,z),(0,-1,0),brass,front,32)
    rod('RetainerSlot_'+str(i+1),(x-.003,-.186,z),(x+.003,-.186,z),.0004,dark,front)
for i,offset in enumerate([-.105,.105]):
    rod('FrameThumbStem_'+str(i),(0,-.147,Z+offset),(0,-.180,Z+offset),.0055,brass,front)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=.010,location=(0,-.181,Z+offset));finish(bpy.context.object,'FrameThumbBall_'+str(i),bright,front)

# Elliptical side windows and curved protective bars.
for sign in (-1,1):
    direction=(sign,0,0);loc=(sign*.139,.012,.221)
    r=ring('SideWindowSeat_'+str(sign),.056,.045,.012,loc,direction,brass,side);r.scale.x=1.20
    pane=cylinder('SideGlass_'+str(sign),.045,.002,(sign*.145,.012,.221),direction,glass,side,80);pane.scale.x=1.20
    rim=torus('SideWindowLip_'+str(sign),.050,.0025,(sign*.149,.012,.221),direction,bright,side);rim.scale.x=1.20
    for yy in (-.015,.015):
        pts=[]
        for k in range(9):
            z=-.052+k*.104/8;bow=.007*(1-(z/.055)**2)
            pts.append((sign*(.154+bow),.012+yy,.221+z))
        curve('SideGuard_V_'+str(sign)+'_'+str(yy),pts,.0025,brass,side)
    for zz in (-.020,.020):
        pts=[]
        for k in range(9):
            y=-.043+k*.086/8;bow=.006*(1-(y/.05)**2)
            pts.append((sign*(.155+bow),.012+y,.221+zz))
        curve('SideGuard_H_'+str(sign)+'_'+str(zz),pts,.0025,brass,side)
    for j in range(4):
        a=2*math.pi*(j+.5)/4
        cylinder('SideSeatRivet_'+str(sign)+'_'+str(j),.0025,.002,(sign*.147,.012+.052*math.sin(a),.221+.063*math.cos(a)),direction,bright,side,24)

def bent_tube(name,centres,outer,inner,parent):
    n=48;verts=[];frames=[]
    for j,c in enumerate(centres):
        tangent=Vector(centres[min(j+1,len(centres)-1)])-Vector(centres[max(j-1,0)])
        tangent.normalize();u=Vector((1,0,0));v=tangent.cross(u).normalized();frames.append((u,v))
    for radius in (outer,inner):
        for centre,(u,v) in zip(centres,frames):
            for i in range(n):
                a=2*math.pi*i/n;verts.append(Vector(centre)+radius*(math.cos(a)*u+math.sin(a)*v))
    count=n*len(centres);faces=[]
    for layer in (0,1):
        for j in range(len(centres)-1):
            for i in range(n):
                a=layer*count+j*n+i;b=layer*count+j*n+(i+1)%n;faces.append((a,b,b+n,a+n))
    for j in (0,len(centres)-1):
        for i in range(n):a=j*n+i;b=j*n+(i+1)%n;faces.append((a,a+count,b+count,b))
    return mesh(name,verts,faces,brass,parent)

ring('RearUpperMount_FunctionUnverified',.030,.014,.010,(.035,.137,.245),(0,1,.15),brass,rear)
path=[]
for i in range(17):
    a=math.pi/2*i/16;path.append((.035,.140+.052*math.sin(a),.245-.052*(1-math.cos(a))))
path +=[(.035,.192,.178),(.035,.192,.160)]
bent_tube('RearUpperBentFitting_Hollow_FunctionUnverified',path,.020,.012,rear)
ring('RearUpperCollar',.029,.019,.012,(.035,.192,.176),mat=brass,parent=rear)
ring('RearUpperThreadCore_ThreadPitchAssumed',.018,.012,.023,(.035,.192,.152),mat=bright,parent=rear)
thread=[]
for i in range(241):
    a=i*2*math.pi/40;thread.append((.035+.018*math.cos(a),.192+.018*math.sin(a),.142+i*.019/240))
thread_object=curve('RearUpperThread_VisualApproximation',thread,.00065,brass,rear)
thread_object.data.resolution_u=1
thread_object.data.bevel_resolution=2
cap=cylinder('RearLowerCap_FunctionUnverified',.018,.022,(-.063,.133,.134),(0,1,.2),brass,rear);bevel(cap,.001,3)
ring('RearLowerMount',.025,.015,.006,(-.063,.124,.131),(0,1,.2),bright,rear)
curve('RearSmallLever_ObservedShapeApproximation',[(-.085,.127,.115),(-.092,.146,.108),(-.103,.147,.106)],.0032,brass,rear)

# Work-oriented attachment points carry neutral labels until identity is established.
for name,loc in [('Anchor_FrontAssembly',(0,-.147,Z)),('Anchor_RearUpper_Unverified',(.035,.192,.143)),('Anchor_RearLower_Unverified',(-.063,.145,.134))]:
    ob=empty(name,root);ob.location=loc;ob['meaning']='Geometric anchor only; does not assert mechanical function.'

# Unwrap every mesh, including boolean apertures with inherited cutter UV layers.
# These are automatic study UVs, not a final hand-authored texture layout.
for ob in list(parts.objects):
    if ob.type!='MESH':continue
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')

# A continuous cylindrical shell map avoids visible smart-project island borders.
# The seam is at the rear; the seamless synthetic noise tiles horizontally.
uv=body.data.uv_layers.active
for polygon in body.data.polygons:
    values=[]
    for index in polygon.loop_indices:
        co=body.data.vertices[body.data.loops[index].vertex_index].co
        values.append((index,math.atan2(co.x,-co.y)/(2*math.pi)+.5,co.z/.43))
    crosses=max(v[1] for v in values)-min(v[1] for v in values)>.5
    for index,u,v in values:uv.data[index].uv=(u+1 if crosses and u<.5 else u,v)

# Studio for evidence renders; excluded from exports.
bpy.ops.mesh.primitive_cylinder_add(vertices=96,radius=.32,depth=.035,location=(0,0,.010))
plinth=bpy.context.object;place(plinth,studio);plinth.name='StudioPlinth';plinth.data.materials.append(floor_mat);bevel(plinth,.008,4)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.009));floor=bpy.context.object;place(floor,studio);floor.name='StudioFloor';floor.data.materials.append(floor_mat)
world=scene.world or bpy.data.worlds.new('StudioWorld');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.035,.055,.08,1);world.node_tree.nodes['Background'].inputs[1].default_value=.30
def light(name,loc,power,size,color):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
    ob=bpy.data.objects.new(name,data);studio.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector((0,0,.23))-ob.location).to_track_quat('-Z','Y').to_euler()
light('KeyLargeSoftbox',(-.65,-.65,.95),100,.60,(1,.83,.65))
light('CoolFill',(.60,-.35,.58),65,.55,(.65,.83,1))
light('RearRim',(.20,.65,.78),130,.45,(1,.91,.78))
light('TopStrip',(-.12,.05,1.0),35,.25,(1,1,1))
camera_data=bpy.data.cameras.new('EvidenceCamera');camera=bpy.data.objects.new('EvidenceCamera',camera_data);studio.objects.link(camera);scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=True
scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
def aim(loc,target,ortho):
    camera.location=loc;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=ortho
aim((.62,-.95,.53),(0,-.005,.225),.59)

# Editable file is saved before producing temporary evaluated export copies.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'sources'/'photo-helmet-study.blend'))

def export_copy(stem,subset):
    copies=[];mapping={};deps=bpy.context.evaluated_depsgraph_get()
    for ob in subset:
        if ob.type not in {'MESH','CURVE','EMPTY'}:continue
        if ob.type=='EMPTY':copy=bpy.data.objects.new(ob.name,None)
        else:copy=bpy.data.objects.new(ob.name,bpy.data.meshes.new_from_object(ob.evaluated_get(deps),depsgraph=deps))
        scene.collection.objects.link(copy);copy.matrix_world=ob.matrix_world.copy();mapping[ob]=copy;copies.append(copy)
    for ob,copy in mapping.items():
        world=copy.matrix_world.copy();copy.parent=mapping.get(ob.parent);copy.matrix_world=world
    bpy.ops.object.select_all(action='DESELECT')
    for ob in copies:ob.select_set(True)
    bpy.context.view_layer.objects.active=next(ob for ob in copies if ob.type=='MESH')
    bpy.ops.export_scene.fbx(filepath=str(OUT/'exports'/(stem+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='COPY',embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/'exports'/(stem+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_extras=True)
    for ob in copies:bpy.data.objects.remove(ob,do_unlink=True)
export_copy('photo-helmet-study',list(parts.objects))
front_subset=[ob for ob in parts.objects if ob==front or ob.parent==front]
export_copy('front-window-study',front_subset)

stats=[];deps=bpy.context.evaluated_depsgraph_get()
for ob in parts.objects:
    if ob.type in {'MESH','CURVE'}:
        evaluated=ob.evaluated_get(deps);me=evaluated.to_mesh();me.calc_loop_triangles()
        stats.append({'name':ob.name,'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'parent':ob.parent.name if ob.parent else None});evaluated.to_mesh_clear()
report={'blender':bpy.app.version_string,'identity':'unconfirmed photo01-04 object; not identified as Mark V','scale':'all numerical geometry is estimated','objects':stats,'triangles':sum(s['triangles'] for s in stats),'materials':len({m.name for ob in parts.objects if ob.type in {'MESH','CURVE'} for m in ob.data.materials}),'original_images_used_as_textures':False,'unity_integration':False,'performance_tested':False}
(OUT/'qa'/'geometry-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
views=[('01-overview',(.62,-.95,.53),(0,-.005,.225),.59),('02-front',(0,-1,.285),(0,-.018,.222),.56),('03-left',(-1,-.03,.33),(0,0,.224),.57),('04-rear',(.48,.88,.43),(0,.018,.226),.59),('05-front-detail',(.15,-.64,.295),(0,-.15,.237),.255)]
for name,loc,target,scale in views:
    aim(loc,target,scale);scene.render.filepath=str(OUT/'previews'/(name+'.png'));bpy.ops.render.render(write_still=True);print('RENDER_READY',name,flush=True)
aim((.62,-.95,.53),(0,-.005,.225),.59)
print('MODEL_READY',report['triangles'],'triangles',flush=True)
