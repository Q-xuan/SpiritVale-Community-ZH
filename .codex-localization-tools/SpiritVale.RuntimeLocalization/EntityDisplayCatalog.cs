using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SpiritVale.RuntimeLocalization;

internal enum EntityCategory
{
    Item,
    Equip,
    Artifact,
    Gem,
    Skill,
    SkillPassive,
    Monster,
    Map
}

internal enum CompactDisplayPolicy
{
    ChineseOnly,
    EnglishOnHold
}

internal sealed class EntityDisplayEntry
{
    internal EntityDisplayEntry(
        EntityCategory category,
        string identity,
        string source,
        string target,
        CompactDisplayPolicy compactPolicy)
    {
        Category = category;
        Identity = identity;
        Source = source;
        Target = target;
        CompactPolicy = compactPolicy;
        Values = EntityDisplayComposer.CreateValues(source, target);
    }

    internal EntityCategory Category { get; }
    internal string Identity { get; }
    internal string Source { get; }
    internal string Target { get; }
    internal CompactDisplayPolicy CompactPolicy { get; }
    internal EntityDisplayValues Values { get; }
}

internal sealed class EntityDisplayCatalog
{
    internal const string Header = "category\tidentity\tsource\ttarget\tcompact_policy";
    private const long MaximumFileBytes = 16L * 1024L * 1024L;
    private const int MaximumDataRows = 100000;
    private const int MaximumLineLength = 32768;

    private readonly IReadOnlyDictionary<EntityDisplayKey, EntityDisplayEntry> _entries;
    private readonly IReadOnlyCollection<EntityDisplayEntry> _entryValues;

    private EntityDisplayCatalog(
        IReadOnlyDictionary<EntityDisplayKey, EntityDisplayEntry> entries,
        IReadOnlyCollection<EntityDisplayEntry> entryValues)
    {
        _entries = entries;
        _entryValues = entryValues;
    }

    internal static EntityDisplayCatalog Empty { get; } = new EntityDisplayCatalog(
        new Dictionary<EntityDisplayKey, EntityDisplayEntry>(),
        Array.Empty<EntityDisplayEntry>());

    internal int Count => _entries.Count;
    internal IReadOnlyCollection<EntityDisplayEntry> Entries => _entryValues;

