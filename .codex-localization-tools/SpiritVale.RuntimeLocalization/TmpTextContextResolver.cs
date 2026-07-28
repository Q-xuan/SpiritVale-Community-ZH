using System;
using System.Collections.Generic;

namespace SpiritVale.RuntimeLocalization;

internal static class TmpTextContextResolver
{
    internal const int MaxAncestorDepth = 6;

    private static readonly string[] GenericObjectNames =
    {
        "Text",
        "Text (TMP)",
        "TMP Text",
        "TextMeshPro",
        "TextMeshProUGUI",
        "Name"
    };

    private static readonly string[] StrongPlayerTextContainers =
    {
        "playername", "charactername", "displayname", "textname", "shopname",
        "sellername", "guildname", "guildmember", "partyname", "teamname",
        "chat", "message"
    };

    private static readonly string[] ReviewedItemNameContainers =
    {
        "uivendingsearchitem", "uivendingitemsell", "marketitem", "marketitemlisting",
        "marketlisting", "inventoryitem", "inventoryitemslot", "uiinventoryitem",
        "equipmentname", "equipname", "lootitem", "tooltipitem"
    };

    private static readonly string[] WorldInteractionActionContainers =
    {
        "bindtomainplayer"
    };

    private static readonly string[] BroadPlayerTextContainers =
    {
        "player", "character", "shop", "seller", "vending", "guild", "party", "team"
    };

    internal static string Resolve(string localName, IEnumerable<string> ancestorNames)
    {
        var fallback = localName ?? string.Empty;
        if (fallback.Trim().Equals("Name", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveAmbiguousName(ancestorNames);
        }
        if (!RequiresAncestor(fallback) || ancestorNames == null)
        {
            return fallback;
        }

        foreach (var ancestorName in ancestorNames)
        {
            if (!RequiresAncestor(ancestorName))
            {
                return ResolveReviewedAncestor(ancestorName) ?? ancestorName;
            }
        }
        return fallback;
    }

    private static string ResolveAmbiguousName(IEnumerable<string> ancestorNames)
    {
        if (ancestorNames == null)
        {
            return "Name";
        }

        foreach (var ancestorName in ancestorNames)
        {
            var compact = Compact(ancestorName);
            if (compact.Length == 0 || RequiresAncestor(ancestorName))
            {
                continue;
            }
            var reviewedAncestor = ResolveReviewedAncestor(ancestorName);
            if (reviewedAncestor != null)
            {
                return reviewedAncestor;
            }
            if (MatchesReviewedContainer(compact, StrongPlayerTextContainers))
            {
                return "PlayerName:" + ancestorName;
            }
            if (IsReviewedClassNameContainer(compact))
            {
                return "ClassName:" + ancestorName;
            }
            // A vending item row contains both "vending" and "item". Treat the
            // concrete item signal as authoritative before the broader player-
            // text guards used for shop names and sellers.
            if (MatchesReviewedContainer(compact, ReviewedItemNameContainers))
            {
                return "ItemName:" + ancestorName;
            }
            if (ContainsAny(compact, BroadPlayerTextContainers))
            {
                return "PlayerName:" + ancestorName;
            }
        }
        return "Name";
    }

    private static string ResolveReviewedAncestor(string ancestorName)
    {
        var compact = Compact(ancestorName);
        return MatchesReviewedContainer(compact, WorldInteractionActionContainers)
            ? "WorldInteractionAction:" + ancestorName
            : null;
    }

    private static bool IsReviewedClassNameContainer(string compact)
    {
        return MatchesReviewedContainer(
            compact,
            new[] { "character", "characterselector", "guicharacterdetails" });
    }

    private static bool MatchesReviewedContainer(string compact, string[] reviewedNames)
    {
        foreach (var reviewedName in reviewedNames)
        {
            if (compact.Equals(reviewedName, StringComparison.Ordinal) ||
                compact.Equals(reviewedName + "clone", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string Compact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        var compact = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                compact.Append(char.ToLowerInvariant(character));
            }
        }
        return compact.ToString();
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (value.IndexOf(needle, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    internal static bool RequiresAncestor(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        var candidate = name.Trim();
        foreach (var genericName in GenericObjectNames)
        {
            if (candidate.Equals(genericName, StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals(genericName + " (Clone)", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
