"""Read-only checks from Git blobs. Requires NumPy, Pillow and SciPy.

Use --repo <checkout> --commit <source SHA> --output <new JSON path>.
Committed source masks/annotations are reused; original-photo segmentation,
geometry, true 3D angles and device behavior are outside this check.
"""
import argparse, subprocess, json, io, hashlib
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import binary_erosion, distance_transform_edt, binary_dilation

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--repo', required=True, help='Git checkout used for exact-revision blob reads.')
parser.add_argument('--commit', required=True, help='Source revision to recompute.')
parser.add_argument('--output', required=True, help='New JSON report path; existing files are refused.')
args = parser.parse_args()
repo = Path(args.repo).resolve(strict=True)
sha = subprocess.check_output(['git', 'rev-parse', '--verify', args.commit+'^{commit}'], cwd=repo, text=True).strip()
out = Path(args.output).resolve()
if out.exists():
    raise FileExistsError('Refusing to overwrite evidence: '+str(out))
stem = 'artifacts/helmet-modeling/superlite17b-2026-11-12/'
def blob(p): return subprocess.check_output(['git','show',sha+':'+p],cwd=repo)
def js(p): return json.loads(blob(stem+p))
def img(p, mode): return np.array(Image.open(io.BytesIO(blob(stem+p))).convert(mode))

r3='shell-rebuild-r3/'
ref_data=blob(stem+r3+'reference-side-visible-mask.png')
ref=img(r3+'reference-side-visible-mask.png','L')>127
rgba=img(r3+'baseline/base_side_shell_visible.png','RGBA')
model=(rgba[:,:,:3]>220).all(axis=2)&(rgba[:,:,3]>220)
bref=ref&~binary_erosion(ref); bmod=model&~binary_erosion(model)
r2m=distance_transform_edt(~bmod)[bref]; m2r=distance_transform_edt(~bref)[bmod]
ys,xs=np.where(ref); L=int(xs.max()-xs.min()); inter=int((ref&model).sum()); union=int((ref|model).sum()); mean=float((r2m.mean()+m2r.mean())/2)
controls=js(r3+'confirmation-controls.json'); ann=js(r3+'annotations-side-r3.json'); metric=js(r3+'baseline-metrics.json')
points=[]
_,idx=distance_transform_edt(~bref,return_indices=True)
for a,c in zip(ann['keypoints_16'],controls['keypoints_16']):
 x,y=c['original_xy_px']; rx,ry=a['original_xy_px']; ny,nx=idx[:,ry,rx]
 points.append({'id':c['id'],'on_boundary':bool(bref[y,x]),'nearest_matches':int(nx)==x and int(ny)==y,'normalization_matches':bool(np.allclose(c['normalized_xy_up'],[(x-xs.min())/L,(786-y)/L])),'adjustment_matches':bool(np.isclose(c['mask_boundary_alignment_adjustment_px'],np.hypot(x-rx,y-ry)))})
angle=ann['projected_window_edge_direction']; pp=np.array(angle['original_xy_px']); a,b=np.linalg.lstsq(np.column_stack([pp[:,1],np.ones(len(pp))]),pp[:,0],rcond=None)[0]; rms=np.sqrt(np.mean((pp[:,0]-(a*pp[:,1]+b))**2))
occ=(rgba[:,:,3]>220)&~model; nocc=int((ref&occ).sum())

def sample(points):
 out=[]
 for a,b in zip(points[:-1],points[1:]):
  a=np.asarray(a,float);b=np.asarray(b,float)
  out.extend(a+(b-a)*t for t in np.linspace(0,1,max(1,int(np.ceil(np.linalg.norm(b-a)/6))),endpoint=False))
 return np.asarray(out+[points[-1]])

r2_result=[]
r2stored=js('shell-study-r2/diagnostics/rendered-boundary-change.json')
for key in ['front','side']:
 annotation=js('camera-baseline-r1/annotations-'+key+'.json')
 rows=[r for r in annotation['polylines'] if r['id']=='C05'] if key=='front' else [r for r in annotation['visible_contours'] if r['id'] in ['yellow_shell_crown_visible_silhouette','yellow_shell_front_shoulder_silhouette']]
 for version in ['before','after']:
  rgba2=img('shell-study-r2/renders/'+version+'-'+key+'-masks.png','RGBA');rgb=rgba2[:,:,:3]; alpha=rgba2[:,:,3]
  mask=(rgb[:,:,1]>210)&(rgb[:,:,0]<40)&(rgb[:,:,2]<40)&(alpha>220)
  boundary=mask&~binary_erosion(mask)&binary_dilation(alpha<128,iterations=3)
  dist=distance_transform_edt(~boundary)
  for row in rows:
   for pass_id in ['a','b']:
    pts=row['pass_1_px' if pass_id=='a' else 'pass_2_px'] if key=='front' else row['pass_'+pass_id+'_xy_px']
    q=np.rint(sample(pts)).astype(int); d=dist[q[:,1],q[:,0]]
    stored=next(r for r in r2stored['views'][key]['measurements'] if r['version']==version and r['curve']==row['id'] and r['pass']==pass_id)
    computed={'median_px':float(np.median(d)),'rms_px':float(np.sqrt(np.mean(d*d))),'p95_px':float(np.percentile(d,95)),'sample_count_not_independent_cases':len(d)}
    r2_result.append({'view':key,'version':version,'curve':row['id'],'pass':pass_id,'computed':computed,'matches_stored':all(np.isclose(value,stored[k]) for k,value in computed.items())})

