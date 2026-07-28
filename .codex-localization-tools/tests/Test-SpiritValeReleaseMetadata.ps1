[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$validator = Join-Path $repositoryRoot '.codex-localization-tools\skills\update-spiritvale-localization\scripts\Test-SpiritValeReleaseMetadata.ps1'
$engine = (Get-Process -Id $PID).Path
$engineArgs = @('-NoProfile')
if ($env:OS -eq 'Windows_NT') { $engineArgs += @('-ExecutionPolicy', 'Bypass') }
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('spiritvale-release-metadata-' + [Guid]::NewGuid().ToString('N'))
$passed = 0

function Write-Utf8([string]$Path, [string]$Content) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function New-ValidFixture([string]$Name) {
    $root = Join-Path $fixtureRoot $Name
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    Write-Utf8 (Join-Path $root 'VERSION') "1.2.30`n"
    Write-Utf8 (Join-Path $root 'CHANGELOG.md') @'
# Changelog

## [1.2.30] - 2026-07-27

### Added

- Current release notes.

## [1.2.29] - 2026-07-23

### Fixed

- Previous release notes.
'@
    Write-Utf8 (Join-Path $root '.codex-localization-tools\SpiritVale.RuntimeLocalization\RuntimeLocalizationPlugin.cs') 'public const string PluginVersion = "1.2.30";'
    Write-Utf8 (Join-Path $root '.codex-localization-tools\installer\PatchService.cs') 'public const string Version = "1.2.30";'
    Write-Utf8 (Join-Path $root '.codex-localization-tools\installer\SpiritVale.ChinesePatch.Installer.csproj') @'
<Project><PropertyGroup>
<Version>1.2.30</Version>
<FileVersion>1.2.30.0</FileVersion>
<Product>SpiritVale v1.2.30</Product>
<AssemblyTitle>SpiritVale v1.2.30</AssemblyTitle>
</PropertyGroup></Project>
'@
    Write-Utf8 (Join-Path $root '.codex-localization-tools\artifacts\live-verification.json') '{"patch_version":"1.2.29"}'
    return $root
}

function Invoke-Validator([string]$Root, [string[]]$ExtraArguments = @()) {
    $arguments = $engineArgs + @('-File', $validator, '-RepositoryRoot', $Root) + $ExtraArguments
    $previousErrorAction = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & $engine @arguments 2>&1 | Out-String
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorAction
    }
    return [PSCustomObject]@{ ExitCode = $exitCode; Output = $output }
}

function ConvertTo-ComparableDiagnostic([string]$Text) {
    $ansiPattern = ([string][char]27) + '\[[0-?]*[ -/]*[@-~]'
    $plain = [regex]::Replace($Text, $ansiPattern, '')
    $plain = [regex]::Replace($plain, '(?m)^\s*\|\s?', '')
    return [regex]::Replace($plain, '\s+', ' ').Trim()
}

function Assert-Pass([string]$Name, [string]$Root, [string[]]$Arguments = @()) {
    $result = Invoke-Validator $Root $Arguments
    if ($result.ExitCode -ne 0) { throw "$Name expected success but failed: $($result.Output)" }
    $script:passed++
}

