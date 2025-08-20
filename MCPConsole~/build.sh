#!/bin/bash

echo "=================================="
echo "Building MCPConsole..."
echo "=================================="

OUTPUT_DIR="../MCPConsole~"

if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
fi

echo "Deleting old executable files..."
rm -f "$OUTPUT_DIR/MCPConsole" "$OUTPUT_DIR/MCPConsole.pdb" 2>/dev/null

# 检测系统架构
if [[ $(uname -m) == "arm64" ]] || [[ $(uname -m) == "aarch64" ]]; then
    if [[ "$OSTYPE" == "darwin"* ]]; then
        RUNTIME="osx-arm64"
    else
        RUNTIME="linux-arm64"
    fi
else
    if [[ "$OSTYPE" == "darwin"* ]]; then
        RUNTIME="osx-x64"
    else
        RUNTIME="linux-x64"
    fi
fi

echo "Detected runtime: $RUNTIME"

echo "Cleaning previous builds..."
dotnet clean -c Release

echo "Restoring NuGet packages..."
dotnet restore

echo "Building project..."
dotnet build -c Release /p:DebugType=None /p:DebugSymbols=false

echo "Publishing standalone executable..."
dotnet publish -c Release -r "$RUNTIME" --self-contained false /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:DebugType=None /p:DebugSymbols=false -o "$OUTPUT_DIR"

if [ $? -ne 0 ]; then
    echo "Build failed, please check error messages."
    exit 1
fi

# 给可执行文件添加执行权限
chmod +x "$OUTPUT_DIR/MCPConsole"

echo "=================================="
echo "Build completed successfully!"
echo "Executable location: $OUTPUT_DIR/MCPConsole"
echo "=================================="

exit 0
