import argparse
import csv
import hashlib
import json
import struct
import sys
from collections import Counter
from pathlib import Path


RAW_DATA_OFFSET = 28
MIN_RAW_SIZE = 700


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def read_string(raw, offset):
    if offset + 4 > len(raw):
        raise ValueError
    size = struct.unpack_from("<I", raw, offset)[0]
    if size > 4096 or offset + 4 + size > len(raw):
        raise ValueError
    value = raw[offset + 4 : offset + 4 + size].decode("utf-8")
    return value, (offset + 4 + size + 3) & ~3


def load_dictionary(path):
    values = {}
    with path.open(encoding="utf-8", newline="") as handle:
        for line_number, row in enumerate(csv.reader(handle, delimiter="\t"), 1):
            if not row or row[0].startswith("#"):
                continue
            if len(row) != 2 or not row[0] or not row[1]:
                raise RuntimeError(f"Unsafe translation row at {path}:{line_number}")
            if row[0] in values:
                raise RuntimeError(f"Duplicate translation source at {path}:{line_number}: {row[0]!r}")
            values[row[0]] = row[1]
    return values


def load_skill_whitelist(path):
    snapshot = json.loads(path.read_text(encoding="utf-8"))
    whitelist = {}
    for entry in snapshot.get("entries", []):
        key = entry.get("key", "")
        if entry.get("category") != "Skills" or not key.startswith("skill.") or not key.endswith(".name"):
            continue
        skill_id = key[len("skill.") : -len(".name")]
        canonical = entry.get("source", "")
        if not skill_id or not canonical:
            raise RuntimeError(f"Invalid skill name entry in {path}: {key!r}")
        if skill_id in whitelist:
            raise RuntimeError(f"Duplicate skill ID in {path}: {skill_id!r}")
        whitelist[skill_id] = canonical
    if not whitelist:
        raise RuntimeError(f"No active skill IDs were extracted from {path}.")
    return snapshot, whitelist


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


def main():
    parser = argparse.ArgumentParser(
        description="Audit runtime skill display aliases in SpiritVale shared assets without modifying game assets."
    )
    parser.add_argument("--tool-root", type=Path, required=True)
    parser.add_argument("--sharedassets", type=Path, required=True)
    parser.add_argument("--snapshot", type=Path, required=True)
    parser.add_argument("--dictionary", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--summary", type=Path, required=True)
    args = parser.parse_args()

    sys.path.insert(0, str(args.tool_root.resolve()))
    import UnityPy  # noqa: PLC0415

    sharedassets = args.sharedassets.resolve()
    snapshot_path = args.snapshot.resolve()
    dictionary_path = args.dictionary.resolve()
    snapshot, whitelist = load_skill_whitelist(snapshot_path)
    translations = load_dictionary(dictionary_path)

    environment = UnityPy.load(str(sharedassets))
    selected = {}
    match_counts = Counter()
    mono_behaviours = 0
    raw_candidates = 0
    parsed_candidates = 0
    for obj in environment.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        mono_behaviours += 1
        raw = obj.get_raw_data()
        if len(raw) <= MIN_RAW_SIZE:
            continue
        raw_candidates += 1
        try:
            candidate = extract_candidate(raw)
        except (UnicodeDecodeError, ValueError):
            continue
        if candidate is None:
            continue
        parsed_candidates += 1
        skill_id, display = candidate
        if skill_id not in whitelist:
            continue
        match_counts[skill_id] += 1
        record = {
            "display": display,
            "path_id": obj.path_id,
            "raw_size": len(raw),
        }
        previous = selected.get(skill_id)
        if previous is None or (record["raw_size"], record["path_id"]) > (
            previous["raw_size"],
            previous["path_id"],
        ):
            selected[skill_id] = record

    rows = []
    for skill_id in sorted(whitelist, key=lambda value: (value.casefold(), value)):
        canonical = whitelist[skill_id]
        record = selected.get(skill_id)
        if record is None:
            rows.append((skill_id, canonical, "", "", "missing"))
            continue
        display = record["display"]
        target = translations.get(display, "")
        rows.append((skill_id, canonical, display, target, "covered" if target else "unreviewed"))

    covered = sum(row[4] == "covered" for row in rows)
    missing = [row[0] for row in rows if row[4] == "missing"]
    uncovered = [row[0] for row in rows if row[4] == "unreviewed"]
    alias_ids = [row[0] for row in rows if row[2] and row[1] != row[2]]
    duplicate_ids = sorted(skill_id for skill_id, count in match_counts.items() if count > 1)

    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.summary.parent.mkdir(parents=True, exist_ok=True)
    with args.report.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(("id", "canonical", "display", "target", "status"))
        writer.writerows(rows)

    summary = {
        "schema_version": 1,
        "sharedassets_name": sharedassets.name,
        "sharedassets_sha256": sha256(sharedassets),
        "sharedassets_size": sharedassets.stat().st_size,
        "source_snapshot_name": snapshot_path.name,
        "source_snapshot_sha256": sha256(snapshot_path),
        "source_bundle_sha256": snapshot.get("bundle_sha256"),
        "source_raw_sha256": snapshot.get("raw_sha256"),
        "dictionary_name": dictionary_path.name,
        "dictionary_sha256": sha256(dictionary_path),
        "mono_behaviours": mono_behaviours,
        "raw_size_threshold": MIN_RAW_SIZE,
        "raw_candidates": raw_candidates,
        "parsed_candidates": parsed_candidates,
        "expected_skill_ids": len(whitelist),
        "resolved_skill_ids": len(selected),
        "covered_display_ids": covered,
        "uncovered_display_ids": len(uncovered),
        "missing_skill_ids": len(missing),
        "runtime_alias_ids": len(alias_ids),
        "duplicate_skill_ids": len(duplicate_ids),
        "coverage_complete": covered == len(whitelist) and not missing and not uncovered,
        "missing_id_samples": missing[:50],
        "uncovered_id_samples": uncovered[:50],
        "runtime_alias_id_samples": alias_ids[:50],
        "duplicate_id_samples": duplicate_ids[:50],
    }
    args.summary.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(
        f"Audited {len(whitelist)} runtime skill aliases: {len(selected)} found, "
        f"{covered} display strings covered, {len(uncovered)} unreviewed, {len(missing)} missing."
    )


if __name__ == "__main__":
    main()
