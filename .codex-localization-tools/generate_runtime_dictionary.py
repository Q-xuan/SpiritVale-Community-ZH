import argparse
import ast
import csv
import json
import re
import struct
from collections import Counter, defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
TOOLS = ROOT / ".codex-localization-tools"
OUTPUT = ROOT / "BepInEx/plugins/SpiritVale.RuntimeLocalization/translations.tsv"
CONFLICT_REPORT = TOOLS / "runtime-dictionary-conflicts.tsv"
SOURCE_RAW = TOOLS / "backups/addressables-game-config.raw"
SHARED_ASSETS = ROOT / "SpiritVale_Data/sharedassets0.assets"
SOURCE_SNAPSHOT = TOOLS / "artifacts/source-snapshot.json"
REVIEWED_ITEM_COMPONENTS = TOOLS / "reviewed-item-name-components.json"
MARKET_SEARCH_KEYWORD_OVERRIDES = TOOLS / "market-search-keyword-overrides.json"
MARKET_SEARCH_KEYWORD_PREFERENCES = TOOLS / "market-search-keyword-preferences.json"
MARKET_SEARCH_CONCEPT_ALIASES = TOOLS / "market-search-concept-aliases.json"

MARKET_SEARCH_TOKEN_PATTERN = re.compile(
    r"[A-Za-z0-9]+(?:['-][A-Za-z0-9]+)*"
)
MARKET_SEARCH_STOP_WORDS = {
    "a", "an", "and", "at", "by", "for", "from", "in", "of", "on", "or",
    "the", "to", "with",
}
MARKET_ITEM_TYPES = {
    "Junks": "Junk",
    "Consumables": "Consumable",
    "Equips": "Equip",
    "Artifacts": "Artifact",
    "Gems": "Gem",
    "Cosmetics": "Cosmetic",
}

EXTRA_ALIASES = {
    "Please open the Steam client and restart the game!": "请打开 Steam 客户端并重新启动游戏！",
    "Quit": "退出",
    "Potions": "药水袋",
    "Potion Pouch": "药水袋",
    "Novice Shoes": "新手鞋",
    "Novice Feet": "新手鞋",
}

# Entries are added only after reviewing every conflicting visible source string.
MANUAL_SOURCE_OVERRIDES = {
    "Arrowcatch Wall": "捕箭壁垒",
    "Artemis": "阿耳忒弥斯",
    "Blacksteel Blade": "黑钢之刃",
    "Blizzard": "暴风雪",
    "Blood Lust": "嗜血",
    "Blunderbuss": "火铳",
    "Bone Channeler": "骸骨导引者",
    "Brimblade": "炽焰之刃",
    "Broad Sword": "阔剑",
    "Cerulean Scepter": "蔚蓝权杖",
    "Chicky Hood": "小鸡兜帽",
    "Chompy Hood": "咬咬兜帽",
    "Crimson Plume": "深红羽翎",
    "Dawnstar": "晨星",
    "Destruction Staff": "毁灭法杖",
    "Elixir Gourd": "灵药葫芦",
    "Everfrost Staff": "永霜法杖",
    "Feathered Scout Hat": "羽饰斥候帽",
    "Fleetrunner": "疾行裤",
    "Force Shot": "强力射击",
    "Guardblade": "护卫之刃",
    "Hawkeye Crossbow": "鹰眼弩",
    "Hidden Strikes": "暗袭",
    "Launcher": "爆破发射器",
    "Litany of Wrath": "愤怒连祷",
    "Night Armor": "夜幕铠甲",
    "Nightfang Stud": "夜牙耳钉",
    "Oathbreaker": "背誓者",
    "Regal Tricorne": "皇家三角帽",
    "Sanctify": "圣化",
    "Sapphire Crown": "蓝宝石王冠",
    "Spineshard": "棘刺碎刃",
    "Stormburst Crossbow": "风暴爆裂弩",
    "Sweeping Order": "扫荡令",
    "Thunderbolt": "霹雳",
    "Unyielding": "不屈",
    "Willow Staff": "柳木法杖",
    "Zeal": "热忱",
    "Zephyr Cross": "西风十字",
    "Chirpy Hat": "啾啾帽",
    "Novice Chest": "新手胸甲",
    "Novice Legs": "新手护腿",
    "Sunflower Clip": "向日葵发夹",
    "Wooden Guard": "木制护盾",
}

