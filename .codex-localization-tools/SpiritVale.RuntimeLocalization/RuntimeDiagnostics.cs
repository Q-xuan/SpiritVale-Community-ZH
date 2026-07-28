using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpiritVale.RuntimeLocalization;

// Read-only probes used to identify the real producer and IME boundary. They never
// change a game argument, issue a request, or write a Unity object.
internal static class RuntimeDiagnostics
{
    internal const string MarkerFileName = "runtime-diagnostics.enabled";
    private static ManualLogSource _log;
    private static readonly Dictionary<int, string> LastInputStates =
        new Dictionary<int, string>();
    private static readonly Dictionary<int, string> LastSearchStates =
        new Dictionary<int, string>();
    private static readonly Dictionary<int, string> LastSearchLateStates =
        new Dictionary<int, string>();
    private static readonly Dictionary<int, string> LastCallbackStates =
        new Dictionary<int, string>();
    private static readonly Dictionary<int, string> LastDescriptionStates =
        new Dictionary<int, string>();
    private static readonly Dictionary<int, string> LastMarketCallbackStates =
        new Dictionary<int, string>();
    private static readonly HashSet<int> MarketInputFieldIds = new HashSet<int>();
    private static int _p2Reports;
    internal static bool Enabled { get; private set; }

    internal static void Initialize(ManualLogSource log, bool enabled)
    {
        Enabled = enabled;
        _log = enabled ? log : null;
        LastInputStates.Clear();
        LastSearchStates.Clear();
        LastSearchLateStates.Clear();
        LastCallbackStates.Clear();
        LastDescriptionStates.Clear();
        LastMarketCallbackStates.Clear();
        MarketInputFieldIds.Clear();
        _p2Reports = 0;
    }

    internal static void ObserveSearch(
        UIVendingSearch __instance,
        string __0,
        bool __1)
    {
        if (__instance == null)
        {
            return;
        }

        try
        {
            var field = __instance.SearchField;
            RegisterMarketField(field);
            var state = string.Join(
                " | ",
                "arg=" + Summarize(__0),
                "force=" + __1,
                "current=" + Summarize(__instance.CurrentSearch),
                "timer=" + __instance.SearchTimer.ToString("0.###", CultureInfo.InvariantCulture),
                DescribeInput(field));
            ReportChanged(LastSearchStates, __instance.GetInstanceID(), "P1 Search", state);
        }
        catch (Exception exception)
        {
            ReportException("P1 Search probe", exception);
        }
    }

    internal static void ObserveSearchLateUpdate(UIVendingSearch __instance)
    {
        if (__instance == null)
        {
            return;
        }

        try
        {
            var field = __instance.SearchField;
            RegisterMarketField(field);
            var state = string.Join(
                " | ",
                "current=" + Summarize(__instance.CurrentSearch),
                DescribeInput(field));
            ReportChanged(LastSearchLateStates, __instance.GetInstanceID(), "P1 UIVendingSearch.LateUpdate", state);
        }
        catch (Exception exception)
        {
            ReportException("P1 UIVendingSearch.LateUpdate probe", exception);
        }
    }

    internal static void ObserveInputCallback(
        UIVendingSearch __instance,
        string __0)
    {
        try
        {
            var field = __instance?.SearchField;
            RegisterMarketField(field);
            var owner = __instance == null ? 0 : __instance.GetInstanceID();
            var state = "callback=" + Summarize(__0) + " | " + DescribeInput(field);
            ReportChanged(LastCallbackStates, owner, "P1 input callback", state);
        }
        catch (Exception exception)
        {
            ReportException("P1 input callback probe", exception);
        }
    }

    internal static void ObserveInputField(TMP_InputField __instance, string eventName)
    {
        if (__instance == null || !MarketInputFieldIds.Contains(__instance.GetInstanceID()))
        {
            return;
        }

        try
        {
            ReportChanged(
                LastInputStates,
                __instance.GetInstanceID(),
                "P1 TMP_InputField." + eventName,
                DescribeInput(__instance));
        }
        catch (Exception exception)
        {
            ReportException("P1 TMP input probe", exception);
        }
    }

    internal static void ObserveTmpStringWrite(
        TMP_Text text,
        string value,
        string method)
    {
        if (!Enabled || !LooksLikeStatScaling(value))
        {
            return;
        }

        ReportP2(text, value, method);
    }

    internal static void ObserveTmpInternalString(TMP_Text __instance, string __0)
    {
        ObserveTmpStringWrite(__instance, __0, "TMP_Text.SetTextInternal");
    }

