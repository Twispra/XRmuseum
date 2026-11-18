"""Measure visible 2-D boundaries; manufacturer pixels stay in local previews."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_erosion, binary_dilation, distance_transform_edt

from fit_projection import project

def sample_curve(points, spacing=6):
    points = np.asarray(points, float)
    sampled = []
    for a, b in zip(points[:-1], points[1:]):
        n = max(1, int(np.ceil(np.linalg.norm(b-a)/spacing)))
        sampled.extend(a+(b-a)*t for t in np.linspace(0, 1, n, endpoint=False))
    return np.asarray(sampled+[points[-1]])

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--fit', required=True); ap.add_argument('--renders', required=True)
    ap.add_argument('--geometry', required=True); ap.add_argument('--reference-root', required=True)
    ap.add_argument('--output', required=True); ap.add_argument('--local-previews', required=True)
    args = ap.parse_args()
    here = Path(__file__).resolve().parent
    out = Path(args.output); out.mkdir(parents=True, exist_ok=True)
    local = Path(args.local_previews); local.mkdir(parents=True, exist_ok=True)
    fits = json.loads(Path(args.fit).read_text())
    parts = json.loads((Path(args.geometry)/'projection-parts.json').read_text())
    geom = np.load(Path(args.geometry)/'projection-geometry.npz')
    palette = {'shell': [0,255,0], 'frame': [0,0,255], 'glass': [128,128,128],
               'rear_weight': [255,0,0], 'handle': [255,255,0], 'neck': [0,255,255], 'other': [13,13,13]}
    report = {'source_sha256': fits['source_sha256'], 'geometry_edited': False,
              'normalization': 'Percent of reference image width, not millimetres or surveyed object width.',
              'views': {}, 'limits': ['Closest visible rendered boundary is a 2-D diagnostic, not a fixed 3-D correspondence.',
              'Flat masks show opaque geometric coverage; transparent glass is temporarily opaque.',
              'Only annotated visible segments are sampled; curves are not closed across occlusions.',
              'Sequential AI retracing is not independent human annotation.',
              'Focal-profile envelope is an analyst-selected sensitivity range, not a confidence interval.']}
    for key, fit in fits['views'].items():
        ann = json.loads((here/f'annotations-{key}.json').read_text(encoding='utf-8-sig'))
        src = Path(args.reference_root)/ann['image']['filename']
        assert hashlib.sha256(src.read_bytes()).hexdigest() == ann['image']['sha256']
        original = Image.open(src).convert('RGB')
        rgba = np.array(Image.open(Path(args.renders)/(key+'-coverage.png')).convert('RGBA'))
        rgb, alpha = rgba[:,:,:3], rgba[:,:,3]
        masks = {name: (np.linalg.norm(rgb.astype(float)-np.asarray(color), axis=2)<35)&(alpha>220) for name,color in palette.items()}
        boundaries = {name: mask & ~binary_erosion(mask) for name,mask in masks.items()}
        # Outer frame excludes its inner glass boundary; shell silhouette is
        # restricted to background exposure. Other boundaries remain diagnostic.
        boundaries['frame'] &= binary_dilation((alpha<128)|masks['shell']|masks['other'], iterations=2)
        boundaries['shell_outer'] = boundaries['shell'] & binary_dilation(alpha<128, iterations=3)
        if key == 'front':
            rows = ann['polylines']
            mapping = {'C01':'frame','C02':'frame','C03':'glass','C04':'glass','C05':'shell_outer'}
        else:
            rows = ann['visible_contours']
            mapping = {'rear_weight_top_visible_edge':'rear_weight','rear_weight_left_lower_visible_edge':'rear_weight',
                       'yellow_shell_crown_visible_silhouette':'shell_outer','yellow_shell_front_shoulder_silhouette':'shell_outer',
                       'yellow_shell_lower_neck_interface_visible_edge':'shell','neck_ring_lower_visible_outer_edge':'neck',
                       'handle_outer_visible_arch':'handle'}
        width, height = original.size
        preview = original.copy(); draw = ImageDraw.Draw(preview)
        # Published image contains only the project's model and numeric traces.
        public = Image.new('RGB', original.size, '#f8fafc')
        color_vis = Image.fromarray(rgb).convert('RGB')
        public.paste(Image.blend(Image.new('RGB',original.size,'white'), color_vis, .18), mask=Image.fromarray(alpha))
        pubdraw = ImageDraw.Draw(public)
        boundary_points = np.argwhere((alpha>220)&~binary_erosion(alpha>220))
        for y,x in boundary_points:
            draw.point((int(x),int(y)), fill='#ff5a36'); pubdraw.point((int(x),int(y)),fill='#ed552e')
        relevant_groups = ['frame','glass','shell_outer'] if key=='front' else ['rear_weight','shell_outer','neck','handle']
        highlighted = np.zeros_like(alpha, dtype=bool)
        for group in relevant_groups:
            highlighted |= boundaries[group]
        highlighted = binary_dilation(highlighted)
        for canvas in [preview,public]:
            pixels=np.array(canvas);pixels[highlighted]=[239,93,46]
            canvas.paste(Image.fromarray(pixels))
        draw=ImageDraw.Draw(preview);pubdraw=ImageDraw.Draw(public)
        metrics = []
        for row in rows:
            group = mapping[row['id']]
            boundary = boundaries[group]
            if not boundary.any():
                metrics.append({'id':row['id'],'status':'no_visible_model_boundary','group':group}); continue
            distmap = distance_transform_edt(~boundary)
            passes = []
            for pass_id in ['a','b']:
                pts = row['pass_1_px' if pass_id=='a' else 'pass_2_px'] if key=='front' else row[f'pass_{pass_id}_xy_px']
                sampled = sample_curve(pts)
                xy = np.rint(sampled).astype(int); distances = distmap[xy[:,1],xy[:,0]]
                sigma = row.get('uncertainty_px',row.get('uncertainty_band_px'))
                passes.append({'pass':pass_id,'sample_spacing_px':6,'samples_not_independent_cases':len(distances),
                    'median_px':float(np.median(distances)),'rms_px':float(np.sqrt(np.mean(distances**2))),
                    'p95_px':float(np.percentile(distances,95)), 'median_percent_image_width':float(np.median(distances)/width*100),
                    'annotation_allowance_px':sigma, 'fraction_within_annotation_allowance':float(np.mean(distances<=sigma))})
            pts = row['pass_1_px'] if key=='front' else row['pass_a_xy_px']
            draw.line([tuple(p) for p in pts],fill='#00a9cf',width=4)
            pubdraw.line([tuple(p) for p in pts],fill='#008eaa',width=4)
            metrics.append({'id':row['id'],'model_group':group,'passes':passes})
        for row in fit['primary']['cap_errors']:
            a=row['observed_px'];b=row['projected_px']
            draw.line([tuple(a),tuple(b)],fill='#e82b35',width=4);pubdraw.line([tuple(a),tuple(b)],fill='#e82b35',width=4)
            for pt, color in [(a,'#0098ba'),(b,'#e82b35')]:
                box=(pt[0]-5,pt[1]-5,pt[0]+5,pt[1]+5)
                draw.ellipse(box,outline=color,width=3);pubdraw.ellipse(box,outline=color,width=3)
        preview.save(local/(key+'-photo-overlay.png'))
        public.save(out/(key+'-contour-diagnostic.png'))
        # Sensitivity over all pass-A candidates within 25% of primary cost.
        candidates=[c for c in fit['candidates'] if c['pass']=='a' and c['objective_cost']<=fit['primary']['objective_cost']*1.25]
        sensitivity=[]
        for row in parts['parts']:
            if not any(token in row['name'] for token in ['Shell_Yellow','Ring_Port','Weights_Brass_Rear','Hinge_Neck','Handle_Brass']): continue
            v=geom['v'+str(row['index'])];v=v[::max(1,len(v)//100)]
            primary=project(v,np.array(fit['primary']['parameters']),fit['primary']['focal_px'])
            delta=np.array([np.linalg.norm(project(v,np.array(c['parameters']),c['focal_px'])-primary,axis=1) for c in candidates])
            sensitivity.append({'part':row['name'],'profile_count':len(candidates),'max_median_shift_px':float(np.max(np.median(delta,axis=1))),
                                'max_point_shift_px':float(np.max(delta))})
        report['views'][key]={'image':ann['image']['filename'],'image_size':[width,height],
                             'visible_boundary_metrics':metrics,'cap_fit_residuals':fit['primary']['cap_errors'],
                             'camera_profile_sensitivity':sensitivity,'profile_selection_rule':'pass A, cost <= 1.25 times primary; diagnostic heuristic',
                             'manufacturer_pixels_in_published_diagnostic':False}
    (out/'baseline-metrics.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('V4_VISIBLE_BOUNDARY_BASELINE_OK; local photo overlays separate from source-free published diagnostics')


if __name__=='__main__': main()
