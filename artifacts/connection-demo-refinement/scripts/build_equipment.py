"""Evidence-led teaching assets, Blender 4.5 LTS. Generate into a NEW output directory.
Original meshes/maps; R01 dimensions only for bore and stroke; all outer dimensions estimated.
Source is editable; never rerun this generator over artist changes.
"""
import bpy,bmesh,math,sys,json,hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]; OUT=Path(args[args.index('--output')+1]).resolve()
if (OUT/'sources/equipment.blend').exists(): raise RuntimeError('Existing source protected. Use a NEW output directory.')
for s in ['sources/parts','exports','textures','previews','qa']:(OUT/s).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1;scene.render.fps=30
scene['evidence']='references/parameters.json; 1916 Navy Mk III pump and telephone, interpretive exterior. Not a matched 1944 set.'
asset=bpy.data.collections.new('01_EDITABLE_ASSETS');scene.collection.children.link(asset)
studio=bpy.data.collections.new('90_STUDIO_NOT_EXPORTED');scene.collection.children.link(studio)
def move(o,col=asset):
 for c in list(o.users_collection):c.objects.unlink(o)
 col.objects.link(o);return o
def node(n,parent=None):
 o=bpy.data.objects.new(n,None);asset.objects.link(o);o.parent=parent;o.empty_display_size=.02;return o
roots={n:node(n) for n in ['Pump_ROOT','SurfaceTelephone_ROOT','DiverTelephone_ROOT']}
for o in roots.values():o['scale_status']='Metres. Outer shape estimated; not scanned or manufactured drawing.'
def mat(n,c,metal=0,rough=.4):
 m=bpy.data.materials.new(n);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;m.diffuse_color=(*c,1);return m
def texture(n,kind='metal'):
 size=1024;rng=np.random.default_rng(542433+(0 if kind=='wood' else 1));y,x=np.mgrid[:size,:size];grain=rng.random((size,size)).astype(np.float32)
 if kind=='wood':
  v=.5+.09*np.sin(y*.38+np.sin(x*.006)*3)+.055*np.sin(y*.10+np.sin(x*.003)*6)+.08*(grain-.5)
  rgb=np.stack((.20+.29*v,.09+.18*v,.035+.085*v),axis=2);rough=.48+.15*grain
 else:
  v=.5+.17*np.sin(x*.02)*np.sin(y*.03)+.06*(grain-.5);rgb=np.stack((.28+.31*v,.15+.27*v,.055+.16*v),axis=2);rough=.33+.19*grain
 height=.025*grain
 for _ in range(180):
  a,b=rng.integers(0,size,2);l=int(rng.integers(8,65));height[b:min(size,b+l),a]-=.045
 gy,gx=np.gradient(height);norm=np.stack((-gx*3,-gy*3,np.ones_like(grain)),axis=2);norm/=np.linalg.norm(norm,axis=2)[:,:,None];norm=norm*.5+.5
 m=mat(n,(.3,.18,.08),0 if kind=='wood' else .85,.5)
 for key,val in [('BaseColor',rgb),('Roughness',np.repeat(rough[:,:,None],3,axis=2)),('Normal',norm)]:
  rgba=np.ones((size,size,4),np.float32);rgba[:,:,:3]=np.clip(val,0,1);im=bpy.data.images.new(n+'_'+key,width=size,height=size,alpha=False);im.colorspace_settings.name='sRGB' if key=='BaseColor' else 'Non-Color';im.pixels.foreach_set(rgba.ravel());im.filepath_raw=str(OUT/'textures'/(n+'_'+key+'.png'));im.file_format='PNG';im.save();im.pack();t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=im
  p=m.node_tree.nodes.get('Principled BSDF')
  if key=='Normal':
   q=m.node_tree.nodes.new('ShaderNodeNormalMap');q.inputs['Strength'].default_value=.22;m.node_tree.links.new(t.outputs['Color'],q.inputs['Color']);m.node_tree.links.new(q.outputs['Normal'],p.inputs['Normal'])
  else:m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color' if key=='BaseColor' else 'Roughness'])
 return m
