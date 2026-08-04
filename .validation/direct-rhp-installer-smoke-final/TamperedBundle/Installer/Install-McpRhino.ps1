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
if ($bundle.product -ne 'MCP_Rhino' -or
    $bundle.schemaVersion -ne 1 -or
    $bundle.transportMode -ne 'router-only' -or
    $bundle.routeProtocolVersion -ne 1 -or
    $definition.transportMode -ne 'router-only') {
    throw 'Unsupported or incompatible MCP_Rhino bundle.'
}
foreach ($file in $bundle.files) {
    $source = Join-Path $BundleRoot ([string]$file.path)
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Bundle file is missing: $($file.path)" }
    $actual = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $file.sha256) { throw "Bundle hash mismatch: $($file.path)" }
}

function Assert-DirectRhpPackageIdentity([string] $Root) {
    $rhpPath = Join-Path $Root 'MCP_Rhino.Server.rhp'
    $duplicateDllPath = Join-Path $Root 'MCP_Rhino.Server.dll'
    $duplicateExePath = Join-Path $Root 'MCP_Rhino.Server.exe'
    $runtimeDllPath = Join-Path $Root 'MCP_Rhino.Server.Runtime.dll'
    $rhpDepsPath = Join-Path $Root 'MCP_Rhino.Server.deps.json'
    $runtimeDepsPath = Join-Path $Root 'MCP_Rhino.Server.Runtime.deps.json'
    $pluginManifestPath = Join-Path $Root 'plugin.manifest'

    if (-not (Test-Path -LiteralPath $rhpPath -PathType Leaf)) { throw "Plug-in RHP is missing under $Root." }
    if (Test-Path -LiteralPath $duplicateDllPath) { throw "Duplicate MCP_Rhino.Server.dll is present under $Root." }
    if (Test-Path -LiteralPath $duplicateExePath) { throw "Development-only MCP_Rhino.Server.exe is present under $Root." }
    if (-not (Test-Path -LiteralPath $runtimeDllPath -PathType Leaf)) { throw "Isolated runtime is missing under $Root." }
    if (-not (Test-Path -LiteralPath $rhpDepsPath -PathType Leaf)) { throw "RHP dependency metadata is missing under $Root." }
    if (-not (Test-Path -LiteralPath $runtimeDepsPath -PathType Leaf)) { throw "Runtime dependency metadata is missing under $Root." }
    if (-not (Test-Path -LiteralPath $pluginManifestPath -PathType Leaf)) { throw "plugin.manifest is missing under $Root." }

    $rhpAssemblyName = [Reflection.AssemblyName]::GetAssemblyName($rhpPath).Name
    $runtimeAssemblyName = [Reflection.AssemblyName]::GetAssemblyName($runtimeDllPath).Name
    if ($rhpAssemblyName -ne 'MCP_Rhino.Server') { throw "Unexpected RHP assembly identity: $rhpAssemblyName" }
    if ($runtimeAssemblyName -ne 'MCP_Rhino.Server.Runtime') { throw "Unexpected runtime assembly identity: $runtimeAssemblyName" }

    $rhpDeps = Get-Content -LiteralPath $rhpDepsPath -Raw
    $runtimeDeps = Get-Content -LiteralPath $runtimeDepsPath -Raw
    if ($rhpDeps.IndexOf('"MCP_Rhino.Server.rhp"', [StringComparison]::Ordinal) -lt 0) {
        throw 'RHP dependency metadata does not name MCP_Rhino.Server.rhp.'
    }
    if ($rhpDeps.IndexOf('"MCP_Rhino.Server.dll"', [StringComparison]::Ordinal) -ge 0) {
        throw 'RHP dependency metadata still names MCP_Rhino.Server.dll.'
    }
    if ($runtimeDeps.IndexOf('"MCP_Rhino.Server.Runtime.dll"', [StringComparison]::Ordinal) -lt 0) {
        throw 'Runtime dependency metadata does not name MCP_Rhino.Server.Runtime.dll.'
    }

    $pluginManifest = Get-Content -LiteralPath $pluginManifestPath -Raw | ConvertFrom-Json
    $runtimeBinaryText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($runtimeDllPath))
    if ($runtimeBinaryText.IndexOf([string]$pluginManifest.id, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw 'The isolated runtime contains the Rhino plug-in GUID.'
    }
}

