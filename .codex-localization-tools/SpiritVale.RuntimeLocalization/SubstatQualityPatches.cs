using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SpiritVale.RuntimeLocalization;

internal static class InventoryDisplayPatches
{
    public static void RegisterInventoryDrawable(
        UIInventoryItem __instance,
        IInfoDrawable __0)
    {
        if (BilingualDisplayRuntime.Enabled)
        {
            BilingualDisplayProducerPatches.RegisterInventoryDrawable(__instance, __0);
        }
        SubstatQualityPatches.ApplyInventoryMarker(__instance, __0);
    }
}

internal static class EquipDescriptionPatches
{
    public static void CanonicalizeAndAppendSummary(
        EquipData __0,
        ref string __result)
    {
        TextTranslationPatches.CanonicalizeEquipDescription(ref __result);
        SubstatQualityPatches.AppendEquipSummary(__0, ref __result);
    }
}

internal static class SubstatQualityPatches
{
    public static void ApplyInventoryMarker(
        UIInventoryItem __instance,
        IInfoDrawable __0)
    {
        if (SubstatQualityHud.ExternalHudLoaded)
        {
            return;
        }

        var text = __instance?.Name;
        if (text == null)
        {
            return;
        }

        try
        {
            var tier = SubstatQualityTier.None;
            if (SubstatQualityHud.InventoryMarkersEnabled && __0 != null)
            {
                var equip = __0.TryCast<EquipData>();
                if (equip != null && SubstatQualityHud.TryEvaluate(equip, out var equipQuality))
                {
                    tier = SubstatQualityHud.Classify(equipQuality);
                }
                else
                {
                    var artifact = __0.TryCast<ArtifactData>();
                    if (artifact != null &&
                        SubstatQualityHud.TryEvaluate(artifact, out var artifactQuality))
                    {
                        tier = SubstatQualityHud.Classify(artifactQuality);
                    }
                }
            }

            InventoryNameDecoration.Apply(text, tier, GetColor(tier));
        }
        catch (Exception exception)
        {
            InventoryNameDecoration.Apply(text, SubstatQualityTier.None, Color.white);
            SubstatQualityHud.ReportDisplayFailure("背包标记", exception);
        }
    }

    public static void AppendEquipSummary(EquipData __0, ref string __result)
    {
        if (!SubstatQualityHud.TooltipSummaryEnabled ||
            !SubstatQualityHud.TryEvaluate(__0, out var quality))
        {
            return;
        }

        AppendSummary(quality, ref __result);
    }

    public static void AppendArtifactSummary(ArtifactData __0, ref string __result)
    {
        if (!SubstatQualityHud.TooltipSummaryEnabled ||
            !SubstatQualityHud.TryEvaluate(__0, out var quality))
        {
            return;
        }

        AppendSummary(quality, ref __result);
    }

    private static void AppendSummary(SubstatQuality quality, ref string result)
    {
        try
        {
            var tier = SubstatQualityHud.Classify(quality);
            var marker = GetMarker(tier);
            var label = GetLabel(tier);
            var text = string.IsNullOrEmpty(marker) ? $"词条 {label}" : $"{marker} {label}";
            if (quality.DisplayedMaximumValid)
            {
                text += $" · 满值 {quality.DisplayedMaximumCount}/{quality.SubstatCount}";
            }
            text += $" · 内部品质 {(int)Math.Round(quality.RollAverage * 100f)}%";

            result = $"<color={ToHex(GetColor(tier))}>{text}</color>\n" + (result ?? string.Empty);
        }
        catch (Exception exception)
        {
            SubstatQualityHud.ReportDisplayFailure("提示框总评", exception);
        }
    }

    private static string GetMarker(SubstatQualityTier tier)
    {
        return tier switch
        {
            SubstatQualityTier.ThreeStars => "★★★",
            SubstatQualityTier.TwoStars => "★★",
            SubstatQualityTier.OneStar => "★",
            _ => null
        };
    }

    private static string GetLabel(SubstatQualityTier tier)
    {
        return tier switch
        {
            SubstatQualityTier.ThreeStars => "满词条",
            SubstatQualityTier.TwoStars => "优秀",
            SubstatQualityTier.OneStar => "良好",
            _ => "普通"
        };
    }

    private static Color GetColor(SubstatQualityTier tier)
    {
        return tier switch
        {
            SubstatQualityTier.ThreeStars => new Color(1f, 0.84f, 0f),
            SubstatQualityTier.TwoStars => new Color(1f, 0.55f, 0f),
            SubstatQualityTier.OneStar => new Color(0.78f, 0.49f, 1f),
            _ => new Color(0.6f, 0.63f, 0.65f)
        };
    }

    private static string ToHex(Color color)
    {
        return $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
    }
}

internal static class InventoryNameDecoration
{
    private static readonly string[] Prefixes = { "★★★ ", "★★ ", "★ " };
    private static readonly Dictionary<int, ColorRecord> Colors =
        new Dictionary<int, ColorRecord>();

    internal static void Apply(
        TMP_Text text,
        SubstatQualityTier tier,
        Color color)
    {
        if (text == null)
        {
            return;
        }

        var marker = tier switch
        {
            SubstatQualityTier.ThreeStars => Prefixes[0],
            SubstatQualityTier.TwoStars => Prefixes[1],
            SubstatQualityTier.OneStar => Prefixes[2],
            _ => string.Empty
        };
        var desired = marker + InventoryNameMarkerText.Strip(text.text);
        BilingualDisplayRuntime.TryWriteOwnedText(text, desired);

        var instanceId = text.GetInstanceID();
        var currentColor = text.color;
        var originalColor = currentColor;
        if (Colors.TryGetValue(instanceId, out var record) && Same(currentColor, record.Applied))
        {
            originalColor = record.Original;
        }

        if (tier == SubstatQualityTier.None)
        {
            text.color = originalColor;
            Colors.Remove(instanceId);
            return;
        }

        text.color = color;
        Colors[instanceId] = new ColorRecord(originalColor, color);
    }

    private static bool Same(Color left, Color right)
    {
        const float epsilon = 0.004f;
        return Math.Abs(left.r - right.r) < epsilon &&
            Math.Abs(left.g - right.g) < epsilon &&
            Math.Abs(left.b - right.b) < epsilon &&
            Math.Abs(left.a - right.a) < epsilon;
    }

    private readonly struct ColorRecord
    {
        internal ColorRecord(Color original, Color applied)
        {
            Original = original;
            Applied = applied;
        }

        internal Color Original { get; }
        internal Color Applied { get; }
    }
}
