# MCP4Unity Installation Guide for AI Agents

> **Target Audience**: AI coding assistants and autonomous agents
> 
> **Purpose**: Step-by-step installation instructions optimized for automated execution

## Prerequisites Check

Before starting, verify:

```bash
# Check Node.js version (requires 18+)
node --version

# Check if Unity project exists
test -d Assets && test -d Library && echo "Unity project detected" || echo "Not a Unity project"
```

## Installation Steps

### Step 1: Install Unity Package

1. **Clone MCP4Unity repository** (if not already cloned):

   ```bash
   git clone https://github.com/Purisky/MCP4Unity.git /tmp/MCP4Unity
   ```

2. **Copy to Unity project**:

   ```bash
   # Assuming current directory is Unity project root
   cp -r /tmp/MCP4Unity Assets/MCP4Unity
   ```

3. **Verify installation**:

   ```bash
   test -d Assets/MCP4Unity/Editor && echo "✅ Unity package installed" || echo "❌ Installation failed"
   ```

### Step 2: Setup TypeScript MCP Server

1. **Determine skill directory**:

   For OpenCode:
   ```bash
   SKILL_DIR=".opencode/skills/mcp4unity"
   ```

   For other MCP clients, adjust path accordingly.

2. **Copy skill files**:

   ```bash
   mkdir -p "$SKILL_DIR"
   cp -r Assets/MCP4Unity/MCPServer~/mcp4unity/* "$SKILL_DIR/"
   ```

3. **Install dependencies**:

   ```bash
   cd "$SKILL_DIR/server"
   npm install
   npm run build
   ```

4. **Verify build**:

   ```bash
   test -f "$SKILL_DIR/server/build/index.js" && echo "✅ Server built successfully" || echo "❌ Build failed"
   ```

### Step 3: Configure Unity Paths

1. **Detect Unity installation** (example for common paths):

   ```bash
   # Windows
   UNITY_PATH=$(find /c/Program\ Files/Unity* -name "Unity.exe" 2>/dev/null | head -1)
   
   # macOS
   # UNITY_PATH="/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity"
   
    # Linux
    # UNITY_PATH="$HOME/Unity/Hub/Editor/*/Editor/Unity"
    ```

2. **Configure Unity path**:

   **Option A: Automatic (Recommended)**
   
   Unity automatically generates `unity_config.json` on first launch. Just start Unity and the config will be created.

   **Option B: Manual configuration** (if Unity is not running):

   Via MCP tool:
   ```
   configureunity unityExePath="$UNITY_PATH"
   ```

   Or manually create config file:
   ```bash
   cat > "$SKILL_DIR/unity_config.json" << EOF
   {
     "unityExePath": "$UNITY_PATH",
     "projectPath": "$(pwd)"
   }
   EOF
   ```

### Step 4: Verify Installation

1. **Start Unity** (if not already running):

   ```
   startunity
   ```

2. **Check Unity status**:

   ```
   getunitystatus
   ```

   Expected output:
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

3. **Test basic tool**:

   ```
   gethierarchy
   ```

   Should return list of GameObjects in the scene.

## Troubleshooting

### Issue: `getunitystatus` returns `not_running`

**Solution**:
```bash
# Start Unity manually
startunity

# Wait 30 seconds for Unity to initialize
sleep 30

# Check status again
getunitystatus
```

### Issue: `getunitystatus` returns `editor_mcp_unresponsive`

**Possible causes**:
- Unity is compiling scripts
- Unity is loading assets
- Unity main thread is blocked

**Solution**:
```bash
# Wait for compilation to complete
sleep 10
getunitystatus

# If still unresponsive after 5 minutes, restart Unity
stopunity
deletescriptassemblies
startunity
```

### Issue: `npm install` fails

**Solution**:
```bash
# Clear npm cache
npm cache clean --force

# Retry installation
cd "$SKILL_DIR/server"
rm -rf node_modules package-lock.json
npm install
```

### Issue: Unity path not found

