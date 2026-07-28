using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SpiritVale.RuntimeLocalization;

internal enum MarketSearchBridgeOutcome
{
    Unchanged,
    Translated,
    Ambiguous,
}

internal enum MarketSearchIndexOutcome
{
    Unchanged,
    Matched,
    NoMatch,
}

internal enum MarketSearchMergeOutcome
{
    Unchanged,
    NoMatch,
    NoSnapshot,
    Matched,
    Merged,
}

internal sealed class MarketSearchIdentity : IEquatable<MarketSearchIdentity>
{
    internal MarketSearchIdentity(string itemType, string itemId)
    {
        ItemType = itemType ?? string.Empty;
        ItemId = itemId ?? string.Empty;
    }

    internal string ItemType { get; }
    internal string ItemId { get; }

    public bool Equals(MarketSearchIdentity other)
    {
        return other != null &&
            ItemType.Equals(other.ItemType, StringComparison.Ordinal) &&
            ItemId.Equals(other.ItemId, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as MarketSearchIdentity);
    }

    public override int GetHashCode()
    {
        return unchecked((ItemType.GetHashCode() * 397) ^ ItemId.GetHashCode());
    }
}

internal sealed class MarketSearchCatalogEntry
{
    internal MarketSearchCatalogEntry(
        string itemType,
        string itemId,
        string source,
        string target,
        IEnumerable<string> aliases)
    {
        Identity = new MarketSearchIdentity(itemType, itemId);
        Source = source ?? string.Empty;
        Target = target ?? string.Empty;
        Aliases = (aliases ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal MarketSearchIdentity Identity { get; }
    internal string Source { get; }
    internal string Target { get; }
    internal IReadOnlyCollection<string> Aliases { get; }
}

internal sealed class MarketSearchQueryBridge
{
    internal const string SupportedDeclaringType = "UIVendingSearch";
    internal const string SupportedMethod = "Search";
    internal const string SupportedCallbackMethod = "_Search_b__10_0";
    internal const string SupportedPlayerType = "PlayerController";
    internal const string SupportedPlayerRequestMethod = "RequestVendorItemList";
    internal const string SupportedManagerType = "VendingManager";
    internal const string SupportedRequestMethod = "RequestItemList";

    private static readonly Regex RichTextTagPattern = new Regex(
        @"<[^>]+>",
        RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<ReverseEntry> _affixes;
    private readonly IReadOnlyList<ReverseEntry> _baseNames;
    private readonly IReadOnlyList<ReverseEntry> _searchNames;
    private readonly IReadOnlyList<ReverseEntry> _keywords;
    private readonly IReadOnlyList<IndexEntry> _catalogIndex;

    internal MarketSearchQueryBridge(IEnumerable<MarketSearchCatalogEntry> catalogEntries)
    {
        _affixes = Array.Empty<ReverseEntry>();
        _baseNames = Array.Empty<ReverseEntry>();
        _searchNames = Array.Empty<ReverseEntry>();
        _keywords = Array.Empty<ReverseEntry>();
        _catalogIndex = BuildCatalogIndex(catalogEntries);
    }

    internal MarketSearchQueryBridge(
        IReadOnlyDictionary<string, string> translations,
        IEnumerable<string> itemAffixes,
        IEnumerable<string> itemBaseNames)
        : this(translations, itemAffixes, itemBaseNames, itemBaseNames)
    {
    }

    internal MarketSearchQueryBridge(
        IReadOnlyDictionary<string, string> translations,
        IEnumerable<string> itemAffixes,
        IEnumerable<string> itemBaseNames,
        IEnumerable<string> marketSearchNames)
        : this(
            translations,
            itemAffixes,
            itemBaseNames,
            marketSearchNames,
            Array.Empty<KeyValuePair<string, string>>())
    {
    }

    internal MarketSearchQueryBridge(
        IReadOnlyDictionary<string, string> translations,
        IEnumerable<string> itemAffixes,
        IEnumerable<string> itemBaseNames,
        IEnumerable<string> marketSearchNames,
        IEnumerable<KeyValuePair<string, string>> marketSearchKeywords)
        : this(
            ResolveReviewedTranslations(translations, itemAffixes),
            ResolveReviewedTranslations(translations, itemBaseNames),
            ResolveReviewedTranslations(translations, marketSearchNames),
            marketSearchKeywords)
    {
    }

    internal MarketSearchQueryBridge(
        IEnumerable<KeyValuePair<string, string>> itemAffixes,
        IEnumerable<KeyValuePair<string, string>> itemBaseNames)
        : this(itemAffixes, itemBaseNames, itemBaseNames)
    {
    }

    internal MarketSearchQueryBridge(
        IEnumerable<KeyValuePair<string, string>> itemAffixes,
        IEnumerable<KeyValuePair<string, string>> itemBaseNames,
        IEnumerable<KeyValuePair<string, string>> marketSearchNames)
        : this(
            itemAffixes,
            itemBaseNames,
            marketSearchNames,
            Array.Empty<KeyValuePair<string, string>>())
    {
    }

    internal MarketSearchQueryBridge(
        IEnumerable<KeyValuePair<string, string>> itemAffixes,
        IEnumerable<KeyValuePair<string, string>> itemBaseNames,
        IEnumerable<KeyValuePair<string, string>> marketSearchNames,
        IEnumerable<KeyValuePair<string, string>> marketSearchKeywords)
    {
        _affixes = BuildReverseEntries(itemAffixes);
        _baseNames = BuildReverseEntries(itemBaseNames);
        _searchNames = BuildReverseEntries(marketSearchNames);
        _keywords = BuildReverseEntries(marketSearchKeywords);
        _catalogIndex = Array.Empty<IndexEntry>();
    }

    internal MarketSearchIndexOutcome TryResolveIdentities(
        string declaringType,
        string method,
        string query,
        out IReadOnlyCollection<MarketSearchIdentity> identities)
    {
        identities = Array.Empty<MarketSearchIdentity>();
        var supportedManagerRequest =
            string.Equals(declaringType, SupportedManagerType, StringComparison.Ordinal) &&
            string.Equals(method, SupportedRequestMethod, StringComparison.Ordinal);
        var supportedClientCallback =
            string.Equals(declaringType, SupportedDeclaringType, StringComparison.Ordinal) &&
            string.Equals(method, SupportedCallbackMethod, StringComparison.Ordinal);
        if ((!supportedManagerRequest && !supportedClientCallback) ||
            string.IsNullOrEmpty(query) ||
            !CjkText.ContainsCjk(query))
        {
            return MarketSearchIndexOutcome.Unchanged;
        }

        var normalizedQuery = NormalizeMarketText(query);
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return MarketSearchIndexOutcome.NoMatch;
        }

        var matches = new HashSet<MarketSearchIdentity>();
        foreach (var entry in _catalogIndex)
        {
            if (entry.SearchFields.Any(field =>
                    field.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0))
            {
                matches.Add(entry.Identity);
            }
        }
        identities = matches
            .OrderBy(value => value.ItemType, StringComparer.Ordinal)
            .ThenBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        return matches.Count == 0
            ? MarketSearchIndexOutcome.NoMatch
            : MarketSearchIndexOutcome.Matched;
    }

    internal static string NormalizeMarketText(string value)
    {
        var visible = RichTextTagPattern.Replace(value ?? string.Empty, string.Empty)
            .Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(visible.Length);
        foreach (var character in visible)
        {
            if (!char.IsWhiteSpace(character) &&
                CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }
        return builder.ToString();
    }

    internal static string StripFormatCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format)
            {
                builder.Append(character);
            }
        }
        return builder.ToString();
    }

