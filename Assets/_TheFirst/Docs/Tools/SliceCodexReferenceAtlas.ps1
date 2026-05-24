param(
    [Parameter(Mandatory = $true)]
    [string]$ReferencePath
)

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"

$outDir = Join-Path (Get-Location) "Assets\Resources\UI\DemoCodexCutout"
if (!(Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

function New-Color([int]$r, [int]$g, [int]$b, [int]$a = 255) {
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
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

function New-Canvas([int]$w, [int]$h) {
    return New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function New-Graphics($bitmap) {
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    return $g
}

function Save-Png($bitmap, [string]$name) {
    $path = Join-Path $outDir "$name.png"
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-SpriteMeta $path
}

function Copy-ReferenceCrop($source, [string]$name, [int]$sx, [int]$sy, [int]$sw, [int]$sh, [int]$ow, [int]$oh) {
    $bmp = New-Canvas $ow $oh
    $g = New-Graphics $bmp
    $g.Clear([System.Drawing.Color]::Transparent)
    $dest = New-Object System.Drawing.Rectangle(0, 0, $ow, $oh)
    $src = New-Object System.Drawing.Rectangle($sx, $sy, $sw, $sh)
    $g.DrawImage($source, $dest, $src, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    Save-Png $bmp $name
    return $bmp
}

function Fill-Rect($bitmap, [int]$x, [int]$y, [int]$w, [int]$h, $color) {
    $g = New-Graphics $bitmap
    $brush = New-Object System.Drawing.SolidBrush($color)
    $g.FillRectangle($brush, $x, $y, $w, $h)
    $brush.Dispose()
    $g.Dispose()
}

function Clear-Rect($bitmap, [int]$x, [int]$y, [int]$w, [int]$h) {
    $g = New-Graphics $bitmap
    $oldMode = $g.CompositingMode
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Transparent)
    $g.FillRectangle($brush, $x, $y, $w, $h)
    $brush.Dispose()
    $g.CompositingMode = $oldMode
    $g.Dispose()
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

function Fill-RoundedGradient($bitmap, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r, $topColor, $bottomColor, $borderColor = $null) {
    $g = New-Graphics $bitmap
    $path = New-RoundedPath $x $y $w $h $r
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        ([System.Drawing.RectangleF]::new($x, $y, $w, $h)),
        $topColor,
        $bottomColor,
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($brush, $path)
    $brush.Dispose()
    if ($borderColor -ne $null) {
        $pen = New-Object System.Drawing.Pen($borderColor, 1.5)
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($pen, $path)
        $pen.Dispose()
    }
    $path.Dispose()
    $g.Dispose()
}

function Add-TextureNoise($bitmap, [int]$x, [int]$y, [int]$w, [int]$h, [int]$count, $color, [int]$seed) {
    $g = New-Graphics $bitmap
    $rand = New-Object System.Random($seed)
    $brush = New-Object System.Drawing.SolidBrush($color)
    for ($i = 0; $i -lt $count; $i++) {
        $px = $x + $rand.Next(0, [Math]::Max(1, $w))
        $py = $y + $rand.Next(0, [Math]::Max(1, $h))
        $g.FillRectangle($brush, $px, $py, 1, 1)
    }
    $brush.Dispose()
    $g.Dispose()
}

function Add-PaperSurface($bitmap, [int]$x, [int]$y, [int]$w, [int]$h) {
    $g = New-Graphics $bitmap
    $paperBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        ([System.Drawing.RectangleF]::new($x, $y, $w, $h)),
        (New-Color 247 211 142 255),
        (New-Color 225 170 94 255),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($paperBrush, $x, $y, $w, $h)
    $paperBrush.Dispose()

    $wash = New-Object System.Drawing.SolidBrush((New-Color 255 232 167 28))
    $g.FillEllipse($wash, $x + 30, $y + 20, [Math]::Max(1, [int]($w * 0.54)), [Math]::Max(1, [int]($h * 0.32)))
    $g.FillEllipse($wash, $x + [int]($w * 0.28), $y + [int]($h * 0.56), [Math]::Max(1, [int]($w * 0.46)), [Math]::Max(1, [int]($h * 0.32)))
    $wash.Dispose()
    $edge = New-Object System.Drawing.SolidBrush((New-Color 73 39 18 24))
    $g.FillRectangle($edge, $x, $y, $w, 12)
    $g.FillRectangle($edge, $x, $y + $h - 14, $w, 14)
    $g.FillRectangle($edge, $x, $y, 14, $h)
    $g.FillRectangle($edge, $x + $w - 16, $y, 16, $h)
    $edge.Dispose()
    $linePen = New-Object System.Drawing.Pen((New-Color 125 76 36 20), 1)
    for ($yy = $y + 36; $yy -lt $y + $h - 16; $yy += 42) {
        $g.DrawLine($linePen, $x + 22, $yy, $x + $w - 22, $yy + 1)
    }
    $linePen.Dispose()
    $g.Dispose()
    Add-TextureNoise $bitmap $x $y $w $h 1400 (New-Color 81 42 19 23) 501
    Add-TextureNoise $bitmap $x $y $w $h 760 (New-Color 255 239 184 22) 502
}

function Draw-StatSlotAsset([string]$name) {
    $w = 216
    $h = 52
    $bmp = New-Canvas $w $h
    $g = New-Graphics $bmp
    $g.Clear([System.Drawing.Color]::Transparent)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(8, 4),
        [System.Drawing.PointF]::new($w - 10, 4),
        [System.Drawing.PointF]::new($w - 4, 12),
        [System.Drawing.PointF]::new($w - 10, $h - 5),
        [System.Drawing.PointF]::new(12, $h - 5),
        [System.Drawing.PointF]::new(3, $h - 13)
    ))
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        ([System.Drawing.RectangleF]::new(0, 0, $w, $h)),
        (New-Color 82 53 34 255),
        (New-Color 39 30 25 255),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($brush, $path)
    $brush.Dispose()
    $pen = New-Object System.Drawing.Pen((New-Color 123 77 40 230), 3)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path)
    $pen.Dispose()
    $hi = New-Object System.Drawing.Pen((New-Color 248 178 78 72), 2)
    $g.DrawLine($hi, 22, 11, $w - 24, 10)
    $hi.Dispose()
    $path.Dispose()
    $g.Dispose()
    Add-TextureNoise $bmp 10 8 ($w - 20) ($h - 14) 72 (New-Color 255 207 112 14) 611
    Save-Png $bmp $name
    $bmp.Dispose()
}

function Draw-RecommendFrameAsset([string]$name) {
    $bmp = New-Canvas 58 58
    $g = New-Graphics $bmp
    $g.Clear([System.Drawing.Color]::Transparent)
    $shadow = New-Object System.Drawing.SolidBrush((New-Color 0 0 0 72))
    $g.FillRectangle($shadow, 6, 7, 48, 48)
    $shadow.Dispose()
    $g.Dispose()
    Fill-RoundedGradient $bmp 3 3 52 52 5 (New-Color 98 62 36 255) (New-Color 38 29 24 255) (New-Color 111 71 38 230)
    Fill-RoundedGradient $bmp 10 10 38 38 3 (New-Color 47 35 28 255) (New-Color 24 21 19 255) (New-Color 198 131 58 110)
    Add-TextureNoise $bmp 5 5 48 48 34 (New-Color 255 203 104 16) 612
    Save-Png $bmp $name
    $bmp.Dispose()
}

function Draw-SelectedCardHighlightAsset([string]$name) {
    $w = 148
    $h = 168
    $bmp = New-Canvas $w $h
    $g = New-Graphics $bmp
    $g.Clear([System.Drawing.Color]::Transparent)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(20, 8),
        [System.Drawing.PointF]::new($w - 20, 8),
        [System.Drawing.PointF]::new($w - 8, 21),
        [System.Drawing.PointF]::new($w - 12, $h - 20),
        [System.Drawing.PointF]::new($w - 27, $h - 8),
        [System.Drawing.PointF]::new(21, $h - 8),
        [System.Drawing.PointF]::new(8, $h - 23),
        [System.Drawing.PointF]::new(12, 22)
    ))
    $glow = @(
        @(14, (New-Color 255 128 10 22)),
        @(9, (New-Color 255 172 28 42)),
        @(5, (New-Color 255 211 72 96)),
        @(2.25, (New-Color 255 249 154 230))
    )
    foreach ($entry in $glow) {
        $pen = New-Object System.Drawing.Pen($entry[1], [float]$entry[0])
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($pen, $path)
        $pen.Dispose()
    }

    $path.Dispose()
    $g.Dispose()

    Save-Png $bmp $name
    $bmp.Dispose()
}

