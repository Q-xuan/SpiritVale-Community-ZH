using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpiritVale.RuntimeLocalization;

var dictionaryPath = args.Length == 0
    ? Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "artifacts", "translations-market-index-candidate.tsv"))
    : Path.GetFullPath(args[0]);
var rows = LoadCatalog(dictionaryPath);
var bridge = new MarketSearchQueryBridge(rows);
var passed = 0;

Check("catalog has 2558 canonical entries", rows.Count == 2558);
Check("catalog has 1948 unique market source names", rows.Select(row => row.Source).Distinct(StringComparer.Ordinal).Count() == 1948);

var goldByChinese = Resolve("金");
Check("single Chinese 金 includes Gold Ore", goldByChinese.Contains(Id("Junk", "Gold Ore")));
var goldByAlias = Resolve("黄金");
Check("concept alias 黄金 includes Gold Ore", goldByAlias.Contains(Id("Junk", "Gold Ore")));
var englishOutcome = bridge.TryResolveIdentities(
    MarketSearchQueryBridge.SupportedManagerType,
    MarketSearchQueryBridge.SupportedRequestMethod,
    "gold",
    out var englishIdentities);
Check("English gold is passed to the original game unchanged",
    englishOutcome == MarketSearchIndexOutcome.Unchanged && englishIdentities.Count == 0);
Check("canonical catalog retains Gold Ore for original English search",
    rows.Any(row => row.Source == "Gold Ore" && row.Identity.Equals(Id("Junk", "Gold Ore"))));

var storms = Resolve("风暴");
Check("风暴 includes Stormburst Crossbow equip", storms.Contains(Id("Equip", "Stormburst Crossbow")));
Check("风暴 includes Tempest Staff equip", storms.Contains(Id("Equip", "Tempest Staff")));
var cacti = Resolve("仙人掌");
Check("仙人掌 includes Cactus card", cacti.Contains(Id("Card", "Cactus")));
Check("仙人掌 includes Cacti card", cacti.Contains(Id("Card", "Cacti")));

var exactGoldOre = Resolve("金矿石");
Check("complete Chinese name includes its canonical identity", exactGoldOre.Contains(Id("Junk", "Gold Ore")));
Check("unknown Chinese yields a local empty result", Resolve("绝对不存在的市场物品").Count == 0);
Check("single CJK characters are indexed", Resolve("矿").Contains(Id("Junk", "Gold Ore")));
Check("rich text is visible-text normalized", Resolve("<color=#FFD700>黄金</color>").Contains(Id("Junk", "Gold Ore")));
Check("full-width and whitespace are market-local normalized", Resolve("　黄 金　").Contains(Id("Junk", "Gold Ore")));
Check("IME zero-width suffix is removed", Resolve("金\u200B").Contains(Id("Junk", "Gold Ore")));
Check("format characters are removed with whitespace",
    MarketSearchQueryBridge.NormalizeMarketText("\u200B　黄\u200D 金\uFEFF") == "黄金");

var committedJinQuery = MarketSearchQueryBridge.SelectCallbackQuery("jin\u200B", "金\u200B");
Check("committed Chinese field wins over pinyin CurrentSearch",
    committedJinQuery == "金\u200B" &&
    MarketSearchQueryBridge.NormalizeMarketText(committedJinQuery) == "金");
var committedHuangQuery = MarketSearchQueryBridge.SelectCallbackQuery("huang", "黄金");
Check("committed Chinese alias field wins over pinyin CurrentSearch",
    committedHuangQuery == "黄金");
var englishCallbackQuery = MarketSearchQueryBridge.SelectCallbackQuery("gold\u200B", "gold\u200B");
var englishCallbackOutcome = bridge.TryResolveIdentities(
    MarketSearchQueryBridge.SupportedDeclaringType,
    MarketSearchQueryBridge.SupportedCallbackMethod,
    englishCallbackQuery,
    out var englishCallbackIdentities);
Check("visible English field does not enter the local canonical index",
    englishCallbackOutcome == MarketSearchIndexOutcome.Unchanged &&
    englishCallbackIdentities.Count == 0);
Check("visible English is retained when CurrentSearch is null",
    MarketSearchQueryBridge.SelectCallbackQuery(null, "gold\u200B") == "gold\u200B");
var nullFieldQuery = MarketSearchQueryBridge.SelectCallbackQuery("jin\u200B", null);
var nullFieldOutcome = bridge.TryResolveIdentities(
    MarketSearchQueryBridge.SupportedDeclaringType,
    MarketSearchQueryBridge.SupportedCallbackMethod,
    nullFieldQuery,
    out var nullFieldIdentities);
Check("null SearchField fails open through non-CJK CurrentSearch",
    nullFieldQuery == "jin\u200B" &&
    nullFieldOutcome == MarketSearchIndexOutcome.Unchanged &&
    nullFieldIdentities.Count == 0);

var callbackOutcome = bridge.TryResolveIdentities(
    MarketSearchQueryBridge.SupportedDeclaringType,
    MarketSearchQueryBridge.SupportedCallbackMethod,
    committedJinQuery,
    out var callbackGold);
