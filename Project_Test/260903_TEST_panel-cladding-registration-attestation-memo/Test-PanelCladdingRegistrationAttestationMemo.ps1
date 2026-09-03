[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$agents = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'AGENTS.md')
$results = Get-Content -Raw -LiteralPath (
    Join-Path $repoRoot 'Project_Test\260903_TEST_panel-cladding-update-command\RESULTS.md')
$execution = Get-Content -Raw -LiteralPath (
    Join-Path $repoRoot 'Project_Exet\260903_EXET_panel-cladding-update-command.md')

foreach ($required in @(
    'same agent execution context',
    'build and stage only',
    'independent host process',
    'Only then report installation/validation as passed',
    'After Rhino starts',
    'restore the prior reachable RHP'
)) {
    if ($agents.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "AGENTS.md is missing production registry attestation rule: $required"
    }
}

if ($results.IndexOf('claimed production registry validation was not a valid', [StringComparison]::OrdinalIgnoreCase) -lt 0 -or
    $execution.IndexOf('original production-install conclusion was wrong', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    throw 'The 1.0.70 execution records do not retract the invalid production registry attestation.'
}

Write-Output 'PASS: production registry activation requires independent host persistence and post-start attestation.'
