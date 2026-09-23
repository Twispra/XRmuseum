import bpy, math
from mathutils import Vector
floor=material('Studio - charcoal',(.017,.025,.032),.15,.42)
plinth=material('Plinth - blue graphite',(.026,.043,.055),.45,.30)
labelmat=material('Label - ivory',(.66,.72,.72),.1,.5)
accent=material('Label - muted copper',(.50,.25,.10),.65,.33)
box('STUDIO_Floor',(0,0,-.022),(200,200,.040),floor,studio_coll,edge=0)
box('STUDIO_GrayboxPlinth',(-.31,.018,.027),(.44,.50,.054),plinth,studio_coll,edge=.008)
box('STUDIO_SamplePlinth',(.29,.018,.029),(.37,.29,.058),plinth,studio_coll,edge=.008)
box('STUDIO_SampleSupport',(.29,.043,.128),(.025,.024,.155),dark_metal,studio_coll,edge=.002)
rod('STUDIO_SampleFoot',(.19,.043,.066),(.39,.043,.066),.007,dark_metal,studio_coll,None)

def text_object(name,body,loc,size,mat):
    cu=bpy.data.curves.new(name,'FONT');cu.body=body;cu.size=size;cu.extrude=.000015
    ob=bpy.data.objects.new(name,cu);studio_coll.objects.link(ob);ob.location=loc
    ob.rotation_euler=(math.pi/2,0,0);cu.materials.append(mat)
    return ob
text_object('STUDIO_Label01','01  /  SCALE STUDY',(-.494,-.235,.028),.013,labelmat)
text_object('STUDIO_Label02','02  /  WINDOW STUDY',(.124,-.130,.028),.0115,labelmat)

world=bpy.data.worlds.new('Helmet_StudioWorld');world.use_nodes=True;scene.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs[0].default_value=(.11,.15,.19,1);bg.inputs[1].default_value=.32

def area(name,loc,target,power,color,size,size_y=None):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color
    if size_y:
        enum(data,'shape','RECTANGLE');data.size=size;data.size_y=size_y
    else: data.size=size
    ob=bpy.data.objects.new(name,data);studio_coll.objects.link(ob)
    ob.location=loc;ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
    return ob
area('STUDIO_KeySoftbox',(-.50,-.65,1.20),(0,0,.24),85,(1,.87,.72),.85,.55)
area('STUDIO_CoolFill',(.75,-.2,.65),(.05,0,.25),55,(.68,.83,1),.60,.8)
area('STUDIO_BackRim',(-.05,.65,.95),(0,0,.28),100,(1,.75,.46),.65,.35)
area('STUDIO_FrontStrip',(.1,-1,.48),(.28,0,.25),13,(1,.98,.93),.11,.65)

def camera(name,loc,target,lens=55,ortho=None):
    data=bpy.data.cameras.new(name);ob=bpy.data.objects.new(name,data)
    studio_coll.objects.link(ob);ob.location=loc
    ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
    data.lens=lens;data.clip_start=.005;data.clip_end=500
    if ortho:
        enum(data,'type','ORTHO');data.ortho_scale=ortho
    return ob
hero=camera('CAM_01_Overview',(.86,-1.95,.97),(0,0,.27),60)
close=camera('CAM_02_WindowCloseup',(.52,-.73,.42),(.29,-.016,.273),66)
open_cam=camera('CAM_03_WindowOpen',(.15,-.76,.46),(.29,0,.273),61)
gray_cam=camera('CAM_04_Graybox',(.24,-1.00,.70),(-.31,.0,.30),60)
scene.camera=hero
try: scene.render.engine='CYCLES'
except TypeError: pass
if hasattr(scene,'cycles'):
    scene.cycles.samples=48;scene.cycles.use_denoising=True
    scene.cycles.max_bounces=8;scene.cycles.transmission_bounces=6
scene.render.resolution_x=1800;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
enum(scene.render.image_settings,'file_format','PNG')
scene.render.film_transparent=False
if 'AgX' in [x.identifier for x in scene.view_settings.bl_rna.properties['view_transform'].enum_items]:
    scene.view_settings.view_transform='AgX'
scene.render.filepath=str(OUT/'renders'/'01_overview.png')
scene['readme']='Two separate assets at 1 BU = 1m. Camera/render stage excluded from exports. Original default scene preserved.'
for sc in bpy.data.screens:
    for ar in sc.areas:
        if ar.type=='VIEW_3D':
            ar.spaces.active.overlay.show_overlays=False
            ar.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'helmet-study-v01.blend'))
print('Studio, four cameras, metre units, material palette and .blend saved.')
