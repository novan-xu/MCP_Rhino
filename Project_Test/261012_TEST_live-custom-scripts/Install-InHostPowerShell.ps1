[CmdletBinding()]
param(
    [ValidateSet('Preflight','CheckAccount','Install','Attest','AfterRestart','Restore')][string] $Mode = 'Install',
    [switch] $DeferRestartVerification
)
# Run from ordinary user PowerShell or an independently verified host process,
# using the account that runs Rhino. Deferral does not waive AfterRestart checks.
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$bundle = Join-Path $repo '.validation\live-custom-scripts\package\MCP_Rhino-1.3.7'
$evidenceBase = Join-Path $repo '.validation\live-custom-scripts'
$legacyEvidence = Join-Path $evidenceBase 'host'
$actualSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$evidence = Join-Path $evidenceBase ('host-' + $actualSid)
$targetFile = Join-Path $evidenceBase 'host-target.json'
# Preserve the first attempt's evidence. Restore must run under that attempt's own account.
if ($Mode -eq 'Restore' -and (Test-Path -LiteralPath (Join-Path $legacyEvidence 'before.json'))) {
    $legacy = Get-Content -LiteralPath (Join-Path $legacyEvidence 'before.json') -Raw | ConvertFrom-Json
    if ($legacy.UserSid -eq $actualSid) { $evidence = $legacyEvidence }
}
if ($Mode -notin @('Preflight','Restore')) {
    if (-not (Test-Path -LiteralPath $targetFile)) { throw 'Missing verified Rhino-account target. Nothing was installed.' }
    $target = Get-Content -LiteralPath $targetFile -Raw | ConvertFrom-Json
    if ($actualSid -ne $target.UserSid) {
        throw "Wrong Windows account. Rhino runs as $($target.UserName). Open PowerShell as that account. Nothing was installed."
    }
    if ($Mode -eq 'CheckAccount') { Write-Output "Correct Rhino account: $($target.UserName). No changes made."; return }
}
$installer = Join-Path $bundle 'Installer\Install-McpRhino.ps1'
$product = Join-Path $env:LOCALAPPDATA 'MCP_Rhino'
$definition = Get-Content -LiteralPath (Join-Path $bundle 'package-manifest.json') -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $bundle 'bundle-manifest.json') -Raw | ConvertFrom-Json
$expectedRhp = Join-Path $product "plugin\$($manifest.productVersion)\MCP_Rhino.Server.rhp"
$subkey = 'Software\McNeel\Rhinoceros\8.0\Plug-Ins\' + $definition.pluginId.ToLowerInvariant()
$hostPowerShell = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
if (-not ('LiveScriptsHostKeyTime' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LiveScriptsHostKeyTime {
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
}
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
        RootTime = [LiveScriptsHostKeyTime]::Read($subkey)
        PlugInTime = [LiveScriptsHostKeyTime]::Read($subkey + '\PlugIn')
        CommandListTime = [LiveScriptsHostKeyTime]::Read($subkey + '\CommandList')
    }
}
function Assert-CurrentRegistration($current) {
    if ($current.FileName -ne $expectedRhp -or -not (Test-Path -LiteralPath $expectedRhp -PathType Leaf)) { throw 'Host registration does not name the new existing RHP.' }
    if ($current.Commands.Count -ne 0 -or $current.CommandSubKeys.Count -ne 0) { throw 'Production CommandList must be exactly empty.' }
}
function Restore-PriorReachableRhp {
    $prior = Get-Content -LiteralPath (Join-Path $evidence 'before.json') -Raw | ConvertFrom-Json
    if ($prior.UserSid -ne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value) { throw 'Rollback account mismatch.' }
    $priorDir = [IO.Path]::GetFullPath((Split-Path -Parent $prior.FileName))
    $allowed = [IO.Path]::GetFullPath((Join-Path $product 'plugin')).TrimEnd('\') + '\'
    if (-not $priorDir.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or $prior.FileName -eq $expectedRhp) { throw 'Unsafe rollback path.' }
    if (-not (Test-Path -LiteralPath $priorDir)) {
        Copy-Item -LiteralPath (Join-Path $evidence 'PriorPlugin') -Destination $priorDir -Recurse
    }
    if (-not (Test-Path -LiteralPath $prior.FileName -PathType Leaf)) { throw 'Prior RHP could not be restored.' }
    & reg.exe import (Join-Path $evidence 'activation-prior-registry.reg')
    if ($LASTEXITCODE -ne 0 -or (Read-Registration).FileName -ne $prior.FileName) { throw 'Prior registration restore failed.' }
    Write-Warning 'Installation failed. Prior reachable RHP and registration restored. The new package is not validated; retain the backup and report the failure.'
}
if ($Mode -eq 'Restore') { Restore-PriorReachableRhp; return }
foreach ($file in $manifest.files) {
    $path = [IO.Path]::GetFullPath((Join-Path $bundle $file.path))
    if (-not $path.StartsWith($bundle.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Bundle path escapes package root.' }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) { throw "Bundle hash mismatch: $path" }
}
if ($Mode -eq 'Preflight') { Write-Output 'Package hashes verified. No installation performed.'; return }
if ($Mode -eq 'Attest') {
    & $installer -Mode Validate -BundleRoot $bundle
    $before = Get-Content -LiteralPath (Join-Path $evidence 'before.json') -Raw | ConvertFrom-Json
    $after = Read-Registration
    if ($after.ProcessId -eq $before.ProcessId -or $after.UserSid -ne $before.UserSid) { throw 'Attestation requires a separate process under the same host account.' }
    Assert-CurrentRegistration $after
    foreach ($field in @('RootTime','PlugInTime','CommandListTime')) {
        if ($after.$field -le $before.$field) { throw "Host key timestamp did not advance: $field" }
    }
    $after | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'after.json') -Encoding UTF8
    Write-Output 'Installer Validate and separate-process host registration attestation passed.'
    return
}
if ($Mode -eq 'AfterRestart') {
    try {
        $installed = Get-Content -LiteralPath (Join-Path $evidence 'after.json') -Raw | ConvertFrom-Json
        $loaded = @(Get-Process Rhino -ErrorAction SilentlyContinue | ForEach-Object {
            $rhinoProcess = $_
            $rhinoProcess.Modules | Where-Object { $_.FileName -eq $expectedRhp } | ForEach-Object {
                [pscustomobject]@{ ProcessId=$rhinoProcess.Id; LoadedRhp=$_.FileName }
            }
        })
        if ($loaded.Count -eq 0) { throw 'The exact updated RHP is not loaded in Rhino.' }
        $current = Read-Registration
        Assert-CurrentRegistration $current
        if ($current.PlugInTime -ne $installed.PlugInTime -or $current.RootTime -le $installed.RootTime -or $current.CommandListTime -le $installed.CommandListTime) {
            throw 'Post-startup registry timestamps do not satisfy the Rhino/installer ownership contract.'
        }
        [pscustomobject]@{ Registration=$current; Loaded=$loaded } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'post-restart.json') -Encoding UTF8
        Write-Output 'Exact RHP load and post-startup host registry attestation passed. Return to Codex to run the live audit.'
    } catch { Restore-PriorReachableRhp; throw }
    return
}
if (@(Get-Process Rhino,'MCP_Rhino.Router' -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Save and close all Rhino and Router processes first. Nothing was installed.' }
if (Test-Path -LiteralPath (Join-Path $evidence 'before.json')) { throw 'Prior activation evidence exists. Use Attest/AfterRestart/Restore or inspect that evidence before another install.' }
$prior = Read-Registration
$priorDir = if ($prior.FileName) { [IO.Path]::GetFullPath((Split-Path -Parent $prior.FileName)) } else { '' }
$allowed = [IO.Path]::GetFullPath((Join-Path $product 'plugin')).TrimEnd('\') + '\'
if (-not $priorDir.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $prior.FileName -PathType Leaf) -or $prior.FileName -eq $expectedRhp) {
    throw 'A reachable previous registry-owned MCP_Rhino RHP is required before activation.'
}
if ($prior.FileName -ne $target.PreviousRhp) { throw 'Host registration differs from the RHP observed loaded in Rhino. Nothing was installed; report this message.' }
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
Copy-Item -LiteralPath $priorDir -Destination (Join-Path $evidence 'PriorPlugin') -Recurse
Copy-Item -LiteralPath (Join-Path $product 'install-manifest.json') -Destination (Join-Path $evidence 'prior-install-manifest.json')
& reg.exe export ('HKCU\' + $subkey) (Join-Path $evidence 'activation-prior-registry.reg') /y
if ($LASTEXITCODE -ne 0) { throw 'Prior registry backup failed.' }
$prior | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'before.json') -Encoding UTF8
try {
    & $hostPowerShell -NoProfile -ExecutionPolicy Bypass -File $installer -Mode Install -BundleRoot $bundle 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'installer.log')
    if ($LASTEXITCODE -ne 0) { throw 'Installer failed.' }
    & $hostPowerShell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Mode Attest 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'attestation.log')
    if ($LASTEXITCODE -ne 0) { throw 'Independent host attestation failed.' }
} catch {
    $failure = $_
    $failure | Out-String | Set-Content -LiteralPath (Join-Path $evidence 'failure.log') -Encoding UTF8
    try { Restore-PriorReachableRhp } catch {
        $_ | Out-String | Add-Content -LiteralPath (Join-Path $evidence 'failure.log') -Encoding UTF8
        Write-Warning 'Automatic rollback could not finish; the prior plug-in backup and registry export remain in the evidence folder.'
    }
    throw $failure
}
Write-Output 'Update installed and host-attested. Reopen Rhino and your documents.'
if ($DeferRestartVerification) {
    Write-Output 'Exact loaded-RHP verification remains pending. Run -Mode AfterRestart after Rhino starts.'
    return
}
Read-Host 'Press Enter after Rhino has opened, to verify the loaded plug-in' | Out-Null
& $hostPowerShell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Mode AfterRestart
if ($LASTEXITCODE -ne 0) { throw 'Post-restart verification failed. Read the error above before continuing.' }
