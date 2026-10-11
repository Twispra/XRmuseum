"""Original SL17B exhibition reconstruction, Blender 4.5 LTS.

Run: blender -b -P build_helmet.py -- --output <NEW_DIRECTORY>
The script refuses an existing delivery. All dimensions are # GUESS, not CAD.
References are external, never projected onto the model or embedded in delivery.
"""
import argparse
import math
import json
import os
import sys
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector

# GUESS: metre-scale exhibition envelope. No source supplies measured dimensions.
GLOBAL_SCALE = 0.86
P = dict(port_center_z=.220, port_y=-.143, port_outer=(.132,.090),
         port_inner=(.115,.075), ring_depth=.009, wall=.004,
         neck_z=.060, neck_rx=.119, neck_ry=.122,
         regulator=(0,-.155,.088), regulator_radius=.048,
         sideblock=(-.139,-.065,.188), shell_segments=80,
         screw_angles=(10,35,65,115,145,170,190,215,245,325,350),
         handle_path=((- .143,.310,.041),(-.139,.316,.041),(-.120,.349,.036),
                      (-.073,.382,.022),(-.015,.387,.019),
                      (.047,.367,.017),(.097,.295,.022),(.102,.287,.023)),
         shell_sections=((- .137,.127,.085,.220),(-.115,.136,.117,.201),
                         (-.075,.142,.141,.195),(-.020,.140,.151,.190),
                         (.040,.131,.147,.184),(.092,.107,.121,.177),
                         (.133,.067,.084,.170),(.153,.008,.017,.166)))
COLORS = dict(yellow=(.95,.59,.007),chrome=(.72,.77,.80),
              bronze=(.59,.38,.12),brass=(.64,.43,.14),
              rubber=(.022,.025,.029),strap=(.012,.014,.016),
              blue=(.038,.090,.33),cap=(.035,.29,.72),glass=(.63,.70,.74))
VIEWS = dict(front=((0,-1.05,.24),(0,-.01,.20)),
             side=((-1.05,.01,.23),(0,0,.20)),
             top=((0,0,1.2),(0,0,.17)),
             threequarter=((- .70,-.95,.55),(0,-.012,.20)))
args=argparse.ArgumentParser()
args.add_argument('--output',default=str(Path(__file__).resolve().parent/'delivery'))
args.add_argument('--no-stages',action='store_true')
argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
opts=args.parse_args(argv)
OUT=Path(opts.output).resolve()
if (OUT/'superlite17b.blend').exists():
    raise RuntimeError('Refusing to overwrite a delivery: '+str(OUT))
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'renders').mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
scene.render.engine='CYCLES'
scene.cycles.samples=12
scene.cycles.use_denoising=True
scene.render.resolution_x=640
scene.render.resolution_y=640
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.world=bpy.data.worlds.new('Studio_World')
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.30,.34,.40,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
MODEL=[]
COL=None

def group(name):
    global COL
    COL=bpy.data.collections.new(name)
    scene.collection.children.link(COL)
    return COL

def finish(o,name,mat,bevel=0,smooth=True):
    o.name=name
    for c in list(o.users_collection): c.objects.unlink(o)
    COL.objects.link(o)
    if mat: o.data.materials.append(mat)
    if o.type=='MESH':
        for f in o.data.polygons: f.use_smooth=smooth
        if bevel:
            m=o.modifiers.new('Edge radii - estimated','BEVEL');m.width=bevel;m.segments=2
            o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    o['basis']='GUESS: photo proportions; not measured engineering geometry'
    MODEL.append(o)
    return o

def material(name,key,metal,rough):
    m=bpy.data.materials.new(name);m.use_nodes=True
    b=m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value=(*COLORS[key],1)
    b.inputs['Metallic'].default_value=metal
    b.inputs['Roughness'].default_value=rough
    m.diffuse_color=(*COLORS[key],1)
    return m

