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
  - Newtonsoft.Json

## Quick Start

1. Copy the `MCP4Unity` folder to the `Assets` directory of your Unity project

2. JSON configuration

```json
{
  "mcpServers": {
    "mcp4unity": {
      "command": "[MCPConsole~ folder path]/MCPConsole[.exe]",
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
public static string Echo_Tool([Desc("stringArg description")] string stringArg, [Desc("intArg description")] int intArg)
{
    return $"echo:{stringArg},{intArg}";
}

[Tool("Retrieves the names of all GameObjects in the hierarchy")]
public static string[] Get_All_GameObject_in_Hierarchy([Desc("If true, only top-level GameObjects are returned; otherwise, all GameObjects are returned.")] bool top)
{
    List<string> gameObjectNames = new();

    if (top)
    {
        foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            gameObjectNames.Add(go.name);
        }
    }
    else
    {
        foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
        {
            gameObjectNames.Add(go.name);
        }
    }
    return gameObjectNames.ToArray();
}
```

## Technical Details

- Based on Model Context Protocol SDK
- Automatically discovers and loads tools from all assemblies referencing MCP4Unity
