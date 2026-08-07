using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SpiritVale.RuntimeLocalization;

internal sealed class EntityDisplayValues
{
    internal EntityDisplayValues(string chinese, string bilingual, string english)
    {
        Chinese = chinese;
        Bilingual = bilingual;
        English = english;
    }

    internal string Chinese { get; }
    internal string Bilingual { get; }
    internal string English { get; }
}

internal static class EntityDisplayComposer
{
    private static readonly Regex RichTextTokenPattern = new Regex(
        @"<[^<>\r\n]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CompositePlaceholderPattern = new Regex(
        @"\{(?:\d+|[A-Za-z_][A-Za-z0-9_.-]*)(?:[^{}]*)?\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PrintfPlaceholderPattern = new Regex(
        @"%(?:\d+\$)?[-+#0 ']*(?:\d+|\*)?(?:\.(?:\d+|\*))?[A-Za-z]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NumberTokenPattern = new Regex(
        @"(?<![A-Za-z0-9_])[+-]?\d+(?:[.,]\d+)*(?:%|[xX])?(?![A-Za-z0-9_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static EntityDisplayValues CreateValues(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidDataException("Entity display source must not be empty.");
        }
        if (string.IsNullOrEmpty(target))
        {
            throw new InvalidDataException("Entity display target must not be empty.");
        }
        if (!HasMatchingProtectedTokens(source, target))
        {
            throw new InvalidDataException(
                "Entity display target does not preserve rich-text, number, or placeholder tokens.");
        }

        return new EntityDisplayValues(
            target,
            target + "\n" + source,
            source);
    }

    internal static string ComposeDetail(EntityDisplayEntry entry, DisplayMode mode)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        return mode == DisplayMode.Bilingual
            ? entry.Values.Bilingual
            : entry.Values.Chinese;
    }

    internal static string ComposeCompact(
        EntityDisplayEntry entry,
        CompactSurfaceMode mode,
        bool temporaryEnglishHeld)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        return mode == CompactSurfaceMode.EnglishOnHold &&
            temporaryEnglishHeld &&
            entry.CompactPolicy == CompactDisplayPolicy.EnglishOnHold
                ? entry.Values.English
                : entry.Values.Chinese;
    }

    internal static bool IsExpectedDisplayValue(
        EntityDisplayValues values,
        bool detailSurface,
        CompactDisplayPolicy compactPolicy,
        bool compactEnglishEnabled,
        string currentValue)
    {
        if (values == null)
        {
            return false;
        }

        var expected = detailSurface
            ? values.Bilingual
            : compactEnglishEnabled && compactPolicy == CompactDisplayPolicy.EnglishOnHold
                ? values.English
                : values.Chinese;
        return string.Equals(currentValue, expected, StringComparison.Ordinal);
    }

    private static bool HasMatchingProtectedTokens(string source, string target)
    {
        return HaveSameTokens(source, target, RichTextTokenPattern) &&
            HaveSameTokens(source, target, CompositePlaceholderPattern) &&
            HaveSameTokens(source, target, PrintfPlaceholderPattern) &&
            HaveSameNumberTokens(source, target);
    }

    private static bool HaveSameNumberTokens(string source, string target)
    {
        var scrubbedSource = ScrubNonNumberTokens(source);
        var scrubbedTarget = ScrubNonNumberTokens(target);
        return HaveSameTokens(scrubbedSource, scrubbedTarget, NumberTokenPattern);
    }

    private static string ScrubNonNumberTokens(string value)
    {
        var scrubbed = RichTextTokenPattern.Replace(value, string.Empty);
        scrubbed = CompositePlaceholderPattern.Replace(scrubbed, string.Empty);
        return PrintfPlaceholderPattern.Replace(scrubbed, string.Empty);
    }

    private static bool HaveSameTokens(string source, string target, Regex pattern)
    {
        var sourceTokens = GetOrderedTokens(source, pattern);
        var targetTokens = GetOrderedTokens(target, pattern);
        return sourceTokens.SequenceEqual(targetTokens, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> GetOrderedTokens(string value, Regex pattern)
    {
        var tokens = new List<string>();
        foreach (Match match in pattern.Matches(value))
        {
            tokens.Add(match.Value);
        }
        tokens.Sort(StringComparer.Ordinal);
        return tokens;
    }
}