yellow=material('M_Shell_Gelcoat_Yellow','yellow',0,.20)
yellow.node_tree.nodes['Principled BSDF'].inputs['Coat Weight'].default_value=.45
chrome=material('M_Polished_Metal','chrome',1,.16)
bronze=material('M_Handle_Brass_Warm','bronze',1,.27)
brass=material('M_Valve_Brass','brass',1,.29)
rubber=material('M_Elastomer_Black','rubber',0,.67)
strapmat=material('M_Nylon_Strap','strap',0,.85)
blue=material('M_HeadCushion_Nylon_Blue','blue',0,.90)
cap=material('M_DustCap_Blue','cap',0,.38)
glass=material('M_Polycarbonate_Clear','glass',0,.06)
gb=glass.node_tree.nodes['Principled BSDF']
gb.inputs['Transmission Weight'].default_value=1
gb.inputs['IOR'].default_value=1.586
# Original procedural texture, never a cropped manufacturer photograph.
for m,scale,strength in [(bronze,180,.12),(brass,150,.08),(strapmat,650,.3),(blue,350,.2)]:
    nt=m.node_tree;n=nt.nodes.new('ShaderNodeTexNoise');n.inputs['Scale'].default_value=scale
    bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=strength
    bump.inputs['Distance'].default_value=.00012
    nt.links.new(n.outputs['Fac'],bump.inputs['Height'])
    nt.links.new(bump.outputs[0],nt.nodes['Principled BSDF'].inputs['Normal'])

def mesh(name,verts,faces,mat,bevel=0,smooth=True):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    bm=bmesh.new();bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    o=bpy.data.objects.new(name,me);COL.objects.link(o)
    return finish(o,name,mat,bevel,smooth)

def cube(name,loc,dim,mat,bevel=.002):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=dim
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,mat,bevel,False)

def cylinder(name,loc,r,d,mat,axis=(0,-1,0),n=32,bevel=.0008):
    bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=d,location=loc)
    o=bpy.context.object;o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler()
    return finish(o,name,mat,bevel)

def ellipsoid(name,loc,dim,mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=1,location=loc)
    o=bpy.context.object;o.scale=dim
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,mat)

def curve(name,pts,r,mat):
    cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=10
    cu.bevel_depth=r;cu.bevel_resolution=3;cu.use_fill_caps=True
    sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(pts)-1)
    for p,co in zip(sp.bezier_points,pts):
        p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new(name,cu);COL.objects.link(o)
    return finish(o,name,mat)

def outline(t,rx,rz):
    c,s=math.cos(t),math.sin(t)
    return rx*c,rz*(s**.80 if s>=0 else -.80*(1-abs(c)**4)**.52)

def annulus(name,outer,inner,ys,zc,mat):
    n=80;verts=[]
    for y,radii in [(ys[0],outer),(ys[0],inner),(ys[1],outer),(ys[1],inner)]:
        verts.extend((x,y,z+zc) for x,z in [outline(2*math.pi*i/n,*radii) for i in range(n)])
    faces=[]
    for i in range(n):
        j=(i+1)%n
        faces += [(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),
                  (i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)]
    return mesh(name,verts,faces,mat,.0006)

def ring_xy(name,rx,ry,z,width,depth,mat):
    n=80;v=[]
    for zz,rr in [(z-depth/2,(rx,ry)),(z-depth/2,(rx-width,ry-width)),
                  (z+depth/2,(rx,ry)),(z+depth/2,(rx-width,ry-width))]:
        v.extend((rr[0]*math.cos(i*2*math.pi/n),rr[1]*math.sin(i*2*math.pi/n),zz) for i in range(n))
    f=[]
    for i in range(n):
        j=(i+1)%n
        f.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),
                  (i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
    return mesh(name,v,f,mat,.001)

def ribbon(name,points,width,mat):
    v=[]
    for co in points:
        v += [(co[0]-width/2,co[1],co[2]),(co[0]+width/2,co[1],co[2])]
    o=mesh(name,v,[(2*i,2*i+1,2*i+3,2*i+2) for i in range(len(points)-1)],mat)
    s=o.modifiers.new('Webbing thickness','SOLIDIFY');s.thickness=.0014
    return o

def look(o,target): o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
group('Studio_Excluded_From_Model')
bpy.ops.object.camera_add(location=VIEWS['threequarter'][0]);camera=bpy.context.object
camera.name='STUDIO_Camera';camera.data.type='ORTHO';camera.data.ortho_scale=.55;scene.camera=camera
look(camera,VIEWS['threequarter'][1])
for name,loc,power,size in [('Key',(-.45,-.50,.65),5,.55),('Fill',(.40,-.20,.40),3,.40),
                            ('Rim',(.10,.40,.60),8,.38),('Front',(0,-.6,.18),1,.30)]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name='STUDIO_'+name
    o.data.energy=power;o.data.shape='DISK';o.data.size=size;look(o,(0,0,.2))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.013))
