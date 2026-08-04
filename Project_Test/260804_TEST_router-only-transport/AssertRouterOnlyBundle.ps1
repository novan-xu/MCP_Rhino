[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BundleRoot
)

$ErrorActionPreference = 'Stop'
$BundleRoot = [IO.Path]::GetFullPath($BundleRoot)
if (-not (Test-Path -LiteralPath $BundleRoot -PathType Container)) {
    throw "Bundle root does not exist: $BundleRoot"
}

$names = @(Get-ChildItem -LiteralPath $BundleRoot -File -Recurse | ForEach-Object { $_.Name })
if ($names -notcontains 'MCP_Rhino.Router.exe') { throw 'Bundle is missing MCP_Rhino.Router.exe.' }
if ($names -notcontains 'MCP_Rhino.Server.rhp') { throw 'Bundle is missing MCP_Rhino.Server.rhp.' }
if ($names -contains 'MCP_Rhino.Bridge.exe') { throw 'Bundle must not contain MCP_Rhino.Bridge.exe.' }
if ($names -contains 'MCP_Rhino.Companion.exe') { throw 'Bundle must not contain MCP_Rhino.Companion.exe.' }

$bundleManifest = Get-Content -LiteralPath (Join-Path $BundleRoot 'bundle-manifest.json') -Raw | ConvertFrom-Json
$packageDefinition = Get-Content -LiteralPath (Join-Path $BundleRoot 'package-manifest.json') -Raw | ConvertFrom-Json
if ($bundleManifest.transportMode -ne 'router-only') { throw 'Bundle manifest must declare router-only transport.' }
if ($packageDefinition.transportMode -ne 'router-only') { throw 'Package manifest must declare router-only transport.' }

function Assert-BinaryDoesNotContain([string] $Path, [string[]] $Values) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    $unicode = [Text.Encoding]::Unicode.GetString($bytes)
    foreach ($value in $Values) {
        if ($ascii.IndexOf($value, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $unicode.IndexOf($value, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Retired command or transport symbol '$value' remains in $Path."
        }
    }
}

$retiredSymbols = @(
    'Mcpchat',
    'McpDebugBridgeOnlyPluginSmoke',
    'McpPanelRuntimePolicySmoke',
    'McpRhinoClaudeCodeCompanionUiSmoke',
    'McpRhinoClaudeCodePanelSmoke',
    'StartBoundPipeServer',
    'TryShowChatPanel',
    'MCP_Rhino.Companion'
)
$plugin = Join-Path $BundleRoot 'Plugin\MCP_Rhino.Server.rhp'
Assert-BinaryDoesNotContain -Path $plugin -Values $retiredSymbols

$router = Join-Path $BundleRoot 'bin\MCP_Rhino.Router.exe'
& $router --validate-install
if ($LASTEXITCODE -ne 0) { throw 'Bundled Router validation failed.' }

Write-Output '[OK] Bundle contains only the supported Router client transport.'
Write-Output '[OK] Bundled Rhino plugin contains no retired command or transport symbols.'
Write-Output '[OK] router-only-bundle-smoke-test'
