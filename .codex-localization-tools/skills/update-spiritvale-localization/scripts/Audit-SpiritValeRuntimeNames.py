import argparse
import csv
import hashlib
import json
import sys
from pathlib import Path


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


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


def main():
    parser = argparse.ArgumentParser(
        description="Audit runtime item and entity display aliases without modifying SpiritVale assets."
    )
    parser.add_argument("--tool-root", type=Path, required=True)
    parser.add_argument("--sharedassets", type=Path, required=True)
    parser.add_argument("--snapshot", type=Path, required=True)
    parser.add_argument("--dictionary", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--summary", type=Path, required=True)
    args = parser.parse_args()

    sys.path.insert(0, str(args.tool_root.resolve()))
    from runtime_name_aliases import (  # noqa: PLC0415
        extract_runtime_names,
        load_name_sources,
        resolve_runtime_aliases,
    )

    sharedassets = args.sharedassets.resolve()
    snapshot_path = args.snapshot.resolve()
    dictionary_path = args.dictionary.resolve()
    snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
    translations = load_dictionary(dictionary_path)
    name_sources, categories = load_name_sources(snapshot)
    records, statistics = extract_runtime_names(
        args.tool_root,
        sharedassets,
        name_sources,
    )
    aliases, unresolved, conflicts = resolve_runtime_aliases(
        records,
        name_sources,
        translations,
    )

    rows = []
    for record in records:
        internal_id = record["internal_id"]
        display = record["display"]
        expected = aliases.get(display, "")
        target = translations.get(display, "")
        status = "covered" if expected and target == expected else "unreviewed"
        rows.append(
            (
                internal_id,
                ",".join(sorted(categories[internal_id])),
                " | ".join(sorted(name_sources[internal_id])),
                display,
                expected,
                target,
                status,
                record["path_id"],
            )
        )

    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.summary.parent.mkdir(parents=True, exist_ok=True)
    with args.report.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(("id", "categories", "canonical", "display", "expected", "target", "status", "path_id"))
        writer.writerows(rows)

    covered = sum(row[6] == "covered" for row in rows)
    summary = {
        "schema_version": 1,
        "sharedassets_name": sharedassets.name,
        "sharedassets_sha256": sha256(sharedassets),
        "source_snapshot_name": snapshot_path.name,
        "source_snapshot_sha256": sha256(snapshot_path),
        "dictionary_name": dictionary_path.name,
        "dictionary_sha256": sha256(dictionary_path),
        **statistics,
        "snapshot_name_ids": len(name_sources),
        "resolved_runtime_ids": len({record["internal_id"] for record in records}),
        "runtime_display_strings": len(rows),
        "covered_display_strings": covered,
        "uncovered_display_strings": len(rows) - covered,
        "unresolved_aliases": len(unresolved),
        "conflicting_aliases": len(conflicts),
        "coverage_complete": bool(rows) and covered == len(rows) and not unresolved and not conflicts,
        "uncovered_samples": [row[3] for row in rows if row[6] != "covered"][:100],
        "unresolved_samples": unresolved[:25],
        "conflict_samples": conflicts[:25],
    }
    args.summary.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(
        f"Audited {len(rows)} runtime display strings across "
        f"{summary['resolved_runtime_ids']} source IDs: {covered} covered, "
        f"{len(rows) - covered} unreviewed, {len(conflicts)} conflicts."
    )


if __name__ == "__main__":
    main()
