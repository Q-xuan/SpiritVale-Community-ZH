param(
    [string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'
$expectedAppId = '3767850'
$expectedBuildId = '24266225'
$expectedExeHash = 'E2C8E55FB082CAAC56C6145C3BB52FAA7CF611E683EA26F4FB011754DD714AA6'
$expectedGameHash = 'D4442C72CC52C02A749CEFBCDFFC5502639E773C7C38783647E544BAC6A51E06'
$expectedMetadataHash = '066AB69DE6FF1CF73FFFCE2B370B77276ACA0899CE6DE62258324E4107DD9A35'
$expectedPluginHash = '35B4400FDAFBEC423BDE9D02CF8FCA7BB21E6DD629A6BBD56ACE2C6BF26868D2'
$expectedDictionaryHash = '41ED91111833FD24E148530F2F42CE3F5A51A2D353160AC1218BBD9545BD35DF'
$expectedLegacyHash = 'AA168EF4AFBE53D72B412AFD402A1F6CC676095A66AC87E5E324544EC9360165'
$localizedStem = 'SpiritVale' + (-join ([char[]]@(0x6C49, 0x5316, 0x8865, 0x4E01)))
$compatibilityLabel = -join ([char[]]@(0x65B0, 0x7248, 0x517C, 0x5BB9, 0x7248))
$outputName = $localizedStem + '-v1.2.21-' + $compatibilityLabel + '.exe'
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$stageRoot = Join-Path $PSScriptRoot 'staging\compat-v1.2.21-build-24266225'
$plugin = Join-Path $stageRoot 'payload\BepInEx\plugins\SpiritVale.RuntimeLocalization\SpiritVale.RuntimeLocalization.dll'
$dictionary = Join-Path $stageRoot 'payload\BepInEx\plugins\SpiritVale.RuntimeLocalization\translations.tsv'
$manifest = Join-Path $stageRoot 'compatibility-manifest.json'
$publishRoot = Join-Path $stageRoot 'publish'
$distRoot = Join-Path $PSScriptRoot 'dist'
$output = Join-Path $distRoot $outputName

function Assert-Hash([string]$Path, [string]$Expected, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Label not found: $Path" }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actual -ne $Expected) { throw "$Label SHA-256 mismatch. Expected $Expected, actual $actual" }
}

$processes = @(Get-Process -Name 'SpiritVale' -ErrorAction SilentlyContinue)
foreach ($process in $processes) {
    try { $processPath = [System.IO.Path]::GetFullPath($process.Path) }
    catch { throw "Cannot read SpiritVale process $($process.Id) path; compatibility packaging stopped conservatively." }
    $targetPath = [System.IO.Path]::GetFullPath((Join-Path $GameRoot 'SpiritVale.exe'))
    if ([string]::Equals($processPath, $targetPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Exit the selected SpiritVale game before compatibility packaging.'
    }
}

$steamApps = Split-Path -Parent (Split-Path -Parent $GameRoot)
$appManifest = Join-Path $steamApps "appmanifest_$expectedAppId.acf"
if (-not (Test-Path -LiteralPath $appManifest -PathType Leaf)) { throw "Steam app manifest not found: $appManifest" }
$appText = Get-Content -LiteralPath $appManifest -Raw -Encoding UTF8
$appId = [regex]::Match($appText, '"appid"\s+"(?<value>[^"]+)"').Groups['value'].Value
$buildId = [regex]::Match($appText, '"buildid"\s+"(?<value>[^"]+)"').Groups['value'].Value
if ($appId -ne $expectedAppId -or $buildId -ne $expectedBuildId) {
    throw "Steam baseline mismatch. App=$appId Build=$buildId"
}

Assert-Hash (Join-Path $GameRoot 'SpiritVale.exe') $expectedExeHash 'SpiritVale.exe'
Assert-Hash (Join-Path $GameRoot 'GameAssembly.dll') $expectedGameHash 'GameAssembly.dll'
Assert-Hash (Join-Path $GameRoot 'SpiritVale_Data\il2cpp_data\Metadata\global-metadata.dat') $expectedMetadataHash 'global-metadata.dat'
Assert-Hash $plugin $expectedPluginHash 'Frozen plugin'
Assert-Hash $dictionary $expectedDictionaryHash 'Frozen dictionary'
if (@(Get-Content -LiteralPath $dictionary -Encoding UTF8 | Where-Object { $_ -and -not $_.StartsWith('#') }).Count -ne 3949) {
    throw 'Frozen dictionary entry count is not 3949.'
}
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw "Compatibility manifest not found: $manifest" }

$legacy = Join-Path $distRoot ($localizedStem + '.exe')
Assert-Hash $legacy $expectedLegacyHash 'Legacy v1.2.20 installer'
$legacyArchiveRoot = Join-Path $PSScriptRoot 'archive\legacy-v1.2.20'
New-Item -ItemType Directory -Path $legacyArchiveRoot -Force | Out-Null
$legacyArchive = Join-Path $legacyArchiveRoot ($localizedStem + '-v1.2.20-legacy.exe')
if (Test-Path -LiteralPath $legacyArchive) {
    Assert-Hash $legacyArchive $expectedLegacyHash 'Archived legacy v1.2.20 installer'
} else {
    Copy-Item -LiteralPath $legacy -Destination $legacyArchive
}

& (Join-Path $PSScriptRoot 'Build-Payload.ps1') -GameRoot $GameRoot -PluginDll $plugin -Translations $dictionary
if ($LASTEXITCODE) { exit $LASTEXITCODE }

if (Test-Path -LiteralPath $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
dotnet publish (Join-Path $PSScriptRoot 'SpiritVale.ChinesePatch.Installer.csproj') `
    -c Release -r win-x64 --self-contained true -o $publishRoot
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$published = Join-Path $publishRoot 'SpiritVale_Chinese_Patch.exe'
if (-not (Test-Path -LiteralPath $published -PathType Leaf)) { throw "Published EXE not found: $published" }
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
if (Test-Path -LiteralPath $output -PathType Leaf) {
    $oldHash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
    $compatArchiveRoot = Join-Path $PSScriptRoot 'archive\compat-v1.2.21'
    New-Item -ItemType Directory -Path $compatArchiveRoot -Force | Out-Null
    $oldArchive = Join-Path $compatArchiveRoot ($localizedStem + "-v1.2.21-$compatibilityLabel-$($oldHash.Substring(0, 12)).exe")
    if (-not (Test-Path -LiteralPath $oldArchive)) { Copy-Item -LiteralPath $output -Destination $oldArchive }
}
Copy-Item -LiteralPath $published -Destination $output -Force

$exeHash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
$payloadPath = Join-Path $PSScriptRoot 'Payload.zip'
$payloadHash = (Get-FileHash -LiteralPath $payloadPath -Algorithm SHA256).Hash
$result = [ordered]@{
    releaseChannel = 'compatibility'
    patchVersion = '1.2.21'
    steamAppId = $expectedAppId
    steamBuildId = $expectedBuildId
    gameAssemblySha256 = $expectedGameHash
    pluginSha256 = $expectedPluginHash
    dictionarySha256 = $expectedDictionaryHash
    dictionaryEntries = 3949
    payloadSha256 = $payloadHash
    output = $output
    outputSize = (Get-Item -LiteralPath $output).Length
    outputSha256 = $exeHash
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stageRoot 'built-artifact.json') -Encoding UTF8
Set-Content -LiteralPath (Join-Path $distRoot ($outputName + '.sha256.txt')) -Value $exeHash -Encoding ascii
$result | Format-List
