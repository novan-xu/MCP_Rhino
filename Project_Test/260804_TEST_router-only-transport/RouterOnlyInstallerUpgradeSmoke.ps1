[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BundleRoot
)

$ErrorActionPreference = 'Stop'
$BundleRoot = [IO.Path]::GetFullPath($BundleRoot)
$artifactBase = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts'))
$fixtureRoot = Join-Path $artifactBase 'installer-upgrade-smoke'
$resolvedFixtureRoot = [IO.Path]::GetFullPath($fixtureRoot)
$artifactPrefix = $artifactBase.TrimEnd('\') + '\'
if (-not $resolvedFixtureRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Fixture root is outside the test artifact directory: $resolvedFixtureRoot"
}

function Require([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    Write-Output "[OK] $Message"
}

function Binary-Contains([string] $Path, [string] $Value) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    $unicode = [Text.Encoding]::Unicode.GetString($bytes)
    return $ascii.IndexOf($Value, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $unicode.IndexOf($Value, [StringComparison]::OrdinalIgnoreCase) -ge 0
}

if (Test-Path -LiteralPath $resolvedFixtureRoot) {
    Remove-Item -LiteralPath $resolvedFixtureRoot -Recurse -Force
}

try {
    $productRoot = Join-Path $resolvedFixtureRoot 'product'
    $rhinoPackageRoot = Join-Path $resolvedFixtureRoot 'rhino-packages'
    $registryTestRoot = 'Registry::HKEY_CURRENT_USER\Software\MCP_Rhino\InstallerTests\RouterOnlyUpgrade\Plug-Ins'
    if (Test-Path -LiteralPath $registryTestRoot) {
        Remove-Item -LiteralPath $registryTestRoot -Recurse -Force
    }
    $legacyBin = Join-Path $productRoot 'bin'
    $legacyPlugin = Join-Path $rhinoPackageRoot 'MCP_Rhino\1.0.0'
    $legacyRollback = Join-Path $productRoot 'rollback\legacy-snapshot'
    $legacyStage = Join-Path $productRoot 'staged\legacy-bundle'
    New-Item -ItemType Directory -Path $legacyBin, $legacyPlugin, $legacyRollback, $legacyStage -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $legacyBin 'MCP_Rhino.Bridge.exe') -Value 'legacy bridge fixture' -Encoding ascii
    Set-Content -LiteralPath (Join-Path $legacyBin 'MCP_Rhino.Companion.exe') -Value 'legacy companion fixture' -Encoding ascii
    Set-Content -LiteralPath (Join-Path $legacyPlugin 'MCP_Rhino.Server.rhp') -Value 'Mcpchat McpRhinoClaudeCodeCompanionUiSmoke' -Encoding ascii
    Set-Content -LiteralPath (Join-Path $legacyRollback 'MCP_Rhino.Bridge.exe') -Value 'legacy rollback fixture' -Encoding ascii
    Set-Content -LiteralPath (Join-Path $legacyStage 'MCP_Rhino.Companion.exe') -Value 'legacy stage fixture' -Encoding ascii
    @{ schemaVersion = 1; product = 'MCP_Rhino'; productVersion = '0.9.0' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $legacyStage 'package-manifest.json') -Encoding utf8

    $legacyManifest = [ordered]@{
        schemaVersion = 1
        product = 'MCP_Rhino'
        productVersion = '1.0.0'
        routeProtocolVersion = 1
        files = @()
        rollbackRoot = $legacyRollback
        rollbackFiles = @(
            @{ path = (Join-Path $legacyRollback 'MCP_Rhino.Bridge.exe'); sha256 = 'fixture'; role = 'rollback' }
        )
        clientAction = $null
    }
    $legacyManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $productRoot 'install-manifest.json') -Encoding utf8

    $installer = Join-Path $BundleRoot 'Installer\Install-McpRhino.ps1'
    & $installer -Mode Install -BundleRoot $BundleRoot -ProductRoot $productRoot -RhinoPackageRoot $rhinoPackageRoot -RhinoPluginRegistryRoot $registryTestRoot | Out-Null

    $installedManifestPath = Join-Path $productRoot 'install-manifest.json'
    $installedManifest = Get-Content -LiteralPath $installedManifestPath -Raw | ConvertFrom-Json
    Require ($installedManifest.transportMode -eq 'router-only') 'Installed ownership manifest declares Router-only transport.'
    Require ($null -eq $installedManifest.rollbackRoot) 'Legacy rollback is destroyed after replacement Router validation.'
    Require (@($installedManifest.rollbackFiles).Count -eq 0) 'No legacy rollback file remains owned or accessible.'

    $retiredFileNames = @(Get-ChildItem -LiteralPath $resolvedFixtureRoot -File -Recurse | Where-Object {
        $_.Name -match '(?i)(MCP_Rhino\.Bridge|MCP_Rhino\.Companion)'
    })
    Require ($retiredFileNames.Count -eq 0) 'Legacy Bridge and Companion executable paths are absent after upgrade.'

    $definition = Get-Content -LiteralPath (Join-Path $BundleRoot 'package-manifest.json') -Raw | ConvertFrom-Json
    $installedPlugin = Join-Path $rhinoPackageRoot "MCP_Rhino\$($definition.productVersion)\MCP_Rhino.Server.rhp"
    foreach ($symbol in @(
        'Mcpchat',
        'McpDebugBridgeOnlyPluginSmoke',
        'McpPanelRuntimePolicySmoke',
        'McpRhinoClaudeCodeCompanionUiSmoke',
        'McpRhinoClaudeCodePanelSmoke'
    )) {
        Require (-not (Binary-Contains -Path $installedPlugin -Value $symbol)) "Installed Rhino plugin omits retired command symbol: $symbol"
    }

    & $installer -Mode Validate -BundleRoot $BundleRoot -ProductRoot $productRoot -RhinoPackageRoot $rhinoPackageRoot -RhinoPluginRegistryRoot $registryTestRoot | Out-Null
    Write-Output '[OK] Router-only installed state validates after a simulated legacy upgrade.'
    Write-Output '[OK] router-only-installer-upgrade-smoke-test'
}
finally {
    if ($null -ne $registryTestRoot -and (Test-Path -LiteralPath $registryTestRoot)) {
        Remove-Item -LiteralPath $registryTestRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $resolvedFixtureRoot) {
        Remove-Item -LiteralPath $resolvedFixtureRoot -Recurse -Force
    }
}