wood=texture('OriginalAshInterpretation','wood');brass=texture('OriginalGunmetalInterpretation')
iron=mat('PaintedIron_VisualEstimate',(.023,.072,.077),.55,.42);steel=mat('Steel_VisualEstimate',(.38,.43,.46),.9,.29)
rubber=mat('Rubber_VisualEstimate',(.017,.022,.024),0,.76);leather=mat('LeatherSeal_Interpretation',(.085,.032,.016),0,.78)
ivory=mat('UncalibratedDial',(.79,.75,.61),0,.67);ink=mat('DialInk',(.026,.035,.034),0,.69)
copper=mat('TinnedCistern_Interpretation',(.33,.40,.37),.8,.44);canvas=mat('Canvas_Interpretation',(.21,.20,.16),0,.92)
def finish(o,n,m,p):
 move(o);o.name=n;o.parent=p
 if m:o.data.materials.append(m)
 if o.type=='MESH':
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
  for f in o.data.polygons:f.use_smooth=True
  if not o.data.uv_layers:
   uv=o.data.uv_layers.new(name='UVMap')
   for face in o.data.polygons:
    normal=face.normal;axis=max(range(3),key=lambda i:abs(normal[i]));u,v=[i for i in range(3) if i!=axis]
    for k in face.loop_indices:
     co=o.data.vertices[o.data.loops[k].vertex_index].co;uv.data[k].uv=(co[u]*3,co[v]*3)
 o['evidence']='R01 visible/structural reference; exact external shape, finish and local dimensions estimated.';return o
def box(n,pos,size,m,p,bevel=.004):
 bpy.ops.mesh.primitive_cube_add();o=bpy.context.object;o.location=pos;o.scale=Vector(size)*.5;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  q=o.modifiers.new('Soft manufactured edges','BEVEL');q.width=bevel;q.segments=3;bpy.ops.object.modifier_apply(modifier=q.name)
 return finish(o,n,m,p)
def cyl(n,pos,r,length,m,p,axis='Z',verts=48):
 bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=length);o=bpy.context.object;o.location=pos
 if axis=='X':o.rotation_euler[1]=math.pi/2
 if axis=='Y':o.rotation_euler[0]=math.pi/2
 return finish(o,n,m,p)
def torus(n,pos,R,r,m,p,axis='Z'):
 bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=10,major_radius=R,minor_radius=r);o=bpy.context.object;o.location=pos
 if axis=='X':o.rotation_euler[1]=math.pi/2
 if axis=='Y':o.rotation_euler[0]=math.pi/2
 return finish(o,n,m,p)
def tube(n,a,b,r,m,p,verts=24):
 a,b=Vector(a),Vector(b);o=cyl(n,(a+b)*.5,r,(b-a).length,m,p,verts=verts);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def path(n,pts,r,m,p):
 curve=bpy.data.curves.new(n,'CURVE');curve.dimensions='3D';curve.bevel_depth=r;curve.bevel_resolution=3;curve.resolution_u=16;curve.use_fill_caps=True
 s=curve.splines.new('BEZIER');s.bezier_points.add(len(pts)-1)
 for q,v in zip(s.bezier_points,pts):q.co=v;q.handle_left_type=q.handle_right_type='AUTO'
 o=bpy.data.objects.new(n,curve);asset.objects.link(o);bpy.ops.object.select_all(action='DESELECT');bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o=bpy.context.object;return finish(o,n,m,p)
def text(n,s,pos,size,p,rot=(math.pi/2,0,0),m=ivory):
 bpy.ops.object.text_add();o=bpy.context.object;o.data.body=s;o.data.size=size;o.data.align_x='CENTER';o.data.extrude=.0004;o.location=pos;o.rotation_euler=rot;bpy.ops.object.convert(target='MESH');return finish(o,n,m,p)
def bolt(n,pos,p,axis='Z',r=.009):
 cyl(n+' washer',pos,r*1.3,.003,brass,p,axis,32);return cyl(n,pos,r,.009,steel,p,axis,6)
# Mark III pump: case based on Plates XII–XVI; removed front and open top are an exhibit choice.
pump=roots['Pump_ROOT'];fixed=node('PumpStructure',pump);case=node('PumpCase',pump);wheel=node('PumpCrankPivot',pump);wheel.location=(0,.08,.76)
for x in [-.27,.27]:
 box('Cast iron upright',(x,.09,.44),(.04,.055,.74),iron,fixed)
 box('Pump foot',(x,.075,.065),(.08,.28,.05),iron,fixed)
 for y in [-.025,.175]:bolt('Frame holding bolt',(x,y,.091),fixed)
