param(
    [string]$RepoRoot = "",
    [string]$PipeName = "mcp_rhino",
    [string]$DocumentPath = "",
    [int]$TimeoutSeconds = 20
)

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
}

$probe = Join-Path $RepoRoot "Project_Archive\Project_Test\260422_TEST_mcp-client-integration\samples\handshake_probe.ps1"
if (-not (Test-Path -LiteralPath $probe)) {
    Write-Error "Handshake probe not found: $probe"
    exit 1
}

$bridgeCandidates = @(
    @{
        Name = "Debug"
        Path = Join-Path $RepoRoot "src\MCP_Rhino.Bridge\bin\Debug\net8.0\MCP_Rhino.Bridge.exe"
    },
    @{
        Name = "Release"
        Path = Join-Path $RepoRoot "src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe"
    }
)

foreach ($candidate in $bridgeCandidates) {
    if (-not (Test-Path -LiteralPath $candidate.Path)) {
        Write-Error "$($candidate.Name) bridge executable not found: $($candidate.Path)"
        exit 1
    }

    Write-Host "Running $($candidate.Name) bridge probe against \\.\pipe\$PipeName"

    $args = @(
        "-ExecutionPolicy", "Bypass",
        "-File", $probe,
        "-BridgeExe", $candidate.Path,
        "-PipeName", $PipeName,
        "-TimeoutSeconds", $TimeoutSeconds,
        "-MinimumToolCount", 1,
        "-ExpectedToolName", "get_document_summary"
    )

    if (-not [string]::IsNullOrWhiteSpace($DocumentPath)) {
        $args += @("-DocumentPath", $DocumentPath)
    }

    & powershell @args
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$($candidate.Name) bridge probe failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
}

Write-Host "Debug and Release bridge probes completed successfully."
