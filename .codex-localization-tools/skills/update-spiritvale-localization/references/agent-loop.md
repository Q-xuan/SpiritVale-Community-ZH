# SpiritVale Localization Agent Loop

Use this contract to keep routine Chinese text maintenance fast while preserving the safety needed for an IL2CPP game update. The PowerShell loop is the source of truth for state; screenshots and live observation are evidence, not substitutes for it.

## Roles

### Luna: Content Maintainer

Own routine work when the current game build and runtime framework are healthy:

- intake screenshots and the latest untranslated runtime session;
- review `artifacts/localized-corpus.tsv` for omissions, machine-like wording, category mistakes, and inconsistent MMO terminology;
- update reviewed override sources and regression tests;
- run `Audit` and `Validate` until all gates pass;
- when the exact target game process is closed, build and deploy the candidate;
- perform two cold starts, inspect logs, check required UI surfaces, and run `RecordLive`;
- hand a live-verified candidate to the packaging skill.

Luna must stop and hand off to Terra when framework behavior, a game update, a crash, interop, source extraction, fonts, or player-text safety is involved.

### Terra: Migration And Incident Owner

Own work that can invalidate the runtime framework or game-version contract:

- migrate to a new Steam build and regenerate IL2CPP interop;
- investigate black screens, crashes, Harmony failures, missing patches, stale deployments, and source-schema changes;
- repair extraction, dictionary generation, runtime templates, context guards, font fallback, control-loop gates, and installer compatibility inputs;
- prove that player-created text remains protected;
- own installer compatibility-policy probes, denylist entries, manifest migrations, and fail-open Harmony registration;
- return a healthy framework, updated tests, reproducible commands, and explicit remaining content work to Luna.

Terra may also perform Luna work, but must not weaken a safety gate merely to complete a migration.

## Single-Writer Rule

Parallelize read-only audits by domain. Assign exactly one integration agent before editing any shared authority:

- `mmo-quality-overrides.json`: polished MMO terms and semantic family changes;
- `missing-zh-reviewed-source-overrides.json`: exact fixed strings confirmed by source data, logs, or screenshots;
- `runtime-manual-overrides.json`: stable general runtime terminology or fragments;
- `market-search-concept-aliases.json`: reviewed local market-index aliases attached to canonical item identities;
- `market-search-keyword-preferences.json`: legacy Chinese-to-English migration input only; never a runtime query-rewrite authority;
- `bilingual-map-entities.json`: reviewed map/location identities for the generated entity display catalog;
- `RuntimeTextTranslator.cs`: stable dynamic shapes only, with managed tests;
- `BilingualDisplayRuntime.cs` and producer patches: trusted entity presentation only; one Terra integrator owns framework changes;
- `Program.cs`: regression and corpus projection tests;
- generated `artifacts/translations.tsv`: never edit by hand.

Every writer must reread the target file immediately before applying a patch. Other agents return candidate tables containing exact source, current target, proposed target, category/reason, confidence, and all related source variants.

## State Machine

### 1. Observe

Run `Status`, `Queue`, and `Audit`. Record the repository, changelog, plugin, installer, and last-live versions; release kind; Steam build ID; game and metadata hashes; interop state; deployed plugin/dictionary hashes; translation count; latest runtime session; and active game process.

Treat `RepositoryVersion` as the current public release candidate, not proof of live acceptance. `LastLiveVerifiedVersion` is historical evidence bound to exact runtime hashes. A mismatch is normal while developing a content release and blocks packaging until `RecordLive` is repeated.

Do not infer that the game is closed from a window disappearing. The loop must find no process whose normalized executable path equals the selected `<game-root>\SpiritVale.exe`.

### 2. Classify

Classify each finding before editing:

| Finding | Authority |
| --- | --- |
| Reviewed class, skill, monster, equipment, affix, material, artifact, or family-wide wording | `mmo-quality-overrides.json` |
| Exact untranslated or partly translated fixed source string | `missing-zh-reviewed-source-overrides.json` |
| Stable general term needed inside trusted mixed text | `runtime-manual-overrides.json` |
| Chinese vending term reaches multiple names or inflections | canonical market catalog plus `market-search-concept-aliases.json` and identity coverage tests |
| Repeating number/tag/prefix/suffix sentence shape | `RuntimeTextTranslator.cs` plus tests |
| Runtime display name differing from canonical source | runtime alias extraction/audit, then reviewed override |
| Player, shop, guild, party, or chat content | preserve unchanged |
| Entity display needs English for guide lookup | generated bilingual entity catalog plus a trusted producer; never the general dictionary or market bridge |
| Crash, black screen, patch failure, stale interop, tofu glyph, or changed source schema | Terra incident |

