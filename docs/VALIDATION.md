# Validation

Repository separation, 2026-10-03:

- Release/x64 build: zero warnings and zero errors.
- Self-contained `win-x64` publish: zero warnings and zero errors; `DestinationHome.Catalogue.exe` is present.
- The PowerShell update script parses and targets that published executable. The script was not run because it downloads external data.
- Tracked-text credential/path patterns, local Markdown destinations and file sizes checked before upload.
- No app launch, fresh online sync, image download or game/emulator operation was performed during separation.

The [preserved offline QA report](history/CATALOGUE_QA_OCT03.json) reports success for 66,704 entries, UUID search, type filtering, an illustrated local item and window sizes 1320×820, 1084×641 and 1600×980. The [screenshot](images/uuid-catalogue-oct03.png) is from the matching October 3 local UI checkpoint. These existing artifacts retain their original evidence boundary.
