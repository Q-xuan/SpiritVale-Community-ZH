param(
    [string]$GameRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$PluginDll,
    [string]$Translations,
    [string]$EntityCatalog
)

$ErrorActionPreference = 'Stop'
$toolRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
if ([string]::IsNullOrWhiteSpace($PluginDll)) {
    $PluginDll = Join-Path $toolRoot 'SpiritVale.RuntimeLocalization\bin\Release\netstandard2.1\SpiritVale.RuntimeLocalization.dll'
}
if ([string]::IsNullOrWhiteSpace($Translations)) {
    $Translations = Join-Path $GameRoot 'BepInEx\plugins\SpiritVale.RuntimeLocalization\translations.tsv'
}
if ([string]::IsNullOrWhiteSpace($EntityCatalog)) {
    $EntityCatalog = Join-Path $GameRoot 'BepInEx\plugins\SpiritVale.RuntimeLocalization\bilingual-entity-catalog.tsv'
}
if (-not (Test-Path -LiteralPath $PluginDll -PathType Leaf)) {
    throw "Release plugin DLL not found: $PluginDll"
}
if (-not (Test-Path -LiteralPath $Translations -PathType Leaf)) {
    throw "Translation dictionary not found: $Translations"
}
if (-not (Test-Path -LiteralPath $EntityCatalog -PathType Leaf)) {
    throw "Bilingual entity catalog not found: $EntityCatalog"
}

$stage = Join-Path $PSScriptRoot 'payload-stage'
$zip = Join-Path $PSScriptRoot 'Payload.zip'
$zipHash = Join-Path $PSScriptRoot 'Payload.sha256'

if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
if (Test-Path -LiteralPath $zipHash) {
    Remove-Item -LiteralPath $zipHash -Force
}
New-Item -ItemType Directory -Path $stage | Out-Null

foreach ($file in @('.doorstop_version', 'doorstop_config.ini', 'winhttp.dll')) {
    Copy-Item -LiteralPath (Join-Path $GameRoot $file) -Destination (Join-Path $stage $file)
}

Copy-Item -LiteralPath (Join-Path $GameRoot 'dotnet') -Destination $stage -Recurse
New-Item -ItemType Directory -Path (Join-Path $stage 'BepInEx\config') -Force | Out-Null
$offlineConfig = @(
    '[IL2CPP]'
    'UpdateInteropAssemblies = true'
    'UnityBaseLibrariesSource = '
    'GlobalMetadataPath = {GameDataPath}/il2cpp_data/Metadata/global-metadata.dat'
) -join [Environment]::NewLine
[System.IO.File]::WriteAllText(
    (Join-Path $stage 'BepInEx\config\BepInEx.cfg'),
    $offlineConfig + [Environment]::NewLine,
    [System.Text.Encoding]::ASCII)
$localizationDisplayConfig = @(
    '[Display]'
    'EntityNameMode = Chinese'
    'CompactSurfaceMode = EnglishToggle'
    'TemporaryEnglishKey = Tab'
) -join [Environment]::NewLine
[System.IO.File]::WriteAllText(
    (Join-Path $stage 'BepInEx\config\local.spiritvale.runtime-localization.cfg'),
    $localizationDisplayConfig + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
New-Item -ItemType Directory -Path (Join-Path $stage 'BepInEx\core') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $GameRoot 'BepInEx\core') -File |
    Where-Object {
        $_.Extension -in @('.dll', '.config') -and
        -not ($_.Extension -eq '.dll' -and $_.Name -like 'XUnity*.dll')
    } |
    Copy-Item -Destination (Join-Path $stage 'BepInEx\core')
$unityLibs = Join-Path $stage 'BepInEx\unity-libs'
New-Item -ItemType Directory -Path $unityLibs -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $GameRoot 'BepInEx\unity-libs') -File |
    Where-Object { $_.Extension -eq '.dll' } |
    Copy-Item -Destination $unityLibs

