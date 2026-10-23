param([switch]$NoAi)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
if ($NoAi) { $env:PET_AI_DISABLED = '1' }
else { $env:PET_AI_DISABLED = '0' }
$localPython = Join-Path $projectRoot '.tools\python\python.exe'
if (-not $env:PET_PYTHON -and (Test-Path $localPython)) { $env:PET_PYTHON = $localPython }
& $dotnet build (Join-Path $projectRoot 'src\DesktopPet\DesktopPet.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Install the .NET 10 SDK and try again.' }
$exe = Join-Path $projectRoot 'src\DesktopPet\bin\Release\net10.0-windows\DesktopPet.exe'
if (Test-Path $localDotnet) { $env:DOTNET_ROOT = Split-Path $localDotnet -Parent }
Start-Process -FilePath $exe -WorkingDirectory $projectRoot -WindowStyle Hidden
Write-Host 'Pet started. Right-click the cat or its system-tray icon and choose Exit to stop it.'
