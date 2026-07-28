import hashlib
import gc
import json
import os
import stat
import struct
from pathlib import Path

import UnityPy


TRANSLATIONS = {
    "archetype.Acolyte.name": "侍祭",
    "archetype.Artificer.name": "巧匠",
    "archetype.Assassin.name": "刺客",
    "archetype.Berserker.name": "狂战士",
    "archetype.Blacksmith.name": "铁匠",
    "archetype.Cardweaver.name": "卡牌编织者",
    "archetype.Chronomancer.name": "时法师",
    "archetype.Craftsman.name": "工艺师",
    "archetype.DragonKnight.name": "龙骑士",
    "archetype.Druid.name": "德鲁伊",
    "archetype.Gemsmith.name": "宝石匠",
    "archetype.Gunslinger.name": "枪手",
    "archetype.Jester.name": "小丑",
    "archetype.Knight.name": "骑士",
    "archetype.Mage.name": "法师",
    "archetype.Merchant.name": "商人",
    "archetype.Monk.name": "武僧",
    "archetype.Necromancer.name": "死灵法师",
    "archetype.Paladin.name": "圣骑士",
    "archetype.Priest.name": "牧师",
    "archetype.Ranger.name": "游侠",
    "archetype.Revenant.name": "亡魂",
    "archetype.Rogue.name": "盗贼",
    "archetype.Scout.name": "斥候",
    "archetype.Shinobi.name": "忍者",
    "archetype.Stylist.name": "造型师",
    "archetype.Summoner.name": "召唤师",
    "archetype.Warlock.name": "术士",
    "archetype.Warrior.name": "战士",
    "archetype.Weaver.name": "织法者",
    "archetype.Wizard.name": "巫师",
    "artifact.Acolyte.name": "神圣誓言",
    "artifact.Atk.name": "战争铭文",
    "artifact.Auto.name": "闪击核心",
    "artifact.Bastion.name": "堡垒",
    "artifact.Berserker_1.name": "血怒",
    "artifact.Cast.name": "法术编织",
    "artifact.Corporeal.name": "实体化",
    "artifact.Cost.name": "永恒施法",
    "artifact.Crit.name": "怒火烙印",
    "artifact.Def.name": "泰坦战甲",
    "artifact.Eternis.name": "埃特尼斯",
    "artifact.Flee.name": "暗影缚契",
    "artifact.Gunslinger_1.name": "神射手",
    "artifact.Healing.name": "生命绽放",
    "artifact.Hexbrand.name": "咒术烙印",
    "artifact.Hit.name": "鹰眼",
    "artifact.Hp.name": "维塔利斯",
    "artifact.Immune.name": "虚无印记",
    "artifact.Knight.name": "荣誉誓约",
    "artifact.Leech.name": "血契",
    "artifact.Mage.name": "奥术本源",
    "artifact.Magic.name": "奥秘",
    "artifact.Matk.name": "星火",
    "artifact.Mdef.name": "帷幕守护",
    "artifact.Melee.name": "钢铁之心",
    "artifact.Movespeed.name": "御风",
    "artifact.Mp.name": "以太核心",
    "artifact.Necromancer_1.name": "墓缚",
    "artifact.Novice.name": "开拓者",
    "artifact.Oathbound.name": "誓约缚身",
    "artifact.Paladin_1.name": "圣盾之光",
    "artifact.Priest_1.name": "圣域恩典",
    "artifact.Primordial.name": "太初",
    "artifact.Ranged.name": "风暴箭袋",
    "artifact.Rogue.name": "无声死神",
    "artifact.Scout.name": "远见",
    "artifact.Shinobi_1.name": "暗影帷幕",
    "artifact.Summoner.name": "灵魂契约",
    "artifact.Vampiric.name": "吸血",
    "artifact.Warrior.name": "钢铁觉醒",
    "artifact.Weaver_1.name": "命运编织",
    "artifact.Wizard_1.name": "风暴引擎",
    "artifact.Wizard_2.name": "冰封领域",
    "artifact.Wizard_3.name": "陨星浩劫",
    "artifact.Wizard_4.name": "奥术元素师",
    "cosmetic.Chirpy Hat.name": "啾啾帽",
    "cosmetic.NoviceChest.name": "新手胸甲",
    "cosmetic.NoviceFeet.name": "新手鞋",
    "cosmetic.NoviceLegs.name": "新手护腿",
    "cosmetic.Potions.name": "药水袋",
    "cosmetic.Sunflower Clip.name": "向日葵发夹",
    "cosmetic.Sword.name": "剑",
    "cosmetic.Wooden Guard.name": "木制护盾",
    "card.Fungi.affix": "治疗",
    "card.Mimic Book.affix": "智力",
    "card.Mosquito Stinger.affix": "急速",
    "gem.SpearThrust Gem.affix": "穿刺连击",
    "gem.SpearThrust Gem.name": "穿刺连击宝石",
    "gem.Heal Gem.affix": "治疗",
    "skill.Cure.name": "治愈",
    "skill.Haste.name": "急速",
    "skill.Heal.name": "治疗",
    "skill.SpearThrust.name": "穿刺连击",
    "status.Haste.name": "急速",
}

