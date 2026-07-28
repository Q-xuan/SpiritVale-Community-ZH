import argparse
import csv
import hashlib
import json
from collections import Counter, defaultdict
from pathlib import Path


CATALOG_HEADER = (
    "category",
    "identity",
    "source",
    "target",
    "compact_policy",
)

CATEGORY_ORDER = {
    "Item": 0,
    "Equip": 1,
    "Artifact": 2,
    "Gem": 3,
    "Skill": 4,
    "SkillPassive": 5,
    "Monster": 6,
    "Map": 7,
}

SOURCE_CATEGORY_MAP = {
    "Junks": "Item",
    "Consumables": "Item",
    "Cards": "Item",
    "Grimoires": "Item",
    "Equips": "Equip",
    "Cosmetics": "Equip",
    "Artifacts": "Artifact",
    "Gems": "Gem",
    "Skills": "Skill",
    "SkillPassives": "SkillPassive",
    "Monsters": "Monster",
}

MARKET_CATEGORY_MAP = {
    "Junk": "Item",
    "Consumable": "Item",
    "Card": "Item",
    "Equip": "Equip",
    "Cosmetic": "Equip",
    "Artifact": "Artifact",
    "Gem": "Gem",
}

COMPACT_POLICY_BY_CATEGORY = {
    category: "english-on-hold" for category in CATEGORY_ORDER
}

ALLOWED_COMPACT_POLICIES = {"chinese-only", "english-on-hold"}
PLAYER_CONTROLLED_LABELS = {
    "Player",
    "Character",
    "Guild",
    "Party",
    "Team",
    "Shop",
    "Chat",
    "Ranking",
}


class CatalogInputError(RuntimeError):
    pass


