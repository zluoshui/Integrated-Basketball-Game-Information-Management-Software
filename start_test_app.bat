@echo off
setlocal
set "DOTNET_EXE=D:\Programs\dotnet\dotnet.exe"
if not exist "%DOTNET_EXE%" (
  echo Cannot find .NET SDK at %DOTNET_EXE%.
  echo Please install .NET 10 SDK or update DOTNET_EXE in this file.
  pause
  exit /b 1
)
"%DOTNET_EXE%" run --project "%~dp0src\BasketballManager\BasketballManager.csproj"
if errorlevel 1 pause
