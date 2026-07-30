[CmdletBinding()]
param(
    [string] $OutputRoot = (Join-Path $PSScriptRoot 'artifacts'),
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$definition = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package-manifest.json') -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = [string]$definition.productVersion }
if ($Version -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') { throw "Invalid package version: $Version" }
foreach ($scriptName in @('Build-McpRhinoPackage.ps1', 'Install-McpRhino.ps1', 'Uninstall-McpRhino.ps1')) {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot $scriptName),
        [ref]$tokens,
        [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count -gt 0) {
        throw "$scriptName has PowerShell syntax errors: $($parseErrors[0].Message)"
    }
}

$outputRootFull = [IO.Path]::GetFullPath($OutputRoot)
$bundleRoot = Join-Path $outputRootFull "MCP_Rhino-$Version"
$workRoot = Join-Path $outputRootFull ".build-$Version"
foreach ($target in @($bundleRoot, $workRoot)) {
    $resolvedTarget = [IO.Path]::GetFullPath($target)
    if (-not $resolvedTarget.StartsWith($outputRootFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the output root: $resolvedTarget"
    }
    if (Test-Path -LiteralPath $resolvedTarget) { Remove-Item -LiteralPath $resolvedTarget -Recurse -Force }
}

New-Item -ItemType Directory -Path $bundleRoot, $workRoot | Out-Null
function Get-RelativePath([string] $BasePath, [string] $FullPath) {
    $base = [IO.Path]::GetFullPath($BasePath).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath($FullPath)
    if (-not $full.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside its expected base: $full"
    }
    return $full.Substring($base.Length)
}

$projects = [ordered]@{
    Server = 'src\MCP_Rhino.Server\MCP_Rhino.Server.csproj'
    Router = 'src\MCP_Rhino.Router\MCP_Rhino.Router.csproj'
    Bridge = 'src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj'
    Companion = 'src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj'
}

foreach ($item in $projects.GetEnumerator()) {
    $publishDir = Join-Path $workRoot $item.Key
    & dotnet publish (Join-Path $repoRoot $item.Value) -c Release --nologo --self-contained false -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "Release publish failed for $($item.Key)." }
}

$pluginSource = Join-Path $workRoot 'Server'
$pluginTarget = Join-Path $bundleRoot 'Plugin'
$binTarget = Join-Path $bundleRoot 'bin'
New-Item -ItemType Directory -Path $pluginTarget, $binTarget | Out-Null
Copy-Item -Path (Join-Path $pluginSource '*') -Destination $pluginTarget -Recurse -Force
if ((-not (Test-Path -LiteralPath (Join-Path $pluginTarget 'MCP_Rhino.Server.rhp'))) -and
    (Test-Path -LiteralPath (Join-Path $pluginTarget 'MCP_Rhino.Server.dll'))) {
    Copy-Item -LiteralPath (Join-Path $pluginTarget 'MCP_Rhino.Server.dll') -Destination (Join-Path $pluginTarget 'MCP_Rhino.Server.rhp')
}
if (-not (Test-Path -LiteralPath (Join-Path $pluginTarget 'MCP_Rhino.Server.rhp'))) {
    throw 'Release publish did not produce MCP_Rhino.Server.rhp.'
}

function Merge-PublishTree([string] $Source, [string] $Destination) {
    Get-ChildItem -LiteralPath $Source -File -Recurse | ForEach-Object {
        $relative = Get-RelativePath $Source $_.FullName
        $target = Join-Path $Destination $relative
        $targetDirectory = Split-Path -Parent $target
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        if (Test-Path -LiteralPath $target) {
            $sourceHash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            $targetHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
            if ($sourceHash -ne $targetHash) { throw "Conflicting publish files: $relative" }
        } else {
            Copy-Item -LiteralPath $_.FullName -Destination $target
        }
    }
}

foreach ($name in @('Router', 'Bridge', 'Companion')) {
    Merge-PublishTree (Join-Path $workRoot $name) $binTarget
}

$assetsTarget = Join-Path $bundleRoot 'Installer'
New-Item -ItemType Directory -Path $assetsTarget | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-McpRhino.ps1') -Destination $assetsTarget
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall-McpRhino.ps1') -Destination $assetsTarget
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ClientConfigs') -Destination $assetsTarget -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'package-manifest.json') -Destination $bundleRoot

$files = Get-ChildItem -LiteralPath $bundleRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = (Get-RelativePath $bundleRoot $_.FullName).Replace('\', '/')
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        length = $_.Length
    }
}
$bundleManifest = [ordered]@{
    schemaVersion = 1
    product = $definition.product
    productVersion = $Version
    routeProtocolVersion = [int]$definition.routeProtocolVersion
    rhinoMajorVersion = [int]$definition.rhinoMajorVersion
    pluginId = $definition.pluginId
    createdUtc = [DateTime]::UtcNow.ToString('O')
    files = @($files)
}
$bundleManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $bundleRoot 'bundle-manifest.json') -Encoding utf8

Remove-Item -LiteralPath $workRoot -Recurse -Force
Write-Output $bundleRoot