Check("client result callback resolves zero-width Chinese query",
    callbackOutcome == MarketSearchIndexOutcome.Matched &&
    callbackGold.Contains(Id("Junk", "Gold Ore")));

var goldA = new FakeMarketItem("listing-gold-a", "Junk", "Gold Ore");
var goldB = new FakeMarketItem("listing-gold-b", "Junk", "Gold Ore");
var serverOriginal = new FakeMarketItem("listing-server-original", "Equip", "Server Result");
var unrelated = new FakeMarketItem("listing-unrelated", "Equip", "Unrelated Item");
var snapshot = new MarketSearchSnapshot<FakeMarketItem>();
snapshot.Capture(17, new[] { goldA, goldB, unrelated });
Check("full snapshot is isolated to its UIVendingSearch instance",
    snapshot.TryGet(17, out var captured) && captured.Count == 3 &&
    !snapshot.TryGet(18, out _));

var nullFieldMergeOutcome = bridge.TryMergeSnapshot(
    MarketSearchQueryBridge.SupportedDeclaringType,
    MarketSearchQueryBridge.SupportedCallbackMethod,
    nullFieldQuery,
    new[] { serverOriginal },
    captured,
    item => item.Identity,
    item => item.ListingId,
    out var nullFieldResults);
Check("null SearchField keeps original callback results unchanged",
    nullFieldMergeOutcome == MarketSearchMergeOutcome.Unchanged &&
    nullFieldResults.SequenceEqual(new[] { serverOriginal }));

var mergeOutcome = bridge.TryMergeSnapshot(
    MarketSearchQueryBridge.SupportedDeclaringType,
    MarketSearchQueryBridge.SupportedCallbackMethod,
    committedJinQuery,
    new[] { serverOriginal, goldA },
    captured,
    item => item.Identity,
    item => item.ListingId,
    out var mergedGold);
Check("callback merge preserves original order and appends canonical snapshot matches",
    mergeOutcome == MarketSearchMergeOutcome.Merged &&
    mergedGold.SequenceEqual(new[] { serverOriginal, goldA, goldB }));
Check("callback merge deduplicates by stable Listing.Id",
    mergedGold.Count(item => item.ListingId == goldA.ListingId) == 1);

var noMatchOutcome = bridge.TryMergeSnapshot(
    MarketSearchQueryBridge.SupportedDeclaringType,
    MarketSearchQueryBridge.SupportedCallbackMethod,
    "绝对不存在的市场物品\u200B",
    new[] { serverOriginal },
    captured,
    item => item.Identity,
    item => item.ListingId,
    out var noMatchResults);
Check("NoMatch leaves original callback results unchanged",
    noMatchOutcome == MarketSearchMergeOutcome.NoMatch &&
    noMatchResults.SequenceEqual(new[] { serverOriginal }));

var englishMergeOutcome = bridge.TryMergeSnapshot(
    MarketSearchQueryBridge.SupportedDeclaringType,
    MarketSearchQueryBridge.SupportedCallbackMethod,
    "gold\u200B",
    new[] { serverOriginal },
    captured,
    item => item.Identity,
    item => item.ListingId,
    out var englishResults);
Check("English callback query remains byte-for-byte game-owned",
    englishMergeOutcome == MarketSearchMergeOutcome.Unchanged &&
    englishResults.SequenceEqual(new[] { serverOriginal }));

var noSnapshotOutcome = bridge.TryMergeSnapshot(
    MarketSearchQueryBridge.SupportedDeclaringType,
    MarketSearchQueryBridge.SupportedCallbackMethod,
    "金\u200B",
    new[] { serverOriginal },
    null,
    item => item.Identity,
    item => item.ListingId,
    out var noSnapshotResults);
Check("missing snapshot fails open to original callback results",
    noSnapshotOutcome == MarketSearchMergeOutcome.NoSnapshot &&
    noSnapshotResults.SequenceEqual(new[] { serverOriginal }));

var invalidListing = new FakeMarketItem(string.Empty, "Junk", "Gold Ore");
var invalidThrew = false;
IReadOnlyList<FakeMarketItem> invalidResults = Array.Empty<FakeMarketItem>();
try
{
    bridge.TryMergeSnapshot(
        MarketSearchQueryBridge.SupportedDeclaringType,
        MarketSearchQueryBridge.SupportedCallbackMethod,
        "金\u200B",
        new[] { serverOriginal },
        new[] { invalidListing },
        item => item.Identity,
        item => item.ListingId,
        out invalidResults);
}
catch (InvalidOperationException)
{
    invalidThrew = true;
}
Check("invalid stable identity is fail-open ready",
    invalidThrew && invalidResults.SequenceEqual(new[] { serverOriginal }));

snapshot.Capture(17, new[] { goldB });
Check("incremental empty-search refresh replaces the previous snapshot",
    snapshot.TryGet(17, out var refreshed) && refreshed.SequenceEqual(new[] { goldB }));
