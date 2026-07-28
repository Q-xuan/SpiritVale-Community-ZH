[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$Tag,
    [switch]$RequireLiveVerification,
    [string]$ArtifactsDirectory,
    [string]$ReleaseNotesPath,
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RequiredFile([string]$Root, [string]$RelativePath) {
    $path = Join-Path $Root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required release file is missing: $RelativePath"
    }
    return (Resolve-Path -LiteralPath $path).Path
}

function ConvertTo-StrictVersion([string]$Value, [string]$Label) {
    if ($Value -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
        throw "$Label must use strict MAJOR.MINOR.PATCH syntax; found '$Value'."
    }
    return [version]$Value
}

function Get-SingleCapture([string]$Path, [string]$Pattern, [string]$Label) {
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $matches = [regex]::Matches($text, $Pattern)
    if ($matches.Count -ne 1) {
        throw "$Label expected one value in $Path; found $($matches.Count)."
    }
    return $matches[0].Groups[1].Value
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Assert-Hash([string]$Path, [string]$Expected, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label is missing: $Path"
    }
    $actual = Get-Sha256 $Path
    if (-not [string]::Equals($actual, $Expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label SHA-256 mismatch: expected $Expected, actual $actual."
    }
}

if (-not $RepositoryRoot) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..\..\..\..'
}
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path

