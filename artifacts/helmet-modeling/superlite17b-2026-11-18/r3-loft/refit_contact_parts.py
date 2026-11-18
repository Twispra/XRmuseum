"""Step5 research fit on twelve explicit meshes; all other objects stay locked."""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

ALLOW=['Ring_PortChrome_560-070','Glass_Port_520-004','ViewPort_Seal_510-260',
 'Handle_Brass_560-014','Handle_FrontFoot','Handle_RearFoot','Bottom_O_Ring_510-446_ESTIMATED',
 'NeckClamp_505-055','NeckDam_Neoprene_510-533','Neck_Yoke_520-117','SideBlock_MountingFoot','RearWeight_560-005_Continuous_RESEARCH']
ap=argparse.ArgumentParser();ap.add_argument('--model',required=True);ap.add_argument('--sections',required=True);ap.add_argument('--output',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
src=Path(a.model).resolve();out=Path(a.output).resolve();assert not out.exists();sh=hashlib.sha256(src.read_bytes()).hexdigest();p=json.loads(Path(a.sections).read_text())['parameters']
bpy.ops.wm.open_mainfile(filepath=str(src));shell=bpy.data.objects['Shell_Yellow_520-065_ESTIMATED'];shell.data.calc_loop_triangles();tree=BVHTree.FromPolygons([shell.matrix_world@v.co for v in shell.data.vertices],[t.vertices[:] for t in shell.data.loop_triangles],all_triangles=True)
records=[]
def record(o,before,reason):
 after=[o.matrix_world@v.co for v in o.data.vertices];records.append({'object':o.name,'vertices':len(after),'max_vertex_displacement_model_m':max((q-v).length for v,q in zip(before,after)),'reason':reason})
# One rigid transform for the three window layers, without silently moving the
# 22 locked fastener/slot meshes. Their resulting mismatch is an explicit defect.
angle=-math.atan(p['front_plane_slope']);rotation=Matrix.Rotation(angle,4,'X');pivot=Vector((0,-.124,p['window_center_z']))
for name in ALLOW[:3]:
 o=bpy.data.objects[name];before=[o.matrix_world@v.co for v in o.data.vertices];inverse=o.matrix_world.inverted()
 for v,q in zip(o.data.vertices,before):v.co=inverse@(pivot+(rotation@(q-pivot)))
 record(o,before,'Same rigid window-layer tilt to confirmed image-direction target. Locked screw alignment remains incomplete.')
# New-contact displacement at each handle foot. The arch interpolates the two
# shifts; original fasteners are not automatically unlocked.
foot_moves={}
for name in ['Handle_FrontFoot','Handle_RearFoot']:
 o=bpy.data.objects[name];before=[o.matrix_world@v.co for v in o.data.vertices];center=sum(before,Vector())/len(before)
 hit,normal,_,_=tree.ray_cast(Vector((center.x,center.y,.6)),Vector((0,0,-1)),1.)
 delta=Vector((0,0,hit.z-center.z+.004)) if hit else Vector();foot_moves[name]=(center.y,delta)
 o.location+=delta;record(o,before,'Foot follows new outer crown; existing arch interpolates endpoint shifts.')
o=bpy.data.objects['Handle_Brass_560-014'];before=[o.matrix_world@v.co for v in o.data.vertices];inverse=o.matrix_world.inverted();yf,df=foot_moves['Handle_FrontFoot'];yr,dr=foot_moves['Handle_RearFoot']
for v,q in zip(o.data.vertices,before):
 t=min(1,max(0,(q.y-yf)/(yr-yf)));v.co=inverse@(q+df*(1-t)+dr*t)
record(o,before,'Arch endpoint interpolation, no global scale.')
# Shared lower-seat vertical displacement, blended down to zero at old latch/
# pin regions. This preserves those fixed-background interfaces as constraints,
# but does not establish a measured or watertight joint.
for name in ['Bottom_O_Ring_510-446_ESTIMATED','NeckClamp_505-055','NeckDam_Neoprene_510-533','Neck_Yoke_520-117']:
 o=bpy.data.objects[name];before=[o.matrix_world@v.co for v in o.data.vertices];inverse=o.matrix_world.inverted()
 for v,q in zip(o.data.vertices,before):
  oldlevel=.108+.48*q.y;newlevel=p['skirt_z0']+p['skirt_slope']*q.y
  w=max(0,min(1,(q.z-(oldlevel-.025))/.025))
  # Rear transverse pin and front latch regions stay as inherited background.
  w*=min(1,max(0,(abs(q.x)-.025)/.035)) if q.y>.10 or q.y<-.075 else 1.
  v.co=inverse@(q+Vector((0,0,(newlevel-oldlevel)*w)))
 record(o,before,'Upper contact band adapt only; lower opening/retained latch background not globally moved. Mechanical fit unverified.')
for name in ['SideBlock_MountingFoot','RearWeight_560-005_Continuous_RESEARCH']:
 o=bpy.data.objects[name];before=[o.matrix_world@v.co for v in o.data.vertices];inverse=o.matrix_world.inverted()
 for v,q in zip(o.data.vertices,before):
  if name.startswith('RearWeight'):
   mount=Vector((0,.145,.219));lock=math.exp(-((q-mount).length/.023)**2);radial=Vector((q.x,q.y-.015,0));radial.normalize()
   hit,normal,_,_=tree.ray_cast(Vector((0,.015,q.z))+radial*.5,-radial,1.)
   if hit:
    target=hit+radial*.004;v.co=inverse@q.lerp(target,1-lock)
  else:
   # Preserve valve-side face; adapt only the inner shell-side face.
   w=min(1,max(0,(q.x+.1249)/.019));hit,normal,_,_=tree.ray_cast(Vector((-.5,q.y,q.z)),Vector((1,0,0)),1.)
   if hit:v.co=inverse@q.lerp(hit+Vector((-.002,0,0)),w)
 record(o,before,'Contact-surface adaptation to new loft; mount/valve-side anchors protected. Not physical installation dimensions.')
out.parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(out));assert hashlib.sha256(src.read_bytes()).hexdigest()==sh
out.with_suffix('.json').write_text(json.dumps({'input_sha256':sh,'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'unlocked_actual_meshes':ALLOW,'locked_other_mesh_count':133,'changes':records,'incomplete_interfaces':['22 locked window screws/slots not refitted to tilted frame','3 locked handle fasteners not refitted','Neck joint mechanical continuity/watertightness unproved','Inherited rear tab/pin gap remains'],'limits':'Independent research assembly. No gate pass from contact refitting alone; geometry/intersections and image metrics must be checked.'},indent=2)+'\n',encoding='utf-8')
print('R3_CONTACT_REFIT_SAVED explicit12 mesh whitelist;133 locked meshes')
