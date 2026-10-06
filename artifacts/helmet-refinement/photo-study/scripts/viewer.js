// Standalone asset review tool. No Unity runtime or historical simulation.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';

const canvas=document.querySelector('canvas'),status=document.querySelector('#status');
const renderer=new THREE.WebGLRenderer({canvas,antialias:true});
renderer.setPixelRatio(Math.min(devicePixelRatio,2));
renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.15;
const scene=new THREE.Scene();scene.background=new THREE.Color('#172631');
const camera=new THREE.PerspectiveCamera(40,1,.002,50);
const controls=new OrbitControls(camera,canvas);controls.enableDamping=true;
const pmrem=new THREE.PMREMGenerator(renderer),room=new RoomEnvironment();
const environment=pmrem.fromScene(room,.04);scene.environment=environment.texture;room.dispose();pmrem.dispose();
scene.add(new THREE.HemisphereLight(0xc6e2ff,0x182431,1.4));
const key=new THREE.DirectionalLight(0xffe5bf,3);key.position.set(.7,1,1);scene.add(key);
const loader=new GLTFLoader();let model=null,centre=new THREE.Vector3(),span=.5;
function fit(direction=new THREE.Vector3(.8,.35,1.3)) {
  direction.normalize();camera.position.copy(centre).addScaledVector(direction,span*1.9);
  controls.target.copy(centre);controls.minDistance=span*.08;controls.maxDistance=span*8;controls.update();
}
function disposeModel() {
  if(!model)return;scene.remove(model);const materials=new Set(),textures=new Set();
  model.traverse(ob=>{ob.geometry?.dispose();if(ob.material)for(const m of (Array.isArray(ob.material)?ob.material:[ob.material]))materials.add(m);});
  for(const m of materials){for(const value of Object.values(m))if(value?.isTexture)textures.add(value);m.dispose();}
  for(const t of textures)t.dispose();model=null;
}
function accept(gltf,label) {
  const box=new THREE.Box3().setFromObject(gltf.scene),size=box.getSize(new THREE.Vector3());
  if(box.isEmpty()||![...size].every(Number.isFinite))throw new Error('模型为空或边界无效');
  disposeModel();model=gltf.scene;scene.add(model);box.getCenter(centre);span=Math.max(...size);fit();
  let triangles=0,parts=0;model.traverse(ob=>{if(ob.isMesh){parts++;triangles+=(ob.geometry.index?.count??ob.geometry.attributes.position.count)/3;}});
  document.querySelector('#component').value='all';status.textContent=`${label} · ${parts} 部件 · ${Math.round(triangles).toLocaleString()} 三角形`;
  document.documentElement.dataset.modelLoaded='true';
}
async function parse(buffer,label){status.textContent='正在载入模型…';try{accept(await loader.parseAsync(buffer,''),label);}catch(error){status.textContent='载入失败：'+error.message;document.documentElement.dataset.modelLoaded='error';}}
document.querySelector('#file').addEventListener('change',async e=>{const file=e.target.files[0];if(file)await parse(await file.arrayBuffer(),file.name);});
const views={overview:[.8,.35,1.3],front:[0,.05,1],rear:[0,.12,-1],left:[-1,.12,0]};
document.querySelectorAll('[data-view]').forEach(b=>b.addEventListener('click',()=>fit(new THREE.Vector3(...views[b.dataset.view]))));
document.querySelector('#component').addEventListener('change',e=>{
  if(!model)return;const selected=e.target.value;
  model.traverse(ob=>{if(!ob.isMesh)return;let match=selected==='all';for(let p=ob;p;p=p.parent)if(p.name.includes(selected))match=true;ob.visible=match;});
  const box=new THREE.Box3();model.updateMatrixWorld(true);
  model.traverse(ob=>{if(ob.isMesh&&ob.visible){const bounds=new THREE.Box3().setFromBufferAttribute(ob.geometry.attributes.position).applyMatrix4(ob.matrixWorld);box.union(bounds);}});
  if(!box.isEmpty()){box.getCenter(centre);span=Math.max(...box.getSize(new THREE.Vector3()));fit(new THREE.Vector3(...(selected.includes('Rear')?views.rear:selected.includes('Front')?views.front:views.overview)));}
});
function resize(){const w=canvas.clientWidth,h=canvas.clientHeight;renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix();}
new ResizeObserver(resize).observe(canvas);resize();fit();
renderer.setAnimationLoop(()=>{controls.update();renderer.render(scene,camera);});
if(location.protocol!=='file:'){
  fetch('../exports/photo-helmet-study.glb').then(r=>{if(!r.ok)throw new Error('模型文件未找到');return r.arrayBuffer();}).then(buffer=>parse(buffer,'整体研究样件')).catch(()=>{status.textContent='请点击“选择 GLB”载入 exports 文件夹中的模型。';});
}
