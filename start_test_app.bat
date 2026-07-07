@echo off
setlocal EnableExtensions

set "APP_ROOT=%~dp0"
set "PROJECT_DIR=%APP_ROOT%src\BasketballManager"
set "PROJECT_FILE=%PROJECT_DIR%\BasketballManager.csproj"
set "CONFIGURATION=Debug"
set "TARGET_FRAMEWORK=net10.0-windows"
set "ASSEMBLY_NAME=BasketballManager"

rem Local test launcher for this WPF project.
rem Prefer the D:\Programs .NET SDK on this machine. Set DOTNET_EXE before
rem running this script if another SDK location is required.
if "%DOTNET_EXE%"=="" (
  if exist "D:\Programs\dotnet\dotnet.exe" (
    set "DOTNET_EXE=D:\Programs\dotnet\dotnet.exe"
  ) else (
    set "DOTNET_EXE=dotnet"
  )
)

if not exist "%PROJECT_FILE%" (
  echo Cannot find project file:
  echo "%PROJECT_FILE%"
  goto :fail
)

rem If DOTNET_EXE is a full path, make that SDK win for this process.
for %%I in ("%DOTNET_EXE%") do set "DOTNET_DIR=%%~dpI"
if not "%DOTNET_DIR%"=="" if exist "%DOTNET_DIR%dotnet.exe" (
  set "DOTNET_ROOT=%DOTNET_DIR:~0,-1%"
  set "PATH=%DOTNET_DIR%;%PATH%"
)

"%DOTNET_EXE%" --list-sdks | findstr /b "10." >nul 2>nul
if errorlevel 1 (
  echo Cannot find a usable .NET 10 SDK.
  echo Tried: "%DOTNET_EXE%"
  echo Install .NET 10 SDK or set DOTNET_EXE to your dotnet.exe path.
  goto :fail
)

cd /d "%APP_ROOT%"

rem WPF generates App.g.cs and MainWindow.g.cs in obj. Those files are build
rem artifacts. Remove only this project's intermediate Debug folder to force
rem XAML markup compilation to regenerate them. Do not delete bin\Debug here:
rem an already-running BasketballManager.exe can lock it and break cleanup.
if exist "%PROJECT_DIR%\obj\%CONFIGURATION%\%TARGET_FRAMEWORK%" (
  echo Cleaning stale WPF generated files...
  rmdir /s /q "%PROJECT_DIR%\obj\%CONFIGURATION%\%TARGET_FRAMEWORK%"
)

rem Build into a temporary output folder. This avoids MSB3021/MSB3027 when an
rem older app instance is still running and locking bin\Debug\...\BasketballManager.exe.
set "LAUNCH_ROOT=%TEMP%\BasketballManagerTestLaunch\%RANDOM%%RANDOM%"
set "BIN_BASE=%LAUNCH_ROOT%\bin\"
set "RUN_EXE=%BIN_BASE%%CONFIGURATION%\%TARGET_FRAMEWORK%\%ASSEMBLY_NAME%.exe"

echo Restoring BasketballManager...
"%DOTNET_EXE%" restore "%PROJECT_FILE%" -v:minimal
if errorlevel 1 goto :fail

echo Building BasketballManager in isolated test output...
"%DOTNET_EXE%" build "%PROJECT_FILE%" -c %CONFIGURATION% -v:minimal --no-restore /p:BaseOutputPath=%BIN_BASE%
if errorlevel 1 goto :fail

if not exist "%RUN_EXE%" (
  echo Cannot find built executable:
  echo "%RUN_EXE%"
  goto :fail
)

echo Starting BasketballManager...
"%RUN_EXE%" %*
if errorlevel 1 goto :fail
exit /b 0

:fail
echo.
echo Build or startup failed. See the messages above.
if not "%NO_PAUSE%"=="1" pause
exit /b 1
