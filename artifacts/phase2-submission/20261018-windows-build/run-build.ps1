$ErrorActionPreference = 'Stop'
$buildRepo = 'E:\Codex\worktrees\phase2-windows-build-20261018\XRmuseum'
$evidence = 'E:\Codex\Temp\phase2-windows-20261018\evidence'
$runtime = 'E:\Codex\Builds\XRmuseum\phase2-20261018-6088c73'
$unityExe = 'E:\unity\36version\2022.3.62f3c1\Editor\Unity.exe'
$sourceCommit = '6088c736e89d05101e09645c1796556012a6fd5d'
function Save-Json($object, $name) { $object | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $evidence $name) -Encoding utf8 }
function Snapshot-Source {
    $rows = foreach ($folder in @('Assets','Packages','ProjectSettings')) {
        Get-ChildItem -LiteralPath (Join-Path $buildRepo "MuseumVR\$folder") -File -Recurse | ForEach-Object {
            [ordered]@{ path = $_.FullName.Substring($buildRepo.Length + 1).Replace('\','/'); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
        }
    }
    return @($rows | Sort-Object { $_.path })
}
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Another Unity Editor is running; do not start a parallel Editor.' }
if ((git -C $buildRepo rev-parse HEAD) -ne $sourceCommit) { throw 'Unexpected source commit' }
$dirtyBefore = @(git -C $buildRepo status --porcelain=v1 --untracked-files=all)
if ($dirtyBefore.Count) { throw "The isolated source is not clean: $dirtyBefore" }
if ((Get-ChildItem -LiteralPath $runtime -Force).Count) { throw 'Runtime output must be empty.' }
$lfs = (git -C $buildRepo lfs ls-files --json | ConvertFrom-Json).files
$lfsRows = foreach ($entry in $lfs) {
    $asset = Get-Item -LiteralPath (Join-Path $buildRepo $entry.name)
    $actual = (Get-FileHash -LiteralPath $asset.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if (!$entry.checkout -or !$entry.downloaded -or $asset.Length -ne $entry.size -or $actual -ne $entry.oid) { throw "Unexpanded or incorrect LFS asset: $($entry.name)" }
    [ordered]@{ path=$entry.name; bytes=$asset.Length; sha256=$actual; matchesLfsObject=$true }
}
Save-Json @($lfsRows) 'lfs-workspace-verification.json'
$before = Snapshot-Source
Save-Json $before 'source-before-import.json'
Copy-Item -LiteralPath (Join-Path $buildRepo 'MuseumVR\ProjectSettings\ProjectSettings.asset') -Destination (Join-Path $evidence 'ProjectSettings.before-build.asset')
Copy-Item -LiteralPath (Join-Path $buildRepo 'MuseumVR\ProjectSettings\EditorBuildSettings.asset') -Destination (Join-Path $evidence 'EditorBuildSettings.before-build.asset')
$originalSettings = 'E:\XRmuseum\MuseumVR\ProjectSettings\ProjectSettings.asset'
$env:MUSEUM_BUILD_OUTPUT = Join-Path $runtime 'MuseumVR.exe'
$env:MUSEUM_QA_OUTPUT = Join-Path $evidence 'qa'
$tempDir = Join-Path $evidence 'temp'
New-Item -ItemType Directory -Path $tempDir | Out-Null
$env:TEMP = $tempDir
$env:TMP = $tempDir
$arguments = @('-batchmode','-buildTarget','Win64','-projectPath',('"'+(Join-Path $buildRepo 'MuseumVR')+'"'),'-logFile',('"'+(Join-Path $evidence 'unity-build.log')+'"'),'-executeMethod','Museum.PhysicalHands.EditorTools.MuseumPhysicalHandsBuild.RunBatch')
$record = [ordered]@{ state='starting'; commit=$sourceCommit; checkout=$buildRepo; unity=$unityExe; arguments=$arguments; buildOutput=$env:MUSEUM_BUILD_OUTPUT; qaOutput=$env:MUSEUM_QA_OUTPUT; startedUtc=[DateTime]::UtcNow.ToString('o'); finishedUtc=$null; pid=$null; exitCode=$null; forced=$false; timeoutSeconds=1800; dirtyBefore=$dirtyBefore; originalSettingsBeforeSha256=(Get-FileHash -LiteralPath $originalSettings -Algorithm SHA256).Hash.ToLowerInvariant(); sourceBeforeCount=$before.Count; lfsCount=$lfsRows.Count }
Save-Json $record 'build-process.json'
$process = Start-Process -FilePath $unityExe -ArgumentList $arguments -PassThru -WindowStyle Hidden
$record.pid = $process.Id
$record.state = 'running'
Save-Json $record 'build-process.json'
Write-Output "Unity build started, PID=$($process.Id), commit=$sourceCommit"
$watch = [Diagnostics.Stopwatch]::StartNew()
while (!$process.WaitForExit(5000)) {
    if ($watch.Elapsed.TotalSeconds -gt $record.timeoutSeconds) { $process.Kill(); $process.WaitForExit(); $record.forced=$true; break }
}
$process.Refresh()
$record.exitCode = $process.ExitCode
$record.finishedUtc = [DateTime]::UtcNow.ToString('o')
$record.state = 'exited'
$after = Snapshot-Source
Save-Json $after 'source-after-exit.json'
$changes = @(Compare-Object ($before | ForEach-Object { "$($_.path)|$($_.bytes)|$($_.sha256)" }) ($after | ForEach-Object { "$($_.path)|$($_.bytes)|$($_.sha256)" }))
$record.sourceAfterCount = $after.Count
$record.sourceDifferenceCount = $changes.Count
$record.dirtyAfter = @(git -C $buildRepo status --porcelain=v1 --untracked-files=all)
$record.originalSettingsAfterSha256 = (Get-FileHash -LiteralPath $originalSettings -Algorithm SHA256).Hash.ToLowerInvariant()
Save-Json $record 'build-process.json'
Write-Output ($record | ConvertTo-Json -Depth 8)
if ($record.forced -or $record.exitCode -ne 0 -or $changes.Count -ne 0 -or $record.dirtyAfter.Count -ne 0) { throw 'Build process, source integrity, or clean-check failed; inspect preserved evidence.' }
if ($record.originalSettingsAfterSha256 -ne $record.originalSettingsBeforeSha256) { throw 'Original workspace settings changed concurrently.' }
$reports = @(Get-ChildItem -LiteralPath $env:MUSEUM_QA_OUTPUT -Filter report.json -Recurse)
if ($reports.Count -ne 1) { throw 'Expected one fresh build report.' }
$report = Get-Content -LiteralPath $reports[0].FullName -Raw | ConvertFrom-Json
if ($report.status -ne 'passed' -or $report.buildResult -ne 'Succeeded' -or $report.totalErrors -ne 0 -or $report.commit -ne $sourceCommit) { throw 'BuildReport did not pass.' }
Write-Output "BUILD_VERIFIED $($reports[0].FullName)"
