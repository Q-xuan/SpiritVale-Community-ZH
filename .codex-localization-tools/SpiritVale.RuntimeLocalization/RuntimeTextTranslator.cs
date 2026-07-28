using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SpiritVale.RuntimeLocalization;

internal static class CjkText
{
    internal static bool ContainsCjk(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (IsCjk(character))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool IsCjk(char value)
    {
        return (value >= '\u3400' && value <= '\u4DBF') ||
            (value >= '\u4E00' && value <= '\u9FFF') ||
            (value >= '\uF900' && value <= '\uFAFF');
    }
}

internal sealed class RuntimeTextTranslator
{
    private const int DefaultCacheCapacity = 2048;
    private const int MaximumCacheableSourceLength = 512;

    private static readonly IReadOnlyDictionary<string, string> NoTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly Regex CharacterCountPattern = new Regex(
        @"^Characters:\s*(\d+)\s*/\s*(\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex LocationPattern = new Regex(
        @"^Location:\s*(.+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex LocalizedLocationPattern = new Regex(
        // Party rows render the localized label and the map on separate lines.
        // Depending on the producer, the separator is either a real CR/LF or
        // the literal two-character sequence "\\n". `.` does not span a real
        // newline, so the old pattern silently left every such map untouched.
        @"^(位置(?:[:：]\s*|\r?\n|\\n))([\s\S]+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PlaytimePattern = new Regex(
        @"^Playtime:\s*(\d+)h\s*(\d+)m$",
        RegexOptions.CultureInvariant);
    private static readonly Regex DeathsPattern = new Regex(
        @"^Deaths:\s*(\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex GameStartsPattern = new Regex(
        @"^Game starts in:\s*(.+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex RoundStartsPattern = new Regex(
        @"^Round Starts In:\s*(.+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PartySummaryPattern = new Regex(
        @"^Members:\s*(\d+)\s*/\s*(\d+)[ \t]*(?:\r?\n|\\n)" +
        @"Exp and Drop Rate:[ \t]*([^\r\n\\]+?)[ \t]*(?:\r?\n|\\n)Level Range:[ \t]*(.+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex NetworkSummaryPattern = new Regex(
        @"^Ping:\s*([^|]+)\|\s*FPS:\s*([^|]+)\|\s*Players:\s*(.+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PerformancePattern = new Regex(
        @"^FPS:\s*(.+?)\s*\(([^)]+)\)\s+Ping:\s*(.+?)\s+Players:\s*(\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex TimedSalePattern = new Regex(
        @"^\[(\d+)\s+(seconds?|minutes?|hours?|days?|weeks?|months?|years?)\s+ago\]\s+Sold\s+(.+?)\s+to\s+(.+?)\s+for\s+(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex JustNowSalePattern = new Regex(
        @"^\[(?:just\s+now|now)\]\s+Sold\s+(.+?)\s+to\s+(.+?)\s+for\s+(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SalePattern = new Regex(
        @"^Sold\s+(.+?)\s+for\s+([\d,]+)\s+Coins$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PartyInvitePattern = new Regex(
        @"^(.+?)\s+invited you to (?:join the|their) party$",
        RegexOptions.CultureInvariant);
    private static readonly Regex GemDescriptionPattern = new Regex(
        @"^一颗闪耀的宝石，封存着一位昔日 (.+?) 的记忆。嵌入神器后，使用者的 (.+?) 将获得强化。$",
        RegexOptions.CultureInvariant);
    private static readonly Regex MonsterNameplatePattern = new Regex(
        @"^(.+?)( {2,}|\r?\n\s*)(.+?)\s+Lv\.(\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex SkillLevelCostPattern = new Regex(
        @"^(.+?)\s+Lv\.(\d+)\s+\[(\d+)\s+([A-Za-z]+)\]([\s\S]*)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PlayerNameplatePattern = new Regex(
        @"^(.+?)(\s+)Lv\.(\d+)\s+(.+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex LevelClassPattern = new Regex(
        @"^Lv\.?(\d+)\s+(.+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ShortLevelPattern = new Regex(
        @"^Lv\.?(\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex StandaloneLevelRangePattern = new Regex(
        @"^Lv\.?(\d+)-(\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex CompactPlaytimePattern = new Regex(
        @"^(\d+)h\s*(\d+)m$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ResetTimerPattern = new Regex(
        @"^Reset in\s+(\d+)h\s*(\d+)m$",
        RegexOptions.CultureInvariant);
    private static readonly Regex LevelValuePattern = new Regex(
        @"^Level\s+(\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex WeightValuePattern = new Regex(
        @"^Weight:\s*([\d,]+(?:\s*/\s*[\d,]+)?)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ItemCountPattern = new Regex(
        @"^Items:\s*(\d+)\s*/\s*(\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex VendingDurationPattern = new Regex(
        @"^Vending lasts\s+(\d+)\s+hours?\.\r?\nAll transactions have\s+([\d.]+)%\s+Tax\.$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ListingTermsPattern = new Regex(
        @"^Listings expire in\s+(\d+)h\.\r?\nListing fee:\s*([\d.]+)%\.\s*Sales tax:\s*([\d.]+)%\.$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex DismantleConfirmationPattern = new Regex(
        @"^Are you sure you want to\s+(?:Dismantle|拆解)\r?\n(.+)\?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex LocalizedPlaytimePattern = new Regex(
        @"^(?:游戏时长|游玩时间)[:：]\s*(\d+)h\s*(\d+)m$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ArtifactSetPattern = new Regex(
        @"^(.+?) Artifact Set$",
        RegexOptions.CultureInvariant);
    private static readonly Regex MapLevelRangePattern = new Regex(
        @"^(.+?)\r?\nLv\.?([0-9]+)-([0-9]+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex UpgradePrefixPattern = new Regex(
        @"^(\+[0-9]+\s+)(.+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex KillCountPattern = new Regex(
        @"^(Monster Kills|Boss Kills):\s*(\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ResourceBarPattern = new Regex(
        @"^(HP|MP)\s+([\d,]+)\s*/\s*([\d,]+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ChannelPattern = new Regex(
        @"^Channel\s+(\d+)\s+\((\d+)\)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex RichTextTagPattern = new Regex(
        @"<[^>]+>",
        RegexOptions.CultureInvariant);
    private static readonly Regex RichColorTagPattern = new Regex(
        @"<color(?:=|\s|>)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex VisibleTokenPattern = new Regex(
        @"\S+",
        RegexOptions.CultureInvariant);
    private static readonly Regex WorldInteractionActionPattern = new Regex(
        @"^\s*(?:\[[^\]\r\n]{1,16}\]\s*)?(?:[▶►▸]\s*)?(?<action>View|Pickup)\s*$",
        RegexOptions.CultureInvariant);
    private static readonly Regex CjkBoundarySpacePattern = new Regex(
        @"(?<=[\u3400-\u9FFF\u3000-\u303F\uFF00-\uFFEF]) +(?=[\u3400-\u9FFF\u3000-\u303F\uFF00-\uFFEF])",
        RegexOptions.CultureInvariant);
    private static readonly Regex CjkRichBoundarySpacePattern = new Regex(
        @"(?<=[\u3400-\u9FFF])((?:<[^>]+>)*)\s+((?:<[^>]+>)*)(?=[\u3400-\u9FFF])",
        RegexOptions.CultureInvariant);
    private static readonly Regex NumberedDictionaryEntryPattern = new Regex(
        @"^(.+?)(\s+\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex StructuredStatLinePattern = new Regex(
        @"^(?<label>[A-Za-z][A-Za-z .]*):(?<value>[^\r\n]*)$",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);
    private const string PerTenStatNames =
        @"力量|灵巧|敏捷|智力|体质|幸运|活力|" +
        @"Strength|Dexterity|Agility|Intelligence|Vitality|Luck|" +
        @"STR|DEX|AGI|INT|VIT|LUK";
    private static readonly Regex PerTenStatScalingPattern = new Regex(
        @"^(?<leading>[ \t\u3000]*)(?<body>(?:" +
        @"(?<gain>[+-]?\d+(?:\.\d+)?%)[ \t\u3000]+(?:per|每)[ \t\u3000]*10[ \t\u3000]*" +
        @"(?:点[ \t\u3000]*)?(?<stat>" + PerTenStatNames + @")" +
        @"|(?:per|每)[ \t\u3000]*10[ \t\u3000]*(?:点[ \t\u3000]*)?" +
        @"(?<stat>" + PerTenStatNames + @")[ \t\u3000]+(?<gain>[+-]?\d+(?:\.\d+)?%))[ \t\u3000]*)" +
        @"(?<tail>\([+-]?\d+(?:\.\d+)?%\)|\[[+-]?\d+(?:\.\d+)?%\]|" +
        @"（[+-]?\d+(?:\.\d+)?%）|［[+-]?\d+(?:\.\d+)?%］|【[+-]?\d+(?:\.\d+)?%】)" +
        @"(?<trailing>[ \t\u3000]*)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex OuterRichTextWrapperPattern = new Regex(
        @"^(?<leading>[ \t]*)(?<open><(?<tag>[A-Za-z][A-Za-z0-9]*)" +
        @"(?:\s*=[^>]*)?(?:\s+[^>]*)?>)(?<content>[\s\S]*)" +
        @"(?<close></\k<tag>>)(?<trailing>[ \t]*)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex RichTagTokenPattern = new Regex(
        @"<(?<closing>/)?(?<name>[A-Za-z][A-Za-z0-9]*)(?:\s*=[^>]*)?(?:\s+[^>]*)?>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex NumericSecondsPerLevelPattern = new Regex(
        @"(?<=\d)\s+seconds?\s+(?:per level|每级)(?=$|[\s<])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex NumericSecondsPattern = new Regex(
        @"(?<=\d)\s+seconds?(?=$|[\s<])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex NumericCompactSecondsPattern = new Regex(
        @"(?<=\d)s(?=$|[\s<])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex NumericManaPattern = new Regex(
        @"(?<=\d)\s+mana(?=$|[\s<])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly string[] ItemNameContextMarkers =
    {
        "ItemName", "Item Name", "MarketItem", "Market Item", "EquipmentName",
        "Equipment Name", "EquipName", "Equip Name", "InventoryItem", "Inventory Item"
    };
    private static readonly string[] PlayerNameTemplateContextMarkers =
        { "Text_Name", "Text Name" };
    private static readonly string[] ProtectedUserContextMarkers =
    {
        "Text_Name", "Text Name", "PlayerName", "Player Name", "CharacterName",
        "Character Name", "DisplayName", "Display Name", "Display_Name", "UserInput",
        "ShopName", "Shop Name", "Seller", "Vending", "Guild",
        "PartyName", "Party Name", "TeamName", "Team Name", "Title", "Chat", "Message"
    };
    private static readonly string[] AlwaysProtectedUserContextMarkers =
    {
        "PlayerName", "Player Name", "CharacterName", "Character Name", "UserInput",
        "ShopName", "Shop Name", "Seller", "Vending", "Guild",
        "PartyName", "Party Name", "TeamName", "Team Name", "Chat", "Message"
    };
    private static readonly string[] StatScalingContextMarkers =
        { "SkillDamage", "Skill Damage", "StatScaling", "Stat Scaling" };

    private readonly IReadOnlyDictionary<string, string> _translations;
    private readonly IReadOnlyList<KeyValuePair<string, string>> _replacementEntries;
    private readonly HashSet<string> _itemBaseNames;
    private readonly IReadOnlyList<ItemAffixEntry> _itemAffixes;
    private readonly BoundedCache<string, TranslationResult> _translationCache;
    private readonly BoundedCache<ContextTranslationKey, TranslationResult> _contextTranslationCache;

    internal static RuntimeTextTranslator Empty { get; } =
        new RuntimeTextTranslator(NoTranslations);

    internal RuntimeTextTranslator(IReadOnlyDictionary<string, string> translations)
        : this(translations, Array.Empty<string>(), Array.Empty<string>())
    {
    }

    internal RuntimeTextTranslator(
        IReadOnlyDictionary<string, string> translations,
        IEnumerable<string> itemAffixes,
        IEnumerable<string> itemBaseNames,
        int cacheCapacity = DefaultCacheCapacity)
    {
        _translations = translations ?? NoTranslations;
        _replacementEntries = _translations
            .Where(pair => !string.IsNullOrEmpty(pair.Key) && pair.Key != pair.Value)
            .OrderByDescending(pair => pair.Key.Length)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();
        _itemBaseNames = new HashSet<string>(
            (itemBaseNames ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value) && _translations.ContainsKey(value)),
            StringComparer.Ordinal);
        _itemAffixes = (itemAffixes ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value) && _translations.ContainsKey(value))
            .Distinct(StringComparer.Ordinal)
            .Select(value => new ItemAffixEntry(value, SplitWords(value)))
            .OrderByDescending(entry => entry.Words.Length)
            .ThenByDescending(entry => entry.Source.Length)
            .ThenBy(entry => entry.Source, StringComparer.Ordinal)
            .ToList();
        _translationCache = new BoundedCache<string, TranslationResult>(
            cacheCapacity,
            StringComparer.Ordinal);
        _contextTranslationCache = new BoundedCache<ContextTranslationKey, TranslationResult>(
            cacheCapacity);
    }

