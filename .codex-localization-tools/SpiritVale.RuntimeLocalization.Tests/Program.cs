using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpiritVale.RuntimeLocalization;

var translations = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["Market Ice Chest"] = "恐狼 Chest",
    ["Potion Copy"] = "携带 Mana；上限 +100 per refine；Mdef +3",
    ["Direwolf"] = "恐狼",
    ["Chest"] = "胸甲",
    ["Mana"] = "法力",
    ["per refine"] = "每次精炼",
    ["Mdef"] = "魔法防御",
    ["Damage"] = "伤害",
    ["Magic Defence"] = "魔法防御",
    ["Death Mage Card"] = "死灵法师卡片",
    ["Mana Potion"] = "法力药水",
    ["Coins"] = "金币",
    ["FPS:"] = "帧率：",
    ["ms"] = "毫秒",
    ["Ping:"] = "延迟：",
    ["Players:"] = "玩家数：",
    ["Kingdom"] = "王国",
    ["Can't loot yet"] = "暂时无法拾取",
    ["Sprout"] = "萌芽",
    ["Earth"] = "大地",
    ["Wind"] = "风",
    ["Knight"] = "骑士",
    ["Taunt"] = "嘲讽",
    ["HP"] = "生命值",
    ["MP"] = "法力值",
    ["MATK"] = "魔法攻击",
    ["lv"] = "等级",
    ["Sunny Meadows"] = "阳光草甸",
    ["Monster Kills"] = "击杀怪物数",
    ["Boss Kills"] = "击杀首领数",
    ["Mystic Lake"] = "秘境湖",
    ["Fire Bunny"] = "火焰兔",
    ["Crown of Spikes"] = "尖刺王冠"
    ,["Slingshot"] = "弹弓"
    ,["Wooden Guard"] = "木制护盾"
    ,["Healthy Pirate Legs"] = "健壮海盗长裤"
    ,["Focus Windstrider Shoes"] = "专注御风者鞋"
    ,["Umbra"] = "翁布拉"
    ,["Fireball"] = "火球"
    ,["Bash"] = "猛击"
    ,["Vital Broad Sword"] = "活力阔剑"
    ,["Vital"] = "活力"
    ,["Broad Sword"] = "阔剑"
    ,["Lv."] = "等级"
    ,["Bee"] = "蜜蜂"
    ,["Bumblebee [Boss]"] = "大黄蜂 [首领]"
    ,["Auryx"] = "奥里克斯"
    ,["Rooster"] = "公鸡"
    ,["Neutral"] = "中立"
    ,["Bonk!"] = "猛敲！"
    ,["Axe Quicken"] = "暴击专注"
    ,["Spear Quicken"] = "精准专注"
    ,["Precision Focus"] = "精准专注"
    ,["Bleed Attack"] = "流血"
    ,["Grave Chill Enemy"] = "墓穴寒意（敌方）"
    ,["Necrotic Presence Enemy"] = "死灵气场（敌方）"
    ,["Sharpen"] = "暴击强化"
    ,["Soul Drain Enemy"] = "灵魂汲取（敌方）"
    ,["Spell Shield"] = "反射魔法"
    ,["Stun Attack"] = "眩晕"
    ,["Summon Abomination"] = "召唤疫病兽"
    ,["Summon Death Mage"] = "召唤死灵法师"
    ,["Summon Skeleton"] = "召唤骸骨战士"
    ,["Summon Skeleton Mage"] = "召唤骸骨法师"
    ,["Summon Wraith"] = "召唤收割者"
    ,["Thorns"] = "反射物理"
    ,["Mage"] = "法师"
    ,["Spellweaver"] = "法术织者"
    ,["Pioneer Artifact Set"] = "先驱神器套装"
    ,["Pirate Set"] = "海盗套装"
    ,["Pirate Set:"] = "海盗套装:"
    ,["Pirate 套装:"] = "海盗套装:"
    ,["装扮Pet"] = "装扮宠物"
    ,["Firewall"] = "火焰之墙"
    ,["POINTS"] = "点数"
    ,["Apply"] = "应用"
    ,["Inventory"] = "背包"
    ,["Cosmetics"] = "装扮"
    ,["Appearance"] = "外观"
    ,["Consumables"] = "消耗品"
    ,["Equipment"] = "装备"
    ,["Cards"] = "卡片"
    ,["Artifacts"] = "神器"
    ,["Gems"] = "宝石"
    ,["Materials"] = "材料"
    ,["Warp"] = "传送"
    ,["Potions"] = "药水袋"
    ,["Potion Pouch"] = "药水袋"
    ,["Server"] = "世界"
    ,["Select Server"] = "选择服务器"
    ,["Gameplay"] = "游戏"
    ,["List items for Sale"] = "上架物品"
    ,["Windy Desert"] = "风蚀沙漠"
    ,["Windy Desert North"] = "风蚀沙漠北部"
    ,["Windy Desert South"] = "风蚀沙漠南部"
    ,["Nevaris"] = "内瓦里斯"
    ,["Nevaris Sewers"] = "内瓦里斯下水道"
    ,["Underground Cavern"] = "地下洞窟"
    ,["Enter a name.."] = "输入角色名……"
    ,["Name already taken"] = "该名称已被占用"
    ,["Body"] = "体型"
    ,["Face"] = "脸型"
    ,["Body Color"] = "肤色"
    ,["Hair Color"] = "发色"
    ,["Hair"] = "发型"
    ,["Brows"] = "眉型"
    ,["Beard"] = "胡须"
    ,["Randomise"] = "随机生成"
    ,["Create character"] = "创建角色"
    ,["Advancements"] = "职业进阶"
    ,["Choose Class"] = "选择职业"
    ,["A balanced melee fighter trained in sword and shield combat. Durable and dependable, ideal for players who enjoy frontline roles and absorbing damage."] = "攻守兼备的近战斗士，精通剑盾作战。坚韧可靠，适合喜欢坚守前线、承受伤害的玩家。"
    ,["Respawn in town"] = "在城镇复活"
    ,["Craftsman Recipes"] = "工匠配方"
    ,["Blacksmith Vendor"] = "铁匠商人"
    ,["Label"] = "标签"
    ,["Craft"] = "制作"
    ,["Interact"] = "互动"
    ,["View"] = "查看"
    ,["Pickup"] = "拾取"
    ,["Waypoint"] = "传送点"
    ,["Stance: Two Handed"] = "姿态：双手持握"
    ,["You are dead"] = "你已死亡"
    ,["[Early Bird]"] = "[早鸟]"
    ,["[SpiritValer]"] = "[灵谷勇士]"
    ,["Sunny Meadows Sunny Meadows"] = "阳光草甸 阳光草甸"
    ,["Sunny Meadows 1"] = "阳光草甸 1"
    ,["Sunny Meadows 2"] = "阳光草甸 2"
    ,["Goblin Cave 1"] = "哥布林洞窟 1"
    ,["Crystal Cave"] = "水晶洞窟"
    ,["Abyss Castle Crypt"] = "深渊城堡墓穴"
    ,["Mystic Lake 2"] = "秘境湖 2"
    ,["Goblin Village"] = "哥布林村落"
    ,["Goblin Field"] = "哥布林原野"
    ,["Forest Field"] = "森林原野"
    ,["Forest Field 1"] = "森林原野 1"
    ,["Free as part of a promotion at Finkle Winkle Ice Cream Shop. Say you're giving out ice cream. How about you go there too?"] = "Finkle Winkle 冰淇淋店促销期间免费赠送。听说他们正在发放冰淇淋，你也去看看吧？"
    ,["Critical Strikes"] = "暴击强化"
    ,["NPC- Sharpen"] = "暴击强化"
    ,["Soldier Termite"] = "白蚁士兵"
    ,["Termite Soldier"] = "白蚁士兵"
    ,["Umbral Fragment"] = "幽影碎片"
    ,["Decay"] = "腐朽"
    ,["Decay Aura"] = "腐朽光环"
    ,["Decay Immunity"] = "腐朽免疫"
    ,["Frenzy"] = "狂乱"
    ,["Berserk"] = "狂暴"
    ,["Defiance"] = "抗御"
    ,["Defiance Aura"] = "抗御光环"
    ,["Unyielding"] = "不屈"
    ,["Tomahawk"] = "投掷斧"
    ,["War Axe"] = "战斧"
    ,["Spook"] = "游魂"
    ,["Spook Card"] = "游魂卡片"
    ,["Apparition"] = "幽魂"
    ,["Angel"] = "天使"
    ,["Angelic"] = "天使风格"
    ,["Angeling"] = "小天使宠物"
    ,["Ready a dedicated heavy weapon and swap to it mid-battle to unleash overwhelming firepower. Unlocks Gatling Guns and Launchers"] = "准备专用重武器，并可在战斗中切换至该武器，以释放压倒性火力。解锁加特林机枪与爆破发射器。"
    ,["A shimmering shield diverts part of incoming harm into mana."] = "闪耀护盾会将部分所受伤害转移至法力。"
    ,["Spread lingering umbral decay beneath enemies, damaging them and marking them with shadow."] = "在敌人脚下散布持续存在的幽影腐化，造成伤害并施加暗影标记。"
    ,["Increases the damage dealt by your auto attacks."] = "提高普通攻击造成的伤害。"
    ,["Increases maximum HP, Vitality, and Healing Received."] = "提高最大生命值、活力与受到的治疗效果。"
    ,["Card"] = "卡片"
    ,["Earthen protection hardens the user and can answer incoming blows with Stun."] = "大地防护会强化使用者；受到攻击时，有概率使攻击者眩晕。"
    ,["Flame shields the user and can answer incoming blows with Burning."] = "火焰保护使用者；受到攻击时，有概率灼烧攻击者。"
    ,["Water shields the user and can answer incoming blows with Frozen."] = "水流保护使用者；受到攻击时，有概率冻结攻击者。"
    ,["Wind shields the user and can answer incoming blows with Chain Lightning."] = "疾风保护使用者；受到攻击时，有概率对攻击者施放连锁闪电。"
    ,["A gem infused with ancient essence. When embedded into an Artifact, it awakens hidden strength within its bearer."] = "一颗注入古老精华的宝石。嵌入神器后，会唤醒持有者体内潜藏的力量。"
    ,["Windproof"] = "风抗"
    ,["Sun Lion Crest"] = "太阳狮冠"
    ,["Savage"] = "凶猛"
    ,["Skull Pendant"] = "颅骨吊坠"
    ,["Purifying"] = "净化"
    ,["Dualblade Sheath"] = "双刃护套"
    ,["Assassin's"] = "刺客之"
    ,["Archer's Beads"] = "弓手珠饰"
    ,["Combo"] = "连击"
    ,["Skullhacker"] = "裂颅者"
    ,["Mana Burn"] = "法力燃烧"
    ,["Royal Dagger"] = "皇家匕首"
    ,["Purging"] = "净化"
};
var reviewedItemAffixes = new[]
{
    "Windproof", "Savage", "Purifying", "Purging", "Assassin's", "Combo", "Mana Burn",
};
var reviewedItemBaseNames = new[]
{
    "Sun Lion Crest", "Skull Pendant", "Dualblade Sheath", "Archer's Beads",
    "Skullhacker", "Royal Dagger", "Crown of Spikes", "Broad Sword", "Vital Broad Sword",
};
var reviewedMarketSearchKeywords = new[]
{
    new KeyValuePair<string, string>("Sun", "太阳"),
    new KeyValuePair<string, string>("Sunflower", "向日葵"),
    new KeyValuePair<string, string>("Gem", "宝石"),
    new KeyValuePair<string, string>("Jewel", "宝石"),
};
var translator = new RuntimeTextTranslator(
    translations,
    reviewedItemAffixes,
    reviewedItemBaseNames);
var marketSearchBridge = new MarketSearchQueryBridge(
    translations,
    reviewedItemAffixes,
    reviewedItemBaseNames,
    reviewedItemBaseNames,
    reviewedMarketSearchKeywords);
var contextSpecificMarketSearchBridge = new MarketSearchQueryBridge(
    new[]
    {
        new KeyValuePair<string, string>("Contextual Affix", "语境词缀"),
    },
    new[]
    {
        new KeyValuePair<string, string>("Royal Dagger", "皇家匕首"),
    });
var failures = 0;
var checks = 0;

if (args.Length == 1 && args[0] == "--benchmark")
{
    RunHotPathBenchmark();
    return 0;
}

Check("exact target cascade", "Market Ice Chest", "恐狼 胸甲");
Check("tooltip target cascade", "Potion Copy", "携带 法力；上限 +100 每次精炼；魔法防御 +3");
Check("unknown composite remains untouched", "Direwolf Chest", "Direwolf Chest", expectedChange: false);
Check("trim preservation", "  Market Ice Chest\n", "  恐狼 胸甲\n");
Check("unknown rich text remains untouched", "<color=Chest>Chest</color> Chest", "<color=Chest>Chest</color> Chest", expectedChange: false);
Check("tag only remains untouched", "<sprite name=\"Mana\">", "<sprite name=\"Mana\">", expectedChange: false);
Check("character count", "Characters: 10 / 10", "角色：10 / 10");
Check("location fragment", "Location: Kingdom", "位置：王国");
Check("numbered location fragment", "Location: Sunny Meadows 2", "位置：阳光草甸 2");
Check("localized numbered location fragment", "位置: Sunny Meadows 2", "位置: 阳光草甸 2");
CheckWithContext("map level range", "Mystic Lake 2\nLv36-40", "Mystic Lake 2", "秘境湖 2\n等级36-40");
CheckWithContext("numbered map level range", "Sunny Meadows 1\nLv1-5", "Sunny Meadows 1", "阳光草甸 1\n等级1-5");
CheckWithContext("numbered map Name node", "Sunny Meadows 1", "Name", "阳光草甸 1");
CheckWithContext("localized newline location", "位置\nForest Field 1", "Location", "位置\n森林原野 1");
// Party browse rows use a localized Location label followed by a newline. Keep
// map names on the system-name path; a similarly named party must remain intact.
var partyLocationCases = new[]
{
    ("Sunny Meadows 2", "阳光草甸 2"),
    ("Goblin Cave 1", "哥布林洞窟 1"),
    ("Crystal Cave", "水晶洞窟"),
    ("Abyss Castle Crypt", "深渊城堡墓穴"),
    ("Underground Cavern", "地下洞窟"),
    ("Nevaris", "内瓦里斯"),
    ("Mystic Lake 2", "秘境湖 2"),
    ("Goblin Village", "哥布林村落"),
    ("Goblin Field", "哥布林原野"),
};
foreach (var (englishMap, chineseMap) in partyLocationCases)
{
    CheckWithContext(
        "party location: " + englishMap,
        "位置\n" + englishMap,
        "Location",
        "位置\n" + chineseMap);
}
CheckWithContext(
    "party location with literal newline escape",
    "位置\\nSunny Meadows 2",
    "Location",
    "位置\\n阳光草甸 2");
CheckWithContext(
    "party name with map-like text stays protected",
    "位置\nSunny Meadows 2",
    "PartyName",
    "位置\nSunny Meadows 2",
    expectedChange: false);
CheckWithContext("map family base: Windy Desert", "Windy Desert", "Name", "风蚀沙漠");
CheckWithContext("map family north: Windy Desert", "Windy Desert North", "Name", "风蚀沙漠北部");
CheckWithContext("map family south: Windy Desert", "Windy Desert South", "Name", "风蚀沙漠南部");
CheckWithContext("map family base: Nevaris", "Nevaris", "Name", "内瓦里斯");
CheckWithContext("map family region: Nevaris Sewers", "Nevaris Sewers", "Name", "内瓦里斯下水道");
CheckWithContext("map singular: Underground Cavern", "Underground Cavern", "Name", "地下洞窟");
CheckWithContext(
    "dictionary-shaped map display name stays protected",
    "Sunny Meadows 1",
    "Display Name",
    "Sunny Meadows 1",
    expectedChange: false);
CheckWithContext("compact monster level", "Lv10 Fire Bunny", "Description", "等级10 火焰兔");
CheckWithContext(
    "rich compact monster level",
    "<color=#FFD700>Lv6 Bee</color>",
    "Description",
    "等级6 蜜蜂");
CheckWithContext(
    "sprite-prefixed map monster level",
    "<sprite name=\"Water\"> Lv6 Bee",
    "Description",
    "等级6 蜜蜂");
CheckWithContext("standalone map level range", "Lv6-10", "Description", "等级6-10");
CheckWithContext("upgraded item name", "+6 Crown of Spikes", "Name", "+6 尖刺王冠");
CheckWithContext("upgraded affixed pirate legs", "+1 Healthy Pirate Legs", "Name", "+1 健壮海盗长裤");
CheckWithContext(
    "plain generic Name composite remains protected",
    "+6 Combo Royal Dagger",
    "Name",
    "+6 Combo Royal Dagger",
    expectedChange: false);
CheckWithContext(
    "market item composite without rich text",
    "+6 Windproof Sun Lion Crest",
    "ItemName:MarketListing",
    "+6 风抗 太阳狮冠");
CheckWithContext(
    "market item rich windproof crest",
    "+6 <color=#FFD700>Windproof</color> <color=#FFFFFF>Sun Lion Crest</color>",
    "Name",
    "+6 <color=#FFD700>风抗</color> <color=#FFFFFF>太阳狮冠</color>");
CheckWithContext(
    "market item rich savage pendant",
    "+6 <color=#FFD700>Savage</color> <color=#FFFFFF>Skull Pendant</color>",
    "Name",
    "+6 <color=#FFD700>凶猛</color> <color=#FFFFFF>颅骨吊坠</color>");
CheckWithContext(
    "market item rich purifying sheath",
    "+6 <color=#FFD700>Purifying</color> <color=#FFFFFF>Dualblade Sheath</color>",
    "Name",
    "+6 <color=#FFD700>净化</color> <color=#FFFFFF>双刃护套</color>");
CheckWithContext(
    "market item rich assassin beads without upgrade",
    "<color=#FFD700>Assassin's</color> <color=#FFFFFF>Archer's Beads</color>",
    "Name",
    "<color=#FFD700>刺客之</color> <color=#FFFFFF>弓手珠饰</color>");
CheckWithContext(
    "market item multiple reviewed affixes",
    "+6 <color=#FFD700>Assassin's Combo</color> <color=#FFFFFF>Skullhacker</color>",
    "Name",
    "+6 <color=#FFD700>刺客之 连击</color> <color=#FFFFFF>裂颅者</color>");
