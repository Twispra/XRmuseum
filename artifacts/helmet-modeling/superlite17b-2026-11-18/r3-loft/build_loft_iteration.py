"""Build a new outer surface from 112 controls, then create real window/neck openings."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,bmesh
from mathutils import Vector

here=Path(__file__).resolve().parent;root=here.parents[3];sys.path.insert(0,str(here))
from loft_geometry import surface,catmull
ap=argparse.ArgumentParser();ap.add_argument('--sections',required=True);ap.add_argument('--output',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
source=root/'artifacts/helmet-modeling/superlite17b-2026-11-12/shell-rebuild-r3/backups/backup_r1_current.blend'
expected='a595dc7f9979d21c95419fcc80fb2619099afcaf2e9735e8676c47037d1aa894';assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
out=Path(a.output).resolve();assert not out.exists(),'Existing iteration protected'
record=json.loads(Path(a.sections).read_text(encoding='utf-8'));p=record['parameters'];controls,rings=surface(p,6,4)
bpy.ops.wm.open_mainfile(filepath=str(source));old=bpy.data.objects['Shell_Yellow_520-065_ESTIMATED'];material=old.data.materials[0];collection=old.users_collection[0]
# Old surface is not sampled, projected or deformed. Only material and object
# identity are reused in this independent Blender research file.
verts=[tuple(v) for ring in rings for v in ring];faces=[];N=len(rings);M=len(rings[0])
for k in range(N-1):
 for j in range(M):faces.append((k*M+j,k*M+(j+1)%M,(k+1)*M+(j+1)%M,(k+1)*M+j))
faces.append(tuple(range(M-1,-1,-1)))
# A true aperture in the independently tilted front plane; not a black cap.
aperture=[]
import math
for j in range(16):
 theta=j*math.tau/16;x=.0995*math.cos(theta);s=math.sin(theta)
 z=p['window_center_z']+(.061 if s>=0 else .055)*s
 if s<0:z+=.006*(1-abs(math.cos(theta)))
 y=p['y_sections'][-1]+p.get('front_plane_slope',0)*(z-p['window_center_z'])
 aperture.append([x,y,z])
aperture=catmull(aperture,4,closed=True);start=len(verts);verts.extend(tuple(v) for v in aperture)
for j in range(M):faces.append(((N-1)*M+j,(N-1)*M+(j+1)%M,start+(j+1)%M,start+j))
mesh=bpy.data.meshes.new('R3_NewSectionLoft_7x16');mesh.from_pydata(verts,[],faces);mesh.update()
bm=bmesh.new();bm.from_mesh(mesh)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=(0,0,p['skirt_z0']),plane_no=(0,-p['skirt_slope'],1),clear_inner=True)
if p.get('skirt_lip_width',0)>0:
    rim=[e for e in bm.edges if e.is_boundary and all(abs(v.co.z-p['skirt_z0']-p['skirt_slope']*v.co.y)<2e-5 for v in e.verts)]
    result=bmesh.ops.extrude_edge_only(bm,edges=rim)
    n=Vector((0,-p['skirt_slope'],1)).normalized()
    for v in result['geom']:
        if isinstance(v,bmesh.types.BMVert):
            radial=Vector((v.co.x,v.co.y-.025,0));radial-=n*radial.dot(n)
            if radial.length>1e-9:v.co+=radial.normalized()*p['skirt_lip_width']-n*.002
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-6)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
oldmesh=old.data;old.data=mesh;mesh.materials.append(material)
bpy.context.view_layer.objects.active=old;old.select_set(True)
solid=old.modifiers.new('New loft estimated composite wall','SOLIDIFY');solid.thickness=p.get('wall_model_m',.0024);solid.offset=-1;solid.use_even_offset=False
bpy.ops.object.modifier_apply(modifier=solid.name)
for face in old.data.polygons:face.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');old.select_set(True);bpy.context.view_layer.objects.active=old
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
old['reconstruction']='New S0-S6 x16 control point loft; no old shell geometry used.'
old['dimensions']='Visual image alignment only, all world dimensions estimated.'
out.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(out))
old.data.calc_loop_triangles()
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
out.with_suffix('.json').write_text(json.dumps({'backup_sha256':expected,'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'feature_iteration':record['feature_iteration'],'new_sections':controls.tolist(),'new_section_count':7,'points_per_section':16,'old_shell_shape_used':False,'new_shell_vertices':len(old.data.vertices),'new_shell_triangles':len(old.data.loop_triangles),'other_parts_refitted':[],'backup_unchanged':True,'limits':'Initial side-profile iteration, aperture and neck are real openings. Contact parts remain unmoved until separately recorded refitting; no complete assembly or image gate pass claimed.'},indent=2)+'\n',encoding='utf-8')
print('R3_NEW_LOFT_SAVED '+str(len(old.data.vertices))+' vertices / '+str(len(old.data.loop_triangles))+' triangles')
