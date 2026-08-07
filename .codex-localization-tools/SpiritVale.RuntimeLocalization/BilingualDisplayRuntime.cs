using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;

namespace SpiritVale.RuntimeLocalization;

/// <summary>
/// Presentation-only bilingual state for TMP fields registered by trusted entity producers.
/// This class intentionally has no generic TMP hook and never reads from translated Chinese
/// to discover an English entity name.
/// </summary>
internal static class BilingualDisplayRuntime
{
    private enum SurfaceKind
    {
        Detail,
        Compact
    }

    private sealed class RegisteredSurface
    {
        internal RegisteredSurface(
            int instanceId,
            TMP_Text text,
            SurfaceKind kind,
            EntityDisplayValues values,
            CompactDisplayPolicy compactPolicy)
        {
            InstanceId = instanceId;
            Text = text;
            Kind = kind;
            Values = values;
            CompactPolicy = compactPolicy;
        }

        internal int InstanceId { get; }
        // A strong wrapper is intentional for IL2CPP: a weak managed proxy can be collected while
        // its native TMP component still exists. Unity owns the native lifetime, and periodic
        // maintenance releases wrappers whose component was destroyed or repurposed.
        internal TMP_Text Text { get; }
        internal SurfaceKind Kind { get; }
        internal EntityDisplayValues Values { get; }
        internal CompactDisplayPolicy CompactPolicy { get; }

        internal bool Owns(string value)
        {
            return string.Equals(value, Values.Chinese, StringComparison.Ordinal) ||
                string.Equals(value, Values.Bilingual, StringComparison.Ordinal) ||
                string.Equals(value, Values.English, StringComparison.Ordinal);
        }
    }

    private static readonly Dictionary<int, RegisteredSurface> RegisteredSurfaces =
        new Dictionary<int, RegisteredSurface>();

    private static readonly Dictionary<string, EntityDisplayEntry> MapEntriesBySource =
        new Dictionary<string, EntityDisplayEntry>(StringComparer.Ordinal);

    private static readonly HashSet<string> AmbiguousMapSources =
        new HashSet<string>(StringComparer.Ordinal);

    private static readonly Dictionary<EntityIdentityKey, EntityDisplayEntry> EntriesByIdentity =
        new Dictionary<EntityIdentityKey, EntityDisplayEntry>();

    private static readonly HashSet<EntityIdentityKey> CanonicalIdentityKeys =
        new HashSet<EntityIdentityKey>();

    private static readonly HashSet<EntityIdentityKey> AmbiguousIdentityKeys =
        new HashSet<EntityIdentityKey>();

    private static readonly string[] ProtectedContentMarkers =
    {
        "playername", "charactername", "shopname",
        "sellername", "guildname", "guildmember", "partyname", "teamname",
        "stallname", "chat", "message", "leaderboard", "ranking", "searchquery",
        "querytext"
    };

    [ThreadStatic]
    private static HashSet<int> _internalWriteIds;

    [ThreadStatic]
    private static int _registrationDepth;

    private static EntityDisplayCatalog _catalog = EntityDisplayCatalog.Empty;
    private static DisplayMode _detailMode = DisplayMode.Chinese;
    private static CompactSurfaceMode _compactMode = CompactSurfaceMode.Chinese;
    private static KeyCode _englishToggleKey = KeyCode.Tab;
    private static bool _compactEnglishEnabled;
    private static bool _englishToggleKeyWasDown;
    private static int _lastEnglishTogglePollFrame = -1;
    private static bool _refreshing;
    private static int _registrationCalls;
    private static bool _keyPollingFailed;
    private static Action<string> _reportWarning;
    private static bool _runtimeFailureReported;
    private static int _mainThreadId;

