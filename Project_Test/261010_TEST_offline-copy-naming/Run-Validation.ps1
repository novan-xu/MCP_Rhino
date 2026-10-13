$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
Push-Location $repo
try {
    foreach ($configuration in @('Debug','Release')) {
        & dotnet build MCP_Rhino.sln -c $configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw "$configuration build failed" }
        & dotnet run --project Project_Test/261010_TEST_offline-copy-edit/OfflineCopyEditSmoke.csproj -c $configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "$configuration naming/copy regression failed" }
    }
} finally { Pop-Location }
