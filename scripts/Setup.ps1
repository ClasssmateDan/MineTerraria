[CmdletBinding()]
param()
. "$PSScriptRoot\Common.ps1"

# Pin the official release and verify its published SHA-256. Game assets are not included in this repository.
$taskVersion = '2026.07.3.0'
$taskExpectedHash = '6f51610f4b0f167d1620d0e8c83264ba14e047421e97d9f8053eb40e3d764e1a'
$taskDev = Join-Path $script:Workspace '.dev'
$taskArchive = Join-Path $taskDev 'tModLoader.zip'
$taskDestination = Join-Path $taskDev 'tModLoader'
New-Item -ItemType Directory -Path $taskDev -Force | Out-Null
if (-not (Test-Path -LiteralPath $taskArchive)) {
    Invoke-WebRequest -Uri "https://github.com/tModLoader/tModLoader/releases/download/v$taskVersion/tModLoader.zip" -OutFile $taskArchive
}
if ((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash -ne $taskExpectedHash) {
    throw 'tModLoader archive checksum mismatch. The archive was not extracted.'
}
if (-not (Test-Path -LiteralPath (Join-Path $taskDestination 'tMLMod.targets'))) {
    Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskDestination
}
Write-Output "Official tModLoader v$taskVersion is ready: $taskDestination"