TARGETS = [
    (
        Path("SpiritVale_Data/StreamingAssets/aa/StandaloneWindows64/client_assets_gameclientconfig_c0a5d3810020e7165e0bea3448c87548.bundle"),
        5072310189623964512,
        "addressables-game-config.raw",
    ),
    (Path("SpiritVale_Data/sharedassets0.assets"), 83215, "sharedassets-game-config.raw"),
]
BACKUP_DIR = Path(".codex-localization-tools/backups")

GLOSSARY_TRANSLATIONS = json.loads(
    Path(".codex-localization-tools/glossary-translations.json").read_text(encoding="utf-8")
)
TRANSLATIONS.update(
    json.loads(Path(".codex-localization-tools/online-translations.json").read_text(encoding="utf-8"))
)
TRANSLATIONS.update(GLOSSARY_TRANSLATIONS)


def read_string(raw, offset):
    size = struct.unpack_from("<I", raw, offset)[0]
    end = (offset + 4 + size + 3) & ~3
    value = raw[offset + 4 : offset + 4 + size].decode("utf-8")
    return value, end


def string_field(raw, key, index):
    encoded_key = key.encode("utf-8")
    marker = struct.pack("<I", len(encoded_key)) + encoded_key
    entry_start = raw.find(marker)
    if entry_start < 0:
        raise RuntimeError(f"Missing localization key: {key}")
    offset = entry_start
    fields = []
    for _ in range(20):
        start = offset
        value, offset = read_string(raw, offset)
        fields.append((start, offset, value))
    if fields[0][2] != key:
        raise RuntimeError(f"Localization key mismatch: {key}")
    return fields[index]


def encoded_string(value):
    encoded = value.encode("utf-8")
    return struct.pack("<I", len(encoded)) + encoded + b"\0" * ((-len(encoded)) % 4)


BACKUP_DIR.mkdir(parents=True, exist_ok=True)
for path, object_id, backup_name in TARGETS:
    environment = UnityPy.load(str(path))
    obj = next(item for item in environment.objects if item.path_id == object_id)
    original = obj.get_raw_data()
    backup_path = BACKUP_DIR / backup_name
    if not backup_path.exists():
        backup_path.write_bytes(original)

    replacements = []
    for key, translation in TRANSLATIONS.items():
        start, end, current = string_field(original, key, 8)
        if current not in ("", translation):
            raise RuntimeError(f"Refusing to replace existing Chinese text for {key}: {current}")
        if current != translation:
            replacements.append((start, end, encoded_string(translation)))

    rebuilt = bytearray(original)
    for start, end, replacement in sorted(replacements, reverse=True):
        rebuilt[start:end] = replacement

    if not replacements and path.name != "sharedassets0.assets":
        print(f"{path}: already localized")
        continue

    obj.set_raw_data(bytes(rebuilt))
    if path.name == "sharedassets0.assets":
        pickup = next(item for item in environment.objects if item.path_id == 94417)
        pickup_raw = pickup.get_raw_data()
        source = encoded_string("Pickup")
        replacement = encoded_string("拾取")
        if source not in pickup_raw and replacement not in pickup_raw:
            raise RuntimeError("Could not find the Pickup prompt text")
        if source in pickup_raw:
            pickup.set_raw_data(pickup_raw.replace(source, replacement, 1))

    temporary = path.with_suffix(path.suffix + ".tmp")
    saved_file = environment.file.save()
    temporary.write_bytes(saved_file)
    if path.name == "sharedassets0.assets":
        del pickup
    del saved_file, obj, environment
    gc.collect()
    path.chmod(path.stat().st_mode | stat.S_IWRITE)
    os.replace(temporary, path)
    digest = hashlib.sha256(bytes(rebuilt)).hexdigest()
    print(f"{path}: updated {len(replacements)} entries, raw sha256={digest}")
