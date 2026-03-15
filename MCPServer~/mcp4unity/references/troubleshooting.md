# Troubleshooting Guide

Common issues and solutions for MCP4Unity.

## MCP Tools Not Responding

**Symptom**: Tools timeout or return no response.

**Diagnosis**:
1. `getunitystatus` - Check Unity state
2. Verify state files exist:
   - `Library/MCP4Unity/mcp_endpoint.json` (process marker)
   - `Library/MCP4Unity/mcp_alive.json` (heartbeat, updated every 1s)
3. Check Unity Console for MCPService errors
4. Test HTTP manually:
   ```bash
   curl --noproxy "*" -s -m 5 -X POST http://127.0.0.1:52429/mcp/ \
     -H "Content-Type: application/json" -d '{"method":"listtools"}'
   ```

**Solutions**:

| Status | Action |
|--------|--------|
| `not_running` | `startunity` → wait 30-60s → `getunitystatus` |
| `editor_mcp_unresponsive` | Wait 1-2 min (compiling/loading), restart if >5 min |
| `batchmode` | `stopunity` → `startunity` (batchmode has no MCP) |

---

## Unity Hangs or Freezes

**Symptom**: Unity unresponsive, tools timeout indefinitely.

**Solution**:
```
stopunity
deletescriptassemblies
startunity
```

**Why**: Force-kill frozen process, clear stale cache, clean startup.

---

## Compilation Errors Persist

**Symptom**: `recompileassemblies` returns errors that don't match code.

**Step 1 - Clear cache**:
```
stopunity
deletescriptassemblies
startunity
```

**Step 2 - Force reimport** (if Step 1 fails):
```
stopunity
Delete Library/ScriptAssemblies/
Delete Library/Bee/
startunity
```

**Step 3 - Verify**:
- Open Unity manually
- Check Console for real errors
- Verify code compiles in IDE

---

## Stale State Files

**Symptom**: `getunitystatus` shows ready but tools timeout.

**Cause**: State files contain stale data:
- `mcp_endpoint.json` - Stale PID
- `mcp_alive.json` - Stale heartbeat (>3s old)

**Automatic**: `getunitystatus` validates PID and heartbeat, returns `not_running` if stale.

**Manual**:
```
stopunity
Delete Library/MCP4Unity/mcp_endpoint.json
Delete Library/MCP4Unity/mcp_alive.json
startunity
```

---

## Port Conflicts

**Symptom**: MCPService fails to start, Console shows "Address already in use".

**Cause**: Another process using port 52429.

**Solution 1 - Auto-fallback**:
Unity auto-tries ports 52429-52439. Check `Library/MCP4Unity/mcp_alive.json` for actual port.

**Solution 2 - Change port**:
Edit `unity_config.json`:
```json
{
  "projects": {
    "MyProject": {
      "mcpPort": 52430
    }
  }
}
```

**Solution 3 - Kill conflicting process**:
```bash
# Windows
netstat -ano | findstr :52429
taskkill /PID <pid> /F

# Linux/Mac
lsof -i :52429
kill -9 <pid>
```

---

## Scene Not Saving

**Symptom**: `savescene()` succeeds but changes not persisted.

**Causes**:
1. Scene not marked dirty
2. Scene path invalid
3. Unity in play mode

**Solutions**:
```
# Mark scene dirty explicitly
runcode("UnityEditor.SceneManagement.EditorSceneManager", "MarkSceneDirty", 
  "UnityEngine.SceneManagement.SceneManager.GetActiveScene()")

# Save with explicit path
savesceneas("Assets/Scenes/MyScene.unity")

# Exit play mode first
runcode("UnityEditor.EditorApplication", "set_isPlaying", "false")
```

---

## GameObject Not Found

**Symptom**: `getgameobjectinfo("ObjectName")` returns "not found".

**Causes**:
1. Object inactive
2. Wrong scene loaded
3. Object in child hierarchy (need full path)

