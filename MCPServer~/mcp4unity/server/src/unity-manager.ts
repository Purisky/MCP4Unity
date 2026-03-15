import * as fs from "fs";
import * as path from "path";
import { spawn, execSync } from "child_process";
import axios from "axios";
import { fileURLToPath } from "url";

interface UnityProjectConfig {
  projectPath: string;
  unityExePath: string;
  mcpPort: number;
}

interface UnityMultiConfig {
  defaultProject?: string; // 默认项目名称
  projects: {
    [projectName: string]: UnityProjectConfig;
  };
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
  private multiConfig: UnityMultiConfig | null = null;
  private configFile: string | null = null;

  constructor() {
    // 延迟初始化 configFile，允许从多项目根目录启动
    // 配置文件统一放在多项目根目录
  }

  /**
   * 查找多项目根目录（包含多个 Unity 项目的父目录）
   * 从当前目录向上查找，直到找到包含至少一个 Unity 项目的目录
   */
  private findMultiProjectRoot(startPath: string): string {
    let currentPath = startPath;
    
    // 向上查找
    while (currentPath !== path.dirname(currentPath)) {
      // 检查当前目录是否是 Unity 项目
      if (this.isUnityProject(currentPath)) {
        // 如果当前目录就是 Unity 项目，返回其父目录作为多项目根目录
        return path.dirname(currentPath);
      }
      
      // 检查当前目录的子目录是否包含 Unity 项目
      try {
        const entries = fs.readdirSync(currentPath, { withFileTypes: true });
        const hasUnityProjects = entries.some(entry => 
          entry.isDirectory() && this.isUnityProject(path.join(currentPath, entry.name))
        );
        
        if (hasUnityProjects) {
          return currentPath;
        }
      } catch (error) {
        // 忽略读取错误，继续向上查找
      }
      
      currentPath = path.dirname(currentPath);
    }
    
    // 如果没找到，返回当前工作目录
    return process.cwd();
  }

  /**
   * 检查目录是否是 Unity 项目（包含 Assets/ 和 ProjectSettings/，严格大小写）
   */
  private isUnityProject(dirPath: string): boolean {
    try {
      const files = fs.readdirSync(dirPath);
      return files.includes("Assets") && files.includes("ProjectSettings");
    } catch (error) {
      return false;
    }
  }

  /**
   * 获取配置文件路径（延迟初始化）
   */
  private getConfigFile(): string {
    if (this.configFile) {
      return this.configFile;
    }

    // 查找多项目根目录
    const multiProjectRoot = this.findMultiProjectRoot(process.cwd());
    this.configFile = path.join(multiProjectRoot, "unity_config.json");
    return this.configFile;
  }

  /**
   * 加载多项目配置
   */
  private loadMultiConfig(): UnityMultiConfig {
    if (this.multiConfig) {
      return this.multiConfig;
    }

    const configFile = this.getConfigFile();
    if (fs.existsSync(configFile)) {
      try {
        const content = fs.readFileSync(configFile, "utf-8");
        this.multiConfig = JSON.parse(content);
        return this.multiConfig!;
      } catch (error) {
        console.error("[UnityManager] Failed to parse config:", error);
      }
    }

    // 返回空配置
    this.multiConfig = { projects: {} };
    return this.multiConfig;
  }

  /**
   * 获取指定项目的配置（支持项目名或项目路径）
   */
  private getProjectConfig(projectPathOrName: string): UnityProjectConfig {
    const multiConfig = this.loadMultiConfig();
    
    // 先尝试作为项目名查找
    if (multiConfig.projects[projectPathOrName]) {
      return multiConfig.projects[projectPathOrName];
    }
    
    // 尝试作为路径查找
    const normalizedPath = path.resolve(projectPathOrName);
    for (const [name, config] of Object.entries(multiConfig.projects)) {
      if (path.resolve(config.projectPath) === normalizedPath) {
        return config;
      }
    }
    
    throw new Error(`Project not configured: ${projectPathOrName}. Please edit unity_config.json manually.`);
  }

