# Extract all official map collision meshes to .tri files
# Output: tools\cs2-phys-extractor\bin\Release\net9.0\tri\

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$extractor = Join-Path $PSScriptRoot "cs2-phys-extractor"

if (-not (Test-Path $extractor)) {
    git clone --depth 1 https://github.com/itzlaith/cs2-phys-extractor $extractor
}

Push-Location $extractor
dotnet build -c Release
"2`n2" | dotnet run -c Release --no-build
Pop-Location

$triSource = Join-Path $extractor "bin\Release\net9.0\tri"
$dest = Join-Path $root "External Aimbot\bin\Release\net8.0\maps"
$destProject = Join-Path $root "External Aimbot\maps"

New-Item -ItemType Directory -Force -Path $dest, $destProject | Out-Null
Copy-Item (Join-Path $triSource "*.tri") $dest -Force
Copy-Item (Join-Path $triSource "de_*.tri") $destProject -Force

Write-Host "Copied map meshes to:"
Write-Host "  $dest"