**Solutions**:
```
# Check active scene
getactivescene()

# Get full hierarchy
gethierarchy()

# Use full path
getgameobjectinfo("Parent/Child/ObjectName")

# Search inactive objects
runcode("UnityEngine.GameObject", "Find", "ObjectName")  # Only finds active
# For inactive, use hierarchy search
```

---

## Component Property Not Updating

**Symptom**: `setserializedproperty()` succeeds but value unchanged.

**Causes**:
1. Wrong property path
2. Property not serialized
3. Property type mismatch

**Solutions**:
```
# List all properties first
getserializedproperties("ObjectName", "ComponentType")

# Use exact property path
setserializedproperty("ObjectName", "Transform", "m_LocalPosition.x", "5.0")

# For non-serialized properties, use runcode
runcode("UnityEngine.GameObject.Find('ObjectName').GetComponent<Rigidbody>()", 
  "set_velocity", "new Vector3(1, 0, 0)")
```

---

## TypeScript Server Issues

**Symptom**: MCP server fails to start or crashes.

**Check Node.js version**:
```bash
node --version  # Should be 18+
```

**Rebuild server**:
```bash
cd {SKILL_ROOT}/server
npm install
npm run build
```

**Check logs**:
- Server logs to stdout/stderr
- Unity logs to `Library/MCP4Unity/mcp_endpoint.json`

**Common errors**:
- `EADDRINUSE`: Port conflict (see Port Conflicts section)
- `MODULE_NOT_FOUND`: Run `npm install`
- `SyntaxError`: Run `npm run build`

---

## Unity Won't Start

**Symptom**: `startunity` times out or Unity crashes on startup.

**Check Unity path**:
```
# Verify unity_config.json
cat unity_config.json
```

**Check Unity version**:
- MCP4Unity requires Unity 2021.3+
- Tested on Unity 6 (6000.3.10f1)

**Check project corruption**:
```
# Delete cache and restart
stopunity
Delete Library/
startunity
```

**Check Unity license**:
- Open Unity manually
- Verify license is activated

---

## Batchmode Compilation Fails

**Symptom**: `runbatchmode` returns exit code 1 but no errors shown.

**Check full log**:
```
# Log path shown in runbatchmode output
cat <project>/Logs/batchmode_compile.log
```

**Common causes**:
- Unity license issue
- Project corruption
- Missing dependencies

**Solution**:
```
# Try manual batchmode
Unity.exe -batchmode -projectPath <path> -quit -logFile -
```

---

## Quick Diagnostic Checklist

Run in order:

1. `getunitystatus` - Check Unity state
2. `getunityconsolelog` - Check for errors
3. Check state files:
   - `Library/MCP4Unity/mcp_endpoint.json` (process marker)
   - `Library/MCP4Unity/mcp_alive.json` (heartbeat)
4. Restart: `stopunity` → `startunity`
5. Clear cache: `deletescriptassemblies` before restart
6. Check Unity Console manually

---

## Getting Help

If issues persist, collect:

1. **Diagnostic info**:
   - `getunitystatus` output
   - Unity Console logs
   - State file contents:
     - `Library/MCP4Unity/mcp_endpoint.json`
     - `Library/MCP4Unity/mcp_alive.json`
   - TypeScript server logs

2. **Unity Console** MCPService messages:
   - Startup/shutdown logs
   - Error messages
   - Port binding info

3. **System info**:
   - Unity version
   - Node.js version
   - OS version

---

## Advanced: Manual Cleanup

**When to use**: After crashes, before version control commits, general cleanup.

**Full cleanup**:
```
stopunity
Delete Library/ScriptAssemblies/
Delete Library/Bee/
Delete Library/MCP4Unity/
Delete Temp/
startunity
```

**Preserve settings**:
```
stopunity
Delete Library/ScriptAssemblies/
Delete Library/Bee/
# Keep Library/EditorUserSettings.asset
startunity
```
