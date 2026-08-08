[CmdletBinding()]
param(
    [ValidateSet('Status', 'Queue', 'Audit', 'Validate', 'Build', 'RecordLive', 'Package', 'All')]
    [string]$Stage = 'Status',
    [string]$GameRoot,
    [string]$ToolRoot,
    [string]$PythonExecutable,
    [switch]$Deploy,
    [switch]$ApproveGameHash,
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$PatchVersion,
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$InstallerVersion,
    [ValidateRange(0, 10)]
    [int]$ColdStarts = 0,
    [string[]]$VerifiedSurface = @(),
    [string[]]$Evidence = @(),
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
if ($PatchVersion -and $InstallerVersion) {
    throw 'Use either -PatchVersion for a content release or -InstallerVersion for an installer-only release, not both.'
}

$controlLoopBytes = [System.IO.File]::ReadAllBytes($MyInvocation.MyCommand.Path)
$controlLoopHasUtf8Bom = $controlLoopBytes.Length -ge 3 -and
    $controlLoopBytes[0] -eq 0xEF -and $controlLoopBytes[1] -eq 0xBB -and $controlLoopBytes[2] -eq 0xBF
if (-not $controlLoopHasUtf8Bom -and
    [System.Text.Encoding]::UTF8.GetString($controlLoopBytes) -match '[^\u0000-\u007F]') {
    throw 'The PowerShell 5 control loop must be ASCII-only unless the file has a UTF-8 BOM.'
}

function Test-GameRoot([string]$Path) {
    $appIdPath = if ($Path) { Join-Path $Path 'steam_appid.txt' } else { $null }
    $appIdValid = -not $appIdPath -or
        -not (Test-Path -LiteralPath $appIdPath -PathType Leaf) -or
        ([System.IO.File]::ReadAllText($appIdPath).Trim() -eq '3767850')
    return $Path -and
        (Test-Path -LiteralPath (Join-Path $Path 'SpiritVale.exe') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $Path 'GameAssembly.dll') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $Path 'SpiritVale_Data\il2cpp_data\Metadata\global-metadata.dat') -PathType Leaf) -and
        $appIdValid
}

function Resolve-SpiritValeRoot([string]$Requested) {
    $candidates = [System.Collections.Generic.List[string]]::new()
    if ($Requested) { $candidates.Add($Requested) }
    if ($env:SPIRITVALE_ROOT) { $candidates.Add($env:SPIRITVALE_ROOT) }
    $candidates.Add((Get-Location).Path)

    try {
        $steamPath = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath
        if ($steamPath) { $candidates.Add((Join-Path $steamPath 'steamapps\common\SpiritVale')) }
    } catch { }

    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
    if ($programFilesX86) { $candidates.Add((Join-Path $programFilesX86 'Steam\steamapps\common\SpiritVale')) }
    foreach ($drive in [System.IO.DriveInfo]::GetDrives() | Where-Object { $_.DriveType -eq 'Fixed' -and $_.IsReady }) {
        $candidates.Add((Join-Path $drive.RootDirectory.FullName 'SteamLibrary\steamapps\common\SpiritVale'))
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-GameRoot $candidate) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    throw 'SpiritVale was not found. Pass -GameRoot with the directory containing SpiritVale.exe.'
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-TranslationCount([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return 0 }
    return @([System.IO.File]::ReadAllLines($Path, [System.Text.Encoding]::UTF8) |
        Where-Object { $_ -and -not $_.StartsWith('#') }).Count
}

function Get-TranslationSourceKeys([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Translation vocabulary baseline is missing: $Path"
    }
    $keys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadAllLines($Path, [System.Text.Encoding]::UTF8)) {
        $lineNumber++
        if (-not $line -or $line.StartsWith('#')) { continue }
        $parts = $line.Split("`t")
        if ($parts.Count -ne 2 -or -not $parts[0] -or -not $parts[1]) {
            throw "Unsafe baseline TSV row at ${Path}:$lineNumber"
        }
        if (-not $keys.Add($parts[0])) {
            throw "Duplicate baseline source '$($parts[0])' at ${Path}:$lineNumber"
        }
    }
    return @($keys)
}

function Get-MaxWriteTime([string[]]$Paths) {
    $times = @($Paths | Where-Object { Test-Path -LiteralPath $_ } |
        ForEach-Object { (Get-Item -LiteralPath $_).LastWriteTimeUtc })
    if ($times.Count -eq 0) { return [DateTime]::MinValue }
    return ($times | Sort-Object -Descending | Select-Object -First 1)
}

function Get-ProjectFiles([string]$Root, [string[]]$Patterns) {
    $files = @()
    foreach ($pattern in $Patterns) {
        $files += @(Get-ChildItem -LiteralPath $Root -File -Filter $pattern -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName)
    }
    return $files
}

function Get-SourceConstant([string]$Path, [string]$Pattern) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $match = [regex]::Match([System.IO.File]::ReadAllText($Path), $Pattern)
    if (-not $match.Success) { return $null }
    return $match.Groups[1].Value
}

function Get-CaseSensitiveJsonMap([string]$Path) {
    Add-Type -AssemblyName System.Web.Extensions
    $serializer = [System.Web.Script.Serialization.JavaScriptSerializer]::new()
    return $serializer.DeserializeObject([System.IO.File]::ReadAllText($Path))
}

function Set-SourceRegex([string]$Path, [string]$Pattern, [string]$Replacement, [string]$Label) {
    $text = [System.IO.File]::ReadAllText($Path)
    $matches = [regex]::Matches($text, $Pattern)
    if ($matches.Count -ne 1) { throw "$Label replacement expected one match in $Path; found $($matches.Count)." }
    $updated = [regex]::Replace($text, $Pattern, $Replacement)
    [System.IO.File]::WriteAllText($Path, $updated, [System.Text.UTF8Encoding]::new($false))
}

function Invoke-Native([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$FilePath failed with exit code $LASTEXITCODE." }
}

function Invoke-Python([string[]]$Arguments) {
    if ($PythonExecutable) {
        if (-not (Test-Path -LiteralPath $PythonExecutable -PathType Leaf)) { throw "Python executable is missing: $PythonExecutable" }
        Invoke-Native (Resolve-Path -LiteralPath $PythonExecutable).Path $Arguments
        return
    }
    if ($env:SPIRITVALE_PYTHON) {
        if (-not (Test-Path -LiteralPath $env:SPIRITVALE_PYTHON -PathType Leaf)) { throw "SPIRITVALE_PYTHON is invalid: $env:SPIRITVALE_PYTHON" }
        Invoke-Native (Resolve-Path -LiteralPath $env:SPIRITVALE_PYTHON).Path $Arguments
        return
    }
    $codexPython = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    if (Test-Path -LiteralPath $codexPython -PathType Leaf) { Invoke-Native $codexPython $Arguments; return }
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($python -and $python.Source -notmatch '(?i)\\WindowsApps\\python(3)?\.exe$') {
        Invoke-Native $python.Source $Arguments
        return
    }
    $launcher = Get-Command py -ErrorAction SilentlyContinue
    if ($launcher) { Invoke-Native $launcher.Source (@('-3') + $Arguments); return }
    throw 'Python 3 was not found. Pass -PythonExecutable or set SPIRITVALE_PYTHON.'
}

$GameRoot = Resolve-SpiritValeRoot $GameRoot
if (-not $ToolRoot) { $ToolRoot = Join-Path $GameRoot '.codex-localization-tools' }
$ToolRoot = (Resolve-Path -LiteralPath $ToolRoot).Path
$RepositoryRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $ToolRoot)).Path

function Get-GameConfigBundle {
    $bundleRoot = Join-Path $GameRoot 'SpiritVale_Data\StreamingAssets\aa\StandaloneWindows64'
    $bundles = @(Get-ChildItem -LiteralPath $bundleRoot -File -Filter 'client_assets_gameclientconfig_*.bundle' -ErrorAction SilentlyContinue)
    if ($bundles.Count -ne 1) { throw "Expected one SpiritVale game config bundle; found $($bundles.Count) in $bundleRoot" }
    return $bundles[0].FullName
}

function Get-SharedAssetsFile {
    $dataRoot = Join-Path $GameRoot 'SpiritVale_Data'
    $assets = @(Get-ChildItem -LiteralPath $dataRoot -Recurse -File -Filter 'sharedassets0.assets' -ErrorAction SilentlyContinue)
    if ($assets.Count -ne 1) { throw "Expected one sharedassets0.assets file; found $($assets.Count) in $dataRoot" }
    return $assets[0].FullName
}

$GameConfigBundle = Get-GameConfigBundle
$SharedAssetsFile = Get-SharedAssetsFile

