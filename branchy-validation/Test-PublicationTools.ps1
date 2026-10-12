[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $sourceRoot 'branchy/tools/PublicSource.Common.ps1')
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixture = Join-Path $temporaryRoot ('branchy-publication-test-' + [Guid]::NewGuid().ToString('N'))
$checks = 0

function Assert-Check {
    param([bool]$Condition, [string]$Description)
    if (-not $Condition) { throw "FAIL: $Description" }
    $script:checks++
    Write-Host "PASS: $Description"
}

function Assert-Rejected {
    param([string]$Content, [string]$Description)
    $path = Join-Path $fixture 'branchy/fixture.cs'
    [IO.File]::WriteAllText($path, $Content)
    $failed = $false
    try {
        Assert-PublicSourceSafe -Files @([pscustomobject]@{ Source = $path; Relative = 'branchy/fixture.cs' }) -WarningAction SilentlyContinue
    }
    catch {
        if ($_.Exception.Message -notlike 'Public source review failed.*') { throw }
        $failed = $true
    }
    Assert-Check $failed $Description
}

try {
    New-Item -ItemType Directory -Path (Join-Path $fixture 'branchy'), (Join-Path $fixture 'branchy-validation') | Out-Null
    foreach ($document in @('LICENSE', 'CONTRIBUTING.md', 'SECURITY.md', 'THIRD-PARTY-NOTICES.md', '.gitignore', '.gitattributes')) {
        Copy-Item -LiteralPath (Join-Path $sourceRoot ('branchy/' + $document)) -Destination (Join-Path $fixture ('branchy/' + $document))
    }
    [IO.File]::WriteAllText((Join-Path $fixture 'README.md'), '# Public fixture')
    [IO.File]::WriteAllText((Join-Path $fixture 'branchy/safe.cs'), '// Public fixture')
    foreach ($name in @('.env', '.env.local', '.envrc', 'private.pfx', 'private.key', 'public.cer', 'private.pubxml.user', 'build.log', 'crash.dmp', 'credentials.json', 'unexpected.dat')) {
        [IO.File]::WriteAllText((Join-Path $fixture ('branchy/' + $name)), 'PRIVATE-FIXTURE-SENTINEL')
    }
    New-Item -ItemType Directory -Path (Join-Path $fixture 'branchy/bin'), (Join-Path $fixture '.git') | Out-Null
    [IO.File]::WriteAllText((Join-Path $fixture 'branchy/bin/leak.cs'), 'PRIVATE-FIXTURE-SENTINEL')
    [IO.File]::WriteAllText((Join-Path $fixture '.git/config'), 'PRIVATE-FIXTURE-SENTINEL')
    $selected = Get-PublicSourceSelection -SourceDirectory $fixture
    Assert-Check ($selected.Files.Relative -contains 'branchy/safe.cs') 'Expected source included'
    Assert-Check ($selected.Files.Relative -contains 'README.md') 'Public root documents included'
    Assert-Check ($selected.Files.Relative -notcontains 'branchy/bin/leak.cs') 'Build directories excluded'
    Assert-Check (@($selected.Files | Where-Object { [IO.File]::ReadAllText($_.Source) -eq 'PRIVATE-FIXTURE-SENTINEL' }).Count -eq 0) 'Environment, keys, certificates, user files and history excluded'
    Assert-PublicSourceSafe -Files $selected.Files
    Assert-Rejected ('ghp_' + ('A' * 36)) 'GitHub token blocked'
    Assert-Rejected ('AK' + 'IA' + ('A' * 16)) 'AWS access key blocked'
    Assert-Rejected ('-----BEGIN ' + 'PRIVATE KEY-----') 'Private key block rejected'
    Assert-Rejected ('C' + ':' + '\Users\Private\project') 'Personal Windows path blocked'
    Assert-Rejected ('https://' + 'person' + ':' + 'password123456' + '@example.invalid/project') 'Credential-bearing URL blocked'
    Assert-Rejected ('person' + '@' + 'private.invalid') 'Non-example email requires review'
    [IO.File]::WriteAllText((Join-Path $fixture 'branchy/fixture.cs'), 'validation@example.invalid')
    Assert-PublicSourceSafe -Files @([pscustomobject]@{ Source = (Join-Path $fixture 'branchy/fixture.cs'); Relative = 'branchy/fixture.cs' })
    Assert-Check $true 'Reserved example email allowed'
    "Publication tooling checks passed: $checks"
}
finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $expectedPrefix = $temporaryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar + 'branchy-publication-test-'
    if (-not $resolvedFixture.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to remove a fixture outside the expected temporary directory.'
    }
    if (Test-Path -LiteralPath $resolvedFixture) {
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