# Keep the larger, hand-reviewed terminology table separate from this script so
# it can be updated without touching the source-key merge logic.
MANUAL_SOURCE_OVERRIDES.update(
    json.loads(
        (TOOLS / "runtime-manual-overrides.json").read_text(encoding="utf-8")
    )
)


def load_static_translations(path):
    module = ast.parse(path.read_text(encoding="utf-8"))
    for node in module.body:
        if not isinstance(node, ast.Assign):
            continue
        if any(isinstance(target, ast.Name) and target.id == "TRANSLATIONS" for target in node.targets):
            return ast.literal_eval(node.value)
    raise RuntimeError("Could not find the static TRANSLATIONS dictionary")


def load_source_texts():
    sources = {}
    for path in (TOOLS / "missing-zh-clean.tsv", TOOLS / "missing-zh-final.tsv"):
        with path.open(encoding="utf-8-sig", newline="") as handle:
            for row in csv.reader(handle, delimiter="\t"):
                if len(row) >= 3:
                    sources[row[0]] = row[2]
    return sources


def load_remaining_source_translations():
    path = TOOLS / "remaining-source-translations.json"
    if not path.exists():
        return {}
    values = json.loads(path.read_text(encoding="utf-8"))
    return {source: target for source, target in values.items() if target and target != source}


def load_reviewed_source_translations():
    """Convert the reviewed key table into source-level runtime entries."""
    reviewed_path = TOOLS / "missing-zh-reviewed.json"
    final_path = TOOLS / "missing-zh-final.tsv"
    if not reviewed_path.exists() or not final_path.exists():
        return {}

    reviewed = json.loads(reviewed_path.read_text(encoding="utf-8"))
    translations = {}
    with final_path.open(encoding="utf-8-sig", newline="") as handle:
        for row in csv.reader(handle, delimiter="\t"):
            if len(row) < 3 or row[0] not in reviewed:
                continue
            source = row[2].strip()
            target = reviewed[row[0]].strip()
            if source and target and source != target:
                previous = translations.get(source)
                if previous is not None and previous != target:
                    raise RuntimeError(
                        f"Reviewed source conflict: {source!r}: {previous!r} vs {target!r}"
                    )
                translations[source] = target
    return translations


def load_reviewed_source_overrides():
    path = TOOLS / "missing-zh-reviewed-source-overrides.json"
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def load_mmo_quality_overrides():
    path = TOOLS / "mmo-quality-overrides.json"
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def read_string(raw, offset):
    size = struct.unpack_from("<I", raw, offset)[0]
    end = (offset + 4 + size + 3) & ~3
    value = raw[offset + 4 : offset + 4 + size].decode("utf-8")
    return value, end


def read_config_field(raw, key, index):
    encoded_key = key.encode("utf-8")
    marker = struct.pack("<I", len(encoded_key)) + encoded_key
    offset = raw.find(marker)
    if offset < 0:
        raise RuntimeError(f"Missing localization key in backup: {key}")

    fields = []
    for _ in range(20):
        value, offset = read_string(raw, offset)
        fields.append(value)
    if fields[0] != key:
        raise RuntimeError(f"Localization key mismatch: {key}")
    return fields[index]


def build_key_translations():
    online = json.loads((TOOLS / "online-translations.json").read_text(encoding="utf-8"))
    glossary = json.loads((TOOLS / "glossary-translations.json").read_text(encoding="utf-8"))
    static = load_static_translations(TOOLS / "apply_game_data_localization.py")

    merged = {}
    origins = {}
    for origin, values in (("online", online), ("glossary", glossary), ("static", static)):
        for key, value in values.items():
            merged[key] = value
            origins[key] = origin
    return merged, origins, set(static)


