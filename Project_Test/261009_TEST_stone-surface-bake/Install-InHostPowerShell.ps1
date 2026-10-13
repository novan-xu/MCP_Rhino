[CmdletBinding()]
param([ValidateSet('Preflight', 'Install', 'Attest', 'WatchRestart')][string] $Mode = 'Install')
# Run Install in an ordinary user PowerShell, after saving and closing Rhino.
# Agent-context registry validation is deliberately not treated as host attestation.
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$bundle = Join-Path $repo 'Packaging\MCP_Rhino\artifacts\MCP_Rhino-1.3.3'
$evidence = Join-Path $repo '.validation\stone-surface-host'
$installer = Join-Path $bundle 'Installer\Install-McpRhino.ps1'
$definition = Get-Content -Raw -LiteralPath (Join-Path $bundle 'package-manifest.json') | ConvertFrom-Json
$manifest = Get-Content -Raw -LiteralPath (Join-Path $bundle 'bundle-manifest.json') | ConvertFrom-Json
$expectedRhp = Join-Path $env:LOCALAPPDATA "MCP_Rhino\plugin\$($definition.productVersion)\MCP_Rhino.Server.rhp"
$subkey = 'Software\McNeel\Rhinoceros\8.0\Plug-Ins\' + $definition.pluginId.ToLowerInvariant()
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class StoneSurfaceKeyTime {
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
        UserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        FileName = $file.FileName
        Commands = @($(if ($commands) { $commands.GetValueNames() }))
        CommandSubKeys = @($(if ($commands) { $commands.GetSubKeyNames() }))
        RootTime = [StoneSurfaceKeyTime]::Read($subkey)
        PlugInTime = [StoneSurfaceKeyTime]::Read($subkey + '\PlugIn')
        CommandListTime = [StoneSurfaceKeyTime]::Read($subkey + '\CommandList')
    }
}
foreach ($file in $manifest.files) {
    $path = [IO.Path]::GetFullPath((Join-Path $bundle $file.path))
    if (-not $path.StartsWith($bundle.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Bundle path escapes package root.' }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) { throw "Bundle hash mismatch: $path" }
}
if ($Mode -eq 'Preflight') { Write-Output 'Staged package hashes verified. No installation performed.'; return }
$targetFile = Join-Path $evidence 'target.json'
if (-not (Test-Path -LiteralPath $targetFile)) { throw 'Missing host target evidence; no installation attempted.' }
$target = Get-Content -Raw -LiteralPath $targetFile | ConvertFrom-Json
$actualSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
if ($actualSid -ne $target.UserSid -or $env:LOCALAPPDATA -ne $target.LocalAppData) {
    throw "Use ordinary PowerShell under the Windows account that runs Rhino ($($target.UserName)). No installation attempted."
}
if ($Mode -eq 'WatchRestart') {
    try {
        $installed = Get-Content -Raw -LiteralPath (Join-Path $evidence 'after.json') | ConvertFrom-Json
        $deadline = [DateTime]::UtcNow.AddMinutes(45)
        do {
            foreach ($rhino in @(Get-Process Rhino -ErrorAction SilentlyContinue)) {
                try { $modules = @($rhino.Modules | Where-Object { $_.FileName -eq $expectedRhp }) }
                catch { continue }
                if ($modules.Count -eq 0) { continue }
                $current = Read-Registration
                if ($current.FileName -ne $expectedRhp -or $current.Commands.Count -ne 0 -or $current.CommandSubKeys.Count -ne 0) {
                    throw 'Unexpected host registration after Rhino restart.'
                }
                if ($current.PlugInTime -ne $installed.PlugInTime) { throw 'Installer-owned PlugIn timestamp changed after Rhino startup.' }
                if ($current.RootTime -le $installed.RootTime -or $current.CommandListTime -le $installed.CommandListTime) { continue }
                $current | Add-Member -NotePropertyName RhinoProcessId -NotePropertyValue $rhino.Id
                $current | Add-Member -NotePropertyName LoadedRhp -NotePropertyValue $modules[0].FileName
                $current | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'post-restart.json') -Encoding UTF8
                return
            }
            Start-Sleep -Seconds 3
        } while ([DateTime]::UtcNow -lt $deadline)
        throw 'Timed out waiting for the new RHP to load in Rhino.'
    } catch {
        $_ | Out-String | Set-Content -LiteralPath (Join-Path $evidence 'post-restart-error.txt') -Encoding UTF8
        exit 1
    }
}
if ($Mode -eq 'Attest') {
    & $installer -Mode Validate -BundleRoot $bundle
    $before = Get-Content -Raw -LiteralPath (Join-Path $evidence 'before.json') | ConvertFrom-Json
    $after = Read-Registration
    if ($after.ProcessId -eq $before.ProcessId) { throw 'Attestation requires a separate process.' }
    if ($after.FileName -ne $expectedRhp -or -not (Test-Path -LiteralPath $expectedRhp -PathType Leaf)) { throw 'Host registry does not point to the new existing RHP.' }
    if ($after.Commands.Count -ne 0 -or $after.CommandSubKeys.Count -ne 0) { throw 'Host CommandList is not exactly empty.' }
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
$prior = Read-Registration
if ($prior.FileName -ne $target.PreviousRhp -or -not (Test-Path -LiteralPath $prior.FileName -PathType Leaf)) {
    throw 'Host registration does not match the independently verified prior RHP; no installation attempted.'
}
$prior | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'before.json') -Encoding UTF8
& $installer -Mode Install -BundleRoot $bundle
& "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Mode Attest
if ($LASTEXITCODE -ne 0) { throw 'Independent host verification failed; do not report installation as passed.' }

# Capture post-restart host evidence automatically; no additional user command is needed.
$watchArguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '" -Mode WatchRestart'
Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList $watchArguments -WindowStyle Hidden | Out-Null
Write-Output 'Update installed. Reopen Rhino and the stone document; restart verification runs automatically.'
