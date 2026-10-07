"""Build self-contained HTML with locally installed three.js/esbuild.
Dependencies are review tools, never Unity Packages. Keep the MIT notice.
"""
import argparse,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--esbuild',required=True);args=p.parse_args();here=Path(__file__).resolve().parent
with tempfile.TemporaryDirectory(prefix='mark-v-review-') as temp:
    bundle=Path(temp)/'bundle.js'
    subprocess.run([args.esbuild,str(here/'viewer.js'),'--bundle','--format=iife','--minify','--outfile='+str(bundle)],check=True)
    content=(here/'viewer-template.html').read_text(encoding='utf-8').replace('__BUNDLE__',bundle.read_text(encoding='utf-8').replace('</script','<\\/script'))
    folder=here.parent/'preview';folder.mkdir(exist_ok=True)
    (folder/'review.html').write_text('\n'.join(line.rstrip() for line in content.splitlines())+'\n',encoding='utf-8')