  /**
   * 解析项目路径（支持项目名、相对路径、绝对路径）
   */
  private resolveProjectPath(projectPathOrName?: string): string {
    const multiConfig = this.loadMultiConfig();
    
    if (!projectPathOrName) {
      // 尝试使用默认项目
      if (multiConfig.defaultProject) {
        const defaultConfig = multiConfig.projects[multiConfig.defaultProject];
        if (defaultConfig) {
          return path.resolve(defaultConfig.projectPath);
        }
      }

      // 从当前目录向上查找 Unity 项目
      let currentPath = process.cwd();
      while (currentPath !== path.dirname(currentPath)) {
        if (this.isUnityProject(currentPath)) {
          return currentPath;
        }
        currentPath = path.dirname(currentPath);
      }
      throw new Error("Not in a Unity project directory. Please specify projectPath parameter or set a default project.");
    }

    // 尝试作为项目名查找
    if (multiConfig.projects[projectPathOrName]) {
      return path.resolve(multiConfig.projects[projectPathOrName].projectPath);
    }

    // 如果是绝对路径，直接返回
    if (path.isAbsolute(projectPathOrName)) {
      return path.resolve(projectPathOrName);
    }

    // 尝试作为相对路径解析
    const relativePath = path.resolve(process.cwd(), projectPathOrName);
    if (this.isUnityProject(relativePath)) {
      return relativePath;
    }

    // 尝试从多项目根目录解析
    const multiProjectRoot = this.findMultiProjectRoot(process.cwd());
    const fromRoot = path.resolve(multiProjectRoot, projectPathOrName);
    if (this.isUnityProject(fromRoot)) {
      return fromRoot;
    }

    throw new Error(`Unity project not found: ${projectPathOrName}`);
  }

  /**
   * 保存配置（已废弃）
   */
  saveConfig(unityExePath: string, projectPath?: string, mcpPort?: number, setAsDefault?: boolean): void {
    throw new Error("saveConfig has been removed. Please manually edit unity_config.json.");
  }

  /**
   * 获取配置文件路径
   */
  getConfigPath(): string {
    return this.getConfigFile();
  }

  /**
   * 获取项目路径
   */
  private getProjectPath(projectPath?: string): string {
    return this.resolveProjectPath(projectPath);
  }
  
  /**
   * 公共方法：解析项目路径（供外部调用）
   */
  resolveProjectPathPublic(projectPathOrName?: string): string {
    return this.resolveProjectPath(projectPathOrName);
  }
  
