param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'),
    [string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$PluginDll,
    [string]$Translations,
    [string]$EntityCatalog
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'SpiritVale.ChinesePatch.Installer.csproj'
$projectText = [System.IO.File]::ReadAllText($projectPath)
$versionMatch = [regex]::Match($projectText, '<Version>(?<version>[^<]+)</Version>')
if (-not $versionMatch.Success) { throw 'Installer project version was not found.' }
$patchVersion = $versionMatch.Groups['version'].Value
$archiveRoot = Join-Path $PSScriptRoot 'release\archive'

# Build-Payload uses terminating errors for failures. Do not inspect
# LASTEXITCODE here because a child script that only uses cmdlets leaves it stale.
& (Join-Path $PSScriptRoot 'Build-Payload.ps1') `
    -GameRoot $GameRoot -PluginDll $PluginDll -Translations $Translations `
    -EntityCatalog $EntityCatalog

$existingExe = Join-Path $OutputDirectory 'SpiritVale_Chinese_Patch.exe'
if (Test-Path -LiteralPath $existingExe -PathType Leaf) {
    $existingHash = (Get-FileHash -LiteralPath $existingExe -Algorithm SHA256).Hash
    $existingVersion = (Get-Item -LiteralPath $existingExe).VersionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($existingVersion)) { $existingVersion = 'unknown' }
    New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
    $archiveBase = 'SpiritVale_Chinese_Patch_v{0}_{1}' -f $existingVersion, $existingHash.Substring(0, 12)
    $archiveExe = Join-Path $archiveRoot ($archiveBase + '.exe')
    if (-not (Test-Path -LiteralPath $archiveExe)) {
        Copy-Item -LiteralPath $existingExe -Destination $archiveExe
        Set-Content -LiteralPath (Join-Path $archiveRoot ($archiveBase + '.sha256.txt')) `
            -Value "$existingHash  $([System.IO.Path]::GetFileName($archiveExe))" -Encoding ascii
        Write-Host "Archived previous frozen installer: $archiveExe"
    }
}

dotnet publish $projectPath `
    -c Release -r win-x64 --self-contained true -o $OutputDirectory `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:PublishTrimmed=false
$exe = Join-Path $OutputDirectory 'SpiritVale_Chinese_Patch.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'Single-file installer publish did not produce its executable.'
}
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$sizeMb = [math]::Round((Get-Item -LiteralPath $exe).Length / 1MB, 2)
Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256.txt') `
    -Value "$hash  SpiritVale_Chinese_Patch.exe" -Encoding ascii
Write-Host "Published: $exe ($sizeMb MB)"
Write-Host "SHA-256: $hash"

$compatibilityName = 'SpiritVale_Chinese_Patch_Compatibility_x64'
$compatibilityStage = Join-Path $PSScriptRoot "release\$compatibilityName"
$compatibilityZip = Join-Path $OutputDirectory ($compatibilityName + '.zip')
$compatibilityHashFile = Join-Path $OutputDirectory ($compatibilityName + '.sha256.txt')

$resolvedInstallerRoot = [System.IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') + '\'
$resolvedCompatibilityStage = [System.IO.Path]::GetFullPath($compatibilityStage)
if (-not $resolvedCompatibilityStage.StartsWith($resolvedInstallerRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Compatibility staging path escaped the installer directory: $resolvedCompatibilityStage"
}
if (Test-Path -LiteralPath $compatibilityStage) {
    Remove-Item -LiteralPath $compatibilityStage -Recurse -Force
}
New-Item -ItemType Directory -Path $compatibilityStage -Force | Out-Null

dotnet publish $projectPath `
    -c Release -r win-x64 --self-contained true -o $compatibilityStage `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=false `
    -p:EnableCompressionInSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:PublishTrimmed=false
if (-not (Test-Path -LiteralPath (Join-Path $compatibilityStage 'SpiritVale_Chinese_Patch.exe') -PathType Leaf)) {
    throw 'Compatibility installer publish did not produce its executable.'
}

$compatibilityReadmeBase64 = 'U3Bpcml0VmFsZSDnroDkvZPkuK3mlofooaXkuIEgdntWRVJTSU9OfSAtIFdpbmRvd3MgeDY0IOWFvOWuueeJiAoKMS4g6K+35YWI5a6M5pW06Kej5Y6L5pW05Liq5paH5Lu25aS577yM5LiN6KaB5Y+q5aSN5Yi2IEVYReOAggoyLiDlj4zlh7sgU3Bpcml0VmFsZV9DaGluZXNlX1BhdGNoLmV4ZeOAggozLiDmnKzljIXoh6rluKYgLk5FVCDov5DooYzml7bvvIzkuI3pnIDopoHlj6booYzlronoo4XjgIIKNC4g6YCC55So5LqOIFdpbmRvd3MgMTAvMTEgNjQg5L2N77yMSW50ZWwg5LiOIEFNRCDlpITnkIblmajpgJrnlKjjgIIKNS4g6Iul5LuN5peg5rOV5ZCv5Yqo77yM6K+35Y+R6YCB5Lul5LiL5pel5b+X77yaCiAgICVMT0NBTEFQUERBVEElXGF1cnl4XFNwaXJpdFZhbGVDaGluZXNlUGF0Y2hcTG9nc1xpbnN0YWxsZXItc3RhcnR1cC5sb2cKCuS9nOiAhe+8mmF1cnl4ClFR576k77yaODgyMTMyODA3CuS4quS6uuaxieWMluWtpuS5oOS9nOWTge+8jOS+teWIoOOAgg=='
$compatibilityReadme = [System.Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String($compatibilityReadmeBase64)).Replace('{VERSION}', $patchVersion)
[System.IO.File]::WriteAllText(
    (Join-Path $compatibilityStage 'Compatibility-Readme-zh-CN.txt'),
    $compatibilityReadme,
    [System.Text.UTF8Encoding]::new($true))

$compatibilityManifestPath = Join-Path $compatibilityStage 'PACKAGE_SHA256.txt'
$compatibilityManifestLines = Get-ChildItem -LiteralPath $compatibilityStage -File -Recurse `
    | Where-Object { $_.FullName -ne $compatibilityManifestPath } `
    | Sort-Object FullName `
    | ForEach-Object {
        $relativePath = $_.FullName.Substring($compatibilityStage.TrimEnd('\').Length).TrimStart('\').Replace('\', '/')
        $fileHash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        "$fileHash  $relativePath"
    }
Set-Content -LiteralPath $compatibilityManifestPath -Value $compatibilityManifestLines -Encoding ascii

if (Test-Path -LiteralPath $compatibilityZip -PathType Leaf) {
    $oldCompatibilityHash = (Get-FileHash -LiteralPath $compatibilityZip -Algorithm SHA256).Hash
    New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
    $compatibilityArchiveName = '{0}_{1}.zip' -f `
        $compatibilityName, $oldCompatibilityHash.Substring(0, 12)
    $compatibilityArchive = Join-Path $archiveRoot $compatibilityArchiveName
    if (-not (Test-Path -LiteralPath $compatibilityArchive)) {
        Copy-Item -LiteralPath $compatibilityZip -Destination $compatibilityArchive
    }
    Remove-Item -LiteralPath $compatibilityZip -Force
}

Compress-Archive -LiteralPath $compatibilityStage -DestinationPath $compatibilityZip -CompressionLevel Optimal
$compatibilityHash = (Get-FileHash -LiteralPath $compatibilityZip -Algorithm SHA256).Hash
$compatibilitySizeMb = [math]::Round((Get-Item -LiteralPath $compatibilityZip).Length / 1MB, 2)
Set-Content -LiteralPath $compatibilityHashFile `
    -Value "$compatibilityHash  $([System.IO.Path]::GetFileName($compatibilityZip))" -Encoding ascii

# Keep the fixed paths for control-loop validation, and publish explicit
# versioned copies as the player-facing deliverables.
$versionedExeName = "SpiritVale_Chinese_Patch_v$patchVersion.exe"
$versionedExe = Join-Path $OutputDirectory $versionedExeName
$versionedExeHashFile = Join-Path $OutputDirectory ($versionedExeName + '.sha256.txt')
Copy-Item -LiteralPath $exe -Destination $versionedExe -Force
Set-Content -LiteralPath $versionedExeHashFile -Value "$hash  $versionedExeName" -Encoding ascii

$versionedCompatibilityName = "SpiritVale_Chinese_Patch_v${patchVersion}_Compatibility_x64.zip"
$versionedCompatibilityZip = Join-Path $OutputDirectory $versionedCompatibilityName
$versionedCompatibilityHashFile = Join-Path $OutputDirectory ($versionedCompatibilityName + '.sha256.txt')
Copy-Item -LiteralPath $compatibilityZip -Destination $versionedCompatibilityZip -Force
Set-Content -LiteralPath $versionedCompatibilityHashFile `
    -Value "$compatibilityHash  $versionedCompatibilityName" -Encoding ascii

$releaseManifest = [ordered]@{
    patchVersion = $patchVersion
    targetFramework = 'net8.0-windows'
    runtimeIdentifier = 'win-x64'
    mainInstaller = [ordered]@{
        file = $versionedExeName
        packaging = 'self-contained-single-file-uncompressed'
        size = (Get-Item -LiteralPath $exe).Length
        sha256 = $hash
    }
    compatibilityPackage = [ordered]@{
        file = $versionedCompatibilityName
        packaging = 'self-contained-multi-file-zip'
        size = (Get-Item -LiteralPath $compatibilityZip).Length
        sha256 = $compatibilityHash
    }
}
$releaseManifest | ConvertTo-Json -Depth 4 | Set-Content `
    -LiteralPath (Join-Path $OutputDirectory "release-v$patchVersion.json") -Encoding UTF8

Write-Host "Compatibility package: $compatibilityZip ($compatibilitySizeMb MB)"
Write-Host "Compatibility SHA-256: $compatibilityHash"
