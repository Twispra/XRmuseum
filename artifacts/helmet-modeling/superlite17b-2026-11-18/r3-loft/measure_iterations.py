"""Frozen user-confirmed gates, all actual mask iterations including failures."""
import argparse,json,math,sys
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import binary_erosion,distance_transform_edt
from loft_geometry import control_sections

def binary(path):
 r=np.asarray(Image.open(path).convert('RGBA'));return (r[:,:,0]>220)&(r[:,:,3]>220)
def edge(a):return a&~binary_erosion(a)
def metrics(a,b,L):
 u=np.count_nonzero(a|b);da=distance_transform_edt(~edge(b))[edge(a)];db=distance_transform_edt(~edge(a))[edge(b)]
 return float(np.count_nonzero(a&b)/u),float((da.mean()+db.mean())/2/L)
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--manifest',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 here=Path(__file__).resolve().parent;root=here.parents[3];old=root/'artifacts/helmet-modeling/superlite17b-2026-11-12/shell-rebuild-r3'
 entries=json.loads(Path(a.manifest).read_text());reference=np.asarray(Image.open(old/'reference-side-visible-mask.png').convert('L'))>127
 before=binary(old/'baseline/base_side_shell_full.png');camera=json.loads((old/'baseline/baseline-cameras-and-inventory.json').read_text())['fixed_views']['side']['configuration']
 sys.path.insert(0,str(root/'artifacts/helmet-modeling/superlite17b-2026-11-12/camera-baseline-r1'));from fit_projection import project
 output=[];prior=None
 for e in entries:
  path=Path(e['renders']);visible=binary(path/'side-visible.png');full=binary(path/'side-full.png');iou,dev=metrics(reference,visible,801);change=np.count_nonzero(before^full)/np.count_nonzero(before|full)
  s=json.loads(Path(e['sections']).read_text());p=s['parameters'];q=np.array([[0,p['y_sections'][-1]+p['front_plane_slope']*(z-p['window_center_z']),z] for z in [.14,.26]])
  xy=project(q,np.array(camera['parameters']),camera['focal_px']);v=xy[0]-xy[1];angle=math.degrees(math.atan2(v[0],v[1]));error=abs(angle-13.95)
  build_path=Path(e.get('shell_build_manifest',str(Path(e['actual_model']).with_suffix('.json'))))
  build_record=json.loads(build_path.read_text())
  change=float(change);current=np.asarray(build_record['new_sections']);moves=[]
  if prior is not None:
   delta=current-prior
   for k in range(7):
    indices=np.where(np.linalg.norm(delta[k],axis=1)>1e-9)[0].tolist()
    if indices:moves.append({'section':'S'+str(k),'control_point_ids_0_to15':indices,'max_control_displacement_model_m':float(np.linalg.norm(delta[k],axis=1).max())})
  else:moves=[{'section':'S0-S6','control_point_ids_0_to15':'all112 newly constructed, no old shell correspondence'}]
  frame_angle=angle if 'contact-refit' in e['id'] and 'rejected' not in e['id'] else 7.861751353250514
  frame_error=abs(frame_angle-13.95)
  output.append({**e,'visible_iou':iou,'symmetric_boundary_deviation_over_L':dev,'full_shell_geometry_xor_over_union_vs_backup':change,
     'new_window_plane_in_plane_vertical_projection_deg':angle,'image_direction_difference_to_confirmed_target_deg':error,
     'frame_plane_in_plane_vertical_projection_deg':frame_angle,'frame_direction_difference_to_confirmed_target_deg':frame_error,
     'frame_measurement_policy':'Frame unchanged in non-refit iterations; explicit rigid rotation only in rejected contact-refit trials. Shell front plane is not substituted for actual frame angle.',
     'gates':{'iou':iou>=.93,'boundary':dev<=.015,'shell_difference':change>=.05,'projected_frame_direction':frame_error<=3},
     'all_image_gates_passed':iou>=.93 and dev<=.015 and change>=.05 and frame_error<=3,'moved_new_controls_from_previous':moves,'control_points_source':'Actual saved build manifest, not later regenerated trial parameter arrays.',
     'limits':'Direction is a model-plane in-plane vector projected into the frozen virtual camera, not an independently observed physical plane. VisibleIoU includes accessory effects; no occluded shape is invented.'})
  prior=current
 out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=False)
 (out/'iterations.json').write_text(json.dumps({'reference_mask_sha256':'81bf0b3e6377a7e5e5f430f11040505347ef173568749b5ae70fd205453cb950','L_px':801,'frozen_camera':True,'original_user_thresholds_unchanged':True,'iterations':output},indent=2)+'\n',encoding='utf-8')
 print(json.dumps([{'iteration':e['id'],'iou':e['visible_iou'],'boundary_pct_L':e['symmetric_boundary_deviation_over_L']*100,'shape_difference_pct':e['full_shell_geometry_xor_over_union_vs_backup']*100,'direction_error':e['image_direction_difference_to_confirmed_target_deg'],'passed':e['all_image_gates_passed']} for e in output]),flush=True)
if __name__=='__main__':main()