    /// <summary>
    /// Initializes presentation state. Both modes fail closed to Chinese for unknown enum values.
    /// In the default Chinese/Chinese configuration no map index is built and producer calls return
    /// before touching a Unity object. The plugin must call this once from Unity's main thread.
    /// </summary>
    internal static void Initialize(
        EntityDisplayCatalog catalog,
        DisplayMode detailMode,
        CompactSurfaceMode compactMode,
        KeyCode englishToggleKey,
        Action<string> reportWarning = null)
    {
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        RestoreAndClearRegistrations();

        _catalog = catalog ?? EntityDisplayCatalog.Empty;
        _detailMode = detailMode == DisplayMode.Bilingual
            ? DisplayMode.Bilingual
            : DisplayMode.Chinese;
        _compactMode = compactMode == CompactSurfaceMode.EnglishToggle
            ? CompactSurfaceMode.EnglishToggle
            : CompactSurfaceMode.Chinese;
        _englishToggleKey = englishToggleKey;
        _compactEnglishEnabled = false;
        _englishToggleKeyWasDown = false;
        _lastEnglishTogglePollFrame = -1;
        _registrationCalls = 0;
        _keyPollingFailed = false;
        _reportWarning = reportWarning;
        _runtimeFailureReported = false;

        MapEntriesBySource.Clear();
        AmbiguousMapSources.Clear();
        EntriesByIdentity.Clear();
        CanonicalIdentityKeys.Clear();
        AmbiguousIdentityKeys.Clear();
        if (IsEnabled)
        {
            BuildLookupIndexes();
        }
    }

    /// <summary>
    /// Category/identity lookup for producers whose identity is authoritative. An identity with
    /// several source aliases resolves only when the catalog contains a canonical source equal to
    /// the identity; otherwise it fails closed and the producer should use the exact-source API.
    /// </summary>
    internal static bool RegisterTrustedDetail(
        TMP_Text text,
        EntityCategory category,
        string identity)
    {
        if (_detailMode != DisplayMode.Bilingual || IsControllerWrite)
        {
            return false;
        }

        return TryResolveByIdentity(category, identity, out var entry) &&
            Register(text, SurfaceKind.Detail, entry.Values, entry.CompactPolicy);
    }

    /// <summary>
    /// Registers a wide, auto-sizing entity title from a producer that knows the exact entity key.
    /// Descriptions, buttons and general TMP setters must not call this method.
    /// </summary>
    internal static bool RegisterTrustedDetail(
        TMP_Text text,
        EntityCategory category,
        string identity,
        string source)
    {
        if (_detailMode != DisplayMode.Bilingual || IsControllerWrite)
        {
            return false;
        }

        return TryResolve(category, identity, source, out var entry) &&
            Register(text, SurfaceKind.Detail, entry.Values, entry.CompactPolicy);
    }

    /// <summary>
    /// Registers a compact entity label whose English name may be shown only after the toggle key
    /// is pressed and the catalog row explicitly permits the compact English view.
    /// </summary>
    internal static bool RegisterTrustedCompact(
        TMP_Text text,
        EntityCategory category,
        string identity)
    {
        if (_compactMode != CompactSurfaceMode.EnglishToggle || IsControllerWrite)
        {
            return false;
        }

        return TryResolveByIdentity(category, identity, out var entry) &&
            Register(text, SurfaceKind.Compact, entry.Values, entry.CompactPolicy);
    }

    internal static bool RegisterTrustedCompact(
        TMP_Text text,
        EntityCategory category,
        string identity,
        string source)
    {
        if (_compactMode != CompactSurfaceMode.EnglishToggle || IsControllerWrite)
        {
            return false;
        }

        return TryResolve(category, identity, source, out var entry) &&
            Register(text, SurfaceKind.Compact, entry.Values, entry.CompactPolicy);
    }

    /// <summary>
    /// Source-only lookup is deliberately restricted to Map rows and trusted map/location
    /// producers. Ambiguous sources are excluded from the index.
    /// </summary>
    internal static bool RegisterTrustedMapOrLocationDetailBySource(
        TMP_Text text,
        string source)
    {
        if (_detailMode != DisplayMode.Bilingual || IsControllerWrite)
        {
            return false;
        }

        return TryResolveUniqueMapSource(source, out var entry) &&
            Register(text, SurfaceKind.Detail, entry.Values, entry.CompactPolicy);
    }

    /// <summary>
    /// Compact counterpart to RegisterTrustedMapOrLocationDetailBySource.
    /// </summary>
    internal static bool RegisterTrustedMapOrLocationCompactBySource(
        TMP_Text text,
        string source)
    {
        if (_compactMode != CompactSurfaceMode.EnglishToggle || IsControllerWrite)
        {
            return false;
        }

        return TryResolveUniqueMapSource(source, out var entry) &&
            Register(text, SurfaceKind.Compact, entry.Values, entry.CompactPolicy);
    }

