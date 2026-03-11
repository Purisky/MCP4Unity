@echo off
echo ==================================
echo Building MCPConsole...
echo ==================================

set SCRIPT_DIR=%~dp0
set PROJECT_FILE=%SCRIPT_DIR%MCPConsole.csproj
set OUTPUT_DIR=%SCRIPT_DIR%
set PUBLISH_DIR=%SCRIPT_DIR%publish_temp

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"

echo Deleting old executable files...
del /f /q "%OUTPUT_DIR%\MCPConsole.exe" "%OUTPUT_DIR%\MCPConsole.pdb" 2>nul
if exist "%OUTPUT_DIR%\MCPConsole.exe" (
    echo File is locked, terminating process...
    taskkill /f /im MCPConsole.exe 2>nul
    del /f /q "%OUTPUT_DIR%\MCPConsole.exe" "%OUTPUT_DIR%\MCPConsole.pdb" 2>nul
)

echo Cleaning previous builds...
dotnet clean "%PROJECT_FILE%" -c Release

echo Restoring NuGet packages...
dotnet restore "%PROJECT_FILE%"

echo Building project...
dotnet build "%PROJECT_FILE%" -c Release /p:DebugType=None /p:DebugSymbols=false

echo Publishing standalone executable...
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:DebugType=None /p:DebugSymbols=false -o "%PUBLISH_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo Build failed, please check error messages.
    exit /b %ERRORLEVEL%
)

copy /y "%PUBLISH_DIR%\MCPConsole.exe" "%OUTPUT_DIR%\MCPConsole.exe" >nul
if exist "%PUBLISH_DIR%\MCPConsole.pdb" copy /y "%PUBLISH_DIR%\MCPConsole.pdb" "%OUTPUT_DIR%\MCPConsole.pdb" >nul

echo ==================================
echo Build completed successfully!
echo Executable location: %OUTPUT_DIR%\MCPConsole.exe
echo ==================================

exit /b 0