CheckWithContext(
    "market item multiword affix and preserved newlines",
    "+12 <color=#FFD700>Mana Burn</color>\n<color=#FFFFFF>Royal\nDagger</color>",
    "Name",
    "+12 <color=#FFD700>法力燃烧</color>\n<color=#FFFFFF>皇家匕首\n</color>");
CheckWithContext(
    "unknown item affix remains untouched",
    "+6 <color=#FFD700>Unknown</color> <color=#FFFFFF>Skull Pendant</color>",
    "Name",
    "+6 <color=#FFD700>Unknown</color> <color=#FFFFFF>Skull Pendant</color>",
    expectedChange: false);
CheckWithContext(
    "unknown item base remains untouched",
    "+6 <color=#FFD700>Savage</color> <color=#FFFFFF>Unknown Pendant</color>",
    "Name",
    "+6 <color=#FFD700>Savage</color> <color=#FFFFFF>Unknown Pendant</color>",
    expectedChange: false);
foreach (var protectedContext in new[]
{
    "Display Name", "Text_Name", "PlayerName", "Shop Name", "Vending", "SellerName",
    "GuildName", "PartyName", "TeamName", "ChatText",
})
{
    CheckWithContext(
        $"composite item shape remains protected in {protectedContext}",
        "+6 <color=#FFD700>Savage</color> <color=#FFFFFF>Skull Pendant</color>",
        protectedContext,
        "+6 <color=#FFD700>Savage</color> <color=#FFFFFF>Skull Pendant</color>",
        expectedChange: false);
}
CheckWithContext("critical chance buff name", "Critical Strikes", "CastName", "暴击强化");
CheckWithContext("runtime critical chance buff alias", "NPC- Sharpen", "Name", "暴击强化");
CheckWithContext("termite lure species", "Soldier Termite", "Name", "白蚁士兵");
CheckWithContext("umbral material family", "Umbral Fragment", "Name", "幽影碎片");
CheckWithContext("decay status family", "Decay Immunity", "Name", "腐朽免疫");
CheckWithContext("frenzy differs from berserk", "Frenzy", "Name", "狂乱");
CheckWithContext("defiance differs from unyielding", "Defiance Aura", "Name", "抗御光环");
CheckWithContext("upgraded distinct tomahawk", "+3 Tomahawk", "Name", "+3 投掷斧");
CheckWithContext("distinct monster card alias", "Spook Card", "Name", "游魂卡片");
CheckWithContext("distinct angel cosmetic", "Angelic", "Name", "天使风格");
CheckWithContext(
    "heavy weapon swap preserves complete unlock mechanic",
    "Ready a dedicated heavy weapon and swap to it mid-battle to unleash overwhelming firepower. Unlocks Gatling Guns and Launchers",
    "Text_Description",
    "准备专用重武器，并可在战斗中切换至该武器，以释放压倒性火力。解锁加特林机枪与爆破发射器。");
CheckWithContext(
    "mana damage diversion preserves target relationship",
    "A shimmering shield diverts part of incoming harm into mana.",
    "Text_Description",
    "闪耀护盾会将部分所受伤害转移至法力。");
CheckWithContext(
    "umbral and shadow terminology remain distinct",
    "Spread lingering umbral decay beneath enemies, damaging them and marking them with shadow.",
    "Text_Description",
    "在敌人脚下散布持续存在的幽影腐化，造成伤害并施加暗影标记。");
CheckWithContext(
    "auto attack terminology",
    "Increases the damage dealt by your auto attacks.",
    "Text_Description",
    "提高普通攻击造成的伤害。");
CheckWithContext(
    "vitality and healing received terminology",
    "Increases maximum HP, Vitality, and Healing Received.",
    "Text_Description",
    "提高最大生命值、活力与受到的治疗效果。");
CheckWithContext("card item terminology", "Card", "Type", "卡片");
foreach (var counterReaction in new[]
{
    (
        "Earthen protection hardens the user and can answer incoming blows with Stun.",
        "大地防护会强化使用者；受到攻击时，有概率使攻击者眩晕。"),
    (
        "Flame shields the user and can answer incoming blows with Burning.",
        "火焰保护使用者；受到攻击时，有概率灼烧攻击者。"),
    (
        "Water shields the user and can answer incoming blows with Frozen.",
        "水流保护使用者；受到攻击时，有概率冻结攻击者。"),
    (
        "Wind shields the user and can answer incoming blows with Chain Lightning.",
        "疾风保护使用者；受到攻击时，有概率对攻击者施放连锁闪电。")
})
{
    CheckWithContext(
        "incoming-hit counter targets the attacker: " + counterReaction.Item1,
        counterReaction.Item1,
        "Text_Description",
        counterReaction.Item2);
}
CheckWithContext("pirate set label", "Pirate Set:", "Description", "海盗套装:");
CheckWithContext("partially localized pirate set label", "Pirate 套装:", "Description", "海盗套装:");
CheckWithContext(
    "embedded pirate set label",
    "完整套装:\n所有属性: +3\nPirate Set:\n猛击伤害: +5%",
    "Description",
    "完整套装:\n所有属性: +3\n海盗套装:\n猛击伤害: +5%");
CheckWithContext(
    "partially localized pirate set label preserves zero-width suffix",
    "Pirate 套装:\u200B",
    "Description",
    "海盗套装:\u200B");
CheckWithContext("upgraded affixed windstrider shoes", "+5 Focus Windstrider Shoes", "Name", "+5 专注御风者鞋");
CheckWithContext(
    "mixed localized Umbra set description",
    "当温柔的双手在世界的残酷面前失败时，Umbra教导力量\n\n件:\n猛击伤害: +5% + 2% 每级",
    "Description",
    "当温柔的双手在世界的残酷面前失败时，翁布拉教导力量\n\n件:\n猛击伤害: +5% + 2% 每级");
CheckWithContext("mixed cosmetic pet type", "装扮Pet", "Type", "装扮宠物");
CheckWithContext("inventory points label", "POINTS", "Label-Title", "点数");
CheckWithContext("inventory apply label", "Apply", "Label-Title", "应用");
CheckWithContext("inventory title", "Inventory", "Label-Title", "背包");
CheckWithContext("cosmetics tab", "Cosmetics", "Label-Title", "装扮");
CheckWithContext("appearance tab", "Appearance", "Label-Title", "外观");
CheckWithContext("consumables tab", "Consumables", "Label-Title", "消耗品");
CheckWithContext("equipment tab", "Equipment", "Label-Title", "装备");
CheckWithContext("cards tab", "Cards", "Label-Title", "卡片");
CheckWithContext("artifacts tab", "Artifacts", "Label-Title", "神器");
CheckWithContext("gems tab", "Gems", "Label-Title", "宝石");
CheckWithContext("materials tab", "Materials", "Label-Title", "材料");
CheckWithContext("warp label", "Warp", "Label-Title", "传送");
CheckWithContext("equipment runtime alias: Potions", "Potions", "Name", "药水袋");
CheckWithContext("equipment canonical name: Potion Pouch", "Potion Pouch", "Name", "药水袋");
CheckWithContext("chat channel: Server", "Server", "Label-Channel", "世界");
CheckWithContext("server selection action", "Select Server", "Label-Title", "选择服务器");
CheckWithContext("settings gameplay tab", "Gameplay", "Label-Title", "游戏");
CheckWithContext("market sale listing title", "List items for Sale", "Label-Title", "上架物品");
CheckWithContext("character name placeholder", "Enter a name..", "Placeholder", "输入角色名……");
CheckWithContext("character name conflict", "Name already taken", "Error", "该名称已被占用");
CheckWithContext("character body label", "Body", "Label-Title", "体型");
CheckWithContext("character face label", "Face", "Label-Title", "脸型");
CheckWithContext("character body color label", "Body Color", "Label-Title", "肤色");
CheckWithContext("character hair color label", "Hair Color", "Label-Title", "发色");
CheckWithContext("character hair label", "Hair", "Label-Title", "发型");
CheckWithContext("character brows label", "Brows", "Label-Title", "眉型");
CheckWithContext("character beard label", "Beard", "Label-Title", "胡须");
CheckWithContext("character randomise label", "Randomise", "Label-Title", "随机生成");
CheckWithContext("character create label casing", "Create character", "Label-Title", "创建角色");
CheckWithContext("character advancements title", "Advancements", "Title_LineDivider_02", "职业进阶");
CheckWithContext("character choose class label", "Choose Class", "Label-Title", "选择职业");
CheckWithContext(
    "character class description",
    "A balanced melee fighter trained in sword and shield combat. Durable and dependable, ideal for players who enjoy frontline roles and absorbing damage.",
    "Description",
    "攻守兼备的近战斗士，精通剑盾作战。坚韧可靠，适合喜欢坚守前线、承受伤害的玩家。");
CheckWithContext("respawn button", "Respawn in town", "Button_Ok", "在城镇复活");
CheckWithContext("craftsman recipes title", "Craftsman Recipes", "Craftsman", "工匠配方");
CheckWithContext("blacksmith vendor bubble", "Blacksmith Vendor", "Label_Bubble", "铁匠商人");
CheckWithContext("generic UI label", "Label", "Label-Medium", "标签");
CheckWithContext("craft label", "Craft", "Label-Title", "制作");
CheckWithContext("interact label", "Interact", "Name", "互动");
CheckWithContext("waypoint label", "Waypoint", "Name", "传送点");
CheckWithContext("two-handed stance", "Stance: Two Handed", "Stance", "姿态：双手持握");
CheckWithContext("death toast", "You are dead", "ToastPopup", "你已死亡");
CheckWithContext("early bird title", "[Early Bird]", "Title", "[早鸟]");
CheckWithContext("SpiritValer title", "[SpiritValer]", "Title", "[灵谷勇士]");
CheckWithContext(
    "built-in title stays untouched as a display name",
    "[Early Bird]",
    "Display Name",
    "[Early Bird]",
    expectedChange: false);
CheckWithContext(
    "Finkle Winkle promotion",
    "Free as part of a promotion at Finkle Winkle Ice Cream Shop. Say you're giving out ice cream. How about you go there too?",
    "Popup",
    "Finkle Winkle 冰淇淋店促销期间免费赠送。听说他们正在发放冰淇淋，你也去看看吧？");
CheckWithContext(
    "compact seconds in trusted description",
    "自动拾取间隔: 1s",
    "Description",
    "自动拾取间隔: 1秒");
CheckWithContext(
    "compact seconds remain untouched in chat",
    "自动拾取间隔: 1s",
    "ChatText",
    "自动拾取间隔: 1s",
    expectedChange: false);
Check("playtime", "Playtime: 999h 59m", "游戏时长：999小时 59分");
Check("compact playtime", "3h 22m", "3小时 22分");
Check("localized playtime", "游玩时间: 3h 22m", "游戏时长：3小时 22分");
CheckWithContext("reset timer", "Reset in 57h 18m", "ResetTimer", "57小时 18分后重置");
CheckWithContext(
    "reset timer remains untouched in chat",
    "Reset in 57h 18m",
    "ChatText",
    "Reset in 57h 18m",
    expectedChange: false);
CheckWithContext("level value", "Level 99", "Level", "等级 99");
CheckWithContext("single weight value", "Weight: 10", "Weight", "重量：10");
CheckWithContext("capacity weight value", "Weight: 1000/1000", "Weight", "重量：1000/1000");
CheckWithContext("spaced capacity weight value", "Weight: 1,000 / 1,200", "Weight", "重量：1,000/1,200");
CheckWithContext("item count", "Items: 20/20", "Label-Amount", "物品：20 / 20");
CheckWithContext(
    "vending duration notice",
    "Vending lasts 24 hours.\nAll transactions have 10% Tax.",
    "Label-Amount",
    "摆摊持续 24 小时。\n所有交易收取 10% 税费。");
CheckWithContext(
    "market listing terms",
    "Listings expire in 48h.\nListing fee: 1%. Sales tax: 10%.",
    "Label-Amount",
    "商品将在 48 小时后下架。\n上架费：1%。销售税：10%。");
CheckWithContext(
    "partially localized dismantle confirmation",
    "Are you sure you want to 拆解\nBroad Sword?",
    "Popup",
    "确定要拆解\n阔剑吗？");
CheckWithContext(
    "English dismantle confirmation",
    "Are you sure you want to Dismantle\nBroad Sword?",
    "Popup",
    "确定要拆解\n阔剑吗？");
Check("deaths", "Deaths: 1", "死亡次数：1");
Check("game countdown", "Game starts in: 00:03", "游戏将在 00:03 后开始");
Check("round countdown", "Round Starts In: 10s", "回合将在 10秒 后开始");
Check(
    "party summary",
    "Members: 7 / 8\nExp and Drop Rate: +160% \nLevel Range: 15",
    "成员：7 / 8\n经验与掉落率：+160%\n等级范围：15");
Check(
    "party summary CRLF",
    "Members: 1 / 8\r\nExp and Drop Rate: +0%\r\nLevel Range: 1-15",
    "成员：1 / 8\r\n经验与掉落率：+0%\r\n等级范围：1-15");
Check(
    "party summary literal newline escapes",
    "Members: 7 / 8\\nExp and Drop Rate: +160% \\nLevel Range: 15",
    "成员：7 / 8\\n经验与掉落率：+160%\\n等级范围：15");
Check(
    "network summary",
    "Ping: 100 | FPS: 60 | Players: 100",
    "延迟：100 | 帧率：60 | 在线：100");
Check(
    "performance summary",
    "FPS: 120 (8.3ms)  Ping: 240  Players: 38",
    "帧率：120（8.3毫秒）  延迟：240  在线：38");
Check(
    "rich performance summary fallback",
    "FPS: <color=#66CC66>120</color> (8.3ms)  Ping: <color=#66CC66>220</color>  Players: 40",
    "帧率：<color=#66CC66>120</color>（8.3毫秒）  延迟：<color=#66CC66>220</color>  在线：40");
Check(
    "sale event",
    "Sold Death Mage Card for 20 Coins",
    "已售出 死灵法师卡片，获得 20 金币");
Check(
    "timed sale with rich text",
    "[5 hours ago] Sold Death Mage Card to Deadly Snake for <sprite name=\"coin\"> 45",
    "[5小时前] 已将 死灵法师卡片 售予 Deadly Snake，售价 <sprite name=\"coin\"> 45");
Check(
    "singular timed sale with coin suffix",
    "[1 hour ago] Sold Mana Potion to Solyere for 45 Coins",
    "[1小时前] 已将 法力药水 售予 Solyere，售价 45 金币");
Check(
    "timed sale preserves dictionary-shaped buyer",
    "[5 hours ago] Sold Mana Potion to Mana Chest for 45 Coins",
    "[5小时前] 已将 法力药水 售予 Mana Chest，售价 45 金币");
CheckWithContext(
    "just-now sale history with rich coin",
    "[just now] Sold Wooden Guard to 王鐵柱 for <sprite name=\"coin\"> 162",
    "ItemName:UIinventoryItem",
    "[刚刚] 已将 木制护盾 售予 王鐵柱，售价 <sprite name=\"coin\"> 162");
CheckWithContext(
    "just-now sale history runtime shape",
    "[just now] Sold Slingshot to 王鐵柱 for  162",
    "ItemName:UIinventoryItem",
    "[刚刚] 已将 弹弓 售予 王鐵柱，售价 162");
CheckWithContext(
    "just-now sale preserves dictionary-shaped buyer",
    "[Just now] Sold Slingshot to Mana Chest for 162 Coins",
    "ItemName:UIinventoryItem",
    "[刚刚] 已将 弹弓 售予 Mana Chest，售价 162 金币");
CheckWithContext(
    "just-now sale remains untouched in chat",
    "[just now] Sold Slingshot to 王鐵柱 for 162 Coins",
    "ChatText",
    "[just now] Sold Slingshot to 王鐵柱 for 162 Coins",
    expectedChange: false);
Check(
    "party invite preserves player name",
    "Soda Candy invited you to join the party",
    "Soda Candy 邀请你加入队伍");
Check(
    "party invite alternate wording preserves player name",
    "APzZEn2 invited you to their party",
    "APzZEn2 邀请你加入队伍");
Check(
    "party invite preserves dictionary-shaped player name",
    "Mana Chest invited you to join the party",
    "Mana Chest 邀请你加入队伍");
Check(
    "party invite alternate wording preserves dictionary-shaped player name",
    "Mana Chest invited you to their party",
    "Mana Chest 邀请你加入队伍");
Check("player name untouched", "Deadly Snake", "Deadly Snake", expectedChange: false);
Check("word boundary protection", "Chesterton", "Chesterton", expectedChange: false);
Check("prefixed player text remains untouched", "Bob: Sold Death Mage Card for 20 Coins", "Bob: Sold Death Mage Card for 20 Coins", expectedChange: false);
Check("chat prepositions remain untouched", "Where to buy potions for 1m gold", "Where to buy potions for 1m gold", expectedChange: false);
Check("loot cooldown", "Can't loot yet", "暂时无法拾取");
Check("monster nameplate", "Sprout  Earth Lv.9", "萌芽  大地 等级9");
Check("multiline monster nameplate", "Sprout\n Earth Lv.9", "萌芽\n 大地 等级9");
Check("giant monster nameplate", "Giant Bee  Wind Lv.6", "巨型蜜蜂  风 等级6");
Check("giant monster multiline nameplate", "Giant Sprout\n Earth Lv.9", "巨型萌芽\n 大地 等级9");
Check("giant boss monster nameplate", "Giant Bumblebee [Boss]  Wind Lv.7", "巨型大黄蜂 [首领]  风 等级7");
Check("player nameplate preserves name", "Mana Chest Lv.16 Knight", "Mana Chest 等级16 骑士");
CheckWithContext("giant player nameplate preserves name", "Giant Alice Lv.6 Knight", "Text_Name", "Giant Alice 等级6 骑士");
Check("short level class", "Lv.16 Knight", "等级16 骑士");
Check("short level", "Lv.16", "等级16");
CheckWithContext(
    "sprite-prefixed short level",
    "<sprite name=\"SkillPoint\"> Lv.1",
    "Level",
    "<sprite name=\"SkillPoint\"> 等级1");
CheckWithContext(
    "rich short level",
    "<color=#7fff00>Lv.2</color>",
    "Level",
    "<color=#7fff00>等级2</color>");
