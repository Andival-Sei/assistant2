Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$windowsAssets = Join-Path $repoRoot "windows-app\Assistant.WinUI\Assistant.WinUI\Assets"
$androidRes = Join-Path $repoRoot "android-app\app\src\main\res"

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Get-StarPoints {
    param(
        [float]$CenterX,
        [float]$CenterY,
        [float]$Arm
    )

    return [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($CenterX, $CenterY - $Arm),
        [System.Drawing.PointF]::new($CenterX + $Arm * 0.32, $CenterY - $Arm * 0.32),
        [System.Drawing.PointF]::new($CenterX + $Arm, $CenterY),
        [System.Drawing.PointF]::new($CenterX + $Arm * 0.32, $CenterY + $Arm * 0.32),
        [System.Drawing.PointF]::new($CenterX, $CenterY + $Arm),
        [System.Drawing.PointF]::new($CenterX - $Arm * 0.32, $CenterY + $Arm * 0.32),
        [System.Drawing.PointF]::new($CenterX - $Arm, $CenterY),
        [System.Drawing.PointF]::new($CenterX - $Arm * 0.32, $CenterY - $Arm * 0.32)
    )
}

function Draw-AppMark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [float]$Scale,
        [float]$OffsetX,
        [float]$OffsetY,
        [bool]$Monochrome = $false
    )

    $Graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $Graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    $inkColor = [System.Drawing.Color]::FromArgb(255, 19, 42, 68)
    $ringColor = if ($Monochrome) { $inkColor } else { [System.Drawing.Color]::FromArgb(255, 58, 210, 198) }
    $ringAccentColor = if ($Monochrome) { $inkColor } else { [System.Drawing.Color]::FromArgb(255, 100, 154, 255) }
    $coreFillColor = if ($Monochrome) { [System.Drawing.Color]::White } else { [System.Drawing.Color]::FromArgb(255, 248, 251, 250) }
    $coreStrokeColor = if ($Monochrome) { $inkColor } else { [System.Drawing.Color]::FromArgb(255, 25, 92, 116) }
    $starColor = if ($Monochrome) { $inkColor } else { [System.Drawing.Color]::FromArgb(255, 245, 136, 107) }
    $sparkColor = if ($Monochrome) { $inkColor } else { [System.Drawing.Color]::FromArgb(255, 255, 206, 122) }
    $shadowColor = [System.Drawing.Color]::FromArgb($(if ($Monochrome) { 28 } else { 52 }), 8, 18, 33)

    $ringShadowPen = [System.Drawing.Pen]::new($shadowColor, [float](56 * $Scale))
    $ringShadowPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $Graphics.DrawEllipse($ringShadowPen, ($OffsetX + 152 * $Scale), ($OffsetY + 152 * $Scale), (208 * $Scale), (208 * $Scale))
    $ringShadowPen.Dispose()

    $ringPen = [System.Drawing.Pen]::new($ringColor, [float](48 * $Scale))
    $ringPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $Graphics.DrawArc($ringPen, ($OffsetX + 152 * $Scale), ($OffsetY + 152 * $Scale), (208 * $Scale), (208 * $Scale), 132, 256)
    $ringPen.Dispose()

    $accentPen = [System.Drawing.Pen]::new($ringAccentColor, [float](48 * $Scale))
    $accentPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $Graphics.DrawArc($accentPen, ($OffsetX + 152 * $Scale), ($OffsetY + 152 * $Scale), (208 * $Scale), (208 * $Scale), 24, 104)
    $accentPen.Dispose()

    $corePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $corePath.AddEllipse(($OffsetX + 194 * $Scale), ($OffsetY + 194 * $Scale), (124 * $Scale), (124 * $Scale))
    $coreBrush = [System.Drawing.SolidBrush]::new($coreFillColor)
    $Graphics.FillPath($coreBrush, $corePath)
    $coreBrush.Dispose()

    $coreStroke = [System.Drawing.Pen]::new($coreStrokeColor, [float](18 * $Scale))
    $Graphics.DrawPath($coreStroke, $corePath)
    $coreStroke.Dispose()
    $corePath.Dispose()

    $starBrush = [System.Drawing.SolidBrush]::new($starColor)
    $Graphics.FillPolygon($starBrush, (Get-StarPoints -CenterX ($OffsetX + 256 * $Scale) -CenterY ($OffsetY + 256 * $Scale) -Arm (52 * $Scale)))
    $starBrush.Dispose()

    foreach ($point in @(
        @{ X = 256; Y = 132 },
        @{ X = 380; Y = 256 },
        @{ X = 256; Y = 380 },
        @{ X = 132; Y = 256 }
    )) {
        $nodeBrush = [System.Drawing.SolidBrush]::new($(if ($Monochrome) { $inkColor } else { $ringColor }))
        $Graphics.FillEllipse($nodeBrush, ($OffsetX + ($point.X - 18) * $Scale), ($OffsetY + ($point.Y - 18) * $Scale), (36 * $Scale), (36 * $Scale))
        $nodeBrush.Dispose()
    }

    $sparkBrush = [System.Drawing.SolidBrush]::new($sparkColor)
    $Graphics.FillPolygon($sparkBrush, (Get-StarPoints -CenterX ($OffsetX + 354 * $Scale) -CenterY ($OffsetY + 178 * $Scale) -Arm (24 * $Scale)))
    $sparkBrush.Dispose()
}

