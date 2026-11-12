"""Low-dimensional shell-only visual candidate, constrained by retained assembly.

Fixed virtual cameras are diagnostic choices, not physical calibration. The
objective is limited to two annotated crown arcs, not whole-object accuracy.
"""
import argparse
import json
from pathlib import Path
import sys
import numpy as np
from scipy.optimize import least_squares
from scipy.spatial import ConvexHull

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent/'camera-baseline-r1'))
from fit_projection import project, segment_distances

def smoothstep(t):
    t=np.clip(t,0,1);return t*t*(3-2*t)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--constraints',required=True);ap.add_argument('--output',required=True)
    args=ap.parse_args();data=np.load(Path(args.constraints)/'shell-data.npz');P=data['points'];outer=P[:2254]
    adjacency=[set() for _ in range(2254)]
    for tri in data['triangles']:
        for a,b in zip(tri,np.roll(tri,-1)):
            if a<2254 and b<2254:adjacency[a].add(int(b));adjacency[b].add(int(a))
    fixed=set(int(x) for x in data['front_ids'])|set(int(x) for x in data['rim_ids'])
    # One-ring exact protection, plus continuous fade across several neighbours.
    fixed|={j for i in list(fixed) for j in adjacency[i]}
    dist=data['fixed_part_distances']
    contacts={i for i,d in enumerate(dist) if d<.006}
    contacts|={j for i in list(contacts) for j in adjacency[i]}
    fixed|=contacts
    weight=smoothstep((dist-.006)/.018)*smoothstep((data['rim_distances']-.003)/.018)
    weight[list(fixed)]=0
    # Symmetrize the displacement support, not the existing asymmetric assembly.
    mirrored=outer.copy();mirrored[:,0]*=-1
    mirror_id=np.argmin(np.sum((outer[:,None,:]-mirrored[None,:,:])**2,axis=2),axis=0)
    weight=np.minimum(weight,weight[mirror_id])
    for _ in range(3):
        mean=np.array([np.mean([weight[j] for j in adjacency[i]]) if adjacency[i] else weight[i] for i in range(len(weight))])
        weight=np.minimum(weight,.6*weight+.4*mean)
        weight[list(fixed)]=0
    # Small local fairing of the existing lower/shoulder ripples. Paired inner
    # vertices receive the same low-frequency displacement; collar remains fixed.
    fair=outer.copy()
    region=np.exp(-((outer[:,1]+.055)/.11)**2-((outer[:,2]-.16)/.065)**2)*weight
    for _ in range(10):
        mean=np.array([np.mean(fair[list(neighbors)],axis=0) if neighbors else fair[i] for i,neighbors in enumerate(adjacency)])
        fair+=.22*region[:,None]*(mean-fair)
    fair_delta=fair-outer
    fair_length=np.linalg.norm(fair_delta,axis=1)
    fair_delta*=np.minimum(1,.003/np.maximum(fair_length,1e-12))[:,None]
    x,y,z=outer.T
    crown=np.exp(-((y-.005)/.095)**2-((z-.265)/.065)**2)*smoothstep((z-.205)/.04)
    rear=np.exp(-((y-.10)/.065)**2-((z-.26)/.055)**2)
    shoulder=np.exp(-((y+.085)/.045)**2-((z-.23)/.065)**2)
    basis=np.zeros((len(outer),3,5))
    basis[:,2,0]=crown
    basis[:,2,1]=crown*(y-.01)/.1
    basis[:,0,2]=crown*x/.12
    basis[:,1,3]=rear
    basis[:,1,4]=shoulder
    basis*=weight[:,None,None]
    fit=json.loads((HERE.parent/'camera-baseline-r1/results/camera-fit.json').read_text(encoding='utf-8'))
    front=json.loads((HERE.parent/'camera-baseline-r1/annotations-front.json').read_text(encoding='utf-8'))
    side=json.loads((HERE.parent/'camera-baseline-r1/annotations-side.json').read_text(encoding='utf-8'))
    front_curve=np.asarray(next(r for r in front['polylines'] if r['id']=='C05')['pass_1_px'],float)
    side_curve=np.asarray(next(r for r in side['visible_contours'] if r['id']=='yellow_shell_crown_visible_silhouette')['pass_a_xy_px'],float)
    targets=[('front',front_curve,25.),('side',side_curve,7.)]
    def deformed(params):return outer+fair_delta+np.einsum('ijk,k->ij',basis,params)
    def curve_errors(points,key,curve):
        c=fit['views'][key]['primary'];q=project(points,np.asarray(c['parameters']),c['focal_px']);polygon=q[ConvexHull(q).vertices]
        return segment_distances(curve,polygon)
    def objective(params):
        points=deformed(params);r=[]
        for key,curve,sigma in targets:r.extend(curve_errors(points,key,curve)/sigma)
        r.extend(params/.010)
        delta=points-outer
        lap=np.array([delta[i]-np.mean(delta[list(neighbors)],axis=0) if neighbors else np.zeros(3) for i,neighbors in enumerate(adjacency)])
        # Penalize steep changes beside locked attachment footprints. A lower
        # silhouette residual is not useful if it introduces a local hump.
        r.extend((lap/.003*.55).ravel())
        # Discourage increased overall height/depth: curve redistribution first.
        extent=np.ptp(points,axis=0)-np.ptp(outer,axis=0)
        r.extend(extent[[1,2]]/.008)
        return np.asarray(r)
    candidates=[]
    for seed in [np.zeros(5),np.array([.004,0,.002,.004,0]),np.array([-.004,.004,0,-.004,0])]:
        fitresult=least_squares(objective,seed,bounds=(-.008,.008),loss='soft_l1',f_scale=2.,max_nfev=180,diff_step=1e-3)
        candidates.append(fitresult)
    best=min(candidates,key=lambda f:f.cost)
    out=Path(args.output).resolve();out.mkdir(parents=True,exist_ok=True)
    choices=[]
    for strength in [.5,1.]:
        delta=(fair_delta+np.einsum('ijk,k->ij',basis,best.x))*strength
        # hard cap is a model safety/search limit, never a measured tolerance.
        lengths=np.linalg.norm(delta,axis=1);delta*=np.minimum(1,.009/np.maximum(lengths,1e-12))[:,None]
        all_delta=np.zeros_like(P);all_delta[:2254]=delta;all_delta[2254:4508]=delta
        name='gentle' if strength==.5 else 'moderate'
        np.savez_compressed(out/(name+'-displacements.npz'),delta=all_delta)
        measurements={}
        for key,curve,sigma in targets:
            before=curve_errors(outer,key,curve);after=curve_errors(outer+delta,key,curve)
            measurements[key]={'model_envelope_approximation':True,'before_curve_rms_px':float(np.sqrt(np.mean(before**2))),'after_curve_rms_px':float(np.sqrt(np.mean(after**2))),'annotation_allowance_px':sigma,'fit_to_same_two_arcs_not_independent_validation':True}
        choices.append({'name':name,'strength':strength,'max_displacement_model_m':float(np.linalg.norm(delta,axis=1).max()),'changed_outer_vertices':int(np.sum(np.linalg.norm(delta,axis=1)>1e-8)),'fixed_ids':sorted(fixed),'metrics':measurements,'outer_displacements':delta.tolist()})
    report={'source_sha256':'f4c3241da1bd541d723d5dcf9cdc9be6a144ca3acdca1911b73002838840ce92','allowed_object':'Shell_Yellow_520-065_ESTIMATED','other_fixed_objects':145,'outer_count':2254,'inner_start':2254,'collar_start':4508,'exact_protected_outer_count':len(fixed),'clearance_lock_model_m':.006,'clearance_fade_model_m':.018,'rim_fade_model_m':.018,'parameter_order':['crown_height','crown_fore_aft_curve_balance','upper_width','upper_rear_depth','forehead_shoulder_depth'],'optimized_parameters_model_m':best.x.tolist(),'solver_cost':float(best.cost),'solver_terminated':bool(best.success),'candidates':choices,'limits':['Fixed uncertain virtual cameras; two fitting arcs are not independent validation.','No absolute scale or factory dimensions recovered.','All shape displacements and protection radii are modelling estimates.','Actual saved-source self-intersection/other-parts checks and rendered silhouette review still required.']}
    (out/'candidate-parameters.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'parameters':best.x.tolist(),'choices':[{k:v for k,v in c.items() if k not in ['fixed_ids','outer_displacements']} for c in choices],'source_edited':False}),flush=True)

if __name__=='__main__':main()
