import argparse
import csv
import hashlib
import json
import re
import struct
import sys
from collections import defaultdict
from pathlib import Path


KEY = re.compile(r"^[A-Za-z][A-Za-z0-9_. -]+$")
MARKER = b"archetype.Acolyte.name"


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def sha256_bytes(value):
    return hashlib.sha256(value).hexdigest().upper()


def read_string(raw, offset):
    if offset + 4 > len(raw):
        raise ValueError
    size = struct.unpack_from("<I", raw, offset)[0]
    if size > 4096 or offset + 4 + size > len(raw):
        raise ValueError
    value = raw[offset + 4 : offset + 4 + size].decode("utf-8")
    return value, (offset + 4 + size + 3) & ~3


def extract_entries(raw):
    entries = {}
    for start in range(0, len(raw) - 80, 4):
        try:
            fields = []
            offset = start
            for _ in range(20):
                value, offset = read_string(raw, offset)
                fields.append(value)
        except (UnicodeDecodeError, ValueError):
            continue
        if (
            not KEY.fullmatch(fields[0])
            or "." not in fields[0]
            or not fields[0].split(".", 1)[0].islower()
            or not fields[1]
            or len(fields[1]) > 32
            or not all(character.isalpha() or character in " _-" for character in fields[1])
            or not fields[3]
        ):
            continue
        entry = {
            "key": fields[0],
            "category": fields[1],
            "source": fields[3],
            "simplified": fields[8],
            "traditional": fields[19],
        }
        previous = entries.get(fields[0])
        if previous is not None and previous != entry:
            raise RuntimeError(f"Conflicting serialized entries for key {fields[0]!r}")
        entries[fields[0]] = entry
    return [entries[key] for key in sorted(entries, key=lambda value: (value.casefold(), value))]


def load_dictionary(path):
    values = {}
    if not path.exists():
        return values
    with path.open(encoding="utf-8", newline="") as handle:
        for line_number, row in enumerate(csv.reader(handle, delimiter="\t"), 1):
            if not row or row[0].startswith("#"):
                continue
            if len(row) != 2:
                raise RuntimeError(f"Unsafe translation row at {path}:{line_number}")
            if row[0] in values:
                raise RuntimeError(f"Duplicate translation source at {path}:{line_number}: {row[0]!r}")
            values[row[0]] = row[1]
    return values


def main():
    parser = argparse.ArgumentParser(description="Audit current SpiritVale localization sources without modifying game assets.")
    parser.add_argument("--tool-root", type=Path, required=True)
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--dictionary", type=Path, required=True)
    parser.add_argument("--baseline-raw", type=Path, required=True)
    parser.add_argument("--raw-output", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--summary", type=Path, required=True)
    parser.add_argument("--snapshot", type=Path, required=True)
    parser.add_argument("--build-id", required=True)
    parser.add_argument("--game-assembly-hash", required=True)
    parser.add_argument("--metadata-hash", required=True)
    args = parser.parse_args()

    sys.path.insert(0, str(args.tool_root.resolve()))
    import UnityPy  # noqa: PLC0415

    bundle = args.bundle.resolve()
    environment = UnityPy.load(str(bundle))
    matches = []
    for obj in environment.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        raw = obj.get_raw_data()
        if MARKER in raw:
            matches.append((obj.path_id, raw))
    if len(matches) != 1:
        raise RuntimeError(f"Expected one game config object in {bundle}; found {len(matches)}")

    object_id, raw = matches[0]
    all_entries = extract_entries(raw)
    entries = [entry for entry in all_entries if not entry["simplified"]]
    baseline_entries = extract_entries(args.baseline_raw.read_bytes()) if args.baseline_raw.exists() else []
    current_by_key = {entry["key"]: entry for entry in all_entries}
    baseline_by_key = {entry["key"]: entry for entry in baseline_entries}
    added_keys = sorted(current_by_key.keys() - baseline_by_key.keys())
    removed_keys = sorted(baseline_by_key.keys() - current_by_key.keys())
    shared_keys = current_by_key.keys() & baseline_by_key.keys()
    source_changed = sorted(key for key in shared_keys if current_by_key[key]["source"] != baseline_by_key[key]["source"])
    simplified_changed = sorted(key for key in shared_keys if current_by_key[key]["simplified"] != baseline_by_key[key]["simplified"])
    translations = load_dictionary(args.dictionary)
    covered = [entry for entry in entries if entry["source"] in translations]
    uncovered = [entry for entry in entries if entry["source"] not in translations]
    category_coverage = defaultdict(lambda: {"total": 0, "covered": 0, "uncovered": 0})
    for entry in entries:
        values = category_coverage[entry["category"]]
        values["total"] += 1
        if entry["source"] in translations:
            values["covered"] += 1
        else:
            values["uncovered"] += 1

    args.raw_output.parent.mkdir(parents=True, exist_ok=True)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.summary.parent.mkdir(parents=True, exist_ok=True)
    args.snapshot.parent.mkdir(parents=True, exist_ok=True)
    if not args.raw_output.exists() or args.raw_output.read_bytes() != raw:
        args.raw_output.write_bytes(raw)
    with args.report.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(("status", "key", "category", "source", "traditional"))
        for entry in entries:
            writer.writerow((
                "covered" if entry["source"] in translations else "unreviewed",
                entry["key"],
                entry["category"],
                entry["source"],
                entry["traditional"],
            ))

    summary = {
        "schema_version": 1,
        "bundle_name": bundle.name,
        "steam_build_id": args.build_id,
        "game_assembly_sha256": args.game_assembly_hash,
        "metadata_sha256": args.metadata_hash,
        "bundle_sha256": sha256(bundle),
        "bundle_size": bundle.stat().st_size,
        "object_path_id": object_id,
        "raw_sha256": sha256_bytes(raw),
        "baseline_raw_sha256": sha256(args.baseline_raw) if args.baseline_raw.exists() else None,
        "raw_matches_baseline": args.baseline_raw.exists() and sha256_bytes(raw) == sha256(args.baseline_raw),
        "dictionary_sha256": sha256(args.dictionary) if args.dictionary.exists() else None,
        "config_entries": len(all_entries),
        "missing_simplified_entries": len(entries),
        "covered_entries": len(covered),
        "uncovered_entries": len(uncovered),
        "uncovered_sources": len({entry["source"] for entry in uncovered}),
        "category_coverage": dict(sorted(category_coverage.items())),
        "added_keys": len(added_keys),
        "removed_keys": len(removed_keys),
        "source_changed_keys": len(source_changed),
        "simplified_changed_keys": len(simplified_changed),
        "added_key_samples": added_keys[:50],
        "removed_key_samples": removed_keys[:50],
        "source_changed_key_samples": source_changed[:50],
        "simplified_changed_key_samples": simplified_changed[:50],
    }
    snapshot = {
        "schema_version": 1,
        "bundle_name": bundle.name,
        "steam_build_id": args.build_id,
        "game_assembly_sha256": args.game_assembly_hash,
        "metadata_sha256": args.metadata_hash,
        "bundle_sha256": summary["bundle_sha256"],
        "object_path_id": object_id,
        "raw_sha256": summary["raw_sha256"],
        "entries": all_entries,
    }
    args.snapshot.write_text(json.dumps(snapshot, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    args.summary.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(
        f"Audited {len(entries)} missing-Simplified-Chinese entries: "
        f"{len(covered)} covered, {len(uncovered)} unreviewed "
        f"({summary['uncovered_sources']} unique sources)."
    )


if __name__ == "__main__":
    main()
