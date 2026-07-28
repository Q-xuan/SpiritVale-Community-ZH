using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace SpiritVale.RuntimeLocalization;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class RuntimeLocalizationPlugin : BasePlugin
{
    public const string PluginGuid = "local.spiritvale.runtime-localization";
    public const string PluginName = "SpiritVale Runtime Localization";
    public const string PluginVersion = "1.2.30";
    private Harmony _harmony;
    private readonly HashSet<MethodInfo> _patchedMethods = new HashSet<MethodInfo>();
    private bool _marketSearchPatched;
    private bool _marketSearchRetryRegistered;
    private AssemblyLoadEventHandler _marketSearchAssemblyLoadHandler;

    public override void Load()
    {
        TranslationCatalog catalog;
        string pluginDirectory;
        string untranslatedLogPath;
        try
        {
            pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? throw new InvalidOperationException("Could not resolve the plugin directory.");
            var tablePath = Path.Combine(pluginDirectory, "translations.tsv");
            untranslatedLogPath = Path.Combine(pluginDirectory, "untranslated-runtime.log");
            catalog = TranslationTable.Load(tablePath);
        }
        catch (Exception exception)
        {
            Log.LogWarning((object)$"Runtime localization disabled without blocking game startup: {exception}");
            return;
        }

        var diagnosticsEnabled = File.Exists(Path.Combine(
            pluginDirectory,
            RuntimeDiagnostics.MarkerFileName));
        TextTranslationPatches.Initialize(catalog, untranslatedLogPath, Log, diagnosticsEnabled);
        MarketSearchPatches.Initialize(catalog, Log);
        TmpFontFallbacks.Initialize(Log);
        var entityNameMode = BilingualDisplayConfiguration.ParseDisplayMode(
            Config.Bind(
                "Display",
                "EntityNameMode",
                nameof(DisplayMode.Chinese),
                "Chinese or Bilingual. Bilingual affects trusted entity detail titles only.").Value);
        var compactSurfaceMode = BilingualDisplayConfiguration.ParseCompactSurfaceMode(
            Config.Bind(
                "Display",
                "CompactSurfaceMode",
                nameof(CompactSurfaceMode.Chinese),
                "Chinese or EnglishToggle. Toggle mode affects trusted compact entity labels only.").Value);
        var temporaryEnglishKeyName = Config.Bind(
            "Display",
            "TemporaryEnglishKey",
            nameof(KeyCode.Tab),
            "Unity KeyCode that toggles English on trusted compact entity labels.").Value;
        var englishToggleKey = ParseTemporaryEnglishKey(temporaryEnglishKeyName);
        var bilingualCatalog = EntityDisplayCatalog.Empty;
        if (entityNameMode == DisplayMode.Bilingual ||
            compactSurfaceMode == CompactSurfaceMode.EnglishToggle)
        {
            var bilingualCatalogPath = Path.Combine(
                pluginDirectory,
                "bilingual-entity-catalog.tsv");
            if (!EntityDisplayCatalog.TryLoad(
                    bilingualCatalogPath,
                    out bilingualCatalog,
                    out var catalogError))
            {
                Log.LogWarning((object)
                    $"Bilingual display failed closed to Chinese: {catalogError}");
                entityNameMode = DisplayMode.Chinese;
                compactSurfaceMode = CompactSurfaceMode.Chinese;
            }
        }
        BilingualDisplayRuntime.Initialize(
            bilingualCatalog,
            entityNameMode,
            compactSurfaceMode,
            englishToggleKey,
            warning => Log.LogWarning((object)warning));
        RuntimeDiagnostics.Initialize(Log, diagnosticsEnabled);
        _harmony = new Harmony(PluginGuid);

        var patched = 0;
        patched += TryPatchFeature("UGUI Text.text", () =>
            PatchStringArgument(AccessTools.PropertySetter(typeof(Text), nameof(Text.text))));
        patched += TryPatchFeature("TextMeshPro TMP_Text.text", () =>
            PatchTmpStringArgument(AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text))));
        patched += TryPatchFeature("UI Toolkit TextElement.text", () =>
            PatchStringArgument(AccessTools.PropertySetter(typeof(TextElement), nameof(TextElement.text))));
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(TMP_Text))
                     .Where(method => method.Name == nameof(TMP_Text.SetText)))
        {
            var parameters = method.GetParameters();
            if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string))
            {
                patched += TryPatchFeature("TextMeshPro TMP_Text.SetText overload", () => PatchTmpStringArgument(method));
            }
        }

        patched += TryPatchFeature("UGUI Text.OnEnable", () => PatchTextOnEnable(
            AccessTools.DeclaredMethod(typeof(Text), "OnEnable"),
            nameof(TextTranslationPatches.TranslateCurrentUguiText)));
        foreach (var type in new[] { typeof(TextMeshProUGUI), typeof(TextMeshPro) })
        {
            patched += TryPatchFeature(type.FullName + ".OnEnable", () => PatchTextOnEnable(
                AccessTools.DeclaredMethod(type, "OnEnable"),
                nameof(TextTranslationPatches.TranslateCurrentTmpText)));
        }

        patched += TryPatchFeature("Loc dyn.scaling_per_stat formatter", TryPatchDynamicScalingFormat);
        patched += TryPatchFeature("Extensions equip description producer", TryPatchEquipDescriptionProducer);
        patched += TryPatchFeature("UIUnitStatus monster nameplate producer", TryPatchMonsterNameplateProducer);
        patched += TryPatchFeature("PlayerController market request bridge", TryPatchMarketSearch);
        if (entityNameMode == DisplayMode.Bilingual ||
            compactSurfaceMode == CompactSurfaceMode.EnglishToggle)
        {
            patched += TryPatchFeature(
                "trusted bilingual entity producers",
                TryPatchBilingualEntityProducers);
        }
        if (compactSurfaceMode == CompactSurfaceMode.EnglishToggle)
        {
            patched += TryPatchFeature(
                "bilingual display lifecycle poll",
                TryPatchBilingualKeyPoll);
        }
        if (diagnosticsEnabled)
        {
            patched += TryPatchFeature("runtime localization diagnostic probes", TryPatchRuntimeDiagnostics);
            Log.LogWarning((object)
                "Runtime diagnostics are explicitly enabled for this session; do not package this marker file.");
        }
        else
        {
            Log.LogInfo((object)"Runtime diagnostics are disabled.");
        }

        Log.LogInfo((object)
            $"Loaded {catalog.Translations.Count} translations and patched {patched} runtime method(s). " +
            $"Entity display={entityNameMode}, compact display={compactSurfaceMode}, " +
            $"entity catalog={bilingualCatalog.Count}.");
    }

    private KeyCode ParseTemporaryEnglishKey(string value)
    {
        if (Enum.TryParse(value, false, out KeyCode key) && key != KeyCode.None)
        {
            return key;
        }

        Log.LogWarning((object)
            $"Unknown TemporaryEnglishKey '{value}'; using {nameof(KeyCode.Tab)}.");
        return KeyCode.Tab;
    }

    private int TryPatchFeature(string feature, Func<int> patch)
    {
        try
        {
            var count = patch();
            if (count == 0) Log.LogWarning((object)$"Runtime localization skipped unavailable feature: {feature}.");
            return count;
        }
        catch (Exception exception)
        {
            Log.LogWarning((object)$"Runtime localization skipped failed feature '{feature}': {exception}");
            return 0;
        }
    }

    private int PatchStringArgument(MethodInfo method)
    {
        if (method == null || _patchedMethods.Contains(method))
        {
            return 0;
        }

        var prefix = AccessTools.Method(
            typeof(TextTranslationPatches),
            nameof(TextTranslationPatches.TranslateFirstArgument));
        _harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        _patchedMethods.Add(method);
        return 1;
    }

    private int PatchTmpStringArgument(MethodInfo method)
    {
        if (method == null || _patchedMethods.Contains(method))
        {
            return 0;
        }

        var prefix = AccessTools.Method(
            typeof(TextTranslationPatches),
            nameof(TextTranslationPatches.TranslateTmpFirstArgument));
        _harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        _patchedMethods.Add(method);
        return 1;
    }

    private int PatchTextOnEnable(MethodInfo method, string postfixName)
    {
        if (method == null || _patchedMethods.Contains(method))
        {
            return 0;
        }

        var postfix = AccessTools.Method(typeof(TextTranslationPatches), postfixName);
        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        _patchedMethods.Add(method);
        return 1;
    }

    private int PatchPostfix(MethodInfo method, Type patchType, string postfixName)
    {
        if (method == null || _patchedMethods.Contains(method))
        {
            return 0;
        }

        var postfix = AccessTools.Method(patchType, postfixName);
        if (postfix == null)
        {
            return 0;
        }
        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        _patchedMethods.Add(method);
        return 1;
    }

    private int TryPatchDynamicScalingFormat()
    {
        var argsType = typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>);
        return PatchPostfix(
            AccessTools.Method(typeof(Loc), "Format", new[] { typeof(string), typeof(string), argsType }),
            typeof(TextTranslationPatches),
            nameof(TextTranslationPatches.CanonicalizeDynamicScalingFormat));
    }

    private int TryPatchEquipDescriptionProducer()
    {
        return PatchPostfix(
            AccessTools.Method(
                typeof(Extensions),
                "ToDescription",
                new[] { typeof(EquipData), typeof(EquipConfig), typeof(bool), typeof(bool) }),
            typeof(TextTranslationPatches),
            nameof(TextTranslationPatches.CanonicalizeEquipDescription));
    }

    private int TryPatchMonsterNameplateProducer()
    {
        return PatchPostfix(
            AccessTools.Method(typeof(UIUnitStatus), "Draw", new[] { typeof(BaseUnitController) }),
            typeof(TextTranslationPatches),
            nameof(TextTranslationPatches.TranslateMonsterNameplate));
    }

    private int TryPatchBilingualEntityProducers()
    {
        var patched = 0;
        patched += PatchPostfix(
            AccessTools.Method(
                typeof(UIInventoryItem),
                "Draw",
                new[] { typeof(IInfoDrawable), typeof(bool) }),
            typeof(BilingualDisplayProducerPatches),
            nameof(BilingualDisplayProducerPatches.RegisterInventoryDrawable));
        patched += PatchPostfix(
            AccessTools.Method(
                typeof(UIInventoryItem),
                "Draw",
                new[] { typeof(MonsterData) }),
            typeof(BilingualDisplayProducerPatches),
            nameof(BilingualDisplayProducerPatches.RegisterInventoryMonster));
        patched += PatchPostfix(
            AccessTools.Method(
                typeof(UIInventoryItem),
                "Draw",
                new[] { typeof(SkillData) }),
            typeof(BilingualDisplayProducerPatches),
            nameof(BilingualDisplayProducerPatches.RegisterInventorySkill));
        patched += PatchPostfix(
            AccessTools.Method(
                typeof(UISkillsItem),
                "Draw",
                new[]
                {
                    typeof(SkillData),
                    typeof(Il2CppSystem.Action),
                    typeof(Il2CppSystem.Action)
                }),
            typeof(BilingualDisplayProducerPatches),
            nameof(BilingualDisplayProducerPatches.RegisterSkillsItem));
        patched += PatchPostfix(
            AccessTools.Method(
                typeof(UISkillButton),
                "Draw",
                new[] { typeof(int) }),
            typeof(BilingualDisplayProducerPatches),
            nameof(BilingualDisplayProducerPatches.RegisterSkillButton));
        patched += PatchPostfix(
            AccessTools.Method(
                typeof(UIWorldMapInfo),
                "Draw",
                new[] { typeof(MapConfig) }),
            typeof(BilingualDisplayProducerPatches),
            nameof(BilingualDisplayProducerPatches.RegisterWorldMapInfo));
        patched += PatchPostfix(
            AccessTools.Method(
                typeof(UIWorldMapItem),
                "Draw",
                new[] { typeof(Il2CppSystem.Action), typeof(Il2CppSystem.Action) }),
            typeof(BilingualDisplayProducerPatches),
            nameof(BilingualDisplayProducerPatches.RegisterWorldMapItem));
        return patched;
    }

    private int TryPatchBilingualKeyPoll()
    {
        return PatchPostfix(
            AccessTools.Method(typeof(UIManager), "LateUpdate", Type.EmptyTypes),
            typeof(BilingualDisplayRuntime),
            nameof(BilingualDisplayRuntime.RefreshEnglishToggle));
    }

    private int PatchMarketSearch(MethodInfo method)
    {
        if (method == null || _patchedMethods.Contains(method))
        {
            return 0;
        }

        var prefix = AccessTools.Method(
            typeof(MarketSearchPatches),
            nameof(MarketSearchPatches.BridgeVendorItemRequest));
        _harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        _patchedMethods.Add(method);
        return 1;
    }

    private int TryPatchMarketSearch()
    {
        if (_marketSearchPatched)
        {
            return 0;
        }
        if (!MarketSearchPatches.IsConfigured)
        {
            return 0;
        }

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));
        if (gameAssembly == null)
        {
            try
            {
                gameAssembly = Assembly.Load("Assembly-CSharp");
            }
            catch (Exception exception)
            {
                RegisterMarketSearchRetry(
                    "Assembly-CSharp is not available during plugin load: " + exception.GetType().Name);
                return 0;
            }
        }

        var playerControllerType = gameAssembly.GetType("PlayerController", throwOnError: false);
        if (playerControllerType == null)
        {
            Log.LogWarning((object)
                "Chinese market request bridge was not patched: Assembly-CSharp lacks PlayerController.");
            return 0;
        }

        var callbackType = typeof(Il2CppSystem.Action<
            Il2CppSystem.Collections.Generic.List<VendingManager.ItemData>>);
        var requestMethod = AccessTools.Method(
            playerControllerType,
            MarketSearchQueryBridge.SupportedPlayerRequestMethod,
            new[] { typeof(string), callbackType });
        if (requestMethod == null)
        {
            Log.LogWarning((object)
                "Chinese market request bridge was not patched: PlayerController request signature changed.");
            return 0;
        }

        if (requestMethod.ReturnType != typeof(void))
        {
            Log.LogWarning((object)
                "Chinese market request bridge was not patched: PlayerController request return type changed.");
            return 0;
        }

        var patched = PatchMarketSearch(requestMethod);
        _marketSearchPatched = _patchedMethods.Contains(requestMethod);
        if (_marketSearchPatched)
        {
            Log.LogInfo((object)
                "Chinese market request bridge patched only the PlayerController wire filter; callbacks remain game-owned.");
        }
        return patched;
    }

    private int TryPatchRuntimeDiagnostics()
    {
        if (!RuntimeDiagnostics.Enabled)
        {
            return 0;
        }

        var patched = 0;
        patched += PatchDiagnostic(
            AccessTools.Method(typeof(TMP_InputField), "SendOnValueChanged"),
            nameof(RuntimeDiagnostics.ObserveInputValueChanged),
            "TMP_InputField.SendOnValueChanged");
        patched += PatchDiagnostic(
            AccessTools.Method(typeof(TMP_InputField), "SendOnValueChangedAndUpdateLabel"),
            nameof(RuntimeDiagnostics.ObserveInputValueChangedAndUpdateLabel),
            "TMP_InputField.SendOnValueChangedAndUpdateLabel");
        patched += PatchDiagnostic(
            AccessTools.Method(typeof(TMP_InputField), "SendOnEndEdit"),
            nameof(RuntimeDiagnostics.ObserveInputEndEdit),
            "TMP_InputField.SendOnEndEdit");
        patched += PatchDiagnostic(
            AccessTools.Method(typeof(TMP_InputField), "SendOnSubmit"),
            nameof(RuntimeDiagnostics.ObserveInputSubmit),
            "TMP_InputField.SendOnSubmit");
        patched += PatchDiagnostic(
            AccessTools.Method(typeof(TMP_InputField), "LateUpdate"),
            nameof(RuntimeDiagnostics.ObserveInputLateUpdate),
            "TMP_InputField.LateUpdate");
        patched += PatchDiagnostic(
            AccessTools.Method(
                typeof(TMP_InputField),
                "OnUpdateSelected",
                new[] { typeof(UnityEngine.EventSystems.BaseEventData) }),
            nameof(RuntimeDiagnostics.ObserveInputUpdateSelected),
            "TMP_InputField.OnUpdateSelected");

        var tmpInternal = AccessTools.Method(
            typeof(TMP_Text),
            "SetTextInternal",
            new[] { typeof(string) });
        patched += PatchDiagnostic(tmpInternal, nameof(RuntimeDiagnostics.ObserveTmpInternalString),
            "TMP_Text.SetTextInternal");

        foreach (var method in AccessTools.GetDeclaredMethods(typeof(TMP_Text)))
        {
            if (method.Name == "SetCharArray")
            {
                patched += PatchDiagnostic(method, nameof(RuntimeDiagnostics.ObserveTmpCharArrayWrite),
                    "TMP_Text.SetCharArray");
                continue;
            }

            if (method.Name == nameof(TMP_Text.SetText) &&
                method.GetParameters().Length > 0 &&
                method.GetParameters()[0].ParameterType != typeof(string))
            {
                patched += PatchDiagnostic(method, nameof(RuntimeDiagnostics.ObserveTmpNonStringWrite),
                    "TMP_Text.SetText non-string overload");
            }
        }

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));
        if (gameAssembly == null)
        {
            return patched;
        }

        var vendingType = gameAssembly.GetType("UIVendingSearch", throwOnError: false);
        if (vendingType != null)
        {
            patched += PatchDiagnostic(
                AccessTools.Method(vendingType, "Search", new[] { typeof(string), typeof(bool) }),
                nameof(RuntimeDiagnostics.ObserveSearch),
                "UIVendingSearch.Search");
            patched += PatchDiagnostic(
                AccessTools.Method(vendingType, "LateUpdate"),
                nameof(RuntimeDiagnostics.ObserveSearchLateUpdate),
                "UIVendingSearch.LateUpdate");
            patched += PatchDiagnostic(
                AccessTools.Method(vendingType, "_Awake_b__7_0", new[] { typeof(string) }),
                nameof(RuntimeDiagnostics.ObserveInputCallback),
                "UIVendingSearch._Awake_b__7_0");
        }

        var inventoryType = gameAssembly.GetType("UIInventoryItem", throwOnError: false);
        if (inventoryType != null)
        {
            patched += PatchDiagnostic(
                AccessTools.Method(inventoryType, "DrawDescription2", new[] { typeof(string) }),
                nameof(RuntimeDiagnostics.ObserveDescriptionProducer),
                "UIInventoryItem.DrawDescription2");
        }

        var extensionsType = gameAssembly.GetType("Extensions", throwOnError: false);
        var equipDataType = gameAssembly.GetType("EquipData", throwOnError: false);
        var equipConfigType = gameAssembly.GetType("EquipConfig", throwOnError: false);
        if (extensionsType != null && equipDataType != null && equipConfigType != null)
        {
            patched += PatchDiagnostic(
                AccessTools.Method(
                    extensionsType,
                    "ToDescription",
                    new[] { equipDataType, equipConfigType, typeof(bool), typeof(bool) }),
                nameof(RuntimeDiagnostics.ObserveEquipDescription),
                "Extensions.ToDescription(EquipData)");
        }

        return patched;
    }

    private int PatchDiagnostic(MethodInfo method, string patchName, string feature)
    {
        if (method == null || _patchedMethods.Contains(method))
        {
            return 0;
        }

        var patch = AccessTools.Method(typeof(RuntimeDiagnostics), patchName);
        if (patch == null)
        {
            Log.LogWarning((object)("Runtime diagnostic patch method is unavailable: " + patchName));
            return 0;
        }

        try
        {
            _harmony.Patch(method, postfix: new HarmonyMethod(patch));
            _patchedMethods.Add(method);
            return 1;
        }
        catch (Exception exception)
        {
            Log.LogWarning((object)(
                "Runtime localization skipped diagnostic feature '" + feature + "': " + exception));
            return 0;
        }
    }

    private void RegisterMarketSearchRetry(string reason)
    {
        if (_marketSearchRetryRegistered)
        {
            return;
        }

        _marketSearchRetryRegistered = true;
        _marketSearchAssemblyLoadHandler = RetryMarketSearchPatchAfterAssemblyLoad;
        AppDomain.CurrentDomain.AssemblyLoad += _marketSearchAssemblyLoadHandler;
        Log.LogWarning((object)
            "Chinese market search bridge deferred until Assembly-CSharp loads: " + reason);
    }

    private void RetryMarketSearchPatchAfterAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        if (!string.Equals(args.LoadedAssembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))
        {
            return;
        }
        if (_marketSearchAssemblyLoadHandler != null)
        {
            AppDomain.CurrentDomain.AssemblyLoad -= _marketSearchAssemblyLoadHandler;
            _marketSearchAssemblyLoadHandler = null;
        }
        _marketSearchRetryRegistered = false;
        if (!_marketSearchPatched)
        {
            TryPatchFeature("deferred PlayerController market request bridge", TryPatchMarketSearch);
        }
    }
}

