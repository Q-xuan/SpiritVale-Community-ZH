---
name: package-spiritvale-localization
description: Freeze, verify, build, transaction-test, and publish the SpiritVale Simplified Chinese patch as a self-contained Windows installer. Use when preparing a friend-facing release, revising installer credits or safety behavior, validating automatic Steam discovery, testing install/update/original-state restore, or regenerating the final EXE and SHA-256 after an installer-only change.
---

# Package SpiritVale Localization

On Windows PowerShell 5, read this skill and all installer C# files with `Get-Content -Encoding UTF8`; the default legacy code page will display valid Chinese text as mojibake.

Build only the installer and release artifacts. Keep translation review, plugin development, deployment, and live game validation in `../update-spiritvale-localization/SKILL.md`.

Treat the live-verified plugin and dictionary as read-only inputs. Keep a single writer for installer sources and release artifacts, and return the changed files, test evidence, remaining gate, and final hashes to the localization task.

## Release Contract

- Verify Steam App ID `3767850` and the intended SpiritVale root.
- Read `../update-spiritvale-localization/references/guardrails.md` before doing release work.
- Never modify Unity assets, `GameAssembly.dll`, metadata, interop, saves, or player-created text.
- Never start the game from this workflow.
- Never deploy, install, restore, or package while the target `SpiritVale.exe` is running.
- Never edit translations, runtime plugin behavior, or patch versions merely to make packaging pass.
- Never add networking, telemetry, advertising, or an online dependency to the installer.
- Treat every newly generated EXE as a new release artifact. Immediately retire the previous EXE hash.

## Require A Frozen Candidate

Run the localization control loop from the game root:

```powershell
powershell -ExecutionPolicy Bypass -File .\.codex-localization-tools\skills\update-spiritvale-localization\scripts\Invoke-SpiritValeLocalizationLoop.ps1 -Stage Status -GameRoot <game-root>
powershell -ExecutionPolicy Bypass -File .\.codex-localization-tools\skills\update-spiritvale-localization\scripts\Invoke-SpiritValeLocalizationLoop.ps1 -Stage Queue -GameRoot <game-root>
powershell -ExecutionPolicy Bypass -File .\.codex-localization-tools\skills\update-spiritvale-localization\scripts\Invoke-SpiritValeLocalizationLoop.ps1 -Stage Audit -GameRoot <game-root>
```

Do not package until all of these are true:

1. The target game process is closed.
2. The candidate plugin, generated dictionary, and bilingual entity catalog are deployed.
3. `Audit`, `Validate`, and managed tests pass.
4. Two cold starts and the required live UI surfaces were checked.
5. `artifacts/live-verification.json` exists and binds those checks to the exact current plugin, dictionary, entity-catalog, and entity-catalog-audit SHA-256 values.
6. `Queue` contains no translation, runtime-residual, log, deployment, or live-verification blocker.
7. Root `VERSION`, the current `CHANGELOG.md` section, all installer version fields, and the declared content/installer-only relationship pass `Test-SpiritValeReleaseMetadata.ps1`.

An older live record cannot approve a rebuilt DLL or dictionary, even when the version string is unchanged.

For a content release, root, changelog, plugin, and installer versions must match. For an installer-only release, the root/installer version may advance while `PluginVersion` remains at the exact live-verified runtime version; plugin, dictionary, catalog, and catalog-audit hashes must remain byte-for-byte identical. Never rewrite the live record to convert one release kind into the other.

## Preserve Release Identity

Keep this visible identity in the installer source and generated EXE metadata:

- Author: `auryx`
- QQ group: `882132807`
- Notice: `个人汉化学习作品，侵删`

Show it on the main installer surface and the About/component page. Set the project `Authors`, `Company`, `Description`, and `Copyright` properties consistently. Check the published EXE metadata rather than trusting source text alone.

## Enforce Installer Safety

Automatic discovery must search Steam registry roots and every `libraryfolders.vdf`, then validate the selected directory against App ID `3767850`, its `installdir`, and Build ID. The installer is fully offline: embed `compatibility-policy.json`, the payload, Unity base libraries, and the IL2CPP auto-generation configuration; never fetch code or a compatibility list.

For install and restore operations:

- Compare the running process executable's normalized full path with `<selected-game-root>\SpiritVale.exe`.
- Do not block a same-named process from another directory, including a Demo installation.
- If a same-named process exists but its executable path cannot be read, block conservatively and explain why.
- Check before the first filesystem write.
- Keep manual executable selection available when automatic discovery fails.

Classify every selected install before writing:

- `Verified`: the embedded extensible list matches Steam Build ID, `GameAssembly.dll` SHA-256, and the optional metadata SHA-256. Install normally.
- `Compatible-Unverified`: the hash is not listed, but App ID/directory, x64 PE files, IL2CPP metadata, offline BepInEx auto-generation conditions, payload whitelist, and payload hashes all pass. Keep install disabled until the user explicitly accepts a clearly labelled compatibility attempt. State that localization completeness may decline and never call the version verified.
- `Blocked`: wrong App ID/directory, missing or malformed critical files, a denylisted rule, failed structure/payload probe, or the exact target process running. Do not write.

Keep `compatible-unverified` installation authority separate from release authority. Only the localization loop may append a Verified entry, and only after the exact candidate passes two cold starts, live UI/log checks, and hash-bound `RecordLive`. Unknown hashes must never make `Package`, `ApproveGameHash`, or `RecordLive` easier to pass. Restoration remains available for unknown hashes, but process, manifest, backup, conflict, and transaction checks still apply.

## Restore The Original State