    internal bool TryTranslate(string source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var cacheable = IsCacheable(source);
        if (cacheable && _translationCache.TryGetValue(source, out var cached))
        {
            translated = cached.Translated;
            return cached.Changed;
        }

        if (_translations.TryGetValue(source, out var exact) && exact != source)
        {
            translated = ApplyPartialTranslations(exact);
            var exactChanged = translated != source;
            if (cacheable)
            {
                _translationCache.TryAdd(source, new TranslationResult(exactChanged, translated));
            }
            return exactChanged;
        }

        var trimmed = source.Trim();
        if (trimmed.Length != source.Length &&
            _translations.TryGetValue(trimmed, out var replacement) &&
            replacement != trimmed)
        {
            var start = source.IndexOf(trimmed, StringComparison.Ordinal);
            translated = source.Substring(0, start) + ApplyPartialTranslations(replacement) +
                source.Substring(start + trimmed.Length);
            var trimmedChanged = translated != source;
            if (cacheable)
            {
                _translationCache.TryAdd(source, new TranslationResult(trimmedChanged, translated));
            }
            return trimmedChanged;
        }

        var changed = ContainsVisibleAsciiLetter(source) &&
            TryTranslateDynamic(source, out translated);
        if (!changed)
        {
            translated = source;
        }
        if (cacheable)
        {
            _translationCache.TryAdd(source, new TranslationResult(changed, translated));
        }
        return changed;
    }

    internal bool MayTranslate(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }
        if (_translations.ContainsKey(source))
        {
            return true;
        }

