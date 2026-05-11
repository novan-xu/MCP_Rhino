param(
    [string]$RepoRoot = "C:\01_Projects\MCP_Rhino",
    [string]$DocumentPath = "C:\Users\Novan\Desktop\Untitled.3dm"
)

$ErrorActionPreference = "Stop"
$BridgeExe = Join-Path $RepoRoot "src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe"
$RunStamp = Get-Date -Format "yyyyMMdd_HHmmss"
$RunId = "tool_audit_$RunStamp"
$ReportPath = Join-Path $RepoRoot "Project_Plan\260508_RUNTIME_TOOL_AUDIT_live-rhino.md"
$ExportDir = Join-Path $env:TEMP "mcp_rhino_tool_audit_$RunStamp"
$DrawingPackageDir = Join-Path $ExportDir "drawing_package"
New-Item -ItemType Directory -Force -Path $ExportDir | Out-Null
New-Item -ItemType Directory -Force -Path $DrawingPackageDir | Out-Null

$State = @{
    ids = @{}
    fixtureErrors = @{}
    data = @{}
    layers = @{
        root = "__MCP_TOOL_AUDIT_$RunStamp"
        geometry = "__MCP_TOOL_AUDIT_$RunStamp::Geometry"
        analysis = "__MCP_TOOL_AUDIT_$RunStamp::Analysis"
        architecture = "__MCP_TOOL_AUDIT_$RunStamp::Architecture"
        blocks = "__MCP_TOOL_AUDIT_$RunStamp::Blocks"
        drawing = "__MCP_TOOL_AUDIT_$RunStamp::Drawing"
        editing = "__MCP_TOOL_AUDIT_$RunStamp::Editing"
        delete = "__MCP_TOOL_AUDIT_$RunStamp::Delete"
        purge = "__MCP_TOOL_AUDIT_$RunStamp::Purge"
        exports = "__MCP_TOOL_AUDIT_$RunStamp::Exports"
    }
    blockName = "MCP_AUDIT_BLOCK_$RunStamp"
    snapshotId = ""
    drawingView = ""
}

$Results = New-Object System.Collections.Generic.List[object]
$AllToolNames = @()
$script:Proc = $null
$script:NextId = 1

function Send-JsonRpc($writer, $obj) {
    $json = $obj | ConvertTo-Json -Depth 100 -Compress
    $writer.WriteLine($json)
    $writer.Flush()
}

function Read-JsonRpcLine($reader, [int]$timeoutSeconds, [string]$label) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ($true) {
        $remaining = $deadline - (Get-Date)
        if ($remaining.TotalMilliseconds -le 0) {
            throw "Timed out waiting for $label."
        }

        $task = $reader.ReadLineAsync()
        if (-not $task.Wait($remaining)) {
            throw "Timed out waiting for $label."
        }

        $line = $task.Result
        if ($null -eq $line) {
            throw "Bridge closed stdout while waiting for $label."
        }

        if (-not [string]::IsNullOrWhiteSpace($line)) {
            return ($line | ConvertFrom-Json)
        }
    }
}

function Stop-Bridge {
    if ($script:Proc) {
        try {
            if (-not $script:Proc.HasExited) {
                $script:Proc.StandardInput.Close()
                [void]$script:Proc.WaitForExit(500)
            }
        }
        catch {
        }

        if (-not $script:Proc.HasExited) {
            try {
                $script:Proc.Kill()
            }
            catch {
            }
        }

        $script:Proc = $null
    }
}

function Start-Bridge {
    Stop-Bridge

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $BridgeExe
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $script:Proc = [System.Diagnostics.Process]::Start($psi)
    if (-not $script:Proc) {
        throw "Failed to start bridge."
    }

    $script:NextId = 1
    [void](Invoke-Rpc "initialize" @{
        protocolVersion = "2025-11-25"
        capabilities = @{}
        clientInfo = @{ name = "codex-runtime-tool-audit"; version = "1.0" }
    } 20)
    Send-JsonRpc $script:Proc.StandardInput @{
        jsonrpc = "2.0"
        method = "notifications/initialized"
        params = @{}
    }
}

function Invoke-Rpc([string]$method, $params, [int]$timeoutSeconds = 45) {
    if (-not $script:Proc -or $script:Proc.HasExited) {
        Start-Bridge
    }

    $id = $script:NextId++
    Send-JsonRpc $script:Proc.StandardInput @{
        jsonrpc = "2.0"
        id = $id
        method = $method
        params = $params
    }

    $msg = Read-JsonRpcLine $script:Proc.StandardOutput $timeoutSeconds $method
    if ($msg.PSObject.Properties.Name -contains "error") {
        throw ($msg.error | ConvertTo-Json -Depth 40 -Compress)
    }

    return $msg.result
}

function Invoke-ToolRaw([string]$name, $arguments, [int]$timeoutSeconds = 45) {
    $result = Invoke-Rpc "tools/call" @{ name = $name; arguments = $arguments } $timeoutSeconds
    $textItem = @($result.content | Where-Object { $_.type -eq "text" } | Select-Object -First 1)
    if (-not $textItem) {
        throw "Tool $name returned no text content."
    }

    $rawText = [string]$textItem.text
    $parsed = $null
    try {
        $parsed = $rawText | ConvertFrom-Json
    }
    catch {
        $parsed = $rawText
    }

    return [pscustomobject]@{ raw = $result; text = $rawText; parsed = $parsed }
}

function Has-Prop($obj, [string]$name) {
    return $null -ne $obj -and $obj.PSObject.Properties.Name -contains $name
}

function Get-Prop($obj, [string]$name) {
    if (Has-Prop $obj $name) {
        return $obj.$name
    }
    return $null
}

function Evaluate-Call($call) {
    $parsed = $call.parsed
    $status = "PASS"
    $message = ""

    if (Has-Prop $parsed "message") {
        $message = [string]$parsed.message
    }

    if (Has-Prop $parsed "success") {
        if (-not [bool]$parsed.success) {
            $status = "FAIL"
        }
    }

    $data = Get-Prop $parsed "data"
    foreach ($field in @("failedCount", "failedObjectCount")) {
        $value = Get-Prop $data $field
        if ($null -ne $value -and [int]$value -gt 0) {
            $status = "FAIL"
        }
    }

    if ($data -and (Has-Prop $data "results")) {
        $badResults = @($data.results | Where-Object {
            (Has-Prop $_ "success") -and -not [bool]$_.success
        })
        if ($badResults.Count -gt 0) {
            $status = "FAIL"
        }
    }

    return [pscustomobject]@{ status = $status; message = $message }
}

function Summarize-Call($call) {
    $parsed = $call.parsed
    if ($parsed -is [string]) {
        return $parsed.Substring(0, [Math]::Min(220, $parsed.Length))
    }

    $data = Get-Prop $parsed "data"
    $parts = New-Object System.Collections.Generic.List[string]
    if (Has-Prop $parsed "message") {
        $parts.Add("message=$($parsed.message)")
    }

    foreach ($field in @(
        "requestedCount",
        "createdCount",
        "succeededCount",
        "changedCount",
        "noopCount",
        "failedCount",
        "matchedCount",
        "wouldChangeCount",
        "deletedCount",
        "updatedObjectCount",
        "matchedObjectCount",
        "previewObjectCount",
        "operationCount",
        "definitionCount",
        "instanceCount",
        "targetObjectCount",
        "exportedObjectCount",
        "outputFileSizeBytes",
        "touchedObjectCount"
    )) {
        $value = Get-Prop $data $field
        if ($null -ne $value) {
            $parts.Add("$field=$value")
        }
    }

    if ($data -and (Has-Prop $data "outputPath")) {
        $parts.Add("outputPath=$($data.outputPath)")
    }
    if ($data -and (Has-Prop $data "snapshotId")) {
        $parts.Add("snapshotId=$($data.snapshotId)")
    }
    if ($data -and (Has-Prop $data "viewNames")) {
        $parts.Add("views=$(@($data.viewNames) -join ',')")
    }

    if ($parts.Count -eq 0) {
        $compact = $call.text -replace "\s+", " "
        return $compact.Substring(0, [Math]::Min(220, $compact.Length))
    }

    return ($parts -join "; ")
}

