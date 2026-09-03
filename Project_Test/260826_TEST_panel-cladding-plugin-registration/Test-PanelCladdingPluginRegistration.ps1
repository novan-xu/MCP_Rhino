[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $BundleRoot
)

$ErrorActionPreference = 'Stop'
$BundleRoot = [IO.Path]::GetFullPath($BundleRoot)
$definition = Get-Content -Raw -LiteralPath (Join-Path $BundleRoot 'package-manifest.json') | ConvertFrom-Json
$installer = Join-Path $BundleRoot 'Installer\Install-PanelCladdingEditor.ps1'
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw "Bundled installer is missing: $installer"
}

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-ExpectedFailure([scriptblock] $Action, [string] $ExpectedText) {
    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message.IndexOf($ExpectedText, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Expected failure containing '$ExpectedText', received: $($_.Exception.Message)"
        }
        return
    }
    throw "Expected failure containing '$ExpectedText', but the action succeeded."
}

$testId = [Guid]::NewGuid().ToString('N')
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "PanelCladdingRegistration-$testId"))
Assert-True ($testRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) "Unsafe test root: $testRoot"

$productRoot = Join-Path $testRoot 'Product'
$pluginRoot = Join-Path $productRoot 'plugin'
$legacyRoot = Join-Path $testRoot 'LegacyPackages'
$registryRoot = "Registry::HKEY_CURRENT_USER\Software\MCP_Rhino\Tests\PanelCladdingRegistration\$testId\Plug-Ins"
$registryCleanupRoot = "Registry::HKEY_CURRENT_USER\Software\MCP_Rhino\Tests\PanelCladdingRegistration\$testId"
$pluginKey = Join-Path $registryRoot ([string]$definition.pluginId).ToLowerInvariant()
$pluginFileKey = Join-Path $pluginKey 'PlugIn'
$commandListKey = Join-Path $pluginKey 'CommandList'
$activeRhp = [IO.Path]::GetFullPath((Join-Path (Join-Path $pluginRoot ([string]$definition.version)) 'PanelCladdingEditor.rhp'))
$expectedCommands = @(
    'PCClear',
    'PCCreate',
    'PCCrvTemplate',
    'PCEditor',
    'PCMatchCrv',
    'PCMatchSrf',
    'PCSpawnCrv',
    'PCSpawnSrf',
    'PCSyncCrv',
    'PCSyncSrf',
    'PCUpdate'
)

function Assert-FullRegistration {
    $registration = Get-ItemProperty -LiteralPath $pluginKey
    Assert-True ([string]$registration.Name -eq 'PanelCladdingEditor') 'Complete registration name is incorrect.'
    Assert-True ([string]::IsNullOrWhiteSpace([string]$registration.FileName)) 'Complete registration must not retain shorthand root FileName.'
    Assert-True ([int]$registration.LoadMode -eq 1) 'Complete registration must load at startup.'
    Assert-True ([int]$registration.Type -eq 16) 'Complete registration plug-in type is incorrect.'
    Assert-True ([int]$registration.IsDotNETPlugIn -eq 1) 'Complete registration must identify a managed plug-in.'
    Assert-True ([int]$registration.DirectoryInstall -eq 0) 'Complete registration must not use directory discovery.'
    Assert-True (Test-Path -LiteralPath $pluginFileKey) 'Complete registration is missing PlugIn.'
    Assert-True (Test-Path -LiteralPath $commandListKey) 'Complete registration is missing CommandList.'

    $pluginFile = Get-ItemProperty -LiteralPath $pluginFileKey
    Assert-True ([IO.Path]::GetFullPath([string]$pluginFile.FileName).Equals($activeRhp, [StringComparison]::OrdinalIgnoreCase)) 'Complete PlugIn FileName is incorrect.'

    $commandProperties = @((Get-ItemProperty -LiteralPath $commandListKey).PSObject.Properties |
        Where-Object { $_.Name -notmatch '^PS(Path|ParentPath|ChildName|Drive|Provider)$' })
    $commandNames = @($commandProperties | ForEach-Object { $_.Name })
    Assert-True ($commandNames.Count -eq $expectedCommands.Count) 'Complete CommandList count is incorrect.'
    foreach ($command in $expectedCommands) {
        Assert-True ($commandNames -contains $command) "Complete CommandList is missing $command."
        $property = $commandProperties | Where-Object Name -eq $command
        Assert-True ([string]$property.Value -eq "2;$command") "Complete CommandList value is incorrect for $command."
    }
}

$installerArgs = @{
    BundleRoot = $BundleRoot
    ProductRoot = $productRoot
    RhinoPluginRoot = $pluginRoot
    LegacyRhinoPackageRoot = $legacyRoot
    RhinoPluginRegistryRoot = $registryRoot
}

try {
    & $installer -Mode Install @installerArgs | Out-Host
    Assert-FullRegistration
    & $installer -Mode Validate @installerArgs | Out-Host

    Remove-Item -LiteralPath $pluginKey -Recurse -Force
    New-Item -Path $pluginKey -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginKey -Name Name -Value 'PanelCladdingEditor' -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginKey -Name FileName -Value $activeRhp -PropertyType String -Force | Out-Null
    Assert-ExpectedFailure {
        & $installer -Mode Validate @installerArgs | Out-Null
    } 'shorthand'

    & $installer -Mode Repair @installerArgs | Out-Host
    Assert-FullRegistration
    & $installer -Mode Validate @installerArgs | Out-Host

    Remove-ItemProperty -LiteralPath $commandListKey -Name PCCrvTemplate
    Assert-ExpectedFailure {
        & $installer -Mode Validate @installerArgs | Out-Null
    } 'command registration'

    & $installer -Mode Repair @installerArgs | Out-Host
    Assert-FullRegistration
    & $installer -Mode Validate @installerArgs | Out-Host

    Write-Output 'PASS: complete install, shorthand rejection, incomplete-command rejection, and full repair behaved as expected.'
}
finally {
    if (Test-Path -LiteralPath $registryCleanupRoot) {
        Remove-Item -LiteralPath $registryCleanupRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $testRoot) {
        $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
        Assert-True ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) "Refusing to remove unsafe test root: $resolvedTestRoot"
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