internal static class MarketSearchPatches
{
    private static MarketSearchQueryBridge _bridge;
    private static ManualLogSource _log;
    private static bool _translatedReported;
    private static bool _ambiguousReported;
    private static bool _failureReported;

    internal static bool IsConfigured => _bridge != null;

    internal static void Initialize(TranslationCatalog catalog, ManualLogSource log)
    {
        _log = log;
        _translatedReported = false;
        _ambiguousReported = false;
        _failureReported = false;
        if (catalog.MarketSearchTranslations.Count == 0)
        {
            _bridge = null;
            _log.LogWarning((object)
                "Chinese market request bridge is unavailable; market queries will use the original game filter.");
            return;
        }
        _bridge = new MarketSearchQueryBridge(
            catalog.ItemAffixTranslations,
            catalog.ItemBaseTranslations,
            catalog.MarketSearchTranslations,
            catalog.MarketSearchKeywordTranslations);
    }

    public static void BridgeVendorItemRequest(ref string __0)
    {
        if (_bridge == null || string.IsNullOrEmpty(__0) || !CjkText.ContainsCjk(__0))
        {
            return;
        }

        try
        {
            var outcome = _bridge.TryBridge(
                MarketSearchQueryBridge.SupportedPlayerType,
                MarketSearchQueryBridge.SupportedPlayerRequestMethod,
                __0,
                out var bridged);
            if (outcome == MarketSearchBridgeOutcome.Translated)
            {
                __0 = bridged;
                if (!_translatedReported)
                {
                    _translatedReported = true;
                    _log.LogInfo((object)
                        "Chinese market request bridge translated a CJK filter at the PlayerController wire boundary.");
                }
                return;
            }
            if (outcome == MarketSearchBridgeOutcome.Ambiguous && !_ambiguousReported)
            {
                _ambiguousReported = true;
                _log.LogWarning((object)
                    "Chinese market request was ambiguous and stayed on the original game filter.");
            }
        }
        catch (Exception exception)
        {
            FailOpen(exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static void FailOpen(string reason)
    {
        if (!_failureReported)
        {
            _failureReported = true;
            _log?.LogWarning((object)
                ("Chinese market request bridge failed open to the original game filter: " + reason));
        }
    }
}

internal static class TextTranslationPatches
{
    private const string DynamicScalingKey = "dyn.scaling_per_stat";
    private static RuntimeTextTranslator _translator = RuntimeTextTranslator.Empty;
    private static ManualLogSource _log;
    private static string _untranslatedLogPath;
    private static readonly HashSet<string> ReportedSources =
        new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> ReportedUntranslated =
        new HashSet<string>(StringComparer.Ordinal);
    private static readonly Regex RichTextTagPattern =
        new Regex("<[^>]+>", RegexOptions.CultureInvariant);
    private static readonly Regex EnglishWordPattern =
        new Regex("(?<![A-Za-z])[A-Za-z]{3,}(?![A-Za-z])", RegexOptions.CultureInvariant);
    private static readonly Regex ShortTemplatePattern =
        new Regex(
            @"(?:\bLv\.\d+\b|\b\d+h\s*\d+m\b|\b\d+(?:\.\d+)?s\b)",
            RegexOptions.CultureInvariant);
    private static bool _logFailureReported;
    private static bool _statScalingContextDiagnosticReported;
    private static bool _captureUntranslatedEnabled;

    internal static void Initialize(
        TranslationCatalog catalog,
        string untranslatedLogPath,
        ManualLogSource log,
        bool captureUntranslatedEnabled)
    {
        _translator = new RuntimeTextTranslator(
            catalog.Translations,
            catalog.ItemAffixes,
            catalog.ItemBaseNames);
        _untranslatedLogPath = untranslatedLogPath;
        _log = log;
        _captureUntranslatedEnabled = captureUntranslatedEnabled;
        if (!_captureUntranslatedEnabled)
        {
            return;
        }
        try
        {
            File.AppendAllText(
                _untranslatedLogPath,
                $"# Session {DateTime.Now:yyyy-MM-dd HH:mm:ss} - remaining visible English\n",
                new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            _logFailureReported = true;
            _log.LogWarning((object)$"Could not initialize untranslated text log: {exception.Message}");
        }
    }

    public static void TranslateFirstArgument(ref string __0)
    {
        if (_translator.TryTranslate(__0, out var translated))
        {
            Report(__0);
            __0 = translated;
        }

        CaptureUntranslated(__0, "setter");
    }

    public static void TranslateTmpFirstArgument(TMP_Text __instance, ref string __0)
    {
        if (BilingualDisplayRuntime.IsInternalWrite(__instance))
        {
            return;
        }

        BilingualDisplayRuntime.Unregister(__instance);
        var source = __0;
        if (!RuntimeDiagnostics.Enabled && !_translator.MayTranslate(source))
        {
            return;
        }
        RuntimeDiagnostics.ObserveTmpStringWrite(__instance, __0, "TMP string argument");
        var context = ResolveTmpContext(__instance, out var hierarchyPath);
        ReportStatScalingContextDiagnostic(__0, context, hierarchyPath);
        if (_translator.TryTranslate(__0, context, out var translated))
        {
            Report(__0);
            __0 = translated;
        }

        TmpFontFallbacks.Ensure(__instance, __0);
        if (_captureUntranslatedEnabled)
        {
            CaptureUntranslated(__0, "TMP-setter:" + context);
        }
        BilingualDisplayProducerPatches.RegisterTranslatedLocation(
            __instance,
            source,
            ref __0,
            context,
            hierarchyPath);
    }

    public static void TranslateCurrentUguiText(Text __instance)
    {
        var source = __instance.text;
        if (_translator.TryTranslate(source, __instance.gameObject.name, out var translated))
        {
            Report(source);
            __instance.text = translated;
            source = translated;
        }
        if (_captureUntranslatedEnabled)
        {
            CaptureUntranslated(source, "UGUI:" + __instance.gameObject.name);
        }
    }

    public static void TranslateCurrentTmpText(TMP_Text __instance)
    {
        var source = __instance.text;
        if (BilingualDisplayRuntime.IsRegisteredDisplayValue(__instance))
        {
            TmpFontFallbacks.Ensure(__instance, source);
            return;
        }

        BilingualDisplayRuntime.Unregister(__instance);
        var originalSource = source;
        if (!RuntimeDiagnostics.Enabled && !_translator.MayTranslate(source))
        {
            return;
        }
        RuntimeDiagnostics.ObserveTmpStringWrite(__instance, source, "TMP OnEnable/current");
        var context = ResolveTmpContext(__instance, out var hierarchyPath);
        ReportStatScalingContextDiagnostic(source, context, hierarchyPath);
        if (_translator.TryTranslate(source, context, out var translated))
        {
            Report(source);
            TmpFontFallbacks.Ensure(__instance, translated);
            __instance.text = translated;
            source = translated;
        }
        else
        {
            TmpFontFallbacks.Ensure(__instance, source);
        }
        if (_captureUntranslatedEnabled)
        {
            CaptureUntranslated(source, "TMP:" + context);
        }
        BilingualDisplayProducerPatches.RegisterTranslatedLocation(
            __instance,
            originalSource,
            source,
            context,
            hierarchyPath);
    }

    public static void CanonicalizeDynamicScalingFormat(string __0, ref string __result)
    {
        if (!string.Equals(__0, DynamicScalingKey, StringComparison.Ordinal))
        {
            return;
        }

        __result = RuntimeTextTranslator.CanonicalizePerTenStatScaling(__result);
    }

    public static void CanonicalizeEquipDescription(ref string __result)
    {
        __result = RuntimeTextTranslator.CanonicalizePerTenStatScaling(__result);
        RuntimeDiagnostics.ObserveEquipDescription(ref __result);
    }

    public static void TranslateMonsterNameplate(
        UIUnitStatus __instance,
        BaseUnitController __0)
    {
        TMP_Text name = null;
        try
        {
            name = __instance?.Name;
            var monster = __0?.TryCast<MonsterController>();
            if (__instance == null || monster == null || name == null)
            {
                BilingualDisplayRuntime.Unregister(name);
                return;
            }

            var source = name.text;
            if (_translator.TryTranslateMonsterNameplate(source, out var translated) &&
                !string.Equals(source, translated, StringComparison.Ordinal))
            {
                Report(source);
                TmpFontFallbacks.Ensure(name, translated);
                name.text = translated;
                source = translated;
            }
            CaptureUntranslated(source, "UIUnitStatus:MonsterNameplate");
            BilingualDisplayProducerPatches.RegisterMonsterNameplate(name, monster);
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(name);
        }
    }

    private static string ResolveTmpContext(TMP_Text text)
    {
        return ResolveTmpContext(text, out _);
    }

    private static string ResolveTmpContext(TMP_Text text, out string hierarchyPath)
    {
        var localName = string.Empty;
        hierarchyPath = string.Empty;
        try
        {
            if (text == null || text.gameObject == null)
            {
                return localName;
            }

            localName = text.gameObject.name ?? string.Empty;
            var inputField = text.GetComponentInParent<TMP_InputField>();
            var inputText = inputField?.textComponent;
            if (inputText != null && inputText.GetInstanceID() == text.GetInstanceID())
            {
                hierarchyPath = localName;
                return "UserInput:" + localName;
            }
            if (!TmpTextContextResolver.RequiresAncestor(localName))
            {
                hierarchyPath = localName;
                return localName;
            }

            var ancestorNames = new List<string>(TmpTextContextResolver.MaxAncestorDepth);
            var ancestor = text.transform.parent;
            for (var depth = 0;
                 ancestor != null && depth < TmpTextContextResolver.MaxAncestorDepth;
                 depth++)
            {
                ancestorNames.Add(ancestor.gameObject?.name ?? string.Empty);
                ancestor = ancestor.parent;
            }
            hierarchyPath = string.Join(
                " > ",
                new[] { localName }.Concat(ancestorNames));
            return TmpTextContextResolver.Resolve(localName, ancestorNames);
        }
        catch
        {
            hierarchyPath = localName;
            return localName;
        }
    }

    private static void ReportStatScalingContextDiagnostic(
        string source,
        string context,
        string hierarchyPath)
    {
        if (!RuntimeDiagnostics.Enabled ||
            _statScalingContextDiagnosticReported ||
            _log == null ||
            !RuntimeTextTranslator.IsSkillDamageScalingBlock(source))
        {
            return;
        }

        var evidence = ((context ?? string.Empty) + " " + (hierarchyPath ?? string.Empty))
            .ToLowerInvariant();
        foreach (var protectedMarker in new[]
        {
            "player", "character", "shop", "seller", "vending", "guild",
            "party", "team", "chat", "message"
        })
        {
            if (evidence.Contains(protectedMarker))
            {
                return;
            }
        }

        _statScalingContextDiagnosticReported = true;
        _log.LogInfo((object)
            $"P2 stat-scaling context diagnostic: resolved-context='{context}', " +
            $"hierarchy-path='{hierarchyPath}', matched-system-skill-damage-block");
    }

    private static void Report(string source)
    {
        if (ReportedSources.Count < 20 && ReportedSources.Add(source))
        {
            _log.LogInfo((object)$"Localized visible text: {source}");
        }
    }

    private static void CaptureUntranslated(string source, string context)
    {
        if (!_captureUntranslatedEnabled ||
            RuntimeTextTranslator.ShouldSuppressUntranslatedCapture(context, source) ||
            _logFailureReported || string.IsNullOrWhiteSpace(source) || source.Length > 500 ||
            ReportedUntranslated.Count >= 5000)
        {
            return;
        }

        var visible = RichTextTagPattern.Replace(source, string.Empty).Trim();
        var reportKey = context + "\u001f" + visible;
        if ((!EnglishWordPattern.IsMatch(visible) && !ShortTemplatePattern.IsMatch(visible)) ||
            !ReportedUntranslated.Add(reportKey))
        {
            return;
        }

        var escaped = visible
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
        try
        {
            File.AppendAllText(
                _untranslatedLogPath,
                $"{context}\t{escaped}\n",
                new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            _logFailureReported = true;
            _log.LogWarning((object)$"Could not write untranslated text log: {exception.Message}");
        }
    }

}

internal static class TmpFontFallbacks
{
    private const int MaximumCachedFonts = 128;
    private const int MaximumCachedCjkCharactersPerFont = 8192;
    private static readonly HashSet<int> ReportedFontPairs = new HashSet<int>();
    private static readonly HashSet<int> ReportedMissingFonts = new HashSet<int>();
    private static readonly Dictionary<int, HashSet<char>> SupportedCjkCharactersByFont =
        new Dictionary<int, HashSet<char>>();
    private static ManualLogSource _log;
    private static TMP_FontAsset _cachedFallback;
    private static UnityEngine.Font _systemFont;
    private static TMP_FontAsset _systemFallback;

    internal static void Initialize(ManualLogSource log)
    {
        _log = log;
    }

    internal static void Ensure(TMP_Text text, string value)
    {
        if (text == null || string.IsNullOrEmpty(value) || !CjkText.ContainsCjk(value))
        {
            return;
        }

        try
        {
            var current = text.font;
            if (current == null || Supports(current, value, false))
            {
                return;
            }
            if (Supports(current, value, true))
            {
                return;
            }

            var fallback = FindFallback(current, value);
            if (fallback == null)
            {
                var currentId = current.GetInstanceID();
                if (ReportedMissingFonts.Add(currentId))
                {
                    _log.LogWarning((object)$"No loaded TMP CJK fallback was found for font '{current.name}'.");
                }
                return;
            }

            var fallbacks = current.fallbackFontAssetTable;
            if (fallbacks == null)
            {
                fallbacks = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
                current.fallbackFontAssetTable = fallbacks;
            }
            if (!fallbacks.Contains(fallback))
            {
                fallbacks.Add(fallback);
                current.ClearFallbackCharacterTable();
            }
            RememberSupported(current, value);
            var pairId = unchecked((current.GetInstanceID() * 397) ^ fallback.GetInstanceID());
            if (ReportedFontPairs.Count < 32 && ReportedFontPairs.Add(pairId))
            {
                _log.LogInfo((object)$"Added TMP CJK fallback '{fallback.name}' to '{current.name}'.");
            }
        }
        catch (Exception exception)
        {
            var fontId = text.font == null ? 0 : text.font.GetInstanceID();
            if (ReportedMissingFonts.Add(fontId))
            {
                _log.LogWarning((object)$"Could not configure TMP CJK fallback: {exception.Message}");
            }
        }
    }

    private static TMP_FontAsset FindFallback(TMP_FontAsset current, string value)
    {
        if (_cachedFallback != null &&
            _cachedFallback.GetInstanceID() != current.GetInstanceID() &&
            Supports(_cachedFallback, value, true))
        {
            return _cachedFallback;
        }

        foreach (var candidate in UnityEngine.Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (candidate == null || candidate.GetInstanceID() == current.GetInstanceID())
            {
                continue;
            }
            if (Supports(candidate, value, true))
            {
                _cachedFallback = candidate;
                return candidate;
            }
        }
        return CreateSystemFallback(value);
    }

    private static TMP_FontAsset CreateSystemFallback(string value)
    {
        if (_systemFallback != null && Supports(_systemFallback, value, true))
        {
            return _systemFallback;
        }

        foreach (var family in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" })
        {
            var sourceFont = UnityEngine.Font.CreateDynamicFontFromOSFont(family, 32);
            if (sourceFont == null)
            {
                continue;
            }
            var candidate = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (candidate == null || !Supports(candidate, value, true))
            {
                continue;
            }

            candidate.name = "SpiritVale Chinese Runtime Fallback";
            candidate.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            candidate.isMultiAtlasTexturesEnabled = true;
            UnityEngine.Object.DontDestroyOnLoad(sourceFont);
            UnityEngine.Object.DontDestroyOnLoad(candidate);
            _systemFont = sourceFont;
            _systemFallback = candidate;
            _log.LogInfo((object)$"Created in-memory TMP CJK fallback from '{family}'.");
            return candidate;
        }
        return null;
    }

    private static bool Supports(TMP_FontAsset font, string value, bool tryAddCharacter)
    {
        HashSet<char> supportedCharacters = null;
        var fontId = font.GetInstanceID();
        if (!SupportedCjkCharactersByFont.TryGetValue(fontId, out supportedCharacters) &&
            SupportedCjkCharactersByFont.Count < MaximumCachedFonts)
        {
            supportedCharacters = new HashSet<char>();
            SupportedCjkCharactersByFont.Add(fontId, supportedCharacters);
        }

        foreach (var character in value)
        {
            if (!CjkText.IsCjk(character) ||
                (supportedCharacters != null && supportedCharacters.Contains(character)))
            {
                continue;
            }
            if (!font.HasCharacter(character, true, tryAddCharacter))
            {
                return false;
            }
            if (supportedCharacters != null &&
                supportedCharacters.Count < MaximumCachedCjkCharactersPerFont)
            {
                supportedCharacters.Add(character);
            }
        }
        return true;
    }

    private static void RememberSupported(TMP_FontAsset font, string value)
    {
        var fontId = font.GetInstanceID();
        if (!SupportedCjkCharactersByFont.TryGetValue(fontId, out var supportedCharacters))
        {
            if (SupportedCjkCharactersByFont.Count >= MaximumCachedFonts)
            {
                return;
            }
            supportedCharacters = new HashSet<char>();
            SupportedCjkCharactersByFont.Add(fontId, supportedCharacters);
        }

        foreach (var character in value)
        {
            if (CjkText.IsCjk(character) &&
                supportedCharacters.Count < MaximumCachedCjkCharactersPerFont)
            {
                supportedCharacters.Add(character);
            }
        }
    }
}

internal sealed class TranslationCatalog
{
    internal TranslationCatalog(
        IReadOnlyDictionary<string, string> translations,
        IReadOnlyCollection<string> itemAffixes,
        IReadOnlyCollection<string> itemBaseNames,
        IReadOnlyCollection<string> marketSearchNames,
        IReadOnlyCollection<KeyValuePair<string, string>> marketSearchKeywords,
        IReadOnlyCollection<MarketSearchCatalogEntry> marketSearchEntries)
    {
        Translations = translations;
        ItemAffixes = itemAffixes;
        ItemBaseNames = itemBaseNames;
        ItemAffixTranslations = itemAffixes
            .Where(translations.ContainsKey)
            .ToDictionary(source => source, source => translations[source], StringComparer.Ordinal);
        ItemBaseTranslations = itemBaseNames
            .Where(translations.ContainsKey)
            .ToDictionary(source => source, source => translations[source], StringComparer.Ordinal);
        var effectiveMarketNames = marketSearchNames.Count == 0
            ? itemBaseNames
            : marketSearchNames;
        MarketSearchNames = effectiveMarketNames;
        MarketSearchTranslations = effectiveMarketNames
            .Where(translations.ContainsKey)
            .ToDictionary(source => source, source => translations[source], StringComparer.Ordinal);
        MarketSearchKeywordTranslations = marketSearchKeywords;
        MarketSearchEntries = marketSearchEntries;
    }

    internal IReadOnlyDictionary<string, string> Translations { get; }
    internal IReadOnlyCollection<string> ItemAffixes { get; }
    internal IReadOnlyCollection<string> ItemBaseNames { get; }
    internal IReadOnlyCollection<string> MarketSearchNames { get; }
    internal IReadOnlyDictionary<string, string> ItemAffixTranslations { get; }
    internal IReadOnlyDictionary<string, string> ItemBaseTranslations { get; }
    internal IReadOnlyDictionary<string, string> MarketSearchTranslations { get; }
    internal IReadOnlyCollection<KeyValuePair<string, string>> MarketSearchKeywordTranslations { get; }
    internal IReadOnlyCollection<MarketSearchCatalogEntry> MarketSearchEntries { get; }
}

internal static class TranslationTable
{
    public static TranslationCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Runtime translation table was not found.", path);
        }

        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        var itemAffixes = new HashSet<string>(StringComparer.Ordinal);
        var itemBaseNames = new HashSet<string>(StringComparer.Ordinal);
        var marketSearchNames = new HashSet<string>(StringComparer.Ordinal);
        var marketSearchKeywords = new List<KeyValuePair<string, string>>();
        var marketSearchKeywordRows = new HashSet<string>(StringComparer.Ordinal);
        var marketSearchEntryRows = new List<string[]>();
        var marketSearchEntryKeys = new HashSet<string>(StringComparer.Ordinal);
        var marketSearchAliases = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var rawLine in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }
            if (rawLine.StartsWith("#market-search-name\t", StringComparison.Ordinal))
            {
                var parts = rawLine.Split(new[] { '\t' }, 3);
                if (parts.Length != 3 || string.IsNullOrEmpty(parts[1]) || string.IsNullOrEmpty(parts[2]))
                {
                    throw new InvalidDataException($"Invalid market-search-name line: {rawLine}");
                }
                marketSearchNames.Add(parts[2]);
                continue;
            }
            if (rawLine.StartsWith("#market-search-entry\t", StringComparison.Ordinal))
            {
                var parts = rawLine.Split(new[] { '\t' }, 5);
                if (parts.Length != 5 || parts.Skip(1).Any(string.IsNullOrEmpty))
                {
                    throw new InvalidDataException($"Invalid market-search-entry line: {rawLine}");
                }
                var entryKey = string.Join("\0", parts.Skip(1));
                if (!marketSearchEntryKeys.Add(entryKey))
                {
                    throw new InvalidDataException($"Duplicate market-search-entry line: {rawLine}");
                }
                marketSearchEntryRows.Add(parts);
                continue;
            }
            if (rawLine.StartsWith("#market-search-alias\t", StringComparison.Ordinal))
            {
                var parts = rawLine.Split(new[] { '\t' }, 4);
                if (parts.Length != 4 || parts.Skip(1).Any(string.IsNullOrEmpty))
                {
                    throw new InvalidDataException($"Invalid market-search-alias line: {rawLine}");
                }
                var identityKey = parts[1] + "\0" + parts[2];
                if (!marketSearchAliases.TryGetValue(identityKey, out var aliases))
                {
                    aliases = new HashSet<string>(StringComparer.Ordinal);
                    marketSearchAliases.Add(identityKey, aliases);
                }
                aliases.Add(parts[3]);
                continue;
            }
            if (rawLine.StartsWith("#market-search-keyword\t", StringComparison.Ordinal))
            {
                var parts = rawLine.Split(new[] { '\t' }, 3);
                if (parts.Length != 3 || string.IsNullOrEmpty(parts[1]) || string.IsNullOrEmpty(parts[2]))
                {
                    throw new InvalidDataException($"Invalid market-search-keyword line: {rawLine}");
                }
                var rowKey = parts[1] + "\0" + parts[2];
                if (!marketSearchKeywordRows.Add(rowKey))
                {
                    throw new InvalidDataException($"Duplicate market-search-keyword line: {rawLine}");
                }
                marketSearchKeywords.Add(new KeyValuePair<string, string>(parts[1], parts[2]));
                continue;
            }
            if (rawLine.StartsWith("#item-affix\t", StringComparison.Ordinal))
            {
                itemAffixes.Add(rawLine.Substring("#item-affix\t".Length));
                continue;
            }
            if (rawLine.StartsWith("#item-base\t", StringComparison.Ordinal))
            {
                itemBaseNames.Add(rawLine.Substring("#item-base\t".Length));
                continue;
            }
            if (rawLine[0] == '#')
            {
                continue;
            }

            var separator = rawLine.IndexOf('\t');
            if (separator <= 0 || separator == rawLine.Length - 1)
            {
                throw new InvalidDataException($"Invalid translation line: {rawLine}");
            }

            var source = rawLine.Substring(0, separator);
            var target = rawLine.Substring(separator + 1);
            if (!translations.TryAdd(source, target))
            {
                throw new InvalidDataException($"Duplicate translation source: {source}");
            }
        }

        var marketSearchEntries = marketSearchEntryRows
            .Select(parts =>
            {
                var identityKey = parts[1] + "\0" + parts[2];
                marketSearchAliases.TryGetValue(identityKey, out var aliases);
                return new MarketSearchCatalogEntry(
                    parts[1],
                    parts[2],
                    parts[3],
                    parts[4],
                    aliases ?? new HashSet<string>(StringComparer.Ordinal));
            })
            .ToArray();
        var knownMarketIdentities = new HashSet<string>(
            marketSearchEntries.Select(entry =>
                entry.Identity.ItemType + "\0" + entry.Identity.ItemId),
            StringComparer.Ordinal);
        var orphanAliases = marketSearchAliases.Keys
            .Where(key => !knownMarketIdentities.Contains(key))
            .ToArray();
        if (orphanAliases.Length != 0)
        {
            throw new InvalidDataException(
                "Runtime translation table has market aliases without canonical entries.");
        }

        return new TranslationCatalog(
            translations,
            itemAffixes,
            itemBaseNames,
            marketSearchNames,
            marketSearchKeywords,
            marketSearchEntries);
    }
}