    internal static void ObserveTmpAfterWrite(TMP_Text text, string method)
    {
        try
        {
            var value = text?.text;
            if (LooksLikeStatScaling(value))
            {
                ReportP2(text, value, method + ".final");
            }
        }
        catch (Exception exception)
        {
            ReportException("P2 TMP post-write probe", exception);
        }
    }

    internal static void ObserveTmpCharArrayWrite(TMP_Text __instance)
    {
        ObserveTmpAfterWrite(__instance, "TMP_Text.SetCharArray");
    }

    internal static void ObserveTmpNonStringWrite(TMP_Text __instance)
    {
        ObserveTmpAfterWrite(__instance, "TMP_Text.SetText(non-string)");
    }

    internal static void ObserveInputValueChanged(TMP_InputField __instance)
    {
        ObserveInputField(__instance, "SendOnValueChanged");
    }

    internal static void ObserveInputValueChangedAndUpdateLabel(TMP_InputField __instance)
    {
        ObserveInputField(__instance, "SendOnValueChangedAndUpdateLabel");
    }

    internal static void ObserveInputEndEdit(TMP_InputField __instance)
    {
        ObserveInputField(__instance, "SendOnEndEdit");
    }

    internal static void ObserveInputSubmit(TMP_InputField __instance)
    {
        ObserveInputField(__instance, "SendOnSubmit");
    }

    internal static void ObserveInputLateUpdate(TMP_InputField __instance)
    {
        ObserveInputField(__instance, "LateUpdate");
    }

    internal static void ObserveInputUpdateSelected(
        TMP_InputField __instance,
        BaseEventData __0)
    {
        ObserveInputField(__instance, "OnUpdateSelected");
    }

