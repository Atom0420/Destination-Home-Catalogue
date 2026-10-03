# Download and use UUID Catalogue

Download **UUID-Catalogue-Windows-x64.zip** from the latest GitHub release. Extract the entire ZIP into a new folder, open `UUID-Catalogue`, and run `DestinationHome.Catalogue.exe`.

Windows x64 is required. The package is self-contained: no Visual Studio, Git, .NET SDK or separate runtime installation is needed. Keep the supporting files together; do not run just the executable inside the ZIP.

The release includes the existing 66,704-entry metadata snapshot, so search, type filtering, item details, six themes and UUID copying are available offline. The full public image collection is excluded from this portable download. Previews appear when your own matching `Data/ImageArchive` is present. Use **OPEN DATA FOLDER** to locate the storage directory. Metadata browsing still works without pictures.

To reuse an existing local picture library, copy its `ImageArchive` directory into the new package's `Data` folder. Its item UUID subdirectories must stay intact. Appearance preferences are saved separately in `Data/viewer-settings.json` and need a writable folder. **UPDATE METADATA** explicitly contacts the configured service; current service availability was not established by the offline release checks.

## Updating an existing installation

Close the app, then either extract the portable package into a new folder and copy your existing picture library/preferences across, or use **DestinationHome-Catalogue-UI-Update-Windows-x64.zip** from the same release. The runtime-only update omits Data and preserves the existing metadata, images and appearance settings. Do not replace files while the viewer is running.

`SHA256SUMS.txt` records the release ZIP hashes. In PowerShell, use `Get-FileHash -Algorithm SHA256` on a downloaded ZIP and compare it to the corresponding line. The application is unsigned; the release does not change that status.

This is an experimental source checkpoint. Offline layout checks passed for all themes, controls and simulated scaling; physical multi-monitor acceptance and live metadata sync remain separate checks.
