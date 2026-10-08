$ErrorActionPreference = 'Stop'
$materialRemovalBefore = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-before.log') | ConvertFrom-Json
$materialRemovalInstall = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-install.log') | ConvertFrom-Json
$materialRemovalAfter = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-independent-validate.log') | ConvertFrom-Json
$materialRemovalPackage = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'package-summary.json') | ConvertFrom-Json
if (-not $materialRemovalBefore.success -or -not $materialRemovalInstall.success -or -not $materialRemovalAfter.success) { throw 'Host operation failed' }
if ($materialRemovalInstall.processId -eq $materialRemovalAfter.processId -or (Get-Process -Id $materialRemovalInstall.processId -ErrorAction SilentlyContinue) -or
    (Get-Process -Id $materialRemovalAfter.processId -ErrorAction SilentlyContinue)) { throw 'Independent completed host processes required' }
$materialRemovalHost = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'host-persistence.json') | ConvertFrom-Json
if (-not $materialRemovalHost.success -or -not $materialRemovalHost.nonceMatched -or -not $materialRemovalHost.probeRemoved) { throw 'Pre-install host persistence gate not satisfied' }
$materialRemovalExpectedPath = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor/plugin/1.0.94/PanelCladdingEditor.rhp'
if ($materialRemovalAfter.rhpPath -ne $materialRemovalExpectedPath -or -not $materialRemovalAfter.rhpExists -or
    $materialRemovalAfter.rhpSha256 -ne $materialRemovalPackage.rhpSha256) { throw 'Registered RHP mismatch' }
$materialRemovalNames = @('PCClear','PCCreate','PCCrvTemplate','PCEditor','PCMatchCrv','PCMatchSrf','PCpid','PCSpawnCrv','PCSpawnSrf','PCSyncCrv','PCSyncSrf','PCUpdate')
$materialRemovalCommands = $materialRemovalAfter.keys[2].values
if (@($materialRemovalCommands.PSObject.Properties).Count -ne $materialRemovalNames.Count) { throw 'Command count mismatch' }
foreach ($materialRemovalName in $materialRemovalNames) {
    if ($materialRemovalCommands.$materialRemovalName -ne ('2;' + $materialRemovalName)) { throw "Command mismatch: $materialRemovalName" }
}
for ($materialRemovalIndex = 0; $materialRemovalIndex -lt 3; $materialRemovalIndex++) {
    if ([datetime]$materialRemovalAfter.keys[$materialRemovalIndex].writtenUtc -le [datetime]$materialRemovalBefore.keys[$materialRemovalIndex].writtenUtc) {
        throw 'Registry timestamp did not advance'
    }
}
$materialRemovalRegistryRoot = $materialRemovalAfter.sid + '\Software\McNeel\Rhinoceros\8.0\Plug-Ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35'
$materialRemovalRegPath = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName GetStringValue -Arguments @{
    hDefKey=[uint32]2147483651; sSubKeyName=($materialRemovalRegistryRoot+'\PlugIn'); sValueName='FileName'
}
if ($materialRemovalRegPath.ReturnValue -ne 0 -or $materialRemovalRegPath.sValue -ne $materialRemovalExpectedPath) { throw 'Independent provider FileName mismatch' }
$materialRemovalRegNames = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName EnumValues -Arguments @{
    hDefKey=[uint32]2147483651; sSubKeyName=($materialRemovalRegistryRoot+'\CommandList')
}
if ($materialRemovalRegNames.ReturnValue -ne 0 -or @(Compare-Object ($materialRemovalNames | Sort-Object) ($materialRemovalRegNames.sNames | Sort-Object)).Count -ne 0) {
    throw 'Independent provider command inventory mismatch'
}
foreach ($materialRemovalName in $materialRemovalNames) {
    $materialRemovalValue = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName GetStringValue -Arguments @{
        hDefKey=[uint32]2147483651; sSubKeyName=($materialRemovalRegistryRoot+'\CommandList'); sValueName=$materialRemovalName
    }
    if ($materialRemovalValue.ReturnValue -ne 0 -or $materialRemovalValue.sValue -ne ('2;' + $materialRemovalName)) { throw 'Independent provider command value mismatch' }
}
$materialRemovalInstalled = Get-Content -Raw -LiteralPath (Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor/install-manifest.json') | ConvertFrom-Json
$materialRemovalBackup = @($materialRemovalInstalled.migrationBackups | Where-Object { $_.original -eq (Split-Path -Parent $materialRemovalBefore.rhpPath) })
if ($materialRemovalBackup.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $materialRemovalBackup[0].backup 'PanelCladdingEditor.rhp')).Hash -ne $materialRemovalBefore.rhpSha256) {
    throw 'Prior RHP rollback copy mismatch'
}
$materialRemovalRhinoCount = @(Get-Process -Name Rhino -ErrorAction SilentlyContinue).Count
[ordered]@{
    version='1.0.94'; priorVersion='1.0.93'; installation='passed'
    installedRhp='%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.94\PanelCladdingEditor.rhp'
    rhpSha256=$materialRemovalAfter.rhpSha256
    hostPersistence='Hidden host probe nonce independently matched through HKEY_USERS/current SID registry provider before activation; probe removed'
    installerValidation='Host Install and mandatory Validate passed; separate post-exit host Validate passed'
    commandListExact=$true; registeredCommands=$materialRemovalNames
    allThreeRegistryTimestampsAdvanced=$true
    installedKeyTimes=@($materialRemovalAfter.keys | Select-Object name,writtenUtc)
    windowsRegistryProviderAgreement=$true; previousRhpRollbackCopyVerified=$true
    rhinoProcessesAfterInstallation=$materialRemovalRhinoCount
    postStartVerification='pending next Rhino launch'
    interactiveUiAcceptance='pending next Rhino launch; offscreen UI checks passed'
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'activation-summary.json') -Encoding utf8
Write-Output "PASS: 1.0.94 independent activation verification, 12 exact commands, three timestamps, rollback hash; Rhino processes=$materialRemovalRhinoCount"
