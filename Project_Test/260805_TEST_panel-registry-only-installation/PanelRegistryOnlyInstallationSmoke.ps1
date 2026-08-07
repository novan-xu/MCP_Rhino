[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BundleRoot
)

$ErrorActionPreference = 'Stop'
$BundleRoot = [IO.Path]::GetFullPath($BundleRoot)
$artifactBase = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts'))
$fixtureRoot = Join-Path $artifactBase 'panel-registry-only-smoke'
$registryRoot = 'Registry::HKEY_CURRENT_USER\Software\MCP_Rhino\InstallerTests\PanelRegistryOnlyInstallation\Plug-Ins'

function Require([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    Write-Output "[OK] $Message"
}

if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
if (Test-Path -LiteralPath $registryRoot) { Remove-Item -LiteralPath $registryRoot -Recurse -Force }

try {
    $definition = Get-Content -Raw -LiteralPath (Join-Path $BundleRoot 'package-manifest.json') | ConvertFrom-Json
    $pluginId = [string]$definition.pluginId
    $productRoot = Join-Path $fixtureRoot 'product'
    $pluginBase = Join-Path $productRoot 'plugin'
    $packageBase = Join-Path $fixtureRoot 'rhino-packages'
    $sourceRhp = Join-Path $BundleRoot 'Plugin\PanelCladdingEditor.rhp'
    foreach ($legacyName in @('PanelCladdingEditor', 'BayHealthPanelCladdingEditor')) {
        $legacyVersion = Join-Path (Join-Path $packageBase $legacyName) '1.0.5'
        New-Item -ItemType Directory -Path $legacyVersion -Force | Out-Null
        Copy-Item -LiteralPath $sourceRhp -Destination (Join-Path $legacyVersion 'PanelCladdingEditor.rhp')
        Set-Content -LiteralPath (Join-Path (Split-Path -Parent $legacyVersion) 'manifest.txt') -Value '1.0.5' -Encoding ascii
    }

    $installer = Join-Path $BundleRoot 'Installer\Install-PanelCladdingEditor.ps1'
    & $installer -Mode Install -BundleRoot $BundleRoot -ProductRoot $productRoot `
        -RhinoPluginRoot $pluginBase -LegacyRhinoPackageRoot $packageBase `
        -RhinoPluginRegistryRoot $registryRoot | Out-Null

    $activeRoot = Join-Path $pluginBase ([string]$definition.version)
    $activeRhp = Join-Path $activeRoot 'PanelCladdingEditor.rhp'
    Require (Test-Path -LiteralPath $activeRhp -PathType Leaf) 'RHP is installed under the registry-only product root.'
    Require (-not (Test-Path -LiteralPath (Join-Path $packageBase 'PanelCladdingEditor'))) 'PanelCladdingEditor legacy package root is removed from discovery.'
    Require (-not (Test-Path -LiteralPath (Join-Path $packageBase 'BayHealthPanelCladdingEditor'))) 'BayHealth legacy package root is removed from discovery.'
    Require (@(Get-ChildItem -LiteralPath $fixtureRoot -Filter 'PanelCladdingEditor.rhp' -File -Recurse).Count -eq 3) `
        'One active RHP plus two recoverable non-discovery migration backups exist.'

    $pluginKey = Join-Path $registryRoot $pluginId.ToLowerInvariant()
    $root = Get-ItemProperty -LiteralPath $pluginKey
    $child = Get-ItemProperty -LiteralPath (Join-Path $pluginKey 'PlugIn')
    Require ([int]$root.LoadMode -eq 1) 'Registry-only plug-in loads at Rhino startup.'
    Require ([int]$root.DirectoryInstall -eq 0) 'Registration is not marked as a Package Manager directory install.'
    Require ([IO.Path]::GetFullPath([string]$root.FileName).Equals([IO.Path]::GetFullPath($activeRhp), [StringComparison]::OrdinalIgnoreCase)) `
        'Top-level bootstrap registration points at the registry-only RHP.'
    Require ([IO.Path]::GetFullPath([string]$child.FileName).Equals([IO.Path]::GetFullPath($activeRhp), [StringComparison]::OrdinalIgnoreCase)) `
        'Detailed registration points at the registry-only RHP.'

    $ownership = Get-Content -Raw -LiteralPath (Join-Path $productRoot 'install-manifest.json') | ConvertFrom-Json
    Require ($ownership.pluginRegistrationMode -eq 'registry-only') 'Ownership manifest records registry-only mode.'
    Require (@($ownership.migrationBackups).Count -eq 2) 'Ownership manifest records both legacy package migrations.'

    & $installer -Mode Validate -BundleRoot $BundleRoot -ProductRoot $productRoot `
        -RhinoPluginRoot $pluginBase -LegacyRhinoPackageRoot $packageBase `
        -RhinoPluginRegistryRoot $registryRoot | Out-Null
    Require $true 'Registry-only installation validates.'

    $strayRoot = Join-Path $packageBase 'UnexpectedPanelCopy\1.0.6'
    New-Item -ItemType Directory -Path $strayRoot -Force | Out-Null
    Copy-Item -LiteralPath $sourceRhp -Destination (Join-Path $strayRoot 'PanelCladdingEditor.rhp')
    $rejected = $false
    try {
        & $installer -Mode Validate -BundleRoot $BundleRoot -ProductRoot $productRoot `
            -RhinoPluginRoot $pluginBase -LegacyRhinoPackageRoot $packageBase `
            -RhinoPluginRegistryRoot $registryRoot | Out-Null
    } catch {
        $rejected = $true
    }
    Require $rejected 'Validation rejects a rediscovered Package Manager RHP.'
    Remove-Item -LiteralPath (Join-Path $packageBase 'UnexpectedPanelCopy') -Recurse -Force

    & (Join-Path $BundleRoot 'Installer\Uninstall-PanelCladdingEditor.ps1') `
        -ProductRoot $productRoot -Confirm:$false | Out-Null
    Require (-not (Test-Path -LiteralPath $pluginKey)) 'Uninstall removes the installer-owned Rhino registration.'
    Require (-not (Test-Path -LiteralPath $activeRoot)) 'Uninstall removes the active registry-only plug-in tree.'
    Require (-not (Test-Path -LiteralPath (Join-Path $productRoot 'install-manifest.json'))) 'Uninstall removes the ownership manifest.'
    Require (Test-Path -LiteralPath (Join-Path $productRoot 'rollback') -PathType Container) 'Uninstall retains recoverable migration backups.'

    Write-Output '[OK] panel-registry-only-installation-smoke-test'
}
finally {
    if (Test-Path -LiteralPath $registryRoot) { Remove-Item -LiteralPath $registryRoot -Recurse -Force }
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
