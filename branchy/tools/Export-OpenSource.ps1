[CmdletBinding()]
param([string]$SourceDirectory = (Join-Path $PSScriptRoot '../..'))

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PublicSource.Common.ps1')

$selection = Get-PublicSourceSelection -SourceDirectory $SourceDirectory
Assert-PublicSourceSafe -Files $selection.Files
[xml]$manifest = Get-Content -LiteralPath (Join-Path $selection.Root 'branchy/Package.appxmanifest') -Raw
$version = $manifest.Package.Identity.Version
$dist = Join-Path $selection.Root 'dist'
if ((Test-Path -LiteralPath $dist) -and ((Get-Item -LiteralPath $dist -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'The output directory must not be a link.'
}
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$name = 'BRANCHY-source-{0}-{1}' -f $version, [Guid]::NewGuid().ToString('N').Substring(0, 8)
$destination = Join-Path $dist $name
$archive = Join-Path $dist ($name + '.zip')
if ((Test-Path -LiteralPath $destination) -or (Test-Path -LiteralPath $archive)) {
    throw 'Refusing to overwrite an existing source snapshot.'
}
New-Item -ItemType Directory -Path $destination | Out-Null
$hashes = [Collections.Generic.List[string]]::new()
foreach ($file in $selection.Files) {
    $target = Join-Path $destination $file.Relative
    $parent = Split-Path -Parent $target
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    Copy-Item -LiteralPath $file.Source -Destination $target
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $hashes.Add(('{0}  {1}' -f $hash, $file.Relative))
}
[IO.File]::WriteAllLines((Join-Path $destination 'MANIFEST.sha256'), $hashes, [Text.UTF8Encoding]::new($false))
Assert-PublicSourceSafe -Files (Get-PublicSourceSelection -SourceDirectory $destination).Files
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($destination, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
[pscustomobject]@{
    Result = 'PASS'
    SourceFolder = $destination
    SourceZip = $archive
    PublicFiles = $selection.Files.Count
    ZipSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    GitHistoryIncluded = $false
}
