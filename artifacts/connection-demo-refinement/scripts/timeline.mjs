// Deterministic review timeline. Seconds are exhibit timing, NOT transmission physics.
export const DURATION=16;
export const STARTS={air:[0,2,5.2,13],voice:[0,2,7,9,14]};
export function stateAt(mode,time){
 if(!STARTS[mode])throw new Error('Unknown demonstration');
 const t=Math.max(0,Math.min(DURATION,Number.isFinite(time)?time:0));
 const starts=STARTS[mode];let stage=0;for(let i=1;i<starts.length;i++)if(t>=starts[i])stage=i;
 const reply=mode==='voice'&&stage===3;
 const moving=mode==='air'?t>=2&&t<13:stage===1||stage===3;
 const emissionStart=reply?9:2;
 const arrived=moving&&t>=emissionStart+3.2;
 return {mode,t,stage,reply,moving,emissionStart,arrived,done:t===DURATION,stageStart:starts[stage],stageEnd:starts[stage+1]??DURATION};
}
export function packetAt(mode,time,index){
 const s=stateAt(mode,time);if(!s.moving)return null;
 if(mode==='air'){
  const emitted=time-2-index*.55;if(emitted<0)return null;
  const p=(emitted%3.2)/3.2;return {p,reply:false};
 }
 const elapsed=time-s.emissionStart-index*.25;
 if(elapsed<0||elapsed>3.2)return null;
 return {p:s.reply?1-elapsed/3.2:elapsed/3.2,reply:s.reply};
}
