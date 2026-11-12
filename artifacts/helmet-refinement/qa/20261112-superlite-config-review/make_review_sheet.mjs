import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const dir=path.dirname(fileURLToPath(import.meta.url));
const require=createRequire(process.env.MUSEUM_NODE_MODULES?path.join(process.env.MUSEUM_NODE_MODULES,'_review.cjs'):import.meta.url);
const sharp=require('sharp');
const r=JSON.parse(fs.readFileSync(path.join(dir,'model-readback.json'),'utf8'));
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;');
const t=(x,y,txt,size=22,fill='#17283b',weight=400)=>`<text x="${x}" y="${y}" font-size="${size}" fill="${fill}" font-weight="${weight}">${esc(txt)}</text>`;
let body=`<rect width="1600" height="1260" fill="#f4f6f8"/>`;
body+=t(36,52,'SuperLite 17B | 现有 v2 的后部结构复核',34,'#10253a',700);
body+=t(36,86,'2026-11-12 · #25 · 从保存源重新渲染 · 原资产未修改 · 非实物测绘 / 非 Unity / 非头显证据',20,'#52687b');
const views=[['rear','背面'],['side','侧面'],['rear_threequarter','侧后']];
views.forEach(([key,label],i)=>{
 const x=30+i*520,y=130,w=500,h=409.09;
 body+=`<rect x="${x}" y="108" width="500" height="439" rx="10" fill="white"/>`+t(x+14,132,label,22,'#15334d',700);
 body+=`<image x="${x}" y="145" width="${w}" height="${h}" href="data:image/png;base64,${fs.readFileSync(path.join(dir,key+'.png')).toString('base64')}"/>`;
 for(const [id,txt,color] of [['A_hinge','A','#126a99'],['B_rear_weight','B','#a46412'],['C_neck_yoke','C','#7253aa']]){
 const p=r.annotation_projections[key][id]; const px=x+p.x_px*w/1100,py=145+p.y_px*h/900;
 body+=`<circle cx="${px}" cy="${py}" r="16" fill="${color}" stroke="white" stroke-width="3"/>`+t(px-8,py+8,txt,23,'white',700);
 }
});
body+=t(36,595,'A  后铰接区',23,'#126a99',700)+t(36,628,'现有：简化矩形块与横销。',21)+t(36,658,'待细化：套筒挂接与后铰片。',21);
body+=t(556,595,'B  后配重',23,'#a46412',700)+t(556,628,'现有：左右薄片式外观。',21)+t(556,658,'待核：后侧轮廓与中心挂接区。',21);
body+=t(1076,595,'C  颈夹 / 轭背景',23,'#7253aa',700)+t(1076,628,'已有几何，保留接口作参照。',21)+t(1076,658,'当前斜角与尺寸仍属估算。',21);
body+=`<rect x="30" y="704" width="1540" height="268" rx="14" fill="#e6eef4"/>`;
body+=t(52,745,'有资料支持的连接关系（概念图，不按比例）',25,'#16364f',700);
const boxes=[['后配重',55,225],['530-090 / 550-339',340,360],['后铰片 / 540-157',760,345],['颈夹组件',1165,365]];
for(const [label,x,w] of boxes){body+=`<rect x="${x}" y="776" width="${w}" height="66" rx="10" fill="white" stroke="#9bb0c3"/>`+t(x+16,817,label,23,'#15334d',600);}
for(const x of [293,713,1118])body+=t(x,817,'→',31,'#126a99',700);
body+=t(55,885,'套筒的外端凸缘为后铰片提供挂接；它与颈夹 / 轭自身的横向铰接是两个连接。',22);
body+=t(55,925,'依据：厂家颈夹手册 #260319004，17BNK-5–8；后铰片公告 #06/2006。',20,'#52687b');
body+=t(36,1021,'通信配置：500-010 为研究目标；现有教学端点不能证明已复原穿壳通信柱。',24,'#16364f',700);
body+=t(36,1062,'500-010 / 500-011 是销售配置区别；维修手册同时允许通信柱和 / 或可选水密接头。',22);
body+=t(36,1101,'仍缺：同配置背照、生产批次、安装坐标与实测尺寸。优先做后部连接研究副本，再评审接入。',22);
body+=t(36,1172,'A / B / C 为现有网格投影包围盒中心，仅辅助定位；通信柱未用假想位置标注。',19,'#52687b');
body+=t(36,1210,'原图、逐对象清单、资料页码与哈希见同目录 model-readback.json / sources.json 及专项报告。',19,'#52687b');
const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1260" font-family="Microsoft YaHei, SimHei, sans-serif">${body}</svg>`;
await sharp(Buffer.from(svg)).png().toFile(path.join(dir,'review-sheet.png'));
console.log('Review sheet rendered: 1600x1260');
