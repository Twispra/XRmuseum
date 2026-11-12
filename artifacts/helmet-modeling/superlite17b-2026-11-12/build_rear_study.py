"""Incremental rear connection study from frozen v4, never a prototype rebuild.

All new dimensions/placement are explicit modelling estimates. The study shows
documented part relationships, not manufacturer CAD, measured proportions or
an installation/maintenance procedure.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ap=argparse.ArgumentParser();ap.add_argument('--output',required=True)
args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
HERE=Path(__file__).resolve().parent
SOURCE=HERE/'baseline-v4/superlite17b-shell-v4.blend'
EXPECTED='6e85f7f048b3a6b8706428854ab8934099da25ff21d427e87fde9de930b1c8a2'
OUTPUT=Path(args.output).resolve()
assert not OUTPUT.exists(), 'Choose a new output; existing studies are protected.'
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==EXPECTED
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene=bpy.context.scene
baseline=json.loads((SOURCE.parent/'readback.json').read_text())
original_names={r['name'] for r in baseline['objects']}
collection=bpy.data.collections.new('Rear_Connection_Study_R1_ESTIMATED')
scene.collection.children.link(collection)
shell=bpy.data.objects['Shell_Yellow_520-065_ESTIMATED']
vertices=[shell.matrix_world@v.co for v in shell.data.vertices]
shell.data.calc_loop_triangles()
bvh=BVHTree.FromPolygons(vertices,[t.vertices[:] for t in shell.data.loop_triangles],all_triangles=True)

def finish(obj,name,material):
    obj.name=name
    for c in list(obj.users_collection):c.objects.unlink(obj)
    collection.objects.link(obj)
    obj.data.materials.clear();obj.data.materials.append(material)
    obj['evidence_scope']='Documented part relationship; all new dimensions and installation coordinates ESTIMATED.'
    return obj

def normal_mesh(name,verts,faces,material):
    m=bpy.data.meshes.new(name+'_Mesh');m.from_pydata(verts,[],faces);m.update()
    bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
    o=bpy.data.objects.new(name,m);collection.objects.link(o);o.data.materials.append(material)
    o['evidence_scope']='Research geometry; not measured dimensions.'
    return o

def cylinder(name,center,radius,depth,material,segments=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments,radius=radius,depth=depth,location=center)
    o=bpy.context.object;o.rotation_euler[0]=math.pi/2
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    return finish(o,name,material)

negative=bpy.data.objects['Weights_Brass_Rear_560-005_-1']
positive=bpy.data.objects['Weights_Brass_Rear_560-005_1']
brass=negative.data.materials[0];metal=bpy.data.objects['Hinge_Pin'].data.materials[0]
old_weights=[negative,positive]
# A continuous rear band is fitted to the existing shell, rather than stretching
# photo pixels into geometry. Height profiles come from v4's existing side/rear
# envelope and remain ESTIMATED; this pass does not claim a better photo fit.
allverts=[];allfaces=[]
N=72;J=24
for layer,offset in enumerate([.0055,.002]):
    for i in range(N+1):
        angle=math.radians(-65+130*i/N)
        direction=Vector((math.sin(angle),math.cos(angle),0))
        rear_fraction=math.cos(angle)
        # Raise only the study weight's lower envelope to leave the preserved
        # v4 neck dam/yoke clear. This is an explicit clearance estimate, not a
        # recovered physical plate edge from the ambiguous photo match.
        lower=.158+.040*rear_fraction
        upper=.207+.045*rear_fraction
        for j in range(J+1):
            z=lower+(upper-lower)*j/J
            center=Vector((0,.015,z));hit,normal,face,d=bvh.ray_cast(center+direction*.5,-direction,1.)
            assert hit is not None
            if normal.dot(direction)<0:normal=-normal
            # Maintain a monotone radial loft. Surface-normal offsets near the
            # retained v4 lower-rim lobes can fold even when every vertex is
            # individually outside the shell.
            allverts.append(tuple(hit+direction*offset))
stride=J+1;layer_size=(N+1)*stride
for layer in range(2):
    for i in range(N):
        for j in range(J):
            k=layer*layer_size+i*stride+j
            allfaces.append((k,k+stride,k+stride+1,k+1))
for i in range(N):
    k=i*stride
    allfaces.append((k,k+layer_size,k+stride+layer_size,k+stride))
    k=i*stride+J
    allfaces.append((k,k+stride,k+stride+layer_size,k+layer_size))
for j in range(J):
    k=j;allfaces.append((k,k+1,k+1+layer_size,k+layer_size))
    k=N*stride+j;allfaces.append((k,k+layer_size,k+1+layer_size,k+1))
weight=normal_mesh('RearWeight_560-005_Continuous_RESEARCH',allverts,allfaces,brass)
bm=bmesh.new();bm.from_mesh(weight.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(weight.data);bm.free()
for o in old_weights:bpy.data.objects.remove(o,do_unlink=True)

# Mount position is tied to this model's rear centre surface, not a recovered
# real-object installation coordinate. A through-hole in the new weight gives
# the sleeve/screw a visible connection rather than an unrelated floating block.
mount_z=.2187
hit,normal,face,d=bvh.ray_cast(Vector((0,.5,mount_z)),Vector((0,-1,0)),1.)
base_y=hit.y+.0055
hole_radius=.0031
cutter=cylinder('Temporary_weight_mount_hole',(0,base_y,mount_z),hole_radius,.05,metal)
mod=weight.modifiers.new('Estimated mount hole','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
bpy.context.view_layer.objects.active=weight;bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.data.objects.remove(cutter,do_unlink=True)
weight.data.calc_loop_triangles()
weight_bvh=BVHTree.FromPolygons([weight.matrix_world@v.co for v in weight.data.vertices],[t.vertices[:] for t in weight.data.loop_triangles],all_triangles=True)
seat_samples=[]
for radius in [.0033,.0047]:
    for i in range(64):
        angle=i*math.tau/64;x=radius*math.cos(angle);z=mount_z+radius*math.sin(angle)
        seat_hit,_,_,_=weight_bvh.ray_cast(Vector((x,.5,z)),Vector((0,-1,0)),1.)
        assert seat_hit is not None
        seat_samples.append(seat_hit.y)
original_mount_surface_y=base_y
base_y=max(seat_samples)

# 550-339 is represented as a hollow shouldered sleeve, with an outer flange.
# Axial direction +Y is this model's posterior direction only.
profile=[(.00315,base_y+.0004),(.0047,base_y+.0004),(.0047,base_y+.024),
         (.0083,base_y+.024),(.0083,base_y+.026),(.00315,base_y+.026)]
verts=[];faces=[];segs=64
for radius,y in profile:
    verts.extend((radius*math.cos(i*math.tau/segs),y,mount_z+radius*math.sin(i*math.tau/segs)) for i in range(segs))
for j in range(len(profile)):
    nxt=(j+1)%len(profile)
    for i in range(segs):faces.append((j*segs+i,j*segs+(i+1)%segs,nxt*segs+(i+1)%segs,nxt*segs+i))
sleeve=normal_mesh('AlignmentSleeve_550-339_FLANGED_ESTIMATED',verts,faces,metal)

# Single-piece plate with an upper sleeve opening and lower mounting region.
# Preserve the v4 transverse pin as a separate connection background.
tab_y=base_y+.0205;tab_thickness=.0022
outline=[(-.017,.1775),(.017,.1775),(.017,.187),(.012,.197)]
outline.extend((.0115*math.cos(t),mount_z+.0115*math.sin(t)) for t in [i*math.pi/24 for i in range(25)])
outline.extend([(-.012,.197),(-.017,.187)])
verts=[(x,tab_y+s*tab_thickness/2,z) for s in [-1,1] for x,z in outline]
n=len(outline);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]
faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
tab=normal_mesh('RearHingeTab_540-157_SINGLE_PIECE_ESTIMATED',verts,faces,metal)
for idx,(x,z,r) in enumerate([(0,mount_z,.0055),(-.009,.184,.002),(.009,.184,.002)]):
    cutter=cylinder('Temporary_tab_hole_'+str(idx),(x,tab_y,z),r,.04,metal)
    mod=tab.modifiers.new('Estimated opening','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
    bpy.context.view_layer.objects.active=tab;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)
old_block=bpy.data.objects.get('Hinge_NeckClamp_Rear')
bpy.data.objects.remove(old_block,do_unlink=True)
screw=cylinder('AlignmentScrew_530-090_VISUAL_ESTIMATED',(0,base_y+.0125,mount_z),.0027,.030,metal,segments=32)
head=cylinder('AlignmentScrew_530-090_Head_ESTIMATED',(0,base_y+.028,mount_z),.0048,.003,metal,segments=32)

for obj in [weight,sleeve,tab,screw,head]:
    obj['dimensions_status']='ESTIMATED, not factory measurement'
    obj['reference_docs']='260319004 17BNK-5/6/7; 241108001 locations4/5/14; Bulletin06/2006'
    for p in obj.data.polygons:p.use_smooth=(obj==weight)
    # Independent editable UVs for the new/replaced research pieces. Existing
    # 141 parts and their UV layers are untouched.
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.02)
    bpy.ops.object.mode_set(mode='OBJECT')

OUTPUT.parent.mkdir(parents=True,exist_ok=True)
scene['rear_study_r1']='Independent structural study; photo-driven shell deformation stopped; new dimensions estimated.'
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT))
changed_removed=sorted(original_names-{o.name for o in scene.objects})
assert changed_removed==sorted(['Weights_Brass_Rear_560-005_-1','Weights_Brass_Rear_560-005_1','Hinge_NeckClamp_Rear'])
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==EXPECTED
report={'baseline_sha256':EXPECTED,'output_sha256':hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
        'removed_original_objects':changed_removed,'added_objects':[o.name for o in [weight,sleeve,tab,screw,head]],
        'preserved_original_names':len(original_names)-len(changed_removed),'estimated_parameters_m':{'mount_z':mount_z,'mount_base_y':base_y,'original_center_surface_y':original_mount_surface_y,'seat_sampled_weight_max_y':max(seat_samples),'weight_outer_offset':.0055,'weight_inner_offset':.002,'band_angle_degrees':[-65,65],'band_height_estimate_trimmed_for_preserved_component_clearance':True,'sleeve_body_radius':.0047,'sleeve_flange_radius':.0083,'sleeve_hole_radius':.00315,'tab_thickness':tab_thickness,'tab_hole_radius':.0055,'sleeve_body_extent_from_base_y':[.0004,.024],'sleeve_flange_extent_from_base_y':[.024,.026],'tab_center_y_from_base':.0205,'sleeve_seat_clearance_estimated':.0004,'tab_lower_pin_interface':'Not aligned or mechanically completed; independent structure study.'},
        'photo_metrics_not_optimized':True,'shell_unchanged':True,'limits':'Interpretive connection study only. No factory dimensions, real load/seal validation, final photogrammetry, Unity export or device result.'}
OUTPUT.with_suffix('.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('REAR_STRUCTURE_STUDY_SAVED; original v4 unchanged; 141 original parts retained')