function Blank-CardInterior($bitmap, [bool]$locked = $false) {
    $topA = if ($locked) { New-Color 25 22 18 255 } else { New-Color 48 35 24 255 }
    $topB = if ($locked) { New-Color 11 10 9 255 } else { New-Color 25 20 17 255 }
    $bottomA = if ($locked) { New-Color 35 27 20 255 } else { New-Color 48 34 24 255 }
    $bottomB = if ($locked) { New-Color 17 15 13 255 } else { New-Color 27 23 20 255 }

    # Match the reference card: keep the ornate frame, corner cuts, badge and bevels,
    # but rebuild only the icon/name wells so runtime icons and text do not ghost over source content.
    Fill-RoundedGradient $bitmap 18 13 90 74 5 $topA $topB (New-Color 121 80 42 120)
    Fill-RoundedGradient $bitmap 15 83 96 37 4 $bottomA $bottomB (New-Color 114 75 39 90)
    Add-TextureNoise $bitmap 22 17 82 60 70 (New-Color 255 196 92 18) 914
    Add-TextureNoise $bitmap 18 86 90 30 42 (New-Color 255 196 92 13) 915
}

function Draw-PreviewAtlas($source) {
    $atlas = New-Canvas 4096 4096
    $g = New-Graphics $atlas
    $g.Clear([System.Drawing.Color]::Transparent)
    $placements = @(
        @("codex_book_frame", 0, 0),
        @("codex_page_left", 1580, 0),
        @("codex_page_right", 2560, 0),
        @("codex_spine", 3140, 0),
        @("codex_header_plaque", 0, 900),
        @("codex_tab_weapons", 420, 900),
        @("codex_tab_passives", 620, 900),
        @("codex_tab_fusion", 820, 900),
        @("codex_tab_monsters", 1020, 900),
        @("codex_footer_bar", 0, 1000),
        @("codex_card_weapon", 1240, 900),
        @("codex_card_locked", 1380, 900),
        @("codex_card_selected", 1520, 900),
        @("codex_detail_panel", 1680, 900),
        @("codex_icon_frame", 2340, 900),
        @("codex_stat_slot", 2500, 900),
        @("codex_recommend_frame", 2820, 900),
        @("codex_close_button", 2920, 900),
        @("skill_node_basic", 3020, 900),
        @("skill_node_capstone", 3160, 900),
        @("skill_connector_gold", 3360, 900),
        @("skill_connector_locked", 3360, 950),
        @("skill_tree_panel_frame", 0, 1120),
        @("skill_popup_panel", 1500, 1120)
    )

    foreach ($p in $placements) {
        $imgPath = Join-Path $outDir "$($p[0]).png"
        if (!(Test-Path $imgPath)) { continue }
        $img = [System.Drawing.Image]::FromFile($imgPath)
        $g.DrawImage($img, $p[1], $p[2], $img.Width, $img.Height)
        $img.Dispose()
    }

    $g.Dispose()
    Save-Png $atlas "codex_cutout_atlas"
    $atlas.Dispose()
}