for z in [.12,.62,.84]:box('Frame horizontal rail',(0,.09,z),(.56,.045,.035),iron,fixed)
box('Gunmetal bedplate',(0,.08,.18),(.45,.22,.035),brass,fixed)
box('Case bottom',(0,.03,.027),(.5,.4,.055),wood,case)
for x in [-.241,.241]:box('Ash case side',(x,.03,.395),(.018,.4,.74),wood,case)
box('Ash case back',(0,.222,.40),(.5,.016,.75),wood,case)
box('Case lower apron',(0,-.165,.11),(.5,.03,.12),wood,case)
for x in [-.246,.246]:
 for y in [-.164,.224]:
  box('Brass corner protector',(x,y,.40),(.025,.026,.73),brass,case,.001)
  for z in [.08,.27,.54,.73]:bolt('Corner screw',(x,y-.018,z),case,'Y',.005)
lid=node('PumpLidPivot',case);lid.location=(0,.228,.782);lid.rotation_euler.x=math.radians(-108)
box('Ash pump lid',(0,-.2,0),(.51,.40,.025),wood,lid)
for x in [-.22,.22]:box('Lid brass strap',(x,-.2,-.016),(.018,.385,.005),brass,lid,.001)
front=node('RemovedFrontPanel',case);front.location=(-.65,-.08,.35);front.rotation_euler=(math.radians(8),0,math.radians(-8))
box('Removable panel',(0,0,0),(.46,.024,.62),wood,front)
for x in [-.17,.17]:box('Panel brace',(x,-.017,0),(.023,.008,.58),brass,front,.001)
cistern=node('CoolingCistern',pump)
for x in [-.204,.204]:box('Cistern side',(x,.08,.32),(.007,.27,.29),copper,cistern,.001)
box('Cistern back',(0,.212,.32),(.414,.007,.29),copper,cistern,.001)
box('Cistern base',(0,.08,.174),(.414,.27,.007),copper,cistern,.001)
# Its front is shown removed for learning; not an authentic open operating cistern.
hiddenfront=box('CisternFront_RemovableTeachingSection',(0,-.052,.32),(.414,.007,.29),copper,cistern,.001)
hiddenfront.hide_render=True
shafts=[];sliders=[];sling=[];pistons=[]
for i,x in enumerate([-.12,.12]):
 chamber=node('PumpCylinder'+str(i),pump)
 # Closed annular cylinder wall, 107.95mm bore; this is not an external-diameter claim.
 N=64;vs=[];fs=[]
 for z in [.195,.465]:
  for r in [.065,.10795/2]:
   vs.extend([(x+r*math.cos(k*2*math.pi/N),.08+r*math.sin(k*2*math.pi/N),z) for k in range(N)])
 for k in range(N):
  a=k;b=(k+1)%N
  fs.extend([(a,b,128+b,128+a),(64+b,64+a,192+a,192+b),(b,a,64+a,64+b),(128+a,128+b,192+b,192+a)])
 me=bpy.data.meshes.new('Cylinder annulus');me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new('CylinderWall'+str(i),me);asset.objects.link(o);finish(o,o.name,brass,chamber)
 for z in [.198,.468]:
  cyl('Cylinder flange',(x,.08,z),.078,.012,brass,chamber)
  for a in range(8):bolt('Cylinder flange bolt',(x+.069*math.cos(a*math.pi/4),.08+.069*math.sin(a*math.pi/4),z+.009),chamber,r=.0045)
 cyl('Stuffing box',(x,.08,.485),.024,.028,brass,chamber)
 for y in [-.018,.18]:
  cyl('Valve casing',(x,y,.477),.016,.036,brass,chamber);cyl('Valve cap',(x,y,.498),.018,.009,steel,chamber,verts=6)
 cyl('Oil cup',(x+.04,.1,.51),.011,.028,brass,chamber)
 slider=node('PumpSlider'+str(i),pump);slider.location=(x,.08,.47);sliders.append(slider)
 cyl('Piston rod',(0,0,-.075),.008,.195,steel,slider);box('Crosshead',(0,0,0),(.055,.034,.025),brass,slider)
 piston=cyl('InterpretivePiston'+str(i),(0,0,-.16),.052,.021,leather,slider);pistons.append(piston)
 for z in [-.15,-.168]:torus('Leather piston lip',(0,0,z),.0505,.0014,leather,slider)
 phase=i*math.pi/2;pin=node('PumpCrankPin'+str(i),wheel);pin.location=(x,.092075*math.cos(phase),.092075*math.sin(phase));shafts.append(pin)
 cyl('Crank pin bearing',(0,0,0),.021,.040,brass,pin,'X')
 o=cyl('PumpSling'+str(i),(x,.08,.65),.012,.29,steel,pump);sling.append(o)
 tube('Cylinder delivery passage',(x,-.008,.455),(x,-.008,.148),.009,brass,fixed)
 box('Air reservoir',(x,.08,.137),(.16,.20,.06),brass,fixed)
 path('Gauge feed pipe'+str(i),[(x,.08,.16),(x,-.08,.30),(x,-.10,.54)],.006,brass,fixed)
 # Gauge faces have no historical pressure/depth calibration.
 cyl('Gauge bezel',(x,-.103,.576),.066,.024,brass,fixed,'Y');cyl('Gauge face',(x,-.119,.576),.058,.003,ivory,fixed,'Y')
 cover=node('GaugeCoverPivot'+str(i),pump);cover.location=(x,-.105,.51);cover.rotation_euler.x=math.radians(110)
 cyl('Hinged brass gauge cover',(0,0,.066),.064,.004,brass,cover,'Y');cyl('Gauge cover hinge',(0,0,0),.006,.033,brass,cover,'X')
 for a in range(36):
  t=math.radians(-130+a*260/35);r=.046
  ob=box('Dial tick',(x+r*math.sin(t),-.122,.576+r*math.cos(t)),(.0015,.0015,.006 if a%5 else .010),ink,fixed,.0002);ob.rotation_euler.y=t
 pointer=node('PumpGaugeNeedle'+str(i),pump);pointer.location=(x,-.125,.576)
 box('Uncalibrated pointer',(0,-.001,.017),(.002,.002,.041),ink,pointer,.0002);cyl('Pointer hub',(0,0,0),.004,.004,brass,pointer,'Y');text('DEMO gauge','DEMO',(x,-.126,.551),.011,fixed,m=ink)
