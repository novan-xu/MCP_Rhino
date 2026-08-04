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

function Merge-PublishTree(
    [string] $Source,
    [string] $Destination,
    [string[]] $ExcludeRelativePaths = @()) {
    Get-ChildItem -LiteralPath $Source -File -Recurse | ForEach-Object {
        $relative = Get-RelativePath $Source $_.FullName
        if ($ExcludeRelativePaths -contains $relative) {
            return
        }

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

function Get-ManagedEntryPointToken([string] $Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 256) { throw "Managed PE file is too small: $Path" }
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
    if ($peOffset -lt 0 -or $peOffset + 24 -ge $bytes.Length -or
        [BitConverter]::ToUInt32($bytes, $peOffset) -ne 0x00004550) {
        throw "Invalid PE signature: $Path"
    }

    $sectionCount = [BitConverter]::ToUInt16($bytes, $peOffset + 6)
    $optionalHeaderSize = [BitConverter]::ToUInt16($bytes, $peOffset + 20)
    $optionalHeader = $peOffset + 24
    $magic = [BitConverter]::ToUInt16($bytes, $optionalHeader)
    $dataDirectories = if ($magic -eq 0x20b) { $optionalHeader + 112 } elseif ($magic -eq 0x10b) { $optionalHeader + 96 } else { throw "Unsupported PE optional header: $Path" }
    $cliRva = [BitConverter]::ToUInt32($bytes, $dataDirectories + (14 * 8))
    if ($cliRva -eq 0) { throw "Managed CLI header is missing: $Path" }

    $sectionTable = $optionalHeader + $optionalHeaderSize
    for ($index = 0; $index -lt $sectionCount; $index++) {
        $section = $sectionTable + ($index * 40)
        $virtualSize = [BitConverter]::ToUInt32($bytes, $section + 8)
        $virtualAddress = [BitConverter]::ToUInt32($bytes, $section + 12)
        $rawSize = [BitConverter]::ToUInt32($bytes, $section + 16)
        $rawPointer = [BitConverter]::ToUInt32($bytes, $section + 20)
        $mappedSize = [Math]::Max($virtualSize, $rawSize)
        if ($cliRva -ge $virtualAddress -and $cliRva -lt ($virtualAddress + $mappedSize)) {
            $cliOffset = $rawPointer + ($cliRva - $virtualAddress)
            return [BitConverter]::ToUInt32($bytes, [int]$cliOffset + 20)
        }
    }

    throw "Managed CLI header does not map to a PE section: $Path"
}

$serverProject = Join-Path $repoRoot 'src\MCP_Rhino.Server\MCP_Rhino.Server.csproj'
$routerProject = Join-Path $repoRoot 'src\MCP_Rhino.Router\MCP_Rhino.Router.csproj'
$runtimePublish = Join-Path $workRoot 'ServerRuntime'
$routerPublish = Join-Path $workRoot 'Router'
$pluginBuild = Join-Path $workRoot 'ServerPlugin'

& dotnet publish $serverProject -c Release --nologo --self-contained false `
    -p:McpRhinoIsolatedRuntime=true -o $runtimePublish
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed for the isolated Server runtime.' }

& dotnet publish $routerProject -c Release --nologo --self-contained false -o $routerPublish
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed for Router.' }

# Publishing an executable project normalizes the self runtime asset back to
# .dll. Rebuild the production shape directly so both the PE file and deps.json
# use MCP_Rhino.Server.rhp.
& dotnet build $serverProject -c Release --nologo -t:Rebuild -o $pluginBuild
if ($LASTEXITCODE -ne 0) { throw 'Direct Release RHP build failed for Server.' }

$pluginTarget = Join-Path $bundleRoot 'Plugin'
$binTarget = Join-Path $bundleRoot 'bin'
New-Item -ItemType Directory -Path $pluginTarget, $binTarget | Out-Null
Merge-PublishTree $pluginBuild $pluginTarget @('MCP_Rhino.Server.exe', 'MCP_Rhino.Server.pdb')
Merge-PublishTree $runtimePublish $pluginTarget
Merge-PublishTree $routerPublish $binTarget

$rhpPath = Join-Path $pluginTarget 'MCP_Rhino.Server.rhp'
$duplicateDllPath = Join-Path $pluginTarget 'MCP_Rhino.Server.dll'
$duplicateExePath = Join-Path $pluginTarget 'MCP_Rhino.Server.exe'
$runtimeDllPath = Join-Path $pluginTarget 'MCP_Rhino.Server.Runtime.dll'
$rhpDepsPath = Join-Path $pluginTarget 'MCP_Rhino.Server.deps.json'
$runtimeDepsPath = Join-Path $pluginTarget 'MCP_Rhino.Server.Runtime.deps.json'
if (-not (Test-Path -LiteralPath $rhpPath -PathType Leaf)) { throw 'Direct Release build did not produce MCP_Rhino.Server.rhp.' }
if (Test-Path -LiteralPath $duplicateDllPath) { throw 'Bundle contains the duplicate MCP_Rhino.Server.dll plug-in identity.' }
if (Test-Path -LiteralPath $duplicateExePath) { throw 'Bundle contains the development-only MCP_Rhino.Server.exe apphost.' }
if (-not (Test-Path -LiteralPath $runtimeDllPath -PathType Leaf)) { throw 'Bundle is missing MCP_Rhino.Server.Runtime.dll.' }
if (-not (Test-Path -LiteralPath $rhpDepsPath -PathType Leaf)) { throw 'Bundle is missing MCP_Rhino.Server.deps.json.' }
if (-not (Test-Path -LiteralPath $runtimeDepsPath -PathType Leaf)) { throw 'Bundle is missing MCP_Rhino.Server.Runtime.deps.json.' }

$rhpAssemblyName = [Reflection.AssemblyName]::GetAssemblyName($rhpPath).Name
$runtimeAssemblyName = [Reflection.AssemblyName]::GetAssemblyName($runtimeDllPath).Name
if ($rhpAssemblyName -ne 'MCP_Rhino.Server') { throw "Unexpected RHP assembly identity: $rhpAssemblyName" }
if ($runtimeAssemblyName -ne 'MCP_Rhino.Server.Runtime') { throw "Unexpected isolated runtime identity: $runtimeAssemblyName" }
if ((Get-ManagedEntryPointToken $rhpPath) -ne 0) { throw 'Production RHP contains a managed executable entry point.' }
if ((Get-ManagedEntryPointToken $runtimeDllPath) -ne 0) { throw 'Isolated runtime contains a managed executable entry point.' }
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
$runtimeBinaryText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($runtimeDllPath))
if ($runtimeBinaryText.IndexOf([string]$definition.pluginId, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw 'The isolated runtime contains the Rhino plug-in GUID.'
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
    transportMode = [string]$definition.transportMode
    routeProtocolVersion = [int]$definition.routeProtocolVersion
    rhinoMajorVersion = [int]$definition.rhinoMajorVersion
    pluginId = $definition.pluginId
    createdUtc = [DateTime]::UtcNow.ToString('O')
    files = @($files)
}
$bundleManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $bundleRoot 'bundle-manifest.json') -Encoding utf8

Remove-Item -LiteralPath $workRoot -Recurse -Force
Write-Output $bundleRoot
