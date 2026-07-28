[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$guard = Join-Path $repositoryRoot '.github\scripts\Test-PublicTree.ps1'
$engine = (Get-Process -Id $PID).Path
$engineArgs = @('-NoProfile')
if ($env:OS -eq 'Windows_NT') { $engineArgs += @('-ExecutionPolicy', 'Bypass') }
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('spiritvale-public-tree-' + [Guid]::NewGuid().ToString('N'))
$passed = 0

function Write-Text([string]$Path, [string]$Content) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function New-Fixture([string]$Name) {
    $root = Join-Path $fixtureRoot $Name
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    & git -c core.excludesFile= -C $root init -q
    foreach ($path in @(
        'README.md', 'VERSION', 'CHANGELOG.md', '.gitignore', '.gitattributes',
        '.github/workflows/ci.yml', '.github/workflows/release.yml',
        '.github/scripts/Test-PublicTree.ps1', '.github/scripts/Test-Repository.ps1',
        '.codex-localization-tools/skills/update-spiritvale-localization/SKILL.md',
        '.codex-localization-tools/skills/update-spiritvale-localization/scripts/Test-SpiritValeReleaseMetadata.ps1'
    )) { Write-Text (Join-Path $root $path) "safe`n" }
    & git -c core.excludesFile= -C $root add -f -- .
    if ($LASTEXITCODE -ne 0) { throw 'Failed to stage fixture.' }
    return $root
}

function Invoke-Guard([string]$Root, [long]$MaxBytes = 10MB) {
    $arguments = $engineArgs + @('-File', $guard, '-RepositoryRoot', $Root, '-MaxFileBytes', $MaxBytes)
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & $engine @arguments 2>&1 | Out-String
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }
    return [PSCustomObject]@{ ExitCode = $code; Output = $output }
}

function Assert-Pass([string]$Name, [string]$Root) {
    $result = Invoke-Guard $Root
    if ($result.ExitCode -ne 0) { throw "$Name expected success: $($result.Output)" }
    $script:passed++
}

function Assert-Fail([string]$Name, [string]$Root, [string]$Expected, [long]$MaxBytes = 10MB) {
    $result = Invoke-Guard $Root $MaxBytes
    if ($result.ExitCode -eq 0) { throw "$Name expected failure." }
    if ($result.Output -notmatch [regex]::Escape($Expected)) { throw "$Name failed for the wrong reason: $($result.Output)" }
    $script:passed++
}

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    $safe = New-Fixture 'safe'
    Assert-Pass 'safe tree' $safe

    $hiddenRequired = New-Fixture 'hidden-required'
    $isWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)
    if ($isWindows) {
        $hiddenPath = Join-Path $hiddenRequired '.gitattributes'
        $hiddenItem = Get-Item -LiteralPath $hiddenPath -Force
        $hiddenItem.Attributes = $hiddenItem.Attributes -bor [System.IO.FileAttributes]::Hidden
    }
    Assert-Pass 'hidden required file' $hiddenRequired

    $binary = New-Fixture 'binary'
    Write-Text (Join-Path $binary 'GameAssembly.dll') 'not a real binary'
    & git -c core.excludesFile= -C $binary add -f -- GameAssembly.dll
    Assert-Fail 'game binary' $binary 'Forbidden public file type: GameAssembly.dll'

    $large = New-Fixture 'large'
    [System.IO.File]::WriteAllBytes((Join-Path $large 'large.dat'), ([byte[]]::new(2048)))
    & git -c core.excludesFile= -C $large add -f -- large.dat
    Assert-Fail 'oversized file' $large 'Oversized public file: large.dat' 1024

    $secretName = New-Fixture 'secret-name'
    Write-Text (Join-Path $secretName '.env') 'SAFE=value'
    & git -c core.excludesFile= -C $secretName add -f -- .env
    Assert-Fail 'secret filename' $secretName 'Secret-like public filename: .env'

    $secretContent = New-Fixture 'secret-content'
    $fakeToken = 'gh' + 'p_' + ('A' * 36)
    Write-Text (Join-Path $secretContent 'config.txt') $fakeToken
    & git -c core.excludesFile= -C $secretContent add -f -- config.txt
    Assert-Fail 'secret signature' $secretContent 'Secret signature found in public file: config.txt'

    Write-Output "Repository guard tests passed: $passed"
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
    Write-Output "Cleanup: removed $fixtureRoot"
}
