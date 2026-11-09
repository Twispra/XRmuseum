from pathlib import Path
import hashlib, json, shutil
from pypdf import PdfReader

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
TECH=ROOT/'artifacts/technical-notes/20261018-build-update'
SUB=ROOT/'phase2小组和个人作业'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

source=ROOT/'docs/activity/2026-10-18-phase2-final-submission-audit.md'
guide=source.read_text(encoding='utf-8-sig').replace('(../external-resources.md)','(../docs/external-resources.md)').replace('(../../artifacts/','(../artifacts/')
(SUB/'提交清单与一致性核查.md').write_text(guide,encoding='utf-8',newline='\n')
shutil.copyfile(ROOT/'artifacts/phase2-submission/20261018-readme-activity/template-package-inventory.json',WORK/'template-package-inventory.json')
pdf=TECH/'phase2-prototype-design-and-technical-note.en.pdf'
data=json.loads((WORK/'final-validation.json').read_text(encoding='utf-8'))
export=dict(sourceCommit=data['prototypeSource'],validationCommit=data['editorSuiteSource'],identicalCommittedUnityTree=data['unitySourceTree'],
            bodyPages=4,referencesPages=1,totalPages=len(PdfReader(pdf).pages),font='Times New Roman',minimumVisibleFontPt=12,
            marginsCm=[2.5,2.5,2.5,2.5],pdf=str(pdf.relative_to(ROOT)),submissionCopy=str((SUB/pdf.name).relative_to(ROOT)),
            pdfSha256=sha(pdf),pdfBytes=pdf.stat().st_size,docxSha256=sha(SUB/'COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.docx'),
            englishManuscriptSha256=sha(ROOT/'docs/phase2-prototype-design-and-technical-note.en.md'),
            references=12,versionBoundBuildEvidenceCommit='d05def503ad286815575ed7038c1f50ddc21dd9c',fixedGitTargetsInReportAndEssays=43,
            renderMethod='Native Microsoft Word ExportAsFixedFormat, packaged render_docx.py rasterization with bundled Poppler',
            visualReview='All five technical-note pages and the one activity-record page inspected by root and independently reviewed; no layout defects.',
            formattingAudit='artifacts/phase2-submission/20261018-final-review/final-format-audit.json',
            verificationScope='Existing Editor suite evidence retained at 024d039. New Windows build and limited 40-second extracted Player observation at 6088c736; no new Unity execution during the document update. Physical PQ604/headset acceptance remains pending.',
            oldCanonicalPdfPreserved=sha(ROOT/'artifacts/technical-notes/20261018/phase2-prototype-design-and-technical-note.en.pdf')=='ec22b0effe718f049863894bc5f56470e8029aaf28337c0d5e5646a34f769b80')
export['submissionCopy']='phase2小组和个人作业/COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.pdf'
(TECH/'export-validation.json').write_text(json.dumps(export,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
print('Saved portable submission guide, template inventory and final export provenance.')
