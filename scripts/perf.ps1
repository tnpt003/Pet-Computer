# Samples CPU, memory, handles and threads of the pet and its Python service once a second.
# Attaches to the running pet (read only) or, if none runs, starts the published pet with
# separate data and closes it afterwards. Do the activity to measure while it runs
# (idle, sleep, fast drags, laser...) and pass -Label to name the run.
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\perf.ps1 -Minutes 5 -Label idle
param([double]$Minutes = 3, [string]$Label = 'run', [double]$Interval = 1)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $projectRoot 'artifacts\win-x64'
$exe = Join-Path $output 'DesktopPet.exe'
$cores = [Environment]::ProcessorCount

$pet = Get-Process DesktopPet -ErrorAction SilentlyContinue | Select-Object -First 1
$started = $false
$savedData = $env:PET_DATA_DIRECTORY
if (-not $pet) {
    if (-not (Test-Path $exe)) { throw 'No pet is running and artifacts\win-x64 is missing: run scripts/publish.ps1 first.' }
    # Keep the measured pet's preferences and memory apart from the user's cat.
    $env:PET_DATA_DIRECTORY = Join-Path $projectRoot ('artifacts\perf-data\' + [Guid]::NewGuid().ToString('N'))
    $pet = Start-Process -FilePath $exe -WorkingDirectory $env:TEMP -PassThru
    $started = $true
    Start-Sleep -Seconds 5 # Let the window and the Python service start.
    Write-Host "Started the published pet (PID $($pet.Id)) with separate data."
} else {
    Write-Host "Attached to the running pet (PID $($pet.Id)); nothing is changed."
}

function Find-Python([int]$parentId) {
    $child = Get-CimInstance Win32_Process -Filter "ParentProcessId = $parentId" | Where-Object Name -like 'python*' | Select-Object -First 1
    if ($child) { return Get-Process -Id $child.ProcessId -ErrorAction SilentlyContinue }
    return $null
}

$folder = Join-Path $projectRoot 'artifacts\perf'
New-Item -ItemType Directory -Force $folder | Out-Null
$csv = Join-Path $folder ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $Label + '.csv')
$rows = New-Object System.Collections.Generic.List[object]
$previous = @{}
$clock = [Diagnostics.Stopwatch]::StartNew()
$end = [TimeSpan]::FromMinutes($Minutes)
$lastTime = 0.0
Write-Host "Sampling for $Minutes minute(s) into $csv ..."
try {
    while ($clock.Elapsed -lt $end) {
        Start-Sleep -Milliseconds ([int]($Interval * 1000))
        $pet.Refresh()
        if ($pet.HasExited) { Write-Warning 'The pet exited; stopping early.'; break }
        $now = $clock.Elapsed.TotalSeconds
        $python = Find-Python $pet.Id
        foreach ($entry in @(@('pet', $pet), @('python', $python))) {
            $name = $entry[0]; $process = $entry[1]
            if (-not $process) { continue }
            $process.Refresh()
            $cpuTime = $process.TotalProcessorTime.TotalSeconds
            $cpu = $null
            if ($previous.ContainsKey($name) -and $previous[$name].Id -eq $process.Id) {
                $cpu = [Math]::Round(100 * ($cpuTime - $previous[$name].Cpu) / ($now - $lastTime) / $cores, 2)
            }
            $previous[$name] = @{ Id = $process.Id; Cpu = $cpuTime }
            if ($cpu -eq $null) { continue }
            $rows.Add([pscustomobject]@{
                Seconds = [Math]::Round($now, 1); Process = $name; CpuPercent = $cpu
                WorkingSetMB = [Math]::Round($process.WorkingSet64 / 1MB, 1); PrivateMB = [Math]::Round($process.PrivateMemorySize64 / 1MB, 1)
                Handles = $process.HandleCount; Threads = $process.Threads.Count
            })
        }
        $lastTime = $now
    }
} finally {
    if ($rows.Count -gt 0) { $rows | Export-Csv -NoTypeInformation -Path $csv }
    if ($started) {
        $pet.CloseMainWindow() | Out-Null
        if (-not $pet.WaitForExit(5000)) { $pet.Kill() }
        $env:PET_DATA_DIRECTORY = $savedData
    }
}

# CPU is the share of the whole machine (all cores = 100%).
foreach ($name in @('pet', 'python')) {
    $data = @($rows | Where-Object Process -eq $name)
    if ($data.Count -eq 0) { continue }
    $cpu = @($data | ForEach-Object CpuPercent | Sort-Object)
    $p95 = $cpu[[Math]::Min($cpu.Count - 1, [int][Math]::Floor($cpu.Count * 0.95))]
    $avg = [Math]::Round(($cpu | Measure-Object -Average).Average, 2)
    $first = $data[0]; $last = $data[-1]
    $maxPrivate = ($data | Measure-Object PrivateMB -Maximum).Maximum
    Write-Host ("{0,-7} CPU avg {1}% p95 {2}% max {3}% | private MB {4} -> {5} (max {6}) | handles {7} -> {8} | threads {9} -> {10}" -f
        $name, $avg, $p95, $cpu[-1], $first.PrivateMB, $last.PrivateMB, $maxPrivate, $first.Handles, $last.Handles, $first.Threads, $last.Threads)
}
Write-Host "Samples: $csv"
