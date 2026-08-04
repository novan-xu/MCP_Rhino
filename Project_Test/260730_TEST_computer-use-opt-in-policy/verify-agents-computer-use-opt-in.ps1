$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$agentsPath = Join-Path $repositoryRoot 'AGENTS.md'
$content = [System.IO.File]::ReadAllText($agentsPath)

$requiredStatements = @(
    'Do not use Computer Use or any other Windows UI automation unless the user explicitly requests Computer Use or Windows UI automation in the current request.'
    'Mentioning an application or file, or asking to "open" or "go to" one, does not by itself authorize UI automation.'
)

foreach ($statement in $requiredStatements) {
    if (-not $content.Contains($statement)) {
        throw "Missing required Computer Use opt-in policy statement: $statement"
    }
}

$hardConstraintsStart = $content.IndexOf('## Hard Constraints', [System.StringComparison]::Ordinal)
$documentPriorityStart = $content.IndexOf('## Document Priority', [System.StringComparison]::Ordinal)
$policyStart = $content.IndexOf($requiredStatements[0], [System.StringComparison]::Ordinal)

if ($hardConstraintsStart -lt 0 -or $documentPriorityStart -lt 0) {
    throw 'Could not locate the Hard Constraints section boundaries.'
}

if ($policyStart -le $hardConstraintsStart -or $policyStart -ge $documentPriorityStart) {
    throw 'The Computer Use opt-in policy is not located under Hard Constraints.'
}

Write-Output 'PASS: AGENTS.md requires explicit current-request authorization for Computer Use.'
