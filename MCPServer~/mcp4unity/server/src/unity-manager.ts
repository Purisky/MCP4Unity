import * as fs from "fs";
import * as path from "path";
import { spawn, execSync } from "child_process";
import axios from "axios";
import { fileURLToPath } from "url";

interface UnityConfig {
  unityExePath: string;
  projectPath: string;
  mcpPort?: number; // Unity Editor MCP 服务端口，默认 52429
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
    // 配置文件放在 Unity 项目根目录
    // 从当前工作目录向上查找 Unity 项目根目录
    const unityProjectRoot = this.findUnityProjectRoot(process.cwd());
    this.configFile = path.join(unityProjectRoot, "unity_config.json");
  }

  /**
   * 查找 Unity 项目根目录（包含 Assets/ 和 ProjectSettings/ 的目录）
   */
  private findUnityProjectRoot(startPath: string): string {
    let currentPath = startPath;
    
    // 向上查找，直到找到 Unity 项目标志或到达根目录
    while (currentPath !== path.dirname(currentPath)) {
      const assetsDir = path.join(currentPath, "Assets");
      const projectSettingsDir = path.join(currentPath, "ProjectSettings");
      
      // 检查是否同时存在 Assets 和 ProjectSettings 目录
      if (fs.existsSync(assetsDir) && fs.existsSync(projectSettingsDir)) {
        return currentPath;
      }
      
      currentPath = path.dirname(currentPath);
    }
    
    // 如果没找到 Unity 项目，返回当前工作目录
    return process.cwd();
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
  saveConfig(unityExePath: string, projectPath?: string, mcpPort?: number): void {
    const targetProjectPath = projectPath || this.findUnityProjectRoot(process.cwd());
    
    // 加载现有配置以保留 mcpPort（如果已存在）
    const targetConfigFile = path.join(targetProjectPath, "unity_config.json");
    let existingConfig: Partial<UnityConfig> = {};
    if (fs.existsSync(targetConfigFile)) {
      try {
        existingConfig = JSON.parse(fs.readFileSync(targetConfigFile, "utf-8"));
      } catch (error) {
        // 忽略解析错误，使用新配置
      }
    }
    
    this.config = {
      unityExePath,
      projectPath: targetProjectPath,
      mcpPort: mcpPort ?? existingConfig.mcpPort ?? 52429, // 优先使用传入值，其次现有值，最后默认值
    };

    fs.writeFileSync(
      targetConfigFile,
      JSON.stringify(this.config, null, 2),
      "utf-8"
    );
    
    // 更新实例的 configFile 路径
    (this as any).configFile = targetConfigFile;
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
    // 添加参数跳过各种阻塞对话框
    const args = [
      "-projectPath", projectPath,
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

  /**
   * 运行 Unity batchmode 编译
   * 返回结构化的编译结果
   */
  runBatchMode(): string {
    const config = this.loadConfig();
    const projectPath = this.getProjectPath();

    if (!config.unityExePath) {
      throw new Error("Unity path not configured. Run configureunity first.");
    }

    if (!fs.existsSync(config.unityExePath)) {
      throw new Error(`Unity executable not found: ${config.unityExePath}`);
    }

    // 使用项目特定的日志文件路径，避免多项目混杂
    const logDir = path.join(projectPath, "Logs");
    if (!fs.existsSync(logDir)) {
      fs.mkdirSync(logDir, { recursive: true });
    }
    
    const logFile = path.join(logDir, "batchmode_compile.log");

    let exitCode = 0;

    try {
      execSync(
        `"${config.unityExePath}" -batchmode -projectPath "${projectPath}" -quit -logFile "${logFile}"`,
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
