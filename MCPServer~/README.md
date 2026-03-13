# MCP4Unity TypeScript Server

TypeScript implementation of the MCP4Unity server for Unity Editor integration.

## Installation

### For End Users

1. **Copy this directory to your AI agent's skill directory**

   For OpenCode users:
   ```bash
   cp -r mcp4unity /path/to/your/project/.opencode/skills/
   ```

   For other MCP clients, copy to your MCP skills directory.

2. **Install dependencies**

   ```bash
   cd /path/to/skills/mcp4unity/server
   npm install
   npm run build
   ```

3. **Configure your AI agent**

   For OpenCode, the skill is auto-discovered from `.opencode/skills/mcp4unity/`.

   For other MCP clients, add to your MCP configuration:

   ```json
   {
     "mcpServers": {
       "mcp4unity": {
         "command": "node",
         "args": ["build/index.js"],
         "cwd": "/path/to/skills/mcp4unity/server"
       }
     }
   }
   ```

4. **First-time setup**

   When you first use the skill, configure Unity paths:
   ```
   configureunity unityExePath="/path/to/Unity.exe"
   ```

   The project path is auto-detected. Configuration is saved to `mcp4unity/unity_config.json`.

## Features

- **Unity process management**: Start/stop/clean Unity from AI agent
- **Detailed status detection**: Distinguish between not running, batchmode, MCP ready, and MCP unresponsive states
- **Auto-detection**: Project path automatically detected by searching upward for Unity project root
- **Source-embedded**: All code in `server/` directory — modify and rebuild instantly
- **No .NET SDK required**: Only requires Node.js

## Documentation

See [SKILL.md](./SKILL.md) for complete documentation including:
- Available tools and parameters
- Unity status detection
- Compilation workflow
- Tool authoring guide
- Development and modification guide

## Requirements

- Node.js 18+
- Unity 2021.3+ with MCP4Unity package installed

## Quick Start

After installation, use these tools:

```bash
# Configure Unity path (first time only)
configureunity unityExePath="/path/to/Unity.exe"

# Check Unity status
getunitystatus

# Start Unity
startunity

# Stop Unity
stopunity
```

## Development

### Rebuilding

```bash
cd server
npm run build
```

### Watch Mode

```bash
cd server
npm run watch
```

Changes take effect immediately after rebuild.

## License

Same as MCP4Unity parent project.