$Paths = @{
    GameExecutable = Join-Path $GameRoot 'SpiritVale.exe'
    GameAssembly = Join-Path $GameRoot 'GameAssembly.dll'
    Metadata = Join-Path $GameRoot 'SpiritVale_Data\il2cpp_data\Metadata\global-metadata.dat'
    InteropAssembly = Join-Path $GameRoot 'BepInEx\interop\Assembly-CSharp.dll'
    InteropHash = Join-Path $GameRoot 'BepInEx\interop\assembly-hash.txt'
    PluginProject = Join-Path $ToolRoot 'SpiritVale.RuntimeLocalization\SpiritVale.RuntimeLocalization.csproj'
    PluginSource = Join-Path $ToolRoot 'SpiritVale.RuntimeLocalization'
    PluginBuild = Join-Path $ToolRoot 'SpiritVale.RuntimeLocalization\bin\Release\netstandard2.1\SpiritVale.RuntimeLocalization.dll'
    TestsProject = Join-Path $ToolRoot 'SpiritVale.RuntimeLocalization.Tests\SpiritVale.RuntimeLocalization.Tests.csproj'
    BilingualTestsProject = Join-Path $ToolRoot 'SpiritVale.BilingualDisplay.Tests\SpiritVale.BilingualDisplay.Tests.csproj'
    Generator = Join-Path $ToolRoot 'generate_runtime_dictionary.py'
    BilingualCatalogGenerator = Join-Path $PSScriptRoot 'Generate-SpiritValeBilingualCatalog.py'
    BilingualMapManifest = Join-Path $ToolRoot 'bilingual-map-entities.json'
    RuntimeNameModule = Join-Path $ToolRoot 'runtime_name_aliases.py'
    SourceAuditScript = Join-Path $PSScriptRoot 'Audit-SpiritValeSources.py'
    SkillAliasAuditScript = Join-Path $PSScriptRoot 'Audit-SpiritValeSkillAliases.py'
    RuntimeNameAuditScript = Join-Path $PSScriptRoot 'Audit-SpiritValeRuntimeNames.py'
    SourceBundle = $GameConfigBundle
    SharedAssets = $SharedAssetsFile
    SourceRaw = Join-Path $ToolRoot 'artifacts\current-addressables-game-config.raw'
    SourceBaseline = Join-Path $ToolRoot 'backups\addressables-game-config.raw'
    SourceReport = Join-Path $ToolRoot 'artifacts\source-coverage.tsv'
    SourceSummary = Join-Path $ToolRoot 'artifacts\source-audit.json'
    SourceSnapshot = Join-Path $ToolRoot 'artifacts\source-snapshot.json'
    SkillAliasReport = Join-Path $ToolRoot 'artifacts\runtime-skill-aliases.tsv'
    SkillAliasSummary = Join-Path $ToolRoot 'artifacts\runtime-skill-aliases.json'
    RuntimeNameReport = Join-Path $ToolRoot 'artifacts\runtime-name-aliases.tsv'
    RuntimeNameSummary = Join-Path $ToolRoot 'artifacts\runtime-name-aliases.json'
    BilingualCatalog = Join-Path $ToolRoot 'artifacts\bilingual-entity-catalog.tsv'
    BilingualCatalogAudit = Join-Path $ToolRoot 'artifacts\bilingual-entity-catalog.audit.json'
    MixedDescriptionReport = Join-Path $ToolRoot 'artifacts\mixed-description-residuals.tsv'
    LocalizedCorpusReport = Join-Path $ToolRoot 'artifacts\localized-corpus.tsv'
    Artifacts = Join-Path $ToolRoot 'artifacts'
    ArtifactDictionary = Join-Path $ToolRoot 'artifacts\translations.tsv'
    ConflictReport = Join-Path $ToolRoot 'artifacts\runtime-dictionary-conflicts.tsv'
    QualityOverrides = Join-Path $ToolRoot 'mmo-quality-overrides.json'
    DeployedPlugin = Join-Path $GameRoot 'BepInEx\plugins\SpiritVale.RuntimeLocalization\SpiritVale.RuntimeLocalization.dll'
    DeployedDictionary = Join-Path $GameRoot 'BepInEx\plugins\SpiritVale.RuntimeLocalization\translations.tsv'
    DeployedBilingualCatalog = Join-Path $GameRoot 'BepInEx\plugins\SpiritVale.RuntimeLocalization\bilingual-entity-catalog.tsv'
    UntranslatedLog = Join-Path $GameRoot 'BepInEx\plugins\SpiritVale.RuntimeLocalization\untranslated-runtime.log'
    InstallerProject = Join-Path $ToolRoot 'installer\SpiritVale.ChinesePatch.Installer.csproj'
    InstallerService = Join-Path $ToolRoot 'installer\PatchService.cs'
    InstallerCompatibilityPolicy = Join-Path $ToolRoot 'installer\compatibility-policy.json'
    Publish = Join-Path $ToolRoot 'installer\Publish.ps1'
    InstallerExe = Join-Path $ToolRoot 'installer\dist\SpiritVale_Chinese_Patch.exe'
    InstallerCompatibilityZip = Join-Path $ToolRoot 'installer\dist\SpiritVale_Chinese_Patch_Compatibility_x64.zip'
    LiveVerification = Join-Path $ToolRoot 'artifacts\live-verification.json'
    RepositoryVersion = Join-Path $RepositoryRoot 'VERSION'
    Changelog = Join-Path $RepositoryRoot 'CHANGELOG.md'
    ReleaseMetadataValidator = Join-Path $PSScriptRoot 'Test-SpiritValeReleaseMetadata.ps1'
    Log = Join-Path $GameRoot 'BepInEx\LogOutput.log'
}

function Get-ReleaseMetadata {
    param(
        [switch]$RequireLiveVerification,
        [string]$MetadataTag,
        [string]$ArtifactsDirectory
    )
    $parameters = @{ RepositoryRoot = $RepositoryRoot; AsJson = $true }
    if ($RequireLiveVerification) { $parameters['RequireLiveVerification'] = $true }
    if ($MetadataTag) { $parameters['Tag'] = $MetadataTag }
    if ($ArtifactsDirectory) { $parameters['ArtifactsDirectory'] = $ArtifactsDirectory }
    $json = & $Paths.ReleaseMetadataValidator @parameters
    return ($json | ConvertFrom-Json)
}

function Assert-ReleaseMetadata {
    $null = Get-ReleaseMetadata
}

function Get-TargetGameProcess {
    $expectedPath = [System.IO.Path]::GetFullPath($Paths.GameExecutable)
    return @(Get-Process -Name 'SpiritVale' -ErrorAction SilentlyContinue | Where-Object {
        $process = $_
        try {
            [string]::Equals(
                [System.IO.Path]::GetFullPath($process.Path),
                $expectedPath,
                [System.StringComparison]::OrdinalIgnoreCase)
        } catch {
            throw "Could not safely resolve the path for SpiritVale process Id $($process.Id)."
        }
    })
}

$InteropReferences = @(
    'Il2Cppmscorlib.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.UI.dll',
    'UnityEngine.UIElementsModule.dll',
    'UnityEngine.TextRenderingModule.dll',
    'Unity.TextMeshPro.dll'
) | ForEach-Object { Join-Path $GameRoot "BepInEx\interop\$_" }

foreach ($required in @($Paths.GameAssembly, $Paths.Metadata, $Paths.PluginProject, $Paths.TestsProject, $Paths.BilingualTestsProject, $Paths.Generator, $Paths.BilingualCatalogGenerator, $Paths.BilingualMapManifest, $Paths.RuntimeNameModule, $Paths.SourceAuditScript, $Paths.SkillAliasAuditScript, $Paths.RuntimeNameAuditScript, $Paths.InstallerService, $Paths.InstallerCompatibilityPolicy, $Paths.QualityOverrides, $Paths.RepositoryVersion, $Paths.Changelog, $Paths.ReleaseMetadataValidator)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required file is missing: $required" }
}

function Get-SteamBuildId {
    $steamApps = Split-Path -Parent (Split-Path -Parent $GameRoot)
    $manifest = Join-Path $steamApps 'appmanifest_3767850.acf'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { return $null }
    $manifestText = [System.IO.File]::ReadAllText($manifest)
    if ($manifestText -notmatch '"appid"\s+"3767850"' -or $manifestText -notmatch '"installdir"\s+"SpiritVale"') { return $null }
    $match = [regex]::Match($manifestText, '"buildid"\s+"(\d+)"')
    if ($match.Success) { return $match.Groups[1].Value }
    return $null
}

if (-not (Get-SteamBuildId)) {
    throw 'The matching Steam appmanifest_3767850.acf was not found for this SpiritVale directory.'
}

function Get-ActiveXUnityFiles {
    $pluginRoot = Join-Path $GameRoot 'BepInEx\plugins'
    if (-not (Test-Path -LiteralPath $pluginRoot)) { return @() }
    return @(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Filter '*.dll' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '(?i)XUnity\.(AutoTranslator|ResourceRedirector)' } |
        Select-Object -ExpandProperty FullName)
}

function Get-LatestRuntimeResidualSummary {
    $empty = [PSCustomObject]@{ Session = $null; CastCount = 0; DescriptionCount = 0; MapCount = 0; ItemNameCount = 0; UiCount = 0 }
    if (-not (Test-Path -LiteralPath $Paths.UntranslatedLog -PathType Leaf)) { return $empty }

    $lines = [System.IO.File]::ReadAllLines($Paths.UntranslatedLog, [System.Text.Encoding]::UTF8)
    $sessionIndex = -1
    for ($index = 0; $index -lt $lines.Length; $index++) {
        if ($lines[$index].StartsWith('# Session ', [System.StringComparison]::Ordinal)) { $sessionIndex = $index }
    }
    if ($sessionIndex -lt 0) { $sessionIndex = 0 }
    $session = if ($lines.Length -gt 0) { $lines[$sessionIndex] } else { $null }
    $castRows = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $descriptionRows = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $mapRows = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $itemNameRows = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $uiRows = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $joinDiscord = ([char]0x52A0).ToString() + ([char]0x5165).ToString() + ' Discord'
    for ($index = $sessionIndex + 1; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        $tab = $line.IndexOf("`t", [System.StringComparison]::Ordinal)
        if ($tab -lt 1) { continue }
        $context = $line.Substring(0, $tab)
        $visible = $line.Substring($tab + 1)
        $isProtected = $context -match '(?i)(Chat|Message|PlayerName|CharacterName|Display ?Name|Text_Name|Shop ?Name|Vending|Guild)'
        if (-not $isProtected -and $context.StartsWith('TMP', [System.StringComparison]::Ordinal) -and
            $visible -match '(?i)[A-Za-z]{3,}' -and $visible -match '(?:!|\uFF01)\s*$') {
            $null = $castRows.Add($line)
        }
        if (-not $isProtected -and
            ($visible -match '(?i)[A-Za-z]{3,}' -or
             $visible -match '(?i)(?<![A-Za-z])(Lv\.?\d+|\d+h\s*\d+m|\d+(?:\.\d+)?s)(?![A-Za-z])') -and
            ($context -match '(?i)(Description|Tooltip|Label-Body|Type)' -or
             $visible -match '(?i)(?<![A-Za-z])(seconds?|mana|Lv\.?\d+|\d+h\s*\d+m|\d+(?:\.\d+)?s)(?![A-Za-z])')) {
            $null = $descriptionRows.Add($line)
        }
        if (-not $isProtected -and
            ($visible -match '(?i)\\nLv\.?\d+-\d+$' -or
             ($context -match '(?i)Location' -and $visible -match '(?i)[A-Za-z]{3,}') -or
             ($context -match '(?i)(^|:)Name$' -and
              $visible -match '(?i)^(?:Sunny Meadows|Forest Field|Mystic Lake|Festering Woods|Forgotten Depths|Goblin Cave|Sanctum of Light) \d+$'))) {
            $null = $mapRows.Add($line)
        }
        if (-not $isProtected -and $context -match '(?i)(^|:)Name$' -and
            $visible -match "(?i)(?: Rune| Relic| Scroll| Jewel| Card| Boots| Chest| Coat| Gloves| Greaves| Hat| Helm| Hood| Hook| Legs| Pants| Shoes| Shield| Sword| Axe| Bow| Staff| Mace| Spear| Knife| Pistol| Rifle| Scythe| Beads| Flask| Coal Hard)$|^\+\d+\s+") {
            $null = $itemNameRows.Add($line)
        }
        $isStableUiContext =
            $context -match '(?i)(Placeholder|Error|Label-(?:Title|Bubble|Amount|Medium)|Title_LineDivider|Button_|Craftsman|(^|:)Level$|Stance|ToastPopup|Weight|ResetTimer|Member Count)' -or
            ($context -match '(?i)(^|:)Title$' -and $visible -match '^\[[A-Za-z][^\]]+\]$') -or
            ($context -match '(?i)(^|:)Popup$' -and $visible -match '(?i)^(?:Are you sure you want to|Free as part of a promotion)') -or
            ($context -match '(?i)(^|:)Name$' -and $visible -match '^(?:Interact|Waypoint)$')
        $isAllowedUiText = $visible -ceq 'PvP' -or $visible -ceq $joinDiscord
        if (-not $isProtected -and $isStableUiContext -and
            $visible -match '(?i)[A-Za-z]{3,}' -and -not $isAllowedUiText) {
            $null = $uiRows.Add($line)
        }
    }
    return [PSCustomObject]@{
        Session = $session
        CastCount = $castRows.Count
        DescriptionCount = $descriptionRows.Count
        MapCount = $mapRows.Count
        ItemNameCount = $itemNameRows.Count
        UiCount = $uiRows.Count
    }
}

