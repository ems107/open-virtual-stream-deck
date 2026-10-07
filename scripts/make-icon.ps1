# Generates server/src/OVSD.Host/ovsd.ico (same design as web/public/favicon.svg) with
# 16/24/32/48/64/256 px PNG frames. Run with Windows PowerShell or PowerShell 7 on Windows.
Add-Type -AssemblyName System.Drawing

$output = Join-Path $PSScriptRoot '..\server\src\OVSD.Host\ovsd.ico'
$sizes = 16, 24, 32, 48, 64, 256

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 64.0
    $fill = { param($color, $x, $y, $w, $h, $r)
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($color))
        $g.FillPath($brush, (New-RoundedRect ($x * $s) ($y * $s) ($w * $s) ($h * $s) ([Math]::Max(1, $r * $s))))
        $brush.Dispose()
    }
    & $fill '#171a21' 0 0 64 64 14
    & $fill '#5b8cff' 10 10 20 20 5
    & $fill '#3a4152' 34 10 20 20 5
    & $fill '#3a4152' 10 34 20 20 5
    & $fill '#3ecf8e' 34 34 20 20 5
    $g.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$stream.ToArray()
}

$frames = $sizes | ForEach-Object { ,(New-Frame $_) }
$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $out
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32)
    $writer.Write([UInt32]$frames[$i].Length); $writer.Write([UInt32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write($frame) }
$writer.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $output)).Path + '\ovsd.ico', $out.ToArray())
Write-Host "Wrote $output"