Check("skill level and cost", "Taunt Lv.1 [10 MP]\n右键点击以分配或更改。", "嘲讽 等级1 [10 法力值]\n右键点击以分配或更改。");
CheckWithContext("exact artifact set", "Pioneer Artifact Set", "Type", "先驱神器套装");
CheckWithContext("dynamic artifact set", "Spellweaver Artifact Set", "Type", "法术织者神器套装");
Check("monster kill count", "Monster Kills: 394", "击杀怪物数：394");
Check("HP resource bar", "HP 1352 / 1352", "生命值 1352 / 1352");
Check("MP resource bar", "MP 247 / 247", "法力值 247 / 247");
Check("channel population", "Channel 7 (79)", "频道 7 (79)");
Check("server ping value", "178ms", "178毫秒");
Check("rich monster nameplate", "<color=#fff>Sprout</color>\n <sprite name=\"earth\">Earth Lv.9", "<color=#fff>萌芽</color>\n <sprite name=\"earth\">大地 等级9");
CheckWithContext("rich giant monster nameplate", "<color=#fff>Giant Bee</color>\n <sprite name=\"wind\">Wind Lv.6", "Name", "<color=#fff>巨型蜜蜂</color>\n <sprite name=\"wind\">风 等级6");
Check("bee nameplate", "Bee  Wind Lv.6", "蜜蜂  风 等级6");
Check("rooster nameplate", "Rooster\n Neutral Lv.10", "公鸡\n 中立 等级10");
CheckCondition(
    "typed monster producer localizes the rooster composite",
    translator.TryTranslateMonsterNameplate(
        "Rooster\n Neutral Lv.10",
        out var typedRoosterNameplate) &&
    typedRoosterNameplate == "公鸡\n 中立 等级10");
CheckCondition(
    "typed monster producer preserves a player nameplate",
    !translator.TryTranslateMonsterNameplate(
        "Mana Chest Lv.16 Knight",
        out var typedPlayerNameplate) &&
    typedPlayerNameplate == "Mana Chest Lv.16 Knight");
var incompleteMonsterTranslator = new RuntimeTextTranslator(
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Rooster"] = "公鸡",
    });
CheckCondition(
    "typed monster producer fails closed when affinity is untranslated",
    !incompleteMonsterTranslator.TryTranslateMonsterNameplate(
        "Rooster  Neutral Lv.10",
        out var incompleteMonsterNameplate) &&
    incompleteMonsterNameplate == "Rooster  Neutral Lv.10");
CheckWithContext("cast announcement", "Bash!", "CastName", "猛击！");
CheckWithContext("exact Fireball cast announcement", "Fireball!", "CastName", "火球！");
CheckWithContext("spaced cast announcement", "Fireball !", "CastName", "火球！");
CheckWithContext("full-width cast announcement", "Fireball！", "CastName", "火球！");
CheckWithContext("rich cast announcement", "<color=#fff>Fireball</color> !", "CastName", "<color=#fff>火球</color>！");
CheckWithContext("rich enclosed cast punctuation", "<color=#fff>Fireball!</color>", "CastName", "<color=#fff>火球！</color>");
CheckWithContext("rich punctuation node", "<color=#fff>Fireball</color><color=#fc0>!</color>", "CastName", "<color=#fff>火球</color><color=#fc0>！</color>");
CheckWithContext("split rich cast name", "<color=#fff>Fire</color><color=#fc0>ball</color>!", "CastName", "火球！");
CheckWithContext("repeated cast punctuation", "Fireball!!", "CastName", "火球！！");
CheckWithContext("spaced repeated cast punctuation", "Fireball! !", "CastName", "火球！！");
CheckWithContext("trailing cast whitespace", "Fireball! ", "CastName", "火球！");
CheckWithContext("skill name with punctuation", "Bonk!!", "CastName", "猛敲！！");
CheckWithContext("spaced skill name with punctuation", "Bonk! !", "CastName", "猛敲！！");
CheckWithContext("standalone cast punctuation", "!", "CastName", "！");
CheckWithContext("spaced standalone cast punctuation", " !", "CastName", "！");
CheckWithContext("repeated standalone cast punctuation", "!!", "CastName", "！！");
CheckWithContext("split standalone cast punctuation", "! !", "CastName", "！！");
CheckWithContext("rich standalone cast punctuation", "<color=#fff>!</color>", "CastName", "<color=#fff>！</color>");
CheckWithContext("non-cast exclamation remains untouched", "Fireball!", "TooltipText", "Fireball!", expectedChange: false);
CheckWithContext("numbered location title", "Sunny Meadows 2", "Text", "阳光草甸 2");
CheckWithContext("generic Name UI remains localizable", "Knight", "Name", "骑士");
CheckWithContext("item Name UI remains localizable", "Vital Broad Sword", "Name", "活力阔剑");
CheckWithContext(
    "live character selector class label",
    "Mage",
    "ClassName:CharacterSelector",
    "法师");
CheckWithContext(
    "live character details class label",
    "Knight",
    "ClassName:GUICharacterDetails",
    "骑士");
CheckWithContext(
    "class-shaped display name stays protected",
    "Mage",
    "Display Name",
    "Mage",
    expectedChange: false);
CheckWithContext(
    "class-shaped Text_Name stays protected",
    "Knight",
    "Text_Name",
    "Knight",
    expectedChange: false);
CheckWithContext("dictionary-shaped display name", "Auryx", "Display Name", "Auryx", expectedChange: false);
CheckWithContext("compact leaderboard display name", "Fireball", "DisplayName", "Fireball", expectedChange: false);
CheckWithContext("underscored leaderboard display name", "Gold", "Display_Name", "Gold", expectedChange: false);
CheckWithContext(
    "producer-qualified leaderboard display name",
    "Mage",
    "LeaderboardEntry.DisplayName",
    "Mage",
    expectedChange: false);
CheckWithContext("guild browse user input", "Fireball", "UserInput:Search", "Fireball", expectedChange: false);
CheckCondition(
    "protected player and search text is never captured as untranslated",
    RuntimeTextTranslator.ShouldSuppressUntranslatedCapture("TMP-setter:DisplayName", "Fireball") &&
    RuntimeTextTranslator.ShouldSuppressUntranslatedCapture("TMP-setter:UserInput:Search", "Gold") &&
    RuntimeTextTranslator.ShouldSuppressUntranslatedCapture("TMP:Guild", "Valhalla") &&
    RuntimeTextTranslator.ShouldSuppressUntranslatedCapture("TMP-setter:ChatText", "Fireball") &&
    !RuntimeTextTranslator.ShouldSuppressUntranslatedCapture("Text_Description", "Fireball"));
CheckWithContext("dictionary-shaped Text_Name stays protected", "Fireball", "Text_Name", "Fireball", expectedChange: false);
CheckWithContext("chat cast remains untouched", "Fireball!", "ChatText", "Fireball!", expectedChange: false);
CheckWithContext("chat skill remains untouched", "Fireball", "ChatText", "Fireball", expectedChange: false);
CheckWithContext("chat punctuated skill remains untouched", "Bonk!!", "ChatText", "Bonk!!", expectedChange: false);
CheckWithContext("chat standalone punctuation remains untouched", "!", "ChatText", "!", expectedChange: false);
CheckWithContext("one-digit timestamp chat remains untouched", "9:05 Fireball", "Text", "9:05 Fireball", expectedChange: false);
CheckWithContext("two-digit timestamp chat remains untouched", "19:05 Fireball", "Text", "19:05 Fireball", expectedChange: false);
CheckWithContext("null TMP text", null, "CastName", string.Empty, expectedChange: false);
var inheritedCastContext = TmpTextContextResolver.Resolve("Text (TMP)", new[] { "TMP Text", "CastName" });
CheckWithContext("inherited TMP context translates cast", "Fireball!", inheritedCastContext, "火球！");
var unresolvedGenericContext = TmpTextContextResolver.Resolve("Text", new[] { "TMP Text", "TextMeshProUGUI" });
CheckWithContext(
    "generic TMP without semantic ancestor stays conservative",
    "Fireball!",
    unresolvedGenericContext,
    "Fireball!",
    expectedChange: false);
CheckCondition(
    "generic TMP context inherits CastName",
    TmpTextContextResolver.Resolve("Text (TMP)", new[] { "TMP Text", "CastName" }) == "CastName" &&
    TmpTextContextResolver.Resolve(string.Empty, new[] { "CastName" }) == "CastName");
CheckCondition(
    "TMP context preserves user-text guards",
    TmpTextContextResolver.Resolve("Text", new[] { "ChatText", "CastName" }) == "ChatText" &&
    TmpTextContextResolver.Resolve("Name", new[] { "CastName" }) == "Name");
var worldInteractionActionContext = TmpTextContextResolver.Resolve(
    "Name",
    new[] { "BindToMainPlayer", "WorldCanvas" });
CheckCondition(
    "world interaction action resolves before broad player guard",
    worldInteractionActionContext == "WorldInteractionAction:BindToMainPlayer" &&
    TmpTextContextResolver.Resolve("Text", new[] { "BindToMainPlayer(Clone)" }) ==
        "WorldInteractionAction:BindToMainPlayer(Clone)");
CheckWithContext(
    "world interaction view action",
    "View",
    worldInteractionActionContext,
    "查看");
CheckWithContext(
    "world interaction pickup action",
    "Pickup",
    worldInteractionActionContext,
    "拾取");
CheckWithContext(
    "world interaction key and icon remain intact",
    "[F] ▶ <color=#fff>View</color>",
    worldInteractionActionContext,
    "[F] ▶ <color=#fff>查看</color>");
CheckWithContext(
    "world interaction unknown text remains protected",
    "Mana Chest",
    worldInteractionActionContext,
    "Mana Chest",
    expectedChange: false);
CheckWithContext(
    "player name matching interaction action remains protected elsewhere",
    "View",
    "PlayerName:Nameplate",
    "View",
    expectedChange: false);
CheckWithContext(
    "chat interaction prompt remains protected",
    "[V] ▶ Pickup",
    "ChatText",
    "[V] ▶ Pickup",
    expectedChange: false);
CheckCondition(
    "ambiguous Name resolves nearest reviewed item ancestor",
    TmpTextContextResolver.Resolve("Name", new[] { "MarketItemListing", "PlayerPanel" }) ==
        "ItemName:MarketItemListing" &&
    TmpTextContextResolver.Resolve("Name", new[] { "InventoryItemSlot", "CharacterPanel" }) ==
        "ItemName:InventoryItemSlot");
var liveVendingSearchItemContext = TmpTextContextResolver.Resolve(
    "Name",
    new[] { "UIVendingSearchItem", "VendingPanel" });
var liveVendingSearchItemCloneContext = TmpTextContextResolver.Resolve(
    "Name",
    new[] { "UIVendingSearchItem(Clone)", "VendingPanel" });
var liveVendingSellItemContext = TmpTextContextResolver.Resolve(
    "Name",
    new[] { "UIVendingItem_Sell", "VendingPanel" });
CheckCondition(
    "ambiguous Name resolves live vending item ancestors",
    liveVendingSearchItemContext == "ItemName:UIVendingSearchItem" &&
    liveVendingSearchItemCloneContext == "ItemName:UIVendingSearchItem(Clone)" &&
    liveVendingSellItemContext == "ItemName:UIVendingItem_Sell");
CheckWithContext(
    "live vending item context translates reviewed composite",
    "+6 Windproof Sun Lion Crest",
    liveVendingSearchItemContext,
    "+6 风抗 太阳狮冠");
CheckWithContext(
    "item-shaped chat text stays protected",
    "+6 Windproof Sun Lion Crest",
    "ChatText",
    "+6 Windproof Sun Lion Crest",
    expectedChange: false);
CheckWithContext(
    "item-shaped vending shop name stays protected",
    "+6 Windproof Sun Lion Crest",
    "Vending",
    "+6 Windproof Sun Lion Crest",
    expectedChange: false);
CheckCondition(
    "ambiguous Name resolves live class label ancestors",
    TmpTextContextResolver.Resolve("Name", new[] { "CharacterSelector" }) ==
        "ClassName:CharacterSelector" &&
    TmpTextContextResolver.Resolve("Name", new[] { "CharacterSelector(Clone)" }) ==
        "ClassName:CharacterSelector(Clone)" &&
    TmpTextContextResolver.Resolve("Name", new[] { "GUICharacterDetails" }) ==
        "ClassName:GUICharacterDetails" &&
    TmpTextContextResolver.Resolve("Name", new[] { "Character" }) ==
        "ClassName:Character");
CheckCondition(
    "similar container names do not enter system translation roles",
    !TmpTextContextResolver.Resolve("Name", new[] { "CharacterSelectorPreview" })
        .StartsWith("ClassName:", StringComparison.Ordinal) &&
    !TmpTextContextResolver.Resolve("Name", new[] { "MarketListingHelp" })
        .StartsWith("ItemName:", StringComparison.Ordinal) &&
    !TmpTextContextResolver.Resolve("Name", new[] { "Itemizer" })
        .StartsWith("ItemName:", StringComparison.Ordinal));
CheckCondition(
    "ambiguous Name resolves nearest player-text ancestor",
    TmpTextContextResolver.Resolve("Name", new[] { "SellerName", "MarketItemListing" }) ==
        "PlayerName:SellerName" &&
    TmpTextContextResolver.Resolve("Name", new[] { "GuildMember", "InventoryItemSlot" }) ==
        "PlayerName:GuildMember" &&
    TmpTextContextResolver.Resolve("Name", new[] { "VendingShop" }) ==
        "PlayerName:VendingShop");
CheckCondition(
    "strong player field wins over item keywords in the same ancestor",
    TmpTextContextResolver.Resolve("Name", new[] { "SellerNameInventoryItem" }) ==
        "PlayerName:SellerNameInventoryItem" &&
    TmpTextContextResolver.Resolve("Name", new[] { "GuildMemberEquipment" }) ==
        "PlayerName:GuildMemberEquipment");
var mixedDescription = "发射一颗命中后爆炸的火球，灼烧敌人并留下 Firewall。";
CheckWithContext(
    "trusted mixed description",
    mixedDescription,
    "Text_Description",
    "发射一颗命中后爆炸的火球，灼烧敌人并留下火焰之墙。");
CheckWithContext(
    "chat mixed description remains untouched",
    mixedDescription,
    "ChatText",
    mixedDescription,
    expectedChange: false);
CheckWithContext(
    "runtime skill numeric units",
    "冷却：<color=#7fff00>6 - 1 seconds per level</color>\n消耗：<color=#7fff00>10 mana</color>",
    "Text_Description",
    "冷却：<color=#7fff00>6 - 1 秒/级</color>\n消耗：<color=#7fff00>10 法力值</color>");
var statScalingDescription =
    "[技能伤害]:\n+2% 每 10 力量 [+8%]\n+2% 每 10 体质 [+8%]\n" +
    "+2% 每 10 灵巧 [+4%]\n+2% 每 10 智力 [+1%]";
CheckWithContext(
    "per-ten stat scaling screenshot family",
    statScalingDescription,
    "Text_Description",
    "[技能伤害]:\n每 10 点力量 +2% [+8%]\n每 10 点体质 +2% [+8%]\n" +
    "每 10 点灵巧 +2% [+4%]\n每 10 点智力 +2% [+1%]");
CheckCondition(
    "trusted producer canonicalizes the complete stat-scaling block",
    RuntimeTextTranslator.CanonicalizePerTenStatScaling(statScalingDescription) ==
    "[技能伤害]:\n每 10 点力量 +2% [+8%]\n每 10 点体质 +2% [+8%]\n" +
    "每 10 点灵巧 +2% [+4%]\n每 10 点智力 +2% [+1%]");
CheckCondition(
    "trusted producer stat-scaling canonicalization is idempotent",
    RuntimeTextTranslator.CanonicalizePerTenStatScaling(
        "每 10 点力量 +2% [+8%]") == "每 10 点力量 +2% [+8%]");
CheckCondition(
    "trusted producer leaves unrelated percentages unchanged",
    RuntimeTextTranslator.CanonicalizePerTenStatScaling(
        "暴击伤害：+20%") == "暴击伤害：+20%");
foreach (var context in new[] { string.Empty, "Text", "Value" })
{
    CheckWithContext(
        $"skill-damage block fallback context: {context}",
        statScalingDescription,
        context,
        "[技能伤害]:\n每 10 点力量 +2% [+8%]\n每 10 点体质 +2% [+8%]\n" +
        "每 10 点灵巧 +2% [+4%]\n每 10 点智力 +2% [+1%]");
}
var unbracketedStatScalingDescription =
    "技能伤害：\n+2% 每 10 力量 [+8%]\n每 10 点体质 +2% [+8%]\n" +
    "+2% 每 10 灵巧 [+4%]\n每 10 点智力 +2% [+1%]";
CheckCondition(
    "unbracketed skill-damage block is structurally recognized",
    RuntimeTextTranslator.IsSkillDamageScalingBlock(unbracketedStatScalingDescription));
CheckCondition(
    "unbracketed skill-damage header literal",
    unbracketedStatScalingDescription.Split('\n')[0] == "技能伤害：");
CheckWithContext(
    "skill-damage block fallback without title brackets",
    unbracketedStatScalingDescription,
    "Value",
    "技能伤害：\n每 10 点力量 +2% [+8%]\n每 10 点体质 +2% [+8%]\n" +
    "每 10 点灵巧 +2% [+4%]\n每 10 点智力 +2% [+1%]");
var liveMixedStatScalingDescription =
    "[技能伤害]:\n+2% 每 10 力量 [+8%]\n每 10 点体质 +2% [+8%]\n" +
    "每 10 点灵巧 +2% [+4%]\n+2% 每 10 智力 [+1%]";
CheckWithContext(
    "live mixed stat-scaling screenshot order",
    liveMixedStatScalingDescription,
    "Value",
    "[技能伤害]:\n每 10 点力量 +2% [+8%]\n每 10 点体质 +2% [+8%]\n" +
    "每 10 点灵巧 +2% [+4%]\n每 10 点智力 +2% [+1%]");
var siblingSpanStatScalingDescription =
    "<color=#ffd51f>[技能伤害]:</color>\n" +
    "<color=#ffd51f>+2% 每 10 力量</color> <color=#7fff00>[+8%]</color>\n" +
    "<color=#ffd51f>每 10 点体质 +2%</color> <color=#7fff00>[+8%]</color>\n" +
    "<color=#ffd51f>每 10 点灵巧 +2%</color> <color=#7fff00>[+4%]</color>\n" +
    "<color=#ffd51f>+2% 每 10 智力</color> <color=#7fff00>[+1%]</color>";
