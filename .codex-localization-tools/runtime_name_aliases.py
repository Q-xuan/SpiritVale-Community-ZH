import struct
import sys
from collections import defaultdict


RAW_DATA_OFFSET = 28
MIN_RAW_SIZE = 80
MAX_STRING_SIZE = 4096


def read_string(raw, offset):
    if offset + 4 > len(raw):
        raise ValueError
    size = struct.unpack_from("<I", raw, offset)[0]
    if size > MAX_STRING_SIZE or offset + 4 + size > len(raw):
        raise ValueError
    value = raw[offset + 4 : offset + 4 + size].decode("utf-8")
    return value, (offset + 4 + size + 3) & ~3


def load_name_sources(snapshot):
    sources = defaultdict(set)
    categories = defaultdict(set)
    for entry in snapshot.get("entries", []):
        key = entry.get("key", "")
        source = entry.get("source", "")
        category = entry.get("category", "")
        if not key.endswith(".name") or "." not in key or not source:
            continue
        internal_id = key.split(".", 1)[1][: -len(".name")]
        if not internal_id:
            continue
        sources[internal_id].add(source)
        categories[internal_id].add(category)
    return dict(sources), dict(categories)


def extract_candidate(raw):
    if len(raw) <= MIN_RAW_SIZE:
        return None
    offset = RAW_DATA_OFFSET
    first_id, offset = read_string(raw, offset)
    second_id, offset = read_string(raw, offset)
    display, _ = read_string(raw, offset)
    if first_id != second_id or not display:
        return None
    return first_id, display


def extract_runtime_names(tool_root, sharedassets, name_sources):
    tool_root = str(tool_root.resolve())
    if tool_root not in sys.path:
        sys.path.insert(0, tool_root)
    import UnityPy  # noqa: PLC0415

    environment = UnityPy.load(str(sharedassets.resolve()))
    records = {}
    statistics = {
        "mono_behaviours": 0,
        "raw_candidates": 0,
        "parsed_candidates": 0,
    }
    for obj in environment.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        statistics["mono_behaviours"] += 1
        raw = obj.get_raw_data()
        if len(raw) <= MIN_RAW_SIZE:
            continue
        statistics["raw_candidates"] += 1
        try:
            candidate = extract_candidate(raw)
        except (UnicodeDecodeError, ValueError):
            continue
        if candidate is None:
            continue
        statistics["parsed_candidates"] += 1
        internal_id, display = candidate
        if internal_id not in name_sources:
            continue
        key = (internal_id, display)
        record = {
            "internal_id": internal_id,
            "display": display,
            "path_id": obj.path_id,
            "raw_size": len(raw),
        }
        previous = records.get(key)
        if previous is None or (record["raw_size"], record["path_id"]) > (
            previous["raw_size"],
            previous["path_id"],
        ):
            records[key] = record
    return sorted(
        records.values(),
        key=lambda record: (
            record["internal_id"].casefold(),
            record["internal_id"],
            record["display"].casefold(),
            record["display"],
        ),
    ), statistics


def resolve_runtime_aliases(records, name_sources, translations):
    aliases = {}
    conflicts = []
    pending = list(records)
    working = dict(translations)

    def translated(value):
        target = working.get(value)
        return target if target and target != value else None

    def choose_target(record):
        internal_id = record["internal_id"]
        display = record["display"]
        canonical_sources = sorted(name_sources[internal_id])

        exact = translated(display)
        if exact:
            return exact
        if display in canonical_sources:
            return translated(display)

        for suffix, target_suffix in ((" Card", "卡片"), (" Pet", "宠物"), (" Mount", "坐骑")):
            if not display.endswith(suffix):
                continue
            if suffix != " Card":
                canonical_targets = {
                    translated(source)
                    for source in canonical_sources
                    if source.endswith(suffix) and translated(source)
                }
                if len(canonical_targets) == 1:
                    return canonical_targets.pop()
            base_target = translated(display[: -len(suffix)])
            if base_target:
                return base_target + target_suffix

        targets = {
            translated(source)
            for source in canonical_sources
            if translated(source)
        }
        if len(targets) == 1:
            return targets.pop()
        return None

    while pending:
        next_pending = []
        progress = False
        for record in pending:
            display = record["display"]
            target = choose_target(record)
            if not target:
                next_pending.append(record)
                continue
            previous = aliases.get(display)
            if previous is not None and previous != target:
                conflicts.append(
                    {
                        **record,
                        "canonical_sources": sorted(name_sources[record["internal_id"]]),
                        "targets": [previous, target],
                    }
                )
                continue
            aliases[display] = target
            working[display] = target
            progress = True
        if not progress:
            pending = next_pending
            break
        pending = next_pending

    unresolved = [
        {
            **record,
            "canonical_sources": sorted(name_sources[record["internal_id"]]),
            "targets": sorted(
                {
                    translated(source)
                    for source in name_sources[record["internal_id"]]
                    if translated(source)
                }
            ),
        }
        for record in pending
    ]
    return aliases, unresolved, conflicts
