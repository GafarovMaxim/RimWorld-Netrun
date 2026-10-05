@echo off
setlocal

echo ========================================
echo Hacking Expansion: Netrun - build
echo ========================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: .NET SDK was not found. Install it and try again.
    pause
    exit /b 1
)

if not "%RIMWORLD_DIR%"=="" (
    echo RimWorld directory: "%RIMWORLD_DIR%"
) else (
    echo RIMWORLD_DIR is not set, using the default from Source\Netrun.csproj.
)
echo.

dotnet build "%~dp0Source\Netrun.csproj" -c Release

if errorlevel 1 (
    echo.
    echo BUILD FAILED
    pause
    exit /b 1
)

echo.
echo BUILD SUCCESSFUL: Assemblies\HackingExpansionNetrun.dll
pause
