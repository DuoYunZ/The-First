Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"

$outDir = Join-Path (Get-Location) "Assets\Resources\UI\DemoCodexCutout"
if (!(Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

function New-Color([int]$r, [int]$g, [int]$b, [int]$a = 255) {
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-BevelRect($g, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r, $topColor, $bottomColor, $borderColor, [float]$border = 6) {
    $path = New-RoundedPath $x $y $w $h $r
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.RectangleF($x, $y, $w, $h)),
        $topColor,
        $bottomColor,
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($brush, $path)
    $brush.Dispose()

    $pen = New-Object System.Drawing.Pen($borderColor, $border)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path)
    $pen.Dispose()

    $hi = New-Object System.Drawing.Pen((New-Color 255 235 174 88), [Math]::Max(1, $border / 2))
    $g.DrawLine($hi, $x + $r, $y + $border, $x + $w - $r, $y + $border)
    $hi.Dispose()

    $path.Dispose()
}

function Draw-Noise($g, [int]$x, [int]$y, [int]$w, [int]$h, [int]$count, $color, [int]$seed = 1107) {
    $rand = New-Object System.Random($seed)
    $brush = New-Object System.Drawing.SolidBrush($color)
    for ($i = 0; $i -lt $count; $i++) {
        $px = $x + $rand.Next(0, $w)
        $py = $y + $rand.Next(0, $h)
        $size = $rand.Next(1, 4)
        $g.FillEllipse($brush, $px, $py, $size, $size)
    }
    $brush.Dispose()
}

function Draw-BrushRect($g, [float]$x, [float]$y, [float]$w, [float]$h, $topColor, $bottomColor, $edgeColor) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]($x + 7), [float]($y + 3)),
        [System.Drawing.PointF]::new([float]($x + $w - 11), [float]($y + 1)),
        [System.Drawing.PointF]::new([float]($x + $w - 2), [float]($y + 8)),
        [System.Drawing.PointF]::new([float]($x + $w - 8), [float]($y + $h - 5)),
        [System.Drawing.PointF]::new([float]($x + 11), [float]($y + $h)),
        [System.Drawing.PointF]::new([float]($x + 1), [float]($y + $h - 8))
    )
    $path.AddPolygon($points)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.RectangleF($x, $y, $w, $h)),
        $topColor,
        $bottomColor,
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($brush, $path)
    $brush.Dispose()
    $pen = New-Object System.Drawing.Pen($edgeColor, 3)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path)
    $pen.Dispose()
    $path.Dispose()
}

function Draw-SoftShadow($g, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r, [int]$alpha = 80) {
    for ($i = 0; $i -lt 5; $i++) {
        $path = New-RoundedPath ($x - $i) ($y + 3 + $i) ($w + $i * 2) ($h + $i * 2) ($r + $i)
        $brush = New-Object System.Drawing.SolidBrush((New-Color 0 0 0 ([Math]::Max(0, $alpha - $i * 14))))
        $g.FillPath($brush, $path)
        $brush.Dispose()
        $path.Dispose()
    }
}

function Draw-InsetLine($g, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r, $color, [float]$width = 2) {
    $path = New-RoundedPath $x $y $w $h $r
    $pen = New-Object System.Drawing.Pen($color, $width)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path)
    $pen.Dispose()
    $path.Dispose()
}

function Draw-Screw($g, [float]$x, [float]$y, [float]$size = 9) {
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.RectangleF($x, $y, $size, $size)),
        (New-Color 205 140 58),
        (New-Color 83 45 24),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillEllipse($brush, $x, $y, $size, $size)
    $brush.Dispose()
    $pen = New-Object System.Drawing.Pen((New-Color 42 25 15 150), 1.4)
    $g.DrawLine($pen, $x + 2, $y + $size / 2, $x + $size - 2, $y + $size / 2)
    $pen.Dispose()
}

