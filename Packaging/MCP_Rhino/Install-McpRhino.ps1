[CmdletBinding()]
param(
    [ValidateSet('Install', 'Repair', 'Validate')]
    [string] $Mode = 'Install',
    [string] $BundleRoot = (Split-Path -Parent $PSScriptRoot),
    [string] $ProductRoot = (Join-Path $env:LOCALAPPDATA 'MCP_Rhino'),
    [string] $RhinoPackageRoot = (Join-Path $env:APPDATA 'McNeel\Rhinoceros\packages\8.0'),
    [switch] $ConfigureClient,
    [string] $ClientConfigPath
)

$ErrorActionPreference = 'Stop'
$BundleRoot = [IO.Path]::GetFullPath($BundleRoot)
$ProductRoot = [IO.Path]::GetFullPath($ProductRoot)
$RhinoPackageRoot = [IO.Path]::GetFullPath($RhinoPackageRoot)
function Get-RelativePath([string] $BasePath, [string] $FullPath) {
    $base = [IO.Path]::GetFullPath($BasePath).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath($FullPath)
    if (-not $full.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside its expected base: $full"
    }
    return $full.Substring($base.Length)
}

$bundleManifestPath = Join-Path $BundleRoot 'bundle-manifest.json'
$definitionPath = Join-Path $BundleRoot 'package-manifest.json'
if (-not (Test-Path -LiteralPath $bundleManifestPath) -or -not (Test-Path -LiteralPath $definitionPath)) {
    throw 'Bundle metadata is missing. Run Build-McpRhinoPackage.ps1 first.'
}
$bundle = Get-Content -LiteralPath $bundleManifestPath -Raw | ConvertFrom-Json
$definition = Get-Content -LiteralPath $definitionPath -Raw | ConvertFrom-Json
if ($bundle.product -ne 'MCP_Rhino' -or $bundle.schemaVersion -ne 1 -or $bundle.routeProtocolVersion -ne 1) {
    throw 'Unsupported or incompatible MCP_Rhino bundle.'
}
foreach ($file in $bundle.files) {
    $source = Join-Path $BundleRoot ([string]$file.path)
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Bundle file is missing: $($file.path)" }
    $actual = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $file.sha256) { throw "Bundle hash mismatch: $($file.path)" }
}

$version = [string]$bundle.productVersion
$binRoot = Join-Path $ProductRoot 'bin'
$packageRoot = Join-Path $RhinoPackageRoot ([string]$definition.pluginPackageName)
$pluginRoot = Join-Path $packageRoot $version
$installManifestPath = Join-Path $ProductRoot 'install-manifest.json'
$priorInstallation = if (Test-Path -LiteralPath $installManifestPath) {
    Get-Content -LiteralPath $installManifestPath -Raw | ConvertFrom-Json
} else {
    $null
}
$expected = @()
foreach ($file in $bundle.files) {
    $relative = [string]$file.path
    if ($relative.StartsWith('bin/', [StringComparison]::OrdinalIgnoreCase)) {
        $expected += [pscustomobject]@{ source = Join-Path $BundleRoot $relative; target = Join-Path $ProductRoot $relative; sha256 = $file.sha256; role = 'bin' }
    } elseif ($relative.StartsWith('Plugin/', [StringComparison]::OrdinalIgnoreCase)) {
        $inside = $relative.Substring('Plugin/'.Length)
        $expected += [pscustomobject]@{ source = Join-Path $BundleRoot $relative; target = Join-Path $pluginRoot $inside; sha256 = $file.sha256; role = 'plugin' }
    }
}

if ($Mode -eq 'Validate') {
    if (($null -eq $priorInstallation) -or
        ($priorInstallation.product -ne 'MCP_Rhino') -or
        ($priorInstallation.schemaVersion -ne 1) -or
        ($priorInstallation.productVersion -ne $version) -or
        ($priorInstallation.routeProtocolVersion -ne 1)) {
        throw 'The installed ownership manifest is missing or incompatible.'
    }
    $failures = @($priorInstallation.files | Where-Object {
        -not (Test-Path -LiteralPath $_.path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $_.path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $_.sha256
    })
    if ($failures.Count -gt 0) { throw "Installed bundle validation failed for $($failures.Count) file(s)." }
    $router = Join-Path $binRoot ([string]$definition.routerExecutable)
    & $router --validate-install
    if ($LASTEXITCODE -ne 0) { throw 'Installed Router validation failed.' }
    Write-Output "MCP_Rhino $version installation is valid."
    return
}

$usingDefaultRoots = $ProductRoot.Equals([IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'MCP_Rhino')), [StringComparison]::OrdinalIgnoreCase) -and
    $RhinoPackageRoot.Equals([IO.Path]::GetFullPath((Join-Path $env:APPDATA 'McNeel\Rhinoceros\packages\8.0')), [StringComparison]::OrdinalIgnoreCase)
