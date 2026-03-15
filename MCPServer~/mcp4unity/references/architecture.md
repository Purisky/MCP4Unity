# Architecture & Internals

Technical details about MCP4Unity's implementation and communication flow.

## System Overview

```
AI Agent (stdio) ─► Node.js MCP Server (TypeScript) ─► HTTP POST ─► Unity Editor (MCPService)
```

### Components

| Component | Technology | Role |
|-----------|-----------|------|
| **AI Agent** | Claude/OpenCode | Initiates tool calls via MCP protocol |
| **MCP Server** | Node.js + TypeScript | Translates MCP stdio ↔ HTTP |
| **Unity MCPService** | C# + HttpListener | Receives HTTP, executes tools on main thread |

---

## TypeScript MCP Server

**Location**: `{SKILL_ROOT}/server/`

**Key Files**:
- `src/index.ts` - MCP server entry point, stdio transport
- `src/unity-client.ts` - HTTP client for Unity communication
- `src/unity-manager.ts` - Unity process management (start/stop/clean)

**Build**:
```bash
cd {SKILL_ROOT}/server
npm run build
```

**Configuration**:
- Unity paths stored in `{SKILL_ROOT}/unity_config.json`
- Endpoint discovery via `Library/MCP4Unity/mcp_endpoint.json`

**HTTP Client Settings**:
```typescript
axios.post(url, data, {
  proxy: false,  // Bypass system HTTP proxy
  timeout: 30000 // 30-second timeout
})
```

---

## Unity MCPService

**Location**: `Assets/MCP4Unity/Runtime/MCPService.cs`

**Initialization**:
```csharp
[InitializeOnLoad]
public class MCPService
{
    static MCPService()
    {
        // Auto-start on Unity load
        StartService();
    }
}
```

**HTTP Listener**:
- Runs on `http://127.0.0.1:{mcpPort}/mcp/` (default port: 52429)
- Listens for POST requests with JSON payloads
- Two endpoints: `listtools` and `calltool`

**Thread Model**:
- HTTP listener runs on thread pool
- `listtools` executes immediately on thread pool
- `calltool` queues to main thread via `EditorMainThread`

---

## Data Flow

### 1. Tool Discovery (listtools)

```
AI Agent → MCP Server → HTTP POST /mcp/ {"method":"listtools"}
  ↓
Unity MCPService (thread pool)
  ↓
MCPFunctionInvoker.GetTools() - Reflection scan for [Tool] attributes
  ↓
Return JSON array of tool definitions
  ↓
MCP Server → AI Agent
```

**Performance**: Instant (cached after first call)

### 2. Tool Execution (calltool)

```
AI Agent → MCP Server → HTTP POST /mcp/ {"method":"calltool", "name":"...", "arguments":{...}}
  ↓
Unity MCPService (thread pool)
  ↓
EditorMainThread.RunAsync(() => {
    MCPFunctionInvoker.Invoke(toolName, args)
})
  ↓
Queue action to main thread
  ↓
EditorApplication.update callback (main thread)
  ↓
Execute tool method
  ↓
Return result
  ↓
MCP Server → AI Agent
```

**Latency**:
- Focused Unity: ~50ms
- Background Unity: ~100-200ms
- Timeout: 25 seconds

---

## EditorMainThread Queue

**Purpose**: Execute code on Unity's main thread from background threads.

**Implementation**:
```csharp
private static ConcurrentQueue<Action> _actions = new();

public static Task<T> RunAsync<T>(Func<T> action)
{
    var tcs = new TaskCompletionSource<T>();
    _actions.Enqueue(() => {
        try { tcs.SetResult(action()); }
        catch (Exception ex) { tcs.SetException(ex); }
    });
    WakeUnity(); // Win32 wake mechanism
    return tcs.Task;
}
```

**Update Loop**:
```csharp
[InitializeOnLoad]
static class EditorMainThread
{
    static EditorMainThread()
    {
        EditorApplication.update += ProcessQueue;
    }
    
    static void ProcessQueue()
    {
        while (_actions.TryDequeue(out var action))
        {
            action();
        }
    }
}
```

---

## Background Wake Mechanism (Windows)

**Problem**: Unity's `EditorApplication.update` runs infrequently when Unity is in background.

**Solution**: Win32 API calls to wake Unity's message loop.

```csharp
[DllImport("user32.dll")]
static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

[DllImport("user32.dll")]
static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

[DllImport("user32.dll")]
static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

static void WakeUnity()
{
    var hwnd = FindWindow("UnityWndClass", null);
    if (hwnd != IntPtr.Zero)
    {
        PostMessage(hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        InvalidateRect(hwnd, IntPtr.Zero, false);
    }
}
```