def choose_translation(source, candidates, static_keys):
    if source in MANUAL_SOURCE_OVERRIDES:
        return MANUAL_SOURCE_OVERRIDES[source]

    static_targets = {target for key, target, _ in candidates if key in static_keys}
    if len(static_targets) == 1:
        return next(iter(static_targets))

    non_english = [(key, target, origin) for key, target, origin in candidates if target != source]
    targets = {target for _, target, _ in non_english or candidates}
    if len(targets) == 1:
        return next(iter(targets))

    counts = Counter(target for _, target, _ in non_english or candidates)
    if len(counts) > 1:
        most_common = counts.most_common()
        if most_common[0][1] > most_common[1][1]:
            return most_common[0][0]
    return None


def add_composite_runtime_aliases(resolved, snapshot, runtime_records, name_sources):
    generated = {}

    def add(source, target):
        if not source or not target or source == target:
            return
        previous = resolved.get(source, generated.get(source))
        if previous is not None and previous != target:
            return
        generated[source] = target

    artifact_sources = set()
    skill_sources = {}
    monster_targets = {}
    monster_ids = set()
    for entry in snapshot.get("entries", []):
        key = entry.get("key", "")
        source = entry.get("source", "")
        category = entry.get("category", "")
        if not key.endswith(".name") or not source or source not in resolved:
            continue
        if category == "Artifacts":
            artifact_sources.add(source)
        elif category == "Skills" and key.endswith(".name"):
            skill_target = resolved.get(source)
            if skill_target and skill_target != source:
                skill_sources[source] = skill_target
        elif category == "Monsters":
            monster_targets[source] = resolved[source]
            monster_ids.add(key.split(".", 1)[1][: -len(".name")])

    for source in artifact_sources:
        target = resolved[source]
        for suffix, target_suffix in (
            ("Rune", "符文"),
            ("Relic", "遗物"),
            ("Scroll", "卷轴"),
            ("Jewel", "宝石"),
        ):
            add(f"{source} {suffix}", target + target_suffix)
            if source == "Arcanum":
                for skill_source, skill_target in skill_sources.items():
                    add(
                        f"{source} {suffix} of {skill_source}",
                        f"{target}{target_suffix}：{skill_target}",
                    )
        add(f"{source} Artifact Set", target + "神器套装")

    for source, target in monster_targets.items():
        add(source + " Card", target + "卡片")

    runtime_target_by_id = {}
    for record in runtime_records:
        display = record["display"]
        target = resolved.get(display)
        if target:
            runtime_target_by_id.setdefault(record["internal_id"], {})[display] = target
    for internal_id in monster_ids:
        for display, target in runtime_target_by_id.get(internal_id, {}).items():
            if not display.endswith((" Card", " Pet", " Mount")):
                add(display + " Card", target + "卡片")

    for source, target in generated.items():
        resolved.setdefault(source, target)
    return len(generated)


def normalize_gem_name_translations(resolved, snapshot, protected_sources):
    """Keep a skill and its corresponding gem on the same reviewed term."""
    normalized = 0
    for entry in snapshot.get("entries", []):
        source = entry.get("source", "")
        if (
            entry.get("category") != "Gems"
            or not entry.get("key", "").endswith(".name")
            or not source.endswith(" Gem")
            or source in protected_sources
        ):
            continue
        base_source = source[: -len(" Gem")]
        base_target = resolved.get(base_source)
        if not base_target:
            continue
        expected = base_target + "宝石"
        if resolved.get(source) != expected:
            resolved[source] = expected
            normalized += 1
    return normalized


