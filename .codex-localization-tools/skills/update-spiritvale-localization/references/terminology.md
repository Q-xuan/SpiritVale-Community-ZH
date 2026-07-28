# MMO Translation Standard

Aim for concise Simplified Chinese that reads like an established Chinese MMO client. Prefer conventional combat terms over literal machine translation. Keep one concept, one term across skills, items, monsters, tooltips, and settings.

## Style

- Skill and item names: short noun or action phrases, usually 2-6 Chinese characters.
- Stats: use familiar labels such as `生命`, `法力`, `物理防御`, `魔法防御`, `暴击率`, `冷却时间`.
- Actions: use direct verbs such as `装备`, `卸下`, `分解`, `精炼`, `邀请`, `离开队伍`.
- Tooltips: retain mechanical precision. Do not add lore or effects absent from the source.
- Monster names: translate species and role consistently; preserve intentional proper names when uncertain.
- UI: prefer `确认`, `取消`, `返回`, `退出游戏`, `服务器`, `角色`, `队伍`, `公会`, `市场`.

## Reviewed Terms

| English | Simplified Chinese |
| --- | --- |
| Spear Quicken | 精准专注 |
| Ransack | 劫掠 |
| Aerial Shot | 空中射击 |
| Might | 威能 |
| Spiked Club | 狼牙棒 |
| Blade Standard | 剑刃战旗 |
| Shardling Familiar | 碎晶魔宠 |
| Death Coil | 死亡缠绕 |
| Witch's Whisk | 女巫扫帚 |
| Chest (equipment slot) | 胸甲 |
| Mana | 法力 |
| Mdef | 魔法防御 |
| per refine | 每次精炼 |
| Auto Attack | 普通攻击 |
| Cure | 净化 |
| Smite | 惩击 |
| True Sight | 真实视野 |
| Wisp | 精魂 |
| Golem | 魔像 |
| Drake | 幼龙 |
| Terrapin | 水龟 |
| Umbral | 幽影 |
| Shadow | 暗影 |
| Thorium | 瑟银 |
| Waystone | 传送石 |
| Conjurer | 唤灵师 |
| Flintlock Pistol | 燧发手枪 |
| Hunting Pike | 狩猎长矛 |
| Lute | 鲁特琴 |
| Umbral Veil | 幽影帷幕 |

## Naming Rules

- Use `技能名 + 宝石` for skill gems. A gem must reuse the exact reviewed skill term; only context collisions such as `Channel Gem` may have an explicit exception.
- Use one artifact base name across its `符文`, `遗物`, `卷轴`, `宝石`, and `神器套装` forms.
- Distinguish mechanics that the English client distinguishes: `Reanimation` is `亡者奴役`, while `Resurrection` is `复活术`; `Anathema` is `谴咒`, while `Curse` remains `诅咒`.
- Translate creature families consistently: elemental wisps are `精魂`, golems are `魔像`, drakes are `幼龙`, and terrapins are `水龟`.
- Keep `Umbral` as `幽影` and `Shadow` as `暗影` so related factions and effects remain recognizable at a glance.
- Prefer established MMO material names where the setting is clearly fantastical, such as `Thorium -> 瑟银`, instead of mixing a scientific label with a fantasy item family.
- Avoid word-by-word output that changes the object type. Examples: `Crossed Axes -> 交叉双斧`, not `交叉轴`; `Holy Cape -> 圣光披风`, not `圣角`.
- Reject software and industrial false friends when the source is equipment: `Flintlock Pistol -> 燧发手枪`, not `燧发枪手枪`; `Breakerhead -> 碎甲重锤`, not `断路器`; `Piercer -> 贯穿者`, not `穿孔器`.
- Keep class equipment on the reviewed class name. For example, all `Weaver` armor uses `织法师`, and skill variants keep the base skill term before `（敌方）` or `（召唤物）`.

Editorial terms that supersede older generated or screenshot-level wording belong in `mmo-quality-overrides.json`. Exact residual strings belong in `missing-zh-reviewed-source-overrides.json`.

Before changing an established term, search the reviewed override files and all generated targets. Update every semantic occurrence or document why contexts differ.