var expectedSiblingSpanStatScalingDescription =
    "<color=#ffd51f>[技能伤害]:</color>\n" +
    "<color=#ffd51f>每 10 点力量 +2%</color> <color=#7fff00>[+8%]</color>\n" +
    "<color=#ffd51f>每 10 点体质 +2%</color> <color=#7fff00>[+8%]</color>\n" +
    "<color=#ffd51f>每 10 点灵巧 +2%</color> <color=#7fff00>[+4%]</color>\n" +
    "<color=#ffd51f>每 10 点智力 +2%</color> <color=#7fff00>[+1%]</color>";
CheckCondition(
    "sibling-span skill-damage block is structurally recognized",
    RuntimeTextTranslator.IsSkillDamageScalingBlock(siblingSpanStatScalingDescription));
CheckWithContext(
    "sibling color spans preserve labels and dynamic tails",
    siblingSpanStatScalingDescription,
    "Text",
    expectedSiblingSpanStatScalingDescription);
CheckWithContext(
    "sibling color spans preserve separator inside primary span",
    "<color=#ffd51f>+2% 每 10 力量 </color><color=#7fff00>[+8%]</color>",
    "Value",
    "<color=#ffd51f>每 10 点力量 +2% </color><color=#7fff00>[+8%]</color>");
CheckWithContext(
    "nested sibling primary span preserves all rich tags",
    "<b><color=#ffd51f><size=20>+2%</size> 每 10 <u>力量</u></color></b> " +
    "<color=#7fff00>[+8%]</color>",
    "Text",
    "<b><color=#ffd51f>每 10 点<u>力量</u> <size=20>+2%</size></color></b> " +
    "<color=#7fff00>[+8%]</color>");
CheckWithContext(
    "sibling color spans are idempotent",
    expectedSiblingSpanStatScalingDescription,
    "Value",
    expectedSiblingSpanStatScalingDescription,
    expectedChange: false);
var productionEnglishStatScalingDescription =
    "[Skill Damage]:\n+2% per 10 Strength [+8%]\n+2% per 10 Vitality [+8%]\n" +
    "+2% per 10 Dexterity [+4%]\n+2% per 10 Intelligence [+1%]";
var productionOrderTranslator = new RuntimeTextTranslator(
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [productionEnglishStatScalingDescription] = siblingSpanStatScalingDescription,
    });
var productionExactChanged = productionOrderTranslator.TryTranslate(
    productionEnglishStatScalingDescription,
    "Value",
    out var productionExactResult);
CheckCondition(
    "exact English projection canonicalizes mixed localized skill-damage block",
    productionExactChanged &&
    productionExactResult == expectedSiblingSpanStatScalingDescription);
var productionRichEnglishStatScalingDescription =
    "<size=24>[Skill Damage]:</size>\n+2% per 10 Strength [+8%]\n" +
    "+2% per 10 Vitality [+8%]\n+2% per 10 Dexterity [+4%]\n" +
    "+2% per 10 Intelligence [+1%]";
var productionVisibleExactChanged = productionOrderTranslator.TryTranslate(
    productionRichEnglishStatScalingDescription,
    "Text",
    out var productionVisibleExactResult);
CheckCondition(
    "visible exact English projection canonicalizes mixed rich localized target",
    productionVisibleExactChanged &&
    productionVisibleExactResult == expectedSiblingSpanStatScalingDescription);
var protectedProductionChanged = productionOrderTranslator.TryTranslate(
    productionEnglishStatScalingDescription,
    "ChatText",
    out var protectedProductionResult);
CheckCondition(
    "production exact skill-damage source remains protected in chat",
    !protectedProductionChanged &&
    protectedProductionResult == productionEnglishStatScalingDescription);
var embeddedStatScalingDescription =
    "攻击：20\n----------------\n[技能伤害]:\n" +
    "<color=#ffd51f>+2% 每 10 力量</color> <color=#7fff00>[+8%]</color>\n" +
    "<color=#ffd51f>+2% 每 10 智力</color> <color=#7fff00>[+1%]</color>\n" +
    "----------------\n冷却：6 秒";
CheckCondition(
    "embedded skill-damage block is structurally recognized",
    RuntimeTextTranslator.IsSkillDamageScalingBlock(embeddedStatScalingDescription));
CheckWithContext(
    "embedded skill-damage block preserves surrounding description",
    embeddedStatScalingDescription,
    "Value",
    "攻击：20\n----------------\n[技能伤害]:\n" +
    "<color=#ffd51f>每 10 点力量 +2%</color> <color=#7fff00>[+8%]</color>\n" +
    "<color=#ffd51f>每 10 点智力 +2%</color> <color=#7fff00>[+1%]</color>\n" +
    "----------------\n冷却：6 秒");
foreach (var context in new[] { string.Empty, "Text", "Value", "SkillDamageValue" })
{
    CheckWithContext(
        $"strict stat-scaling fragment context: {context}",
        "<color=#ffd51f>+2% 每 10 力量</color> <color=#7fff00>[+8%]</color>",
        context,
        "<color=#ffd51f>每 10 点力量 +2%</color> <color=#7fff00>[+8%]</color>");
}
foreach (var context in new[] { "ChatText", "Text Area", "PlayerName", "Unknown" })
{
    CheckWithContext(
        $"strict stat-scaling fragment remains protected: {context}",
        "<color=#ffd51f>+2% 每 10 力量</color> <color=#7fff00>[+8%]</color>",
        context,
        "<color=#ffd51f>+2% 每 10 力量</color> <color=#7fff00>[+8%]</color>",
        expectedChange: false);
}
CheckWithContext(
    "generic multiline text does not enter fragment fallback",
    "普通系统文本\n+2% 每 10 力量 [+8%]",
    "Text",
    "普通系统文本\n+2% 每 10 力量 [+8%]",
    expectedChange: false);
foreach (var stat in new[] { "力量", "灵巧", "敏捷", "智力", "体质", "幸运" })
{
    CheckWithContext(
        $"per-ten stat scaling term: {stat}",
        $"+2.5% 每 10 {stat} (-12.5%)",
        "Tooltip",
        $"每 10 点{stat} +2.5% (-12.5%)");
}
var sourceStatTerms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Strength"] = "力量",
    ["Dexterity"] = "灵巧",
    ["Agility"] = "敏捷",
    ["Intelligence"] = "智力",
    ["Vitality"] = "体质",
    ["Luck"] = "幸运",
};
foreach (var stat in sourceStatTerms)
{
    CheckWithContext(
        $"per-ten source stat order: {stat.Key}",
        $"+1.25% per 10 {stat.Key} [+6.25%]",
        "Text_Description",
        $"每 10 点{stat.Value} +1.25% [+6.25%]");
    CheckWithContext(
        $"per-ten source stat-first order: {stat.Key}",
        $"per 10 {stat.Key} -2% (-4%)",
        "Tooltip",
        $"每 10 点{stat.Value} -2% (-4%)");
}
CheckWithContext(
    "per-ten source abbreviations",
    "+2% per 10 VIT ［+8%］",
    "Description",
    "每 10 点体质 +2% ［+8%］");
CheckWithContext(
    "per-ten source full-width spacing",
    "+2%　per　10　Strength　(+8%)",
    "Description",
    "每 10 点力量 +2% (+8%)");
CheckWithContext(
    "per-ten stat scaling preserves independent rich tags",
    "<color=#f0c>+2%</color> 每 10 <color=#0cf>力量</color> <color=#7f0>[+8%]</color>",
    "Description",
    "每 10 点<color=#0cf>力量</color> <color=#f0c>+2%</color> <color=#7f0>[+8%]</color>");
CheckWithContext(
    "per-ten source rich tags and full-width brackets",
    "<color=#f0c>+2%</color> per 10 <color=#0cf>Strength</color> <color=#7f0>【+8%】</color>",
    "Description",
    "每 10 点<color=#0cf>力量</color> <color=#f0c>+2%</color> <color=#7f0>【+8%】</color>");
CheckWithContext(
    "per-ten source stat-first rich tags",
    "per 10 <color=#0cf>Vitality</color> <color=#f0c>+1%</color> <color=#7f0>（-3%）</color>",
    "Tooltip",
    "每 10 点<color=#0cf>体质</color> <color=#f0c>+1%</color> <color=#7f0>（-3%）</color>");
CheckWithContext(
    "per-ten stat scaling preserves outer rich tag",
    "<color=#fc0>+3% 每 10 体质 (+21%)</color>",
    "Info",
    "<color=#fc0>每 10 点体质 +3% (+21%)</color>");
CheckWithContext(
    "per-ten stat scaling preserves CRLF and unrelated lines",
    "技能伤害：\r\n+3% 每 10 力量 (+20%)\r\n固定伤害：+10",
    "Description",
    "技能伤害：\r\n每 10 点力量 +3% (+20%)\r\n固定伤害：+10");
CheckWithContext(
    "correct per-ten stat order is idempotent",
    "每 10 点力量 +3% (+20%)",
    "Description",
    "每 10 点力量 +3% (+20%)",
    expectedChange: false);
foreach (var negative in new[]
{
    "+3% 每 30 力量 (+20%)",
    "+3% 每 10 力量",
    "Maximum ASPD is 185, increased by 1 per 30 AGI, up to 193",
    "ATK per STR",
    "MATK per STR",
})
{
    CheckWithContext(
        $"unrelated stat scaling remains exact: {negative}",
        negative,
        "Description",
        negative,
        expectedChange: false);
}
CheckWithContext(
    "per-ten stat scaling remains untouched in chat",
    "+3% 每 10 力量 (+20%)",
    "ChatText",
    "+3% 每 10 力量 (+20%)",
    expectedChange: false);
CheckWithContext(
    "per-ten stat scaling remains untouched outside descriptions",
    "+3% 每 10 力量 (+20%)",
    "ItemName:UIinventoryItem",
    "+3% 每 10 力量 (+20%)",
    expectedChange: false);
foreach (var protectedContext in new[]
{
    "ChatText", "PlayerName", "Shop Name", "Vending", "Message"
})
{
    CheckWithContext(
        $"skill-damage block remains protected in {protectedContext}",
        statScalingDescription,
        protectedContext,
        statScalingDescription,
        expectedChange: false);
}
CheckWithContext(
    "unknown context single stat line remains untouched",
    "+2% 每 10 力量 [+8%]",
    "Unknown",
    "+2% 每 10 力量 [+8%]",
    expectedChange: false);
foreach (var protectedContext in new[] { "PlayerName", "Shop Name", "Message", "Unknown" })
{
    CheckWithContext(
        $"per-ten stat scaling protected context: {protectedContext}",
        "+2% per 10 Strength [+8%]",
        protectedContext,
        "+2% per 10 Strength [+8%]",
        expectedChange: false);
}
CheckWithContext(
    "structured artifact stats",
    "Damage: 100% + lv * 100% MATK\nMagic Defence: 100\nHP: +10%",
    "Description",
    "伤害： 100% + 等级 * 100% 魔法攻击\n魔法防御： 100\n生命值： +10%");
CheckWithContext(
    "structured stats remain untouched in chat",
    "Damage: 100% + lv * 100% MATK\nMagic Defence: 100\nHP: +10%",
    "ChatText",
    "Damage: 100% + lv * 100% MATK\nMagic Defence: 100\nHP: +10%",
    expectedChange: false);
var ancientEssenceGemDescription =
    "A gem infused with ancient essence. When embedded into an Artifact, it awakens hidden strength within its bearer.";
var localizedAncientEssenceGemDescription =
    "一颗注入古老精华的宝石。嵌入神器后，会唤醒持有者体内潜藏的力量。";
CheckWithContext(
    "ancient essence artifact gem description",
    ancientEssenceGemDescription,
    "Description",
    localizedAncientEssenceGemDescription);
CheckWithContext(
    "ancient essence artifact gem mixed description",
    ancientEssenceGemDescription + "\n[唯一] 仅一个副本生效。\n\n死亡缠绕伤害: +2% 每次精炼",
    "Description",
    localizedAncientEssenceGemDescription + "\n[唯一] 仅一个副本生效。\n\n死亡缠绕伤害: +2% 每次精炼");
CheckWithContext(
    "ancient essence sentence remains untouched in chat",
    ancientEssenceGemDescription,
    "ChatText",
    ancientEssenceGemDescription,
    expectedChange: false);
Check(
    "gem description template",
    "一颗闪耀的宝石，封存着一位昔日 Mage 的记忆。嵌入神器后，使用者的 Fireball 将获得强化。",
    "一颗闪耀的宝石，封存着一位昔日法师的记忆。嵌入神器后，使用者的火球将获得强化。");
Check(
    "unknown gem description remains untouched",
    "一颗闪耀的宝石，封存着一位昔日 UnknownClass 的记忆。嵌入神器后，使用者的 Fireball 将获得强化。",
    "一颗闪耀的宝石，封存着一位昔日 UnknownClass 的记忆。嵌入神器后，使用者的 Fireball 将获得强化。",
    expectedChange: false);
foreach (var alias in new[]
{
    "Axe Quicken", "Spear Quicken", "Bleed Attack", "Grave Chill Enemy", "Necrotic Presence Enemy",
    "Sharpen", "Soul Drain Enemy", "Spell Shield", "Stun Attack",
    "Summon Abomination", "Summon Death Mage", "Summon Skeleton",
    "Summon Skeleton Mage", "Summon Wraith", "Thorns"
})
{
    CheckWithContext($"runtime skill alias: {alias}", alias + "!", "CastName", translations[alias] + "！");
}
CheckWithContext("rich exact item", "<color=#fff>Vital Broad Sword</color>", "ItemName", "<color=#fff>活力阔剑</color>");
CheckWithContext("rich exact item Name", "<color=#fff>Vital Broad Sword</color>", "Name", "<color=#fff>活力阔剑</color>");
CheckWithContext(
    "split rich exact item Name",
    "<color=#fc0>Vital</color> <color=#fff>Broad Sword</color>",
    "Name",
    "<color=#fc0>活力</color><color=#fff>阔剑</color>");
CheckWithContext("unknown Name remains untouched", "Bee!", "Name", "Bee!", expectedChange: false);
CheckCondition("CJK detection", CjkText.ContainsCjk("大黄蜂"));
CheckCondition("ASCII is not CJK", !CjkText.ContainsCjk("Bumblebee"));
CheckCondition("numeric HUD value skips contextual work", !translator.MayTranslate("123 / 456"));
CheckCondition("TMP format placeholder skips contextual work", !translator.MayTranslate("{0:0}"));
CheckCondition("exact dictionary value requires translation work", translator.MayTranslate("Inventory"));
CheckCondition("dynamic ASCII value requires translation work", translator.MayTranslate("186ms"));
CheckCondition("localized CJK value keeps font processing", translator.MayTranslate("背包"));

var cacheTranslator = new RuntimeTextTranslator(
    translations,
    reviewedItemAffixes,
    reviewedItemBaseNames,
    cacheCapacity: 4);
CheckWithTranslator(
    "cache preserves an untranslated miss",
    cacheTranslator,
    "Cooldown 9.8",
    "Value",
    "Cooldown 9.8",
    expectedChange: false);
var cachedPlainCount = cacheTranslator.CachedTranslationCount;
var cachedContextCount = cacheTranslator.CachedContextTranslationCount;
CheckWithTranslator(
    "cached untranslated miss remains stable",
    cacheTranslator,
    "Cooldown 9.8",
    "Value",
    "Cooldown 9.8",
    expectedChange: false);
CheckCondition(
    "cache hit does not add duplicate entries",
    cacheTranslator.CachedTranslationCount == cachedPlainCount &&
    cacheTranslator.CachedContextTranslationCount == cachedContextCount);
var exactCacheTranslator = new RuntimeTextTranslator(
    translations,
    reviewedItemAffixes,
    reviewedItemBaseNames,
    cacheCapacity: 4);
var firstExactChanged = exactCacheTranslator.TryTranslate("Market Ice Chest", out var firstExact);
var exactCacheCount = exactCacheTranslator.CachedTranslationCount;
var secondExactChanged = exactCacheTranslator.TryTranslate("Market Ice Chest", out var secondExact);
CheckCondition(
    "exact translations are cached without changing output",
    firstExactChanged && secondExactChanged &&
    firstExact == "恐狼 胸甲" && secondExact == firstExact &&
    exactCacheCount == 1 && exactCacheTranslator.CachedTranslationCount == exactCacheCount);
for (var cacheIndex = 0; cacheIndex < 12; cacheIndex++)
{
    cacheTranslator.TryTranslate("Unknown runtime value " + cacheIndex, "Value", out _);
}
CheckCondition(
    "translation caches remain bounded",
    cacheTranslator.CachedTranslationCount <= 4 &&
    cacheTranslator.CachedContextTranslationCount <= 4);
var protectedCacheTranslator = new RuntimeTextTranslator(
    translations,
    reviewedItemAffixes,
    reviewedItemBaseNames,
    cacheCapacity: 4);
CheckWithTranslator(
    "protected player text bypasses the cache",
    protectedCacheTranslator,
    "Fireball",
    "UserInput:CharacterName",
    "Fireball",
    expectedChange: false);
CheckCondition(
    "protected player text is not retained",
    protectedCacheTranslator.CachedTranslationCount == 0 &&
    protectedCacheTranslator.CachedContextTranslationCount == 0);