    internal static void ObserveMarketCallback(
        UIVendingSearch __instance,
        string query,
        int originalCount,
        int snapshotCount,
        string outcome,
        int mergedCount)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            var owner = __instance == null ? 0 : __instance.GetInstanceID();
            var state = "query=" + Summarize(query) +
                " | original=" + originalCount +
                " | snapshot=" + snapshotCount +
                " | outcome=" + outcome +
                " | merged=" + mergedCount;
            ReportChanged(LastMarketCallbackStates, owner, "P1 market callback", state);
        }
        catch (Exception exception)
        {
            ReportException("P1 market callback probe", exception);
        }
    }

    internal static void ObserveDescriptionProducer(
        UIInventoryItem __instance,
        string __0)
    {
        if (!Enabled || !LooksLikeStatScaling(__0))
        {
            return;
        }

        try
        {
            var text = __instance?.Description;
            var hierarchy = text == null ? "<null>" : GetHierarchy(text.transform);
            var finalText = text == null ? null : text.text;
            var state = "producer=" + Summarize(__0) +
                " | final=" + Summarize(finalText) +
                " | hierarchy=" + hierarchy;
            var owner = __instance == null ? 0 : __instance.GetInstanceID();
            ReportChanged(LastDescriptionStates, owner, "P2 UIInventoryItem.DrawDescription2", state);
        }
        catch (Exception exception)
        {
            ReportException("P2 description producer probe", exception);
        }
    }

    internal static void ObserveEquipDescription(ref string __result)
    {
        if (Enabled && LooksLikeStatScaling(__result))
        {
            ReportP2(null, __result, "Extensions.ToDescription(EquipData)");
        }
    }

    private static void ReportP2(TMP_Text text, string value, string method)
    {
        if (_p2Reports >= 32)
        {
            return;
        }

        try
        {
            var context = text == null ? "<null>" : text.gameObject?.name ?? "<unnamed>";
            var hierarchy = text == null ? "<null>" : GetHierarchy(text.transform);
            _p2Reports++;
            _log?.LogInfo((object)(
                "DIAG P2 stat-scaling write: method=" + method +
                " | object=" + context +
                " | hierarchy=" + hierarchy +
                " | raw=" + Summarize(value)));
        }
        catch (Exception exception)
        {
            ReportException("P2 stat-scaling probe", exception);
        }
    }

    private static bool LooksLikeStatScaling(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf('%') < 0)
        {
            return false;
        }

        var visible = StripRichText(value);
        if (visible.IndexOf("per 10", StringComparison.OrdinalIgnoreCase) >= 0 ||
            visible.IndexOf("\u6bcf 10", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        return new[]
        {
            "\u529b\u91cf", "\u4f53\u8d28", "\u7075\u5de7", "\u654f\u6377",
            "\u667a\u529b", "\u5e78\u8fd0",
            "Strength", "Vitality", "Dexterity", "Agility", "Intelligence", "Luck",
        }.Any(marker => visible.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) &&
            visible.IndexOf("10", StringComparison.Ordinal) >= 0;
    }

    private static void RegisterMarketField(TMP_InputField field)
    {
        if (field == null)
        {
            return;
        }

        try
        {
            var id = field.GetInstanceID();
            if (MarketInputFieldIds.Add(id))
            {
                _log?.LogInfo((object)(
                    "DIAG P1 registered UIVendingSearch.SearchField: id=" + id +
                    " | path=" + GetHierarchy(field.transform)));
            }
        }
        catch (Exception exception)
        {
            ReportException("P1 market field registration", exception);
        }
    }

    private static string DescribeInput(TMP_InputField field)
    {
        if (field == null)
        {
            return "field=<null>";
        }

        var component = field.textComponent;
        string composition;
        string globalComposition;
        try
        {
            composition = field.compositionString;
        }
        catch
        {
            composition = "<error>";
        }

        try
        {
            globalComposition = UnityEngine.Input.compositionString;
        }
        catch
        {
            globalComposition = "<error>";
        }

        return string.Join(
            " | ",
            "path=" + GetHierarchy(field.transform),
            "focused=" + field.isFocused,
            "text=" + Summarize(field.text),
            "component=" + Summarize(component?.text),
            "fieldComposition=" + Summarize(composition),
            "globalComposition=" + Summarize(globalComposition));
    }

    private static void ReportChanged(
        IDictionary<int, string> states,
        int owner,
        string label,
        string state)
    {
        if (_log == null)
        {
            return;
        }

        if (states.TryGetValue(owner, out var previous) &&
            string.Equals(previous, state, StringComparison.Ordinal))
        {
            return;
        }

        states[owner] = state;
        _log.LogInfo((object)("DIAG " + label + ": " + state));
    }

    private static string GetHierarchy(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        var names = new List<string>(8);
        var current = transform;
        for (var depth = 0; current != null && depth < 8; depth++)
        {
            names.Add(current.gameObject?.name ?? "<unnamed>");
            current = current.parent;
        }
        names.Reverse();
        return string.Join(" > ", names);
    }

    private static string StripRichText(string value)
    {
        var builder = new StringBuilder(value ?? string.Empty);
        var start = builder.ToString().IndexOf('<');
        while (start >= 0)
        {
            var end = builder.ToString().IndexOf('>', start + 1);
            if (end < 0)
            {
                break;
            }
            builder.Remove(start, end - start + 1);
            start = builder.ToString().IndexOf('<');
        }
        return builder.ToString();
    }

    private static string Summarize(string value)
    {
        if (value == null)
        {
            return "<null>";
        }

        var visible = StripRichText(value);
        var cjk = value.Count(CjkText.IsCjk);
        var ascii = value.Count(character => character <= 0x7F);
        var format = value.Count(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format);
        var tags = SummarizeTags(value);
        var codepoints = string.Join(
            " ",
            value.Take(24).Select(character =>
                "U+" + ((int)character).ToString("X4", CultureInfo.InvariantCulture) +
                "/" + CharUnicodeInfo.GetUnicodeCategory(character)));
        return "len=" + value.Length +
            ",visibleLen=" + visible.Length +
            ",cjk=" + cjk +
            ",ascii=" + ascii +
            ",format=" + format +
            ",tags=" + tags +
            ",codes=" + codepoints;
    }

    private static string SummarizeTags(string value)
    {
        var tags = new List<string>();
        var cursor = 0;
        while (cursor < value.Length && tags.Count < 12)
        {
            var start = value.IndexOf('<', cursor);
            if (start < 0)
            {
                break;
            }
            var end = value.IndexOf('>', start + 1);
            if (end < 0)
            {
                break;
            }
            var body = value.Substring(start + 1, end - start - 1).Trim();
            var closing = body.StartsWith("/", StringComparison.Ordinal);
            if (closing)
            {
                body = body.Substring(1).Trim();
            }
            var separator = body.IndexOfAny(new[] { ' ', '=' });
            var name = separator < 0 ? body : body.Substring(0, separator);
            if (!string.IsNullOrEmpty(name))
            {
                tags.Add((closing ? "/" : string.Empty) + name);
            }
            cursor = end + 1;
        }
        return tags.Count == 0 ? "-" : string.Join(",", tags);
    }

    private static void ReportException(string label, Exception exception)
    {
        _log?.LogWarning((object)(label + " failed open: " + exception.GetType().Name +
            ": " + exception.Message));
    }
}
