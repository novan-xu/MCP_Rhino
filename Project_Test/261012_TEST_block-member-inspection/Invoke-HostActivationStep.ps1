param(
    [Parameter(Mandatory=$true)][ValidateSet('Install','AfterRestart')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
Start-Transcript -LiteralPath $OutputPath -NoClobber | Out-Null
try {
    & (Join-Path $PSScriptRoot 'Install-InHostPowerShell.ps1') -Mode $Mode -DeferRestartVerification
    if (-not $?) { throw 'Host activation step returned failure.' }
} catch {
    Write-Error -ErrorRecord $_ -ErrorAction Continue
    exit 1
} finally {
    Stop-Transcript | Out-Null
}
