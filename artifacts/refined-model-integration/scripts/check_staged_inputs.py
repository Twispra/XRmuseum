"""Verify staged Unity inputs against the recorded successful editor runs.

Run after staging product files, before committing. Only Git line-ending
normalisation and Git LFS pointers may differ from the tested working bytes.
The output path must be new; old evidence is never overwritten.
"""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--output', required=True)
args = p.parse_args()
repo = Path(__file__).resolve().parents[3]
output = Path(args.output).resolve()
if output.exists():
    raise SystemExit('Choose a new output; existing evidence is preserved.')
index = json.loads((repo / 'artifacts/refined-model-integration/qa/integration-validation.json').read_text(encoding='utf-8-sig'))
assert index['status'] == 'passed'
target = json.loads((repo / index['reports'][0]['report']).read_text(encoding='utf-8-sig'))
last = json.loads((repo / index['laterTestRunnerValidatedBy']).read_text(encoding='utf-8-sig'))
later = {f['path']: f['sha256'] for f in last['provenance']['files']}
records = []
for f in target['files']:
    path = f['path']
    expected = later[path] if path in index['laterTestRunnerOnlyChanges'] else f['sha256']
    working = (repo / path).read_bytes()
    assert hashlib.sha256(working).hexdigest() == expected, 'Working input changed: ' + path
    staged = subprocess.check_output(['git', 'show', ':' + path], cwd=repo)
    if staged.startswith(b'version https://git-lfs.github.com/spec/v1\n'):
        assert b'oid sha256:' + expected.encode() + b'\n' in staged, 'LFS input changed: ' + path
        representation = 'Git LFS pointer to tested bytes'
    else:
        assert staged.replace(b'\r\n', b'\n') == working.replace(b'\r\n', b'\n'), 'Staged input changed: ' + path
        representation = 'identical' if staged == working else 'Git line-ending normalisation only'
    records.append({'path': path, 'testedSha256': expected, 'indexRepresentation': representation})
result = {'status': 'passed', 'verifiedInputs': len(records), 'records': records,
          'scope': 'Staged content identity; functional results remain in the referenced Unity reports.'}
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print('PASS: staged inputs match tested content:', len(records))
