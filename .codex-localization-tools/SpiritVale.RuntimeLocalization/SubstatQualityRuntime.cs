using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace SpiritVale.RuntimeLocalization;

internal static class SubstatQualityHud
{
    private const int CacheCapacity = 4096;
    private static readonly Dictionary<IntPtr, CachedQuality> EquipCache =
        new Dictionary<IntPtr, CachedQuality>();
    private static readonly Dictionary<IntPtr, CachedQuality> ArtifactCache =
        new Dictionary<IntPtr, CachedQuality>();

    private static ConfigEntry<bool> _inventoryMarkers;
    private static ConfigEntry<bool> _tooltipSummary;
    private static ConfigEntry<bool> _useDisplayedMaximum;
    private static ConfigEntry<float> _oneStarThreshold;
    private static ConfigEntry<float> _twoStarThreshold;
    private static ConfigEntry<float> _threeStarThreshold;
    private static ManualLogSource _log;
    private static bool _externalHudLoaded;
    private static bool _evaluationFailureReported;
    private static bool _displayFailureReported;

    internal static bool InventoryMarkersEnabled =>
        !_externalHudLoaded && _inventoryMarkers?.Value == true;

    internal static bool TooltipSummaryEnabled =>
        !_externalHudLoaded && _tooltipSummary?.Value == true;

    internal static bool ExternalHudLoaded => _externalHudLoaded;

    internal static void Initialize(ConfigFile config, ManualLogSource log)
    {
        _log = log;
        _evaluationFailureReported = false;
        _displayFailureReported = false;
        EquipCache.Clear();
        ArtifactCache.Clear();

        _inventoryMarkers = config.Bind(
            "词条品质 HUD",
            "背包星级标记",
            true,
            "在装备和神器名称前显示星级，并用颜色突出高品质词条。");
        _tooltipSummary = config.Bind(
            "词条品质 HUD",
            "提示框品质总评",
            true,
            "在装备和神器说明顶部显示满值数量与内部品质百分比。");
        _useDisplayedMaximum = config.Bind(
            "词条品质 HUD",
            "按显示满值分级",
            true,
            "true 按实际显示值达到上限的比例分级；false 按内部 Roll 百分比平均值分级。");
        _threeStarThreshold = config.Bind(
            "词条品质 HUD",
            "三星阈值",
            1f,
            "达到该比例时显示三星。默认 1.00 表示全部词条达到显示上限。");
        _twoStarThreshold = config.Bind(
            "词条品质 HUD",
            "二星阈值",
            0.75f,
            "达到该比例时显示二星。");
        _oneStarThreshold = config.Bind(
            "词条品质 HUD",
            "一星阈值",
            0.5f,
            "达到该比例时显示一星；低于该值不标记。");

        _externalHudLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            string.Equals(
                assembly.GetName().Name,
                "SpiritValeSubstatHUD",
                StringComparison.OrdinalIgnoreCase));
        if (_externalHudLoaded)
        {
            _log.LogWarning((object)
                "检测到独立的 SpiritVale Substat HUD；内置词条品质 HUD 已让位，避免重复标记和重复总评。");
            return;
        }

