"""Original deterministic sound design; NOT historical recordings or voice reconstruction.
48kHz mono PCM16. No actual speech. Silence by default in viewer; user-controlled volume.
"""
import argparse,json,wave,hashlib
from pathlib import Path
import numpy as np
p=argparse.ArgumentParser();p.add_argument('--output',required=True);args=p.parse_args();out=Path(args.output);out.mkdir(parents=True,exist_ok=True)
if (out/'mechanical-air.wav').exists():raise RuntimeError('Audio already exists; protect edits')
sr=48000;duration=16;n=sr*duration;t=np.arange(n)/sr
def softgate(start,end,edge=.12):
 a=np.clip((t-start)/edge,0,1);b=np.clip((end-t)/edge,0,1);return np.sin(a*np.pi/2)**2*np.sin(b*np.pi/2)**2
def tone(at,freq,amp=.10,length=.20):
 u=t-at;env=np.where((u>=0)&(u<length),np.sin(np.clip(u/length,0,1)*np.pi)**2,0);return amp*env*np.sin(2*np.pi*freq*u)
def filtered_noise(seed,low,high):
 rng=np.random.default_rng(seed);a=rng.normal(0,1,n);spec=np.fft.rfft(a);f=np.fft.rfftfreq(n,1/sr);mask=np.exp(-(low/np.maximum(f,1))**4)*np.exp(-(f/high)**4);x=np.fft.irfft(spec*mask,n);return x/max(np.std(x),1e-6)
manifest=[]
for scheme in ['mechanical','clear','gentle']:
 for mode in ['air','voice']:
  x=np.zeros(n);airgate=softgate(2,13);outgate=softgate(2,6.8);replygate=softgate(9,13.8)
  if mode=='air':
   if scheme=='mechanical':
    x+=.018*filtered_noise(3301,100,1400)*airgate*(.65+.35*np.sin(2*np.pi*1.3*t)**2)
    x+=.022*(np.sin(2*np.pi*71*t)+.22*np.sin(2*np.pi*142*t))*airgate
    for s in np.arange(2,12.9,1/1.3):x+=tone(s,280,.06,.07)+tone(s+.008,510,.028,.045)
   elif scheme=='clear':
    x+=.008*filtered_noise(3302,180,950)*airgate
    for s in np.arange(2,12,1.25):x+=tone(s,360,.07,.17)+tone(s+.13,470,.035,.15)
   else:x+=.006*filtered_noise(3303,90,700)*airgate+.011*np.sin(2*np.pi*145*t)*airgate
   for at in [2,5.2,13]:x+=tone(at,430 if scheme!='gentle' else 310,.035 if scheme!='gentle' else .018,.32)
  else:
   if scheme=='mechanical':
    x+=.012*filtered_noise(3341,260,1750)*(outgate+replygate)
    # Filtered syllable-like envelopes, no intelligible or claimed recorded speech.
    for s in [2,2.35,2.8,3.2,9,9.45,9.8,10.3]:x+=tone(s,170,.05,.21)+tone(s,340,.016,.2)
    for at in [2,9]:x+=tone(at,850,.07,.035)
   elif scheme=='clear':
    for s in [2,2.45,2.9]:x+=tone(s,440,.10,.20)+tone(s+.12,550,.07,.20)
    for s in [9,9.45,9.9]:x+=tone(s,550,.10,.20)+tone(s+.12,440,.07,.20)
   else:
    x+=tone(2,330,.033,.45)+tone(2.35,390,.023,.4)+tone(9,390,.033,.45)+tone(9.35,330,.023,.4)
   for at in [5.2,12.2]:x+=tone(at,650 if scheme!='gentle' else 420,.04 if scheme!='gentle' else .020,.27)
  # Remove DC, retain variant loudness difference; hard ceiling is a digital headroom policy only.
  x-=x.mean();peak=float(np.max(np.abs(x)));ceiling=10**(-9/20)
  if peak>ceiling:x*=ceiling/peak
  x[:int(.025*sr)]*=np.linspace(0,1,int(.025*sr));x[-int(.05*sr):]*=np.linspace(1,0,int(.05*sr))
  samples=np.round(x*32767).astype('<i2');f=out/(scheme+'-'+mode+'.wav')
  with wave.open(str(f),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(sr);w.writeframes(samples.tobytes())
  manifest.append({'file':f.name,'scheme':scheme,'mode':mode,'sample_rate':sr,'channels':1,'bits':16,'seconds':16,'peak_dbfs':round(20*np.log10(max(np.max(np.abs(x)),1e-12)),3),'rms_dbfs':round(20*np.log10(max(np.sqrt(np.mean(x*x)),1e-12)),3),'clipped_samples':int(np.sum(abs(samples.astype(np.int32))>=32767)),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'origin':'Original procedural synthesis, seed fixed. Not recordings; no intelligible speech.','physical_sound_pressure':'not measured; dBFS is digital level only'})
(out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n');print(json.dumps(manifest))
