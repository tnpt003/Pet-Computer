# Samples CPU, memory, handles and threads of the pet and its Python service once a second.
# Attaches to the running pet (read only) or, if none runs, starts the published pet with
# separate data and closes it afterwards. Do the activity to measure while it runs
# (idle, sleep, fast drags, laser...) and pass -Label to name the run.
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\perf.ps1 -Minutes 5 -Label idle
#   -Battle run|paused: start a separate pet that keeps a Battle Mode fight running (or paused);
#   -Battle expedition: the same with the Expedition's heaviest kind of battle (the last elite
#   encounter, the last step's threat, a full set of relics; a run in memory only);
#   -Battle crowd: a fight that keeps up to 16 slimes on the floor (the most the field draws).
#   With -Battle the pet logs its frame numbers once a second; they are summed up at the end
#   (fps, the longest gap between two frames, enemies on the floor).
#   -Trophies: a separate pet with every keepsake, so the display corner shows (Pet Mode).
#   For these the user's pet must be closed first (one pet per session).
param([double]$Minutes = 3, [string]$Label = 'run', [double]$Interval = 1, [ValidateSet('', 'run', 'paused', 'expedition', 'crowd')][string]$Battle = '', [switch]$Trophies)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PetPerfWindow {
    public delegate bool Callback(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int max);
    // The pet window's title ends with "Desktop Pet"; bubbles, the HUD and menus are skipped.
    public static IntPtr Find(int processId) {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hwnd, unused) => {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            var title = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, title, 256);
            if (pid == processId && IsWindowVisible(hwnd) && title.ToString().EndsWith("Desktop Pet")) { result = hwnd; return false; }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
'@
if (($Battle -or $Trophies) -and (Get-Process DesktopPet -ErrorAction SilentlyContinue)) {
    throw 'A pet is running: close it first, then measure the battle with a separate pet.'
}
$savedAutostart = $env:PET_BATTLE_AUTOSTART
$savedProbe = $env:PET_CONTEXT_PROBE
if ($Battle) {
    $env:PET_BATTLE_AUTOSTART = $Battle
    $env:PET_CONTEXT_PROBE = 'off' # A fullscreen app or an idle machine must not pause the measured fight.
}
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
    if ($Trophies) {
        # Every built-in keepsake (expedition.json version 3), in the separate data only.
        New-Item -ItemType Directory -Force $env:PET_DATA_DIRECTORY | Out-Null
        Set-Content -Encoding ascii -Path (Join-Path $env:PET_DATA_DIRECTORY 'expedition.json') -Value '{ "Version": 3, "Keepsakes": [ "cup_bronze", "cup_silver", "cup_gold", "slime_cushion", "expedition_scarf" ] }'
    }
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
        # WM_CLOSE to the pet window itself (found by title, as check-publish.ps1 does):
        # MainWindowHandle may be a bubble or the HUD, which would leave only Kill.
        $window = [PetPerfWindow]::Find($pet.Id)
        if ($window -ne [IntPtr]::Zero) { [PetPerfWindow]::PostMessage($window, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }
        if (-not $pet.WaitForExit(5000)) { Write-Warning 'The measured pet did not close; stopping it.'; $pet.Kill() }
        $measuredLog = Join-Path $env:PET_DATA_DIRECTORY 'logs\desktoppet.log'
        $env:PET_DATA_DIRECTORY = $savedData
    }
    $env:PET_BATTLE_AUTOSTART = $savedAutostart
    $env:PET_CONTEXT_PROBE = $savedProbe
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
if ($Battle -and $measuredLog -and (Test-Path $measuredLog)) {
    # "Frame stats: 59.8 fps, longest gap 21 ms, 14 enemies" once a second while the fight ran.
    $frames = @(Select-String -Path $measuredLog -Pattern 'Frame stats: ([\d.]+) fps, longest gap (\d+) ms, (\d+) enemies' | ForEach-Object {
        [pscustomobject]@{ Fps = [double]$_.Matches[0].Groups[1].Value; Gap = [double]$_.Matches[0].Groups[2].Value; Enemies = [int]$_.Matches[0].Groups[3].Value } })
    if ($frames.Count -gt 0) {
        $fps = @($frames | ForEach-Object Fps | Sort-Object); $gaps = @($frames | ForEach-Object Gap | Sort-Object)
        $p95gap = $gaps[[Math]::Min($gaps.Count - 1, [int][Math]::Floor($gaps.Count * 0.95))]
        Write-Host ("frames  {0} s | fps median {1} min {2} | longest gap p95 {3} ms max {4} ms | enemies avg {5} max {6}" -f
            $frames.Count, $fps[[int][Math]::Floor($fps.Count / 2)], $fps[0], $p95gap, $gaps[-1],
            [Math]::Round(($frames | Measure-Object Enemies -Average).Average, 1), ($frames | Measure-Object Enemies -Maximum).Maximum)
    }
}
Write-Host "Samples: $csv"
