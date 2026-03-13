# MCP4Unity

- [中文](./README_CN.md) / [English](./README.md)

## Project Overview

MCP4Unity is a Unity Editor extension that exposes Unity methods as MCP (Model Context Protocol) tools, callable by AI agents via HTTP. It bridges the gap between AI coding assistants and the Unity Editor, enabling real-time scene inspection, asset management, compilation, and arbitrary Editor code execution — all without leaving your AI workflow.

## Architecture

```
AI Agent (stdio) ─► Node.js MCP Server (TypeScript) ─► HTTP POST ─► Unity Editor (MCPService)
```

### Components

| Component | Role |
|-----------|------|
| **MCP Server (TypeScript)** | Speaks MCP protocol over stdio with AI agent, translates requests to HTTP calls to Unity. Manages Unity process lifecycle. |
| **MCPService** | Unity Editor-side `[InitializeOnLoad]` singleton. Runs an `HttpListener` on a local port (default 8080, auto-fallback). |
| **EditorMainThread** | Thread marshaling layer. Uses `ConcurrentQueue<Action>` + `EditorApplication.update` to execute Unity API calls on main thread. |
| **MCPFunctionInvoker** | Scans assemblies for `[Tool]`-attributed methods, builds tool registry, invokes by name. |

### Background Stability (Windows)

When Unity Editor is unfocused/minimized, `EditorApplication.update` may not fire frequently. MCP4Unity uses Win32 APIs to wake the editor:

- `PostMessage(WM_NULL)` — nudges the message pump
- `SetTimer` — injects periodic `WM_TIMER` messages
- `InvalidateRect` — triggers a repaint

A managed `System.Threading.Timer` polls every 200ms while work items are queued, ensuring tool calls complete in ~100-200ms even when Unity is in the background.

### Domain Reload Safety

Unity's domain reload (triggered by script recompilation) destroys all managed state. MCP4Unity handles this via:

- `AssemblyReloadEvents.beforeAssemblyReload` — stops HTTP listener before reload
- `EditorApplication.quitting` — stops on editor exit
- `[InitializeOnLoad]` static constructor — restarts service after reload

### AssetImportWorker Guard

Unity 6 spawns `AssetImportWorker` child processes with `-batchMode`. The `[InitializeOnLoad]` static constructor detects this and skips service startup in worker processes, preventing port conflicts.

## Key Features

- **Unity process management**: Start/stop/clean Unity from AI agent
- **Detailed status detection**: Distinguish between not running, batchmode, MCP ready, and MCP unresponsive states
- **Source-embedded**: All TypeScript code in `MCPServer~/mcp4unity/` — modify and rebuild instantly
- Built-in HTTP server with dynamic local port (prefers 8080, auto-fallback)
- Automatic scanning and registration of `[Tool]`-attributed methods
- Tool management UI: **Window > MCP Service Manager**
- Auto-start on editor launch
- Project-isolated endpoint discovery (`Library/MCP4Unity/mcp_endpoint.json`)
- Background stability — tool calls work reliably even when Unity is unfocused
- Domain reload safe — survives script recompilation

## Requirements

### Unity Side
- Unity 2021.3 or later (tested on Unity 6 / 6000.3.10f1)
- Newtonsoft.Json (com.unity.nuget.newtonsoft-json)

### MCP Server Side
- Node.js 18+ (for running the TypeScript MCP server)

## Installation

### For AI Agents

**Automated installation guide**: See [INSTALL_FOR_AGENTS.md](./INSTALL_FOR_AGENTS.md) for step-by-step instructions optimized for AI agents.

### For Human Developers

1. **Clone this repository**:
   ```bash
   git clone https://github.com/Purisky/MCP4Unity.git
   ```

2. **Copy to Unity project**:
   ```bash
   cp -r MCP4Unity /path/to/YourUnityProject/Assets/
   ```

3. **Setup MCP Server**:
   ```bash
   # Copy skill to your MCP client's skill directory
   cp -r Assets/MCP4Unity/MCPServer~/mcp4unity /path/to/skills/
   
   # Install dependencies
   cd /path/to/skills/mcp4unity/server
   npm install
   npm run build
   ```

4. **Configure Unity path**:
   
   Unity automatically generates `unity_config.json` on first launch. If you need to configure manually:
   ```
   configureunity unityExePath="/path/to/Unity.exe"
   ```

5. **Verify installation**:
   ```
   getunitystatus
   ```

For detailed instructions, troubleshooting, and configuration options, see [INSTALL_FOR_AGENTS.md](./INSTALL_FOR_AGENTS.md).

## Quick Start

### Unity Management Tools

```bash
# Configure Unity path (first time only)
configureunity unityExePath="/path/to/Unity.exe"

# Check Unity status
getunitystatus

# Start Unity
startunity

# Stop Unity
stopunity

# Clean Unity cache
deletescriptassemblies
```

### Unity Status Detection

`getunitystatus` returns detailed status:

- `not_running`: Unity process not found
- `batchmode`: Unity running in headless/batchmode (no MCP service)
- `editor_mcp_ready`: Unity Editor running, MCP service responsive
- `editor_mcp_unresponsive`: Unity Editor running but MCP not responding (compiling/loading/blocked)

### Unity Tools

```bash
# Get hierarchy
gethierarchy

# Get active scene
getactivescene

# Find assets
findassets path="Assets/Prefabs"

# Recompile assemblies
recompileassemblies

# Get Unity console log
getunityconsolelog filter="error"
```

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

## TypeScript Server Development

### Rebuilding

```bash
cd Assets/MCP4Unity/MCPServer~/mcp4unity/server
npm run build
```

### Watch Mode

```bash
npm run watch
```

Changes take effect immediately after rebuild.

### Adding New Management Tools

Edit `server/src/index.ts`:
1. Add tool definition to `MANAGEMENT_TOOLS` array
2. Add handler case in `handleManagementTool()` function
3. Rebuild

### Project Structure

```
Assets/MCP4Unity/
├── Editor/                    # Unity Editor scripts
│   ├── MCPService.cs         # HTTP listener
│   ├── MCPFunctionInvoker.cs # Tool registry
│   └── ...
├── MCPServer~/
│   ├── README.md              # TypeScript server documentation
│   └── mcp4unity/             # MCP server skill
│       ├── server/            # TypeScript source
│       ├── mcp.json           # MCP configuration
│       └── SKILL.md           # Full documentation
└── README.md                  # This file
```

## Documentation

- **Full TypeScript Server Documentation**: [MCPServer~/mcp4unity/SKILL.md](./MCPServer~/mcp4unity/SKILL.md)
- **Unity Tool Authoring**: See "Development Tools" section above
- **GitHub Repository**: https://github.com/Purisky/MCP4Unity

## Troubleshooting

### Unity not responding

```bash
# Check detailed status
getunitystatus

# If unresponsive, restart Unity
stopunity
deletescriptassemblies
startunity
```

### MCP server not connecting

1. Verify Node.js is installed: `node --version`
2. Check server build: `cd server && npm run build`
3. Verify Unity is running: `getunitystatus`
4. Check Unity console for errors: **Window > MCP Service Manager**

### Port conflicts

Unity auto-fallbacks from port 8080 if occupied. Check `Library/MCP4Unity/mcp_endpoint.json` for the actual port.

## License

MIT License - see LICENSE file for details

## Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request

For bugs and feature requests, open an issue on GitHub.
