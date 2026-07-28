[CmdletBinding()]
param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '..\..'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$ciPath = Join-Path $RepositoryRoot '.github\workflows\ci.yml'
$releasePath = Join-Path $RepositoryRoot '.github\workflows\release.yml'
foreach ($path in @($ciPath, $releasePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Workflow is missing: $path" }
}
$ci = [System.IO.File]::ReadAllText($ciPath)
$release = [System.IO.File]::ReadAllText($releasePath)

function Require-Pattern([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -notmatch $Pattern) { throw $Message }
}

Require-Pattern $ci '(?m)^\s*push:\s*$' 'CI must run on push.'
Require-Pattern $ci '(?m)^\s*pull_request:\s*$' 'CI must run on pull requests.'
Require-Pattern $ci "(?m)^\s*- '\*\*'\s*$" 'CI must check pushes to every branch.'
Require-Pattern $ci '(?m)^\s*- master\s*$' 'CI pull requests must target master.'
Require-Pattern $ci '(?ms)^permissions:\s*\r?\n\s+contents:\s+read\s*$' 'CI must use read-only contents permission.'
if ($ci -match '(?m)^\s*(pull_request_target|tags|workflow_dispatch):') { throw 'CI contains an unsafe or release-only trigger.' }

Require-Pattern $release "(?m)^\s*- 'v\*\.\*\.\*'\s*$" 'Release must use the broad v*.*.* tag glob and strict code validation.'
Require-Pattern $release 'Test-SpiritValeReleaseMetadata\.ps1' 'Release must run the shared metadata validator.'
Require-Pattern $release 'RequireLiveVerification' 'Release must require live verification.'
Require-Pattern $release 'ArtifactsDirectory' 'Release must verify frozen package artifacts.'
Require-Pattern $release '(?m)^\s*runs-on:\s*\[self-hosted, Windows, X64, spiritvale-release\]\s*$' 'Release build must use the dedicated Windows runner.'
Require-Pattern $release 'gh release create' 'Release must publish with GitHub CLI.'
Require-Pattern $release '--verify-tag' 'Release must refuse missing tags.'
Require-Pattern $release '(?ms)publish:\s+.*?permissions:\s*\r?\n\s+contents:\s+write' 'Only the publish job may request write permission.'
if ($release -match '(?m)^\s*(workflow_dispatch|pull_request_target):') { throw 'Release contains an unauthorized trigger.' }

$pins = @(
    '3d3c42e5aac5ba805825da76410c181273ba90b1',
    '67a3573c9a986a3f9c594539f4ab511d57bb3ce9',
    'ea165f8d65b6e75b540449e92b4886f43607fa02',
    '634f93cb2916e3fdff6788551b99b062d0335ce0'
)
foreach ($pin in $pins) {
    if (($ci + $release) -notmatch [regex]::Escape('@' + $pin)) { throw "Pinned action SHA is missing: $pin" }
}
Write-Output 'Workflow policy: PASS'
