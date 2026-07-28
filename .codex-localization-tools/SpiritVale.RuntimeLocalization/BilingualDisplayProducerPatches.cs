using System;
using System.Text.RegularExpressions;
using TMPro;

namespace SpiritVale.RuntimeLocalization;

internal static class BilingualDisplayProducerPatches
{
    private static readonly Regex LabeledLocationPattern = new Regex(
        @"^(?:Location)(?:[:：]\s*|\r?\n|\\n)(?<map>[\s\S]+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static void RegisterInventoryDrawable(
        UIInventoryItem __instance,
        IInfoDrawable __0)
    {
        TMP_Text text = null;
        try
        {
            text = __instance?.Name;
            if (__instance == null || __0 == null)
            {
                BilingualDisplayRuntime.Unregister(text);
                return;
            }

            var item = __0.TryCast<InventoryItemData>();
            if (item == null)
            {
                BilingualDisplayRuntime.Unregister(text);
                return;
            }
            RegisterInventoryItem(__instance, item);
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
            // The normal Chinese translation remains authoritative on any producer mismatch.
        }
    }

    public static void RegisterInventoryMonster(
        UIInventoryItem __instance,
        MonsterData __0)
    {
        TMP_Text text = null;
        try
        {
            text = __instance?.Name;
            BilingualDisplayRuntime.Unregister(text);
            if (__instance == null || __0 == null || string.IsNullOrEmpty(__0.Id))
            {
                return;
            }

            var config = App.ServerRuntime?.GetMonster(__0.Id);
            RegisterInventorySurface(
                __instance,
                EntityCategory.Monster,
                __0.Id,
                config?.DisplayName,
                config?.DisplayName);
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    public static void RegisterInventorySkill(
        UIInventoryItem __instance,
        SkillData __0)
    {
        TMP_Text text = null;
        try
        {
            text = __instance?.Name;
            BilingualDisplayRuntime.Unregister(text);
            if (__instance == null || __0 == null)
            {
                return;
            }
            RegisterSkillSurface(__instance, __0.Id);
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    public static void RegisterSkillsItem(
        UISkillsItem __instance,
        SkillData __0)
    {
        TMP_Text text = null;
        try
        {
            text = __instance?.Name;
            BilingualDisplayRuntime.Unregister(text);
            if (__instance == null || __0 == null)
            {
                return;
            }
            RegisterSkillText(text, __0.Id, detail: false);
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    public static void RegisterSkillButton(UISkillButton __instance)
    {
        TMP_Text text = null;
        try
        {
            text = __instance?.DisplayName;
            BilingualDisplayRuntime.Unregister(text);
            if (__instance == null)
            {
                return;
            }
            RegisterSkillText(text, __instance.Data?.Id, detail: false);
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    public static void RegisterWorldMapInfo(
        UIWorldMapInfo __instance,
        MapConfig __0)
    {
        TMP_Text text = null;
        try
        {
            text = __instance?.Name;
            BilingualDisplayRuntime.Unregister(text);
            if (__instance == null || __0 == null || string.IsNullOrEmpty(__0.Id))
            {
                return;
            }
            BilingualDisplayRuntime.RegisterTrustedDetail(
                text,
                EntityCategory.Map,
                __0.Id,
                __0.DisplayName);
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    public static void RegisterWorldMapItem(UIWorldMapItem __instance)
    {
        TMP_Text text = null;
        try
        {
            text = __instance?.DisplayName;
            BilingualDisplayRuntime.Unregister(text);
            if (__instance == null)
            {
                return;
            }

            var config = __instance.Config;
            if (config == null || string.IsNullOrEmpty(config.Id))
            {
                return;
            }

            BilingualDisplayRuntime.RegisterTrustedCompact(
                text,
                EntityCategory.Map,
                config.Id,
                config.DisplayName);
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    internal static void RegisterMonsterNameplate(
        TMP_Text text,
        MonsterController monster)
    {
        BilingualDisplayRuntime.Unregister(text);
        if (text == null || monster == null)
        {
            return;
        }

        try
        {
            var identity = monster.MonsterId;
            var config = App.ServerRuntime?.GetMonster(identity);
            if (config == null)
            {
                BilingualDisplayRuntime.Unregister(text);
                return;
            }

            BilingualDisplayRuntime.RegisterTrustedCompact(
                text,
                EntityCategory.Monster,
                identity,
                config.DisplayName);
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    internal static void RegisterTranslatedLocation(
        TMP_Text text,
        string source,
        ref string translated,
        string context,
        string hierarchyPath)
    {
        BilingualDisplayRuntime.Unregister(text);
        if (!TryResolveTrustedLocationSource(
                text,
                source,
                translated,
                context,
                hierarchyPath,
                out var mapSource))
        {
            return;
        }

        try
        {
            if (BilingualDisplayRuntime.PrepareTrustedMapOrLocationCompositeCompactWrite(
                    text,
                    mapSource,
                    source,
                    translated,
                    out var desired))
            {
                translated = desired;
            }
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    internal static void RegisterTranslatedLocation(
        TMP_Text text,
        string source,
        string translated,
        string context,
        string hierarchyPath)
    {
        BilingualDisplayRuntime.Unregister(text);
        if (!TryResolveTrustedLocationSource(
                text,
                source,
                translated,
                context,
                hierarchyPath,
                out var mapSource))
        {
            return;
        }

        try
        {
            var match = LabeledLocationPattern.Match(source);
            if (match.Success)
            {
                BilingualDisplayRuntime.RegisterTrustedMapOrLocationCompositeCompactBySource(
                    text,
                    mapSource,
                    source,
                    translated);
                return;
            }

            BilingualDisplayRuntime.RegisterTrustedMapOrLocationCompactBySource(text, source.Trim());
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    private static bool TryResolveTrustedLocationSource(
        TMP_Text text,
        string source,
        string translated,
        string context,
        string hierarchyPath,
        out string mapSource)
    {
        mapSource = null;
        if (text == null || string.IsNullOrEmpty(source) ||
            string.IsNullOrEmpty(translated) ||
            !IsTrustedLocationContext(context, hierarchyPath))
        {
            return false;
        }

        var match = LabeledLocationPattern.Match(source);
        mapSource = (match.Success ? match.Groups["map"].Value : source).Trim();
        return mapSource.Length != 0;
    }

    private static void RegisterInventoryItem(
        UIInventoryItem view,
        InventoryItemData item)
    {
        var text = view?.Name;
        var runtime = App.ServerRuntime;
        if (runtime == null || item == null || string.IsNullOrEmpty(item.Id))
        {
            BilingualDisplayRuntime.Unregister(text);
            return;
        }

        var equip = item.TryCast<EquipData>();
        if (equip != null)
        {
            var config = runtime.GetEquip(item.Id);
            RegisterInventorySurface(
                view,
                EntityCategory.Equip,
                item.Id,
                config?.DisplayName,
                config == null ? null : Extensions.ToDisplayName(equip, config));
            return;
        }

        var artifact = item.TryCast<ArtifactData>();
        if (artifact != null)
        {
            var config = runtime.GetArtifactSet(item.Id);
            RegisterInventorySurface(
                view,
                EntityCategory.Artifact,
                item.Id,
                config?.DisplayName,
                config == null ? null : Extensions.ToDisplayName(artifact, config));
            return;
        }

        var gem = item.TryCast<GemData>();
        if (gem != null)
        {
            var config = runtime.GetGem(item.Id);
            var dynamicEnglish = config?.DisplayName;
            if (config != null && gem.Refine > 0)
            {
                dynamicEnglish = "+" + gem.Refine + " " + config.DisplayName;
            }
            RegisterInventorySurface(
                view,
                EntityCategory.Gem,
                item.Id,
                config?.DisplayName,
                dynamicEnglish);
            return;
        }

        var configItem = runtime.GetItem(item.Id);
        var dynamicItemName = configItem?.DisplayName;
        var junk = item.TryCast<JunkData>();
        var junkConfig = configItem?.TryCast<JunkConfig>();
        if (junk != null && junkConfig != null)
        {
            dynamicItemName = Extensions.ToDisplayName(junk, junkConfig);
        }
        RegisterInventorySurface(
            view,
            EntityCategory.Item,
            item.Id,
            configItem?.DisplayName,
            dynamicItemName);
    }

    private static void RegisterSkillSurface(UIInventoryItem view, string identity)
    {
        var text = view?.Name;
        BilingualDisplayRuntime.Unregister(text);
        if (view == null)
        {
            return;
        }

        try
        {
            RegisterSkillText(text, identity, IsDetailSurface(view));
        }
        catch
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    private static void RegisterSkillText(TMP_Text text, string identity, bool detail)
    {
        BilingualDisplayRuntime.Unregister(text);
        if (text == null || string.IsNullOrEmpty(identity))
        {
            return;
        }

        var config = App.ServerRuntime?.GetSkill(identity);
        var source = config?.DisplayName;
        if (string.IsNullOrEmpty(source))
        {
            BilingualDisplayRuntime.Unregister(text);
            return;
        }

        if (Register(text, EntityCategory.Skill, identity, source, source, detail))
        {
            return;
        }
        Register(text, EntityCategory.SkillPassive, identity, source, source, detail);
    }

    private static void RegisterInventorySurface(
        UIInventoryItem view,
        EntityCategory category,
        string identity,
        string catalogSource,
        string dynamicEnglish)
    {
        var text = view?.Name;
        if (text == null || string.IsNullOrEmpty(identity) ||
            string.IsNullOrEmpty(catalogSource))
        {
            BilingualDisplayRuntime.Unregister(text);
            return;
        }

        var dynamicChinese = text.text;
        if (!Register(
            text,
            category,
            identity,
            catalogSource,
            dynamicEnglish ?? catalogSource,
            IsDetailSurface(view),
            dynamicChinese))
        {
            BilingualDisplayRuntime.Unregister(text);
        }
    }

    private static bool Register(
        TMP_Text text,
        EntityCategory category,
        string identity,
        string catalogSource,
        string dynamicEnglish,
        bool detail,
        string dynamicChinese = null)
    {
        if (dynamicChinese != null &&
            !string.Equals(dynamicEnglish, catalogSource, StringComparison.Ordinal))
        {
            return detail
                ? BilingualDisplayRuntime.RegisterTrustedCompositeEntityDetail(
                    text, category, identity, catalogSource, dynamicEnglish, dynamicChinese)
                : BilingualDisplayRuntime.RegisterTrustedCompositeEntityCompact(
                    text, category, identity, catalogSource, dynamicEnglish, dynamicChinese);
        }

        return detail
            ? BilingualDisplayRuntime.RegisterTrustedDetail(text, category, identity, catalogSource)
            : BilingualDisplayRuntime.RegisterTrustedCompact(text, category, identity, catalogSource);
    }

    private static bool IsDetailSurface(UIInventoryItem view)
    {
        try
        {
            return view.GetComponentInParent<UIItemPopup>() != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTrustedLocationContext(string context, string hierarchyPath)
    {
        return !string.IsNullOrEmpty(context) &&
            context.Equals("Location", StringComparison.OrdinalIgnoreCase);
    }
}
