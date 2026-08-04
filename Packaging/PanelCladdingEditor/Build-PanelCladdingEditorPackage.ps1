[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$OutputRoot = (Join-Path $PSScriptRoot "artifacts")
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$project = Join-Path $repoRoot "src\PanelCladdingEditor\PanelCladdingEditor.csproj"
$buildOutput = Join-Path $repoRoot "src\PanelCladdingEditor\bin\Release\net8.0"
$publish = Join-Path $OutputRoot "publish"
$bundle = Join-Path $OutputRoot "PanelCladdingEditor-$Version"
$plugin = Join-Path $bundle "Plugin"

if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
if (Test-Path -LiteralPath $bundle) { Remove-Item -LiteralPath $bundle -Recurse -Force }
New-Item -ItemType Directory -Path $plugin -Force | Out-Null

dotnet publish $project -c Release -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw "PanelCladdingEditor publish failed." }
dotnet build $project -c Release -t:Rebuild --nologo
if ($LASTEXITCODE -ne 0) { throw "PanelCladdingEditor direct RHP build failed." }

Get-ChildItem -LiteralPath $publish -File | Where-Object {
    $_.Name -notin @("RhinoCommon.dll", "Rhino.UI.dll", "Eto.dll", "Eto.Wpf.dll") -and
    $_.Name -notin @("PanelCladdingEditor.dll", "PanelCladdingEditor.rhp", "PanelCladdingEditor.deps.json") -and
    $_.Extension -ne ".pdb"
} | Copy-Item -Destination $plugin
Copy-Item -LiteralPath (Join-Path $buildOutput "PanelCladdingEditor.rhp") -Destination $plugin
Copy-Item -LiteralPath (Join-Path $buildOutput "PanelCladdingEditor.deps.json") -Destination $plugin

$rhp = Join-Path $plugin "PanelCladdingEditor.rhp"
if (-not (Test-Path -LiteralPath $rhp)) {
    throw "Release publish did not produce PanelCladdingEditor.rhp directly."
}
if (Test-Path -LiteralPath (Join-Path $plugin "PanelCladdingEditor.dll")) {
    throw "Package must not contain both PanelCladdingEditor.dll and PanelCladdingEditor.rhp."
}
$depsPath = Join-Path $plugin "PanelCladdingEditor.deps.json"
$depsText = Get-Content -Raw -LiteralPath $depsPath
if ($depsText -notmatch 'PanelCladdingEditor\.rhp' -or $depsText -match 'PanelCladdingEditor\.dll') {
    throw "Runtime dependency manifest must identify PanelCladdingEditor.rhp, not a copied DLL."
}

@"
---
name: PanelCladdingEditor
version: $Version
authors:
  - Panel Cladding Editor contributors
description: Standalone Rhino 8 panel cladding type editor
keywords:
  - cladding
  - curtain-wall
  - panel
  - guid:7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35
"@ | Set-Content -LiteralPath (Join-Path $bundle "manifest.yml") -Encoding utf8

$forbidden = Get-ChildItem -LiteralPath $plugin -File | Where-Object {
    $_.Name -match "^(MCP_Rhino|ModelContextProtocol|Microsoft\.Extensions\.Hosting)"
}
if ($forbidden) { throw "Forbidden dependency in package: $($forbidden.Name -join ', ')" }

$files = Get-ChildItem -LiteralPath $bundle -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relativePath = $_.FullName.Substring($bundle.Length).TrimStart('\')
    [ordered]@{
        path = $relativePath.Replace('\', '/')
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
[ordered]@{ name = "PanelCladdingEditor"; version = $Version; files = @($files) } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $bundle "bundle-manifest.json") -Encoding utf8

Write-Output $bundle