for x in [-.34,.34]:
 cyl('Axle bearing',(x,0,0),.027,.08,brass,wheel,'X');torus('Flywheel rim',(x,0,0),.37,.022,iron,wheel,'X');torus('Flywheel rim outer edge',(x+(.009 if x>0 else -.009),0,0),.37,.003,brass,wheel,'X')
 cyl('Flywheel hub',(x,0,0),.045,.070,iron,wheel,'X')
 for j in range(8):
  t=j*math.pi/4;tube('Wrought iron spoke',(x,0,0),(x,.353*math.cos(t),.353*math.sin(t)),.009,iron,wheel)
  cyl('Spoke lug',(x,.345*math.cos(t),.345*math.sin(t)),.012,.033,iron,wheel,'X')
 hand=node('HandQuillPivot'+('L' if x<0 else 'R'),wheel);hand.location=(x+(-.13 if x<0 else .13),.34 if x<0 else -.34,0)
 cyl('Rotating hardwood quill',(0,0,0),.018,.19,wood,hand,'X')
 for q in [-.097,.097]:cyl('Quill ferrule',(q,0,0),.019,.009,brass,hand,'X')
cyl('Common crank shaft',(0,0,0),.015,.81,steel,wheel,'X')
outlet=node('Anchor_AirOutlet',pump);outlet.location=(-.12,-.112,.095);outlet['role']='Connect downstream hose; not actual rated fitting'
path('Air delivery nozzle',[(-.12,.08,.137),(-.12,-.06,.137),(-.12,-.112,.095)],.012,brass,fixed)
cyl('Illustrative oil separator',(-.12,-.155,.091),.028,.084,brass,fixed,'Y')
outlet.location=(-.12,-.204,.091);cyl('Air union',outlet.location,.019,.025,brass,fixed,'Y',6)
text('Pump plaque','MARK III / STUDY',(0,-.184,.167),.019,case)
# Surface telephone: six-cell battery box; attendant transmitter is separate, not box-mounted.
phone=roots['SurfaceTelephone_ROOT'];pcase=node('TelephoneCase',phone)
box('Ash battery box base',(0,0,.018),(.43,.27,.036),wood,pcase)
for x in [-.207,.207]:box('Battery box side',(x,0,.112),(.016,.27,.19),wood,pcase)
for y in [-.127,.127]:box('Battery box face',(0,y,.112),(.43,.016,.19),wood,pcase)
for x in [-.213,.213]:
 for y in [-.129,.129]:
  box('Box brass corner',(x,y,.11),(.015,.018,.19),brass,pcase,.001)
  for z in [.045,.175]:bolt('Box screw',(x,y-.012,z),pcase,'Y',.003)
