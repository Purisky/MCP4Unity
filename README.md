# MCP4Unity

- [中文](./README_CN.md)/[English](./README.md)

## Project Overview

MCP4Unity is a Unity Editor extension that exposes Unity methods as MCP (Model Context Protocol) tools, callable by AI agents via HTTP. It bridges the gap between AI coding assistants and the Unity Editor, enabling real-time scene inspection, asset management, compilation, and arbitrary Editor code execution — all without leaving your AI workflow.

## Architecture

```
AI Agent (stdio) ─► MCPConsole.exe (MCP SDK v1.1.0) ─► HTTP POST ─► Unity Editor (HttpListener)
```

| Component | Role |
|-----------|------|
| **MCPConsole.exe** | Standalone .NET 9.0 console app. Speaks MCP protocol over stdio with the AI agent, translates MCP requests to HTTP calls to Unity. |
| **MCPService** | Unity Editor-side `[InitializeOnLoad]` singleton. Runs an `HttpListener` on a local port (default 8080, auto-fallback), receives HTTP requests, dispatches tool invocations. |
| **EditorMainThread** | Thread marshaling layer. `calltool` requests arrive on thread pool threads but most Unity APIs require the main thread. Uses `ConcurrentQueue<Action>` + `EditorApplication.update` to drain work on the main thread. |
| **MCPFunctionInvoker** | Scans all loaded assemblies for `[Tool]`-attributed static methods, builds the tool registry, and invokes them by name. |

### Background Stability (Windows)

When Unity Editor is unfocused/minimized, `EditorApplication.update` may not fire frequently. MCP4Unity uses Win32 APIs to wake the editor:

- `PostMessage(WM_NULL)` — nudges the message pump
- `SetTimer` — injects periodic `WM_TIMER` messages into Unity's message loop
- `InvalidateRect` — triggers a repaint

A managed `System.Threading.Timer` polls every 200ms while work items are queued, and auto-stops when the queue drains. This ensures tool calls complete in ~100-200ms even when Unity is in the background.

### Domain Reload Safety

Unity's domain reload (triggered by script recompilation) destroys all managed state. MCP4Unity handles this via:

- `AssemblyReloadEvents.beforeAssemblyReload` — stops the HTTP listener cleanly before reload
- `EditorApplication.quitting` — stops on editor exit
- `[InitializeOnLoad]` static constructor — restarts the service after reload

### AssetImportWorker Guard

Unity 6 spawns `AssetImportWorker` child processes with `-batchMode`. The `[InitializeOnLoad]` static constructor detects this via `Environment.GetCommandLineArgs()` and skips service startup in worker processes, preventing port conflicts.

## Key Features

- Built-in HTTP server with dynamic local port (prefers 8080, auto-fallback when occupied)
- Automatic scanning and registration of methods with `[Tool]` attribute
- Tool management UI: **Window > MCP Service Manager**
- Auto-start on editor launch
- Project-isolated endpoint discovery (`Library/MCP4Unity/mcp_endpoint.json`)
- Supports either startup order (MCPConsole-first or Unity-first)
- Background stability — tool calls work reliably even when Unity is unfocused
- Domain reload safe — survives script recompilation without manual restart

## Requirements

- Unity 2021.3 or later (tested on Unity 6 / 6000.3.10f1)
  - Newtonsoft.Json (com.unity.nuget.newtonsoft-json)
- .NET 9.0 Runtime (for MCPConsole.exe, unless published self-contained)

## Quick Start

1. Copy the `MCP4Unity` folder to `Assets/` in your Unity project (or add as a git submodule)

2. Build MCPConsole:

```bash
cd Assets/MCP4Unity/MCPConsole~
build.bat
```

This publishes `MCPConsole.exe` to the `MCPConsole~` folder (framework-dependent, requires .NET 9.0 runtime).

3. Configure your AI tool's MCP settings:

```json
{
  "mcpServers": {
    "mcp4unity": {
      "command": "<path-to>/Assets/MCP4Unity/MCPConsole~/MCPConsole.exe"
    }
  }
}
```

4. Open Unity Editor — MCP4Unity starts automatically. Verify via **Window > MCP Service Manager** or check that `Library/MCP4Unity/mcp_endpoint.json` exists.

## Development Tools

Create your own tool methods:

1. Create a C# class in an Editor folder (e.g. `Assets/Editor/MCPTools/`)
2. Add `public static` methods with the `[Tool("description")]` attribute
3. Use `[Desc("description")]` on parameters for documentation
4. Tools are auto-discovered on next compilation — no registration needed

### Example

```csharp
using MCP4Unity;
using UnityEngine;
using UnityEditor;

namespace MCP
{
    public class MyTool
    {
        [Tool("Echo back the input")]
        public static string Echo(
            [Desc("Text to echo")] string text,
            [Desc("Repeat count")] int count = 1)
        {
            return string.Join(", ", Enumerable.Repeat(text, count));
        }

        [Tool("Get all GameObjects in hierarchy")]
        public static string[] GetAllGameObjects(
            [Desc("If true, only root objects")] bool topOnly = false)
        {
            var names = new List<string>();
            if (topOnly)
            {
                foreach (var go in UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene().GetRootGameObjects())
                    names.Add(go.name);
            }
            else
            {
                foreach (var go in Object.FindObjectsByType<GameObject>(
                    FindObjectsSortMode.None))
                    names.Add(go.name);
            }
            return names.ToArray();
        }
    }
}
```

### Tool Authoring Rules

| Rule | Detail |
|------|--------|
| **Signature** | `public static`. Return `string`, `string[]`, `Task<string>`, or any JSON-serializable type. |
| **Attributes** | `[Tool("desc")]` on method (required). `[Desc("desc")]` on params (recommended). `[ParamDropdown("method")]` for enum-like options. |
| **Naming** | MCP tool name = method name lowercased. `GetHierarchy` → `gethierarchy`. |
| **Async** | Return `Task<string>` and use `async`/`await` for long-running operations (e.g. compilation). |
| **Error handling** | Return descriptive error strings. Don't throw exceptions — the MCP bridge serializes the return value directly. |
| **Location** | Project tools in `Assets/Editor/MCPTools/`. Don't modify `Assets/MCP4Unity/` (submodule). |

## MCPConsole Technical Details

- **SDK**: [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) v1.1.0
- **Target**: .NET 9.0, single-file publish
- **Transport**: stdio (MCP protocol) ↔ HTTP (Unity bridge)
- **Proxy bypass**: `HttpClient` configured with `UseProxy = false` to avoid interference from system HTTP proxies
- **Endpoint discovery**: Reads `Library/MCP4Unity/mcp_endpoint.json` from the Unity project to find the HTTP port. Falls back to polling if Unity hasn't started yet.

## Building MCPConsole

```bash
cd MCPConsole~
build.bat
```

`build.bat` performs: clean → restore → build (Release) → publish (win-x64, single-file, framework-dependent, ReadyToRun). Output: `MCPConsole~/MCPConsole.exe`.

To publish self-contained (no .NET runtime dependency):

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true -o .
```
