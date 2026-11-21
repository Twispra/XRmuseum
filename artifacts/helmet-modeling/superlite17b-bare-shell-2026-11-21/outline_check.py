"""Compare a saved shell to the two visible outlines in Kirby Morgan 17BHDL-2.

Run with Python, NumPy, SciPy and Pillow. Blender handles mesh readback.
The optional diagram contains mask edges only. Recreate the reference raster with
pdftoppm -f 2 -l 2 -scale-to 1500 -png -singlefile handle-weights.pdf weights-page2
"""
import argparse
import hashlib
import json
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np


REFERENCE_SHA = "e232d6a3b1919433ed5f1b62983a748997fca39957fdee70e2f2fbc15270a6d4"
CROP = (504, 906, 817, 1215)
# Six-parameter projection fitted on the first newly built shell. Keep it fixed
# across later revisions so changing the camera cannot inflate the reported gain.
INITIAL_SHA = "7fb1e712c396845e4d8226bc5a4abc31ae848e58ba8ffc415485fb252d6fe546"
LOCKED_PARAMS = {
    "azimuth_from_front_toward_minus_x_deg": 38.55892339094866,
    "elevation_deg": 18.857120300463905,
    "clockwise_image_roll_deg": -27.212714639289306,
    "scale_px_per_m": 986.3496618016029,
    "model_origin_world_m": [-0.0016460791230201721, -0.004371464252471924, 0.16005337983369827],
    "origin_in_page_px": [641.8678534023335, 1050.155337736541],
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_shell(model, destination):
    import bpy

    bpy.ops.wm.open_mainfile(filepath=str(model))
    objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(objects) != 1:
        raise ValueError("Expected one shell mesh in the saved scene")
    obj = objects[0]
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    try:
        vertices = np.array([tuple(evaluated.matrix_world @ v.co) for v in mesh.vertices])
        mesh.calc_loop_triangles()
        faces = np.array([list(t.vertices) for t in mesh.loop_triangles])
        group = obj.vertex_groups["Face_Opening"].index
        rim = {v.index for v in mesh.vertices
               if any(g.group == group and g.weight > 0.999 for g in v.groups)}
        neighbors = {i: [] for i in rim}
        for edge in mesh.edges:
            a, b = edge.vertices
            if a in rim and b in rim:
                neighbors[a].append(b)
                neighbors[b].append(a)
        if not rim or any(len(v) != 2 for v in neighbors.values()):
            raise ValueError("Face_Opening must identify a single evaluated mesh edge loop")
        first = min(rim)
        ordered, previous, i = [], None, first
        while True:
            ordered.append(i)
            following = next(n for n in neighbors[i] if n != previous)
            if following == first:
                break
            if len(ordered) >= len(rim):
                raise ValueError("Invalid opening loop")
            previous, i = i, following
        if len(ordered) != len(rim):
            raise ValueError("Face_Opening consists of multiple loops")
        np.savez(destination, points=vertices, triangles=faces, opening=ordered)
    finally:
        evaluated.to_mesh_clear()


def reference_masks(path):
    from PIL import Image
    from scipy import ndimage

    if digest(path) != REFERENCE_SHA:
        raise ValueError("Expected the 1160 x 1500 page-2 raster recorded in the report")
    with Image.open(path) as image:
        if image.size != (1160, 1500):
            raise ValueError("Different reference image dimensions")
        rgb = np.asarray(image.convert("RGB").crop(CROP), dtype=np.int16)
    # The isolated shell is separated from all neighboring exploded components by white space.
    labels, _ = ndimage.label(rgb.min(axis=2) < 220)
    body = ndimage.binary_fill_holes(labels == labels[8, 121])
    yy, xx = np.indices(body.shape)
    xx, yy = xx + CROP[0], yy + CROP[1]
    region = (xx > 620) & (xx < 805) & (yy > 950) & (yy < 1113) & body
    gray, _ = ndimage.label((np.ptp(rgb, axis=2) < 17) & region)
    # Include both gray back wall and white-through-neck pixels inside the window.
    opening = ndimage.binary_fill_holes(gray == gray[1010-CROP[1], 695-CROP[0]])
    if body.sum() != 69743 or opening.sum() != 12671:
        raise RuntimeError("Segmentation differs from the visually checked reference masks")
    return body, opening


def pixel_points(mask):
    return np.column_stack(np.where(mask))[:, ::-1] + np.array(CROP[:2])


def camera_matrix(azimuth, elevation, roll):
    yaw, pitch, angle = np.radians([azimuth, elevation, roll])
    back = np.array([-np.sin(yaw)*np.cos(pitch), -np.cos(yaw)*np.cos(pitch), np.sin(pitch)])
    right = np.array([np.cos(yaw), -np.sin(yaw), 0.0])
    up = np.cross(back, right)
    image_roll = np.array([[np.cos(angle), -np.sin(angle)], [np.sin(angle), np.cos(angle)]])
    return image_roll @ np.stack([right, -up])


def project(points, params):
    azimuth, elevation, roll, scale, x, y = params
    return points @ camera_matrix(azimuth, elevation, roll).T * scale + [x, y]


def fit_camera(points, opening_indices, body_mask, opening_mask):
    from scipy.optimize import least_squares
    from scipy.spatial import ConvexHull

    unit = np.max(np.ptp(points, axis=0))
    center = (points.max(axis=0) + points.min(axis=0)) * 0.5
    vertices = (points - center) / unit
    hull = vertices[ConvexHull(vertices).vertices]
    opening = vertices[opening_indices]
    ref_body = pixel_points(body_mask)
    ref_hull = ref_body[ConvexHull(ref_body).vertices]
    target_win = pixel_points(opening_mask)
    # Bbox center, not area centroid. Area centroid varies with rim tessellation.
    target_center = (target_win.min(axis=0) + target_win.max(axis=0))*0.5
    theta = np.arange(32)*np.pi/16
    directions = np.column_stack([np.cos(theta), np.sin(theta)]).T
    supports = (ref_hull @ directions).max(axis=0)
    aperture_directions = directions[:, ::4]
    target_window_supports = (target_win @ aperture_directions).max(axis=0)

    def residuals(params):
        world_directions = camera_matrix(*params[:3]).T @ directions
        predicted = (hull @ world_directions).max(axis=0)*params[3] + params[4:] @ directions
        win = project(opening, params)
        win_center = (win.min(axis=0) + win.max(axis=0))*0.5
        return np.r_[predicted - supports, 2*(win_center - target_center),
                     2*((win @ aperture_directions).max(axis=0) - target_window_supports)]

    trials = []
    for yaw, pitch, roll in ((28, 7, 0), (47, 15, -6), (65, 25, 6), (58, -8, -8)):
        fitted = least_squares(residuals, [yaw, pitch, roll, 295, 659, 1060],
                               bounds=([15, -18, -32, 190, 560, 970],
                                       [75, 47, 32, 470, 750, 1140]), max_nfev=120)
        trials.append((float(fitted.cost), fitted.x))
    best = min(trials, key=lambda item: item[0])[1]
    parameters = {
        "azimuth_from_front_toward_minus_x_deg": best[0], "elevation_deg": best[1],
        "clockwise_image_roll_deg": best[2], "scale_px_per_m": best[3]/unit,
        "model_origin_world_m": center.tolist(), "origin_in_page_px": best[4:].tolist(),
        "pose_search_bounds_deg": [[15, 75], [-18, 47], [-32, 32]],
        "outer_support_rms_px": float(np.sqrt(np.mean(residuals(best)[:32]**2))),
        "aperture_box_center_offset_px": (residuals(best)[32:34]/2).tolist(),
    }
    return project(vertices, best), parameters


def raster_masks(points, triangles, opening, refs):
    from PIL import Image, ImageDraw
    from scipy import ndimage

    low = np.floor(np.minimum(points.min(axis=0), CROP[:2])).astype(int) - 5
    high = np.ceil(np.maximum(points.max(axis=0), CROP[2:])).astype(int) + 5
    size = tuple(high - low + 1)
    projected = np.rint(points - low).astype(int)
    img = Image.new("1", size)
    draw = ImageDraw.Draw(img)
    coords = [tuple(map(int, point)) for point in projected]
    for i, j, k in triangles:
        draw.polygon([coords[i], coords[j], coords[k]], fill=1)
    outer = ndimage.binary_fill_holes(np.asarray(img))
    hole = Image.new("1", size)
    ImageDraw.Draw(hole).polygon([coords[i] for i in opening], fill=1)
    placed = []
    x, y = np.array(CROP[:2]) - low
    for mask in refs:
        new = np.zeros(outer.shape, dtype=bool)
        new[y:y+mask.shape[0], x:x+mask.shape[1]] = mask
        placed.append(new)
    return placed, [outer, np.asarray(hole)], low


def edge(mask):
    from scipy import ndimage
    return mask ^ ndimage.binary_erosion(mask)


def compare(ref, model, low, scale):
    from scipy import ndimage

    a, b = edge(ref), edge(model)
    all_values, worst = [], None
    for start, end, direction in ((a, b, "reference_to_model"), (b, a, "model_to_reference")):
        distances, indices = ndimage.distance_transform_edt(~end, return_indices=True)
        samples = distances[start]
        all_values.append(samples)
        src = np.argwhere(start)[samples.argmax()]
        dst = indices[:, src[0], src[1]]
        distance = float(samples.max())
        if worst is None or distance > worst["distance_px"]:
            worst = {
                "direction": direction, "distance_px": distance,
                "from_page_xy": (src[::-1] + low).tolist(), "to_page_xy": (dst[::-1] + low).tolist(),
            }
    vals = np.concatenate(all_values)
    mean = sum(float(v.mean()) for v in all_values)*0.5
    return {
        "reference_area_px": int(ref.sum()), "model_area_px": int(model.sum()),
        "reference_bbox_xyxy": bbox(ref, low), "model_bbox_xyxy": bbox(model, low),
        "occupied_region_iou": np.count_nonzero(ref & model)/np.count_nonzero(ref | model),
        "symmetric_mean_edge_px": mean, "mean_edge_fraction_of_ref_shell_height": mean/scale,
        "pooled_edge_p95_px": float(np.percentile(vals, 95)), "worst_pair": worst,
        "worst_fraction_of_ref_shell_height": worst["distance_px"]/scale,
    }


def bbox(mask, low):
    xy = np.argwhere(mask)[:, ::-1]
    return [*(xy.min(axis=0) + low).tolist(), *(xy.max(axis=0) + low).tolist()]


def diagram(path, refs, models, low, stats):
    from PIL import Image, ImageDraw, ImageFont

    paper, reference, study, shared = "#F6F5EE", "#007A71", "#C24C21", "#333B41"
    def font(size):
        try:
            return ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", size)
        except OSError:
            return ImageFont.load_default()
    out = Image.new("RGB", (1040, 570), paper)
    draw = ImageDraw.Draw(out)
    for idx, (ref, model, label) in enumerate(zip(refs, models, ["Shell silhouette", "Face opening"])):
        a, b = edge(ref), edge(model)
        canvas = Image.new("RGB", (ref.shape[1], ref.shape[0]), paper)
        draw_contours = ImageDraw.Draw(canvas)
        for mask, color in ((a, reference), (b, study), (a & b, shared)):
            draw_contours.point([(int(x), int(y)) for y, x in np.argwhere(mask)], fill=color)
        pair = stats[idx]["worst_pair"]
        frm, to = np.array(pair["from_page_xy"]) - low, np.array(pair["to_page_xy"]) - low
        draw_contours.line([tuple(frm),tuple(to)], fill="#71679C", width=1)
        left, top, right, bottom = bbox(a|b, np.zeros(2, dtype=int))
        cropped = canvas.crop((max(0, left-7), max(0, top-7), min(canvas.width, right+8), min(canvas.height, bottom+8)))
        factor = min(460/cropped.width, 395/cropped.height)
        cropped = cropped.resize((round(cropped.width*factor), round(cropped.height*factor)),
                                 Image.Resampling.LANCZOS)
        cx = 260 + idx*520
        out.paste(cropped, (cx-cropped.width//2, 76+(395-cropped.height)//2))
        draw.text((cx, 19), label, font=font(25), fill=shared, anchor="mt")
        draw.text((cx, 473), f"Max boundary distance  {pair['distance_px']:.1f} px",
                  font=font(17), fill=shared, anchor="mt")
    legend = [("17BHDL-2", reference), ("Saved mesh", study), ("Coincident", shared), ("Largest gap", "#71679C")]
    for i, (label, color) in enumerate(legend):
        x = 70 + i*245
        draw.line((x, 536, x+29, 536), fill=color, width=3)
        draw.text((x+38, 522), label, font=font(18), fill=shared)
    with path.open("xb") as output:
        out.save(output, format="PNG")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ("model", "reference", "blender", "report"):
        parser.add_argument("--"+arg, type=Path, required=True)
    parser.add_argument("--diagram", type=Path)
    parser.add_argument("--refit-camera", action="store_true",
                        help="Refit to this file instead of the documented first-pass pose.")
    args = parser.parse_args()
    if args.report.exists() or (args.diagram and args.diagram.exists()):
        raise FileExistsError("Select unused report and diagram paths")
    args.report.parent.mkdir(parents=True, exist_ok=True)
    if args.diagram:
        args.diagram.parent.mkdir(parents=True, exist_ok=True)
    base_hash = digest(args.model)
    ref_masks = reference_masks(args.reference)
    with tempfile.TemporaryDirectory(prefix="sl17b-projection-") as temporary:
        geometry = Path(temporary) / "mesh.npz"
        subprocess.run([str(args.blender), "--background", "--factory-startup", "--disable-autoexec",
                        "--threads", "2", "--python-exit-code", "3", "--python", str(Path(__file__).resolve()),
                        "--", "--extract", str(args.model.resolve()), str(geometry)], check=True)
        with np.load(geometry) as geo:
            if args.refit_camera:
                uv, params = fit_camera(geo["points"], geo["opening"], *ref_masks)
            else:
                params = dict(LOCKED_PARAMS)
                parameters = [params["azimuth_from_front_toward_minus_x_deg"], params["elevation_deg"],
                              params["clockwise_image_roll_deg"], params["scale_px_per_m"],
                              *params["origin_in_page_px"]]
                uv = project(geo["points"] - params["model_origin_world_m"], parameters)
            refs, projected, low = raster_masks(uv, geo["triangles"], geo["opening"], ref_masks)
    ref_bbox = bbox(refs[0], low)
    height = ref_bbox[3]-ref_bbox[1]+1
    stats = [compare(a,b,low,height) for a,b in zip(refs,projected)]
    result = {
        "model_file": args.model.name, "model_sha256": base_hash,
        "reference": "Kirby Morgan 17B Handle and Weights, document 260519005, 17BHDL-2, isolated CAD illustration",
        "manufacturer_pdf_sha256": "7b07dce3f5d4746e6c93c60fde2e4bbfb60434e1fc9c91f7dbe048601a11da4a",
        "reference_raster_sha256": REFERENCE_SHA, "reference_crop_page_xyxy": list(CROP),
        "reference_shell_height_px": height, "reference_window_to_shell_area": refs[1].sum()/refs[0].sum(),
        "model_window_to_shell_area": projected[1].sum()/projected[0].sum(),
        "method": "Orthographic view fit using 32 shell outer support distances, the 2D window bounding-box center, and eight aperture support distances at double pixel weight to constrain foreshortening and roll. Mean edge, P95 and worst deviations use the whole raster including concavities; these are descriptive in-sample estimates. Silhouette fills through-holes. Aperture comes from the actual outer face-opening loop. X increases right and Y down in page pixels.",
        "limits": "Single uncalibrated illustrated angle, roughly 300 px across the shell. Pose, scale and the mesh are not independently constrained. Raster edges are uncertain by about 1–2 px. No physical dimensions, unobserved rear shape, or fit approval follow from these figures.",
        "camera_mode": "refit_on_input" if args.refit_camera else "first_pass_pose_locked",
        "first_pass_model_sha256": INITIAL_SHA, "camera": params,
        "shell_outer": stats[0], "face_opening": stats[1],
    }
    if base_hash != digest(args.model):
        raise RuntimeError("Source changed during readback")
    if args.diagram:
        diagram(args.diagram, refs, projected, low, stats)
        result["diagram_sha256"] = digest(args.diagram)
    with args.report.open("x", encoding="utf-8") as handle:
        json.dump(result, handle, indent=2)
        handle.write("\n")
    print(json.dumps({key: result[key] for key in ("shell_outer", "face_opening", "camera")}, indent=2))


if __name__ == "__main__":
    if "--" in sys.argv and sys.argv[sys.argv.index("--")+1:][:1] == ["--extract"]:
        model_file, data_file = sys.argv[-2:]
        read_shell(Path(model_file), Path(data_file))
    else:
        main()