phoneLid=node('TelephoneLidPivot',phone);phoneLid.location=(0,.134,.21);phoneLid.rotation_euler.x=math.radians(-112)
box('Telephone lid',(0,-.135,0),(.434,.274,.021),wood,phoneLid)
for x in [-.19,.19]:box('Lid binding',(x,-.135,-.013),(.016,.26,.004),brass,phoneLid,.001)
for x in [-.13,.13]:cyl('Lid hinge',(x,.132,.206),.008,.055,brass,phone,'X')
cells=node('SixDryCells_Interpretive',phone);cells['source']='R01 printed41: six cells. Exact cell format and wiring layout estimated.'
for x in [-.125,-.048,.029]:
 for y in [-.065,.065]:
  cyl('Dry cell', (x,y,.095),.032,.145,rubber,cells);cyl('Cell end cap',(x,y,.17),.032,.007,steel,cells);cyl('Cell positive stud',(x,y,.181),.006,.010,brass,cells)
  box('Original battery label',(x,y-.032,.1),(.034,.001,.065),canvas,cells,.0002)
box('Storage compartment divider',(.09,0,.105),(.009,.246,.18),wood,phone)
for x in [-.12,.12]:
 cyl('Cable receptacle',(x,-.151,.10),.018,.035,brass,phone,'Y',6);torus('Connector knurl',(x,-.17,.10),.015,.0018,steel,phone,'Y')
text('Outlet identifiers','ATTENDANT      DIVER',(0,-.138,.15),.013,phone)
switch=node('TelephoneSwitchPivot',phone);switch.location=(0,-.146,.069)
cyl('Switch gland',(0,0,0),.013,.017,brass,switch,'Y');tube('DPST switch lever',(0,-.006,0),(0,-.028,.020),.004,steel,switch)
text('Telephone plaque','WIRED TELEPHONE / STUDY',(0,-.141,.191),.012,phone)
anchor=node('Anchor_TelephoneOutlet',phone);anchor.location=(.12,-.179,.10)
surfaceMic=node('SurfaceTransmitter',phone);surfaceMic.location=(-.30,-.02,.23)
cyl('Attendant transmitter body',(0,0,0),.047,.041,rubber,surfaceMic,'Y');torus('Transmitter retaining ring',(0,-.024,0),.041,.005,brass,surfaceMic,'Y')
diaphragm=cyl('SurfaceDiaphragm',(0,-.029,0),.036,.003,canvas,surfaceMic,'Y')
for i in range(-3,4):
 for j in range(-3,4):
  if i*i+j*j<11:cyl('Transmitter grille aperture',(i*.009,-.032,j*.009),.0018,.002,ink,surfaceMic,'Y',12)
