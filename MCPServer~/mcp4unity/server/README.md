# MCP4Unity TypeScript Server

TypeScript implementation of the MCP4Unity server for Unity Editor integration.

## Quick Start

```bash
# Install dependencies
npm install

# Build
npm run build

# Watch mode (auto-rebuild on changes)
npm run watch
```

## Architecture

```
AI Agent (stdio) ─► Node.js MCP Server ─► HTTP ─► Unity Editor (MCPService)
```

### Components

- **index.ts**: MCP server entry point, handles stdio transport and tool routing
- **unity-client.ts**: HTTP client for Unity communication, reads endpoint config
- **unity-manager.ts**: Unity process management (start/stop/clean)

## Configuration

Unity paths are stored in `../unity_config.json` (created by `configureunity` tool):

```json
{
  "unityExePath": "/path/to/Unity.exe",
  "projectPath": "/path/to/unity/project"
}
```

The `projectPath` is optional and will be auto-detected by searching upward from the current directory for a folder containing `Assets/` and `Library/`.

## Development

### Adding New Management Tools

Edit `index.ts` and add to `MANAGEMENT_TOOLS` array and `handleManagementTool()` function.

### Modifying Unity Communication

Edit `unity-client.ts` to change HTTP request format or error handling.

### Testing

Run the server standalone:

```bash
node build/index.js
```

Then send MCP requests via stdin (JSON-RPC format).

## Dependencies

- `@modelcontextprotocol/sdk`: MCP protocol implementation
- `axios`: HTTP client for Unity communication

## License

Same as MCP4Unity parent project.
