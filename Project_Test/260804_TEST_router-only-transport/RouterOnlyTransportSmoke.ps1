[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))

function Read-RepoFile([string] $RelativePath) {
    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file is missing: $RelativePath"
    }

    return Get-Content -LiteralPath $path -Raw
}

function Require([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    Write-Output "[OK] $Message"
}

$clientConfig = Read-RepoFile 'Packaging\MCP_Rhino\ClientConfigs\mcp-rhino.generic.json'
$installer = Read-RepoFile 'Packaging\MCP_Rhino\Install-McpRhino.ps1'
$solution = Read-RepoFile 'MCP_Rhino.sln'
$serverProject = Read-RepoFile 'src\MCP_Rhino.Server\MCP_Rhino.Server.csproj'
$plugin = Read-RepoFile 'src\MCP_Rhino.Server\Infrastructure\Plugin\MCP_Rhino.RhinoPlugin.cs'
$bootstrap = Read-RepoFile 'src\MCP_Rhino.Server\Infrastructure\Plugin\ServerBootstrap.cs'
$packageBuild = Read-RepoFile 'Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1'
$packageDefinition = Read-RepoFile 'Packaging\MCP_Rhino\package-manifest.json'
$workflow = Read-RepoFile 'Runtime_Workflow\MCP_Rhino Workflow.md'
$architecture = Read-RepoFile 'Project_Guides\MCP_Rhino Architecture.md'

Require ($clientConfig -match 'MCP_Rhino\.Router\.exe') 'Tracked MCP client template launches Router.'
Require ($clientConfig -notmatch 'MCP_Rhino\.Bridge') 'Tracked MCP client template does not launch Bridge.'
Require ($installer -match 'routerExecutable') 'Installer-owned MCP entry resolves the installed Router.'
Require ($installer -match "transportMode\s*=\s*'router-only'") 'Installer records Router-only ownership.'
Require ($installer -match 'priorIsRouterOnly') 'Legacy upgrade distinguishes retired transport rollbacks.'
Require ($packageDefinition -match '"transportMode"\s*:\s*"router-only"') 'Package definition declares Router-only transport.'
$localConfigPath = Join-Path $repoRoot '.mcp.json'
if (Test-Path -LiteralPath $localConfigPath -PathType Leaf) {
    $localConfig = Get-Content -LiteralPath $localConfigPath -Raw
    Require ($localConfig -match 'MCP_Rhino\.Router\.exe') 'Local ignored MCP config launches Router.'
    Require ($localConfig -notmatch 'MCP_Rhino\.Bridge') 'Local ignored MCP config does not launch Bridge.'
}
$codexConfigPath = Join-Path $repoRoot '.codex\config.toml'
if (Test-Path -LiteralPath $codexConfigPath -PathType Leaf) {
    $codexConfig = Get-Content -LiteralPath $codexConfigPath -Raw
    Require ($codexConfig -match 'MCP_Rhino\.Router\.exe') 'Local Codex MCP config launches Router.'
    Require ($codexConfig -notmatch 'MCP_Rhino\.Bridge') 'Local Codex MCP config does not launch Bridge.'
}
Require ($solution -notmatch 'MCP_Rhino\.Bridge|MCP_Rhino\.Companion') 'Solution contains no Bridge or Companion project.'
Require ($serverProject -notmatch 'MCP_RHINO_BRIDGE_PIPE_ONLY') 'Debug and Release share the Router-only plugin shape.'
Require ($plugin -match 'PlugInLoadTime\.AtStartup') 'Plugin loads at startup to publish route endpoints.'
Require ($plugin -notmatch 'TryShowChatPanel|StartBoundPipeServer|DeveloperDebugPipeName|IsBridgePipeOnlyBuild') 'Plugin exposes no legacy transport or chat entrypoint.'
Require ($bootstrap -notmatch 'StartBoundPipeServer|CreateConnectionHost|_pipeServer|_boundPipe') 'Server bootstrap owns routed pipe hosts only.'
Require ($packageBuild -notmatch "Bridge\s*=|Companion\s*=|@\('Router',\s*'Bridge'|@\('Router',\s*'Companion'") 'Package build publishes Router only.'
Require (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj'))) 'Bridge project source is retired.'
Require (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj'))) 'Companion project source is retired.'

$retiredCommandFiles = @(
    'McpChatCommand.cs',
    'McpDebugBridgeOnlyPluginSmokeCommand.cs',
    'McpPanelRuntimePolicySmokeCommand.cs',
    'McpRhinoClaudeCodeCompanionUiSmokeCommand.cs',
    'McpRhinoClaudeCodePanelSmokeCommand.cs'
)
foreach ($fileName in $retiredCommandFiles) {
    $matches = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\MCP_Rhino.Server') -Filter $fileName -File -Recurse -ErrorAction SilentlyContinue)
    Require ($matches.Count -eq 0) "Retired Rhino command source is absent: $fileName"
}

$activeCommandNames = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\MCP_Rhino.Server\Infrastructure\Plugin') -Filter '*.cs' -File -Recurse |
    ForEach-Object {
        $source = Get-Content -LiteralPath $_.FullName -Raw
        [regex]::Matches($source, 'EnglishName\s*=>\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    })
$retiredCommandNames = @($activeCommandNames | Where-Object { $_ -match '(?i)(chat|companion|panel|bridge|claudecode)' })
Require ($retiredCommandNames.Count -eq 0) 'Active Rhino command registry contains no chat, companion, panel, Bridge, or ClaudeCode entry.'
Require ($workflow -match 'only supported MCP client connection') 'Runtime workflow declares Router as the only supported connection.'
Require ($architecture -match 'Router-only transport contract') 'Architecture declares the Router-only transport contract.'

Write-Output '[OK] router-only-transport-smoke-test'
