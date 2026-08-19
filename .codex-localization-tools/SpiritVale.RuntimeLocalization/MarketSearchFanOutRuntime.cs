using System;
using System.Collections.Generic;
using BepInEx.Logging;
using SpiritVale.Vending.Contracts;

namespace SpiritVale.RuntimeLocalization;

internal static class MarketSearchFanOutRuntime
{
    internal const int MaximumQueries = 4;
    private const int MinimumPageSize = 50;

    internal static bool TryDispatch(
        PlayerController player,
        SearchRequest request,
        Il2CppSystem.Action<VendingManager.VendingSearchPage> completion,
        IReadOnlyList<MarketSearchFanOutQuery> queries,
        ManualLogSource log)
    {
        if (player == null || request == null || completion == null ||
            queries == null || queries.Count == 0 || queries.Count > MaximumQueries)
        {
            return false;
        }

        var pending = new PendingFanOut(completion, queries.Count, log);
        var failedDispatches = 0;
        var dispatched = 0;
        foreach (var query in queries)
        {
            try
            {
                var candidateRequest = CloneRequest(request, query.Query);
                var candidateCallback = pending.CreateCallback(query.Identities);
                player.RequestVendorItemList(candidateRequest, candidateCallback);
                dispatched++;
            }
            catch (Exception exception)
            {
                failedDispatches++;
                log?.LogWarning((object)(
                    "Chinese market candidate request failed: " + exception.Message));
            }
        }

        if (dispatched == 0)
        {
            return false;
        }
        pending.Skip(failedDispatches);
        return true;
    }

    private static SearchRequest CloneRequest(SearchRequest source, string query)
    {
        return new SearchRequest
        {
            Archetype = source.Archetype,
            Cursor = source.Cursor,
            EquipType = source.EquipType,
            HasCard = source.HasCard,
            HasGem = source.HasGem,
            ItemCategory = source.ItemCategory,
            ItemType = source.ItemType,
            MaximumLevel = source.MaximumLevel,
            MaximumPotential = source.MaximumPotential,
            MaximumRefine = source.MaximumRefine,
            MaximumUnitPrice = source.MaximumUnitPrice,
            MinimumLevel = source.MinimumLevel,
            MinimumPotential = source.MinimumPotential,
            MinimumQuantity = source.MinimumQuantity,
            MinimumRefine = source.MinimumRefine,
            MinimumUnitPrice = source.MinimumUnitPrice,
            PageSize = Math.Max(source.PageSize, MinimumPageSize),
            Query = query,
            StatFilters = source.StatFilters,
            StatMatchMode = source.StatMatchMode,
        };
    }

    private sealed class PendingFanOut
    {
        private readonly object _gate = new object();
        private readonly Il2CppSystem.Action<VendingManager.VendingSearchPage> _completion;
        private readonly ManualLogSource _log;
        private readonly HashSet<string> _listingIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<VendingManager.VendingItemData> _items =
            new List<VendingManager.VendingItemData>();
        private readonly List<Il2CppSystem.Action<VendingManager.VendingSearchPage>> _callbacks =
            new List<Il2CppSystem.Action<VendingManager.VendingSearchPage>>();
        private VendingManager.VendingSearchPage _firstPage;
        private VendingManager.VendingSearchPage _successfulPage;
        private int _remaining;
        private bool _completed;

        internal PendingFanOut(
            Il2CppSystem.Action<VendingManager.VendingSearchPage> completion,
            int expectedResponses,
            ManualLogSource log)
        {
            _completion = completion;
            _remaining = expectedResponses;
            _log = log;
        }

        internal Il2CppSystem.Action<VendingManager.VendingSearchPage> CreateCallback(
            IReadOnlyCollection<MarketSearchIdentity> identities)
        {
            var allowed = new HashSet<MarketSearchIdentity>(
                identities ?? Array.Empty<MarketSearchIdentity>());
            System.Action<VendingManager.VendingSearchPage> managed =
                page => Accept(page, allowed);
            Il2CppSystem.Action<VendingManager.VendingSearchPage> callback = managed;
            lock (_gate)
            {
                _callbacks.Add(callback);
            }
            return callback;
        }

        internal void Skip(int count)
        {
            if (count <= 0)
            {
                return;
            }
            CompleteResponse(null, null, count);
        }

        private void Accept(
            VendingManager.VendingSearchPage page,
            HashSet<MarketSearchIdentity> allowed)
        {
            CompleteResponse(page, allowed, 1);
        }

        private void CompleteResponse(
            VendingManager.VendingSearchPage page,
            HashSet<MarketSearchIdentity> allowed,
            int completedResponses)
        {
            VendingManager.VendingSearchPage completedPage = null;
            lock (_gate)
            {
                if (_completed)
                {
                    return;
                }

                _firstPage ??= page;
                if (page?.Success == true)
                {
                    _successfulPage ??= page;
                    AddMatchingItems(page.Items, allowed);
                }

                _remaining -= completedResponses;
                if (_remaining > 0)
                {
                    return;
                }

                _completed = true;
                completedPage = _successfulPage ?? _firstPage;
                if (_successfulPage != null)
                {
                    var merged = new Il2CppSystem.Collections.Generic.List<
                        VendingManager.VendingItemData>();
                    foreach (var item in _items)
                    {
                        merged.Add(item);
                    }
                    completedPage.Items = merged;
                    completedPage.HasMore = false;
                    completedPage.NextCursor = null;
                }
            }

            try
            {
                _completion.Invoke(completedPage);
            }
            catch (Exception exception)
            {
                _log?.LogWarning((object)(
                    "Chinese market merged callback failed: " + exception.Message));
            }
            finally
            {
                lock (_gate)
                {
                    _callbacks.Clear();
                }
            }
        }

        private void AddMatchingItems(
            Il2CppSystem.Collections.Generic.List<VendingManager.VendingItemData> items,
            HashSet<MarketSearchIdentity> allowed)
        {
            if (items == null || allowed == null || allowed.Count == 0)
            {
                return;
            }

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var listing = item?.Listing;
                var snapshot = listing?.Item;
                if (snapshot == null || string.IsNullOrEmpty(listing.ListingId))
                {
                    continue;
                }

                var identity = new MarketSearchIdentity(
                    snapshot.Type.ToString(),
                    snapshot.ItemId);
                if (allowed.Contains(identity) && _listingIds.Add(listing.ListingId))
                {
                    _items.Add(item);
                }
            }
        }
    }
}
