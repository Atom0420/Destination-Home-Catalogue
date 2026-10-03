# Validation

Appearance update, 2026-10-03:

- The user's published portable-PDB checksums match all eight inspected C# source documents, including the new appearance, interactive and content controls.
- Independent Release/x64 build and self-contained win-x64 publish: zero warnings and zero errors.
- Fresh `--verify-offline` run on this repository's published binary passes with the existing 66,704-entry snapshot and 36 selected local preview files copied into ignored build output. No preferences, logs or downloaded image collection were committed.
- All six themes at three window sizes pass: 18 layout checks. UUID search, type filtering, local image display, settings round-trip, motion switches, long-detail reachability, dropdown keyboard/popup behavior, embedded icon/artwork and simulated 125%/150%/200% layout/text scaling pass.
- All four PowerShell scripts parse. `Package-Catalogue.ps1 -SkipFullArchive` creates the runtime-only update and source archives; their contents include the standalone executable/new source/assets and exclude local Data, Git/IDE/build state and PDBs where applicable.
- The portable release ZIP includes the exact published executable/DLL and supporting runtime plus the existing 66,704-entry metadata snapshot. ZIP integrity passes; no pictures, preferences, logs or PDBs are included. Release ZIP SHA-256 hashes are recorded.
- The supplied ICO contains valid PNG-backed 16/24/32/48/64/128/256 pixel frames. The icon master is preserved unchanged.
- Tracked text credential/path patterns, local Markdown destinations and file sizes checked before upload.
- No fresh metadata sync, image download, game/emulator launch or full offline-data packaging was performed.

The [fresh repository QA report](history/CATALOGUE_THEMES_REPO_QA_OCT03.json) records this independent published binary's checks. The [original theme QA report](history/CATALOGUE_THEMES_QA_OCT03.json) and theme gallery are preserved from the matching local source workspace. The earlier [basic QA report](history/CATALOGUE_QA_OCT03.json) and [interface screenshot](images/uuid-catalogue-oct03.png) remain historical evidence.

Simulated scaling checks are not acceptance on every physical monitor or display-DPI transition. Compilation and offline UI verification do not prove current service availability or a completed network sync.
