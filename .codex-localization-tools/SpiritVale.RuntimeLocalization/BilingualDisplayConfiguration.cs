using System;

namespace SpiritVale.RuntimeLocalization;

internal enum DisplayMode
{
    Chinese,
    Bilingual
}

internal enum CompactSurfaceMode
{
    Chinese,
    EnglishToggle,
    // Legacy preview configuration spelling retained as a value alias.
    EnglishOnHold = EnglishToggle
}

internal static class BilingualDisplayConfiguration
{
    internal static DisplayMode ParseDisplayMode(string value)
    {
        return string.Equals(value, nameof(DisplayMode.Bilingual), StringComparison.Ordinal)
            ? DisplayMode.Bilingual
            : DisplayMode.Chinese;
    }

    internal static CompactSurfaceMode ParseCompactSurfaceMode(string value)
    {
        if (string.Equals(value, nameof(CompactSurfaceMode.EnglishToggle), StringComparison.Ordinal) ||
            string.Equals(value, nameof(CompactSurfaceMode.EnglishOnHold), StringComparison.Ordinal))
        {
            return CompactSurfaceMode.EnglishToggle;
        }

        return CompactSurfaceMode.Chinese;
    }

    internal static bool NextCompactEnglishState(
        CompactSurfaceMode mode,
        bool compactEnglishEnabled,
        bool toggleKeyPressed)
    {
        if (mode != CompactSurfaceMode.EnglishToggle)
        {
            return false;
        }

        return toggleKeyPressed
            ? !compactEnglishEnabled
            : compactEnglishEnabled;
    }

    /// <summary>
    /// Consumes exactly one physical key press. The caller keeps the latch for the lifetime of
    /// the input poller, so several LateUpdate calls in one frame (or repeat polls while Alt is
    /// held) cannot toggle compact labels back and forth.
    /// </summary>
    internal static bool TryConsumeCompactEnglishToggle(
        CompactSurfaceMode mode,
        bool compactEnglishEnabled,
        bool toggleKeyIsDown,
        ref bool toggleKeyWasDown,
        out bool nextCompactEnglishEnabled)
    {
        nextCompactEnglishEnabled = compactEnglishEnabled;
        if (mode != CompactSurfaceMode.EnglishToggle)
        {
            toggleKeyWasDown = false;
            return false;
        }

        if (!toggleKeyIsDown)
        {
            toggleKeyWasDown = false;
            return false;
        }

        if (toggleKeyWasDown)
        {
            return false;
        }

        toggleKeyWasDown = true;
        nextCompactEnglishEnabled = !compactEnglishEnabled;
        return true;
    }
}