function Draw-TabNotch($g, [float]$x, [float]$y, [float]$w, [float]$h, $topColor, $bottomColor, $borderColor) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]($x + 9), [float]($y + 2)),
        [System.Drawing.PointF]::new([float]($x + $w - 9), [float]($y + 2)),
        [System.Drawing.PointF]::new([float]($x + $w - 2), [float]($y + 10)),
        [System.Drawing.PointF]::new([float]($x + $w - 8), [float]($y + $h - 5)),
        [System.Drawing.PointF]::new([float]($x + 8), [float]($y + $h - 5)),
        [System.Drawing.PointF]::new([float]($x + 2), [float]($y + 10))
    )
    $path.AddPolygon($points)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.RectangleF($x, $y, $w, $h)),
        $topColor,
        $bottomColor,
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($brush, $path)
    $brush.Dispose()
    $pen = New-Object System.Drawing.Pen($borderColor, 4)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path)
    $pen.Dispose()
    $hi = New-Object System.Drawing.Pen((New-Color 255 223 137 96), 2)
    $g.DrawLine($hi, $x + 16, $y + 10, $x + $w - 20, $y + 10)
    $hi.Dispose()
    $path.Dispose()
}

function Draw-CardBase($g, [int]$x, [int]$y, [int]$w, [int]$h, $accent, [bool]$locked = $false) {
    Draw-SoftShadow $g ($x + 3) ($y + 4) ($w - 6) ($h - 7) 7 78
    $outerTop = if ($locked) { New-Color 34 30 27 } else { New-Color 74 55 37 }
    $outerBottom = if ($locked) { New-Color 16 15 14 } else { New-Color 31 26 23 }
    Draw-BevelRect $g ($x + 5) ($y + 5) ($w - 10) ($h - 10) 6 $outerTop $outerBottom (New-Color 29 19 13) 4
    Draw-InsetLine $g ($x + 10) ($y + 10) ($w - 20) ($h - 20) 3 (New-Color 218 148 69 115) 2
    Draw-InsetLine $g ($x + 14) ($y + 14) ($w - 28) 58 2 (New-Color 33 25 20 205) 3

    $wellTop = if ($locked) { New-Color 15 14 13 } else { New-Color 47 39 33 }
    $wellBottom = if ($locked) { New-Color 8 8 8 } else { New-Color 25 23 21 }
    Draw-BevelRect $g ($x + 18) ($y + 18) ($w - 36) 51 3 $wellTop $wellBottom (New-Color 16 12 10 220) 2

    $accentBrush = New-Object System.Drawing.SolidBrush($accent)
    $g.FillRectangle($accentBrush, $x + 8, $y + 9, 7, $h - 18)
    $accentBrush.Dispose()

    $bottomBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        ([System.Drawing.RectangleF]::new([float]($x + 14), [float]($y + 75), [float]($w - 28), 38)),
        (New-Color 52 38 28 196),
        (New-Color 30 25 22 210),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($bottomBrush, $x + 14, $y + 75, $w - 28, 38)
    $bottomBrush.Dispose()
    $sep = New-Object System.Drawing.Pen((New-Color 227 148 54 72), 1.5)
    $g.DrawLine($sep, $x + 17, $y + 76, $x + $w - 18, $y + 76)
    $sep.Dispose()

    $starBrush = New-Object System.Drawing.SolidBrush((New-Color 226 166 66 185))
    for ($s = 0; $s -lt 3; $s++) {
        $cx = $x + 36 + $s * 15
        $cy = $y + $h - 18
        $g.FillPolygon($starBrush, [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($cx, $cy - 5),
            [System.Drawing.PointF]::new($cx + 2, $cy - 1),
            [System.Drawing.PointF]::new($cx + 6, $cy),
            [System.Drawing.PointF]::new($cx + 2, $cy + 2),
            [System.Drawing.PointF]::new($cx + 3, $cy + 6),
            [System.Drawing.PointF]::new($cx, $cy + 3),
            [System.Drawing.PointF]::new($cx - 3, $cy + 6),
            [System.Drawing.PointF]::new($cx - 2, $cy + 2),
            [System.Drawing.PointF]::new($cx - 6, $cy),
            [System.Drawing.PointF]::new($cx - 2, $cy - 1)
        ))
    }
    $starBrush.Dispose()

    if ($locked) {
        $fogBrush = New-Object System.Drawing.SolidBrush((New-Color 0 0 0 108))
        $g.FillRectangle($fogBrush, $x + 10, $y + 10, $w - 20, $h - 20)
        $fogBrush.Dispose()
    }

    Draw-Noise $g $x $y $w $h 58 (New-Color 255 222 138 16) 6132
}

