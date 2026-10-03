param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin\CatalogueViewer-Release'))

$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
dotnet publish (Join-Path $PSScriptRoot 'CatalogueViewer.csproj') -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'The catalogue viewer build failed.' }

$archiveDirectory = Join-Path $OutputDirectory 'Data\ImageArchive'
if (Test-Path (Join-Path $archiveDirectory '.git')) {
    git -C $archiveDirectory pull --ff-only
} elseif (!(Test-Path $archiveDirectory)) {
    git clone --depth 1 --single-branch --branch master https://github.com/DestinationHome/ImageArchive.git $archiveDirectory
} else {
    throw 'The existing image folder is not a Git checkout. Keep that offline copy and use a new output directory to refresh all images.'
}
if ($LASTEXITCODE -ne 0) { throw 'The image archive download failed.' }

$viewer = Join-Path $OutputDirectory 'DestinationHome.Catalogue.exe'
$dataDirectory = Join-Path $OutputDirectory 'Data'
$syncProcess = Start-Process -FilePath $viewer -ArgumentList @('--sync-only', '--data-dir', ('"' + $dataDirectory + '"')) -WindowStyle Hidden -PassThru -Wait
if ($syncProcess.ExitCode -ne 0) { throw ('Metadata download failed. See ' + (Join-Path $dataDirectory 'sync.log')) }
Write-Host ('Offline catalogue ready: ' + $viewer)