function Get-LoopStatus {
    $gameTime = Get-MaxWriteTime @($Paths.GameAssembly, $Paths.Metadata)
    $interopFiles = @($InteropReferences + $Paths.InteropAssembly + $Paths.InteropHash)
    $existingInteropFiles = @($interopFiles | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    $interopFresh = $existingInteropFiles.Count -eq $interopFiles.Count
    if ($interopFresh) {
        $interopFresh = @($existingInteropFiles | Where-Object { (Get-Item -LiteralPath $_).LastWriteTimeUtc -lt $gameTime }).Count -eq 0
    }
    $logErrors = 0
    if (Test-Path -LiteralPath $Paths.Log) {
        $logErrors = @(Select-String -LiteralPath $Paths.Log -Pattern '\[(Fatal|Error)\s*:' -ErrorAction SilentlyContinue).Count
    }
    $interopHash = $null
    if (Test-Path -LiteralPath $Paths.InteropHash) { $interopHash = ([System.IO.File]::ReadAllText($Paths.InteropHash)).Trim() }
    $interopAutoUpdate = $null
    $bepInExConfig = Join-Path $GameRoot 'BepInEx\config\BepInEx.cfg'
    if (Test-Path -LiteralPath $bepInExConfig) {
        $match = [regex]::Match([System.IO.File]::ReadAllText($bepInExConfig), '(?im)^\s*UpdateInteropAssemblies\s*=\s*(true|false)\s*$')
        if ($match.Success) { $interopAutoUpdate = [bool]::Parse($match.Groups[1].Value) }
    }
    $pluginCode = Join-Path $Paths.PluginSource 'RuntimeLocalizationPlugin.cs'
    $runtimeResiduals = Get-LatestRuntimeResidualSummary
    $sourceBundleHash = Get-Sha256 $Paths.SourceBundle
    $installerPolicy = Get-CaseSensitiveJsonMap $Paths.InstallerCompatibilityPolicy
    $verifiedBuilds = @($installerPolicy['verifiedBuilds'])
    $currentGameHash = Get-Sha256 $Paths.GameAssembly
    $currentMetadataHash = Get-Sha256 $Paths.Metadata
    $currentBuildId = Get-SteamBuildId
    $installerGameHashApproved = @($verifiedBuilds | Where-Object {
        [string]$_['steamBuildId'] -eq [string]$currentBuildId -and
        [string]$_['gameAssemblySha256'] -eq [string]$currentGameHash -and
        ([string]::IsNullOrWhiteSpace([string]$_['metadataSha256']) -or
         [string]$_['metadataSha256'] -eq [string]$currentMetadataHash)
    }).Count -gt 0
    $sourceAuditHash = $null
    $uncoveredSources = $null
    $monsterCoverage = $null
    if (Test-Path -LiteralPath $Paths.SourceSummary) {
        $sourceAudit = Get-CaseSensitiveJsonMap $Paths.SourceSummary
        $sourceAuditHash = [string]$sourceAudit['bundle_sha256']
        $uncoveredSources = [int]$sourceAudit['uncovered_sources']
        $categories = $sourceAudit['category_coverage']
        if ($categories -and $categories.ContainsKey('Monsters')) {
            $monsterValues = $categories['Monsters']
            $monsterCoverage = "$($monsterValues['covered'])/$($monsterValues['total'])"
        }
    }
    $activeDictionary = $Paths.ArtifactDictionary
    if (-not (Test-Path -LiteralPath $activeDictionary -PathType Leaf)) { $activeDictionary = $Paths.DeployedDictionary }
    $sharedAssetsHash = Get-Sha256 $Paths.SharedAssets
    $sourceSnapshotHash = Get-Sha256 $Paths.SourceSnapshot
    $skillAliasAuditFresh = $false
    $skillAliasDictionaryFresh = $false
    $skillAliasCoverage = $null
    $skillAliasCoverageComplete = $false
    if (Test-Path -LiteralPath $Paths.SkillAliasSummary -PathType Leaf) {
        $skillAliasAudit = Get-CaseSensitiveJsonMap $Paths.SkillAliasSummary
        $skillAliasAuditFresh =
            $sharedAssetsHash -eq [string]$skillAliasAudit['sharedassets_sha256'] -and
            $sourceSnapshotHash -eq [string]$skillAliasAudit['source_snapshot_sha256']
        $skillAliasDictionaryFresh = (Get-Sha256 $activeDictionary) -eq [string]$skillAliasAudit['dictionary_sha256']
        $skillAliasCoverage = "$($skillAliasAudit['covered_display_ids'])/$($skillAliasAudit['expected_skill_ids'])"
        $skillAliasCoverageComplete = [bool]$skillAliasAudit['coverage_complete']
    }
    $runtimeNameAuditFresh = $false
    $runtimeNameDictionaryFresh = $false
    $runtimeNameCoverage = $null
    $runtimeNameCoverageComplete = $false
    if (Test-Path -LiteralPath $Paths.RuntimeNameSummary -PathType Leaf) {
        $runtimeNameAudit = Get-CaseSensitiveJsonMap $Paths.RuntimeNameSummary
        $runtimeNameAuditFresh =
            $sharedAssetsHash -eq [string]$runtimeNameAudit['sharedassets_sha256'] -and
            $sourceSnapshotHash -eq [string]$runtimeNameAudit['source_snapshot_sha256']
        $runtimeNameDictionaryFresh = (Get-Sha256 $activeDictionary) -eq [string]$runtimeNameAudit['dictionary_sha256']
        $runtimeNameCoverage = "$($runtimeNameAudit['covered_display_strings'])/$($runtimeNameAudit['runtime_display_strings'])"
        $runtimeNameCoverageComplete = [bool]$runtimeNameAudit['coverage_complete']
    }
    $bilingualCatalogFresh = $false
    $bilingualCatalogCoverageComplete = $false
    $bilingualCatalogRows = $null
    if ((Test-Path -LiteralPath $Paths.BilingualCatalog -PathType Leaf) -and
        (Test-Path -LiteralPath $Paths.BilingualCatalogAudit -PathType Leaf)) {
        $bilingualAudit = Get-CaseSensitiveJsonMap $Paths.BilingualCatalogAudit
        $bilingualCatalogFresh =
            $sourceSnapshotHash -eq [string]$bilingualAudit['source_snapshot_sha256'] -and
            (Get-Sha256 $Paths.RuntimeNameReport) -eq [string]$bilingualAudit['runtime_names_sha256'] -and
            (Get-Sha256 $Paths.RuntimeNameSummary) -eq [string]$bilingualAudit['runtime_names_summary_sha256'] -and
            (Get-Sha256 $Paths.SkillAliasReport) -eq [string]$bilingualAudit['skill_aliases_sha256'] -and
            (Get-Sha256 $Paths.SkillAliasSummary) -eq [string]$bilingualAudit['skill_aliases_summary_sha256'] -and
            (Get-Sha256 $activeDictionary) -eq [string]$bilingualAudit['dictionary_sha256'] -and
            (Get-Sha256 $Paths.BilingualMapManifest) -eq [string]$bilingualAudit['map_manifest_sha256'] -and
            (Get-Sha256 $Paths.BilingualCatalog) -eq [string]$bilingualAudit['catalog_sha256']
        $bilingualCatalogCoverageComplete = [bool]$bilingualAudit['coverage_complete']
        $bilingualCatalogRows = [int]$bilingualAudit['catalog_rows']
    }
    $releaseMetadata = $null
    $releaseMetadataError = $null
    try {
        $releaseMetadata = Get-ReleaseMetadata
    } catch {
        $releaseMetadataError = $_.Exception.Message
    }
    return [PSCustomObject]@{
        GameRoot = $GameRoot
        SteamAppId = '3767850'
        SteamBuildId = $currentBuildId
        GameRunning = [bool](Get-TargetGameProcess)
        GameAssemblySha256 = $currentGameHash
        MetadataSha256 = $currentMetadataHash
        InteropHash = $interopHash
        InteropFresh = $interopFresh
        InteropReferenceFiles = "$($existingInteropFiles.Count)/$($interopFiles.Count)"
        InteropAutoUpdate = $interopAutoUpdate
        SourceBundle = (Split-Path -Leaf $Paths.SourceBundle)
        SourceBundleSha256 = $sourceBundleHash
        SourceAuditFresh = ($sourceBundleHash -eq $sourceAuditHash)
        UncoveredSourceStrings = $uncoveredSources
        MonsterSourceCoverage = $monsterCoverage
        SharedAssets = (Split-Path -Leaf $Paths.SharedAssets)
        SharedAssetsSha256 = $sharedAssetsHash
        SkillAliasAuditFresh = $skillAliasAuditFresh
        SkillAliasDictionaryFresh = $skillAliasDictionaryFresh
        SkillAliasDisplayCoverage = $skillAliasCoverage
        SkillAliasCoverageComplete = $skillAliasCoverageComplete
        RuntimeNameAuditFresh = $runtimeNameAuditFresh
        RuntimeNameDictionaryFresh = $runtimeNameDictionaryFresh
        RuntimeNameDisplayCoverage = $runtimeNameCoverage
        RuntimeNameCoverageComplete = $runtimeNameCoverageComplete
        BilingualCatalogFresh = $bilingualCatalogFresh
        BilingualCatalogCoverageComplete = $bilingualCatalogCoverageComplete
        BilingualCatalogRows = $bilingualCatalogRows
        BuiltBilingualCatalogSha256 = Get-Sha256 $Paths.BilingualCatalog
        DeployedBilingualCatalogSha256 = Get-Sha256 $Paths.DeployedBilingualCatalog
        PluginVersion = Get-SourceConstant $pluginCode 'public const string PluginVersion = "([^"]+)";'
        InstallerVersion = Get-SourceConstant $Paths.InstallerService 'public const string Version = "([^"]+)";'
        InstallerProjectVersion = Get-SourceConstant $Paths.InstallerProject '<Version>([^<]+)</Version>'
        InstallerFileVersion = Get-SourceConstant $Paths.InstallerProject '<FileVersion>(\d+\.\d+\.\d+)(?:\.\d+)?</FileVersion>'
        InstallerProductVersion = Get-SourceConstant $Paths.InstallerProject '<Product>[^<]*\bv(\d+\.\d+\.\d+)\b[^<]*</Product>'
        InstallerAssemblyTitleVersion = Get-SourceConstant $Paths.InstallerProject '<AssemblyTitle>[^<]*\bv(\d+\.\d+\.\d+)\b[^<]*</AssemblyTitle>'
        RepositoryVersion = if ($releaseMetadata) { [string]$releaseMetadata.Version } else { $null }
        ChangelogVersion = if ($releaseMetadata) { [string]$releaseMetadata.ChangelogVersion } else { $null }
        ReleaseKind = if ($releaseMetadata) { [string]$releaseMetadata.ReleaseKind } else { $null }
        LastLiveVerifiedVersion = if ($releaseMetadata) { [string]$releaseMetadata.LiveVersion } else { $null }
        ReleaseMetadataValid = ($null -ne $releaseMetadata)
        ReleaseMetadataError = $releaseMetadataError
        BuiltPluginSha256 = Get-Sha256 $Paths.PluginBuild
        DeployedPluginSha256 = Get-Sha256 $Paths.DeployedPlugin
        ArtifactTranslations = Get-TranslationCount $Paths.ArtifactDictionary
        DeployedTranslations = Get-TranslationCount $Paths.DeployedDictionary
        InstallerGameHashApproved = $installerGameHashApproved
        InstallerVerifiedBuildCount = $verifiedBuilds.Count
        InstallerSha256 = Get-Sha256 $Paths.InstallerExe
        ActiveXUnityFiles = @(Get-ActiveXUnityFiles).Count
        LogErrorLines = $logErrors
        RuntimeResidualSession = $runtimeResiduals.Session
        RuntimeCastResiduals = $runtimeResiduals.CastCount
        RuntimeDescriptionResiduals = $runtimeResiduals.DescriptionCount
        RuntimeMapResiduals = $runtimeResiduals.MapCount
        RuntimeItemNameResiduals = $runtimeResiduals.ItemNameCount
        RuntimeUiResiduals = $runtimeResiduals.UiCount
    }
}

function Show-Status {
    Get-LoopStatus | Format-List
}

function Get-QueueItems {
    $status = Get-LoopStatus
    $items = [System.Collections.Generic.List[object]]::new()
    function Add-Item([string]$Priority, [string]$Action, [string]$Reason) {
        $items.Add([PSCustomObject]@{ Priority = $Priority; Action = $Action; Reason = $Reason })
    }

    if ($status.GameRunning) { Add-Item 'BLOCKER' 'CloseGame' 'SpiritVale is running; deployment is locked.' }
    if ($status.InteropAutoUpdate -eq $false) { Add-Item 'BLOCKER' 'EnableInteropUpdates' 'BepInEx UpdateInteropAssemblies is false.' }
    if (-not $status.InteropFresh) { Add-Item 'BLOCKER' 'ColdBootInterop' 'Interop is missing or older than the current game binaries.' }
    if ($status.ActiveXUnityFiles -gt 0) { Add-Item 'BLOCKER' 'DisableXUnity' "$($status.ActiveXUnityFiles) active XUnity DLL(s) were found." }
    if (-not $status.ReleaseMetadataValid) {
        Add-Item 'BLOCKER' 'SyncReleaseMetadata' $status.ReleaseMetadataError
    }
    if (-not $status.SourceAuditFresh) { Add-Item 'BLOCKER' 'AuditSources' 'The current Addressables source bundle has not been audited.' }
    elseif ($status.UncoveredSourceStrings -gt 0) { Add-Item 'REQUIRED' 'ReviewNewSources' "$($status.UncoveredSourceStrings) unique fixed source string(s) are not covered." }
    if (-not $status.SkillAliasAuditFresh) {
        Add-Item 'BLOCKER' 'AuditSkillAliases' 'The current sharedassets skill display strings have not been audited.'
    } elseif (-not $status.SkillAliasDictionaryFresh) {
        Add-Item 'REQUIRED' 'AuditSkillAliases' 'The skill alias audit does not match the current translation dictionary.'
    } elseif (-not $status.SkillAliasCoverageComplete) {
        Add-Item 'REQUIRED' 'ReviewSkillAliases' "Runtime skill display coverage is $($status.SkillAliasDisplayCoverage)."
    }
    if (-not $status.RuntimeNameAuditFresh) {
        Add-Item 'BLOCKER' 'AuditRuntimeNames' 'Current sharedassets item and entity display strings have not been audited.'
    } elseif (-not $status.RuntimeNameDictionaryFresh) {
        Add-Item 'REQUIRED' 'AuditRuntimeNames' 'The runtime name audit does not match the current translation dictionary.'
    } elseif (-not $status.RuntimeNameCoverageComplete) {
        Add-Item 'REQUIRED' 'ReviewRuntimeNames' "Runtime item/entity name coverage is $($status.RuntimeNameDisplayCoverage)."
    }
    if (-not $status.BilingualCatalogFresh) {
        Add-Item 'REQUIRED' 'BuildBilingualCatalog' 'The bilingual entity catalog is missing or stale for the current audited inputs.'
    } elseif (-not $status.BilingualCatalogCoverageComplete) {
        Add-Item 'REQUIRED' 'ReviewBilingualCatalog' 'The bilingual entity catalog does not cover every expected entity.'
    }

    $dictionaryInputs = @(
        $Paths.Generator,
        $Paths.RuntimeNameModule,
        (Join-Path $ToolRoot 'online-translations.json'),
        (Join-Path $ToolRoot 'glossary-translations.json'),
        (Join-Path $ToolRoot 'apply_game_data_localization.py'),
        (Join-Path $ToolRoot 'missing-zh-clean.tsv'),
        (Join-Path $ToolRoot 'missing-zh-final.tsv'),
        (Join-Path $ToolRoot 'remaining-source-translations.json'),
        (Join-Path $ToolRoot 'missing-zh-reviewed.json'),
        (Join-Path $ToolRoot 'missing-zh-reviewed-source-overrides.json'),
        (Join-Path $ToolRoot 'runtime-manual-overrides.json'),
        (Join-Path $ToolRoot 'mmo-quality-overrides.json'),
        $Paths.SourceRaw,
        $Paths.SharedAssets
    )
    $artifactTime = Get-MaxWriteTime @($Paths.ArtifactDictionary)
    if ($artifactTime -lt (Get-MaxWriteTime $dictionaryInputs)) {
        Add-Item 'REQUIRED' 'BuildDictionary' 'Reviewed translation inputs are newer than the generated artifact.'
    }

    $pluginInputs = @(Get-ProjectFiles $Paths.PluginSource @('*.cs', '*.csproj')) + $InteropReferences
    if ((Get-MaxWriteTime @($Paths.PluginBuild)) -lt (Get-MaxWriteTime $pluginInputs)) {
        Add-Item 'REQUIRED' 'BuildPlugin' 'Plugin source or generated interop is newer than the Release DLL.'
    }
    if ((Get-Sha256 $Paths.PluginBuild) -ne (Get-Sha256 $Paths.DeployedPlugin) -or
        (Get-Sha256 $Paths.ArtifactDictionary) -ne (Get-Sha256 $Paths.DeployedDictionary) -or
        (Get-Sha256 $Paths.BilingualCatalog) -ne (Get-Sha256 $Paths.DeployedBilingualCatalog)) {
        Add-Item 'REQUIRED' 'Deploy' 'Built artifacts and deployed files differ.'
    }
    if (-not $status.InstallerGameHashApproved) {
        Add-Item 'AFTER-LIVE-TEST' 'ApproveGameHash' 'Installer compatibility hash does not match the current build.'
    }
    $installerVersions = @(
        $status.InstallerVersion,
        $status.InstallerProjectVersion,
        $status.InstallerFileVersion,
        $status.InstallerProductVersion,
        $status.InstallerAssemblyTitleVersion
    ) | Select-Object -Unique
    if (@($installerVersions).Count -ne 1) {
        Add-Item 'BLOCKER' 'SyncInstallerVersion' 'Installer source and metadata versions are inconsistent.'
    }

    $installerInputs = @(Get-ChildItem -LiteralPath (Join-Path $ToolRoot 'installer') -Recurse -File |
        Where-Object {
            $_.FullName -notmatch '(?i)\\(bin|obj|dist|payload-stage|release|self-test-[^\\]+|release-selftest-[^\\]+)\\' -and
            $_.Name -ne 'Payload.zip'
        } | Select-Object -ExpandProperty FullName)
    $packageInputs = @($Paths.PluginBuild, $Paths.ArtifactDictionary, $Paths.BilingualCatalog) + $installerInputs
    if ((Get-MaxWriteTime @($Paths.InstallerExe)) -lt (Get-MaxWriteTime $packageInputs)) {
        Add-Item 'REQUIRED' 'Package' 'Release inputs are newer than the published installer.'
    }
    if ($status.LogErrorLines -gt 0) { Add-Item 'REVIEW' 'InspectLog' "$($status.LogErrorLines) error/fatal line(s) exist in the latest BepInEx log." }
    if ($status.RuntimeCastResiduals -gt 0) {
        Add-Item 'REQUIRED' 'ReviewRuntimeCastText' "$($status.RuntimeCastResiduals) untranslated cast announcement(s) remain in the latest runtime session."
    }
    if ($status.RuntimeDescriptionResiduals -gt 0) {
        Add-Item 'REQUIRED' 'ReviewRuntimeDescriptions' "$($status.RuntimeDescriptionResiduals) gameplay description/type residual(s) remain in the latest runtime session."
    }
    if ($status.RuntimeMapResiduals -gt 0) {
        Add-Item 'REQUIRED' 'ReviewRuntimeMapText' "$($status.RuntimeMapResiduals) untranslated map label(s) remain in the latest runtime session."
    }
    if ($status.RuntimeItemNameResiduals -gt 0) {
        Add-Item 'REQUIRED' 'ReviewRuntimeItemNames' "$($status.RuntimeItemNameResiduals) untranslated runtime item name(s) remain in the latest runtime session."
    }
    if ($status.RuntimeUiResiduals -gt 0) {
        Add-Item 'REQUIRED' 'ReviewRuntimeUiText' "$($status.RuntimeUiResiduals) untranslated stable UI label(s) remain in the latest runtime session."
    }
    try {
        Assert-LiveVerification $status
    } catch {
        Add-Item 'AFTER-LIVE-TEST' 'RecordLive' $_.Exception.Message
    }
    return @($items)
}

function Show-Queue {
    $items = @(Get-QueueItems)
    if ($items.Count -eq 0) { Write-Output 'Queue is empty. Live verification is still required after game updates.'; return }
    $items | Format-Table -AutoSize -Wrap
}

function Invoke-SourceAudit {
    $dictionary = $Paths.ArtifactDictionary
    if (-not (Test-Path -LiteralPath $dictionary)) { $dictionary = $Paths.DeployedDictionary }
    New-Item -ItemType Directory -Path $Paths.Artifacts -Force | Out-Null
    $auditOutput = @(Invoke-Python @(
        $Paths.SourceAuditScript,
        '--tool-root', $ToolRoot,
        '--bundle', $Paths.SourceBundle,
        '--dictionary', $dictionary,
        '--baseline-raw', $Paths.SourceBaseline,
        '--raw-output', $Paths.SourceRaw,
        '--report', $Paths.SourceReport,
        '--summary', $Paths.SourceSummary,
        '--snapshot', $Paths.SourceSnapshot,
        '--build-id', (Get-SteamBuildId),
        '--game-assembly-hash', (Get-Sha256 $Paths.GameAssembly),
        '--metadata-hash', (Get-Sha256 $Paths.Metadata)
    ))
    foreach ($line in $auditOutput) { Write-Host $line }
    return Get-CaseSensitiveJsonMap $Paths.SourceSummary
}

function Invoke-SkillAliasAudit([string]$DictionaryPath) {
    if (-not $DictionaryPath) {
        $DictionaryPath = $Paths.ArtifactDictionary
        if (-not (Test-Path -LiteralPath $DictionaryPath -PathType Leaf)) { $DictionaryPath = $Paths.DeployedDictionary }
    }
    if (-not (Test-Path -LiteralPath $Paths.SourceSnapshot -PathType Leaf)) {
        throw 'The source snapshot is missing. Run -Stage Audit first.'
    }
    New-Item -ItemType Directory -Path $Paths.Artifacts -Force | Out-Null
    $auditOutput = @(Invoke-Python @(
        $Paths.SkillAliasAuditScript,
        '--tool-root', $ToolRoot,
        '--sharedassets', $Paths.SharedAssets,
        '--snapshot', $Paths.SourceSnapshot,
        '--dictionary', $DictionaryPath,
        '--report', $Paths.SkillAliasReport,
        '--summary', $Paths.SkillAliasSummary
    ))
    foreach ($line in $auditOutput) { Write-Host $line }
    return Get-CaseSensitiveJsonMap $Paths.SkillAliasSummary
}

function Assert-SkillAliasCoverage([object]$Audit, [string]$DictionaryPath) {
    $expected = [int]$Audit['expected_skill_ids']
    if ($expected -le 0) { throw "No active skill IDs were extracted. Inspect $($Paths.SkillAliasReport)" }
    if ([string]$Audit['sharedassets_sha256'] -ne (Get-Sha256 $Paths.SharedAssets) -or
        [string]$Audit['source_snapshot_sha256'] -ne (Get-Sha256 $Paths.SourceSnapshot) -or
        [string]$Audit['dictionary_sha256'] -ne (Get-Sha256 $DictionaryPath)) {
        throw 'Skill alias audit inputs changed while validation was running. Repeat the audit.'
    }
    if ([int]$Audit['resolved_skill_ids'] -ne $expected -or
        [int]$Audit['covered_display_ids'] -ne $expected -or
        [int]$Audit['uncovered_display_ids'] -ne 0 -or
        [int]$Audit['missing_skill_ids'] -ne 0 -or
        -not [bool]$Audit['coverage_complete']) {
        throw "Runtime skill display coverage is $($Audit['covered_display_ids'])/$expected. Inspect $($Paths.SkillAliasReport)"
    }
}

function Invoke-RuntimeNameAudit([string]$DictionaryPath) {
    if (-not $DictionaryPath) {
        $DictionaryPath = $Paths.ArtifactDictionary
        if (-not (Test-Path -LiteralPath $DictionaryPath -PathType Leaf)) { $DictionaryPath = $Paths.DeployedDictionary }
    }
    if (-not (Test-Path -LiteralPath $Paths.SourceSnapshot -PathType Leaf)) {
        throw 'The source snapshot is missing. Run -Stage Audit first.'
    }
    New-Item -ItemType Directory -Path $Paths.Artifacts -Force | Out-Null
    $auditOutput = @(Invoke-Python @(
        $Paths.RuntimeNameAuditScript,
        '--tool-root', $ToolRoot,
        '--sharedassets', $Paths.SharedAssets,
        '--snapshot', $Paths.SourceSnapshot,
        '--dictionary', $DictionaryPath,
        '--report', $Paths.RuntimeNameReport,
        '--summary', $Paths.RuntimeNameSummary
    ))
    foreach ($line in $auditOutput) { Write-Host $line }
    return Get-CaseSensitiveJsonMap $Paths.RuntimeNameSummary
}

function Assert-RuntimeNameCoverage([object]$Audit, [string]$DictionaryPath) {
    if ([string]$Audit['sharedassets_sha256'] -ne (Get-Sha256 $Paths.SharedAssets) -or
        [string]$Audit['source_snapshot_sha256'] -ne (Get-Sha256 $Paths.SourceSnapshot) -or
        [string]$Audit['dictionary_sha256'] -ne (Get-Sha256 $DictionaryPath)) {
        throw 'Runtime name audit inputs changed while validation was running. Repeat the audit.'
    }
    if ([int]$Audit['runtime_display_strings'] -lt 1000 -or
        [int]$Audit['uncovered_display_strings'] -ne 0 -or
        [int]$Audit['unresolved_aliases'] -ne 0 -or
        [int]$Audit['conflicting_aliases'] -ne 0 -or
        -not [bool]$Audit['coverage_complete']) {
        throw "Runtime item/entity name coverage is $($Audit['covered_display_strings'])/$($Audit['runtime_display_strings']). Inspect $($Paths.RuntimeNameReport)"
    }
}

function Invoke-BilingualCatalog([string]$DictionaryPath) {
    New-Item -ItemType Directory -Path $Paths.Artifacts -Force | Out-Null
    $output = @(Invoke-Python @(
        $Paths.BilingualCatalogGenerator,
        '--source-snapshot', $Paths.SourceSnapshot,
        '--runtime-names', $Paths.RuntimeNameReport,
        '--runtime-names-summary', $Paths.RuntimeNameSummary,
        '--skill-aliases', $Paths.SkillAliasReport,
        '--skill-aliases-summary', $Paths.SkillAliasSummary,
        '--dictionary', $DictionaryPath,
        '--map-manifest', $Paths.BilingualMapManifest,
        '--catalog', $Paths.BilingualCatalog,
        '--audit', $Paths.BilingualCatalogAudit
    ))
    foreach ($line in $output) { Write-Host $line }
    return Get-CaseSensitiveJsonMap $Paths.BilingualCatalogAudit
}

function Assert-BilingualCatalog([object]$Audit, [string]$DictionaryPath) {
    if ([string]$Audit['source_snapshot_sha256'] -ne (Get-Sha256 $Paths.SourceSnapshot) -or
        [string]$Audit['runtime_names_sha256'] -ne (Get-Sha256 $Paths.RuntimeNameReport) -or
        [string]$Audit['runtime_names_summary_sha256'] -ne (Get-Sha256 $Paths.RuntimeNameSummary) -or
        [string]$Audit['skill_aliases_sha256'] -ne (Get-Sha256 $Paths.SkillAliasReport) -or
        [string]$Audit['skill_aliases_summary_sha256'] -ne (Get-Sha256 $Paths.SkillAliasSummary) -or
        [string]$Audit['dictionary_sha256'] -ne (Get-Sha256 $DictionaryPath) -or
        [string]$Audit['map_manifest_sha256'] -ne (Get-Sha256 $Paths.BilingualMapManifest) -or
        [string]$Audit['catalog_sha256'] -ne (Get-Sha256 $Paths.BilingualCatalog)) {
        throw 'Bilingual entity catalog inputs changed while validation was running. Repeat validation.'
    }
    if (-not [bool]$Audit['coverage_complete'] -or
        [int]$Audit['missing_rows'] -ne 0 -or
        [int]$Audit['catalog_rows'] -lt 1000) {
        throw "Bilingual entity catalog coverage is incomplete. Inspect $($Paths.BilingualCatalogAudit)"
    }
    foreach ($category in @('Item', 'Equip', 'Artifact', 'Gem', 'Skill', 'SkillPassive', 'Monster', 'Map')) {
        $coverage = $Audit['category_coverage'][$category]
        if (-not $coverage -or [int]$coverage['expected'] -le 0 -or
            [int]$coverage['covered'] -ne [int]$coverage['expected'] -or
            [int]$coverage['missing'] -ne 0) {
            throw "Bilingual entity category '$category' is incomplete. Inspect $($Paths.BilingualCatalogAudit)"
        }
    }
}

function Get-RegexSignature([string]$Text, [string]$Pattern, [bool]$SortValues) {
    $values = @([regex]::Matches($Text, $Pattern) | ForEach-Object { $_.Value })
    if ($SortValues) { $values = @($values | Sort-Object) }
    return ($values -join [char]0x1F)
}

function Assert-FormattingTokens([string]$Source, [string]$Target, [string]$Path, [int]$LineNumber) {
    $sourcePlaceholders = Get-RegexSignature $Source '\{\d+(?::[^{}]+)?\}' $true
    $targetPlaceholders = Get-RegexSignature $Target '\{\d+(?::[^{}]+)?\}' $true
    if ($sourcePlaceholders -ne $targetPlaceholders) { throw "Placeholder mismatch at ${Path}:$LineNumber" }
    $sourceTags = Get-RegexSignature $Source '</?[A-Za-z][^>]*>' $false
    $targetTags = Get-RegexSignature $Target '</?[A-Za-z][^>]*>' $false
    if ($sourceTags -ne $targetTags) { throw "Rich-text tag mismatch at ${Path}:$LineNumber" }
}

function Test-TranslationTable([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Translation table is missing: $Path" }
    $map = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadAllLines($Path, [System.Text.Encoding]::UTF8)) {
        $lineNumber++
        if (-not $line -or $line.StartsWith('#')) { continue }
        $parts = $line.Split("`t")
        if ($parts.Count -ne 2 -or -not $parts[0] -or -not $parts[1]) { throw "Unsafe TSV row at ${Path}:$lineNumber" }
        if ($map.ContainsKey($parts[0])) { throw "Duplicate source '$($parts[0])' at ${Path}:$lineNumber" }
        if ($parts[0] -eq $parts[1]) { throw "No-op translation '$($parts[0])' at ${Path}:$lineNumber" }
        if ($parts[0].Trim().ToLowerInvariant() -in @('to', 'for')) { throw "Unsafe chat fragment '$($parts[0])' in $Path" }
        Assert-FormattingTokens $parts[0] $parts[1] $Path $lineNumber
        $map[$parts[0]] = $parts[1]
    }
    if ($map.Count -lt 100) { throw "Translation table is unexpectedly small: $($map.Count) entries." }

    $baselineKeys = @(Get-TranslationSourceKeys $Paths.DeployedDictionary)
    $missingBaselineKeys = @($baselineKeys | Where-Object { -not $map.ContainsKey($_) })
    if ($missingBaselineKeys.Count -gt 0) {
        $sample = @($missingBaselineKeys | Sort-Object | Select-Object -First 10) -join ', '
        throw "Translation vocabulary dropped: $($missingBaselineKeys.Count) deployed source key(s) are missing from $Path. Missing sample: $sample"
    }
    $additionCount = $map.Count - $baselineKeys.Count
    Write-Output "Translation vocabulary check passed: baseline=$($baselineKeys.Count), current=$($map.Count), additions=$additionCount."

    $effectiveOverrides = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    $qualityOverrides = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    $gemSuffix = [string]::Concat([char]0x5B9D, [char]0x77F3)
    foreach ($overrideFile in @(
        'runtime-manual-overrides.json',
        'missing-zh-reviewed-source-overrides.json',
        'mmo-quality-overrides.json'
    )) {
        $overridePath = Join-Path $ToolRoot $overrideFile
        if (-not (Test-Path -LiteralPath $overridePath)) { continue }
        $overrides = Get-CaseSensitiveJsonMap $overridePath
        foreach ($key in $overrides.Keys) {
            $effectiveOverrides[$key] = [string]$overrides[$key]
            if ($overrideFile -eq 'mmo-quality-overrides.json') {
                $qualityOverrides[$key] = [string]$overrides[$key]
            }
        }
    }
    foreach ($key in $effectiveOverrides.Keys) {
        $expected = $effectiveOverrides[$key]
        if ($key.EndsWith(' Gem', [System.StringComparison]::Ordinal) -and
            -not $qualityOverrides.ContainsKey($key)) {
            $baseKey = $key.Substring(0, $key.Length - ' Gem'.Length)
            if ($map.ContainsKey($baseKey)) {
                $expected = $map[$baseKey] + $gemSuffix
            }
        }
        if (-not $map.ContainsKey($key) -or $map[$key] -ne $expected) {
            $actual = if ($map.ContainsKey($key)) { $map[$key] } else { '<missing>' }
            throw "Reviewed override '$key' is missing or stale in $Path (expected '$expected', actual '$actual')"
        }
    }
    Write-Output "Validated $($map.Count) translations: $Path"
}

function Invoke-Validation([string]$DictionaryPath, [bool]$RunTests) {
    Assert-ReleaseMetadata
    $forbidden = 'ClassInjector|RegisterTypeInIl2Cpp|AddComponent\s*<|RuntimeLocalizationScanner'
    $hits = @(Get-ChildItem -LiteralPath $Paths.PluginSource -Recurse -File -Filter '*.cs' |
        Select-String -Pattern $forbidden -AllMatches)
    if ($hits.Count -gt 0) { throw "Forbidden IL2CPP injection pattern found: $($hits.Path -join ', ')" }
    $activeXUnity = @(Get-ActiveXUnityFiles)
    if ($activeXUnity.Count -gt 0) { throw "Active XUnity DLLs must be disabled: $($activeXUnity -join ', ')" }
    $status = Get-LoopStatus
    if ($status.InteropAutoUpdate -eq $false) { throw 'BepInEx UpdateInteropAssemblies must be true before validating an update.' }
    if (-not $status.SourceAuditFresh) { throw 'The current game config bundle has not been audited. Run -Stage Audit first.' }
    if ($status.UncoveredSourceStrings -gt 0) { throw "$($status.UncoveredSourceStrings) source strings remain unreviewed. Inspect $($Paths.SourceReport)" }
    $installerVersions = @(
        $status.InstallerVersion,
        $status.InstallerProjectVersion,
        $status.InstallerFileVersion,
        $status.InstallerProductVersion,
        $status.InstallerAssemblyTitleVersion
    ) | Select-Object -Unique
    if (@($installerVersions).Count -ne 1) {
        throw 'Installer source and metadata versions are inconsistent.'
    }
    Test-TranslationTable $DictionaryPath
    $skillAliasAudit = Invoke-SkillAliasAudit $DictionaryPath
    Assert-SkillAliasCoverage $skillAliasAudit $DictionaryPath
    $runtimeNameAudit = Invoke-RuntimeNameAudit $DictionaryPath
    Assert-RuntimeNameCoverage $runtimeNameAudit $DictionaryPath
    $bilingualAudit = Invoke-BilingualCatalog $DictionaryPath
    Assert-BilingualCatalog $bilingualAudit $DictionaryPath
    if ($RunTests) {
        Invoke-Native 'dotnet' @(
            'run', '--project', $Paths.TestsProject, '-c', 'Release', '--',
            '--dictionary', $DictionaryPath,
            '--snapshot', $Paths.SourceSnapshot,
            '--skill-aliases', $Paths.SkillAliasReport,
            '--quality-overrides', $Paths.QualityOverrides,
            '--corpus-report', $Paths.LocalizedCorpusReport,
            '--residual-report', $Paths.MixedDescriptionReport
        )
        Invoke-Native 'dotnet' @(
            'run', '--project', $Paths.BilingualTestsProject, '-c', 'Release', '--no-restore'
        )
        Invoke-Python @(
            (Join-Path $ToolRoot 'tests\test_bilingual_entity_catalog.py')
        )
    }
    Write-Output 'Static localization validation passed.'
}

function Update-InstallerSourceVersion([string]$Version) {
    Set-SourceRegex $Paths.InstallerService 'public const string Version = "[^"]+";' "public const string Version = `"$Version`";" 'PatchInfo.Version'
    Set-SourceRegex $Paths.InstallerProject '<Version>[^<]+</Version>' "<Version>$Version</Version>" 'installer Version'
    Set-SourceRegex $Paths.InstallerProject '<FileVersion>[^<]+</FileVersion>' "<FileVersion>$Version.0</FileVersion>" 'installer FileVersion'
    Set-SourceRegex $Paths.InstallerProject '(<Product>[^<]*\bv)\d+\.\d+\.\d+(\b[^<]*</Product>)' ('${1}' + $Version + '${2}') 'installer Product display version'
    Set-SourceRegex $Paths.InstallerProject '(<AssemblyTitle>[^<]*\bv)\d+\.\d+\.\d+(\b[^<]*</AssemblyTitle>)' ('${1}' + $Version + '${2}') 'installer AssemblyTitle display version'
}

function Set-RepositoryVersion([string]$Version) {
    [System.IO.File]::WriteAllText(
        $Paths.RepositoryVersion,
        $Version + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
}

function Update-PatchVersion([string]$Version) {
    if (-not $Version) { return }
    $pluginCode = Join-Path $Paths.PluginSource 'RuntimeLocalizationPlugin.cs'
    Set-SourceRegex $pluginCode 'public const string PluginVersion = "[^"]+";' "public const string PluginVersion = `"$Version`";" 'PluginVersion'
    Update-InstallerSourceVersion $Version
    Set-RepositoryVersion $Version
    Write-Output "Patch version synchronized to $Version."
}

function Update-InstallerReleaseVersion([string]$Version) {
    if (-not $Version) { return }
    Assert-LiveVerification (Get-LoopStatus)
    Update-InstallerSourceVersion $Version
    Set-RepositoryVersion $Version
    Write-Output "Installer-only release version synchronized to $Version; runtime payload version is unchanged."
}

function Sync-InstallerGameHash {
    $status = Get-LoopStatus
    Assert-LiveVerification $status
    $policy = Get-Content -LiteralPath $Paths.InstallerCompatibilityPolicy -Raw -Encoding UTF8 | ConvertFrom-Json
    $alreadyApproved = @($policy.verifiedBuilds | Where-Object {
        $_.steamBuildId -eq $status.SteamBuildId -and
        $_.gameAssemblySha256 -eq $status.GameAssemblySha256 -and
        $_.metadataSha256 -eq $status.MetadataSha256
    }).Count -gt 0
    if (-not $alreadyApproved) {
        $policy.verifiedBuilds += [PSCustomObject]@{
            steamBuildId = [string]$status.SteamBuildId
            gameAssemblySha256 = [string]$status.GameAssemblySha256
            metadataSha256 = [string]$status.MetadataSha256
            verifiedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
        [System.IO.File]::WriteAllText(
            $Paths.InstallerCompatibilityPolicy,
            (($policy | ConvertTo-Json -Depth 8) + [Environment]::NewLine),
            [System.Text.UTF8Encoding]::new($false))
    }
    Write-Output "Installer verified-build policy approved Build $($status.SteamBuildId), hash $($status.GameAssemblySha256)."
}

$RequiredLiveSurfaces = @(
    'server-list', 'character', 'hud', 'inventory', 'skills', 'monsters',
    'market', 'map', 'settings', 'bilingual-details', 'english-toggle'
)

function Get-VerificationState([object]$Status) {
    $sourceAudit = Get-CaseSensitiveJsonMap $Paths.SourceSummary
    return [ordered]@{
        steam_build_id = [string]$Status.SteamBuildId
        game_assembly_sha256 = [string]$Status.GameAssemblySha256
        metadata_sha256 = [string]$Status.MetadataSha256
        interop_hash = [string]$Status.InteropHash
        source_bundle_sha256 = [string]$Status.SourceBundleSha256
        source_raw_sha256 = [string]$sourceAudit['raw_sha256']
        sharedassets_sha256 = [string]$Status.SharedAssetsSha256
        skill_alias_audit_sha256 = [string](Get-Sha256 $Paths.SkillAliasSummary)
        runtime_name_audit_sha256 = [string](Get-Sha256 $Paths.RuntimeNameSummary)
        patch_version = [string]$Status.PluginVersion
        plugin_sha256 = [string](Get-Sha256 $Paths.DeployedPlugin)
        dictionary_sha256 = [string](Get-Sha256 $Paths.DeployedDictionary)
        bilingual_catalog_sha256 = [string](Get-Sha256 $Paths.DeployedBilingualCatalog)
        bilingual_catalog_audit_sha256 = [string](Get-Sha256 $Paths.BilingualCatalogAudit)
        bilingual_catalog_rows = [int]$Status.BilingualCatalogRows
        translation_count = [int](Get-TranslationCount $Paths.DeployedDictionary)
        log_sha256 = [string](Get-Sha256 $Paths.Log)
        runtime_untranslated_log_sha256 = [string](Get-Sha256 $Paths.UntranslatedLog)
    }
}

function Assert-LiveVerification([object]$Status) {
    if (-not (Test-Path -LiteralPath $Paths.LiveVerification -PathType Leaf)) { throw 'No live verification record exists for the current artifacts.' }
    if ($Status.GameRunning) { throw 'SpiritVale is running; live verification cannot be finalized or packaged.' }
    if (-not $Status.SourceAuditFresh -or $Status.UncoveredSourceStrings -gt 0) { throw 'Source coverage is stale or incomplete.' }
    if (-not $Status.SkillAliasAuditFresh -or -not $Status.SkillAliasDictionaryFresh -or -not $Status.SkillAliasCoverageComplete) {
        throw 'Runtime skill display coverage is stale or incomplete.'
    }
    if (-not $Status.RuntimeNameAuditFresh -or -not $Status.RuntimeNameDictionaryFresh -or -not $Status.RuntimeNameCoverageComplete) {
        throw 'Runtime item and entity name coverage is stale or incomplete.'
    }
    if (-not $Status.BilingualCatalogFresh -or -not $Status.BilingualCatalogCoverageComplete) {
        throw 'Bilingual entity catalog coverage is stale or incomplete.'
    }
    if ($Status.LogErrorLines -gt 0) { throw 'The latest BepInEx log contains error/fatal lines.' }
    if ($Status.RuntimeCastResiduals -gt 0) { throw 'The latest runtime session contains untranslated cast announcements.' }
    if ($Status.RuntimeDescriptionResiduals -gt 0) { throw 'The latest runtime session contains unresolved gameplay descriptions or type labels.' }
    if ($Status.RuntimeMapResiduals -gt 0) { throw 'The latest runtime session contains untranslated map labels.' }
    if ($Status.RuntimeItemNameResiduals -gt 0) { throw 'The latest runtime session contains untranslated runtime item names.' }
    if ($Status.RuntimeUiResiduals -gt 0) { throw 'The latest runtime session contains untranslated stable UI labels.' }
    if ((Get-Sha256 $Paths.PluginBuild) -ne (Get-Sha256 $Paths.DeployedPlugin) -or
        (Get-Sha256 $Paths.ArtifactDictionary) -ne (Get-Sha256 $Paths.DeployedDictionary) -or
        (Get-Sha256 $Paths.BilingualCatalog) -ne (Get-Sha256 $Paths.DeployedBilingualCatalog)) {
        throw 'Built and deployed artifacts differ; repeat deployment and live verification.'
    }

    $record = Get-CaseSensitiveJsonMap $Paths.LiveVerification
    $current = Get-VerificationState $Status
    foreach ($key in $current.Keys) {
        if ([string]$record[$key] -ne [string]$current[$key]) { throw "Live verification is stale for $key." }
    }
    if ([int]$record['cold_starts'] -lt 2) { throw 'Live verification must include two cold starts.' }
    $surfaces = @($record['verified_surfaces'] | ForEach-Object { ([string]$_).ToLowerInvariant() })
    $missing = @($RequiredLiveSurfaces | Where-Object { $_ -notin $surfaces })
    if ($missing.Count -gt 0) { throw "Live verification is missing: $($missing -join ', ')." }
    $evidenceRecords = @($record['evidence'])
    if ($evidenceRecords.Count -eq 0) { throw 'Live verification has no screenshot evidence.' }
    foreach ($evidenceRecord in $evidenceRecords) {
        $evidencePath = [string]$evidenceRecord['path']
        if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) { throw "Live evidence is missing: $evidencePath" }
        if ((Get-Sha256 $evidencePath) -ne [string]$evidenceRecord['sha256']) { throw "Live evidence changed: $evidencePath" }
    }
}

function Write-LiveVerification {
    Assert-ReleaseMetadata
    $status = Get-LoopStatus
    if ($ColdStarts -lt 2) { throw 'RecordLive requires -ColdStarts 2 or greater.' }
    $surfaces = @($VerifiedSurface | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ } | Select-Object -Unique)
    $missing = @($RequiredLiveSurfaces | Where-Object { $_ -notin $surfaces })
    if ($missing.Count -gt 0) { throw "RecordLive is missing required surfaces: $($missing -join ', ')" }
    $evidencePaths = @($Evidence | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($evidencePaths.Count -eq 0) { throw 'RecordLive requires at least one -Evidence screenshot path.' }
    foreach ($path in $evidencePaths) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Evidence file is missing: $path" }
    }
    Assert-LiveVerificationPrerequisites $status
    $state = Get-VerificationState $status
    $record = [ordered]@{ schema_version = 1; recorded_at_utc = [DateTime]::UtcNow.ToString('o'); cold_starts = $ColdStarts }
    foreach ($key in $state.Keys) { $record[$key] = $state[$key] }
    $record['verified_surfaces'] = $surfaces
    $record['evidence'] = @($evidencePaths | ForEach-Object {
        $resolved = (Resolve-Path -LiteralPath $_).Path
        [ordered]@{ path = $resolved; sha256 = Get-Sha256 $resolved }
    })
    [System.IO.File]::WriteAllText(
        $Paths.LiveVerification,
        (($record | ConvertTo-Json -Depth 5) + [Environment]::NewLine),
        [System.Text.UTF8Encoding]::new($false)
    )
    Assert-LiveVerification (Get-LoopStatus)
    Write-Output "Live verification recorded: $($Paths.LiveVerification)"
}