    internal static bool PrepareTrustedMapOrLocationCompactWrite(
        TMP_Text text,
        string source,
        out string desired)
    {
        desired = null;
        if (_compactMode != CompactSurfaceMode.EnglishToggle || IsControllerWrite ||
            !TryResolveUniqueMapSource(source, out var entry))
        {
            return false;
        }

        return Register(
            text,
            SurfaceKind.Compact,
            entry.Values,
            entry.CompactPolicy,
            false,
            out desired);
    }

    internal static bool RegisterTrustedMapOrLocationCompositeCompactBySource(
        TMP_Text text,
        string mapSource,
        string dynamicEnglish,
        string dynamicChinese)
    {
        if (_compactMode != CompactSurfaceMode.EnglishToggle || IsControllerWrite ||
            !TryResolveUniqueMapSource(mapSource, out var entry) ||
            !TryNormalizeDynamicChinese(
                text,
                dynamicEnglish,
                dynamicChinese,
                out var normalizedChinese) ||
            !ContainsBaseEntity(entry, dynamicEnglish, normalizedChinese) ||
            !TryCreateDynamicValues(dynamicEnglish, normalizedChinese, out var values))
        {
            return false;
        }

        return Register(text, SurfaceKind.Compact, values, entry.CompactPolicy);
    }

    /// <summary>
    /// Setter-prefix variant: registers ownership without recursively writing TMP and returns the
    /// value that the original setter must receive. This prevents the original setter from
    /// overwriting a toggled-English value produced during its prefix.
    /// </summary>
    internal static bool PrepareTrustedMapOrLocationCompositeCompactWrite(
        TMP_Text text,
        string mapSource,
        string dynamicEnglish,
        string dynamicChinese,
        out string desired)
    {
        desired = null;
        if (_compactMode != CompactSurfaceMode.EnglishToggle || IsControllerWrite ||
            !TryResolveUniqueMapSource(mapSource, out var entry) ||
            !TryNormalizeDynamicChinese(
                text,
                dynamicEnglish,
                dynamicChinese,
                out var normalizedChinese) ||
            !ContainsBaseEntity(entry, dynamicEnglish, normalizedChinese) ||
            !TryCreateDynamicValues(dynamicEnglish, normalizedChinese, out var values))
        {
            return false;
        }

        return Register(
            text,
            SurfaceKind.Compact,
            values,
            entry.CompactPolicy,
            false,
            out desired);
    }

    /// <summary>
    /// Registers a composed entity title. The catalog key authenticates the base entity while
    /// the producer supplies complete English and Chinese names including affixes and enhancement
    /// markers. The composer validates protected rich-text, placeholder and numeric tokens.
    /// </summary>
    internal static bool RegisterTrustedCompositeEntityDetail(
        TMP_Text text,
        EntityCategory category,
        string identity,
        string catalogSource,
        string dynamicEnglish,
        string dynamicChinese)
    {
        if (_detailMode != DisplayMode.Bilingual || IsControllerWrite ||
            !IsCompositeCategory(category) ||
            !TryResolve(category, identity, catalogSource, out var entry) ||
            !TryNormalizeDynamicChinese(
                text,
                dynamicEnglish,
                dynamicChinese,
                out var normalizedChinese) ||
            !ContainsBaseEntity(entry, dynamicEnglish, normalizedChinese) ||
            !TryCreateDynamicValues(dynamicEnglish, normalizedChinese, out var values))
        {
            return false;
        }

        return Register(text, SurfaceKind.Detail, values, entry.CompactPolicy);
    }

    internal static bool RegisterTrustedCompositeEquipmentDetail(
        TMP_Text text,
        EntityCategory category,
        string identity,
        string catalogSource,
        string dynamicEnglish,
        string dynamicChinese)
    {
        if (_detailMode != DisplayMode.Bilingual || IsControllerWrite ||
            !IsEquipmentCategory(category) ||
            !TryResolve(category, identity, catalogSource, out var entry) ||
            !TryNormalizeDynamicChinese(
                text,
                dynamicEnglish,
                dynamicChinese,
                out var normalizedChinese) ||
            !ContainsBaseEntity(entry, dynamicEnglish, normalizedChinese) ||
            !TryCreateDynamicValues(dynamicEnglish, normalizedChinese, out var values))
        {
            return false;
        }

        return Register(text, SurfaceKind.Detail, values, entry.CompactPolicy);
    }

