import * as fs from "fs";
import * as path from "path";
import { spawn, execSync } from "child_process";
import axios from "axios";
import { fileURLToPath } from "url";

interface UnityConfig {
  unityExePath: string;
  projectPath: string;
}

export enum UnityStatus {
  NOT_RUNNING = "not_running",
  BATCHMODE = "batchmode",
  EDITOR_MCP_READY = "editor_mcp_ready",
  EDITOR_MCP_UNRESPONSIVE = "editor_mcp_unresponsive",
}

export interface UnityStatusDetail {
  status: UnityStatus;
  message: string;
  processRunning: boolean;
  endpointExists: boolean;
  mcpResponsive: boolean;
  batchMode: boolean;
}

export class UnityManager {
  private config: UnityConfig | null = null;
  private readonly configFile: string;

  constructor() {
    // ES module: get __dirname equivalent
    const __filename = fileURLToPath(import.meta.url);
    const __dirname = path.dirname(__filename);
    
    // 配置文件放在 skill 根目录
    const skillRoot = path.resolve(__dirname, "../..");
    this.configFile = path.join(skillRoot, "unity_config.json");
  }

  /**
   * 加载配置
   */
  private loadConfig(): UnityConfig {
    if (this.config) {
      return this.config;
    }

    if (fs.existsSync(this.configFile)) {
      try {
        const content = fs.readFileSync(this.configFile, "utf-8");
        this.config = JSON.parse(content);
        return this.config!;
      } catch (error) {
        console.error("[UnityManager] Failed to parse config:", error);
      }
    }

    // 默认配置
    this.config = {
      unityExePath: "",
      projectPath: process.cwd(),
    };
    return this.config;
  }

  /**
   * 保存配置
   */
  saveConfig(unityExePath: string, projectPath?: string): void {
    this.config = {
      unityExePath,
      projectPath: projectPath || process.cwd(),
    };

    fs.writeFileSync(
      this.configFile,
      JSON.stringify(this.config, null, 2),
      "utf-8"
    );
  }

  /**
   * 获取配置文件路径
   */
  getConfigPath(): string {
    return this.configFile;
  }

  /**
   * 获取项目路径
   */
  private getProjectPath(): string {
    const config = this.loadConfig();
    if (config.projectPath && config.projectPath !== process.cwd()) {
      return config.projectPath;
    }
    
    // 如果配置的路径就是当前目录，尝试向上查找项目根目录
    let current = process.cwd();
    for (let i = 0; i < 5; i++) {
      const assetsPath = path.join(current, "Assets");
      const libraryPath = path.join(current, "Library");
      if (fs.existsSync(assetsPath) && fs.existsSync(libraryPath)) {
        return current;
      }
      const parent = path.dirname(current);
      if (parent === current) break;
      current = parent;
    }
    
    return config.projectPath || process.cwd();
  }

  /**
   * 检查 Unity 进程是否正在运行
   */
  private isUnityProcessRunning(): boolean {
    try {
      if (process.platform === "win32") {
        // 使用 tasklist.exe 替代 PowerShell，兼容 Git Bash
        const result = execSync(
          'tasklist.exe /FI "IMAGENAME eq Unity.exe" /NH',
          { encoding: "utf-8", timeout: 5000 }
        );
        return result.includes("Unity.exe");
      } else {
        execSync("pgrep -x Unity", { encoding: "utf-8", timeout: 5000 });
        return true;
      }
    } catch (error) {
      console.error("[UnityManager] Failed to check Unity process:", error);
      return false;
    }
  }

  /**
   * 检查是否为 Batchmode（通过检查命令行参数）
   */
  private isUnityBatchMode(): boolean {
    try {
      if (process.platform === "win32") {
        // 使用 wmic 替代 PowerShell，兼容 Git Bash
        const result = execSync(
          'wmic process where "name=\'Unity.exe\'" get commandline /format:list',
          { encoding: "utf-8", timeout: 5000 }
        );
        return result.includes("-batchmode") || result.includes("-batchMode");
      } else {
        const result = execSync("ps aux | grep Unity | grep -i batchmode", {
          encoding: "utf-8",
          timeout: 5000,
        });
        return result.trim().length > 0;
      }
    } catch (error) {
      console.error("[UnityManager] Failed to check batch mode:", error);
      return false;
    }
  }

  /**
   * 检查进程是否存在
   */
  private isProcessAlive(pid: number): boolean {
    try {
      if (process.platform === "win32") {
        // 使用 tasklist.exe 替代 PowerShell
        const result = execSync(
          `tasklist.exe /FI "PID eq ${pid}" /NH`,
          { encoding: "utf-8", timeout: 5000 }
        );
        return result.includes(pid.toString());
      } else {
        execSync(`kill -0 ${pid}`, { timeout: 5000 });
        return true;
      }
    } catch (error) {
      return false;
    }
  }

  /**
   * 检查 MCP 端点文件是否存在且有效
   */
  private mcpEndpointExists(): boolean {
    const endpoint = this.readMcpEndpoint();
    if (!endpoint) {
      return false;
    }

    // 如果端点文件包含 PID，验证进程是否存活
    const projectPath = this.getProjectPath();
    const endpointFile = path.join(
      projectPath,
      "Library",
      "MCP4Unity",
      "mcp_endpoint.json"
    );

    try {
      const content = fs.readFileSync(endpointFile, "utf-8");
      const data = JSON.parse(content);
      const pid = data.Pid || data.pid;

      if (pid) {
        return this.isProcessAlive(pid);
      }
    } catch (error) {
      // 无法读取或解析，认为无效
      return false;
    }

    return true;
  }