function Assert-Fail([string]$Name, [string]$Root, [string]$Expected, [string[]]$Arguments = @()) {
    $result = Invoke-Validator $Root $Arguments
    if ($result.ExitCode -eq 0) { throw "$Name expected failure but succeeded." }
    $actualDiagnostic = ConvertTo-ComparableDiagnostic $result.Output
    $expectedDiagnostic = ConvertTo-ComparableDiagnostic $Expected
    if ($actualDiagnostic -notmatch [regex]::Escape($expectedDiagnostic)) {
        throw "$Name failed for the wrong reason. Expected '$Expected'; output: $($result.Output)"
    }
    $script:passed++
}

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null

    $formattingExpected = 'Live verification version 1.2.29 does not match required runtime version 1.2.30'
    $escape = [string][char]27
    $formattingProbe = "Live verification version ${escape}[31;1m1.2.29${escape}[0m`n    | does not match required runtime version 1.2.30"
    if ((ConvertTo-ComparableDiagnostic $formattingProbe) -ne $formattingExpected) {
        throw 'PowerShell diagnostic formatting normalization failed.'
    }
    $passed++

    $valid = New-ValidFixture 'valid'
    Assert-Pass 'valid metadata' $valid
    Assert-Pass 'matching tag' $valid @('-Tag', 'v1.2.30')
    $notes = Join-Path $valid 'out\release-notes.md'
    Assert-Pass 'release notes extraction' $valid @('-ReleaseNotesPath', $notes)
    if ((Get-Content -LiteralPath $notes -Raw -Encoding UTF8) -notmatch 'Current release notes') {
        throw 'release notes extraction omitted the current section body.'
    }
    $passed++

    $missing = New-ValidFixture 'missing'
    Remove-Item -LiteralPath (Join-Path $missing 'CHANGELOG.md') -Force
    Assert-Fail 'missing changelog' $missing 'Required release file is missing: CHANGELOG.md'

    $duplicate = New-ValidFixture 'duplicate'
    Add-Content -LiteralPath (Join-Path $duplicate 'CHANGELOG.md') -Encoding UTF8 -Value "`n## [1.2.30] - 2026-07-27`n`n- Duplicate.`n"
    Assert-Fail 'duplicate section' $duplicate 'duplicate release section 1.2.30'

    $empty = New-ValidFixture 'empty'
    Write-Utf8 (Join-Path $empty 'CHANGELOG.md') "# Changelog`n`n## [1.2.30] - 2026-07-27`n`n### Added`n`n## [1.2.29] - 2026-07-23`n`n- Previous.`n"
    Assert-Fail 'empty section' $empty 'release 1.2.30 is empty'

    $outOfOrder = New-ValidFixture 'out-of-order'
    Write-Utf8 (Join-Path $outOfOrder 'CHANGELOG.md') "# Changelog`n`n## [1.2.30] - 2026-07-27`n`n- Current.`n`n## [1.2.28] - 2026-07-22`n`n- Older.`n`n## [1.2.29] - 2026-07-23`n`n- Out of order.`n"
    Assert-Fail 'out-of-order sections' $outOfOrder 'release sections are out of order at 1.2.29'

    $future = New-ValidFixture 'future'
    (Get-Content -LiteralPath (Join-Path $future 'CHANGELOG.md') -Raw -Encoding UTF8).Replace('2026-07-27', '2099-01-01') |
        Set-Content -LiteralPath (Join-Path $future 'CHANGELOG.md') -Encoding UTF8
    Assert-Fail 'future date' $future 'release 1.2.30 has a future date'

    $mismatch = New-ValidFixture 'version-mismatch'
    (Get-Content -LiteralPath (Join-Path $mismatch '.codex-localization-tools\installer\PatchService.cs') -Raw -Encoding UTF8).Replace('1.2.30', '1.2.31') |
        Set-Content -LiteralPath (Join-Path $mismatch '.codex-localization-tools\installer\PatchService.cs') -Encoding UTF8
    Assert-Fail 'version mismatch' $mismatch 'Installer version 1.2.31 does not match VERSION 1.2.30'

    Assert-Fail 'tag without v' $valid "Tag '1.2.30' does not match VERSION v1.2.30" @('-Tag', '1.2.30')
    Assert-Fail 'malformed tag' $valid "Tag 'v1.2' does not match VERSION v1.2.30" @('-Tag', 'v1.2')
    Assert-Fail 'mismatched tag' $valid "Tag 'v1.2.31' does not match VERSION v1.2.30" @('-Tag', 'v1.2.31')
    Assert-Fail 'stale live version' $valid 'Live verification version 1.2.29 does not match required runtime version 1.2.30' @('-RequireLiveVerification')

    $global:LASTEXITCODE = 0
    Write-Output "Release metadata tests passed: $passed"
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
    Write-Output "Cleanup: removed $fixtureRoot"
}
