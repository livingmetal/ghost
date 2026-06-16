param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePng,

    [Parameter(Mandatory = $true)]
    [string]$OutputIco
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $SourcePng)) {
    throw "Source PNG not found: $SourcePng"
}

Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName PresentationCore

function New-PngBytes {
    param(
        [System.Windows.Media.Imaging.BitmapSource]$Source,
        [int]$Size
    )

    $scaleX = $Size / $Source.PixelWidth
    $scaleY = $Size / $Source.PixelHeight

    $transform = New-Object System.Windows.Media.ScaleTransform($scaleX, $scaleY)
    $bitmap = New-Object System.Windows.Media.Imaging.TransformedBitmap
    $bitmap.BeginInit()
    $bitmap.Source = $Source
    $bitmap.Transform = $transform
    $bitmap.EndInit()
    $bitmap.Freeze()

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    return $stream.ToArray()
}

$image = New-Object System.Windows.Media.Imaging.BitmapImage
$image.BeginInit()
$image.UriSource = [Uri](Resolve-Path -LiteralPath $SourcePng).Path
$image.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
$image.CreateOptions = [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat
$image.EndInit()
$image.Freeze()

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$entries = foreach ($size in $sizes) {
    [PSCustomObject]@{
        Size = $size
        Bytes = New-PngBytes -Source $image -Size $size
    }
}

$outputDirectory = Split-Path -Parent $OutputIco
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$fileStream = [System.IO.File]::Create($OutputIco)
try {
    $writer = New-Object System.IO.BinaryWriter($fileStream)

    # ICONDIR
    $writer.Write([UInt16]0) # reserved
    $writer.Write([UInt16]1) # icon
    $writer.Write([UInt16]$entries.Count)

    $imageOffset = 6 + ($entries.Count * 16)
    foreach ($entry in $entries) {
        $sizeByte = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$sizeByte)      # width
        $writer.Write([byte]$sizeByte)      # height
        $writer.Write([byte]0)              # palette colors
        $writer.Write([byte]0)              # reserved
        $writer.Write([UInt16]1)            # planes
        $writer.Write([UInt16]32)           # bit count
        $writer.Write([UInt32]$entry.Bytes.Length)
        $writer.Write([UInt32]$imageOffset)
        $imageOffset += $entry.Bytes.Length
    }

    foreach ($entry in $entries) {
        $writer.Write([byte[]]$entry.Bytes)
    }
}
finally {
    $fileStream.Dispose()
}

Write-Host "Generated icon: $OutputIco"
