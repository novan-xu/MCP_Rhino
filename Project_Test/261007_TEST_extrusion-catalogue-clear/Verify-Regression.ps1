$ErrorActionPreference = 'Stop'
$extrusionClearRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $extrusionClearRoot
try {
    foreach ($extrusionClearConfig in @('Debug', 'Release')) {
        & dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/PanelExtrusionAssignmentSmoke.csproj -c $extrusionClearConfig -- --clear-qa $PSScriptRoot > (Join-Path $PSScriptRoot "$extrusionClearConfig-extrusion.log") 2>&1
        if ($LASTEXITCODE -ne 0) { throw "$extrusionClearConfig extrusion smoke failed" }
        Write-Output "PASS $extrusionClearConfig extrusion catalogue clear and assignment smoke"
        & dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c $extrusionClearConfig --nologo > (Join-Path $PSScriptRoot "$extrusionClearConfig-build.log") 2>&1
        if ($LASTEXITCODE -ne 0) { throw "$extrusionClearConfig standalone editor build failed" }
        Write-Output "PASS $extrusionClearConfig standalone editor build"
    }
} finally { Pop-Location }
