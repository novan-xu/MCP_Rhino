[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BundleRoot,
    [string] $TempRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$validationBase = [IO.Path]::GetFullPath((Join-Path $repoRoot '.validation'))
# The scratch root must live under the repository .validation directory, so default it there
# instead of beside this script.
if ([string]::IsNullOrWhiteSpace($TempRoot)) {
    $TempRoot = Join-Path $validationBase 'direct-rhp-plugin-identity'
}
$tempRootFull = [IO.Path]::GetFullPath($TempRoot)
$validationPrefix = $validationBase.TrimEnd('\') + '\'
if (-not $tempRootFull.StartsWith($validationPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "TempRoot must stay under the repository .validation directory: $tempRootFull"
}
if (Test-Path -LiteralPath $tempRootFull) {
    Remove-Item -LiteralPath $tempRootFull -Recurse -Force
}

$bundleRootFull = (Resolve-Path $BundleRoot).Path
$installer = Join-Path $bundleRootFull 'Installer\Install-McpRhino.ps1'
$definition = Get-Content -LiteralPath (Join-Path $bundleRootFull 'package-manifest.json') -Raw | ConvertFrom-Json
$productRoot = Join-Path $tempRootFull 'Product'
$legacyRhinoPackages = Join-Path $tempRootFull 'RhinoPackages'
$legacyPackageRoot = Join-Path $legacyRhinoPackages ([string]$definition.pluginPackageName)
$rhinoPluginRoot = Join-Path $productRoot 'plugin'
$oldPluginRoot = Join-Path $legacyPackageRoot '1.0.0'
$newPluginRoot = Join-Path $rhinoPluginRoot ([string]$definition.productVersion)
$oldBinRoot = Join-Path $productRoot 'bin'
$registryTestRoot = 'Registry::HKEY_CURRENT_USER\Software\MCP_Rhino\InstallerTests\DirectRhpPluginIdentity\Plug-Ins'
$registryPluginKey = Join-Path $registryTestRoot ([string]$definition.pluginId).ToLowerInvariant()

$identityProbeProject = Join-Path $PSScriptRoot 'RhpExecutableProbe\RhpExecutableProbe.csproj'
& dotnet build $identityProbeProject -c Release --nologo -p:McpRhinoCliHost=true | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Rhino identity probe build failed.' }
$identityProbe = Join-Path $PSScriptRoot 'RhpExecutableProbe\bin\Release\net8.0\RhpExecutableProbe.dll'
$bundleRhp = Join-Path $bundleRootFull 'Plugin\MCP_Rhino.Server.rhp'
$rhinoCommon = 'C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll'
$identityRows = @(& dotnet $identityProbe --rhino-identities $bundleRhp $rhinoCommon)
if ($LASTEXITCODE -ne 0) { throw 'Packaged Rhino identity enumeration failed.' }
$pluginRows = @($identityRows | Where-Object { $_.StartsWith('RHINO_IDENTITY|kind=PLUGIN|', [StringComparison]::Ordinal) })
$commandRows = @($identityRows | Where-Object { $_.StartsWith('RHINO_IDENTITY|kind=COMMAND|', [StringComparison]::Ordinal) })
if ($pluginRows.Count -ne 1) {
    throw 'The production RHP does not contain exactly one canonical Rhino plug-in identity.'
}
# Rhino reads the plug-in id from the assembly-level GuidAttribute, so assert that field and not the
# plug-in class GUID; an assembly without one resolves to Guid.Empty and collides with every other RHP.
if ($pluginRows[0] -notmatch 'pluginId=([0-9a-fA-F-]{36})') {
    throw "The production RHP identity row does not report a plug-in id: $($pluginRows[0])"
}
$packagedPluginId = $Matches[1]
if (-not $packagedPluginId.Equals([string]$definition.pluginId, [StringComparison]::OrdinalIgnoreCase)) {
    throw "The production RHP declares plug-in id '$packagedPluginId' instead of the manifest id '$($definition.pluginId)'."
}
if ($pluginRows[0] -notmatch 'declared=True') {
    throw 'The production RHP does not declare an assembly-level plug-in id; Rhino would load it as Guid.Empty.'
}
if ($commandRows.Count -ne 0) { throw "The production RHP still contains $($commandRows.Count) Rhino command type(s)." }

if (Test-Path -LiteralPath $registryTestRoot) {
    Remove-Item -LiteralPath $registryTestRoot -Recurse -Force
}
New-Item -Path (Join-Path $registryPluginKey 'CommandList') -Force | Out-Null
New-Item -Path (Join-Path $registryPluginKey 'Panels') -Force | Out-Null
New-Item -Path (Join-Path $registryPluginKey 'PlugIn') -Force | Out-Null
New-ItemProperty -LiteralPath $registryPluginKey -Name Name -Value 'MCP_Rhino' -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath (Join-Path $registryPluginKey 'PlugIn') -Name FileName -Value 'C:\retired\MCP_Rhino.Server.rhp' -PropertyType String -Force | Out-Null

New-Item -ItemType Directory -Path $oldPluginRoot, $oldBinRoot -Force | Out-Null
Set-Content -LiteralPath (Join-Path $oldPluginRoot 'MCP_Rhino.Server.rhp') -Value 'synthetic duplicate-identity package' -Encoding ascii
Set-Content -LiteralPath (Join-Path $oldBinRoot 'legacy-router.txt') -Value 'synthetic old Router' -Encoding ascii
$priorManifest = [ordered]@{
    schemaVersion = 1
    product = 'MCP_Rhino'
    productVersion = '1.0.0'
    transportMode = 'router-only'
    routeProtocolVersion = 1
    pluginRoot = $oldPluginRoot
    files = @()
    clientAction = $null
}
$priorManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $productRoot 'install-manifest.json') -Encoding utf8

Set-Content -LiteralPath (Join-Path $legacyPackageRoot 'manifest.txt') -Value '1.0.0' -Encoding ascii
& $installer -Mode Install -BundleRoot $bundleRootFull -ProductRoot $productRoot -RhinoPluginRoot $rhinoPluginRoot -LegacyRhinoPackageRoot $legacyRhinoPackages -RhinoPluginRegistryRoot $registryTestRoot | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Isolated package upgrade failed.' }
if (Test-Path -LiteralPath $oldPluginRoot) { throw 'The prior owned 1.0.0 plug-in root remains discoverable.' }
if (Test-Path -LiteralPath $legacyPackageRoot) { throw 'The legacy Rhino Package Manager product root remains discoverable.' }
if (-not (Test-Path -LiteralPath (Join-Path $newPluginRoot 'MCP_Rhino.Server.rhp') -PathType Leaf)) {
    throw 'The corrected RHP was not installed.'
}
if (Test-Path -LiteralPath (Join-Path $newPluginRoot 'MCP_Rhino.Server.dll')) {
    throw 'The duplicate plug-in DLL was installed.'
}

& $installer -Mode Validate -BundleRoot $bundleRootFull -ProductRoot $productRoot -RhinoPluginRoot $rhinoPluginRoot -LegacyRhinoPackageRoot $legacyRhinoPackages -RhinoPluginRegistryRoot $registryTestRoot | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Isolated package validation failed.' }

$registration = Get-ItemProperty -LiteralPath $registryPluginKey
$registeredFile = Get-ItemProperty -LiteralPath (Join-Path $registryPluginKey 'PlugIn')
if ([int]$registration.LoadMode -ne 1) { throw 'Canonical Rhino registration is not AtStartup.' }
if ([int]$registration.DirectoryInstall -ne 0) { throw 'Canonical Rhino registration is not registry-only.' }
if (-not ([IO.Path]::GetFullPath([string]$registration.FileName)).Equals(
    [IO.Path]::GetFullPath((Join-Path $newPluginRoot 'MCP_Rhino.Server.rhp')),
    [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Shorthand Rhino startup registration does not point to the active installed RHP.'
}
if (-not ([IO.Path]::GetFullPath([string]$registeredFile.FileName)).Equals(
    [IO.Path]::GetFullPath((Join-Path $newPluginRoot 'MCP_Rhino.Server.rhp')),
    [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Canonical Rhino registration does not point to the active installed RHP.'
}
if (Test-Path -LiteralPath (Join-Path $registryPluginKey 'CommandList')) { throw 'Legacy Rhino command registration survived.' }
if (Test-Path -LiteralPath (Join-Path $registryPluginKey 'Panels')) { throw 'Legacy Rhino panel registration survived.' }

$tamperedBundle = Join-Path $tempRootFull 'TamperedBundle'
Copy-Item -LiteralPath $bundleRootFull -Destination $tamperedBundle -Recurse
Copy-Item -LiteralPath (Join-Path $tamperedBundle 'Plugin\MCP_Rhino.Server.rhp') `
    -Destination (Join-Path $tamperedBundle 'Plugin\MCP_Rhino.Server.dll')
$rejected = $false
try {
    & (Join-Path $tamperedBundle 'Installer\Install-McpRhino.ps1') `
        -Mode Validate `
        -BundleRoot $tamperedBundle `
        -ProductRoot $productRoot `
        -RhinoPluginRoot $rhinoPluginRoot `
        -LegacyRhinoPackageRoot $legacyRhinoPackages `
        -RhinoPluginRegistryRoot $registryTestRoot | Out-Host
} catch {
    if ($_.Exception.Message -notmatch 'Duplicate MCP_Rhino.Server.dll') {
        throw
    }
    $rejected = $true
}
if (-not $rejected) { throw 'A bundle containing the duplicate plug-in DLL was not rejected.' }

if (Test-Path -LiteralPath $registryTestRoot) {
    Remove-Item -LiteralPath $registryTestRoot -Recurse -Force
}

Write-Output "[OK] direct-rhp-package-regression|installed=$newPluginRoot|duplicateRejected=true|oldVersionDiscoverable=false|canonicalRegistration=true|legacyRegistrySurfaces=false"
