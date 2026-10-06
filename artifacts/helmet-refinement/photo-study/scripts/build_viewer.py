"""Bundle standalone HTML. Dependencies: three 0.180.0, esbuild 0.25.10.

Set NODE_PATH to your temporary npm installation, pass --esbuild path/to/esbuild.
The dependencies are review-tool-only; do not add them to Unity Packages.
"""
import argparse, subprocess, tempfile
from pathlib import Path

parser=argparse.ArgumentParser();parser.add_argument('--esbuild',required=True)
args=parser.parse_args();folder=Path(__file__).resolve().parent
with tempfile.TemporaryDirectory(prefix='helmet-review-') as temporary:
    bundle=Path(temporary)/'bundle.js'
    subprocess.run([args.esbuild,str(folder/'viewer.js'),'--bundle','--format=iife','--minify','--outfile='+str(bundle)],check=True)
    content=(folder/'viewer-template.html').read_text(encoding='utf-8').replace('__BUNDLE__',bundle.read_text(encoding='utf-8').replace('</script','<\\/script'))
    # Trailing shader whitespace has no meaning; omit it from generated review diffs.
    content='\n'.join(line.rstrip() for line in content.splitlines())+'\n'
    (folder.parent/'preview/review.html').write_text(content,encoding='utf-8')