def build_item_name_metadata(resolved, snapshot):
    """Return reviewed affix and base-name sources for safe runtime composition."""
    affixes = set()
    base_names = set()
    for entry in snapshot.get("entries", []):
        key = entry.get("key", "")
        category = entry.get("category", "")
        source = entry.get("source", "")
        if not source or source not in resolved or resolved[source] == source:
            continue
        if key.endswith(".affix"):
            affixes.add(source)
        if category in {"Equips", "Cosmetics"} and key.endswith(".name"):
            base_names.add(source)
    reviewed = json.loads(REVIEWED_ITEM_COMPONENTS.read_text(encoding="utf-8"))
    for field, target in (
        ("item_affixes", affixes),
        ("item_base_names", base_names),
    ):
        values = reviewed.get(field, [])
        if not isinstance(values, list) or any(not isinstance(value, str) for value in values):
            raise RuntimeError(f"Invalid reviewed item component list: {field}")
        for source in values:
            if not source or source not in resolved or resolved[source] == source:
                raise RuntimeError(f"Reviewed item component is not translated: {field} {source!r}")
            target.add(source)
    return affixes, base_names


def build_market_search_name_metadata(resolved, snapshot, runtime_records):
    """Return the proven VendingListing.ItemType name whitelist by category."""
    item_categories = {
        "Junks",
        "Consumables",
        "Equips",
        "Artifacts",
        "Gems",
        "Cosmetics",
    }
    names_by_category = {category: set() for category in item_categories}
    names_by_category["Cards"] = set()
    for entry in snapshot.get("entries", []):
        category = entry.get("category", "")
        key = entry.get("key", "")
        source = entry.get("source", "")
        if (
            category in item_categories
            and key.endswith(".name")
            and source in resolved
            and resolved[source] != source
        ):
            names_by_category[category].add(source)
    for record in runtime_records:
        display = record.get("display", "")
        if display.endswith(" Card") and display in resolved and resolved[display] != display:
            names_by_category["Cards"].add(display)
    return names_by_category


def market_search_rule_matches(source, name):
    source_tokens = market_search_tokens(source)
    name_tokens = market_search_tokens(name)
    if contains_token_sequence(name_tokens, source_tokens):
        return True
    if len(source_tokens) != 1 or len(source_tokens[0]) < 4:
        return False
    folded = source_tokens[0].casefold()
    return any(token.casefold().startswith(folded) for token in name_tokens)


def build_market_search_entries(resolved, snapshot, runtime_records):
    """Build canonical VendingListing identities and their local search fields."""
    entries = {}

    def add(item_type, item_id, source):
        target = resolved.get(source, "")
        if not item_type or not item_id or not source or not target or target == source:
            return
        key = (item_type, item_id, source, target)
        entries.setdefault(key, set())

    for entry in snapshot.get("entries", []):
        category = entry.get("category", "")
        key = entry.get("key", "")
        source = entry.get("source", "")
        item_type = MARKET_ITEM_TYPES.get(category)
        if not item_type or not key.endswith(".name") or "." not in key:
            continue
        item_id = key.split(".", 1)[1][: -len(".name")]
        add(item_type, item_id, source)

    for record in runtime_records:
        source = record.get("display", "")
        if source.endswith(" Card"):
            add("Card", record.get("internal_id", ""), source)

    alias_rules = []
    concept_aliases = json.loads(
        MARKET_SEARCH_CONCEPT_ALIASES.read_text(encoding="utf-8")
    )
    if not isinstance(concept_aliases, dict):
        raise RuntimeError("Market search concept aliases must be a JSON object.")
    for source, aliases in concept_aliases.items():
        if not isinstance(source, str) or not market_search_tokens(source):
            raise RuntimeError(f"Invalid market search concept source: {source!r}")
        if not isinstance(aliases, list) or not aliases:
            raise RuntimeError(f"Invalid market search concept aliases: {source!r}")
        for alias in aliases:
            if (
                not isinstance(alias, str)
                or not alias.strip()
                or alias != alias.strip()
                or not any("\u3400" <= character <= "\u9fff" for character in alias)
                or any(character in alias for character in "\t\r\n")
            ):
                raise RuntimeError(
                    f"Invalid market search concept alias: {source!r} -> {alias!r}"
                )
            alias_rules.append((source, alias))

    preferences = json.loads(
        MARKET_SEARCH_KEYWORD_PREFERENCES.read_text(encoding="utf-8")
    )
    if not isinstance(preferences, dict):
        raise RuntimeError("Market search keyword preferences must be a JSON object.")
    for alias, source in preferences.items():
        if not isinstance(alias, str) or not isinstance(source, str):
            raise RuntimeError(f"Invalid market search preference migration row: {alias!r}")
        alias_rules.append((source, alias))

    matched_rules = set()
    for key, aliases in entries.items():
        source = key[2]
        for rule_source, alias in alias_rules:
            if market_search_rule_matches(rule_source, source):
                aliases.add(alias)
                matched_rules.add((rule_source, alias))
    unmatched_rules = sorted(set(alias_rules) - matched_rules)
    if unmatched_rules:
        raise RuntimeError(f"Market search alias rules matched no canonical item: {unmatched_rules}")

    return [
        {
            "item_type": key[0],
            "item_id": key[1],
            "source": key[2],
            "target": key[3],
            "aliases": tuple(sorted(aliases, key=lambda value: (value.casefold(), value))),
        }
        for key, aliases in sorted(
            entries.items(),
            key=lambda value: tuple(part.casefold() for part in value[0]),
        )
    ]


