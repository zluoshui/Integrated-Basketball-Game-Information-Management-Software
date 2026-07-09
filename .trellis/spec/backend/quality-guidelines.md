# Quality Guidelines

> Code quality standards for backend development.

---

## Overview

<!--
Document your project's quality standards here.

Questions to answer:
- What patterns are forbidden?
- What linting rules do you enforce?
- What are your testing requirements?
- What code review standards apply?
-->

(To be filled by the team)

---

## Forbidden Patterns

<!-- Patterns that should never be used and why -->

(To be filled by the team)

---

## Required Patterns

<!-- Patterns that must always be used -->

### Scenario: Local Run And Publish Scripts

#### 1. Scope / Trigger
- Trigger: any `.bat`, PowerShell script, README command, or CI/local command used to build, run, self-check, or publish the desktop app.

#### 2. Signatures
- Build: `dotnet build .\BasketballManager.sln`
- Run: `dotnet run --project .\src\BasketballManager\BasketballManager.csproj`
- Self-check: `dotnet run --project .\src\BasketballManager\BasketballManager.csproj -- --self-check`
- Publish: `dotnet publish .\src\BasketballManager\BasketballManager.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true`
- Optional env: `DOTNET_EXE=<path to dotnet.exe>`

#### 3. Contracts
- Scripts must default to `dotnet` from `PATH`.
- Scripts may honor `DOTNET_EXE` when a developer needs a custom SDK path.
- Scripts and README commands must not hard-code personal absolute paths such as `D:\Programs\dotnet\dotnet.exe`.
- Publish output must stay under ignored build folders such as `bin/` or `obj/`.
- WPF run scripts that build a desktop project may remove only that project's `obj\<Configuration>\<TargetFramework>` folder before build so XAML generated files such as `App.g.cs` and `MainWindow.g.cs` are recreated.

#### 4. Validation & Error Matrix
- `dotnet` missing and `DOTNET_EXE` unset -> show a plain error telling the user to install .NET 10 SDK or set `DOTNET_EXE`.
- Build failure -> keep the command output visible; do not swallow errors.
- Missing WPF generated `.g.cs` files under `obj` -> clean only the affected project's target-framework intermediate folder, then rebuild.
- Publish output outside ignored folders -> update `.gitignore` or output path before committing.

#### 5. Good/Base/Bad Cases
- Good: `DOTNET_EXE=D:\Programs\dotnet\dotnet.exe start_test_app.bat` works on a developer machine.
- Base: `start_test_app.bat` works when .NET 10 SDK is on `PATH`.
- Bad: `start_test_app.bat` always calls one developer's absolute SDK path.

#### 6. Tests Required
- Run `dotnet build .\BasketballManager.sln`.
- Run `dotnet run --project .\src\BasketballManager\BasketballManager.csproj -- --self-check`.
- Run the documented self-contained publish command before phase 0 acceptance.

#### 7. Wrong vs Correct

Wrong:

```bat
set "DOTNET_EXE=D:\Programs\dotnet\dotnet.exe"
"%DOTNET_EXE%" run --project src\BasketballManager\BasketballManager.csproj
```

Correct:

```bat
if "%DOTNET_EXE%"=="" set "DOTNET_EXE=dotnet"
"%DOTNET_EXE%" run --project "%~dp0src\BasketballManager\BasketballManager.csproj"
```

---

## Testing Requirements

<!-- What level of testing is expected -->

(To be filled by the team)

---

## Code Review Checklist

<!-- What reviewers should check -->

(To be filled by the team)
