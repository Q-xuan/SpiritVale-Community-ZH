[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [string]$CandidatePath,
    [Parameter(Mandatory = $true)]
    [string]$AuthorityPath,
    [string]$AuditPath,
    [switch]$AllowOverwrite,
    [switch]$ReportOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

function Read-JsonMap([string]$Path) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    Add-Type -AssemblyName System.Web.Extensions
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $parsed = $serializer.DeserializeObject(
        [System.IO.File]::ReadAllText($resolved, [System.Text.Encoding]::UTF8)
    )
    if ($parsed -isnot [System.Collections.IDictionary]) {
        throw "Expected a JSON object: $Path"
    }
    $map = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([System.StringComparer]::Ordinal)
    foreach ($key in $parsed.Keys) {
        $value = [string]$parsed[$key]
        if ([string]::IsNullOrWhiteSpace([string]$key) -or [string]::IsNullOrWhiteSpace($value)) {
            throw "Candidate contains an empty key or value: $Path"
        }
        if ($value.Contains("`t") -or $value.Contains("`r") -or $value.Contains("`n")) {
            throw "Candidate contains an unsafe tab/newline value: $key"
        }
        $map.Add([string]$key, $value)
    }
    return $map
}

$candidates = Read-JsonMap $CandidatePath
$authority = Read-JsonMap $AuthorityPath

if ($AuditPath) {
    $auditResolved = (Resolve-Path -LiteralPath $AuditPath).Path
    Add-Type -AssemblyName System.Web.Extensions
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $audit = $serializer.DeserializeObject(
        [System.IO.File]::ReadAllText($auditResolved, [System.Text.Encoding]::UTF8)
    )
    if ($audit['overall_passed'] -ne $true) {
        throw "Candidate audit is not passing: $AuditPath"
    }
    $reportedCount = [int]$audit['candidates']['total']
    if ($reportedCount -ne $candidates.Count) {
        throw "Candidate/audit count mismatch: JSON=$($candidates.Count), audit=$reportedCount"
    }
}

$collisions = @(
    foreach ($key in $candidates.Keys) {
        if ($authority.ContainsKey($key) -and $authority[$key] -ne $candidates[$key]) {
            [PSCustomObject]@{ Key = $key; Existing = $authority[$key]; Candidate = $candidates[$key] }
        }
    }
)
if ($collisions.Count -gt 0 -and -not $AllowOverwrite) {
    $sample = ($collisions | Select-Object -First 12 | ForEach-Object {
        "[$($_.Key)] '$($_.Existing)' -> '$($_.Candidate)'"
    }) -join "`n"
    throw "Refusing to overwrite $($collisions.Count) reviewed values. Re-run with -AllowOverwrite after review:`n$sample"
}

foreach ($key in $candidates.Keys) {
    $authority[$key] = $candidates[$key]
}

Write-Output "Candidates=$($candidates.Count) Collisions=$($collisions.Count) AuthorityAfter=$($authority.Count)"
if ($ReportOnly) {
    exit 0
}

$sorted = New-Object 'System.Collections.Generic.SortedDictionary[string,string]' ([System.StringComparer]::Ordinal)
foreach ($key in $authority.Keys) {
    $sorted.Add($key, $authority[$key])
}
$json = $sorted | ConvertTo-Json -Depth 3
$outputPath = (Resolve-Path -LiteralPath $AuthorityPath).Path
if ($PSCmdlet.ShouldProcess($outputPath, 'Merge reviewed localization candidates')) {
    [System.IO.File]::WriteAllText(
        $outputPath,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false)
    )
    $verified = Read-JsonMap $AuthorityPath
    if ($verified.Count -ne $authority.Count) {
        throw "Post-write authority count mismatch: $($verified.Count) vs $($authority.Count)"
    }
    Write-Output "Merged and verified: $outputPath"
}
