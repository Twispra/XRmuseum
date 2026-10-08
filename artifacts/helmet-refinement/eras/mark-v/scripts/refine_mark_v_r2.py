"""R2 delta edit of the reviewed R1 source, not a fresh reconstruction.
blender -b --python refine_mark_v_r2.py -- --source R1_BLEND --output NEW_DIRECTORY
Loads source; refuses an unexpected source hash or existing output. No Unity changes.
"""
import bpy,bmesh,math,json,sys,hashlib,ast
from pathlib import Path
from mathutils import Vector
import numpy as np
args=sys.argv[sys.argv.index('--')+1:]
SOURCE=Path(args[args.index('--source')+1]).resolve()
OUT=Path(args[args.index('--output')+1]).resolve()
original=Path(__file__).with_name('build_mark_v.py')
# The caller must explicitly identify the input revision; never edit it in place.
EXPECTED=json.loads((SOURCE.parent.parent/'qa/geometry-report.json').read_text(encoding='utf-8'))
input_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
if OUT==SOURCE.parent.parent or (OUT/'sources/mark-v-mod1.blend').exists():
    raise RuntimeError('Use a new output directory; existing sources are protected.')
for sub in ['sources/parts','exports','textures','previews','qa']:(OUT/sub).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene=bpy.context.scene;parts=bpy.data.collections['01_MARK_V_EDITABLE']
original_mesh_data={ob.data.name for ob in parts.objects if ob.type=='MESH'}
studio=bpy.data.collections['90_REVIEW_STUDIO_NOT_EXPORTED'];root=bpy.data.objects['MarkVMod1_ROOT']
groups={ob.name:ob for ob in root.children if ob.type=='EMPTY' and 'Assembly' in ob.name or ob.name=='FrontWindowPivot'}
bronze=bpy.data.materials['CastBronze_OriginalInterpretation'];edge=bpy.data.materials['HandledBronze_EstimatedWear']
rubber=bpy.data.materials['FaceSeal_Rubber_Approximate'];interior=bpy.data.materials['InteriorTin_Approximate']
body=bpy.data.objects['Bonnet_HollowCopper_TinFinish'];door=bpy.data.objects['FrontWindowPivot'];seat=groups['FrontSeatAssembly']
assert json.loads((SOURCE.parent.parent/'qa/export-readback.json').read_text(encoding='utf-8'))['checks'][0]['source_sha256']==input_hash,'Input differs from reviewed R1 asset'
assert not scene.get('refinement_revision'),'Already refined input: do not repeat deformation'
door.rotation_euler.z=0;body.hide_render=False;body.hide_viewport=False
bpy.context.view_layer.update()
# Load only named helper function definitions. No top-level generator code runs.
helpers={'move','empty','finish','mesh','bevel','cylinder','sphere','ring','torus','rod','curve','cut','thread','export_copy','noise'}
tree=ast.parse(original.read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in helpers],type_ignores=[]),str(original),'exec'),globals())
changed=[]
def remove(name):
    ob=bpy.data.objects.get(name)
    if ob:bpy.data.objects.remove(ob,do_unlink=True)
def preserve_parent(ob,parent):
    bpy.context.view_layer.update();w=ob.matrix_world.copy();ob.parent=parent;ob.matrix_world=w
def narrow_outer(name,old_outer,new_outer,inner):
    ob=bpy.data.objects[name]
    for v in ob.data.vertices:
        r=math.hypot(v.co.x,v.co.y)
        if r>inner+.0005:
            newr=inner+(r-inner)*(new_outer-inner)/(old_outer-inner)
            v.co.x*=newr/r;v.co.y*=newr/r
    changed.append(name)
for name,old,new,inner in [('FrontSeat_CastFrame',.086,.080,.060),('Door_CastFrame',.080,.076,.059),('TopSeat',.066,.0625,.048),('SideSeat_-1',.067,.065,.048),('SideSeat_1',.067,.065,.048)]:
    narrow_outer(name,old,new,inner)
for v in bpy.data.objects['Door_HandledOuterLip'].data.vertices:
    r=math.hypot(v.co.x,v.co.y);v.co.x*=.074/.077;v.co.y*=.074/.077
