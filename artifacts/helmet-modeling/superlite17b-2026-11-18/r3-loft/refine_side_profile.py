"""Coarse visible-mask search for the next overall-profile iteration.

The frozen occluder mask is a search approximation only. Final results must be
measured from actual Blender depth/coverage renders, never from this objective.
"""
import argparse,json,sys
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
from scipy.optimize import minimize
from scipy.spatial import ConvexHull
from loft_geometry import surface,control_sections
ROOT=Path(__file__).resolve().parents[4];sys.path.insert(0,str(ROOT/'artifacts/helmet-modeling/superlite17b-2026-11-12/camera-baseline-r1'));from fit_projection import project

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--initial',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
 old=ROOT/'artifacts/helmet-modeling/superlite17b-2026-11-12/shell-rebuild-r3';record=json.loads(Path(a.initial).read_text());initial=record['parameters'];c=json.loads((old/'baseline/baseline-cameras-and-inventory.json').read_text())['fixed_views']['side']['configuration'];W,H=400,218
 ref=np.asarray(Image.open(old/'reference-side-visible-mask.png').convert('L').resize((W,H),Image.Resampling.NEAREST))>127
 rgba=np.asarray(Image.open(old/'baseline/base_side_shell_visible.png').convert('RGBA').resize((W,H),Image.Resampling.NEAREST));occlusion=(rgba[:,:,3]>220)&(rgba[:,:,0]<30)
 x0=np.r_[initial['tops'][:6],initial['widths'][:6],.90,initial['skirt_z0'],initial['skirt_slope']]
 def decode(x):
  p=json.loads(json.dumps(initial));p['tops'][:6]=x[:6].tolist();p['widths'][:6]=x[6:12].tolist();p['y_sections'][:6]=((np.asarray(initial['y_sections'][:6])+.124)*x[12]-.124).tolist();p['skirt_z0']=float(x[13]);p['skirt_slope']=float(x[14]);return p
 def evaluate(x):
  p=decode(x);_,rings=surface(p,2,2);pts=rings.reshape(-1,3);level=pts[:,2]-p['skirt_z0']-p['skirt_slope']*pts[:,1];pts=pts[level>=0]
  xy=project(pts,np.asarray(c['parameters']),c['focal_px'])*[W/1600,H/873]
  try:poly=xy[ConvexHull(xy).vertices]
  except Exception:return 1e4
  im=Image.new('1',(W,H));ImageDraw.Draw(im).polygon([tuple(q) for q in poly],fill=1);full=np.asarray(im,dtype=bool);visible=full&~occlusion
  union=np.count_nonzero(visible|ref);iou=np.count_nonzero(visible&ref)/max(1,union)
  smooth=np.sum(np.diff(x[:6],n=2)**2)/.0005+np.sum(np.diff(x[6:12],n=2)**2)/.0008
  return 1-iou+.003*smooth+.005*np.sum(((x-x0)/np.r_[np.full(12,.06),.3,.08,.6])**2)
 bounds=[(.20,.30),(.24,.33),(.27,.34),(.27,.34),(.24,.32),(.24,.30)]+[(.007,.04),(.04,.10),(.07,.13),(.08,.14),(.08,.14),(.09,.14)]+[(.7,1.02),(.025,.14),(.10,.95)]
 f=minimize(evaluate,np.clip(x0,[b[0] for b in bounds],[b[1] for b in bounds]),method='Powell',bounds=bounds,options={'maxiter':12,'maxfev':2400,'xtol':.0005,'ftol':.0002})
 p=decode(f.x);record={'feature_iteration':'06 overall side profile only, coarse mask hypothesis; final depth-render metric required','parameters':p,'sections_S0_to_S6_16_points':control_sections(p).tolist(),'section_count':7,'points_per_section':16,'old_shell_vertices_used':False,'search_objective':float(f.fun),'search_terminated':bool(f.success),'limits':'Convex envelope and fixed occluder approximation used for parameter search only; not final IoU. No test thresholds changed.'}
 out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=False);(out/'sections.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8');print(json.dumps({'params':p,'search_cost':float(f.fun),'evaluations':int(f.nfev)}),flush=True)
if __name__=='__main__':main()