        _log.LogInfo((object)
            $"词条品质 HUD 已初始化：背包标记={InventoryMarkersEnabled}，提示框总评={TooltipSummaryEnabled}，" +
            $"分级依据={(_useDisplayedMaximum.Value ? "显示满值" : "内部 Roll")}。");
    }

    internal static SubstatQualityTier Classify(SubstatQuality quality)
    {
        return SubstatQualityCalculator.Classify(
            quality.GetScore(_useDisplayedMaximum?.Value != false),
            _oneStarThreshold?.Value ?? 0.5f,
            _twoStarThreshold?.Value ?? 0.75f,
            _threeStarThreshold?.Value ?? 1f);
    }

    internal static void ReportDisplayFailure(string operation, Exception exception)
    {
        if (_displayFailureReported)
        {
            return;
        }
        _displayFailureReported = true;
        _log?.LogWarning((object)$"词条品质 HUD {operation}失败，已保持原始显示：{exception.Message}");
    }

    internal static bool TryEvaluate(EquipData data, out SubstatQuality quality)
    {
        quality = default;
        if (data == null)
        {
            return false;
        }

        try
        {
            var substats = data.Substats;
            if (substats == null || substats.Count == 0)
            {
                return false;
            }

            var useDisplayedMaximum = _useDisplayedMaximum?.Value != false;
            var fingerprint = Fingerprint(data.Id, substats);
            if (TryReadCache(EquipCache, data.Pointer, fingerprint, useDisplayedMaximum, out quality))
            {
                return true;
            }

            EquipSubstatRuntime config = null;
            Il2CppSystem.Collections.Generic.List<StatValue> actual = null;
            if (useDisplayedMaximum)
            {
                var equip = App.ServerRuntime?.GetEquip(data.Id);
                config = equip == null ? null : Formula.GetSubstatConfig(equip);
                actual = Formula.GetSubstats(data);
            }

            if (!TryEvaluateCore(substats, config, actual, out quality))
            {
                return false;
            }
            StoreCache(EquipCache, data.Pointer, fingerprint, useDisplayedMaximum, quality);
            return true;
        }
        catch (Exception exception)
        {
            ReportEvaluationFailure("装备", exception);
            return false;
        }
    }

    internal static bool TryEvaluate(ArtifactData data, out SubstatQuality quality)
    {
        quality = default;
        if (data == null)
        {
            return false;
        }

        try
        {
            var substats = data.Substats;
            if (substats == null || substats.Count == 0)
            {
                return false;
            }

            var useDisplayedMaximum = _useDisplayedMaximum?.Value != false;
            var fingerprint = Fingerprint(data.Id, substats);
            if (TryReadCache(ArtifactCache, data.Pointer, fingerprint, useDisplayedMaximum, out quality))
            {
                return true;
            }

            var config = useDisplayedMaximum ? Formula.GetArtifactSubstatConfig() : null;
            var actual = useDisplayedMaximum ? Formula.GetSubstats(data) : null;
            if (!TryEvaluateCore(substats, config, actual, out quality))
            {
                return false;
            }
            StoreCache(ArtifactCache, data.Pointer, fingerprint, useDisplayedMaximum, quality);
            return true;
        }
        catch (Exception exception)
        {
            ReportEvaluationFailure("神器", exception);
            return false;
        }
    }

    private static bool TryEvaluateCore(
        Il2CppSystem.Collections.Generic.List<StatData> substats,
        EquipSubstatRuntime config,
        Il2CppSystem.Collections.Generic.List<StatValue> actual,
        out SubstatQuality quality)
    {
        var normalizedRollSum = 0f;
        var substatCount = 0;
        var displayedMaximumCount = 0;
        var displayedComparedCount = 0;
        for (var index = 0; index < substats.Count; index++)
        {
            var substat = substats[index];
            if (substat == null)
            {
                continue;
            }

            normalizedRollSum += SubstatQualityCalculator.NormalizeRoll(substat.Value);
            substatCount++;
            if (config == null || actual == null ||
                !Formula.GetSubstatRange(substat.Type, config, out var minimum, out var maximum) ||
                !TryGetActualValue(actual, substat.Type, out var value))
            {
                continue;
            }

            displayedComparedCount++;
            if (SubstatQualityCalculator.IsDisplayedMaximum(value, minimum, maximum))
            {
                displayedMaximumCount++;
            }
        }

        return SubstatQualityCalculator.TryCreate(
            normalizedRollSum,
            substatCount,
            displayedMaximumCount,
            displayedComparedCount,
            out quality);
    }

    private static bool TryGetActualValue(
        Il2CppSystem.Collections.Generic.List<StatValue> values,
        StatType type,
        out float value)
    {
        value = 0f;
        for (var index = 0; index < values.Count; index++)
        {
            var candidate = values[index];
            if (candidate == null || candidate.Type != type || candidate.Value == null)
            {
                continue;
            }

            value = candidate.Value.Value;
            return true;
        }
        return false;
    }

    private static int Fingerprint(
        string itemId,
        Il2CppSystem.Collections.Generic.List<StatData> substats)
    {
        unchecked
        {
            var hash = (17 * 31) + (itemId?.GetHashCode() ?? 0);
            for (var index = 0; index < substats.Count; index++)
            {
                var substat = substats[index];
                hash = (hash * 31) + (substat?.Type.GetHashCode() ?? 0);
                hash = (hash * 31) + (substat?.Value.GetHashCode() ?? 0);
            }
            return hash;
        }
    }

    private static bool TryReadCache(
        Dictionary<IntPtr, CachedQuality> cache,
        IntPtr pointer,
        int fingerprint,
        bool useDisplayedMaximum,
        out SubstatQuality quality)
    {
        if (pointer != IntPtr.Zero && cache.TryGetValue(pointer, out var cached) &&
            cached.Fingerprint == fingerprint &&
            cached.UseDisplayedMaximum == useDisplayedMaximum)
        {
            quality = cached.Quality;
            return true;
        }

        quality = default;
        return false;
    }

    private static void StoreCache(
        Dictionary<IntPtr, CachedQuality> cache,
        IntPtr pointer,
        int fingerprint,
        bool useDisplayedMaximum,
        SubstatQuality quality)
    {
        if (pointer == IntPtr.Zero)
        {
            return;
        }
        if (cache.Count >= CacheCapacity && !cache.ContainsKey(pointer))
        {
            cache.Clear();
        }
        cache[pointer] = new CachedQuality(fingerprint, useDisplayedMaximum, quality);
    }

    private static void ReportEvaluationFailure(string itemKind, Exception exception)
    {
        if (_evaluationFailureReported)
        {
            return;
        }
        _evaluationFailureReported = true;
        _log?.LogWarning((object)$"词条品质 HUD 评估{itemKind}失败，已保持原始显示：{exception.Message}");
    }

    private readonly struct CachedQuality
    {
        internal CachedQuality(int fingerprint, bool useDisplayedMaximum, SubstatQuality quality)
        {
            Fingerprint = fingerprint;
            UseDisplayedMaximum = useDisplayedMaximum;
            Quality = quality;
        }

        internal int Fingerprint { get; }
        internal bool UseDisplayedMaximum { get; }
        internal SubstatQuality Quality { get; }
    }
}
