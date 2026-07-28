using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SpiritVale.RuntimeLocalization;

internal static class BilingualDisplayCoreTests
{
    private static readonly List<string> Failures = new List<string>();
    private static int _checks;

    private static int Main()
    {
        TestStrictConfigurationParsing();
        TestForwardCatalogLookupAndComposition();
        TestFailClosedLoading();
        TestMalformedCatalogsAreRejected();

        if (Failures.Count == 0)
        {
            Console.WriteLine($"Bilingual display core: {_checks} checks passed.");
            return 0;
        }

        foreach (var failure in Failures)
        {
            Console.Error.WriteLine("FAIL: " + failure);
        }
        return 1;
    }

    private static void TestStrictConfigurationParsing()
    {
        Equal(DisplayMode.Chinese, BilingualDisplayConfiguration.ParseDisplayMode(null), "null display mode");
        Equal(DisplayMode.Chinese, BilingualDisplayConfiguration.ParseDisplayMode("Chinese"), "Chinese display mode");
        Equal(DisplayMode.Bilingual, BilingualDisplayConfiguration.ParseDisplayMode("Bilingual"), "Bilingual display mode");
        Equal(DisplayMode.Chinese, BilingualDisplayConfiguration.ParseDisplayMode("bilingual"), "case-sensitive display mode");
        Equal(DisplayMode.Chinese, BilingualDisplayConfiguration.ParseDisplayMode(" Bilingual"), "untrimmed display mode");
        Equal(CompactSurfaceMode.Chinese, BilingualDisplayConfiguration.ParseCompactSurfaceMode(null), "null compact mode");
        Equal(CompactSurfaceMode.Chinese, BilingualDisplayConfiguration.ParseCompactSurfaceMode("Chinese"), "Chinese compact mode");
        Equal(CompactSurfaceMode.EnglishToggle, BilingualDisplayConfiguration.ParseCompactSurfaceMode("EnglishToggle"), "toggle compact mode");
        Equal(CompactSurfaceMode.EnglishToggle, BilingualDisplayConfiguration.ParseCompactSurfaceMode("EnglishOnHold"), "legacy hold mode maps to toggle");
        Equal(CompactSurfaceMode.Chinese, BilingualDisplayConfiguration.ParseCompactSurfaceMode("englishtoggle"), "case-sensitive toggle compact mode");
        Equal(CompactSurfaceMode.Chinese, BilingualDisplayConfiguration.ParseCompactSurfaceMode("English"), "unknown compact mode");
        False(BilingualDisplayConfiguration.NextCompactEnglishState(
            CompactSurfaceMode.EnglishToggle,
            false,
            false), "toggle stays Chinese without a key edge");
        True(BilingualDisplayConfiguration.NextCompactEnglishState(
            CompactSurfaceMode.EnglishToggle,
            false,
            true), "first key edge enables English");
        False(BilingualDisplayConfiguration.NextCompactEnglishState(
            CompactSurfaceMode.EnglishToggle,
            true,
            true), "second key edge restores Chinese");
        False(BilingualDisplayConfiguration.NextCompactEnglishState(
            CompactSurfaceMode.Chinese,
            true,
            true), "Chinese compact mode fails closed");
        False(BilingualDisplayConfiguration.NextCompactEnglishState(
            (CompactSurfaceMode)99,
            true,
            true), "unknown compact mode fails closed");

        var keyWasDown = false;
        False(BilingualDisplayConfiguration.TryConsumeCompactEnglishToggle(
            CompactSurfaceMode.EnglishToggle,
            false,
            false,
            ref keyWasDown,
            out var compactEnglishEnabled), "released key does not toggle");
        False(keyWasDown, "released key clears latch");
        False(compactEnglishEnabled, "released key preserves Chinese state");

        True(BilingualDisplayConfiguration.TryConsumeCompactEnglishToggle(
            CompactSurfaceMode.EnglishToggle,
            false,
            true,
            ref keyWasDown,
            out compactEnglishEnabled), "first physical press toggles");
        True(keyWasDown, "first physical press latches");
        True(compactEnglishEnabled, "first physical press enables English");

        False(BilingualDisplayConfiguration.TryConsumeCompactEnglishToggle(
            CompactSurfaceMode.EnglishToggle,
            compactEnglishEnabled,
            true,
            ref keyWasDown,
            out var repeatedCompactEnglishEnabled), "held key cannot toggle again");
        True(keyWasDown, "held key retains latch");
        True(repeatedCompactEnglishEnabled, "held key preserves English state");

        False(BilingualDisplayConfiguration.TryConsumeCompactEnglishToggle(
            CompactSurfaceMode.EnglishToggle,
            compactEnglishEnabled,
            false,
            ref keyWasDown,
            out var releasedCompactEnglishEnabled), "release only arms next press");
        False(keyWasDown, "release clears held latch");
        True(releasedCompactEnglishEnabled, "release preserves English state");

        True(BilingualDisplayConfiguration.TryConsumeCompactEnglishToggle(
            CompactSurfaceMode.EnglishToggle,
            releasedCompactEnglishEnabled,
            true,
            ref keyWasDown,
            out var restoredChinese), "second physical press toggles back");
        False(restoredChinese, "second physical press restores Chinese");

        True(keyWasDown, "disabled mode test starts latched");
        False(BilingualDisplayConfiguration.TryConsumeCompactEnglishToggle(
            CompactSurfaceMode.Chinese,
            true,
            true,
            ref keyWasDown,
            out var disabledModeState), "disabled mode does not toggle");
        False(keyWasDown, "disabled mode clears latch");
        True(disabledModeState, "disabled mode leaves supplied state untouched");
    }

