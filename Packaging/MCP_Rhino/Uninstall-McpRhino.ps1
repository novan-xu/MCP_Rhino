[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $ProductRoot = (Join-Path $env:LOCALAPPDATA 'MCP_Rhino')
)

$ErrorActionPreference = 'Stop'
$ProductRoot = [IO.Path]::GetFullPath($ProductRoot)
$manifestPath = Join-Path $ProductRoot 'install-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "No installer-owned MCP_Rhino manifest exists at $manifestPath." }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.product -ne 'MCP_Rhino' -or $manifest.schemaVersion -ne 1) { throw 'Unsupported installation manifest.' }

$defaultRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'MCP_Rhino'))
if ($ProductRoot.Equals($defaultRoot, [StringComparison]::OrdinalIgnoreCase)) {
    $active = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq 'Rhino' -or $_.ProcessName -eq 'MCP_Rhino.Router' })
    if ($active.Count -gt 0) { throw 'Close Rhino and MCP_Rhino.Router processes before uninstalling.' }
}

if ($null -ne $manifest.clientAction) {
    $action = $manifest.clientAction
    if (Test-Path -LiteralPath $action.path) {
        $config = Get-Content -LiteralPath $action.path -Raw | ConvertFrom-Json
        $current = $config.mcpServers.PSObject.Properties['mcp-rhino']
        $currentJson = if ($null -eq $current) { $null } else { $current.Value | ConvertTo-Json -Depth 20 -Compress }
        $ownedJson = $action.installedEntry | ConvertTo-Json -Depth 20 -Compress
        if ($currentJson -eq $ownedJson) {
            $config.mcpServers.PSObject.Properties.Remove('mcp-rhino')
            if ($null -ne $action.previousEntryJson) {
                $previous = $action.previousEntryJson | ConvertFrom-Json
                $config.mcpServers | Add-Member -NotePropertyName 'mcp-rhino' -NotePropertyValue $previous
            }
            $config | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $action.path -Encoding utf8
        } else {
            Write-Warning "Client entry changed after installation and was left untouched: $($action.path)"
        }
    }
}

$retained = @()
$ownedFiles = @($manifest.files) + @($manifest.rollbackFiles)
foreach ($file in $ownedFiles) {
    if (-not (Test-Path -LiteralPath $file.path -PathType Leaf)) { continue }
    $actual = (Get-FileHash -LiteralPath $file.path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $file.sha256) {
        $retained += $file.path
        Write-Warning "Modified installer file was left untouched: $($file.path)"
        continue
    }
    if ($PSCmdlet.ShouldProcess($file.path, 'Remove installer-owned file')) { Remove-Item -LiteralPath $file.path -Force }
}

$cleanupRoots = @(
    [string]$manifest.pluginRoot,
    [string]$manifest.binRoot,
    [string]$manifest.rollbackRoot,
    (Split-Path -Parent ([string]$manifest.pluginRoot))
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object { $_.Length } -Descending -Unique
foreach ($root in $cleanupRoots) {
    if (Test-Path -LiteralPath $root -PathType Container) {
        Get-ChildItem -LiteralPath $root -Directory -Recurse |
            Sort-Object { $_.FullName.Length } -Descending |
            ForEach-Object {
                if (-not (Get-ChildItem -LiteralPath $_.FullName -Force | Select-Object -First 1)) {
                    Remove-Item -LiteralPath $_.FullName
                }
            }
    }
    if ((Test-Path -LiteralPath $root -PathType Container) -and -not (Get-ChildItem -LiteralPath $root -Force | Select-Object -First 1)) {
        Remove-Item -LiteralPath $root
    }
}
if ($retained.Count -eq 0) {
    Remove-Item -LiteralPath $manifestPath -Force
    Write-Output 'MCP_Rhino installer-owned files and client entry were removed. No Rhino documents were touched.'
} else {
    Write-Warning 'Uninstall retained modified files and the ownership manifest for manual review.'
}
