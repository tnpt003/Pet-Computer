# Shared by the project scripts; dot-source it: . (Join-Path $PSScriptRoot 'common.ps1')
$projectRoot = Split-Path $PSScriptRoot -Parent
# The project's own SDK (.tools\dotnet) when present, otherwise the one on PATH.
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
