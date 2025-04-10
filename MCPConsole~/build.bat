@echo off
echo ==================================
echo Building MCPConsole...
echo ==================================

:: Set output directory
set OUTPUT_DIR=..\MCPConsole~

:: Ensure output directory exists
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

:: Clean previous builds
echo Cleaning previous builds...
dotnet clean -c Release

:: Restore package dependencies
echo Restoring NuGet packages...
dotnet restore

:: Build the project
echo Building project...
dotnet build -c Release

:: Publish the project (single file, self-contained)
echo Publishing standalone executable...
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true /p:PublishReadyToRun=true -o "%OUTPUT_DIR%"

:: Check build result
if %ERRORLEVEL% NEQ 0 (
    echo Build failed, please check error messages.
    exit /b %ERRORLEVEL%
)

echo ==================================
echo Build completed successfully!
echo Executable location: %OUTPUT_DIR%\MCPConsole.exe
echo ==================================

:: Open output directory
explorer "%OUTPUT_DIR%"

exit /b 0