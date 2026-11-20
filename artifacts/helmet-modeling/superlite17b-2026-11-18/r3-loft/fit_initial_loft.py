"""First feature iteration: overall side silhouette using a brand-new section net."""
import argparse,json,sys
from pathlib import Path
import numpy as np
from scipy.optimize import least_squares
from scipy.spatial import ConvexHull
from loft_geometry import surface,params_default

ROOT=Path(__file__).resolve().parents[4]
sys.path.insert(0,str(ROOT/'artifacts/helmet-modeling/superlite17b-2026-11-12/camera-baseline-r1'))
from fit_projection import project,segment_distances

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--output',required=True);a=ap.parse_args()
    old=ROOT/'artifacts/helmet-modeling/superlite17b-2026-11-12'
    cameras=json.loads((old/'camera-baseline-r1/results/camera-fit.json').read_text(encoding='utf-8'))
    front=json.loads((old/'camera-baseline-r1/annotations-front.json').read_text(encoding='utf-8'))
    side=json.loads((old/'camera-baseline-r1/annotations-side.json').read_text(encoding='utf-8'))
    top=np.asarray(next(c for c in side['visible_contours'] if c['id']=='yellow_shell_crown_visible_silhouette')['pass_a_xy_px'])
    shoulder=np.asarray(next(c for c in side['visible_contours'] if c['id']=='yellow_shell_front_shoulder_silhouette')['pass_a_xy_px'])
    lower=np.asarray(next(c for c in side['visible_contours'] if c['id']=='yellow_shell_lower_neck_interface_visible_edge')['pass_a_xy_px'])
    fronttop=np.asarray(next(c for c in front['polylines'] if c['id']=='C05')['pass_1_px'])
    initial=params_default();x0=np.r_[initial['tops'][:6],initial['widths'][:6],initial['skirt_z0'],initial['skirt_slope']]
    def decode(x):
        p=params_default();p['tops'][:6]=x[:6].tolist();p['widths'][:6]=x[6:12].tolist();p['skirt_z0']=float(x[12]);p['skirt_slope']=float(x[13]);return p
    def residual(x):
        p=decode(x);_,r=surface(p,3,3);pts=r.reshape(-1,3);valid=pts[:,2]>=p['skirt_z0']+p['skirt_slope']*pts[:,1];pts=pts[valid]
        values=[]
        for key,curve,sigma in [('side',top,7),('side',shoulder,8),('front',fronttop,25)]:
            c=cameras['views'][key]['primary'];xy=project(pts,np.asarray(c['parameters']),c['focal_px']);poly=xy[ConvexHull(xy).vertices]
            values.extend(segment_distances(curve,poly)/sigma)
        # Lower rim samples are generated from true section-plane intersections.
        skirt=[]
        for ring in r:
            level=ring[:,2]-p['skirt_z0']-p['skirt_slope']*ring[:,1]
            for i in range(len(ring)):
                j=(i+1)%len(ring)
                if level[i]*level[j]<0:
                    t=level[i]/(level[i]-level[j]);skirt.append(ring[i]*(1-t)+ring[j]*t)
        c=cameras['views']['side']['primary'];xy=project(np.asarray(skirt),np.asarray(c['parameters']),c['focal_px'])
        values.extend(np.min(np.linalg.norm(lower[:,None,:]-xy[None,:,:],axis=2),axis=1)/10)
        values.extend((x-x0)/np.r_[np.full(6,.045),np.full(6,.035),.05,.4])
        values.extend(np.diff(x[:6],n=2)/.025)
        values.extend(np.diff(x[6:12],n=2)/.025)
        return np.asarray(values)
    lo=np.r_[[.21,.25,.27,.27,.25,.24],[.007,.05,.08,.085,.085,.09],.055,.05]
    hi=np.r_[[.28,.33,.35,.35,.33,.31],[.045,.12,.16,.16,.16,.15],.15,.65]
    candidates=[]
    for scale in [1.,.97,1.03]:
        seed=np.clip(x0*np.r_[np.full(12,scale),1,1],lo+1e-6,hi-1e-6)
        f=least_squares(residual,seed,bounds=(lo,hi),loss='soft_l1',f_scale=2,max_nfev=180,diff_step=.0001)
        candidates.append(f)
    best=min(candidates,key=lambda f:f.cost);p=decode(best.x);controls,_=surface(p)
    out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=False)
    (out/'sections.json').write_text(json.dumps({'feature_iteration':'01 overall side profile only','parameters':p,'sections_S0_to_S6_16_points':controls.tolist(),'section_count':7,'points_per_section':16,'old_shell_vertices_used':False,'solver_terminated':bool(best.success),'cost':float(best.cost),'frozen_camera_source':'2026-11-12/camera-baseline-r1/results/camera-fit.json','limits':'Image-aligned new surface hypothesis. Partial curves used for fitting; no independent real dimensions or hidden silhouette recovered. Window tilt/temple/skirt detailing remain later feature iterations.'},indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'params':p,'cost':float(best.cost),'new_control_points':112,'old_vertices_used':False}),flush=True)
if __name__=='__main__':main()
