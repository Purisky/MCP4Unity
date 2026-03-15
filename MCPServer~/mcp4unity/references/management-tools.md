# Management Tools Reference

Management tools handle Unity Editor process control, configuration, and maintenance tasks. These tools work even when Unity is not running.

## Available Tools

### configureunity

**Purpose**: Configure Unity executable path, project path, and MCP service port (first-time setup).

**Parameters**:
- `unityExePath` (required, string): Full path to Unity.exe
  - Example: `"path/to/Unity/Editor/Unity.exe"`
- `projectPath` (optional, string): Full path to Unity project root
  - Auto-detected from current working directory if omitted
  - Must contain `Assets/` and `ProjectSettings/` folders
- `mcpPort` (optional, number): Unity Editor MCP service port
  - Default: `52429`
  - Use different ports for multiple Unity projects to avoid conflicts

**Returns**: Configuration status and saved paths.

**Example Usage**:

```javascript
// Configure Unity path (project auto-detected, default port 52429)
configureunity("path/to/Unity/Editor/Unity.exe")

// Configure with explicit project path
configureunity(
  "path/to/Unity/Editor/Unity.exe",
  "path/to/your/unity/project"
)

// Configure with custom MCP port (for multiple projects)
configureunity(
  "path/to/Unity/Editor/Unity.exe",
  "path/to/your/unity/project",
  52430
)
```

**Example Response**:
```json
{
  "success": true,
  "unityExePath": "path/to/Unity/Editor/Unity.exe",
  "projectPath": "path/to/your/unity/project",
  "mcpPort": 52429,
  "configFile": "path/to/your/unity/project/unity_config.json"
}
```

**Configuration File Format** (`unity_config.json`):
```json
{
  "unityExePath": "path/to/Unity/Editor/Unity.exe",
  "projectPath": "path/to/your/unity/project",
  "mcpPort": 52429
}
```

**Usage Notes**:
- Only needed once per Unity project
- Configuration saved to `{UNITY_PROJECT_ROOT}/unity_config.json`
- Project path auto-detection searches upward from current directory for Unity project root (containing Assets/ and ProjectSettings/)
- Supports multiple Unity projects with independent configurations
- Validates Unity.exe exists before saving
- **Multi-project setup**: Use different `mcpPort` values for each project to avoid port conflicts
- **Port persistence**: If `mcpPort` is omitted, existing port configuration is preserved

---

### startunity

**Purpose**: Start Unity Editor with automatic cleanup.

**Parameters**: None

**Returns**: Process start status and PID.

**Automatic Cleanup** (before starting):
1. Deletes scene backup files (`*.backup`)
2. Deletes ScriptAssemblies cache
3. Starts Unity Editor in normal mode (not batchmode)

**Example Response**:
```json
{
  "success": true,
  "pid": 12345,
  "projectPath": "path/to/your/unity/project",
  "cleanupPerformed": {
    "sceneBackups": true,
    "scriptAssemblies": true
  }
}
```

**Usage Notes**:
- Requires prior configuration via `configureunity`
- Unity takes 30-60s to fully start
- MCP service becomes available after Unity UI loads
- Use `getunitystatus` to check when ready
- Cleanup helps prevent stale cache issues

---

### runbatchmode

**Purpose**: Force close Unity Editor process.

**Parameters**: None

**Returns**: Success status.

**Example Response**:
```json
{
  "success": true,
  "message": "Unity process terminated"
}
```

**Usage Notes**:
- Kills Unity process immediately (no graceful shutdown)
- Unsaved changes will be lost
- Use when Unity is frozen or unresponsive
- Safe to call even if Unity is not running

---

### getunitystatus

**Purpose**: Get Unity Editor status with optional detailed diagnostics.

**Parameters**:
- `detailed` (optional, boolean): Return detailed JSON diagnostics
  - `false` (default): Simple status string with emoji
  - `true`: Full JSON object with all diagnostic fields

**Returns**: Status information (format depends on `detailed` parameter).

**Simple Mode (default)**:

```
getunitystatus()
// or
getunitystatus(detailed=false)
```

**Example Responses**:
```
❌ Unity is not running
⚙️ Unity is running in batchmode (headless)
✅ Unity Editor is running and MCP service is ready
⚠️ Unity Editor is running but MCP service is unresponsive (may be compiling, loading, or main thread blocked)
```

**Detailed Mode**:

```
getunitystatus(detailed=true)
```

**Example Response**:
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

**Status Values**:
- `not_running`: Unity process not found
- `batchmode`: Unity running in headless/batchmode (no MCP service)
- `editor_mcp_ready`: Unity Editor running, MCP service responsive
- `editor_mcp_unresponsive`: Unity Editor running but MCP not responding