summary={'head':sha,'reference_sha256':hashlib.sha256(ref_data).hexdigest(),'ref_count':int(ref.sum()),'model_count':int(model.sum()),'intersection':inter,'union':union,'iou':inter/union,'boundary_mean_px':mean,'L':L,'mean_over_L':mean/L,'directional_means':[float(r2m.mean()),float(m2r.mean())],'occluded_ref_px':nocc,'occluded_ref_fraction':nocc/int(ref.sum()),'conditional_iou_ceiling':1-nocc/int(ref.sum()),'all_points_valid':all(all(p[k] for k in ['on_boundary','nearest_matches','normalization_matches','adjustment_matches']) for p in points),'angle_fit':{'a':float(a),'b':float(b),'rms':float(rms),'angle_deg':float(np.degrees(np.arctan(a)))},'stored_angle':angle['angle_from_image_vertical_deg'],'report_matches':{'iou':bool(np.isclose(inter/union,metric['visible_yellow_baseline']['iou'])),'mean':bool(np.isclose(mean,metric['visible_yellow_baseline']['symmetric_mean_boundary_distance_px']))},'points':points,'r2_comparison':r2_result,'r2_all_matches':bool(all(r['matches_stored'] for r in r2_result)),'limits':['Masks are committed observations, not independently validated original-photo segmentation.','Computed projected angle is not recovered 3D window inclination.','Source geometry, annotations and historical reports are read-only; only the requested JSON output is written.']}
stored_r1=js('camera-baseline-r1/results/baseline-metrics.json')
r1_result=[]
palette={'shell':[0,255,0],'frame':[0,0,255],'glass':[128,128,128],'rear_weight':[255,0,0],'handle':[255,255,0],'neck':[0,255,255],'other':[13,13,13]}
for key in ['front','side']:
 ann1=js('camera-baseline-r1/annotations-'+key+'.json'); rgba1=img('camera-baseline-r1/results/'+key+'-coverage.png','RGBA'); rgb1=rgba1[:,:,:3]; alpha1=rgba1[:,:,3]
 masks={name:(np.linalg.norm(rgb1.astype(float)-np.array(color),axis=2)<35)&(alpha1>220) for name,color in palette.items()}
 boundaries={name:mask&~binary_erosion(mask) for name,mask in masks.items()}
 boundaries['frame'] &= binary_dilation((alpha1<128)|masks['shell']|masks['other'],iterations=2)
 boundaries['shell_outer']=boundaries['shell']&binary_dilation(alpha1<128,iterations=3)
 if key=='front':
  rows=ann1['polylines']; mapping={'C01':'frame','C02':'frame','C03':'glass','C04':'glass','C05':'shell_outer'}
 else:
  rows=ann1['visible_contours']; mapping={'rear_weight_top_visible_edge':'rear_weight','rear_weight_left_lower_visible_edge':'rear_weight','yellow_shell_crown_visible_silhouette':'shell_outer','yellow_shell_front_shoulder_silhouette':'shell_outer','yellow_shell_lower_neck_interface_visible_edge':'shell','neck_ring_lower_visible_outer_edge':'neck','handle_outer_visible_arch':'handle'}
 for row in rows:
  edge=boundaries[mapping[row['id']]]
  if not edge.any(): continue
  distance=distance_transform_edt(~edge)
  for pas in ['a','b']:
   pts=row['pass_1_px' if pas=='a' else 'pass_2_px'] if key=='front' else row['pass_'+pas+'_xy_px']
   q=np.rint(sample(pts)).astype(int); d=distance[q[:,1],q[:,0]]
   expected=next(z for z in stored_r1['views'][key]['visible_boundary_metrics'] if z['id']==row['id']); expected=next(z for z in expected['passes'] if z['pass']==pas)
   actual={'median_px':float(np.median(d)),'rms_px':float(np.sqrt(np.mean(d*d))),'p95_px':float(np.percentile(d,95)),'samples_not_independent_cases':len(d)}
   r1_result.append({'view':key,'curve':row['id'],'pass':pas,'computed':actual,'matches_stored':bool(all(np.isclose(v,expected[k]) for k,v in actual.items()))})
summary['r1_comparison']=r1_result
summary['r1_all_matches']=all(r['matches_stored'] for r in r1_result)
summary['report_matches'].update({
 'point_count':len(points)==len(ann['keypoints_16'])==len(controls['keypoints_16'])==16,
 'r1_row_count':len(r1_result)==24,
 'r2_row_count':len(r2_result)==12,
 'reference_sha256':summary['reference_sha256']==metric['reference_mask_sha256']==controls['reference_mask_sha256'],
 'normalization_L':L==metric['normalization']['L_px']==controls['normalization']['L_px'],
 'occluded_reference_px':nocc==metric['current_occlusion_diagnostic']['reference_yellow_behind_current_model_occluder_px'],
 'window_angle_rounding':round(summary['angle_fit']['angle_deg'],2)==angle['angle_from_image_vertical_deg'],
})
checks = [summary['all_points_valid'], summary['r1_all_matches'], summary['r2_all_matches'], *summary['report_matches'].values()]
summary['all_recomputed_checks_pass'] = all(checks)
out.parent.mkdir(parents=True, exist_ok=True)
with out.open('x', encoding='utf-8') as stream:
    stream.write(json.dumps(summary, ensure_ascii=False, indent=2)+'\n')
print(json.dumps({'head': sha, 'r1_rows': len(r1_result), 'r2_rows': len(r2_result), 'all_recomputed_checks_pass': summary['all_recomputed_checks_pass']}))
if not summary['all_recomputed_checks_pass']:
    raise SystemExit(1)
