$ErrorActionPreference = 'Stop'
$baseDir = 'E:\Codex\Temp\phase2-windows-20261018'
$evidence = Join-Path $baseDir 'evidence'
$package = Get-Content -LiteralPath (Join-Path $evidence 'package-verification.json') -Raw | ConvertFrom-Json
$exe = Join-Path $package.extractedRuntime 'MuseumVR.exe'
if ($package.status -ne 'passed' -or !(Test-Path -LiteralPath $exe)) { throw 'Verify and extract the package before launching it.' }
$logPath = Join-Path $evidence 'player-smoke.log'
if (Test-Path -LiteralPath $logPath) { throw 'Use a new smoke log.' }
$arguments = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"'+$logPath+'"'))
$record = [ordered]@{ sourceCommit=$package.sourceCommit; executable=$exe; arguments=$arguments; startedUtc=[DateTime]::UtcNow.ToString('o'); finishedUtc=$null; pid=$null; observedSeconds=40; aliveAfterObservation=$false; windowHandle=$null; responded=$null; closeRequested=$false; closeAccepted=$false; exitCode=$null; forced=$false; startupObservation='running'; log=$logPath; note='Local extracted-player startup smoke check only. No physical headset/controller, PQ604 PC, interaction, stereo rendering or performance verification is implied. Exit/forced termination and startup observation are separate results; the final startup conclusion also requires log review.' }
$process = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $package.extractedRuntime -WindowStyle Hidden -PassThru
$record.pid = $process.Id
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'player-smoke-process.json') -Encoding utf8
Write-Output "Extracted player started, PID=$($process.Id)"
$watch = [Diagnostics.Stopwatch]::StartNew()
while (!$process.HasExited -and $watch.Elapsed.TotalSeconds -lt $record.observedSeconds) { Start-Sleep -Milliseconds 500; $process.Refresh() }
$record.aliveAfterObservation = !$process.HasExited
if (!$process.HasExited) {
    $record.responded = $process.Responding
    $record.windowHandle = $process.MainWindowHandle.ToInt64()
    $record.closeRequested = $true
    $record.closeAccepted = $process.CloseMainWindow()
    if (!$process.WaitForExit(15000)) { $process.Kill(); $process.WaitForExit(); $record.forced = $true }
}
$process.WaitForExit()
$record.exitCode = $process.ExitCode
$record.finishedUtc = [DateTime]::UtcNow.ToString('o')
$record.startupObservation = if ($record.aliveAfterObservation -and $record.responded) { 'observed-for-40-seconds-log-review-required' } else { 'failed' }
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'player-smoke-process.json') -Encoding utf8
Write-Output ($record | ConvertTo-Json -Depth 8)
if (!$record.aliveAfterObservation -or !$record.responded -or (!$record.forced -and $record.exitCode -ne 0)) { throw 'Player exited early, did not respond, or returned an unexpected exit code. Inspect the smoke log.' }
