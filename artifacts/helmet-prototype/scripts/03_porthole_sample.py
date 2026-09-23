import math, random
from mathutils import Vector
random.seed(5424)

# Cross-section sample: curved copper skin with a real opening, not a textured disk.
N=192;steps=12;verts=[]
for layer in range(2):
    for j in range(steps+1):
        t=j/steps
        for i in range(N):
            a=2*math.pi*i/N
            outer=.144/(abs(math.cos(a))**5+abs(math.sin(a))**5)**.2
            r=.082*(1-t)+outer*t
            x=r*math.cos(a);z=r*math.sin(a)
            y=.017 + (x*x+z*z)/.80 + layer*.0038
            verts.append((x,y,z))
L=N*(steps+1);faces=[]
for layer in range(2):
    for j in range(steps):
        for i in range(N):
            a=layer*L+j*N+i;b=layer*L+j*N+(i+1)%N;c=b+N;d=a+N
            faces.append((a,b,c,d) if layer==0 else (d,c,b,a))
for j in (0,steps):
    for i in range(N):
        a=j*N+i;b=j*N+(i+1)%N
        faces.append((a,a+L,b+L,b) if j==0 else (a,b,b+L,a+L))
panel=mesh('DETAIL_CurvedCopperSkin_3p8mm',verts,faces,detail_coll,copper,detail_root)
bevel(panel,.0006,3)

flange=lathe('DETAIL_FixedPortFlange',[(.078,-.015),(.095,-.015),(.107,-.010),(.111,-.003),(.111,.003),(.106,.006),(.103,.015),(.089,.018),(.078,.017)],(0,.006,0),(0,-1,0),192,detail_coll,brass,detail_root)
bevel(flange,.0007,3)
ring('DETAIL_InteriorPortLiner',.081,.075,.044,(0,.021,0),mat=dark_metal,coll=detail_coll,parent=detail_root,n=128)
torus('DETAIL_FlangeJointOxide',.108,.00075,(0,.009,0),mat=patina,coll=detail_coll,parent=detail_root)
ring('DETAIL_CompressedRubberSeal',.096,.076,.0028,(0,-.015,0),mat=rubber,coll=detail_coll,parent=detail_root,n=128,edge=.0003)

# Actual moving assembly; origin is on the hinge pin, preserving world positions.
hinge=empty('DETAIL_Window_HINGE_PIVOT',detail_coll,(.110,-.016,0),detail_root)
hinge['axis']='local Z';hinge['opening_range_deg']=[0,100]
hinge['operation_note']='Illustrative hinge assembly, dimensions estimated from photographic reference.'
moving=[]
door=lathe('DETAIL_MovingWindowCastFrame',[(.074,-.005),(.091,-.005),(.098,-.001),(.100,.005),(.097,.011),(.091,.014),(.074,.014)],(0,-.023,0),(0,-1,0),192,detail_coll,brass,detail_root)
bevel(door,.00085,4);moving.append(door)
moving.append(torus('DETAIL_PolishedBezelLip',.0753,.0011,(0,-.037,0),mat=brass_bright,coll=detail_coll,parent=detail_root,n=192))
moving.append(torus('DETAIL_OuterCastingHighlight',.096,.00075,(0,-.032,0),mat=brass_bright,coll=detail_coll,parent=detail_root,n=192))
moving.append(cylinder('DETAIL_OpticalGlass_4mm',.075,.004,(0,-.023,0),(0,-1,0),mat=glass,coll=detail_coll,parent=detail_root,n=192,edge=.00035))
moving.append(ring('DETAIL_GlassRetainer_Back',.079,.0718,.003,(0,-.018,0),mat=dark_metal,coll=detail_coll,parent=detail_root,n=128,edge=.0004))

# Four offset diagonal bars: central diamond, not an invented central cross.
for direction in (-1,1):
    d=Vector((1,0,direction)).normalized()
    perp=Vector((-direction,0,1)).normalized()
    for s in (-.031,.031):
        c=perp*s+Vector((0,-.043,0))
        h=math.sqrt(.0765**2-s*s)
        moving.append(rod('DETAIL_CastDiamondGuard',c-d*h,c+d*h,.0030,brass,detail_coll,detail_root,24))

# Countersunk slotted heads, washers and subtle machining lines.
def slotted_screw(name,x,z,y=-.038,rad=.0045):
    washer=ring(name+'_Seat',rad*1.36,rad*.73,.0012,(x,y+.002,z),mat=dark_metal,coll=detail_coll,parent=detail_root,n=48,edge=.00015)
    head=cylinder(name+'_Head',rad,.0032,(x,y,z),(0,-1,0),mat=brass_bright,coll=detail_coll,parent=detail_root,n=48,edge=0)
    slot=box('TEMP_Slot',(x,y-.0015,z),(rad*1.65,.0032,.00085),coll=detail_coll,edge=0)
    boolean_cut(head,slot)
    bevel(head,.0003,3)
    moving.extend([washer,head])
