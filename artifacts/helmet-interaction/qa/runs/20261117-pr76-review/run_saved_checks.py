"""Run read-only saved-model checks into a new directory; keep raw logs local."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--repo', type=Path, required=True)
parser.add_argument('--blender', type=Path, required=True)
parser.add_argument('--output-root', type=Path, required=True)
args = parser.parse_args()
repo = args.repo.resolve()
out = args.output_root.resolve()
out.mkdir(parents=True, exist_ok=False)
stem = repo / 'artifacts/helmet-modeling/superlite17b-2026-11-12'
qa = repo / 'artifacts/helmet-interaction/qa/runs/20261117-pr76-review'
head = '797984ede023e803b641fd46a558baa51e02bb80'
changed = set(subprocess.check_output(['git', 'diff', '--name-only'], cwd=repo, text=True).splitlines())
original = subprocess.check_output(['git', 'diff', '--name-only', 'origin/main...' + head], cwd=repo, text=True).splitlines()
inputs = {p: hashlib.sha256((repo / p).read_bytes()).hexdigest() for p in original if p not in changed}
commands = [
    ('v4', stem / 'audit_v4_baseline.py', ['--output', str(out / 'v4-readback.json')]),
    ('rear', stem / 'check_rear_study.py', ['--study', str(stem / 'sources/superlite17b-rear-study-r1.blend'), '--output', str(out / 'rear')]),
    ('shell', stem / 'shell-study-r2/check_shell_candidate.py', ['--candidate', str(stem / 'sources/superlite17b-shell-study-r2.blend'), '--parameters', str(stem / 'shell-study-r2/candidate-parameters.json'), '--choice', 'moderate', '--output', str(out / 'shell')]),
    ('r3', qa / 'read_r3_inventory.py', ['--repo', str(repo), '--output', str(out / 'r3-inventory.json')]),
]

def run(item):
    name, script, tail = item
    command = [str(args.blender), '--background', '--factory-startup', '--disable-autoexec', '--python-exit-code', '1', '--python', str(script), '--'] + tail
    with (out / (name + '.log')).open('wb') as log:
        result = subprocess.run(command, cwd=repo, stdout=log, stderr=subprocess.STDOUT, timeout=300)
    return {'check': name, 'exit_code': result.returncode, 'natural_exit': True}

with ThreadPoolExecutor(max_workers=2) as pool:
    runs = list(pool.map(run, commands))
mutated = [p for p, digest in inputs.items() if hashlib.sha256((repo / p).read_bytes()).hexdigest() != digest]
summary = {'reviewed_assets_commit': head, 'execution_head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repo, text=True).strip(),
           'extra_tracked_modifications': sorted(changed), 'runs': runs,
           'historical_input_files_hashed': len(inputs), 'historical_input_changes': mutated,
           'scope': 'Saved-model inventory, preserved objects and model triangle checks; no new shape, rendering, Unity, physical fit or device acceptance.'}
if all(r['exit_code'] == 0 for r in runs):
    v4 = json.loads((out / 'v4-readback.json').read_text(encoding='utf-8'))
    rear = json.loads((out / 'rear/report.json').read_text(encoding='utf-8'))
    shell = json.loads((out / 'shell/report.json').read_text(encoding='utf-8'))
    r3 = json.loads((out / 'r3-inventory.json').read_text(encoding='utf-8'))
    saved_v4 = json.loads((stem / 'baseline-v4/readback.json').read_text(encoding='utf-8'))
    assert v4['objects'] == saved_v4['objects']
    summary.update({'blender': v4['blender'], 'v4_objects_match_archived_readback': True,
                    'v4_meshes': v4['meshes'], 'v4_triangles': v4['triangles'],
                    'rear_preserved_parts': rear['preserved_original_part_count'], 'rear_new_parts': len(rear['new_parts']),
                    'rear_new_vs_retained_checks': rear['new_vs_retained_checks'],
                    'rear_tab_pin_gap_model_m': rear['uncompleted_lower_tab_pin_y_gap_model_m'],
                    'shell_fixed_parts': shell['fixed_other_parts'], 'shell_geometry_gate_passed': shell['geometry_gate_passed'],
                    'shell_existing_overlap_candidates': shell['baseline_nonadjacent_self_overlap_pairs'],
                    'r3_inventory': r3})
summary['passed'] = all(r['exit_code'] == 0 for r in runs) and not mutated
with (out / 'summary.json').open('x', encoding='utf-8') as stream:
    stream.write(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2))
assert summary['passed'], 'See new run logs; historical inputs must remain unchanged.'
