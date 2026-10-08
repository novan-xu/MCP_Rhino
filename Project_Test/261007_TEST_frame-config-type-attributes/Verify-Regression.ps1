param([string[]]$Configurations = @('Debug', 'Release'))
$ErrorActionPreference = 'Stop'
$frameAttributeRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$frameAttributeSuites = @(
    '260820_TEST_panel-extrusion-assignment',
    '260819_TEST_panel-cladding-curve-topology',
    '260819_TEST_panel-cladding-hide-mask',
    '260819_TEST_panel-cladding-scoped-save',
    '260818_TEST_panel-cladding-topology-persistence',
    '260818_TEST_panel-cladding-create-command',
    '260820_TEST_panel-cladding-curve-template-ui',
    '260820_TEST_panel-cladding-sparse-topology',
    '260819_TEST_panel-cladding-layout-reconciliation',
    '260807_TEST_panel-cladding-surface-sync',
    '260819_TEST_panel-cladding-surface-sync-structural-grid',
    '260807_TEST_panel-cladding-clear',
    '260805_TEST_panel-cladding-match',
    '260903_TEST_panel-cladding-update-command'
)
$frameAttributeFailures = @()
Push-Location $frameAttributeRoot
try {
    foreach ($frameAttributeConfig in $Configurations) {
        foreach ($frameAttributeSuite in $frameAttributeSuites) {
            $frameAttributeProject = Get-ChildItem -LiteralPath (Join-Path 'Project_Test' $frameAttributeSuite) -Filter '*.csproj' | Select-Object -ExpandProperty FullName
            if (@($frameAttributeProject).Count -ne 1) { throw "Ambiguous project: $frameAttributeSuite" }
            & dotnet run --project $frameAttributeProject -c $frameAttributeConfig > (Join-Path $PSScriptRoot "$frameAttributeConfig-$frameAttributeSuite.log") 2>&1
            if ($LASTEXITCODE -ne 0) {
                $frameAttributeFailures += "$frameAttributeConfig $frameAttributeSuite"
                Write-Output "FAIL $frameAttributeConfig $frameAttributeSuite"
            } else { Write-Output "PASS $frameAttributeConfig $frameAttributeSuite" }
        }
        & dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c $frameAttributeConfig --nologo > (Join-Path $PSScriptRoot "$frameAttributeConfig-build.log") 2>&1
        if ($LASTEXITCODE -ne 0) { $frameAttributeFailures += "$frameAttributeConfig build" }
        else { Write-Output "PASS $frameAttributeConfig standalone build" }
    }
    if ($frameAttributeFailures.Count -gt 0) { throw "Failed: $($frameAttributeFailures -join ', ')" }
} finally { Pop-Location }
