$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$pythonRoot = Join-Path $projectRoot '.tools\python'
$python = Join-Path $pythonRoot 'python.exe'

# Official, pinned x64 embeddable distribution; standard library and license included.
if (-not (Test-Path $python)) {
    $archive = Join-Path $projectRoot '.tools\python-3.13.15-embed-amd64.zip'
    New-Item -ItemType Directory -Force (Split-Path $archive -Parent) | Out-Null
    $expectedHash = 'd1f04d990aee1253d8569e8e5104e30fa9f5fa830899f14843448872d936a2cf'
    if (-not (Test-Path $archive)) {
        Invoke-WebRequest -Uri 'https://www.python.org/ftp/python/3.13.15/python-3.13.15-embed-amd64.zip' -OutFile $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
        throw "Python archive checksum mismatch: $archive"
    }
    Expand-Archive -LiteralPath $archive -DestinationPath $pythonRoot -Force
}
& $python -c 'import sys; assert sys.maxsize > 2**32 and sys.version_info[:2] == (3, 13)'
if ($LASTEXITCODE -ne 0) { throw 'The bundled Python runtime must be Python 3.13 x64.' }

& $dotnet publish (Join-Path $projectRoot 'src\DesktopPet\DesktopPet.csproj') -c Release -p:PublishProfile=Windows-x64
if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Close any running published pet and check the build output.' }
$output = Join-Path $projectRoot 'artifacts\win-x64'
foreach ($file in @('DesktopPet.exe', 'coreclr.dll', 'hostfxr.dll', 'PresentationFramework.dll',
                    'ai\service.py', 'ai\personality.json', 'python\python.exe', 'python\python313.zip', 'python\LICENSE.txt')) {
    if (-not (Test-Path (Join-Path $output $file))) { throw "Missing published dependency: $file" }
}
Write-Host "Published application: $output\DesktopPet.exe"
# Shareable archive of the whole folder, named by the assembly version.
$version = (Get-Item (Join-Path $output 'DesktopPet.exe')).VersionInfo.ProductVersion -replace '\+.*$', ''
$zip = Join-Path $projectRoot "artifacts\DesktopPet-$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip
Write-Host "Shareable archive: $zip"
Write-Host 'Open this folder in File Explorer and double-click DesktopPet.exe.'
Write-Host 'Copy the entire win-x64 folder when sharing it. Exit from the cat or tray context menu.'
