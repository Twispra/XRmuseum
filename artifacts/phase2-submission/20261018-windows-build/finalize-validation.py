import hashlib
import json
import re
import shutil
from datetime import datetime, timezone
from pathlib import Path

BASE = Path(r'E:\Codex\Temp\phase2-windows-20261018')
EVIDENCE = BASE / 'evidence'
REPO = Path(r'E:\XRmuseum')
PUBLISH = REPO / 'artifacts/phase2-submission/20261018-windows-build'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')

process = read(EVIDENCE / 'build-process.json')
package = read(EVIDENCE / 'package-verification.json')
smoke = read(EVIDENCE / 'player-smoke-process.json')
build_path = Path(package['unityBuildReport'])
build = read(build_path)
log_path = EVIDENCE / 'player-smoke.log'
log = log_path.read_text(encoding='utf-8-sig', errors='replace')
assert process['exitCode'] == 0 and not process['forced'] and process['sourceDifferenceCount'] == 0
assert build['status'] == 'passed' and build['totalErrors'] == 0
assert package['status'] == 'passed' and package['zipCrcPassed'] and package['extractedFileHashesAllMatched']
assert smoke['aliveAfterObservation'] and smoke['responded'] and smoke['observedSeconds'] >= 40
assert 'Initialize engine version: 2022.3.62f3c1' in log
assert 'Direct3D 11.0' in log
assert 'MUSEUM_HAND_DEVICE_DIAGNOSTICS' in log
exception_pattern = re.compile(r'\b(?:NullReferenceException|MissingReferenceException|MissingMethodException|TypeLoadException|InvalidOperationException|Argument(?:Null|OutOfRange)?Exception|DllNotFoundException|BadImageFormatException|FileNotFoundException|IndexOutOfRangeException|NotSupportedException|UnauthorizedAccessException|IOException|UnityException):')
unexpected = [line for line in log.splitlines() if exception_pattern.search(line)]
assert not unexpected, unexpected
headset_unavailable = 'XR_ERROR_FORM_FACTOR_UNAVAILABLE' in log
previous_files = read(EVIDENCE / 'submission-existing-files.json')
submission_dir = Path(package['zip']).parent
for entry in previous_files:
    path = submission_dir / entry['name']
    assert path.stat().st_size == entry['bytes'] and sha(path) == entry['sha256'], path
assert sha(REPO / 'MuseumVR/ProjectSettings/ProjectSettings.asset') == process['originalSettingsBeforeSha256']
assert sha(Path(package['zip'])) == package['zipSha256']
extracted = Path(package['extractedRuntime'])
for entry in package['runtimeFiles']:
    path = extracted / entry['path']
    assert path.stat().st_size == entry['bytes'] and sha(path) == entry['sha256'], path

summary = dict(status='passed-build-and-package; limited-desktop-startup-observed',
               checkedUtc=datetime.now(timezone.utc).isoformat(), sourceCommit=build['commit'],
               unity=build['unity'], target=build['target'], buildConfiguration=build['options'],
               buildResult=build['buildResult'], buildErrors=build['totalErrors'],
               buildWarnings=build['totalWarnings'], buildProcessNaturalExitCode=process['exitCode'],
               sourceFilesBeforeImport=process['sourceBeforeCount'], sourceFilesAfterExit=process['sourceAfterCount'],
               sourceDifferences=process['sourceDifferenceCount'], expandedLfsAssetsVerified=process['lfsCount'],
               originalWorkspaceSettingsPreserved=True, previousSubmissionFilesPreserved=len(previous_files),
               zip=package['zip'], zipBytes=package['zipBytes'], zipSha256=package['zipSha256'],
               zipFiles=package['files'], uncompressedBytes=package['uncompressedBytes'],
               zipCrcPassed=True, extractedHashesAllMatched=True, extractedFilesUnchangedAfterLaunch=True,
               playerStartupObservedSeconds=smoke['observedSeconds'], playerResponded=smoke['responded'],
               engineInitialized=True, direct3D11Initialized=True, savedSceneHandRigStartObserved=True,
               unexpectedManagedExceptionLines=unexpected, headsetUnavailableDiagnostic=headset_unavailable,
               playerObservationStoppedByForce=smoke['forced'], playerObservationExitCode=smoke['exitCode'],
               playerShutdownNote='Hidden launch returned no MainWindowHandle; CloseMainWindow returned false. Only the test-owned player PID was terminated after the 40-second responsive observation. This is not a successful natural-exit test.',
               limits=['No available headset: OpenXR reports XR_ERROR_FORM_FACTOR_UNAVAILABLE under the existing VirtualDesktopXR runtime.',
                       'PQ604A/B/C PCs, headset/controller tracking, stereo rendering, interaction, performance, comfort and accessibility remain unverified.'],
               localEvidenceDirectory=str(EVIDENCE), buildLogSha256=sha(EVIDENCE / 'unity-build.log'),
               playerLogSha256=sha(log_path), sourceBeforeManifestSha256=sha(EVIDENCE / 'source-before-import.json'),
               sourceAfterManifestSha256=sha(EVIDENCE / 'source-after-exit.json'))
save(EVIDENCE / 'submission-validation.json', summary)
PUBLISH.mkdir(parents=True, exist_ok=False)
for name in ['build-process.json', 'package-verification.json', 'player-smoke-process.json', 'submission-validation.json', 'lfs-workspace-verification.json']:
    shutil.copyfile(EVIDENCE / name, PUBLISH / name)
shutil.copyfile(build_path, PUBLISH / 'windows-build-report.json')
shutil.copyfile(BASE / 'README-START-HERE.txt', PUBLISH / 'README-START-HERE.txt')
for name in ['run-build.ps1', 'package-build.py', 'smoke-player.ps1', 'finalize-validation.py']:
    shutil.copyfile(BASE / name, PUBLISH / name)
print(json.dumps({k:v for k,v in summary.items() if k not in ['limits', 'playerShutdownNote']}, ensure_ascii=True, indent=2))
