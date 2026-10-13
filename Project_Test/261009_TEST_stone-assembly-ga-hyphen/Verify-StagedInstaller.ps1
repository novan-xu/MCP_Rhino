[CmdletBinding()]
param([Parameter(Mandatory)][string] $BundleRoot)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$fixture = Join-Path $repo '.validation\ga-hyphen-installer'
$registry = 'Registry::HKEY_CURRENT_USER\Software\MCP_Rhino\InstallerTests\GaHyphen\Plug-Ins'
$definition = Get-Content -Raw -LiteralPath (Join-Path $BundleRoot 'package-manifest.json') | ConvertFrom-Json
$rootKey = Join-Path $registry $definition.pluginId.ToLowerInvariant()
$pluginKey = Join-Path $rootKey 'PlugIn'
$expected = Join-Path $fixture "product\plugin\$($definition.productVersion)\MCP_Rhino.Server.rhp"
$installer = Join-Path $BundleRoot 'Installer\Install-McpRhino.ps1'
$parameters = @{
    BundleRoot = $BundleRoot
    ProductRoot = Join-Path $fixture 'product'
    RhinoPluginRoot = Join-Path $fixture 'product\plugin'
    LegacyRhinoPackageRoot = Join-Path $fixture 'legacy'
    RhinoPluginRegistryRoot = $registry
}
& $installer -Mode Install @parameters
& $installer -Mode Validate @parameters
if ((Get-ItemPropertyValue -LiteralPath $pluginKey -Name FileName) -ne $expected) { throw 'Wrong RHP registration.' }
if ((Get-Item -LiteralPath (Join-Path $rootKey 'CommandList')).GetValueNames().Count -ne 0) { throw 'Nonempty command list.' }
Remove-ItemProperty -LiteralPath $rootKey -Name FileName
& $installer -Mode Validate @parameters
Write-Output '[OK] Installer-owned PlugIn path and empty CommandList survive simulated Rhino root hydration.'
Set-ItemProperty -LiteralPath $pluginKey -Name FileName -Value 'C:\missing-rhp\MCP_Rhino.Server.rhp'
$rejected = $false
try { & $installer -Mode Validate @parameters } catch { $rejected = $true }
if (-not $rejected) { throw 'Stale installer-owned path was accepted.' }
Set-ItemProperty -LiteralPath $pluginKey -Name FileName -Value $expected
New-ItemProperty -LiteralPath (Join-Path $rootKey 'CommandList') -Name RetiredCommand -Value '2;RetiredCommand' -PropertyType String | Out-Null
$rejected = $false
try { & $installer -Mode Validate @parameters } catch { $rejected = $true }
if (-not $rejected) { throw 'Unexpected command was accepted.' }
Remove-ItemProperty -LiteralPath (Join-Path $rootKey 'CommandList') -Name RetiredCommand
& $installer -Mode Validate @parameters
Write-Output '[OK] Validation rejects a stale PlugIn path and unexpected production commands.'
Set-ItemProperty -LiteralPath $rootKey -Name Name -Value 'MCP_Rhino.Server'
& $installer -Mode Repair @parameters
& $installer -Mode Validate @parameters
Write-Output '[OK] Upgrade accepts the product name written by Rhino after successful load.'
Write-Output '[INFO] Test registry/filesystem fixture only; no production installation or host attestation claimed.'
