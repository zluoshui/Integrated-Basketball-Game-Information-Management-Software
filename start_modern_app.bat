@echo off
setlocal EnableExtensions

set "APP_ROOT=%~dp0"
set "DOTNET_EXE=%DOTNET_EXE%"
if "%DOTNET_EXE%"=="" (
  if exist "D:\Programs\dotnet\dotnet.exe" (
    set "DOTNET_EXE=D:\Programs\dotnet\dotnet.exe"
  ) else (
    set "DOTNET_EXE=dotnet"
  )
)

set "API_PROJECT=%APP_ROOT%src\BasketballManager.Api\BasketballManager.Api.csproj"
set "DESKTOP_PROJECT_DIR=%APP_ROOT%src\BasketballManager.Desktop"
set "DESKTOP_PROJECT=%APP_ROOT%src\BasketballManager.Desktop\BasketballManager.Desktop.csproj"
set "WEBUI_DIR=%APP_ROOT%src\BasketballManager.WebUI"
set "API_WWWROOT=%APP_ROOT%src\BasketballManager.Api\wwwroot"
set "CONFIGURATION=Debug"
set "DESKTOP_TARGET_FRAMEWORK=net10.0-windows"

if not exist "%API_PROJECT%" (
  echo Cannot find API project.
  goto :fail
)

echo Building Core + API...
"%DOTNET_EXE%" build "%API_PROJECT%" -c Debug -v:minimal
if errorlevel 1 goto :fail

if exist "%WEBUI_DIR%\package.json" (
  pushd "%WEBUI_DIR%"
  if not exist "node_modules" (
    echo Installing WebUI dependencies...
    call npm install
    if errorlevel 1 (
      popd
      goto :fail
    )
  )
  echo Building WebUI...
  call npm run build
  if errorlevel 1 (
    popd
    goto :fail
  )
  popd

  echo Publishing SPA into API wwwroot...
  if exist "%API_WWWROOT%" rmdir /s /q "%API_WWWROOT%"
  mkdir "%API_WWWROOT%"
  xcopy /e /i /y "%WEBUI_DIR%\dist\*" "%API_WWWROOT%\" >nul
)

if not exist "%API_WWWROOT%\index.html" (
  echo Frontend index.html missing at:
  echo "%API_WWWROOT%\index.html"
  echo Please ensure WebUI build succeeded.
  goto :fail
)

echo Building Desktop shell + API output with wwwroot...
"%DOTNET_EXE%" build "%API_PROJECT%" -c %CONFIGURATION% -v:minimal
if errorlevel 1 goto :fail
if exist "%DESKTOP_PROJECT_DIR%\obj\%CONFIGURATION%\%DESKTOP_TARGET_FRAMEWORK%" (
  echo Cleaning stale Desktop WPF generated files...
  rmdir /s /q "%DESKTOP_PROJECT_DIR%\obj\%CONFIGURATION%\%DESKTOP_TARGET_FRAMEWORK%"
)
"%DOTNET_EXE%" build "%DESKTOP_PROJECT%" -c %CONFIGURATION% -v:minimal
if errorlevel 1 goto :fail

set "DESKTOP_EXE=%DESKTOP_PROJECT_DIR%\bin\%CONFIGURATION%\%DESKTOP_TARGET_FRAMEWORK%\BasketballManager.Desktop.exe"
set "API_OUTPUT_INDEX=%APP_ROOT%src\BasketballManager.Api\bin\%CONFIGURATION%\net10.0\wwwroot\index.html"
if not exist "%DESKTOP_EXE%" (
  echo Cannot find desktop executable:
  echo "%DESKTOP_EXE%"
  goto :fail
)
if not exist "%API_OUTPUT_INDEX%" (
  echo API output missing SPA index:
  echo "%API_OUTPUT_INDEX%"
  goto :fail
)

echo Starting modern UI...
start "" "%DESKTOP_EXE%"
exit /b 0

:fail
echo.
echo Modern UI startup failed.
if not "%NO_PAUSE%"=="1" pause
exit /b 1