    internal static string SelectCallbackQuery(
        string currentSearch,
        string visibleFieldText)
    {
        if (CjkText.ContainsCjk(visibleFieldText))
        {
            return visibleFieldText;
        }
        return currentSearch ?? visibleFieldText ?? string.Empty;
    }

    internal MarketSearchMergeOutcome TryMergeSnapshot<T>(
        string declaringType,
        string method,
        string query,
        IEnumerable<T> originalResults,
        IEnumerable<T> snapshot,
        Func<T, MarketSearchIdentity> identitySelector,
        Func<T, string> stableIdSelector,
        out IReadOnlyList<T> merged)
    {
        var original = (originalResults ?? Array.Empty<T>()).ToArray();
        merged = original;
        var resolution = TryResolveIdentities(
            declaringType,
            method,
            query,
            out var identities);
        if (resolution == MarketSearchIndexOutcome.Unchanged)
        {
            return MarketSearchMergeOutcome.Unchanged;
        }
        if (resolution == MarketSearchIndexOutcome.NoMatch)
        {
            return MarketSearchMergeOutcome.NoMatch;
        }
        if (snapshot == null)
        {
            return MarketSearchMergeOutcome.NoSnapshot;
        }
        if (identitySelector == null)
        {
            throw new ArgumentNullException(nameof(identitySelector));
        }
        if (stableIdSelector == null)
        {
            throw new ArgumentNullException(nameof(stableIdSelector));
        }

        var canonicalMatches = new HashSet<MarketSearchIdentity>(identities);
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in original)
        {
            var stableId = stableIdSelector(item);
            if (string.IsNullOrEmpty(stableId))
            {
                throw new InvalidOperationException(
                    "An original market result has no stable listing identity.");
            }
            stableIds.Add(stableId);
        }

