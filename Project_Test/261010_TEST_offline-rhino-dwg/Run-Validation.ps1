[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
Push-Location $repo
try {
    foreach ($configuration in @('Debug', 'Release')) {
        & dotnet build .\MCP_Rhino.sln -c $configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw "$configuration solution build failed." }
        & dotnet run --project Project_Test/261010_TEST_offline-copy-edit/OfflineCopyEditSmoke.csproj -c $configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "$configuration bounded JS regression failed." }
        & dotnet run --project Project_Archive/Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke/RouterProtocolSmoke.csproj -c $configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "$configuration Router protocol regression failed." }
        & dotnet run --project (Join-Path $PSScriptRoot 'OfflineRhinoSmoke.csproj') -c $configuration -- $configuration
        if ($LASTEXITCODE -ne 0) { throw "$configuration native Rhino integration failed." }
        $cliOutput = Join-Path $PSScriptRoot "bin\cli-$configuration"
        & dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -c $configuration --nologo -m:1 -p:McpRhinoCliHost=true -o $cliOutput
        if ($LASTEXITCODE -ne 0) { throw "$configuration CLI validation build failed." }
        & dotnet (Join-Path $cliOutput 'MCP_Rhino.Server.dll') mcp-tool-safety-annotations-smoke-test
        if ($LASTEXITCODE -ne 0) { throw "$configuration MCP safety smoke failed." }
        & (Join-Path $repo "src\MCP_Rhino.Router\bin\$configuration\net8.0\MCP_Rhino.Router.exe") --validate-install
        if ($LASTEXITCODE -ne 0) { throw "$configuration Router bundle validation failed." }
    }
} finally { Pop-Location }
