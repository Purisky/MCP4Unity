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

export class UnityClient {
  private httpClient: AxiosInstance;
  private cachedUrl: string | null = null;
  private readonly defaultPort = 8080;

  constructor() {
    this.httpClient = axios.create({
      timeout: 30000,
      proxy: false,
    });
  }

  /**
   * 解析 Unity MCP 服务的 URL
   */
  private async resolveUnityMcpUrl(): Promise<string> {
    if (this.cachedUrl) {
      return this.cachedUrl;
    }

    const projectPath = process.cwd();
    const endpointFile = path.join(
      projectPath,
      "Library",
      "MCP4Unity",
      "mcp_endpoint.json"
    );

    // 尝试读取端点配置文件
    if (fs.existsSync(endpointFile)) {
      try {
        const content = fs.readFileSync(endpointFile, "utf-8");
        const endpoint: McpEndpoint = JSON.parse(content);
        this.cachedUrl = endpoint.url;
        console.error(`[UnityClient] Resolved URL from config: ${this.cachedUrl}`);
        return this.cachedUrl;
      } catch (error) {
        console.error("[UnityClient] Failed to parse endpoint file:", error);
      }
    }

    // 回退到默认端口
    this.cachedUrl = `http://127.0.0.1:${this.defaultPort}/mcp/`;
    console.error(`[UnityClient] Using fallback URL: ${this.cachedUrl}`);
    return this.cachedUrl;
  }

  /**
   * 调用 Unity MCP 服务
   */
  private async callUnityMcpService(
    method: string,
    parameters?: Record<string, unknown>
  ): Promise<string> {
    console.error(`[UnityClient] Calling Unity: method=${method}`);

    try {
      const url = await this.resolveUnityMcpUrl();
      console.error(`[UnityClient] Resolved URL: ${url}`);

      const request = {
        method,
        params: parameters ? JSON.stringify(parameters) : null,
      };

      const response = await this.httpClient.post<McpResponse>(url, request, {
        headers: {
          "Content-Type": "application/json",
        },
      });

      if (response.status !== 200) {
        this.cachedUrl = null;
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const mcpResponse = response.data;
      if (!mcpResponse.success) {
        throw new Error(mcpResponse.error || "Unknown error from Unity");
      }

      return mcpResponse.result;
    } catch (error) {
      // 清除缓存 URL，下次重新解析
      this.cachedUrl = null;
      
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
  async listTools(): Promise<Tool[]> {
    try {
      const result = await this.callUnityMcpService("listtools");
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
    name: string,
    args: Record<string, unknown>
  ): Promise<string> {
    try {
      return await this.callUnityMcpService("calltool", {
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
