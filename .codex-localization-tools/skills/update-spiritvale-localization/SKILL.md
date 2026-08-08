---
name: update-spiritvale-localization
description: Maintain, rebuild, deploy, validate, version, and package the Simplified Chinese localization for the Unity IL2CPP game SpiritVale. Use after a SpiritVale/Steam update, when screenshots reveal untranslated or poor text, when runtime translations stop applying, when BepInEx interop changes, or when preparing a changelog-backed Chinese patch release and installer.
---

# Update SpiritVale Localization

On Windows PowerShell 5, read this skill and UTF-8 localization sources with `Get-Content -Encoding UTF8`; the default legacy code page will display valid Chinese text as mojibake.

Keep the PowerShell control loop ASCII-only while it remains UTF-8 without a BOM. Build any non-ASCII comparison string from Unicode code points, or deliberately add and verify a UTF-8 BOM; otherwise Windows PowerShell 5 can silently corrupt literals and create false residual blockers.

Use the bundled loop as the deterministic control plane. Keep translation review and live UI inspection as agent decisions.

Read [agent-loop.md](references/agent-loop.md) before delegating work to Luna or Terra. It defines ownership, edit targets, handoff artifacts, update migration, incident triage, and the release state machine. Keep one writer for each authoritative JSON file; parallel agents may audit, but only the assigned integrator may merge candidates.

## Non-Negotiable Safety

- Verify Steam App ID `3767850`; stop if the root or manifest does not match.
- Never run legacy asset mutation scripts: `apply_chinese_ui_localization.py`, `apply_game_data_localization.py`, `apply_il2cpp_ui_templates.py`, `apply_runtime_display_names.py`, or `restore_runtime_display_names.py`.
- Write only under `.codex-localization-tools` and `BepInEx/plugins/SpiritVale.RuntimeLocalization`.
- Never modify Unity bundles, `GameAssembly.dll`, metadata, interop, saves, or player-created text.
- Never add an injected IL2CPP `MonoBehaviour` for display polling; patch an existing game update method and fail closed to Chinese.
- Never deploy or package while the game is running. Never approve a game hash without a matching live verification record.

## Start The Loop

1. Read [guardrails.md](references/guardrails.md) before changing files.
2. Resolve the game directory. Prefer the user's current SpiritVale directory; otherwise pass `-GameRoot` explicitly.
3. Run `Status`, `Queue`, then the read-only source audit:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Invoke-SpiritValeLocalizationLoop.ps1 -Stage Status -GameRoot <game-root>
powershell -ExecutionPolicy Bypass -File scripts/Invoke-SpiritValeLocalizationLoop.ps1 -Stage Queue -GameRoot <game-root>
powershell -ExecutionPolicy Bypass -File scripts/Invoke-SpiritValeLocalizationLoop.ps1 -Stage Audit -GameRoot <game-root>
```

Treat every queued blocker as required. Do not deploy over a running game.

`Status` reports `RepositoryVersion`, `ChangelogVersion`, `PluginVersion`, `InstallerVersion`, `ReleaseKind`, and `LastLiveVerifiedVersion` separately. `Queue` must show no `SyncReleaseMetadata` blocker before validation or release work. A newer candidate than the last live record is expected during development; it remains unreleasable until `RecordLive` binds the current runtime hashes.

The `Audit` stage dynamically locates `SpiritVale_Data/sharedassets0.assets`, scans its `MonoBehaviour` skill records, and validates the current active-skill ID set and runtime display strings against the current generated dictionary. It also regenerates `artifacts/bilingual-entity-catalog.tsv` and its audit, binding the entity catalog to the current source snapshot, runtime names, skill aliases, dictionary, map manifest, Build, and game hashes. Review `artifacts/runtime-skill-aliases.tsv` whenever the ID set changes or a runtime display differs from the canonical `Skills` entry in `source-snapshot.json`. Source-bundle coverage and an old fixed count are not sufficient.

## Handle A Game Update

If `Queue` reports stale IL2CPP interop, launch the game once and wait for BepInEx `Il2CppInteropGen` to finish. Exit the game normally, rerun `Status`, and only then build the plugin. Never copy an old `BepInEx/interop` directory into a new game build.

Review new visible English from screenshots, the latest session in `BepInEx/plugins/SpiritVale.RuntimeLocalization/untranslated-runtime.log`, or extracted localization data. `Queue` reports cast announcements, gameplay descriptions, map labels, item names, and stable UI-label residuals from that latest session; clear those findings before recording live verification. Put reviewed exact source-to-target overrides in `.codex-localization-tools/missing-zh-reviewed-source-overrides.json`. Put stable general terminology in `runtime-manual-overrides.json`. Do not hand-edit the generated `translations.tsv`.

Use `artifacts/localized-corpus.tsv` as the review corpus after `Validate`. It records each source-snapshot key/category, the English source, the built-in Simplified Chinese value, and the final value projected through the real runtime translator. Review the `runtime-target` column rather than assuming the built-in Chinese column is what players see. Regenerate this report after every translation-source or translator change.

Keep broad editorial upgrades in `mmo-quality-overrides.json`. That file has final precedence over legacy, glossary, and screenshot-level overrides and is the authority for polished class, skill, monster, item, affix, and material names. Use it only for reviewed improvements that should survive future game versions; do not put one-off runtime captures or player-controlled text there. The generator derives non-exception gem names from the reviewed base skill term, and validation requires all artifact components to follow their artifact base name.

Read [terminology.md](references/terminology.md) while reviewing MMO terms. Preserve placeholders, rich-text tags, player names, shop names, guild names, and chat content. For a new dynamic sentence shape, add a managed test before changing `RuntimeTextTranslator.cs`.

## Validate And Deploy

Run static checks and managed tests, then build and deploy while the game is closed:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Invoke-SpiritValeLocalizationLoop.ps1 -Stage Validate -GameRoot <game-root>
powershell -ExecutionPolicy Bypass -File scripts/Invoke-SpiritValeLocalizationLoop.ps1 -Stage Build -Deploy -GameRoot <game-root>
```