function Write-SpriteMeta($pngPath, [int]$maxTextureSize = 4096) {
    $metaPath = "$pngPath.meta"
    $guid = [guid]::NewGuid().ToString("N")
    $spriteId = [guid]::NewGuid().ToString("N").Substring(0, 24) + "00000000"
    if (Test-Path $metaPath) {
        $existingGuid = Select-String -LiteralPath $metaPath -Pattern '^guid:\s+([a-fA-F0-9]+)' | Select-Object -First 1
        if ($existingGuid -and $existingGuid.Matches.Count -gt 0) {
            $guid = $existingGuid.Matches[0].Groups[1].Value
        }

        $existingSpriteId = Select-String -LiteralPath $metaPath -Pattern 'spriteID:\s+([a-fA-F0-9]+)' | Select-Object -First 1
        if ($existingSpriteId -and $existingSpriteId.Matches.Count -gt 0) {
            $spriteId = $existingSpriteId.Matches[0].Groups[1].Value
        }
    }
    $meta = @"
fileFormatVersion: 2
guid: $guid
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: $maxTextureSize
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: $maxTextureSize
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: $spriteId
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
    Set-Content -LiteralPath $metaPath -Value $meta -Encoding UTF8
}

function Save-Crop($atlas, [string]$name, [int]$x, [int]$y, [int]$w, [int]$h) {
    $crop = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $dest = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $src = New-Object System.Drawing.Rectangle($x, $y, $w, $h)
    $g.DrawImage($atlas, $dest, $src, [System.Drawing.GraphicsUnit]::Pixel)
    $path = Join-Path $outDir "$name.png"
    $crop.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $crop.Dispose()
    Write-SpriteMeta $path
}