# Seat remains on the shell, but its front surface meets the gasket rather than
# leaving a visible gap. Its cast thickness remains a photographic estimate.
frame=bpy.data.objects['FrontSeat_CastFrame']
for v in frame.data.vertices:v.co.z*=.028/.024
frame.location.y=-.138
remove('FaceplateSeal_Rubber')
seal=ring('FaceplateSeal_Rubber',.070,.064,.0024,(0,-.1532,.347),(0,-1,0),rubber,None)
preserve_parent(seal,door)
seal['reference']='S08 26009 / S09 flat door gasket; dimensions estimated'
# Explicit revolved profile avoids Boolean/bevel slivers at the very shallow groove.
old=bpy.data.objects['Door_CastFrame'];w=old.matrix_world.copy();remove(old.name)
profile=[(.059,-.007),(.0636,-.007),(.0636,-.0058),(.0704,-.0058),(.0704,-.007),(.0758,-.007),(.076,-.0068),(.076,.0068),(.0758,.007),(.0592,.007),(.059,.0068)]
vv=[(r*math.cos(j*2*math.pi/96),r*math.sin(j*2*math.pi/96),z) for r,z in profile for j in range(96)]
ff=[]
for i in range(len(profile)):
    for j in range(96):ff.append((i*96+j,i*96+(j+1)%96,((i+1)%len(profile))*96+(j+1)%96,((i+1)%len(profile))*96+j))
frame=mesh('Door_CastFrame',vv,ff,bronze,None);frame.matrix_world=w;preserve_parent(frame,door)
frame['groove_status']='Estimated 1.2mm groove; not a measured manufacturing tolerance'
# A retaining washer and swing-bolt pivot, instead of a static decorative screw.
toggle=empty('DoorTogglePivot',seat);toggle.location=(-.089,-.143,.347)
toggle['release_angle_deg']=-70;toggle['motion_status']='Illustrated release, not measured travel'
moving=[ob for ob in list(seat.children) if ob.name.startswith(('DoorToggleBolt_','DoorToggleThread_','DoorToggleNutCore','DoorToggleWing_'))]
for ob in moving:preserve_parent(ob,toggle)
nutpivot=empty('DoorToggleNutPivot',toggle);nutpivot.location=(0,-.034,0)
bpy.context.view_layer.update()
for ob in moving:
    if 'NutCore' in ob.name or 'Wing_' in ob.name:preserve_parent(ob,nutpivot)
washer=ring('DoorToggle_RetainingWasher',.0081,.0040,.0013,(-.089,-.185,.347),(0,-1,0),edge,None,48)
washer.modifiers[0].width=.00015
preserve_parent(washer,toggle)
rod('DoorToggle_PivotPin',(-.089,-.143,.336),(-.089,-.143,.358),.0026,edge,seat)
for sign in [-1,1]:
    rod('DoorToggle_FixedLug_'+str(sign),(-.073,-.137,.347+sign*.008),(-.089,-.143,.347+sign*.008),.0035,bronze,seat)
    rod('DoorToggle_PinKeeper_'+str(sign),(-.092,-.145,.347+sign*.012),(-.086,-.145,.347+sign*.012),.0007,edge,seat,16)
# Actual holes and keeper on the removable front hinge pin.
pin=bpy.data.objects['HingePin_Removable']
for z in [.308,.315]:
    cutter=cylinder('TEMP_PinHole',.0010,.012,(.087,-.146,z),(1,0,0),None,root,24)
    cut(pin,cutter,'Cotter hole, estimated location')
curve('HingePin_KeeperCotter',[(.079,-.146,.308),(.082,-.149,.308),(.087,-.149,.308),(.094,-.148,.308),(.098,-.146,.307),(.094,-.144,.309),(.082,-.144,.308),(.079,-.146,.308)],.00065,edge,seat,2)
# A broader, slightly less regular hand-formed corselet; a shared smooth field
# deforms the plate AND its associated fittings, preserving their seating.
for ob in list(groups['CorseletAssembly'].children):
    if ob.type!='MESH':continue
    w=ob.matrix_world.copy();inv=w.inverted()
    for v in ob.data.vertices:
        p=w@v.co;r=math.hypot(p.x,p.y);a=math.atan2(p.x,-p.y)
        t=max(0,min(1,(r-.109)/.074))
        p.x*=1+.030*t*t
        p.z+=.0022*t*t*math.cos(3*a+.4)+.0010*t*t*math.sin(7*a)
        v.co=inv@p
    changed.append(ob.name)
