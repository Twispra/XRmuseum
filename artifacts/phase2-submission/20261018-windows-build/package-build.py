import hashlib
import json
import struct
import zipfile
from datetime import datetime, timezone
from pathlib import Path

BASE = Path(r'E:\Codex\Temp\phase2-windows-20261018')
EVIDENCE = BASE / 'evidence'
RUNTIME = Path(r'E:\Codex\Builds\XRmuseum\phase2-20261018-6088c73')
DESTINATION = Path(r'E:\XRmuseum\phase2小组和个人作业')
ZIP_PATH = DESTINATION / 'COMP5424-Group2-Phase2-Windows-x64-6088c73.zip'
EXTRACT = BASE / 'zip-extracted'
ROOT = 'XRmuseum-Phase2-Windows-x64'
COMMIT = '6088c736e89d05101e09645c1796556012a6fd5d'

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')

def listing(folder):
    return [dict(path=f.relative_to(folder).as_posix(), bytes=f.stat().st_size, sha256=sha(f))
            for f in sorted(folder.rglob('*')) if f.is_file()]

process = json.loads((EVIDENCE / 'build-process.json').read_text(encoding='utf-8-sig'))
reports = list((EVIDENCE / 'qa').glob('*/report.json'))
assert len(reports) == 1
report = json.loads(reports[0].read_text(encoding='utf-8-sig'))
assert process['state'] == 'exited' and process['exitCode'] == 0 and not process['forced']
assert process['sourceDifferenceCount'] == 0 and not process['dirtyAfter']
assert report['status'] == 'passed' and report['buildResult'] == 'Succeeded' and report['totalErrors'] == 0
assert report['commit'] == process['commit'] == COMMIT
assert all(x['unchanged'] for x in report['sources'])
assert not ZIP_PATH.exists() and not EXTRACT.exists(), 'Use fresh package and extraction paths'
assert (RUNTIME / 'README-START-HERE.txt').is_file(), 'Write verified operation instructions first'
assert (RUNTIME / 'MuseumVR.exe').is_file()
assert (RUNTIME / 'UnityPlayer.dll').is_file()
assert (RUNTIME / 'MonoBleedingEdge').is_dir()
assert (RUNTIME / 'MuseumVR_Data' / 'Managed' / 'Assembly-CSharp.dll').is_file()
assert (RUNTIME / 'MuseumVR_Data' / 'Plugins' / 'x86_64' / 'UnityOpenXR.dll').is_file()
assert (RUNTIME / 'MuseumVR_Data' / 'UnitySubsystems' / 'UnityOpenXR' / 'UnitySubsystemsManifest.json').is_file()
exe = RUNTIME / 'MuseumVR.exe'
with exe.open('rb') as stream:
    assert stream.read(2) == b'MZ'
    stream.seek(0x3c)
    offset = struct.unpack('<I', stream.read(4))[0]
    stream.seek(offset)
    assert stream.read(4) == b'PE\x00\x00'
    machine = struct.unpack('<H', stream.read(2))[0]
    assert machine == 0x8664, 'Executable is not Windows x64'
assert sha(exe) == report['exeSha256']

runtime_files = listing(RUNTIME)
save(EVIDENCE / 'runtime-files-before-packaging.json', runtime_files)
manifest = dict(project='XRmuseum', course='COMP5424', group=2, phase=2,
                sourceCommit=COMMIT, repository='https://github.com/Twispra/XRmuseum',
                unity=report['unity'], target=report['target'], buildOptions=report['options'],
                buildUtc=report['utc'], buildResult=report['buildResult'],
                buildErrors=report['totalErrors'], buildWarnings=report['totalWarnings'],
                scene=report['scene'], executable='MuseumVR.exe',
                fileCountExcludingThisManifest=len(runtime_files),
                bytesExcludingThisManifest=sum(x['bytes'] for x in runtime_files),
                manifestNote='This manifest lists all other package files. It intentionally excludes itself.',
                verificationLimit='Package integrity and a separate local launch smoke check are recorded externally. PQ604 PCs and physical headset/controller operation require on-site verification.',
                files=runtime_files)
save(RUNTIME / 'PACKAGE-MANIFEST.json', manifest)
all_files = listing(RUNTIME)
with zipfile.ZipFile(ZIP_PATH, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for entry in all_files:
        archive.write(RUNTIME / entry['path'], f"{ROOT}/{entry['path']}")

with zipfile.ZipFile(ZIP_PATH) as archive:
    bad = archive.testzip()
    assert bad is None, f'CRC failed: {bad}'
    names = archive.namelist()
    assert len(names) == len(set(names)) == len(all_files)
    assert all(n.startswith(ROOT + '/') and '..' not in Path(n).parts for n in names)
    archive.extractall(EXTRACT)
extracted_files = listing(EXTRACT / ROOT)
assert all_files == extracted_files, 'Extracted package differs in paths, sizes, or SHA-256 hashes'
verification = dict(status='passed', utc=datetime.now(timezone.utc).isoformat(), sourceCommit=COMMIT,
                    zip=str(ZIP_PATH), zipBytes=ZIP_PATH.stat().st_size, zipSha256=sha(ZIP_PATH),
                    runtime=str(RUNTIME), extractedRuntime=str(EXTRACT / ROOT),
                    files=len(all_files), uncompressedBytes=sum(x['bytes'] for x in all_files),
                    zipCrcPassed=True, extractedFileHashesAllMatched=True, windowsPeMachine=hex(machine),
                    unityBuildReport=str(reports[0]), runtimeFiles=all_files)
save(EVIDENCE / 'package-verification.json', verification)
print(json.dumps({k:v for k,v in verification.items() if k != 'runtimeFiles'}, ensure_ascii=False, indent=2))