The build writes generated artifacts under `.codex-localization-tools/artifacts`; `-Deploy` copies only the plugin DLL, generated dictionary, and generated bilingual entity catalog into the game.

The optional entity-display modes live in `BepInEx/config/local.spiritvale.runtime-localization.cfg`. Defaults must remain `EntityNameMode = Chinese`, `CompactSurfaceMode = Chinese`, and `TemporaryEnglishKey = Tab`. `Bilingual` may affect trusted detail titles only. `EnglishToggle` may affect trusted compact entity labels only: each press of the configured key switches Chinese and English, while key release has no display effect. `EnglishOnHold` remains a legacy accepted configuration value and must normalize to `EnglishToggle`. Never feed composed display text back into partial translation, market queries, input fields, or player-created text.

`Audit`, `Validate`, and `Build` must all report complete runtime skill display coverage using the current extracted active-skill count. Managed tests exercise every covered runtime alias with ASCII, spaced, and full-width cast punctuation, plus standalone/rich-text punctuation nodes. Corpus tests must also cover every mixed-language gem and skill/passive description in the current source snapshot. A missing shared-assets object, untranslated display alias, unreviewed set addition/removal, or residual English gameplay term in those corpora blocks deployment and release work.

`Validate` also treats the currently deployed dictionary as a fixed vocabulary baseline. Every deployed source key must remain present; translated targets may be improved and new keys may be added, but an equal-count replacement cannot hide a removed key. The successful deploy becomes the next baseline, so vocabulary is monotonic across agent-loop runs.

`Validate` and `Build` write `artifacts/mixed-description-residuals.tsv`. Review it when polishing a release: gameplay terms are blockers, while intentional proper names, creator signatures, acronyms, and branded titles may remain when documented. This report is the handoff list for Luna; do not infer quality only from the aggregate localized count.

Cold-start the game twice and complete [live-checklist.md](references/live-checklist.md). Inspect `BepInEx/LogOutput.log` after each run. A loading screen, server list, or main HUD check alone is not sufficient. Record the verified surfaces and screenshot evidence with the `RecordLive` stage; this binds the live result to the exact current hashes.

Luna may own normal screenshot/log intake, corpus review, exact or editorial overrides, regression tests, `Validate`, a closed-game build/deploy, and the live checklist. Escalate to Terra when a Steam build/hash changes, interop is stale, source extraction or coverage changes, runtime patching fails, the game crashes or black-screens, fonts render tofu, player text is touched, or a safe fix would require framework code. Terra must leave the control loop in a state where Luna can resume without undocumented manual steps.

## Package A Release

Use the dedicated [package-spiritvale-localization](../package-spiritvale-localization/SKILL.md) skill for installer credits, process-path protection, install/update/original-state restore transactions, release self-tests, payload verification, and friend-facing delivery. Keep this skill responsible for the translation candidate and live-verification gates.

Only after live verification, package the exact tested artifacts. `-ApproveGameHash` records the current `GameAssembly.dll` hash as installer-compatible:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Invoke-SpiritValeLocalizationLoop.ps1 -Stage Package -ApproveGameHash -GameRoot <game-root>
```

Before changing a version, add one non-empty, UTC-dated `## [x.y.z] - YYYY-MM-DD` section to the root `CHANGELOG.md`. Keep sections unique and newest-first.

- Content release: run `Build -PatchVersion x.y.z`. This synchronizes the root `VERSION`, plugin version, and all current installer version fields. The new runtime payload requires normal deployment, two cold starts, `RecordLive`, and packaging.
- Installer-only release: run `Build -InstallerVersion x.y.z` only while the game is closed and the existing plugin, dictionary, catalog, audit, screenshots, and live record still match byte-for-byte. This advances root/installer identity while preserving `PluginVersion`. Any runtime payload change invalidates this path.

Never edit `artifacts/live-verification.json` to make a version pass. Do not treat the frozen `v1.2.21` compatibility manifest or old `v1.1.0` bundles as current version authorities. The shared `Test-SpiritValeReleaseMetadata.ps1` validator checks strict SemVer, changelog uniqueness/order/date/content, installer fields, release kind, optional tag equality, live hashes, and final package hashes.

The package stage rebuilds the payload, publishes the uncompressed single-file installer and self-contained multi-file compatibility ZIP, runs its transactional self-test, validates `release-vx.y.z.json`, and emits matching SHA-256 files. Create or push `vx.y.z` only after this stage passes; the tag triggers the repository Release workflow and is a real publication action.

## Finish

Rerun `Status` and `Queue`. Report the repository, changelog, plugin, installer, and last-live versions; release kind; Steam build ID; game and metadata hashes; interop hash; translation count; plugin hash; test results; installer hash; and any UI surfaces not inspected. Do not claim release readiness while version metadata, live checks, or package hashes remain pending.