Prefer exact whole strings. Do not add broad fragments such as `to` or `for`. Preserve placeholders, rich-text tags, line breaks, punctuation intent, numbers, and player-controlled values.

### 3. Review The Authoritative Corpus

Run `Validate` to regenerate `artifacts/localized-corpus.tsv`. Treat each row as:

```text
key  category  source  built-in-simplified  runtime-target
```

Review `runtime-target`, because trusted descriptions may receive additional runtime substitutions that are absent from the built-in Chinese value. Keep duplicate English sources as separate rows when their keys or categories differ.

Audit in batches:

1. classes and class descriptions;
2. skills, passives, cast aliases, and skill gems;
3. monsters, ranks, summons, familiars, and pets;
4. equipment, slots, affixes, cards, materials, and consumables;
5. artifacts and every rune/relic/scroll/gem/set variant;
6. maps, quests, NPC text, settings, dialogs, and compact UI labels;
7. mixed descriptions, dynamic counters, timers, levels, refine values, and rich text.

Reject different English concepts collapsing into an ambiguous Chinese name unless the game intentionally treats them as the same concept. Update every semantic family member together.

For an intentional shared Chinese vending term, keep display translation separate from backend search. Attach a reviewed alias to every matching canonical `(ItemType, ItemId)` in the generated local market catalog. Never rewrite a Chinese query to one preferred English word, issue multiple server searches, or mutate the search field. Audit every alias as a batch, prove all canonical candidates remain reachable, leave English queries byte-for-byte unchanged, and fail open to the original search if the local cache or Harmony contract changes. Do not add broad runtime text fragments to solve search-only behavior.

### 4. Patch And Test

Edit only authoritative source files. Add managed tests for:

- every new dynamic format;
- every context-sensitive replacement;
- rich-text, ASCII punctuation, full-width punctuation, spacing, and trim variants when applicable;
- negative cases proving chat, names, display-name dictionaries, and unknown composites remain untouched;
- skill/gem and artifact-family consistency rules.

Run `Validate`. Require:

- source coverage `3579/3579` or the current complete count after a verified source update;
- runtime skill aliases `278/278` or the current verified active-skill count;
- runtime names `2730/2730` or the current verified extracted-name count;
- zero dictionary conflicts and zero unexplained missing entries;
- bilingual entity catalog coverage complete for Item, Equip, Artifact, Gem, Skill, SkillPassive, Monster, and Map, with a fresh hash-bound audit;
- every managed test passing;
- `localized-corpus.tsv` regenerated successfully;
- no unexplained English gameplay term in mixed-description residuals.

Counts may increase after a game update. A decrease is a blocker unless Terra documents the source change and intentionally updates the baseline.

### 5. Build And Deploy

Only continue when the exact target game process is closed and `Validate` passes. Add the candidate section to root `CHANGELOG.md`, then run `Build -Deploy -PatchVersion x.y.z` for a content release. Verify root/changelog/plugin/installer identity and the source/deployed plugin/dictionary/entity-catalog hashes.

For an installer-only revision, keep the runtime version and payload hashes unchanged, add the new changelog section, and use `Build -InstallerVersion x.y.z`. The loop must validate the old runtime against its exact live record before editing installer identity. If that proof fails, use the normal content-release path and repeat live acceptance.

Do not copy old interop into a new build. Do not deploy a DLL built against stale interop. Do not start the game from a content-only background task unless that task explicitly owns live acceptance.

### 6. Live Acceptance

Cold-start twice and complete `live-checklist.md`. Use Computer Use or direct observation to inspect the actual UI, not only logs. Check screenshots for untranslated English, mojibake, tofu squares, clipping, overlapping text, broken tags, changed player names, and incorrect dynamic values.

For optional entity display, approve the default Chinese configuration first. Then test `EntityNameMode = Bilingual` on trusted detail titles and `CompactSurfaceMode = EnglishToggle` with the configured toggle key: one press changes trusted compact labels to English, key release changes nothing, and the next press restores Chinese. The legacy `EnglishOnHold` value must be accepted and normalized to this same toggle behavior. Confirm input fields, market wire queries, descriptions, player/shop/guild/chat/ranking text, and unknown TMP surfaces are unchanged. Restart between configuration modes; the first release does not promise hot config reload.

After each run, inspect `BepInEx/LogOutput.log` and the latest untranslated runtime session. Any new fixed source returns to step 2. A framework exception, crash, black screen, or unsafe replacement goes to Terra.

Run `RecordLive` only after all required accessible surfaces pass. Include exact screenshot evidence paths and list inaccessible surfaces as unverified. The record must bind the current plugin and dictionary hashes.

### 7. Release

