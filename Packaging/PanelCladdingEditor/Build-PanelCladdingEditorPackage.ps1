[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $PSScriptRoot "artifacts" }
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$project = Join-Path $repoRoot "src\PanelCladdingEditor\PanelCladdingEditor.csproj"
$definitionPath = Join-Path $PSScriptRoot "package-manifest.json"
$definition = Get-Content -Raw -LiteralPath $definitionPath | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = [string]$definition.version }
if (-not $Version.Equals([string]$definition.version, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Requested version '$Version' does not match package-manifest.json version '$($definition.version)'."
}
$buildOutput = Join-Path $repoRoot "src\PanelCladdingEditor\bin\Release\net8.0-windows"
$publish = Join-Path $OutputRoot "publish"
$bundle = Join-Path $OutputRoot "PanelCladdingEditor-$Version"
$plugin = Join-Path $bundle "Plugin"
$installer = Join-Path $bundle "Installer"

if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
if (Test-Path -LiteralPath $bundle) { Remove-Item -LiteralPath $bundle -Recurse -Force }
New-Item -ItemType Directory -Path $plugin, $installer -Force | Out-Null

dotnet publish $project -c Release -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw "PanelCladdingEditor publish failed." }
dotnet build $project -c Release -t:Rebuild --nologo
if ($LASTEXITCODE -ne 0) { throw "PanelCladdingEditor direct RHP build failed." }

Get-ChildItem -LiteralPath $publish -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($publish.Length).TrimStart('\')
    $runtimePath = $relativePath.Replace('\', '/')
    $excluded = $_.Name -in @("RhinoCommon.dll", "Rhino.UI.dll", "Eto.dll", "Eto.Wpf.dll") -or
        $_.Name -in @("PanelCladdingEditor.dll", "PanelCladdingEditor.rhp", "PanelCladdingEditor.deps.json") -or
        $_.Extension -eq ".pdb" -or
        ($runtimePath.StartsWith("runtimes/", [StringComparison]::OrdinalIgnoreCase) -and
         -not $runtimePath.StartsWith("runtimes/win-x64/", [StringComparison]::OrdinalIgnoreCase))
    if (-not $excluded) {
        $destination = Join-Path $plugin $relativePath
        $destinationDirectory = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }
}
$nativeRuntimeRoot = Join-Path $publish 'runtimes\win-x64\native'
foreach ($nativeName in @('libSkiaSharp.dll', 'libHarfBuzzSharp.dll')) {
    $nativeSource = Join-Path $nativeRuntimeRoot $nativeName
    if (-not (Test-Path -LiteralPath $nativeSource -PathType Leaf)) {
        throw "Required Windows x64 native dependency is missing from publish output: $nativeSource"
    }
    Copy-Item -LiteralPath $nativeSource -Destination (Join-Path $plugin $nativeName) -Force
}
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

$forbidden = Get-ChildItem -LiteralPath $plugin -Recurse -File | Where-Object {
    $_.Name -match "^(MCP_Rhino|ModelContextProtocol|Microsoft\.Extensions\.Hosting)"
}
if ($forbidden) { throw "Forbidden dependency in package: $($forbidden.Name -join ', ')" }

Copy-Item -LiteralPath $definitionPath -Destination (Join-Path $bundle "package-manifest.json") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Install-PanelCladdingEditor.ps1") -Destination $installer -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Uninstall-PanelCladdingEditor.ps1") -Destination $installer -Force

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
