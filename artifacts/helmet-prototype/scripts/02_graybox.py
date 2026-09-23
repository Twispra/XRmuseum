import math
from mathutils import Vector

# The entire envelope is calibrated after construction; local dimensions here are metres.
outer=[(.116,.137),(.119,.154),(.132,.184),(.146,.225),(.151,.280),(.150,.326),(.140,.383),(.115,.433),(.076,.468),(.028,.489),(.001,.493)]
inner=[(max(.001,r-.004),z-.003) for r,z in reversed(outer)]
shell=lathe('GB_Bonnet_Hollow',outer+inner,n=96,coll=gray_coll,mat=gray,parent=gray_root)
for name,c,norm,r in [('Front',(0,-.143,.300),(0,-1,0),.078),('Left',(-.141,0,.300),(-1,0,0),.060),('Right',(.141,0,.300),(1,0,0),.060),('Top',(0,-.047,.447),(0,-.38,.925),.040)]:
    cut=cylinder('TEMP_Cutter',r,.105,c,norm,coll=gray_coll,n=64,edge=0)
    boolean_cut(shell,cut)
    ri=r;ro=r+.014
    ring('GB_'+name+'_PortRim',ro,ri,.012,c,norm,gray_trim,gray_coll,gray_root,n=64)
    cylinder('GB_'+name+'_GlassPlaceholder',ri,.003,Vector(c)+Vector(norm)*.006,norm,gray_dark,gray_coll,gray_root,n=64,edge=0)
bevel(shell,.0008,2)

# Curved shoulder/breast plate with an actual neck opening.
N=96;verts=[]
for layer in range(2):
    for j in range(9):
        t=j/8
        for i in range(N):
            a=2*math.pi*i/N
            inner_r=.110
            rx=(1-t)*inner_r+t*.165
            ry=(1-t)*inner_r+t*(.206 if math.sin(a)<0 else .179)
            z=.134*(1-t)+t*(.014+.043*(math.cos(a)**2))-.004*layer
            verts.append((rx*math.cos(a),ry*math.sin(a),z))
faces=[];L=N*9
for layer in range(2):
    for j in range(8):
        for i in range(N):
            a=layer*L+j*N+i;b=layer*L+j*N+(i+1)%N;c=b+N;d=a+N
            faces.append((a,b,c,d) if layer==0 else (d,c,b,a))
for j in (0,8):
    for i in range(N):
        a=j*N+i;b=j*N+(i+1)%N
        faces.append((a,a+L,b+L,b) if j==0 else (a,b,b+L,a+L))
breast=mesh('GB_Corselet_ShoulderPlate',verts,faces,gray_coll,gray,gray_root)
bevel(breast,.001,2)
ring('GB_NeckLockRing',.127,.107,.017,(0,0,.143),(0,0,1),gray_trim,gray_coll,gray_root,96,.001)
for i in range(12):
    a=2*math.pi*i/12
    cylinder('GB_NeckFastener_%02d'%i,.006,.010,(.120*math.cos(a),.120*math.sin(a),.157),mat=gray_dark,coll=gray_coll,parent=gray_root,n=12)

# Front diamond grille and provisional mechanical connectors.
for direction in (-1,1):
    d=Vector((1,0,direction)).normalized();perp=Vector((-direction,0,1)).normalized()
    for offset in (-.030,.030):
        span=math.sqrt(.076**2-offset**2)
        c=Vector((0,-.159,.300))+perp*offset
        rod('GB_FrontDiamondGuard',c-d*span,c+d*span,.003,gray_trim,gray_coll,gray_root)
box('GB_Hinge',(.097,-.151,.300),(.018,.026,.070),gray_dark,gray_coll,gray_root,.002)
rod('GB_FrontLatch',(-.092,-.158,.285),(-.112,-.171,.315),.005,gray_trim,gray_coll,gray_root)
rod('GB_AirConnector',( .06,.135,.35),(.06,.185,.37),.014,gray_dark,gray_coll,gray_root)
ring('GB_AirConnectorThread',.019,.010,.023,(.06,.182,.37),(0,1,.2),gray_trim,gray_coll,gray_root,48)
box('GB_InterpretiveLabel',(0,-.167,.088),(.088,.004,.030),gray_trim,gray_coll,gray_root,.003)
for i in range(48):
    def edge_point(a):
        return (.165*math.cos(a),(.206 if math.sin(a)<0 else .179)*math.sin(a),.014+.043*math.cos(a)**2+.003)
    rod('GB_CorseletRolledEdge',edge_point(i*2*math.pi/48),edge_point((i+1)*2*math.pi/48),.004,gray_trim,gray_coll,gray_root,10)
for x in (-.055,.055):
    ring('GB_WeightAttachmentEye',.012,.006,.004,(x,-.143,.104),(0,-.8,.6),gray_dark,gray_coll,gray_root,40,.001)

# Normalize to the one documented object envelope, not a universal Mark V specification.
bpy.context.view_layer.update()
objects=[o for o in gray_coll.objects if o.type=='MESH']
pts=[o.matrix_world@Vector(c) for o in objects for c in o.bound_box]
lo=Vector(tuple(min(p[k] for p in pts) for k in range(3)))
hi=Vector(tuple(max(p[k] for p in pts) for k in range(3)))
target=Vector((.3175,.4191,.4953))
factor=Vector(tuple(target[k]/(hi[k]-lo[k]) for k in range(3)))
for ob in objects:
    for v in ob.data.vertices:
        v.co.x*=factor.x;v.co.y*=factor.y;v.co.z=(v.co.z-lo.z)*factor.z
gray_root['envelope_m']=[.3175,.4191,.4953]
gray_root['dimension_basis']='Case Auctions lot 636: approx 12.5 in W x 16.5 in D x 19.5 in H; local geometry is interpretive.'
gray_root['status']='Greybox: proportion and interaction-scale study, not historical reconstruction.'
gray_root.location=(-.31,.025,.058)
focus((0,0,.26),1.5)
print('Greybox complete: nominal W317.5 x D419.1 x H495.3 mm, hollow bonnet, 4 apertures, separate parts.')
