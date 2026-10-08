$ErrorActionPreference = 'Stop'
$curveMatchRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$curveMatchProjects = [ordered]@{
    'topology' = 'Project_Test/260819_TEST_panel-cladding-curve-topology/PanelCladdingCurveTopologySmoke.csproj'
    'hide' = 'Project_Test/260819_TEST_panel-cladding-hide-mask/PanelCladdingHideMaskSmoke.csproj'
    'match' = 'Project_Test/260805_TEST_panel-cladding-match/PanelCladdingMatchSmoke.csproj'
    'assignments' = 'Project_Test/260820_TEST_panel-extrusion-assignment/PanelExtrusionAssignmentSmoke.csproj'
}
Push-Location $curveMatchRoot
try {
    foreach ($curveMatchConfig in @('Debug', 'Release')) {
        foreach ($curveMatchSuite in $curveMatchProjects.GetEnumerator()) {
            & dotnet run --project $curveMatchSuite.Value -c $curveMatchConfig > (Join-Path $PSScriptRoot "$curveMatchConfig-$($curveMatchSuite.Key).log") 2>&1
            if ($LASTEXITCODE -ne 0) { throw "$curveMatchConfig $($curveMatchSuite.Key) smoke failed" }
            Write-Output "PASS $curveMatchConfig $($curveMatchSuite.Key) smoke"
        }
        & dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c $curveMatchConfig --nologo > (Join-Path $PSScriptRoot "$curveMatchConfig-build.log") 2>&1
        if ($LASTEXITCODE -ne 0) { throw "$curveMatchConfig standalone editor build failed" }
        Write-Output "PASS $curveMatchConfig standalone editor build"
    }
} finally { Pop-Location }
