param(
    [Parameter(Mandatory=$true)][string]$BundlePath,
    [Parameter(Mandatory=$true)][string]$CertificatePath,
    [Parameter(Mandatory=$true)][string]$BackupDirectory,
    [Parameter(Mandatory=$true)][string]$ResultPath
)

$ErrorActionPreference = 'Stop'
try {
    $bundle = (Resolve-Path -LiteralPath $BundlePath).Path
    $certificateFile = (Resolve-Path -LiteralPath $CertificatePath).Path
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificateFile)
    $signature = Get-AuthenticodeSignature -LiteralPath $bundle
    if ($certificate.Subject -ne 'CN=BRANCHY' -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
        throw 'Unexpected development certificate.'
    }
    Import-Certificate -FilePath $certificateFile -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    if ((Get-AuthenticodeSignature -LiteralPath $bundle).Status -ne 'Valid') { throw 'Invalid package signature.' }

    $packages = @(Get-AppxPackage -Name 'C4D295E7-1BF1-47A2-844B-3D9F33BD0A62')
    $previous = $packages | Where-Object Publisher -ne 'CN=BRANCHY' | Select-Object -First 1
    $packageDataRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Packages'
    $dataFolders = @('LocalState', 'RoamingState', 'Settings')
    if ($null -ne $previous) {
        $running = @(Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith($previous.InstallLocation+'\', [StringComparison]::OrdinalIgnoreCase) })
        if ($running.Count -gt 0) { throw 'Close the previous BRANCHY instance before migrating its settings.' }
        New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
        $oldData = Join-Path $packageDataRoot $previous.PackageFamilyName
        foreach ($folder in $dataFolders) {
            $source = Join-Path $oldData $folder
            $backup = Join-Path $BackupDirectory $folder
            if ((Test-Path -LiteralPath $source) -and -not (Test-Path -LiteralPath $backup)) {
                Copy-Item -LiteralPath $source -Destination $backup -Recurse
            }
        }
    }

    $alreadyInstalled = $packages | Where-Object Publisher -eq 'CN=BRANCHY' | Select-Object -First 1
    Add-AppxPackage -Path $bundle -ForceApplicationShutdown
    $installed = Get-AppxPackage -Name 'C4D295E7-1BF1-47A2-844B-3D9F33BD0A62' | Where-Object Publisher -eq 'CN=BRANCHY' | Select-Object -First 1
    if ($null -eq $installed -or $installed.Status -ne 'Ok') { throw 'Package registration was not confirmed.' }

    if ($null -ne $previous -and $null -eq $alreadyInstalled) {
        $newData = Join-Path $packageDataRoot $installed.PackageFamilyName
        foreach ($folder in $dataFolders) {
            $backup = Join-Path $BackupDirectory $folder
            $destination = Join-Path $newData $folder
            if (Test-Path -LiteralPath $backup) {
                New-Item -ItemType Directory -Path $destination -Force | Out-Null
                Get-ChildItem -LiteralPath $backup -Force | ForEach-Object {
                    Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
                }
            }
        }
    }

    if ($null -ne $previous) {
        if (-not (Test-Path -LiteralPath (Join-Path $BackupDirectory 'Settings'))) { throw 'Settings backup is missing; previous package retained.' }
        Remove-AppxPackage -Package $previous.PackageFullName
    }

    @{ Success=$true; Version=$installed.Version.ToString(); Family=$installed.PackageFamilyName; PreviousFamily=$previous.PackageFamilyName; Backup=$BackupDirectory } |
        ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding utf8
}
catch {
    @{ Success=$false; Error=$_.Exception.Message } | ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding utf8
    exit 1
}