CheckMarketSearch("Chinese complete item name", "尖刺王冠", "Crown of Spikes", MarketSearchBridgeOutcome.Translated);
CheckMarketSearch("Chinese affix plus base", "风抗 太阳狮冠", "Windproof Sun Lion Crest", MarketSearchBridgeOutcome.Translated);
CheckMarketSearch("reviewed Chinese keyword", "太阳", "Sun", MarketSearchBridgeOutcome.Translated);
CheckMarketSearch("reviewed Chinese compound keyword", "向日葵", "Sunflower", MarketSearchBridgeOutcome.Translated);
CheckMarketSearch("upgraded keyword preserves prefix", "+6 太阳", "+6 Sun", MarketSearchBridgeOutcome.Translated);
CheckMarketSearch("rich keyword preserves markup", "<color=#FFD700>太阳</color>", "<color=#FFD700>Sun</color>", MarketSearchBridgeOutcome.Translated);
CheckMarketSearch("unique Chinese base substring", "太阳狮", "Sun Lion Crest", MarketSearchBridgeOutcome.Translated);
CheckMarketSearch("upgraded unique Chinese base substring", "+6 太阳狮", "+6 Sun Lion Crest", MarketSearchBridgeOutcome.Translated);
CheckMarketSearch("English source remains exact", "Crown of Spikes", "Crown of Spikes", MarketSearchBridgeOutcome.Unchanged);
CheckMarketSearch("English keyword remains exact", "sun", "sun", MarketSearchBridgeOutcome.Unchanged);
CheckMarketSearch("upgrade level and spacing are preserved", "  +6 风抗 太阳狮冠  ", "  +6 Windproof Sun Lion Crest  ", MarketSearchBridgeOutcome.Translated);
CheckMarketSearch(
    "rich text is preserved",
    "<color=#FFD700>风抗</color> <color=#FFFFFF>太阳狮冠</color>",
    "<color=#FFD700>Windproof</color> <color=#FFFFFF>Sun Lion Crest</color>",
    MarketSearchBridgeOutcome.Translated);
CheckMarketSearch("ambiguous Chinese affix remains exact", "净化 双刃护套", "净化 双刃护套", MarketSearchBridgeOutcome.Ambiguous);
CheckMarketSearch("unknown Chinese remains exact", "不存在的装备", "不存在的装备", MarketSearchBridgeOutcome.Unchanged);
CheckMarketSearch("ambiguous Chinese keyword remains exact", "宝石", "宝石", MarketSearchBridgeOutcome.Ambiguous);
CheckMarketSearch("common one-character base substring remains exact", "剑", "剑", MarketSearchBridgeOutcome.Ambiguous);
CheckMarketSearch("affix-only substring remains exact", "风抗", "风抗", MarketSearchBridgeOutcome.Unchanged);
var contextualOutcome = contextSpecificMarketSearchBridge.TryBridge(
    MarketSearchQueryBridge.SupportedDeclaringType,
    MarketSearchQueryBridge.SupportedMethod,
    "语境词缀 皇家匕首",
    out var contextualQuery);
CheckCondition(
    "market bridge accepts explicit context-specific affix targets",
    contextualOutcome == MarketSearchBridgeOutcome.Translated &&
    contextualQuery == "Contextual Affix Royal Dagger");
foreach (var protectedSurface in new[]
{
    ("ChatInput", "Submit"),
    ("CharacterNameInput", "Submit"),
    ("ShopNameInput", "Submit"),
    ("GuildNameInput", "Submit"),
    ("PartyNameInput", "Submit"),
    ("SettingsInput", "Apply"),
    ("UIVendingSearch", "CreateShop"),
})
{
    CheckMarketSearch(
        $"market bridge rejects {protectedSurface.Item1}.{protectedSurface.Item2}",
        "风抗 太阳狮冠",
        "风抗 太阳狮冠",
        MarketSearchBridgeOutcome.Unchanged,
        protectedSurface.Item1,
        protectedSurface.Item2);
}

CheckIdempotent("恐狼 胸甲");
CheckIdempotent("<color=Chest>胸甲</color> 胸甲");
CheckIdempotent("[5小时前] 已将 死灵法师卡片 售予 Deadly Snake，售价 45");

var corpusSummary = RunCorpusChecks(args);

if (failures != 0)
{
    Console.Error.WriteLine($"FAILED: {failures} runtime localization test(s)");
    return 1;
}

Console.WriteLine($"PASSED: {checks} runtime localization tests" + corpusSummary);
return 0;

void Check(string name, string source, string expected, bool expectedChange = true)
{
    checks++;
    var changed = translator.TryTranslate(source, out var actual);
    if (changed == expectedChange && actual == expected)
    {
        return;
    }

    failures++;
    Console.Error.WriteLine(
        $"{name}: changed={changed}, expectedChange={expectedChange}\n" +
        $"  expected: {expected}\n  actual:   {actual}");
}