for i in range(4):
    a=math.pi/4+i*math.pi/2
    slotted_screw('DETAIL_FrameScrew_%02d'%i,.088*math.cos(a),.088*math.sin(a))
for r in (.083,.0842,.0854):
    moving.append(torus('DETAIL_ConcentricMachining',r,.000085,(0,-.03715,0),mat=dark_metal,coll=detail_coll,parent=detail_root,n=256,k=6))

# Alternating hinge knuckles with pin and end caps, on right as in front reference.
for z,span in [(-.027,.021),(.027,.021)]:
    cylinder('DETAIL_HingeFixedKnuckle',.0075,span,(.110,-.016,z),mat=brass,coll=detail_coll,parent=detail_root,n=48)
    box('DETAIL_HingeFixedLeaf',(.111,.000,z),(.022,.032,.018),brass,detail_coll,detail_root,.002)
knuckle=cylinder('DETAIL_HingeMovingKnuckle',.0073,.028,(.110,-.016,0),mat=brass,coll=detail_coll,parent=detail_root,n=48)
moving.append(knuckle)
moving.append(box('DETAIL_HingeMovingLeaf',(.097,-.022,0),(.027,.016,.023),brass,detail_coll,detail_root,.002))
cylinder('DETAIL_HingePin',.0034,.088,(.110,-.016,0),mat=dark_metal,coll=detail_coll,parent=detail_root,n=32)
for z in (-.046,.046):
    cylinder('DETAIL_HingePinCap',.0055,.004,(.110,-.016,z),mat=brass_bright,coll=detail_coll,parent=detail_root,n=32)

# Opposite-side locking thumb screw, restrained mechanical detail.
box('DETAIL_LatchFixedLug',(-.111,-.003,0),(.023,.023,.042),brass,detail_coll,detail_root,.003)
rod('DETAIL_LatchStem',(-.114,-.006,0),(-.114,-.043,0),.0048,dark_metal,detail_coll,detail_root,32)
cylinder('DETAIL_LatchThumbWheel',.014,.010,(-.114,-.047,0),(0,-1,0),brass,detail_coll,detail_root,64,.001)
torus('DETAIL_ThumbWheelEdge',.0125,.0012,(-.114,-.053,0),mat=brass_bright,coll=detail_coll,parent=detail_root,n=96)
for i in range(32):
    a=i*2*math.pi/32
    rod('DETAIL_ThumbWheelKnurl',(-.114+.0135*math.cos(a),-.043,.0135*math.sin(a)),(-.114+.0135*math.cos(a),-.050,.0135*math.sin(a)),.00038,brass_bright,detail_coll,detail_root,8)

# Skin fastening rivets; distribution is explicitly a prototype design assumption.
for i in range(12):
    a=(i+.5)*2*math.pi/12;r=.133
    x=r*math.cos(a);z=r*math.sin(a);y=.017+(x*x+z*z)/.8
    cylinder('DETAIL_SkinRivet_%02d'%i,.0034,.0028,(x,y-.001,z),(0,-1,0),copper,detail_coll,detail_root,32,.001)
    torus('DETAIL_RivetOxide_%02d'%i,.0036,.00032,(x,y-.0003,z),mat=patina,coll=detail_coll,parent=detail_root,n=40,k=6)

# Tiny, sparse geometric wear marks: original geometry, not a generated photograph.
for i in range(30):
    a=random.uniform(0,2*math.pi);r=random.uniform(.090,.094)
    c=Vector((r*math.cos(a),-.0374,r*math.sin(a)))
    tangent=Vector((-math.sin(a),0,math.cos(a)))
    moving.append(rod('DETAIL_EdgeWear',c,c+tangent*random.uniform(.0008,.003),.000045,brass_bright,detail_coll,detail_root,6))

# All moving geometry has its local origin translated into hinge coordinates.
for ob in moving:
    for v in ob.data.vertices: v.co-=hinge.location
    ob.parent=hinge
detail_root.location=(.29,-.035,.273)
detail_root['window_clear_aperture_m']=.148
detail_root['glass_thickness_m']=.004
detail_root['quality_status']='Blender material/geometry study; procedural materials are not yet baked for URP.'
detail_root['historical_status']='Mark V-inspired visual study. Skin rivets, dimensions and hinge/latch internals are interpretive.'
focus((0,0,.26),1.5)
print('Porthole sample complete: real aperture, glass, gasket, diamond guard, moving hinge, slotted screws and machined metal.')
