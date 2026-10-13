$ErrorActionPreference = 'Stop'
$pcboundsBefore = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-before.log') | ConvertFrom-Json
$pcboundsInstall = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-install.log') | ConvertFrom-Json
$pcboundsAfter = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-independent-validate.log') | ConvertFrom-Json
$pcboundsPackage = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'package-summary.json') | ConvertFrom-Json
if (-not $pcboundsBefore.success -or -not $pcboundsInstall.success -or -not $pcboundsAfter.success) { throw 'Host operation failed' }
if ($pcboundsInstall.processId -eq $pcboundsAfter.processId -or (Get-Process -Id $pcboundsInstall.processId -ErrorAction SilentlyContinue) -or
    (Get-Process -Id $pcboundsAfter.processId -ErrorAction SilentlyContinue)) { throw 'Independent completed host processes required' }
$pcboundsExpectedPath = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor/plugin/1.0.93/PanelCladdingEditor.rhp'
if ($pcboundsAfter.rhpPath -ne $pcboundsExpectedPath -or -not $pcboundsAfter.rhpExists -or
    $pcboundsAfter.rhpSha256 -ne $pcboundsPackage.rhpSha256) { throw 'Registered RHP mismatch' }
$pcboundsNames = @('PCClear','PCCreate','PCCrvTemplate','PCEditor','PCMatchCrv','PCMatchSrf','PCpid','PCSpawnCrv','PCSpawnSrf','PCSyncCrv','PCSyncSrf','PCUpdate')
$pcboundsCommands = $pcboundsAfter.keys[2].values
if (@($pcboundsCommands.PSObject.Properties).Count -ne $pcboundsNames.Count) { throw 'Command count mismatch' }
foreach ($pcboundsName in $pcboundsNames) {
    if ($pcboundsCommands.$pcboundsName -ne ('2;' + $pcboundsName)) { throw "Command mismatch: $pcboundsName" }
}
for ($pcboundsIndex = 0; $pcboundsIndex -lt 3; $pcboundsIndex++) {
    if ([datetime]$pcboundsAfter.keys[$pcboundsIndex].writtenUtc -le [datetime]$pcboundsBefore.keys[$pcboundsIndex].writtenUtc) {
        throw 'Registry timestamp did not advance'
    }
}
$pcboundsRegistryRoot = $pcboundsAfter.sid + '\Software\McNeel\Rhinoceros\8.0\Plug-Ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35'
$pcboundsRegPath = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName GetStringValue -Arguments @{
    hDefKey=[uint32]2147483651; sSubKeyName=($pcboundsRegistryRoot+'\PlugIn'); sValueName='FileName'
}
if ($pcboundsRegPath.ReturnValue -ne 0 -or $pcboundsRegPath.sValue -ne $pcboundsExpectedPath) { throw 'Independent provider FileName mismatch' }
$pcboundsRegNames = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName EnumValues -Arguments @{
    hDefKey=[uint32]2147483651; sSubKeyName=($pcboundsRegistryRoot+'\CommandList')
}
if ($pcboundsRegNames.ReturnValue -ne 0 -or @(Compare-Object ($pcboundsNames | Sort-Object) ($pcboundsRegNames.sNames | Sort-Object)).Count -ne 0) {
    throw 'Independent provider command inventory mismatch'
}
foreach ($pcboundsName in $pcboundsNames) {
    $pcboundsValue = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName GetStringValue -Arguments @{
        hDefKey=[uint32]2147483651; sSubKeyName=($pcboundsRegistryRoot+'\CommandList'); sValueName=$pcboundsName
    }
    if ($pcboundsValue.ReturnValue -ne 0 -or $pcboundsValue.sValue -ne ('2;' + $pcboundsName)) { throw 'Independent provider command value mismatch' }
}
$pcboundsInstalled = Get-Content -Raw -LiteralPath (Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor/install-manifest.json') | ConvertFrom-Json
$pcboundsBackup = @($pcboundsInstalled.migrationBackups | Where-Object { $_.original -eq (Split-Path -Parent $pcboundsBefore.rhpPath) })
if ($pcboundsBackup.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $pcboundsBackup[0].backup 'PanelCladdingEditor.rhp')).Hash -ne $pcboundsBefore.rhpSha256) {
    throw 'Prior RHP rollback copy mismatch'
}
$pcboundsRhinoCount = @(Get-Process -Name Rhino -ErrorAction SilentlyContinue).Count
[ordered]@{
    version='1.0.93'; priorVersion='1.0.92'; installation='passed'
    installedRhp='%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.93\PanelCladdingEditor.rhp'
    rhpSha256=$pcboundsAfter.rhpSha256
    hostPersistence='Hidden host probe nonce independently matched through HKEY_USERS/current SID registry provider before activation; probe removed'
    installerValidation='Host Install and mandatory Validate passed; separate post-exit host Validate passed'
    commandListExact=$true; registeredCommands=$pcboundsNames
    allThreeRegistryTimestampsAdvanced=$true
    installedKeyTimes=@($pcboundsAfter.keys | Select-Object name,writtenUtc)
    windowsRegistryProviderAgreement=$true; previousRhpRollbackCopyVerified=$true
    rhinoProcessesAfterInstallation=$pcboundsRhinoCount
    postStartVerification='pending next Rhino launch'
    nativeCommandAcceptance='pending; rhcommon_c could not initialize outside Rhino'
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'activation-summary.json') -Encoding utf8
Write-Output "PASS: 1.0.93 independent activation verification, 12 exact commands, three timestamps, rollback hash; Rhino processes=$pcboundsRhinoCount"
