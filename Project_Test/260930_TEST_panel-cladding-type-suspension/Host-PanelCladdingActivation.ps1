[CmdletBinding()]
param(
    [ValidateSet('Probe', 'Snapshot', 'Install', 'Validate')]
    [string] $Mode = 'Snapshot',
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,
    [string] $BundleRoot
)

$ErrorActionPreference = 'Stop'
$result = [ordered]@{ mode = $Mode; success = $false; processId = $PID }
try {
    $result.sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $productRoot = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor'
    if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
        $BundleRoot = Join-Path $productRoot 'staged\1.0.78-key-renumbering-260930'
    }
    $bundleRoot = [IO.Path]::GetFullPath($BundleRoot)
    $definition = Get-Content -Raw -LiteralPath (Join-Path $bundleRoot 'package-manifest.json') | ConvertFrom-Json
    if ($definition.name -ne 'PanelCladdingEditor' -or $definition.version -notmatch '^\d+\.\d+\.\d+$') {
        throw 'Unexpected bundle identity.'
    }
    $installer = Join-Path $bundleRoot 'Installer\Install-PanelCladdingEditor.ps1'
    if ($Mode -eq 'Probe') {
        $probe = 'HKCU:\Software\PanelCladdingEditor\CodexHostAttestation-260930'
        if (Test-Path -LiteralPath $probe) { throw 'Host probe key already exists.' }
        New-Item -Path $probe -Force | Out-Null
        $result.nonce = [Guid]::NewGuid().ToString('D')
        New-ItemProperty -LiteralPath $probe -Name Nonce -Value $result.nonce -PropertyType String | Out-Null
    }
    if ($Mode -eq 'Install') {
        if (@(Get-Process -Name Rhino -ErrorAction SilentlyContinue).Count -gt 0) {
            throw 'Rhino must be closed before activation.'
        }
        $pluginBase = [IO.Path]::GetFullPath((Join-Path $productRoot 'plugin'))
        $rollbackBase = [IO.Path]::GetFullPath((Join-Path $productRoot 'rollback'))
        $legacyBase = [IO.Path]::GetFullPath((Join-Path $env:APPDATA 'McNeel\Rhinoceros\packages\8.0'))
        $moveTargets = @($pluginBase, $rollbackBase, (Join-Path $pluginBase $definition.version))
        $moveTargets += @(Get-ChildItem -LiteralPath $pluginBase -Directory | ForEach-Object { $_.FullName })
        $moveTargets += @('PanelCladdingEditor', 'BayHealthPanelCladdingEditor') | ForEach-Object { Join-Path $legacyBase $_ }
        foreach ($target in $moveTargets) {
            $resolved = [IO.Path]::GetFullPath($target)
            if (-not ($resolved.StartsWith($productRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
                $resolved -eq (Join-Path $legacyBase 'PanelCladdingEditor') -or
                $resolved -eq (Join-Path $legacyBase 'BayHealthPanelCladdingEditor'))) {
                throw "Installer move target outside product directories: $resolved"
            }
            if ((Test-Path -LiteralPath $resolved) -and
                ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "Installer target is a reparse point: $resolved"
            }
        }
        $result.validatedMoveTargets = $moveTargets
        $evidenceRoot = Split-Path -Parent $OutputPath
        Copy-Item -LiteralPath (Join-Path $productRoot 'install-manifest.json') -Destination (Join-Path $evidenceRoot 'activation-prior-manifest.log')
        & "$env:SystemRoot\System32\reg.exe" export 'HKCU\Software\McNeel\Rhinoceros\8.0\Plug-Ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35' (Join-Path $evidenceRoot 'activation-prior-registry.reg') /y | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Host registry backup failed.' }
        $result.installOutput = @(& $installer -Mode Install -BundleRoot $bundleRoot)
        $result.validateOutput = @(& $installer -Mode Validate -BundleRoot $bundleRoot)
    }
    if ($Mode -eq 'Validate') {
        $result.validateOutput = @(& $installer -Mode Validate -BundleRoot $bundleRoot)
    }

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class PanelRegistryTime {
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)]
    static extern int RegQueryInfoKey(IntPtr key, IntPtr cls, IntPtr clsLen, IntPtr reserved,
        IntPtr subKeys, IntPtr maxSubKeyLen, IntPtr maxClassLen, IntPtr values,
        IntPtr maxValueNameLen, IntPtr maxValueLen, IntPtr securityLen, out long written);
    public static string Read(IntPtr key) {
        long value;
        int error = RegQueryInfoKey(key, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
            IntPtr.Zero, IntPtr.Zero, out value);
        if (error != 0) throw new System.ComponentModel.Win32Exception(error);
        return DateTime.FromFileTimeUtc(value).ToString("O");
    }
}
'@
    $subkey = 'Software\McNeel\Rhinoceros\8.0\Plug-Ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35'
    $result.keys = @()
    foreach ($suffix in @('', '\PlugIn', '\CommandList')) {
        $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($subkey + $suffix)
        if ($null -eq $key) { throw "Missing registry key: $suffix" }
        try {
            $values = [ordered]@{}
            foreach ($name in $key.GetValueNames()) { $values[$name] = $key.GetValue($name) }
            $result.keys += [ordered]@{ name = $suffix; writtenUtc = [PanelRegistryTime]::Read($key.Handle.DangerousGetHandle()); values = $values }
        } finally { $key.Dispose() }
    }
    $result.rhpPath = $result.keys[1].values.FileName
    $result.rhpExists = Test-Path -LiteralPath $result.rhpPath -PathType Leaf
    if ($result.rhpExists) { $result.rhpSha256 = (Get-FileHash -LiteralPath $result.rhpPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    $result.success = $true
} catch {
    $result.error = $_.Exception.Message
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
if (-not $result.success) { exit 1 }