function Assert-LiveVerificationPrerequisites([object]$Status) {
    if ($Status.GameRunning) { throw 'Exit SpiritVale before recording live verification.' }
    if (-not $Status.SourceAuditFresh -or $Status.UncoveredSourceStrings -gt 0) { throw 'Source coverage is stale or incomplete.' }
    if (-not $Status.SkillAliasAuditFresh -or -not $Status.SkillAliasDictionaryFresh -or -not $Status.SkillAliasCoverageComplete) {
        throw 'Runtime skill display coverage is stale or incomplete.'
    }
    if (-not $Status.RuntimeNameAuditFresh -or -not $Status.RuntimeNameDictionaryFresh -or -not $Status.RuntimeNameCoverageComplete) {
        throw 'Runtime item and entity name coverage is stale or incomplete.'
    }
    if (-not $Status.BilingualCatalogFresh -or -not $Status.BilingualCatalogCoverageComplete) {
        throw 'Bilingual entity catalog coverage is stale or incomplete.'
    }
    if ($Status.LogErrorLines -gt 0) { throw 'The latest BepInEx log contains error/fatal lines.' }
    if ($Status.RuntimeCastResiduals -gt 0) { throw 'The latest runtime session contains untranslated cast announcements.' }
    if ($Status.RuntimeDescriptionResiduals -gt 0) { throw 'The latest runtime session contains unresolved gameplay descriptions or type labels.' }
    if ($Status.RuntimeMapResiduals -gt 0) { throw 'The latest runtime session contains untranslated map labels.' }
    if ($Status.RuntimeItemNameResiduals -gt 0) { throw 'The latest runtime session contains untranslated runtime item names.' }
    if ($Status.RuntimeUiResiduals -gt 0) { throw 'The latest runtime session contains untranslated stable UI labels.' }
    if (-not (Test-Path -LiteralPath $Paths.Log -PathType Leaf)) { throw 'BepInEx LogOutput.log is missing.' }
    if ((Get-Sha256 $Paths.PluginBuild) -ne (Get-Sha256 $Paths.DeployedPlugin) -or
        (Get-Sha256 $Paths.ArtifactDictionary) -ne (Get-Sha256 $Paths.DeployedDictionary) -or
        (Get-Sha256 $Paths.BilingualCatalog) -ne (Get-Sha256 $Paths.DeployedBilingualCatalog)) {
        throw 'Built and deployed artifacts differ.'
    }
}

