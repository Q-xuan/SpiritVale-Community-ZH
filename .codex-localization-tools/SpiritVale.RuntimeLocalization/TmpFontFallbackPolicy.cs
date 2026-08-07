namespace SpiritVale.RuntimeLocalization;

internal enum MissingCjkFontStrategy
{
    UseCurrentFont,
    UseSharedFallback
}

internal enum FontFallbackRole
{
    GameAsset,
    SharedFallback
}

internal static class TmpFontFallbackPolicy
{
    internal static MissingCjkFontStrategy Select(bool currentFontSupportsWithoutMutation)
    {
        return currentFontSupportsWithoutMutation
            ? MissingCjkFontStrategy.UseCurrentFont
            : MissingCjkFontStrategy.UseSharedFallback;
    }

    internal static bool MayPopulateDynamically(FontFallbackRole role)
    {
        return role == FontFallbackRole.SharedFallback;
    }

    internal static bool ShouldPromoteCurrentToShared(bool sharedFallbackSelected)
    {
        return !sharedFallbackSelected;
    }
}
