param(
    [string]$MetadataPath = (Join-Path $PSScriptRoot 'bin\CatalogueViewer-Release\Data\catalogue.json')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$portableRuntime = Join-Path $PSScriptRoot 'bin\CatalogueViewer-Release'
$portableOutput = Join-Path $PSScriptRoot 'Deliverables\UUID-Catalogue-Windows-x64.zip'
if (!(Test-Path -LiteralPath (Join-Path $portableRuntime 'DestinationHome.Catalogue.exe'))) { throw 'Publish the self-contained catalogue first.' }
if (!(Test-Path -LiteralPath $MetadataPath)) { throw 'A locally obtained catalogue snapshot is required for this offline-ready package.' }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($portableOutput)) | Out-Null
$portableTemporary = $portableOutput + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
$portableStream = [IO.File]::Create($portableTemporary)
$portableArchive = [IO.Compression.ZipArchive]::new($portableStream, [IO.Compression.ZipArchiveMode]::Create)
function Add-PortableFile([string]$Source, [string]$Name) {
    $entry = $portableArchive.CreateEntry($Name.Replace('\', '/'), [IO.Compression.CompressionLevel]::Optimal)
    $input = [IO.File]::OpenRead($Source)
    $output = $entry.Open()
    try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
}
try {
    foreach ($file in [IO.Directory]::EnumerateFiles($portableRuntime, '*', [IO.SearchOption]::AllDirectories)) {
        $relative = [IO.Path]::GetRelativePath($portableRuntime, $file)
        if ($relative.StartsWith('Data\', [StringComparison]::OrdinalIgnoreCase) -or $relative.EndsWith('.pdb', [StringComparison]::OrdinalIgnoreCase)) { continue }
        Add-PortableFile $file ('UUID-Catalogue/' + $relative)
    }
    Add-PortableFile $MetadataPath 'UUID-Catalogue/Data/catalogue.json'
    Add-PortableFile (Join-Path $PSScriptRoot 'docs\INSTALLATION.md') 'UUID-Catalogue/START-HERE.md'
    Add-PortableFile (Join-Path $PSScriptRoot 'LICENSE-NOTICE.md') 'UUID-Catalogue/LICENSE-NOTICE.md'
} finally { $portableArchive.Dispose(); $portableStream.Dispose() }
[IO.File]::Move($portableTemporary, $portableOutput, $true)
Get-Item -LiteralPath $portableOutput | Select-Object Name, Length