function Promote-SourceBaseline {
    $summary = Get-CaseSensitiveJsonMap $Paths.SourceSummary
    $snapshotRoot = Join-Path $ToolRoot 'backups\source-snapshots'
    New-Item -ItemType Directory -Path $snapshotRoot -Force | Out-Null
    if (Test-Path -LiteralPath $Paths.SourceBaseline) {
        $oldHash = Get-Sha256 $Paths.SourceBaseline
        $oldSnapshot = Join-Path $snapshotRoot ("baseline-$oldHash.raw")
        if (-not (Test-Path -LiteralPath $oldSnapshot)) { Copy-Item -LiteralPath $Paths.SourceBaseline -Destination $oldSnapshot }
    }
    $rawHash = [string]$summary['raw_sha256']
    $snapshotName = "build-$((Get-SteamBuildId))-$($rawHash.Substring(0, 16)).raw"
    $snapshot = Join-Path $snapshotRoot $snapshotName
    if (-not (Test-Path -LiteralPath $snapshot)) { Copy-Item -LiteralPath $Paths.SourceRaw -Destination $snapshot }
    $jsonSnapshot = [System.IO.Path]::ChangeExtension($snapshot, '.json')
    if (-not (Test-Path -LiteralPath $jsonSnapshot)) { Copy-Item -LiteralPath $Paths.SourceSnapshot -Destination $jsonSnapshot }
    Copy-Item -LiteralPath $Paths.SourceRaw -Destination $Paths.SourceBaseline -Force
    $null = Invoke-SourceAudit
    Write-Output "Source baseline promoted: $snapshot"
}

