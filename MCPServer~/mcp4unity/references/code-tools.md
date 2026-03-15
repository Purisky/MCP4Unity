# Code Tools Reference

Script compilation, console logs, and editor code execution.

## recompileassemblies

Trigger Unity script compilation and wait for completion.

**Parameters**: None

**Returns** (formatted text):
```
✅ 编译成功

警告 (1):
  Assets/Scripts/Enemy.cs(15,10): CS0649: Field 'Enemy.health' is never assigned to
```

Or on failure:
```
❌ 编译失败

错误 (1):
  Assets/Scripts/Player.cs(42,5): CS0103: The name 'transform' does not exist
```

**Notes**:
- Blocks until compilation completes
- Auto-refreshes AssetDatabase
- Returns immediately if nothing to compile

---

## getunityconsolelog

Retrieve Unity Console entries.

**Parameters**:
- `filter` (optional, string, default: "all"): Log type
  - `"all"`: All logs
  - `"error"`: Only errors
  - `"warning"`: Only warnings
  - `"log"`: Only regular logs
- `collapse` (optional, bool, default: false): Collapse duplicates
- `maxCount` (optional, int, default: 10): Max entries

**Returns** (formatted text):
```
📊 日志统计: ❌错误 2 | ⚠️警告 1 | 🟢信息 5

[❌ Error] NullReferenceException: Object reference not set
  Assets/Scripts/GameManager.cs:78
  
[⚠️ Warning] Shader warning in 'Custom/MyShader': ...
```

**Notes**:
- Retrieves from Unity's console buffer
- Does not clear console
- Use after `recompileassemblies` for errors

---

## runcode

Execute arbitrary static methods.

**Parameters**:
- `typeDotMethod` (string): Fully-qualified method name
  - Format: `"Namespace.ClassName.MethodName"`
  - Must be public, static, parameterless

**Examples**:
```javascript
runcode("UnityEditor.AssetDatabase.Refresh")
runcode("UnityEditor.LogEntries.Clear")
runcode("MyNamespace.EditorUtils.RegenerateAllPrefabs")
```

**Returns**:
```json
{
  "success": true,
  "returnValue": null,
  "executionTime": "0.023s"
}
```

**Notes**:
- Method must be accessible from Editor
- Cannot pass parameters
- Timeout: 30 seconds
- Exceptions caught and returned

**Common Methods**:
- `UnityEditor.AssetDatabase.Refresh` - Refresh assets
- `UnityEditor.AssetDatabase.SaveAssets` - Force save
- `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation` - Trigger compile

---

## Common Workflows

### Compile and Fix Errors
```
1. getunitystatus → editor_mcp_ready
2. recompileassemblies
3. If errors → fix code → repeat 2
4. Clean → done
```

### Check Console After Compilation
```
1. recompileassemblies
2. getunityconsolelog(filter="error")
```

### Execute Custom Editor Method
```
1. runcode("MyNamespace.EditorUtils.MyMethod")
```

---

## Troubleshooting

**Compilation timeout**:
- Check `getunitystatus`
- If `editor_mcp_unresponsive`, wait or restart

**runcode fails**:
- Verify method is public, static, parameterless
- Check namespace/class spelling
- Ensure method accessible from Editor

**MCP unresponsive**:
- All tools require Unity Editor + MCP running
- Check `getunitystatus` first
- See `troubleshooting.md` for recovery
