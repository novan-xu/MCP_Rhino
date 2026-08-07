[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    # Optional explicit plug-in files, in product order (MCP_Rhino, PanelCladdingEditor).
    # Use this to check packaged bundle output instead of repository build output.
    [string[]] $PluginPath,

    # Skip the RHP rebuild. Only safe when the expected .rhp files already exist.
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# Each entry pairs an RHP project with the package manifest that declares its canonical plug-in id.
$products = @(
    [pscustomobject]@{
        Name     = 'MCP_Rhino'
        Project  = Join-Path $repoRoot 'src\MCP_Rhino.Server\MCP_Rhino.Server.csproj'
        Rhp      = Join-Path $repoRoot "src\MCP_Rhino.Server\bin\$Configuration\net8.0\MCP_Rhino.Server.rhp"
        Manifest = Join-Path $repoRoot 'Packaging\MCP_Rhino\package-manifest.json'
    },
    [pscustomobject]@{
        Name     = 'PanelCladdingEditor'
        Project  = Join-Path $repoRoot 'src\PanelCladdingEditor\PanelCladdingEditor.csproj'
        Rhp      = Join-Path $repoRoot "src\PanelCladdingEditor\bin\$Configuration\net8.0\PanelCladdingEditor.rhp"
        Manifest = Join-Path $repoRoot 'Packaging\PanelCladdingEditor\package-manifest.json'
    }
)

if ($PluginPath) {
    if ($PluginPath.Count -ne $products.Count) {
        throw "-PluginPath must list exactly $($products.Count) files, in product order: $($products.Name -join ', ')."
    }
    for ($index = 0; $index -lt $products.Count; $index++) {
        $products[$index].Rhp = (Resolve-Path -LiteralPath $PluginPath[$index]).Path
    }
}
elseif (-not $SkipBuild) {
    # A solution build leaves PanelCladdingEditor's output as the test-host .dll, so rebuild each
    # plug-in project on its own to restore the .rhp that Rhino actually loads.
    foreach ($product in $products) {
        & dotnet build $product.Project -c $Configuration --nologo | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "$($product.Name) RHP build failed." }
    }
}

foreach ($product in $products) {
    if (-not (Test-Path -LiteralPath $product.Rhp -PathType Leaf)) {
        throw "Missing plug-in file for $($product.Name): $($product.Rhp)"
    }
}

$probeProject = Join-Path $PSScriptRoot 'PluginAssemblyIdentityProbe\PluginAssemblyIdentityProbe.csproj'
& dotnet build $probeProject -c Release --nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Plug-in assembly identity probe build failed.' }
$probe = Join-Path $PSScriptRoot 'PluginAssemblyIdentityProbe\bin\Release\net8.0\PluginAssemblyIdentityProbe.dll'

$rows = @(& dotnet $probe @($products | ForEach-Object { $_.Rhp }))
if ($LASTEXITCODE -ne 0) { throw 'Plug-in assembly identity enumeration failed.' }
if ($rows.Count -ne $products.Count) {
    throw "Expected $($products.Count) identity rows, received $($rows.Count)."
}

$seen = @{}
for ($index = 0; $index -lt $products.Count; $index++) {
    $product = $products[$index]
    $row = $rows[$index]
    if ($row -notmatch 'pluginId=([0-9a-fA-F-]{36})\|declared=(True|False)') {
        throw "Unparsable identity row for $($product.Name): $row"
    }
    $pluginId = $Matches[1]
    $declared = $Matches[2]

    if ($declared -ne 'True') {
        throw "$($product.Name) does not declare an assembly-level GuidAttribute; Rhino would load it as Guid.Empty."
    }
    if ($pluginId -eq '00000000-0000-0000-0000-000000000000') {
        throw "$($product.Name) resolves to an empty Rhino plug-in id."
    }

    $expected = (Get-Content -Raw -LiteralPath $product.Manifest | ConvertFrom-Json).pluginId
    if (-not $pluginId.Equals([string]$expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$($product.Name) declares plug-in id '$pluginId' instead of the manifest id '$expected'."
    }

    $key = $pluginId.ToLowerInvariant()
    if ($seen.ContainsKey($key)) {
        throw "$($product.Name) shares plug-in id '$pluginId' with $($seen[$key]); Rhino rejects the second load with 'ID already in use'."
    }
    $seen[$key] = $product.Name

    Write-Output "[OK] $($product.Name)|configuration=$Configuration|pluginId=$pluginId"
}

Write-Output "[OK] rhino-plugin-assembly-identity|configuration=$Configuration|products=$($products.Count)|distinctPluginIds=$($seen.Count)"
