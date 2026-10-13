param(
    [string]$ScriptPath = 'V:\01 Project Folders\P00012 Met Tang\03-Design-Eng\03-BIM\05-Wireframe\01_grasshopper\05_STN\StoneSurfaceBake.cs',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$delivery = (Resolve-Path -LiteralPath $ScriptPath).Path
$project = Join-Path $repository 'Project_Test\261009_TEST_grasshopper-stone-bake\GrasshopperStoneBake.Tests.csproj'
$setup = Join-Path $repository 'Scripts\Grasshopper\SetupStoneSurfaceBake.cs'
Write-Output "Validating linked delivery: $delivery"
Write-Output "SHA256: $((Get-FileHash -LiteralPath $delivery).Hash)"
& dotnet run --project $project -c $Configuration -- $delivery $setup --check-undo --check-assembly-surfaces --check-missing-only 2>&1 |
    Tee-Object -FilePath (Join-Path $PSScriptRoot "validation-$Configuration.log")
if ($LASTEXITCODE -ne 0) { throw "Linked script validation failed ($Configuration)." }
