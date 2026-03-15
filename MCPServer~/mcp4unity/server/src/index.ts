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
    name: "startunity",
    description: "Start Unity Editor (auto-cleans backups & assemblies)",
    inputSchema: {
      type: "object",
      properties: {
        projectPath: {
          type: "string",
          description: "Path to Unity project (optional, uses default project or auto-detects)",
        },
      },
    },
  },
  {
    name: "runbatchmode",
    description: "Run Unity in batchmode to compile and check for errors (recommended for compilation checks)",
    inputSchema: {
      type: "object",
      properties: {
        projectPath: {
          type: "string",
          description: "Path to Unity project (optional, auto-detects from current directory)",
        },
      },
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
    name: "getunitystatus",
    description: "Get Unity status with optional detailed diagnostics (not_running/batchmode/editor_mcp_ready/editor_mcp_unresponsive)",
    inputSchema: {
      type: "object",
      properties: {
        projectPath: {
          type: "string",
          description: "Path to Unity project (optional, uses default project or auto-detects)",
        },
        detailed: {
          type: "boolean",
          description: "Return detailed JSON diagnostics (default: false for simple status string)",
        },
      },
    },
  },
  {
    name: "deletescenebackups",
    description: "Delete scene recovery files",
    inputSchema: {
      type: "object",
      properties: {
        projectPath: {
          type: "string",
          description: "Path to Unity project (optional, uses default project or auto-detects)",
        },
      },
    },
  },
  {
    name: "deletescriptassemblies",
    description: "Delete ScriptAssemblies cache",
    inputSchema: {
      type: "object",
      properties: {
        projectPath: {
          type: "string",
          description: "Path to Unity project (optional, uses default project or auto-detects)",
        },
      },
    },
  },
];

// List tools handler
server.setRequestHandler(ListToolsRequestSchema, async () => {
  try {
    // 使用默认项目路径列出工具
    const defaultProjectPath = unityManager.getDefaultProjectPath();
    const unityTools = await unityClient.listTools(defaultProjectPath);
    
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

    // 转发到 Unity - 从参数中获取项目路径
    const projectPath = (args as any)?.projectPath || unityManager.getDefaultProjectPath();
    const resolvedProjectPath = unityManager.resolveProjectPathPublic(projectPath);
    const result = await unityClient.callTool(resolvedProjectPath, name, args || {});
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
    case "startunity": {
      const projectPath = args.projectPath as string | undefined;
      unityManager.deleteSceneBackups(projectPath);
      unityManager.deleteScriptAssemblies(projectPath);
      unityManager.startUnity(projectPath);
      return "✅ Unity starting (cleaned backups & assemblies)...";
    }

    case "runbatchmode": {
      const projectPath = args.projectPath as string | undefined;
      const output = unityManager.runBatchMode(projectPath);
      return output;
    }

    case "stopunity":
      unityManager.stopUnity();
      return "✅ Unity stopped";

    case "getunitystatus": {
      const projectPath = args.projectPath as string | undefined;
      const detailed = (args.detailed as boolean) ?? false;
      const status = await unityManager.getUnityStatus(projectPath);
      
      if (detailed) {
        return JSON.stringify(status, null, 2);
      } else {
        // 简洁输出
        const statusEmoji = {
          not_running: "❌",
          batchmode: "⚙️",
          editor_mcp_ready: "✅",
          editor_mcp_unresponsive: "⚠️",
        }[status.status] || "❓";
        
        return `${statusEmoji} ${status.message}`;
      }
    }

    case "deletescenebackups": {
      const projectPath = args.projectPath as string | undefined;
      unityManager.deleteSceneBackups(projectPath);
      return "✅ Scene backups deleted";
    }

    case "deletescriptassemblies": {
      const projectPath = args.projectPath as string | undefined;
      unityManager.deleteScriptAssemblies(projectPath);
      return "✅ ScriptAssemblies deleted";
    }

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
