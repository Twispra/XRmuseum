// Independent asset review only. No Unity runtime bindings.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
const canvas=document.querySelector('canvas'),status=document.querySelector('#status');
const renderer=new THREE.WebGLRenderer({canvas,antialias:true});renderer.setPixelRatio(Math.min(devicePixelRatio,2));
renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.0;
const scene=new THREE.Scene();scene.background=new THREE.Color('#18232c');
const camera=new THREE.PerspectiveCamera(38,1,.002,50),controls=new OrbitControls(camera,canvas);controls.enableDamping=true;
const pmrem=new THREE.PMREMGenerator(renderer),room=new RoomEnvironment(),environment=pmrem.fromScene(room,.04);scene.environment=environment.texture;room.dispose();pmrem.dispose();
scene.add(new THREE.HemisphereLight(0xdce9f5,0x202326,1));const key=new THREE.DirectionalLight(0xffe5c9,2.4);key.position.set(.7,1,1);scene.add(key);
const loader=new GLTFLoader();let model=null,centre=new THREE.Vector3(),span=.5,door=null,opening=0,openTarget=0,shellHidden=false;
const views={overview:[.85,.43,1.3],front:[0,.06,1],rear:[0,.2,-1],left:[1,.06,0],right:[-1,.06,0]};
function fit(direction=new THREE.Vector3(...views.overview)){
  direction.normalize();camera.position.copy(centre).addScaledVector(direction,span*1.85);controls.target.copy(centre);controls.minDistance=span*.10;controls.maxDistance=span*8;controls.update();
}
function disposeModel(){
  if(!model)return;scene.remove(model);const ms=new Set(),ts=new Set();model.traverse(ob=>{ob.geometry?.dispose();if(ob.material)for(const m of Array.isArray(ob.material)?ob.material:[ob.material])ms.add(m);});
  for(const m of ms){for(const v of Object.values(m))if(v?.isTexture)ts.add(v);m.dispose();}for(const t of ts)t.dispose();model=null;
}
function accept(gltf,label){
  const box=new THREE.Box3().setFromObject(gltf.scene),size=box.getSize(new THREE.Vector3());if(box.isEmpty()||![...size].every(Number.isFinite))throw new Error('模型边界无效');
  disposeModel();model=gltf.scene;scene.add(model);box.getCenter(centre);span=Math.max(...size);fit();
  let triangles=0,count=0;model.traverse(ob=>{if(ob.name==='FrontWindowPivot')door=ob;if(ob.isMesh){count++;triangles+=(ob.geometry.index?.count??ob.geometry.attributes.position.count)/3;}});
  opening=0;openTarget=0;shellHidden=false;document.querySelector('#component').value='all';document.querySelector('#door').textContent='打开前窗';document.querySelector('#shell').textContent='查看内部示意';
  document.querySelector('#door').disabled=!door;
  status.textContent=`${label} · ${count} 分件 · ${Math.round(triangles).toLocaleString()} 三角形`;document.documentElement.dataset.modelLoaded='true';
}
async function parse(buffer,label){status.textContent='正在载入模型…';door=null;try{accept(await loader.parseAsync(buffer,''),label);}catch(e){status.textContent='载入失败：'+e.message;document.documentElement.dataset.modelLoaded='error';}}
document.querySelector('#file').addEventListener('change',async e=>{const f=e.target.files[0];if(f)await parse(await f.arrayBuffer(),f.name);});
function filter(){
  if(!model)return;const selected=document.querySelector('#component').value;const box=new THREE.Box3();model.updateMatrixWorld(true);
  document.querySelector('#note').textContent=shellHidden||selected==='InteriorInterpretiveAssembly'?'内部管路与电话件为史料支持的示意，具体形状和走线未由该件内部照片验证。':'参考1944年馆藏实物；局部尺寸、旧化和开门角度为建模估算。';
  model.traverse(ob=>{if(!ob.isMesh)return;let match=selected==='all',isShell=false;for(let p=ob;p;p=p.parent){if(p.name===selected)match=true;if(p.name==='ShellAssembly')isShell=true;}
    ob.visible=match&&!(shellHidden&&isShell);if(ob.visible)box.union(new THREE.Box3().setFromBufferAttribute(ob.geometry.attributes.position).applyMatrix4(ob.matrixWorld));
  });
  if(!box.isEmpty()){box.getCenter(centre);span=Math.max(...box.getSize(new THREE.Vector3()));fit(new THREE.Vector3(...(selected.includes('Connection')?views.rear:selected.includes('Telephone')?views.left:views.overview)));}
}
document.querySelector('#component').addEventListener('change',filter);
document.querySelectorAll('[data-view]').forEach(b=>b.addEventListener('click',()=>fit(new THREE.Vector3(...views[b.dataset.view]))));
document.querySelector('#door').addEventListener('click',()=>{openTarget=openTarget?0:THREE.MathUtils.degToRad(100);document.querySelector('#door').textContent=openTarget?'关闭前窗':'打开前窗';document.documentElement.dataset.doorOpen=String(Boolean(openTarget));});
document.querySelector('#shell').addEventListener('click',()=>{shellHidden=!shellHidden;document.querySelector('#shell').textContent=shellHidden?'恢复外壳':'查看内部示意';document.documentElement.dataset.shellHidden=String(shellHidden);filter();document.querySelector('#note').textContent=shellHidden?'内部管路与电话件为史料支持的示意，具体形状和走线未由该件内部照片验证。':'参考1944年馆藏实物；局部尺寸、旧化和开门角度为建模估算。';});
document.querySelector('#reset').addEventListener('click',()=>{shellHidden=false;openTarget=0;document.querySelector('#component').value='all';document.querySelector('#door').textContent='打开前窗';document.querySelector('#shell').textContent='查看内部示意';document.querySelector('#note').textContent='参考1944年馆藏实物；局部尺寸、旧化和开门角度为建模估算。';filter();document.documentElement.dataset.doorOpen='false';document.documentElement.dataset.shellHidden='false';});
function resize(){const w=canvas.clientWidth,h=canvas.clientHeight;renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix();}new ResizeObserver(resize).observe(canvas);resize();fit();
const clock=new THREE.Clock();renderer.setAnimationLoop(()=>{const dt=Math.min(clock.getDelta(),.1);opening=THREE.MathUtils.damp(opening,openTarget,8,dt);if(door)door.rotation.y=opening;controls.update();renderer.render(scene,camera);});
if(location.protocol!=='file:')fetch('../exports/mark-v-mod1.glb').then(r=>{if(!r.ok)throw new Error('文件未找到');return r.arrayBuffer();}).then(b=>parse(b,'Mark V Mod.1')).catch(()=>{status.textContent='选择 exports 中的 GLB 文件。';});
