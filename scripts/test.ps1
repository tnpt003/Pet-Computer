param([switch]$Ui)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$localPython = Join-Path $projectRoot '.tools\python\python.exe'
$python = if ($env:PET_PYTHON) { $env:PET_PYTHON } elseif (Test-Path $localPython) { $localPython } else { $null }
if (-not $python) {
    foreach ($candidate in @('py', 'python')) {
        if (Get-Command $candidate -ErrorAction SilentlyContinue) {
            $resolved = & $candidate -c 'import sys; print(sys.executable)' 2>$null
            if ($LASTEXITCODE -eq 0 -and $resolved -and (Test-Path $resolved)) { $python = $resolved; break }
        }
    }
}
if ($python) {
    $env:PET_TEST_PYTHON = $python
    & $python (Join-Path $projectRoot 'tests\ai\test_service.py') -v
    if ($LASTEXITCODE -ne 0) { throw 'Python behavior tests failed.' }
} else {
    Write-Warning 'Python not found: running C# checks only. Set PET_PYTHON to include Python integration checks.'
}
& $dotnet run --project (Join-Path $projectRoot 'tests\DesktopPet.Tests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Physics/AI checks failed.' }
if ($Ui) {
    & $dotnet run --project (Join-Path $projectRoot 'tests\DesktopPet.UiChecks') -c Release
    # 1: some checks failed (listed above); anything else: the run broke before finishing.
    if ($LASTEXITCODE -eq 1) { throw 'WPF regression checks failed (see the FAIL lines above).' }
    if ($LASTEXITCODE -ne 0) { throw "WPF checks could not finish (exit code $LASTEXITCODE). Run from an interactive Windows desktop; see the error above." }
}
