#!/bin/bash

echo "=================================="
echo "Building MCPConsole..."
echo "=================================="

# 设置dotnet路径
DOTNET_PATH="/usr/local/share/dotnet/dotnet"
if [ ! -f "$DOTNET_PATH" ]; then
    # 尝试其他可能的路径
    if command -v dotnet &> /dev/null; then
        DOTNET_PATH="dotnet"
    elif [ -f "/usr/local/bin/dotnet" ]; then
        DOTNET_PATH="/usr/local/bin/dotnet"
    elif [ -f "/opt/dotnet/dotnet" ]; then
        DOTNET_PATH="/opt/dotnet/dotnet"
    else
        echo "ERROR: dotnet not found. Please install .NET SDK."
        exit 1
    fi
fi

echo "Using dotnet at: $DOTNET_PATH"

OUTPUT_DIR="."
PUBLISH_DIR="./publish_temp"

echo "Deleting old executable files..."
rm -f "$OUTPUT_DIR/MCPConsole" "$OUTPUT_DIR/MCPConsole.pdb" 2>/dev/null
rm -rf "$PUBLISH_DIR" 2>/dev/null

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
"$DOTNET_PATH" clean MCPConsole.csproj -c Release

echo "Restoring NuGet packages..."
"$DOTNET_PATH" restore MCPConsole.csproj 

echo "Building project..."
"$DOTNET_PATH" build MCPConsole.csproj -c Release /p:DebugType=None /p:DebugSymbols=false

echo "Publishing standalone executable..."
"$DOTNET_PATH" publish MCPConsole.csproj -c Release -r "$RUNTIME" --self-contained false /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:DebugType=None /p:DebugSymbols=false -o "$PUBLISH_DIR"

if [ $? -ne 0 ]; then
    echo "Build failed, please check error messages."
    exit 1
fi

cp -f "$PUBLISH_DIR/MCPConsole" "$OUTPUT_DIR/MCPConsole"
if [ -f "$PUBLISH_DIR/MCPConsole.pdb" ]; then
    cp -f "$PUBLISH_DIR/MCPConsole.pdb" "$OUTPUT_DIR/MCPConsole.pdb"
fi

# 给可执行文件添加执行权限
chmod +x "$OUTPUT_DIR/MCPConsole"

echo "=================================="
echo "Build completed successfully!"
echo "Executable location: $OUTPUT_DIR/MCPConsole"
echo "=================================="

exit 0