def market_search_tokens(value):
    return tuple(MARKET_SEARCH_TOKEN_PATTERN.findall(value))


def contains_token_sequence(container, candidate):
    if not candidate or len(candidate) > len(container):
        return False
    folded_container = tuple(token.casefold() for token in container)
    folded_candidate = tuple(token.casefold() for token in candidate)
    return any(
        folded_container[index : index + len(folded_candidate)] == folded_candidate
        for index in range(len(folded_container) - len(folded_candidate) + 1)
    )


def build_market_search_keyword_metadata(resolved, market_search_names):
    """Return reviewed Chinese aliases for English vending-search terms."""
    all_market_names = set().union(*market_search_names.values())
    market_token_sequences = [market_search_tokens(name) for name in all_market_names]
    keywords = set()

    for source, target in resolved.items():
        tokens = market_search_tokens(source)
        if (
            source in all_market_names
            or not 1 <= len(tokens) <= 3
            or source != " ".join(tokens)
            or (len(tokens) == 1 and tokens[0].casefold() in MARKET_SEARCH_STOP_WORDS)
            or not any("\u3400" <= character <= "\u9fff" for character in target)
            or not any(contains_token_sequence(name_tokens, tokens) for name_tokens in market_token_sequences)
        ):
            continue
        keywords.add((source, target))

    overrides = json.loads(MARKET_SEARCH_KEYWORD_OVERRIDES.read_text(encoding="utf-8"))
    if not isinstance(overrides, dict):
        raise RuntimeError("Market search keyword overrides must be a JSON object.")
    for source, aliases in overrides.items():
        tokens = market_search_tokens(source) if isinstance(source, str) else ()
        if (
            not tokens
            or source != " ".join(tokens)
            or not any(contains_token_sequence(name_tokens, tokens) for name_tokens in market_token_sequences)
            or not isinstance(aliases, list)
            or not aliases
        ):
            raise RuntimeError(f"Invalid market search keyword override: {source!r}")
        for alias in aliases:
            if (
                not isinstance(alias, str)
                or not alias.strip()
                or alias != alias.strip()
                or not any("\u3400" <= character <= "\u9fff" for character in alias)
                or any(character in alias for character in "\t\r\n")
            ):
                raise RuntimeError(
                    f"Invalid market search keyword alias: {source!r} -> {alias!r}"
                )
            keywords.add((source, alias))

    preferences = json.loads(
        MARKET_SEARCH_KEYWORD_PREFERENCES.read_text(encoding="utf-8")
    )
    if not isinstance(preferences, dict):
        raise RuntimeError("Market search keyword preferences must be a JSON object.")
    for target, source in preferences.items():
        tokens = market_search_tokens(source) if isinstance(source, str) else ()
        relevant_names = [
            name
            for name in all_market_names
            if target in resolved.get(name, "")
        ] if isinstance(target, str) else []
        if (
            not isinstance(target, str)
            or not target.strip()
            or target != target.strip()
            or not any("\u3400" <= character <= "\u9fff" for character in target)
            or any(character in target for character in "\t\r\n")
            or not tokens
            or source != " ".join(tokens)
            or len(tokens) > 3
            or any("\u3400" <= character <= "\u9fff" for character in source)
            or not relevant_names
            or not any(source.casefold() in name.casefold() for name in relevant_names)
        ):
            raise RuntimeError(
                f"Invalid market search keyword preference: {target!r} -> {source!r}"
            )
        keywords = {
            (keyword_source, keyword_target)
            for keyword_source, keyword_target in keywords
            if keyword_target != target
        }
        keywords.add((source, target))
    return keywords


