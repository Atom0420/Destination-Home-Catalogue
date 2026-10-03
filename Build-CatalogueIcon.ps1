param(
    [string]$Source = (Join-Path $PSScriptRoot 'Assets\catalogue-icon.png'),
    [string]$Destination = (Join-Path $PSScriptRoot 'Assets\catalogue.ico')
)

# Format conversion only: keep the generated master artwork unchanged.
Add-Type -AssemblyName System.Drawing
$sourceImage = [System.Drawing.Image]::FromFile($Source)
$frames = [System.Collections.Generic.List[object]]::new()
try {
    if ($sourceImage.Width -ne $sourceImage.Height) { throw 'The icon master must be square.' }
    foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.DrawImage($sourceImage, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add([pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() })
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $output = [System.IO.File]::Create($Destination)
    $writer = [System.IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    } finally { $writer.Dispose() }
} finally { $sourceImage.Dispose() }
Get-Item -LiteralPath $Destination | Select-Object FullName, Length
