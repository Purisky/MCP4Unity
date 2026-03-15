import axios, { AxiosInstance } from "axios";
import { Tool } from "@modelcontextprotocol/sdk/types.js";
import * as fs from "fs";
import * as path from "path";

interface McpEndpoint {
  port: number;
  url: string;
  Url?: string;
  Port?: number;
}

interface McpResponse {
  success: boolean;
  result: string;
  error?: string;
}

interface UnityToolDefinition {
  name: string;
  description: string;
  parameters: Array<{
    name: string;
    type: string;
    description: string;
    required: boolean;
  }>;
}

interface UnityMultiConfig {
  defaultProject?: string;
  projects: {
    [key: string]: {
      projectPath: string;
      unityExePath: string;
      mcpPort: number;
    };
  };
}

export class UnityClient {
  private httpClient: AxiosInstance;
  private readonly defaultPort = 52429;

  constructor() {
    this.httpClient = axios.create({
      timeout: 30000,
      proxy: false,
    });
  }

  /**
   * 从配置文件读取项目的端口号
   */
  private getPortFromConfig(projectPath: string): number {
    try {
      // 向上查找 unity_config.json
      let currentPath = projectPath;
      while (currentPath) {
        const configPath = path.join(currentPath, "unity_config.json");
        if (fs.existsSync(configPath)) {
          const content = fs.readFileSync(configPath, "utf-8");
          const config: UnityMultiConfig = JSON.parse(content);
          
          // 遍历所有项目，匹配当前项目路径
          if (config.projects) {
            for (const projectConfig of Object.values(config.projects)) {
              const normalizedConfigPath = path.resolve(projectConfig.projectPath);
              const normalizedProjectPath = path.resolve(projectPath);
              
              if (normalizedConfigPath === normalizedProjectPath) {
                console.error(`[UnityClient] Found port ${projectConfig.mcpPort} for project ${projectPath}`);
                return projectConfig.mcpPort;
              }
            }
          }
        }
        
        const parentPath = path.dirname(currentPath);
        if (parentPath === currentPath) break;
        currentPath = parentPath;
      }
    } catch (error) {
      console.error("[UnityClient] Failed to read config:", error);
    }
    
    console.error(`[UnityClient] No config found for ${projectPath}, using default port ${this.defaultPort}`);
    return this.defaultPort;
  }

  /**
   * 解析 Unity MCP 服务的 URL
   */
  private resolveUnityMcpUrl(projectPath: string): string {
    const port = this.getPortFromConfig(projectPath);
    const url = `http://127.0.0.1:${port}/mcp/`;
    console.error(`[UnityClient] Using URL: ${url}`);
    return url;
  }

  /**
   * 调用 Unity MCP 服务
   */
  private async callUnityMcpService(
    projectPath: string,
    method: string,
    parameters?: Record<string, unknown>
  ): Promise<string> {
    console.error(`[UnityClient] Calling Unity: method=${method}, project=${projectPath}`);

    try {
      const url = this.resolveUnityMcpUrl(projectPath);
      console.error(`[UnityClient] Resolved URL: ${url}`);

      const request = {
        method,
        params: parameters || {},
      };

      const response = await this.httpClient.post<McpResponse>(url, request, {
        headers: {
          "Content-Type": "application/json",
        },
      });

      if (response.status !== 200) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const mcpResponse = response.data;
      if (!mcpResponse.success) {
        throw new Error(mcpResponse.error || "Unknown error from Unity");
      }

      return mcpResponse.result;
    } catch (error) {
      if (axios.isAxiosError(error)) {
        if (error.code === "ECONNREFUSED") {
          throw new Error(
            "Unity is not running or MCP service is not started. Use 'startunity' to launch Unity."
          );
        }
        if (error.code === "ETIMEDOUT") {
          throw new Error(
            "Unity MCP service timeout. Unity may be busy or frozen."
          );
        }
        if (error.code === "ECONNRESET") {
          throw new Error(
            "Unity connection reset. Unity may have crashed or restarted."
          );
        }
        throw new Error(`HTTP error: ${error.message}`);
      }
      
      // 捕获所有其他错误，避免崩溃
      const errorMessage = error instanceof Error ? error.message : String(error);
      throw new Error(`Unity communication failed: ${errorMessage}`);
    }
  }

  /**
   * 列出 Unity 侧的所有工具
   */
  async listTools(projectPath: string): Promise<Tool[]> {
    try {
      const result = await this.callUnityMcpService(projectPath, "listtools");
      const unityTools: UnityToolDefinition[] = JSON.parse(result);

      return unityTools.map((tool) => this.convertUnityToolToMcpTool(tool));
    } catch (error) {
      console.error("[UnityClient] Failed to list tools:", error);
      // 返回空数组而不是抛出错误，让服务器继续运行
      return [];
    }
  }

  /**
   * 调用 Unity 侧的工具
   */
  async callTool(
    projectPath: string,
    name: string,
    args: Record<string, unknown>
  ): Promise<string> {
    try {
      return await this.callUnityMcpService(projectPath, "calltool", {
        name,
        arguments: args,
      });
    } catch (error) {
      // 重新抛出错误，但确保错误消息清晰
      const errorMessage = error instanceof Error ? error.message : String(error);
      throw new Error(`Failed to call Unity tool '${name}': ${errorMessage}`);
    }
  }

  /**
   * 转换 Unity 工具定义为 MCP Tool 格式
   */
  private convertUnityToolToMcpTool(unityTool: UnityToolDefinition): Tool {
    const properties: Record<string, { type: string; description: string }> = {};
    const required: string[] = [];

    for (const param of unityTool.parameters) {
      properties[param.name] = {
        type: this.mapUnityTypeToJsonSchema(param.type),
        description: param.description,
      };

      if (param.required) {
        required.push(param.name);
      }
    }

    return {
      name: unityTool.name,
      description: unityTool.description,
      inputSchema: {
        type: "object",
        properties,
        required: required.length > 0 ? required : undefined,
      },
    };
  }

  /**
   * 映射 Unity 类型到 JSON Schema 类型
   */
  private mapUnityTypeToJsonSchema(unityType: string): string {
    const typeMap: Record<string, string> = {
      String: "string",
      Int32: "number",
      Boolean: "boolean",
      Single: "number",
      Double: "number",
      "String[]": "array",
      "Int32[]": "array",
    };

    return typeMap[unityType] || "string";
  }
}
