# Save every build as a new versioned file. Never overwrite a delivered build,
# including when the same version is rebuilt for a local verification.
function Save-VersionedModArtifact {
    param([string]$Source, [string]$Directory, [version]$Version, [string]$Name = 'DimensionalTerraria')
    if ($Name -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'Invalid artifact name.' }
    $taskBase = $Name + '-v' + $Version.ToString()
    $taskDestination = Join-Path $Directory ($taskBase + '.tmod')
    if (Test-Path -LiteralPath $taskDestination) {
        $taskSuffix = [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
        $taskDestination = Join-Path $Directory ($taskBase + '-' + $taskSuffix + '.tmod')
    }
    [IO.File]::Copy($Source, $taskDestination, $false)
    return $taskDestination
}
