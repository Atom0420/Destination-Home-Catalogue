param([switch]$SkipFullArchive)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$projectRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$releaseRoot = Join-Path $projectRoot 'bin\CatalogueViewer-Release'
$deliveryRoot = Join-Path $projectRoot 'Deliverables'
$prefix = 'DestinationHome-Catalogue/'
if (!(Test-Path -LiteralPath (Join-Path $releaseRoot 'DestinationHome.Catalogue.exe'))) { throw 'Publish the catalogue first.' }
[System.IO.Directory]::CreateDirectory($deliveryRoot) | Out-Null

function Add-ArchiveFile($Archive, [string]$Source, [string]$EntryName) {
    $entry = $Archive.CreateEntry($EntryName.Replace('\', '/'), [System.IO.Compression.CompressionLevel]::Fastest)
    $inputStream = [System.IO.File]::OpenRead($Source)
    $entryStream = $entry.Open()
    try { $inputStream.CopyTo($entryStream) } finally { $entryStream.Dispose(); $inputStream.Dispose() }
}

function Write-Archive([string]$Destination, [scriptblock]$Contents) {
    # Write a fresh archive with bounded memory, then atomically replace the prior deliverable.
    $temporary = $Destination + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    $fileStream = [System.IO.File]::Create($temporary)
    $archive = [System.IO.Compression.ZipArchive]::new($fileStream, [System.IO.Compression.ZipArchiveMode]::Create)
    try { & $Contents $archive } finally { $archive.Dispose(); $fileStream.Dispose() }
    [System.IO.File]::Move($temporary, $Destination, $true)
    Get-Item -LiteralPath $Destination | Select-Object Name, Length
}

$runtimeFiles = @([System.IO.Directory]::EnumerateFiles($releaseRoot, '*', [System.IO.SearchOption]::AllDirectories) | Where-Object {
    $relative = [System.IO.Path]::GetRelativePath($releaseRoot, $_)
    !$relative.StartsWith('Data\', [System.StringComparison]::OrdinalIgnoreCase) -and !$_.EndsWith('.pdb', [System.StringComparison]::OrdinalIgnoreCase)
})

# Refresh the unpacked runtime without touching its existing offline data directory.
$unpackedRoot = Join-Path $deliveryRoot 'DestinationHome-Catalogue-Windows-x64\DestinationHome-Catalogue'
[System.IO.Directory]::CreateDirectory($unpackedRoot) | Out-Null
foreach ($source in $runtimeFiles) {
    $destination = Join-Path $unpackedRoot ([System.IO.Path]::GetRelativePath($releaseRoot, $source))
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destination)) | Out-Null
    [System.IO.File]::Copy($source, $destination, $true)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CATALOGUE-UPDATE.txt') -Destination $unpackedRoot

$updatePath = Join-Path $deliveryRoot 'DestinationHome-Catalogue-UI-Update-Windows-x64.zip'
Write-Archive $updatePath {
    param($archive)
    foreach ($source in $runtimeFiles) { Add-ArchiveFile $archive $source ([System.IO.Path]::GetRelativePath($releaseRoot, $source)) }
    Add-ArchiveFile $archive (Join-Path $PSScriptRoot 'CATALOGUE-UPDATE.txt') 'CATALOGUE-UPDATE.txt'
}

$sourcePath = Join-Path $deliveryRoot 'DestinationHome-Catalogue-Source.zip'
Write-Archive $sourcePath {
    param($archive)
    foreach ($source in [System.IO.Directory]::EnumerateFiles($projectRoot, '*', [System.IO.SearchOption]::AllDirectories)) {
        $relative = [System.IO.Path]::GetRelativePath($projectRoot, $source)
        if ($relative -match '(^|\\)(bin|obj|\.vs|\.vscode|\.git|Deliverables|Data|ImageArchive)(\\|$)' -or $relative -match '\.(user|suo|log|tmp|dmp|pdb)$') { continue }
        Add-ArchiveFile $archive $source $relative
    }
}

if (!$SkipFullArchive) {
    Write-Archive (Join-Path $deliveryRoot 'DestinationHome-Catalogue-Windows-x64.zip') {
        param($archive)
        foreach ($source in $runtimeFiles) { Add-ArchiveFile $archive $source ($prefix + [System.IO.Path]::GetRelativePath($releaseRoot, $source)) }
        Add-ArchiveFile $archive (Join-Path $PSScriptRoot 'CATALOGUE-UPDATE.txt') ($prefix + 'CATALOGUE-UPDATE.txt')
        Add-ArchiveFile $archive (Join-Path $projectRoot 'README.md') ($prefix + 'README.md')
        Add-ArchiveFile $archive (Join-Path $releaseRoot 'Data\catalogue.json') ($prefix + 'Data/catalogue.json')
        $imageRoot = Join-Path $releaseRoot 'Data\ImageArchive'
        $images = 0
        foreach ($source in [System.IO.Directory]::EnumerateFiles($imageRoot, '*.png', [System.IO.SearchOption]::AllDirectories)) {
            Add-ArchiveFile $archive $source ($prefix + 'Data/ImageArchive/' + [System.IO.Path]::GetRelativePath($imageRoot, $source))
            $images++
        }
        "Included $images local PNG pictures."
    }
}

$hashFiles = @((Join-Path $releaseRoot 'DestinationHome.Catalogue.exe'), (Join-Path $releaseRoot 'DestinationHome.Catalogue.dll'), $updatePath, $sourcePath)
if (!$SkipFullArchive) { $hashFiles += Join-Path $deliveryRoot 'DestinationHome-Catalogue-Windows-x64.zip' }
$hashLines = @($hashFiles | ForEach-Object { $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; "$($hash.Hash)  $([System.IO.Path]::GetFileName($_))" })
[System.IO.File]::WriteAllLines((Join-Path $deliveryRoot 'Catalogue-SHA256.txt'), $hashLines)
