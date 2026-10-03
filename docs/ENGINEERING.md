# Storage and sync contracts

## Snapshot and identity

The viewer loads `Data/catalogue.json` relative to `AppContext.BaseDirectory`. `CatalogueSnapshot` records download time and item records with names/descriptions, maker, version, image paths, legal information and type metadata. Home UUIDs use the complete 35-character `8-8-8-8` hexadecimal form. They are not standard GUID strings.

The form debounces filter changes, searches item metadata and presents the resulting list with local thumbnails and a separate detail preview. The displayed count reflects the current filter. Local picture paths are resolved beneath `Data/ImageArchive`; missing or invalid previews leave the metadata usable. No game process or emulator is required.

## Explicit metadata sync

`CatalogueService` is configured for `https://destinationhome.online/api/catalog`. Requests use a GraphQL objects query with 100 records per page, a 30-second HTTP timeout and at most four attempts with increasing backoff. This is the preserved source contract; current service behavior has not been verified during repository preparation.

Items are collected in a case-insensitive UUID dictionary. A page indicating more results must provide a nonblank cursor different from the prior cursor. Every 25 pages, a checkpoint is written through a temporary file and replacement. Resume accepts matching-endpoint checkpoints saved within the last day. Final records are sorted by display name, then UUID, written to a temporary snapshot and moved into place only after the full sync. Completion deletes the checkpoint. Cancellation and failure leave the previous completed snapshot intact.

`--sync-only` supports `--data-dir` and writes progress to `sync.log`. Normal UI browsing loads local files; the update button explicitly starts network activity and supports cancellation. The service contains no embedded authentication credentials.

## Pictures and updates

`Update-OfflineCatalogue.ps1` publishes a self-contained Windows x64 build, obtains the public ImageArchive through Git and then invokes the published sync-only executable. Existing Git image checkouts use `pull --ff-only`; an existing non-Git image directory causes a stop. Image downloads and metadata sync are separate operations and do not imply that every entry has a public preview.

## Appearance, controls and resources

`CatalogueAppearance` owns one of six immutable palettes plus separate Animated/Reactive flags and a change event. `CataloguePreferences` loads the local JSON settings with fallback defaults and saves through a temporary file. The form applies palette changes across window chrome, grid and custom controls; save failures are reported without reverting the visible selection.

`CatalogueInteractiveControls` implements painted buttons, choice popups, toggles and scrollbar interaction. `CatalogueContentControls` routes grid/detail wheel scrolling through those scrollbars and renders the complete local image without hover cropping. The details viewport intentionally allows scrolling content beyond its bounds; other controls are checked for clipping. Interaction timers stop when transitions finish or appearance changes.

The project embeds icon/artwork with `DestinationHome.Catalogue.CatalogueIcon` and `DestinationHome.Catalogue.CatalogueArtwork` logical resource names. Form initialization reads those exact resources. The namespace adaptation must keep all three resource references synchronized. PerMonitorV2 and DPI-based detail measurements are part of the layout contract.

## Packaging boundaries

The packaging script uses the repository root, emits standalone executable/archive names and excludes local Data from runtime-only updates. Source packaging excludes `.git`, IDE/build state, Deliverables, downloaded data and logs. Full offline packaging is explicit local distribution work; it includes your snapshot and PNG previews and is not committed.

## Offline verification

`--verify-offline` checks an illustrated UUID, the clothing filter, six palettes at three window sizes, appearance switches, settings round-trip, popup/keyboard selection, long-detail reachability and simulated 125%/150%/200% layout/text scaling. It verifies embedded icon/artwork and writes a report plus rendered screenshots. Preference fixtures are saved in the report directory; appearance control exercises suppress saving the user's preferences. A clean compilation does not replace these checks or establish physical multi-monitor behavior or service availability.
