$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\scripts\Artifacts.ps1"
$taskTestDirectory = Join-Path $PSScriptRoot ('..\.dev\artifact-check-' + [Guid]::NewGuid().ToString('N'))
$taskTestDirectory = [IO.Path]::GetFullPath($taskTestDirectory)
New-Item -ItemType Directory -Path $taskTestDirectory | Out-Null
$taskSourceFile = Join-Path $taskTestDirectory 'input.tmod'
[IO.File]::WriteAllText($taskSourceFile, 'first build')
$taskFirst = Save-VersionedModArtifact $taskSourceFile $taskTestDirectory ([version]'0.11.0')
$taskFirstHash = (Get-FileHash -LiteralPath $taskFirst).Hash
[IO.File]::WriteAllText($taskSourceFile, 'rebuilt version')
$taskSecond = Save-VersionedModArtifact $taskSourceFile $taskTestDirectory ([version]'0.11.0')
$taskNext = Save-VersionedModArtifact $taskSourceFile $taskTestDirectory ([version]'0.11.1')
if ($taskFirst -eq $taskSecond -or $taskFirstHash -ne (Get-FileHash -LiteralPath $taskFirst).Hash -or
    (Get-Content -LiteralPath $taskSecond -Raw) -ne 'rebuilt version' -or
    [IO.Path]::GetFileName($taskFirst) -ne 'DimensionalTerraria-v0.11.0.tmod' -or
    [IO.Path]::GetFileName($taskSecond) -notlike 'DimensionalTerraria-v0.11.0-*.tmod' -or
    [IO.Path]::GetFileName($taskNext) -ne 'DimensionalTerraria-v0.11.1.tmod') {
    throw 'Versioned artifacts must preserve previous builds and include the version.'
}
Write-Output 'PASS versioned artifacts: new version, same-version rebuild, original bytes preserved'
$taskRenamed = Save-VersionedModArtifact $taskSourceFile $taskTestDirectory ([version]'0.24.0') -Name 'MineTerraria'
if ([IO.Path]::GetFileName($taskRenamed) -ne 'MineTerraria-v0.24.0.tmod' -or $taskFirstHash -ne (Get-FileHash -LiteralPath $taskFirst).Hash) {
    throw 'Renaming the public artifact must preserve the old packages.'
}
Write-Output 'PASS renamed artifact: MineTerraria filename, previous DimensionalTerraria builds preserved'
