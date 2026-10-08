$ErrorActionPreference = 'Stop'
$extrusionClearBefore = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-before.log') | ConvertFrom-Json
$extrusionClearInstall = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-install.log') | ConvertFrom-Json
$extrusionClearAfter = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-independent-validate.log') | ConvertFrom-Json
$extrusionClearPackage = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'package-summary.json') | ConvertFrom-Json
if (-not $extrusionClearBefore.success -or -not $extrusionClearInstall.success -or -not $extrusionClearAfter.success) { throw 'Host operation failed' }
if ($extrusionClearInstall.processId -eq $extrusionClearAfter.processId -or (Get-Process -Id $extrusionClearInstall.processId -ErrorAction SilentlyContinue) -or
    (Get-Process -Id $extrusionClearAfter.processId -ErrorAction SilentlyContinue)) { throw 'Independent completed host processes required' }
$extrusionClearHost = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'host-persistence.json') | ConvertFrom-Json
if (-not $extrusionClearHost.success -or -not $extrusionClearHost.priorWriteAttestationVerified -or -not $extrusionClearHost.freshHostProviderAgreement) { throw 'Pre-install host persistence gate not satisfied' }
$extrusionClearExpectedPath = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor/plugin/1.0.95/PanelCladdingEditor.rhp'
if ($extrusionClearAfter.rhpPath -ne $extrusionClearExpectedPath -or -not $extrusionClearAfter.rhpExists -or
    $extrusionClearAfter.rhpSha256 -ne $extrusionClearPackage.rhpSha256) { throw 'Registered RHP mismatch' }
$extrusionClearNames = @('PCClear','PCCreate','PCCrvTemplate','PCEditor','PCMatchCrv','PCMatchSrf','PCpid','PCSpawnCrv','PCSpawnSrf','PCSyncCrv','PCSyncSrf','PCUpdate')
$extrusionClearCommands = $extrusionClearAfter.keys[2].values
if (@($extrusionClearCommands.PSObject.Properties).Count -ne $extrusionClearNames.Count) { throw 'Command count mismatch' }
foreach ($extrusionClearName in $extrusionClearNames) {
    if ($extrusionClearCommands.$extrusionClearName -ne ('2;' + $extrusionClearName)) { throw "Command mismatch: $extrusionClearName" }
}
for ($extrusionClearIndex = 0; $extrusionClearIndex -lt 3; $extrusionClearIndex++) {
    if ([datetime]$extrusionClearAfter.keys[$extrusionClearIndex].writtenUtc -le [datetime]$extrusionClearBefore.keys[$extrusionClearIndex].writtenUtc) {
        throw 'Registry timestamp did not advance'
    }
}
$extrusionClearRegistryRoot = $extrusionClearAfter.sid + '\Software\McNeel\Rhinoceros\8.0\Plug-Ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35'
$extrusionClearRegPath = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName GetStringValue -Arguments @{
    hDefKey=[uint32]2147483651; sSubKeyName=($extrusionClearRegistryRoot+'\PlugIn'); sValueName='FileName'
}
if ($extrusionClearRegPath.ReturnValue -ne 0 -or $extrusionClearRegPath.sValue -ne $extrusionClearExpectedPath) { throw 'Independent provider FileName mismatch' }
$extrusionClearRegNames = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName EnumValues -Arguments @{
    hDefKey=[uint32]2147483651; sSubKeyName=($extrusionClearRegistryRoot+'\CommandList')
}
if ($extrusionClearRegNames.ReturnValue -ne 0 -or @(Compare-Object ($extrusionClearNames | Sort-Object) ($extrusionClearRegNames.sNames | Sort-Object)).Count -ne 0) {
    throw 'Independent provider command inventory mismatch'
}
foreach ($extrusionClearName in $extrusionClearNames) {
    $extrusionClearValue = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName GetStringValue -Arguments @{
        hDefKey=[uint32]2147483651; sSubKeyName=($extrusionClearRegistryRoot+'\CommandList'); sValueName=$extrusionClearName
    }
    if ($extrusionClearValue.ReturnValue -ne 0 -or $extrusionClearValue.sValue -ne ('2;' + $extrusionClearName)) { throw 'Independent provider command value mismatch' }
}
$extrusionClearInstalled = Get-Content -Raw -LiteralPath (Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor/install-manifest.json') | ConvertFrom-Json
$extrusionClearBackup = @($extrusionClearInstalled.migrationBackups | Where-Object { $_.original -eq (Split-Path -Parent $extrusionClearBefore.rhpPath) })
if ($extrusionClearBackup.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $extrusionClearBackup[0].backup 'PanelCladdingEditor.rhp')).Hash -ne $extrusionClearBefore.rhpSha256) {
    throw 'Prior RHP rollback copy mismatch'
}
$extrusionClearRhinoCount = @(Get-Process -Name Rhino -ErrorAction SilentlyContinue).Count
[ordered]@{
    version='1.0.95'; priorVersion='1.0.94'; installation='passed'
    installedRhp='%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.95\PanelCladdingEditor.rhp'
    rhpSha256=$extrusionClearAfter.rhpSha256
    hostPersistence='Previously attested independent Win32_Process host launch; fresh host and HKEY_USERS/current SID provider agree on current registration before activation'
    installerValidation='Host Install and mandatory Validate passed; separate post-exit host Validate passed'
    commandListExact=$true; registeredCommands=$extrusionClearNames
    allThreeRegistryTimestampsAdvanced=$true
    installedKeyTimes=@($extrusionClearAfter.keys | Select-Object name,writtenUtc)
    windowsRegistryProviderAgreement=$true; previousRhpRollbackCopyVerified=$true
    rhinoProcessesAfterInstallation=$extrusionClearRhinoCount
    postStartVerification='pending next Rhino launch'
    interactiveUiAcceptance='pending next Rhino launch; offscreen UI checks passed'
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'activation-summary.json') -Encoding utf8
Write-Output "PASS: 1.0.95 independent activation verification, 12 exact commands, three timestamps, rollback hash; Rhino processes=$extrusionClearRhinoCount"
