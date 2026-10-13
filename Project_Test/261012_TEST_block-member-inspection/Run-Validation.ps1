[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$server = Join-Path $repo 'src\MCP_Rhino.Server\MCP_Rhino.Server.csproj'
$outputRoot = Join-Path $repo '.validation\block-member-inspection'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Start-Transcript -Path (Join-Path $outputRoot 'validation.log') -Force
try {
    foreach ($configuration in @('Debug', 'Release')) {
        & dotnet build $server -c $configuration --nologo -m:1 -o (Join-Path $outputRoot "plugin-$configuration")
        if ($LASTEXITCODE -ne 0) { throw "$configuration plugin build failed." }
        $cli = Join-Path $outputRoot "cli-$configuration"
        & dotnet build $server -c $configuration --nologo -m:1 -p:McpRhinoCliHost=true -o $cli
        if ($LASTEXITCODE -ne 0) { throw "$configuration CLI build failed." }
        foreach ($slug in @('block-member-inspection-smoke-test', 'mcp-tool-safety-annotations-smoke-test', 'mcp-tool-overlap-cleanup-smoke-test')) {
            & dotnet (Join-Path $cli 'MCP_Rhino.Server.dll') $slug
            if ($LASTEXITCODE -ne 0) { throw "$configuration $slug failed." }
        }
    }
    & dotnet run --project (Join-Path $PSScriptRoot 'NativeChecks.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Native regression failed.' }
    & node (Join-Path $PSScriptRoot 'Compare-StoneSizes.test.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'Comparison regression failed.' }
} finally { Stop-Transcript }
