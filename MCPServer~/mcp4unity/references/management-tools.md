# Management Tools Reference

Management tools handle Unity Editor process control, configuration, and maintenance tasks. These tools work even when Unity is not running.

## Available Tools

### configureunity

**Purpose**: Configure Unity executable path and project path (first-time setup).

**Parameters**:
- `unityExePath` (required, string): Full path to Unity.exe
  - Example: `"Path/to/Editor/Unity.exe"`
- `projectPath` (optional, string): Full path to Unity project root
  - Auto-detected from current working directory if omitted
  - Must contain `Assets/` and `Library/` folders

**Returns**: Configuration status and saved paths.

**Example Usage**:

```javascript
// Configure Unity path (project auto-detected)
configureunity("Path/to/Editor/Unity.exe")

// Configure both paths explicitly
configureunity(
  "Path/to/Editor/Unity.exe",
  "Path/to/MyUnityProject"
)
```

**Example Response**:
```json
{
  "success": true,
  "unityExePath": "Path/to/Editor/Unity.exe",
  "projectPath": "Path/to/MyUnityProject",
  "configFile": "{SKILL_ROOT}/unity_config.json"
}
```

**Usage Notes**:
- Only needed once per project
- Configuration saved to `{SKILL_ROOT}/unity_config.json`
- Project path auto-detection searches upward for Unity project root
- Validates Unity.exe exists before saving

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
  "projectPath": "Path/to/MyUnityProject",
  "cleanupPerformed": {
    "sceneBackups": true,
    "scriptAssemblies": true
  }
}
```

**Usage Notes**:
- Requires prior configuration via `configureunity`
- Cleanup helps prevent stale cache issues
- Unity takes 30-60s to fully start and load MCP service
- Use `getunitystatus` to check when MCP is ready

---

### stopunity

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

### isunityrunning

**Purpose**: Quick boolean check if Unity process is running.

**Parameters**: None

**Returns**: Simple boolean status.

**Example Response**:
```json
{
  "running": true
}
```

**Usage Notes**:
- Fast check (no HTTP requests)
- Only checks if process exists
- Does not verify MCP service is responsive
- Use `getunitystatus` for detailed status

---

### getunitystatus

**Purpose**: Get detailed Unity Editor status with diagnostics.

**Parameters**: None

**Returns**: Comprehensive status information including:
- Process running state
- MCP endpoint file existence
- MCP service responsiveness
- Batchmode detection
- Diagnostic messages

**Status Values**:
- `not_running`: Unity process not found
- `batchmode`: Unity running in headless/batchmode (no MCP service)
- `editor_mcp_ready`: Unity Editor running, MCP service responsive
- `editor_mcp_unresponsive`: Unity Editor running but MCP not responding

**Example Response** (Ready):
```json
{
  "status": "editor_mcp_ready",
  "message": "Unity Editor is running and MCP service is ready",
  "processRunning": true,
  "endpointExists": true,
  "mcpResponsive": true,
  "batchMode": false,
  "endpoint": "http://localhost:54321"
}
```

**Example Response** (Unresponsive):
```json
{
  "status": "editor_mcp_unresponsive",
  "message": "Unity Editor is running but MCP service is not responding (compiling or loading)",
  "processRunning": true,
  "endpointExists": true,
  "mcpResponsive": false,
  "batchMode": false,
  "endpoint": "http://localhost:54321"
}
```

**Example Response** (Not Running):
```json
{
  "status": "not_running",
  "message": "Unity process not found",
  "processRunning": false,
  "endpointExists": false,
  "mcpResponsive": false,
  "batchMode": false
}
```

**Usage Notes**:
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

**Location**: `{SKILL_ROOT}/unity_config.json`

**Format**:
```json
{
  "unityExePath": "Path/to/Editor/Unity.exe",
  "projectPath": "Path/to/UnityProject"
}
```

**Auto-created by**: `configureunity` tool

**Used by**: All management tools and MCP tools

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