    /// <summary>
    /// Compact counterpart for a composed entity title.
    /// </summary>
    internal static bool RegisterTrustedCompositeEntityCompact(
        TMP_Text text,
        EntityCategory category,
        string identity,
        string catalogSource,
        string dynamicEnglish,
        string dynamicChinese)
    {
        if (_compactMode != CompactSurfaceMode.EnglishToggle || IsControllerWrite ||
            !IsCompositeCategory(category) ||
            !TryResolve(category, identity, catalogSource, out var entry) ||
            !TryNormalizeDynamicChinese(
                text,
                dynamicEnglish,
                dynamicChinese,
                out var normalizedChinese) ||
            !ContainsBaseEntity(entry, dynamicEnglish, normalizedChinese) ||
            !TryCreateDynamicValues(dynamicEnglish, normalizedChinese, out var values))
        {
            return false;
        }

        return Register(text, SurfaceKind.Compact, values, entry.CompactPolicy);
    }

    internal static bool RegisterTrustedCompositeEquipmentCompact(
        TMP_Text text,
        EntityCategory category,
        string identity,
        string catalogSource,
        string dynamicEnglish,
        string dynamicChinese)
    {
        if (_compactMode != CompactSurfaceMode.EnglishToggle || IsControllerWrite ||
            !IsEquipmentCategory(category) ||
            !TryResolve(category, identity, catalogSource, out var entry) ||
            !TryNormalizeDynamicChinese(
                text,
                dynamicEnglish,
                dynamicChinese,
                out var normalizedChinese) ||
            !ContainsBaseEntity(entry, dynamicEnglish, normalizedChinese) ||
            !TryCreateDynamicValues(dynamicEnglish, normalizedChinese, out var values))
        {
            return false;
        }

        return Register(text, SurfaceKind.Compact, values, entry.CompactPolicy);
    }

    /// <summary>
    /// Removes a pooled label before a producer repurposes it for non-entity or player content.
    /// Refresh also drops a registration automatically when its current text is no longer one of
    /// the three values owned by that registration.
    /// </summary>
    internal static void Unregister(TMP_Text text)
    {
        if (!IsEnabled || !IsMainThread || IsControllerWrite || text == null)
        {
            return;
        }

        try
        {
            RegisteredSurfaces.Remove(text.GetInstanceID());
        }
        catch (Exception exception)
        {
            ReportRuntimeFailure("unregister", exception);
        }
    }

    /// <summary>
    /// Harmony may attach this parameterless method as a postfix to one existing LateUpdate
    /// method. It polls one physical key press and toggles trusted compact labels; it does not
    /// create or inject a Unity component.
    /// </summary>
    public static void RefreshEnglishToggle()
    {
        if (!IsEnabled || !IsMainThread)
        {
            return;
        }

        try
        {
            if (_compactMode != CompactSurfaceMode.EnglishToggle || _keyPollingFailed)
            {
                _englishToggleKeyWasDown = false;
                RefreshEnglishToggleState(false);
                return;
            }

            // UIManager.LateUpdate can be invoked more than once in Unity's frame. GetKeyDown
            // remains true for that whole frame, so an unguarded poll could flip twice and flash.
            var frame = Time.frameCount;
            if (_lastEnglishTogglePollFrame == frame)
            {
                return;
            }
            _lastEnglishTogglePollFrame = frame;

            if (!BilingualDisplayConfiguration.TryConsumeCompactEnglishToggle(
                _compactMode,
                _compactEnglishEnabled,
                Input.GetKey(_englishToggleKey),
                ref _englishToggleKeyWasDown,
                out var compactEnglishEnabled))
            {
                return;
            }

            RefreshEnglishToggleState(compactEnglishEnabled);
        }
        catch (Exception exception)
        {
            _keyPollingFailed = true;
            _englishToggleKeyWasDown = false;
            RefreshEnglishToggleState(false);
            ReportRuntimeFailure("toggle-key polling", exception);
        }
    }

