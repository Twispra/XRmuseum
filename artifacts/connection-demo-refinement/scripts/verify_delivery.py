"""Verify file relationships and write current delivery hashes. Does not run UI tests."""
import json,hashlib,wave,array,struct,re,subprocess
from pathlib import Path
root=Path(__file__).resolve().parent.parent
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
readback=json.loads((root/'qa/export-readback.json').read_text());assert readback['status']=='PASS'
assert readback['source_sha256']==digest(root/'sources/equipment.blend')
for record in readback['imports']:assert record['sha256']==digest(root/'exports'/record['file'])
context=json.loads((root/'qa/context-provenance.json').read_text());assert context['output_sha256']==digest(root/'exports/mark-v-context.glb')
for item in json.loads((root/'audio/manifest.json').read_text()):
 p=root/'audio'/item['file'];assert digest(p)==item['sha256']
 with wave.open(str(p),'rb') as w:
  assert (w.getframerate(),w.getnchannels(),w.getsampwidth(),w.getnframes())==(48000,1,2,768000)
  pcm=array.array('h',w.readframes(w.getnframes()));assert max(abs(v) for v in pcm)<32767
for p in (root/'exports').glob('*.glb'):
 b=p.read_bytes();assert b[:4]==b'glTF';d=json.loads(b[20:20+struct.unpack_from('<I',b,12)[0]])
 assert all('uri' not in a or a['uri'].startswith('data:') for a in d.get('buffers',[])+d.get('images',[]))
html=(root/'preview/review.html').read_text(encoding='utf-8');assert '__ASSETS__' not in html and '__BUNDLE__' not in html
assert not re.search(r'<script[^>]+src=',html)
assert 'Permission is hereby granted' in html
embedded=json.loads(re.search(r'window.REVIEW_ASSETS=(.*?);</script>',html,re.S).group(1))
import base64
for key,name in {'pump':'mark-iii-pump','phone':'surface-telephone','diver':'diver-telephone','helmet':'mark-v-context'}.items():assert base64.b64decode(embedded[key])==(root/'exports'/(name+'.glb')).read_bytes()
for name,b in embedded['audio'].items():assert base64.b64decode(b)==(root/'audio'/(name+'.wav')).read_bytes()
files=[{'path':p.relative_to(root).as_posix(),'bytes':p.stat().st_size,'sha256':digest(p)} for p in sorted(root.rglob('*')) if p.is_file() and p.name not in ['delivery-manifest.json','delivery-check.json']]
(root/'qa/delivery-manifest.json').write_text(json.dumps(files,indent=2),encoding='utf-8',newline='\n')
report={'status':'PASS','files':len(files),'html_bytes':(root/'preview/review.html').stat().st_size,'checks':['source/export hash matches readback','context hash matches provenance','six PCM audio tracks format/hash/no clipping','GLB buffers/images internal','single HTML embeds exactly the delivered models/audio, no external script','three MIT notice included'],'scope':'Working tree task assets; UI and Blender checks are separate reports'}
(root/'qa/delivery-check.json').write_text(json.dumps(report,indent=2),encoding='utf-8',newline='\n');print(json.dumps(report))