floor=bpy.context.object;floor.name='STUDIO_Floor'
fm=bpy.data.materials.new('Studio_Backdrop');fm.diffuse_color=(.085,.10,.125,1)
floor.data.materials.append(fm)

def render_stage(stage):
    if opts.no_stages:return
    for key,(loc,target) in VIEWS.items():
        camera.location=loc;look(camera,target)
        scene.render.filepath=str(OUT/'renders'/('stage%d_%s.png'%(stage,key)))
        bpy.ops.render.render(write_still=True)
    print('STAGE_COMPLETED',stage,flush=True)

# Stage 1: shell loft connects to the window; rear and neck remain hollow.
group('01_Shell_and_FacePort')
n=P['shell_segments'];v=[]
for k,(y,rx,rz,zc) in enumerate(P['shell_sections']):
    for i in range(n):
        t=2*math.pi*i/n;x,z=outline(t,rx,rz)
        # GUESS: gradually fade window-like lower contour into rounded rear dome.
        blend=min(1,k/3);z=z*(1-blend)+rz*math.sin(t)*blend
        v.append((x,y,z+zc))
f=[]
for k in range(len(P['shell_sections'])-1):
    for i in range(n):
        j=(i+1)%n;f.append((k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i))
f.append(tuple(range((len(P['shell_sections'])-1)*n,len(v))))
shell=mesh('Shell_Yellow_520-065_ESTIMATED',v,f,yellow)
bm=bmesh.new();bm.from_mesh(shell.data)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                       plane_co=(0,0,P['neck_z']),plane_no=(0,0,1),clear_inner=True)
bm.to_mesh(shell.data);bm.free()
sub=shell.modifiers.new('Smooth shell loft','SUBSURF');sub.levels=1
sol=shell.modifiers.new('Hollow composite wall - estimated','SOLIDIFY');sol.thickness=P['wall'];sol.offset=-1
annulus('Ring_PortChrome_560-070',P['port_outer'],P['port_inner'],(-.150,-.138),.220,chrome)
annulus('ViewPort_Seal_510-260',(.117,.077),(.112,.073),(-.145,-.136),.220,rubber)
# A shallow polycarbonate lens with actual thickness, not a dark opaque card.
nn=80;verts=[(0,-.147,.220)]
# GUESS: use a near-flat lens, avoiding unmeasured lens-like optical distortion.
verts.extend((x,-.147,z+.220) for x,z in
             [outline(2*math.pi*i/nn,.114,.074) for i in range(nn)])
lens=mesh('Glass_Port_520-004',verts,[(0,i+1,(i+1)%nn+1) for i in range(nn)],glass)
ss=lens.modifiers.new('Polycarbonate thickness - estimated','SOLIDIFY');ss.thickness=.003
render_stage(1)