**Example Response (not running)**:
```json
{
  "status": "not_running",
  "message": "Unity is not running",
  "processRunning": false,
  "endpointExists": false,
  "mcpResponsive": false,
  "batchMode": false
}
```

**Usage Notes**:
- **Simple mode**: Quick status check for scripts/automation
- **Detailed mode**: Debugging, diagnostics, or when you need specific fields
- Use before calling MCP tools to verify readiness
- `editor_mcp_unresponsive` is common during:
  - Initial Unity startup (30-60s)
  - Script compilation
  - Asset import
  - Domain reload
- Validates endpoint file PID matches running process (detects stale files)

---

### deletescenebackups

**Purpose**: Delete Unity scene recovery backup files.

**Parameters**: None

**Returns**: Number of files deleted.

**Example Response**:
```json
{
  "success": true,
  "filesDeleted": 1,
  "path": "Temp/__Backupscenes"
}
```

**Usage Notes**:
- Deletes the entire `Temp/__Backupscenes` directory
- Unity automatically recreates this folder as needed
- Safe to run anytime
- Useful for cleanup before commits

---

### deletescriptassemblies

**Purpose**: Delete Unity's ScriptAssemblies cache folder.

**Parameters**: None

**Returns**: Success status.

**Example Response**:
```json
{
  "success": true,
  "path": "Library/ScriptAssemblies",
  "message": "ScriptAssemblies cache deleted"
}
```

**Usage Notes**:
- Forces full script recompilation on next Unity start
- Useful for resolving:
  - Persistent compilation errors
  - Assembly reference issues
  - Stale cache problems
- Unity must be closed before deleting
- Unity recreates folder on next startup

---

## Workflow: Unity Lifecycle Management

### First-Time Setup

```
configureunity(unityExePath)
  ↓
startunity
  ↓
Wait 30-60s
  ↓
getunitystatus → editor_mcp_ready
  ↓
Ready to use MCP tools
```

### Compilation Check Workflow

**Recommended: Use batchmode for compilation checks**

```
1. stopunity (if Unity is running)
   ↓
2. runbatchmode
   ↓
3. Check output:
   ├─ ✅ Success → Done
   └─ ❌ Failure → Fix first 20 errors
   ↓
4. Repeat step 2-3 until compilation succeeds
```

**Why batchmode is preferred**:
- No MCP service dependency
- Token-efficient output (max 20 errors)
- Faster than Editor mode
- Iterative error fixing workflow
- No UI overhead

**When to use Editor mode compilation**:
- Need to test runtime behavior immediately after compilation
- Working with Editor-only code that requires Unity Editor context
- Debugging compilation issues that only occur in Editor mode

**Editor mode workflow** (fallback):
```
getunitystatus
  ├─ editor_mcp_ready → recompileassemblies
  ├─ editor_mcp_unresponsive → wait or restart
  ├─ not_running → startunity
  └─ batchmode → wait for completion
```

### Normal Startup

```
getunitystatus
  ↓
not_running? → startunity → wait → getunitystatus
  ↓
editor_mcp_ready? → proceed
  ↓
editor_mcp_unresponsive? → wait 30s → retry
```

### Recovery from Hang/Freeze

```
getunitystatus → editor_mcp_unresponsive (>5min)
  ↓
stopunity
  ↓
deletescriptassemblies
  ↓
startunity
  ↓
Wait for editor_mcp_ready
```

### Clean Restart

```
stopunity
  ↓
deletescenebackups
  ↓
deletescriptassemblies
  ↓
startunity
```

---

## Configuration File

**Location**: `{UNITY_PROJECT_ROOT}/unity_config.json` (Unity project root directory containing Assets/ and ProjectSettings/)

**Format**:
```json
{
  "unityExePath": "path/to/Unity/Editor/Unity.exe",
  "projectPath": "path/to/your/unity/project",
  "mcpPort": 52429
}
```

**Auto-created by**: `configureunity` tool

**Used by**: All management tools and MCP tools

**Multi-project support**: Each Unity project has its own independent configuration file, allowing multiple Unity projects to coexist with different Unity versions and ports.

---

## Tips and Best Practices

**Startup timing**:
- Unity takes 30-60s to fully start
- MCP service starts after Unity Editor UI loads
- Use `getunitystatus` in a loop with 5s intervals
- Don't call MCP tools until `editor_mcp_ready`

**Unresponsive detection**:
- `editor_mcp_unresponsive` during compilation is normal
- If unresponsive >5min, likely frozen
- Recovery: stop → clean → restart

**Cleanup strategy**:
- Run `deletescenebackups` before git commits
- Run `deletescriptassemblies` when seeing weird compilation errors
- `startunity` auto-cleans both (convenient)

**Process management**:
- `stopunity` is safe to call anytime
- No graceful shutdown (kills process)
- Unsaved changes are lost
- Use for recovery, not normal workflow
