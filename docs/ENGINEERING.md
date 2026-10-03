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

## Offline verification

`--verify-offline` uses the form against an existing local snapshot, exercises an illustrated UUID, checks the clothing type filter and records three window sizes. The preserved October 3 report is prior local UI evidence. A clean compilation does not replace that check or prove service availability.