path('Attendant transmitter cord',[(-.30,.01,.20),(-.32,.03,.07),(-.18,-.19,.06),(-.12,-.19,.10)],.004,rubber,phone)
headset=node('AttendantReceiver',phone);headset.location=(-.30,.03,.39)
pts=[(.095*math.cos(i*math.pi/24),.01,.11*math.sin(i*math.pi/24)) for i in range(25)]
path('Spring headband',pts,.003,steel,headset)
cyl('Single attendant receiver',(-.095,.01,0),.034,.034,rubber,headset,'X');torus('Receiver cushion',(-.12,.01,0),.028,.006,leather,headset,'X')
path('Receiver cord',[(-.30-.095,.04,.38),(-.41,-.1,.10),(-.22,-.18,.035),(-.12,-.18,.10)],.003,rubber,phone)
# Diver telephone: interpretive separable headset and wall transmitter for close observation.
diver=roots['DiverTelephone_ROOT'];cap=node('DiverCanvasCap',diver)
bpy.ops.mesh.primitive_uv_sphere_add(segments=40,ring_count=24,radius=1);ob=bpy.context.object;ob.location=(0,0,.12);ob.scale=(.105,.09,.125);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);finish(ob,'Canvas cap form_Interpretive',canvas,cap)
for x in [-.108,.108]:
 cyl('Diver receiver',(x,0,.09),.036,.027,rubber,diver,'X');torus('Diver earpiece',(x+(-.013 if x<0 else .013),0,.09),.028,.006,leather,diver,'X')
path('Chin strap',[(-.105,0,.07),(-.08,-.04,-.05),(0,-.045,-.085),(.08,-.04,-.05),(.105,0,.07)],.005,leather,diver)
path('Headset wire',[(-.10,.02,.09),(-.07,.03,.26),(.07,.03,.26),(.10,.02,.09)],.003,rubber,diver)
mic=node('DiverTransmitter',diver);mic.location=(.16,-.01,.13)
box('Wall transmitter mounting plate',(0,.014,0),(.078,.012,.085),brass,mic,.005)
cyl('Diver transmitter cup',(0,-.007,0),.036,.03,brass,mic,'Y');cyl('DiverDiaphragm',(0,-.027,0),.031,.003,rubber,mic,'Y')
for k in range(-2,3):box('Microphone protection grille',(k*.009,-.033,0),(.002,.004,.044),steel,mic,.0004)
da=node('Anchor_DiverTelephone',diver);da.location=(.16,.012,.13)
path('Diver wiring_LayoutEstimated',[(.16,.02,.13),(.16,.10,.2),(0,.11,.21),(-.11,.10,.09)],.003,rubber,diver)
# Named baked mechanical loops: wheel (2s/one turn), phase-correct sliders and slings.
scene.frame_start=1;scene.frame_end=61
def pose(t):
 wheel.rotation_euler.x=t
 for i in range(2):
  angle=t+i*math.pi/2;y=.08+.092075*math.cos(angle);z=.76+.092075*math.sin(angle)
  end=Vector(([-.12,.12][i],.08,z-math.sqrt(.29**2-(y-.08)**2)));start=Vector(([-.12,.12][i],y,z))
  sliders[i].location=end;sling[i].location=(end+start)*.5;sling[i].rotation_euler=(start-end).to_track_quat('Z','Y').to_euler()
 for side in ['L','R']:bpy.data.objects['HandQuillPivot'+side].rotation_euler.x=-t
 for i in range(2):bpy.data.objects['PumpGaugeNeedle'+str(i)].rotation_euler.y=.20+.035*math.sin(t+i*math.pi/2)
for f in range(1,62):
 pose((f-1)/60*2*math.pi)
 for o in [wheel,*sliders,*sling,bpy.data.objects['HandQuillPivotL'],bpy.data.objects['HandQuillPivotR'],bpy.data.objects['PumpGaugeNeedle0'],bpy.data.objects['PumpGaugeNeedle1']]:
  o.keyframe_insert(data_path='location',frame=f);o.keyframe_insert(data_path='rotation_euler',frame=f)
 for name,home in [('SurfaceDiaphragm',-.029),('DiverDiaphragm',-.027)]:
  d=bpy.data.objects[name];d.location.y=home+.0008*math.sin((f-1)/60*2*math.pi*8);d.keyframe_insert(data_path='location',frame=f)
for o in list(asset.objects):
 if o.animation_data and o.animation_data.action:
  a=o.animation_data.action;a.name=o.name+'_MechanicalLoop'
  for fc in a.fcurves:
   for k in fc.keyframe_points:k.interpolation='LINEAR'