void RunHotPathBenchmark()
{
    var cases = new[]
    {
        (Source: "186ms", Context: "Ping"),
        (Source: "123 / 456", Context: "Value"),
        (Source: "Cooldown 9.8", Context: "Value"),
        (Source: "Inventory", Context: "Text"),
        (Source: "<color=#fff>Vital Broad Sword</color>", Context: "ItemName"),
        (Source: "FPS: 144 (6.9ms) Ping: 38ms Players: 42", Context: "Diagnostics"),
    };
    const int iterations = 25000;

    foreach (var item in cases)
    {
        translator.TryTranslate(item.Source, item.Context, out _);
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var checksum = 0;
    var changedCount = 0;
    var stopwatch = Stopwatch.StartNew();
    for (var iteration = 0; iteration < iterations; iteration++)
    {
        foreach (var item in cases)
        {
            if (translator.TryTranslate(item.Source, item.Context, out var translated))
            {
                changedCount++;
            }
            checksum = unchecked((checksum * 397) ^ translated.Length);
        }
    }
    stopwatch.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    Console.WriteLine(
        $"HOT_PATH calls={iterations * cases.Length} elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F1} " +
        $"allocated_bytes={allocated} changed={changedCount} checksum={checksum}");

    var dynamicSources = Enumerable.Range(0, 5000)
        .Select(value => value + ".25ms")
        .ToArray();
    var dynamicTranslator = new RuntimeTextTranslator(
        translations,
        reviewedItemAffixes,
        reviewedItemBaseNames);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    checksum = 0;
    changedCount = 0;
    stopwatch.Restart();
    foreach (var source in dynamicSources)
    {
        if (dynamicTranslator.TryTranslate(source, "Ping", out var translated))
        {
            changedCount++;
        }
        checksum = unchecked((checksum * 397) ^ translated.Length);
    }
    stopwatch.Stop();
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    Console.WriteLine(
        $"DYNAMIC_MISS calls={dynamicSources.Length} elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F1} " +
        $"allocated_bytes={allocated} changed={changedCount} checksum={checksum}");
}

void CheckIdempotent(string value)
{
    Check("idempotence", value, value, expectedChange: false);
}

void CheckCondition(string name, bool condition)
{
    checks++;
    if (condition)
    {
        return;
    }
    failures++;
    Console.Error.WriteLine($"{name}: condition failed");
}

void CheckMarketSearch(
    string name,
    string query,
    string expected,
    MarketSearchBridgeOutcome expectedOutcome,
    string declaringType = MarketSearchQueryBridge.SupportedDeclaringType,
    string method = MarketSearchQueryBridge.SupportedMethod)
{
    checks++;
    var outcome = marketSearchBridge.TryBridge(
        declaringType,
        method,
        query,
        out var actual);
    if (outcome == expectedOutcome && actual == expected)
    {
        return;
    }

    failures++;
    Console.Error.WriteLine(
        $"{name}: outcome={outcome}, expectedOutcome={expectedOutcome}\n" +
        $"  expected: {expected}\n  actual:   {actual}");
}

void CheckWithContext(string name, string source, string context, string expected, bool expectedChange = true)
{
    checks++;
    var changed = translator.TryTranslate(source, context, out var actual);
    if (changed == expectedChange && actual == expected)
    {
        return;
    }
    failures++;
    Console.Error.WriteLine(
        $"{name}: changed={changed}, expectedChange={expectedChange}\n" +
        $"  expected: {expected}\n  actual:   {actual}");
}

void CheckWithTranslator(
    string name,
    RuntimeTextTranslator subject,
    string source,
    string context,
    string expected,
    bool expectedChange = true)
{
    checks++;
    var changed = subject.TryTranslate(source, context, out var actual);
    if (changed == expectedChange && actual == expected)
    {
        return;
    }
    failures++;
    Console.Error.WriteLine(
        $"{name}: changed={changed}, expectedChange={expectedChange}\n" +
        $"  expected: {expected}\n  actual:   {actual}");
}

string RunCorpusChecks(string[] arguments)
{
    if (arguments.Length == 0)
    {
        return string.Empty;
    }

    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index + 1 < arguments.Length; index += 2)
    {
        values[arguments[index]] = arguments[index + 1];
    }
    if (!values.TryGetValue("--dictionary", out var dictionaryPath) ||
        !values.TryGetValue("--snapshot", out var snapshotPath) ||
        !values.TryGetValue("--skill-aliases", out var skillAliasesPath) ||
        !values.TryGetValue("--corpus-report", out var corpusReportPath))
    {
        failures++;
        Console.Error.WriteLine(
            "corpus checks require --dictionary, --snapshot, --skill-aliases, and --corpus-report");
        return string.Empty;
    }

    var fullTranslations = new Dictionary<string, string>(StringComparer.Ordinal);
    var fullItemAffixes = new HashSet<string>(StringComparer.Ordinal);
    var fullItemBaseNames = new HashSet<string>(StringComparer.Ordinal);
    var fullMarketSearchNames = new HashSet<string>(StringComparer.Ordinal);
    var fullMarketSearchNamesByCategory = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    var fullMarketSearchKeywords = new List<KeyValuePair<string, string>>();
    var fullMarketSearchKeywordRows = new HashSet<string>(StringComparer.Ordinal);
    var fullMarketSearchEntryRows = new List<string[]>();
    var fullMarketSearchEntryKeys = new HashSet<string>(StringComparer.Ordinal);
    var fullMarketSearchAliases = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    var fullMarketSearchAliasRows = new HashSet<string>(StringComparer.Ordinal);
    foreach (var line in File.ReadLines(dictionaryPath))
    {
        if (string.IsNullOrEmpty(line))
        {
            continue;
        }
        if (line.StartsWith("#market-search-name\t", StringComparison.Ordinal))
        {
            var marketParts = line.Split(new[] { '\t' }, 3);
            if (marketParts.Length != 3 || string.IsNullOrEmpty(marketParts[1]) || string.IsNullOrEmpty(marketParts[2]))
            {
                failures++;
                Console.Error.WriteLine($"invalid market-search-name row: {line}");
                return string.Empty;
            }
            if (!fullMarketSearchNamesByCategory.TryGetValue(marketParts[1], out var categoryNames))
            {
                categoryNames = new HashSet<string>(StringComparer.Ordinal);
                fullMarketSearchNamesByCategory.Add(marketParts[1], categoryNames);
            }
            categoryNames.Add(marketParts[2]);
            fullMarketSearchNames.Add(marketParts[2]);
            continue;
        }
        if (line.StartsWith("#market-search-keyword\t", StringComparison.Ordinal))
        {
            var keywordParts = line.Split(new[] { '\t' }, 3);
            var keywordRow = keywordParts.Length == 3
                ? keywordParts[1] + "\0" + keywordParts[2]
                : string.Empty;
            if (keywordParts.Length != 3 ||
                string.IsNullOrEmpty(keywordParts[1]) ||
                string.IsNullOrEmpty(keywordParts[2]) ||
                !fullMarketSearchKeywordRows.Add(keywordRow))
            {
                failures++;
                Console.Error.WriteLine($"invalid market-search-keyword row: {line}");
                return string.Empty;
            }
            fullMarketSearchKeywords.Add(
                new KeyValuePair<string, string>(keywordParts[1], keywordParts[2]));
            continue;
        }
        if (line.StartsWith("#market-search-entry\t", StringComparison.Ordinal))
        {
            var entryParts = line.Split(new[] { '\t' }, 5);
            var entryKey = entryParts.Length == 5
                ? string.Join("\0", entryParts.Skip(1))
                : string.Empty;
            if (entryParts.Length != 5 ||
                entryParts.Skip(1).Any(string.IsNullOrEmpty) ||
                !fullMarketSearchEntryKeys.Add(entryKey))
            {
                failures++;
                Console.Error.WriteLine($"invalid market-search-entry row: {line}");
                return string.Empty;
            }
            fullMarketSearchEntryRows.Add(entryParts);
            continue;
        }
        if (line.StartsWith("#market-search-alias\t", StringComparison.Ordinal))
        {
            var aliasParts = line.Split(new[] { '\t' }, 4);
            var identityKey = aliasParts.Length == 4
                ? aliasParts[1] + "\0" + aliasParts[2]
                : string.Empty;
            var aliasRow = aliasParts.Length == 4
                ? identityKey + "\0" + aliasParts[3]
                : string.Empty;
            if (aliasParts.Length != 4 ||
                aliasParts.Skip(1).Any(string.IsNullOrEmpty) ||
                !fullMarketSearchAliasRows.Add(aliasRow))
            {
                failures++;
                Console.Error.WriteLine($"invalid market-search-alias row: {line}");
                return string.Empty;
            }
            if (!fullMarketSearchAliases.TryGetValue(identityKey, out var aliases))
            {
                aliases = new HashSet<string>(StringComparer.Ordinal);
                fullMarketSearchAliases.Add(identityKey, aliases);
            }
            aliases.Add(aliasParts[3]);
            continue;
        }
        if (line.StartsWith("#item-affix\t", StringComparison.Ordinal))
        {
            fullItemAffixes.Add(line.Substring("#item-affix\t".Length));
            continue;
        }
        if (line.StartsWith("#item-base\t", StringComparison.Ordinal))
        {
            fullItemBaseNames.Add(line.Substring("#item-base\t".Length));
            continue;
        }
        if (line.StartsWith("#", StringComparison.Ordinal))
        {
            continue;
        }
        var parts = line.Split(new[] { '\t' }, 2);
        if (parts.Length != 2 || !fullTranslations.TryAdd(parts[0], parts[1]))
        {
            failures++;
            Console.Error.WriteLine($"invalid corpus dictionary row: {line}");
            return string.Empty;
        }
    }

    CheckCondition(
        "runtime dictionary exposes reviewed item affix metadata",
        fullItemAffixes.Count == 265 &&
        new[] { "Windproof", "Savage", "Purifying", "Assassin's", "Combo", "Mana Burn" }
            .All(fullItemAffixes.Contains));
    CheckCondition(
        "runtime dictionary exposes reviewed item base metadata",
        fullItemBaseNames.Count == 1138 &&
        new[]
        {
            "Sun Lion Crest", "Skull Pendant", "Dualblade Sheath", "Archer's Beads",
            "Skullhacker", "Royal Dagger",
        }.All(fullItemBaseNames.Contains));
    var expectedMarketSearchCategoryCounts = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Artifacts"] = 45,
        ["Cards"] = 328,
        ["Consumables"] = 31,
        ["Cosmetics"] = 1120,
        ["Equips"] = 622,
        ["Gems"] = 129,
        ["Junks"] = 280,
    };
    CheckCondition(
        "runtime dictionary exposes only proven market ItemType categories",
        fullMarketSearchNamesByCategory.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(expectedMarketSearchCategoryCounts.Keys));
    foreach (var expectedCategory in expectedMarketSearchCategoryCounts)
    {
        CheckCondition(
            $"market search metadata count: {expectedCategory.Key}",
            fullMarketSearchNamesByCategory.TryGetValue(expectedCategory.Key, out var names) &&
            names.Count == expectedCategory.Value);
    }
    CheckCondition("1948 unique reviewed market search names", fullMarketSearchNames.Count == 1948);
    CheckCondition("575 reviewed market search keywords", fullMarketSearchKeywords.Count == 575);
    var fullMarketSearchEntries = fullMarketSearchEntryRows
        .Select(parts =>
        {
            fullMarketSearchAliases.TryGetValue(parts[1] + "\0" + parts[2], out var aliases);
            return new MarketSearchCatalogEntry(
                parts[1],
                parts[2],
                parts[3],
                parts[4],
                aliases != null
                    ? (IEnumerable<string>)aliases
                    : Array.Empty<string>());
        })
        .ToArray();
    var fullMarketIdentityKeys = fullMarketSearchEntries
        .Select(entry => entry.Identity.ItemType + "\0" + entry.Identity.ItemId)
        .ToHashSet(StringComparer.Ordinal);
    CheckCondition("2558 canonical market search entries", fullMarketSearchEntries.Length == 2558);
    CheckCondition("129 local market concept aliases", fullMarketSearchAliasRows.Count == 129);
    CheckCondition(
        "market aliases reference canonical identities",
        fullMarketSearchAliases.Keys.All(fullMarketIdentityKeys.Contains));
    CheckCondition(
        "canonical market entries use only VendingListing ItemType names",
        fullMarketSearchEntries.Select(entry => entry.Identity.ItemType)
            .ToHashSet(StringComparer.Ordinal)
            .SetEquals(new[] { "Junk", "Consumable", "Equip", "Artifact", "Card", "Gem", "Cosmetic" }));
    CheckCondition(
        "market search keywords include reviewed Chinese aliases",
        fullMarketSearchKeywordRows.Contains("Sun\0太阳") &&
        fullMarketSearchKeywordRows.Contains("Sunflower\0向日葵") &&
        fullMarketSearchKeywordRows.Contains("Gold\0金"));
    var ambiguousMarketKeywordTargets = fullMarketSearchKeywords
        .GroupBy(pair => pair.Value, StringComparer.Ordinal)
        .Where(group => group.Select(pair => pair.Key).Distinct(StringComparer.Ordinal).Count() > 1)
        .ToDictionary(
            group => group.Key,
            group => group.Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
    CheckCondition(
        "reviewed market keyword preferences eliminate reverse-search ambiguity",
        ambiguousMarketKeywordTargets.Count == 0);
    CheckCondition(
        "market search metadata includes representative ItemType names",
        new[]
        {
            "Sun Lion Crest", "Aerial Shot Gem", "Abomination Card", "Lunaris Shard",
            "Box of Mastery",
        }.All(fullMarketSearchNames.Contains));
    var fullMarketIndex = new MarketSearchQueryBridge(fullMarketSearchEntries);
    var fullMarketRequestBridge = new MarketSearchQueryBridge(
        fullTranslations,
        fullItemAffixes,
        fullItemBaseNames,
        fullMarketSearchNames,
        fullMarketSearchKeywords);
    var goldWireOutcome = fullMarketRequestBridge.TryBridge(
        MarketSearchQueryBridge.SupportedPlayerType,
        MarketSearchQueryBridge.SupportedPlayerRequestMethod,
        "金\u200B",
        out var goldWireFilter);
    CheckCondition(
        "PlayerController wire bridge maps committed Chinese gold without format residue",
        goldWireOutcome == MarketSearchBridgeOutcome.Translated &&
        goldWireFilter == "Gold");
    var englishWireOutcome = fullMarketRequestBridge.TryBridge(
        MarketSearchQueryBridge.SupportedPlayerType,
        MarketSearchQueryBridge.SupportedPlayerRequestMethod,
        "gold",
        out var englishWireFilter);
    CheckCondition(
        "PlayerController wire bridge leaves English search byte-for-byte unchanged",
        englishWireOutcome == MarketSearchBridgeOutcome.Unchanged &&
        englishWireFilter == "gold");

    HashSet<MarketSearchIdentity> ResolveMarketIdentities(
        string query,
        out MarketSearchIndexOutcome outcome,
        string declaringType = MarketSearchQueryBridge.SupportedManagerType,
        string method = MarketSearchQueryBridge.SupportedRequestMethod)
    {
        outcome = fullMarketIndex.TryResolveIdentities(
            declaringType,
            method,
            query,
            out var identities);
        return new HashSet<MarketSearchIdentity>(identities);
    }

    var goldByChinese = ResolveMarketIdentities("金", out var goldByChineseOutcome);
    CheckCondition(
        "canonical market index: 金 includes Gold Ore",
        goldByChineseOutcome == MarketSearchIndexOutcome.Matched &&
        goldByChinese.Contains(new MarketSearchIdentity("Junk", "Gold Ore")));
    var goldByAlias = ResolveMarketIdentities("黄金", out var goldByAliasOutcome);
    CheckCondition(
        "canonical market index: 黄金 includes Gold Ore",
        goldByAliasOutcome == MarketSearchIndexOutcome.Matched &&
        goldByAlias.Contains(new MarketSearchIdentity("Junk", "Gold Ore")));
    var englishGold = ResolveMarketIdentities("gold", out var englishGoldOutcome);
    CheckCondition(
        "canonical market index leaves English gold byte-for-byte unchanged",
        englishGoldOutcome == MarketSearchIndexOutcome.Unchanged && englishGold.Count == 0);
    var stormIdentities = ResolveMarketIdentities("风暴", out var stormOutcome);
    CheckCondition(
        "canonical market index: 风暴 retains Stormburst and Tempest",
        stormOutcome == MarketSearchIndexOutcome.Matched &&
        stormIdentities.Contains(new MarketSearchIdentity("Equip", "Stormburst Crossbow")) &&
        stormIdentities.Contains(new MarketSearchIdentity("Equip", "Tempest Staff")));
    var cactusIdentities = ResolveMarketIdentities("仙人掌", out var cactusOutcome);
    CheckCondition(
        "canonical market index: 仙人掌 retains Cactus and Cacti",
        cactusOutcome == MarketSearchIndexOutcome.Matched &&
        cactusIdentities.Contains(new MarketSearchIdentity("Card", "Cactus")) &&
        cactusIdentities.Contains(new MarketSearchIdentity("Card", "Cacti")));
    var exactChinese = ResolveMarketIdentities("金矿石", out var exactChineseOutcome);
    CheckCondition(
        "complete Chinese market name resolves canonical ItemData identity",
        exactChineseOutcome == MarketSearchIndexOutcome.Matched &&
        exactChinese.Contains(new MarketSearchIdentity("Junk", "Gold Ore")));
    CheckCondition(
        "unknown Chinese market query yields a local empty result",
        ResolveMarketIdentities("绝对不存在的市场物品", out var unknownChineseOutcome).Count == 0 &&
        unknownChineseOutcome == MarketSearchIndexOutcome.NoMatch);
    CheckCondition(
        "single CJK market query is indexed",
        ResolveMarketIdentities("矿", out var singleCjkOutcome)
            .Contains(new MarketSearchIdentity("Junk", "Gold Ore")) &&
        singleCjkOutcome == MarketSearchIndexOutcome.Matched);
    CheckCondition(
        "rich market query uses visible local index text",
        ResolveMarketIdentities("<color=#FFD700>黄金</color>", out var richMarketOutcome)
            .Contains(new MarketSearchIdentity("Junk", "Gold Ore")) &&
        richMarketOutcome == MarketSearchIndexOutcome.Matched);
    CheckCondition(
        "market query normalizes full-width whitespace locally",
        ResolveMarketIdentities("　黄 金　", out var spacedMarketOutcome)
            .Contains(new MarketSearchIdentity("Junk", "Gold Ore")) &&
        spacedMarketOutcome == MarketSearchIndexOutcome.Matched);
    foreach (var protectedMarketContext in new[]
             {
                 ("PlayerName", "SetName"),
                 ("ChatPanel", "Send"),
                 ("ShopName", "SetText"),
             })
    {
        var protectedIdentities = ResolveMarketIdentities(
            "黄金",
            out var protectedOutcome,
            protectedMarketContext.Item1,
            protectedMarketContext.Item2);
        CheckCondition(
            $"canonical market index excludes {protectedMarketContext.Item1}",
            protectedOutcome == MarketSearchIndexOutcome.Unchanged &&
            protectedIdentities.Count == 0);
    }
    foreach (var sourceGroup in fullMarketSearchEntries
                 .GroupBy(entry => entry.Source, StringComparer.Ordinal))
    {
        var groupEntries = sourceGroup.ToArray();
        var targets = groupEntries
            .Select(entry => entry.Target)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var exactOutcome = MarketSearchIndexOutcome.Unchanged;
        var resolvedIdentities = targets.Length == 1
            ? ResolveMarketIdentities(targets[0], out exactOutcome)
            : new HashSet<MarketSearchIdentity>();
        CheckCondition(
            $"exact Chinese market name includes canonical IDs: {sourceGroup.Key}",
            targets.Length == 1 &&
            exactOutcome == MarketSearchIndexOutcome.Matched &&
            groupEntries.All(entry => resolvedIdentities.Contains(entry.Identity)));
    }
    var fullTranslator = new RuntimeTextTranslator(
        fullTranslations,
        fullItemAffixes,
        fullItemBaseNames);
    var fullMarketSearchBridge = new MarketSearchQueryBridge(
        fullTranslations,
        fullItemAffixes,
        fullItemBaseNames,
        fullMarketSearchNames,
        fullMarketSearchKeywords);
    var completeItemOutcome = fullMarketSearchBridge.TryBridge(
        MarketSearchQueryBridge.SupportedDeclaringType,
        MarketSearchQueryBridge.SupportedMethod,
        "尖刺王冠",
        out var completeItemQuery);
    CheckCondition(
        "current dictionary bridges a unique complete Chinese item name",
        completeItemOutcome == MarketSearchBridgeOutcome.Translated &&
        completeItemQuery == "Crown of Spikes");
    foreach (var marketExample in new[]
    {
        (Name: "gem", Chinese: "空中射击宝石", English: "Aerial Shot Gem"),
        (Name: "card", Chinese: "憎恶卡片", English: "Abomination Card"),
        (Name: "material", Chinese: "露娜里斯碎片", English: "Lunaris Shard"),
        (Name: "consumable", Chinese: "精通宝箱", English: "Box of Mastery"),
    })
    {
        var outcome = fullMarketSearchBridge.TryBridge(
            MarketSearchQueryBridge.SupportedDeclaringType,
            MarketSearchQueryBridge.SupportedMethod,
            marketExample.Chinese,
            out var query);
        CheckCondition(
            $"current dictionary bridges a reviewed Chinese {marketExample.Name} name",
            outcome == MarketSearchBridgeOutcome.Translated && query == marketExample.English);
    }
    var compositeItemOutcome = fullMarketSearchBridge.TryBridge(
        MarketSearchQueryBridge.SupportedDeclaringType,
        MarketSearchQueryBridge.SupportedMethod,
        "+6 风抗 太阳狮冠",
        out var compositeItemQuery);
    CheckCondition(
        "current dictionary bridges a reviewed Chinese affix and base",
        compositeItemOutcome == MarketSearchBridgeOutcome.Translated &&
        compositeItemQuery == "+6 Windproof Sun Lion Crest");
    var keywordOutcome = fullMarketSearchBridge.TryBridge(
        MarketSearchQueryBridge.SupportedDeclaringType,
        MarketSearchQueryBridge.SupportedMethod,
        "太阳",
        out var keywordQuery);
    CheckCondition(
        "current dictionary bridges a reviewed Chinese market keyword",
        keywordOutcome == MarketSearchBridgeOutcome.Translated &&
        keywordQuery == "Sun");
    var compoundKeywordOutcome = fullMarketSearchBridge.TryBridge(
        MarketSearchQueryBridge.SupportedDeclaringType,
        MarketSearchQueryBridge.SupportedMethod,
        "向日葵",
        out var compoundKeywordQuery);
    CheckCondition(
        "current dictionary bridges a reviewed Chinese compound keyword",
        compoundKeywordOutcome == MarketSearchBridgeOutcome.Translated &&
        compoundKeywordQuery == "Sunflower");
    var preferredMarketKeywords = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["暗影"] = "Shadow",
        ["风暴"] = "Storm",
        ["火焰"] = "Fire",
        ["碎片"] = "Shard",
        ["兔子"] = "Bunny",
        ["仙人掌"] = "Cact",
        ["幽魂之王"] = "Wraith",
        ["陨星"] = "Meteor",
        ["斩首野兔"] = "Hare",
    };
    foreach (var preferredKeyword in preferredMarketKeywords)
    {
        var preferredOutcome = fullMarketSearchBridge.TryBridge(
            MarketSearchQueryBridge.SupportedDeclaringType,
            MarketSearchQueryBridge.SupportedMethod,
            preferredKeyword.Key,
            out var preferredQuery);
        CheckCondition(
            $"current dictionary bridges reviewed complex keyword: {preferredKeyword.Key}",
            preferredOutcome == MarketSearchBridgeOutcome.Translated &&
            preferredQuery == preferredKeyword.Value &&
            fullMarketSearchKeywordRows.Contains(
                preferredKeyword.Value + "\0" + preferredKeyword.Key));
    }
    CheckCondition(
        "Storm preference reaches the reported Stormburst Crossbow",
        "Stormburst Crossbow".IndexOf(
            preferredMarketKeywords["风暴"],
            StringComparison.OrdinalIgnoreCase) >= 0);
    CheckCondition(
        "Meteor preference reaches Meteor and Meteoric market names",
        new[] { "Meteor Gem", "Meteoric Staff" }.All(name =>
            name.IndexOf(
                preferredMarketKeywords["陨星"],
                StringComparison.OrdinalIgnoreCase) >= 0));
    CheckCondition(
        "Cact preference reaches Cactus and Cacti market names",
        new[] { "Cactus Card", "Cacti Card" }.All(name =>
            name.IndexOf(
                preferredMarketKeywords["仙人掌"],
                StringComparison.OrdinalIgnoreCase) >= 0));
    var substringItemOutcome = fullMarketSearchBridge.TryBridge(
        MarketSearchQueryBridge.SupportedDeclaringType,
        MarketSearchQueryBridge.SupportedMethod,
        "太阳狮",
        out var substringItemQuery);
    CheckCondition(
        "current dictionary bridges a unique Chinese base substring",
        substringItemOutcome == MarketSearchBridgeOutcome.Translated &&
        substringItemQuery == "Sun Lion Crest");
    var ambiguousBaseOutcome = fullMarketSearchBridge.TryBridge(
        MarketSearchQueryBridge.SupportedDeclaringType,
        MarketSearchQueryBridge.SupportedMethod,
        "箭袋",
        out var ambiguousBaseQuery);
    CheckCondition(
        "current dictionary preserves an ambiguous Chinese base name",
        ambiguousBaseOutcome == MarketSearchBridgeOutcome.Ambiguous &&
        ambiguousBaseQuery == "箭袋");
    var ambiguousAffixOutcome = fullMarketSearchBridge.TryBridge(
        MarketSearchQueryBridge.SupportedDeclaringType,
        MarketSearchQueryBridge.SupportedMethod,
        "净化 双刃护套",
        out var ambiguousAffixQuery);
    CheckCondition(
        "current dictionary preserves an ambiguous Chinese affix",
        ambiguousAffixOutcome == MarketSearchBridgeOutcome.Ambiguous &&
        ambiguousAffixQuery == "净化 双刃护套");
    var expectedAmbiguousMarketTargets = new[]
    {
        "白蚁士兵", "柑橘", "贯箭之盾", "箭袋", "毛皮", "日蚀刃", "水手帽",
        "兔子卡片", "仙人掌卡片", "遗失的鞋",
    };
    var actualAmbiguousMarketTargets = fullMarketSearchNames
        .GroupBy(source => fullTranslations[source], StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .OrderBy(target => target, StringComparer.Ordinal)
        .ToArray();
    CheckCondition(
        "market search dictionary has exactly 10 reviewed duplicate Chinese targets",
        actualAmbiguousMarketTargets.OrderBy(target => target, StringComparer.Ordinal)
            .SequenceEqual(expectedAmbiguousMarketTargets.OrderBy(target => target, StringComparer.Ordinal)));
    foreach (var target in expectedAmbiguousMarketTargets)
    {
        var outcome = fullMarketSearchBridge.TryBridge(
            MarketSearchQueryBridge.SupportedDeclaringType,
            MarketSearchQueryBridge.SupportedMethod,
            target,
            out var query);
        CheckCondition(
            $"market search preserves duplicate Chinese target: {target}",
            outcome == MarketSearchBridgeOutcome.Ambiguous && query == target);
    }
    var runtimeSkillAliases = 0;
    var runtimeDisplayAliases = 0;
    var spearQuickenAliasCanonicalChecks = 0;
    foreach (var line in File.ReadLines(skillAliasesPath))
    {
        if (line.StartsWith("id\t", StringComparison.Ordinal))
        {
            continue;
        }
        var parts = line.Split('\t');
        if (parts.Length != 5 || parts[4] != "covered")
        {
            failures++;
            Console.Error.WriteLine($"invalid or uncovered runtime skill alias row: {line}");
            continue;
        }

        runtimeSkillAliases++;
        var id = parts[0];
        var canonical = parts[1];
        var display = parts[2];
        var target = parts[3];
        if (canonical != display)
        {
            runtimeDisplayAliases++;
        }
        if (id == "SpearQuicken")
        {
            spearQuickenAliasCanonicalChecks++;
            CheckCondition(
                "SpearQuicken display alias matches Precision Focus canonical target",
                canonical == "Precision Focus" &&
                display == "Spear Quicken" &&
                fullTranslations.TryGetValue(canonical, out var canonicalTarget) &&
                target == canonicalTarget);
        }
        foreach (var source in new[] { display + "!", display + " !", display + "！" })
        {
            CheckWithTranslator(
                $"runtime skill alias punctuation: {parts[0]} / {source}",
                fullTranslator,
                source,
                "CastName",
                target + "！");
        }
    }
    CheckCondition("278 covered runtime skill aliases", runtimeSkillAliases == 278);
    CheckCondition("27 runtime skill display aliases", runtimeDisplayAliases == 27);
    CheckCondition("one SpearQuicken alias-to-canonical check", spearQuickenAliasCanonicalChecks == 1);
    var expectedMonsterTerms = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Aqua Merling"] = "水蓝鱼人",
        ["Canary Merling"] = "金黄鱼人",
        ["Roseate Merling"] = "玫红鱼人",
        ["Darter"] = "箭蜓",
        ["Skimmer"] = "掠水蜓",
        ["Hawker"] = "巡猎蜓",
        ["Nymph"] = "花仙",
        ["Stormjelly"] = "风暴水母",
        ["Stormjelly Pet"] = "风暴水母宠物",
        ["Echo Priest"] = "回声牧师",
        ["Elder Fire"] = "火焰长老",
        ["Inferno Bat"] = "炼狱蝙蝠",
        ["Snout Robot"] = "长吻机器人",
        ["Spiderling Robot"] = "幼蛛机器人",
        ["Trooper Robot"] = "突击机器人",
    };
    foreach (var pair in expectedMonsterTerms)
    {
        CheckCondition(
            $"reviewed monster term: {pair.Key}",
            fullTranslations.TryGetValue(pair.Key, out var target) && target == pair.Value);
    }
    var expectedArtifactSetTerms = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Pioneer Artifact Set"] = "先驱神器套装",
        ["Spellweaver Artifact Set"] = "法术织者神器套装",
    };
    foreach (var pair in expectedArtifactSetTerms)
    {
        CheckCondition(
            $"reviewed artifact set term: {pair.Key}",
            fullTranslations.TryGetValue(pair.Key, out var target) && target == pair.Value);
    }
    var expectedRuntimeMarketTerms = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Coal Hard"] = "硬煤",
        ["Arcanum Rune"] = "奥秘符文",
        ["Bloodbind Relic"] = "血契遗物",
        ["Iron Wake Rune"] = "钢铁觉醒符文",
        ["Pirate Legs"] = "海盗长裤",
        ["Pirate Coat"] = "海盗外套",
        ["Pirate Hat"] = "海盗帽",
        ["Pirate Hook"] = "海盗钩",
        ["Pirate Pants"] = "海盗长裤",
        ["Pirate Shoes"] = "海盗鞋",
        ["Pirate Set"] = "海盗套装",
        ["Pirate Set:"] = "海盗套装:",
        ["Pirate 套装:"] = "海盗套装:",
        ["装扮Pet"] = "装扮宠物",
        ["Digger's Flask"] = "掘地者水壶",
        ["Bloodbind Scroll"] = "血契卷轴",
        ["Spellweaver Jewel"] = "法术织者宝石",
        ["Blitzcore Rune"] = "闪击核心符文",
        ["Hound Card"] = "猎犬卡片",
        ["Skeleton Giant Card"] = "骷髅巨人卡片",
        ["Direwolf Legs"] = "暮光猎手护腿",
        ["Gnat Card"] = "小飞虫卡片",
        ["Eternis Relic"] = "埃特尼斯遗物",
        ["Azure Gazer Card"] = "蔚蓝凝视者卡片",
        ["Goblin Grunt Card"] = "哥布林步兵卡片",
        ["Vanilla Ice Card"] = "香草冰卡片",
        ["Matyr Card"] = "殉道者卡片",
    };
    foreach (var pair in expectedRuntimeMarketTerms)
    {
        CheckCondition(
            $"reviewed runtime market term: {pair.Key}",
            fullTranslations.TryGetValue(pair.Key, out var target) && target == pair.Value);
    }
    var expectedRuntimeUiTerms = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["POINTS"] = "点数",
        ["Apply"] = "应用",
        ["Inventory"] = "背包",
        ["Cosmetics"] = "装扮",
        ["Appearance"] = "外观",
        ["Consumables"] = "消耗品",
        ["Equipment"] = "装备",
        ["Cards"] = "卡片",
        ["Artifacts"] = "神器",
        ["Gems"] = "宝石",
        ["Materials"] = "材料",
        ["Warp"] = "传送",
        ["Potions"] = "药水袋",
        ["Potion Pouch"] = "药水袋",
        ["Server"] = "世界",
        ["Select Server"] = "选择服务器",
        ["Gameplay"] = "游戏",
        ["List items for Sale"] = "上架物品",
        ["Enter a name.."] = "输入角色名……",
        ["Name already taken"] = "该名称已被占用",
        ["Body"] = "体型",
        ["Face"] = "脸型",
        ["Body Color"] = "肤色",
        ["Hair Color"] = "发色",
        ["Hair"] = "发型",
        ["Brows"] = "眉型",
        ["Beard"] = "胡须",
        ["Randomise"] = "随机生成",
        ["Create character"] = "创建角色",
        ["Advancements"] = "职业进阶",
        ["Choose Class"] = "选择职业",
        ["A balanced melee fighter trained in sword and shield combat. Durable and dependable, ideal for players who enjoy frontline roles and absorbing damage."] =
            "攻守兼备的近战斗士，精通剑盾作战。坚韧可靠，适合喜欢坚守前线、承受伤害的玩家。",
        ["Respawn in town"] = "在城镇复活",
        ["Craftsman Recipes"] = "工匠配方",
        ["Blacksmith Vendor"] = "铁匠商人",
        ["Label"] = "标签",
        ["Craft"] = "制作",
        ["Interact"] = "互动",
        ["View"] = "查看",
        ["Pickup"] = "拾取",
        ["Waypoint"] = "传送点",
        ["Stance: Two Handed"] = "姿态：双手持握",
        ["You are dead"] = "你已死亡",
        ["[Early Bird]"] = "[早鸟]",
        ["[SpiritValer]"] = "[灵谷勇士]",
        ["Sunny Meadows Sunny Meadows"] = "阳光草甸 阳光草甸",
        ["Sunny Meadows 1"] = "阳光草甸 1",
        ["Forest Field 1"] = "森林原野 1",
        ["Free as part of a promotion at Finkle Winkle Ice Cream Shop. Say you're giving out ice cream. How about you go there too?"] =
            "Finkle Winkle 冰淇淋店促销期间免费赠送。听说他们正在发放冰淇淋，你也去看看吧？",
    };
    foreach (var pair in expectedRuntimeUiTerms)
    {
        CheckCondition(
            $"reviewed runtime UI term: {pair.Key}",
            fullTranslations.TryGetValue(pair.Key, out var target) && target == pair.Value);
    }
    var expectedMmoQualityTerms = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Auto Attack"] = "普通攻击",
        ["Archon"] = "执政官",
        ["Blindfold"] = "蒙眼布",
        ["Breakerhead"] = "碎甲重锤",
        ["Bronze Plugs"] = "青铜耳塞",
        ["Cardweaver"] = "卡牌织法师",
        ["Chronomancer"] = "时空法师",
        ["Codex Umbra"] = "翁布拉法典",
        ["Codex Vitae"] = "维泰法典",
        ["Conjurer"] = "唤灵师",
        ["Cotton Mask"] = "棉布口罩",
        ["Crossed Axes"] = "交叉双斧",
        ["Direwolf"] = "恐狼",
        ["Drooping Burrow"] = "困倦鼹窝",
        ["Echoing Spire"] = "回响尖塔",
        ["Fae Court"] = "妖精王庭",
        ["Flintlock Pistol"] = "燧发手枪",
        ["Forest Friend Hat"] = "森林伙伴帽",
        ["Game Master Utility"] = "GM工具",
        ["Garden"] = "夜之花园",
        ["Grand Archive"] = "大档案馆",
        ["Gravion"] = "格拉维恩",
        ["Happy Chipper Hat"] = "欢欣森林帽",
        ["Hellhorn Hood"] = "地狱角兜帽",
        ["Holy Cape"] = "圣光披风",
        ["Hunting Pike"] = "狩猎长矛",
        ["Iron Ankh"] = "铁制安卡",
        ["Iron Fortitude"] = "钢铁意志",
        ["Jagtooth"] = "锯牙",
        ["Jewelcrest Mace"] = "宝冠钉锤",
        ["Life Drain (Summon)"] = "生命汲取（召唤物）",
        ["Lute"] = "鲁特琴",
        ["Lunaris"] = "露娜里斯",
        ["Magma Golem"] = "岩浆魔像",
        ["Meteoric Staff"] = "陨星法杖",
        ["Onyx Bolt"] = "玛瑙雷矢",
        ["Oni"] = "鬼族",
        ["Ornamented Staff"] = "华饰法杖",
        ["Packborn"] = "狼群之裔",
        ["Parrying Knife"] = "招架匕首",
        ["Piercer"] = "贯穿者",
        ["Piña Colada"] = "椰林飘香",
        ["Pomsky"] = "庞斯基犬",
        ["Reanimation"] = "亡者奴役",
        ["Reap (Summon)"] = "收割（召唤物）",
        ["Red Delicious"] = "红蛇果",
        ["Resurrection"] = "复活术",
        ["Rime Drake"] = "霜冻幼龙",
        ["Rod"] = "魔法短杖",
        ["Shadow Dancers"] = "暗影舞鞋",
        ["Sharkbite Hood"] = "鲨噬兜帽",
        ["Shining Sun Shield"] = "耀阳之盾",
        ["Sky Raider Hat"] = "天际掠夺者帽",
        ["Skycloth"] = "天穹布",
        ["Smite"] = "惩击",
        ["Snowbun Earmuffs"] = "雪兔耳罩",
        ["Solar Pulse"] = "日曜脉冲",
        ["Solar Relic"] = "日曜圣物",
        ["Solar Spear"] = "日曜长矛",
        ["Spelltech"] = "魔导科技",
        ["Spire"] = "尖塔",
        ["Sporeling"] = "孢子精",
        ["Springram Horns"] = "春日羊角",
        ["Starbound Hat"] = "星缚帽",
        ["Stonebound Boots"] = "石缚长靴",
        ["Stormplate Chest"] = "风暴胸甲",
        ["Stormplate Legs"] = "风暴护腿",
        ["Stormplate Shoes"] = "风暴战靴",
        ["Stormreef"] = "风暴礁岛",
        ["Summon Bonekin"] = "召唤骸骨战士",
        ["Suncrest Mace"] = "日冠钉锤",
        ["Terrapin v2000"] = "水龟 v2000",
        ["Tetra Vortex"] = "元素过载",
        ["Thorium"] = "瑟银",
        ["True Sight"] = "真实视野",
        ["Turtle Shell"] = "龟甲",
        ["Umbral Veil"] = "幽影帷幕",
        ["Valiant Crown"] = "英勇王冠",
        ["Ventilator Mask"] = "滤毒面罩",
        ["Vitae"] = "维泰",
        ["Vulkanite"] = "火山岩",
        ["War Banner"] = "战旗",
        ["Warlord Emblem Shield"] = "督军纹章盾",
        ["Waystone"] = "传送石",
        ["Weaver Chest"] = "织法师胸甲",
        ["Weaver Gauntlets"] = "织法师护手",
        ["Weaver Legs"] = "织法师护腿",
        ["Weaver Shoes"] = "织法师鞋",
        ["Wide Bleed"] = "群体流血",
        ["Wind Gem"] = "疾风宝石",
        ["Accurate"] = "精确",
        ["Angel"] = "天使",
        ["Angelic"] = "天使风格",
        ["Angeling"] = "小天使宠物",
        ["Apparition"] = "幽魂",
        ["Autumn"] = "秋日",
        ["Berserk"] = "狂暴",
        ["Black School Uniform Top"] = "黑色校服上装",
        ["Black School Wear"] = "黑色校服上装",
        ["Blue School Uniform Top"] = "蓝色校服上装",
        ["Blue School Wear"] = "蓝色校服上装",
        ["Bumble Bee Beanie"] = "大黄蜂针织帽",
        ["Bumblebee Beanie"] = "大黄蜂针织帽",
        ["Citrus"] = "柑橘",
        ["Cosmetic Converter"] = "装扮转换器",
        ["Creepy Crawly Pet"] = "诡异爬虫宠物",
        ["Critical Strikes"] = "暴击强化",
        ["Cyclopling"] = "独眼幼体",
        ["Decay"] = "腐朽",
        ["Decay Aura"] = "腐朽光环",
        ["Decay Immunity"] = "腐朽免疫",
        ["Defiance"] = "抗御",
        ["Defiance Aura"] = "抗御光环",
        ["Flameguard"] = "护焰",
        ["Fleet"] = "轻捷",
        ["Frenzy"] = "狂乱",
        ["Guild Charter"] = "公会章程",
        ["Gunslinger Pants"] = "枪手长裤",
        ["Mangrove Piece"] = "红树木片",
        ["Nexus Robot"] = "中枢机器人",
        ["NPC- Sharpen"] = "暴击强化",
        ["Pink School Uniform Top"] = "粉色校服上装",
        ["Pink School Wear"] = "粉色校服上装",
        ["Poisonous"] = "有毒",
        ["Precision"] = "精准",
        ["Pure"] = "纯粹",
        ["Purity"] = "纯净",
        ["Rapid"] = "疾速",
        ["Regent's Stormhood"] = "摄政王风暴兜帽",
        ["Rusted Binocs"] = "锈蚀望远镜",
        ["Rusted Binoculars"] = "锈蚀望远镜",
        ["Sharpen"] = "暴击强化",
        ["Soldier Termite"] = "白蚁士兵",
        ["Spook"] = "游魂",
        ["Spook Card"] = "游魂卡片",
        ["Stability"] = "稳固",
        ["Sukeban Female Pants"] = "不良风女式长裤",
        ["Sukeban Female Shoes"] = "不良风女式鞋",
        ["Sukeban Female Top"] = "不良风女式上装",
        ["Sukeban Male Pants"] = "不良风男式长裤",
        ["Sukeban Male Shoes"] = "不良风男式鞋",
        ["Sukeban Male Top"] = "不良风男式上装",
        ["Sukeban Shoes"] = "不良风男式鞋",
        ["Summon Speed"] = "召唤物加速",
        ["Sunborn Petal Tuft"] = "日生花瓣簇",
        ["Swift"] = "迅捷",
        ["Tea Container"] = "茶叶罐",
        ["Termite Soldier"] = "白蚁士兵",
        ["Tomahawk"] = "投掷斧",
        ["Toxic"] = "剧毒",
        ["Umbral Fragment"] = "幽影碎片",
        ["Unyielding"] = "不屈",
        ["War Axe"] = "战斧",
        ["Yellow School Uniform Top"] = "黄色校服上装",
        ["Yellow School Wear"] = "黄色校服上装",
        ["Advancements"] = "职业进阶",
        ["Gameplay"] = "游戏",
        ["Interact"] = "互动",
        ["List items for Sale"] = "上架物品",
        ["Potions"] = "药水袋",
        ["Potion Pouch"] = "药水袋",
        ["Server"] = "世界",
        ["Select Server"] = "选择服务器",
        ["Spear Quicken"] = "精准专注",
        ["Windy Desert"] = "风蚀沙漠",
        ["Windy Desert North"] = "风蚀沙漠北部",
        ["Windy Desert South"] = "风蚀沙漠南部",
        ["Nevaris"] = "内瓦里斯",
        ["Nevaris Sewers"] = "内瓦里斯下水道",
        ["Underground Cavern"] = "地下洞窟",
        ["Welcome to Nevaris. The last bastion of hope."] =
            "欢迎来到内瓦里斯。这里是希望的最后堡垒。",
        ["Nevaris welcomes all adventurers. The wilderness has not agreed to this policy."] =
            "内瓦里斯欢迎所有冒险家。荒野还没有同意这一政策。",
        ["Take this Waystone. It is attuned to Nevaris, and will carry you back here when you need to return."] =
            "拿好这块传送石。它已与内瓦里斯同调，需要返回时会将你送回这里。",
        ["Nevaris is bound to your Waystone now. Let it be your anchor."] =
            "内瓦里斯现已与你的传送石绑定。让它成为你的锚点。",
        ["whitesteel"] = "白钢",
    };
    foreach (var pair in expectedMmoQualityTerms)
    {
        CheckCondition(
            $"reviewed MMO-quality term: {pair.Key}",
            fullTranslations.TryGetValue(pair.Key, out var target) && target == pair.Value);
    }
    CheckCondition(
        "Windy Desert directional variants derive from the reviewed base name",
        fullTranslations.TryGetValue("Windy Desert", out var windyDesertTarget) &&
        fullTranslations.TryGetValue("Windy Desert North", out var windyDesertNorthTarget) &&
        fullTranslations.TryGetValue("Windy Desert South", out var windyDesertSouthTarget) &&
        windyDesertNorthTarget == windyDesertTarget + "北部" &&
        windyDesertSouthTarget == windyDesertTarget + "南部");
    CheckCondition(
        "Nevaris region variant derives from the reviewed base name",
        fullTranslations.TryGetValue("Nevaris", out var nevarisTarget) &&
        fullTranslations.TryGetValue("Nevaris Sewers", out var nevarisSewersTarget) &&
        nevarisSewersTarget == nevarisTarget + "下水道");
    CheckCondition(
        "Underground Cavern singular map name remains reviewed",
        fullTranslations.TryGetValue("Underground Cavern", out var undergroundCavernTarget) &&
        undergroundCavernTarget == "地下洞窟");
    var expectedNevarisNpcTargets = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Welcome to Nevaris. The last bastion of hope."] =
            "欢迎来到内瓦里斯。这里是希望的最后堡垒。",
        ["Nevaris welcomes all adventurers. The wilderness has not agreed to this policy."] =
            "内瓦里斯欢迎所有冒险家。荒野还没有同意这一政策。",
        ["Take this Waystone. It is attuned to Nevaris, and will carry you back here when you need to return."] =
            "拿好这块传送石。它已与内瓦里斯同调，需要返回时会将你送回这里。",
        ["Nevaris is bound to your Waystone now. Let it be your anchor."] =
            "内瓦里斯现已与你的传送石绑定。让它成为你的锚点。",
    };
    foreach (var pair in expectedNevarisNpcTargets)
    {
        CheckWithTranslator(
            $"fixed Nevaris NPC sentence: {pair.Key}",
            fullTranslator,
            pair.Key,
            "Text_Description",
            pair.Value);
    }
    var expectedDescriptionUiAuditTerms = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Ready a dedicated heavy weapon and swap to it mid-battle to unleash overwhelming firepower. Unlocks Gatling Guns and Launchers"] =
            "准备专用重武器，并可在战斗中切换至该武器，以释放压倒性火力。解锁加特林机枪与爆破发射器。",
        ["A shimmering shield diverts part of incoming harm into mana."] =
            "闪耀护盾会将部分所受伤害转移至法力。",
        ["Spread lingering umbral decay beneath enemies, damaging them and marking them with shadow."] =
            "在敌人脚下散布持续存在的幽影腐化，造成伤害并施加暗影标记。",
        ["Increases the damage dealt by your auto attacks."] = "提高普通攻击造成的伤害。",
        ["Increases maximum HP, Vitality, and Healing Received."] =
            "提高最大生命值、活力与受到的治疗效果。",
        ["Card"] = "卡片",
        ["Earthen protection hardens the user and can answer incoming blows with Stun."] =
            "大地防护会强化使用者；受到攻击时，有概率使攻击者眩晕。",
        ["Flame shields the user and can answer incoming blows with Burning."] =
            "火焰保护使用者；受到攻击时，有概率灼烧攻击者。",
        ["Water shields the user and can answer incoming blows with Frozen."] =
            "水流保护使用者；受到攻击时，有概率冻结攻击者。",
        ["Wind shields the user and can answer incoming blows with Chain Lightning."] =
            "疾风保护使用者；受到攻击时，有概率对攻击者施放连锁闪电。",
        ["A gem infused with ancient essence. When embedded into an Artifact, it awakens hidden strength within its bearer."] =
            "一颗注入古老精华的宝石。嵌入神器后，会唤醒持有者体内潜藏的力量。",
        ["Greatly increases all stats, Move Speed, and damage reduction. Grants Game Master Cloaking and Resurrection."] =
            "大幅提高所有属性、移动速度与伤害减免。获得管理员隐匿与复活术。",
        ["Violence and fallen enemies return life and mana to the user."] =
            "伤害敌人或将其击败时，会为使用者恢复生命与法力。",
    };
    foreach (var pair in expectedDescriptionUiAuditTerms)
    {
        CheckCondition(
            $"reviewed description/UI audit term: {pair.Key}",
            fullTranslations.TryGetValue(pair.Key, out var target) && target == pair.Value);
    }
    CheckWithTranslator(
        "live map monster row: Bee",
        fullTranslator,
        "Lv6 Bee",
        "Description",
        "等级6 蜜蜂");
    CheckWithTranslator(
        "live map monster row: Fire Bunny",
        fullTranslator,
        "Lv10 Fire Bunny",
        "Description",
        "等级10 火焰兔");
    CheckWithTranslator(
        "live map monster row: Hermit King boss",
        fullTranslator,
        "Lv45 Hermit King [Boss]",
        "Description",
        "等级45 隐士王 [首领]");
    CheckWithTranslator(
        "runtime upgraded item",
        fullTranslator,
        "+6 Crown of Spikes",
        "Name",
        "+6 尖刺王冠");
    var liveFullMarketItemContext = TmpTextContextResolver.Resolve(
        "Name",
        new[] { "UIVendingSearchItem", "VendingPanel" });
    CheckWithTranslator(
        "live vending exact item name",
        fullTranslator,
        "Wooden Guard",
        liveFullMarketItemContext,
        "木制护盾");
    CheckWithTranslator(
        "live character class label",
        fullTranslator,
        "Acolyte",
        TmpTextContextResolver.Resolve("Name", new[] { "Character" }),
        "侍祭");
    CheckWithTranslator(
        "class-shaped player display name stays unchanged",
        fullTranslator,
        "Mage",
        "Display Name",
        "Mage",
        expectedChange: false);
    CheckWithTranslator(
        "dictionary-shaped vending shop name stays unchanged",
        fullTranslator,
        "Guild Charter",
        "Vending",
        "Guild Charter",
        expectedChange: false);
    CheckWithTranslator(
        "runtime upgraded combo item",
        fullTranslator,
        "+6 Combo Royal Dagger",
        "ItemName:MarketListing",
        "+6 连击 皇家匕首");
    CheckWithTranslator(
        "runtime market windproof crest composite",
        fullTranslator,
        "+6 Windproof Sun Lion Crest",
        "ItemName:MarketListing",
        "+6 风抗 太阳狮冠");
    CheckWithTranslator(
        "runtime market savage pendant composite",
        fullTranslator,
        "+6 Savage Skull Pendant",
        "ItemName:MarketListing",
        "+6 凶猛 颅骨吊坠");
    CheckWithTranslator(
        "runtime market purifying sheath composite",
        fullTranslator,
        "+6 Purifying Dualblade Sheath",
        "ItemName:MarketListing",
        "+6 净化 双刃护套");
    CheckWithTranslator(
        "runtime market assassin beads composite without upgrade",
        fullTranslator,
        "Assassin's Archer's Beads",
        "ItemName:MarketListing",
        "刺客之 弓手珠饰");
    CheckWithTranslator(
        "runtime market multiple reviewed affixes",
        fullTranslator,
        "+6 Assassin's  Combo Skullhacker",
        "ItemName:MarketListing",
        "+6 刺客之  连击 裂颅者");
    CheckWithTranslator(
        "runtime generic plain Name composite stays protected",
        fullTranslator,
        "+6 Savage Skull Pendant",
        "Name",
        "+6 Savage Skull Pendant",
        expectedChange: false);
    CheckWithTranslator(
        "runtime unknown affix composite stays unchanged",
        fullTranslator,
        "+6 Unknown Skull Pendant",
        "ItemName:MarketListing",
        "+6 Unknown Skull Pendant",
        expectedChange: false);
    CheckWithTranslator(
        "runtime unknown base composite stays unchanged",
        fullTranslator,
        "+6 Savage Unknown Pendant",
        "ItemName:MarketListing",
        "+6 Savage Unknown Pendant",
        expectedChange: false);
    CheckWithTranslator(
        "runtime seller name composite stays unchanged",
        fullTranslator,
        "+6 Savage Skull Pendant",
        "SellerName",
        "+6 Savage Skull Pendant",
        expectedChange: false);
    CheckWithTranslator(
        "runtime ancient essence artifact gem mixed description",
        fullTranslator,
        "A gem infused with ancient essence. When embedded into an Artifact, it awakens hidden strength within its bearer.\n[唯一] 仅一个副本生效。\n\n死亡缠绕伤害: +2% 每次精炼",
        "Description",
        "一颗注入古老精华的宝石。嵌入神器后，会唤醒持有者体内潜藏的力量。\n[唯一] 仅一个副本生效。\n\n死亡缠绕伤害: +2% 每次精炼");
    CheckWithTranslator(
        "runtime map label",
        fullTranslator,
        "Mystic Lake 2\nLv36-40",
        "Mystic Lake 2",
        "秘境湖 2\n等级36-40");
    var asciiWord = new Regex(
        @"(?<![A-Za-z])[A-Za-z]{3,}(?![A-Za-z])",
        RegexOptions.CultureInvariant);
    var gemDescriptions = 0;
    var mixedSkillDescriptions = 0;
    var mixedDescriptions = 0;
    var fullyLocalizedDescriptions = 0;
    var forbiddenGameplayResiduals = 0;
    var gemNameConsistencyChecks = 0;
    var artifactCompositeChecks = 0;
    var mixedDescriptionResiduals = new List<string>();
    var gemNameExceptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Anchor Gem"] = "锚定宝石",
        ["Channel Gem"] = "引导宝石",
        ["Spike Gem"] = "尖刺宝石",
        ["Wind Gem"] = "疾风宝石",
    };
    var forbiddenGameplayToken = new Regex(
        @"(?<![A-Za-z])(?:seconds?|mana|cooldown|damage|healing|armor|health|power|speed|chance|level|ATK|MATK|DEF|MDEF|PDEF|HP|MP|lv)(?![A-Za-z])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    var artifactDescriptionCandidatePath = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(snapshotPath)) ?? string.Empty,
        "artifact-description-candidates.json");
    if (!File.Exists(artifactDescriptionCandidatePath))
    {
        failures++;
        Console.Error.WriteLine(
            $"artifact description candidate file is missing: {artifactDescriptionCandidatePath}");
        return string.Empty;
    }
    var artifactDescriptionCandidates = new Dictionary<string, string>(StringComparer.Ordinal);
    var artifactDescriptionDuplicateSources = 0;
    using (var candidateDocument = JsonDocument.Parse(File.ReadAllText(artifactDescriptionCandidatePath)))
    {
        if (candidateDocument.RootElement.ValueKind != JsonValueKind.Object)
        {
            failures++;
            Console.Error.WriteLine("artifact description candidate root must be a JSON object");
            return string.Empty;
        }
        foreach (var property in candidateDocument.RootElement.EnumerateObject())
        {
            var target = property.Value.GetString() ?? string.Empty;
            if (!artifactDescriptionCandidates.TryAdd(property.Name, target))
            {
                artifactDescriptionDuplicateSources++;
            }
        }
    }
    CheckCondition("180 unique artifact narrative candidates", artifactDescriptionCandidates.Count == 180);
    CheckCondition("no duplicate artifact narrative sources", artifactDescriptionDuplicateSources == 0);
    var corpusReportRows = new List<string>
    {
        "key\tcategory\tsource\tbuilt-in-simplified\truntime-target",
    };
    var corpusEntryCount = 0;
    var artifactNarrativeRuntimeProjectionChecks = 0;
    var nevarisNpcRuntimeProjectionChecks = 0;
    using var document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
    var snapshotEntries = document.RootElement.GetProperty("entries").EnumerateArray().ToArray();
    var artifactDescriptionIds = new[]
    {
        "Acolyte", "Atk", "Auto", "Bastion", "Berserker_1", "Cast", "Corporeal", "Cost",
        "Crit", "Def", "Eternis", "Flee", "Gunslinger_1", "Healing", "Hexbrand", "Hit",
        "Hp", "Immune", "Knight", "Leech", "Mage", "Magic", "Matk", "Mdef", "Melee",
        "Movespeed", "Mp", "Necromancer_1", "Novice", "Oathbound", "Paladin_1", "Priest_1",
        "Primordial", "Ranged", "Rogue", "Scout", "Shinobi_1", "Summoner", "Vampiric",
        "Warrior", "Weaver_1", "Wizard_1", "Wizard_2", "Wizard_3", "Wizard_4",
    };
    var expectedArtifactDescriptionSources = new HashSet<string>(StringComparer.Ordinal);
    var artifactDescriptionSourceToKey = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var artifactId in artifactDescriptionIds)
    {
        var coveredSegments = 0;
        for (var segment = 1; segment <= 4; segment++)
        {
            var expectedKey = $"artifact.{artifactId}.description_{segment}";
            var matches = snapshotEntries
                .Where(entry => (entry.GetProperty("key").GetString() ?? string.Empty) == expectedKey)
                .ToArray();
            CheckCondition($"single artifact narrative snapshot key: {expectedKey}", matches.Length == 1);
            if (matches.Length != 1)
            {
                continue;
            }
            var expectedSource = matches[0].GetProperty("source").GetString() ?? string.Empty;
            CheckCondition(
                $"artifact narrative candidate covers: {expectedKey}",
                artifactDescriptionCandidates.ContainsKey(expectedSource));
            if (artifactDescriptionCandidates.ContainsKey(expectedSource))
            {
                coveredSegments++;
            }
            CheckCondition(
                $"artifact narrative source is unique: {expectedKey}",
                expectedArtifactDescriptionSources.Add(expectedSource));
            artifactDescriptionSourceToKey[expectedSource] = expectedKey;
        }
        CheckCondition($"artifact narrative set 4/4: {artifactId}", coveredSegments == 4);
    }
    CheckCondition("45 artifact narrative sets", artifactDescriptionIds.Length == 45);
    CheckCondition("180 artifact narrative snapshot sources", expectedArtifactDescriptionSources.Count == 180);
    CheckCondition(
        "artifact narrative candidate source set is exact",
        expectedArtifactDescriptionSources.SetEquals(artifactDescriptionCandidates.Keys));
    var artifactCodexUmbra = 0;
    var artifactCodexVitae = 0;
    var artifactUnsignedSegments = 0;
    foreach (var pair in artifactDescriptionCandidates)
    {
        var source = pair.Key;
        var target = pair.Value;
        var key = artifactDescriptionSourceToKey.TryGetValue(source, out var mappedKey)
            ? mappedKey
            : "<unmapped>";
        CheckCondition($"artifact narrative target is non-empty: {key}", !string.IsNullOrWhiteSpace(target));
        CheckCondition($"artifact narrative target has no ASCII prose: {key}", !asciiWord.IsMatch(target));
        CheckCondition(
            $"artifact narrative target has terminal punctuation: {key}",
            target.EndsWith("。", StringComparison.Ordinal) ||
            target.EndsWith("——翁布拉法典", StringComparison.Ordinal) ||
            target.EndsWith("——维泰法典", StringComparison.Ordinal));
        CheckCondition(
            $"artifact narrative format signature is preserved: {key}",
            GetRegexSignature(source, @"(?<![A-Za-z])[-+]?\d+(?:[.,]\d+)*(?:%|[xX])?", true) ==
                GetRegexSignature(target, @"(?<![A-Za-z])[-+]?\d+(?:[.,]\d+)*(?:%|[xX])?", true) &&
            GetRegexSignature(source, @"\{\d+(?::[^{}]+)?\}", true) ==
                GetRegexSignature(target, @"\{\d+(?::[^{}]+)?\}", true) &&
            GetRegexSignature(source, @"</?[A-Za-z][^>]*>", false) ==
                GetRegexSignature(target, @"</?[A-Za-z][^>]*>", false) &&
            source.Count(character => character == '\n') == target.Count(character => character == '\n'));
        var codexSignatureMatches = false;
        if (source.EndsWith(" - Codex Umbra", StringComparison.Ordinal))
        {
            artifactCodexUmbra++;
            codexSignatureMatches = target.EndsWith("——翁布拉法典", StringComparison.Ordinal);
        }
        else if (source.EndsWith(" - Codex Vitae", StringComparison.Ordinal))
        {
            artifactCodexVitae++;
            codexSignatureMatches = target.EndsWith("——维泰法典", StringComparison.Ordinal);
        }
        else
        {
            artifactUnsignedSegments++;
            codexSignatureMatches =
                !target.EndsWith("——翁布拉法典", StringComparison.Ordinal) &&
                !target.EndsWith("——维泰法典", StringComparison.Ordinal);
        }
        if (key.EndsWith(".description_4", StringComparison.Ordinal))
        {
            codexSignatureMatches &=
                source.EndsWith(" - Codex Umbra", StringComparison.Ordinal) ||
                source.EndsWith(" - Codex Vitae", StringComparison.Ordinal);
        }
        CheckCondition($"artifact narrative Codex signature matches: {key}", codexSignatureMatches);
        CheckCondition(
            $"artifact narrative exact dictionary target: {key}",
            fullTranslations.TryGetValue(source, out var dictionaryTarget) && dictionaryTarget == target);
    }
    CheckCondition("22 Codex Umbra artifact signatures", artifactCodexUmbra == 22);
    CheckCondition("23 Codex Vitae artifact signatures", artifactCodexVitae == 23);
    CheckCondition("135 unsigned artifact narrative segments", artifactUnsignedSegments == 135);
    foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
    {
        var key = entry.GetProperty("key").GetString() ?? string.Empty;
        var category = entry.GetProperty("category").GetString() ?? string.Empty;
        var source = entry.GetProperty("source").GetString() ?? string.Empty;
        var simplified = entry.GetProperty("simplified").GetString() ?? string.Empty;
        var projectionSource = fullTranslations.ContainsKey(source)
            ? source
            : (string.IsNullOrEmpty(simplified) ? source : simplified);
        fullTranslator.TryTranslate(projectionSource, "Text_Description", out var runtimeTarget);
        if (artifactDescriptionCandidates.TryGetValue(source, out var expectedArtifactNarrativeTarget))
        {
            artifactNarrativeRuntimeProjectionChecks++;
            CheckCondition(
                $"artifact narrative runtime projection: {key}",
                runtimeTarget == expectedArtifactNarrativeTarget);
        }
        if (expectedNevarisNpcTargets.TryGetValue(source, out var expectedNevarisNpcTarget))
        {
            nevarisNpcRuntimeProjectionChecks++;
            CheckCondition(
                $"fixed Nevaris NPC corpus projection: {key}",
                runtimeTarget == expectedNevarisNpcTarget);
        }
        corpusReportRows.Add(string.Join(
            "\t",
            EscapeTsv(key),
            EscapeTsv(category),
            EscapeTsv(source),
            EscapeTsv(simplified),
            EscapeTsv(runtimeTarget)));
        corpusEntryCount++;
        if (category == "Gems" && key.EndsWith(".name", StringComparison.Ordinal) &&
            source.EndsWith(" Gem", StringComparison.Ordinal))
        {
            var baseSource = source.Substring(0, source.Length - " Gem".Length);
            var hasExpected = gemNameExceptions.TryGetValue(source, out var expectedGem);
            if (!hasExpected && fullTranslations.TryGetValue(baseSource, out var baseTarget))
            {
                expectedGem = baseTarget + "宝石";
                hasExpected = true;
            }
            CheckCondition(
                $"consistent gem name: {source}",
                hasExpected && fullTranslations.TryGetValue(source, out var gemTarget) &&
                gemTarget == expectedGem);
            gemNameConsistencyChecks++;
        }
        if (category == "Artifacts" && key.EndsWith(".name", StringComparison.Ordinal) &&
            fullTranslations.TryGetValue(source, out var artifactTarget))
        {
            foreach (var suffix in new[]
            {
                (" Rune", "符文"),
                (" Relic", "遗物"),
                (" Scroll", "卷轴"),
                (" Jewel", "宝石"),
                (" Artifact Set", "神器套装"),
            })
            {
                CheckCondition(
                    $"consistent artifact component: {source}{suffix.Item1}",
                    fullTranslations.TryGetValue(source + suffix.Item1, out var componentTarget) &&
                    componentTarget == artifactTarget + suffix.Item2);
                artifactCompositeChecks++;
            }
        }
        if (asciiWord.IsMatch(simplified))
        {
            mixedDescriptions++;
            fullTranslator.TryTranslate(simplified, "Text_Description", out var cleaned);
            if (!asciiWord.IsMatch(cleaned))
            {
                fullyLocalizedDescriptions++;
            }
            else
            {
                mixedDescriptionResiduals.Add(
                    $"{key}\t{category}\t" + cleaned
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n")
                        .Replace("\t", "\\t"));
            }
            if (forbiddenGameplayToken.IsMatch(cleaned))
            {
                forbiddenGameplayResiduals++;
                Console.Error.WriteLine($"forbidden gameplay token remains in {key}: {cleaned}");
            }
        }
        if (category == "Gems" && asciiWord.IsMatch(simplified))
        {
            gemDescriptions++;
            CheckCorpusEntry(entry, simplified, fullTranslator, asciiWord);
        }
        if ((category == "Skills" || category == "SkillPassives") &&
            asciiWord.IsMatch(simplified))
        {
            mixedSkillDescriptions++;
            CheckCorpusEntry(entry, simplified, fullTranslator, asciiWord);
        }
    }

    CheckCondition("103 mixed gem description corpus entries", gemDescriptions == 103);
    CheckCondition("22 mixed skill description corpus entries", mixedSkillDescriptions == 22);
    CheckCondition("131 consistent gem name records", gemNameConsistencyChecks == 131);
    CheckCondition("225 consistent artifact component names", artifactCompositeChecks == 225);
    CheckCondition(
        "180 artifact narrative runtime projections",
        artifactNarrativeRuntimeProjectionChecks == 180);
    CheckCondition(
        "4 fixed Nevaris NPC runtime projections",
        nevarisNpcRuntimeProjectionChecks == 4);
    CheckCondition("no forbidden gameplay tokens in mixed descriptions", forbiddenGameplayResiduals == 0);
    CheckCondition(
        "localized corpus covers every snapshot record",
        corpusEntryCount == document.RootElement.GetProperty("entries").GetArrayLength());
    var corpusReportDirectory = Path.GetDirectoryName(Path.GetFullPath(corpusReportPath));
    if (!string.IsNullOrEmpty(corpusReportDirectory))
    {
        Directory.CreateDirectory(corpusReportDirectory);
    }
    File.WriteAllLines(
        corpusReportPath,
        corpusReportRows,
        new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    CheckCondition(
        "localized corpus report row count",
        File.ReadLines(corpusReportPath).Count() == corpusEntryCount + 1);
    if (values.TryGetValue("--residual-report", out var residualReportPath))
    {
        var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(residualReportPath));
        if (!string.IsNullOrEmpty(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
        }
        File.WriteAllLines(
            residualReportPath,
            new[] { "key\tcategory\ttranslated" }
                .Concat(mixedDescriptionResiduals.OrderBy(value => value, StringComparer.Ordinal)));
    }
    return $"; corpus: {runtimeSkillAliases} runtime skill aliases, {gemDescriptions} gem descriptions, {mixedSkillDescriptions} skill descriptions, " +
        $"{gemNameConsistencyChecks} gem names, {artifactCompositeChecks} artifact components, " +
        $"{fullyLocalizedDescriptions}/{mixedDescriptions} mixed descriptions fully localized";
}

string GetRegexSignature(string value, string pattern, bool sortValues)
{
    var matches = Regex.Matches(value ?? string.Empty, pattern)
        .Cast<Match>()
        .Select(match => match.Value);
    if (sortValues)
    {
        matches = matches.OrderBy(match => match, StringComparer.Ordinal);
    }
    return string.Join("\u001F", matches);
}

string EscapeTsv(string value)
{
    return (value ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t");
}

void CheckCorpusEntry(
    JsonElement entry,
    string source,
    RuntimeTextTranslator fullTranslator,
    Regex asciiWord)
{
    var changed = fullTranslator.TryTranslate(source, "Text_Description", out var actual);
    if (changed && !asciiWord.IsMatch(actual))
    {
        return;
    }

    failures++;
    var key = entry.GetProperty("key").GetString() ?? "<unknown>";
    Console.Error.WriteLine(
        $"corpus description failed: {key}\n  changed: {changed}\n  actual: {actual}");
}