$pluginStage = Join-Path $stage 'BepInEx\plugins\SpiritVale.RuntimeLocalization'
New-Item -ItemType Directory -Path $pluginStage -Force | Out-Null
Copy-Item -LiteralPath $PluginDll -Destination (Join-Path $pluginStage 'SpiritVale.RuntimeLocalization.dll')
Copy-Item -LiteralPath $Translations -Destination (Join-Path $pluginStage 'translations.tsv')
Copy-Item -LiteralPath $EntityCatalog -Destination (Join-Path $pluginStage 'bilingual-entity-catalog.tsv')

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entryCount = $archive.Entries.Count
    $entryNames = @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) } | ForEach-Object { $_.FullName.Replace('/', '\') })
    $pluginEntries = @($entryNames | Where-Object { $_ -like 'BepInEx\plugins\SpiritVale.RuntimeLocalization\*' })
    $expectedPluginEntries = @(
        'BepInEx\plugins\SpiritVale.RuntimeLocalization\SpiritVale.RuntimeLocalization.dll',
        'BepInEx\plugins\SpiritVale.RuntimeLocalization\translations.tsv',
        'BepInEx\plugins\SpiritVale.RuntimeLocalization\bilingual-entity-catalog.tsv'
    )
    $unexpectedPluginEntries = @($pluginEntries | Where-Object { $_ -notin $expectedPluginEntries })
    $missingPluginEntries = @($expectedPluginEntries | Where-Object { $_ -notin $pluginEntries })
    $forbiddenEntries = @($entryNames | Where-Object {
        $_ -like 'BepInEx\interop\*' -or
        $_ -like 'BepInEx\cache\*' -or
        $_ -like 'BepInEx\core\XUnity*.dll' -or
        $_ -like 'BepInEx\plugins\XUnity.AutoTranslator\*' -or
        $_ -like 'BepInEx\plugins\XUnity.ResourceRedirector\*' -or
        $_ -match '(?i)(untranslated-runtime|ErrorLog|LogOutput|\.pdb$|\.xml$)'
    })
    if ($unexpectedPluginEntries.Count -or $missingPluginEntries.Count -or $forbiddenEntries.Count) {
        throw "Payload whitelist validation failed. Unexpected=$($unexpectedPluginEntries -join ', ') Missing=$($missingPluginEntries -join ', ') Forbidden=$($forbiddenEntries -join ', ')"
    }
    foreach ($required in @(
        '.doorstop_version',
        'doorstop_config.ini',
        'winhttp.dll',
        'BepInEx\config\BepInEx.cfg',
        'BepInEx\config\local.spiritvale.runtime-localization.cfg',
        'BepInEx\core\BepInEx.Unity.IL2CPP.dll',
        'BepInEx\core\Il2CppInterop.Generator.dll',
        'BepInEx\core\Cpp2IL.Core.dll',
        'BepInEx\core\LibCpp2IL.dll'
    )) {
        if ($required -notin $entryNames) { throw "Payload IL2CPP/offline probe file missing: $required" }
    }
} finally {
    $archive.Dispose()
}
$sizeMb = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 2)
$translationCount = @(Get-Content -LiteralPath $Translations -Encoding UTF8 | Where-Object { $_ -and -not $_.StartsWith('#') }).Count
$pluginHash = (Get-FileHash -LiteralPath $PluginDll -Algorithm SHA256).Hash
$catalogHash = (Get-FileHash -LiteralPath $EntityCatalog -Algorithm SHA256).Hash
$payloadHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Set-Content -LiteralPath $zipHash -Value $payloadHash -Encoding ascii
Write-Host "Payload ready: $entryCount entries, $sizeMb MB, $translationCount translations"
Write-Host "Plugin DLL SHA-256: $pluginHash"
Write-Host "Bilingual entity catalog SHA-256: $catalogHash"
Write-Host "Payload SHA-256: $payloadHash"
