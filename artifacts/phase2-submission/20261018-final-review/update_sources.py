from pathlib import Path
import hashlib, json, shutil

ROOT = Path(r'E:/XRmuseum')
SUBMISSION = ROOT / 'phase2小组和个人作业'
BACKUP = SUBMISSION / '原稿备份/20261018-2145-final-review'
WORK = ROOT / 'artifacts/technical-notes/20261018-build-update'
REVIEW = Path(__file__).parent
BUILD_EVIDENCE_COMMIT = 'd05def503ad286815575ed7038c1f50ddc21dd9c'

backup = {row['name']: row for row in json.loads((BACKUP/'backup-manifest.json').read_text(encoding='utf-8-sig'))}
changes = []
def replace_personal(name, old, new):
    path = SUBMISSION / name
    assert hashlib.sha256(path.read_bytes()).hexdigest() == backup[name]['sha256'], 'Concurrent change: '+name
    value = path.read_text(encoding='utf-8-sig')
    assert value.count(old) == 1
    value = value.replace(old, new)
    path.write_text(value, encoding='utf-8', newline='\n')
    changes.append(dict(file=name, beforeSha256=backup[name]['sha256'], afterSha256=hashlib.sha256(path.read_bytes()).hexdigest(), old=old, new=new))

replace_personal('蔡子申26134128g-r2kh8sck4y-bot.txt',
    '[PR #67](https://github.com/Twispra/XRmuseum/pull/67) proposes an independent left-plinth exhibit with rotation controls and three-language text, but remains unmerged pending the group’s layout decision.',
    '[PR #67](https://github.com/Twispra/XRmuseum/pull/67) subsequently merged on 18 October as an independent left-plinth exhibit with rotation controls and three-language text. My initial exhibit work is distinct from [Twispra’s follow-up confirmation/cancellation, occlusion and panel-placement repairs](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/activity/2026-10-18-pr67-review-and-merge.md). The wider layout in [Issue #64](https://github.com/Twispra/XRmuseum/issues/64) remains under review.')
replace_personal('张非凡26108425g-nikoyeah830-wq.txt',
    'added a summonable panel that faces the viewer once, remains fixed, blocks toggling during a drag and preserves exhibit state.',
    'added a summonable panel that faces the viewer once, remains fixed and preserves exhibit state, with initial guards for helmet and section drags.')
