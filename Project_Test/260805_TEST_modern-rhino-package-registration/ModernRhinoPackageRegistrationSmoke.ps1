[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BundleRoot
)

$ErrorActionPreference = 'Stop'
$BundleRoot = [IO.Path]::GetFullPath($BundleRoot)
$artifactBase = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts'))
$fixtureRoot = Join-Path $artifactBase 'registration-smoke'
$registryRoot = 'Registry::HKEY_CURRENT_USER\Software\MCP_Rhino\InstallerTests\ModernPackageRegistration\Plug-Ins'

function Require([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    Write-Output "[OK] $Message"
}

if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
if (Test-Path -LiteralPath $registryRoot) { Remove-Item -LiteralPath $registryRoot -Recurse -Force }

try {
    $definition = Get-Content -LiteralPath (Join-Path $BundleRoot 'package-manifest.json') -Raw | ConvertFrom-Json
    $pluginId = [string]$definition.pluginId
    $pluginKey = Join-Path $registryRoot $pluginId.ToLowerInvariant()
    $pluginFileKey = Join-Path $pluginKey 'PlugIn'
    $installer = Join-Path $BundleRoot 'Installer\Install-McpRhino.ps1'
    $productRoot = Join-Path $fixtureRoot 'product'
    $legacyRhinoPackages = Join-Path $fixtureRoot 'rhino-packages'
    $rhinoPluginRoot = Join-Path $productRoot 'plugin'
    $installedRhp = Join-Path $rhinoPluginRoot "$($definition.productVersion)\MCP_Rhino.Server.rhp"

    # Reproduce the incomplete 1.1.2 registration that loaded the RHP but left
    # Rhino's package-install pass pending on every startup.
    New-Item -Path $pluginFileKey -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginKey -Name Name -Value 'MCP_Rhino' -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginKey -Name LoadMode -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginKey -Name IsDotNETPlugIn -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginKey -Name DirectoryInstall -Value 0 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginFileKey -Name FileName -Value 'C:\retired\MCP_Rhino.Server.rhp' -PropertyType String -Force | Out-Null

    & $installer -Mode Install -BundleRoot $BundleRoot -ProductRoot $productRoot `
        -RhinoPluginRoot $rhinoPluginRoot -LegacyRhinoPackageRoot $legacyRhinoPackages `
        -RhinoPluginRegistryRoot $registryRoot | Out-Null

    $registration = Get-ItemProperty -LiteralPath $pluginKey
    $pluginFile = Get-ItemProperty -LiteralPath $pluginFileKey
    Require ([int]$registration.LoadMode -eq 1) 'Installed registration loads MCP_Rhino at startup.'
    Require ([int]$registration.DirectoryInstall -eq 0) 'Installed registration is registry-only.'
    Require ([IO.Path]::GetFullPath([string]$pluginFile.FileName).Equals(
        [IO.Path]::GetFullPath($installedRhp), [StringComparison]::OrdinalIgnoreCase)) `
        'Installed registration points to the active package RHP.'

    & $installer -Mode Validate -BundleRoot $BundleRoot -ProductRoot $productRoot `
        -RhinoPluginRoot $rhinoPluginRoot -LegacyRhinoPackageRoot $legacyRhinoPackages `
        -RhinoPluginRegistryRoot $registryRoot | Out-Null
    Require $true 'Registry-only registration validates.'

    Set-ItemProperty -LiteralPath $pluginKey -Name DirectoryInstall -Value 1
    $rejectedIncomplete = $false
    try {
        & $installer -Mode Validate -BundleRoot $BundleRoot -ProductRoot $productRoot `
            -RhinoPluginRoot $rhinoPluginRoot -LegacyRhinoPackageRoot $legacyRhinoPackages `
            -RhinoPluginRegistryRoot $registryRoot | Out-Null
    } catch {
        $rejectedIncomplete = $true
    }
    Require $rejectedIncomplete 'Validation rejects Package Manager directory mode.'

    Set-ItemProperty -LiteralPath $pluginKey -Name DirectoryInstall -Value 0
    Set-ItemProperty -LiteralPath $pluginFileKey -Name FileName -Value (Join-Path $fixtureRoot 'stale\MCP_Rhino.Server.rhp')
    $rejectedStalePath = $false
    try {
        & $installer -Mode Validate -BundleRoot $BundleRoot -ProductRoot $productRoot `
            -RhinoPluginRoot $rhinoPluginRoot -LegacyRhinoPackageRoot $legacyRhinoPackages `
            -RhinoPluginRegistryRoot $registryRoot | Out-Null
    } catch {
        $rejectedStalePath = $true
    }
    Require $rejectedStalePath 'Validation rejects a completed registration that points to a stale RHP.'

    Write-Output '[OK] modern-rhino-package-registration-smoke-test'
}
finally {
    if (Test-Path -LiteralPath $registryRoot) { Remove-Item -LiteralPath $registryRoot -Recurse -Force }
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
