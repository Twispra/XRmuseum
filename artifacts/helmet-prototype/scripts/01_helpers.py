"""Original metre-scale geometry, built live through Blender MCP. No downloaded mesh."""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector, Matrix

OUT = Path('E:/XRmuseum/artifacts/helmet-prototype')
OUT.mkdir(parents=True, exist_ok=True)
for sub in ('renders', 'exports', 'qa'):
    (OUT/sub).mkdir(exist_ok=True)

def enum(owner, prop, value):
    available = [i.identifier for i in owner.bl_rna.properties[prop].enum_items]
    if value not in available:
        raise ValueError(f'{prop}: {value} not in {available}')
    setattr(owner, prop, value)

scene = bpy.data.scenes.new('XRmuseum_Helmet_Study_v01')
bpy.context.window.scene = scene
enum(scene.unit_settings, 'system', 'METRIC')
scene.unit_settings.scale_length = 1.0
scene['purpose'] = 'Original Mark V-inspired teaching prototype; dimensions calibrated to a published object envelope, not a measured replica.'
scene['units'] = 'metres; 1 Blender unit = 1 metre'

def collection(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c

gray_coll = collection('01_GRAYBOX_RealScale')
detail_coll = collection('02_PORTHOLE_QualitySample')
studio_coll = collection('03_STUDIO_NotForExport')

def empty(name, coll, loc=(0,0,0), parent=None):
    ob = bpy.data.objects.new(name, None)
    coll.objects.link(ob)
    ob.location = loc
    ob.parent = parent
    ob.empty_display_size = .025
    return ob

gray_root = empty('Helmet_Graybox_ROOT', gray_coll)
detail_root = empty('Porthole_Sample_ROOT', detail_coll)

def mesh(name, verts, faces, coll, mat=None, parent=None, smooth=True):
    me = bpy.data.meshes.new(name+'_Mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    ob.parent = parent
    if mat: me.materials.append(mat)
    for p in me.polygons: p.use_smooth = smooth
    return ob

def bevel(ob, width=.0007, segments=3):
    mod = ob.modifiers.new('Machined edge radius', 'BEVEL')
    mod.width = width
    mod.segments = segments
    enum(mod, 'limit_method', 'ANGLE')
    mod.angle_limit = math.radians(28)
    mod.harden_normals = True
    return mod

def basis(normal):
    n = Vector(normal).normalized()
    u = Vector((1,0,0)) if abs(n.x)<.9 else Vector((0,1,0))
    u = (u - n*u.dot(n)).normalized()
    v = n.cross(u)
    return u,v,n

def lathe(name, profile, center=(0,0,0), normal=(0,0,1), n=96,
          coll=None, mat=None, parent=None, closed=True, xy_scale=(1,1), smooth=True):
    u,v,w = basis(normal)
    c=Vector(center)
    verts=[]
    for radius, axial in profile:
        for i in range(n):
            a=2*math.pi*i/n
            verts.append(c+u*(radius*math.cos(a)*xy_scale[0])+v*(radius*math.sin(a)*xy_scale[1])+w*axial)
    faces=[]
    segments=len(profile) if closed else len(profile)-1
    for j in range(segments):
        jj=(j+1)%len(profile)
        for i in range(n):
            ii=(i+1)%n
            faces.append((j*n+i,j*n+ii,jj*n+ii,jj*n+i))
    return mesh(name, verts, faces, coll or detail_coll, mat, parent, smooth)

def ring(name, ro, ri, depth, center, normal=(0,-1,0), mat=None, coll=None, parent=None, n=96, edge=.0005):
    ob=lathe(name,[(ri,-depth/2),(ro,-depth/2),(ro,depth/2),(ri,depth/2)],center,normal,n,coll,mat,parent)
    if edge: bevel(ob,edge)
    return ob

def cylinder(name, radius, depth, center, normal=(0,0,1), mat=None, coll=None, parent=None, n=48, edge=.0004):
    u,v,w=basis(normal);c=Vector(center)
    verts=[c+u*(radius*math.cos(i*2*math.pi/n))+v*(radius*math.sin(i*2*math.pi/n))+w*z for z in (-depth/2,depth/2) for i in range(n)]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    ob=mesh(name,verts,faces,coll or detail_coll,mat,parent)
    ob.data.polygons[0].use_smooth=False;ob.data.polygons[1].use_smooth=False
    if edge: bevel(ob,edge)
    return ob

def box(name, center, dims, mat=None, coll=None, parent=None, edge=.001):
    c=Vector(center);x,y,z=[d/2 for d in dims]
    verts=[c+Vector(v) for v in [(-x,-y,-z),(-x,-y,z),(-x,y,-z),(-x,y,z),(x,-y,-z),(x,-y,z),(x,y,-z),(x,y,z)]]
    faces=[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]
    ob=mesh(name,verts,faces,coll or detail_coll,mat,parent,False)
    if edge: bevel(ob,edge)
    return ob

def rod(name, a, b, radius, mat, coll, parent, n=20):
    a=Vector(a);b=Vector(b)
    return cylinder(name,radius,(b-a).length,(a+b)/2,b-a,mat,coll,parent,n, min(radius*.2,.0005))

def torus(name, radius, tube, center, normal=(0,-1,0), mat=None, coll=None, parent=None,n=128,k=12):
    prof=[(radius+tube*math.cos(2*math.pi*j/k),tube*math.sin(2*math.pi*j/k)) for j in range(k)]
    return lathe(name,prof,center,normal,n,coll,mat,parent)

def material(name, color, metallic=0, rough=.4, variation=None):
    m=bpy.data.materials.new(name)
    m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metallic
    p.inputs['Roughness'].default_value=rough
    m.diffuse_color=(*color,1)
    if variation:
        ns=m.node_tree.nodes;ls=m.node_tree.links
        tex=ns.new('ShaderNodeTexCoord');tex.location=(-850,150)
        noise=ns.new('ShaderNodeTexNoise');noise.location=(-640,150)
        enum(noise,'noise_dimensions','3D');enum(noise,'noise_type','FBM')
        noise.inputs['Scale'].default_value=variation.get('scale',18)
        noise.inputs['Detail'].default_value=3
        noise.inputs['Roughness'].default_value=.65
        ls.new(tex.outputs['Object'],noise.inputs['Vector'])
        ramp=ns.new('ShaderNodeValToRGB');ramp.location=(-360,180)
        ramp.color_ramp.elements[0].position=.20
        ramp.color_ramp.elements[0].color=(*variation['dark'],1)
        ramp.color_ramp.elements[1].position=.80
        ramp.color_ramp.elements[1].color=(*color,1)
        ls.new(noise.outputs[0],ramp.inputs[0]);ls.new(ramp.outputs[0],p.inputs['Base Color'])
        fine=ns.new('ShaderNodeTexNoise');fine.location=(-610,-120)
        enum(fine,'noise_dimensions','3D');enum(fine,'noise_type','FBM')
        fine.inputs['Scale'].default_value=1450
        fine.inputs['Detail'].default_value=2
        ls.new(tex.outputs['Object'],fine.inputs['Vector'])
        bump=ns.new('ShaderNodeBump');bump.location=(-210,-70)
        bump.inputs['Strength'].default_value=.18
        bump.inputs['Distance'].default_value=.000028
        ls.new(fine.outputs[0],bump.inputs['Height']);ls.new(bump.outputs[0],p.inputs['Normal'])
    return m

gray=material('Clay - warm neutral',(.29,.33,.34),0,.55)
gray_dark=material('Clay - recessed parts',(.085,.105,.112),0,.5)
gray_trim=material('Clay - edges',(.42,.46,.46),0,.44)
copper=material('Copper - lightly oxidised',(.50,.20,.105),.96,.31,{'dark':(.17,.056,.028),'scale':22})
brass=material('Brass - machined warm alloy',(.64,.43,.17),.97,.27,{'dark':(.28,.155,.045),'scale':32})
brass_bright=material('Brass - polished edge',(.74,.53,.25),1,.23)
patina=material('Oxide - restrained crevice deposits',(.045,.12,.105),.32,.75)
rubber=material('Rubber - compressed gasket',(.011,.015,.014),0,.69)
dark_metal=material('Interior - aged bronze',(.072,.042,.022),.85,.45)
glass=material('Glass - actual 4 mm thickness',(.88,.97,.94),0,.065)
p=next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
p.inputs['Transmission Weight'].default_value=1
p.inputs['IOR'].default_value=1.47

def boolean_cut(ob, cutter):
    mod=ob.modifiers.new('Real aperture', 'BOOLEAN')
    enum(mod,'operation','DIFFERENCE')
    mod.object=cutter
    bpy.context.view_layer.objects.active=ob
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)

def focus(center, distance=1.6):
    q=(Vector((.7,-1.6,.8))-Vector((0,0,.15))).to_track_quat('Z','Y')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                sp=area.spaces.active
                sp.region_3d.view_location=Vector(center)
                sp.region_3d.view_distance=distance
                sp.region_3d.view_rotation=q
                enum(sp.shading,'type','MATERIAL')

print('Created isolated study scene and reusable metre-scale modelling helpers.')