$atlas = New-Object System.Drawing.Bitmap(4096, 4096, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($atlas)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

$rects = [ordered]@{
    codex_book_frame = @(0, 0, 1560, 860)
    codex_page_left = @(1580, 0, 720, 790)
    codex_page_right = @(2320, 0, 720, 790)
    codex_spine = @(3060, 0, 44, 790)
    codex_detail_panel = @(0, 900, 638, 172)
    codex_icon_frame = @(660, 900, 148, 148)
    codex_card_weapon = @(830, 900, 104, 128)
    codex_card_passive = @(950, 900, 104, 128)
    codex_card_locked = @(1070, 900, 104, 128)
    codex_card_selected = @(1190, 900, 116, 140)
    codex_stat_slot = @(1320, 900, 292, 56)
    codex_recommend_frame = @(1630, 900, 58, 58)
    codex_tab = @(1700, 900, 230, 64)
    codex_close_button = @(1950, 900, 72, 72)
    codex_header_plaque = @(2660, 900, 380, 78)
    codex_pumpkin_badge = @(3060, 900, 72, 72)
    skill_connector_gold = @(2040, 900, 256, 32)
    skill_connector_locked = @(2040, 950, 256, 32)
    skill_node_basic = @(2320, 900, 128, 128)
    skill_node_capstone = @(2470, 900, 180, 180)
    skill_tree_panel_frame = @(0, 1120, 1480, 700)
    skill_popup_panel = @(1500, 1120, 520, 300)
}

# Component language copied from the annotated reference:
# A book shell/leather stack, B parchment pages, C center spine, D title plaque,
# E category tabs, F collection cards, G detail hero panel, H stat/recommend chips.
$r = $rects.codex_book_frame
Draw-SoftShadow $g ($r[0] + 24) ($r[1] + 26) ($r[2] - 48) ($r[3] - 46) 26 120
Draw-BevelRect $g $r[0] $r[1] $r[2] $r[3] 26 (New-Color 86 45 28) (New-Color 28 15 12) (New-Color 20 10 8) 18
Draw-InsetLine $g ($r[0] + 22) ($r[1] + 22) ($r[2] - 44) ($r[3] - 44) 16 (New-Color 177 99 44 125) 4
Draw-BevelRect $g ($r[0] + 42) ($r[1] + 44) ($r[2] - 84) ($r[3] - 88) 13 (New-Color 133 76 42 226) (New-Color 57 30 22 236) (New-Color 45 23 15) 8
$pageStackPen = New-Object System.Drawing.Pen((New-Color 214 159 88 118), 3)
for ($i = 0; $i -lt 11; $i++) {
    $g.DrawArc($pageStackPen, $r[0] + 2 + $i * 5, $r[1] + 70 + $i * 7, 150, $r[3] - 140 - $i * 13, 91, 178)
    $g.DrawArc($pageStackPen, $r[0] + $r[2] - 152 - $i * 5, $r[1] + 70 + $i * 7, 150, $r[3] - 140 - $i * 13, -89, 178)
}
$pageStackPen.Dispose()
$edgePen = New-Object System.Drawing.Pen((New-Color 35 18 12 175), 5)
$g.DrawLine($edgePen, $r[0] + 72, $r[1] + 48, $r[0] + $r[2] - 72, $r[1] + 48)
$g.DrawLine($edgePen, $r[0] + 72, $r[1] + $r[3] - 48, $r[0] + $r[2] - 72, $r[1] + $r[3] - 48)
$edgePen.Dispose()
$capBrush = New-Object System.Drawing.SolidBrush((New-Color 97 55 30 230))
$capPen = New-Object System.Drawing.Pen((New-Color 226 151 66 170), 4)
foreach ($corner in @(
    @(($r[0] + 10), ($r[1] + 12)),
    @(($r[0] + $r[2] - 88), ($r[1] + 12)),
    @(($r[0] + 10), ($r[1] + $r[3] - 88)),
    @(($r[0] + $r[2] - 88), ($r[1] + $r[3] - 88))
)) {
    $g.FillRectangle($capBrush, $corner[0], $corner[1], 78, 78)
    $g.DrawRectangle($capPen, $corner[0] + 7, $corner[1] + 7, 64, 64)
    Draw-Screw $g ($corner[0] + 15) ($corner[1] + 15) 10
    Draw-Screw $g ($corner[0] + 53) ($corner[1] + 53) 10
}
$capBrush.Dispose()
$capPen.Dispose()
Draw-Noise $g $r[0] $r[1] $r[2] $r[3] 620 (New-Color 255 210 118 14) 4101

foreach ($pageName in @("codex_page_left", "codex_page_right")) {
    $r = $rects[$pageName]
    Draw-SoftShadow $g ($r[0] + 10) ($r[1] + 13) ($r[2] - 20) ($r[3] - 22) 9 72
    Draw-BevelRect $g $r[0] $r[1] $r[2] $r[3] 8 (New-Color 211 160 86) (New-Color 156 93 48) (New-Color 91 48 26) 9
    Draw-BevelRect $g ($r[0] + 18) ($r[1] + 22) ($r[2] - 36) ($r[3] - 44) 6 (New-Color 255 232 166) (New-Color 221 176 101) (New-Color 177 109 48 138) 4
    Draw-InsetLine $g ($r[0] + 34) ($r[1] + 40) ($r[2] - 68) ($r[3] - 80) 2 (New-Color 123 73 35 145) 2
    Draw-InsetLine $g ($r[0] + 48) ($r[1] + 54) ($r[2] - 96) ($r[3] - 108) 1 (New-Color 255 239 176 55) 2
    $wash = New-Object System.Drawing.SolidBrush((New-Color 255 242 190 32))
    $g.FillEllipse($wash, $r[0] + 80, $r[1] + 54, 410, 230)
    $g.FillEllipse($wash, $r[0] + 210, $r[1] + 400, 340, 270)
    $wash.Dispose()
    $edge = New-Object System.Drawing.Pen((New-Color 122 72 34 52), 1)
    for ($yy = $r[1] + 62; $yy -lt $r[1] + $r[3] - 50; $yy += 36) {
        $g.DrawLine($edge, $r[0] + 42, $yy, $r[0] + $r[2] - 43, $yy + 2)
    }
    $edge.Dispose()
    Draw-Noise $g $r[0] $r[1] $r[2] $r[3] 540 (New-Color 93 52 23 23) 4102
}

$r = $rects.codex_spine
Draw-BevelRect $g ($r[0] + 5) $r[1] ($r[2] - 10) $r[3] 3 (New-Color 92 48 29) (New-Color 45 24 16) (New-Color 45 22 14) 4
$spinePen = New-Object System.Drawing.Pen((New-Color 238 172 69 120), 2)
for ($y = $r[1] + 32; $y -lt ($r[1] + $r[3]); $y += 58) {
    $g.DrawArc($spinePen, $r[0] - 8, $y, $r[2] + 16, 26, 0, 180)
}
$spinePen.Dispose()

$r = $rects.codex_detail_panel
Draw-SoftShadow $g $r[0] ($r[1] + 3) $r[2] ($r[3] - 4) 8 72
Draw-BevelRect $g $r[0] $r[1] $r[2] $r[3] 8 (New-Color 91 62 39) (New-Color 35 25 22) (New-Color 90 48 24) 6
Draw-BevelRect $g ($r[0] + 14) ($r[1] + 14) ($r[2] - 28) ($r[3] - 28) 4 (New-Color 68 47 34 216) (New-Color 38 29 25 226) (New-Color 213 141 61 72) 2
Draw-InsetLine $g ($r[0] + 26) ($r[1] + 26) ($r[2] - 52) ($r[3] - 52) 2 (New-Color 255 222 141 28) 1
Draw-Noise $g $r[0] $r[1] $r[2] $r[3] 96 (New-Color 255 209 122 20) 4103

$r = $rects.codex_icon_frame
Draw-SoftShadow $g ($r[0] + 4) ($r[1] + 5) ($r[2] - 8) ($r[3] - 10) 6 90
Draw-BevelRect $g $r[0] $r[1] $r[2] $r[3] 6 (New-Color 217 101 25) (New-Color 136 50 18) (New-Color 72 34 19) 7
Draw-BevelRect $g ($r[0] + 14) ($r[1] + 14) ($r[2] - 28) ($r[3] - 28) 3 (New-Color 121 65 31) (New-Color 47 31 24) (New-Color 248 171 53 115) 3
Draw-InsetLine $g ($r[0] + 27) ($r[1] + 27) ($r[2] - 54) ($r[3] - 54) 2 (New-Color 255 220 112 54) 1

foreach ($cardSpec in @(
    @("codex_card_weapon", (New-Color 204 94 24), $false),
    @("codex_card_passive", (New-Color 79 145 49), $false),
    @("codex_card_locked", (New-Color 72 57 41), $true)
)) {
    $name = $cardSpec[0]
    $r = $rects[$name]
    Draw-CardBase $g $r[0] $r[1] $r[2] $r[3] $cardSpec[1] $cardSpec[2]
}

$r = $rects.codex_card_selected
$glow = New-Object System.Drawing.SolidBrush((New-Color 255 194 42 86))
$g.FillEllipse($glow, $r[0] - 16, $r[1] - 12, $r[2] + 32, $r[3] + 24)
$glow.Dispose()
Draw-BevelRect $g ($r[0] + 8) ($r[1] + 8) ($r[2] - 16) ($r[3] - 16) 8 (New-Color 255 213 70 52) (New-Color 255 135 26 42) (New-Color 255 231 105 245) 5
Draw-InsetLine $g ($r[0] + 16) ($r[1] + 16) ($r[2] - 32) ($r[3] - 32) 5 (New-Color 255 251 179 150) 2

$r = $rects.codex_stat_slot
Draw-SoftShadow $g $r[0] ($r[1] + 2) $r[2] ($r[3] - 4) 3 55
Draw-BrushRect $g $r[0] $r[1] $r[2] $r[3] (New-Color 87 57 36 248) (New-Color 43 32 28 248) (New-Color 128 80 42 228)
$statHi = New-Object System.Drawing.Pen((New-Color 255 197 92 72), 2)
$g.DrawLine($statHi, $r[0] + 20, $r[1] + 10, $r[0] + $r[2] - 26, $r[1] + 8)
$statHi.Dispose()

$r = $rects.codex_recommend_frame
Draw-SoftShadow $g ($r[0] + 2) ($r[1] + 3) ($r[2] - 4) ($r[3] - 5) 5 62
Draw-BevelRect $g $r[0] $r[1] $r[2] $r[3] 5 (New-Color 91 58 36) (New-Color 36 29 25) (New-Color 112 72 39) 4
Draw-InsetLine $g ($r[0] + 8) ($r[1] + 8) ($r[2] - 16) ($r[3] - 16) 2 (New-Color 241 170 75 92) 2

$r = $rects.codex_tab
Draw-SoftShadow $g ($r[0] + 4) ($r[1] + 7) ($r[2] - 8) ($r[3] - 12) 8 76
Draw-TabNotch $g ($r[0] + 3) ($r[1] + 5) ($r[2] - 6) ($r[3] - 10) (New-Color 128 82 46) (New-Color 58 39 29) (New-Color 31 21 16)
$shine = New-Object System.Drawing.Pen((New-Color 255 227 151 105), 3)
$g.DrawLine($shine, $r[0] + 18, $r[1] + 14, $r[0] + $r[2] - 24, $r[1] + 14)
$shine.Dispose()

$r = $rects.codex_header_plaque
Draw-SoftShadow $g ($r[0] + 3) ($r[1] + 11) ($r[2] - 6) ($r[3] - 18) 12 95
Draw-BevelRect $g $r[0] ($r[1] + 7) $r[2] ($r[3] - 14) 12 (New-Color 146 94 48) (New-Color 76 49 30) (New-Color 42 27 18) 6
Draw-InsetLine $g ($r[0] + 16) ($r[1] + 18) ($r[2] - 32) ($r[3] - 36) 8 (New-Color 244 174 79 82) 2
$boltBrush = New-Object System.Drawing.SolidBrush((New-Color 67 42 25))
foreach ($bx in @(($r[0] + 28), ($r[0] + $r[2] - 38))) {
    $g.FillEllipse($boltBrush, $bx, $r[1] + 32, 12, 12)
}
$boltBrush.Dispose()

$r = $rects.codex_pumpkin_badge
$pumpkinBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.RectangleF($r[0], $r[1], $r[2], $r[3])),
    (New-Color 248 155 32),
    (New-Color 170 72 21),
    [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$g.FillEllipse($pumpkinBrush, $r[0] + 10, $r[1] + 13, 52, 47)
$pumpkinBrush.Dispose()
$pumpkinPen = New-Object System.Drawing.Pen((New-Color 92 45 19), 3)
$g.DrawEllipse($pumpkinPen, $r[0] + 10, $r[1] + 13, 52, 47)
$g.DrawArc($pumpkinPen, $r[0] + 21, $r[1] + 14, 16, 45, 88, 180)
$g.DrawArc($pumpkinPen, $r[0] + 34, $r[1] + 14, 16, 45, -88, 180)
$pumpkinPen.Dispose()
$stemBrush = New-Object System.Drawing.SolidBrush((New-Color 80 82 30))
$g.FillRectangle($stemBrush, $r[0] + 31, $r[1] + 7, 12, 16)
$stemBrush.Dispose()

$r = $rects.codex_close_button
Draw-BevelRect $g ($r[0] + 3) ($r[1] + 3) ($r[2] - 6) ($r[3] - 6) 8 (New-Color 214 55 43) (New-Color 139 30 28) (New-Color 86 22 18) 5
$xPen = New-Object System.Drawing.Pen((New-Color 255 232 211), 8)
$xPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$xPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($xPen, $r[0] + 24, $r[1] + 24, $r[0] + $r[2] - 24, $r[1] + $r[3] - 24)
$g.DrawLine($xPen, $r[0] + $r[2] - 24, $r[1] + 24, $r[0] + 24, $r[1] + $r[3] - 24)
$xPen.Dispose()

$r = $rects.skill_connector_gold
$penDark = New-Object System.Drawing.Pen((New-Color 118 55 18 220), 15)
$penDark.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$penDark.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($penDark, $r[0] + 8, $r[1] + 16, $r[0] + $r[2] - 8, $r[1] + 16)
$penDark.Dispose()
$penLight = New-Object System.Drawing.Pen((New-Color 255 177 37 240), 8)
$penLight.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$penLight.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($penLight, $r[0] + 12, $r[1] + 12, $r[0] + $r[2] - 12, $r[1] + 20)
$penLight.Dispose()

$r = $rects.skill_connector_locked
$lockedPen = New-Object System.Drawing.Pen((New-Color 90 75 64 190), 12)
$lockedPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$lockedPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($lockedPen, $r[0] + 8, $r[1] + 16, $r[0] + $r[2] - 8, $r[1] + 16)
$lockedPen.Dispose()

$r = $rects.skill_node_basic
Draw-BevelRect $g ($r[0] + 15) ($r[1] + 15) ($r[2] - 30) ($r[3] - 30) 42 (New-Color 217 99 21) (New-Color 132 49 17) (New-Color 80 34 18) 7
Draw-BevelRect $g ($r[0] + 31) ($r[1] + 31) ($r[2] - 62) ($r[3] - 62) 30 (New-Color 255 173 41) (New-Color 166 64 21) (New-Color 255 204 79 150) 3

$r = $rects.skill_node_capstone
Draw-BevelRect $g ($r[0] + 12) ($r[1] + 12) ($r[2] - 24) ($r[3] - 24) 62 (New-Color 255 137 18) (New-Color 129 38 13) (New-Color 92 31 14) 10
Draw-BevelRect $g ($r[0] + 34) ($r[1] + 34) ($r[2] - 68) ($r[3] - 68) 44 (New-Color 255 212 60) (New-Color 184 70 18) (New-Color 255 231 112 180) 5

$r = $rects.skill_tree_panel_frame
Draw-BevelRect $g $r[0] $r[1] $r[2] $r[3] 6 (New-Color 35 32 31 248) (New-Color 21 20 21 248) (New-Color 255 121 14) 12
Draw-BevelRect $g ($r[0] + 28) ($r[1] + 28) ($r[2] - 56) ($r[3] - 56) 2 (New-Color 49 45 44 228) (New-Color 28 27 28 228) (New-Color 116 65 31 130) 3
Draw-Noise $g $r[0] $r[1] $r[2] $r[3] 420 (New-Color 255 209 122 12) 4105

$r = $rects.skill_popup_panel
Draw-BevelRect $g $r[0] $r[1] $r[2] $r[3] 8 (New-Color 87 55 31 246) (New-Color 42 30 25 246) (New-Color 191 88 25) 7
Draw-BevelRect $g ($r[0] + 26) ($r[1] + 26) ($r[2] - 52) ($r[3] - 52) 3 (New-Color 70 46 31 226) (New-Color 32 26 24 226) (New-Color 240 152 42 100) 2
Draw-Noise $g $r[0] $r[1] $r[2] $r[3] 90 (New-Color 255 220 140 20) 4106

$atlasPath = Join-Path $outDir "codex_cutout_atlas.png"
$atlas.Save($atlasPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-SpriteMeta $atlasPath 4096

foreach ($name in $rects.Keys) {
    $r = $rects[$name]
    Save-Crop $atlas $name $r[0] $r[1] $r[2] $r[3]
}

$g.Dispose()
$atlas.Dispose()

Write-Host "Generated cutout atlas and sliced sprites in $outDir"
