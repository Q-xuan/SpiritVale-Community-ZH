[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..\..'),
    [ValidateSet('Ci', 'Release')]
    [string]$Mode = 'Ci',
    [switch]$IncludeUntracked
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$publicTree = Join-Path $PSScriptRoot 'Test-PublicTree.ps1'
$workflowPolicy = Join-Path $PSScriptRoot 'Test-WorkflowPolicy.ps1'
$agentLoopSafety = Join-Path $RepositoryRoot '.github\tests\Test-AgentLoopSafety.ps1'
$metadataValidator = Join-Path $RepositoryRoot '.codex-localization-tools\skills\update-spiritvale-localization\scripts\Test-SpiritValeReleaseMetadata.ps1'
$localizationLoop = Join-Path $RepositoryRoot '.codex-localization-tools\skills\update-spiritvale-localization\scripts\Invoke-SpiritValeLocalizationLoop.ps1'

$treeParameters = @{ RepositoryRoot = $RepositoryRoot; AsJson = $true }
if ($IncludeUntracked) { $treeParameters['IncludeUntracked'] = $true }
$tree = (& $publicTree @treeParameters) | ConvertFrom-Json
$metadata = (& $metadataValidator -RepositoryRoot $RepositoryRoot -AsJson) | ConvertFrom-Json
$null = & $workflowPolicy -RepositoryRoot $RepositoryRoot
$null = & $agentLoopSafety -RepositoryRoot $RepositoryRoot

$tokens = $null
$parseErrors = $null
$loopAst = [System.Management.Automation.Language.Parser]::ParseFile(
    $localizationLoop,
    [ref]$tokens,
    [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw "Localization loop failed to parse: $($parseErrors.Message -join '; ')" }
$recordLive = $loopAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Write-LiveVerification'
}, $true)
$metadataGate = $recordLive.Body.Find({
    param($node)
    $node -is [System.Management.Automation.Language.CommandAst] -and
        $node.GetCommandName() -eq 'Assert-ReleaseMetadata'
}, $true)
if ($null -eq $metadataGate) { throw 'RecordLive must validate release metadata before writing live evidence.' }

$readmeTitle = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'README.md') -Encoding UTF8 -TotalCount 1
if ($readmeTitle -ne '# SpiritVale-Community-ZH') { throw 'README title is incorrect.' }

foreach ($skillFolder in @('update-spiritvale-localization', 'package-spiritvale-localization')) {
    $skillPath = Join-Path $RepositoryRoot ".codex-localization-tools\skills\$skillFolder\SKILL.md"
    $skillText = [System.IO.File]::ReadAllText($skillPath, [System.Text.Encoding]::UTF8)
    if ($skillText -notmatch "(?s)^---\s*\r?\nname:\s*$([regex]::Escape($skillFolder))\s*\r?\ndescription:\s*.+?\r?\n---") {
        throw "Skill frontmatter is invalid: $skillFolder"
    }
    $uiPath = Join-Path $RepositoryRoot ".codex-localization-tools\skills\$skillFolder\agents\openai.yaml"
    $uiText = [System.IO.File]::ReadAllText($uiPath, [System.Text.Encoding]::UTF8)
    if ($uiText -notmatch [regex]::Escape('$' + $skillFolder)) { throw "Skill default prompt does not invoke `$$skillFolder." }
}

Write-Output "Repository checks: PASS ($($tree.Files) files, $($tree.Bytes) bytes, version $($metadata.Version), mode $Mode)"
