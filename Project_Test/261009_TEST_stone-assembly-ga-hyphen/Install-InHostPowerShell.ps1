[CmdletBinding()]
param([ValidateSet('Preflight', 'Install', 'Attest')][string] $Mode = 'Install')
# Run Install in an ordinary user PowerShell, after saving and closing Rhino.
# Agent-context registry validation is deliberately not treated as host attestation.
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$bundle = Join-Path $repo 'Packaging\MCP_Rhino\artifacts\MCP_Rhino-1.3.2'
$evidence = Join-Path $repo '.validation\ga-hyphen-host'
$installer = Join-Path $bundle 'Installer\Install-McpRhino.ps1'
$definition = Get-Content -Raw -LiteralPath (Join-Path $bundle 'package-manifest.json') | ConvertFrom-Json
$manifest = Get-Content -Raw -LiteralPath (Join-Path $bundle 'bundle-manifest.json') | ConvertFrom-Json
$expectedRhp = Join-Path $env:LOCALAPPDATA "MCP_Rhino\plugin\$($definition.productVersion)\MCP_Rhino.Server.rhp"
$subkey = 'Software\McNeel\Rhinoceros\8.0\Plug-Ins\' + $definition.pluginId.ToLowerInvariant()
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class GaHyphenKeyTime {
 [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegQueryInfoKey(IntPtr h, IntPtr c, IntPtr cl, IntPtr r, IntPtr sk, IntPtr msk, IntPtr mcl, IntPtr v, IntPtr mvn, IntPtr mvl, IntPtr sd, out long ft);
 public static long Read(string path) {
  using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path)) {
   if(key==null)return 0;
   long t; int rc=RegQueryInfoKey(key.Handle.DangerousGetHandle(),IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,out t);
   if(rc!=0)throw new System.ComponentModel.Win32Exception(rc);
   return t;
  }
 }
}
'@
function Read-Registration {
    $key = 'HKCU:\' + $subkey
    $file = Get-ItemProperty -LiteralPath ($key + '\PlugIn') -ErrorAction SilentlyContinue
    $commands = Get-Item -LiteralPath ($key + '\CommandList') -ErrorAction SilentlyContinue
    [pscustomobject]@{
        ProcessId = $PID
        FileName = $file.FileName
        Commands = @($(if ($commands) { $commands.GetValueNames() }))
        RootTime = [GaHyphenKeyTime]::Read($subkey)
        PlugInTime = [GaHyphenKeyTime]::Read($subkey + '\PlugIn')
        CommandListTime = [GaHyphenKeyTime]::Read($subkey + '\CommandList')
    }
}
foreach ($file in $manifest.files) {
    $path = [IO.Path]::GetFullPath((Join-Path $bundle $file.path))
    if (-not $path.StartsWith($bundle.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Bundle path escapes package root.' }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) { throw "Bundle hash mismatch: $path" }
}
if ($Mode -eq 'Preflight') { Write-Output 'Staged package hashes verified. No installation performed.'; return }
if ($Mode -eq 'Attest') {
    & $installer -Mode Validate -BundleRoot $bundle
    $before = Get-Content -Raw -LiteralPath (Join-Path $evidence 'before.json') | ConvertFrom-Json
    $after = Read-Registration
    if ($after.ProcessId -eq $before.ProcessId) { throw 'Attestation requires a separate process.' }
    if ($after.FileName -ne $expectedRhp -or -not (Test-Path -LiteralPath $expectedRhp -PathType Leaf)) { throw 'Host registry does not point to the new existing RHP.' }
    if ($after.Commands.Count -ne 0) { throw 'Host CommandList is not exactly empty.' }
    foreach ($field in @('RootTime', 'PlugInTime', 'CommandListTime')) {
        if ($after.$field -le $before.$field) { throw "Host key timestamp did not advance: $field" }
    }
    $after | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'after.json') -Encoding UTF8
    Write-Output 'Independent host registration attestation passed. Reopen Rhino and the stone document, then return to Codex.'
    return
}
if (@(Get-Process Rhino,'MCP_Rhino.Router' -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Save your work and close all Rhino and Router processes, then rerun. Nothing was installed.'
}
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
Read-Registration | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'before.json') -Encoding UTF8
& $installer -Mode Install -BundleRoot $bundle
& "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Mode Attest
if ($LASTEXITCODE -ne 0) { throw 'Independent host verification failed; do not report installation as passed.' }
