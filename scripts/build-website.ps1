$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $projectRoot 'artifacts/website'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Copy-Item -Path (Join-Path $projectRoot 'website/*') -Destination $output -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $output 'media') | Out-Null
foreach ($name in @('desktop-companion.png', 'battle-hub.png', 'boss-fight.png', 'desktop-pet-intro.mp4')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "docs/media/$name") -Destination (Join-Path $output 'media') -Force
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/DesktopPet/Assets/Pet.ico') -Destination (Join-Path $output 'favicon.ico') -Force
Write-Output "Website: $output"