Assert-DirectRhpPackageIdentity (Join-Path $BundleRoot 'Plugin')

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
        ($priorInstallation.transportMode -ne 'router-only') -or
        ($priorInstallation.routeProtocolVersion -ne 1)) {
        throw 'The installed ownership manifest is missing or incompatible.'
    }
    $failures = @($priorInstallation.files | Where-Object {
        -not (Test-Path -LiteralPath $_.path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $_.path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $_.sha256
    })
    if ($failures.Count -gt 0) { throw "Installed bundle validation failed for $($failures.Count) file(s)." }
    Assert-DirectRhpPackageIdentity $pluginRoot
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
    $stagedBase = [IO.Path]::GetFullPath((Join-Path $ProductRoot 'staged'))
    $productPrefix = $ProductRoot.TrimEnd('\') + '\'
    if (-not $stagedBase.StartsWith($productPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to manage a staged path outside the product root: $stagedBase"
    }
    New-Item -ItemType Directory -Path $stagedBase -Force | Out-Null
    Get-ChildItem -LiteralPath $stagedBase -Directory | ForEach-Object {
        $stagedDefinitionPath = Join-Path $_.FullName 'package-manifest.json'
        if (Test-Path -LiteralPath $stagedDefinitionPath -PathType Leaf) {
            $stagedDefinition = Get-Content -LiteralPath $stagedDefinitionPath -Raw | ConvertFrom-Json
            if ($stagedDefinition.product -eq 'MCP_Rhino') {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force
            }
        }
    }

    $stageRoot = Join-Path $stagedBase ("{0}-{1}" -f $version, [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff'))
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
} | Where-Object { -not $_.FullName.StartsWith($packageRoot, [StringComparison]::OrdinalIgnoreCase) })
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
Assert-DirectRhpPackageIdentity $pluginStage

$rollbackRoot = $null
$priorIsRouterOnly = ($null -ne $priorInstallation) -and
    ($priorInstallation.transportMode -eq 'router-only')
$ownedPluginRoots = @()
if (Test-Path -LiteralPath $pluginRoot -PathType Container) {
    $ownedPluginRoots += $pluginRoot
}
if ($null -ne $priorInstallation -and -not [string]::IsNullOrWhiteSpace([string]$priorInstallation.pluginRoot)) {
    $priorPluginRoot = [IO.Path]::GetFullPath([string]$priorInstallation.pluginRoot)
    $packagePrefix = $packageRoot.TrimEnd('\') + '\'
    if (-not $priorPluginRoot.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Prior owned plug-in root is outside the MCP_Rhino package root: $priorPluginRoot"
    }
    if ((Test-Path -LiteralPath $priorPluginRoot -PathType Container) -and
        -not ($ownedPluginRoots -contains $priorPluginRoot)) {
        $ownedPluginRoots += $priorPluginRoot
    }
}

if ((Test-Path -LiteralPath $binRoot) -or $ownedPluginRoots.Count -gt 0) {
    $rollbackRoot = Join-Path $ProductRoot ("rollback\{0}" -f [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff'))
    New-Item -ItemType Directory -Path $rollbackRoot -Force | Out-Null
    if (Test-Path -LiteralPath $binRoot) { Move-Item -LiteralPath $binRoot -Destination (Join-Path $rollbackRoot 'bin') }
    foreach ($ownedPluginRoot in $ownedPluginRoots) {
        $ownedVersion = Split-Path -Leaf $ownedPluginRoot
        Move-Item -LiteralPath $ownedPluginRoot -Destination (Join-Path $rollbackRoot "Plugin-$ownedVersion")
    }
}
Move-Item -LiteralPath $binStage -Destination $binRoot
Move-Item -LiteralPath $pluginStage -Destination $pluginRoot
Assert-DirectRhpPackageIdentity $pluginRoot
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

$routerPath = Join-Path $binRoot ([string]$definition.routerExecutable)
& $routerPath --validate-install
if ($LASTEXITCODE -ne 0) { throw 'Installed Router validation failed.' }

# Rollbacks from an installation that predates the Router-only contract contain
# the retired Bridge/Companion binaries and command-bearing Rhino plugin. Keep
# them only until the replacement Router validates, then remove the entire
# installer-owned rollback tree so an older orphaned snapshot cannot stay live.
if (-not $priorIsRouterOnly) {
    $rollbackBase = [IO.Path]::GetFullPath((Join-Path $ProductRoot 'rollback'))
    $productPrefix = $ProductRoot.TrimEnd('\') + '\'
    if (-not $rollbackBase.StartsWith($productPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a rollback path outside the product root: $rollbackBase"
    }
    if (Test-Path -LiteralPath $rollbackBase -PathType Container) {
        Remove-Item -LiteralPath $rollbackBase -Recurse -Force
    }
    $rollbackRoot = $null

    # Old installers could stage complete Bridge/Companion bundles while Rhino
    # was running. Remove only staged bundles that carry package metadata and do
    # not declare the Router-only contract; leave current Router-only stages intact.
    $stagedBase = [IO.Path]::GetFullPath((Join-Path $ProductRoot 'staged'))
    if (Test-Path -LiteralPath $stagedBase -PathType Container) {
        Get-ChildItem -LiteralPath $stagedBase -Directory | ForEach-Object {
            $stagedDefinitionPath = Join-Path $_.FullName 'package-manifest.json'
            if (Test-Path -LiteralPath $stagedDefinitionPath -PathType Leaf) {
                $stagedDefinition = Get-Content -LiteralPath $stagedDefinitionPath -Raw | ConvertFrom-Json
                if ($stagedDefinition.transportMode -ne 'router-only') {
                    Remove-Item -LiteralPath $_.FullName -Recurse -Force
                }
            }
        }
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
    schemaVersion = 1; product = 'MCP_Rhino'; productVersion = $version
    transportMode = 'router-only'; routeProtocolVersion = 1
    installedUtc = [DateTime]::UtcNow.ToString('O'); pluginId = $definition.pluginId
    binRoot = $binRoot; pluginRoot = $pluginRoot; rollbackRoot = $rollbackRoot
    files = @($installedFiles); rollbackFiles = @($rollbackFiles); clientAction = $clientAction
}
New-Item -ItemType Directory -Path $ProductRoot -Force | Out-Null
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $installManifestPath -Encoding utf8

$snippet = [ordered]@{ mcpServers = [ordered]@{ 'mcp-rhino' = [ordered]@{ type = 'stdio'; command = $routerPath; args = @() } } }
Write-Output "MCP_Rhino $version installed. Rhino loads the .rhp at startup; each MCP client launches its own Router."
$snippet | ConvertTo-Json -Depth 5
