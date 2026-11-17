"""Verify the five saved LFS sources against the exact reviewed Git pointers."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--repo', type=Path, required=True)
parser.add_argument('--commit', required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
stem = 'artifacts/helmet-modeling/superlite17b-2026-11-12/'
paths = [
    'baseline-v4/superlite17b-shell-v4.blend',
    'sources/superlite17b-rear-study-r1.blend',
    'sources/superlite17b-shell-study-r2.blend',
    'shell-rebuild-r3/backups/backup_r1_current.blend',
    'shell-rebuild-r3/work/work_r3.blend',
]
rows = []
for path in paths:
    relative = stem + path
    pointer = subprocess.check_output(['git', 'show', args.commit + ':' + relative], cwd=args.repo).decode()
    fields = dict(line.split(' ', 1) for line in pointer.strip().splitlines())
    assert fields['version'] == 'https://git-lfs.github.com/spec/v1'
    data = (args.repo / relative).read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    assert len(data) == int(fields['size'])
    assert digest == fields['oid'].removeprefix('sha256:')
    assert not data.startswith(b'version https://git-lfs.github.com/spec/')
    rows.append({'path': relative, 'bytes': len(data), 'sha256': digest, 'expanded_and_matches_pointer': True})
assert len({row['sha256'] for row in rows[-3:]}) == 1
report = {'reviewed_commit': args.commit, 'files': rows, 'r2_backup_work_byte_identical': True,
          'scope': 'Exact saved sources and LFS expansion only; does not prove geometry accuracy or Unity integration.'}
args.output.parent.mkdir(parents=True, exist_ok=True)
with args.output.open('x', encoding='utf-8') as stream:
    stream.write(json.dumps(report, indent=2) + '\n')
print('ASSET_POINTER_CHECK_OK 5 expanded sources; r2/backup/work identical')
