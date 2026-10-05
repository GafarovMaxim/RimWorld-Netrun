@echo off
setlocal

echo ========================================
echo Hacking Expansion: Black ICE
echo Build script
echo ========================================
echo.

if "%RIMWORLD_DIR%"=="" (
    echo RIMWORLD_DIR is not set.
    echo.
    echo Example:
    echo set "RIMWORLD_DIR=E:\SteamLibrary\steamapps\common\RimWorld"
    echo.
    pause
    exit /b 1
)

echo RimWorld directory:
echo "%RIMWORLD_DIR%"
echo.

if not exist "%RIMWORLD_DIR%\RimWorldWin64_Data\Managed\Assembly-CSharp.dll" (
    echo ERROR: Assembly-CSharp.dll was not found.
    echo.
    echo Expected:
    echo "%RIMWORLD_DIR%\RimWorldWin64_Data\Managed\Assembly-CSharp.dll"
    echo.
    pause
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: .NET SDK was not found.
    echo.
    echo Install the .NET SDK and try again.
    echo.
    pause
    exit /b 1
)

echo Building...
echo.

dotnet build "Source\BlackIce.csproj" -c Release

if errorlevel 1 (
    echo.
    echo ========================================
    echo BUILD FAILED
    echo ========================================
    echo.
    pause
    exit /b 1
)

echo.
echo ========================================
echo BUILD SUCCESSFUL
echo ========================================
echo.

if not exist "Assemblies" mkdir "Assemblies"

if exist "Assemblies\HackingExpansionBlackIce.dll" (
    echo DLL created:
    echo "Assemblies\HackingExpansionBlackIce.dll"
) else (
    echo WARNING: DLL was not found in Assemblies.
)

echo.
pause