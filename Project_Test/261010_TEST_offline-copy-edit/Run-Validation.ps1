[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
Push-Location $repo
try {
    foreach ($configuration in @('Debug', 'Release')) {
        & dotnet build .\MCP_Rhino.sln -c $configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw "$configuration solution build failed." }
        & dotnet run --project (Join-Path $PSScriptRoot 'OfflineCopyEditSmoke.csproj') -c $configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "$configuration offline-copy-edit-smoke-test failed." }
        & dotnet run --project Project_Archive/Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke/RouterProtocolSmoke.csproj -c $configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "$configuration Router protocol smoke failed." }
        $cliOutput = Join-Path $PSScriptRoot "bin\cli-$configuration"
        & dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -c $configuration --nologo -m:1 -p:McpRhinoCliHost=true -o $cliOutput
        if ($LASTEXITCODE -ne 0) { throw "$configuration CLI validation build failed." }
        & dotnet (Join-Path $cliOutput 'MCP_Rhino.Server.dll') mcp-tool-safety-annotations-smoke-test
        if ($LASTEXITCODE -ne 0) { throw "$configuration MCP safety smoke failed." }
    }
} finally { Pop-Location }
