$ErrorActionPreference = 'Stop'
$pcboundsRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $pcboundsRoot
try {
    $pcboundsProjects = @(
        'Project_Test/261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj',
        'Project_Test/261007_TEST_panel-cladding-pid-layer-scope/PanelCladdingPidLayerScopeSmoke.csproj',
        'Project_Test/261007_TEST_panel-cladding-pid-setup-keys/PanelCladdingPidSetupKeysSmoke.csproj',
        'Project_Test/261007_TEST_panel-cladding-point-order/PanelCladdingPointOrderSmoke.csproj',
        'Project_Test/261007_TEST_panel-cladding-pid-planar-bounds/PanelCladdingPidPlanarBoundsSmoke.csproj'
    )
    foreach ($pcboundsConfig in @('Debug', 'Release')) {
        foreach ($pcboundsProject in $pcboundsProjects) {
            $pcboundsName = [IO.Path]::GetFileNameWithoutExtension($pcboundsProject)
            & dotnet run --project $pcboundsProject -c $pcboundsConfig --nologo > (Join-Path $PSScriptRoot "$pcboundsConfig-$pcboundsName.log") 2>&1
            if ($LASTEXITCODE -ne 0) { throw "$pcboundsConfig $pcboundsName failed" }
            Write-Output "PASS $pcboundsConfig $pcboundsName"
        }
        & dotnet build MCP_Rhino.sln -c $pcboundsConfig --nologo -m:1 -p:BuildInParallel=false > (Join-Path $PSScriptRoot "$pcboundsConfig-solution.log") 2>&1
        if ($LASTEXITCODE -ne 0) { throw "$pcboundsConfig solution build failed" }
        Write-Output "PASS $pcboundsConfig solution build"
        & dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c $pcboundsConfig --nologo > (Join-Path $PSScriptRoot "$pcboundsConfig-standalone.log") 2>&1
        if ($LASTEXITCODE -ne 0) { throw "$pcboundsConfig standalone build failed" }
        Write-Output "PASS $pcboundsConfig standalone RHP build"
    }
} finally { Pop-Location }
