[CmdletBinding()]
param()
. "$PSScriptRoot\Common.ps1"
$taskProject = Join-Path $script:Workspace 'tests\SpatialTests\SpatialTests.csproj'
Invoke-BuildEnvironment {
    dotnet restore $taskProject --configfile (Join-Path $script:Workspace 'NuGet.Config') --verbosity quiet
    Assert-NativeSuccess 'Test restore'
    dotnet build $taskProject --no-restore --configuration Release -p:UseAppHost=false --verbosity quiet
    Assert-NativeSuccess 'Test build'
    dotnet (Join-Path $script:Workspace 'tests\SpatialTests\bin\Release\net8.0\SpatialTests.dll')
    Assert-NativeSuccess 'Spatial tests'

    $taskLocalizationProject = Join-Path $script:Workspace 'tests\LocalizationTests\LocalizationTests.csproj'
    dotnet restore $taskLocalizationProject --configfile (Join-Path $script:Workspace 'NuGet.Config') --verbosity quiet
    Assert-NativeSuccess 'Localization restore'
    dotnet build $taskLocalizationProject --no-restore --configuration Release -p:UseAppHost=false --verbosity quiet
    Assert-NativeSuccess 'Localization build'
    dotnet (Join-Path $script:Workspace 'tests\LocalizationTests\bin\Release\net8.0\LocalizationTests.dll') (Join-Path $script:Workspace 'DimensionalTerraria\Localization')
    Assert-NativeSuccess 'Localization syntax'
}
& (Join-Path $script:Workspace 'tests\AssetTests.ps1')
& (Join-Path $script:Workspace 'tests\ArtifactTests.ps1')
