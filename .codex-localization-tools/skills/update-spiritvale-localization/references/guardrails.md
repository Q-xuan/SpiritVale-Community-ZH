# Safety Guardrails

## Runtime Architecture

- Use the dedicated BepInEx 6 IL2CPP Harmony plugin and exact runtime dictionary.
- Keep XUnity AutoTranslator and XUnity ResourceRedirector disabled. They previously caused instability in this game.
- Never register or inject a new IL2CPP type. Reject `ClassInjector`, `RegisterTypeInIl2Cpp`, `AddComponent<T>()`, and runtime scanner components.
- Do not modify `GameAssembly.dll`, `global-metadata.dat`, Unity assets, addressables, or saved games.
- Do not run legacy asset mutation scripts, especially `apply_runtime_display_names.py`.
- Never package `BepInEx/interop`, cache, logs, debug symbols, XUnity, or untranslated capture files.

## Update Ordering

1. Let Steam finish updating.
2. Capture `GameAssembly.dll` and metadata hashes.
3. Start the game once so BepInEx regenerates interop for the new build.
4. Exit the game completely.
5. Rebuild against the newly generated interop.
6. Deploy, cold-start twice, inspect logs, and live-check UI.
7. Only then approve the current game hash and package an installer.

Never overwrite a DLL while `SpiritVale.exe` is running. A successful compile against stale interop is not compatibility evidence.

## Text Integrity

- Preserve format placeholders such as `{0}`, numeric values, line breaks, and TextMeshPro rich-text tags.
- Preserve user-controlled text: player names, character names, shops, guilds, parties, and chat.
- Avoid generic fragment entries such as `to` and `for`; they can rewrite chat and names.
- Prefer exact whole strings. Use dynamic templates only for stable anchored formats and test both normal and rich-text variants.
- Keep source keys unique and deterministic. Resolve conflicts before generating the runtime dictionary.
- Treat screenshots as evidence of a visible source string, not permission to translate nearby user content.
- Treat active skill names serialized in `sharedassets0.assets` as runtime aliases. Filter them through the current `Skills` ID set from `source-snapshot.json`, compare that set with the prior migration baseline, and never infer translations from unrelated `MonoBehaviour` strings or preserve an old fixed count after a source change.

## Release Integrity

- Treat root `VERSION` as repository/installer release identity and `PluginVersion` as runtime-content identity. A content release aligns them; an installer-only release may differ only while exact live-verified runtime hashes remain unchanged.
- Require exactly one non-empty, UTC-dated, newest-first `CHANGELOG.md` section matching `VERSION`. Reject malformed or mismatched `vX.Y.Z` tags.
- Build payloads from the Release plugin DLL and generated artifact dictionary.
- Require installer whitelist validation and transactional self-test to pass.
- Record SHA-256 for the installer, compatibility ZIP, deployed plugin, dictionary, and bilingual entity catalog; verify them against the release manifest before publication.
- Do not mark a new `GameAssembly` hash compatible until live verification succeeds.
- Never edit live-verification history, frozen compatibility metadata, or an old release bundle to make current metadata pass.
