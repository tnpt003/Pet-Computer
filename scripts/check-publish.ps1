$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $projectRoot 'artifacts\win-x64'
$exe = Join-Path $output 'DesktopPet.exe'
if (-not (Test-Path $exe)) { throw 'Run scripts/publish.ps1 first.' }
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PetPublishWindow {
    public delegate bool Callback(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    public static IntPtr Find(int processId) {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hwnd, unused) => {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == processId && IsWindowVisible(hwnd)) { result = hwnd; return false; }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
'@
$saved = @{}
foreach ($name in @('PATH', 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'PET_PYTHON', 'PET_AI_DISABLED', 'PET_DATA_DIRECTORY')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$petProcess = $null
$pythonProcess = $null
try {
    $env:PATH = "$env:SystemRoot\System32;$env:SystemRoot"
    $env:DOTNET_ROOT = Join-Path $projectRoot '.tools\nonexistent-runtime'
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:PET_PYTHON = $null
    $env:PET_AI_DISABLED = '0'
    # Keep smoke-test preferences and AI memory separate from the user's cat.
    $env:PET_DATA_DIRECTORY = Join-Path $projectRoot ('artifacts\smoke-data\' + [Guid]::NewGuid().ToString('N'))
    $petProcess = Start-Process -FilePath $exe -WorkingDirectory $env:TEMP -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 250
        $petProcess.Refresh()
        if ($petProcess.HasExited) { throw "Published pet exited unexpectedly: $($petProcess.ExitCode)" }
        $child = Get-CimInstance Win32_Process -Filter "ParentProcessId = $($petProcess.Id)" |
            Where-Object { $_.ExecutablePath -eq (Join-Path $output 'python\python.exe') } | Select-Object -First 1
    } while (-not $child -and [DateTime]::UtcNow -lt $deadline)
    if (-not $child) { throw 'Published pet did not launch its bundled Python service.' }
    $pythonProcess = [Diagnostics.Process]::GetProcessById($child.ProcessId)
    $runtime = $petProcess.Modules | Where-Object ModuleName -eq 'coreclr.dll' | Select-Object -First 1
    if (-not $runtime -or $runtime.FileName -ne (Join-Path $output 'coreclr.dll')) {
        throw 'Pet did not load the bundled .NET runtime.'
    }
    $window = [PetPublishWindow]::Find($petProcess.Id)
    if ($window -eq [IntPtr]::Zero) { throw 'Pet window is not visible.' }
    Write-Host 'PASS: Published pet starts from an unrelated directory using bundled .NET and Python.'
    $duplicate = Start-Process -FilePath $exe -WorkingDirectory $env:TEMP -WindowStyle Hidden -PassThru
    try {
        if (-not $duplicate.WaitForExit(5000) -or $duplicate.ExitCode -ne 0) { throw 'Second launch did not hand off to the existing pet.' }
        $petProcess.Refresh()
        if ($petProcess.HasExited) { throw 'Second launch stopped the original pet.' }
        Write-Host 'PASS: Second launch exits successfully while the original pet stays running.'
    } finally {
        if (-not $duplicate.HasExited) { $duplicate.Kill() }
        $duplicate.Dispose()
    }
    if (-not [PetPublishWindow]::PostMessage($window, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)) {
        throw 'Could not request normal window close.'
    }
    if (-not $petProcess.WaitForExit(5000) -or $petProcess.ExitCode -ne 0) { throw 'Pet did not exit cleanly.' }
    if (-not $pythonProcess.WaitForExit(3000)) { throw 'Owned Python service remained running after pet exit.' }
    Write-Host 'PASS: Closing the published pet exits the app and its Python service.'
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    # Cleanup is limited to the test-owned processes, even if an assertion fails.
    foreach ($owned in @($petProcess, $pythonProcess)) {
        if ($owned) {
            if (-not $owned.HasExited) { $owned.Kill() }
            $owned.Dispose()
        }
    }
}