class CatalogCoverageError(RuntimeError):
    pass


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def read_json(path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise CatalogInputError(f"Could not read JSON input {path}: {exc}") from exc


def validate_cell(value, label):
    if not isinstance(value, str) or not value:
        raise CatalogInputError(f"Invalid {label}: {value!r}")
    if value != value.strip() or any(character in value for character in "\t\r\n\0"):
        raise CatalogInputError(f"Unsafe {label}: {value!r}")
    return value


def load_dictionary(path):
    translations = {}
    market_entries = []
    metadata_counts = Counter()
    with path.open(encoding="utf-8", newline="") as handle:
        for line_number, row in enumerate(csv.reader(handle, delimiter="\t"), 1):
            if not row:
                continue
            if row[0].startswith("#"):
                metadata_counts[row[0]] += 1
                if row[0] == "#market-search-entry":
                    if len(row) != 5:
                        raise CatalogInputError(
                            f"Invalid market entry at {path}:{line_number}"
                        )
                    market_entries.append(
                        {
                            "item_type": validate_cell(row[1], "market item type"),
                            "identity": validate_cell(row[2], "market identity"),
                            "source": validate_cell(row[3], "market source"),
                            "target": validate_cell(row[4], "market target"),
                        }
                    )
                continue
            if len(row) != 2:
                raise CatalogInputError(
                    f"Invalid translation row at {path}:{line_number}"
                )
            source = validate_cell(row[0], "translation source")
            target = validate_cell(row[1], "translation target")
            if source in translations:
                raise CatalogInputError(
                    f"Duplicate translation source at {path}:{line_number}: {source!r}"
                )
            translations[source] = target
    return translations, market_entries, metadata_counts


def load_tsv(path, expected_header):
    with path.open(encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle, delimiter="\t")
        actual_header = tuple(reader.fieldnames or ())
        if actual_header != tuple(expected_header):
            raise CatalogInputError(
                f"Unexpected TSV header in {path}: {actual_header!r}"
            )
        return list(reader)


def load_map_manifest(path):
    document = read_json(path)
    if document.get("schema_version") != 1:
        raise CatalogInputError(f"Unsupported map manifest schema in {path}")
    entries = document.get("entries")
    if not isinstance(entries, list) or not entries:
        raise CatalogInputError(f"Map manifest has no entries: {path}")
    seen = set()
    result = []
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            raise CatalogInputError(f"Invalid map entry {index} in {path}")
        identity = validate_cell(entry.get("identity"), "map identity")
        source = validate_cell(entry.get("source"), "map source")
        policy = entry.get("compact_policy", "english-on-hold")
        if policy not in ALLOWED_COMPACT_POLICIES:
            raise CatalogInputError(
                f"Invalid map compact policy at {path}:{index + 1}: {policy!r}"
            )
        key = (identity, source)
        if key in seen:
            raise CatalogInputError(f"Duplicate map entry in {path}: {key!r}")
        seen.add(key)
        result.append((identity, source, policy))
    return result


def validate_audit_summary(label, summary, source_hash, dictionary_hash):
    if summary.get("source_snapshot_sha256") != source_hash:
        raise CatalogInputError(
            f"{label} summary does not match the source snapshot"
        )
    if summary.get("dictionary_sha256") != dictionary_hash:
        raise CatalogInputError(f"{label} summary does not match the dictionary")
    if summary.get("coverage_complete") is not True:
        raise CatalogInputError(f"{label} audit coverage is incomplete")


class CatalogBuilder:
    def __init__(self, translations):
        self.translations = translations
        self.expected = defaultdict(set)
        self.rows = {}
        self.raw_origin_counts = Counter()

    def add(self, category, identity, source, origin, reported_target=None, policy=None):
        if category not in CATEGORY_ORDER:
            raise CatalogInputError(f"Unsupported bilingual category: {category!r}")
        identity = validate_cell(identity, "catalog identity")
        source = validate_cell(source, "catalog source")
        policy = policy or COMPACT_POLICY_BY_CATEGORY[category]
        if policy not in ALLOWED_COMPACT_POLICIES:
            raise CatalogInputError(f"Unsupported compact policy: {policy!r}")

        key = (category, identity, source)
        self.expected[key].add(origin)
        self.raw_origin_counts[origin] += 1
        target = self.translations.get(source)
        if not target or target == source:
            return
        validate_cell(target, "catalog target")
        if reported_target is not None and reported_target != target:
            raise CatalogInputError(
                f"Stale target for {category}/{identity}/{source}: "
                f"report has {reported_target!r}, dictionary has {target!r}"
            )
        row = (category, identity, source, target, policy)
        previous = self.rows.get(key)
        if previous is not None and previous != row:
            raise CatalogInputError(
                f"Conflicting catalog row for {category}/{identity}/{source}"
            )
        self.rows[key] = row


def collect_snapshot_entries(builder, snapshot):
    entries = snapshot.get("entries")
    if not isinstance(entries, list) or not entries:
        raise CatalogInputError("Source snapshot has no entries")
    seen_keys = set()
    for entry in entries:
        if not isinstance(entry, dict):
            raise CatalogInputError("Source snapshot contains a non-object entry")
        key = entry.get("key", "")
        if key in seen_keys:
            raise CatalogInputError(f"Duplicate source snapshot key: {key!r}")
        seen_keys.add(key)
        category = SOURCE_CATEGORY_MAP.get(entry.get("category", ""))
        if not category or not key.endswith(".name") or "." not in key:
            continue
        identity = key.split(".", 1)[1][:-len(".name")]
        builder.add(category, identity, entry.get("source", ""), "source-snapshot")


def collect_runtime_name_entries(builder, rows, summary):
    covered = 0
    for row in rows:
        status = row["status"]
        if status == "covered":
            covered += 1
        for source_category in filter(None, row["categories"].split(",")):
            category = SOURCE_CATEGORY_MAP.get(source_category)
            if not category:
                continue
            builder.add(
                category,
                row["id"],
                row["display"],
                "runtime-name-alias",
                reported_target=row["target"],
            )
    if len(rows) != int(summary.get("runtime_display_strings", -1)):
        raise CatalogInputError("Runtime name report row count does not match its summary")
    if covered != int(summary.get("covered_display_strings", -1)):
        raise CatalogInputError("Runtime name covered count does not match its summary")


def collect_skill_alias_entries(builder, rows, summary):
    covered = 0
    for row in rows:
        if row["status"] == "covered":
            covered += 1
        builder.add(
            "Skill",
            row["id"],
            row["display"],
            "runtime-skill-alias",
            reported_target=row["target"],
        )
    if len(rows) != int(summary.get("expected_skill_ids", -1)):
        raise CatalogInputError("Skill alias report row count does not match its summary")
    if covered != int(summary.get("covered_display_ids", -1)):
        raise CatalogInputError("Skill alias covered count does not match its summary")


def collect_market_entries(builder, entries):
    for entry in entries:
        category = MARKET_CATEGORY_MAP.get(entry["item_type"])
        if category is None:
            raise CatalogInputError(
                f"Unsupported market item type: {entry['item_type']!r}"
            )
        builder.add(
            category,
            entry["identity"],
            entry["source"],
            "market-canonical-entry",
            reported_target=entry["target"],
        )


def collect_map_entries(builder, entries):
    for identity, source, policy in entries:
        builder.add("Map", identity, source, "map-manifest", policy=policy)


def catalog_sort_key(row):
    return (
        CATEGORY_ORDER[row[0]],
        row[1].casefold(),
        row[1],
        row[2].casefold(),
        row[2],
    )


def make_collision_reports(rows):
    source_categories = defaultdict(set)
    target_sources = defaultdict(set)
    identity_sources = defaultdict(set)
    source_identities = defaultdict(set)
    for category, identity, source, target, _ in rows:
        source_categories[source].add(category)
        target_sources[target].add(source)
        identity_sources[(category, identity)].add(source)
        source_identities[(category, source)].add(identity)

    cross_category = [
        {
            "source": source,
            "categories": sorted(categories, key=lambda item: CATEGORY_ORDER[item]),
        }
        for source, categories in source_categories.items()
        if len(categories) > 1
    ]
    target_collisions = [
        {"target": target, "sources": sorted(sources, key=str.casefold)}
        for target, sources in target_sources.items()
        if len(sources) > 1
    ]
    identity_variants = [
        {
            "category": key[0],
            "identity": key[1],
            "sources": sorted(sources, key=str.casefold),
        }
        for key, sources in identity_sources.items()
        if len(sources) > 1
    ]
    source_identity_collisions = [
        {
            "category": key[0],
            "source": key[1],
            "identities": sorted(identities, key=str.casefold),
        }
        for key, identities in source_identities.items()
        if len(identities) > 1
    ]
    for report in (
        cross_category,
        target_collisions,
        identity_variants,
        source_identity_collisions,
    ):
        report.sort(key=lambda entry: json.dumps(entry, ensure_ascii=False))
    return cross_category, target_collisions, identity_variants, source_identity_collisions


def write_catalog(path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(CATALOG_HEADER)
        writer.writerows(rows)


def write_json(path, document):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(document, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def generate_catalog(
    source_snapshot,
    runtime_names,
    runtime_names_summary,
    skill_aliases,
    skill_aliases_summary,
    dictionary,
    map_manifest,
    catalog,
    audit,
):
    inputs = (
        source_snapshot,
        runtime_names,
        runtime_names_summary,
        skill_aliases,
        skill_aliases_summary,
        dictionary,
        map_manifest,
    )
    for path in inputs:
        if not path.is_file():
            raise CatalogInputError(f"Required bilingual catalog input is missing: {path}")

    source_hash = sha256(source_snapshot)
    dictionary_hash = sha256(dictionary)
    snapshot = read_json(source_snapshot)
    runtime_summary = read_json(runtime_names_summary)
    skill_summary = read_json(skill_aliases_summary)
    validate_audit_summary("Runtime name", runtime_summary, source_hash, dictionary_hash)
    validate_audit_summary("Skill alias", skill_summary, source_hash, dictionary_hash)
    if runtime_summary.get("sharedassets_sha256") != skill_summary.get("sharedassets_sha256"):
        raise CatalogInputError("Runtime name and skill alias audits use different shared assets")

    translations, market_entries, metadata_counts = load_dictionary(dictionary)
    runtime_rows = load_tsv(
        runtime_names,
        ("id", "categories", "canonical", "display", "expected", "target", "status", "path_id"),
    )
    skill_rows = load_tsv(
        skill_aliases,
        ("id", "canonical", "display", "target", "status"),
    )
    maps = load_map_manifest(map_manifest)

    builder = CatalogBuilder(translations)
    collect_snapshot_entries(builder, snapshot)
    collect_runtime_name_entries(builder, runtime_rows, runtime_summary)
    collect_skill_alias_entries(builder, skill_rows, skill_summary)
    collect_market_entries(builder, market_entries)
    collect_map_entries(builder, maps)

    rows = sorted(builder.rows.values(), key=catalog_sort_key)
    expected_by_category = Counter(key[0] for key in builder.expected)
    covered_by_category = Counter(row[0] for row in rows)
    missing_keys = sorted(
        set(builder.expected) - set(builder.rows),
        key=lambda key: (CATEGORY_ORDER[key[0]], key[1].casefold(), key[2].casefold()),
    )
    category_coverage = {
        category: {
            "expected": expected_by_category[category],
            "covered": covered_by_category[category],
            "missing": expected_by_category[category] - covered_by_category[category],
        }
        for category in CATEGORY_ORDER
    }

    write_catalog(catalog, rows)
    (
        cross_category,
        target_collisions,
        identity_variants,
        source_identity_collisions,
    ) = make_collision_reports(rows)
    bilingual_lengths = [len(target) + 1 + len(source) for _, _, source, target, _ in rows]
    player_controlled_rows = sum(
        row[0] in PLAYER_CONTROLLED_LABELS for row in rows
    )
    coverage_complete = bool(rows) and not missing_keys
    document = {
        "schema_version": 1,
        "steam_build_id": str(snapshot.get("steam_build_id", "")),
        "game_assembly_sha256": snapshot.get("game_assembly_sha256"),
        "metadata_sha256": snapshot.get("metadata_sha256"),
        "source_bundle_sha256": snapshot.get("bundle_sha256"),
        "sharedassets_sha256": runtime_summary.get("sharedassets_sha256"),
        "source_snapshot_name": source_snapshot.name,
        "source_snapshot_sha256": source_hash,
        "runtime_names_name": runtime_names.name,
        "runtime_names_sha256": sha256(runtime_names),
        "runtime_names_summary_name": runtime_names_summary.name,
        "runtime_names_summary_sha256": sha256(runtime_names_summary),
        "skill_aliases_name": skill_aliases.name,
        "skill_aliases_sha256": sha256(skill_aliases),
        "skill_aliases_summary_name": skill_aliases_summary.name,
        "skill_aliases_summary_sha256": sha256(skill_aliases_summary),
        "dictionary_name": dictionary.name,
        "dictionary_sha256": dictionary_hash,
        "map_manifest_name": map_manifest.name,
        "map_manifest_sha256": sha256(map_manifest),
        "catalog_name": catalog.name,
        "catalog_sha256": sha256(catalog),
        "catalog_rows": len(rows),
        "category_coverage": category_coverage,
        "coverage_complete": coverage_complete,
        "missing_rows": len(missing_keys),
        "missing_samples": [
            {
                "category": key[0],
                "identity": key[1],
                "source": key[2],
                "origins": sorted(builder.expected[key]),
            }
            for key in missing_keys[:100]
        ],
        "origin_input_rows": dict(sorted(builder.raw_origin_counts.items())),
        "compact_policy_counts": dict(
            sorted(Counter(row[4] for row in rows).items())
        ),
        "cross_category_source_groups": len(cross_category),
        "cross_category_source_samples": cross_category[:100],
        "target_collision_groups": len(target_collisions),
        "target_collision_samples": target_collisions[:100],
        "identity_source_variant_groups": len(identity_variants),
        "identity_source_variant_samples": identity_variants[:100],
        "source_identity_collision_groups": len(source_identity_collisions),
        "source_identity_collision_samples": source_identity_collisions[:100],
        "preview_lengths": {
            "maximum_bilingual_characters": max(bilingual_lengths, default=0),
            "over_24_characters": sum(length > 24 for length in bilingual_lengths),
            "over_32_characters": sum(length > 32 for length in bilingual_lengths),
            "over_40_characters": sum(length > 40 for length in bilingual_lengths),
        },
        "safety": {
            "lookup_direction": "category+identity+source->target",
            "reverse_lookup_generated": False,
            "player_controlled_rows": player_controlled_rows,
            "market_search_entry_rows_consumed": len(market_entries),
            "market_search_alias_rows_consumed": 0,
            "market_search_keyword_rows_consumed": 0,
            "dictionary_market_search_alias_rows": metadata_counts["#market-search-alias"],
            "dictionary_market_search_keyword_rows": metadata_counts["#market-search-keyword"],
        },
        "bilingual_gate_only": True,
        "pure_chinese_requires_catalog": False,
    }
    write_json(audit, document)
    if player_controlled_rows:
        raise CatalogInputError("Player-controlled categories entered the bilingual catalog")
    if not coverage_complete:
        raise CatalogCoverageError(
            f"Bilingual entity coverage is incomplete: {len(missing_keys)} rows missing"
        )
    return document


def main():
    parser = argparse.ArgumentParser(
        description="Generate the hash-bound SpiritVale bilingual entity catalog."
    )
    parser.add_argument("--source-snapshot", type=Path, required=True)
    parser.add_argument("--runtime-names", type=Path, required=True)
    parser.add_argument("--runtime-names-summary", type=Path, required=True)
    parser.add_argument("--skill-aliases", type=Path, required=True)
    parser.add_argument("--skill-aliases-summary", type=Path, required=True)
    parser.add_argument("--dictionary", type=Path, required=True)
    parser.add_argument("--map-manifest", type=Path, required=True)
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--audit", type=Path, required=True)
    args = parser.parse_args()
    document = generate_catalog(
        source_snapshot=args.source_snapshot.resolve(),
        runtime_names=args.runtime_names.resolve(),
        runtime_names_summary=args.runtime_names_summary.resolve(),
        skill_aliases=args.skill_aliases.resolve(),
        skill_aliases_summary=args.skill_aliases_summary.resolve(),
        dictionary=args.dictionary.resolve(),
        map_manifest=args.map_manifest.resolve(),
        catalog=args.catalog.resolve(),
        audit=args.audit.resolve(),
    )
    print(
        f"Generated {document['catalog_rows']} bilingual entity rows for "
        f"Steam Build {document['steam_build_id']}; coverage complete."
    )


if __name__ == "__main__":
    main()
