"""Per-iteration fixed three-view sheets; originals stay in private local output."""
import argparse,json
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import binary_erosion

def edge(im):
 a=np.asarray(im.convert('RGBA'));b=(a[:,:,0]>220)&(a[:,:,3]>220);return b&~binary_erosion(b)
def overlay(image,mask,color):
 a=np.array(image.convert('RGB'));a[mask]=color;return Image.fromarray(a)
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--metrics',required=True);ap.add_argument('--references',required=True);ap.add_argument('--private-output',required=True);ap.add_argument('--public-output',required=True);a=ap.parse_args()
 here=Path(__file__).resolve().parent;root=here.parents[3];old=root/'artifacts/helmet-modeling/superlite17b-2026-11-12/shell-rebuild-r3';data=json.loads(Path(a.metrics).read_text());pub=Path(a.public_output).resolve();pri=Path(a.private_output).resolve();pub.mkdir(parents=True,exist_ok=False);pri.mkdir(parents=True,exist_ok=False)
 font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20);large=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',26)
 for item in data['iterations']:
  masks=Path(item['renders']);id=item['id'];folder=pub/id;folder.mkdir();private=pri/id;private.mkdir()
  for key,photo in [('side','04_side_rear_view.png'),('34','01_overview_front34.png'),('front',None)]:
   base=Image.open(old/'baseline'/('base_'+key+'.png')).convert('RGB');newmask=Image.open(masks/(key+'-visible.png')).convert('RGBA');full=Image.open(masks/(key+'-full.png')).convert('RGBA');bmask=Image.open(old/'baseline'/('base_'+key+'_shell_full.png')).convert('RGBA')
   blank=Image.new('RGB',base.size,'#eef2f6');newgray=Image.new('RGB',base.size,'#777d85');newgray.paste(Image.new('RGB',base.size,'#ecd96c'),mask=newmask.getchannel('A'))
   # Actual visible coverage is labelled as coverage, never passed off as a beauty render.
   a0=np.array(newgray);rgba=np.array(newmask);a0[(rgba[:,:,0]<30)&(rgba[:,:,3]>220)]=[80,85,90];newgray=Image.fromarray(a0)
   before=overlay(base,edge(bmask),[255,140,0]);newgray=overlay(newgray,edge(full),[220,30,90])
   reference=Image.new('RGB',base.size,'white')
   if key=='side':reference=Image.open(old/'reference-side-visible-mask.png').convert('RGB')
   elif key=='34':
    ann=json.loads((old.parent/'camera-baseline-r1/annotations-front.json').read_text(encoding='utf-8'));d=ImageDraw.Draw(reference)
    for line in ann['polylines']:d.line([tuple(p) for p in line['pass_1_px']],fill='#0099bb',width=3)
   else:ImageDraw.Draw(reference).text((30,100),'未提供正前实物照片',font=large,fill='black')
   for private_mode in [False,True]:
    r=reference
    if private_mode and photo:r=Image.open(Path(a.references)/photo).convert('RGB')
    sheet=Image.new('RGB',(1830,850),'white');draw=ImageDraw.Draw(sheet)
    draw.text((20,12),'r3 '+id+' / '+key+' / 未通过全部验收',font=large,fill='#7c1439')
    status=f"IoU {item['visible_iou']:.4f}   均偏 {item['symmetric_boundary_deviation_over_L']*100:.2f}%L   壳形差 {item['full_shell_geometry_xor_over_union_vs_backup']*100:.2f}%   窗框方向差 {item['frame_direction_difference_to_confirmed_target_deg']:.2f}°"
    draw.text((20,50),status,font=font,fill='black')
    for x,title,img in [(10,'参考照片' if private_mode and photo else '参考可见mask/局部描线',r),(620,'固定相机备份版',before),(1230,'新版实际覆盖渲染（非材质图）',newgray)]:
     draw.text((x,95),title,font=font,fill='black');img=img.copy();img.thumbnail((590,670),Image.Resampling.LANCZOS);sheet.paste(img,(x,140))
    sheet.save((private if private_mode else folder)/(key+'-three-columns.png'))
 print('R3_ITERATION_SHEETS_COMPLETE '+str(len(data['iterations']))+' iterations x3 views; source photos private')
if __name__=='__main__':main()
