$ErrorActionPreference = 'Stop'
$materialRemovalRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $materialRemovalRoot
try {
    foreach ($materialRemovalConfig in @('Debug', 'Release')) {
        & dotnet run --project Project_Test/260818_TEST_material-catalogue-editing/MaterialCatalogueEditingSmoke.csproj -c $materialRemovalConfig -- $PSScriptRoot > (Join-Path $PSScriptRoot "$materialRemovalConfig-editing.log") 2>&1
        if ($LASTEXITCODE -ne 0) { throw "$materialRemovalConfig material catalogue smoke failed" }
        Write-Output "PASS $materialRemovalConfig material catalogue editing/removal smoke"
        & dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c $materialRemovalConfig --nologo > (Join-Path $PSScriptRoot "$materialRemovalConfig-build.log") 2>&1
        if ($LASTEXITCODE -ne 0) { throw "$materialRemovalConfig standalone editor build failed" }
        Write-Output "PASS $materialRemovalConfig standalone editor build"
    }
} finally { Pop-Location }