  /**
   * 读取 MCP 端点配置
   */
  private readMcpEndpoint(): { port: number; url: string } | null {
    const projectPath = this.getProjectPath();
    const endpointFile = path.join(
      projectPath,
      "Library",
      "MCP4Unity",
      "mcp_endpoint.json"
    );

    if (!fs.existsSync(endpointFile)) {
      return null;
    }

    try {
      const content = fs.readFileSync(endpointFile, "utf-8");
      const data = JSON.parse(content);
      // Unity 使用大写字段名 (Url, Port)
      return {
        url: data.Url || data.url,
        port: data.Port || data.port,
      };
    } catch (error) {
      return null;
    }
  }

  /**
   * 测试 MCP 服务是否响应
   */
  private async testMcpResponsive(): Promise<boolean> {
    const endpoint = this.readMcpEndpoint();
    if (!endpoint) {
      return false;
    }

    try {
      const response = await axios.post(
        endpoint.url,
        {
          method: "listtools",
          params: null,
        },
        {
          timeout: 3000,
          proxy: false,
        }
      );
      return response.status === 200 && response.data.success === true;
    } catch (error) {
      return false;
    }
  }

  /**
   * 获取详细的 Unity 状态
   */
  async getUnityStatus(): Promise<UnityStatusDetail> {
    const processRunning = this.isUnityProcessRunning();
    const endpointExists = this.mcpEndpointExists();
    const batchMode = processRunning ? this.isUnityBatchMode() : false;

    // 未启动
    if (!processRunning) {
      return {
        status: UnityStatus.NOT_RUNNING,
        message: "Unity is not running",
        processRunning: false,
        endpointExists: false,
        mcpResponsive: false,
        batchMode: false,
      };
    }

    // Batchmode
    if (batchMode) {
      return {
        status: UnityStatus.BATCHMODE,
        message: "Unity is running in batchmode (headless)",
        processRunning: true,
        endpointExists: false,
        mcpResponsive: false,
        batchMode: true,
      };
    }

    // Editor 模式但 MCP 端点不存在
    if (!endpointExists) {
      return {
        status: UnityStatus.EDITOR_MCP_UNRESPONSIVE,
        message:
          "Unity Editor is running but MCP service not started (check Unity Console for errors)",
        processRunning: true,
        endpointExists: false,
        mcpResponsive: false,
        batchMode: false,
      };
    }

    // Editor 模式，端点存在，测试响应
    const mcpResponsive = await this.testMcpResponsive();

    if (mcpResponsive) {
      return {
        status: UnityStatus.EDITOR_MCP_READY,
        message: "Unity Editor is running and MCP service is ready",
        processRunning: true,
        endpointExists: true,
        mcpResponsive: true,
        batchMode: false,
      };
    } else {
      return {
        status: UnityStatus.EDITOR_MCP_UNRESPONSIVE,
        message:
          "Unity Editor is running but MCP service is unresponsive (may be compiling, loading, or main thread blocked)",
        processRunning: true,
        endpointExists: true,
        mcpResponsive: false,
        batchMode: false,
      };
    }
  }

  /**
   * 检查 Unity 是否正在运行（简化版，保持向后兼容）
   */
  isUnityRunning(): boolean {
    return this.mcpEndpointExists();
  }

  /**
   * 启动 Unity
   */
  startUnity(): void {
    const config = this.loadConfig();
    if (!config.unityExePath) {
      throw new Error(
        "Unity path not configured. Use configureunity first."
      );
    }

    const projectPath = this.getProjectPath();

    // 使用 spawn 启动 Unity（跨平台兼容）
    try {
      spawn(config.unityExePath, ["-projectPath", projectPath], {
        detached: true,
        stdio: "ignore",
        shell: false,
      }).unref();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      throw new Error(`Failed to start Unity: ${errorMessage}`);
    }
  }

  /**
   * 停止 Unity
   */
  stopUnity(): void {
    if (process.platform === "win32") {
      try {
        // 使用 taskkill.exe 替代 PowerShell
        execSync("taskkill.exe /F /IM Unity.exe", {
          timeout: 10000,
        });
      } catch (error) {
        // 进程可能不存在，忽略错误
        console.error("[UnityManager] Failed to stop Unity (may not be running):", error);
      }
    } else {
      try {
        execSync("pkill -9 Unity", { timeout: 10000 });
      } catch (error) {
        // 进程可能不存在，忽略错误
        console.error("[UnityManager] Failed to stop Unity (may not be running):", error);
      }
    }
  }

  /**
   * 删除场景备份文件
   */
  deleteSceneBackups(): void {
    const projectPath = this.getProjectPath();
    const backupPath = path.join(projectPath, "Temp", "__Backupscenes");

    if (fs.existsSync(backupPath)) {
      fs.rmSync(backupPath, { recursive: true, force: true });
    }
  }

  /**
   * 删除 ScriptAssemblies 缓存
   */
  deleteScriptAssemblies(): void {
    const projectPath = this.getProjectPath();
    const assemblyPath = path.join(projectPath, "Library", "ScriptAssemblies");

    if (fs.existsSync(assemblyPath)) {
      fs.rmSync(assemblyPath, { recursive: true, force: true });
    }
  }
}