snapshot.Capture(19, new[] { goldA });
Check("a new UI instance cannot consume the prior instance snapshot",
    !snapshot.TryGet(17, out _) && snapshot.TryGet(19, out var newOwner) &&
    newOwner.SequenceEqual(new[] { goldA }));
snapshot.Clear();
Check("snapshot clear removes captured market references", !snapshot.TryGet(19, out _));

foreach (var context in new[]
         {
             ("PlayerName", "SetName"),
             ("ChatPanel", "Send"),
             ("ShopName", "SetText"),
             ("UIVendingSearch", "Search"),
             ("UIVendingSearch", "CreateShop"),
             ("VendingManager", "RefreshCache"),
             ("FakeUIVendingSearch", "_Search_b__10_0"),
         })
{
    var outcome = bridge.TryResolveIdentities(context.Item1, context.Item2, "黄金", out var identities);
    Check(context.Item1 + " is excluded from the market index",
        outcome == MarketSearchIndexOutcome.Unchanged && identities.Count == 0);
}

foreach (var sourceGroup in rows.GroupBy(row => row.Source, StringComparer.Ordinal))
{
    var groupRows = sourceGroup.ToArray();
    var targets = groupRows.Select(row => row.Target).Distinct(StringComparer.Ordinal).ToArray();
    Check("exact Chinese resolves canonical IDs: " + sourceGroup.Key, targets.Length == 1);
    var resolved = Resolve(targets[0]);
    Check("exact Chinese includes every canonical ID: " + sourceGroup.Key,
        groupRows.All(row => resolved.Contains(row.Identity)));

    var zeroWidthResolved = Resolve(targets[0] + "\u200B");
    Check("zero-width exact Chinese includes every canonical ID: " + sourceGroup.Key,
        groupRows.All(row => zeroWidthResolved.Contains(row.Identity)));

    var propertySnapshot = groupRows
        .Select((row, index) => new FakeMarketItem(
            sourceGroup.Key + "\u001f" + index,
            row.Identity.ItemType,
            row.Identity.ItemId))
        .ToArray();
    var propertyOutcome = bridge.TryMergeSnapshot(
        MarketSearchQueryBridge.SupportedDeclaringType,
        MarketSearchQueryBridge.SupportedCallbackMethod,
        targets[0] + "\u200B",
        Array.Empty<FakeMarketItem>(),
        propertySnapshot,
        item => item.Identity,
        item => item.ListingId,
        out var propertyMerged);
    Check("callback merge reaches every canonical ID: " + sourceGroup.Key,
        propertyOutcome == MarketSearchMergeOutcome.Merged &&
        propertyMerged.Count == propertySnapshot.Length);
}

Console.WriteLine($"Passed {passed} P1 market-search checks across {rows.Count} canonical entries.");
return 0;

HashSet<MarketSearchIdentity> Resolve(string query)
{
    var outcome = bridge.TryResolveIdentities(
        MarketSearchQueryBridge.SupportedManagerType,
        MarketSearchQueryBridge.SupportedRequestMethod,
        query,
        out var identities);
    if (outcome == MarketSearchIndexOutcome.NoMatch)
    {
        return new HashSet<MarketSearchIdentity>();
    }
    Check("query is handled by local index: " + query, outcome == MarketSearchIndexOutcome.Matched);
    return new HashSet<MarketSearchIdentity>(identities);
}

void Check(string name, bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("FAILED: " + name);
    }
    passed++;
}

static MarketSearchIdentity Id(string itemType, string itemId) =>
    new MarketSearchIdentity(itemType, itemId);

static IReadOnlyList<MarketSearchCatalogEntry> LoadCatalog(string path)
{
    var entries = new List<string[]>();
    var aliases = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    foreach (var line in File.ReadLines(path))
    {
        if (line.StartsWith("#market-search-entry\t", StringComparison.Ordinal))
        {
            var parts = line.Split('\t');
            if (parts.Length != 5)
            {
                throw new InvalidDataException("Invalid market entry: " + line);
            }
            entries.Add(parts);
        }
        else if (line.StartsWith("#market-search-alias\t", StringComparison.Ordinal))
        {
            var parts = line.Split('\t');
            if (parts.Length != 4)
            {
                throw new InvalidDataException("Invalid market alias: " + line);
            }
            var key = parts[1] + "\0" + parts[2];
            if (!aliases.TryGetValue(key, out var values))
            {
                values = new HashSet<string>(StringComparer.Ordinal);
                aliases.Add(key, values);
            }
            values.Add(parts[3]);
        }
    }
    return entries.Select(parts =>
    {
        aliases.TryGetValue(parts[1] + "\0" + parts[2], out var values);
        return new MarketSearchCatalogEntry(
            parts[1],
            parts[2],
            parts[3],
            parts[4],
            values != null ? (IEnumerable<string>)values : Array.Empty<string>());
    }).ToArray();
}

internal sealed class FakeMarketItem
{
    internal FakeMarketItem(string listingId, string itemType, string itemId)
    {
        ListingId = listingId;
        Identity = new MarketSearchIdentity(itemType, itemId);
    }

    internal string ListingId { get; }
    internal MarketSearchIdentity Identity { get; }
}
