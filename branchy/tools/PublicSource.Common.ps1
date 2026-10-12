Set-StrictMode -Version Latest

function Get-PublicSourceSelection {
    param([Parameter(Mandatory)][string]$SourceDirectory)

    $root = (Resolve-Path -LiteralPath $SourceDirectory).Path
    $allowedExtensions = @(
        '.cs', '.cpp', '.h', '.xaml', '.xml', '.resw', '.json', '.md', '.txt',
        '.ps1', '.py', '.csproj', '.vcxproj', '.slnx', '.manifest',
        '.appxmanifest', '.pubxml', '.rc', '.def', '.svg', '.png', '.ico', '.ttf'
    )
    $excludedDirectories = @(
        '.git', '.vs', '.vscode', '.idea', '.codex', '.agents', '.aws', '.private',
        'bin', 'obj', 'AppPackages', 'BundleArtifacts', 'Debug', 'Release',
        'dist', 'node_modules', '__pycache__', 'TestResults', 'packages'
    )
    $files = [Collections.Generic.List[object]]::new()
    $excludedCount = 0
    foreach ($scope in @('branchy', 'branchy-validation')) {
        $scopePath = Join-Path $root $scope
        if (-not (Test-Path -LiteralPath $scopePath -PathType Container)) {
            throw "Missing source directory: $scope"
        }
        $pending = [Collections.Generic.Stack[string]]::new()
        $pending.Push($scopePath)
        while ($pending.Count -gt 0) {
            $directory = $pending.Pop()
            if ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Source links are not allowed in a public snapshot.'
            }
            foreach ($entry in Get-ChildItem -LiteralPath $directory -Force) {
                if ($entry.PSIsContainer -and $entry.Name -in $excludedDirectories) {
                    $excludedCount++
                    continue
                }
                if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                    throw 'Source links are not allowed in a public snapshot.'
                }
                if ($entry.PSIsContainer) {
                    $pending.Push($entry.FullName)
                    continue
                }
                $relative = $entry.FullName.Substring($root.TrimEnd('\', '/').Length + 1).Replace('\', '/')
                $blockedName = $entry.Name -match '(?i)(^\.env|\.env$|\.(pfx|p12|pem|key|snk|jks|keystore|cer|crt|user|suo|log|dmp|dump)$|^(?:secrets|credentials)(?:\.|$)|^appsettings\..*\.json$)'
                $allowed = $entry.Extension.ToLowerInvariant() -in $allowedExtensions -or $entry.Name -in @('LICENSE', '.gitignore', '.gitattributes')
                if ($blockedName -or -not $allowed -or $relative -eq 'branchy/docs/public-source-readme.md') {
                    $excludedCount++
                    continue
                }
                $files.Add([pscustomobject]@{ Source = $entry.FullName; Relative = $relative })
            }
        }
    }
    $rootFiles = [ordered]@{
        'branchy/docs/public-source-readme.md' = 'README.md'
        'branchy/LICENSE' = 'LICENSE'
        'branchy/CONTRIBUTING.md' = 'CONTRIBUTING.md'
        'branchy/SECURITY.md' = 'SECURITY.md'
        'branchy/THIRD-PARTY-NOTICES.md' = 'THIRD-PARTY-NOTICES.md'
        'branchy/.gitignore' = '.gitignore'
        'branchy/.gitattributes' = '.gitattributes'
    }
    foreach ($relative in $rootFiles.Keys) {
        $path = Join-Path $root $relative
        # A previously exported snapshot already has its public root README.
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -and $relative -eq 'branchy/docs/public-source-readme.md') {
            $path = Join-Path $root 'README.md'
        }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing public document: $relative"
        }
        if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Public document links are not allowed.'
        }
        $files.Add([pscustomobject]@{ Source = $path; Relative = $rootFiles[$relative] })
    }
    return [pscustomobject]@{ Root = $root; Files = @($files.ToArray() | Sort-Object Relative); ExcludedCount = $excludedCount }
}

function Assert-PublicSourceSafe {
    param([Parameter(Mandatory)][object[]]$Files)

    $patterns = [ordered]@{
        PrivateKey = '-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----'
        GitHubToken = '(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})'
        AwsAccessKey = '(?:AKIA|ASIA)[A-Z0-9]{16}'
        AzureAccountKey = '(?i)AccountKey=[A-Za-z0-9+/]{40,}={0,2}'
        CredentialUrl = 'https?://[^\s/"''<>]+:[^\s/"''<>]+@'
        AssignedSecret = '(?i)\b(?:password|client_secret|api_key|access_token)\b\s*[:=]\s*["''][A-Za-z0-9+/=_-]{12,}["'']'
        PersonalPath = '(?<![A-Za-z0-9])[A-Za-z]:[\\/]|/(?:Users|home)/'
    }
    $findings = [Collections.Generic.List[string]]::new()
    foreach ($file in $Files) {
        $extension = [IO.Path]::GetExtension($file.Source).ToLowerInvariant()
        $binary = $extension -in @('.png', '.ico', '.ttf')
        if ($binary) {
            continue
        }
        $lineNumber = 0
        foreach ($line in [IO.File]::ReadLines($file.Source)) {
            $lineNumber++
            foreach ($rule in $patterns.Keys) {
                if ($line -match $patterns[$rule]) {
                    $findings.Add(('{0}:{1} [{2}]' -f $file.Relative, $lineNumber, $rule))
                }
            }
            # Preserve legal attribution; only flag non-example emails outside notices.
            if ($file.Relative -notmatch '(?i)(LICENSE|THIRD-PARTY-NOTICES)' -and $line -match '[A-Za-z0-9._%+-]+@(?!example\.(?:invalid|com|org|net)\b)[A-Za-z0-9.-]+\.[A-Za-z]{2,}') {
                $findings.Add(('{0}:{1} [EmailNeedsReview]' -f $file.Relative, $lineNumber))
            }
        }
    }
    if ($findings.Count -gt 0) {
        # Report locations only, never echo credentials or personal data.
        $findings | Sort-Object -Unique | ForEach-Object { Write-Warning $_ }
        throw 'Public source review failed. Inspect the reported files privately before exporting.'
    }
}