scene.frame_set(1)
def select_family(root):
 bpy.ops.object.select_all(action='DESELECT');family=[root,*root.children_recursive]
 for o in family:o.select_set(True)
 bpy.context.view_layer.objects.active=root;return family
def export(root,name):
 select_family(root)
 bpy.ops.export_scene.gltf(filepath=str(OUT/'exports'/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_animations=True,export_animation_mode='SCENE',export_frame_range=True,export_force_sampling=True,export_extras=True)
 bpy.ops.export_scene.fbx(filepath=str(OUT/'exports'/(name+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True)
for k,n in [('Pump_ROOT','mark-iii-pump'),('SurfaceTelephone_ROOT','surface-telephone'),('DiverTelephone_ROOT','diver-telephone')]:export(roots[k],n)
# Studio renders are not exported. Originals have local coordinates suitable for independent placement.
for k,loc in [('Pump_ROOT',(-.48,0,0)),('SurfaceTelephone_ROOT',(.48,-.02,.26)),('DiverTelephone_ROOT',(.78,.22,.65))]:roots[k].location=loc
slate=mat('StudioSlate',(.013,.023,.033),.15,.46)
ground=box('Studio floor',(0,0,-.026),(200,200,.03),slate,None,.001);move(ground,studio)
for n,pos,size in [('Phone presentation block',(.47,0,.115),(.58,.4,.23)),('Diver presentation block',(.76,.25,.48),(.32,.27,.34))]:move(box(n,pos,size,slate,None),studio)
scene.world=bpy.data.worlds.new('Review world');scene.world.color=(.15,.15,.15);scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.render.resolution_x=1440;scene.render.resolution_y=1080;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
def light(n,pos,energy,size,color):
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name=n;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;o.data.color=color;o.rotation_euler=(Vector((0,0,.5))-o.location).to_track_quat('-Z','Y').to_euler();move(o,studio)
light('Large softbox',(-2,-3,4),650,3,(.82,.92,1));light('Warm rim',(2,1,3),850,2,(1,.73,.42));light('Fill',(2,-2,1.5),260,2,(.57,.85,1))
bpy.ops.object.camera_add();cam=bpy.context.object;move(cam,studio);scene.camera=cam;cam.data.lens=48
views=[('01-equipment-overview', (2,-3,1.8),(0,0,.55)),('02-pump-detail',(.85,-1.6,1.15),(-.48,.0,.57)),('03-telephone-detail',(1.1,-1.25,1.0),(.46,0,.40)),('04-pump-rear',(-1.7,2.5,1.5),(-.48,0,.55))]
if '--quick' in args:views=views[:1];scene.cycles.samples=12;scene.render.resolution_percentage=70
for n,pos,target in views:
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/'previews'/(n+'.png'));bpy.ops.render.render(write_still=True)
# Save editable source and independent parts with packed images. Source retains studio pose, per-part libraries localise root.
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'sources/equipment.blend'))
for name,root in roots.items():
 prev=root.location.copy();root.location=(0,0,0);bpy.data.libraries.write(str(OUT/'sources/parts'/(name+'.blend')),set([root,*root.children_recursive]),fake_user=True,path_remap='RELATIVE');root.location=prev
metrics=[]
for name,root in roots.items():
 meshes=[o for o in root.children_recursive if o.type=='MESH'];tri=sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in meshes);bad=[]
 for o in meshes:
  bm=bmesh.new();bm.from_mesh(o.data);non=sum(not e.is_manifold for e in bm.edges);zero=sum(f.calc_area()<1e-12 for f in bm.faces);bm.free()
  if non or zero:bad.append({'mesh':o.name,'nonmanifold_edges':non,'faces_below_1e12_m2':zero})
 metrics.append({'root':name,'mesh_count':len(meshes),'triangles':tri,'topology_notes':bad,'animated_nodes':sum(bool(o.animation_data) for o in root.children_recursive)})
(OUT/'qa/source-geometry.json').write_text(json.dumps({'blender':bpy.app.version_string,'facts_bore_m':.10795,'facts_stroke_m':.18415,'source_metrics':metrics,'source_sha256':hashlib.sha256((OUT/'sources/equipment.blend').read_bytes()).hexdigest()},indent=2),encoding='utf-8',newline='\n')
print('DELIVERY_METRICS',json.dumps(metrics))
