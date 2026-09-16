[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$source = Get-Content -Raw -LiteralPath (Join-Path $repo 'src\PanelCladdingEditor\Infrastructure\Rhino\LivePanelViewportHighlight.cs')
[xml]$rhinoDocs = Get-Content -Raw -LiteralPath 'C:\Program Files\Rhino 8\System\RhinoCommon.xml'
$members = $rhinoDocs.doc.members.member
$wire = $members | Where-Object { $_.name -eq 'M:Rhino.Display.DisplayPipeline.DrawBrepWires(Rhino.Geometry.Brep,System.Drawing.Color,System.Int32)' }
$curve = $members | Where-Object { $_.name -eq 'M:Rhino.Display.DisplayPipeline.DrawCurve(Rhino.Geometry.Curve,System.Drawing.Color,System.Int32)' }
if (@($wire.param)[2].name -ne 'wireDensity' -or @($curve.param)[2].name -ne 'thickness') {
    throw 'Rhino API overload semantics changed; review the actual drawing API before updating this test.'
}
if ($source -match 'e\.Display\.DrawBrepWires\(') {
    throw 'Regression: DrawBrepWires accepts wire density, not line thickness.'
}
if ($source -notmatch 'protected override void DrawForeground\(DrawEventArgs e\)' -or
    $source -match 'protected override void PostDrawObjects\(') {
    throw 'The identifying outline must render in the foreground rather than compete with panel depth.'
}
if ($source -notmatch 'foreach \(var edge in brep\.Edges\)' -or
    $source -notmatch 'DrawCurve\(edge, Color\.FromArgb\(25, 55, 65\), 6\)' -or
    $source -notmatch 'DrawCurve\(edge, Color\.FromArgb\(0, 230, 255\), 3\)') {
    throw 'Expected true pixel-width halo and cyan trimmed-edge outline.'
}
Write-Output '[OK] Rhino API contract: wireDensity is not thickness; DrawCurve supplies pixel thickness.'
Write-Output '[OK] Foreground boundary outline uses 6-pixel halo and 3-pixel cyan strokes.'
Write-Output '[LIMIT] Source/API regression only; native viewport appearance still requires Rhino verification.'
