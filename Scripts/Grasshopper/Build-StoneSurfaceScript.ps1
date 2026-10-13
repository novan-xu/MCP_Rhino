[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$entry = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'StoneSurfaceBake.entry.csx')
$core = Get-Content -Raw -LiteralPath (Join-Path $repo 'src\GrasshopperStoneBake\Infrastructure\Rhino\Live\StoneSurfaceBake.cs')
$component = Get-Content -Raw -LiteralPath (Join-Path $repo 'src\GrasshopperStoneBake\Infrastructure\Grasshopper\Live\LiveStoneSurfaceComponent.cs')
$output = Join-Path $PSScriptRoot 'StoneSurfaceBake.cs'
[IO.File]::WriteAllText($output, $entry.TrimEnd() + "`n`n" + $component.TrimEnd() + "`n`n" + $core, [Text.UTF8Encoding]::new($false))
Write-Output $output