# One long stud on the wearer's left side of the front-centre stud (S08).
studs=[ob for ob in groups['CorseletAssembly'].children if ob.name.endswith('_Stud')]
longstud=min([ob for ob in studs if ob.matrix_world.translation.x>.02],key=lambda o:o.matrix_world.translation.y)
for v in longstud.data.vertices:v.co.z=(v.co.z+.012)*1.65-.012
longstud['length_status']='one long stud supported by S08; extra length is an estimate'
longstud['nominal_extra_length_m']=.0156
thread_name=longstud.name[:-5]+'_StudThread_12TPI'
old_thread=bpy.data.objects[thread_name];thread_world=old_thread.matrix_world.copy();remove(thread_name)
longthread=thread(thread_name,(0,0,0),.00635,.0254/12,.0326,groups['CorseletAssembly'])
longthread.matrix_world=thread_world
# Flattened sheet-metal channels follow the inner wall; no fictional round pipes.
inside=groups['InteriorInterpretiveAssembly']
for ob in list(inside.children):
    if ob.name.startswith('AirDuct_'):remove(ob.name)
def sheet_duct(name,points,width,height):
    points=[Vector(p) for p in points];vv=[];ff=[];thick=.020*.0254
    # A thin closed-section strip; dimensions of width/height and routing assumed.
    section=[(-width/2,0),(-width/2,height),(width/2,height),(width/2,0),(width/2-thick,0),(width/2-thick,height-thick),(-width/2+thick,height-thick),(-width/2+thick,0)]
    for i,p in enumerate(points):
        tangent=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
        n=Vector((-p.x,-p.y,.33-p.z)).normalized();u=tangent.cross(n).normalized();n=u.cross(tangent).normalized()
        vv.extend(p+u*x+n*z for x,z in section)
    for i in range(len(points)-1):
        for j in range(8):a=i*8+j;b=i*8+(j+1)%8;ff.append((a,b,b+8,a+8))
    ff.extend([tuple(reversed(range(8))),tuple((len(points)-1)*8+j for j in range(8))])
    ob=bevel(mesh(name,vv,ff,interior,inside),.00015,2)
    ob['evidence']='S09 .020 inch sheet copper channel; current-description nominal gauge, routing/width/height interpretive'
sheet_duct('AirDuct_Interpretive_BackToCrown',[(-.061,.112,.344),(-.052,.115,.387),(-.035,.103,.430),(0,.069,.466),(0,.025,.488),(0,-.024,.488)],.019,.0045)
for sign in [-1,1]:
    sheet_duct('AirDuct_Interpretive_SideBranch_'+str(sign),[(0,.043,.482),(sign*.038,.025,.478),(sign*.071,.011,.462),(sign*.101,0,.432),(sign*.128,0,.390)],.013,.0035)
