"""Bounded perspective matching against frozen v4; never edits source geometry.

Install scipy in a task-local directory and put it on PYTHONPATH, or use an
existing scientific Python environment. Camera parameters are virtual fits to
unscaled product photographs, not recovered physical camera intrinsics.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from scipy.optimize import least_squares
from scipy.spatial import ConvexHull

TARGET = np.array([0., .015, .17])


def basis(p):
    yaw, elevation, roll = p[:3]
    right = np.array([np.cos(yaw), np.sin(yaw), 0.])
    view = np.array([np.sin(yaw)*np.cos(elevation), -np.cos(yaw)*np.cos(elevation), np.sin(elevation)])
    up = np.cross(view, right)
    return np.cos(roll)*right + np.sin(roll)*up, -np.sin(roll)*right + np.cos(roll)*up, view


def project(x, p, focal):
    right, up, view = basis(p)
    q = np.asarray(x) - TARGET
    depth = np.exp(p[3]) - q @ view
    return np.column_stack([p[4] + focal*(q @ right)/depth,
                            p[5] - focal*(q @ up)/depth])


def segment_distances(samples, polygon):
    a = polygon
    b = np.roll(polygon, -1, axis=0)
    edge = b-a
    delta = samples[:, None, :] - a[None, :, :]
    t = np.clip(np.sum(delta*edge[None, :, :], axis=2) / np.maximum(np.sum(edge*edge, axis=1), 1e-12), 0, 1)
    closest = a[None, :, :] + t[:, :, None]*edge[None, :, :]
    return np.min(np.linalg.norm(samples[:, None, :]-closest, axis=2), axis=1)


def silhouette_envelope(vertices, p, focal):
    xy = project(vertices, p, focal)
    return xy[ConvexHull(xy).vertices]


def end_face_center(vertices, view, part_name):
    # Axes are checked against the preserved construction and saved geometry.
    # PCA's smallest axis is NOT reliable for the thicker blue caps: the axial
    # variance can exceed radial variance. Never infer all cap axes that way.
    c = vertices.mean(axis=0)
    axis = np.array([0., 1., 0.]) if 'DustCap' in part_name else np.array([-.98, -.14, 0.])
    axis /= np.linalg.norm(axis)
    if axis @ view < 0:
        axis = -axis
    t = (vertices-c) @ axis
    end = vertices[t > t.max()-max(1e-7, np.ptp(t)*.01)]
    return end.mean(axis=0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--geometry', required=True)
    ap.add_argument('--output', required=True)
    args = ap.parse_args()
    here = Path(__file__).resolve().parent
    geomdir = Path(args.geometry)
    data = np.load(geomdir/'projection-geometry.npz')
    parts = json.loads((geomdir/'projection-parts.json').read_text())
    byname = {r['name']: data[f"v{r['index']}"] for r in parts['parts']}
    annotations = {key: json.loads((here/f'annotations-{key}.json').read_text(encoding='utf-8-sig')) for key in ['front', 'side']}
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)
    result = {'source_sha256': parts['source_sha256'], 'geometry_edited': False,
              'method': 'Fixed-focal profiles, bounded perspective pose/crop; robust soft-L1; two sequential AI annotation passes.',
              'limits': ['Model geometry is estimated, not a real-world calibration target.',
                         'No metric scale, lens metadata or same-specimen identity.',
                         'Open-curve registration uses projected convex envelopes as an initial camera-matching approximation; final diagnostics require rendered visible masks.',
                         'Side view has only three confidently visible cap faces; handle outline adds estimated model geometry and is not independent ground truth.',
                         'The old 44.99/59.87 px values used different correspondences and cannot be called a directly comparable accuracy improvement.'],
              'annotations_sha256': {k: hashlib.sha256((here/f'annotations-{k}.json').read_bytes()).hexdigest() for k in annotations},
              'views': {}}
    side_cap_map = {
        'emergency_knob_silver_end_face_center': 'Knob_Black_Emergency_520-525_CentreCap',
        'upper_blue_dust_cap_visible_end_center': 'Emergency_Inlet_DustCap',
        'lower_blue_dust_cap_visible_end_center': 'CheckValve_DustCap'}
    for key, ann in annotations.items():
        width, height = ann['image']['width_px'], ann['image']['height_px']
        cap_rows = [r for r in ann['landmarks'] if r['id'] in side_cap_map] if key == 'side' else []
        traces = [r for r in ann['polylines'] if r['boundary_type'] == 'retainer_outer_boundary'] if key == 'front' else [r for r in ann['visible_contours'] if r['id'] == 'handle_outer_visible_arch']
        boundary_vertices = byname['Ring_PortChrome_560-070' if key == 'front' else 'Handle_Brass_560-014']
        candidates = []
        for pass_id in ['a', 'b']:
            curves = []
            for row in traces:
                points = row['pass_1_px' if pass_id == 'a' else 'pass_2_px'] if key == 'front' else row[f'pass_{pass_id}_xy_px']
                curves.append((np.asarray(points, float), row.get('uncertainty_px', row.get('uncertainty_band_px', 8))))
            def residual(p, focal):
                polygon = silhouette_envelope(boundary_vertices, p, focal)
                values = []
                for points, sigma in curves:
                    weight = 1. if key == 'front' else .55
                    values.extend(segment_distances(points, polygon)/sigma*weight)
                if key == 'side':
                    view = basis(p)[2]
                    modelpoints = np.array([end_face_center(byname[side_cap_map[r['id']]], view, side_cap_map[r['id']]) for r in cap_rows])
                    observed = np.array([r[f'pass_{pass_id}_xy_px'] for r in cap_rows])
                    sigmas = np.array([r['uncertainty_radius_px'] for r in cap_rows])
                    values.extend(((project(modelpoints, p, focal)-observed)/sigmas[:, None]).ravel())
                return np.asarray(values)
            for factor in [.8, 1.3, 2.2, 3.8, 6.]:
                focal = width*factor
                yaw0s = [-.45, -.7, -1.0] if key == 'front' else [-1.8, -2.05, -2.3]
                best = None
                for yaw in yaw0s:
                    for elevation in [.05, .3, .55]:
                        x0 = [yaw, elevation, 0., np.log(max(.48, focal*.29/(height*.8))), width*.52, height*.55]
                        lo = [-1.5 if key == 'front' else -2.65, -.35, -.3, np.log(.4), width*.05, -height*.1]
                        hi = [-.08 if key == 'front' else -1.3, .85, .3, np.log(8.), width*.95, height*1.1]
                        fit = least_squares(lambda p: residual(p, focal), np.clip(x0, np.array(lo)+1e-7, np.array(hi)-1e-7),
                                            bounds=(lo, hi), loss='soft_l1', f_scale=2., max_nfev=250, x_scale='jac')
                        if best is None or fit.cost < best.cost:
                            best = fit
                p = best.x
                polygon = silhouette_envelope(boundary_vertices, p, focal)
                trace_dist = np.concatenate([segment_distances(points, polygon) for points, _ in curves])
                cap_errors = []
                if key == 'side':
                    view = basis(p)[2]
                    for row in cap_rows:
                        pt = end_face_center(byname[side_cap_map[row['id']]], view, side_cap_map[row['id']])
                        xy = project([pt], p, focal)[0]
                        observed = row[f'pass_{pass_id}_xy_px']
                        cap_errors.append({'annotation_id': row['id'], 'model_part': side_cap_map[row['id']],
                                           'definition': 'Outward end-face center of thin cap mesh, not whole-object mean.',
                                           'model_point_world': pt.tolist(), 'observed_px': observed, 'projected_px': xy.tolist(),
                                           'error_px': float(np.linalg.norm(xy-observed))})
                candidates.append({'pass': pass_id, 'focal_width_factor': factor, 'focal_px': focal,
                    'parameters': p.tolist(), 'parameter_order': ['yaw_rad', 'elevation_rad', 'roll_rad', 'log_model_distance', 'crop_cx_px', 'crop_cy_px'],
                    'image_size': [width, height], 'target_world': TARGET.tolist(),
                    'objective_cost': float(best.cost), 'solver_success': bool(best.success),
                    'open_curve_rms_px': float(np.sqrt(np.mean(trace_dist**2))), 'open_curve_p95_px': float(np.percentile(trace_dist, 95)),
                    'cap_errors': cap_errors,
                    'camera_basis_right_up_back': [v.tolist() for v in basis(p)],
                    'bounds_active': [int(x) for x in best.active_mask]})
                print(key, pass_id, factor, 'cost', round(best.cost, 2), 'curveRMS', round(candidates[-1]['open_curve_rms_px'], 2), flush=True)
        primary = min([c for c in candidates if c['pass'] == 'a'], key=lambda c: c['objective_cost'])
        # All focal profiles are retained. Similar residuals do not establish a unique camera.
        result['views'][key] = {'annotation_file': f'annotations-{key}.json', 'primary': primary, 'candidates': candidates,
                                 'fit_inputs': ['C01/C02 visible outer frame curves; fastener IDs excluded'] if key == 'front' else ['three verified cap end faces', 'visible handle outer arch, downweighted'],
                                 'excluded_from_fit': ['rear weight', 'rear hinge', 'shell', 'neck', 'unknown fastener roles']}
    (out/'camera-fit.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print('V4_PERSPECTIVE_MATCH_COMPLETE; no geometry edited', flush=True)


if __name__ == '__main__':
    main()
