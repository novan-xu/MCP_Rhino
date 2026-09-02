[CmdletBinding()]
param(
    [ValidateSet('Install', 'Repair', 'Validate')]
    [string] $Mode = 'Install',
    [string] $BundleRoot,
    [string] $ProductRoot = (Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor'),
    [string] $RhinoPluginRoot = (Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor\plugin'),
    [string] $LegacyRhinoPackageRoot = (Join-Path $env:APPDATA 'McNeel\Rhinoceros\packages\8.0'),
    [string] $RhinoPluginRegistryRoot = 'Registry::HKEY_CURRENT_USER\Software\McNeel\Rhinoceros\8.0\Plug-Ins'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($BundleRoot)) { $BundleRoot = Split-Path -Parent $PSScriptRoot }
$BundleRoot = [IO.Path]::GetFullPath($BundleRoot)
$ProductRoot = [IO.Path]::GetFullPath($ProductRoot)
$RhinoPluginRoot = [IO.Path]::GetFullPath($RhinoPluginRoot)
$LegacyRhinoPackageRoot = [IO.Path]::GetFullPath($LegacyRhinoPackageRoot)
$defaultRegistryRoot = 'Registry::HKEY_CURRENT_USER\Software\McNeel\Rhinoceros\8.0\Plug-Ins'
if ($RhinoPluginRegistryRoot.StartsWith('HKCU:\', [StringComparison]::OrdinalIgnoreCase)) {
    $RhinoPluginRegistryRoot = 'Registry::HKEY_CURRENT_USER\' + $RhinoPluginRegistryRoot.Substring('HKCU:\'.Length)
}
if (-not $RhinoPluginRegistryRoot.StartsWith('Registry::HKEY_CURRENT_USER\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'RhinoPluginRegistryRoot must be an explicit HKEY_CURRENT_USER registry provider path.'
}

function Test-PathUnder([string] $Candidate, [string] $Root) {
    $candidatePath = [IO.Path]::GetFullPath($Candidate)
    $rootPrefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    return $candidatePath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Get-PanelPluginKey([string] $RegistryRoot, [string] $PluginId) {
    return Join-Path $RegistryRoot $PluginId.ToLowerInvariant()
}

function Assert-DirectPanelRhp([string] $Root) {
    $rhp = Join-Path $Root 'PanelCladdingEditor.rhp'
    $dll = Join-Path $Root 'PanelCladdingEditor.dll'
    $deps = Join-Path $Root 'PanelCladdingEditor.deps.json'
    if (-not (Test-Path -LiteralPath $rhp -PathType Leaf)) { throw "PanelCladdingEditor.rhp is missing under $Root." }
    if (Test-Path -LiteralPath $dll -PathType Leaf) { throw "Duplicate plug-in identity found under ${Root}: PanelCladdingEditor.dll must not accompany the RHP." }
    if (-not (Test-Path -LiteralPath $deps -PathType Leaf)) { throw "PanelCladdingEditor.deps.json is missing under $Root." }
    $depsText = Get-Content -Raw -LiteralPath $deps
    if ($depsText.IndexOf('PanelCladdingEditor.rhp', [StringComparison]::Ordinal) -lt 0 -or
        $depsText.IndexOf('PanelCladdingEditor.dll', [StringComparison]::Ordinal) -ge 0) {
        throw 'PanelCladdingEditor dependency metadata does not identify the direct RHP build.'
    }
    if ([Reflection.AssemblyName]::GetAssemblyName($rhp).Name -ne 'PanelCladdingEditor') {
        throw 'PanelCladdingEditor.rhp has an unexpected managed assembly identity.'
    }
}

function Get-PanelExpectedCommands {
    return @(
        'PCClear',
        'PCCreate',
        'PCCrvTemplate',
        'PCEditor',
        'PCMatchCrv',
        'PCMatchSrf',
        'PCSpawnCrv',
        'PCSpawnSrf',
        'PCSyncCrv',
        'PCSyncSrf'
    )
}

function Set-PanelPluginRegistration([string] $RegistryRoot, [string] $PluginId, [string] $RhpPath) {
    $pluginKey = Get-PanelPluginKey $RegistryRoot $PluginId
    $pluginFileKey = Join-Path $pluginKey 'PlugIn'
    $commandListKey = Join-Path $pluginKey 'CommandList'
    $canonicalRhp = [IO.Path]::GetFullPath($RhpPath)

    if (Test-Path -LiteralPath $pluginKey) {
        $existing = Get-ItemProperty -LiteralPath $pluginKey -ErrorAction SilentlyContinue
        $existingFile = Get-ItemProperty -LiteralPath $pluginFileKey -ErrorAction SilentlyContinue
        $existingName = [string]$existing.Name
        $existingPaths = @([string]$existing.FileName, [string]$existingFile.FileName) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        if ((-not [string]::IsNullOrWhiteSpace($existingName) -and
             -not $existingName.Equals('PanelCladdingEditor', [StringComparison]::OrdinalIgnoreCase)) -or
            @($existingPaths | Where-Object {
                [IO.Path]::GetFileName($_) -ne 'PanelCladdingEditor.rhp'
            }).Count -gt 0) {
            throw "The PanelCladdingEditor plug-in GUID is registered to an unowned product: $pluginKey"
        }

        # Replace only the verified product-owned key so stale shorthand,
        # partial, or older command registrations cannot survive repair.
        Remove-Item -LiteralPath $pluginKey -Recurse -Force
    }

    New-Item -Path $pluginKey -Force | Out-Null
    foreach ($entry in ([ordered]@{
        Name = 'PanelCladdingEditor'
        EnglishName = 'PanelCladdingEditor'
        Organization = 'BayHealth project team'
        Address = ''
        Country = ''
        Phone = ''
        EMail = ''
        WebSite = ''
        UpdateURL = ''
        Fax = ''
        Description = 'Standalone Rhino 8 panel cladding type editor.'
        RegPath = '\\HKEY_CURRENT_USER\' + $pluginKey.Substring('Registry::HKEY_CURRENT_USER\'.Length)
    }).GetEnumerator()) {
        New-ItemProperty -LiteralPath $pluginKey -Name $entry.Key -Value $entry.Value -PropertyType String -Force | Out-Null
    }
    foreach ($entry in ([ordered]@{
        AddToHelpMenu = 0
        LoadMode = 1
        Type = 16
        IsDotNETPlugIn = 1
        DirectoryInstall = 0
    }).GetEnumerator()) {
        New-ItemProperty -LiteralPath $pluginKey -Name $entry.Key -Value $entry.Value -PropertyType DWord -Force | Out-Null
    }

    New-Item -Path $pluginFileKey, $commandListKey -Force | Out-Null
    New-ItemProperty -LiteralPath $pluginFileKey -Name FileName -Value $canonicalRhp -PropertyType String -Force | Out-Null
    foreach ($command in @(Get-PanelExpectedCommands)) {
        New-ItemProperty -LiteralPath $commandListKey -Name $command -Value "2;$command" -PropertyType String -Force | Out-Null
    }
}

function Assert-PanelPluginRegistration([string] $RegistryRoot, [string] $PluginId, [string] $RhpPath) {
    $pluginKey = Get-PanelPluginKey $RegistryRoot $PluginId
    $pluginFileKey = Join-Path $pluginKey 'PlugIn'
    $commandListKey = Join-Path $pluginKey 'CommandList'
    if (-not (Test-Path -LiteralPath $pluginKey)) {
        throw 'The canonical PanelCladdingEditor registry registration is missing.'
    }

    $root = Get-ItemProperty -LiteralPath $pluginKey
    $canonicalRhp = [IO.Path]::GetFullPath($RhpPath)
    $rootFileName = [string]$root.FileName
    $hasPluginFile = Test-Path -LiteralPath $pluginFileKey
    $hasCommandList = Test-Path -LiteralPath $commandListKey
    if (-not [string]::IsNullOrWhiteSpace($rootFileName)) {
        throw 'The complete PanelCladdingEditor registry registration still contains shorthand FileName.'
    }

    if (-not $hasPluginFile -or -not $hasCommandList) {
        throw 'The complete PanelCladdingEditor registry registration is incomplete.'
    }

    $child = Get-ItemProperty -LiteralPath $pluginFileKey
    $expectedRegPath = '\\HKEY_CURRENT_USER\' + $pluginKey.Substring('Registry::HKEY_CURRENT_USER\'.Length)
    if (-not ([string]$root.Name).Equals('PanelCladdingEditor', [StringComparison]::OrdinalIgnoreCase) -or
        -not ([string]$root.EnglishName).Equals('PanelCladdingEditor', [StringComparison]::OrdinalIgnoreCase) -or
        -not ([string]$root.RegPath).Equals($expectedRegPath, [StringComparison]::OrdinalIgnoreCase) -or
        [int]$root.LoadMode -ne 1 -or [int]$root.Type -ne 16 -or
        [int]$root.DirectoryInstall -ne 0 -or
        [int]$root.IsDotNETPlugIn -ne 1 -or
        -not ([IO.Path]::GetFullPath([string]$child.FileName)).Equals($canonicalRhp, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The complete PanelCladdingEditor registry registration is inconsistent.'
    }

    $expectedCommands = @(Get-PanelExpectedCommands)
    $commandProperties = @((Get-ItemProperty -LiteralPath $commandListKey).PSObject.Properties |
        Where-Object { $_.Name -notmatch '^PS(Path|ParentPath|ChildName|Drive|Provider)$' } |
        Sort-Object Name)
    $registeredCommands = @($commandProperties | ForEach-Object { $_.Name })
    $missingCommands = @($expectedCommands | Where-Object { $_ -notin $registeredCommands })
    $unexpectedCommands = @($registeredCommands | Where-Object { $_ -notin $expectedCommands })
    $invalidCommandValues = @($commandProperties | Where-Object { [string]$_.Value -ne "2;$($_.Name)" })
    if ($missingCommands.Count -gt 0 -or $unexpectedCommands.Count -gt 0 -or $invalidCommandValues.Count -gt 0) {
        throw "The complete PanelCladdingEditor command registration is inconsistent. Missing: $($missingCommands -join ', '); unexpected: $($unexpectedCommands -join ', '); invalid values: $($invalidCommandValues.Name -join ', ')."
    }
}

function Assert-SinglePanelDiscovery(
    [string] $PluginBase,
    [string] $ActivePluginRoot,
    [string] $PackageBase,
    [string[]] $LegacyNames
) {
    $activeRhp = [IO.Path]::GetFullPath((Join-Path $ActivePluginRoot 'PanelCladdingEditor.rhp'))
    $installed = if (Test-Path -LiteralPath $PluginBase -PathType Container) {
        @(Get-ChildItem -LiteralPath $PluginBase -Filter 'PanelCladdingEditor.rhp' -File -Recurse -ErrorAction SilentlyContinue)
    } else { @() }
    if ($installed.Count -ne 1 -or
        -not ([IO.Path]::GetFullPath($installed[0].FullName)).Equals($activeRhp, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The registry-owned plug-in tree must contain exactly one active PanelCladdingEditor.rhp.'
    }

    $packageCopies = if (Test-Path -LiteralPath $PackageBase -PathType Container) {
        @(Get-ChildItem -LiteralPath $PackageBase -Filter 'PanelCladdingEditor.rhp' -File -Recurse -ErrorAction SilentlyContinue)
    } else { @() }
    if ($packageCopies.Count -gt 0) {
        throw "A Package Manager-discovered PanelCladdingEditor RHP remains: $($packageCopies[0].FullName)"
    }
    foreach ($name in $LegacyNames) {
        $root = Join-Path $PackageBase $name
        if (Test-Path -LiteralPath (Join-Path $root 'manifest.txt') -PathType Leaf) {
            throw "Legacy Package Manager selection metadata remains: $root"
        }
    }
}

$bundleManifestPath = Join-Path $BundleRoot 'bundle-manifest.json'
$definitionPath = Join-Path $BundleRoot 'package-manifest.json'
if (-not (Test-Path -LiteralPath $bundleManifestPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $definitionPath -PathType Leaf)) {
    throw 'PanelCladdingEditor bundle metadata is missing. Run Build-PanelCladdingEditorPackage.ps1 first.'
}
$bundle = Get-Content -Raw -LiteralPath $bundleManifestPath | ConvertFrom-Json
$definition = Get-Content -Raw -LiteralPath $definitionPath | ConvertFrom-Json
if ($bundle.name -ne 'PanelCladdingEditor' -or $definition.name -ne 'PanelCladdingEditor' -or
    $bundle.version -ne $definition.version -or $definition.pluginRegistrationMode -ne 'registry-only') {
    throw 'Unsupported or inconsistent PanelCladdingEditor bundle.'
}
foreach ($file in $bundle.files) {
    $source = Join-Path $BundleRoot ([string]$file.path).Replace('/', '\')
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Bundle file is missing: $($file.path)" }
    $actual = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne [string]$file.sha256) { throw "Bundle hash mismatch: $($file.path)" }
}
Assert-DirectPanelRhp (Join-Path $BundleRoot 'Plugin')

$version = [string]$definition.version
$pluginId = [string]$definition.pluginId
$pluginRoot = Join-Path $RhinoPluginRoot $version
$rhpPath = Join-Path $pluginRoot 'PanelCladdingEditor.rhp'
$manifestPath = Join-Path $ProductRoot 'install-manifest.json'
$legacyNames = @($definition.legacyPluginPackageNames | ForEach-Object { [string]$_ })
$legacyRoots = @($legacyNames | ForEach-Object { Join-Path $LegacyRhinoPackageRoot $_ })
$prior = if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
} else { $null }

if ($Mode -eq 'Validate') {
    if ($null -eq $prior -or $prior.product -ne 'PanelCladdingEditor' -or
        $prior.schemaVersion -ne 1 -or $prior.version -ne $version -or
        $prior.pluginRegistrationMode -ne 'registry-only') {
        throw 'The installed PanelCladdingEditor ownership manifest is missing or incompatible.'
    }
    $invalid = @($prior.files | Where-Object {
        -not (Test-Path -LiteralPath $_.path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $_.path -Algorithm SHA256).Hash.ToLowerInvariant() -ne [string]$_.sha256
    })
    if ($invalid.Count -gt 0) { throw "Installed file validation failed for $($invalid.Count) file(s)." }
    Assert-DirectPanelRhp $pluginRoot
    Assert-PanelPluginRegistration $RhinoPluginRegistryRoot $pluginId $rhpPath
    Assert-SinglePanelDiscovery $RhinoPluginRoot $pluginRoot $LegacyRhinoPackageRoot $legacyNames
    Write-Output "PanelCladdingEditor $version registry-only installation is valid (complete registration)."
    return
}

$defaultProductRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor'))
$defaultPluginRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor\plugin'))
$defaultPackageRoot = [IO.Path]::GetFullPath((Join-Path $env:APPDATA 'McNeel\Rhinoceros\packages\8.0'))
$usingDefaultFiles = $ProductRoot.Equals($defaultProductRoot, [StringComparison]::OrdinalIgnoreCase) -and
    $RhinoPluginRoot.Equals($defaultPluginRoot, [StringComparison]::OrdinalIgnoreCase) -and
    $LegacyRhinoPackageRoot.Equals($defaultPackageRoot, [StringComparison]::OrdinalIgnoreCase)
$usingDefaultRegistry = $RhinoPluginRegistryRoot.Equals($defaultRegistryRoot, [StringComparison]::OrdinalIgnoreCase)
if ($usingDefaultFiles -ne $usingDefaultRegistry) {
    throw 'Default filesystem roots and the production Rhino registry root must be used together.'
}
$usingDefaultRoots = $usingDefaultFiles -and $usingDefaultRegistry
if ($usingDefaultRoots -and @(Get-Process -Name Rhino -ErrorAction SilentlyContinue).Count -gt 0) {
    $stageBase = Join-Path $ProductRoot 'staged'
    New-Item -ItemType Directory -Path $stageBase -Force | Out-Null
    $stageRoot = Join-Path $stageBase ("{0}-{1}" -f $version, [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff'))
    New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $BundleRoot '*') -Destination $stageRoot -Recurse -Force
    $stageInstaller = Join-Path $stageRoot 'Installer\Install-PanelCladdingEditor.ps1'
    Write-Warning "PanelCladdingEditor $version was staged because Rhino is running. Close every Rhino window, then run: powershell -ExecutionPolicy Bypass -File `"$stageInstaller`" -Mode Install -BundleRoot `"$stageRoot`""
    Write-Output $stageRoot
    return
}

if ($null -ne $prior -and ($prior.product -ne 'PanelCladdingEditor' -or $prior.schemaVersion -ne 1)) {
    throw 'The existing PanelCladdingEditor ownership manifest is incompatible.'
}
if ($null -eq $prior -and (Test-Path -LiteralPath $RhinoPluginRoot -PathType Container) -and
    @(Get-ChildItem -LiteralPath $RhinoPluginRoot -Force | Where-Object { $_.Name -ne 'staged' }).Count -gt 0) {
    throw 'An unowned registry-only PanelCladdingEditor plug-in tree already exists.'
}

$allowedRoots = @($RhinoPluginRoot) + $legacyRoots
$searchRoots = @($LegacyRhinoPackageRoot, $RhinoPluginRoot)
if ($usingDefaultRoots) {
    $searchRoots += @(
        (Join-Path $env:APPDATA 'McNeel\Rhinoceros\8.0\Plug-ins'),
        (Join-Path $env:LOCALAPPDATA 'McNeel\Rhinoceros\8.0\Plug-ins')
    )
}
$unexpected = @($searchRoots | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | ForEach-Object {
    Get-ChildItem -LiteralPath $_ -Filter 'PanelCladdingEditor.rhp' -File -Recurse -ErrorAction SilentlyContinue
} | Where-Object {
    $candidate = $_.FullName
    -not @($allowedRoots | Where-Object { Test-PathUnder $candidate $_ }).Count
})
if ($unexpected.Count -gt 0) { throw "Another PanelCladdingEditor RHP is discoverable: $($unexpected[0].FullName)" }

$installId = [Guid]::NewGuid().ToString('N')
$pluginStage = Join-Path $RhinoPluginRoot ".$version-$installId"
$rollbackRoot = Join-Path (Join-Path $ProductRoot 'rollback') $installId
New-Item -ItemType Directory -Path $pluginStage, $rollbackRoot -Force | Out-Null
$installedFiles = @()
foreach ($file in @($bundle.files | Where-Object { ([string]$_.path).StartsWith('Plugin/', [StringComparison]::OrdinalIgnoreCase) })) {
    $relative = ([string]$file.path).Substring('Plugin/'.Length).Replace('/', '\')
    $source = Join-Path (Join-Path $BundleRoot 'Plugin') $relative
    $target = Join-Path $pluginStage $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}
Assert-DirectPanelRhp $pluginStage

$moved = @()
$activated = $false
try {
    if (Test-Path -LiteralPath $pluginRoot -PathType Container) {
        $backup = Join-Path $rollbackRoot 'prior-active'
        Move-Item -LiteralPath $pluginRoot -Destination $backup
        $moved += [pscustomobject]@{ original = $pluginRoot; backup = $backup }
    }
    Get-ChildItem -LiteralPath $RhinoPluginRoot -Directory -ErrorAction SilentlyContinue | Where-Object {
        $_.FullName -ne $pluginStage -and $_.Name -ne 'staged' -and $_.FullName -ne $rollbackRoot
    } | ForEach-Object {
        $backup = Join-Path $rollbackRoot ("prior-plugin-" + $_.Name)
        Move-Item -LiteralPath $_.FullName -Destination $backup
        $moved += [pscustomobject]@{ original = $_.FullName; backup = $backup }
    }
    Move-Item -LiteralPath $pluginStage -Destination $pluginRoot
    $activated = $true
    Set-PanelPluginRegistration $RhinoPluginRegistryRoot $pluginId $rhpPath

    foreach ($legacyRoot in $legacyRoots) {
        if (-not (Test-Path -LiteralPath $legacyRoot -PathType Container)) { continue }
        $backup = Join-Path $rollbackRoot ("legacy-package-" + (Split-Path -Leaf $legacyRoot))
        Move-Item -LiteralPath $legacyRoot -Destination $backup
        $moved += [pscustomobject]@{ original = $legacyRoot; backup = $backup }
    }

    Assert-DirectPanelRhp $pluginRoot
    Assert-PanelPluginRegistration $RhinoPluginRegistryRoot $pluginId $rhpPath
    Assert-SinglePanelDiscovery $RhinoPluginRoot $pluginRoot $LegacyRhinoPackageRoot $legacyNames

    $installedFiles = @(Get-ChildItem -LiteralPath $pluginRoot -File -Recurse | ForEach-Object {
        [ordered]@{
            path = $_.FullName
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    [ordered]@{
        schemaVersion = 1
        product = 'PanelCladdingEditor'
        version = $version
        installedUtc = [DateTime]::UtcNow.ToString('O')
        pluginId = $pluginId
        pluginRegistrationMode = 'registry-only'
        pluginRoot = $pluginRoot
        rhinoPluginRegistryRoot = $RhinoPluginRegistryRoot
        legacyPackageRoot = $LegacyRhinoPackageRoot
        legacyPackageNames = $legacyNames
        files = $installedFiles
        migrationBackups = $moved
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8
}
catch {
    $failure = $_
    $pluginKey = Get-PanelPluginKey $RhinoPluginRegistryRoot $pluginId
    if (Test-Path -LiteralPath $pluginKey) { Remove-Item -LiteralPath $pluginKey -Recurse -Force }
    if ($activated -and (Test-Path -LiteralPath $pluginRoot -PathType Container)) {
        Remove-Item -LiteralPath $pluginRoot -Recurse -Force
    }
    foreach ($entry in @($moved | Sort-Object { ([string]$_.original).Length } -Descending)) {
        if (Test-Path -LiteralPath $entry.backup) {
            New-Item -ItemType Directory -Path (Split-Path -Parent ([string]$entry.original)) -Force | Out-Null
            Move-Item -LiteralPath $entry.backup -Destination $entry.original
        }
    }
    throw $failure
}

Write-Output "PanelCladdingEditor $version installed registry-only at $pluginRoot. Restart Rhino to load the plug-in."