sheet_duct('AirDuct_Interpretive_TopBranch',[(0,-.024,.488),(0,-.053,.474),(0,-.075,.450)],.013,.0035)
# Six new original maps: quieter macro variation, more fine grain and scuffs.
# They do not reproduce the specimen's exact wear or measured optical properties.
def revise_surface(material,low,high,seed,rough):
    size=2048;n,rng=noise(size,seed);grain=rng.random(n.shape).astype(np.float32)
    micro,_=noise(size,seed+100);v=.18+.64*n
    rgb=np.array(low)*(1-v[:,:,None])+np.array(high)*v[:,:,None]
    pits=np.maximum(0,(grain-.997)/.003)*(micro>.5)
    rgb*=.97+.06*grain[:,:,None];rgb*=1-.24*pits[:,:,None]
    scratches=np.zeros_like(n)
    for j in range(140):
        x,y=rng.integers(0,size,2);length=int(rng.integers(8,65));slope=rng.uniform(-1,1)
        for k in range(length):scratches[(y+int(k*slope))%size,(x+k)%size]=.25
    rgb+=scratches[:,:,None]*.065
    roughmap=np.clip(rough+.13*n+.025*grain-.05*scratches,0,1)
    h=.012*micro+.035*grain-.015*scratches-.03*pits;gy,gx=np.gradient(h)
    normal=np.stack((-gx*1.4,-gy*1.4,np.ones_like(n)),axis=2);normal/=np.linalg.norm(normal,axis=2)[:,:,None];normal=normal*.5+.5
    for kind,val in [('BaseColor',rgb),('Roughness',np.repeat(roughmap[:,:,None],3,axis=2)),('Normal',normal)]:
        im=bpy.data.images[material.name+'_'+kind];rgba=np.ones((size,size,4),dtype=np.float32);rgba[:,:,:3]=np.clip(val,0,1)
        if im.packed_file:im.unpack(method='REMOVE')
        im.pixels.foreach_set(rgba.ravel());im.filepath_raw=str(OUT/'textures'/(im.name+'.png'));im.file_format='PNG';im.save();im.pack()
    material.node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value=.88
revise_surface(bpy.data.materials['AgedTin_OriginalInterpretation'],(.105,.112,.113),(.275,.290,.288),5424201,.53)
revise_surface(bronze,(.115,.063,.028),(.295,.178,.073),5424202,.44)
p=edge.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.285,.173,.065,1);p.inputs['Roughness'].default_value=.39
p=interior.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.14,.16,.16,1);p.inputs['Roughness'].default_value=.60
p=bpy.data.materials['HistoricalGlass_VisualPropertiesAssumed'].node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.96,.975,.98,1);p.inputs['Roughness'].default_value=.11;p.inputs['Transmission Weight'].default_value=1
# Guard rims and feet join the bowed bars to their screws and cast window seats.
# Their small stand-off dimensions are modeled estimates, not manufacturer drawings.
lip=bpy.data.objects['Door_HandledOuterLip'];lip.location.y-=.008
for j in range(4):
    a=math.pi/4+j*math.pi/2
    cylinder('DoorGuard_MountFoot_'+str(j),.0034,.010,(.073*math.cos(a),-.174,.347+.073*math.sin(a)),(0,-1,0),bronze,door,32)
for sign in [-1,1]:
    torus('SideGuard_SupportRim_'+str(sign),.0615,.0018,(sign*.165,0,.337),(sign,0,0),bronze,groups['SideWindowsAssembly'])
    for j in range(4):
        a=math.pi/4+j*math.pi/2
        cylinder('SideGuard_MountFoot_'+str(sign)+'_'+str(j),.0032,.016,(sign*.157,.059*math.cos(a),.337+.059*math.sin(a)),(sign,0,0),bronze,groups['SideWindowsAssembly'],32)
        bpy.data.objects['SideGuardScrew_'+str(sign)+'_'+str(j)].location.x=sign*.1665
tc=Vector((0,-.085,.462));td=Vector((0,-.58,.815));q=td.to_track_quat('Z','Y')
ob=torus('TopGuard_SupportRim',.062,.0015,tc+td*.024,td,bronze,groups['TopWindowAssembly']);ob.scale=(1.42,.88,1)
for j in range(4):
    a=math.pi/4+j*math.pi/2
    cylinder('TopGuard_MountFoot_'+str(j),.0032,.017,tc+q@Vector((.087*math.cos(a),.054*math.sin(a),.0155)),td,bronze,groups['TopWindowAssembly'],32)
for name in ['NeckJoint_RolledLip','RearLock_Pivot','RearLock_BallA','RearLock_BallB','RearLock_Lever']:
    bpy.data.objects[name].data.materials.clear();bpy.data.objects[name].data.materials.append(bpy.data.materials['AgedTin_OriginalInterpretation'])
