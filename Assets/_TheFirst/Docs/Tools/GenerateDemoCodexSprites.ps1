Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"

$outDir = Join-Path (Get-Location) "Assets\Resources\UI\DemoCodex"
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

    $highlight = New-Object System.Drawing.Pen((New-Color 255 228 159 95), [Math]::Max(1, $border / 2))
    $g.DrawLine($highlight, $x + $r, $y + $border, $x + $w - $r, $y + $border)
    $g.DrawLine($highlight, $x + $border, $y + $r, $x + $border, $y + $h - $r)
    $highlight.Dispose()

    $shadow = New-Object System.Drawing.Pen((New-Color 57 25 12 110), [Math]::Max(1, $border / 2))
    $g.DrawLine($shadow, $x + $r, $y + $h - $border, $x + $w - $r, $y + $h - $border)
    $g.DrawLine($shadow, $x + $w - $border, $y + $r, $x + $w - $border, $y + $h - $r)
    $shadow.Dispose()
    $path.Dispose()
}

function Draw-Noise($g, [int]$w, [int]$h, [int]$count, $color) {
    $rand = New-Object System.Random(1107)
    for ($i = 0; $i -lt $count; $i++) {
        $x = $rand.Next(0, $w)
        $y = $rand.Next(0, $h)
        $size = $rand.Next(1, 4)
        $brush = New-Object System.Drawing.SolidBrush($color)
        $g.FillEllipse($brush, $x, $y, $size, $size)
        $brush.Dispose()
    }
}

function Draw-BrushRect($g, [float]$x, [float]$y, [float]$w, [float]$h, $topColor, $bottomColor, $edgeColor) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]($x + 7), [float]($y + 3)),
        [System.Drawing.PointF]::new([float]($x + $w - 10), [float]($y + 0)),
        [System.Drawing.PointF]::new([float]($x + $w - 2), [float]($y + 7)),
        [System.Drawing.PointF]::new([float]($x + $w - 7), [float]($y + $h - 5)),
        [System.Drawing.PointF]::new([float]($x + 10), [float]($y + $h)),
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

function Save-Image($name, [int]$w, [int]$h, [scriptblock]$draw) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    & $draw $g $w $h
    $path = Join-Path $outDir "$name.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    Write-SpriteMeta $path
}