    /// <summary>
    /// Refresh entry for a producer patch that already owns input polling, and for deterministic
    /// runtime tests. Only compact registrations are changed by the toggle state.
    /// </summary>
    public static void RefreshEnglishToggleState(bool compactEnglishEnabled)
    {
        if (!IsEnabled || !IsMainThread || _refreshing)
        {
            return;
        }

        var stateChanged = _compactEnglishEnabled != compactEnglishEnabled;
        if (!stateChanged)
        {
            return;
        }
        _compactEnglishEnabled = compactEnglishEnabled;

        _refreshing = true;
        List<int> staleIds = null;
        try
        {
            foreach (var pair in RegisteredSurfaces)
            {
                var registration = pair.Value;
                if (!TryGetLiveText(registration, out var text) || IsProtectedText(text))
                {
                    AddStaleId(ref staleIds, pair.Key);
                    continue;
                }

                string current;
                try
                {
                    current = text.text;
                }
                catch
                {
                    AddStaleId(ref staleIds, pair.Key);
                    continue;
                }

                // A pooled component with new content is no longer ours. This check runs before
                // any write so a stale registration cannot overwrite a player/chat/query label.
                if (!registration.Owns(current))
                {
                    AddStaleId(ref staleIds, pair.Key);
                    continue;
                }

                if (registration.Kind != SurfaceKind.Compact)
                {
                    continue;
                }

                var desired = ComposeCompact(registration, compactEnglishEnabled);
                if (!TryWrite(text, desired))
                {
                    AddStaleId(ref staleIds, pair.Key);
                }
            }
        }
        catch (Exception exception)
        {
            ReportRuntimeFailure("compact refresh", exception);
        }
        finally
        {
            if (staleIds != null)
            {
                foreach (var instanceId in staleIds)
                {
                    RegisteredSurfaces.Remove(instanceId);
                }
            }
            _refreshing = false;
        }
    }

