$ErrorActionPreference = 'Stop'
$script:Workspace = Split-Path -Parent $PSScriptRoot

function Invoke-BuildEnvironment {
    param([scriptblock]$Action)
    $taskVariables = @{
        DOTNET_CLI_HOME = Join-Path $script:Workspace '.dev\dotnet-home'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
        NUGET_PACKAGES = Join-Path $script:Workspace '.dev\nuget'
        APPDATA = Join-Path $script:Workspace '.dev\appdata'
    }
    $taskPrevious = @{}
    foreach ($taskName in $taskVariables.Keys) {
        $taskPrevious[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
        [Environment]::SetEnvironmentVariable($taskName, $taskVariables[$taskName], 'Process')
    }
    New-Item -ItemType Directory -Path $taskVariables.APPDATA -Force | Out-Null
    try { & $Action }
    finally {
        foreach ($taskName in $taskPrevious.Keys) {
            [Environment]::SetEnvironmentVariable($taskName, $taskPrevious[$taskName], 'Process')
        }
    }
}

function Assert-NativeSuccess {
    param([string]$Step)
    if ($LASTEXITCODE -ne 0) { throw "$Step failed (exit code $LASTEXITCODE)." }
}