(REVIEW/'personal-text-revisions.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2),encoding='utf-8')

data = json.loads((ROOT/'artifacts/technical-notes/20261018/report-content.json').read_text(encoding='utf-8'))
data['subtitle'] = 'COMP5424 Extended Reality | Group 2 | 18 October 2026 | Final build update'
data['version'] = 'Prototype and ZIP source 6088c736 | Editor suites at 024d039'
data['pages'][0]['blocks'][3]['rows'][-1] = [
    'Evaluable delivery',
    'Public repository, six member identities, source, Windows ZIP and version-bound reports. [3,7,9,11]',
    'Build/package checks passed; PQ604 and headset acceptance remain pending.'
]
page = data['pages'][2]
page['blocks'] = [
    {'type':'p','text':'The prototype and ZIP use repository revision 6088c736, including the left exhibit. Its committed MuseumVR tree is identical to frozen Editor validation source 024d039 (tree eaa3237d). The table retains archived suite results; those suites were not rerun for this update. The new build used a clean worktree, excluding the pre-existing local ProjectSettings change. [3,6,7,9]'},
    page['blocks'][1],
    {'type':'p','text':'Each Editor process exited naturally with code 0 and left 1,299 project files unchanged. Synthetic devices exercised production Input Actions in saved scenes. Counts include repeated text, glyph and fingerprint assertions; they are not participants or independent usability tasks and must not be summed. Monocular captures do not establish binocular readability or physical reach. [7]'},
    {'type':'p','text':'Historical runs at 7e5b539 include 31 direct-contact checks for the default era and specified surfaces, and 318 isolated-comparison checks. Comparison inputs are unchanged, but that suite was not rerun after left-plinth integration; coverage remains version-specific. [8]'},
    {'type':'h2','text':'Build and real-device evidence'},
    {'type':'p','text':'A clean 6088c736 Windows x64 Development/Mono build on 18 October succeeded with zero errors and two existing optional OpenXR warnings; the Editor exited naturally with code 0. All 1,299 project files remained unchanged. The ZIP contains 272 Unity outputs plus instructions and a manifest: 274 files, 74,726,292 bytes. CRC and extracted-file SHA-256 checks passed. [9]'},
    {'type':'p','text':'The extracted Player completed a 40-second local startup observation: Mono, D3D11, input and scene scripts initialized without unexpected managed exceptions. No headset was available; OpenXR reported XR_ERROR_FORM_FACTOR_UNAVAILABLE. The hidden-window probe ended by terminating its own Player process (exit -1). Natural Player shutdown, stereo rendering and VR interaction were not verified. PQ604 acceptance remains pending. [9]'},
    {'type':'p','text':'Earlier Quest 3/Virtual Desktop feedback reported improved head rotation/viewing height and, in an earlier hand build, grabbing, hand-gap and joystick problems. Later repairs still need a complete headset retest. This is formative developer feedback, not participant research or approval of compatibility with the course’s SteamVR/Meta Quest Link lab environment. [2,10]'}
]
data['pages'][3]['blocks'][2]['rows'][0] = [
    'Before submission',
    'Use the packaged 6088c736 build to complete the core journey on PQ604 with the approved runtime.',
    'Record headset/controller tracking, complete-flow outcome, hardware/runtime, errors and target-device performance.'
]
data['references'][8] = {
    'id':9,
    'text':'Windows x64 build, complete ZIP and limited desktop startup observation, source 6088c736, 18 October 2026. Package SHA-256: 3914fb87cb3e1974…096b1cdbdfd4f102 (full value in manifest).',
    'links':[
        ['Build and observation record',f'https://github.com/Twispra/XRmuseum/blob/{BUILD_EVIDENCE_COMMIT}/docs/activity/2026-10-18-phase2-windows-submission.md'],
        ['Package verification',f'https://github.com/Twispra/XRmuseum/blob/{BUILD_EVIDENCE_COMMIT}/artifacts/phase2-submission/20261018-windows-build/package-verification.json']
    ]
}
(WORK/'report-content.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
builder = (ROOT/'artifacts/technical-notes/20261018/build_report.py').read_text(encoding='utf-8')
builder = builder.replace("WORK = ROOT / 'artifacts/technical-notes/20261018'", "WORK = Path(__file__).resolve().parent")
builder = builder.replace("normal.paragraph_format.line_spacing = 1.04", "normal.paragraph_format.line_spacing = 1.04\nnormal.paragraph_format.right_indent = Cm(0.12)")
builder = builder.replace("p.paragraph_format.line_spacing=1.0", "p.paragraph_format.line_spacing=1.0\n            p.paragraph_format.right_indent=Pt(0)")
(WORK/'build_report.py').write_text(builder,encoding='utf-8',newline='\n')

activity = (ROOT/'artifacts/phase2-submission/20261018-readme-activity/fill_activity_record.py').read_text(encoding='utf-8')
activity = activity.replace('WORK = ROOT / "artifacts/phase2-submission/20261018-readme-activity"', 'WORK = Path(__file__).resolve().parent')
activity = activity.replace('22 Sep - 18 Oct: #1, #2, #24, #49, #52, #58; later integration repairs.', '22 Sep - 18 Oct: #1, #2, #24, #49, #52, #58, #67; follow-up repairs.')
activity = activity.replace('13 - 18 Oct: #51, #53, #66, #69; Twispra follow-up repairs. #67 unmerged.', '13 - 18 Oct: #51, #53, #66, #69; #67 merged after Twispra repairs.')
activity = activity.replace('#35, #44, #69; photo study stays separate pending #64/#67.', 'Central #35/#44/#69; #67 adds a separate left study; full #64 layout open.')
activity = activity.replace('Final build / PQ604 compatibility', 'PQ604 / complete VR walkthrough')
activity = activity.replace('Build/ZIP final revision; run full lab journey before Phase 2 submission.', '18 Oct: 6088c73 build/ZIP verified; complete the headset journey on PQ604.')
activity = activity.replace('Left-plinth placement / historical limits', 'Full display layout / historical limits')
activity = activity.replace('Review #64 before merging #67; improve reference evidence.', '#67 merged; review the wider #64 layout and uncertain historical details.')
activity = activity.replace('    "0109e3f": "0109e3f75019f585454da638b56101f235d40770",', '    "6088c73": "6088c736e89d05101e09645c1796556012a6fd5d",')
activity = activity.replace('0109e3f|d84c93f', '6088c73|d84c93f')
activity = activity.replace('main: 0109e3f', 'Prototype: 6088c73')
activity = activity.replace('inventory = json.loads((WORK / "template-package-inventory.json").read_text(encoding="utf-8"))', 'inventory = json.loads((ROOT / "artifacts/phase2-submission/20261018-readme-activity/template-package-inventory.json").read_text(encoding="utf-8"))')
(REVIEW/'fill_activity_record.py').write_text(activity,encoding='utf-8',newline='\n')
print('Updated two personal factual boundaries and prepared new report/activity builders; old canonical reports preserved.')