**Effect**: Reduces background latency from 500ms+ to ~100-200ms.

---

## Endpoint Discovery

**File**: `Library/MCP4Unity/mcp_endpoint.json`

**Format**:
```json
{
  "port": 52429,
  "pid": 12345,
  "timestamp": "2026-03-14T10:30:00Z"
}
```

**Lifecycle**:
- **Created**: When MCPService starts successfully
- **Updated**: On every service restart
- **Deleted**: When Unity quits or service stops
- **Validated**: `getunitystatus` checks PID matches running process

**Why in Library/**:
- Project-isolated (multiple Unity projects don't interfere)
- Gitignored by default
- Cleaned on Library folder deletion

---

## Domain Reload Handling

**Problem**: Unity reloads assemblies on script changes, stopping all static state.

**Solution**: Lifecycle hooks.

```csharp
[InitializeOnLoad]
public class MCPService
{
    static MCPService()
    {
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
        StartService();
    }
    
    static void OnBeforeReload()
    {
        StopService(); // Clean shutdown before reload
    }
}
```

**After reload**: `[InitializeOnLoad]` constructor runs again, restarting service.

---

## AssetImportWorker Guard

**Problem**: Unity 6 spawns worker processes for asset import. If they try to start MCPService, port conflicts occur.

**Solution**: Detect worker process via command-line args.

```csharp
static bool IsAssetImportWorker()
{
    var args = Environment.GetCommandLineArgs();
    return args.Any(a => a.Contains("AssetImportWorker"));
}

static MCPService()
{
    if (IsAssetImportWorker()) return; // Skip service startup
    StartService();
}
```

---

## Tool Invocation

**Reflection-based dispatch**:

```csharp
public static object Invoke(string toolName, Dictionary<string, object> args)
{
    // 1. Find method by name (case-insensitive)
    var method = FindToolMethod(toolName);
    
    // 2. Match arguments to parameters
    var parameters = MapArguments(method, args);
    
    // 3. Invoke
    var result = method.Invoke(null, parameters);
    
    // 4. Handle async
    if (result is Task task)
    {
        task.Wait();
        result = GetTaskResult(task);
    }
    
    return result;
}
```

**Async handling**:
```csharp
if (method.ReturnType.IsGenericType && 
    method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
{
    var task = (Task)method.Invoke(null, parameters);
    await task;
    var resultProperty = task.GetType().GetProperty("Result");
    return resultProperty.GetValue(task);
}
```

---

## Error Handling

**HTTP Level**:
- Connection refused → Unity not running
- Timeout → Unity frozen or main thread blocked
- 500 error → Tool execution exception

**Tool Level**:
- Return error strings instead of throwing
- Exceptions caught and serialized to JSON

**Timeout**:
- `EditorMainThread.RunAsync` has 25-second timeout
- Prevents indefinite hangs on modal dialogs or long operations

---

## Performance Characteristics

| Operation | Latency | Notes |
|-----------|---------|-------|
| `listtools` | <10ms | Thread pool, cached |
| `calltool` (focused) | ~50ms | Main thread queue |
| `calltool` (background) | ~100-200ms | Win32 wake + queue |
| Async tool | Variable | Depends on operation |
| Timeout | 25s | Hard limit |

---

## Security Considerations

**Localhost only**:
- HTTP listener binds to `127.0.0.1` (not `0.0.0.0`)
- No external network access

**No authentication**:
- Assumes local machine is trusted
- MCP server and Unity run on same machine

**Arbitrary code execution**:
- `runcode` tool can execute any static method
- Intended for trusted AI agents only

---

## Debugging

**Enable verbose logging** (Unity Console):
```csharp
// In MCPService.cs
private const bool DEBUG = true;
```

**Test HTTP directly**:
```bash
curl --noproxy "*" -X POST http://127.0.0.1:52429/mcp/ \
  -H "Content-Type: application/json" \
  -d '{"method":"listtools"}'
```

Note: Replace `52429` with your configured port if different.

**Check endpoint file**:
```bash
cat Library/MCP4Unity/mcp_endpoint.json
```

**Monitor process**:
```bash
# Windows
tasklist | findstr Unity

# Linux/Mac
ps aux | grep Unity
```

---

## Extending the System

**Add new management tools** (no Unity running):
- Implement in `server/src/unity-manager.ts`
- Register in `server/src/index.ts`

**Add new Unity tools** (requires Unity):
- Create in `Assets/Editor/MCPTools/`
- Use `[Tool]` attribute
- See `references/authoring-guide.md`

**Modify communication protocol**:
- Edit `server/src/unity-client.ts` (TypeScript side)
- Edit `Assets/MCP4Unity/Runtime/MCPService.cs` (Unity side)
- Keep JSON format compatible
