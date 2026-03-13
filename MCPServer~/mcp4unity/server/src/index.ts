#!/usr/bin/env node

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  Tool,
} from "@modelcontextprotocol/sdk/types.js";
import { UnityClient } from "./unity-client.js";
import { UnityManager } from "./unity-manager.js";

const server = new Server(
  {
    name: "mcp4unity",
    version: "1.0.0",
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

const unityClient = new UnityClient();
const unityManager = new UnityManager();

// Unity 管理工具定义
const MANAGEMENT_TOOLS: Tool[] = [
  {
    name: "configureunity",
    description: "Configure Unity paths (first-time setup)",
    inputSchema: {
      type: "object",
      properties: {
        unityExePath: {
          type: "string",
          description: "Path to Unity.exe",
        },
        projectPath: {
          type: "string",
          description: "Path to Unity project (optional, defaults to current directory)",
        },
      },
      required: ["unityExePath"],
    },
  },
  {
    name: "startunity",
    description: "Start Unity (auto-cleans backups & assemblies)",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
  {
    name: "stopunity",
    description: "Force close Unity",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
  {
    name: "isunityrunning",
    description: "Check if Unity is running (simple boolean check)",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
  {
    name: "getunitystatus",
    description: "Get detailed Unity status (not_running/batchmode/editor_mcp_ready/editor_mcp_unresponsive)",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
  {
    name: "deletescenebackups",
    description: "Delete scene recovery files",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
  {
    name: "deletescriptassemblies",
    description: "Delete ScriptAssemblies cache",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
];

// List tools handler
server.setRequestHandler(ListToolsRequestSchema, async () => {
  try {
    // 获取 Unity 侧的工具列表
    const unityTools = await unityClient.listTools();
    
    // 合并管理工具和 Unity 工具
    return {
      tools: [...MANAGEMENT_TOOLS, ...unityTools],
    };
  } catch (error) {
    // 如果 Unity 未运行，只返回管理工具
    console.error("[MCP4Unity] Failed to list Unity tools:", error);
    return {
      tools: MANAGEMENT_TOOLS,
    };
  }
});

// Call tool handler
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    // 处理管理工具
    const managementResult = await handleManagementTool(name, args || {});
    if (managementResult !== null) {
      return {
        content: [
          {
            type: "text",
            text: managementResult,
          },
        ],
      };
    }

    // 转发到 Unity
    const result = await unityClient.callTool(name, args || {});
    return {
      content: [
        {
          type: "text",
          text: result,
        },
      ],
    };
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);
    return {
      content: [
        {
          type: "text",
          text: `❌ Error: ${errorMessage}`,
        },
      ],
      isError: true,
    };
  }
});

// 处理管理工具
async function handleManagementTool(
  name: string,
  args: Record<string, unknown>
): Promise<string | null> {
  switch (name) {
    case "configureunity": {
      const unityExePath = args.unityExePath as string;
      const projectPath = (args.projectPath as string | undefined) || process.cwd();
      unityManager.saveConfig(unityExePath, projectPath);
      return `✅ Unity configured: ${unityExePath}`;
    }

    case "startunity":
      unityManager.deleteSceneBackups();
      unityManager.deleteScriptAssemblies();
      unityManager.startUnity();
      return "✅ Unity starting (cleaned backups & assemblies)...";

    case "stopunity":
      unityManager.stopUnity();
      return "✅ Unity stopped";

    case "isunityrunning":
      return unityManager.isUnityRunning()
        ? "✅ Unity is running"
        : "❌ Unity is not running";

    case "getunitystatus": {
      const status = await unityManager.getUnityStatus();
      return JSON.stringify(status, null, 2);
    }

    case "deletescenebackups":
      unityManager.deleteSceneBackups();
      return "✅ Scene backups deleted";

    case "deletescriptassemblies":
      unityManager.deleteScriptAssemblies();
      return "✅ ScriptAssemblies deleted";

    default:
      return null;
  }
}

// Start server
async function main() {
  // Handle EPIPE errors to prevent crashes when client disconnects
  process.stdout.on('error', (err) => {
    if (err.code === 'EPIPE') {
      console.error('[MCP4Unity] Client disconnected (EPIPE)');
      process.exit(0);
    }
  });

  process.stdin.on('error', (err) => {
    console.error('[MCP4Unity] stdin error:', err);
  });

  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("[MCP4Unity] Server started");
}

main().catch((error) => {
  console.error("[MCP4Unity] Fatal error:", error);
  process.exit(1);
});