    private static void TestForwardCatalogLookupAndComposition()
    {
        WithCatalog(
            EntityDisplayCatalog.Header + "\n" +
            "Skill\tFireball\t<color=#FFCC00>Fireball +2 {0}</color>\t<color=#FFCC00>火球 +2 {0}</color>\tdetail-invalid\n",
            path =>
            {
                False(EntityDisplayCatalog.TryLoad(path, out _, out _), "unknown policy rejected");
            });

        WithCatalog(
            EntityDisplayCatalog.Header + "\n" +
            "Skill\tFireball\t<color=#FFCC00>Fireball +2 {0}</color>\t<color=#FFCC00>火球 +2 {0}</color>\tchinese-only\n" +
            "Map\tSunnyMeadows2\tSunny Meadows 2\t阳光草甸 2\tenglish-on-hold\n",
            path =>
            {
                var catalog = EntityDisplayCatalog.Load(path);
                Equal(2, catalog.Count, "catalog count");
                True(catalog.TryGet(
                    EntityCategory.Skill,
                    "Fireball",
                    "<color=#FFCC00>Fireball +2 {0}</color>",
                    out var skill), "trusted forward lookup");
                Equal(
                    "<color=#FFCC00>火球 +2 {0}</color>\n<color=#FFCC00>Fireball +2 {0}</color>",
                    EntityDisplayComposer.ComposeDetail(skill, DisplayMode.Bilingual),
                    "two-line rich detail");
                Equal(skill.Target, EntityDisplayComposer.ComposeDetail(skill, DisplayMode.Chinese), "Chinese detail");
                Equal(skill.Source, skill.Values.English, "cached English");
                Equal(skill.Target, skill.Values.Chinese, "cached Chinese");
                Equal(skill.Target + "\n" + skill.Source, skill.Values.Bilingual, "cached bilingual");
                False(catalog.TryGet(EntityCategory.Item, "Fireball", skill.Source, out _), "wrong category rejected");
                False(catalog.TryGet(EntityCategory.Skill, "Other", skill.Source, out _), "wrong identity rejected");
                False(catalog.TryGet(EntityCategory.Skill, "Fireball", skill.Target, out _), "target reverse lookup absent");

                True(catalog.TryGet(EntityCategory.Map, "SunnyMeadows2", "Sunny Meadows 2", out var map), "map lookup");
                Equal("阳光草甸 2", EntityDisplayComposer.ComposeCompact(
                    map,
                    CompactSurfaceMode.EnglishToggle,
                    false), "toggle inactive");
                Equal("Sunny Meadows 2", EntityDisplayComposer.ComposeCompact(
                    map,
                    CompactSurfaceMode.EnglishToggle,
                    true), "toggle active");
                Equal("阳光草甸 2", EntityDisplayComposer.ComposeCompact(
                    map,
                    CompactSurfaceMode.Chinese,
                    true), "Chinese compact mode");
                Equal(skill.Target, EntityDisplayComposer.ComposeCompact(
                    skill,
                    CompactSurfaceMode.EnglishToggle,
                    true), "Chinese-only policy");
            });
    }