    /// <summary>
    /// TMP translation prefixes must return without translating while this reports true. That
    /// keeps presentation output from being fed back into partial translation.
    /// </summary>
    internal static bool IsInternalWrite(TMP_Text text)
    {
        if (_internalWriteIds == null || text == null)
        {
            return false;
        }

        try
        {
            return _internalWriteIds.Contains(text.GetInstanceID());
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsRegisteredDisplayValue(TMP_Text text)
    {
        if (!IsEnabled || !IsMainThread || text == null)
        {
            return false;
        }

        try
        {
            var instanceId = text.GetInstanceID();
            if (!RegisteredSurfaces.TryGetValue(instanceId, out var registration) ||
                !TryGetLiveText(registration, out var liveText) ||
                IsProtectedText(liveText) ||
                !EntityDisplayComposer.IsExpectedDisplayValue(
                    registration.Values,
                    registration.Kind == SurfaceKind.Detail,
                    registration.CompactPolicy,
                    _compactEnglishEnabled,
                    liveText.text))
            {
                RegisteredSurfaces.Remove(instanceId);
                return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static void Reset()
    {
        if (_mainThreadId != 0 && !IsMainThread)
        {
            return;
        }

        RestoreAndClearRegistrations();
        _catalog = EntityDisplayCatalog.Empty;
        _detailMode = DisplayMode.Chinese;
        _compactMode = CompactSurfaceMode.Chinese;
        _compactEnglishEnabled = false;
        _englishToggleKeyWasDown = false;
        _lastEnglishTogglePollFrame = -1;
        _registrationCalls = 0;
        _keyPollingFailed = false;
        _reportWarning = null;
        MapEntriesBySource.Clear();
        AmbiguousMapSources.Clear();
        EntriesByIdentity.Clear();
        CanonicalIdentityKeys.Clear();
        AmbiguousIdentityKeys.Clear();
        _mainThreadId = 0;
    }

    private static bool IsEnabled =>
        _detailMode == DisplayMode.Bilingual ||
        _compactMode == CompactSurfaceMode.EnglishToggle;

    private static bool IsControllerWrite =>
        _registrationDepth != 0 ||
        (_internalWriteIds != null && _internalWriteIds.Count != 0);

    private static bool IsMainThread =>
        _mainThreadId != 0 &&
        Thread.CurrentThread.ManagedThreadId == _mainThreadId;

    private static bool TryResolve(
        EntityCategory category,
        string identity,
        string source,
        out EntityDisplayEntry entry)
    {
        entry = null;
        return IsMainThread &&
            _catalog.TryGet(category, identity, source, out entry);
    }

    private static bool TryResolveByIdentity(
        EntityCategory category,
        string identity,
        out EntityDisplayEntry entry)
    {
        entry = null;
        if (!IsMainThread || string.IsNullOrEmpty(identity))
        {
            return false;
        }

        var key = new EntityIdentityKey(category, identity);
        return !AmbiguousIdentityKeys.Contains(key) &&
            EntriesByIdentity.TryGetValue(key, out entry);
    }

    private static bool TryResolveUniqueMapSource(
        string source,
        out EntityDisplayEntry entry)
    {
        entry = null;
        return IsMainThread &&
            !string.IsNullOrEmpty(source) &&
            !AmbiguousMapSources.Contains(source) &&
            MapEntriesBySource.TryGetValue(source, out entry);
    }

    private static bool TryCreateDynamicValues(
        string dynamicEnglish,
        string dynamicChinese,
        out EntityDisplayValues values)
    {
        values = null;
        try
        {
            values = EntityDisplayComposer.CreateValues(dynamicEnglish, dynamicChinese);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNormalizeDynamicChinese(
        TMP_Text text,
        string dynamicEnglish,
        string candidateChinese,
        out string normalizedChinese)
    {
        normalizedChinese = candidateChinese;
        if (!IsMainThread || text == null || string.IsNullOrEmpty(candidateChinese))
        {
            return false;
        }

        try
        {
            var instanceId = text.GetInstanceID();
            if (RegisteredSurfaces.TryGetValue(instanceId, out var registration) &&
                TryGetLiveText(registration, out var registeredText) &&
                registeredText.GetInstanceID() == instanceId)
            {
                if (string.Equals(
                        candidateChinese,
                        registration.Values.Bilingual,
                        StringComparison.Ordinal) ||
                    candidateChinese.StartsWith(
                        registration.Values.Bilingual + "\n",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        candidateChinese,
                        registration.Values.English,
                        StringComparison.Ordinal))
                {
                    normalizedChinese = registration.Values.Chinese;
                }
                return true;
            }
        }
        catch
        {
            return false;
        }

        // Presentation output must never become the input of a new presentation composition.
        return string.IsNullOrEmpty(dynamicEnglish) ||
            !candidateChinese.EndsWith(
                "\n" + dynamicEnglish,
                StringComparison.Ordinal);
    }

    private static bool ContainsBaseEntity(
        EntityDisplayEntry entry,
        string dynamicEnglish,
        string dynamicChinese)
    {
        return entry != null &&
            !string.IsNullOrEmpty(dynamicEnglish) &&
            !string.IsNullOrEmpty(dynamicChinese) &&
            ContainsEnglishEntity(dynamicEnglish, entry.Source) &&
            dynamicChinese.IndexOf(entry.Target, StringComparison.Ordinal) >= 0;
    }

    private static bool ContainsEnglishEntity(string value, string entity)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(entity))
        {
            return false;
        }

        var searchFrom = 0;
        while (searchFrom <= value.Length - entity.Length)
        {
            var index = value.IndexOf(entity, searchFrom, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var end = index + entity.Length;
            var leftBounded = index == 0 ||
                !char.IsLetterOrDigit(entity[0]) ||
                !char.IsLetterOrDigit(value[index - 1]);
            var rightBounded = end == value.Length ||
                !char.IsLetterOrDigit(entity[entity.Length - 1]) ||
                !char.IsLetterOrDigit(value[end]);
            if (leftBounded && rightBounded)
            {
                return true;
            }
            searchFrom = index + 1;
        }
        return false;
    }

    private static bool IsCompositeCategory(EntityCategory category)
    {
        return category == EntityCategory.Item ||
            category == EntityCategory.Equip ||
            category == EntityCategory.Artifact ||
            category == EntityCategory.Gem;
    }

    private static bool IsEquipmentCategory(EntityCategory category)
    {
        return category == EntityCategory.Item || category == EntityCategory.Equip;
    }

    private static bool Register(
        TMP_Text text,
        SurfaceKind kind,
        EntityDisplayValues values,
        CompactDisplayPolicy compactPolicy)
    {
        return Register(text, kind, values, compactPolicy, true, out _);
    }

    private static bool Register(
        TMP_Text text,
        SurfaceKind kind,
        EntityDisplayValues values,
        CompactDisplayPolicy compactPolicy,
        bool writeImmediately,
        out string desired)
    {
        desired = null;
        if (!IsMainThread || text == null || values == null)
        {
            return false;
        }

        _registrationDepth++;
        try
        {
            if (IsProtectedText(text))
            {
                return false;
            }

            _registrationCalls++;
            if (_registrationCalls % 128 == 0)
            {
                PruneStaleRegistrations();
            }

            var instanceId = text.GetInstanceID();
            var registration = new RegisteredSurface(
                instanceId,
                text,
                kind,
                values,
                compactPolicy);
            desired = kind == SurfaceKind.Detail
                ? values.Bilingual
                : ComposeCompact(registration, _compactEnglishEnabled);

            RegisteredSurfaces[instanceId] = registration;
            if (!writeImmediately)
            {
                return true;
            }
            if (TryWrite(text, desired))
            {
                return true;
            }

            RegisteredSurfaces.Remove(instanceId);
            return false;
        }
        catch (Exception exception)
        {
            ReportRuntimeFailure("producer registration", exception);
            return false;
        }
        finally
        {
            _registrationDepth--;
        }
    }

    private static string ComposeCompact(
        RegisteredSurface registration,
        bool compactEnglishEnabled)
    {
        return _compactMode == CompactSurfaceMode.EnglishToggle &&
            compactEnglishEnabled &&
            registration.CompactPolicy == CompactDisplayPolicy.EnglishOnHold
                ? registration.Values.English
                : registration.Values.Chinese;
    }

    private static bool TryWrite(TMP_Text text, string desired)
    {
        try
        {
            if (string.Equals(text.text, desired, StringComparison.Ordinal))
            {
                return true;
            }

            var instanceId = text.GetInstanceID();
            _internalWriteIds ??= new HashSet<int>();
            var added = _internalWriteIds.Add(instanceId);
            try
            {
                TmpFontFallbacks.Ensure(text, desired);
                text.text = desired;
                return string.Equals(text.text, desired, StringComparison.Ordinal);
            }
            finally
            {
                if (added)
                {
                    _internalWriteIds.Remove(instanceId);
                }
            }
        }
        catch (Exception exception)
        {
            ReportRuntimeFailure("TMP write", exception);
            return false;
        }
    }

    private static bool TryGetLiveText(
        RegisteredSurface registration,
        out TMP_Text text)
    {
        text = registration.Text;
        if (text == null)
        {
            return false;
        }

        try
        {
            return text.GetInstanceID() == registration.InstanceId;
        }
        catch
        {
            text = null;
            return false;
        }
    }

    private static bool IsInputText(TMP_Text text)
    {
        try
        {
            // Reject every TMP descendant of an input field, not only its configured textComponent.
            return text.GetComponentInParent<TMP_InputField>() != null;
        }
        catch
        {
            // A component that cannot prove it is outside an input field is not safe to register.
            return true;
        }
    }

    private static bool IsProtectedText(TMP_Text text)
    {
        if (IsInputText(text))
        {
            return true;
        }

        try
        {
            var gameObject = text.gameObject;
            var transform = text.transform;
            if (gameObject == null || transform == null)
            {
                return true;
            }

            var localName = gameObject.name ?? string.Empty;
            var ancestorNames = new List<string>(TmpTextContextResolver.MaxAncestorDepth);
            var ancestor = transform.parent;
            for (var depth = 0;
                 ancestor != null && depth < TmpTextContextResolver.MaxAncestorDepth;
                 depth++)
            {
                ancestorNames.Add(ancestor.gameObject?.name ?? string.Empty);
                ancestor = ancestor.parent;
            }

            var resolved = TmpTextContextResolver.Resolve(localName, ancestorNames);
            if (resolved.StartsWith("PlayerName:", StringComparison.Ordinal) ||
                ContainsProtectedMarker(localName))
            {
                return true;
            }
            foreach (var ancestorName in ancestorNames)
            {
                if (ContainsProtectedMarker(ancestorName))
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            // Unknown hierarchy state cannot prove that this is an entity display.
            return true;
        }
    }

    private static bool ContainsProtectedMarker(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var compact = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                compact.Append(char.ToLowerInvariant(character));
            }
        }

        var candidate = compact.ToString();
        foreach (var marker in ProtectedContentMarkers)
        {
            if (candidate.IndexOf(marker, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static void PruneStaleRegistrations()
    {
        List<int> staleIds = null;
        foreach (var pair in RegisteredSurfaces)
        {
            var registration = pair.Value;
            if (!TryGetLiveText(registration, out var text) || IsProtectedText(text))
            {
                AddStaleId(ref staleIds, pair.Key);
                continue;
            }

            try
            {
                if (!registration.Owns(text.text))
                {
                    AddStaleId(ref staleIds, pair.Key);
                }
            }
            catch
            {
                AddStaleId(ref staleIds, pair.Key);
            }
        }

        if (staleIds == null)
        {
            return;
        }
        foreach (var instanceId in staleIds)
        {
            RegisteredSurfaces.Remove(instanceId);
        }
    }

    private static void BuildLookupIndexes()
    {
        foreach (var entry in _catalog.Entries)
        {
            IndexIdentity(entry);
            if (entry.Category != EntityCategory.Map ||
                AmbiguousMapSources.Contains(entry.Source))
            {
                continue;
            }

            if (!MapEntriesBySource.TryGetValue(entry.Source, out var existing))
            {
                MapEntriesBySource.Add(entry.Source, entry);
                continue;
            }

            if (!string.Equals(existing.Identity, entry.Identity, StringComparison.Ordinal))
            {
                MapEntriesBySource.Remove(entry.Source);
                AmbiguousMapSources.Add(entry.Source);
            }
        }
    }

    private static void IndexIdentity(EntityDisplayEntry entry)
    {
        var key = new EntityIdentityKey(entry.Category, entry.Identity);
        var isCanonical = string.Equals(
            entry.Source,
            entry.Identity,
            StringComparison.Ordinal);
        if (isCanonical)
        {
            EntriesByIdentity[key] = entry;
            CanonicalIdentityKeys.Add(key);
            AmbiguousIdentityKeys.Remove(key);
            return;
        }

        if (CanonicalIdentityKeys.Contains(key) || AmbiguousIdentityKeys.Contains(key))
        {
            return;
        }

        if (EntriesByIdentity.ContainsKey(key))
        {
            EntriesByIdentity.Remove(key);
            AmbiguousIdentityKeys.Add(key);
            return;
        }

        EntriesByIdentity.Add(key, entry);
    }

    private static void RestoreAndClearRegistrations()
    {
        if (RegisteredSurfaces.Count == 0)
        {
            return;
        }

        foreach (var registration in RegisteredSurfaces.Values)
        {
            if (!TryGetLiveText(registration, out var text) || IsProtectedText(text))
            {
                continue;
            }

            try
            {
                if (registration.Owns(text.text))
                {
                    TryWrite(text, registration.Values.Chinese);
                }
            }
            catch
            {
                // Reset is fail-closed cleanup and must not interrupt plugin shutdown/reload.
            }
        }
        RegisteredSurfaces.Clear();
    }

    private static void AddStaleId(ref List<int> staleIds, int instanceId)
    {
        staleIds ??= new List<int>();
        staleIds.Add(instanceId);
    }

    private static void ReportRuntimeFailure(string operation, Exception exception)
    {
        if (_runtimeFailureReported || _reportWarning == null)
        {
            return;
        }

        _runtimeFailureReported = true;
        try
        {
            _reportWarning($"Bilingual display skipped an unsafe {operation}: {exception.Message}");
        }
        catch
        {
            // Diagnostics must never affect the game UI path.
        }
    }

    private readonly struct EntityIdentityKey : IEquatable<EntityIdentityKey>
    {
        private readonly EntityCategory _category;
        private readonly string _identity;

        internal EntityIdentityKey(EntityCategory category, string identity)
        {
            _category = category;
            _identity = identity;
        }

        public bool Equals(EntityIdentityKey other)
        {
            return _category == other._category &&
                string.Equals(_identity, other._identity, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EntityIdentityKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)_category * 397) ^
                    StringComparer.Ordinal.GetHashCode(_identity);
            }
        }
    }
}
