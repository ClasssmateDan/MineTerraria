[CmdletBinding()]
param([string]$TmlDirectory = '')
. "$PSScriptRoot\Common.ps1"
. "$PSScriptRoot\Artifacts.ps1"
if (-not $TmlDirectory) { $TmlDirectory = Join-Path $script:Workspace '.dev\tModLoader' }
$TmlDirectory = (Resolve-Path -LiteralPath $TmlDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $TmlDirectory 'tMLMod.targets'))) {
    throw 'No tModLoader build targets found. Run scripts/Setup.ps1 first.'
}
$taskProject = Join-Path $script:Workspace 'MineTerraria\MineTerraria.csproj'
$taskSource = Join-Path $script:Workspace 'MineTerraria'
$taskAssembly = Join-Path $taskSource 'bin\Release\net8.0\DimensionalTerraria.dll'
$taskProfile = Join-Path $script:Workspace '.dev\profile'
$taskModPath = Join-Path $taskProfile 'Mods'
$taskArtifactDirectory = Join-Path $script:Workspace 'artifacts'
$taskVersionText = (Select-String -LiteralPath (Join-Path $taskSource 'build.txt') -Pattern '^version\s*=\s*(.+)$').Matches.Groups[1].Value.Trim()
$taskModVersion = [version]::Parse($taskVersionText)
New-Item -ItemType Directory -Path $taskModPath, $taskArtifactDirectory -Force | Out-Null

Invoke-BuildEnvironment {
    dotnet restore $taskProject --configfile (Join-Path $script:Workspace 'NuGet.Config') --verbosity quiet
    Assert-NativeSuccess 'Restore'
    dotnet build $taskProject --no-restore --configuration Release -p:BuildMod=false "-p:TmlDirectory=$TmlDirectory" --verbosity minimal
    Assert-NativeSuccess 'Compile'
    $taskApiTests = Join-Path $script:Workspace 'tests\EngineApiTests\EngineApiTests.csproj'
    dotnet restore $taskApiTests --configfile (Join-Path $script:Workspace 'NuGet.Config') --verbosity quiet
    Assert-NativeSuccess 'API test restore'
    dotnet build $taskApiTests --no-restore --configuration Release -p:UseAppHost=false --verbosity quiet
    Assert-NativeSuccess 'API test build'
    dotnet (Join-Path $script:Workspace 'tests\EngineApiTests\bin\Release\net8.0\EngineApiTests.dll') $TmlDirectory $taskAssembly
    Assert-NativeSuccess 'Native interaction bindings'
    # The official compiler derives the internal mod ID from the source folder.
    # Stage only packaging inputs under the legacy ID; public sources stay named MineTerraria.
    $taskStageRoot = [IO.Path]::GetFullPath((Join-Path $script:Workspace '.dev\package-source'))
    $taskStageSource = [IO.Path]::GetFullPath((Join-Path $taskStageRoot 'DimensionalTerraria'))
    $taskExpectedStage = [IO.Path]::GetFullPath((Join-Path $script:Workspace '.dev\package-source\DimensionalTerraria'))
    if ($taskStageSource -ne $taskExpectedStage -or -not $taskStageSource.StartsWith($taskStageRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Packaging source must remain inside the isolated staging directory.'
    }
    if ((Test-Path -LiteralPath $taskStageRoot) -and ((Get-Item -LiteralPath $taskStageRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Packaging root must not be a reparse point.'
    }
    if (Test-Path -LiteralPath $taskStageSource) {
        $taskStageItem = Get-Item -LiteralPath $taskStageSource -Force
        if ($taskStageItem.Attributes -band [IO.FileAttributes]::ReparsePoint -or @(Get-ChildItem -LiteralPath $taskStageSource -Recurse -Force -Attributes ReparsePoint).Count -gt 0) {
            throw 'Packaging staging must not contain reparse points.'
        }
        if ((Resolve-Path -LiteralPath $taskStageSource).Path -ne $taskExpectedStage) { throw 'Unexpected resolved packaging directory.' }
        Remove-Item -LiteralPath $taskStageSource -Recurse -Force
    }
    New-Item -ItemType Directory -Path $taskStageSource -Force | Out-Null
    foreach ($taskInput in Get-ChildItem -LiteralPath $taskSource -Force | Where-Object { $_.Name -notin @('bin', 'obj') }) {
        Copy-Item -LiteralPath $taskInput.FullName -Destination $taskStageSource -Recurse
    }
    Push-Location -LiteralPath $TmlDirectory
    try {
        dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $taskProfile -modpath $taskModPath -build $taskStageSource -eac $taskAssembly
        Assert-NativeSuccess 'Package'
    }
    finally { Pop-Location }
    dotnet (Join-Path $script:Workspace 'tests\EngineApiTests\bin\Release\net8.0\EngineApiTests.dll') $TmlDirectory $taskAssembly (Join-Path $taskModPath 'DimensionalTerraria.tmod')
    Assert-NativeSuccess 'Packaged icon preparation on loader thread'
}
$taskArtifact = Save-VersionedModArtifact -Source (Join-Path $taskModPath 'DimensionalTerraria.tmod') -Directory $taskArtifactDirectory -Version $taskModVersion -Name 'MineTerraria'
Write-Output "Built mod: $taskArtifact"
