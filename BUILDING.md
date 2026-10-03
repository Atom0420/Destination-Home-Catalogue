# Build, deployment and data

The project targets Windows x64, `net8.0-windows`, Windows Forms and the .NET 8 API surface. Install a Windows .NET SDK that supports this target. Visual Studio with .NET desktop development is optional. The source has no project references or external NuGet package references. The theme palettes and custom controls compile into the executable. `Assets/catalogue.ico` and `Assets/catalogue-icon.png` are embedded through the project file; retain them when building. `ApplicationHighDpiMode` is PerMonitorV2. `NuGetAudit` is disabled in the preserved project; compilation is not a dependency security audit.

```powershell
dotnet build .\CatalogueViewer.csproj -c Release -p:Platform=x64
dotnet publish .\CatalogueViewer.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o .\bin\CatalogueViewer-Release
```

The executable is `DestinationHome.Catalogue.exe`. Keep all published supporting files, plus your own `Data` directory, together. Self-contained output needs no separately installed .NET runtime. Other builds require the .NET 8 Desktop Runtime. Build output is ignored by Git.

## Explicit online updates

The application reads `Data/catalogue.json` and pictures below `Data/ImageArchive` relative to the executable. Normal browsing of existing files is offline. **UPDATE METADATA** sends requests to the configured Destination Home service and refreshes item metadata only; it does not fetch the picture collection.

With Git and the .NET SDK installed, the following script publishes the app, clones or fast-forward pulls the public `DestinationHome/ImageArchive` master branch, then starts a metadata sync:

```powershell
.\Update-OfflineCatalogue.ps1
# Or choose a separate destination:
.\Update-OfflineCatalogue.ps1 -OutputDirectory .\bin\CatalogueViewer-Updated
```

This can download a large collection. It is unnecessary for compiling the source. The script refuses to overwrite a pre-existing image directory that is not a Git checkout; preserve that copy and choose a new output directory. Do not interrupt or relocate an active sync. Keep a separate backup of valuable local data.

## Command-line modes

```powershell
# Network operation; metadata only, progress written to Data\sync.log:
.\bin\CatalogueViewer-Release\DestinationHome.Catalogue.exe --sync-only --data-dir .\bin\CatalogueViewer-Release\Data

# Opens the UI, exercises existing local data and writes its QA artifacts:
.\bin\CatalogueViewer-Release\DestinationHome.Catalogue.exe --verify-offline --report-dir .\bin\Catalogue-QA
```

The offline verification mode uses the executable's own `Data` directory. It needs an existing snapshot, representative clothing entries and at least one resolvable local image. It checks UUID search, type filtering, all six palettes at three window sizes, appearance controls, preference round-trip, dropdown keyboard/popup behavior, long details and simulated 125%/150%/200% layout/text scaling. These checks are separate from compilation and physical monitor acceptance. See [validation](docs/VALIDATION.md).


## Icon and packaging

The supplied icon assets are ready to build. To regenerate the multi-size ICO from the unchanged master:

```powershell
.\Build-CatalogueIcon.ps1
```

After publishing to the default directory, prepare a runtime-only update and source archive without including offline data:

```powershell
.\Package-Catalogue.ps1 -SkipFullArchive
```

Output goes to ignored `Deliverables/`: `DestinationHome-Catalogue-UI-Update-Windows-x64.zip`, `DestinationHome-Catalogue-Source.zip`, an unpacked runtime directory and `Catalogue-SHA256.txt`. The update excludes `Data` and PDBs, so an existing snapshot, pictures and preferences remain separate. The source archive excludes Git/IDE state, build output, data directories, logs and personal IDE files. Use [CATALOGUE-UPDATE.txt](CATALOGUE-UPDATE.txt) for extraction instructions.

Without `-SkipFullArchive`, the script also creates `DestinationHome-Catalogue-Windows-x64.zip` from your local metadata and PNG image collection. That optional collection can be large and is not a source-control artifact. Neither packaging mode contacts the network. The separate update script does.


## Portable release package

After the self-contained publish, with your existing metadata snapshot at `bin/CatalogueViewer-Release/Data/catalogue.json`:

```powershell
.\Package-PortableRelease.ps1
```

`Deliverables/UUID-Catalogue-Windows-x64.zip` contains the supporting runtime, app, metadata snapshot and first-use instructions. It omits PDBs, picture archives, local logs and saved preferences. `-MetadataPath` can select another existing snapshot. This helper does not build the app or download data. It is separate from the optional full offline/image packaging. Release users need none of these build tools; see [installation](docs/INSTALLATION.md).
