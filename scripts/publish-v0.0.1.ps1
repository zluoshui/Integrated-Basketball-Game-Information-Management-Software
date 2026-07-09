param(
  [string]$Version = "0.0.1"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $Root "BasketballManager.sln"))) {
  $Root = Get-Location
}

$Dotnet = if (Test-Path "D:\Programs\dotnet\dotnet.exe") { "D:\Programs\dotnet\dotnet.exe" } else { "dotnet" }
$WebUI = Join-Path $Root "src\BasketballManager.WebUI"
$ApiProj = Join-Path $Root "src\BasketballManager.Api\BasketballManager.Api.csproj"
$DesktopProj = Join-Path $Root "src\BasketballManager.Desktop\BasketballManager.Desktop.csproj"
$ApiWww = Join-Path $Root "src\BasketballManager.Api\wwwroot"
$OutRoot = Join-Path $Root "publish\v$Version"
$ApiOut = Join-Path $OutRoot "api"
$DesktopOut = Join-Path $OutRoot "desktop"

Write-Host "Building WebUI..."
Push-Location $WebUI
if (-not (Test-Path "node_modules")) {
  npm install
}
npm run build
Pop-Location

Write-Host "Copying SPA to API wwwroot..."
if (Test-Path $ApiWww) { Remove-Item $ApiWww -Recurse -Force }
New-Item -ItemType Directory -Path $ApiWww | Out-Null
Copy-Item -Recurse (Join-Path $WebUI "dist\*") $ApiWww

Write-Host "Publishing API..."
& $Dotnet publish $ApiProj -c Release -r win-x64 --self-contained true -o $ApiOut
if ($LASTEXITCODE -ne 0) { throw "API publish failed" }

Write-Host "Publishing Desktop..."
& $Dotnet publish $DesktopProj -c Release -r win-x64 --self-contained true -o $DesktopOut
if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed" }

Copy-Item (Join-Path $Root "LICENSE") $OutRoot -Force
Copy-Item (Join-Path $Root "README.md") $OutRoot -Force
Copy-Item (Join-Path $Root "update.json") $OutRoot -Force
Copy-Item (Join-Path $Root "CONTRIBUTORS.md") $OutRoot -Force

$RunBat = @"
@echo off
setlocal
cd /d %~dp0
if not exist "desktop\BasketballManager.Desktop.exe" (
  echo Desktop executable not found.
  pause
  exit /b 1
)
start "" "desktop\BasketballManager.Desktop.exe"
"@
Set-Content -Path (Join-Path $OutRoot "run.bat") -Value $RunBat -Encoding ASCII

$Notes = @"
# Basketball Manager v$Version

## Package contents
- desktop/: WebView2 shell
- api/: local API + SPA wwwroot
- run.bat: launch helper
- LICENSE / README.md / update.json / CONTRIBUTORS.md

## Notes
- First open-source release.
- Built-in demo competition is created automatically when no competition exists.
"@
Set-Content -Path (Join-Path $OutRoot "RELEASE_NOTES.md") -Value $Notes -Encoding UTF8

Write-Host "Publish complete: $OutRoot"
Get-ChildItem $OutRoot | Select-Object Name, Mode