        var trimmed = source.Trim();
        return (trimmed.Length != source.Length && _translations.ContainsKey(trimmed)) ||
            ContainsVisibleAsciiLetter(source) ||
            CjkText.ContainsCjk(source);
    }

    internal bool TryTranslate(string source, string context, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }
        var cacheable = IsCacheable(source);
        var cacheKey = new ContextTranslationKey(source, context);
        if (cacheable && _contextTranslationCache.TryGetValue(cacheKey, out var cached))
        {
            translated = cached.Translated;
            return cached.Changed;
        }
        if (IsAlwaysProtectedUserTextContext(context, source) || IsChatLine(source))
        {
            return false;
        }

        var changed = TryTranslateWithContext(source, context, out translated);
        if (cacheable)
        {
            _contextTranslationCache.TryAdd(
                cacheKey,
                new TranslationResult(changed, translated));
        }
        return changed;
    }

    private bool TryTranslateWithContext(string source, string context, out string translated)
    {
        translated = source;
        if (IsWorldInteractionActionContext(context))
        {
            return TryTranslateWorldInteractionAction(source, out translated);
        }

        // Item composition must run before generic exact/dynamic translation so a
        // previously seen affix/base pair cannot mask the reviewed segment path.
        if (TryTranslateReviewedCompositeItemName(source, context, out translated))
        {
            return true;
        }
        if (IsPlayerNameTemplateContext(context))
        {
            return TryTranslatePlayerNameplate(source, out translated);
        }

        if (TryTranslate(source, out translated))
        {
            translated = CanonicalizeTranslatedStatScaling(translated, context);
            return true;
        }

        var visible = RemoveRichTextTags(source).Trim();
        if (visible.Length != source.Length &&
            _translations.TryGetValue(visible, out var visibleExact) &&
            visibleExact != visible)
        {
            translated = ApplyPartialTranslations(source);
            var expectedVisible = ApplyPartialTranslations(visibleExact);
            if (!CjkBoundarySpacePattern.IsMatch(expectedVisible))
            {
                translated = CjkRichBoundarySpacePattern.Replace(translated, "$1$2");
            }

            if (!RemoveRichTextTags(translated).Trim()
                    .Equals(expectedVisible, StringComparison.Ordinal))
            {
                translated = expectedVisible;
            }
            translated = CanonicalizeTranslatedStatScaling(translated, context);
            return translated != source;
        }
        if (IsProtectedUserTextContext(context))
        {
            return false;
        }
        if (IsCastAnnouncementContext(context) &&
            TryTranslateCastAnnouncement(source, visible, out translated))
        {
            return true;
        }

        var numberedMatch = NumberedDictionaryEntryPattern.Match(visible);
        if (numberedMatch.Success && _translations.ContainsKey(numberedMatch.Groups[1].Value))
        {
            translated = ApplyPartialTranslations(source);
            return translated != source;
        }

        if (TryTranslateSkillDamageScalingBlock(source, out translated))
        {
            return true;
        }
        if (IsStatScalingFragmentContext(context) &&
            IsSingleStatScalingFragment(source))
        {
            var scalingFragment = RewritePerTenStatScalingLines(source);
            if (scalingFragment != source)
            {
                translated = scalingFragment;
                return true;
            }
        }

        if (IsTrustedDescriptionContext(context))
        {
            var scalingCandidate = RewritePerTenStatScalingLines(source);
            if (scalingCandidate != source)
            {
                translated = NormalizeDescriptionUnits(ApplyPartialTranslations(scalingCandidate));
                return translated != source;
            }
            if (CjkText.ContainsCjk(source))
            {
                translated = RewritePerTenStatScalingLines(
                    NormalizeDescriptionUnits(ApplyPartialTranslations(source)));
                return translated != source;
            }
        }
        if (IsTrustedDescriptionContext(context) &&
            TryTranslateStructuredDescription(source, out translated))
        {
            return true;
        }
        return false;
    }

    private bool TryTranslateWorldInteractionAction(string source, out string translated)
    {
        translated = source;
        var visible = RemoveRichTextTags(source);
        var match = WorldInteractionActionPattern.Match(visible);
        if (!match.Success)
        {
            return false;
        }

        var action = match.Groups["action"];
        if (!_translations.TryGetValue(action.Value, out var target) || target == action.Value)
        {
            return false;
        }

        translated = ReplaceVisibleRangePreservingMarkup(
            source,
            action.Index,
            action.Length,
            target);
        return translated != source;
    }

    private bool TryTranslateReviewedCompositeItemName(
        string source,
        string context,
        out string translated)
    {
        translated = source;
        var richColorName = RichColorTagPattern.IsMatch(source);
        if (!IsItemNameContext(context) &&
            !(context.Equals("Name", StringComparison.OrdinalIgnoreCase) && richColorName))
        {
            return false;
        }

        var visible = RemoveRichTextTags(source);
        var tokens = VisibleTokenPattern.Matches(visible).Cast<Match>().ToArray();
        var affixStart = tokens.Length > 0 && IsUpgradeToken(tokens[0].Value) ? 1 : 0;
        if (tokens.Length - affixStart < 2)
        {
            return false;
        }

        for (var baseStart = affixStart + 1; baseStart < tokens.Length; baseStart++)
        {
            var baseSource = string.Join(
                " ",
                tokens.Skip(baseStart).Select(token => token.Value));
            if (!_itemBaseNames.Contains(baseSource) ||
                !_translations.TryGetValue(baseSource, out var baseTarget) ||
                baseTarget == baseSource)
            {
                continue;
            }

            var replacements = new List<VisibleReplacement>();
            if (!TrySegmentItemAffixes(tokens, affixStart, baseStart, replacements))
            {
                continue;
            }
            replacements.Add(new VisibleReplacement(
                tokens[baseStart].Index,
                tokens[tokens.Length - 1].Index + tokens[tokens.Length - 1].Length -
                    tokens[baseStart].Index,
                baseTarget));

            var candidate = source;
            foreach (var replacement in replacements.OrderByDescending(value => value.Start))
            {
                candidate = ReplaceVisibleRangePreservingMarkup(
                    candidate,
                    replacement.Start,
                    replacement.Length,
                    replacement.Target);
            }
            translated = candidate;
            return translated != source;
        }
        return false;
    }

    private bool TrySegmentItemAffixes(
        Match[] tokens,
        int position,
        int end,
        List<VisibleReplacement> replacements)
    {
        if (position == end)
        {
            return true;
        }

        foreach (var affix in _itemAffixes)
        {
            if (position + affix.Words.Length > end ||
                !MatchesWords(tokens, position, affix.Words))
            {
                continue;
            }
            if (!_translations.TryGetValue(affix.Source, out var target) || target == affix.Source)
            {
                continue;
            }
            var last = tokens[position + affix.Words.Length - 1];
            var replacement = new VisibleReplacement(
                tokens[position].Index,
                last.Index + last.Length - tokens[position].Index,
                target);
            replacements.Add(replacement);
            if (TrySegmentItemAffixes(
                    tokens,
                    position + affix.Words.Length,
                    end,
                    replacements))
            {
                return true;
            }
            replacements.RemoveAt(replacements.Count - 1);
        }
        return false;
    }

    private static bool MatchesWords(Match[] tokens, int start, string[] words)
    {
        for (var index = 0; index < words.Length; index++)
        {
            if (!tokens[start + index].Value.Equals(words[index], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
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

            var inRange = visibleIndex >= start && visibleIndex < end;
            if (inRange)
            {
                if (!inserted)
                {
                    builder.Append(replacement);
                    inserted = true;
                }
                if (source[index] == '\r' || source[index] == '\n' || source[index] == '\t')
                {
                    builder.Append(source[index]);
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

    private static bool IsUpgradeToken(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] != '+' || value.Length == 1)
        {
            return false;
        }
        for (var index = 1; index < value.Length; index++)
        {
            if (!char.IsDigit(value[index]))
            {
                return false;
            }
        }
        return true;
    }

    private static string[] SplitWords(string value)
    {
        return Regex.Split(value.Trim(), @"\s+")
            .Where(word => word.Length > 0)
            .ToArray();
    }

    private bool TryTranslateCastAnnouncement(string source, string visible, out string translated)
    {
        var candidateEnd = visible.Length;
        var punctuationCount = 0;
        while (candidateEnd > 0)
        {
            while (candidateEnd > 0 && char.IsWhiteSpace(visible[candidateEnd - 1]))
            {
                candidateEnd--;
            }
            if (candidateEnd == 0 ||
                (visible[candidateEnd - 1] != '!' && visible[candidateEnd - 1] != '！'))
            {
                break;
            }

            candidateEnd--;
            punctuationCount++;
            while (candidateEnd > 0 && char.IsWhiteSpace(visible[candidateEnd - 1]))
            {
                candidateEnd--;
            }

            var baseText = visible.Substring(0, candidateEnd);
            if (baseText.Length > 0 && !_translations.ContainsKey(baseText))
            {
                continue;
            }

            var expectedVisible = (baseText.Length == 0 ? string.Empty : TranslateFragment(baseText)) +
                new string('！', punctuationCount);
            var candidate = RewriteCastPunctuation(ApplyPartialTranslations(source));
            var candidateVisible = RemoveRichTextTags(candidate).Trim();
            translated = candidateVisible.Equals(expectedVisible, StringComparison.Ordinal)
                ? candidate
                : expectedVisible;
            return translated != source;
        }

        translated = source;
        return false;
    }

    private static string RewriteCastPunctuation(string source)
    {
        var visibleIndexes = new List<int>(source.Length);
        var insideTag = false;
        for (var index = 0; index < source.Length; index++)
        {
            var value = source[index];
            if (value == '<')
            {
                insideTag = true;
            }
            else if (value == '>')
            {
                insideTag = false;
            }
            else if (!insideTag)
            {
                visibleIndexes.Add(index);
            }
        }

        var removeIndexes = new HashSet<int>();
        var punctuationIndexes = new HashSet<int>();
        var cursor = visibleIndexes.Count - 1;
        while (cursor >= 0 && char.IsWhiteSpace(source[visibleIndexes[cursor]]))
        {
            removeIndexes.Add(visibleIndexes[cursor--]);
        }

        while (cursor >= 0 && IsCastExclamation(source[visibleIndexes[cursor]]))
        {
            punctuationIndexes.Add(visibleIndexes[cursor--]);
            while (cursor >= 0 && char.IsWhiteSpace(source[visibleIndexes[cursor]]))
            {
                removeIndexes.Add(visibleIndexes[cursor--]);
            }
        }
        if (punctuationIndexes.Count == 0)
        {
            return source;
        }

        var builder = new StringBuilder(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            if (removeIndexes.Contains(index))
            {
                continue;
            }
            builder.Append(punctuationIndexes.Contains(index) ? '！' : source[index]);
        }
        return builder.ToString();
    }

    private static bool IsCastExclamation(char value)
    {
        return value == '!' || value == '！';
    }

    private bool TryTranslateStructuredDescription(string source, out string translated)
    {
        var lineCount = source
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Count(line => !string.IsNullOrWhiteSpace(line));
        var matches = StructuredStatLinePattern.Matches(source);
        if (lineCount < 2 || matches.Count != lineCount)
        {
            translated = source;
            return false;
        }

        var labelsChanged = false;
        var rebuilt = StructuredStatLinePattern.Replace(source, match =>
        {
            var label = match.Groups["label"].Value;
            var localizedLabel = TranslateFragment(label);
            labelsChanged |= localizedLabel != label;
            return localizedLabel + "：" + match.Groups["value"].Value;
        });
        translated = NormalizeDescriptionUnits(ApplyPartialTranslations(rebuilt));
        translated = RewritePerTenStatScalingLines(translated);
        return labelsChanged && translated != source;
    }

    private bool TryTranslateDynamic(string source, out string translated)
    {
        Match match;
        if (TryTranslateFrequentDynamic(source, out translated))
        {
            return true;
        }

        var visibleSource = RemoveRichTextTags(source);
        match = GemDescriptionPattern.Match(visibleSource);
        if (match.Success &&
            _translations.ContainsKey(match.Groups[1].Value) &&
            _translations.ContainsKey(match.Groups[2].Value))
        {
            if (visibleSource.Length != source.Length)
            {
                translated = ApplyPartialTranslations(source);
                return translated != source;
            }
            translated = $"一颗闪耀的宝石，封存着一位昔日{TranslateFragment(match.Groups[1].Value)}的记忆。" +
                $"嵌入神器后，使用者的{TranslateFragment(match.Groups[2].Value)}将获得强化。";
            return true;
        }

        if (TryTranslateMonsterNameplate(source, out translated))
        {
            return true;
        }

        match = SkillLevelCostPattern.Match(visibleSource);
        if (match.Success && _translations.ContainsKey(match.Groups[1].Value))
        {
            if (visibleSource.Length != source.Length)
            {
                translated = ApplyPartialTranslations(source);
                return translated != source;
            }
            translated = $"{TranslateFragment(match.Groups[1].Value)} 等级{match.Groups[2].Value} " +
                $"[{match.Groups[3].Value} {TranslateFragment(match.Groups[4].Value)}]" +
                match.Groups[5].Value;
            return true;
        }

        match = MapLevelRangePattern.Match(visibleSource);
        if (match.Success)
        {
            var mapName = match.Groups[1].Value;
            var localizedMapName = TranslateFragment(mapName);
            if (localizedMapName != mapName)
            {
                translated = $"{localizedMapName}\n等级{match.Groups[2].Value}-{match.Groups[3].Value}";
                return translated != source;
            }
        }

        match = ArtifactSetPattern.Match(visibleSource);
        if (match.Success && _translations.ContainsKey(match.Groups[1].Value))
        {
            var expectedVisible = TranslateFragment(match.Groups[1].Value) + "神器套装";
            if (visibleSource.Length == source.Length)
            {
                translated = expectedVisible;
                return true;
            }

            var candidate = ApplyPartialTranslations(source);
            translated = RemoveRichTextTags(candidate).Trim()
                    .Equals(expectedVisible, StringComparison.Ordinal)
                ? candidate
                : expectedVisible;
            return translated != source;
        }

        match = UpgradePrefixPattern.Match(visibleSource);
        if (match.Success && _translations.ContainsKey(match.Groups[2].Value))
        {
            translated = match.Groups[1].Value + TranslateFragment(match.Groups[2].Value);
            return translated != source;
        }

        if (TryTranslatePlayerNameplate(source, out translated))
        {
            return true;
        }

        var trimmedVisibleSource = visibleSource.Trim();
        match = StandaloneLevelRangePattern.Match(trimmedVisibleSource);
        if (match.Success)
        {
            translated = $"等级{match.Groups[1].Value}-{match.Groups[2].Value}";
            return true;
        }

        match = LevelClassPattern.Match(trimmedVisibleSource);
        if (match.Success)
        {
            var monsterName = match.Groups[2].Value;
            var wrappedMonsterName = " " + monsterName + " ";
            var localizedMonsterName = ApplyPartialTranslations(wrappedMonsterName).Trim();
            if (localizedMonsterName == monsterName)
            {
                localizedMonsterName = TranslateMonsterName(monsterName);
            }
            if (localizedMonsterName != monsterName)
            {
                translated = $"等级{match.Groups[1].Value} {localizedMonsterName}";
                return true;
            }
        }

        match = ShortLevelPattern.Match(trimmedVisibleSource);
        if (match.Success)
        {
            var expectedVisible = "等级" + match.Groups[1].Value;
            var rawIndex = source.IndexOf(match.Value, StringComparison.Ordinal);
            translated = rawIndex >= 0
                ? source.Substring(0, rawIndex) + expectedVisible +
                    source.Substring(rawIndex + match.Value.Length)
                : expectedVisible;
            return true;
        }

        match = LocalizedPlaytimePattern.Match(source);
        if (match.Success)
        {
            translated = $"游戏时长：{match.Groups[1].Value}小时 {match.Groups[2].Value}分";
            return true;
        }

        match = CompactPlaytimePattern.Match(source);
        if (match.Success)
        {
            translated = $"{match.Groups[1].Value}小时 {match.Groups[2].Value}分";
            return true;
        }

        match = ResetTimerPattern.Match(source);
        if (match.Success)
        {
            translated = $"{match.Groups[1].Value}小时 {match.Groups[2].Value}分后重置";
            return true;
        }

        match = LevelValuePattern.Match(source);
        if (match.Success)
        {
            translated = $"等级 {match.Groups[1].Value}";
            return true;
        }

        match = WeightValuePattern.Match(source);
        if (match.Success)
        {
            translated = $"重量：{Regex.Replace(match.Groups[1].Value, @"\s+", string.Empty)}";
            return true;
        }

        match = ItemCountPattern.Match(source);
        if (match.Success)
        {
            translated = $"物品：{match.Groups[1].Value} / {match.Groups[2].Value}";
            return true;
        }

        match = VendingDurationPattern.Match(source);
        if (match.Success)
        {
            translated = $"摆摊持续 {match.Groups[1].Value} 小时。\n所有交易收取 {match.Groups[2].Value}% 税费。";
            return true;
        }

        match = ListingTermsPattern.Match(source);
        if (match.Success)
        {
            translated = $"商品将在 {match.Groups[1].Value} 小时后下架。\n" +
                $"上架费：{match.Groups[2].Value}%。销售税：{match.Groups[3].Value}%。";
            return true;
        }

        match = DismantleConfirmationPattern.Match(source);
        if (match.Success)
        {
            translated = $"确定要拆解\n{TranslateFragment(match.Groups[1].Value)}吗？";
            return true;
        }

        match = KillCountPattern.Match(source);
        if (match.Success)
        {
            translated = $"{TranslateFragment(match.Groups[1].Value)}：{match.Groups[2].Value}";
            return true;
        }

        match = source.StartsWith("Characters:", StringComparison.Ordinal)
            ? CharacterCountPattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = $"角色：{match.Groups[1].Value} / {match.Groups[2].Value}";
            return true;
        }

        match = source.StartsWith("Location:", StringComparison.Ordinal)
            ? LocationPattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = $"位置：{TranslateFragment(match.Groups[1].Value)}";
            return true;
        }

        match = source.StartsWith("位置", StringComparison.Ordinal)
            ? LocalizedLocationPattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = match.Groups[1].Value + TranslateFragment(match.Groups[2].Value);
            return translated != source;
        }

        match = source.StartsWith("Playtime:", StringComparison.Ordinal)
            ? PlaytimePattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = $"游戏时长：{match.Groups[1].Value}小时 {match.Groups[2].Value}分";
            return true;
        }

        match = source.StartsWith("Deaths:", StringComparison.Ordinal)
            ? DeathsPattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = $"死亡次数：{match.Groups[1].Value}";
            return true;
        }

        match = source.StartsWith("Game starts in:", StringComparison.Ordinal)
            ? GameStartsPattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = $"游戏将在 {LocalizeDuration(match.Groups[1].Value)} 后开始";
            return true;
        }

        match = source.StartsWith("Round Starts In:", StringComparison.Ordinal)
            ? RoundStartsPattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = $"回合将在 {LocalizeDuration(match.Groups[1].Value)} 后开始";
            return true;
        }

        match = source.StartsWith("Members:", StringComparison.Ordinal)
            ? PartySummaryPattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            var newline = source.IndexOf("\\n", StringComparison.Ordinal) >= 0
                ? "\\n"
                : source.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            translated = $"成员：{match.Groups[1].Value} / {match.Groups[2].Value}{newline}" +
                $"经验与掉落率：{match.Groups[3].Value.Trim()}{newline}" +
                $"等级范围：{match.Groups[4].Value.Trim()}";
            return true;
        }

        match = source.StartsWith("[", StringComparison.Ordinal)
            ? JustNowSalePattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = $"[刚刚] 已将 {TranslateFragment(match.Groups[1].Value)} " +
                $"售予 {match.Groups[2].Value}，售价 {TranslateFragment(match.Groups[3].Value)}";
            return true;
        }

        match = source.StartsWith("[", StringComparison.Ordinal)
            ? TimedSalePattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            var age = match.Groups[1].Value + LocalizeTimeUnit(match.Groups[2].Value) + "前";
            var item = TranslateFragment(match.Groups[3].Value);
            translated = $"[{age}] 已将 {item} 售予 {match.Groups[4].Value}，售价 " +
                TranslateFragment(match.Groups[5].Value);
            return true;
        }

        match = source.StartsWith("Sold ", StringComparison.Ordinal)
            ? SalePattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = $"已售出 {TranslateFragment(match.Groups[1].Value)}，获得 " +
                $"{match.Groups[2].Value} 金币";
            return true;
        }

        match = source.EndsWith(" party", StringComparison.Ordinal)
            ? PartyInvitePattern.Match(source)
            : Match.Empty;
        if (match.Success)
        {
            translated = $"{match.Groups[1].Value} 邀请你加入队伍";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateFrequentDynamic(string source, out string translated)
    {
        translated = source;
        Match match;
        if (source.Length > 2 && char.IsDigit(source[0]) &&
            source.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            var numericLength = source.Length - 2;
            var valid = true;
            for (var index = 0; index < numericLength; index++)
            {
                var character = source[index];
                if (!char.IsDigit(character) && character != '.' && character != ',')
                {
                    valid = false;
                    break;
                }
            }
            if (valid)
            {
                translated = source.Substring(0, numericLength) + "毫秒";
                return true;
            }
        }

        if (source.StartsWith("FPS:", StringComparison.Ordinal))
        {
            match = PerformancePattern.Match(source);
            if (match.Success)
            {
                translated = $"帧率：{match.Groups[1].Value}（{LocalizeMilliseconds(match.Groups[2].Value)}）  " +
                    $"延迟：{match.Groups[3].Value}  在线：{match.Groups[4].Value}";
                return true;
            }
        }

        if (source.StartsWith("Ping:", StringComparison.Ordinal))
        {
            match = NetworkSummaryPattern.Match(source);
            if (match.Success)
            {
                translated = $"延迟：{match.Groups[1].Value.Trim()} | 帧率：{match.Groups[2].Value.Trim()} | " +
                    $"在线：{match.Groups[3].Value.Trim()}";
                return true;
            }
        }

        if (source.StartsWith("HP ", StringComparison.Ordinal) ||
            source.StartsWith("MP ", StringComparison.Ordinal))
        {
            match = ResourceBarPattern.Match(source);
            if (match.Success)
            {
                var label = match.Groups[1].Value == "HP" ? "生命值" : "法力值";
                translated = $"{label} {match.Groups[2].Value} / {match.Groups[3].Value}";
                return true;
            }
        }

        if (source.StartsWith("Channel ", StringComparison.Ordinal))
        {
            match = ChannelPattern.Match(source);
            if (match.Success)
            {
                translated = $"频道 {match.Groups[1].Value} ({match.Groups[2].Value})";
                return true;
            }
        }

        return false;
    }

    internal bool TryTranslateMonsterNameplate(string source, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var visibleSource = RemoveRichTextTags(source);
        var match = MonsterNameplatePattern.Match(visibleSource);
        if (!match.Success)
        {
            return false;
        }

        var monsterName = match.Groups[1].Value;
        var affinity = match.Groups[3].Value;
        var localizedName = TranslateMonsterName(monsterName);
        var localizedAffinity = TranslateFragment(affinity);
        if (string.Equals(localizedName, monsterName, StringComparison.Ordinal) ||
            string.Equals(localizedAffinity, affinity, StringComparison.Ordinal))
        {
            return false;
        }

        if (visibleSource.Length != source.Length)
        {
            translated = TranslateRichMonsterNameplate(source, monsterName);
            return !string.Equals(translated, source, StringComparison.Ordinal);
        }

        translated = $"{localizedName}{match.Groups[2].Value}" +
            $"{localizedAffinity} 等级{match.Groups[4].Value}";
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static string RewritePerTenStatScalingLines(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var builder = new StringBuilder(source.Length + 16);
        var position = 0;
        var changed = false;
        while (position < source.Length)
        {
            var lineEnd = position;
            while (lineEnd < source.Length &&
                   source[lineEnd] != '\r' &&
                   source[lineEnd] != '\n')
            {
                lineEnd++;
            }

            var line = source.Substring(position, lineEnd - position);
            if (TryRewritePerTenStatScalingLine(line, out var rewritten))
            {
                builder.Append(rewritten);
                changed = true;
            }
            else
            {
                builder.Append(line);
            }

            if (lineEnd >= source.Length)
            {
                break;
            }
            if (source[lineEnd] == '\r' &&
                lineEnd + 1 < source.Length &&
                source[lineEnd + 1] == '\n')
            {
                builder.Append("\r\n");
                position = lineEnd + 2;
            }
            else
            {
                builder.Append(source[lineEnd]);
                position = lineEnd + 1;
            }
        }
        return changed ? builder.ToString() : source;
    }

    internal static string CanonicalizePerTenStatScaling(string source)
    {
        return RewritePerTenStatScalingLines(source);
    }

    private static bool TryTranslateSkillDamageScalingBlock(
        string source,
        out string translated)
    {
        translated = source;
        if (!IsSkillDamageScalingBlock(source))
        {
            return false;
        }

        translated = RewritePerTenStatScalingLines(source);
        return translated != source;
    }

    private static string CanonicalizeTranslatedStatScaling(
        string translated,
        string context)
    {
        if (IsProtectedUserTextContext(context))
        {
            return translated;
        }
        if (TryTranslateSkillDamageScalingBlock(translated, out var canonicalBlock))
        {
            return canonicalBlock;
        }
        if (IsStatScalingFragmentContext(context) &&
            IsSingleStatScalingFragment(translated))
        {
            return RewritePerTenStatScalingLines(translated);
        }
        return translated;
    }

    internal static bool IsSkillDamageScalingBlock(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var visibleLines = RemoveRichTextTags(source)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (visibleLines.Length < 3)
        {
            return false;
        }

        for (var headerIndex = 0; headerIndex < visibleLines.Length - 2; headerIndex++)
        {
            if (!IsSkillDamageHeader(visibleLines[headerIndex]))
            {
                continue;
            }

            var scalingLineCount = 0;
            var hasOldOrder = false;
            for (var lineIndex = headerIndex + 1;
                 lineIndex < visibleLines.Length;
                 lineIndex++)
            {
                var line = visibleLines[lineIndex];
                if (string.IsNullOrWhiteSpace(line) ||
                    !PerTenStatScalingPattern.IsMatch(line))
                {
                    break;
                }

                scalingLineCount++;
                hasOldOrder |= TryRewritePerTenStatScalingLine(line, out _);
            }
            if (scalingLineCount >= 2 && hasOldOrder)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsSkillDamageHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed == "[技能伤害]:" ||
            trimmed == "[技能伤害]：" ||
            trimmed == "技能伤害:" ||
            trimmed == "技能伤害：";
    }

    private static bool TryRewritePerTenStatScalingLine(
        string source,
        out string rewritten)
    {
        rewritten = source;
        var outerPrefix = string.Empty;
        var outerSuffix = string.Empty;
        var inner = source;
        while (TryPeelOuterRichTextWrapper(
                   inner,
                   out var prefix,
                   out var content,
                   out var suffix))
        {
            outerPrefix += prefix;
            outerSuffix = suffix + outerSuffix;
            inner = content;
        }

        var visible = RemoveRichTextTags(inner);
        var match = PerTenStatScalingPattern.Match(visible);
        if (!match.Success)
        {
            return false;
        }

        if (!TryExtractRawVisibleSlice(inner, match.Groups["gain"], out var gain) ||
            !TryExtractRawVisibleSlice(inner, match.Groups["stat"], out var stat) ||
            !TryExtractRawVisibleSlice(inner, match.Groups["tail"], out var tail) ||
            new[] { gain, stat, tail }.OrderBy(value => value.Start)
                .Zip(new[] { gain, stat, tail }.OrderBy(value => value.Start).Skip(1),
                    (left, right) => left.End > right.Start).Any(value => value))
        {
            return TryRewritePerTenStatScalingSiblingSpans(
                source,
                inner,
                outerPrefix,
                outerSuffix,
                match,
                out rewritten);
        }

        var localizedStat = TranslatePerTenStat(stat.Text, match.Groups["stat"].Value);
        if (localizedStat == null)
        {
            return false;
        }

        var orderedSlices = new[] { gain, stat, tail }
            .OrderBy(value => value.Start)
            .ToArray();
        var outsideGroups = new List<string>();
        var cursor = 0;
        foreach (var slice in orderedSlices)
        {
            outsideGroups.Add(inner.Substring(cursor, slice.Start - cursor));
            cursor = slice.End;
        }
        outsideGroups.Add(inner.Substring(cursor));
        if (outsideGroups.Any(value => value.IndexOf('<') >= 0 || value.IndexOf('>') >= 0))
        {
            return TryRewritePerTenStatScalingSiblingSpans(
                source,
                inner,
                outerPrefix,
                outerSuffix,
                match,
                out rewritten);
        }

        rewritten = outerPrefix + match.Groups["leading"].Value +
            "每 10 点" + localizedStat + " " + gain.Text + " " + tail.Text +
            match.Groups["trailing"].Value + outerSuffix;
        return rewritten != source;
    }

    private static bool TryRewritePerTenStatScalingSiblingSpans(
        string source,
        string inner,
        string outerPrefix,
        string outerSuffix,
        Match match,
        out string rewritten)
    {
        rewritten = source;
        if (!TryExtractRawVisibleSlice(inner, match.Groups["body"], out var body) ||
            !TryExtractRawVisibleSlice(inner, match.Groups["tail"], out var tail) ||
            body.End > tail.Start)
        {
            return false;
        }

        var beforeBody = inner.Substring(0, body.Start);
        var betweenBodyAndTail = inner.Substring(body.End, tail.Start - body.End);
        var afterTail = inner.Substring(tail.End);
        if (new[] { beforeBody, betweenBodyAndTail, afterTail }
            .Any(value => value.IndexOf('<') >= 0 || value.IndexOf('>') >= 0))
        {
            return false;
        }

        var bodyPrefix = string.Empty;
        var bodySuffix = string.Empty;
        var bodyInner = body.Text;
        while (TryPeelOuterRichTextWrapper(
                   bodyInner,
                   out var prefix,
                   out var content,
                   out var suffix))
        {
            bodyPrefix += prefix;
            bodySuffix = suffix + bodySuffix;
            bodyInner = content;
        }

        var gainGroup = match.Groups["gain"];
        var statGroup = match.Groups["stat"];
        var bodyGroup = match.Groups["body"];
        if (!TryExtractRawVisibleSlice(
                bodyInner,
                gainGroup.Index - bodyGroup.Index,
                gainGroup.Length,
                gainGroup.Value,
                out var gain) ||
            !TryExtractRawVisibleSlice(
                bodyInner,
                statGroup.Index - bodyGroup.Index,
                statGroup.Length,
                statGroup.Value,
                out var stat))
        {
            return false;
        }

        var first = gain.Start < stat.Start ? gain : stat;
        var second = gain.Start < stat.Start ? stat : gain;
        var bodyOutside = new[]
        {
            bodyInner.Substring(0, first.Start),
            bodyInner.Substring(first.End, second.Start - first.End),
            bodyInner.Substring(second.End),
        };
        if (bodyOutside.Any(value => value.IndexOf('<') >= 0 || value.IndexOf('>') >= 0))
        {
            return false;
        }

        var localizedStat = TranslatePerTenStat(stat.Text, statGroup.Value);
        if (localizedStat == null)
        {
            return false;
        }

        var bodyTrailing = bodyInner.Substring(second.End);
        var rewrittenBody = bodyPrefix + "每 10 点" + localizedStat + " " + gain.Text +
            bodyTrailing + bodySuffix;
        var visibleBody = RemoveRichTextTags(rewrittenBody);
        var separator = visibleBody.Length > 0 &&
            !char.IsWhiteSpace(visibleBody[visibleBody.Length - 1])
                ? " "
                : string.Empty;
        rewritten = outerPrefix + match.Groups["leading"].Value + rewrittenBody + separator +
            tail.Text + match.Groups["trailing"].Value + outerSuffix;
        return rewritten != source;
    }

    private static string TranslatePerTenStat(string rawStat, string visibleStat)
    {
        string target;
        switch (visibleStat.ToUpperInvariant())
        {
            case "力量":
            case "STRENGTH":
            case "STR":
                target = "力量";
                break;
            case "灵巧":
            case "DEXTERITY":
            case "DEX":
                target = "灵巧";
                break;
            case "敏捷":
            case "AGILITY":
            case "AGI":
                target = "敏捷";
                break;
            case "智力":
            case "INTELLIGENCE":
            case "INT":
                target = "智力";
                break;
            case "体质":
            case "活力":
            case "VITALITY":
            case "VIT":
                target = "体质";
                break;
            case "幸运":
            case "LUCK":
            case "LUK":
                target = "幸运";
                break;
            default:
                return null;
        }

        var visibleLength = RemoveRichTextTags(rawStat).Length;
        return ReplaceVisibleRangePreservingMarkup(rawStat, 0, visibleLength, target);
    }

    private static bool TryPeelOuterRichTextWrapper(
        string source,
        out string prefix,
        out string content,
        out string suffix)
    {
        prefix = string.Empty;
        content = source;
        suffix = string.Empty;
        var match = OuterRichTextWrapperPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }
        var wrapperDepth = 0;
        var matchingCloseIndex = -1;
        foreach (Match tagMatch in RichTagTokenPattern.Matches(source))
        {
            if (!tagMatch.Groups["name"].Value.Equals(
                    match.Groups["tag"].Value,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            wrapperDepth += tagMatch.Groups["closing"].Success ? -1 : 1;
            if (wrapperDepth == 0)
            {
                matchingCloseIndex = tagMatch.Index;
                break;
            }
        }
        if (matchingCloseIndex != match.Groups["close"].Index)
        {
            return false;
        }
        prefix = match.Groups["leading"].Value + match.Groups["open"].Value;
        content = match.Groups["content"].Value;
        suffix = match.Groups["close"].Value + match.Groups["trailing"].Value;
        return true;
    }

    private static bool TryExtractRawVisibleSlice(
        string source,
        Group group,
        out RawVisibleSlice slice)
    {
        return TryExtractRawVisibleSlice(
            source,
            group.Index,
            group.Length,
            group.Value,
            out slice);
    }

    private static bool TryExtractRawVisibleSlice(
        string source,
        int visibleStart,
        int visibleLength,
        string expectedVisible,
        out RawVisibleSlice slice)
    {
        slice = null;
        var rawIndexes = new List<int>();
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '<')
            {
                var tagEnd = source.IndexOf('>', index + 1);
                if (tagEnd >= 0)
                {
                    index = tagEnd;
                    continue;
                }
            }
            rawIndexes.Add(index);
        }
        if (visibleLength == 0 ||
            visibleStart < 0 ||
            visibleStart + visibleLength > rawIndexes.Count)
        {
            return false;
        }

        var rawStart = rawIndexes[visibleStart];
        var rawEnd = rawIndexes[visibleStart + visibleLength - 1] + 1;
        while (rawStart > 0 && source[rawStart - 1] == '>')
        {
            var tagStart = source.LastIndexOf('<', rawStart - 1);
            if (tagStart < 0 ||
                tagStart + 1 >= source.Length ||
                source[tagStart + 1] == '/')
            {
                break;
            }
            rawStart = tagStart;
        }
        while (rawEnd < source.Length && source[rawEnd] == '<')
        {
            var tagEnd = source.IndexOf('>', rawEnd + 1);
            if (tagEnd < 0 ||
                rawEnd + 1 >= source.Length ||
                source[rawEnd + 1] != '/')
            {
                break;
            }
            rawEnd = tagEnd + 1;
        }

        var text = source.Substring(rawStart, rawEnd - rawStart);
        if (!RemoveRichTextTags(text)
                .Equals(expectedVisible, StringComparison.Ordinal) ||
            !HasBalancedRichTags(text))
        {
            return false;
        }
        slice = new RawVisibleSlice(rawStart, rawEnd, text);
        return true;
    }

    private static bool HasBalancedRichTags(string source)
    {
        var stack = new Stack<string>();
        foreach (Match match in RichTagTokenPattern.Matches(source))
        {
            var name = match.Groups["name"].Value;
            if (name.Equals("sprite", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("br", StringComparison.OrdinalIgnoreCase) ||
                match.Value.EndsWith("/>", StringComparison.Ordinal))
            {
                continue;
            }
            if (!match.Groups["closing"].Success)
            {
                stack.Push(name);
                continue;
            }
            if (stack.Count == 0 ||
                !stack.Pop().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return stack.Count == 0;
    }

    private bool TryTranslatePlayerNameplate(string source, out string translated)
    {
        translated = source;
        var match = PlayerNameplatePattern.Match(source);
        if (!match.Success || !_translations.ContainsKey(match.Groups[4].Value))
        {
            return false;
        }
        translated = $"{match.Groups[1].Value}{match.Groups[2].Value}等级{match.Groups[3].Value} " +
            TranslateFragment(match.Groups[4].Value);
        return translated != source;
    }

    private string TranslateFragment(string source)
    {
        var current = source;
        if (_translations.TryGetValue(source, out var exact) && exact != source)
        {
            current = exact;
        }
        return ApplyPartialTranslations(current);
    }

    private string TranslateMonsterName(string source)
    {
        const string giantPrefix = "Giant ";
        if (source.StartsWith(giantPrefix, StringComparison.Ordinal))
        {
            var baseName = source.Substring(giantPrefix.Length);
            if (_translations.ContainsKey(baseName))
            {
                return "巨型" + TranslateFragment(baseName);
            }
        }
        return TranslateFragment(source);
    }

    private string TranslateRichMonsterNameplate(string source, string visibleMonsterName)
    {
        var translated = ApplyPartialTranslations(source);
        const string giantPrefix = "Giant ";
        if (!visibleMonsterName.StartsWith(giantPrefix, StringComparison.Ordinal) ||
            !_translations.ContainsKey(visibleMonsterName.Substring(giantPrefix.Length)))
        {
            return translated;
        }

        return ReplaceBounded(translated, giantPrefix, "巨型");
    }

    private string ApplyPartialTranslations(string text)
    {
        if (!ContainsVisibleAsciiLetter(text))
        {
            return text;
        }

        var current = text;
        foreach (var entry in _replacementEntries)
        {
            if (entry.Key.Length >= current.Length ||
                current.IndexOf(entry.Key, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            current = ReplaceBounded(current, entry.Key, entry.Value);
        }
        return current;
    }

    private static bool IsItemNameContext(string context)
    {
        return ContainsContext(context, ItemNameContextMarkers);
    }

    private static bool IsPlayerNameTemplateContext(string context)
    {
        return ContainsContext(context, PlayerNameTemplateContextMarkers);
    }

    private static bool IsProtectedUserTextContext(string context)
    {
        if (string.IsNullOrEmpty(context))
        {
            return false;
        }
        if (IsTrustedSystemNameContext(context))
        {
            return false;
        }
        return context.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
            ContainsContext(context, ProtectedUserContextMarkers);
    }

    private static bool IsCastAnnouncementContext(string context)
    {
        return !string.IsNullOrEmpty(context) &&
            context.IndexOf("CastName", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsWorldInteractionActionContext(string context)
    {
        return !string.IsNullOrEmpty(context) &&
            context.StartsWith("WorldInteractionAction:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrustedDescriptionContext(string context)
    {
        if (string.IsNullOrEmpty(context))
        {
            return false;
        }
        return context.IndexOf("Description", StringComparison.OrdinalIgnoreCase) >= 0 ||
            context.IndexOf("Tooltip", StringComparison.OrdinalIgnoreCase) >= 0 ||
            context.Equals("Text_Info", StringComparison.OrdinalIgnoreCase) ||
            context.Equals("Info", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStatScalingFragmentContext(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return true;
        }
        return IsTrustedDescriptionContext(context) ||
            context.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
            context.Equals("Value", StringComparison.OrdinalIgnoreCase) ||
            ContainsContext(context, StatScalingContextMarkers);
    }

    private static bool IsSingleStatScalingFragment(string source)
    {
        var visible = RemoveRichTextTags(source);
        return visible.IndexOf('\r') < 0 &&
            visible.IndexOf('\n') < 0 &&
            PerTenStatScalingPattern.IsMatch(visible);
    }

    private static bool IsAlwaysProtectedUserTextContext(string context, string source)
    {
        if (string.IsNullOrEmpty(context))
        {
            return false;
        }
        if (IsTrustedSystemNameContext(context))
        {
            return false;
        }
        return CompactStartsWith(context, "userinput") ||
            (CompactContains(context, "displayname") &&
                !CompactEquals(source, "displayname")) ||
            ContainsContext(context, AlwaysProtectedUserContextMarkers);
    }

    internal static bool ShouldSuppressUntranslatedCapture(string context, string source)
    {
        return IsAlwaysProtectedUserTextContext(context, source ?? string.Empty) ||
            IsProtectedUserTextContext(context) ||
            IsChatLine(source);
    }

    private static bool IsTrustedSystemNameContext(string context)
    {
        return context.StartsWith("ItemName:", StringComparison.OrdinalIgnoreCase) ||
            context.StartsWith("ClassName:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsContext(string context, params string[] values)
    {
        if (string.IsNullOrEmpty(context))
        {
            return false;
        }
        foreach (var value in values)
        {
            if (context.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static bool CompactStartsWith(string value, string expected)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var expectedIndex = 0;
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }
            if (expectedIndex >= expected.Length)
            {
                return true;
            }
            if (char.ToLowerInvariant(character) != expected[expectedIndex])
            {
                return false;
            }
            expectedIndex++;
        }
        return expectedIndex == expected.Length;
    }

    private static bool CompactContains(string value, string expected)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        for (var start = 0; start < value.Length; start++)
        {
            if (!char.IsLetterOrDigit(value[start]) ||
                char.ToLowerInvariant(value[start]) != expected[0])
            {
                continue;
            }

            var expectedIndex = 0;
            for (var index = start; index < value.Length; index++)
            {
                var character = value[index];
                if (!char.IsLetterOrDigit(character))
                {
                    continue;
                }
                if (char.ToLowerInvariant(character) != expected[expectedIndex])
                {
                    break;
                }
                expectedIndex++;
                if (expectedIndex == expected.Length)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool CompactEquals(string value, string expected)
    {
        if (value == null)
        {
            return false;
        }

        var expectedIndex = 0;
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }
            if (expectedIndex >= expected.Length ||
                char.ToLowerInvariant(character) != expected[expectedIndex])
            {
                return false;
            }
            expectedIndex++;
        }
        return expectedIndex == expected.Length;
    }

    internal int CachedTranslationCount => _translationCache.Count;
    internal int CachedContextTranslationCount => _contextTranslationCache.Count;

    private readonly struct TranslationResult
    {
        internal TranslationResult(bool changed, string translated)
        {
            Changed = changed;
            Translated = translated;
        }

        internal bool Changed { get; }
        internal string Translated { get; }
    }

    private readonly struct ContextTranslationKey : IEquatable<ContextTranslationKey>
    {
        private readonly string _source;
        private readonly string _context;

        internal ContextTranslationKey(string source, string context)
        {
            _source = source;
            _context = context;
        }

        public bool Equals(ContextTranslationKey other)
        {
            return string.Equals(_source, other._source, StringComparison.Ordinal) &&
                string.Equals(_context, other._context, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ContextTranslationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(_source ?? string.Empty) * 397) ^
                    StringComparer.Ordinal.GetHashCode(_context ?? string.Empty);
            }
        }
    }

    private sealed class BoundedCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, TValue> _values;
        private readonly Queue<TKey> _insertionOrder;
        private readonly object _gate = new object();

        internal BoundedCache(int capacity, IEqualityComparer<TKey> comparer = null)
        {
            _capacity = Math.Max(0, capacity);
            _values = new Dictionary<TKey, TValue>(comparer);
            _insertionOrder = new Queue<TKey>();
        }

        internal int Count
        {
            get
            {
                lock (_gate)
                {
                    return _values.Count;
                }
            }
        }

        internal bool TryGetValue(TKey key, out TValue value)
        {
            lock (_gate)
            {
                return _values.TryGetValue(key, out value);
            }
        }

        internal bool TryAdd(TKey key, TValue value)
        {
            if (_capacity == 0)
            {
                return false;
            }

            lock (_gate)
            {
                if (_values.ContainsKey(key))
                {
                    return false;
                }
                while (_values.Count >= _capacity && _insertionOrder.Count != 0)
                {
                    _values.Remove(_insertionOrder.Dequeue());
                }
                _values.Add(key, value);
                _insertionOrder.Enqueue(key);
                return true;
            }
        }
    }

    private sealed class ItemAffixEntry
    {
        internal ItemAffixEntry(string source, string[] words)
        {
            Source = source;
            Words = words;
        }

        internal string Source { get; }
        internal string[] Words { get; }
    }

    private sealed class RawVisibleSlice
    {
        internal RawVisibleSlice(int start, int end, string text)
        {
            Start = start;
            End = end;
            Text = text;
        }

        internal int Start { get; }
        internal int End { get; }
        internal string Text { get; }
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

    private static bool ContainsVisibleAsciiLetter(string text)
    {
        var insideTag = false;
        foreach (var value in text)
        {
            if (value == '<')
            {
                insideTag = true;
            }
            else if (value == '>')
            {
                insideTag = false;
            }
            else if (!insideTag && IsAsciiLetter(value))
            {
                return true;
            }
        }
        return false;
    }

    private static string RemoveRichTextTags(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
        {
            return text ?? string.Empty;
        }
        return RichTextTagPattern.Replace(text, string.Empty);
    }

    private static bool IsCacheable(string source)
    {
        return !string.IsNullOrEmpty(source) &&
            source.Length <= MaximumCacheableSourceLength;
    }

    private static bool IsChatLine(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 5 || !char.IsDigit(value[0]))
        {
            return false;
        }

        var colon = value.Length > 1 && value[1] == ':'
            ? 1
            : value.Length > 2 && char.IsDigit(value[1]) && value[2] == ':' ? 2 : -1;
        return colon >= 0 && value.Length > colon + 3 &&
            char.IsDigit(value[colon + 1]) &&
            char.IsDigit(value[colon + 2]) &&
            char.IsWhiteSpace(value[colon + 3]);
    }

    private static string ReplaceBounded(string text, string source, string target)
    {
        var first = text.IndexOf(source, StringComparison.Ordinal);
        if (first < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var cursor = 0;
        var changed = false;
        while (first >= 0)
        {
            if (IsValidMatch(text, source, first))
            {
                builder.Append(text, cursor, first - cursor);
                builder.Append(target);
                cursor = first + source.Length;
                changed = true;
            }

            var nextStart = Math.Max(first + source.Length, cursor);
            first = text.IndexOf(source, nextStart, StringComparison.Ordinal);
        }

        if (!changed)
        {
            return text;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return builder.ToString();
    }

    private static bool IsValidMatch(string text, string source, int index)
    {
        var end = index + source.Length;
        if (IsAsciiLetter(source[0]) && index > 0 && IsAsciiLetter(text[index - 1]))
        {
            return false;
        }
        if (IsAsciiLetter(source[source.Length - 1]) && end < text.Length && IsAsciiLetter(text[end]))
        {
            return false;
        }

        var lastOpen = text.LastIndexOf('<', index);
        var lastClose = text.LastIndexOf('>', index);
        return lastOpen <= lastClose;
    }

    private static string LocalizeDuration(string value)
    {
        if (value.EndsWith("s", StringComparison.Ordinal) &&
            int.TryParse(value.Substring(0, value.Length - 1), out var seconds))
        {
            return seconds + "秒";
        }
        return value;
    }

    private static string NormalizeCjkSpacing(string value)
    {
        return CjkBoundarySpacePattern.Replace(value, string.Empty);
    }

    private static string NormalizeDescriptionUnits(string value)
    {
        var normalized = NumericSecondsPerLevelPattern.Replace(value, " 秒/级");
        normalized = NumericSecondsPattern.Replace(normalized, " 秒");
        normalized = NumericCompactSecondsPattern.Replace(normalized, "秒");
        normalized = NumericManaPattern.Replace(normalized, " 法力值");
        return NormalizeCjkSpacing(normalized);
    }

    private static string LocalizeMilliseconds(string value)
    {
        return value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            ? value.Substring(0, value.Length - 2) + "毫秒"
            : value;
    }

    private static string LocalizeTimeUnit(string unit)
    {
        switch (unit.TrimEnd('s', 'S').ToLowerInvariant())
        {
            case "second": return "秒";
            case "minute": return "分钟";
            case "hour": return "小时";
            case "day": return "天";
            case "week": return "周";
            case "month": return "个月";
            case "year": return "年";
            default: return unit;
        }
    }

    private static bool IsAsciiLetter(char value)
    {
        return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
    }
}