Rerun `Status` and `Queue`. A release candidate requires matching root/changelog/installer metadata, the declared content or installer-only version relationship, and an empty content/framework/deployment/live queue. Then invoke `package-spiritvale-localization`; do not duplicate installer logic here.

After packaging, validate the versioned EXE, compatibility ZIP, SHA-256 sidecars, and `release-vx.y.z.json`. Only then create or push the exact `vx.y.z` tag. The tag-triggered workflow must revalidate tag/version/changelog equality and frozen hashes before publishing; never use a tag as a test signal.

## New Version Migration

Terra follows this order:

1. Let Steam finish and capture the new build ID plus `GameAssembly.dll` and metadata hashes.
2. Run `Status`, `Queue`, and `Audit`; preserve the previous known-good release artifacts.
3. Start the updated game once only to let BepInEx regenerate interop, then exit completely.
4. Verify interop freshness and rebuild against it.
5. Re-extract sources and compare category/key sets, active skill ID/canonical/display tuples, runtime-name ID/category/canonical/display tuples, category counts, and `localized-corpus.tsv` with the prior baseline.
6. Classify every addition, removal, renamed tuple, changed English, changed built-in Chinese, and runtime-only alias. Never accept an old fixed count as proof and never hide a count drop behind a previous baseline.
7. Repair framework or source-schema changes, add regression tests, and run `Validate`.
8. Deploy, perform two cold starts and full live acceptance, then record verification.
9. Approve the new game hash only after live verification succeeds. Package last.

The migration handoff to Luna must state the new baseline counts, changed categories, compatible game hash, known unverified surfaces, and exact remaining corpus rows needing editorial work.

## Installer Compatibility Triage

Luna may continue normal content maintenance when an updated game is `Compatible-Unverified` and all structural probes pass, but may not add the hash to Verified or publish an installer. Luna returns untranslated screenshots/log rows and preserves all compatibility evidence.

Terra owns every `Blocked` result, any PE/metadata/source-schema/interop/Harmony signature change, and every denylist decision. Terra must repair or document the structural condition without weakening process protection, immutable backups, transactions, conflict preservation, restore behavior, payload integrity, or release gates.

An explicit user compatibility attempt authorizes only a local install. It is never evidence for `RecordLive`, `ApproveGameHash`, or `Package`. Verified promotion still requires the exact deployed plugin/dictionary hashes, two cold starts, clean logs, required UI surfaces, and a matching live record.

## Incident Triage

### Translation Does Not Apply

Compare generated and deployed dictionary hashes, patch versions, latest session timestamp, source context, and exact whitespace/tag shape. Determine whether the text is an exact source, a runtime alias, a trusted mixed description, a dynamic template, or player-controlled content. Fix the narrowest authoritative layer and add a reproducing test.

### Translation Disappears After An Update

Check Steam/game hashes, interop freshness, plugin load/version lines, Harmony patch success, generated/deployed hashes, extracted source diffs, and alias counts. Do not blindly re-add old strings before proving the new runtime source.

### Crash Or Black Screen

Stop deployment work. Preserve the full current log and crash evidence. Check for stale interop, plugin exceptions, Harmony failures, active XUnity components, unsupported IL2CPP type injection, and a DLL copied while the game was running. Reproduce with the smallest controlled change and restore the known-good candidate when needed.

### Mojibake Or Tofu Squares

Distinguish UTF-8 decoding errors from missing TMP glyphs. Read source files with explicit UTF-8. For tofu, verify the runtime CJK fallback log and the exact affected font/surface; do not replace valid Chinese with ASCII as a workaround.

For Windows PowerShell 5, an unmarked UTF-8 `.ps1` file must remain ASCII-only. Represent required non-ASCII comparison literals with Unicode code points, or add and verify a UTF-8 BOM. The control loop enforces this invariant because a misdecoded allow-list entry can falsely report already translated text as a runtime residual.

### Player Text Was Changed

Treat this as a release blocker. Add a negative regression using the exact context, narrow the trusted-context or template guard, and recheck player, shop, guild, party, and chat surfaces before recording live verification.

## Handoff Report

Every Luna/Terra turn reports:

- exact files and authoritative entries changed;
- candidate rows intentionally deferred and why;
- Steam build ID and relevant hashes;
- source, skill-alias, runtime-name, dictionary, corpus, and test counts;
- repository/changelog/plugin/installer/last-live versions and release kind;
- build/deploy status and exact deployed versions/hashes;
- cold-start/log/UI evidence and unverified surfaces;
- next owner: Luna content, Terra incident/migration, or packaging.

Never report the framework as stable from static tests alone. Stability requires the current game build, fresh interop, matching deployed hashes, two clean cold starts, clean logs, live UI evidence, and a hash-bound `RecordLive` result.
