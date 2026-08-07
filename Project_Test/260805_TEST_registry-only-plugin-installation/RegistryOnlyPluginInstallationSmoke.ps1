[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BundleRoot
)

$ErrorActionPreference = 'Stop'
$BundleRoot = [IO.Path]::GetFullPath($BundleRoot)
$artifactBase = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts'))
$fixtureRoot = Join-Path $artifactBase 'registry-only-smoke'
$registryRoot = 'Registry::HKEY_CURRENT_USER\Software\MCP_Rhino\InstallerTests\RegistryOnlyInstallation\Plug-Ins'

function Require([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    Write-Output "[OK] $Message"
}

if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
if (Test-Path -LiteralPath $registryRoot) { Remove-Item -LiteralPath $registryRoot -Recurse -Force }

try {
    $definition = Get-Content -LiteralPath (Join-Path $BundleRoot 'package-manifest.json') -Raw | ConvertFrom-Json
    $pluginId = [string]$definition.pluginId
    $productRoot = Join-Path $fixtureRoot 'product'
    $pluginBase = Join-Path $productRoot 'plugin'
    $legacyPackages = Join-Path $fixtureRoot 'rhino-packages'
    $legacyProduct = Join-Path $legacyPackages 'MCP_Rhino'
    $legacyPlugin = Join-Path $legacyProduct '1.1.4'
    $legacyRhp = Join-Path $legacyPlugin 'MCP_Rhino.Server.rhp'
    $pluginKey = Join-Path $registryRoot $pluginId.ToLowerInvariant()
    $pluginFileKey = Join-Path $pluginKey 'PlugIn'
    $installer = Join-Path $BundleRoot 'Installer\Install-McpRhino.ps1'

    New-Item -ItemType Directory -Path $legacyPlugin, $productRoot, $pluginFileKey -Force | Out-Null
    Set-Content -LiteralPath $legacyRhp -Value 'owned legacy package-discovered RHP' -Encoding ascii
    Set-Content -LiteralPath (Join-Path $legacyProduct 'manifest.txt') -Value '1.1.4' -Encoding ascii
    New-ItemProperty -LiteralPath $pluginKey -Name Name -Value 'MCP_Rhino' -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginKey -Name LoadMode -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginKey -Name DirectoryInstall -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginFileKey -Name FileName -Value $legacyRhp -PropertyType String -Force | Out-Null

    [ordered]@{
        schemaVersion = 1
        product = 'MCP_Rhino'
        productVersion = '1.1.4'
        transportMode = 'router-only'
        routeProtocolVersion = 1
        pluginRoot = $legacyPlugin
        files = @()
        rollbackFiles = @()
        clientAction = $null
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $productRoot 'install-manifest.json') -Encoding utf8

    & $installer -Mode Install -BundleRoot $BundleRoot -ProductRoot $productRoot `
        -RhinoPluginRoot $pluginBase -LegacyRhinoPackageRoot $legacyPackages `
        -RhinoPluginRegistryRoot $registryRoot | Out-Null

    $newPlugin = Join-Path $pluginBase "$($definition.productVersion)\MCP_Rhino.Server.rhp"
    Require (Test-Path -LiteralPath $newPlugin -PathType Leaf) 'RHP is installed under the registry-only product root.'
    Require (-not (Test-Path -LiteralPath $legacyProduct)) 'Legacy Package Manager product root is removed.'

    $registration = Get-ItemProperty -LiteralPath $pluginKey
    $pluginFile = Get-ItemProperty -LiteralPath $pluginFileKey
    Require ([int]$registration.LoadMode -eq 1) 'Registry-only plug-in loads at Rhino startup.'
    Require ([int]$registration.DirectoryInstall -eq 0) 'Registration is not marked as a Package Manager directory install.'
    Require ([IO.Path]::GetFullPath([string]$registration.FileName).Equals(
        [IO.Path]::GetFullPath($newPlugin), [StringComparison]::OrdinalIgnoreCase)) `
        'Shorthand startup registration points at the registry-only RHP.'
    Require ([IO.Path]::GetFullPath([string]$pluginFile.FileName).Equals(
        [IO.Path]::GetFullPath($newPlugin), [StringComparison]::OrdinalIgnoreCase)) `
        'Registration points at the sole registry-only RHP.'

    $manifest = Get-Content -LiteralPath (Join-Path $productRoot 'install-manifest.json') -Raw | ConvertFrom-Json
    Require ($manifest.pluginRegistrationMode -eq 'registry-only') 'Ownership manifest records registry-only mode.'

    & $installer -Mode Validate -BundleRoot $BundleRoot -ProductRoot $productRoot `
        -RhinoPluginRoot $pluginBase -LegacyRhinoPackageRoot $legacyPackages `
        -RhinoPluginRegistryRoot $registryRoot | Out-Null
    Require $true 'Registry-only installation validates.'

    New-Item -ItemType Directory -Path $legacyPlugin -Force | Out-Null
    Set-Content -LiteralPath $legacyRhp -Value 'stray duplicate RHP' -Encoding ascii
    $rejectedDuplicate = $false
    try {
        & $installer -Mode Validate -BundleRoot $BundleRoot -ProductRoot $productRoot `
            -RhinoPluginRoot $pluginBase -LegacyRhinoPackageRoot $legacyPackages `
            -RhinoPluginRegistryRoot $registryRoot | Out-Null
    } catch {
        $rejectedDuplicate = $true
    }
    Require $rejectedDuplicate 'Validation rejects a rediscovered Package Manager RHP.'

    Remove-Item -LiteralPath $legacyProduct -Recurse -Force
    & (Join-Path $BundleRoot 'Installer\Uninstall-McpRhino.ps1') -ProductRoot $productRoot -Confirm:$false | Out-Null
    Require (-not (Test-Path -LiteralPath $pluginKey)) 'Uninstall removes the installer-owned Rhino registration.'
    Require (-not (Test-Path -LiteralPath (Join-Path $productRoot 'install-manifest.json'))) 'Uninstall removes the ownership manifest.'
    Require (-not (Test-Path -LiteralPath $pluginBase)) 'Uninstall removes the registry-only plug-in tree.'

    Write-Output '[OK] registry-only-plugin-installation-smoke-test'
}
finally {
    if (Test-Path -LiteralPath $registryRoot) { Remove-Item -LiteralPath $registryRoot -Recurse -Force }
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
