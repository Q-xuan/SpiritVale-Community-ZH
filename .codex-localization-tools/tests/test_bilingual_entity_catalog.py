import csv
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


TOOLS = Path(__file__).resolve().parents[1]
GENERATOR_PATH = (
    TOOLS
    / "skills"
    / "update-spiritvale-localization"
    / "scripts"
    / "Generate-SpiritValeBilingualCatalog.py"
)
SPEC = importlib.util.spec_from_file_location("bilingual_catalog_generator", GENERATOR_PATH)
GENERATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GENERATOR)


def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


class BilingualCatalogTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.snapshot = self.root / "source-snapshot.json"
        self.dictionary = self.root / "translations.tsv"
        self.runtime_names = self.root / "runtime-name-aliases.tsv"
        self.runtime_summary = self.root / "runtime-name-aliases.json"
        self.skill_aliases = self.root / "runtime-skill-aliases.tsv"
        self.skill_summary = self.root / "runtime-skill-aliases.json"
        self.maps = self.root / "bilingual-map-entities.json"
        self.catalog = self.root / "bilingual-entity-catalog.tsv"
        self.audit = self.root / "bilingual-entity-catalog.audit.json"
        self.write_fixture()

    def tearDown(self):
        self.temp.cleanup()

    def write_fixture(self):
        snapshot = {
            "schema_version": 1,
            "steam_build_id": "12345",
            "game_assembly_sha256": "A" * 64,
            "metadata_sha256": "B" * 64,
            "bundle_sha256": "C" * 64,
            "entries": [
                {
                    "key": "equip.Sword.name",
                    "category": "Equips",
                    "source": "Guide Sword",
                    "simplified": "",
                },
                {
                    "key": "skill.Fire.name",
                    "category": "Skills",
                    "source": "Fireball",
                    "simplified": "",
                },
                {
                    "key": "monster.Flame.name",
                    "category": "Monsters",
                    "source": "Flame",
                    "simplified": "",
                },
            ],
        }
        self.snapshot.write_text(
            json.dumps(snapshot, ensure_ascii=False), encoding="utf-8"
        )
        self.dictionary.write_text(
            "# SpiritVale visible text translations\n"
            "#market-search-entry\tEquip\tSword\tGuide Sword\t\u6307\u5f15\u4e4b\u5251\n"
            "#market-search-alias\tEquip\tSword\t\u653b\u7565\u5251\n"
            "#market-search-keyword\tGuide\t\u6307\u5f15\n"
            "Fireball\t\u706b\u7403\u672f\n"
            "Flame\t\u706b\u7130\n"
            "Flame Beast\t\u706b\u7130\n"
            "Guide Sword\t\u6307\u5f15\u4e4b\u5251\n"
            "Sunny Meadows 2\t\u9633\u5149\u8349\u7538 2\n",
            encoding="utf-8",
        )
        self.runtime_names.write_text(
            "id\tcategories\tcanonical\tdisplay\texpected\ttarget\tstatus\tpath_id\n"
            "Flame\tMonsters\tFlame\tFlame Beast\t\u706b\u7130\t\u706b\u7130\tcovered\t10\n",
            encoding="utf-8",
        )
        self.skill_aliases.write_text(
            "id\tcanonical\tdisplay\ttarget\tstatus\n"
            "Fire\tFireball\tFireball\t\u706b\u7403\u672f\tcovered\n",
            encoding="utf-8",
        )
        source_hash = file_hash(self.snapshot)
        dictionary_hash = file_hash(self.dictionary)
        common = {
            "schema_version": 1,
            "sharedassets_sha256": "D" * 64,
            "source_snapshot_sha256": source_hash,
            "dictionary_sha256": dictionary_hash,
            "coverage_complete": True,
        }
        self.runtime_summary.write_text(
            json.dumps(
                {
                    **common,
                    "runtime_display_strings": 1,
                    "covered_display_strings": 1,
                }
            ),
            encoding="utf-8",
        )
        self.skill_summary.write_text(
            json.dumps(
                {
                    **common,
                    "expected_skill_ids": 1,
                    "covered_display_ids": 1,
                }
            ),
            encoding="utf-8",
        )
        self.maps.write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "entries": [
                        {
                            "identity": "Meadow_2",
                            "source": "Sunny Meadows 2",
                        }
                    ],
                }
            ),
            encoding="utf-8",
        )

    def generate(self):
        return GENERATOR.generate_catalog(
            source_snapshot=self.snapshot,
            runtime_names=self.runtime_names,
            runtime_names_summary=self.runtime_summary,
            skill_aliases=self.skill_aliases,
            skill_aliases_summary=self.skill_summary,
            dictionary=self.dictionary,
            map_manifest=self.maps,
            catalog=self.catalog,
            audit=self.audit,
        )

    def read_rows(self):
        with self.catalog.open(encoding="utf-8", newline="") as handle:
            return list(csv.DictReader(handle, delimiter="\t"))

    def test_generates_strict_forward_catalog_and_hash_bindings(self):
        audit = self.generate()
        rows = self.read_rows()
        self.assertEqual(tuple(rows[0]), GENERATOR.CATALOG_HEADER)
        self.assertIn(
            {
                "category": "Map",
                "identity": "Meadow_2",
                "source": "Sunny Meadows 2",
                "target": "\u9633\u5149\u8349\u7538 2",
                "compact_policy": "english-on-hold",
            },
            rows,
        )
        self.assertEqual(audit["steam_build_id"], "12345")
        self.assertEqual(audit["source_snapshot_sha256"], file_hash(self.snapshot))
        self.assertEqual(audit["runtime_names_sha256"], file_hash(self.runtime_names))
        self.assertEqual(audit["skill_aliases_sha256"], file_hash(self.skill_aliases))
        self.assertEqual(audit["dictionary_sha256"], file_hash(self.dictionary))
        self.assertEqual(audit["catalog_sha256"], file_hash(self.catalog))
        self.assertTrue(audit["coverage_complete"])
        self.assertFalse(audit["safety"]["reverse_lookup_generated"])
        self.assertFalse(audit["pure_chinese_requires_catalog"])

    def test_keeps_distinct_english_sources_that_share_a_chinese_target(self):
        audit = self.generate()
        monster_sources = {
            row["source"]
            for row in self.read_rows()
            if row["category"] == "Monster"
        }
        self.assertEqual(monster_sources, {"Flame", "Flame Beast"})
        self.assertEqual(audit["target_collision_groups"], 1)

    def test_does_not_consume_search_aliases_or_player_text(self):
        audit = self.generate()
        sources = {row["source"] for row in self.read_rows()}
        self.assertNotIn("\u653b\u7565\u5251", sources)
        self.assertNotIn("Guide", sources)
        self.assertEqual(audit["safety"]["market_search_alias_rows_consumed"], 0)
        self.assertEqual(audit["safety"]["market_search_keyword_rows_consumed"], 0)
        self.assertEqual(audit["safety"]["player_controlled_rows"], 0)

    def test_rejects_stale_runtime_summary(self):
        summary = json.loads(self.runtime_summary.read_text(encoding="utf-8"))
        summary["dictionary_sha256"] = "0" * 64
        self.runtime_summary.write_text(json.dumps(summary), encoding="utf-8")
        with self.assertRaises(GENERATOR.CatalogInputError):
            self.generate()

    def test_missing_entity_target_writes_incomplete_audit_and_fails(self):
        maps = json.loads(self.maps.read_text(encoding="utf-8"))
        maps["entries"].append(
            {"identity": "MissingMap", "source": "Missing Map"}
        )
        self.maps.write_text(json.dumps(maps), encoding="utf-8")
        with self.assertRaises(GENERATOR.CatalogCoverageError):
            self.generate()
        audit = json.loads(self.audit.read_text(encoding="utf-8"))
        self.assertFalse(audit["coverage_complete"])
        self.assertEqual(audit["category_coverage"]["Map"]["missing"], 1)

    def test_rejects_unsafe_map_identity(self):
        maps = json.loads(self.maps.read_text(encoding="utf-8"))
        maps["entries"][0]["identity"] = "Meadow\t2"
        self.maps.write_text(json.dumps(maps), encoding="utf-8")
        with self.assertRaises(GENERATOR.CatalogInputError):
            self.generate()


if __name__ == "__main__":
    unittest.main()