function Add-Result([string]$name, [string]$status, [string]$message, [string]$detail, [int]$durationMs) {
    $Results.Add([pscustomobject]@{
        name = $name
        status = $status
        message = ($message -replace "`r?`n", " ")
        detail = ($detail -replace "`r?`n", " ")
        durationMs = $durationMs
    }) | Out-Null
}

function Run-Test(
    [string]$name,
    [scriptblock]$argBuilder,
    [scriptblock]$after = $null,
    [int]$timeoutSeconds = 45,
    [string]$blockedPattern = ""
) {
    if ($AllToolNames -notcontains $name) {
        Add-Result $name "NOT_REGISTERED" "Tool was not exposed by tools/list." "" 0
        return $null
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $args = & $argBuilder
        $call = Invoke-ToolRaw $name $args $timeoutSeconds
        $eval = Evaluate-Call $call
        $status = $eval.status
        $message = $eval.message
        if ($status -eq "FAIL" -and -not [string]::IsNullOrWhiteSpace($blockedPattern) -and $message -match $blockedPattern) {
            $status = "BLOCKED"
        }

        $detail = Summarize-Call $call
        $sw.Stop()
        Add-Result $name $status $message $detail ([int]$sw.ElapsedMilliseconds)
        if ($status -eq "PASS" -and $after) {
            & $after $call.parsed
        }
        return $call.parsed
    }
    catch {
        $sw.Stop()
        $msg = $_.Exception.Message
        $status = "FAIL"
        if ($msg.StartsWith("BLOCKED:")) {
            $status = "BLOCKED"
            $msg = $msg.Substring(8).Trim()
        }

        Add-Result $name $status $msg "" ([int]$sw.ElapsedMilliseconds)
        Stop-Bridge
        return $null
    }
}

function First-CreatedId($parsed) {
    $data = Get-Prop $parsed "data"
    if ($data -and (Has-Prop $data "createdObjects")) {
        $items = @($data.createdObjects)
        if ($items.Count -gt 0 -and (Has-Prop $items[0] "objectId")) {
            return [string]$items[0].objectId
        }
    }

    if ($data -and (Has-Prop $data "results")) {
        foreach ($item in @($data.results)) {
            if (Has-Prop $item "objectId") {
                return [string]$item.objectId
            }
            if (Has-Prop $item "createdObjectIds") {
                $ids = @($item.createdObjectIds)
                if ($ids.Count -gt 0) {
                    return [string]$ids[0]
                }
            }
            if (Has-Prop $item "resultObjectIds") {
                $ids = @($item.resultObjectIds)
                if ($ids.Count -gt 0) {
                    return [string]$ids[0]
                }
            }
        }
    }

    return $null
}

function Store-FirstId([string]$key, $parsed) {
    $id = First-CreatedId $parsed
    if ($id) {
        $State.ids[$key] = $id
    }
}

function Require-Id([string]$key) {
    if (-not $State.ids.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$State.ids[$key])) {
        $extra = if ($State.fixtureErrors.ContainsKey($key)) { " $($State.fixtureErrors[$key])" } else { "" }
        throw "BLOCKED: missing fixture object id '$key'.$extra"
    }
    return [string]$State.ids[$key]
}

function Add-FixtureId([string]$key, [string]$toolName, $arguments, [int]$timeoutSeconds = 45) {
    try {
        $parsed = (Invoke-ToolRaw $toolName $arguments $timeoutSeconds).parsed
        $id = First-CreatedId $parsed
        if ($id) {
            $State.ids[$key] = $id
        }
        else {
            $State.fixtureErrors[$key] = "Fixture tool $toolName returned no object id."
        }
    }
    catch {
        $State.fixtureErrors[$key] = $_.Exception.Message
        Stop-Bridge
    }
}

function Common([string]$layer, [int]$r = 80, [int]$g = 160, [int]$b = 220) {
    return @{
        layerFullPath = $layer
        color = @{ r = $r; g = $g; b = $b }
        userText = @{ auditRun = $RunId }
    }
}

function Pt([double]$x, [double]$y, [double]$z) {
    return @{ x = $x; y = $y; z = $z }
}

function Escape-Md([string]$s) {
    if ($null -eq $s) {
        return ""
    }
    return (($s -replace "\|", "\|" -replace "`r?`n", " ") -replace "\s+", " ").Trim()
}

