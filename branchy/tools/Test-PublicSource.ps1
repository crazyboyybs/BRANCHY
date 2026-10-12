[CmdletBinding()]
param([string]$SourceDirectory = (Join-Path $PSScriptRoot '../..'))

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PublicSource.Common.ps1')

$selection = Get-PublicSourceSelection -SourceDirectory $SourceDirectory
Assert-PublicSourceSafe -Files $selection.Files
[pscustomobject]@{
    Result = 'PASS'
    PublicFiles = $selection.Files.Count
    ExcludedFilesOrDirectories = $selection.ExcludedCount
    Scope = 'Current BRANCHY source snapshot only; Git history and local build artifacts are not included.'
}