function Write-SpriteMeta($pngPath) {
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
  maxTextureSize: 2048
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
    maxTextureSize: 2048
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

Save-Image "codex_book_frame" 1560 860 {
    param($g, $w, $h)
    Draw-BevelRect $g 6 8 ($w - 12) ($h - 16) 24 (New-Color 114 55 27) (New-Color 54 25 17) (New-Color 38 19 13) 18
    Draw-BevelRect $g 40 46 ($w - 80) ($h - 92) 14 (New-Color 156 82 38 210) (New-Color 88 40 23 220) (New-Color 56 26 17) 9
    $edgePen = New-Object System.Drawing.Pen((New-Color 218 151 77 110), 3)
    for ($i = 0; $i -lt 8; $i++) {
        $offset = 55 + $i * 8
        $g.DrawArc($edgePen, 5 + $i * 4, $offset, 134, $h - 115 - $i * 13, 88, 184)
        $g.DrawArc($edgePen, $w - 140 - $i * 4, $offset, 134, $h - 115 - $i * 13, -92, 184)
    }
    $edgePen.Dispose()
    Draw-Noise $g $w $h 420 (New-Color 255 205 120 16)
}

Save-Image "codex_page_left" 720 790 {
    param($g, $w, $h)
    Draw-BevelRect $g 0 0 $w $h 4 (New-Color 238 197 126) (New-Color 202 151 82) (New-Color 122 65 33) 9
    Draw-BevelRect $g 24 30 ($w - 48) ($h - 60) 3 (New-Color 254 226 158) (New-Color 223 178 102) (New-Color 186 123 56 125) 3
    $wash = New-Object System.Drawing.SolidBrush((New-Color 255 239 185 34))
    $g.FillEllipse($wash, 80, 54, 390, 235)
    $g.FillEllipse($wash, 250, 410, 330, 280)
    $wash.Dispose()
    Draw-Noise $g $w $h 560 (New-Color 102 58 24 24)
}

Save-Image "codex_page_right" 720 790 {
    param($g, $w, $h)
    Draw-BevelRect $g 0 0 $w $h 4 (New-Color 244 205 134) (New-Color 210 160 90) (New-Color 122 65 33) 9
    Draw-BevelRect $g 24 30 ($w - 48) ($h - 60) 3 (New-Color 255 229 166) (New-Color 226 181 105) (New-Color 186 123 56 125) 3
    $wash = New-Object System.Drawing.SolidBrush((New-Color 255 242 193 34))
    $g.FillEllipse($wash, 120, 40, 440, 225)
    $g.FillEllipse($wash, 70, 390, 360, 260)
    $wash.Dispose()
    Draw-Noise $g $w $h 560 (New-Color 102 58 24 22)
}

Save-Image "codex_spine" 44 790 {
    param($g, $w, $h)
    Draw-BevelRect $g 5 0 ($w - 10) $h 3 (New-Color 92 48 29) (New-Color 45 24 16) (New-Color 45 22 14) 4
    $pen = New-Object System.Drawing.Pen((New-Color 238 172 69 120), 2)
    for ($y = 32; $y -lt $h; $y += 58) {
        $g.DrawArc($pen, -8, $y, $w + 16, 26, 0, 180)
    }
    $pen.Dispose()
}

Save-Image "codex_tab" 230 64 {
    param($g, $w, $h)
    Draw-BevelRect $g 3 5 ($w - 6) ($h - 10) 8 (New-Color 214 103 35) (New-Color 108 53 25) (New-Color 63 31 19) 5
    $shine = New-Object System.Drawing.Pen((New-Color 255 227 151 105), 3)
    $g.DrawLine($shine, 18, 14, $w - 24, 14)
    $shine.Dispose()
}

Save-Image "codex_card_weapon" 104 128 {
    param($g, $w, $h)
    Draw-BevelRect $g 2 2 ($w - 4) ($h - 4) 7 (New-Color 181 80 27) (New-Color 103 42 20) (New-Color 61 28 16) 5
    Draw-BevelRect $g 13 14 ($w - 26) 62 4 (New-Color 222 137 37) (New-Color 130 56 22) (New-Color 245 171 49 90) 2
    Draw-Noise $g $w $h 65 (New-Color 255 186 86 34)
}

Save-Image "codex_card_passive" 104 128 {
    param($g, $w, $h)
    Draw-BevelRect $g 2 2 ($w - 4) ($h - 4) 7 (New-Color 75 130 42) (New-Color 39 76 33) (New-Color 28 53 28) 5
    Draw-BevelRect $g 13 14 ($w - 26) 62 4 (New-Color 111 157 51) (New-Color 48 88 34) (New-Color 209 255 126 68) 2
    Draw-Noise $g $w $h 65 (New-Color 212 255 126 26)
}

Save-Image "codex_card_locked" 104 128 {
    param($g, $w, $h)
    Draw-BevelRect $g 2 2 ($w - 4) ($h - 4) 7 (New-Color 65 48 37) (New-Color 33 28 24) (New-Color 18 15 13) 5
    Draw-BevelRect $g 13 14 ($w - 26) 62 4 (New-Color 52 43 36) (New-Color 27 25 24) (New-Color 255 255 255 28) 2
    Draw-Noise $g $w $h 35 (New-Color 255 255 255 14)
}

Save-Image "codex_card_selected" 116 140 {
    param($g, $w, $h)
    $brush = New-Object System.Drawing.SolidBrush((New-Color 255 188 38 58))
    $g.FillEllipse($brush, -12, -8, $w + 24, $h + 16)
    $brush.Dispose()
    Draw-BevelRect $g 7 7 ($w - 14) ($h - 14) 8 (New-Color 255 198 58 88) (New-Color 255 118 21 76) (New-Color 255 225 92 230) 5
}

Save-Image "codex_detail_panel" 638 172 {
    param($g, $w, $h)
    Draw-BevelRect $g 2 2 ($w - 4) ($h - 4) 8 (New-Color 80 58 39) (New-Color 44 30 24) (New-Color 112 63 31) 5
    Draw-BevelRect $g 20 18 ($w - 40) ($h - 36) 4 (New-Color 70 47 32 90) (New-Color 37 27 23 96) (New-Color 255 210 118 40) 2
    Draw-Noise $g $w $h 96 (New-Color 255 209 122 20)
}

Save-Image "codex_icon_frame" 148 148 {
    param($g, $w, $h)
    Draw-BevelRect $g 2 2 ($w - 4) ($h - 4) 6 (New-Color 221 105 28) (New-Color 155 57 19) (New-Color 88 41 20) 6
    Draw-BevelRect $g 18 18 ($w - 36) ($h - 36) 2 (New-Color 90 51 28) (New-Color 51 31 22) (New-Color 245 169 55 80) 2
}

Save-Image "codex_stat_slot" 292 56 {
    param($g, $w, $h)
    Draw-BrushRect $g 2 2 ($w - 4) ($h - 4) (New-Color 85 55 34 246) (New-Color 45 32 27 246) (New-Color 116 75 42 210)
}

Save-Image "codex_recommend_frame" 58 58 {
    param($g, $w, $h)
    Draw-BevelRect $g 1 1 ($w - 2) ($h - 2) 5 (New-Color 95 58 32) (New-Color 45 32 25) (New-Color 118 73 36) 3
}

Save-Image "codex_close_button" 72 72 {
    param($g, $w, $h)
    Draw-BevelRect $g 3 3 ($w - 6) ($h - 6) 8 (New-Color 214 55 43) (New-Color 139 30 28) (New-Color 86 22 18) 5
    $pen = New-Object System.Drawing.Pen((New-Color 255 232 211), 8)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen, 24, 24, $w - 24, $h - 24)
    $g.DrawLine($pen, $w - 24, 24, 24, $h - 24)
    $pen.Dispose()
}