def main(
    output=OUTPUT,
    conflict_report=CONFLICT_REPORT,
    source_raw=SOURCE_RAW,
    sharedassets=SHARED_ASSETS,
    source_snapshot=SOURCE_SNAPSHOT,
):
    key_translations, origins, static_keys = build_key_translations()
    sources = load_source_texts()
    remaining = load_remaining_source_translations()
    reviewed_remaining = load_reviewed_source_translations()
    reviewed_overrides = load_reviewed_source_overrides()
    quality_overrides = load_mmo_quality_overrides()
    backup = source_raw.read_bytes()

    candidates_by_source = defaultdict(list)
    for key, target in key_translations.items():
        source = sources.get(key)
        if source is None:
            source = read_config_field(backup, key, 3)
        candidates_by_source[source].append((key, target, origins[key]))

    resolved = {}
    unresolved = []
    for source, candidates in sorted(candidates_by_source.items()):
        target = choose_translation(source, candidates, static_keys)
        if target is None:
            unresolved.append((source, candidates))
        else:
            resolved[source] = target

    conflict_report.parent.mkdir(parents=True, exist_ok=True)
    with conflict_report.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(("source", "key", "origin", "candidate"))
        for source, candidates in unresolved:
            for key, target, origin in candidates:
                writer.writerow((source, key, origin, target))

    if unresolved:
        raise SystemExit(
            f"Refusing to write runtime dictionary: {len(unresolved)} source conflicts remain. "
            f"Review {conflict_report}."
        )

    # The remaining categories (monsters, junks, cards, NPCs and grimoires)
    # are not represented in the original key translation tables. Add their
    # source-level translations after resolving the keyed entries, while
    # allowing the reviewed terminology table to take precedence.
    for source, target in remaining.items():
        resolved.setdefault(source, target)
    for source, target in reviewed_remaining.items():
        resolved[source] = target

    resolved.update(EXTRA_ALIASES)
    resolved.update(MANUAL_SOURCE_OVERRIDES)
    resolved.update(reviewed_overrides)
    resolved.update(quality_overrides)

    snapshot = json.loads(source_snapshot.read_text(encoding="utf-8"))
    normalized_gem_count = normalize_gem_name_translations(
        resolved,
        snapshot,
        set(quality_overrides),
    )
    from runtime_name_aliases import (  # noqa: PLC0415
        extract_runtime_names,
        load_name_sources,
        resolve_runtime_aliases,
    )

    name_sources, _ = load_name_sources(snapshot)
    runtime_records, _ = extract_runtime_names(TOOLS, sharedassets, name_sources)
    runtime_aliases, unresolved_runtime, runtime_conflicts = resolve_runtime_aliases(
        runtime_records,
        name_sources,
        resolved,
    )
    if unresolved_runtime or runtime_conflicts:
        samples = [record["display"] for record in (unresolved_runtime + runtime_conflicts)[:20]]
        raise RuntimeError(
            "Runtime display-name aliases require review: "
            f"{len(unresolved_runtime)} unresolved, {len(runtime_conflicts)} conflicts. "
            f"Samples: {samples}"
        )
    for source, target in runtime_aliases.items():
        resolved.setdefault(source, target)
    composite_alias_count = add_composite_runtime_aliases(
        resolved,
        snapshot,
        runtime_records,
        name_sources,
    )
    item_affixes, item_base_names = build_item_name_metadata(resolved, snapshot)
    market_search_names = build_market_search_name_metadata(
        resolved,
        snapshot,
        runtime_records,
    )
    market_search_keywords = build_market_search_keyword_metadata(
        resolved,
        market_search_names,
    )
    market_search_entries = build_market_search_entries(
        resolved,
        snapshot,
        runtime_records,
    )
    for source, target in resolved.items():
        if not source or not target or "\t" in source or "\t" in target or "\n" in source or "\n" in target:
            raise RuntimeError(f"Unsafe translation entry: {source!r} -> {target!r}")

    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write("# SpiritVale visible text translations\n")
        for source in sorted(item_affixes, key=lambda value: (value.casefold(), value)):
            handle.write(f"#item-affix\t{source}\n")
        for source in sorted(item_base_names, key=lambda value: (value.casefold(), value)):
            handle.write(f"#item-base\t{source}\n")
        for category in sorted(market_search_names):
            for source in sorted(
                market_search_names[category],
                key=lambda value: (value.casefold(), value),
            ):
                handle.write(f"#market-search-name\t{category}\t{source}\n")
        for entry in market_search_entries:
            handle.write(
                "#market-search-entry\t"
                f"{entry['item_type']}\t{entry['item_id']}\t"
                f"{entry['source']}\t{entry['target']}\n"
            )
            for alias in entry["aliases"]:
                handle.write(
                    "#market-search-alias\t"
                    f"{entry['item_type']}\t{entry['item_id']}\t{alias}\n"
                )
        for source, target in sorted(
            market_search_keywords,
            key=lambda value: (
                value[0].casefold(), value[0], value[1].casefold(), value[1]
            ),
        ):
            handle.write(f"#market-search-keyword\t{source}\t{target}\n")
        for source in sorted(resolved, key=lambda value: (value.casefold(), value)):
            handle.write(f"{source}\t{resolved[source]}\n")
    print(
        f"Wrote {len(resolved)} runtime translations to {output} "
        f"({len(runtime_aliases)} asset aliases, {composite_alias_count} composite aliases, "
        f"{normalized_gem_count} normalized gem names, {len(item_affixes)} item affixes, "
        f"{len(item_base_names)} item bases, "
        f"{sum(len(values) for values in market_search_names.values())} market rows / "
            f"{len(set().union(*market_search_names.values()))} unique market names; "
            f"{len(market_search_entries)} canonical market entries / "
            f"{sum(len(entry['aliases']) for entry in market_search_entries)} concept aliases; "
        f"{len(market_search_keywords)} market keywords; "
        + ", ".join(
            f"{category}={len(market_search_names[category])}"
            for category in sorted(market_search_names)
        )
        + ")"
    )


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Build the SpiritVale runtime translation table.")
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--conflict-report", type=Path, default=CONFLICT_REPORT)
    parser.add_argument("--source-raw", type=Path, default=SOURCE_RAW)
    parser.add_argument("--sharedassets", type=Path, default=SHARED_ASSETS)
    parser.add_argument("--source-snapshot", type=Path, default=SOURCE_SNAPSHOT)
    args = parser.parse_args()
    main(
        output=args.output,
        conflict_report=args.conflict_report,
        source_raw=args.source_raw,
        sharedassets=args.sharedassets,
        source_snapshot=args.source_snapshot,
    )
