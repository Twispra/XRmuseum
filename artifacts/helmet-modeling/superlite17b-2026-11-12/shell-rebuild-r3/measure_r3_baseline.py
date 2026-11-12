"""Steps 1-3: freeze visible-mask metric and provisional image-space controls."""
import argparse,hashlib,json
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import binary_erosion,distance_transform_edt

def boundary(mask):return mask & ~binary_erosion(mask)
def compare(a,b,L):
    intersection=np.count_nonzero(a&b);union=np.count_nonzero(a|b)
    aa=boundary(a);bb=boundary(b)
    to_b=distance_transform_edt(~bb)[aa];to_a=distance_transform_edt(~aa)[bb]
    mean=(float(to_a.mean())+float(to_b.mean()))/2
    return {'iou':float(intersection/union) if union else 1.,'intersection_px':int(intersection),'union_px':int(union),
            'symmetric_mean_boundary_distance_px':mean,'symmetric_mean_boundary_distance_over_L':mean/L,
            'directional_boundary_means_px':{'reference_to_model':float(to_b.mean()),'model_to_reference':float(to_a.mean())},
            'boundary_definition':'All visible-yellow component boundaries, including hardware occlusion edges; no hidden completion.',
            'xor_over_union':float(np.count_nonzero(a^b)/union) if union else 0.}

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--reference-root',required=True);ap.add_argument('--local-previews',required=True);args=ap.parse_args()
    here=Path(__file__).resolve().parent;private=Path(args.local_previews);private.mkdir(parents=True,exist_ok=True)
    ref=np.asarray(Image.open(here/'reference-side-visible-mask.png').convert('L'))>127
    rgba=np.asarray(Image.open(here/'baseline/base_side_shell_visible.png').convert('RGBA'))
    model=(rgba[:,:,0]>220)&(rgba[:,:,1]>220)&(rgba[:,:,2]>220)&(rgba[:,:,3]>220)
    yy,xx=np.where(ref);x0,x1,y0,y1=int(xx.min()),int(xx.max()),int(yy.min()),int(yy.max());L=x1-x0
    assert L>0
    # Sanity checks define formula behaviour, not tests of implementation truth.
    identical=compare(ref,ref,L);assert identical['iou']==1 and identical['symmetric_mean_boundary_distance_px']==0
    metrics=compare(ref,model,L)
    Image.fromarray(model.astype(np.uint8)*255).save(here/'baseline/base_side_visible_binary_mask.png')
    annotations=json.loads((here/'annotations-side-r3.json').read_text(encoding='utf-8'))
    y_origin=max(y1,max(row['original_xy_px'][1] for row in annotations['keypoints_16']))
    keypoints=[]
    _,nearest=distance_transform_edt(~boundary(ref),return_indices=True)
    for row in annotations['keypoints_16']:
        rawx,rawy=row['original_xy_px'];y,x=[int(v) for v in nearest[:,rawy,rawx]]
        adjustment=float(np.hypot(x-rawx,y-rawy));allowance=max(row['uncertainty_radius_px'],adjustment+2)
        keypoints.append({**row,'initial_manual_xy_px':[rawx,rawy],'original_xy_px':[x,y],
            'mask_boundary_alignment_adjustment_px':adjustment,'alignment_status':'Proposed visible-mask boundary anchor, not automatic 3D correspondence; confirm visually.',
            'normalized_xy_up':[(x-x0)/L,(y_origin-y)/L],'uncertainty_radius_px':allowance,'uncertainty_radius_over_L':allowance/L})
    angle=annotations['projected_window_edge_direction']
    controls={'reference_image_sha256':annotations['image']['sha256'],'reference_mask_sha256':hashlib.sha256((here/'reference-side-visible-mask.png').read_bytes()).hexdigest(),
        'status':'PROVISIONAL; user confirmation required before Step 4',
        'normalization':{'L_px':L,'origin_px':[x0,y_origin],'visible_mask_bbox_xyxy':[x0,y0,x1,y1],
            'formula':'x=(u-x0)/L; y=(y_bottom-v)/L; x right, y up',
            'limitation':'L is the observed visible-yellow projected x-span, including small exposed yellow regions. Hidden full-shell fore-aft length is not observed. This normalization definition must be confirmed; not physical length.'},
        'keypoints_16':keypoints,'window_edge_projected_angle':angle,'closed_full_shell_profile_available':False,
        'do_not_close_across_occlusion':True,'section_positions_not_final_3d_planes':{'S1':500,'S2':640,'S3':800,'S4':960,'S5':1110},
        'sections_note':'Original user sketch image x positions retained for discussion only; converting image columns into 3D section planes needs the confirmed reconstruction camera and profile.'}
    (here/'confirmation-controls.json').write_text(json.dumps(controls,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    frozen=json.loads((here/'baseline/baseline-cameras-and-inventory.json').read_text(encoding='utf-8'))
    sidecam=frozen['fixed_views']['side']['configuration'];up=np.asarray(sidecam['camera_basis_right_up_back'][1]);right=np.asarray(sidecam['camera_basis_right_up_back'][0]);view=np.asarray(sidecam['camera_basis_right_up_back'][2]);p=np.asarray(sidecam['parameters']);f=sidecam['focal_px'];target=np.asarray(sidecam['target_world']);distance=np.exp(p[3])
    def project(q):
        q=np.asarray(q)-target;z=distance-q@view
        return np.array([p[4]+f*(q@right)/z,p[5]-f*(q@up)/z])
    # Saved frame is vertical in the old construction. Project a vertical line
    # lying in that frame for a descriptive baseline, not a recovered real plane.
    p0=project([0,-.13072,.13]);p1=project([0,-.13072,.26]);d=p0-p1
    apparent=float(np.degrees(np.arctan2(d[0],d[1])))
    report={'backup_sha256':frozen['backup_sha256'],'reference_mask_sha256':controls['reference_mask_sha256'],
        'metric_definition_frozen_before_reconstruction':True,'visible_yellow_baseline':metrics,'normalization':controls['normalization'],
        'requested_gates':{'side_iou_min':.93,'symmetric_mean_boundary_over_L_max':.015,'shell_silhouette_change_xor_over_union_min':.05,'window_projected_tilt_difference_deg_max':3.,'pixel_change_definition':'Full shell geometry projection with all accessories hidden; XOR/union on same pixel grid. Do not count material/light changes or accessory refitting toward the5% shell-shape gate.','mask_and_L_scope_status':'Provisional visible-mask reference and visible x-span; user confirmation required before treating as final acceptance definition.'},
        'backup_vs_unmodified_work_pixel_change':None,'byte_identical_work_inferred_change':0.,'work_rendered_as_new_version':False,'new_shell_built':False,'comparison_stage':'baseline only, not an iteration passed',
        'window_tilt':{'reference_local_edge_deg_from_image_vertical':angle['angle_from_image_vertical_deg'],'reference_conservative_uncertainty_deg':angle['conservative_angle_uncertainty_deg'],'backup_projected_vertical_line_in_frame_plane_deg':apparent,'numeric_difference_between_different_features_deg':abs(apparent-angle['angle_from_image_vertical_deg']),'comparable_plane_angle_error_deg':None,'true_reference_3d_plane_angle_deg':None,'limitation':'Curved short visible seat edge versus model in-plane vertical direction are not a uniquely identified real plane. ±5° observation uncertainty exceeds requested3° gate; user must confirm target definition before claiming a pass.'},
        'full_hidden_shell_iou':None,'front_photo_metrics':None,
        'limits':['Visible-mask IoU includes differences from accessories and occlusion, not only shell shape.','No hidden shell silhouette is invented from metal/strap edges.','No true front photograph supplied, so front view is diagnostic only.','Existing estimated camera retained; not a uniquely recovered camera.','Step4 reconstruction and Step5 refitting await explicit point/angle confirmation.']}
    (here/'baseline-metrics.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    occluder=(rgba[:,:,3]>220)&~model
    occluded_reference=int(np.count_nonzero(ref&occluder))
    report['current_occlusion_diagnostic']={'reference_yellow_behind_current_model_occluder_px':occluded_reference,
        'fraction_of_reference_yellow':occluded_reference/int(np.count_nonzero(ref)),
        'conditional_iou_ceiling':1-occluded_reference/int(np.count_nonzero(ref)),
        'conditions':'Only if current camera, current opaque accessory coverage and shell-behind-accessories order are all fixed. Not a bound after authorized contact-part refitting or different cameras. Not a reason to lower the user acceptance gate.'}
    (here/'baseline-metrics.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
    annotated=Image.new('RGB',ref.shape[::-1],'#f3f6f9');arr=np.array(annotated);arr[ref]=[245,208,77];arr[boundary(ref)]=[30,125,155];annotated=Image.fromarray(arr);draw=ImageDraw.Draw(annotated)
    clean=Image.open(Path(args.reference_root)/'04_side_rear_view.png').convert('RGB');private_image=clean.copy();pdraw=ImageDraw.Draw(private_image)
    for row in keypoints:
        x,y=row['original_xy_px']
        for dr in [draw,pdraw]:
            dr.ellipse((x-5,y-5,x+5,y+5),fill='#b0004b');dr.text((x+8,y-20),row['id'],font=font,fill='#9a0044')
    for x in controls['section_positions_not_final_3d_planes'].values():draw.line([(x,100),(x,790)],fill='#a86ec1',width=1)
    line=angle['original_xy_px'];draw.line([tuple(q) for q in line],fill='#0066dd',width=4);pdraw.line([tuple(q) for q in line],fill='#0066dd',width=4)
    annotated.save(here/'keypoints-confirmation.png');private_image.save(private/'reference-B-keypoints.png')
    overlay=clean.copy();pixels=np.array(overlay);pixels[boundary(ref)]=[0,170,200];pixels[boundary(model)]=[225,40,70];overlay=Image.fromarray(pixels);overlay.save(private/'baseline-reference-B-overlay.png')
    # No fake third-column model: work_r3 is still an identical backup copy.
    base=Image.open(here/'baseline/base_side.png').convert('RGB');base.thumbnail((800,600));clean.thumbnail((800,600))
    sheet=Image.new('RGB',(2430,680),'white');dr=ImageDraw.Draw(sheet)
    for x,label,img in [(10,'参考B（原始照片）',clean),(820,'备份版（固定相机）',base)]:dr.text((x,15),label,font=font,fill='black');sheet.paste(img,(x,65))
    dr.text((1630,15),'新版：等待16点/倾角确认',font=font,fill='black');dr.text((1630,95),'尚未重建，不用备份图冒充新版。',font=font,fill='black')
    sheet.save(private/'step03-side-three-columns.png')
    print(json.dumps({'iou':metrics['iou'],'boundary_mean_px':metrics['symmetric_mean_boundary_distance_px'],'boundary_mean_over_L':metrics['symmetric_mean_boundary_distance_over_L'],'L':L,'reference_angle':angle['angle_from_image_vertical_deg'],'backup_apparent_angle':apparent,'new_shell_built':False}))

if __name__=='__main__':main()
