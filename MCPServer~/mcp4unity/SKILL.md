---
name: mcp4unity
description: (project - Skill) MCP4Unity framework for Unity Editor interaction. Includes Unity management (start/stop/clean), compilation workflow, scene/asset/hierarchy manipulation, component editing, property modification, and tool authoring. Use for: compile/build/fix errors, start/stop Unity, inspect/modify hierarchy/assets/components, create/delete GameObjects, add/remove components, set properties, manage scenes, run editor code, create MCP tools. Triggers: 'compile', 'build', 'start unity', 'stop unity', 'hierarchy', 'scene', 'GameObject', 'component', 'property', 'find asset', 'create object', 'add component', 'set property', 'MCP tool'.
mcp:
  mcp4unity:
    type: local
    enabled: true
    command: "node"
    args: ["build/index.js"]
    cwd: "{SKILL_ROOT}/server"
---

# MCP4Unity - Unity Editor Integration

MCP4Unity exposes Unity Editor methods as MCP tools, enabling AI agents to interact with Unity in real-time.

## Architecture (TypeScript Implementation)

```
AI Agent (stdio) ─► Node.js MCP Server (TypeScript) ─► HTTP POST ─► Unity Editor (MCPService)
```

| Component | Role |
|-----------|------|
| **server/src/index.ts** | MCP server entry point. Handles stdio transport, tool registration, and request routing. |
| **server/src/unity-client.ts** | HTTP client for Unity communication. Reads `Library/MCP4Unity/mcp_endpoint.json`, forwards tool calls to Unity. |
| **server/src/unity-manager.ts** | Unity process management. Handles start/stop/clean operations without Unity running. |
| **Unity MCPService** | Unity Editor-side HTTP listener (unchanged from C# version). |

### Why TypeScript?

- **Source-embedded**: All code in `server/` directory — modify and rebuild instantly
- **No compilation dependency**: Only requires Node.js, no .NET SDK needed
- **Ecosystem alignment**: Matches mainstream MCP implementations, easier to reference and extend
- **Debugging**: Native VS Code support with breakpoints

## Unity Management Tools

These tools work even when Unity is not running:

| Tool | Parameters | Description |
|------|-----------|-------------|
| `configureunity` | `unityExePath`, `projectPath` | Configure Unity paths (first-time setup) |
| `startunity` | none | Start Unity (auto-cleans backups & assemblies) |
| `stopunity` | none | Force close Unity |
| `isunityrunning` | none | Check if Unity is running (simple boolean) |
| `getunitystatus` | none | Get detailed Unity status with diagnostics |
| `deletescenebackups` | none | Delete scene recovery files |
| `deletescriptassemblies` | none | Delete ScriptAssemblies cache |

**First-time setup**:
```
configureunity unityExePath="/path/to/Unity.exe"
```

> **Note**: `projectPath` is optional and auto-detected from the current working directory. The server automatically searches upward for the Unity project root (containing `Assets/` and `Library/` folders).

> **Configuration Storage**: Unity paths are stored in `{SKILL_ROOT}/unity_config.json` (auto-created on first use).

### Unity Status Detection

`getunitystatus` returns detailed status information:

**Status Values:**
- `not_running`: Unity process not found
- `batchmode`: Unity running in headless/batchmode (no MCP service)
- `editor_mcp_ready`: Unity Editor running, MCP service responsive
- `editor_mcp_unresponsive`: Unity Editor running but MCP not responding (compiling/loading/blocked)

**Response Format:**
```json
{
  "status": "editor_mcp_ready",
  "message": "Unity Editor is running and MCP service is ready",
  "processRunning": true,
  "endpointExists": true,
  "mcpResponsive": true,
  "batchMode": false
}
```

**Use Cases:**
- Before calling Unity tools, check if MCP is ready
- Detect compilation/loading issues (editor_mcp_unresponsive)
- Distinguish between batchmode and editor mode
- Validate stale endpoint files (checks process PID)

## Compilation Workflow

### Detection: Is Unity Running?

Use `getunitystatus` to check Unity status:
- **`editor_mcp_ready`** → use MCP tools (`recompileassemblies`)
- **`editor_mcp_unresponsive`** → wait or restart Unity
- **`not_running`** → use `startunity` first
- **`batchmode`** → wait for batchmode to complete

### MCP Strategy (Unity Running)

1. `getunitystatus` - Check if MCP is ready
2. `recompileassemblies` - Trigger compilation
3. If errors: `getunityconsolelog(filter="error")` for details
4. Fix errors
5. Repeat until clean

### Troubleshooting

**MCP tools timeout**:
1. `getunitystatus` - Check detailed status
2. If `editor_mcp_unresponsive`: wait for compilation/loading to complete
3. If still unresponsive after 5min: `stopunity` → `deletescriptassemblies` → `startunity`

**Unity hangs >5min**:
1. `stopunity`
2. `deletescriptassemblies`
3. `startunity`

## Available Tools

### Code Tools (`Assets/Editor/MCPTools/CodeTool.cs`)

| Tool | Parameters | Returns |
|------|-----------|---------|
| `recompileassemblies` | none | Triggers script compilation, waits for completion, returns errors/warnings with file paths and line numbers |
| `getunityconsolelog` | none | Last 10 Unity Console entries with type (error/warning/log), message, and file location |
| `runcode` | `typeDotMethod` (string) — fully-qualified static method name | Invokes any parameterless static method and returns its result. Example: `"UnityEditor.AssetDatabase.Refresh"` |

### Scene Tools (`Assets/Editor/MCPTools/SceneTool.cs`)

| Tool | Parameters | Returns |
|------|-----------|---------|
| `getactivescene` | none | Active scene name, path, dirty state, root object count, and list of all loaded scenes |
| `gethierarchy` | `topOnly` (bool, default false) — root objects only; `maxDepth` (int, default 0) — tree depth limit (0 = unlimited) | Full hierarchy tree with indentation showing parent-child relationships |
| `getgameobjectinfo` | `nameOrPath` (string) — object name or hierarchy path like `"Canvas/Panel/Button"` | Transform (pos/rot/scale), tag, layer, active state, children list, and all component type names |
| `savescene` | none | Saves current active scene to its existing path |
| `savesceneas` | `path` (string) — save path (e.g., 'Assets/Scenes/NewScene.unity') | Saves current scene to specified path |
| `loadscene` | `scenePathOrName` (string) — scene path or name; `mode` (string, default "Single") — "Single" or "Additive" | Loads scene, replacing current or adding to loaded scenes |
| `createscene` | `setup` (string, default "DefaultGameObjects") — "Empty" or "DefaultGameObjects"; `mode` (string, default "Single") — "Single" or "Additive" | Creates new scene with specified setup |
| `closescene` | `sceneName` (string) — scene name; `removeScene` (bool, default true) — remove or just unload | Closes specified scene (cannot close last scene) |
| `setactivescene` | `sceneName` (string) — scene name | Sets specified loaded scene as active |

### Asset Tools (`Assets/Editor/MCPTools/AssetTool.cs`)

| Tool | Parameters | Returns |
|------|-----------|---------|
| `findassets` | `filter` (string) — Unity search filter; `folder` (string, optional) — scope folder path | Asset paths matching the filter. Filter syntax: `t:Texture2D`, `l:MyLabel`, `MyAssetName`, or combinations |
| `getassetinfo` | `assetPath` (string) — e.g. `"Assets/Sprites/hero.png"` | Type, GUID, file size, main asset type, and dependency list. For textures: width/height. For GameObjects: component summary |
| `importasset` | `assetPath` (string); `options` (string, default `"Default"`) — ImportAssetOptions enum name | Force reimport an asset. Options: `Default`, `ForceUpdate`, `ForceSynchronousImport`, `ImportRecursive` |

### Component Tools (`Assets/Editor/MCPTools/ComponentTool.cs`)

| Tool | Parameters | Returns |
|------|-----------|---------|
| `getcomponents` | `name` (string) — GameObject name or hierarchy path | All components on the object with their type names and enabled/disabled state |
| `getserializedproperties` | `name` (string) — GameObject name or hierarchy path; `componentNameOrIndex` (string) — component type name or index | All serialized property names, types, and current values via SerializedObject API |

### Hierarchy Tools (`Assets/Editor/MCPTools/HierarchyTool.cs`)

| Tool | Parameters | Returns |
|------|-----------|---------|
| `addcomponent` | `nameOrPath` (string) — GameObject name or hierarchy path; `componentTypeName` (string) — full component type name (e.g., 'UnityEngine.BoxCollider') | Adds component to GameObject, returns success/error message |
| `removecomponent` | `nameOrPath` (string) — GameObject name or hierarchy path; `componentNameOrIndex` (string) — component type name or index | Removes component from GameObject (cannot remove Transform) |
| `creategameobject` | `name` (string) — new GameObject name; `parentNameOrPath` (string, optional) — parent object path (empty = root) | Creates new GameObject in scene, optionally parented |
| `deletegameobject` | `nameOrPath` (string) — GameObject name or hierarchy path | Deletes GameObject from scene |
| `setactive` | `nameOrPath` (string) — GameObject name or hierarchy path; `active` (bool) — activation state | Sets GameObject active/inactive state |
| `setparent` | `childNameOrPath` (string) — child object path; `parentNameOrPath` (string, optional) — parent path (empty = root); `worldPositionStays` (bool, default true) — preserve world coordinates | Changes GameObject parent |
| `setname` | `nameOrPath` (string) — GameObject name or hierarchy path; `newName` (string) — new name | Renames GameObject |
| `duplicategameobject` | `nameOrPath` (string) — GameObject name or hierarchy path | Duplicates GameObject with all components and children |

### Property Tools (`Assets/Editor/MCPTools/PropertyTool.cs`)

| Tool | Parameters | Returns |
|------|-----------|---------|
| `setserializedproperty` | `nameOrPath` (string) — GameObject name or hierarchy path; `componentNameOrIndex` (string) — component type name or index; `propertyPath` (string) — property path (e.g., 'm_LocalPosition.x', 'm_Size'); `value` (string) — new value (auto-converted to correct type) | Sets single serialized property value on component |
| `setmultipleproperties` | `nameOrPath` (string) — GameObject name or hierarchy path; `componentNameOrIndex` (string) — component type name or index; `propertyPaths` (string) — comma-separated property paths; `values` (string) — comma-separated values | Batch sets multiple properties at once |

## Common Workflows

### Inspect a GameObject

1. `gethierarchy` with `topOnly=true` to see root objects
2. `getgameobjectinfo` with the target name to see transform + components
3. `getcomponents` to list all components with enabled state
4. `getserializedproperties` to read specific component field values

### Modify GameObject and Components

1. `creategameobject` to create new object (optionally parented)
2. `addcomponent` to add components (use full type name like 'UnityEngine.BoxCollider')
3. `setserializedproperty` to modify single property value
4. `setmultipleproperties` to batch modify multiple properties
5. `setactive` / `setparent` / `setname` for common operations
6. `duplicategameobject` to clone objects with all components
7. `removecomponent` / `deletegameobject` to clean up

### Find and Examine Assets

1. `findassets` with filter like `t:ScriptableObject MyConfig` or `t:Prefab Player`
2. `getassetinfo` on the result path to see type, size, dependencies

### Scene Management

1. `getactivescene` to check current scene status
2. `savescene` / `savesceneas` to save changes
3. `loadscene` to load another scene (Single or Additive mode)
4. `createscene` to create new empty or default scene
5. `closescene` / `setactivescene` for multi-scene workflows

### Compile and Fix

Use the `unity-compile-fix` skill instead — it wraps these tools with a full fix loop. But for one-off compilation checks:

1. `recompileassemblies` to trigger compilation
2. `getunityconsolelog` to see console output if needed

### Execute Arbitrary Editor Code

`runcode` can invoke any public static parameterless method. Examples:
- `"UnityEditor.AssetDatabase.Refresh"` — refresh asset database
- `"UnityEditor.EditorApplication.ExecuteMenuItem"` — not directly (has params), but any custom `[MenuItem]` handler that is parameterless works

## Authoring New MCP Tools

New tools go in `Assets/Editor/MCPTools/`, organized by module. Each file is a plain C# class (no MonoBehaviour) in the `MCP` namespace.

### Minimal Template

```csharp
using MCP4Unity;
using UnityEngine;
using UnityEditor;

namespace MCP
{
    public class MyModuleTool
    {
        [Tool("Brief description of what this tool does")]
        public static string MyToolName(
            [Desc("What this parameter is for")] string param1,
            [Desc("Optional parameter")] int param2 = 0)
        {
            // Use any UnityEditor API here
            return "result string";
        }
    }
}
```

### Rules

1. **Method signature**: Must be `public static`. Any class in any assembly that references MCP4Unity.Runtime works, but project tools go in `Assets/Editor/MCPTools/`.
2. **Attributes**: `[Tool("description")]` on the method (required). `[Desc("description")]` on parameters (recommended). `[ParamDropdown("staticMethodName")]` for enum-like parameter options (optional).
3. **Return types**: `string`, `string[]`, `Task<string>`, or any JSON-serializable type. For async operations, return `Task<string>` and use `async`/`await`.
4. **Tool naming**: MCP tool name = method name lowercased. `GetHierarchy` becomes `gethierarchy`.
5. **No modifications to `Assets/MCP4Unity/`**: This is a submodule. All custom tools live in `Assets/Editor/MCPTools/`.
6. **File organization**: Group related tools in one file named `{Module}Tool.cs`. Current modules: Code, Scene, Asset, Component.
7. **Error handling**: Return descriptive error strings rather than throwing exceptions. The MCP bridge serializes the return value as the tool response.
8. **Editor-only**: All tool files live in an Editor folder, so they have full access to `UnityEditor` APIs but won't be included in builds.

### Async Example (Compilation)

```csharp
[Tool("Recompile and wait for result")]
public static async Task<string> RecompileAssemblies()
{
    var tcs = new TaskCompletionSource<bool>();
    CompilationPipeline.compilationFinished += _ => tcs.TrySetResult(true);
    CompilationPipeline.RequestScriptCompilation();
    await tcs.Task;
    // ... collect and return results
}
```

### Dropdown Parameter Example

```csharp
public static string[] GetImportOptions()
{
    return new[] { "Default", "ForceUpdate", "ForceSynchronousImport" };
}

[Tool("Import an asset")]
public static string ImportAsset(
    [Desc("Asset path")] string assetPath,
    [ParamDropdown("GetImportOptions")][Desc("Import options")] string options = "Default")
{
    // ...
}
```

## Architecture & Internals

Understanding the internal architecture helps when debugging connectivity or latency issues.

### Data Flow

```
AI Agent (stdio) ─► Node.js MCP Server (TypeScript) ─► HTTP POST ─► Unity Editor (HttpListener :8080)
```

- **Node.js MCP Server**: TypeScript implementation in `.opencode/skills/mcp4unity/server/`. Translates MCP stdio ↔ HTTP. Built with `npm run build`. Uses `axios` with `proxy: false` to bypass system HTTP proxies.
- **MCPService**: `[InitializeOnLoad]` singleton in Unity. Runs `HttpListener` on local port (default 8080). Dispatches `listtools` on thread pool directly, routes `calltool` through `EditorMainThread` for main-thread execution.
- **EditorMainThread**: `ConcurrentQueue<Action>` drained by `EditorApplication.update`. Win32 wake mechanism (`PostMessage` + `SetTimer` + `InvalidateRect`) ensures ~100-200ms response even when Unity is unfocused/background.

### Two categories of MCP calls

| Category | Path | Thread | Latency |
|----------|------|--------|---------|
| `listtools` | `MCPFunctionInvoker.GetTools()` | Thread pool | Instant |
| `calltool` | `EditorMainThread.RunAsync(...)` → main thread queue | Main thread | ~100-200ms (background), ~50ms (focused) |

### Reliability Features

- **Domain Reload**: `AssemblyReloadEvents.beforeAssemblyReload` stops listener before reload; `[InitializeOnLoad]` restarts after.
- **AssetImportWorker Guard**: Detects Unity 6 worker processes via command-line args, skips service startup to prevent port conflicts.
- **Background Stability**: Win32 `SetTimer`/`InvalidateRect`/`PostMessage(WM_NULL)` wake mechanism. Auto-starts when queue has items, auto-stops when drained.
- **25s Timeout**: `EditorMainThread.RunAsync` has a timeout — if main thread is blocked (modal dialog, compilation), the request fails with `TimeoutException` instead of hanging forever.

### Endpoint Discovery

The TypeScript server reads `Library/MCP4Unity/mcp_endpoint.json` to find Unity's HTTP port. This file is:
- **Created** when MCPService starts successfully
- **Deleted** when MCPService stops or editor quits
- **Project-isolated** — stored in `Library/` so multiple Unity projects don't interfere

### Troubleshooting

If MCP tools are unresponsive:
1. Check `Library/MCP4Unity/mcp_endpoint.json` exists — if not, MCP service isn't running
2. Check Unity Console for errors — `MCPService` logs startup/shutdown events
3. Verify port isn't blocked: `curl --noproxy "*" -s -m 5 -X POST http://127.0.0.1:8080/mcp/ -H "Content-Type: application/json" -d "{\"method\":\"listtools\"}"`
4. If `listtools` works but `calltool` times out: Unity main thread is likely blocked (modal dialog, long compilation, domain reload in progress)
5. Check for proxy interference — TypeScript server uses `proxy: false` but manual curl tests need `--noproxy "*"`

## Development & Modification

### Rebuilding the TypeScript Server

```bash
cd {SKILL_ROOT}/server
npm run build
```

Changes take effect immediately after rebuild — no need to restart the AI agent.

### Project Structure

```
{SKILL_ROOT}/
├── server/
│   ├── src/
│   │   ├── index.ts           # MCP server entry point
│   │   ├── unity-client.ts    # Unity HTTP communication
│   │   └── unity-manager.ts   # Unity process management
│   ├── build/                 # Compiled JavaScript (generated)
│   ├── package.json
│   └── tsconfig.json
├── unity_config.json          # Unity paths configuration (auto-created)
├── mcp.json                   # MCP server configuration
└── SKILL.md                   # This file
```

## Project Context

- **Unity version**: 6000.3.10f1 (Unity 6)
- **MCP4Unity submodule**: `Assets/MCP4Unity/` (git submodule, Unity-side implementation unchanged)
- **Custom tools location**: `Assets/Editor/MCPTools/`
- **TypeScript server**: `{SKILL_ROOT}/server/` (source-embedded, modify freely)
- **Configuration file**: `{SKILL_ROOT}/unity_config.json` (Unity paths, auto-created)
- **MCP SDK**: @modelcontextprotocol/sdk v1.0.4, Node.js
- **MCP config**: Configured in `{SKILL_ROOT}/mcp.json`