  /**
   * 获取默认项目路径
   */
  getDefaultProjectPath(): string {
    return this.resolveProjectPath();
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
   * 检查 MCP 端点文件是否存在且进程存活
   */
  private mcpEndpointExists(projectPath?: string): boolean {
    const targetProjectPath = this.getProjectPath(projectPath);
    const endpointFile = path.join(
      targetProjectPath,
      "Library",
      "MCP4Unity",
      "mcp_endpoint.json"
    );

    if (!fs.existsSync(endpointFile)) {
      return false;
    }

    try {
      const content = fs.readFileSync(endpointFile, "utf-8");
      const data = JSON.parse(content);
      const pid = data.Pid || data.pid;

      if (pid) {
        return this.isProcessAlive(pid);
      }
    } catch (error) {
      return false;
    }

    return false;
  }

  /**
   * 读取 MCP 心跳文件
   */
  private readMcpAlive(projectPath?: string): { port: number; lastHeartbeat: Date; connectedClients: string[] } | null {
    const targetProjectPath = this.getProjectPath(projectPath);
    const aliveFile = path.join(
      targetProjectPath,
      "Library",
      "MCP4Unity",
      "mcp_alive.json"
    );

    if (!fs.existsSync(aliveFile)) {
      return null;
    }

    try {
      const content = fs.readFileSync(aliveFile, "utf-8");
      const data = JSON.parse(content);
      const stats = fs.statSync(aliveFile);
      
      return {
        port: data.Port || data.port,
        lastHeartbeat: stats.mtime,
        connectedClients: data.ConnectedClients || data.connectedClients || [],
      };
    } catch (error) {
      return null;
    }
  }

  /**
   * 检查 MCP 心跳是否有效（3秒内有更新）
   */
  private isMcpAlive(projectPath?: string): boolean {
    const alive = this.readMcpAlive(projectPath);
    if (!alive) {
      return false;
    }

    const now = new Date();
    const timeSinceLastHeartbeat = now.getTime() - alive.lastHeartbeat.getTime();
    return timeSinceLastHeartbeat < 3000; // 3秒超时
  }

  /**
   * 读取 MCP 端点配置（已废弃，保留用于兼容）
   */
  private readMcpEndpoint(projectPath?: string): { port: number; url: string } | null {
    const targetProjectPath = this.getProjectPath(projectPath);
    const endpointFile = path.join(
      targetProjectPath,
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
   * 测试 MCP 服务是否响应（心跳 + HTTP 都要通过）
   */
  private async testMcpResponsive(projectPath?: string): Promise<boolean> {
    // 1. 首先检查心跳文件
    if (!this.isMcpAlive(projectPath)) {
      return false;
    }

    // 2. 心跳有效，再测试 HTTP 连接
    const alive = this.readMcpAlive(projectPath);
    if (!alive) {
      return false;
    }

    const url = `http://127.0.0.1:${alive.port}/mcp/`;

    try {
      const response = await axios.post(
        url,
        {
          method: "listtools",
          params: {},
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
  async getUnityStatus(projectPath?: string): Promise<UnityStatusDetail> {
    const targetProjectPath = this.getProjectPath(projectPath);
    const processRunning = this.isUnityProcessRunning();
    const endpointExists = this.mcpEndpointExists(projectPath);
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

    // Editor 模式但 MCP 端点不存在（进程未启动或已死亡）
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

    // Editor 模式，端点存在，测试 MCP 响应（心跳 + HTTP）
    const mcpResponsive = await this.testMcpResponsive(projectPath);

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
      // 区分心跳超时和 HTTP 失败
      const alive = this.isMcpAlive(projectPath);
      const message = alive
        ? "Unity Editor is running, heartbeat active, but HTTP unresponsive (network issue or service restarting)"
        : "Unity Editor is running but MCP service heartbeat timeout (>3s) - may be compiling, loading, or main thread blocked";
      
      return {
        status: UnityStatus.EDITOR_MCP_UNRESPONSIVE,
        message,
        processRunning: true,
        endpointExists: true,
        mcpResponsive: false,
        batchMode: false,
      };
    }
  }

  /**
   * 启动 Unity
   */
  startUnity(projectPath?: string): void {
    const targetProjectPath = this.getProjectPath(projectPath);
    const config = this.getProjectConfig(targetProjectPath);
    
    if (!config.unityExePath) {
      throw new Error(
        "Unity path not configured. Use configureunity first."
      );
    }

    // 使用 spawn 启动 Unity（跨平台兼容）
    // 添加参数跳过各种阻塞对话框
    const args = [
      "-projectPath", targetProjectPath,
      "-disable-assembly-updater",  // 跳过程序集更新器
      "-accept-apiupdate",           // 自动接受 API 更新
      "-silent-crashes",             // 跳过崩溃恢复对话框
      "-ignoreCompilerErrors"        // 忽略编译错误，跳过 Safe Mode
    ];
    
    try {
      spawn(config.unityExePath, args, {
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
  deleteSceneBackups(projectPath?: string): void {
    const targetProjectPath = this.getProjectPath(projectPath);
    const backupPath = path.join(targetProjectPath, "Temp", "__Backupscenes");

    if (fs.existsSync(backupPath)) {
      fs.rmSync(backupPath, { recursive: true, force: true });
    }
  }

  /**
   * 删除 ScriptAssemblies 缓存
   */
  deleteScriptAssemblies(projectPath?: string): void {
    const targetProjectPath = this.getProjectPath(projectPath);
    const assemblyPath = path.join(targetProjectPath, "Library", "ScriptAssemblies");

    if (fs.existsSync(assemblyPath)) {
      fs.rmSync(assemblyPath, { recursive: true, force: true });
    }
  }

  /**
   * 运行 Unity batchmode 编译
   * 返回结构化的编译结果
   */
  runBatchMode(projectPath?: string): string {
    const targetProjectPath = this.getProjectPath(projectPath);
    const config = this.getProjectConfig(targetProjectPath);

    if (!config.unityExePath) {
      throw new Error("Unity path not configured. Run configureunity first.");
    }

    if (!fs.existsSync(config.unityExePath)) {
      throw new Error(`Unity executable not found: ${config.unityExePath}`);
    }

    // 使用项目特定的日志文件路径，避免多项目混杂
    const logDir = path.join(targetProjectPath, "Logs");
    if (!fs.existsSync(logDir)) {
      fs.mkdirSync(logDir, { recursive: true });
    }
    
    const logFile = path.join(logDir, "batchmode_compile.log");

    let exitCode = 0;

    try {
      execSync(
        `"${config.unityExePath}" -batchmode -projectPath "${targetProjectPath}" -quit -logFile "${logFile}"`,
        {
          encoding: "utf-8",
          timeout: 180000, // 3 分钟超时
          stdio: "ignore", // 不捕获 stdout，直接写入文件
        }
      );
    } catch (error: any) {
      exitCode = error.status || 1;
      // 编译失败是正常情况，继续读取日志
    }

    // 读取日志文件
    let rawOutput = "";
    if (fs.existsSync(logFile)) {
      rawOutput = fs.readFileSync(logFile, "utf-8");
    } else {
      throw new Error(`Log file not found: ${logFile}`);
    }

    // 解析编译结果
    return this.parseBatchModeOutput(rawOutput, exitCode, logFile);
  }

  /**
   * 解析 batchmode 输出，提取关键信息
   */
  private parseBatchModeOutput(
    rawOutput: string,
    exitCode: number,
    logFilePath: string
  ): string {
    const lines = rawOutput.split("\n");
    
    // 提取编译错误和警告
    const errors: Array<{ file: string; line: string; message: string }> = [];
    const warnings: Array<{ file: string; line: string; message: string }> = [];
    
    // 正则匹配编译错误: Assets/...cs(123,45): error CS0246: ...
    const errorRegex = /^(.+?\.cs)\((\d+),\d+\): error (CS\d+): (.+)$/;
    const warningRegex = /^(.+?\.cs)\((\d+),\d+\): warning (CS\d+): (.+)$/;
    
    for (const line of lines) {
      const trimmed = line.trim();
      
      // 匹配错误
      const errorMatch = trimmed.match(errorRegex);
      if (errorMatch) {
        errors.push({
          file: errorMatch[1],
          line: errorMatch[2],
          message: `${errorMatch[3]}: ${errorMatch[4]}`,
        });
        continue;
      }
      
      // 匹配警告
      const warningMatch = trimmed.match(warningRegex);
      if (warningMatch) {
        warnings.push({
          file: warningMatch[1],
          line: warningMatch[2],
          message: `${warningMatch[3]}: ${warningMatch[4]}`,
        });
      }
    }

    // 构建结构化输出
    const result = {
      success: exitCode === 0 && errors.length === 0,
      exitCode,
      summary: {
        totalErrors: errors.length,
        totalWarnings: warnings.length,
      },
      errors: errors.slice(0, 20), // 只返回前 20 条错误
      warnings: warnings.slice(0, 10), // 只返回前 10 条警告
      truncated: {
        errors: errors.length > 20,
        warnings: warnings.length > 10,
      },
      logPath: logFilePath,
    };

    // 格式化输出
    let output = "";
    
    if (result.success) {
      output += "✅ 编译成功\n\n";
    } else {
      output += "❌ 编译失败\n\n";
    }

    output += `📊 统计:\n`;
    output += `  - 错误: ${result.summary.totalErrors}\n`;
    output += `  - 警告: ${result.summary.totalWarnings}\n`;
    output += `  - 退出码: ${result.exitCode}\n\n`;

    if (errors.length > 0) {
      output += `🔴 错误 (显示前 ${Math.min(20, errors.length)} 条):\n\n`;
      result.errors.forEach((err, idx) => {
        output += `${idx + 1}. ${err.file}:${err.line}\n`;
        output += `   ${err.message}\n\n`;
      });
      
      if (result.truncated.errors) {
        output += `⚠️  还有 ${errors.length - 20} 条错误未显示\n`;
        output += `   修复上述错误后重新编译以查看剩余错误\n\n`;
      }
    }

    if (warnings.length > 0 && warnings.length <= 10) {
      output += `⚠️  警告 (显示前 ${Math.min(10, warnings.length)} 条):\n\n`;
      result.warnings.forEach((warn, idx) => {
        output += `${idx + 1}. ${warn.file}:${warn.line}\n`;
        output += `   ${warn.message}\n\n`;
      });
      
      if (result.truncated.warnings) {
        output += `⚠️  还有 ${warnings.length - 10} 条警告未显示\n\n`;
      }
    }

    output += `📄 完整日志: ${result.logPath}\n`;

    return output;
  }
}
