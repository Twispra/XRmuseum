from pathlib import Path
from urllib.parse import urlparse, unquote
from datetime import datetime, timezone
import hashlib, json, re, subprocess
from pypdf import PdfReader

ROOT = Path(r'E:/XRmuseum')
SUB = ROOT/'phase2小组和个人作业'
WORK = Path(__file__).parent
BACKUP = SUB/'原稿备份/20261018-2145-final-review'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def load(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def git(*args): return subprocess.check_output(['git',*args],cwd=ROOT,text=True,encoding='utf-8').strip()

audit = load(WORK/'final-format-audit.json')
build = load(ROOT/'artifacts/phase2-submission/20261018-windows-build/submission-validation.json')
repo = load(WORK/'repository-check.json')
assert repo['visibility']=='public' and not repo['private']
assert repo['mainSha']==build['sourceCommit']=='6088c736e89d05101e09645c1796556012a6fd5d'
assert git('rev-parse','HEAD:MuseumVR')==git('rev-parse','6088c736:MuseumVR')==git('rev-parse','024d039:MuseumVR')
assert (git('rev-parse','6088c736:MuseumVR'))=='eaa3237d73d386dc7b64bfc6c1994078ff2f958d'
assert sha(ROOT/'MuseumVR/ProjectSettings/ProjectSettings.asset')=='81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587'

allowed_changes={
    '蔡子申26134128g-r2kh8sck4y-bot.txt','张非凡26108425g-nikoyeah830-wq.txt',
    'COMP5424-Group2-Phase2-ActivityRecord.docx','COMP5424-Group2-Phase2-ActivityRecord.pdf',
    'COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.docx','COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.pdf'}
originals=load(BACKUP/'backup-manifest.json')
for entry in originals:
    assert sha(BACKUP/entry['name'])==entry['sha256'], 'Backup changed: '+entry['name']
    if entry['name'] not in allowed_changes:
        assert sha(SUB/entry['name'])==entry['sha256'], 'Unexpected submitted-file change: '+entry['name']

readme=(ROOT/'README.md').read_text(encoding='utf-8-sig')
specs=set()
essays=[]
for entry in audit['essays']:
    path=SUB/entry['file']; value=path.read_text(encoding='utf-8-sig')
    assert sha(path)==entry['sha256'] and entry['within750']
    match=re.match(r'(.+?)(\d{8}g)-(.+)\.txt$',path.name)
    assert match, path.name
    name,student,accounts=match.groups()
    assert name in readme and student in readme
    assert all(account in readme for account in accounts.split('&'))
    essays.append(dict(name=name,studentId=student,accounts=accounts.split('&'),file=path.name,words=entry['conservativeWordTokensUrlsCountAsOne'],sha256=entry['sha256']))
    for url in re.findall(r'https?://[^\s)<>]+',value):
        parts=urlparse(url).path.strip('/').split('/')
        if len(parts)>=4 and parts[:2]==['Twispra','XRmuseum']:
            if parts[2]=='commit': specs.add(parts[3])
            elif parts[2]=='blob' and len(parts)>=5: specs.add(parts[3]+':'+unquote('/'.join(parts[4:])))

data=load(ROOT/'artifacts/technical-notes/20261018-build-update/report-content.json')
for ref in data['references']:
    for label,url in ref.get('links',[]):
        parts=urlparse(url).path.strip('/').split('/')
        if parts[2]=='commit': specs.add(parts[3])
        elif parts[2] in ['blob','tree'] and len(parts)>=5: specs.add(parts[3]+':'+unquote('/'.join(parts[4:])))
result=subprocess.run(['git','cat-file','--batch-check'],cwd=ROOT,input='\n'.join(sorted(specs))+'\n',text=True,encoding='utf-8',capture_output=True,check=True)
assert len(result.stdout.splitlines())==len(specs) and not any(line.endswith(' missing') for line in result.stdout.splitlines()), result.stdout

note=SUB/'COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.pdf'
activity=SUB/'COMP5424-Group2-Phase2-ActivityRecord.pdf'
assert len(PdfReader(note).pages)==5 and len(PdfReader(activity).pages)==1
assert sha(note)==sha(ROOT/'artifacts/technical-notes/20261018-build-update/phase2-prototype-design-and-technical-note.en.pdf')
assert sha(activity)==sha(WORK/'phase2-activity-record.pdf')
text='\n'.join(p.extract_text() for p in PdfReader(note).pages)
assert 'References' in PdfReader(note).pages[4].extract_text()
for marker in ['6088c736','024d039','274','74,726,292','40-second','exit -1','PQ604','XR_ERROR_FORM_FACTOR_UNAVAILABLE']:
    assert marker in text, marker
assert 'The latest recorded complete Windows64 Development build is from 806993a' not in text
act_text=' '.join(' '.join(p.extract_text() for p in PdfReader(activity).pages).split())
assert '6088c73' in act_text and '#67 unmerged' not in act_text
assert 'before merging #67' not in act_text and 'build/ZIP verified' in act_text
assert sha(Path(build['zip']))==build['zipSha256']

summary=dict(checkedUtc=datetime.now(timezone.utc).isoformat(), status='materials-aligned; physical-target-environment-acceptance-pending',
             prototypeSource=build['sourceCommit'],editorSuiteSource='024d03942cc93d90453583e1c7e33e4eac96ecf2',
             unitySourceTree=git('rev-parse','6088c736:MuseumVR'), repositoryPublic=True,readmeIdentitiesAllMatchFilenames=True,
             pdfs=[dict(file=note.name,sha256=sha(note),bodyPages=4,referencePages=1,totalPages=5),dict(file=activity.name,sha256=sha(activity),totalPages=1)],
             technicalFormat='A4; 2.5 cm section margins; Times New Roman >=12pt; all page images inspected; body boundary repaired',
             essays=essays, allIndividualEssaysWithin750=True, contributionReview='No overlapping exclusive ownership found. Two minimal factual/scope corrections; four other essays unchanged.',
             fixedGitObjectAndFileTargetsVerified=len(specs),linkScope='All fixed-revision Git targets resolve locally. New report build links use evidence commit d05def503ad286815575ed7038c1f50ddc21dd9c, published through the existing PR branch. PR/issue links retain original traceable repository records.',
             zip=dict(file=Path(build['zip']).name,bytes=build['zipBytes'],sha256=build['zipSha256'],files=274,unchanged=True,crcAndAllInternalManifestHashesPassed=True),
             buildResult='Succeeded; 0 errors / 2 existing optional warnings; Editor natural exit 0',
             startupResult='40-second extracted Player startup observation; no available HMD; test-owned Player actively terminated, exit -1; natural Player exit not verified',
             backup=str(BACKUP), allBackupFilesMatchOriginalManifest=True,originalWorkspaceSettingsPreserved=True,
             notSubmitted='Canvas upload/text entry has not been performed.',
             pending='PQ604A/B/C actual hardware/software, complete headset/controller journey, stereo rendering, performance, comfort, accessibility and user research remain unverified.')
(WORK/'final-validation.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps(dict(status=summary['status'],essays={e['name']:e['words'] for e in essays},fixedGitTargets=len(specs),pdfHashes={p['file']:p['sha256'] for p in summary['pdfs']},zipUnchanged=True),ensure_ascii=True,indent=2))
