[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputRoot = Join-Path $repo '.validation\live-custom-scripts'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Start-Transcript -Path (Join-Path $outputRoot 'validation.log') -Force
try {
    foreach ($configuration in @('Debug', 'Release')) {
        & dotnet build (Join-Path $repo 'MCP_Rhino.sln') -c $configuration --nologo -m:1
        if ($LASTEXITCODE -ne 0) { throw "$configuration solution build failed." }
        $cli = Join-Path $outputRoot "cli-$configuration"
        & dotnet build (Join-Path $repo 'src\MCP_Rhino.Server\MCP_Rhino.Server.csproj') -c $configuration --nologo -m:1 -p:McpRhinoCliHost=true -o $cli
        if ($LASTEXITCODE -ne 0) { throw "$configuration CLI build failed." }
        foreach ($slug in @('live-custom-scripts-smoke-test', 'mcp-tool-safety-annotations-smoke-test', 'mcp-tool-overlap-cleanup-smoke-test')) {
            & dotnet (Join-Path $cli 'MCP_Rhino.Server.dll') $slug
            if ($LASTEXITCODE -ne 0) { throw "$configuration $slug failed." }
        }
    }
    & dotnet run --project (Join-Path $PSScriptRoot 'NativeChecks.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Native script regression failed.' }
    & dotnet run --project (Join-Path $repo 'Project_Archive\Project_Test\260729_TEST_multi-document-rhino-router\RouterProtocolSmoke\RouterProtocolSmoke.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Router protocol regression failed.' }
} finally { Stop-Transcript }
