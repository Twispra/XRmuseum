"""Build a self-contained review HTML. No network requests or Unity project edits.
Pass --esbuild and --node-modules to existing local tools; no automatic installs.
"""
import argparse,base64,json,os,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--esbuild',required=True);p.add_argument('--node-modules',required=True);a=p.parse_args()
root=Path(__file__).resolve().parent.parent
env=os.environ.copy();env['NODE_PATH']=a.node_modules
with tempfile.TemporaryDirectory() as d:
 out=Path(d)/'viewer.js'
 subprocess.run([a.esbuild,str(root/'scripts/viewer.js'),'--bundle','--minify','--format=iife','--target=es2020','--legal-comments=inline','--outfile='+str(out)],env=env,check=True)
 assets={k:base64.b64encode((root/'exports'/v).read_bytes()).decode() for k,v in {'pump':'mark-iii-pump.glb','phone':'surface-telephone.glb','diver':'diver-telephone.glb','helmet':'mark-v-context.glb'}.items()}
 assets['audio']={f.stem:base64.b64encode(f.read_bytes()).decode() for f in sorted((root/'audio').glob('*.wav'))}
 html=(root/'scripts/viewer-template.html').read_text(encoding='utf-8').replace('__ASSETS__',json.dumps(assets,separators=(',',':'))).replace('__BUNDLE__',out.read_text(encoding='utf-8').replace('</script','<\\/script'))
 (root/'preview').mkdir(exist_ok=True);(root/'preview/review.html').write_text('\n'.join(line.rstrip() for line in html.splitlines())+'\n',encoding='utf-8',newline='\n')
 license_file=Path(a.node_modules)/'three/LICENSE'
 html_path=root/'preview/review.html';html_path.write_text('<!-- Three.js 0.180.0: '+license_file.read_text(encoding='utf-8')+' -->\n'+html_path.read_text(encoding='utf-8'),encoding='utf-8',newline='\n')
 (root/'preview/three-LICENSE.txt').write_text(license_file.read_text(encoding='utf-8'),encoding='utf-8',newline='\n')
 print(json.dumps({'html_bytes':html_path.stat().st_size,'models':list(assets)[:-1],'audio_tracks':len(assets['audio']),'network_dependencies':0,'three':'0.180.0'}))
