from pathlib import Path
import hashlib, json

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
PREVIOUS=ROOT/'artifacts/technical-notes/20261018-build-update'
BACKUP=ROOT/'phase2小组和个人作业/原稿备份/20261018-2156-page3-concise'
manifest=json.loads((BACKUP/'backup-manifest.json').read_text(encoding='utf-8-sig'))
for entry in manifest:
    assert hashlib.sha256((BACKUP/entry['backup']).read_bytes()).hexdigest()==entry['sha256']
    assert hashlib.sha256(Path(entry['source']).read_bytes()).hexdigest()==entry['sha256'], 'Source changed after backup'
old=json.loads((PREVIOUS/'report-content.json').read_text(encoding='utf-8'))
data=json.loads(json.dumps(old))
data['pages'][2]={
    'heading':'3 Prototype checks and Windows build',
    'blocks':[
        {'type':'p','text':'During integration, we used the saved MuseumStart scene to check that the features still worked together. We concentrated on actions a visitor would take: changing an era, inspecting a component, recalling the settings panel and recovering the exhibit or viewpoint. The linked reports retain the detailed checks and their versions. [6,7,8]'},
        {'type':'table','widths':[3.7,12.3],'headers':['Area','What we checked and changed'],'rows':[
            ['Central exhibit','Era changes kept the visible helmet, captions and hotspots aligned. Editor interaction checks also covered fixed-centre rotation, the window, section controls and reset. Direct-contact coverage was narrower and focused on the default helmet. [7,8]'],
            ['Left display and settings','We checked button selection, cancellation and panel placement. Integration repairs blocked selection through solid objects and kept the panel clear of the added model. [6,7]'],
            ['Language','Saved translations and glyphs were checked against runtime text as the language changed. This checks whether text is present; translation quality and headset readability remain evaluation tasks. [7,12]']
        ]},
        {'type':'p','text':'These checks helped us find integration faults before packaging. They used simulated inputs and project data, so they do not show whether visitors can find the controls unaided or understand the content.'},
        {'type':'h2','text':'The submitted Windows build'},
        {'type':'p','text':'We built revision 6088c736 for Windows x64 using the agreed Unity version. The build completed without errors, with two existing optional OpenXR migration warnings. We included the runtime dependencies and startup instructions, then extracted the ZIP and verified that the packaged files matched the build output. [9]'},
        {'type':'p','text':'The extracted application stayed responsive during a 40-second desktop startup check and reached scene initialization. No headset was available, so OpenXR did not establish a headset session. We actively stopped the test process afterward; normal shutdown and VR operation were not verified. A full walkthrough on PQ604A/B/C with a headset and controllers is still required. [9]'},
        {'type':'p','text':'Earlier Quest 3 development feedback helped us identify viewing-height, hand-control and joystick problems. Those findings concern earlier builds; the current combined prototype still needs a complete headset retest for navigation, reach and comfort. [10]'}
    ]
}
assert all(data['pages'][i]==old['pages'][i] for i in [0,1,3])
assert data['references']==old['references']
(WORK/'report-content.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
(WORK/'build_report.py').write_text((PREVIOUS/'build_report.py').read_text(encoding='utf-8'),encoding='utf-8',newline='\n')
print('Prepared page 3 revision; other body pages and references are unchanged.')
