import assert from 'node:assert/strict';
import {stateAt,packetAt,STARTS,DURATION} from './timeline.mjs';
let checks=0;const check=(f)=>{f();checks++;};
for(const mode of ['air','voice']){
 STARTS[mode].forEach((t,i)=>check(()=>assert.equal(stateAt(mode,t).stage,i)));
 check(()=>assert.equal(stateAt(mode,-2).t,0));check(()=>assert.equal(stateAt(mode,20).t,DURATION));
 check(()=>assert.equal(stateAt(mode,NaN).t,0));check(()=>assert.equal(packetAt(mode,0,0),null));
 check(()=>assert.equal(packetAt(mode,1.99,0),null));
 check(()=>assert.equal(packetAt(mode,2,0).p,0));
 check(()=>assert.equal(stateAt(mode,5.19).arrived,false));check(()=>assert.equal(stateAt(mode,5.2).arrived,true));
 for(let t=0;t<=16;t+=.025)for(let i=0;i<5;i++){const p=packetAt(mode,t,i);if(p)check(()=>assert.ok(p.p>=0&&p.p<=1));}
}
check(()=>assert.equal(stateAt('voice',9).reply,true));check(()=>assert.equal(packetAt('voice',9,0).p,1));
check(()=>assert.ok(Math.abs(packetAt('voice',12.2,0).p)<1e-12));check(()=>assert.equal(packetAt('voice',12.25,0),null));
check(()=>assert.equal(stateAt('voice',14).moving,false));check(()=>assert.equal(packetAt('air',13,0),null));
check(()=>assert.throws(()=>stateAt('unknown',0)));
console.log(JSON.stringify({status:'PASS',checks,notes:'Pure timeline boundaries and packet directions only; browser controls separately tested.'}));
