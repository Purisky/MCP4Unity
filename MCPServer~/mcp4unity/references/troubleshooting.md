# Troubleshooting Guide

Common issues and solutions for MCP4Unity.

## MCP Tools Not Responding

### Symptom
Tools timeout or return no response.

### Diagnosis Steps

1. **Check Unity status**:
   ```
   getunitystatus
   ```
   
2. **Verify endpoint file exists**:
   - Check `Library/MCP4Unity/mcp_endpoint.json`
   - If missing, MCP service isn't running

3. **Check Unity Console**:
   - Look for MCPService startup/shutdown messages
   - Check for errors during service initialization

4. **Test HTTP connectivity** (manual):
   ```bash
   curl --noproxy "*" -s -m 5 -X POST http://127.0.0.1:8080/mcp/ \
     -H "Content-Type: application/json" \
     -d '{"method":"listtools"}'
   ```

### Solutions

**If `getunitystatus` returns `not_running`**:
```
startunity
Wait 30-60s
getunitystatus → should be editor_mcp_ready
```

**If `getunitystatus` returns `editor_mcp_unresponsive`**:
- Unity is starting up, compiling, or loading
- Wait 1-2 minutes and retry
- If persists >5 minutes, restart Unity

**If `getunitystatus` returns `batchmode`**:
- Unity is running in headless mode (no MCP service)
- Stop Unity and start normally via `startunity`

---

## Unity Hangs or Freezes

### Symptom
Unity becomes unresponsive, tools timeout indefinitely.

### Solution

```
stopunity
deletescriptassemblies
startunity
```

**Why this works**:
- `stopunity` force-kills the frozen process
- `deletescriptassemblies` clears stale cache
- `startunity` performs clean startup with cache cleanup

---

## Compilation Errors Persist

### Symptom
`recompileassemblies` returns errors that don't match actual code state.

### Solution

**Step 1: Clear cache and restart**:
```
stopunity
deletescriptassemblies
startunity
```

**Step 2: Force full reimport** (if Step 1 fails):
```
stopunity
Delete Library/ScriptAssemblies/
Delete Library/Bee/
startunity
```

**Step 3: Check for actual errors**:
- Open Unity manually
- Check Console for real errors
- Verify code compiles in IDE

---

## Stale Endpoint File

### Symptom
`getunitystatus` shows `editor_mcp_ready` but tools still timeout.

### Cause
Endpoint file (`Library/MCP4Unity/mcp_endpoint.json`) contains stale PID from previous Unity session.

### Solution

**Automatic** (getunitystatus validates PID):
- `getunitystatus` checks if PID matches running process
- Returns `not_running` if PID is stale

**Manual**:
```
stopunity
Delete Library/MCP4Unity/mcp_endpoint.json
startunity
```

---

## Port Conflicts

### Symptom
MCPService fails to start, Unity Console shows "Address already in use" error.

### Cause
Another process is using port 8080 (default MCP port).

### Solution

**Option 1: Kill conflicting process**:
```bash
# Windows
netstat -ano | findstr :8080
taskkill /PID <pid> /F

# Linux/Mac
lsof -i :8080
kill -9 <pid>
```

**Option 2: Change MCP port** (advanced):
- Edit `Assets/MCP4Unity/Runtime/MCPService.cs`
- Change `DefaultPort = 8080` to another port
- Rebuild Unity project

---

## Proxy Interference

### Symptom
Tools work in some environments but not others. HTTP requests fail with proxy errors.

### Cause
System HTTP proxy intercepts localhost requests.

### Solution

**Already handled** in TypeScript server:
```typescript
// server/src/unity-client.ts uses proxy: false
axios.post(url, data, { proxy: false })
```

**If still failing**, check system proxy settings:
- Windows: Internet Options → Connections → LAN Settings
- Ensure "Bypass proxy server for local addresses" is checked

---

## Domain Reload Issues

### Symptom
MCP service stops responding after script changes or entering Play mode.

### Cause
Unity's domain reload stops and restarts the MCP service.

### Solution

**Normal behavior**:
- Service automatically restarts after domain reload
- Wait 5-10 seconds after compilation
- Use `getunitystatus` to check when ready

**If service doesn't restart**:
```
stopunity
startunity
```

---

## Async Tool Timeouts

### Symptom
Async tools (like `recompileassemblies`) timeout after 25 seconds.

### Cause
Operation takes longer than the 25-second timeout.

### Solution

**For compilation**:
- Large projects may exceed timeout
- Use `unity-compile-fix` skill which handles retries

**For custom async tools**:
- Break long operations into smaller chunks
- Return progress updates instead of waiting for completion
- Consider polling pattern instead of single long-running call

---

## Background Unity Responsiveness

### Symptom
Tools are slow (200-500ms) when Unity is in background or minimized.

### Cause
Windows reduces update frequency for background applications.

### Solution

**Normal behavior**:
- Background: ~100-200ms latency
- Focused: ~50ms latency
- This is expected and handled by Win32 wake mechanism

**If latency >1 second**:
- Check if Unity is actually frozen (Task Manager CPU usage)
- Restart Unity if frozen

---

## Configuration Issues

### Symptom
`startunity` fails with "Unity path not configured" error.

### Solution

```
configureunity("C:/Path/To/Unity.exe")
```

**Finding Unity path**:
- Unity Hub: Preferences → Installs → Show in Explorer
- Typical paths:
  - Windows: `C:/Program Files/Unity/Hub/Editor/{version}/Editor/Unity.exe`
  - Mac: `/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity`
  - Linux: `~/Unity/Hub/Editor/{version}/Editor/Unity`

---

## Scene Backup Clutter

### Symptom
Many `*.backup` files in Assets folder.

### Solution

```
deletescenebackups
```

**When to use**:
- Before committing to version control
- After recovering from crashes
- General cleanup

---

## Quick Diagnostic Checklist

Run these in order to diagnose most issues:

1. `getunitystatus` - Check Unity state
2. `getunityconsolelog` - Check for Unity errors
3. Check `Library/MCP4Unity/mcp_endpoint.json` exists
4. Restart Unity: `stopunity` → `startunity`
5. Clear cache: `deletescriptassemblies` before restart
6. Check Unity Console manually for service errors

---

## Getting Help

If issues persist:

1. **Collect diagnostic info**:
   - Output of `getunitystatus`
   - Unity Console logs
   - Contents of `Library/MCP4Unity/mcp_endpoint.json`
   - TypeScript server logs (if running manually)

2. **Check Unity Console** for MCPService messages:
   - Service startup/shutdown events
   - Port binding errors
   - Tool execution errors

3. **Test manually**:
   ```bash
   curl --noproxy "*" -X POST http://127.0.0.1:8080/mcp/ \
     -H "Content-Type: application/json" \
     -d '{"method":"listtools"}'
   ```

4. **Verify MCP4Unity submodule**:
   ```bash
   cd Assets/MCP4Unity
   git status
   # Should show clean working tree
   ```
