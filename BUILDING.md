# Build, deployment and data

The project targets Windows x64, `net8.0-windows`, Windows Forms and the .NET 8 API surface. Install a Windows .NET SDK that supports this target. Visual Studio with .NET desktop development is optional. The source has no project references or external NuGet package references. `NuGetAudit` is disabled in the preserved project; compilation is not a dependency security audit.

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

The offline verification mode uses the executable's own `Data` directory. It needs an existing snapshot, representative clothing entries and at least one resolvable local image. It checks UUID search, type filtering and three window sizes; it is separate from build evidence. See [validation](docs/VALIDATION.md).
