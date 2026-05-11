# Bridge handshake probe.
#
# Spawns MCP_Rhino.Bridge.exe, sends MCP JSON-RPC requests over stdio,
# prints the raw JSON-RPC responses, and exits nonzero on timeout or error.
# Use to isolate "Bridge <-> Rhino" from "Client <-> Bridge" when something
# is wrong end-to-end.
#
# Prerequisites:
#   1. Build MCP_Rhino.Server and MCP_Rhino.Bridge in the configuration being tested.
#   2. Rhino 8 running with the MCP_Rhino .rhp plugin loaded.
#   3. The target pipe exists. Default: \\.\pipe\mcp_rhino.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File handshake_probe.ps1 `
#     -BridgeExe "C:\Projects\MCP_Rhino\src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe"

param(
    [Parameter(Mandatory = $true)]
    [string]$BridgeExe,

    [string]$PipeName = "mcp_rhino",

    [int]$TimeoutSeconds = 15,

    [string]$ProtocolVersion = "2025-11-25",

    [int]$MinimumToolCount = 1,

    [string]$ExpectedToolName = "",

    [string]$DocumentPath = ""
)

if (-not (Test-Path -LiteralPath $BridgeExe)) {
    Write-Error "Bridge exe not found: $BridgeExe"
    exit 1
}

function Send-JsonRpc($writer, $obj) {
    $json = $obj | ConvertTo-Json -Depth 30 -Compress
    $writer.WriteLine($json)
    $writer.Flush()
}

function Read-JsonRpcLine($reader, [int]$timeoutSeconds, [string]$label) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)

    while ($true) {
        $remaining = $deadline - (Get-Date)
        if ($remaining.TotalMilliseconds -le 0) {
            return $null
        }

        $task = $reader.ReadLineAsync()
        if (-not $task.Wait($remaining)) {
            return $null
        }

        $line = $task.Result
        if ($null -eq $line) {
            throw "Bridge closed stdout while waiting for $label."
        }

        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        return $line
    }
}

function Convert-Response($line, [string]$label) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        throw "Timed out waiting for $label response."
    }

    [Console]::Out.WriteLine($line)

    $message = $line | ConvertFrom-Json
    if ($message.PSObject.Properties.Name -contains "error") {
        throw "$label returned JSON-RPC error: $line"
    }

    return $message
}

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $BridgeExe
if (-not [string]::Equals($PipeName, "mcp_rhino", [System.StringComparison]::OrdinalIgnoreCase)) {
    $psi.Arguments = "--pipe $PipeName"
}
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true

$proc = $null
$exitCode = 0

try {
    $proc = [System.Diagnostics.Process]::Start($psi)
    if ($null -eq $proc) {
        throw "Failed to start bridge process: $BridgeExe"
    }

    Send-JsonRpc $proc.StandardInput @{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = @{
            protocolVersion = $ProtocolVersion
            capabilities = @{}
            clientInfo = @{ name = "handshake-probe"; version = "0.2.0" }
        }
    }
    [void](Convert-Response (Read-JsonRpcLine $proc.StandardOutput $TimeoutSeconds "initialize") "initialize")

    Send-JsonRpc $proc.StandardInput @{
        jsonrpc = "2.0"
        method = "notifications/initialized"
        params = @{}
    }

    Send-JsonRpc $proc.StandardInput @{
        jsonrpc = "2.0"
        id = 2
        method = "ping"
        params = @{}
    }
    [void](Convert-Response (Read-JsonRpcLine $proc.StandardOutput $TimeoutSeconds "ping") "ping")

    Send-JsonRpc $proc.StandardInput @{
        jsonrpc = "2.0"
        id = 3
        method = "tools/list"
        params = @{}
    }
    $toolsMessage = Convert-Response (Read-JsonRpcLine $proc.StandardOutput $TimeoutSeconds "tools/list") "tools/list"
    $tools = @($toolsMessage.result.tools)
    if ($tools.Count -lt $MinimumToolCount) {
        throw "tools/list returned $($tools.Count) tools, expected at least $MinimumToolCount."
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedToolName)) {
        $toolNames = @($tools | ForEach-Object { $_.name })
        if ($toolNames -notcontains $ExpectedToolName) {
            throw "tools/list did not include expected tool '$ExpectedToolName'."
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($DocumentPath)) {
        Send-JsonRpc $proc.StandardInput @{
            jsonrpc = "2.0"
            id = 4
            method = "tools/call"
            params = @{
                name = "get_document_summary"
                arguments = @{
                    filePath = $DocumentPath
                    maxObjectSummaries = 1
                    maxLayerSummaries = 5
                    maxNamedViews = 5
                    maxMaterials = 5
                }
            }
        }
        [void](Convert-Response (Read-JsonRpcLine $proc.StandardOutput $TimeoutSeconds "get_document_summary") "get_document_summary")
    }
}
catch {
    $exitCode = 1
    Write-Error $_.Exception.Message
}
finally {
    if ($null -ne $proc) {
        try {
            if (-not $proc.HasExited) {
                $proc.StandardInput.Close()
                $proc.WaitForExit(2000) | Out-Null
            }
        }
        catch {
        }

        if (-not $proc.HasExited) {
            $proc.Kill()
        }

        $stderr = $proc.StandardError.ReadToEnd()
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            Write-Warning "Bridge stderr:"
            Write-Warning $stderr
        }
    }
}

exit $exitCode
