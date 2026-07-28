[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..\..'),
    [long]$MaxFileBytes = 10MB,
    [switch]$IncludeUntracked,
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path

$tracked = @(& git -c core.excludesFile= -C $RepositoryRoot ls-files)
if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }
$paths = [System.Collections.Generic.List[string]]::new()
foreach ($path in $tracked) { if ($path) { $paths.Add($path.Replace('\', '/')) } }
if ($IncludeUntracked) {
    $untracked = @(& git -c core.excludesFile= -C $RepositoryRoot ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files --others failed.' }
    foreach ($path in $untracked) { if ($path) { $paths.Add($path.Replace('\', '/')) } }
}
$paths = @($paths | Sort-Object -Unique)
if ($paths.Count -eq 0) { throw 'The public tree has no files.' }

$required = @(
    'README.md', 'VERSION', 'CHANGELOG.md', '.gitignore', '.gitattributes',
    '.github/workflows/ci.yml', '.github/workflows/release.yml',
    '.github/scripts/Test-PublicTree.ps1', '.github/scripts/Test-Repository.ps1',
    '.codex-localization-tools/skills/update-spiritvale-localization/SKILL.md',
    '.codex-localization-tools/skills/update-spiritvale-localization/scripts/Test-SpiritValeReleaseMetadata.ps1'
)
$missingRequired = @($required | Where-Object { $_ -notin $paths })
if ($missingRequired.Count -gt 0) { throw "Public tree is missing required files: $($missingRequired -join ', ')" }

$forbiddenPathPattern = '^(SpiritVale_Data|BepInEx|dotnet|D3D12|NuGet|\.codegraph|\.omo|\.uv-cache|\.SpiritValeChinesePatch|SpiritVale_Chinese_Patch_Release)(/|$)|/(bin|obj|dist|release|archive|payload-stage|self-test-[^/]+|release-selftest-[^/]+)(/|$)'
$forbiddenExtensionPattern = '(?i)\.(exe|dll|pdb|pyd|pyc|zip|bundle|assets|resS)$'
$secretNamePattern = '(?i)(^|/)(\.env(?:\..*)?|id_rsa|id_ed25519|credentials?(?:\..*)?|[^/]+\.(pem|key|pfx|p12))$'
$secretContentPattern = '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|ghp_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|AKIA[0-9A-Z]{16}'
$textExtensions = @('.cs', '.csproj', '.ps1', '.py', '.json', '.md', '.tsv', '.txt', '.yaml', '.yml', '')
$totalBytes = [long]0
$largestBytes = [long]0

foreach ($relativePath in $paths) {
    if ($relativePath -match $forbiddenPathPattern) { throw "Forbidden public path: $relativePath" }
    if ($relativePath -match $forbiddenExtensionPattern) { throw "Forbidden public file type: $relativePath" }
    if ($relativePath -match $secretNamePattern) { throw "Secret-like public filename: $relativePath" }
    $fullPath = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "Tracked public file is missing: $relativePath" }
    $item = Get-Item -LiteralPath $fullPath -Force
    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Public tree contains a reparse point: $relativePath" }
    if ($item.Length -gt $MaxFileBytes) { throw "Oversized public file: $relativePath ($($item.Length) bytes)" }
    $totalBytes += $item.Length
    if ($item.Length -gt $largestBytes) { $largestBytes = $item.Length }
    if ($item.Extension -in $textExtensions) {
        $hit = Select-String -LiteralPath $fullPath -Pattern $secretContentPattern -Quiet -ErrorAction SilentlyContinue
        if ($hit) { throw "Secret signature found in public file: $relativePath" }
    }
}

$result = [PSCustomObject]@{
    Status = 'pass'
    Files = $paths.Count
    Bytes = $totalBytes
    LargestFileBytes = $largestBytes
    IncludedUntracked = [bool]$IncludeUntracked
}
if ($AsJson) { $result | ConvertTo-Json -Compress } else { $result }
