$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
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

# Which source this build is: the SDK puts the commit into the informational version
# ("1.0.0+<commit>"); ".dirty" is added when the working tree has uncommitted changes, since the
# commit alone would not describe them. Without git the version stays as it is.
$revisionArgs = @()
$revision = $null
$dirty = $false
if (Get-Command git -ErrorAction SilentlyContinue) {
    $revision = (& git -C $projectRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -eq 0 -and $revision) {
        $dirty = [bool](& git -C $projectRoot status --porcelain 2>$null)
        $revisionArgs = @("-p:SourceRevisionId=$revision$(if ($dirty) { '.dirty' })")
    } else { $revision = $null }
}
& $dotnet publish (Join-Path $projectRoot 'src\DesktopPet\DesktopPet.csproj') -c Release -p:PublishProfile=Windows-x64 @revisionArgs
if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Close any running published pet and check the build output.' }
$output = Join-Path $projectRoot 'artifacts\win-x64'
foreach ($file in @('DesktopPet.exe', 'coreclr.dll', 'hostfxr.dll', 'PresentationFramework.dll',
                    'ai\service.py', 'ai\speech.py', 'ai\learning.py', 'ai\seasons.py', 'ai\lines.json', 'ai\calendar.json', 'ai\personality.json', 'python\python.exe', 'python\python313.zip', 'python\LICENSE.txt')) {
    if (-not (Test-Path (Join-Path $output $file))) { throw "Missing published dependency: $file" }
}
Write-Host "Published application: $output\DesktopPet.exe"
# Shareable archive of the whole folder, named by version and commit so packages of different
# sources never replace each other; a build with uncommitted changes also gets the time.
$productVersion = (Get-Item (Join-Path $output 'DesktopPet.exe')).VersionInfo.ProductVersion
Write-Host "Build version: $productVersion"
$version = $productVersion -replace '\+.*$', ''
$suffix = if ($revision) { '-' + $revision.Substring(0, 7) + $(if ($dirty) { '-dirty-' + (Get-Date -Format 'yyyyMMdd-HHmmss') }) } else { '' }
$zip = Join-Path $projectRoot "artifacts\DesktopPet-$version$suffix-win-x64.zip"
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip
Write-Host "Shareable archive: $zip"
Write-Host 'Open this folder in File Explorer and double-click DesktopPet.exe.'
Write-Host 'Copy the entire win-x64 folder when sharing it. Exit from the cat or tray context menu.'