# Stage 2: hardware is assembled in world coordinates (-Y forward, +Z up).
group('02_Metal_and_Valves')
path=P['handle_path'];verts=[]
for y,z,w in path: verts += [(-w,y,z),(w,y,z)]
handle=mesh('Handle_Brass_560-014',verts,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(path)-1)],bronze)
sub=handle.modifiers.new('Arch smoothing','SUBSURF');sub.levels=2
sol=handle.modifiers.new('Handle thickness - estimated','SOLIDIFY');sol.thickness=.005
cube('Handle_FrontFoot',(0,-.141,.314),(.084,.019,.010),bronze,.003)
cube('Handle_RearFoot',(0,.099,.284),(.045,.019,.010),bronze,.003)
reg=Vector(P['regulator'])
cylinder('Regulator_SuperFlow_505-027_Body',reg,.045,.036,chrome)
ellipsoid('Regulator_ConvexCover',reg+Vector((0,-.025,0)),(.048,.012,.048),chrome)
cylinder('Regulator_PurgeButton',reg+Vector((0,-.038,0)),.020,.004,rubber,n=48)
cylinder('Regulator_AdjustmentStem',(.056,-.154,.090),.012,.030,chrome,axis=(1,0,0),n=6)
cylinder('Regulator_AdjustmentKnob',(.082,-.154,.090),.015,.025,chrome,axis=(1,0,0))
cylinder('Regulator_InletNipple',(-.062,-.153,.088),.012,.036,chrome,axis=(1,0,0),n=6)
cylinder('Regulator_InletCap',(-.083,-.153,.088),.013,.018,rubber,axis=(1,0,0))
sb=Vector(P['sideblock'])
cylinder('SideBlock_Chrome_505-024',sb,.019,.090,chrome,axis=(0,1,.05),bevel=.004)
cube('SideBlock_MountingFoot',(-.128,-.065,.176),(.022,.063,.021),bronze)
for name,root,axis,rad in [('Knob_Black_SteadyFlow_520-524',(-.147,-.117,.199),(-.26,-.96,.10),.023),
                           ('Knob_Black_Emergency_520-525',(-.148,-.015,.184),(-.98,-.14,0),.020)]:
    a=Vector(axis).normalized();root=Vector(root)
    cylinder(name+'_Stem',root,.010,.018,chrome,axis=a)
    cylinder(name,root+a*.018,rad,.028,rubber,axis=a,n=48,bevel=.001)
    cylinder(name+'_CentreCap',root+a*.033,rad*.45,.002,chrome,axis=a,n=24)
    u=a.cross(Vector((0,0,1))).normalized();w=u.cross(a).normalized()
    for i in range(12):
        t=i*math.tau/12
        cylinder(name+'_Grip_%02d'%i,root+a*.018+(rad-.001)*(u*math.cos(t)+w*math.sin(t)),
                 .002,.025,rubber,axis=a,n=8,bevel=0)
cylinder('CheckValve_Brass_555-195',(-.153,-.010,.148),.014,.061,brass,axis=(0,1,0))
cylinder('CheckValve_Hex',(-.153,.026,.148),.016,.013,brass,axis=(0,1,0),n=6)
cylinder('CheckValve_DustCap',(-.153,.042,.148),.011,.020,cap,axis=(0,1,0))
cylinder('Emergency_Inlet_DustCap',(-.145,.047,.190),.009,.015,cap,axis=(0,1,0))
cylinder('Emergency_InletStem',(-.145,.011,.190),.009,.064,brass,axis=(0,1,0))
cylinder('CheckValve_SideBlock_Adapter',(-.145,-.015,.171),.009,.034,brass,axis=(0,0,1))
cylinder('BentTube_UpperCoupling',(-.142,-.101,.161),.012,.027,chrome,axis=(0,-.30,-.95),n=6)
tubepts=[(-.142,-.103,.156),(-.145,-.129,.116),(-.124,-.150,.071),(-.093,-.155,.073),(-.067,-.155,.088)]
curve('BentTube_Chrome_555-155',tubepts,.006,chrome)
cylinder('BentTube_LowerCoupling',(-.084,-.155,.080),.012,.026,chrome,axis=(.95,0,.30),n=6)
cylinder('NoseBlock_Hex', (0,-.159,.147),.008,.014,chrome,n=6)
cylinder('NoseBlock_Knob_550-062',(0,-.174,.147),.008,.016,chrome)
render_stage(2)