function New-Canvas {
    param([int]$Width, [int]$Height)

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    return @{
        Bitmap = $bitmap
        Graphics = $graphics
    }
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Draw-SquareIcon {
    param(
        [int]$Size,
        [string]$Path
    )

    $canvas = New-Canvas -Width $Size -Height $Size
    $bitmap = $canvas.Bitmap
    $graphics = $canvas.Graphics

    $radius = $Size * 0.28
    $bgPath = New-RoundedRectanglePath -X 0 -Y 0 -Width $Size -Height $Size -Radius $radius
    $bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new($Size * 0.04, $Size * 0.02),
        [System.Drawing.PointF]::new($Size * 0.94, $Size * 0.98),
        [System.Drawing.Color]::FromArgb(255, 243, 248, 255),
        [System.Drawing.Color]::FromArgb(255, 211, 226, 245)
    )
    $graphics.FillPath($bgBrush, $bgPath)
    $graphics.SetClip($bgPath)

    $shineBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new($Size * 0.1, $Size * 0.06),
        [System.Drawing.PointF]::new($Size * 0.58, $Size * 0.42),
        [System.Drawing.Color]::FromArgb(132, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(0, 255, 255, 255)
    )
    $graphics.FillEllipse($shineBrush, $Size * 0.02, $Size * -0.12, $Size * 0.88, $Size * 0.56)

    $panelPath = New-RoundedRectanglePath -X ($Size * 0.14) -Y ($Size * 0.14) -Width ($Size * 0.72) -Height ($Size * 0.72) -Radius ($Size * 0.22)
    $panelBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new($Size * 0.18, $Size * 0.12),
        [System.Drawing.PointF]::new($Size * 0.84, $Size * 0.84),
        [System.Drawing.Color]::FromArgb(255, 16, 34, 58),
        [System.Drawing.Color]::FromArgb(255, 12, 86, 123)
    )
    $graphics.FillPath($panelBrush, $panelPath)

    $panelHighlightBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(34, 255, 255, 255))
    $graphics.FillEllipse($panelHighlightBrush, $Size * 0.18, $Size * 0.16, $Size * 0.44, $Size * 0.18)
    $panelHighlightBrush.Dispose()

    $panelStroke = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(54, 255, 255, 255), [float]($Size * 0.008))
    $graphics.DrawPath($panelStroke, $panelPath)
    $panelStroke.Dispose()
    $panelBrush.Dispose()

    Draw-AppMark -Graphics $graphics -Scale ($Size / 512.0) -OffsetX 0 -OffsetY 0
    $graphics.ResetClip()

    Save-Png -Bitmap $bitmap -Path $Path

    $shineBrush.Dispose()
    $bgBrush.Dispose()
    $panelPath.Dispose()
    $bgPath.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Draw-UnplatedIcon {
    param(
        [int]$Size,
        [string]$Path
    )

    $canvas = New-Canvas -Width $Size -Height $Size
    $bitmap = $canvas.Bitmap
    $graphics = $canvas.Graphics
    Draw-AppMark -Graphics $graphics -Scale ($Size / 512.0) -OffsetX 0 -OffsetY 0 -Monochrome $true
    Save-Png -Bitmap $bitmap -Path $Path
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Draw-BannerIcon {
    param(
        [int]$Width,
        [int]$Height,
        [string]$Path,
        [bool]$Wide = $false
    )

    $canvas = New-Canvas -Width $Width -Height $Height
    $bitmap = $canvas.Bitmap
    $graphics = $canvas.Graphics

    $bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(0, 0),
        [System.Drawing.PointF]::new($Width, $Height),
        [System.Drawing.Color]::FromArgb(255, 243, 248, 255),
        [System.Drawing.Color]::FromArgb(255, 211, 226, 245)
    )
    $graphics.FillRectangle($bgBrush, 0, 0, $Width, $Height)

    $glowBrush = [System.Drawing.Drawing2D.PathGradientBrush]::new([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($Width * 0.12, $Height * 0.08),
        [System.Drawing.PointF]::new($Width * 0.9, $Height * 0.18),
        [System.Drawing.PointF]::new($Width * 0.82, $Height * 0.92),
        [System.Drawing.PointF]::new($Width * 0.06, $Height * 0.78)
    ))
    $glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(64, 255, 255, 255)
    $glowBrush.SurroundColors = [System.Drawing.Color[]]@([System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $graphics.FillRectangle($glowBrush, 0, 0, $Width, $Height)

    $iconSize = if ($Wide) { [Math]::Min($Height * 0.8, 220) } else { [Math]::Min($Height * 0.74, 220) }
    $offsetX = ($Width - $iconSize) / 2
    $offsetY = ($Height - $iconSize) / 2
    $panelPath = New-RoundedRectanglePath -X ($offsetX + $iconSize * 0.14) -Y ($offsetY + $iconSize * 0.14) -Width ($iconSize * 0.72) -Height ($iconSize * 0.72) -Radius ($iconSize * 0.22)
    $panelBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new($offsetX + $iconSize * 0.18, $offsetY + $iconSize * 0.12),
        [System.Drawing.PointF]::new($offsetX + $iconSize * 0.84, $offsetY + $iconSize * 0.84),
        [System.Drawing.Color]::FromArgb(255, 16, 34, 58),
        [System.Drawing.Color]::FromArgb(255, 12, 86, 123)
    )
    $graphics.FillPath($panelBrush, $panelPath)
    $panelStroke = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(48, 255, 255, 255), [float]($iconSize * 0.008))
    $graphics.DrawPath($panelStroke, $panelPath)
    $panelStroke.Dispose()
    $panelBrush.Dispose()
    $panelPath.Dispose()

    Draw-AppMark -Graphics $graphics -Scale ($iconSize / 512.0) -OffsetX $offsetX -OffsetY $offsetY

    Save-Png -Bitmap $bitmap -Path $Path

    $glowBrush.Dispose()
    $bgBrush.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Write-Ico {
    param(
        [string]$Path,
        [int[]]$Sizes
    )

    $frames = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $Sizes) {
        $tempPng = [System.IO.Path]::GetTempFileName()
        Remove-Item $tempPng
        $tempPng = "$tempPng.png"
        Draw-SquareIcon -Size $size -Path $tempPng
        $frames.Add([System.IO.File]::ReadAllBytes($tempPng))
        Remove-Item $tempPng
    }

    $stream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter($stream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$frames.Count)

        $offset = 6 + ($frames.Count * 16)
        for ($i = 0; $i -lt $frames.Count; $i++) {
            $size = $Sizes[$i]
            $bytes = $frames[$i]
            $entrySize = if ($size -ge 256) { 0 } else { $size }
            $writer.Write([byte]$entrySize)
            $writer.Write([byte]$entrySize)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$bytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $bytes.Length
        }

        for ($i = 0; $i -lt $frames.Count; $i++) {
            $writer.Write($frames[$i])
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Update-AndroidFallbacks {
    $fallbacks = @(
        @{ Folder = "mipmap-mdpi"; Size = 48 },
        @{ Folder = "mipmap-hdpi"; Size = 72 },
        @{ Folder = "mipmap-xhdpi"; Size = 96 },
        @{ Folder = "mipmap-xxhdpi"; Size = 144 },
        @{ Folder = "mipmap-xxxhdpi"; Size = 192 }
    )

    foreach ($fallback in $fallbacks) {
        $dir = Join-Path $androidRes $fallback.Folder
        Get-ChildItem $dir -Filter "ic_launcher*.webp" -ErrorAction SilentlyContinue | Remove-Item -Force
        Draw-SquareIcon -Size $fallback.Size -Path (Join-Path $dir "ic_launcher.png")

        $canvas = New-Canvas -Width $fallback.Size -Height $fallback.Size
        $bitmap = $canvas.Bitmap
        $graphics = $canvas.Graphics
        $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.PointF]::new(0, 0),
            [System.Drawing.PointF]::new($fallback.Size, $fallback.Size),
            [System.Drawing.Color]::FromArgb(255, 243, 248, 255),
            [System.Drawing.Color]::FromArgb(255, 211, 226, 245)
        )
        $graphics.FillEllipse($brush, 0, 0, $fallback.Size, $fallback.Size)
        Draw-AppMark -Graphics $graphics -Scale ($fallback.Size / 512.0) -OffsetX 0 -OffsetY 0
        Save-Png -Bitmap $bitmap -Path (Join-Path $dir "ic_launcher_round.png")
        $brush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

Draw-SquareIcon -Size 88 -Path (Join-Path $windowsAssets "Square44x44Logo.scale-200.png")
Draw-UnplatedIcon -Size 24 -Path (Join-Path $windowsAssets "Square44x44Logo.targetsize-24_altform-unplated.png")
Draw-SquareIcon -Size 300 -Path (Join-Path $windowsAssets "Square150x150Logo.scale-200.png")
Draw-SquareIcon -Size 50 -Path (Join-Path $windowsAssets "StoreLogo.png")
Draw-BannerIcon -Width 620 -Height 300 -Path (Join-Path $windowsAssets "Wide310x150Logo.scale-200.png") -Wide $true
Draw-BannerIcon -Width 1240 -Height 600 -Path (Join-Path $windowsAssets "SplashScreen.scale-200.png")
Draw-SquareIcon -Size 48 -Path (Join-Path $windowsAssets "LockScreenLogo.scale-200.png")
Write-Ico -Path (Join-Path $windowsAssets "AppIcon.ico") -Sizes @(16, 24, 32, 48, 64, 128, 256)
Update-AndroidFallbacks
