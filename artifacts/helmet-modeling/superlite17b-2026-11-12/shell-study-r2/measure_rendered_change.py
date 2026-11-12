"""Measure exact same-camera visible crown arcs, keeping photos local."""
import argparse,hashlib,json
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
from scipy.ndimage import binary_erosion,binary_dilation,distance_transform_edt

def sample(points):
    out=[]
    for a,b in zip(points[:-1],points[1:]):
        a=np.asarray(a,float);b=np.asarray(b,float)
        out.extend(a+(b-a)*t for t in np.linspace(0,1,max(1,int(np.ceil(np.linalg.norm(b-a)/6))),endpoint=False))
    return np.asarray(out+[points[-1]])

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--renders',required=True);ap.add_argument('--output',required=True);ap.add_argument('--references',required=True);ap.add_argument('--local-previews',required=True)
    args=ap.parse_args();here=Path(__file__).resolve().parent;renders=Path(args.renders);out=Path(args.output);out.mkdir(parents=True,exist_ok=True);local=Path(args.local_previews);local.mkdir(parents=True,exist_ok=True)
    result={'camera_policy':'Same frozen camera before/after, never refit to new geometry.','distance_unit':'pixels and reference image width fraction, not millimetres.','fit_not_holdout':'These two crown arcs contributed to shape fitting. A second sequential AI trace is an annotation sensitivity check, not independent validation.','views':{}}
    for key in ['front','side']:
        ann=json.loads((here.parent/'camera-baseline-r1'/('annotations-'+key+'.json')).read_text(encoding='utf-8'));src=Path(args.references)/ann['image']['filename'];assert hashlib.sha256(src.read_bytes()).hexdigest()==ann['image']['sha256']
        rows=[r for r in ann['polylines'] if r['id']=='C05'] if key=='front' else [r for r in ann['visible_contours'] if r['id'] in ['yellow_shell_crown_visible_silhouette','yellow_shell_front_shoulder_silhouette']]
        original=Image.open(src).convert('RGB');masks={};boundaries={};measurements=[]
        for version in ['before','after']:
            rgba=np.asarray(Image.open(renders/(version+'-'+key+'-masks.png')).convert('RGBA'));rgb=rgba[:,:,:3];alpha=rgba[:,:,3]
            mask=(rgb[:,:,1]>210)&(rgb[:,:,0]<40)&(rgb[:,:,2]<40)&(alpha>220)
            boundary=mask & ~binary_erosion(mask) & binary_dilation(alpha<128,iterations=3)
            masks[version]=mask;boundaries[version]=boundary
            distances=distance_transform_edt(~boundary)
            for row in rows:
                for pass_id in ['a','b']:
                    pts=row['pass_1_px' if pass_id=='a' else 'pass_2_px'] if key=='front' else row['pass_'+pass_id+'_xy_px'];q=np.rint(sample(pts)).astype(int);v=distances[q[:,1],q[:,0]]
                    measurements.append({'version':version,'curve':row['id'],'pass':pass_id,'median_px':float(np.median(v)),'rms_px':float(np.sqrt(np.mean(v**2))),'p95_px':float(np.percentile(v,95)),'sample_count_not_independent_cases':len(v),'uncertainty_allowance_px':row.get('uncertainty_px',row.get('uncertainty_band_px'))})
        overlay=original.copy();public=Image.new('RGB',original.size,'#f6f8fb')
        for canvas in [overlay,public]:
            arr=np.asarray(canvas).copy()
            for ver,color in [('before',[244,139,42]),('after',[195,40,84])]:arr[binary_dilation(boundaries[ver])]=color
            canvas.paste(Image.fromarray(arr));draw=ImageDraw.Draw(canvas)
            for row in rows:
                pts=row['pass_1_px'] if key=='front' else row['pass_a_xy_px'];draw.line([tuple(p) for p in pts],fill='#008da7',width=4)
        overlay.save(local/(key+'-shell-only-photo-overlay.png'));public.save(out/(key+'-shell-contours.png'))
        result['views'][key]={'measurements':measurements,'changed_visible_shell_pixels':int(np.sum(masks['before']^masks['after'])),'image_size':list(original.size),'published_image_contains_manufacturer_pixels':False}
    (out/'rendered-boundary-change.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print('RENDERED_SHELL_COMPARISON_MEASURED same cameras / photos local')

if __name__=='__main__':main()