group('03_Exhaust_Straps_Neck_Weights')
ring_xy('NeckClamp_505-055',.121,.123,.058,.012,.018,rubber)
ring_xy('Neck_Yoke_520-117',.127,.127,.041,.022,.012,rubber)
ring_xy('NeckDam_Neoprene_510-533',.104,.108,.027,.027,.026,rubber)
# Blue lining is the cushion seen at the base, not a claim of blue neoprene.
ring_xy('NeckCushion_VisibleBlue',.089,.093,.018,.018,.018,blue)
for sign in [-1,1]:
    # GUESS: exterior wings from references 1 and 12; valve internals omitted.
    o=ellipsoid('Exhaust_Black_Wing_'+str(sign),(sign*.087,-.130,.071),(.049,.030,.043),rubber)
    o.rotation_euler[1]=sign*-.27
    pts=[(sign*.122,-.119,.146),(sign*.123,-.145,.126),
         (sign*.116,-.157,.095),(sign*.096,-.155,.066),(sign*.064,-.150,.051)]
    verts=[]
    for x,y,z in pts:verts.extend([(x-sign*.022,y,z),(x+sign*.022,y+.018,z+.005)])
    o=mesh('Whisker_Deflector_'+str(sign),verts,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(4)],rubber)
    s=o.modifiers.new('Rubber wing thickness','SOLIDIFY');s.thickness=.003
    s=o.modifiers.new('Rubber smoothing','SUBSURF');s.levels=1
    cylinder('Exhaust_Outlet_Rim_'+str(sign),(sign*.028,-.158,.036),.012,.025,rubber)
    cylinder('Exhaust_Outlet_Recess_'+str(sign),(sign*.028,-.172,.036),.008,.002,strapmat)
    cube('Whisker_Clamp_'+str(sign),(sign*.049,-.169,.039),(.009,.008,.018),rubber)
    cylinder('Whisker_Clamp_BrassScrew_'+str(sign),(sign*.049,-.174,.043),.002,.003,brass,n=12,bevel=0)
rear=ellipsoid('Weights_Brass_Rear_560-005',(0,.136,.181),(.088,.014,.066),bronze)
for sign in [-1,1]:
    ellipsoid('Weights_Brass_Side_'+str(sign),(sign*.122,.042,.213),(.012,.040,.031),bronze)
    # GUESS: webbing follows the loft surface, using tangent angular offsets.
    verts=[]
    for k,t in zip([2,3,4,5,6],[.9,.5,.1,-.45,-.85]):
        y,rx,rz,zc=P['shell_sections'][k]
        for angle in [t-.10,t+.10]:
            verts.append((sign*rx*math.cos(angle)*1.016,y,zc+rz*math.sin(angle)*1.016))
    st=mesh('Strap_Black_Diagonal_'+str(sign),verts,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(4)],strapmat)
    ss=st.modifiers.new('Webbing thickness','SOLIDIFY');ss.thickness=.0015
    buckle=cube('Strap_Adjuster_'+str(sign),(sign*.100,-.071,.303),(.031,.010,.016),rubber)
    buckle.rotation_euler[1]=sign*-.65
cube('Hinge_NeckClamp_Rear',(0,.126,.060),(.048,.026,.030),chrome,.002)
cylinder('Hinge_Pin',(0,.132,.061),.007,.063,chrome,axis=(1,0,0))
for sign in [-1,1]:
    cube('Neck_Latch_'+str(sign),(sign*.067,-.102,.045),(.029,.033,.023),chrome)
    cylinder('Neck_PullPin_'+str(sign),(sign*.086,-.104,.045),.004,.022,chrome,axis=(1,0,0))
render_stage(3)

group('04_Visible_Interior')
for sign in [-1,1]:
    ellipsoid('Interior_Cushion_'+str(sign),(sign*.090,.005,.215),(.020,.070,.075),blue)
    cylinder('Earphone_VisualOnly_'+str(sign),(sign*.107,-.060,.218),.022,.010,rubber,axis=(1,0,0))
ellipsoid('Interior_Cushion_Crown',(0,.017,.285),(.065,.064,.012),blue)
ellipsoid('OralNasal_Mask_510-747',(0,-.068,.156),(.052,.035,.049),rubber)
# GUESS: visible upper air train path and holes; no simulated gas internals.
airpts=[]
for i in range(13):
    t=math.radians(18+144*i/12);x,z=outline(t,.109,.070)
    airpts.append((x,-.128,z+.220))
curve('AirTrain_Defog_545-016',airpts,.004,rubber)
render_stage(4)