Save-Image "skill_connector_gold" 256 32 {
    param($g, $w, $h)
    $penDark = New-Object System.Drawing.Pen((New-Color 118 55 18 220), 15)
    $penDark.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penDark.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($penDark, 8, 16, $w - 8, 16)
    $penDark.Dispose()
    $penLight = New-Object System.Drawing.Pen((New-Color 255 177 37 240), 8)
    $penLight.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penLight.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($penLight, 12, 12, $w - 12, 20)
    $penLight.Dispose()
    $twist = New-Object System.Drawing.Pen((New-Color 255 232 100 210), 3)
    for ($x = 16; $x -lt $w; $x += 22) {
        $g.DrawLine($twist, $x, 5, $x + 12, 27)
    }
    $twist.Dispose()
}

Save-Image "skill_connector_locked" 256 32 {
    param($g, $w, $h)
    $pen = New-Object System.Drawing.Pen((New-Color 90 75 64 190), 12)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen, 8, 16, $w - 8, 16)
    $pen.Dispose()
}

Save-Image "skill_node_basic" 128 128 {
    param($g, $w, $h)
    Draw-BevelRect $g 15 15 ($w - 30) ($h - 30) 42 (New-Color 217 99 21) (New-Color 132 49 17) (New-Color 80 34 18) 7
    Draw-BevelRect $g 31 31 ($w - 62) ($h - 62) 30 (New-Color 255 173 41) (New-Color 166 64 21) (New-Color 255 204 79 150) 3
}

Save-Image "skill_node_capstone" 180 180 {
    param($g, $w, $h)
    Draw-BevelRect $g 12 12 ($w - 24) ($h - 24) 62 (New-Color 255 137 18) (New-Color 129 38 13) (New-Color 92 31 14) 10
    Draw-BevelRect $g 34 34 ($w - 68) ($h - 68) 44 (New-Color 255 212 60) (New-Color 184 70 18) (New-Color 255 231 112 180) 5
}

Save-Image "skill_tree_panel_frame" 1480 700 {
    param($g, $w, $h)
    Draw-BevelRect $g 4 4 ($w - 8) ($h - 8) 6 (New-Color 35 32 31 248) (New-Color 21 20 21 248) (New-Color 255 121 14) 12
    Draw-BevelRect $g 28 28 ($w - 56) ($h - 56) 2 (New-Color 49 45 44 228) (New-Color 28 27 28 228) (New-Color 116 65 31 130) 3
    Draw-Noise $g $w $h 420 (New-Color 255 209 122 12)
    $pen = New-Object System.Drawing.Pen((New-Color 255 168 35 160), 4)
    $g.DrawLine($pen, 42, 38, $w - 42, 38)
    $g.DrawLine($pen, 42, $h - 38, $w - 42, $h - 38)
    $pen.Dispose()
}

Save-Image "skill_popup_panel" 520 300 {
    param($g, $w, $h)
    Draw-BevelRect $g 4 4 ($w - 8) ($h - 8) 8 (New-Color 87 55 31 246) (New-Color 42 30 25 246) (New-Color 191 88 25) 7
    Draw-BevelRect $g 26 26 ($w - 52) ($h - 52) 3 (New-Color 70 46 31 226) (New-Color 32 26 24 226) (New-Color 240 152 42 100) 2
    Draw-Noise $g $w $h 90 (New-Color 255 220 140 20)
}

Write-Host "Generated Demo Codex sprites in $outDir"