if (!(Test-Path -LiteralPath $ReferencePath)) {
    throw "Reference image not found: $ReferencePath"
}

$src = [System.Drawing.Image]::FromFile($ReferencePath)

# Full source slices from the supplied AI reference image.
$book = Copy-ReferenceCrop $src "codex_book_frame" 0 58 934 670 1560 860
# Keep the reference book, title, tabs and page frame as one integrated background.
# Only repaint dynamic content wells so runtime cards/text do not sit over source demo content.
Add-PaperSurface $book 110 132 856 570
Fill-RoundedGradient $book 118 718 844 64 7 (New-Color 89 58 34 255) (New-Color 48 34 27 255) (New-Color 130 79 36 235)
Add-TextureNoise $book 128 728 824 44 140 (New-Color 255 200 94 16) 701
Add-PaperSurface $book 1016 56 462 176
Add-PaperSurface $book 1016 232 462 548
Save-Png $book "codex_book_frame"
$book.Dispose()

$leftPage = Copy-ReferenceCrop $src "codex_page_left" 24 80 570 612 960 790
Add-PaperSurface $leftPage 48 34 864 722
Save-Png $leftPage "codex_page_left"
$leftPage.Dispose()

$rightPage = Copy-ReferenceCrop $src "codex_page_right" 596 80 324 612 540 790
Add-PaperSurface $rightPage 44 42 452 668
Save-Png $rightPage "codex_page_right"
$rightPage.Dispose()

