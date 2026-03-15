# Management Tools Reference

Unity Editor process control and maintenance. Work without Unity running.

## startunity

Start Unity Editor with automatic cleanup.

**Parameters**:
- `projectPath` (optional, string): Project name or path (uses `defaultProject` if omitted)

**Returns**: Process start status

**Automatic Cleanup**:
1. Deletes scene backups (`*.backup`)
2. Deletes ScriptAssemblies cache
3. Starts Unity in normal mode

**Notes**:
- Requires `unity_config.json`
- Takes 30-60s to start
- Use `getunitystatus` to check readiness

---

## stopunity

Force close Unity Editor.

**Parameters**: None

**Returns**:
```json
{"success": true, "message": "Unity process terminated"}
```

**Notes**:
- Kills immediately (no graceful shutdown)
- Unsaved changes lost
- Use when frozen/unresponsive
- Safe if Unity not running

---

## runbatchmode

Run Unity in batchmode for compilation check.

**Parameters**:
- `projectPath` (optional, string): Project name or path

**Returns**: Structured compilation result

**Example Success**:
```json
{
  "success": true,
  "errors": 0,
  "warnings": 2,
  "exitCode": 0,
  "logPath": "E:\\Project\\Logs\\batchmode_compile.log"
}
```

**Example Failure**:
```json
{
  "success": false,
  "errors": 3,
  "warnings": 1,
  "exitCode": 1,
  "errorDetails": [
    {
      "file": "Assets/Scripts/Player.cs",
      "line": 42,
      "message": "CS0103: The name 'foo' does not exist"
    }
  ],
  "logPath": "E:\\Project\\Logs\\batchmode_compile.log"
}
```

**Notes**:
- Stops Unity if running
- Returns first 20 errors
- More reliable than Editor mode compilation
- Use for CI/CD or automated checks

---

## getunitystatus

Check Unity Editor status.

**Parameters**:
- `projectPath` (optional, string): Project name or path
- `detailed` (optional, boolean): Return JSON diagnostics (default: false)

**Simple Mode (default)**:
```
getunitystatus()
```

Returns:
- `❌ Unity is not running`
- `⚙️ Unity is running in batchmode`
- `✅ Unity Editor is running and MCP service is ready`
- `⚠️ Unity Editor is running but MCP service is unresponsive`

**Detailed Mode**:
```
getunitystatus(detailed=true)
```

Returns:
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
- `not_running` - Unity not running
- `batchmode` - Headless mode (no MCP)
- `editor_mcp_ready` - Editor + MCP ready
- `editor_mcp_unresponsive` - Editor running, MCP not responding

**Use Cases**:
- Check before calling MCP tools
- Detect compilation/loading issues
- Distinguish batchmode vs editor
- Verify endpoint file validity

---

## deletescenebackups

Delete scene backup files.

**Parameters**:
- `projectPath` (optional, string): Project name or path

**Returns**: Success status

**What it deletes**:
- `Temp/__Backupscenes/*.backup`

**Notes**:
- Safe to run anytime
- Frees disk space
- Unity auto-creates backups

---

## deletescriptassemblies

Delete script compilation cache.

**Parameters**:
- `projectPath` (optional, string): Project name or path

**Returns**: Success status

**What it deletes**:
- `Library/ScriptAssemblies/`

**Notes**:
- Forces full recompilation on next Unity start
- Use when compilation errors persist
- Safe to run anytime

---

## configureunity

Configure Unity path for a project.

**Parameters**:
- `projectPath` (string): Project name or full path
- `unityExePath` (string): Unity.exe absolute path
- `mcpPort` (optional, number): MCP service port (default: 52429)

**Returns**: Success status

**Example**:
```javascript
configureunity(
  "ProjectA",
  "C:\\Unity\\2021.3.10f1\\Editor\\Unity.exe",
  52429
)
```

**Notes**:
- Creates/updates `unity_config.json`
- Each project needs unique port
- Use when adding new project

---

## Common Workflows

### First-Time Setup
```
1. configureunity("MyProject", "C:\\Unity\\Editor\\Unity.exe", 52429)
2. startunity("MyProject")
3. Wait 30-60s
4. getunitystatus("MyProject") → should be editor_mcp_ready
```

### Check Compilation (Recommended)
```
1. stopunity
2. runbatchmode("MyProject")
3. Check errors → fix code
4. Repeat 2-3 until success
```

### Unity Frozen Recovery
```
1. stopunity
2. deletescriptassemblies("MyProject")
3. startunity("MyProject")
```

### Switch Projects
```
1. stopunity
2. startunity("ProjectB")
3. getunitystatus("ProjectB")
```

---

## Multi-Project Configuration

**unity_config.json** structure:
```json
{
  "defaultProject": "ProjectA",
  "projects": {
    "ProjectA": {
      "projectPath": "C:\\Path\\To\\ProjectA",
      "unityExePath": "C:\\Unity\\Editor\\Unity.exe",
      "mcpPort": 52429
    },
    "ProjectB": {
      "projectPath": "C:\\Path\\To\\ProjectB",
      "unityExePath": "C:\\Unity\\Editor\\Unity.exe",
      "mcpPort": 52430
    }
  }
}
```

**Notes**:
- `defaultProject` used when `projectPath` omitted
- Each project must have unique `mcpPort`
- Can run multiple Unity instances simultaneously

---

## Troubleshooting

**startunity fails**:
- Check `unity_config.json` exists
- Verify Unity path correct
- Check Unity license activated

**getunitystatus returns unresponsive**:
- Wait 1-2 min (compiling/loading)
- Restart if >5 min

**Port conflicts**:
- Change `mcpPort` in config
- Unity auto-fallbacks 52429-52439

**Batchmode hangs**:
- Check full log at `Logs/batchmode_compile.log`
- Verify project not corrupted
