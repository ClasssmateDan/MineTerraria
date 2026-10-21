[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
$taskFixture = Join-Path $taskWorkspace ('.dev\audio-test-' + [guid]::NewGuid().ToString('N'))
$taskFixtureNative = Join-Path $taskFixture 'Libraries\Native\Windows'
$taskAudioScript = Join-Path $taskWorkspace 'scripts\AudioWorkaround.ps1'
New-Item -ItemType Directory -Path $taskFixtureNative -Force | Out-Null
Set-Content -LiteralPath (Join-Path $taskFixture 'tModLoader.dll') -Value 'Isolated test fixture; never executed'
Set-Content -LiteralPath (Join-Path $taskFixtureNative 'FAudio.dll') -Value 'original FAudio test bytes'
Set-Content -LiteralPath (Join-Path $taskFixtureNative 'avutil-56.dll') -Value 'original avutil test bytes'
$taskOriginalAudio = (Get-FileHash -LiteralPath (Join-Path $taskFixtureNative 'FAudio.dll')).Hash
$taskOriginalUtil = (Get-FileHash -LiteralPath (Join-Path $taskFixtureNative 'avutil-56.dll')).Hash

& $taskAudioScript -TmlDirectory $taskFixture -Mode Status | Out-Null
if ((Get-FileHash -LiteralPath (Join-Path $taskFixtureNative 'FAudio.dll')).Hash -ne $taskOriginalAudio -or
    (Test-Path -LiteralPath (Join-Path $taskFixtureNative 'avcodec-58.dll'))) { throw 'Status changed the installation.' }
Write-Output 'PASS audio status is read-only'

& $taskAudioScript -TmlDirectory $taskFixture -Mode Install | Out-Null
$taskPatchedAudio = (Get-FileHash -LiteralPath (Join-Path $taskFixtureNative 'FAudio.dll')).Hash
if ($taskPatchedAudio -eq $taskOriginalAudio -or -not (Test-Path -LiteralPath (Join-Path $taskFixtureNative 'avcodec-58.dll'))) { throw 'Audio files were not installed.' }
Write-Output 'PASS official audio payload installs into an isolated fixture'

& $taskAudioScript -TmlDirectory $taskFixture -Mode Install | Out-Null
& $taskAudioScript -TmlDirectory $taskFixture -Mode Restore | Out-Null
if ((Get-FileHash -LiteralPath (Join-Path $taskFixtureNative 'FAudio.dll')).Hash -ne $taskOriginalAudio -or
    (Get-FileHash -LiteralPath (Join-Path $taskFixtureNative 'avutil-56.dll')).Hash -ne $taskOriginalUtil -or
    (Test-Path -LiteralPath (Join-Path $taskFixtureNative 'avcodec-58.dll'))) { throw 'Repeated installation lost the original backup.' }
Write-Output 'PASS repeated install preserves originals and restore removes only added files'

& $taskAudioScript -TmlDirectory $taskFixture -Mode Install | Out-Null
Set-Content -LiteralPath (Join-Path $taskFixtureNative 'FAudio.dll') -Value 'changed by a simulated Steam update'
$taskRefused = $false
try { & $taskAudioScript -TmlDirectory $taskFixture -Mode Restore | Out-Null }
catch { if ($_.Exception.Message -like '*Library changed since backup*') { $taskRefused = $true } else { throw } }
if (-not $taskRefused) { throw 'Restore overwrote a library changed by another application.' }
Write-Output 'PASS restore refuses to overwrite externally changed libraries'

Copy-Item -LiteralPath (Join-Path $taskWorkspace '.dev\audio-workaround\files\FAudio.dll') -Destination (Join-Path $taskFixtureNative 'FAudio.dll') -Force
& $taskAudioScript -TmlDirectory $taskFixture -Mode Restore | Out-Null
Write-Output 'PASS audio fixture restores after the external modification is resolved'