    private static void TestFailClosedLoading()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tsv");
        False(EntityDisplayCatalog.TryLoad(missing, out var catalog, out var error), "missing catalog fails closed");
        Equal(0, catalog.Count, "missing catalog returns empty catalog");
        True(!string.IsNullOrEmpty(error), "missing catalog reports error");
        False(catalog.TryGet(EntityCategory.Item, "id", "source", out _), "empty catalog lookup");
    }

    private static void TestMalformedCatalogsAreRejected()
    {
        AssertRejected("wrong\theader\n", "bad header");
        AssertRejected(EntityDisplayCatalog.Header + "\n", "no rows");
        AssertRejected(EntityDisplayCatalog.Header + "\nQuest\tid\tSource\t目标\tchinese-only\n", "unknown category");
        AssertRejected(EntityDisplayCatalog.Header + "\nItem\tid\tSource\t目标\tunknown\n", "unknown policy");
        AssertRejected(EntityDisplayCatalog.Header + "\nItem\tid\tSource\t目标\textra\tfield\n", "extra tab field");
        AssertRejected(
            EntityDisplayCatalog.Header + "\n" +
            "Item\tid\tSource\t目标\tchinese-only\n" +
            "Item\tid\tSource\t目标\tchinese-only\n",
            "duplicate key");
        AssertRejected(
            EntityDisplayCatalog.Header + "\n" +
            "Skill\tid\tDamage +25% {0}\t伤害 +20% {1}\tchinese-only\n",
            "protected token mismatch");

        var malformedUtf8 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tsv");
        try
        {
            var prefix = Encoding.UTF8.GetBytes(EntityDisplayCatalog.Header + "\nItem\tid\tSource\t");
            var bytes = new byte[prefix.Length + 4];
            Buffer.BlockCopy(prefix, 0, bytes, 0, prefix.Length);
            bytes[prefix.Length] = 0xC3;
            bytes[prefix.Length + 1] = 0x28;
            bytes[prefix.Length + 2] = (byte)'\t';
            bytes[prefix.Length + 3] = (byte)'x';
            File.WriteAllBytes(malformedUtf8, bytes);
            False(EntityDisplayCatalog.TryLoad(malformedUtf8, out _, out _), "malformed UTF-8 rejected");
        }
        finally
        {
            File.Delete(malformedUtf8);
        }
    }

    private static void AssertRejected(string contents, string name)
    {
        WithCatalog(contents, path => False(EntityDisplayCatalog.TryLoad(path, out _, out _), name));
    }

    private static void WithCatalog(string contents, Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tsv");
        try
        {
            File.WriteAllText(path, contents, new UTF8Encoding(false));
            action(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void True(bool value, string name)
    {
        _checks++;
        if (!value) Failures.Add(name);
    }

    private static void False(bool value, string name)
    {
        _checks++;
        if (value) Failures.Add(name);
    }

    private static void Equal<T>(T expected, T actual, string name)
    {
        _checks++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            Failures.Add($"{name}: expected '{expected}', got '{actual}'");
        }
    }
}
