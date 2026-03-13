# Code Tools Reference

Code tools handle Unity script compilation, console log retrieval, and arbitrary editor code execution.

## Available Tools

### recompileassemblies

**Purpose**: Trigger Unity script compilation and wait for completion.

**Parameters**: None

**Returns**: Formatted string with compilation results (not JSON):
- Success/failure status
- List of errors with file path, line number, and message
- List of warnings with file path, line number, and message

**Example Response**:
```
✅ 编译成功

警告 (1):
  Assets/Scripts/Enemy.cs(15,10): CS0649: Field 'Enemy.health' is never assigned to
```

Or on failure:
```
❌ 编译失败

错误 (1):
  Assets/Scripts/Player.cs(42,5): CS0103: The name 'transform' does not exist in the current context
```

**Usage Notes**:
- Blocks until compilation completes (no timeout)
- Automatically refreshes AssetDatabase before compiling
- Returns immediately if no scripts need recompilation
- Note: EditorMainThread has 25-second timeout for queuing, but compilation itself waits indefinitely

---

### getunityconsolelog

**Purpose**: Retrieve recent Unity Console entries.

**Parameters**:
- `filter` (optional, string, default: "all"): Filter by log type
  - `"all"` - All log types (default)
  - `"error"` - Only errors
  - `"warning"` - Only warnings
  - `"log"` - Only regular logs
- `collapse` (optional, bool, default: false): Collapse duplicate log entries
- `maxCount` (optional, int, default: 10): Maximum number of logs to return

**Returns**: Formatted string with log statistics and entries (not JSON).

**Example Response**:
```
📊 日志统计: ❌错误 2 | ⚠️警告 1 | 🟢信息 5

[❌ Error] NullReferenceException: Object reference not set to an instance of an object
  Assets/Scripts/GameManager.cs:78
  
[⚠️ Warning] Shader warning in 'Custom/MyShader': ...
```

**Usage Notes**:
- Retrieves logs from Unity's internal console buffer
- Does not clear the console
- Useful after `recompileassemblies` to get compilation errors
- Returns formatted text with statistics and log entries
- Use `collapse=true` to hide duplicate messages

---

### runcode

**Purpose**: Execute arbitrary static methods in the Unity Editor.

**Parameters**:
- `typeDotMethod` (required, string): Fully-qualified static method name
  - Format: `"Namespace.ClassName.MethodName"`
  - Method must be public, static, and parameterless

**Returns**: 
- Method return value (if any)
- Execution status (success/failure)
- Error message (if execution failed)

**Example Usage**:

```javascript
// Refresh asset database
runcode("UnityEditor.AssetDatabase.Refresh")

// Clear console
runcode("UnityEditor.LogEntries.Clear")

// Custom editor utility
runcode("MyNamespace.EditorUtils.RegenerateAllPrefabs")
```

**Example Response**:
```json
{
  "success": true,
  "returnValue": null,
  "executionTime": "0.023s"
}
```

**Usage Notes**:
- Method must be accessible from Unity Editor context
- Cannot pass parameters (use custom wrapper methods if needed)
- Useful for triggering MenuItem handlers or custom editor utilities
- Timeout: 30 seconds
- Exceptions are caught and returned in error message

**Common Use Cases**:
- `UnityEditor.AssetDatabase.Refresh` - Refresh asset database
- `UnityEditor.AssetDatabase.SaveAssets` - Force save all assets
- `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation` - Alternative compilation trigger
- Custom `[MenuItem]` handlers (if parameterless)

---

## Workflow: Compile and Fix Errors

**Recommended approach** (use `unity-compile-fix` skill for full automation):

1. Check Unity status: `getunitystatus`
2. If `editor_mcp_ready`, trigger compilation: `recompileassemblies`
3. If errors returned, fix them in source files
4. Repeat steps 2-3 until clean
5. Optionally verify with `getunityconsolelog(filter="error")`

**Manual approach** (for one-off checks):

```
recompileassemblies
  ↓
errors? → fix code → recompileassemblies
  ↓
clean → done
```

---

## Error Handling

**Compilation timeout**:
- If compilation takes >60s, `recompileassemblies` returns partial results
- Check `getunitystatus` to see if Unity is responsive
- If `editor_mcp_unresponsive`, wait or restart Unity

**MCP service unresponsive**:
- All code tools require Unity Editor with MCP service running
- If tools timeout, check `getunitystatus` first
- See `references/troubleshooting.md` for recovery steps

**Invalid method in runcode**:
- Returns error with exception details
- Verify method is public, static, parameterless
- Check namespace and class name spelling
