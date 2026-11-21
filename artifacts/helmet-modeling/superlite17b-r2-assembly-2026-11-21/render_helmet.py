"""Read a saved helmet and light it for front, side, rear and underside inspection."""
import argparse
import hashlib
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


VIEWS=(("front","斜前 · 面窗与呼吸总成",(-.85,-1.21,.39)),
       ("side","无阀侧 · 外壳、配重与提手",(1.16,.24,.23)),
       ("rear","阀侧后斜 · 供气入口与后铰",(-1.17,.72,.32)),
       ("below","底部 · 颈套、轭与锁扣",(-.22,-.51,-1.10)))


def layout(directory,output):
    from PIL import Image, ImageDraw, ImageFont
    images=[Image.open(directory/(key+".png")).convert("RGBA") for key,_,_ in VIEWS]
    w,h=images[0].size; margin=16; title=45
    sheet=Image.new("RGB",(2*w+3*margin,2*(h+title)+3*margin),(237,239,237))
    draw=ImageDraw.Draw(sheet)
    size=22 if w<640 else 28
    try: font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",size)
    except OSError: font=ImageFont.load_default(size=size)
    for i,(img,(_,label,_)) in enumerate(zip(images,VIEWS)):
        x=margin+(i%2)*(w+margin); y=margin+(i//2)*(h+title+margin)
        sheet.paste((211,216,215),(x,y,x+w,y+h))
        sheet.paste(img,(x,y),img.getchannel("A"))
        draw.text((x+4,y+h+5),label,font=font,fill=(32,45,49))
    with output.open("xb") as file: sheet.save(file,format="PNG",optimize=True)


def shoot(source,dest,quick):
    import bpy
    from mathutils import Vector
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene=bpy.context.scene
    scene.render.engine="CYCLES"
    scene.cycles.samples=10 if quick else 48
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=8
    scene.cycles.transmission_bounces=6
    scene.render.film_transparent=True
    scene.cycles.film_transparent_glass=False
    scene.render.resolution_x,scene.render.resolution_y=(590,512) if quick else (900,760)
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    scene.view_settings.view_transform="Khronos PBR Neutral"
    scene.view_settings.look="None"
    scene.view_settings.exposure=-1.35
    world=bpy.data.worlds.new("Temporary product studio");scene.world=world;world.use_nodes=True
    nodes=world.node_tree.nodes;links=world.node_tree.links
    tex=nodes.new("ShaderNodeTexCoord");axes=nodes.new("ShaderNodeSeparateXYZ")
    links.new(tex.outputs["Generated"],axes.inputs[0])
    remap=nodes.new("ShaderNodeMapRange");remap.inputs["From Min"].default_value=-1
    links.new(axes.outputs["Z"],remap.inputs["Value"])
    ramp=nodes.new("ShaderNodeValToRGB");links.new(remap.outputs[0],ramp.inputs[0])
    gradient=[(0.,.11),(.23,.23),(.43,.55),(.49,.10),(.54,.15),(.71,.76),(1.,.51)]
    for i,(p,v) in enumerate(gradient):
        if i==0: e=ramp.color_ramp.elements[0]
        elif i==len(gradient)-1: e=ramp.color_ramp.elements[-1]
        else: e=ramp.color_ramp.elements.new(p)
        e.position=p;e.color=(v,v,v,1)
    links.new(ramp.outputs["Color"],nodes["Background"].inputs["Color"])
    nodes["Background"].inputs["Strength"].default_value=.8
    target=Vector((0,-.012,.153))
    for name,loc,power,sx,sy,tint in (
        ("Key",(-.52,-.67,.78),43,.67,.48,(1,.965,.90)),
        ("Cool strip",(.54,-.47,.47),29,.20,.67,(.88,.94,1)),
        ("Rear softbox",(.16,.59,.65),40,.56,.24,(1,.95,.84)),
        ("Front card",(-.13,-.57,.16),3.5,.41,.07,(.96,.99,1)),
        ("Underside",(-.20,-.22,-.34),5,.42,.28,(.85,.89,1))):
        light=bpy.data.lights.new(name,"AREA");light.shape="RECTANGLE"
        light.energy=power;light.size=sx;light.size_y=sy;light.color=tint
        ob=bpy.data.objects.new(name,light);scene.collection.objects.link(ob)
        ob.location=loc;ob.rotation_euler=(target-ob.location).to_track_quat("-Z","Y").to_euler()
    cam=bpy.data.cameras.new("Presentation");camera=bpy.data.objects.new("Presentation",cam)
    scene.collection.objects.link(camera);scene.camera=camera;cam.type="ORTHO"
    cam.clip_start=.001;cam.clip_end=20
    for key,_,look in VIEWS:
        camera.location=target+Vector(look).normalized()*1.05
        camera.rotation_euler=(target-camera.location).to_track_quat("-Z","Y").to_euler()
        cam.ortho_scale=.515 if key!="below" else .54
        scene.render.filepath=str(dest/(key+".png"))
        bpy.ops.render.render(write_still=True)


def main():
    if len(sys.argv)==4 and sys.argv[1]=="--layout":
        layout(Path(sys.argv[2]),Path(sys.argv[3]));return
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model",type=Path,required=True)
    parser.add_argument("--sheet",type=Path,required=True)
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    source,output=args.model.resolve(),args.sheet.resolve()
    if output.exists(): raise FileExistsError(output)
    original=hashlib.sha256(source.read_bytes()).digest()
    output.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="17b-presentation-") as scratch:
        shoot(source,Path(scratch),args.quick)
        python=shutil.which("python") or shutil.which("python3")
        subprocess.run([python,__file__,"--layout",scratch,str(output)],check=True)
    if hashlib.sha256(source.read_bytes()).digest()!=original:raise ValueError("Saved input changed")


if __name__=="__main__": main()
