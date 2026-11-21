"""Build the editable 500-010 exhibit assembly around the approved, saved shell."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
import breathing_system
import interior_comms
import neck_and_weights
import port_and_handle


SHELL_NAME="SL17B_YellowShell_Study"
APPROVED_SHA256="800f5175684cf9a31f83344bb03c16af2a079f7e77391a27ca1b17bdbccf777e"


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--shell",required=True,type=Path)
    parser.add_argument("--output",required=True,type=Path)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    src,out=args.shell.resolve(),args.output.resolve()
    if out.exists() or out.suffix.lower()!=".blend":
        raise ValueError("Choose a new .blend output.")
    if hashlib.sha256(src.read_bytes()).hexdigest()!=APPROVED_SHA256:
        raise ValueError("Expected the approved R2 bare shell.")
    bpy.ops.wm.open_mainfile(filepath=str(src))
    shell=bpy.data.objects[SHELL_NAME]
    if len(bpy.data.objects)!=1: raise ValueError("Expected the one-object shell scene")
    bpy.ops.object.select_all(action="DESELECT")
    port=port_and_handle.build(shell)
    bpy.context.view_layer.update()
    neck_and_weights.build(shell)
    bpy.context.view_layer.update()
    interior_comms.build(shell)
    bpy.context.view_layer.update()
    air=breathing_system.build(shell,port)
    scene=bpy.context.scene
    # Store design datums as scene metadata. QA reads the actual saved meshes.
    scene["assembly_reference"]="SuperLite 17B 500-010, KM drawing 241108001; chrome bent tube 555-155 variant"
    scene["shell_input_sha256"]=APPROVED_SHA256
    scene["whisker_seats_m"]=json.dumps(port["kidney_pads"])
    scene["tube_seats_mm"]=json.dumps(air["bent_tube_seats_mm"])
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active=shell
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                space=area.spaces.active
                space.region_3d.view_rotation=Vector((.68,1.15,-.28)).to_track_quat("-Z","Y")
                space.region_3d.view_location=(0,-.012,.153)
                space.region_3d.view_distance=.73
                space.clip_start=.001
                space.overlay.show_overlays=False
                space.shading.type="MATERIAL"
                space.shading.studio_light="studio.exr"
    bpy.context.preferences.filepaths.save_version=0
    out.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out),compress=True)
    print("Saved",out.name,"with",len(scene.objects),"objects")


if __name__=="__main__": main()