group('05_Fasteners_and_Identification')
prototype=None
for i,deg in enumerate(P['screw_angles']):
    x,z=outline(math.radians(deg),.123,.082)
    o=cylinder('Screws_Port_%02d'%(i+1),(x,-.152,z+.220),.0034,.0028,chrome,n=16,bevel=0)
    if prototype is None:prototype=o.data
    else:o.data=prototype # linked mesh, as requested; separate editable objects
    slot=cube('Screw_Slot_%02d'%(i+1),(x,-.1536,z+.220),(.0045,.0003,.0006),rubber,0)
    slot.rotation_euler[1]=deg*.07
for i,x in enumerate([-.027,0,.027]):
    cylinder('Handle_Fastener_%d'%i,(x,-.141,.322),.003,.004,chrome,n=16,bevel=0)
for sign in [-1,1]:
    for j in range(2):
        cylinder('Whisker_Fastener_%d_%d'%(sign,j),(sign*(.107-j*.026),-.156,.139-j*.008),
                 .003,.003,chrome,n=16,bevel=0)
# Original blank label plates: layout cues, no counterfeit manufacturer logo.
tag=cube('SideBlock_Label_Blank',(-.155,-.070,.167),(.002,.042,.039),rubber,.0004)
for i in range(3):
    cube('Label_Layout_Line_%d'%i,(-.1564,-.070,.170-i*.006),(.0004,.026-i*.003,.0008),chrome,0)
for i in range(16):
    t=i*math.tau/16
    curve('NoseKnob_Knurl_%02d'%i,[(.008*math.cos(t),-.168,.147+.008*math.sin(t)),
                                 (.008*math.cos(t),-.180,.147+.008*math.sin(t))],.00025,chrome)
render_stage(5)

# Bake curves/modifiers, UV unwrap; separate parts and material slots survive.
for o in MODEL:
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    if o.type=='CURVE':bpy.ops.object.convert(target='MESH')
    if o.type=='MESH' and o.data.users>1:o.data=o.data.copy()
    for m in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=m.name)
    if o.type=='MESH':
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
        o.location*=GLOBAL_SCALE
        # Make an independent baked export mesh before applying global scale.
        for v in o.data.vertices:v.co*=GLOBAL_SCALE
render_scale=GLOBAL_SCALE
for key,(loc,target) in list(VIEWS.items()):
    VIEWS[key]=(tuple(c*render_scale for c in loc),tuple(c*render_scale for c in target))
camera.data.ortho_scale=.51*render_scale
scene.render.resolution_x=1080;scene.render.resolution_y=1080;scene.cycles.samples=32
render_stage(6)
camera.location=VIEWS['threequarter'][0];look(camera,VIEWS['threequarter'][1])
bpy.ops.object.select_all(action='DESELECT')
for o in MODEL:o.select_set(True)
bpy.context.view_layer.objects.active=shell
# Open in material mode focused on the complete model.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.region_3d.view_distance=.60
            area.spaces.active.region_3d.view_location=(0,0,.17)
            area.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
scene['historical_basis']='SL17B photos and Document #150424001; local dimensions GUESS'
scene['measured_dimensions_available']=False
scene['global_scale_guess']=GLOBAL_SCALE
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'superlite17b.blend'))
stats=[]
for o in MODEL:
    o.data.calc_loop_triangles()
    stats.append(dict(name=o.name,triangles=len(o.data.loop_triangles),uv=bool(o.data.uv_layers)))
corners=[o.matrix_world@Vector(c) for o in MODEL for c in o.bound_box]
bounds=[max(v[i] for v in corners)-min(v[i] for v in corners) for i in range(3)]
report=dict(blender=bpy.app.version_string,global_scale_guess=GLOBAL_SCALE,
            measured_dimensions=False,dimensions_m=bounds,mesh_count=len(stats),
            triangle_count=sum(x['triangles'] for x in stats),objects=stats)
(OUT/'geometry-summary.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('DELIVERY',json.dumps({k:v for k,v in report.items() if k!='objects'}),flush=True)
if report['triangle_count']>60000:raise RuntimeError('Triangle budget exceeded')
