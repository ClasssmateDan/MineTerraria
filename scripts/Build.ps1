[CmdletBinding()]
param([string]$TmlDirectory = '')
. "$PSScriptRoot\Common.ps1"
. "$PSScriptRoot\Artifacts.ps1"
if (-not $TmlDirectory) { $TmlDirectory = Join-Path $script:Workspace '.dev\tModLoader' }
$TmlDirectory = (Resolve-Path -LiteralPath $TmlDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $TmlDirectory 'tMLMod.targets'))) {
    throw 'No tModLoader build targets found. Run scripts/Setup.ps1 first.'
}
$taskProject = Join-Path $script:Workspace 'DimensionalTerraria\DimensionalTerraria.csproj'
$taskSource = Join-Path $script:Workspace 'DimensionalTerraria'
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
    Push-Location -LiteralPath $TmlDirectory
    try {
        dotnet tModLoader.dll -server -nosteam -tmlsavedirectory $taskProfile -modpath $taskModPath -build $taskSource -eac $taskAssembly
        Assert-NativeSuccess 'Package'
    }
    finally { Pop-Location }
    dotnet (Join-Path $script:Workspace 'tests\EngineApiTests\bin\Release\net8.0\EngineApiTests.dll') $TmlDirectory $taskAssembly (Join-Path $taskModPath 'DimensionalTerraria.tmod')
    Assert-NativeSuccess 'Packaged icon preparation on loader thread'
}
$taskArtifact = Save-VersionedModArtifact -Source (Join-Path $taskModPath 'DimensionalTerraria.tmod') -Directory $taskArtifactDirectory -Version $taskModVersion -Name 'MineTerraria'
Write-Output "Built mod: $taskArtifact"
