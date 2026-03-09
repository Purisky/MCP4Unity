@echo off
echo ==================================
echo Building MCPConsole...
echo ==================================

set OUTPUT_DIR=..\MCPConsole~

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Deleting old executable files...
del /f /q "%OUTPUT_DIR%\MCPConsole.exe" "%OUTPUT_DIR%\MCPConsole.pdb" 2>nul
if exist "%OUTPUT_DIR%\MCPConsole.exe" (
    echo File is locked, terminating process...
    taskkill /f /im MCPConsole.exe 2>nul
    del /f /q "%OUTPUT_DIR%\MCPConsole.exe" "%OUTPUT_DIR%\MCPConsole.pdb" 2>nul
)

echo Cleaning previous builds...
dotnet clean -c Release

echo Restoring NuGet packages...
dotnet restore

echo Building project...
dotnet build -c Release /p:DebugType=None /p:DebugSymbols=false

echo Publishing standalone executable...
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:DebugType=None /p:DebugSymbols=false -o "%OUTPUT_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo Build failed, please check error messages.
    exit /b %ERRORLEVEL%
)

echo ==================================
echo Build completed successfully!
echo Executable location: %OUTPUT_DIR%\MCPConsole.exe
echo ==================================

exit /b 0