for ob in list(parts.objects):
    if ob.type=='CURVE':
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH')
    if ob.type!='MESH':continue
    if ob.data.name not in original_mesh_data or ob.name in {'Door_CastFrame','HingePin_Removable'}:
        bpy.context.view_layer.objects.active=ob
        for m in list(ob.modifiers):
            if m.type=='BEVEL':m.width=min(m.width,.00020)
            bpy.ops.object.modifier_apply(modifier=m.name)
    bm=bmesh.new();bm.from_mesh(ob.data)
    if ob.data.name not in original_mesh_data or ob.name in {'Door_CastFrame','HingePin_Removable'}:
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
        bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
    if not ob.data.uv_layers or ob.name in {'Door_CastFrame','HingePin_Removable'}:
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
scene['refinement_revision']='R2';scene['input_source_sha256']=input_hash
scene['new_evidence']='S08/S09 manufacturer construction; S10-12 current part photos, not 1944 measured geometry'
root['refinement_revision']='R2'
bpy.context.view_layer.update();bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'sources/mark-v-mod1.blend'))
export_copy('mark-v-mod1',list(parts.objects));export_copy('mark-v-front-window',[door,*door.children])
for im in bpy.data.images:
    if im.packed_file:im.unpack(method='REMOVE')
    im.filepath='//../../textures/'+Path(im.filepath).name
for name,group in groups.items():
    descendants=list(group.children_recursive)
    bpy.data.libraries.write(str(OUT/'sources/parts'/(name+'.blend')),{group,*descendants},fake_user=True,compress=True,path_remap='NONE')
for im in bpy.data.images:im.filepath=str(OUT/'textures'/Path(im.filepath).name);im.pack()
deps=bpy.context.evaluated_depsgraph_get();stats=[]
for ob in parts.objects:
    if ob.type!='MESH':continue
    ev=ob.evaluated_get(deps);me=ev.to_mesh();me.calc_loop_triangles();stats.append({'name':ob.name,'parent':ob.parent.name if ob.parent else None,'vertices':len(me.vertices),'triangles':len(me.loop_triangles)});ev.to_mesh_clear()
report={**EXPECTED,'revision':'R2','input_source_sha256':input_hash,'meshes':len(stats),'triangles':sum(s['triangles'] for s in stats),'parts':stats,'generator_sha256':hashlib.sha256(original.read_bytes()).hexdigest(),'revision_script_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'source_sha256':hashlib.sha256((OUT/'sources/mark-v-mod1.blend').read_bytes()).hexdigest(),'changed_geometry':changed,'interior_status':'flattened sheet channels; routing still interpretive'}
(OUT/'qa/geometry-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
camera=scene.camera;d=camera.data
def aim(loc,target,scale):
    camera.location=loc;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=scale
views=[('01-overview',(.70,-1,.63),(0,0,.259),.66),('02-front',(0,-1,.37),(0,-.008,.259),.64),('03-wearer-left',(1,0,.37),(0,0,.260),.65),('04-wearer-right',(-1,0,.36),(0,0,.260),.65),('05-rear',(-.5,.9,.47),(0,.025,.275),.66),('06-window-detail',(.18,-.65,.40),(0,-.135,.348),.29),('07-rear-connections',(-.28,.75,.40),(0,.165,.311),.28),('08-brail-detail',(.35,-.5,.26),(0,-.100,.145),.35)]
for name,loc,target,scale in views:
    aim(loc,target,scale);scene.render.filepath=str(OUT/'previews'/(name+'.png'));bpy.ops.render.render(write_still=True);print('R2_RENDER_READY',name,flush=True)
toggle.rotation_euler.z=math.radians(-70);door.rotation_euler.z=math.radians(100);bpy.context.view_layer.update();aim((.55,-.95,.50),(0,-.10,.343),.38)
scene.render.filepath=str(OUT/'previews/09-window-open.png');bpy.ops.render.render(write_still=True)
door.rotation_euler.z=0;toggle.rotation_euler.z=0;body.hide_render=True;aim((.62,-.95,.68),(0,0,.329),.54)
scene.render.filepath=str(OUT/'previews/10-interior-interpretive.png');bpy.ops.render.render(write_still=True)
print('MARK_V_R2_READY',report['meshes'],report['triangles'],flush=True)
