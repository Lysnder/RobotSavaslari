param(
    [Parameter(Mandatory = $true)][string]$FramesDirectory,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [int]$FramesPerSecond = 5
)
# Encodes the optional -bot-evidence-dir PNG sequence without an external encoder.
# MJPEG AVI, one video stream; playback cadence is nominal (captures use unscaled time).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$frames = @(Get-ChildItem -LiteralPath $FramesDirectory -Filter 'frame-*.png' | Sort-Object Name)
if ($frames.Count -eq 0) { throw 'No captured frames found.' }
if ($FramesPerSecond -lt 1) { throw 'FramesPerSecond must be positive.' }
$firstImage = [Drawing.Image]::FromFile($frames[0].FullName)
$videoWidth = $firstImage.Width
$videoHeight = $firstImage.Height
$firstImage.Dispose()
$stream = [IO.File]::Open([IO.Path]::GetFullPath($OutputPath), [IO.FileMode]::Create)
$writer = New-Object IO.BinaryWriter($stream)
function FourCC([string]$value) { $writer.Write([Text.Encoding]::ASCII.GetBytes($value)) }
function U32([uint32]$value) { $writer.Write($value) }
function BeginChunk([string]$id) {
    FourCC $id
    $sizePosition = $stream.Position
    U32 0
    return $sizePosition
}
function EndChunk([long]$sizePosition) {
    $end = $stream.Position
    $stream.Position = $sizePosition
    U32 ($end - $sizePosition - 4)
    $stream.Position = $end
    if (($end % 2) -ne 0) { $writer.Write([byte]0) }
}
try {
    $riff = BeginChunk 'RIFF'; FourCC 'AVI '
    $header = BeginChunk 'LIST'; FourCC 'hdrl'
    $main = BeginChunk 'avih'
    U32 ([Math]::Round(1000000 / $FramesPerSecond)); U32 0; U32 0; U32 16
    U32 $frames.Count; U32 0; U32 1; U32 ($videoWidth * $videoHeight * 3)
    U32 $videoWidth; U32 $videoHeight
    1..4 | ForEach-Object { U32 0 }
    EndChunk $main
    $videoStream = BeginChunk 'LIST'; FourCC 'strl'
    $streamHeader = BeginChunk 'strh'
    FourCC 'vids'; FourCC 'MJPG'; U32 0
    $writer.Write([uint16]0); $writer.Write([uint16]0)
    U32 0; U32 1; U32 $FramesPerSecond; U32 0; U32 $frames.Count
    U32 ($videoWidth * $videoHeight * 3); U32 ([uint32]::MaxValue); U32 0
    $writer.Write([int16]0); $writer.Write([int16]0)
    $writer.Write([int16]$videoWidth); $writer.Write([int16]$videoHeight)
    EndChunk $streamHeader
    $format = BeginChunk 'strf'
    U32 40; $writer.Write([int32]$videoWidth); $writer.Write([int32]$videoHeight)
    $writer.Write([uint16]1); $writer.Write([uint16]24); FourCC 'MJPG'
    U32 ($videoWidth * $videoHeight * 3)
    1..4 | ForEach-Object { U32 0 }
    EndChunk $format; EndChunk $videoStream; EndChunk $header
    $movie = BeginChunk 'LIST'; $movieOrigin = $stream.Position; FourCC 'movi'
    $index = New-Object 'Collections.Generic.List[object]'
    foreach ($frame in $frames) {
        $bitmap = [Drawing.Image]::FromFile($frame.FullName)
        $buffer = New-Object IO.MemoryStream
        try {
            if ($bitmap.Width -ne $videoWidth -or $bitmap.Height -ne $videoHeight) { throw 'Frame dimensions changed.' }
            $bitmap.Save($buffer, [Drawing.Imaging.ImageFormat]::Jpeg)
            $bytes = $buffer.ToArray()
            $offset = $stream.Position - $movieOrigin
            $chunk = BeginChunk '00dc'
            $writer.Write($bytes)
            EndChunk $chunk
            $index.Add(@([uint32]$offset, [uint32]$bytes.Length))
        }
        finally { $bitmap.Dispose(); $buffer.Dispose() }
    }
    EndChunk $movie
    $indexChunk = BeginChunk 'idx1'
    foreach ($entry in $index) { FourCC '00dc'; U32 16; U32 $entry[0]; U32 $entry[1] }
    EndChunk $indexChunk; EndChunk $riff
}
finally { $writer.Dispose(); $stream.Dispose() }
Write-Output "Encoded $($frames.Count) frames at $FramesPerSecond fps: $OutputPath"