Copy-ReferenceCrop $src "codex_spine" 580 72 36 624 54 790 | ForEach-Object { $_.Dispose() }
Copy-ReferenceCrop $src "codex_header_plaque" 209 27 300 66 380 78 | ForEach-Object { $_.Dispose() }
Copy-ReferenceCrop $src "codex_pumpkin_badge" 218 27 66 66 72 72 | ForEach-Object { $_.Dispose() }

Copy-ReferenceCrop $src "codex_tab" 66 105 144 48 230 64 | ForEach-Object { $_.Dispose() }
Copy-ReferenceCrop $src "codex_tab_weapons" 66 105 144 48 230 64 | ForEach-Object { $_.Dispose() }
Copy-ReferenceCrop $src "codex_tab_passives" 214 105 141 48 220 64 | ForEach-Object { $_.Dispose() }
Copy-ReferenceCrop $src "codex_tab_fusion" 360 105 117 48 190 64 | ForEach-Object { $_.Dispose() }
Copy-ReferenceCrop $src "codex_tab_monsters" 481 105 118 48 190 64 | ForEach-Object { $_.Dispose() }

$footer = Copy-ReferenceCrop $src "codex_footer_bar" 80 624 492 47 820 64
Fill-RoundedGradient $footer 8 6 804 52 7 (New-Color 89 58 34 255) (New-Color 48 34 27 255) (New-Color 130 79 36 235)
Add-TextureNoise $footer 16 10 788 44 130 (New-Color 255 200 94 16) 616
Save-Png $footer "codex_footer_bar"
$footer.Dispose()

$weaponCard = Copy-ReferenceCrop $src "codex_card_weapon" 463 184 105 122 126 146
Blank-CardInterior $weaponCard $false
Save-Png $weaponCard "codex_card_weapon"
Save-Png $weaponCard "codex_card_passive"
$weaponCard.Dispose()

$lockedCard = Copy-ReferenceCrop $src "codex_card_locked" 257 763 105 124 126 146
Blank-CardInterior $lockedCard $true
Save-Png $lockedCard "codex_card_locked"
$lockedCard.Dispose()

Draw-SelectedCardHighlightAsset "codex_card_selected"

$detail = Copy-ReferenceCrop $src "codex_detail_panel" 574 770 203 96 638 172
Fill-Rect $detail 28 24 585 124 (New-Color 56 42 30 255)
Save-Png $detail "codex_detail_panel"
$detail.Dispose()

$iconFrame = Copy-ReferenceCrop $src "codex_icon_frame" 410 760 113 130 148 148
Fill-Rect $iconFrame 25 20 98 82 (New-Color 42 31 24 255)
Fill-Rect $iconFrame 20 106 108 24 (New-Color 42 31 24 255)
Save-Png $iconFrame "codex_icon_frame"
$iconFrame.Dispose()

Draw-StatSlotAsset "codex_stat_slot"
Draw-RecommendFrameAsset "codex_recommend_frame"
Copy-ReferenceCrop $src "codex_close_button" 1587 65 45 45 72 72 | ForEach-Object { $_.Dispose() }

Copy-ReferenceCrop $src "skill_node_basic" 816 778 72 73 128 128 | ForEach-Object { $_.Dispose() }
Copy-ReferenceCrop $src "skill_node_capstone" 1096 757 132 132 180 180 | ForEach-Object { $_.Dispose() }
Copy-ReferenceCrop $src "skill_connector_gold" 1263 790 170 30 256 32 | ForEach-Object { $_.Dispose() }
Copy-ReferenceCrop $src "skill_connector_locked" 1264 832 170 24 256 32 | ForEach-Object { $_.Dispose() }

$treePanel = Copy-ReferenceCrop $src "skill_tree_panel_frame" 957 60 682 667 1480 700
Clear-Rect $treePanel 118 76 1240 545
Save-Png $treePanel "skill_tree_panel_frame"
$treePanel.Dispose()

$popup = Copy-ReferenceCrop $src "skill_popup_panel" 574 770 203 96 520 300
Fill-Rect $popup 28 24 464 244 (New-Color 55 41 30 255)
Save-Png $popup "skill_popup_panel"
$popup.Dispose()

Draw-PreviewAtlas $src
$src.Dispose()

Write-Host "Sliced reference codex sprites into $outDir"
