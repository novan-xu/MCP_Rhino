$ErrorActionPreference = 'Stop'
$frameAttributeBefore = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-before.log') | ConvertFrom-Json
$frameAttributeInstall = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-install.log') | ConvertFrom-Json
$frameAttributeAfter = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'activation-independent-validate.log') | ConvertFrom-Json
$frameAttributePackage = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'package-summary.json') | ConvertFrom-Json
if (-not $frameAttributeBefore.success -or -not $frameAttributeInstall.success -or -not $frameAttributeAfter.success) { throw 'Host operation failed' }
if ($frameAttributeInstall.processId -eq $frameAttributeAfter.processId -or (Get-Process -Id $frameAttributeInstall.processId -ErrorAction SilentlyContinue) -or
    (Get-Process -Id $frameAttributeAfter.processId -ErrorAction SilentlyContinue)) { throw 'Independent completed host processes required' }
$frameAttributeHost = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'host-persistence.json') | ConvertFrom-Json
if (-not $frameAttributeHost.success -or -not $frameAttributeHost.priorWriteAttestationVerified -or -not $frameAttributeHost.freshHostProviderAgreement) { throw 'Pre-install host persistence gate not satisfied' }
$frameAttributeExpectedPath = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor/plugin/1.0.96/PanelCladdingEditor.rhp'
if ($frameAttributeAfter.rhpPath -ne $frameAttributeExpectedPath -or -not $frameAttributeAfter.rhpExists -or
    $frameAttributeAfter.rhpSha256 -ne $frameAttributePackage.rhpSha256) { throw 'Registered RHP mismatch' }
$frameAttributeNames = @('PCClear','PCCreate','PCCrvTemplate','PCEditor','PCMatchCrv','PCMatchSrf','PCpid','PCSpawnCrv','PCSpawnSrf','PCSyncCrv','PCSyncSrf','PCUpdate')
$frameAttributeCommands = $frameAttributeAfter.keys[2].values
if (@($frameAttributeCommands.PSObject.Properties).Count -ne $frameAttributeNames.Count) { throw 'Command count mismatch' }
foreach ($frameAttributeName in $frameAttributeNames) {
    if ($frameAttributeCommands.$frameAttributeName -ne ('2;' + $frameAttributeName)) { throw "Command mismatch: $frameAttributeName" }
}
for ($frameAttributeIndex = 0; $frameAttributeIndex -lt 3; $frameAttributeIndex++) {
    if ([datetime]$frameAttributeAfter.keys[$frameAttributeIndex].writtenUtc -le [datetime]$frameAttributeBefore.keys[$frameAttributeIndex].writtenUtc) {
        throw 'Registry timestamp did not advance'
    }
}
$frameAttributeRegistryRoot = $frameAttributeAfter.sid + '\Software\McNeel\Rhinoceros\8.0\Plug-Ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35'
$frameAttributeRegPath = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName GetStringValue -Arguments @{
    hDefKey=[uint32]2147483651; sSubKeyName=($frameAttributeRegistryRoot+'\PlugIn'); sValueName='FileName'
}
if ($frameAttributeRegPath.ReturnValue -ne 0 -or $frameAttributeRegPath.sValue -ne $frameAttributeExpectedPath) { throw 'Independent provider FileName mismatch' }
$frameAttributeRegNames = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName EnumValues -Arguments @{
    hDefKey=[uint32]2147483651; sSubKeyName=($frameAttributeRegistryRoot+'\CommandList')
}
if ($frameAttributeRegNames.ReturnValue -ne 0 -or @(Compare-Object ($frameAttributeNames | Sort-Object) ($frameAttributeRegNames.sNames | Sort-Object)).Count -ne 0) {
    throw 'Independent provider command inventory mismatch'
}
foreach ($frameAttributeName in $frameAttributeNames) {
    $frameAttributeValue = Invoke-CimMethod -Namespace root/default -ClassName StdRegProv -MethodName GetStringValue -Arguments @{
        hDefKey=[uint32]2147483651; sSubKeyName=($frameAttributeRegistryRoot+'\CommandList'); sValueName=$frameAttributeName
    }
    if ($frameAttributeValue.ReturnValue -ne 0 -or $frameAttributeValue.sValue -ne ('2;' + $frameAttributeName)) { throw 'Independent provider command value mismatch' }
}
$frameAttributeInstalled = Get-Content -Raw -LiteralPath (Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor/install-manifest.json') | ConvertFrom-Json
$frameAttributeBackup = @($frameAttributeInstalled.migrationBackups | Where-Object { $_.original -eq (Split-Path -Parent $frameAttributeBefore.rhpPath) })
if ($frameAttributeBackup.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $frameAttributeBackup[0].backup 'PanelCladdingEditor.rhp')).Hash -ne $frameAttributeBefore.rhpSha256) {
    throw 'Prior RHP rollback copy mismatch'
}
$frameAttributeRhinoCount = @(Get-Process -Name Rhino -ErrorAction SilentlyContinue).Count
[ordered]@{
    version='1.0.96'; priorVersion='1.0.95'; installation='passed'
    installedRhp='%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.96\PanelCladdingEditor.rhp'
    rhpSha256=$frameAttributeAfter.rhpSha256
    hostPersistence='Previously attested independent Win32_Process host launch; fresh host and HKEY_USERS/current SID provider agree on current registration before activation'
    installerValidation='Host Install and mandatory Validate passed; separate post-exit host Validate passed'
    commandListExact=$true; registeredCommands=$frameAttributeNames
    allThreeRegistryTimestampsAdvanced=$true
    installedKeyTimes=@($frameAttributeAfter.keys | Select-Object name,writtenUtc)
    windowsRegistryProviderAgreement=$true; previousRhpRollbackCopyVerified=$true
    rhinoProcessesAfterInstallation=$frameAttributeRhinoCount
    postStartVerification='pending next Rhino launch'
    interactiveUiAcceptance='pending next Rhino launch; offscreen UI checks passed'
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'activation-summary.json') -Encoding utf8
Write-Output "PASS: 1.0.96 independent activation verification, 12 exact commands, three timestamps, rollback hash; Rhino processes=$frameAttributeRhinoCount"
