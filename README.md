# MCP4Unity

- [中文](./README_CN.md)/[English](./README.md)

## Project Overview

MCP4Unity is a Unity editor extension. It allows you to expose Unity methods as tools that can be remotely invoked via HTTP requests, supporting dynamic loading and invocation of tool methods.

## Key Features

- Built-in HTTP server listening on port 8080
- Automatic scanning and registration of methods with `[Tool]` attribute
- Provides tool management interface (Window/MCP Service Manager)
- Supports auto-start

## Requirements

- Unity 2021.3 or later
- Node.js 18 or later
- npm 9 or later

## Quick Start

1. Copy the `MCP4Unity` folder to the `Assets` directory of your Unity project
2. Navigate to the Assets directory

```bash
cd MCP4Unity/mcp.ts
npm install
npm run build
```

3. JSON configuration

```json
{
  "mcpServers": {
    "mcp4unity": {
      "command": "node",
      "args": ["[mcp.ts folder path]/build/index.js"],
      "env": {},
      "disabled": false,
      "autoApprove": []
    },
  }
}
```

## Development Tools

Create your own tool methods:

1. Create static methods in any class
2. Mark methods with `[Tool]` attribute
3. Use `[Tool]` attribute to add descriptions for parameters

Example:

```csharp
[Tool("Echo description")]
public static string Echo_Tool([Tool("stringArg description")] string stringArg, [Tool("intArg description")] int intArg)
{
    return $"echo:{stringArg},{intArg}";
}
```

## Technical Details

- Based on Model Context Protocol SDK
- Developed using C# and TypeScript
- Automatically discovers and loads tools from all assemblies referencing MCP4Unity