    internal static EntityDisplayCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Entity display catalog path must not be empty.", nameof(path));
        }
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Entity display catalog was not found.", path);
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"Entity display catalog exceeds the {MaximumFileBytes}-byte safety limit.");
        }

        var entries = new Dictionary<EntityDisplayKey, EntityDisplayEntry>();
        var values = new List<EntityDisplayEntry>();
        var strictUtf8 = new UTF8Encoding(false, true);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, strictUtf8, true, 4096, false);

        var lineNumber = 0;
        var headerRead = false;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Length > MaximumLineLength)
            {
                throw InvalidLine(lineNumber, "line exceeds the safety limit");
            }
            if (!headerRead)
            {
                if (!string.Equals(line, Header, StringComparison.Ordinal))
                {
                    throw InvalidLine(lineNumber, "header does not match the required schema");
                }
                headerRead = true;
                continue;
            }
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (values.Count >= MaximumDataRows)
            {
                throw new InvalidDataException(
                    $"Entity display catalog exceeds the {MaximumDataRows}-row safety limit.");
            }

            var fields = line.Split('\t');
            if (fields.Length != 5)
            {
                throw InvalidLine(lineNumber, "expected exactly five tab-separated fields");
            }
            for (var index = 0; index < fields.Length; index++)
            {
                ValidateField(fields[index], lineNumber, index + 1);
            }

            if (!TryParseCategory(fields[0], out var category))
            {
                throw InvalidLine(lineNumber, $"unknown category '{fields[0]}'");
            }
            if (!TryParseCompactPolicy(fields[4], out var compactPolicy))
            {
                throw InvalidLine(lineNumber, $"unknown compact policy '{fields[4]}'");
            }

            EntityDisplayEntry entry;
            try
            {
                entry = new EntityDisplayEntry(
                    category,
                    fields[1],
                    fields[2],
                    fields[3],
                    compactPolicy);
            }
            catch (InvalidDataException exception)
            {
                throw InvalidLine(lineNumber, exception.Message);
            }

            var key = new EntityDisplayKey(category, entry.Identity, entry.Source);
            if (!entries.TryAdd(key, entry))
            {
                throw InvalidLine(lineNumber, "duplicate category/identity/source key");
            }
            values.Add(entry);
        }

        if (!headerRead)
        {
            throw new InvalidDataException("Entity display catalog is empty or has no header.");
        }
        if (values.Count == 0)
        {
            throw new InvalidDataException("Entity display catalog contains no entity rows.");
        }

        return new EntityDisplayCatalog(entries, values.ToArray());
    }

    internal static bool TryLoad(
        string path,
        out EntityDisplayCatalog catalog,
        out string error)
    {
        try
        {
            catalog = Load(path);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            catalog = Empty;
            error = exception.Message;
            return false;
        }
    }

    internal bool TryGet(
        EntityCategory category,
        string identity,
        string source,
        out EntityDisplayEntry entry)
    {
        entry = null;
        if (!IsKnownCategory(category) ||
            string.IsNullOrEmpty(identity) ||
            string.IsNullOrEmpty(source))
        {
            return false;
        }

        return _entries.TryGetValue(
            new EntityDisplayKey(category, identity, source),
            out entry);
    }

    private static void ValidateField(string value, int lineNumber, int fieldNumber)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw InvalidLine(lineNumber, $"field {fieldNumber} is empty");
        }
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw InvalidLine(lineNumber, $"field {fieldNumber} has leading or trailing whitespace");
        }
        foreach (var character in value)
        {
            if (character < ' ' || character == '\u007f')
            {
                throw InvalidLine(lineNumber, $"field {fieldNumber} contains a control character");
            }
        }
    }

    private static bool TryParseCategory(string value, out EntityCategory category)
    {
        switch (value)
        {
            case nameof(EntityCategory.Item):
                category = EntityCategory.Item;
                return true;
            case nameof(EntityCategory.Equip):
                category = EntityCategory.Equip;
                return true;
            case nameof(EntityCategory.Artifact):
                category = EntityCategory.Artifact;
                return true;
            case nameof(EntityCategory.Gem):
                category = EntityCategory.Gem;
                return true;
            case nameof(EntityCategory.Skill):
                category = EntityCategory.Skill;
                return true;
            case nameof(EntityCategory.SkillPassive):
                category = EntityCategory.SkillPassive;
                return true;
            case nameof(EntityCategory.Monster):
                category = EntityCategory.Monster;
                return true;
            case nameof(EntityCategory.Map):
                category = EntityCategory.Map;
                return true;
            default:
                category = default;
                return false;
        }
    }

    private static bool TryParseCompactPolicy(
        string value,
        out CompactDisplayPolicy policy)
    {
        switch (value)
        {
            case "chinese-only":
                policy = CompactDisplayPolicy.ChineseOnly;
                return true;
            case "english-on-hold":
                policy = CompactDisplayPolicy.EnglishOnHold;
                return true;
            default:
                policy = default;
                return false;
        }
    }

    private static bool IsKnownCategory(EntityCategory category)
    {
        return category >= EntityCategory.Item && category <= EntityCategory.Map;
    }

    private static InvalidDataException InvalidLine(int lineNumber, string reason)
    {
        return new InvalidDataException(
            $"Invalid entity display catalog line {lineNumber}: {reason}.");
    }

    private readonly struct EntityDisplayKey : IEquatable<EntityDisplayKey>
    {
        private readonly EntityCategory _category;
        private readonly string _identity;
        private readonly string _source;

        internal EntityDisplayKey(EntityCategory category, string identity, string source)
        {
            _category = category;
            _identity = identity;
            _source = source;
        }

        public bool Equals(EntityDisplayKey other)
        {
            return _category == other._category &&
                string.Equals(_identity, other._identity, StringComparison.Ordinal) &&
                string.Equals(_source, other._source, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EntityDisplayKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)_category;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_identity);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_source);
                return hash;
            }
        }
    }
}