$active = @()
if ($usingDefaultRoots) {
    $active = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq 'Rhino' -or $_.ProcessName -eq 'MCP_Rhino.Router' })
}
if ($active.Count -gt 0) {
    $stageRoot = Join-Path $ProductRoot ("staged\{0}-{1}" -f $version, [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff'))
    New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $BundleRoot '*') -Destination $stageRoot -Recurse -Force
    Write-Warning "MCP_Rhino was staged at $stageRoot. Close Rhino and Router processes, then run its Installer\Install-McpRhino.ps1. No installed component was replaced."
    return
}

if (((Test-Path -LiteralPath $binRoot) -or (Test-Path -LiteralPath $pluginRoot)) -and $null -eq $priorInstallation) {
    throw 'An unowned MCP_Rhino installation already exists. Refusing to overwrite it without an installer ownership manifest.'
}
if (($null -ne $priorInstallation) -and
    ($priorInstallation.product -ne 'MCP_Rhino' -or $priorInstallation.schemaVersion -ne 1)) {
    throw 'The existing MCP_Rhino ownership manifest is incompatible.'
}

$pluginSearchRoots = @($RhinoPackageRoot)
if ($usingDefaultRoots) {
    $pluginSearchRoots += @(
        (Join-Path $env:APPDATA 'McNeel\Rhinoceros\8.0\Plug-ins'),
        (Join-Path $env:LOCALAPPDATA 'McNeel\Rhinoceros\8.0\Plug-ins')
    )
}
$otherRhps = @($pluginSearchRoots | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | ForEach-Object {
    Get-ChildItem -LiteralPath $_ -Filter 'MCP_Rhino.Server.rhp' -File -Recurse -ErrorAction SilentlyContinue
} | Where-Object { -not $_.FullName.StartsWith($pluginRoot, [StringComparison]::OrdinalIgnoreCase) })
if ($otherRhps.Count -gt 0) {
    throw "Another MCP_Rhino plugin copy is discoverable: $($otherRhps[0].FullName)"
}

$installId = [Guid]::NewGuid().ToString('N')
$binStage = Join-Path $ProductRoot ".bin-$installId"
$pluginStage = Join-Path $packageRoot ".$version-$installId"
New-Item -ItemType Directory -Path $binStage, $pluginStage -Force | Out-Null
foreach ($item in $expected) {
    $stageBase = if ($item.role -eq 'bin') { $binStage } else { $pluginStage }
    $sourceBase = if ($item.role -eq 'bin') { Join-Path $BundleRoot 'bin' } else { Join-Path $BundleRoot 'Plugin' }
    $relative = Get-RelativePath $sourceBase $item.source
    $stageTarget = Join-Path $stageBase $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $stageTarget) -Force | Out-Null
    Copy-Item -LiteralPath $item.source -Destination $stageTarget
}

