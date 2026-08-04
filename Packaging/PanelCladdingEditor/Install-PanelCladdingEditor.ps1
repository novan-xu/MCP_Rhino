[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$BundleRoot,
    [ValidateSet("Install", "Validate")] [string]$Mode = "Install",
    [string]$RhinoPackageRoot = (Join-Path $env:APPDATA "McNeel\Rhinoceros\packages\8.0")
)

$ErrorActionPreference = "Stop"
$bundle = (Resolve-Path -LiteralPath $BundleRoot).Path
$manifestPath = Join-Path $bundle "bundle-manifest.json"
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Bundle manifest is missing." }
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

foreach ($file in $manifest.files) {
    $path = Join-Path $bundle ([string]$file.path).Replace('/', '\')
    if (-not (Test-Path -LiteralPath $path)) { throw "Bundle file missing: $($file.path)" }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne [string]$file.sha256) { throw "Bundle hash mismatch: $($file.path)" }
}

$packageRoot = Join-Path $RhinoPackageRoot "PanelCladdingEditor"
$target = Join-Path $packageRoot ([string]$manifest.version)
if ($Mode -eq "Validate") {
    if (-not (Test-Path -LiteralPath (Join-Path $target "PanelCladdingEditor.rhp"))) {
        throw "PanelCladdingEditor is not installed at $target"
    }
    if (Test-Path -LiteralPath (Join-Path $target "PanelCladdingEditor.dll")) {
        throw "Duplicate plug-in identity found: PanelCladdingEditor.dll must not accompany the .rhp."
    }
    $depsText = Get-Content -Raw -LiteralPath (Join-Path $target "PanelCladdingEditor.deps.json")
    if ($depsText -notmatch 'PanelCladdingEditor\.rhp' -or $depsText -match 'PanelCladdingEditor\.dll') {
        throw "Installed runtime manifest does not identify the direct .rhp build."
    }
    $activeVersion = (Get-Content -Raw -LiteralPath (Join-Path $packageRoot "manifest.txt")).Trim()
    if ($activeVersion -ne [string]$manifest.version) {
        throw "Rhino package manifest selects '$activeVersion', expected '$($manifest.version)'."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $target "manifest.yml"))) {
        throw "Rhino package manifest.yml is missing at $target"
    }
    Write-Output "Validated $target"
    return
}

$defaultRoot = [IO.Path]::GetFullPath((Join-Path $env:APPDATA "McNeel\Rhinoceros\packages\8.0"))
$requestedRoot = [IO.Path]::GetFullPath($RhinoPackageRoot)
$rhinoRunning = @(Get-Process -Name Rhino -ErrorAction SilentlyContinue).Count -gt 0
if ($rhinoRunning -and $requestedRoot.Equals($defaultRoot, [StringComparison]::OrdinalIgnoreCase)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $target = Join-Path (Join-Path $env:LOCALAPPDATA "PanelCladdingEditor\staged") "$($manifest.version)-$stamp"
    Write-Warning "Rhino is running; staging the standalone package for installation after Rhino exits."
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -Path (Join-Path $bundle "Plugin\*") -Destination $target -Recurse -Force
Copy-Item -LiteralPath (Join-Path $bundle "manifest.yml") -Destination $target -Force
Copy-Item -LiteralPath $manifestPath -Destination $target -Force
if (-not $rhinoRunning -or -not $requestedRoot.Equals($defaultRoot, [StringComparison]::OrdinalIgnoreCase)) {
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $packageRoot "manifest.txt") -Value ([string]$manifest.version) -Encoding ascii
}
Write-Output $target