        var combined = new List<T>(original);
        foreach (var item in snapshot)
        {
            var identity = identitySelector(item);
            if (identity == null ||
                string.IsNullOrEmpty(identity.ItemType) ||
                string.IsNullOrEmpty(identity.ItemId))
            {
                throw new InvalidOperationException(
                    "A market snapshot item has no canonical identity.");
            }
            if (!canonicalMatches.Contains(identity))
            {
                continue;
            }

            var stableId = stableIdSelector(item);
            if (string.IsNullOrEmpty(stableId))
            {
                throw new InvalidOperationException(
                    "A matching market snapshot item has no stable listing identity.");
            }
            if (stableIds.Add(stableId))
            {
                combined.Add(item);
            }
        }

        if (combined.Count == original.Length)
        {
            return MarketSearchMergeOutcome.Matched;
        }
        merged = combined;
        return MarketSearchMergeOutcome.Merged;
    }

    private static IReadOnlyList<IndexEntry> BuildCatalogIndex(
        IEnumerable<MarketSearchCatalogEntry> catalogEntries)
    {
        var entries = new List<IndexEntry>();
        foreach (var entry in catalogEntries ?? Array.Empty<MarketSearchCatalogEntry>())
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.Identity.ItemType) ||
                string.IsNullOrWhiteSpace(entry.Identity.ItemId) ||
                string.IsNullOrWhiteSpace(entry.Target))
            {
                continue;
            }
            var fields = new[] { entry.Target }
                .Concat(entry.Aliases)
                .Select(NormalizeMarketText)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (fields.Length != 0)
            {
                entries.Add(new IndexEntry(entry.Identity, fields));
            }
        }
        return entries;
    }

    internal MarketSearchBridgeOutcome TryBridge(
        string declaringType,
        string method,
        string query,
        out string bridged)
    {
        bridged = query ?? string.Empty;
        var supportedUiSearch =
            string.Equals(declaringType, SupportedDeclaringType, StringComparison.Ordinal) &&
            string.Equals(method, SupportedMethod, StringComparison.Ordinal);
        var supportedPlayerRequest =
            string.Equals(declaringType, SupportedPlayerType, StringComparison.Ordinal) &&
            string.Equals(method, SupportedPlayerRequestMethod, StringComparison.Ordinal);
        var wireQuery = StripFormatCharacters(query);
        if ((!supportedUiSearch && !supportedPlayerRequest) ||
            string.IsNullOrEmpty(wireQuery) ||
            !CjkText.ContainsCjk(wireQuery))
        {
            return MarketSearchBridgeOutcome.Unchanged;
        }

        var visible = RichTextTagPattern.Replace(wireQuery, string.Empty);
        var nameStart = 0;
        while (nameStart < visible.Length && char.IsWhiteSpace(visible[nameStart]))
        {
            nameStart++;
        }
        var nameEnd = visible.Length;
        while (nameEnd > nameStart && char.IsWhiteSpace(visible[nameEnd - 1]))
        {
            nameEnd--;
        }
        if (nameStart == nameEnd)
        {
            return MarketSearchBridgeOutcome.Unchanged;
        }

        var upgradeEnd = nameStart;
        if (visible[upgradeEnd] == '+')
        {
            var digitEnd = upgradeEnd + 1;
            while (digitEnd < nameEnd && char.IsDigit(visible[digitEnd]))
            {
                digitEnd++;
            }
            if (digitEnd > upgradeEnd + 1)
            {
                while (digitEnd < nameEnd && char.IsWhiteSpace(visible[digitEnd]))
                {
                    digitEnd++;
                }
                if (digitEnd < nameEnd)
                {
                    nameStart = digitEnd;
                }
            }
        }

        var visibleName = visible.Substring(nameStart, nameEnd - nameStart);
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        AddExactSearchNameCandidates(wireQuery, visibleName, nameStart, candidates);
        AddCompositeCandidates(wireQuery, visibleName, nameStart, candidates);

        if (candidates.Count == 1)
        {
            bridged = candidates.First();
            return MarketSearchBridgeOutcome.Translated;
        }
        if (candidates.Count > 1)
        {
            return MarketSearchBridgeOutcome.Ambiguous;
        }

        var keywordOutcome = TryExactKeyword(
            wireQuery,
            visibleName,
            nameStart,
            out var keywordCandidate);
        if (keywordOutcome == MarketSearchBridgeOutcome.Translated)
        {
            bridged = keywordCandidate;
        }
        if (keywordOutcome != MarketSearchBridgeOutcome.Unchanged)
        {
            return keywordOutcome;
        }

        var substringOutcome = TryUniqueBaseSubstringFallback(
            wireQuery,
            visibleName,
            nameStart,
            out var substringCandidate);
        if (substringOutcome == MarketSearchBridgeOutcome.Translated)
        {
            bridged = substringCandidate;
        }
        return substringOutcome;
    }

    private MarketSearchBridgeOutcome TryExactKeyword(
        string query,
        string visibleName,
        int nameStart,
        out string candidate)
    {
        candidate = query;
        foreach (var entry in _keywords)
        {
            if (!visibleName.Equals(entry.Target, StringComparison.Ordinal))
            {
                continue;
            }
            if (entry.Sources.Count != 1)
            {
                return MarketSearchBridgeOutcome.Ambiguous;
            }
            candidate = ApplyReplacements(
                query,
                new[]
                {
                    new VisibleReplacement(
                        nameStart,
                        visibleName.Length,
                        entry.Sources[0]),
                });
            return MarketSearchBridgeOutcome.Translated;
        }
        return MarketSearchBridgeOutcome.Unchanged;
    }

    private void AddExactSearchNameCandidates(
        string query,
        string visibleName,
        int nameStart,
        HashSet<string> candidates)
    {
        foreach (var entry in _searchNames)
        {
            if (!visibleName.Equals(entry.Target, StringComparison.Ordinal))
            {
                continue;
            }
            foreach (var source in entry.Sources)
            {
                candidates.Add(ApplyReplacements(
                    query,
                    new[] { new VisibleReplacement(nameStart, entry.Target.Length, source) }));
            }
        }
    }

    private void AddCompositeCandidates(
        string query,
        string visibleName,
        int nameStart,
        HashSet<string> candidates)
    {
        foreach (var baseEntry in _baseNames)
        {
            if (!visibleName.EndsWith(baseEntry.Target, StringComparison.Ordinal))
            {
                continue;
            }

            var baseStart = visibleName.Length - baseEntry.Target.Length;
            var affixEnd = baseStart;
            while (affixEnd > 0 && char.IsWhiteSpace(visibleName[affixEnd - 1]))
            {
                affixEnd--;
            }
            if (affixEnd == 0)
            {
                continue;
            }

            var segmentations = new List<List<VisibleReplacement>>();
            FindAffixSegmentations(
                visibleName,
                0,
                affixEnd,
                new List<VisibleReplacement>(),
                segmentations);
            foreach (var segmentation in segmentations)
            {
                foreach (var baseSource in baseEntry.Sources)
                {
                    var replacements = segmentation
                        .Select(value => new VisibleReplacement(
                            nameStart + value.Start,
                            value.Length,
                            value.Target))
                        .ToList();
                    replacements.Add(new VisibleReplacement(
                        nameStart + baseStart,
                        baseEntry.Target.Length,
                        baseSource));
                    candidates.Add(ApplyReplacements(query, replacements));
                    if (candidates.Count > 1)
                    {
                        return;
                    }
                }
            }
        }
    }

    private MarketSearchBridgeOutcome TryUniqueBaseSubstringFallback(
        string query,
        string visibleName,
        int nameStart,
        out string candidate)
    {
        candidate = query;
        var normalizedQuery = RemoveWhitespace(visibleName);
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return MarketSearchBridgeOutcome.Unchanged;
        }

        var matches = _searchNames
            .Select(entry => new
            {
                Entry = entry,
                NormalizedTarget = RemoveWhitespace(entry.Target),
            })
            .Where(value =>
                !normalizedQuery.Equals(value.NormalizedTarget, StringComparison.Ordinal) &&
                value.NormalizedTarget.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0)
            .ToList();
        if (matches.Count == 0)
        {
            return MarketSearchBridgeOutcome.Unchanged;
        }
        if (CountCjk(normalizedQuery) < 2 ||
            matches.Count != 1 ||
            matches[0].Entry.Sources.Count != 1)
        {
            return MarketSearchBridgeOutcome.Ambiguous;
        }

        candidate = ApplyReplacements(
            query,
            new[]
            {
                new VisibleReplacement(
                    nameStart,
                    visibleName.Length,
                    matches[0].Entry.Sources[0]),
            });
        return MarketSearchBridgeOutcome.Translated;
    }

    private static string RemoveWhitespace(string value)
    {
        return new string((value ?? string.Empty)
            .Where(character => !char.IsWhiteSpace(character))
            .ToArray());
    }

    private static int CountCjk(string value)
    {
        return (value ?? string.Empty).Count(CjkText.IsCjk);
    }

    private void FindAffixSegmentations(
        string value,
        int position,
        int end,
        List<VisibleReplacement> current,
        List<List<VisibleReplacement>> results)
    {
        while (position < end && char.IsWhiteSpace(value[position]))
        {
            position++;
        }
        if (position == end)
        {
            results.Add(new List<VisibleReplacement>(current));
            return;
        }

        foreach (var entry in _affixes)
        {
            if (position + entry.Target.Length > end ||
                !value.Substring(position, entry.Target.Length)
                    .Equals(entry.Target, StringComparison.Ordinal))
            {
                continue;
            }

            var next = position + entry.Target.Length;
            var needsSeparator = next < value.Length && !char.IsWhiteSpace(value[next]);
            foreach (var source in entry.Sources)
            {
                current.Add(new VisibleReplacement(
                    position,
                    entry.Target.Length,
                    needsSeparator ? source + " " : source));
                FindAffixSegmentations(value, next, end, current, results);
                current.RemoveAt(current.Count - 1);
                if (results.Count > 1)
                {
                    return;
                }
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> ResolveReviewedTranslations(
        IReadOnlyDictionary<string, string> translations,
        IEnumerable<string> reviewedSources)
    {
        var safeTranslations = translations ??
            new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in reviewedSources ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(source) &&
                safeTranslations.TryGetValue(source, out var target))
            {
                yield return new KeyValuePair<string, string>(source, target);
            }
        }
    }

    private static IReadOnlyList<ReverseEntry> BuildReverseEntries(
        IEnumerable<KeyValuePair<string, string>> reviewedEntries)
    {
        var grouped = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entry in reviewedEntries ??
                     Array.Empty<KeyValuePair<string, string>>())
        {
            var source = entry.Key;
            var target = entry.Value;
            if (string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(target) ||
                target.Equals(source, StringComparison.Ordinal) ||
                !CjkText.ContainsCjk(target))
            {
                continue;
            }
            if (!grouped.TryGetValue(target, out var sources))
            {
                sources = new HashSet<string>(StringComparer.Ordinal);
                grouped.Add(target, sources);
            }
            sources.Add(source);
        }

        return grouped
            .Select(pair => new ReverseEntry(
                pair.Key,
                pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray()))
            .OrderByDescending(entry => entry.Target.Length)
            .ThenBy(entry => entry.Target, StringComparer.Ordinal)
            .ToList();
    }

    private static string ApplyReplacements(
        string source,
        IEnumerable<VisibleReplacement> replacements)
    {
        var current = source;
        foreach (var replacement in replacements.OrderByDescending(value => value.Start))
        {
            current = ReplaceVisibleRangePreservingMarkup(
                current,
                replacement.Start,
                replacement.Length,
                replacement.Target);
        }
        return current;
    }

    private static string ReplaceVisibleRangePreservingMarkup(
        string source,
        int start,
        int length,
        string replacement)
    {
        var end = start + length;
        var visibleIndex = 0;
        var inserted = false;
        var builder = new StringBuilder(source.Length + replacement.Length);
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '<')
            {
                var tagEnd = source.IndexOf('>', index + 1);
                if (tagEnd >= 0)
                {
                    builder.Append(source, index, tagEnd - index + 1);
                    index = tagEnd;
                    continue;
                }
            }

            if (visibleIndex >= start && visibleIndex < end)
            {
                if (!inserted)
                {
                    builder.Append(replacement);
                    inserted = true;
                }
            }
            else
            {
                builder.Append(source[index]);
            }
            visibleIndex++;
        }
        return inserted ? builder.ToString() : source;
    }

    private sealed class ReverseEntry
    {
        internal ReverseEntry(string target, IReadOnlyList<string> sources)
        {
            Target = target;
            Sources = sources;
        }

        internal string Target { get; }
        internal IReadOnlyList<string> Sources { get; }
    }

    private sealed class IndexEntry
    {
        internal IndexEntry(
            MarketSearchIdentity identity,
            IReadOnlyCollection<string> searchFields)
        {
            Identity = identity;
            SearchFields = searchFields;
        }

        internal MarketSearchIdentity Identity { get; }
        internal IReadOnlyCollection<string> SearchFields { get; }
    }

    private sealed class VisibleReplacement
    {
        internal VisibleReplacement(int start, int length, string target)
        {
            Start = start;
            Length = length;
            Target = target;
        }

        internal int Start { get; }
        internal int Length { get; }
        internal string Target { get; }
    }
}

internal sealed class MarketSearchSnapshot<T>
{
    private readonly object _gate = new object();
    private int _ownerId;
    private T[] _items = Array.Empty<T>();
    private bool _hasSnapshot;

    internal void Capture(int ownerId, IEnumerable<T> items)
    {
        var captured = (items ?? Array.Empty<T>()).ToArray();
        lock (_gate)
        {
            _ownerId = ownerId;
            _items = captured;
            _hasSnapshot = true;
        }
    }

    internal bool TryGet(int ownerId, out IReadOnlyList<T> items)
    {
        lock (_gate)
        {
            if (_hasSnapshot && _ownerId == ownerId)
            {
                items = _items.ToArray();
                return true;
            }
        }
        items = Array.Empty<T>();
        return false;
    }

    internal void Clear()
    {
        lock (_gate)
        {
            _ownerId = 0;
            _items = Array.Empty<T>();
            _hasSnapshot = false;
        }
    }
}