function Invoke-Build([bool]$ShouldDeploy, [bool]$RunTests) {
    $status = Get-LoopStatus
    if (-not $status.InteropFresh) { throw 'Interop is stale. Cold-start the updated game, wait for Il2CppInteropGen, exit, and retry.' }
    if (-not $status.SourceAuditFresh) { throw 'Source audit is stale. Run -Stage Audit before building.' }
    if ($ShouldDeploy -and $status.GameRunning) { throw 'SpiritVale is running. Exit the game before deployment.' }
    Update-PatchVersion $PatchVersion
    Update-InstallerReleaseVersion $InstallerVersion
    Assert-ReleaseMetadata
    New-Item -ItemType Directory -Path $Paths.Artifacts -Force | Out-Null
    Invoke-Python @(
        $Paths.Generator,
        '--output', $Paths.ArtifactDictionary,
        '--conflict-report', $Paths.ConflictReport,
        '--source-raw', $Paths.SourceRaw,
        '--sharedassets', $Paths.SharedAssets,
        '--source-snapshot', $Paths.SourceSnapshot
    )
    $sourceAudit = Invoke-SourceAudit
    if ([int]$sourceAudit['uncovered_sources'] -gt 0) {
        throw "$($sourceAudit['uncovered_sources']) source strings remain unreviewed. Inspect $($Paths.SourceReport)"
    }
    Invoke-Native 'dotnet' @('build', $Paths.PluginProject, '-c', 'Release', '-v:minimal', '-clp:ErrorsOnly', "-p:SpiritValeGameRoot=$GameRoot")
    Invoke-Validation $Paths.ArtifactDictionary $RunTests

    if ($ShouldDeploy) {
        $targetDirectory = Split-Path -Parent $Paths.DeployedPlugin
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        if (Get-TargetGameProcess) { throw 'SpiritVale started during the build. Deployment was cancelled.' }
        $backupRoot = Join-Path $ToolRoot ('deploy-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        $originalExists = @{}
        foreach ($pair in @(
            @($Paths.DeployedPlugin, 'SpiritVale.RuntimeLocalization.dll'),
            @($Paths.DeployedDictionary, 'translations.tsv'),
            @($Paths.DeployedBilingualCatalog, 'bilingual-entity-catalog.tsv')
        )) {
            $originalExists[$pair[0]] = Test-Path -LiteralPath $pair[0]
            if ($originalExists[$pair[0]]) { Copy-Item -LiteralPath $pair[0] -Destination (Join-Path $backupRoot $pair[1]) }
        }
        try {
            Copy-Item -LiteralPath $Paths.PluginBuild -Destination $Paths.DeployedPlugin -Force
            if (Get-TargetGameProcess) { throw 'SpiritVale started during deployment.' }
            Copy-Item -LiteralPath $Paths.ArtifactDictionary -Destination $Paths.DeployedDictionary -Force
            if (Get-TargetGameProcess) { throw 'SpiritVale started during deployment.' }
            Copy-Item -LiteralPath $Paths.BilingualCatalog -Destination $Paths.DeployedBilingualCatalog -Force
            if (Get-TargetGameProcess) { throw 'SpiritVale started during deployment.' }
        } catch {
            foreach ($pair in @(
                @('SpiritVale.RuntimeLocalization.dll', $Paths.DeployedPlugin),
                @('translations.tsv', $Paths.DeployedDictionary),
                @('bilingual-entity-catalog.tsv', $Paths.DeployedBilingualCatalog)
            )) {
                $backup = Join-Path $backupRoot $pair[0]
                if ($originalExists[$pair[1]] -and (Test-Path -LiteralPath $backup)) {
                    Copy-Item -LiteralPath $backup -Destination $pair[1] -Force
                } elseif (-not $originalExists[$pair[1]] -and (Test-Path -LiteralPath $pair[1])) {
                    Remove-Item -LiteralPath $pair[1] -Force
                }
            }
            throw
        }
        Write-Output "Deployed plugin $(Get-Sha256 $Paths.DeployedPlugin), $(Get-TranslationCount $Paths.DeployedDictionary) translations, and bilingual catalog $(Get-Sha256 $Paths.DeployedBilingualCatalog)."
    }
}

function Invoke-Package([bool]$BuildFirst) {
    if (Get-TargetGameProcess) { throw 'Exit SpiritVale before packaging.' }
    if ($BuildFirst) { Invoke-Build $false (-not $SkipTests) }
    $status = Get-LoopStatus
    Assert-LiveVerification $status
    if ($ApproveGameHash) { Sync-InstallerGameHash }
    $status = Get-LoopStatus
    if (-not $status.InstallerGameHashApproved) {
        throw 'Installer game hash is not approved. Complete live checks, then rerun Package with -ApproveGameHash.'
    }
    Assert-ReleaseMetadata
    & $Paths.Publish -GameRoot $GameRoot -PluginDll $Paths.PluginBuild `
        -Translations $Paths.ArtifactDictionary -EntityCatalog $Paths.BilingualCatalog
    if ($LASTEXITCODE -ne 0) { throw "Installer publish failed with exit code $LASTEXITCODE." }
    $distRoot = Split-Path -Parent $Paths.InstallerExe
    $distEntries = @(Get-ChildItem -LiteralPath $distRoot -Recurse -Force)
    $allowedDistFiles = @(
        'SpiritVale_Chinese_Patch.exe',
        'SHA256.txt',
        'SpiritVale_Chinese_Patch_Compatibility_x64.zip',
        'SpiritVale_Chinese_Patch_Compatibility_x64.sha256.txt',
        "SpiritVale_Chinese_Patch_v$($status.InstallerVersion).exe",
        "SpiritVale_Chinese_Patch_v$($status.InstallerVersion).exe.sha256.txt",
        "SpiritVale_Chinese_Patch_v$($status.InstallerVersion)_Compatibility_x64.zip",
        "SpiritVale_Chinese_Patch_v$($status.InstallerVersion)_Compatibility_x64.zip.sha256.txt",
        "release-v$($status.InstallerVersion).json"
    )
    $unexpectedDist = @($distEntries | Where-Object {
        $_.PSIsContainer -or $_.Name -notin $allowedDistFiles
    })
    if ($unexpectedDist.Count -gt 0) { throw "Installer dist contains unexpected output: $($unexpectedDist.FullName -join ', ')" }
    $installerHash = Get-Sha256 $Paths.InstallerExe
    $hashFile = Join-Path $distRoot 'SHA256.txt'
    if (-not (Test-Path -LiteralPath $hashFile) -or [System.IO.File]::ReadAllText($hashFile) -notmatch [regex]::Escape($installerHash)) {
        throw 'Installer SHA256.txt does not match the published executable.'
    }
    $compatibilityHash = Get-Sha256 $Paths.InstallerCompatibilityZip
    $compatibilityHashFile = Join-Path $distRoot 'SpiritVale_Chinese_Patch_Compatibility_x64.sha256.txt'
    if (-not $compatibilityHash -or
        -not (Test-Path -LiteralPath $compatibilityHashFile) -or
        [System.IO.File]::ReadAllText($compatibilityHashFile) -notmatch [regex]::Escape($compatibilityHash)) {
        throw 'Compatibility ZIP hash file does not match the published package.'
    }
    $null = Get-ReleaseMetadata -RequireLiveVerification -ArtifactsDirectory $distRoot

    $selfTestRoot = Join-Path $ToolRoot ('installer\self-test-' + [Guid]::NewGuid().ToString('N'))
    $selfTest = Start-Process -FilePath $Paths.InstallerExe `
        -ArgumentList @('--self-test', ('"{0}"' -f $selfTestRoot)) -WindowStyle Hidden -Wait -PassThru
    if ($selfTest.ExitCode -ne 0) { throw "Installer self-test failed. Inspect $selfTestRoot\self-test.log" }
    Remove-Item -LiteralPath $selfTestRoot -Recurse -Force

    $compatibilityExtractRoot = Join-Path $ToolRoot ('installer\compatibility-extract-' + [Guid]::NewGuid().ToString('N'))
    $compatibilitySelfTestRoot = Join-Path $ToolRoot ('installer\self-test-compatibility-' + [Guid]::NewGuid().ToString('N'))
    try {
        Expand-Archive -LiteralPath $Paths.InstallerCompatibilityZip -DestinationPath $compatibilityExtractRoot
        $compatibilityExecutables = @(Get-ChildItem -LiteralPath $compatibilityExtractRoot -Recurse -File |
            Where-Object { $_.Name -eq 'SpiritVale_Chinese_Patch.exe' })
        if ($compatibilityExecutables.Count -ne 1) {
            throw "Compatibility ZIP must contain exactly one installer EXE; found $($compatibilityExecutables.Count)."
        }
        if (-not (Test-Path -LiteralPath (Join-Path $compatibilityExecutables[0].DirectoryName 'coreclr.dll') -PathType Leaf)) {
            throw 'Compatibility ZIP does not contain the self-contained .NET runtime.'
        }
        $compatibilitySelfTest = Start-Process -FilePath $compatibilityExecutables[0].FullName `
            -ArgumentList @('--self-test', ('"{0}"' -f $compatibilitySelfTestRoot)) -WindowStyle Hidden -Wait -PassThru
        if ($compatibilitySelfTest.ExitCode -ne 0) {
            throw "Compatibility installer self-test failed. Inspect $compatibilitySelfTestRoot\self-test.log"
        }
    } finally {
        if (Test-Path -LiteralPath $compatibilityExtractRoot) {
            Remove-Item -LiteralPath $compatibilityExtractRoot -Recurse -Force
        }
        if (Test-Path -LiteralPath $compatibilitySelfTestRoot) {
            Remove-Item -LiteralPath $compatibilitySelfTestRoot -Recurse -Force
        }
    }
    Promote-SourceBaseline
    Write-Output "Installer and compatibility-package self-tests passed: $($Paths.InstallerExe)"
    Write-Output "Installer SHA-256: $installerHash"
    Write-Output "Compatibility ZIP SHA-256: $compatibilityHash"
}

switch ($Stage) {
    'Status' { Show-Status }
    'Queue' { Show-Queue }
    'Audit' {
        $audit = Invoke-SourceAudit
        $skillAliasAudit = Invoke-SkillAliasAudit
        $dictionary = $Paths.ArtifactDictionary
        if (-not (Test-Path -LiteralPath $dictionary -PathType Leaf)) { $dictionary = $Paths.DeployedDictionary }
        Assert-SkillAliasCoverage $skillAliasAudit $dictionary
        $runtimeNameAudit = Invoke-RuntimeNameAudit $dictionary
        Assert-RuntimeNameCoverage $runtimeNameAudit $dictionary
        $bilingualAudit = Invoke-BilingualCatalog $dictionary
        Assert-BilingualCatalog $bilingualAudit $dictionary
        $audit | Format-List
        $skillAliasAudit | Format-List
        $runtimeNameAudit | Format-List
        $bilingualAudit | Format-List
    }
    'Validate' {
        $dictionary = $Paths.ArtifactDictionary
        if (-not (Test-Path -LiteralPath $dictionary)) { $dictionary = $Paths.DeployedDictionary }
        Invoke-Validation $dictionary (-not $SkipTests)
    }
    'Build' { Invoke-Build ([bool]$Deploy) (-not $SkipTests) }
    'RecordLive' { Write-LiveVerification }
    'Package' { Invoke-Package $true }
    'All' {
        Show-Status
        Show-Queue
        $null = Invoke-SourceAudit
        Invoke-Build ([bool]$Deploy) (-not $SkipTests)
        Invoke-Package $false
        Show-Status
        Show-Queue
    }
}