try {
    Start-Bridge
    $AllToolNames = @((Invoke-Rpc "tools/list" @{} 20).tools | Select-Object -ExpandProperty name)

    Run-Test "create_layers" { @{ filePath = $DocumentPath; entries = @(
        @{ fullPath = $State.layers.root; color = @{ r=120; g=120; b=120 }; visible = $true; locked = $false },
        @{ fullPath = $State.layers.geometry; color = @{ r=80; g=160; b=220 }; visible = $true; locked = $false },
        @{ fullPath = $State.layers.analysis; color = @{ r=120; g=220; b=120 }; visible = $true; locked = $false },
        @{ fullPath = $State.layers.architecture; color = @{ r=220; g=180; b=80 }; visible = $true; locked = $false },
        @{ fullPath = $State.layers.blocks; color = @{ r=180; g=80; b=220 }; visible = $true; locked = $false },
        @{ fullPath = $State.layers.drawing; color = @{ r=220; g=80; b=80 }; visible = $true; locked = $false },
        @{ fullPath = $State.layers.editing; color = @{ r=80; g=220; b=220 }; visible = $true; locked = $false },
        @{ fullPath = $State.layers.delete; color = @{ r=180; g=180; b=180 }; visible = $true; locked = $false },
        @{ fullPath = $State.layers.purge; color = @{ r=150; g=150; b=150 }; visible = $true; locked = $false },
        @{ fullPath = $State.layers.exports; color = @{ r=120; g=180; b=220 }; visible = $true; locked = $false }
    ) } } | Out-Null
    Run-Test "get_document_summary" { @{ filePath = $DocumentPath; maxObjectSummaries = 5; maxLayerSummaries = 20; maxNamedViews = 10; maxMaterials = 10 } } | Out-Null
    Run-Test "get_layers" { @{ filePath = $DocumentPath } } | Out-Null
    Run-Test "find_layer_candidates" { @{ filePath = $DocumentPath; layerQuery = $State.layers.root; exactMatch = $false } } | Out-Null
    Run-Test "set_current_layer_in_live" { @{ filePath = $DocumentPath; fullPath = $State.layers.geometry; exactMatch = $true } } | Out-Null
    Run-Test "get_current_layer_in_live" { @{ filePath = $DocumentPath } } | Out-Null
    Run-Test "preview_modify_layers" { @{ filePath = $DocumentPath; entries = @(@{ fullPath = $State.layers.geometry; color = @{ r=0; g=128; b=255 } }) } } | Out-Null
    Run-Test "modify_layers" { @{ filePath = $DocumentPath; entries = @(@{ fullPath = $State.layers.geometry; color = @{ r=0; g=128; b=255 } }) } } | Out-Null

    Run-Test "create_points" { @{ filePath = $DocumentPath; items = @(@{ x=0; y=0; z=0 }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "point" $p } | Out-Null
    Run-Test "create_lines" { @{ filePath = $DocumentPath; items = @(@{ startX=0; startY=5; startZ=0; endX=10; endY=5; endZ=0 }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "line" $p } | Out-Null
    Run-Test "create_polylines" { @{ filePath = $DocumentPath; items = @(@{ points = @((Pt 0 10 0),(Pt 5 12 0),(Pt 10 10 0)); closed = $false; name = "audit_polyline" }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "polyline" $p } | Out-Null
    Run-Test "create_circles" { @{ filePath = $DocumentPath; items = @(@{ centerX=20; centerY=0; centerZ=0; radius=2; normalX=0; normalY=0; normalZ=1; name="audit_circle" }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "circle" $p } | Out-Null
    Run-Test "create_arcs" { @{ filePath = $DocumentPath; items = @(@{ mode="ThreePoint"; startX=20; startY=5; startZ=0; midX=22; midY=7; midZ=0; endX=24; endY=5; endZ=0 }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "arc" $p } | Out-Null
    Run-Test "create_ellipses" { @{ filePath = $DocumentPath; items = @(@{ centerX=30; centerY=0; centerZ=0; radiusX=3; radiusY=1.5; normalX=0; normalY=0; normalZ=1; xAxisX=1; xAxisY=0; xAxisZ=0; name="audit_ellipse" }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "ellipse" $p } | Out-Null
    Run-Test "create_nurbs_curves" { @{ filePath = $DocumentPath; items = @(@{ controlPoints = @((Pt 30 5 0),(Pt 33 8 0),(Pt 36 5 0),(Pt 39 7 0)); degree = 3; closed = $false; name="audit_nurbs" }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "nurbs" $p } | Out-Null
    Run-Test "create_surfaces" { @{ filePath = $DocumentPath; items = @(@{ mode="FourCorners"; corner0X=40; corner0Y=0; corner0Z=0; corner1X=48; corner1Y=0; corner1Z=0; corner2X=48; corner2Y=8; corner2Z=0; corner3X=40; corner3Y=8; corner3Z=0 }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "surface" $p } | Out-Null
    Run-Test "create_spheres" { @{ filePath = $DocumentPath; items = @(@{ centerX=60; centerY=0; centerZ=2; radius=2; name="audit_sphere" }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "sphere" $p } | Out-Null
    Run-Test "create_boxes" { @{ filePath = $DocumentPath; items = @(@{ originX=70; originY=0; originZ=0; sizeX=4; sizeY=4; sizeZ=4; representation="Brep"; name="audit_box" }); common = (Common $State.layers.architecture); autoCreateLayers = $false } } { param($p) Store-FirstId "box" $p } | Out-Null
    Run-Test "create_cones" { @{ filePath = $DocumentPath; items = @(@{ baseX=80; baseY=0; baseZ=0; radius=2; height=5; axisX=0; axisY=0; axisZ=1; capBottom=$true; name="audit_cone" }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "cone" $p } | Out-Null
    Run-Test "create_cylinders" { @{ filePath = $DocumentPath; items = @(@{ baseX=90; baseY=0; baseZ=0; radius=2; height=5; axisX=0; axisY=0; axisZ=1; capBottom=$true; capTop=$true; name="audit_cylinder" }); common = (Common $State.layers.geometry) } } { param($p) Store-FirstId "cylinder" $p } | Out-Null
    Run-Test "create_columns" { @{ filePath = $DocumentPath; items = @(@{ centerX=100; centerY=0; centerZ=0; profileShape="Rectangular"; width=2; depth=2; baseElevation=0; height=8; representation="Brep"; name="audit_column" }); common = (Common $State.layers.architecture); autoCreateLayers=$false } } { param($p) Store-FirstId "column" $p } | Out-Null
    Run-Test "create_beams" { @{ filePath = $DocumentPath; items = @(@{ startX=105; startY=0; startZ=8; endX=115; endY=0; endZ=8; profileShape="Rectangular"; width=1; depth=1; name="audit_beam" }); common = (Common $State.layers.architecture); autoCreateLayers=$false } } { param($p) Store-FirstId "beam" $p } | Out-Null
    Run-Test "create_slabs" { @{ filePath = $DocumentPath; items = @(@{ footprint=@((Pt 120 0 0),(Pt 128 0 0),(Pt 128 8 0),(Pt 120 8 0),(Pt 120 0 0)); elevation=0; thickness=0.5; representation="Brep"; name="audit_slab" }); common = (Common $State.layers.architecture); autoCreateLayers=$false } } { param($p) Store-FirstId "slab" $p } | Out-Null
    Run-Test "create_walls" { @{ filePath = $DocumentPath; items = @(@{ baseline=@((Pt 130 0 0),(Pt 140 0 0)); height=6; thickness=0.5; baseElevation=0; alignment="Center"; name="audit_wall" }); common = (Common $State.layers.architecture); autoCreateLayers=$false } } { param($p) Store-FirstId "wall" $p } | Out-Null
    Run-Test "create_extrusions" { @{ filePath = $DocumentPath; items = @(@{ profilePoints=@((Pt 145 0 0),(Pt 149 0 0),(Pt 149 4 0),(Pt 145 4 0),(Pt 145 0 0)); height=5; cap=$true; representation="Extrusion"; name="audit_extrusion" }); common = (Common $State.layers.architecture); autoCreateLayers=$false } } { param($p) Store-FirstId "extrusion" $p } | Out-Null
    Run-Test "create_planar_breps" { @{ filePath = $DocumentPath; items = @(@{ loops=@(@{ points=@((Pt 150 0 0),(Pt 156 0 0),(Pt 156 4 0),(Pt 150 4 0),(Pt 150 0 0)) }); name="audit_planar_brep" }); common = (Common $State.layers.architecture); autoCreateLayers=$false } } { param($p) Store-FirstId "planarBrep" $p } | Out-Null
    Add-FixtureId "analysisLine" "create_lines" @{ filePath=$DocumentPath; items=@(@{startX=200;startY=0;startZ=0;endX=210;endY=0;endZ=0}); common=(Common $State.layers.analysis) }
    Add-FixtureId "analysisPoint" "create_points" @{ filePath=$DocumentPath; items=@(@{x=202;y=2;z=0}); common=(Common $State.layers.analysis) }
    Add-FixtureId "analysisSphere" "create_spheres" @{ filePath=$DocumentPath; items=@(@{centerX=220;centerY=0;centerZ=2;radius=2;name="analysis_sphere"}); common=(Common $State.layers.analysis) }
    Add-FixtureId "analysisSurface" "create_surfaces" @{ filePath=$DocumentPath; items=@(@{mode="FourCorners";corner0X=230;corner0Y=0;corner0Z=0;corner1X=238;corner1Y=0;corner1Z=0;corner2X=238;corner2Y=8;corner2Z=0;corner3X=230;corner3Y=8;corner3Z=0}); common=(Common $State.layers.analysis) }
    Add-FixtureId "intLineA" "create_lines" @{ filePath=$DocumentPath; items=@(@{startX=240;startY=-5;startZ=0;endX=240;endY=5;endZ=0}); common=(Common $State.layers.analysis) }
    Add-FixtureId "intLineB" "create_lines" @{ filePath=$DocumentPath; items=@(@{startX=235;startY=0;startZ=0;endX=245;endY=0;endZ=0}); common=(Common $State.layers.analysis) }
    Add-FixtureId "continuityA" "create_lines" @{ filePath=$DocumentPath; items=@(@{startX=250;startY=0;startZ=0;endX=255;endY=0;endZ=0}); common=(Common $State.layers.analysis) }
    Add-FixtureId "continuityB" "create_lines" @{ filePath=$DocumentPath; items=@(@{startX=255;startY=0;startZ=0;endX=260;endY=0;endZ=0}); common=(Common $State.layers.analysis) }
    Add-FixtureId "profileCircleA" "create_circles" @{ filePath=$DocumentPath; items=@(@{centerX=270;centerY=0;centerZ=0;radius=2;normalX=0;normalY=0;normalZ=1;name="loft_a"}); common=(Common $State.layers.geometry) }
    Add-FixtureId "profileCircleB" "create_circles" @{ filePath=$DocumentPath; items=@(@{centerX=270;centerY=0;centerZ=5;radius=3;normalX=0;normalY=0;normalZ=1;name="loft_b"}); common=(Common $State.layers.geometry) }
    Add-FixtureId "sweepRail" "create_lines" @{ filePath=$DocumentPath; items=@(@{startX=280;startY=0;startZ=0;endX=290;endY=0;endZ=0}); common=(Common $State.layers.geometry) }
    Add-FixtureId "sweepProfile" "create_circles" @{ filePath=$DocumentPath; items=@(@{centerX=280;centerY=0;centerZ=0;radius=1;normalX=1;normalY=0;normalZ=0;name="sweep_profile"}); common=(Common $State.layers.geometry) }
    Add-FixtureId "splitCurve" "create_lines" @{ filePath=$DocumentPath; items=@(@{startX=300;startY=0;startZ=0;endX=310;endY=0;endZ=0}); common=(Common $State.layers.geometry) }
    Add-FixtureId "splitReplaceCurve" "create_lines" @{ filePath=$DocumentPath; items=@(@{startX=315;startY=0;startZ=0;endX=325;endY=0;endZ=0}); common=(Common $State.layers.geometry) }
    Add-FixtureId "projectCurve" "create_circles" @{ filePath=$DocumentPath; items=@(@{centerX=335;centerY=4;centerZ=5;radius=1;normalX=0;normalY=0;normalZ=1;name="project_curve"}); common=(Common $State.layers.geometry) }
    Add-FixtureId "projectSurface" "create_surfaces" @{ filePath=$DocumentPath; items=@(@{mode="FourCorners";corner0X=330;corner0Y=0;corner0Z=0;corner1X=340;corner1Y=0;corner1Z=0;corner2X=340;corner2Y=8;corner2Z=0;corner3X=330;corner3Y=8;corner3Z=0}); common=(Common $State.layers.geometry) }
    Add-FixtureId "editCurve" "create_nurbs_curves" @{ filePath=$DocumentPath; items=@(@{controlPoints=@((Pt 350 0 0),(Pt 353 3 0),(Pt 356 0 0),(Pt 359 2 0));degree=3;closed=$false;name="edit_curve"}); common=(Common $State.layers.editing) }
    Add-FixtureId "editCurve2" "create_nurbs_curves" @{ filePath=$DocumentPath; items=@(@{controlPoints=@((Pt 360 0 0),(Pt 363 3 0),(Pt 366 0 0),(Pt 369 2 0));degree=3;closed=$false;name="edit_curve_2"}); common=(Common $State.layers.editing) }
    Add-FixtureId "editSurface" "create_surfaces" @{ filePath=$DocumentPath; items=@(@{mode="FourCorners";corner0X=370;corner0Y=0;corner0Z=0;corner1X=378;corner1Y=0;corner1Z=0;corner2X=378;corner2Y=8;corner2Z=0;corner3X=370;corner3Y=8;corner3Z=0}); common=(Common $State.layers.editing) }
    Add-FixtureId "rebuildSurface" "create_surfaces" @{ filePath=$DocumentPath; items=@(@{mode="FourCorners";corner0X=380;corner0Y=0;corner0Z=0;corner1X=388;corner1Y=0;corner1Z=0;corner2X=388;corner2Y=0;corner2Z=8;corner3X=380;corner3Y=0;corner3Z=8}); common=(Common $State.layers.editing) }
    Add-FixtureId "tweakSurface" "create_surfaces" @{ filePath=$DocumentPath; items=@(@{mode="FourCorners";corner0X=390;corner0Y=0;corner0Z=0;corner1X=398;corner1Y=0;corner1Z=0;corner2X=398;corner2Y=8;corner2Z=0;corner3X=390;corner3Y=8;corner3Z=0}); common=(Common $State.layers.editing) }
    Add-FixtureId "flipSurface" "create_surfaces" @{ filePath=$DocumentPath; items=@(@{mode="FourCorners";corner0X=400;corner0Y=0;corner0Z=0;corner1X=408;corner1Y=0;corner1Z=0;corner2X=408;corner2Y=8;corner2Z=0;corner3X=400;corner3Y=8;corner3Z=0}); common=(Common $State.layers.editing) }
    Add-FixtureId "replaceTarget" "create_points" @{ filePath=$DocumentPath; items=@(@{x=410;y=0;z=0}); common=(Common $State.layers.editing) }
    Add-FixtureId "editingPoint" "create_points" @{ filePath=$DocumentPath; items=@(@{x=420;y=0;z=0}); common=(Common $State.layers.editing) }
    Add-FixtureId "blockSource" "create_boxes" @{ filePath=$DocumentPath; items=@(@{originX=430;originY=0;originZ=0;sizeX=2;sizeY=2;sizeZ=2;representation="Brep";name="block_source"}); common=(Common $State.layers.blocks); autoCreateLayers=$false }
    Add-FixtureId "boolTarget" "create_boxes" @{ filePath=$DocumentPath; items=@(@{originX=440;originY=0;originZ=0;sizeX=5;sizeY=5;sizeZ=5;representation="Brep";name="bool_target"}); common=(Common $State.layers.architecture); autoCreateLayers=$false }
    Add-FixtureId "boolCutter" "create_boxes" @{ filePath=$DocumentPath; items=@(@{originX=442;originY=1;originZ=1;sizeX=3;sizeY=3;sizeZ=3;representation="Brep";name="bool_cutter"}); common=(Common $State.layers.architecture); autoCreateLayers=$false }
    Add-FixtureId "openingWall" "create_walls" @{ filePath=$DocumentPath; items=@(@{baseline=@((Pt 455 0 0),(Pt 465 0 0));height=6;thickness=0.5;baseElevation=0;alignment="Center";name="opening_wall"}); common=(Common $State.layers.architecture); autoCreateLayers=$false }
    Add-FixtureId "drawingBox" "create_boxes" @{ filePath=$DocumentPath; items=@(@{originX=470;originY=0;originZ=0;sizeX=4;sizeY=4;sizeZ=4;representation="Brep";name="drawing_box"}); common=(Common $State.layers.drawing); autoCreateLayers=$false }
    Add-FixtureId "deletePoint" "create_points" @{ filePath=$DocumentPath; items=@(@{x=480;y=0;z=0}); common=(Common $State.layers.delete) }

    Run-Test "filter_objects" { @{ filePath=$DocumentPath; confirmedLayerFullPaths=@($State.layers.geometry); objectTypes=@(); matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "resolve_object_ids_in_live" { @{ filePath=$DocumentPath; objectIds=@((Require-Id "point"),(Require-Id "line")) } } | Out-Null
    Run-Test "get_object_metrics_in_live" { @{ filePath=$DocumentPath; objectIds=@((Require-Id "point"),(Require-Id "line"),(Require-Id "sphere")) } } | Out-Null
    Run-Test "get_object_metrics_by_filter" { @{ filePath=$DocumentPath; confirmedLayerFullPaths=@($State.layers.analysis); matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "get_mass_properties_in_live" { @{ filePath=$DocumentPath; objectIds=@((Require-Id "analysisSphere")); kind="Volume" } } | Out-Null
    Run-Test "get_mass_properties_by_filter" { @{ filePath=$DocumentPath; kind="Auto"; confirmedLayerFullPaths=@($State.layers.analysis); objectTypes=@("Brep"); matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "get_geometry_frames_in_live" { @{ filePath=$DocumentPath; entries=@(@{entryId="lineStart";objectId=(Require-Id "analysisLine");kind="CurveStart"}) } } | Out-Null
    Run-Test "get_geometry_frames_by_filter" { @{ filePath=$DocumentPath; kind="CurveStart"; confirmedLayerFullPaths=@($State.layers.analysis); objectTypes=@("Curve"); entryIdPrefix="audit_frame"; matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "get_curvature_samples_in_live" { @{ filePath=$DocumentPath; entries=@(@{entryId="circleCurvature";objectId=(Require-Id "circle");mode="EvenByCount";sampleCount=4}) } } | Out-Null
    Run-Test "get_curvature_samples_by_filter" { @{ filePath=$DocumentPath; mode="EvenByCount"; sampleCount=3; confirmedLayerFullPaths=@($State.layers.geometry); objectTypes=@("Curve"); entryIdPrefix="audit_curv"; matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "get_contour_curves_in_live" { @{ filePath=$DocumentPath; entries=@(@{entryId="sphereContour";objectId=(Require-Id "analysisSphere");startX=220;startY=0;startZ=-5;endX=220;endY=0;endZ=8;interval=1}) } } | Out-Null
    Run-Test "get_contour_curves_by_filter" { @{ filePath=$DocumentPath; startX=220;startY=0;startZ=-5;endX=220;endY=0;endZ=8;interval=1;confirmedLayerFullPaths=@($State.layers.analysis);objectTypes=@("Brep");entryIdPrefix="audit_contour";matchMode="All";userAttributeMatchMode="All" } } | Out-Null
    Run-Test "intersect_objects_in_live" { @{ filePath=$DocumentPath; entries=@(@{entryId="curveCurve";kind="CurveCurve";first=@{objectId=(Require-Id "intLineA")};second=@{objectId=(Require-Id "intLineB")};tolerance=0.01}) } } | Out-Null
    Run-Test "get_closest_points_in_live" { @{ filePath=$DocumentPath; entries=@(@{entryId="pointToLine";targetKind="Auto";source=@{objectId=(Require-Id "analysisPoint")};target=@{objectId=(Require-Id "analysisLine")}}) } } | Out-Null
    Run-Test "measure_distances_in_live" { @{ filePath=$DocumentPath; entries=@(@{entryId="pointPointDistance";from=@{geometry=@{primitive="Point";x=0;y=0;z=0}};to=@{geometry=@{primitive="Point";x=3;y=4;z=0}};tolerance=0.01}) } } | Out-Null
    Run-Test "measure_angles_in_live" { @{ filePath=$DocumentPath; entries=@(@{entryId="vectorAngle";mode="TwoVectors";firstVector=@{x=1;y=0;z=0};secondVector=@{x=0;y=1;z=0}}) } } | Out-Null
    Run-Test "check_continuity_in_live" { @{ filePath=$DocumentPath; entries=@(@{entryId="g0Lines";firstObjectId=(Require-Id "continuityA");secondObjectId=(Require-Id "continuityB");targetKind="G0";positionTolerance=0.01;angleToleranceRadians=0.1;curvatureToleranceRatio=0.1}) } } | Out-Null
    Run-Test "select_objects_in_live" { @{ filePath=$DocumentPath; objectIds=@((Require-Id "point"),(Require-Id "line")); selectionMode="Replace" } } | Out-Null
    Run-Test "get_selected_objects_in_live" { @{ filePath=$DocumentPath } } | Out-Null

    Run-Test "create_curve_offsets" { @{ filePath=$DocumentPath; entries=@(@{curveObjectId=(Require-Id "circle");distance=1;planeOriginX=20;planeOriginY=0;planeOriginZ=0;planeNormalX=0;planeNormalY=0;planeNormalZ=1;cornerStyle="Sharp";name="audit_offset"}); common=(Common $State.layers.geometry 255 160 80) } } { param($p) Store-FirstId "offsetCurve" $p } | Out-Null
    Run-Test "create_curve_extrusions" { @{ filePath=$DocumentPath; entries=@(@{curveObjectId=(Require-Id "circle");vectorX=0;vectorY=0;vectorZ=4;cap=$true;name="audit_curve_extrusion"}); common=(Common $State.layers.geometry 255 160 80) } } { param($p) Store-FirstId "curveExtrusion" $p } | Out-Null
    Run-Test "create_pipes" { @{ filePath=$DocumentPath; entries=@(@{curveObjectId=(Require-Id "analysisLine");radius=0.25;capStyle="Flat";localBlending=$false;fitRail=$true;name="audit_pipe"}); common=(Common $State.layers.geometry 255 160 80) } } { param($p) Store-FirstId "pipe" $p } | Out-Null
    Run-Test "create_lofts" { @{ filePath=$DocumentPath; entries=@(@{curveObjectIds=@((Require-Id "profileCircleA"),(Require-Id "profileCircleB"));loftStyle="Normal";closed=$false;name="audit_loft"}); common=(Common $State.layers.geometry 255 160 80) } } { param($p) Store-FirstId "loft" $p } | Out-Null
    Run-Test "create_sweep_one_rail" { @{ filePath=$DocumentPath; entries=@(@{railCurveObjectId=(Require-Id "sweepRail");profileCurveObjectIds=@((Require-Id "sweepProfile"));name="audit_sweep"}); common=(Common $State.layers.geometry 255 160 80) } } { param($p) Store-FirstId "sweep" $p } 60 | Out-Null
    Run-Test "project_curves" { @{ filePath=$DocumentPath; entries=@(@{curveObjectId=(Require-Id "projectCurve");targetObjectIds=@((Require-Id "projectSurface"));directionX=0;directionY=0;directionZ=-1;name="audit_project"}); common=(Common $State.layers.geometry 255 160 80) } } { param($p) Store-FirstId "projectedCurve" $p } | Out-Null
    Run-Test "preview_split_curves" { @{ filePath=$DocumentPath; entries=@(@{curveObjectId=(Require-Id "splitCurve");points=@(@{x=305;y=0;z=0});pointTolerance=0.1;name="audit_split_preview"}) } } | Out-Null
    Run-Test "create_split_curve_segments" { @{ filePath=$DocumentPath; entries=@(@{curveObjectId=(Require-Id "splitCurve");points=@(@{x=305;y=0;z=0});pointTolerance=0.1;name="audit_split_create"}); common=(Common $State.layers.geometry 255 160 80) } } { param($p) Store-FirstId "splitSegment" $p } | Out-Null
    Run-Test "replace_split_curves" { @{ filePath=$DocumentPath; entries=@(@{curveObjectId=(Require-Id "splitReplaceCurve");points=@(@{x=320;y=0;z=0});pointTolerance=0.1;name="audit_split_replace"}); common=(Common $State.layers.geometry 255 160 80) } } { param($p) Store-FirstId "replaceSplitSegment" $p } | Out-Null

    Run-Test "set_document_user_strings" { @{ filePath=$DocumentPath; entries=@(@{section="MCP_Audit";key="RunId";value=$RunId}) } } | Out-Null
    Run-Test "get_document_user_strings" { @{ filePath=$DocumentPath } } | Out-Null
    Run-Test "delete_document_user_strings" { @{ filePath=$DocumentPath; entries=@(@{section="MCP_Audit";key="RunId"}) } } | Out-Null
    Run-Test "preview_object_user_text_writes" { @{ filePath=$DocumentPath; entries=@(@{objectId=(Require-Id "editingPoint");key="audit_key";value="preview_value"}) } } | Out-Null
    Run-Test "apply_object_user_text_writes" { @{ filePath=$DocumentPath; entries=@(@{objectId=(Require-Id "editingPoint");key="audit_key";value="applied_value"}) } } | Out-Null
    Run-Test "get_object_user_strings" { @{ filePath=$DocumentPath; objectIds=@((Require-Id "editingPoint")) } } | Out-Null
    Run-Test "delete_object_user_text" { @{ filePath=$DocumentPath; entries=@(@{objectId=(Require-Id "editingPoint");key="audit_key"}) } } | Out-Null
    Run-Test "preview_object_edits" { @{ filePath=$DocumentPath; confirmedLayerFullPaths=@($State.layers.editing); objectTypes=@("Point"); operations=@(@{operationType="SetDisplayColor";color=@{r=255;g=0;b=255}}); matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "apply_object_edits" { @{ filePath=$DocumentPath; confirmedLayerFullPaths=@($State.layers.editing); objectTypes=@("Point"); operations=@(@{operationType="SetUserText";key="object_edit_audit";value=$RunId}); matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "preview_bulk_object_attribute_recipe" { @{ filePath=$DocumentPath; confirmedLayerFullPaths=@($State.layers.editing); objectTypes=@("Point"); userTextWrites=@(@{key="recipe_preview";valueTemplate="preview_{index}"}); displayColor=@{r=100;g=50;b=200}; matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "apply_bulk_object_attribute_recipe" { @{ filePath=$DocumentPath; confirmedLayerFullPaths=@($State.layers.editing); objectTypes=@("Point"); userTextWrites=@(@{key="recipe_apply";valueTemplate="apply_{index}"}); displayColor=@{r=50;g=200;b=120}; objectNameTemplate="audit_{index}"; matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "preview_transform_objects" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "editingPoint")); transform=@{kind="Translate";vectorX=1;vectorY=0;vectorZ=0}; matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "transform_objects" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "editingPoint")); transform=@{kind="Translate";vectorX=1;vectorY=0;vectorZ=0}; matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "preview_replace_geometry" { @{ filePath=$DocumentPath; entries=@(@{objectId=(Require-Id "replaceTarget");geometry=@{primitive="Point";x=411;y=1;z=0}}) } } | Out-Null
    Run-Test "replace_geometry" { @{ filePath=$DocumentPath; entries=@(@{objectId=(Require-Id "replaceTarget");geometry=@{primitive="Point";x=412;y=1;z=0}}) } } | Out-Null
    Run-Test "get_editable_geometry_descriptor" { @{ filePath=$DocumentPath; objectId=(Require-Id "editCurve"); detail="Full" } } | Out-Null
    Run-Test "preview_edit_control_points" { @{ filePath=$DocumentPath; entries=@(@{objectId=(Require-Id "editCurve");targetMode="CurveIndex";pointIndex=1;x=353;y=4;z=0}) } } | Out-Null
    Run-Test "edit_control_points" { @{ filePath=$DocumentPath; entries=@(@{objectId=(Require-Id "editCurve");targetMode="CurveIndex";pointIndex=1;x=353;y=4;z=0}) } } | Out-Null
    $curveEditSpec = @{ operation=@{kind="DerivedOperation";derivedKind="TranslateByVector";derivedParameters=@{vectorX=0.5;vectorY=0;vectorZ=0}}; pointSelector=@{kind="All"}; points=@() }
    Run-Test "preview_edit_curve_geometry" { @{ filePath=$DocumentPath; objectId=(Require-Id "editCurve2"); editSpec=$curveEditSpec; expectedStrategy="ExactTransform" } } | Out-Null
    Run-Test "apply_edit_curve_geometry" { @{ filePath=$DocumentPath; objectId=(Require-Id "editCurve2"); editSpec=$curveEditSpec; expectedStrategy="ExactTransform" } } | Out-Null
    $surfaceEditSpec = @{ operation=@{kind="DerivedOperation";derivedKind="TranslateByVector";derivedParameters=@{vectorX=0;vectorY=0.5;vectorZ=0}}; pointSelector=@{kind="All"} }
    Run-Test "preview_edit_surface_geometry" { @{ filePath=$DocumentPath; objectId=(Require-Id "editSurface"); editSpec=$surfaceEditSpec; expectedStrategy="ExactTransform" } } | Out-Null
    Run-Test "apply_edit_surface_geometry" { @{ filePath=$DocumentPath; objectId=(Require-Id "editSurface"); editSpec=$surfaceEditSpec; expectedStrategy="ExactTransform" } } | Out-Null
    Run-Test "inspect_surface_rebuild_descriptor" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "rebuildSurface")) } } | Out-Null
    Run-Test "preview_redefine_surface_point_order" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "rebuildSurface")); spec=@{guideMode="Auto";direction="Clockwise";startAnchorMode="ClosestToOrigin"} } } | Out-Null
    Run-Test "apply_redefine_surface_point_order" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "rebuildSurface")); spec=@{guideMode="Auto";direction="Clockwise";startAnchorMode="ClosestToOrigin"}; replaceOriginal=$true } } | Out-Null
    Run-Test "preview_tweak_surface_directions" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "tweakSurface")); operations=@("SwapUV") } } | Out-Null
    Run-Test "apply_tweak_surface_directions" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "tweakSurface")); operations=@("SwapUV") } } | Out-Null
    Run-Test "apply_flip_surface_front_back" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "flipSurface")) } } | Out-Null
    Run-Test "apply_standard_four_point_surface_rebuild" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "surface")) } } | Out-Null

    Run-Test "preview_boolean_objects" { @{ filePath=$DocumentPath; entries=@(@{operation="Difference";targetObjectIds=@((Require-Id "boolTarget"));cutterObjectIds=@((Require-Id "boolCutter"));deleteTargets=$true;deleteCutters=$false}); tolerance=0.01 } } | Out-Null
    Run-Test "apply_boolean_objects" { @{ filePath=$DocumentPath; entries=@(@{operation="Difference";targetObjectIds=@((Require-Id "boolTarget"));cutterObjectIds=@((Require-Id "boolCutter"));deleteTargets=$true;deleteCutters=$false}); tolerance=0.01 } } { param($p) Store-FirstId "booleanResult" $p } | Out-Null
    Run-Test "preview_openings" { @{ filePath=$DocumentPath; items=@(@{targetObjectId=(Require-Id "openingWall");kind="Rectangular";centerX=460;centerY=0;centerZ=3;width=2;height=2;depth=1;retainCutter=$false}); tolerance=0.01 } } | Out-Null
    Run-Test "apply_openings" { @{ filePath=$DocumentPath; items=@(@{targetObjectId=(Require-Id "openingWall");kind="Rectangular";centerX=460;centerY=0;centerZ=3;width=2;height=2;depth=1;retainCutter=$false}); common=(Common $State.layers.architecture); metadata=@{category="Audit";levelName="";systemName="ToolAudit";sourceTag=$RunId}; autoCreateLayers=$false; tolerance=0.01 } } | Out-Null

    Run-Test "preview_create_block_definitions" { @{ filePath=$DocumentPath; items=@(@{name=$State.blockName;description="runtime audit block";sourceObjectIds=@((Require-Id "blockSource"));basePoint=@{x=430;y=0;z=0};sourceObjectPolicy="KeepVisible";duplicateDefinitionPolicy="VersionedName"}) } } | Out-Null
    Run-Test "apply_create_block_definitions" { @{ filePath=$DocumentPath; items=@(@{name=$State.blockName;description="runtime audit block";sourceObjectIds=@((Require-Id "blockSource"));basePoint=@{x=430;y=0;z=0};sourceObjectPolicy="KeepVisible";duplicateDefinitionPolicy="VersionedName"}) } } | Out-Null
    Run-Test "list_block_definitions" { @{ filePath=$DocumentPath; includeLinked=$true; includeNestedSummary=$true } } | Out-Null
    Run-Test "get_block_definition_details" { @{ filePath=$DocumentPath; definitionName=$State.blockName; includeNestedSummary=$true } } | Out-Null
    Run-Test "preview_insert_block_instances" { @{ filePath=$DocumentPath; items=@(@{definitionName=$State.blockName;origin=@{x=435;y=0;z=0};rotationDegrees=0;scale=1}); common=(Common $State.layers.blocks); autoCreateLayers=$false } } | Out-Null
    Run-Test "apply_insert_block_instances" { @{ filePath=$DocumentPath; items=@(@{definitionName=$State.blockName;origin=@{x=435;y=0;z=0};rotationDegrees=0;scale=1}); common=(Common $State.layers.blocks); autoCreateLayers=$false } } { param($p) Store-FirstId "blockInstance" $p } | Out-Null
    Run-Test "list_block_instances" { @{ filePath=$DocumentPath; definitionName=$State.blockName; includeHidden=$true } } | Out-Null
    Run-Test "get_block_instance_details" { @{ filePath=$DocumentPath; objectId=(Require-Id "blockInstance") } } | Out-Null
    Run-Test "preview_transform_block_instances" { @{ filePath=$DocumentPath; items=@(@{objectId=(Require-Id "blockInstance");translationX=1;translationY=0;translationZ=0;rotationDegrees=5;scale=1}) } } | Out-Null
    Run-Test "apply_transform_block_instances" { @{ filePath=$DocumentPath; items=@(@{objectId=(Require-Id "blockInstance");translationX=1;translationY=0;translationZ=0;rotationDegrees=5;scale=1}) } } | Out-Null
    Run-Test "preview_explode_block_instances" { @{ filePath=$DocumentPath; items=@(@{objectId=(Require-Id "blockInstance");explodeNestedInstances=$true;skipHiddenPieces=$true}) } } | Out-Null
    Run-Test "apply_explode_block_instances" { @{ filePath=$DocumentPath; items=@(@{objectId=(Require-Id "blockInstance");explodeNestedInstances=$true;skipHiddenPieces=$true}) } } | Out-Null
    Run-Test "preview_purge_unused_block_definitions" { @{ filePath=$DocumentPath; definitionNames=@($State.blockName); includeAllUnused=$false; policy="UnusedLocalOnly" } } | Out-Null
    Run-Test "apply_purge_unused_block_definitions" { @{ filePath=$DocumentPath; definitionNames=@($State.blockName); includeAllUnused=$false; policy="UnusedLocalOnly" } } | Out-Null

    Run-Test "capture_viewport_image" { @{ filePath=$DocumentPath; imageSizePx=@{width=320;height=240}; backgroundTransparent=$false } } $null 60 | Out-Null
    Run-Test "setup_drawing_views" { @{ filePath=$DocumentPath; confirmedLayerFullPaths=@($State.layers.drawing); preset="Standard8"; fitMarginPercent=5 } } { param($p) $d=Get-Prop $p "data"; if($d -and (Has-Prop $d "viewNames")){ $views=@($d.viewNames); if($views.Count -gt 0){$State.drawingView=[string]$views[0]} } } 60 | Out-Null
    Run-Test "capture_drawing_export_state" { @{ filePath=$DocumentPath; confirmedLayerFullPaths=@($State.layers.drawing); matchMode="All"; userAttributeMatchMode="All" } } { param($p) $d=Get-Prop $p "data"; if($d -and (Has-Prop $d "snapshotId")){ $State.snapshotId=[string]$d.snapshotId } } | Out-Null
    Run-Test "apply_drawing_export_style" { if([string]::IsNullOrWhiteSpace($State.snapshotId)){throw "BLOCKED: missing drawing snapshot id."}; @{ filePath=$DocumentPath; snapshotId=$State.snapshotId; objectColor=@{r=20;g=20;b=20} } } | Out-Null
    Run-Test "set_drawing_export_background" { if([string]::IsNullOrWhiteSpace($State.snapshotId)){throw "BLOCKED: missing drawing snapshot id."}; @{ filePath=$DocumentPath; snapshotId=$State.snapshotId; color=@{r=255;g=255;b=255} } } | Out-Null
    Run-Test "export_drawing_package" { @{ filePath=$DocumentPath; outputDirectory=$DrawingPackageDir; confirmedLayerFullPaths=@($State.layers.drawing); exportPdf=$true; exportJpg=$true; imageSizePx=@{width=320;height=240}; pageSizeMm=@{widthMm=100;heightMm=100}; dotsPerInch=96; overwriteExisting=$true; preset="Standard8"; fitMarginPercent=5; matchMode="All"; userAttributeMatchMode="All" } } $null 120 | Out-Null
    Run-Test "restore_drawing_export_state" { if([string]::IsNullOrWhiteSpace($State.snapshotId)){throw "BLOCKED: missing drawing snapshot id."}; @{ filePath=$DocumentPath; snapshotId=$State.snapshotId } } | Out-Null
    $viewNameForExport = if([string]::IsNullOrWhiteSpace($State.drawingView)){""}else{$State.drawingView}
    Run-Test "export_to_image" { @{ filePath=$DocumentPath; outputPath=(Join-Path $ExportDir "audit_view.png"); viewName=$viewNameForExport; imageSizePx=@{width=320;height=240}; dotsPerInch=96; backgroundTransparent=$false; overwriteExisting=$true } } $null 90 | Out-Null
    Run-Test "export_to_pdf" { @{ filePath=$DocumentPath; outputPath=(Join-Path $ExportDir "audit_view.pdf"); viewName=$viewNameForExport; pageSizeMm=@{widthMm=100;heightMm=100}; dotsPerInch=96; overwriteExisting=$true } } $null 90 | Out-Null
    $exportIds = @((Require-Id "box"),(Require-Id "sphere"),(Require-Id "line"))
    Run-Test "export_to_dwg" { @{ filePath=$DocumentPath; outputPath=(Join-Path $ExportDir "audit_selected.dwg"); selectedObjectIds=$exportIds; overwriteExisting=$true; formatOptions=@{} } } $null 120 | Out-Null
    Run-Test "export_to_dxf" { @{ filePath=$DocumentPath; outputPath=(Join-Path $ExportDir "audit_selected.dxf"); selectedObjectIds=$exportIds; overwriteExisting=$true; formatOptions=@{} } } $null 120 | Out-Null
    Run-Test "export_to_stl" { @{ filePath=$DocumentPath; outputPath=(Join-Path $ExportDir "audit_selected.stl"); selectedObjectIds=@((Require-Id "box"),(Require-Id "sphere")); overwriteExisting=$true; formatOptions=@{} } } $null 120 | Out-Null
    Run-Test "export_to_ifc" { @{ filePath=$DocumentPath; outputPath=(Join-Path $ExportDir "audit_selected.ifc"); selectedObjectIds=@((Require-Id "box"),(Require-Id "wall"),(Require-Id "slab")); overwriteExisting=$true; formatOptions=@{} } } $null 120 | Out-Null

    Run-Test "search_rhino_reference" { @{ query="AddLine"; limit=5 } } | Out-Null
    Run-Test "list_rhino_reference_modules" { @{ query="Geometry"; limit=10 } } | Out-Null
    Run-Test "get_rhino_reference_function" { @{ functionName="RhinoDoc.Objects.Add" } } | Out-Null
    Run-Test "get_mcp_rhino_tool_help" { @{ toolName="create_lines"; limit=10 } } | Out-Null
    Run-Test "list_worksession_attachments" { @{ filePath=$DocumentPath } } | Out-Null
    Run-Test "update_linked_block" { @{ filePath=$DocumentPath; definitionNames=@($State.blockName) } } $null 45 "(linked|not linked|No linked|not a linked|failed|not found)" | Out-Null

    $deleteLayerPath = "$($State.layers.delete)::EmptyDeleteLayer"
    $purgeLayerPath = "$($State.layers.purge)::EmptyPurgeLayer"
    try {
        [void](Invoke-ToolRaw "create_layers" @{
            filePath=$DocumentPath
            entries=@(
                @{fullPath=$deleteLayerPath;color=@{r=200;g=100;b=100};visible=$true;locked=$false},
                @{fullPath=$purgeLayerPath;color=@{r=100;g=100;b=200};visible=$true;locked=$false}
            )
        })
    }
    catch {
        Stop-Bridge
    }

    Run-Test "preview_delete_objects" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "deletePoint")); matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "delete_objects" { @{ filePath=$DocumentPath; confirmedObjectIds=@((Require-Id "deletePoint")); matchMode="All"; userAttributeMatchMode="All" } } | Out-Null
    Run-Test "preview_delete_layers" { @{ filePath=$DocumentPath; fullPaths=@($deleteLayerPath) } } | Out-Null
    Run-Test "delete_layers" { @{ filePath=$DocumentPath; fullPaths=@($deleteLayerPath) } } | Out-Null
    Run-Test "preview_purge_layers" { @{ filePath=$DocumentPath; fullPaths=@($purgeLayerPath) } } | Out-Null
    Run-Test "purge_layers" { @{ filePath=$DocumentPath; fullPaths=@($purgeLayerPath) } } | Out-Null

    Run-Test "append_activity_log" { @{ logRoot=(Join-Path $RepoRoot "Runtime_Log"); task="audit every registered MCP Rhino tool against live test document"; tools=$AllToolNames } } | Out-Null

    $tested = @($Results | Select-Object -ExpandProperty name)
    foreach ($toolName in $AllToolNames | Sort-Object) {
        if ($tested -notcontains $toolName) {
            Add-Result $toolName "NOT_RUN" "No audit case reached this registered tool." "" 0
        }
    }
}
finally {
    Stop-Bridge
}

$orderedResults = @($Results | Sort-Object name)
$summary = $orderedResults | Group-Object status | Sort-Object Name
$passCount = @($orderedResults | Where-Object status -eq "PASS").Count
$failCount = @($orderedResults | Where-Object status -eq "FAIL").Count
$blockedCount = @($orderedResults | Where-Object status -eq "BLOCKED").Count
$notRunCount = @($orderedResults | Where-Object { $_.status -in @("NOT_RUN", "NOT_REGISTERED") }).Count

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Runtime MCP Tool Audit - Live Rhino") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("- Date: 2026-05-08") | Out-Null
$lines.Add("- Run id: ``$RunId``") | Out-Null
$lines.Add("- Document: ``$DocumentPath``") | Out-Null
$lines.Add("- Bridge: ``$BridgeExe``") | Out-Null
$lines.Add("- Export scratch directory: ``$ExportDir``") | Out-Null
$lines.Add("- Registered MCP tools: $($AllToolNames.Count)") | Out-Null
$lines.Add("- Report path: ``$ReportPath``") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Method") | Out-Null
$lines.Add("") | Out-Null
$lines.Add('The audit enumerated `tools/list` from the running bridge, created disposable layers and fixture geometry in the active Rhino document, then called every registered tool with concrete arguments. A tool is marked PASS when the JSON-RPC call returned and the tool response did not report `success=false`, failed entries, or failed result rows. Fixture-dependent tools are marked BLOCKED when the call reached the tool but the active document lacks an external prerequisite such as a linked block definition.') | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Summary") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("- PASS: $passCount") | Out-Null
$lines.Add("- FAIL: $failCount") | Out-Null
$lines.Add("- BLOCKED: $blockedCount") | Out-Null
$lines.Add("- NOT_RUN / NOT_REGISTERED: $notRunCount") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Failures And Blockers") | Out-Null
$lines.Add("") | Out-Null
$problemRows = @($orderedResults | Where-Object { $_.status -ne "PASS" })
if ($problemRows.Count -eq 0) {
    $lines.Add("No non-PASS results were recorded.") | Out-Null
}
else {
    $lines.Add("| Tool | Status | Message | Detail |") | Out-Null
    $lines.Add("|---|---:|---|---|") | Out-Null
    foreach ($r in $problemRows) {
        $lines.Add("| ``$(Escape-Md $r.name)`` | $(Escape-Md $r.status) | $(Escape-Md $r.message) | $(Escape-Md $r.detail) |") | Out-Null
    }
}
$lines.Add("") | Out-Null
$lines.Add("## Full Tool Results") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("| Tool | Status | Duration ms | Message / Counts |") | Out-Null
$lines.Add("|---|---:|---:|---|") | Out-Null
foreach ($r in $orderedResults) {
    $msg = if ([string]::IsNullOrWhiteSpace($r.detail)) { $r.message } else { $r.detail }
    $lines.Add("| ``$(Escape-Md $r.name)`` | $(Escape-Md $r.status) | $($r.durationMs) | $(Escape-Md $msg) |") | Out-Null
}
$lines.Add("") | Out-Null
$lines.Add("## Notes") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("- The active Rhino document was intentionally modified with disposable audit layers, geometry, block definitions, user text, selection state, named views, drawing style state, and export operations.") | Out-Null
$lines.Add("- Export outputs were written to the scratch directory listed above, not to the project tree.") | Out-Null
$lines.Add('- This is a runtime audit report, not a construction PLAN. It is stored in `Project_Plan` only because the user requested that destination.') | Out-Null

[System.IO.File]::WriteAllLines($ReportPath, $lines, [System.Text.UTF8Encoding]::new($false))
[pscustomobject]@{
    reportPath = $ReportPath
    registered = $AllToolNames.Count
    pass = $passCount
    fail = $failCount
    blocked = $blockedCount
    notRun = $notRunCount
    exportDir = $ExportDir
} | ConvertTo-Json -Depth 10
