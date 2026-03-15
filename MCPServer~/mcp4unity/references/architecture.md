# Architecture & Internals

Technical implementation details of MCP4Unity.

## System Overview

```
AI Agent (stdio) ─► Node.js MCP Server ─► HTTP POST ─► Unity Editor (MCPService)
```

| Component | Technology | Role |
|-----------|-----------|------|
| **AI Agent** | Claude/OpenCode | Initiates tool calls via MCP |
| **MCP Server** | Node.js + TypeScript | Translates stdio ↔ HTTP |
| **Unity MCPService** | C# + HttpListener | Executes tools on main thread |

---

## TypeScript MCP Server

**Location**: `{SKILL_ROOT}/server/`

**Key Files**:
- `index.ts` - MCP stdio transport
- `unity-client.ts` - HTTP client
- `unity-manager.ts` - Process management

**Configuration**:
- Unity paths: `unity_config.json`
- State files (in `Library/MCP4Unity/`):
  - `mcp_endpoint.json` - Process marker (PID, ProjectPath, StartedAtUtc)
  - `mcp_alive.json` - Heartbeat (Port, ConnectedClients[], updated every 1s, 3s timeout)

---

## Unity MCPService

**Location**: `Assets/MCP4Unity/Editor/MCPService.cs`

**Auto-start**: `[InitializeOnLoad]` static constructor

**HTTP Listener**:
- Endpoint: `http://127.0.0.1:{port}/mcp/` (default: 52429)
- Methods: `listtools`, `calltool`

**Thread Model**:
- HTTP listener: thread pool
- `listtools`: immediate (thread pool)
- `calltool`: queued to main thread via `EditorMainThread`

---

## Data Flow

### Tool Discovery (listtools)
```
AI Agent → MCP Server → HTTP POST {"method":"listtools"}
  ↓
MCPFunctionInvoker.GetTools() (reflection scan for [Tool])
  ↓
Return JSON tool definitions
```

### Tool Execution (calltool)
```
AI Agent → MCP Server → HTTP POST {"method":"calltool", "name":"...", "arguments":{}}
  ↓
EditorMainThread.RunAsync(() => MCPFunctionInvoker.Invoke(...))
  ↓
Queue to main thread → EditorApplication.update callback
  ↓
Execute tool → Return result
```

**Latency**: 50ms (focused) / 100-200ms (background) | Timeout: 25s

---

## EditorMainThread Queue

Executes code on Unity's main thread from background threads.

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
    WakeUnity(); // Win32 wake
    return tcs.Task;
}
```

**Update Loop**:
```csharp
[InitializeOnLoad]
static EditorMainThread()
{
    EditorApplication.update += ProcessQueue;
}

static void ProcessQueue()
{
    while (_actions.TryDequeue(out var action))
        action();
}
```

---

## Background Stability (Windows)

When Unity is unfocused/minimized, `EditorApplication.update` may not fire. Win32 APIs wake the editor:

```csharp
[DllImport("user32.dll")]
static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

[DllImport("user32.dll")]
static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

static void WakeUnity()
{
    var hwnd = Process.GetCurrentProcess().MainWindowHandle;
    PostMessage(hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
    SetTimer(hwnd, IntPtr.Zero, 100, IntPtr.Zero);
    InvalidateRect(hwnd, IntPtr.Zero, false);
}
```

**Effect**: Reduces background latency from 500ms+ to ~100-200ms.

---

## State File System

### mcp_endpoint.json - Process Marker
```json
{
  "Pid": 12345,
  "ProjectPath": "E:\\Path\\To\\Project",
  "StartedAtUtc": "2026-03-15T12:34:14Z"
}
```
**Lifecycle**: Created on start, deleted on graceful stop
**Purpose**: Process identity verification

### mcp_alive.json - Heartbeat
```json
{
  "Port": 52429,
  "ConnectedClients": ["127.0.0.1:50880"]
}
```
**Lifecycle**: Created on start, updated every 1s, deleted on stop
**Purpose**: Heartbeat detection (3s timeout) + port discovery

**Heartbeat Detection**:
- TypeScript reads file mtime (`fs.statSync(file).mtime`)
- If `now - mtime > 3000ms`, service is dead
- Detects frozen Unity main thread

**Why Dual Files**:
- `mcp_endpoint.json` - Stable identity (PID, startup time)
- `mcp_alive.json` - Dynamic state (port, clients, heartbeat)

---

## Domain Reload Handling

Unity reloads assemblies on script changes. Lifecycle hooks handle this:

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
        StopService(); // Clean shutdown
    }
}
```

**Zombie Listener Detection**:
```csharp
private static bool IsEndpointStale(int boundPort)
{
    var alive = ReadAliveState();
    return alive == null || alive.Port != boundPort;
}

// Listener loop checks staleness every 2s
while (!IsEndpointStale(boundPort))
{
    // Process requests
}
// Self-terminate if stale
```

---

## MCPFunctionInvoker

Discovers and invokes `[Tool]`-attributed methods via reflection.

**Tool Discovery**:
```csharp
public static List<ToolDefinition> GetTools()
{
    var tools = new List<ToolDefinition>();
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = method.GetCustomAttribute<ToolAttribute>();
                if (attr != null)
                    tools.Add(CreateToolDefinition(method, attr));
            }
        }
    }
    return tools;
}
```

**Tool Invocation**:
```csharp
public static object Invoke(string toolName, Dictionary<string, object> args)
{
    var method = _toolRegistry[toolName];
    var parameters = MapParameters(method, args);
    return method.Invoke(null, parameters);
}
```

**Async Support**:
```csharp
if (method.ReturnType == typeof(Task<string>))
{
    var task = (Task<string>)method.Invoke(null, parameters);
    return await task;
}
```

---

## Port Allocation

**Strategy**: Prefer configured port, fallback on conflict.

```csharp
public void Start()
{
    int port = GetConfiguredPort(); // From unity_config.json
    if (!TryStartHttpListener(port))
    {
        // Port in use, try fallback
        for (int i = 1; i <= 10; i++)
        {
            if (TryStartHttpListener(port + i))
                break;
        }
    }
}
```

**Zombie Reclaim**:
```csharp
catch (HttpListenerException ex) when (IsAddressInUse(ex))
{
    // Check if zombie from our PID
    var endpoint = ReadEndpointState();
    var alive = ReadAliveState();
    if (endpoint.Pid == CurrentPid && alive.Port == port)
    {
        // Trigger zombie self-termination
        SaveEndpointState(0, "");
        Thread.Sleep(3000);
        TryStartHttpListener(port); // Retry
    }
}
```

---

## Performance Characteristics

| Operation | Latency | Notes |
|-----------|---------|-------|
| listtools | <10ms | Cached after first call |
| calltool (sync) | 50-200ms | Depends on Unity focus |
| calltool (async) | Variable | Up to 25s timeout |
| Heartbeat update | 1s interval | File write every second |

---

## Security

**Localhost only**: HTTP listener binds to `127.0.0.1` (not `0.0.0.0`)

**No authentication**: Assumes local machine is trusted

**Arbitrary code execution**: `runcode` tool can execute any static method (intended for trusted AI agents only)

---

## Debugging

**Enable verbose logging**:
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

**Check state files**:
```bash
cat Library/MCP4Unity/mcp_endpoint.json
cat Library/MCP4Unity/mcp_alive.json
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

**Add management tools** (no Unity running):
- Implement in `server/src/unity-manager.ts`
- Register in `server/src/index.ts`

**Add Unity tools** (requires Unity):
- Create in `Assets/Editor/MCPTools/`
- Use `[Tool]` attribute
- See `authoring-guide.md`
