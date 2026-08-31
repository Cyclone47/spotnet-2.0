@echo off
setlocal enabledelayedexpansion

title Spotnet 2.0 - Database Quick-Repair Tool

echo ======================================================================
echo             Spotnet 2.0 Database Quick-Repair Utility                
echo ======================================================================
echo.

set "TOOL_DIR=%~dp0tools\DbRepair"
if not exist "%TOOL_DIR%" (
    set "TOOL_DIR=%~dp0..\..\tools\DbRepair"
)

where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK dotnet CLI was not found in PATH!
    pause
    exit /b 1
)

echo Running database repair and optimization...
echo.
dotnet run --project "%TOOL_DIR%\DbRepair.csproj" -c Release

echo.
pause
