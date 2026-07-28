[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..\..')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$loopPath = Join-Path $RepositoryRoot '.codex-localization-tools\skills\update-spiritvale-localization\scripts\Invoke-SpiritValeLocalizationLoop.ps1'
$payloadPath = Join-Path $RepositoryRoot '.codex-localization-tools\installer\Build-Payload.ps1'
$patchServicePath = Join-Path $RepositoryRoot '.codex-localization-tools\installer\PatchService.cs'
$pluginProjectPath = Join-Path $RepositoryRoot '.codex-localization-tools\SpiritVale.RuntimeLocalization\SpiritVale.RuntimeLocalization.csproj'
$failures = New-Object 'System.Collections.Generic.List[string]'

$tokens = $null
$parseErrors = $null
$loopAst = [System.Management.Automation.Language.Parser]::ParseFile(
    $loopPath,
    [ref]$tokens,
    [ref]$parseErrors)
if (@($parseErrors).Count -gt 0) {
    throw "Localization loop failed to parse: $($parseErrors.Message -join '; ')"
}
$targetProcessFunction = $loopAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Get-TargetGameProcess'
}, $true)
if ($null -eq $targetProcessFunction) {
    throw 'Get-TargetGameProcess was not found.'
}

$fakeProcess = [PSCustomObject]@{
    Id = 4242
    ProcessName = 'SpiritVale'
}
$fakeProcess | Add-Member -MemberType ScriptProperty -Name Path -Value {
    throw [System.UnauthorizedAccessException]::new('Process path access denied.')
}

$processLookupThrew = $false
try {
    $result = @(& {
        param([string]$FunctionText, $Process)

        $Paths = [PSCustomObject]@{
            GameExecutable = 'C:\SpiritVale\SpiritVale.exe'
        }
        function Get-Process {
            param([string]$Name, $ErrorAction)
            return $Process
        }

        . ([scriptblock]::Create($FunctionText))
        Get-TargetGameProcess
    } $targetProcessFunction.Extent.Text $fakeProcess)
} catch {
    $processLookupThrew = $true
}
if (-not $processLookupThrew) {
    $failures.Add("Get-TargetGameProcess returned $($result.Count) process(es) when process Id 4242 Path access failed; expected a throw.")
}

$tokens = $null
$parseErrors = $null
$payloadAst = [System.Management.Automation.Language.Parser]::ParseFile(
    $payloadPath,
    [ref]$tokens,
    [ref]$parseErrors)
if (@($parseErrors).Count -gt 0) {
    throw "Payload builder failed to parse: $($parseErrors.Message -join '; ')"
}
$configAssignment = $payloadAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and
        $node.Left.Extent.Text -eq '$localizationDisplayConfig'
}, $true)
if ($null -eq $configAssignment) {
    throw 'The localizationDisplayConfig assignment was not found.'
}
$localizationDisplayConfig = & ([scriptblock]::Create($configAssignment.Right.Extent.Text))
$configLines = @($localizationDisplayConfig -split '\r?\n')
$expectedConfigLines = @(
    '[Display]'
    'EntityNameMode = Chinese'
    'CompactSurfaceMode = Chinese'
    'TemporaryEnglishKey = Tab'
)
$configMatches = $configLines.Count -eq $expectedConfigLines.Count
if ($configMatches) {
    for ($index = 0; $index -lt $expectedConfigLines.Count; $index++) {
        if ($configLines[$index] -cne $expectedConfigLines[$index]) {
            $configMatches = $false
            break
        }
    }
}
if (-not $configMatches) {
    $failures.Add('Packaged localization display config does not exactly match the expected ordered lines.')
}

$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
$patchServiceSource = [System.IO.File]::ReadAllText($patchServicePath, $utf8)
$compactModePatterns = [ordered]@{
    ManifestDefault = 'public string DefaultCompactSurfaceMode \{ get; set; \} = "(?<Value>[A-Za-z]+)";'
    PayloadProbe = 'CompactSurfaceMode\\s\*=\\s\*(?<Value>[A-Za-z]+)\\s\*\$"'
    ActiveManifest = 'activeManifest\.DefaultCompactSurfaceMode == "(?<Value>[A-Za-z]+)"'
}
$compactModeValues = New-Object 'System.Collections.Generic.List[string]'
$compactModeContractMatches = $true
foreach ($contractName in $compactModePatterns.Keys) {
    $matches = [regex]::Matches($patchServiceSource, $compactModePatterns[$contractName])
    if ($matches.Count -ne 1) {
        $compactModeContractMatches = $false
        break
    }
    $compactModeValues.Add($matches[0].Groups['Value'].Value)
}
if ($compactModeContractMatches) {
    foreach ($compactModeValue in $compactModeValues) {
        if ($compactModeValue -cne 'Chinese') {
            $compactModeContractMatches = $false
            break
        }
    }
}
if (-not $compactModeContractMatches) {
    $failures.Add('Installer compact-surface manifest default, payload probe, and active-manifest check must all require Chinese.')
}

$debugTypeOutput = & dotnet msbuild $pluginProjectPath -nologo -p:Configuration=Release -getProperty:DebugType 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    throw "Failed to evaluate the release plugin DebugType: $debugTypeOutput"
}
$debugType = $debugTypeOutput.Trim()
if ($debugType -cne 'none') {
    $failures.Add('Release plugin DebugType must be none so identical sources hash equally across runner workspaces.')
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Output "FAIL: $failure"
    }
    throw "Agent loop safety tests failed: $($failures.Count)"
}

Write-Output 'Agent loop safety tests passed: 4'
