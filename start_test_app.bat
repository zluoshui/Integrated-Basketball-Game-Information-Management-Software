@echo off
setlocal

set "APP_ROOT=%~dp0"
set "PROJECT_FILE=%APP_ROOT%src\BasketballManager\BasketballManager.csproj"
set "CONFIGURATION=Debug"

rem This launcher is for local testing. It prefers the D:\Programs .NET install
rem used on this machine, but DOTNET_EXE can override it when needed.
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

rem If DOTNET_EXE is a full path, make that SDK the current process default too.
for %%I in ("%DOTNET_EXE%") do set "DOTNET_DIR=%%~dpI"
if exist "%DOTNET_DIR%dotnet.exe" (
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

rem WPF creates App.g.cs and MainWindow.g.cs under obj\. They are generated
rem build files, not source files. If obj is only partially cleaned, MSBuild can
rem keep stale compile inputs and CSC reports CS2001 for those generated files.
rem Clean first so this test launcher always regenerates WPF files from XAML.
echo Cleaning stale generated WPF build files...
"%DOTNET_EXE%" clean "%PROJECT_FILE%" -c %CONFIGURATION% -v:minimal
if errorlevel 1 goto :fail

echo Building BasketballManager...
"%DOTNET_EXE%" build "%PROJECT_FILE%" -c %CONFIGURATION% -v:minimal
if errorlevel 1 goto :fail

echo Starting BasketballManager...
"%DOTNET_EXE%" run --project "%PROJECT_FILE%" -c %CONFIGURATION% --no-build -- %*
if errorlevel 1 goto :fail
exit /b 0

:fail
echo.
echo Build or startup failed. See the messages above.
pause
exit /b 1