$rollbackRoot = $null
if ((Test-Path -LiteralPath $binRoot) -or (Test-Path -LiteralPath $pluginRoot)) {
    $rollbackRoot = Join-Path $ProductRoot ("rollback\{0}" -f [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff'))
    New-Item -ItemType Directory -Path $rollbackRoot -Force | Out-Null
    if (Test-Path -LiteralPath $binRoot) { Move-Item -LiteralPath $binRoot -Destination (Join-Path $rollbackRoot 'bin') }
    if (Test-Path -LiteralPath $pluginRoot) { Move-Item -LiteralPath $pluginRoot -Destination (Join-Path $rollbackRoot 'Plugin') }
}
Move-Item -LiteralPath $binStage -Destination $binRoot
Move-Item -LiteralPath $pluginStage -Destination $pluginRoot
Set-Content -LiteralPath (Join-Path $packageRoot 'manifest.txt') -Value $version -Encoding ascii
@"
---
name: MCP_Rhino
version: $version
authors:
- MCP_Rhino
description: Multi-document MCP integration for Rhino 8.
keywords:
- mcp
- automation
- guid:$($definition.pluginId.ToString().ToLowerInvariant())
"@ | Set-Content -LiteralPath (Join-Path $pluginRoot 'manifest.yml') -Encoding utf8

$clientAction = if ($null -ne $priorInstallation) { $priorInstallation.clientAction } else { $null }
if ($ConfigureClient) {
    if ([string]::IsNullOrWhiteSpace($ClientConfigPath)) { throw '-ClientConfigPath is required with -ConfigureClient.' }
    $ClientConfigPath = [IO.Path]::GetFullPath($ClientConfigPath)
    $config = if (Test-Path -LiteralPath $ClientConfigPath) { Get-Content -LiteralPath $ClientConfigPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    if ($null -eq $config.mcpServers) { $config | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([pscustomobject]@{}) }
    $previous = $config.mcpServers.PSObject.Properties['mcp-rhino']
    $preservePriorAction = $null -ne $clientAction -and
        ([string]$clientAction.path).Equals($ClientConfigPath, [StringComparison]::OrdinalIgnoreCase)
    $previousJson = if ($preservePriorAction) {
        $clientAction.previousEntryJson
    } elseif ($null -eq $previous) {
        $null
    } else {
        $previous.Value | ConvertTo-Json -Depth 20 -Compress
    }
    $ownedEntry = [ordered]@{ type = 'stdio'; command = (Join-Path $binRoot ([string]$definition.routerExecutable)); args = @() }
    if ($null -ne $previous) { $config.mcpServers.PSObject.Properties.Remove('mcp-rhino') }
    $config.mcpServers | Add-Member -NotePropertyName 'mcp-rhino' -NotePropertyValue $ownedEntry
    New-Item -ItemType Directory -Path (Split-Path -Parent $ClientConfigPath) -Force | Out-Null
    $config | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $ClientConfigPath -Encoding utf8
    $clientAction = [ordered]@{ path = $ClientConfigPath; previousEntryJson = $previousJson; installedEntry = $ownedEntry }
}

$installedFiles = @($expected | ForEach-Object { [ordered]@{ path = $_.target; sha256 = $_.sha256; role = $_.role } })
foreach ($generated in @(
    [pscustomobject]@{ path = (Join-Path $packageRoot 'manifest.txt'); role = 'plugin-metadata' },
    [pscustomobject]@{ path = (Join-Path $pluginRoot 'manifest.yml'); role = 'plugin-metadata' }
)) {
    $installedFiles += [ordered]@{
        path = $generated.path
        sha256 = (Get-FileHash -LiteralPath $generated.path -Algorithm SHA256).Hash.ToLowerInvariant()
        role = $generated.role
    }
}
$rollbackFiles = if ($null -ne $rollbackRoot -and (Test-Path -LiteralPath $rollbackRoot)) {
    @(Get-ChildItem -LiteralPath $rollbackRoot -File -Recurse | ForEach-Object {
        [ordered]@{
            path = $_.FullName
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            role = 'rollback'
        }
    })
} else {
    @()
}
$manifest = [ordered]@{
    schemaVersion = 1; product = 'MCP_Rhino'; productVersion = $version; routeProtocolVersion = 1
    installedUtc = [DateTime]::UtcNow.ToString('O'); pluginId = $definition.pluginId
    binRoot = $binRoot; pluginRoot = $pluginRoot; rollbackRoot = $rollbackRoot
    files = @($installedFiles); rollbackFiles = @($rollbackFiles); clientAction = $clientAction
}
New-Item -ItemType Directory -Path $ProductRoot -Force | Out-Null
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $installManifestPath -Encoding utf8

$routerPath = Join-Path $binRoot ([string]$definition.routerExecutable)
& $routerPath --validate-install
if ($LASTEXITCODE -ne 0) { throw 'Installed Router validation failed.' }
$snippet = [ordered]@{ mcpServers = [ordered]@{ 'mcp-rhino' = [ordered]@{ type = 'stdio'; command = $routerPath; args = @() } } }
Write-Output "MCP_Rhino $version installed. Rhino loads the .rhp at startup; each MCP client launches its own Router."
$snippet | ConvertTo-Json -Depth 5