Present uninstall as `恢复原版` and require confirmation. Restore the files captured before the first patch installation, not files from the latest overlay update.

Create a sealed initial-backup manifest before the first install write. Record each normalized relative path, whether it existed before installation, its byte size, and SHA-256. Existing records and backups are immutable. When a later installer introduces a genuinely new payload path, it may transactionally append only that missing path and its pre-update state, reseal the manifest, and leave every existing record and backup byte-for-byte unchanged.

Restoration must:

- work even when the current `GameAssembly.dll` hash is unknown or not yet supported;
- preserve patch files modified by the user as `.user-modified*` copies;
- restore pre-existing same-name files from the initial backup;
- remove files originally introduced by the patch;
- restore XUnity files that this installer disabled;
- detect user-changed managed files before mutation, show the conflict list, and preserve accepted conflicts as `.user-modified*` copies instead of silently overwriting them;
- include the manifest and backup-state cleanup in the same transaction;
- roll back all touched files after any injected or real failure, leaving no half-restored state.

If the manifest or any required initial backup is missing, stop before mutation and direct the user to Steam's file verification instead of guessing.

## Build And Test

Package the exact live-verified candidate:

```powershell
powershell -ExecutionPolicy Bypass -File .\.codex-localization-tools\skills\update-spiritvale-localization\scripts\Invoke-SpiritValeLocalizationLoop.ps1 -Stage Package -ApproveGameHash -GameRoot <game-root>
```

The package stage must rebuild `Payload.zip`, publish a .NET 8 LTS self-contained x64 uncompressed single-file EXE plus a self-contained multi-file compatibility ZIP, run the installer self-test, and write matching SHA-256 files. Keep ReadyToRun and trimming disabled. The compatibility ZIP must contain `coreclr.dll`, a package hash manifest, and a Chinese readme instructing users to extract the complete directory.

It must also emit `release-vx.y.z.json` and pass strict release-metadata validation against the exact versioned assets. Do not create or push the release tag from packaging code.

Require self-tests for:

1. First install and automatic payload integrity.
2. Repeated install and overlay update while retaining the first-install backup byte for byte.
3. Corrupted or substituted payload rejection before target mutation.
4. Exact-path process blocking.
5. A same-named process from another directory not being blocked.
6. An unreadable process path being blocked conservatively.
7. XUnity disable and restore.
8. User-modified file conflict reporting and preservation.
9. Injected install or update failure with complete rollback.
10. Verified classification and normal install.
11. Compatible-Unverified classification, zero-write rejection without consent, and explicit compatibility install without promotion to Verified.
12. Blocked wrong App ID, malformed PE/metadata, denylist, and payload probes.
13. Restore on an unknown game hash.
14. Cross-version update while preserving the immutable first backup and recording actual Build/hashes/compatibility level.
15. Active-manifest migration to the current schema.
16. Missing, truncated, or hash-mismatched initial backup rejection before restore mutation.
17. Injected restore failure with complete rollback.
18. A legacy or sealed pre-catalog install can append the new entity-catalog path, preserve a pre-existing user file, and remove a patch-introduced file on restore without changing older backups.

Do not weaken process checks in production to make the temporary-directory self-test work. Inject a deterministic process probe into the test service instead.

## Verify The Payload

Independently inspect the final files after packaging:

- Compare embedded/staged plugin SHA-256 with the deployed plugin and `live-verification.json`.
- Compare embedded/staged `translations.tsv` SHA-256 and entry count with the generated and deployed dictionary.
- Compare embedded/staged `bilingual-entity-catalog.tsv` SHA-256 and row count with the generated and deployed entity catalog and its audit.
- Record `Payload.zip` size, entry count, and SHA-256.
- Confirm the active manifest records actual Steam Build ID, GameAssembly/metadata hashes, compatibility level, payload archive hash, plugin/dictionary/entity-catalog payload hashes, and the default Chinese display modes.
- Confirm the payload whitelist excludes interop, logs, caches, debug symbols, XUnity, untranslated captures, and project sources.
- Confirm EXE product/file versions, author metadata, size, and SHA-256.
- Confirm root `VERSION`, the extracted changelog section, installer metadata, and `release-vx.y.z.json` identify the same version and release kind.
- Confirm the compatibility ZIP opens, contains the apphost, managed DLL, `coreclr.dll`, startup readme, and per-file SHA-256 manifest, and that its external SHA-256 file matches.
- Rerun `Status` and `Queue`; both must still describe the same frozen candidate and an empty release queue.

An installer-only revision may reuse the existing live verification only when the embedded plugin, dictionary, and entity catalog hashes remain byte-for-byte identical. Any payload change requires the normal validation and live-check gates again.

## Deliver

Send `installer/dist/SpiritVale_Chinese_Patch.exe` as the normal package. For systems where the EXE does not open, send `installer/dist/SpiritVale_Chinese_Patch_Compatibility_x64.zip` and require the player to extract the complete directory before running its EXE. SHA-256 files are optional; never send `Payload.zip`, `payload-stage`, loose plugin/dictionary files, or intermediate publish output.

Report:

- patch and EXE file versions;
- Steam build ID and approved game hash;
- EXE and compatibility ZIP absolute paths, byte sizes, and SHA-256 values;
- plugin, dictionary, and entity-catalog SHA-256 values plus dictionary/catalog row counts;
- transaction self-test result;
- automatic path discovery and `恢复原版` behavior;
- explicit confirmation that the previous installer hash is obsolete.

After this report is complete, the maintainer may push the exact `vx.y.z` tag. That tag triggers real GitHub Release publication; never push it as a workflow smoke test.
