namespace SpiritVale.RuntimeLocalization;

internal enum SubstatQualityTier
{
    None,
    OneStar,
    TwoStars,
    ThreeStars
}

internal readonly struct SubstatQuality
{
    internal SubstatQuality(
        float rollAverage,
        int displayedMaximumCount,
        int substatCount,
        bool displayedMaximumValid)
    {
        RollAverage = rollAverage;
        DisplayedMaximumCount = displayedMaximumCount;
        SubstatCount = substatCount;
        DisplayedMaximumValid = displayedMaximumValid;
    }

    internal float RollAverage { get; }
    internal int DisplayedMaximumCount { get; }
    internal int SubstatCount { get; }
    internal bool DisplayedMaximumValid { get; }

    internal float GetScore(bool useDisplayedMaximum)
    {
        if (useDisplayedMaximum && DisplayedMaximumValid && SubstatCount > 0)
        {
            return (float)DisplayedMaximumCount / SubstatCount;
        }

        return RollAverage;
    }
}

internal static class SubstatQualityCalculator
{
    internal static float NormalizeRoll(float value)
    {
        return Clamp01(value / 100f);
    }

    internal static bool IsDisplayedMaximum(float value, float minimum, float maximum)
    {
        const float epsilon = 0.01f;
        return maximum >= minimum
            ? value >= maximum - epsilon
            : value <= maximum + epsilon;
    }

    internal static bool TryCreate(
        float normalizedRollSum,
        int substatCount,
        int displayedMaximumCount,
        int displayedComparedCount,
        out SubstatQuality quality)
    {
        if (substatCount <= 0)
        {
            quality = default;
            return false;
        }

        var displayedMaximumValid =
            displayedComparedCount == substatCount &&
            displayedMaximumCount >= 0 &&
            displayedMaximumCount <= displayedComparedCount;
        quality = new SubstatQuality(
            Clamp01(normalizedRollSum / substatCount),
            displayedMaximumValid ? displayedMaximumCount : 0,
            substatCount,
            displayedMaximumValid);
        return true;
    }

    internal static SubstatQualityTier Classify(
        float score,
        float oneStarThreshold,
        float twoStarThreshold,
        float threeStarThreshold)
    {
        var oneStar = Clamp01(oneStarThreshold);
        var twoStars = System.Math.Max(oneStar, Clamp01(twoStarThreshold));
        var threeStars = System.Math.Max(twoStars, Clamp01(threeStarThreshold));
        var normalizedScore = Clamp01(score);

        if (normalizedScore >= threeStars)
        {
            return SubstatQualityTier.ThreeStars;
        }
        if (normalizedScore >= twoStars)
        {
            return SubstatQualityTier.TwoStars;
        }
        return normalizedScore >= oneStar
            ? SubstatQualityTier.OneStar
            : SubstatQualityTier.None;
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || value <= 0f)
        {
            return 0f;
        }
        return value >= 1f ? 1f : value;
    }
}

internal static class InventoryNameMarkerText
{
    private static readonly string[] Prefixes = { "★★★ ", "★★ ", "★ " };

    internal static string Strip(string value)
    {
        var text = value ?? string.Empty;
        foreach (var prefix in Prefixes)
        {
            if (text.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return text.Substring(prefix.Length);
            }
        }
        return text;
    }

    internal static string PreserveMarker(string current, string desired)
    {
        return GetPrefix(current) + Strip(desired);
    }

    private static string GetPrefix(string value)
    {
        var text = value ?? string.Empty;
        foreach (var prefix in Prefixes)
        {
            if (text.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return prefix;
            }
        }
        return string.Empty;
    }
}
