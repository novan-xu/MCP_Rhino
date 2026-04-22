# Bridge handshake probe.
#
# Spawns MCP_Rhino.Bridge.exe, sends a MCP `initialize` + `tools/list`
# JSON-RPC request over stdio, prints the raw responses, then exits.
# Use to isolate "Bridge <-> Rhino" from "Client <-> Bridge" when something
# is wrong end-to-end.
#
# Prerequisites:
#   1. dotnet build of MCP_Rhino.Server and MCP_Rhino.Bridge in Release.
#   2. Rhino 8 running with the MCP_Rhino .rhp plugin loaded
#      (command line shows "Named pipe ready: \\.\pipe\mcp_rhino").
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File handshake_probe.ps1 `
#     -BridgeExe "C:\Projects\MCP_Rhino\src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe"

param(
    [Parameter(Mandatory = $true)]
    [string]$BridgeExe
)

if (-not (Test-Path -LiteralPath $BridgeExe)) {
    Write-Error "Bridge exe not found: $BridgeExe"
    exit 1
}

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $BridgeExe
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)

function Send-JsonRpc($writer, $obj) {
    $json = $obj | ConvertTo-Json -Depth 10 -Compress
    $writer.WriteLine($json)
    $writer.Flush()
}

$initialize = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{ name = "handshake-probe"; version = "0.1.0" }
    }
}

$initialized = @{
    jsonrpc = "2.0"
    method = "notifications/initialized"
    params = @{}
}

$toolsList = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/list"
    params = @{}
}

Send-JsonRpc $proc.StandardInput $initialize
Send-JsonRpc $proc.StandardInput $initialized
Send-JsonRpc $proc.StandardInput $toolsList

# Read responses until we've seen two (initialize response + tools/list response)
# or timeout after 10 seconds.
$deadline = (Get-Date).AddSeconds(10)
$responses = 0
while ($responses -lt 2 -and (Get-Date) -lt $deadline) {
    if ($proc.StandardOutput.Peek() -ge 0) {
        $line = $proc.StandardOutput.ReadLine()
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        Write-Output $line
        $responses++
    } else {
        Start-Sleep -Milliseconds 50
    }
}

$proc.StandardInput.Close()
$proc.WaitForExit(2000) | Out-Null
if (-not $proc.HasExited) { $proc.Kill() }

$stderr = $proc.StandardError.ReadToEnd()
if (-not [string]::IsNullOrWhiteSpace($stderr)) {
    Write-Warning "Bridge stderr:"
    Write-Warning $stderr
}