**Solution**:
```bash
# Manually specify Unity path
configureunity unityExePath="/path/to/Unity.exe"
```

Common Unity paths:
- Windows: `C:\Program Files\Unity\Hub\Editor\{version}\Editor\Unity.exe`
- macOS: `/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity`
- Linux: `$HOME/Unity/Hub/Editor/{version}/Editor/Unity`

## Post-Installation

### Verify All Tools Work

```bash
# Management tools
getunitystatus
isunityrunning

# Unity tools
gethierarchy
getactivescene
findassets path="Assets"
```

### Create Test Tool (Optional)

Create `Assets/Editor/MCPTools/TestTool.cs`:

```csharp
using MCP4Unity;

namespace MCP
{
    public class TestTool
    {
        [Tool("Test tool for verification")]
        public static string TestEcho(string message)
        {
            return $"Echo: {message}";
        }
    }
}
```

Wait for Unity to compile, then test:

```
testecho message="Hello MCP4Unity"
```

Expected output: `Echo: Hello MCP4Unity`

## Complete Installation Script

For automated installation, use this script:

```bash
#!/bin/bash
set -e

echo "🚀 Installing MCP4Unity..."

# Step 1: Install Unity package
echo "📦 Installing Unity package..."
git clone https://github.com/Purisky/MCP4Unity.git /tmp/MCP4Unity
cp -r /tmp/MCP4Unity Assets/MCP4Unity
echo "✅ Unity package installed"

# Step 2: Setup MCP server
echo "🔧 Setting up MCP server..."
SKILL_DIR=".opencode/skills/mcp4unity"
mkdir -p "$SKILL_DIR"
cp -r Assets/MCP4Unity/MCPServer~/mcp4unity/* "$SKILL_DIR/"

cd "$SKILL_DIR/server"
npm install
npm run build
cd -
echo "✅ MCP server built"

# Step 3: Configure Unity paths
echo "⚙️  Configuring Unity paths..."
# Note: Unity path must be configured manually via configureunity tool

echo "✅ Installation complete!"
echo ""
echo "Next steps:"
echo "1. Configure Unity path: configureunity unityExePath=\"/path/to/Unity.exe\""
echo "2. Start Unity: startunity"
echo "3. Verify: getunitystatus"
```

## Configuration Reference

### unity_config.json

Location: `{SKILL_DIR}/unity_config.json`

```json
{
  "unityExePath": "/path/to/Unity.exe",
  "projectPath": "/path/to/unity/project"
}
```

- `unityExePath`: **Required**. Path to Unity executable.
- `projectPath`: **Optional**. Auto-detected if omitted (searches upward for `Assets/` and `Library/` folders).

### MCP Server Configuration

For OpenCode, no additional configuration needed (auto-discovered).

For other MCP clients, add to MCP configuration:

```json
{
  "mcpServers": {
    "mcp4unity": {
      "command": "node",
      "args": ["build/index.js"],
      "cwd": "/absolute/path/to/skills/mcp4unity/server"
    }
  }
}
```

## Available Tools After Installation

See [SKILL.md](../MCPServer~/mcp4unity/SKILL.md) for complete tool reference.

### Management Tools
- `configureunity` - Configure Unity paths
- `getunitystatus` - Get detailed Unity status
- `startunity` - Start Unity Editor
- `stopunity` - Stop Unity Editor
- `isunityrunning` - Check if Unity is running
- `deletescriptassemblies` - Clean Unity cache
- `deletescenebackups` - Delete scene recovery files

### Unity Tools
- `gethierarchy` - Get scene hierarchy
- `getactivescene` - Get active scene info
- `findassets` - Find assets by path/type
- `recompileassemblies` - Trigger recompilation
- `getunityconsolelog` - Get Unity console logs
- And 30+ more tools...

## Support

For issues or questions:
- GitHub Issues: https://github.com/Purisky/MCP4Unity/issues
- Documentation: See README.md and SKILL.md
