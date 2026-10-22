# Installer and update packages (Velopack) from the published folder artifacts\win-x64
# (run scripts/publish.ps1 first). Output: artifacts\release (DesktopPet-Setup.exe and the update
# files). -Upload (GitHub Actions, GITHUB_TOKEN set): first fetches the latest release for delta
# updates, then creates the GitHub release v<version> with every file. Never run -Upload by hand
# unless you mean to publish a release.
param([switch]$Upload)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
if (Test-Path (Join-Path $projectRoot '.tools')) { $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli' }
# vpk targets an older .NET; let it run on the SDK's newer runtime.
$env:DOTNET_ROLL_FORWARD = 'Major'
$repoUrl = 'https://github.com/tnpt003/Pet-Computer'
# The install folder is %LOCALAPPDATA%\<packId>; uninstalling deletes it. It must never be the
# user's data folder %LOCALAPPDATA%\DesktopPet (PetPreferences.DataDirectory).
$packId = 'PetComputer'
if ($packId -eq 'DesktopPet') { throw 'The package id must differ from the data folder name.' }

$app = Join-Path $projectRoot 'artifacts\win-x64'
$exe = Join-Path $app 'DesktopPet.exe'
if (-not (Test-Path $exe)) { throw 'Run scripts/publish.ps1 first.' }
$productVersion = (Get-Item $exe).VersionInfo.ProductVersion
if ($productVersion -match '\.dirty') { throw "The published app has uncommitted changes ($productVersion): commit, publish, then pack." }
$version = $productVersion -replace '\+.*$', ''
if ($env:GITHUB_REF_NAME -and $env:GITHUB_REF_NAME -ne "v$version") { throw "Tag $env:GITHUB_REF_NAME does not match the app version $version (DesktopPet.csproj)." }

$output = Join-Path $projectRoot 'artifacts\release'
New-Item -ItemType Directory -Force $output | Out-Null
& $dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }
if ($Upload) {
    if (-not $env:GITHUB_TOKEN) { throw 'GITHUB_TOKEN is needed to upload.' }
    # The previous release (if any) for delta packages; a first release has none.
    & $dotnet vpk download github --repoUrl $repoUrl --token $env:GITHUB_TOKEN -o $output
}
& $dotnet vpk pack -u $packId -v $version -p $app -e DesktopPet.exe -o $output --packTitle 'Desktop Pet' --packAuthors 'tnpt003' `
    -i (Join-Path $projectRoot 'src\DesktopPet\Assets\Pet.ico') --shortcuts 'Desktop,StartMenuRoot'
if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed.' }
Write-Host "Packages for $version in $output"
if ($Upload) {
    & $dotnet vpk upload github --repoUrl $repoUrl --token $env:GITHUB_TOKEN -o $output --publish --releaseName "Desktop Pet $version" --tag "v$version"
    if ($LASTEXITCODE -ne 0) { throw 'vpk upload failed.' }
    Write-Host "Released v$version on GitHub."
}
