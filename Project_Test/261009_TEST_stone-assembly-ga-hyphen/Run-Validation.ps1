[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$server = Join-Path $repo 'src\MCP_Rhino.Server\MCP_Rhino.Server.csproj'
foreach ($configuration in @('Debug', 'Release')) {
    $output = Join-Path $PSScriptRoot "bin\cli-$configuration"
    & dotnet build $server -c $configuration --nologo -m:1 -p:McpRhinoCliHost=true -o $output
    if ($LASTEXITCODE -ne 0) { throw "$configuration CLI build failed." }
    foreach ($slug in @('stone-wireframe-bake-smoke-test', 'mcp-tool-safety-annotations-smoke-test', 'mcp-tool-overlap-cleanup-smoke-test', 'multi-document-rhino-router-smoke-test')) {
        & dotnet (Join-Path $output 'MCP_Rhino.Server.dll') $slug
        if ($LASTEXITCODE -ne 0) { throw "$configuration $slug failed." }
    }
}
