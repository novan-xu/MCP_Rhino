$ErrorActionPreference = 'Stop'
$env:PATH = 'C:\Program Files\Rhino 8\System;' + $env:PATH
$pcpointDll = Join-Path $PSScriptRoot 'bin\Debug\net8.0-windows\PanelCladdingPointOrderSmoke.dll'
$pcpointLog = Join-Path $PSScriptRoot 'native-host.log'
$pcpointProcess = Start-Process -FilePath 'C:\Program Files\dotnet\dotnet.exe' -ArgumentList @(('"' + $pcpointDll + '"'), '--native') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $pcpointLog -RedirectStandardError (Join-Path $PSScriptRoot 'native-host-error.log')
[ordered]@{ exitCode = $pcpointProcess.ExitCode; hostProcessId = $PID; finishedUtc = [DateTime]::UtcNow.ToString('O') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'native-host-result.log') -Encoding utf8
