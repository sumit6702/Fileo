@echo off
setlocal
cd /d "%~dp0"

echo Launching Fileo...

if exist "bin\Release\net8.0-windows\Fileo.exe" (
    start "" "bin\Release\net8.0-windows\Fileo.exe"
    exit /b 0
)

if exist "bin\Debug\net8.0-windows\Fileo.exe" (
    start "" "bin\Debug\net8.0-windows\Fileo.exe"
    exit /b 0
)

dotnet run -c Release
