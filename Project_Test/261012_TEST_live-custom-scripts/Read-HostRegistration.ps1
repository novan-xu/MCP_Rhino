param([Parameter(Mandatory=$true)][string]$OutputPath, [Parameter(Mandatory=$true)][string]$Challenge)
# Read-only host observation. The only write is the caller's local evidence JSON.
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$registrationSubkey = 'Software\McNeel\Rhinoceros\8.0\Plug-Ins\7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class HostObservationKeyTime {
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
$hostProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $PID"
$hostParent = Get-CimInstance Win32_Process -Filter "ProcessId = $($hostProcess.ParentProcessId)"
$plugin = Get-ItemProperty -LiteralPath ('HKCU:\' + $registrationSubkey + '\PlugIn') -ErrorAction SilentlyContinue
$commands = Get-Item -LiteralPath ('HKCU:\' + $registrationSubkey + '\CommandList') -ErrorAction SilentlyContinue
$products = @(
 foreach ($productName in @('MCP_Rhino','PanelCladdingEditor')) {
  $manifestPath = Join-Path $env:LOCALAPPDATA ($productName + '\install-manifest.json')
  if (-not (Test-Path -LiteralPath $manifestPath)) { continue }
  $owned = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
  $id = if ($productName -eq 'MCP_Rhino') { '7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a' } else { '7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35' }
  $registered = Get-ItemProperty -LiteralPath ('HKCU:\Software\McNeel\Rhinoceros\8.0\Plug-Ins\' + $id + '\PlugIn') -ErrorAction SilentlyContinue
  $loaded = @(Get-Process Rhino -ErrorAction SilentlyContinue | ForEach-Object {
   $rhino = $_
   $rhino.Modules | Where-Object { $_.FileName -eq $registered.FileName } | ForEach-Object { [pscustomobject]@{ProcessId=$rhino.Id;FileName=$_.FileName} }
  })
  [pscustomobject]@{
   Product=$productName
   Version=$(if ($owned.productVersion) { $owned.productVersion } else { $owned.version })
   InstalledUtc=$owned.installedUtc
   PluginRoot=$owned.pluginRoot
   RegisteredFileName=$registered.FileName
   RhpExists=[bool]($registered.FileName -and (Test-Path -LiteralPath $registered.FileName -PathType Leaf))
   Loaded=$loaded
  }
 }
)
$observation = [pscustomobject]@{
 Challenge = $Challenge
 ObservedUtc = [DateTime]::UtcNow.ToString('o')
 Identity = $identity.Name
 UserSid = $identity.User.Value
 ProcessId = $PID
 SessionId = $hostProcess.SessionId
 ParentProcessId = $hostProcess.ParentProcessId
 ParentProcessName = $hostParent.Name
 ParentExecutablePath = $hostParent.ExecutablePath
 FileName = $plugin.FileName
 RhpExists = [bool]($plugin.FileName -and (Test-Path -LiteralPath $plugin.FileName -PathType Leaf))
 Commands = @($(if ($commands) { $commands.GetValueNames() }))
 CommandSubKeys = @($(if ($commands) { $commands.GetSubKeyNames() }))
 RootTime = [HostObservationKeyTime]::Read($registrationSubkey)
 PlugInTime = [HostObservationKeyTime]::Read($registrationSubkey + '\PlugIn')
 CommandListTime = [HostObservationKeyTime]::Read($registrationSubkey + '\CommandList')
 InstalledProducts = $products
}
$json = $observation | ConvertTo-Json -Depth 6
$stream = [IO.File]::Open($OutputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
try {
 $writer = New-Object IO.StreamWriter($stream, (New-Object Text.UTF8Encoding($false)))
 $writer.Write($json)
 $writer.Flush()
} finally { if ($writer) { $writer.Dispose() }; $stream.Dispose() }
