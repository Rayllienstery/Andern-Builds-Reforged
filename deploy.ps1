# Deploy Andern-Builds-Reforged from dev/ staging into the live SPT user/mods folder.
# Stop SPT Server before running if the DLL is already loaded.

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot          # c:\Games\SPT
$Proj = Join-Path $PSScriptRoot "AndernBuildsReforged\AndernBuildsReforged.csproj"
$OutDir = Join-Path $PSScriptRoot "AndernBuildsReforged\bin\Release\net9.0"
$LiveMod = Join-Path $Root "SPT\user\mods\Andern-Builds-Reforged"

Write-Host "Building $Proj ..."
dotnet build $Proj -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

New-Item -ItemType Directory -Force -Path $LiveMod | Out-Null

$dll = Join-Path $OutDir "Andern-Builds-Reforged.dll"
$fast = Join-Path $OutDir "fastJSON5.dll"
if (-not (Test-Path $dll)) { throw "Missing $dll" }

Copy-Item -Force $dll $LiveMod
if (Test-Path $fast) { Copy-Item -Force $fast $LiveMod }

foreach ($dir in @("config", "presets", "trader")) {
    $src = Join-Path $PSScriptRoot "AndernBuildsReforged\$dir"
    $dst = Join-Path $LiveMod $dir
    if (Test-Path $dst) { Remove-Item -Recurse -Force $dst }
    Copy-Item -Recurse $src $dst
}

Copy-Item -Force (Join-Path $PSScriptRoot "LICENSE") (Join-Path $LiveMod "LICENSE") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $PSScriptRoot "README.md") (Join-Path $LiveMod "README.md") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $PSScriptRoot "CHANGELOG.md") (Join-Path $LiveMod "CHANGELOG.md") -ErrorAction SilentlyContinue

Write-Host "Deployed Andern-Builds-Reforged -> $LiveMod"
Get-ChildItem $LiveMod | Format-Table Name, Length -AutoSize
Write-Host "Remember: disable BarlogM-Andern only after verifying this mod loads."
