# Live Verification Checklist

Use UI automation or direct observation. Capture screenshots for failures and newly reviewed surfaces.

## Cold Starts

- Start from Steam and wait for the server list. Confirm the process remains responsive.
- Exit normally and repeat once. A second clean start catches stale interop/cache and first-run-only failures.
- Confirm `BepInEx/LogOutput.log` contains the expected plugin version, translation count, and patch count.
- Confirm the latest session in `BepInEx/plugins/SpiritVale.RuntimeLocalization/untranslated-runtime.log` has no cast-announcement or gameplay-description residuals reported by the loop.
- Reject crashes, black screens, unhandled exceptions, Harmony patch failures, or active XUnity loads.

## Required Surfaces

- Server list: region, status, population, ping, units, buttons.
- Character screen: class, level, location, playtime, deaths, create/delete controls.
- Main HUD: health/mana, level/experience, quests, FPS/ping/player count, countdowns.
- Inventory and equipment: every slot, item name, stat, tooltip, refine text, context menu.
- Gem tooltips: open at least one class-skill gem and confirm both the embedded class and skill name are Chinese with natural spacing.
- Skills: names, descriptions, costs, cooldowns, requirements, passive/active labels; trigger skills in combat and confirm their world-space cast names use the audited runtime display aliases.
- Monsters: names, ranks, combat messages, drops, cards, familiar/pet references.
- Combat overlays: hover a monster and confirm its name, element, level, health, damage numbers, and `Can't loot yet` prompt render as Chinese rather than square missing-glyph boxes.
- Market: tabs, filters, prices, buy/sell dialogs, sale history, dynamic buyer names.
- Party and social: invitations, member summary, guild/friend UI; verify player names remain unchanged.
- Map, quests, NPC dialogs, settings, confirmation dialogs, death/respawn and disconnect screens.

## Optional Entity Display Modes

- Start with no display config or explicit `EntityNameMode = Chinese` and `CompactSurfaceMode = Chinese`. Confirm behavior matches the prior pure-Chinese release and the log reports both modes as Chinese.
- Restart with `EntityNameMode = Bilingual`. Check item/equipment, artifact, gem, skill/passive, and wide map detail titles. Require Chinese on the first line and the exact catalog English on the second; descriptions and compact lists remain Chinese.
- Restart with `CompactSurfaceMode = EnglishToggle` and `TemporaryEnglishKey = Tab`. On a map label, skill button, inventory tile, and monster nameplate, press Tab once and confirm only the trusted entity label becomes English. Release the key and confirm the English label remains, then press Tab once more and confirm Chinese returns.
- For backward compatibility, restart with the legacy `CompactSurfaceMode = EnglishOnHold` value and confirm it has the same per-press toggle behavior as `EnglishToggle`; it must not require holding the key.
- While compact labels are toggled to English, type Chinese and English market searches and inspect player, shop, guild, party, chat, and ranking names. Inputs and player-created text must remain byte-for-byte unchanged, and market results must match the existing query bridge behavior.
- Change a mode to an unknown value or temporarily remove the entity catalog, restart, and confirm the feature fails closed to Chinese without blocking game startup. Restore the candidate files before recording live verification.
- Repeat the relevant title and compact-label checks at `1280x720` and `1920x1080`; reject clipping, overlap, stale pooled labels, blank text, a label that changes on key release, or a label that fails to return to Chinese after the second press, closing a panel, changing scene, reconnecting, or dying.

## Acceptance

- No visible mojibake, placeholder loss, clipped critical text, or unexplained English on tested surfaces.
- No square/tofu glyphs in small world-space fonts. Confirm the log records a successful TMP CJK fallback when a combat font needs one.
- Dynamic numbers and rich-text colors/icons remain intact.
- `artifacts/runtime-skill-aliases.tsv` reports `covered` for all 278 active skill IDs, including display names that differ from the canonical localization source.
- `artifacts/bilingual-entity-catalog.audit.json` reports fresh, complete coverage and its catalog SHA-256 matches both the deployed file and the live record.
- Player-created text remains byte-for-byte unchanged apart from surrounding fixed UI templates.
- Record any inaccessible content as unverified rather than calling the localization complete.
