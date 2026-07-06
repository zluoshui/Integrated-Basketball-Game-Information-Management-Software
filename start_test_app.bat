@echo off
setlocal
if "%DOTNET_EXE%"=="" set "DOTNET_EXE=dotnet"
"%DOTNET_EXE%" --info >nul 2>nul
if errorlevel 1 (
  echo Cannot find a usable .NET SDK.
  echo Install .NET 10 SDK or set DOTNET_EXE to your dotnet.exe path.
  pause
  exit /b 1
)
"%DOTNET_EXE%" run --project "%~dp0src\BasketballManager\BasketballManager.csproj"
if errorlevel 1 pause