$versionPath = Resolve-RequiredFile $RepositoryRoot 'VERSION'
$versionLines = @([System.IO.File]::ReadAllLines($versionPath, [System.Text.Encoding]::UTF8) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($versionLines.Count -ne 1) { throw 'VERSION must contain exactly one non-empty line.' }
$version = $versionLines[0].Trim()
$versionValue = ConvertTo-StrictVersion $version 'VERSION'

$changelogPath = Resolve-RequiredFile $RepositoryRoot 'CHANGELOG.md'
$changelog = [System.IO.File]::ReadAllText($changelogPath, [System.Text.Encoding]::UTF8)
$strictHeadingPattern = '(?m)^## \[(?<version>(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))\] - (?<date>\d{4}-\d{2}-\d{2})\r?$'
$releaseHeadings = @([regex]::Matches($changelog, $strictHeadingPattern))
$allBracketHeadings = @([regex]::Matches($changelog, '(?m)^## \[(?<label>[^\]]+)\](?: - (?<date>[^\r\n]+))?\r?$'))
foreach ($heading in $allBracketHeadings) {
    if ($heading.Groups['label'].Value -eq 'Unreleased') { continue }
    if (-not [regex]::IsMatch($heading.Value, '^## \[(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\] - \d{4}-\d{2}-\d{2}\r?$')) {
        throw "Malformed CHANGELOG release heading: $($heading.Value.Trim())"
    }
}
if ($releaseHeadings.Count -eq 0) { throw 'CHANGELOG.md has no release sections.' }

$seenVersions = @{}
$previousVersion = $null
$allLevelTwo = @([regex]::Matches($changelog, '(?m)^## .+\r?$'))
$currentHeading = $null
$currentNotes = $null
foreach ($heading in $releaseHeadings) {
    $headingVersion = $heading.Groups['version'].Value
    if ($seenVersions.ContainsKey($headingVersion)) {
        throw "CHANGELOG.md contains duplicate release section $headingVersion."
    }
    $seenVersions[$headingVersion] = $true
    $parsedVersion = ConvertTo-StrictVersion $headingVersion 'CHANGELOG version'
    if ($null -ne $previousVersion -and $previousVersion.CompareTo($parsedVersion) -le 0) {
        throw "CHANGELOG.md release sections are out of order at $headingVersion."
    }
    $previousVersion = $parsedVersion

    try {
        $date = [DateTime]::ParseExact(
            $heading.Groups['date'].Value,
            'yyyy-MM-dd',
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal)
    } catch {
        throw "CHANGELOG.md has an invalid date for $headingVersion."
    }
    if ($date.Date -gt [DateTime]::UtcNow.Date) {
        throw "CHANGELOG.md release $headingVersion has a future date."
    }

    $nextHeading = @($allLevelTwo | Where-Object { $_.Index -gt $heading.Index } |
        Sort-Object Index | Select-Object -First 1)
    $sectionEnd = if ($nextHeading.Count -eq 1) { $nextHeading[0].Index } else { $changelog.Length }
    $section = $changelog.Substring($heading.Index + $heading.Length, $sectionEnd - ($heading.Index + $heading.Length)).Trim()
    $contentLines = @($section -split '\r?\n' | Where-Object {
        $line = $_.Trim()
        $line -and -not $line.StartsWith('#') -and -not $line.StartsWith('<!--')
    })
    if ($contentLines.Count -eq 0) {
        throw "CHANGELOG.md release $headingVersion is empty."
    }
    if ($headingVersion -eq $version) {
        if ($null -ne $currentHeading) { throw "CHANGELOG.md contains duplicate release section $version." }
        $currentHeading = $heading
        $currentNotes = $section
    }
}
if ($null -eq $currentHeading) { throw "CHANGELOG.md is missing release section $version." }

$pluginPath = Resolve-RequiredFile $RepositoryRoot '.codex-localization-tools\SpiritVale.RuntimeLocalization\RuntimeLocalizationPlugin.cs'
$installerService = Resolve-RequiredFile $RepositoryRoot '.codex-localization-tools\installer\PatchService.cs'
$installerProject = Resolve-RequiredFile $RepositoryRoot '.codex-localization-tools\installer\SpiritVale.ChinesePatch.Installer.csproj'
$pluginVersion = Get-SingleCapture $pluginPath 'public const string PluginVersion = "([^"]+)";' 'PluginVersion'
$installerVersions = @(
    (Get-SingleCapture $installerService 'public const string Version = "([^"]+)";' 'PatchInfo.Version'),
    (Get-SingleCapture $installerProject '<Version>([^<]+)</Version>' 'installer Version'),
    (Get-SingleCapture $installerProject '<FileVersion>(\d+\.\d+\.\d+)(?:\.\d+)?</FileVersion>' 'installer FileVersion'),
    (Get-SingleCapture $installerProject '<Product>[^<]*\bv(\d+\.\d+\.\d+)\b[^<]*</Product>' 'installer Product version'),
    (Get-SingleCapture $installerProject '<AssemblyTitle>[^<]*\bv(\d+\.\d+\.\d+)\b[^<]*</AssemblyTitle>' 'installer AssemblyTitle version')
)
foreach ($installerVersion in $installerVersions) {
    $null = ConvertTo-StrictVersion $installerVersion 'installer version'
    if ($installerVersion -ne $version) {
        throw "Installer version $installerVersion does not match VERSION $version."
    }
}
$pluginVersionValue = ConvertTo-StrictVersion $pluginVersion 'PluginVersion'
$releaseKind = if ($pluginVersion -eq $version) { 'content' } else { 'installer-only' }
if ($releaseKind -eq 'installer-only' -and $pluginVersionValue.CompareTo($versionValue) -ge 0) {
    throw "Installer-only VERSION $version must be newer than PluginVersion $pluginVersion."
}

if ($Tag) {
    if ($Tag -ne "v$version") { throw "Tag '$Tag' does not match VERSION v$version." }
}

$livePath = Join-Path $RepositoryRoot '.codex-localization-tools\artifacts\live-verification.json'
$liveVersion = $null
$liveVerified = $false
if (Test-Path -LiteralPath $livePath -PathType Leaf) {
    $live = Get-Content -LiteralPath $livePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $liveVersion = [string]$live.patch_version
    if ($RequireLiveVerification) {
        $expectedLiveVersion = if ($releaseKind -eq 'content') { $version } else { $pluginVersion }
        if ($liveVersion -ne $expectedLiveVersion) {
            throw "Live verification version $liveVersion does not match required runtime version $expectedLiveVersion."
        }
        $toolRoot = Join-Path $RepositoryRoot '.codex-localization-tools'
        Assert-Hash (Join-Path $toolRoot 'SpiritVale.RuntimeLocalization\bin\Release\netstandard2.1\SpiritVale.RuntimeLocalization.dll') ([string]$live.plugin_sha256) 'Live-verified plugin'
        Assert-Hash (Join-Path $toolRoot 'artifacts\translations.tsv') ([string]$live.dictionary_sha256) 'Live-verified dictionary'
        Assert-Hash (Join-Path $toolRoot 'artifacts\bilingual-entity-catalog.tsv') ([string]$live.bilingual_catalog_sha256) 'Live-verified bilingual catalog'
        Assert-Hash (Join-Path $toolRoot 'artifacts\bilingual-entity-catalog.audit.json') ([string]$live.bilingual_catalog_audit_sha256) 'Live-verified bilingual catalog audit'
        $liveVerified = $true
    }
} elseif ($RequireLiveVerification) {
    throw 'Live verification record is required for release.'
}

$artifactsVerified = $false
if ($ArtifactsDirectory) {
    $ArtifactsDirectory = (Resolve-Path -LiteralPath $ArtifactsDirectory).Path
    $manifestPath = Resolve-RequiredFile $ArtifactsDirectory "release-v$version.json"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$manifest.patchVersion -ne $version) {
        throw "Release manifest version $($manifest.patchVersion) does not match VERSION $version."
    }
    foreach ($asset in @($manifest.mainInstaller, $manifest.compatibilityPackage)) {
        $assetPath = Join-Path $ArtifactsDirectory ([string]$asset.file)
        Assert-Hash $assetPath ([string]$asset.sha256) "Release asset $($asset.file)"
        if ((Get-Item -LiteralPath $assetPath).Length -ne [long]$asset.size) {
            throw "Release asset $($asset.file) size does not match its manifest."
        }
        $hashFile = $assetPath + '.sha256.txt'
        $hashText = [System.IO.File]::ReadAllText((Resolve-RequiredFile (Split-Path -Parent $hashFile) (Split-Path -Leaf $hashFile)))
        if ($hashText -notmatch [regex]::Escape([string]$asset.sha256)) {
            throw "Release asset $($asset.file) hash sidecar does not match its manifest."
        }
    }
    $artifactsVerified = $true
}

if ($ReleaseNotesPath) {
    $parent = Split-Path -Parent $ReleaseNotesPath
    if ($parent -and -not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($ReleaseNotesPath, $currentNotes + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

$result = [PSCustomObject]@{
    Version = $version
    ChangelogVersion = $currentHeading.Groups['version'].Value
    PluginVersion = $pluginVersion
    InstallerVersion = $installerVersions[0]
    ReleaseKind = $releaseKind
    LiveVersion = $liveVersion
    LiveVerified = $liveVerified
    Tag = $Tag
    ArtifactsVerified = $artifactsVerified
}
if ($AsJson) { $result | ConvertTo-Json -Compress } else { $result }
