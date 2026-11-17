"""Check fresh outputs and refusal guards with a synthetic private-preview fixture."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--repo', type=Path, required=True)
parser.add_argument('--blender', type=Path, required=True)
parser.add_argument('--output-root', type=Path, required=True)
args = parser.parse_args()
repo = args.repo.resolve()
out = args.output_root.resolve()
out.mkdir(parents=True, exist_ok=False)
source = repo / 'artifacts/helmet-modeling/superlite17b-2026-11-12/shell-rebuild-r3'
fixture = out / 'fixture'
fixture.mkdir()
paths = ['measure_r3_baseline.py', 'annotations-side-r3.json', 'reference-side-visible-mask.png',
         'baseline/base_side_shell_visible.png', 'baseline/baseline-cameras-and-inventory.json', 'baseline/base_side.png']
original_hashes = {p: hashlib.sha256((source / p).read_bytes()).hexdigest() for p in paths}
for p in paths:
    (fixture / p).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source / p, fixture / p)
refs = out / 'synthetic-refs'
refs.mkdir()
preview = refs / '04_side_rear_view.png'
shutil.copyfile(source / 'baseline/base_side.png', preview)
annotation = json.loads((fixture / 'annotations-side-r3.json').read_text(encoding='utf-8'))
annotation['image']['sha256'] = hashlib.sha256(preview.read_bytes()).hexdigest()
(fixture / 'annotations-side-r3.json').write_text(json.dumps(annotation), encoding='utf-8')
command = [sys.executable, str(fixture / 'measure_r3_baseline.py'), '--reference-root', str(refs)]
runs = []

def run(name, result_dir, private_dir, expected_error=None):
    result = subprocess.run(command + ['--output-root', str(result_dir), '--local-previews', str(private_dir)], capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=60)
    (out / (name + '.log')).write_text(result.stdout + result.stderr, encoding='utf-8')
    passed = result.returncode == 0 if expected_error is None else result.returncode != 0 and expected_error in result.stderr
    assert passed, name
    runs.append({'check': name, 'exit_code': result.returncode, 'expected_failure': expected_error is not None, 'passed': True})

run('fresh-output', out / 'results', out / 'private')
for p in ['baseline-metrics.json', 'confirmation-controls.json', 'keypoints-confirmation.png', 'baseline/base_side_visible_binary_mask.png']:
    assert (out / 'results' / p).is_file()
actual = json.loads((out / 'results/baseline-metrics.json').read_text(encoding='utf-8'))
expected = json.loads((source / 'baseline-metrics.json').read_text(encoding='utf-8'))
assert actual == expected
run('existing-output', out / 'results', out / 'new-private', 'FileExistsError')
assert not (out / 'new-private').exists()
run('nested-private', out / 'nested', out / 'nested/private', 'ValueError')
assert not (out / 'nested').exists()
preview.write_bytes(preview.read_bytes() + b'fixture-change')
run('wrong-reference-hash', out / 'wrong-hash', out / 'wrong-private', 'Reference image SHA-256 differs')
assert not (out / 'wrong-hash').exists() and not (out / 'wrong-private').exists()
preview.unlink()
run('missing-reference', out / 'missing', out / 'missing-private', 'FileNotFoundError')
assert not (out / 'missing').exists() and not (out / 'missing-private').exists()

protected = source.parent / 'baseline-v4/readback.json'
before = hashlib.sha256(protected.read_bytes()).hexdigest()
audit = source.parent / 'audit_v4_baseline.py'
result = subprocess.run([str(args.blender), '--background', '--factory-startup', '--disable-autoexec', '--python-exit-code', '1', '--python', str(audit), '--', '--output', str(protected)], capture_output=True, timeout=60)
(out / 'existing-v4-report.log').write_bytes(result.stdout + result.stderr)
assert result.returncode == 1 and b'Refusing to overwrite report' in result.stdout + result.stderr
assert hashlib.sha256(protected.read_bytes()).hexdigest() == before
runs.append({'check': 'existing-v4-report', 'exit_code': result.returncode, 'expected_failure': True, 'passed': True})
assert all(hashlib.sha256((source / p).read_bytes()).hexdigest() == digest for p, digest in original_hashes.items())
summary = {'checks': runs, 'fresh_metrics_equal_archived': True, 'source_files_unchanged': True,
           'fixture': 'Private preview image is an existing own-model render; only the isolated fixture annotation hash is changed. Numeric masks/camera data remain the archived inputs.',
           'limits': 'Tests output routing and input-identity guards. Not a replay of source B segmentation, nor a new physical-accuracy acceptance.'}
(out / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
print(json.dumps(summary, indent=2))
