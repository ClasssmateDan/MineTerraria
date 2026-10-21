[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TmlDirectory,
    [ValidateSet('Status', 'Install', 'Restore')][string]$Mode = 'Status'
)
. "$PSScriptRoot\Common.ps1"

# Official maintainer attachment in https://github.com/tModLoader/tModLoader/issues/2863
# Keep the SDL audio fallback separate from the mod. Only the three required audio
# DLLs are selected; unrelated native libraries in the archive are not installed.
$taskArchiveHash = 'C33A2F5F03FAFD5E67C7CFB58A2E0AC960FEF03E64A68E6575CE88CCF0647EC4'
$taskAudioNames = @('FAudio.dll', 'avcodec-58.dll', 'avutil-56.dll')
$taskTmlRoot = (Resolve-Path -LiteralPath $TmlDirectory).Path.TrimEnd([char[]]'\/')
if (-not (Test-Path -LiteralPath (Join-Path $taskTmlRoot 'tModLoader.dll') -PathType Leaf)) { throw 'Not a tModLoader installation.' }
$taskNativeRoot = Join-Path $taskTmlRoot 'Libraries\Native\Windows'
if (-not (Test-Path -LiteralPath $taskNativeRoot -PathType Container)) { throw 'Windows native library directory is missing.' }
$taskKeyBytes = [System.Text.Encoding]::UTF8.GetBytes($taskTmlRoot.ToUpperInvariant())
$taskHasher = [System.Security.Cryptography.SHA256]::Create()
try { $taskTargetKey = ([BitConverter]::ToString($taskHasher.ComputeHash($taskKeyBytes))).Replace('-', '').Substring(0, 16) }
finally { $taskHasher.Dispose() }
$taskBackupRoot = Join-Path $script:Workspace ('.dev\audio-workaround\backups\' + $taskTargetKey)
$taskManifestPath = Join-Path $taskBackupRoot 'backup.json'
$taskAudioManifest = $null
if (Test-Path -LiteralPath $taskManifestPath) {
    $taskAudioManifest = Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json
    if ($taskAudioManifest.Target -ne $taskTmlRoot -or $taskAudioManifest.Files.Count -ne 3) { throw 'Backup does not match the target installation.' }
    foreach ($taskName in $taskAudioNames) {
        if (@($taskAudioManifest.Files | Where-Object Name -eq $taskName).Count -ne 1) { throw 'Invalid backup file list.' }
    }
}

if ($Mode -eq 'Status') {
    [pscustomobject]@{ Target = $taskTmlRoot; Backup = $(if ($taskAudioManifest) { $taskBackupRoot } else { 'None' }) } | Format-List
    foreach ($taskName in $taskAudioNames) {
        $taskDestination = Join-Path $taskNativeRoot $taskName
        [pscustomobject]@{ File = $taskName; SHA256 = $(if (Test-Path -LiteralPath $taskDestination) { (Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash } else { 'Absent' }) } | Format-List
    }
    return
}

# Native DLL replacement must happen while the game and its bundled runtime are closed.
$taskBundledRuntime = Join-Path $taskTmlRoot 'dotnet\dotnet.exe'
if (Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $taskBundledRuntime }) { throw 'Close tModLoader before changing its audio libraries.' }

function Assert-AudioTargetUnchanged {
    param([switch]$AfterFailedInstall)
    foreach ($taskEntry in $taskAudioManifest.Files) {
        $taskDestination = Join-Path $taskNativeRoot $taskEntry.Name
        if (-not $AfterFailedInstall -and (Test-Path -LiteralPath $taskDestination)) {
            $taskCurrentHash = (Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash
            if ($taskCurrentHash -ne $taskEntry.PatchHash -and $taskCurrentHash -ne $taskEntry.OriginalHash) {
                throw "Library changed since backup: $($taskEntry.Name). Refusing to overwrite it."
            }
        }
        if ($taskEntry.Existed) {
            $taskSavedFile = Join-Path $taskBackupRoot $taskEntry.Name
            if (-not (Test-Path -LiteralPath $taskSavedFile) -or (Get-FileHash -LiteralPath $taskSavedFile -Algorithm SHA256).Hash -ne $taskEntry.OriginalHash) {
                throw "Missing or damaged backup: $($taskEntry.Name)"
            }
        }
    }
}

function Restore-AudioFiles {
    param([switch]$AfterFailedInstall)
    Assert-AudioTargetUnchanged -AfterFailedInstall:$AfterFailedInstall
    foreach ($taskEntry in $taskAudioManifest.Files) {
        # Names are from the fixed allowlist validated above; no recursive operations.
        $taskDestination = Join-Path $taskNativeRoot $taskEntry.Name
        if ($taskEntry.Existed) { Copy-Item -LiteralPath (Join-Path $taskBackupRoot $taskEntry.Name) -Destination $taskDestination -Force }
        elseif (Test-Path -LiteralPath $taskDestination) { Remove-Item -LiteralPath $taskDestination }
    }
}

if ($Mode -eq 'Restore') {
    if (-not $taskAudioManifest) { throw 'No audio backup exists for this installation.' }
    Restore-AudioFiles
    Write-Output "Original audio libraries restored: $taskNativeRoot"
    return
}

$taskAudioArchive = Join-Path $script:Workspace 'artifacts\IAudioClient-workaround.zip'
if (-not (Test-Path -LiteralPath $taskAudioArchive) -or (Get-FileHash -LiteralPath $taskAudioArchive -Algorithm SHA256).Hash -ne $taskArchiveHash) {
    throw 'The verified official artifacts/IAudioClient-workaround.zip package is missing or modified.'
}
$taskAudioStaging = Join-Path $script:Workspace '.dev\audio-workaround\files'
New-Item -ItemType Directory -Path $taskAudioStaging -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskAudioZip = [System.IO.Compression.ZipFile]::OpenRead($taskAudioArchive)
try {
    foreach ($taskName in $taskAudioNames) {
        $taskZipEntry = $taskAudioZip.GetEntry('Libraries/Native/Windows/' + $taskName)
        if (-not $taskZipEntry) { throw "Missing audio file: $taskName" }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($taskZipEntry, (Join-Path $taskAudioStaging $taskName), $true)
    }
}
finally { $taskAudioZip.Dispose() }

if (-not $taskAudioManifest) {
    New-Item -ItemType Directory -Path $taskBackupRoot -Force | Out-Null
    $taskOriginalFiles = foreach ($taskName in $taskAudioNames) {
        $taskDestination = Join-Path $taskNativeRoot $taskName
        $taskExisted = Test-Path -LiteralPath $taskDestination -PathType Leaf
        $taskOriginalHash = $null
        if ($taskExisted) {
            $taskOriginalHash = (Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash
            Copy-Item -LiteralPath $taskDestination -Destination (Join-Path $taskBackupRoot $taskName) -Force
        }
        [pscustomobject]@{ Name = $taskName; Existed = $taskExisted; OriginalHash = $taskOriginalHash;
            PatchHash = (Get-FileHash -LiteralPath (Join-Path $taskAudioStaging $taskName) -Algorithm SHA256).Hash }
    }
    $taskAudioManifest = [pscustomobject]@{ Target = $taskTmlRoot; Files = @($taskOriginalFiles) }
    $taskAudioManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $taskManifestPath -Encoding UTF8
}
Assert-AudioTargetUnchanged
try {
    foreach ($taskEntry in $taskAudioManifest.Files) {
        $taskSource = Join-Path $taskAudioStaging $taskEntry.Name
        if ((Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash -ne $taskEntry.PatchHash) { throw 'Package does not match the recorded backup.' }
        Copy-Item -LiteralPath $taskSource -Destination (Join-Path $taskNativeRoot $taskEntry.Name) -Force
    }
}
catch {
    Restore-AudioFiles -AfterFailedInstall
    throw
}
Write-Output "Official SDL audio workaround installed. Restart tModLoader. Backup: $taskBackupRoot"
