from pathlib import Path
from urllib.parse import urlparse, unquote
import hashlib, json, shutil, subprocess
from pypdf import PdfReader

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
SUB=ROOT/'phase2小组和个人作业'
BACKUP=SUB/'原稿备份/20261018-2156-page3-concise'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def load(path): return json.loads(path.read_text(encoding='utf-8-sig'))
manifest=load(BACKUP/'backup-manifest.json')
for entry in manifest: assert sha(BACKUP/entry['backup'])==entry['sha256']
previous=load(ROOT/'artifacts/technical-notes/20261018-build-update/report-content.json')
current=load(WORK/'report-content.json')
assert all(previous['pages'][i]==current['pages'][i] for i in [0,1,3])
assert previous['references']==current['references']
pdf=SUB/'COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.pdf'
old=PdfReader(BACKUP/pdf.name); new=PdfReader(pdf)
assert len(new.pages)==5
assert all(old.pages[i].extract_text()==new.pages[i].extract_text() for i in [0,1,3,4])
page3=' '.join(new.pages[2].extract_text().split())
for term in ['6088c736','40-second','No headset was available','We actively stopped','normal shutdown and VR operation were not verified','PQ604A/B/C']:
    assert term in page3, term
for removed in ['128,389','1,299','74,726,292','eaa3237d','31 direct-contact','318 isolated']:
    assert removed not in page3, removed

urls=[]; targets=[]
for ref in current['references']:
    for label,url in ref.get('links',[]):
        urls.append(url); parts=urlparse(url).path.strip('/').split('/')
        if parts[2]=='commit': targets.append(parts[3])
        elif parts[2] in ['blob','tree']: targets.append(parts[3]+':'+unquote('/'.join(parts[4:])))
batch=subprocess.run(['git','cat-file','--batch-check'],cwd=ROOT,text=True,input='\n'.join(sorted(set(targets)))+'\n',encoding='utf-8',capture_output=True,check=True)
assert not any(line.endswith(' missing') for line in batch.stdout.splitlines())

prior_audit=load(ROOT/'artifacts/phase2-submission/20261018-final-review/final-validation.json')
for essay in prior_audit['essays']: assert sha(SUB/essay['file'])==essay['sha256']
activity=SUB/'COMP5424-Group2-Phase2-ActivityRecord.pdf'
assert sha(activity)==prior_audit['pdfs'][1]['sha256']
assert sha(SUB/prior_audit['zip']['file'])==prior_audit['zip']['sha256']
assert sha(ROOT/'MuseumVR/ProjectSettings/ProjectSettings.asset')=='81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587'
review=load(WORK/'independent-review.json')
shutil.copyfile(pdf,WORK/'phase2-prototype-design-and-technical-note.en.pdf')

guide=(BACKUP/'提交清单与一致性核查.md').read_text(encoding='utf-8-sig')
guide=guide.replace('# 2026-10-18 Phase 2 最终提交材料核查', '# 2026-10-18 Phase 2 最终提交材料核查\n\n同日文稿精简：仅技术说明第 3 页改为围绕小组交互检查、整合修复与构建限制的叙述；正文仍为四页，参考文献另页。下列技术 PDF 指纹已更新，活动 PDF、六篇个人文案和 ZIP 未改变。')
guide=guide.replace('a0f1da738bd8f77ab452612f24113f4fc0f3d137bf0d2653a75245ac826c7a2d',sha(pdf))
guide=guide.replace('技术说明的 801／271／11／128,389／989 继续对应既有 **024d039 Editor 套件**；没有重跑或汇总成参与者数量。','第 3 页已将测试统计改为核心交互检查摘要，详细的 801／271／11／128,389／989 等数据保留在 **024d039 Editor 套件**的仓库证据中；没有重跑或汇总成参与者数量。')
guide=guide.replace('新规范副本位于 `artifacts/technical-notes/20261018-build-update/`','构建更新稿保留于 `artifacts/technical-notes/20261018-build-update/`；当前精简稿位于 `artifacts/technical-notes/20261018-concise/`')
guide+='\n\n本次第 3 页精简及格式复核见 [修订记录](../docs/activity/2026-10-18-phase2-page3-concise.md)；当前 PDF 的规范副本与源数据见 [精简稿目录](../artifacts/technical-notes/20261018-concise/)。旧技术稿、源文件及本清单已另备份于 `原稿备份/20261018-2156-page3-concise/`。\n'
(SUB/'提交清单与一致性核查.md').write_text(guide,encoding='utf-8',newline='\n')

validation=dict(sourceCommit=prior_audit['prototypeSource'],editorEvidenceSource=prior_audit['editorSuiteSource'],
                change='Only technical-report page 3 rewritten; no product changes or new product tests.',
                backup=str(BACKUP),backupFilesVerified=len(manifest),oldPdfSha256=sha(BACKUP/pdf.name),
                pdf='artifacts/technical-notes/20261018-concise/phase2-prototype-design-and-technical-note.en.pdf',
                submissionCopy=str(pdf.relative_to(ROOT)),pdfSha256=sha(pdf),pdfBytes=pdf.stat().st_size,
                docxSha256=sha(SUB/'COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.docx'),
                manuscriptSha256=sha(ROOT/'docs/phase2-prototype-design-and-technical-note.en.md'),
                bodyPages=4,referencesPages=1,totalPages=5,font='Times New Roman',minimumVisibleFontPt=12,marginsCm=[2.5]*4,
                unchangedPages=[1,2,4,5],unchangedPagesTextMatches=True,
                visualReview='All five page PNGs inspected by root and independent reviewer; unchanged pages also compared pixel by pixel.',
                independentReview='artifacts/technical-notes/20261018-concise/independent-review.json',
                references=12,uniqueVersionedLinks=len(set(urls)),fixedGitTargetsResolve=True,
                activityPdfUnchanged=True,allSixPersonalTextsUnchanged=True,zipUnchanged=True,originalSettingsPreserved=True,
                limitationsRetained='No available headset during desktop startup; process actively stopped; normal shutdown, VR operation and PQ604 walkthrough remain unverified.')
(WORK/'export-validation.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({k:validation[k] for k in ['pdfSha256','pdfBytes','bodyPages','referencesPages','unchangedPages','uniqueVersionedLinks','zipUnchanged']},ensure_ascii=True,indent=2))
