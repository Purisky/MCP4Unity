# MCP4Unity

- [中文](./README_CN.md)/[English](./README.md)

## 项目概述

MCP4Unity 是一个 Unity 编辑器扩展，将 Unity 方法以 MCP（Model Context Protocol）工具的形式暴露出来，供 AI 代理通过 HTTP 调用。它在 AI 编程助手和 Unity 编辑器之间架起桥梁，支持实时场景检查、资产管理、编译触发及任意编辑器代码执行——无需离开 AI 工作流。

## 架构

```
AI Agent (stdio) ─► MCPConsole.exe (MCP SDK v1.1.0) ─► HTTP POST ─► Unity Editor (HttpListener)
```

| 组件 | 职责 |
|------|------|
| **MCPConsole.exe** | 独立 .NET 9.0 控制台应用。通过 stdio 与 AI 代理通信 MCP 协议，将 MCP 请求转换为 HTTP 调用发送给 Unity。 |
| **MCPService** | Unity 编辑器侧 `[InitializeOnLoad]` 单例。运行 `HttpListener` 监听本地端口（默认 8080，被占用时自动回退），接收 HTTP 请求并分发工具调用。 |
| **EditorMainThread** | 线程调度层。`calltool` 请求在线程池线程到达，但大多数 Unity API 需要在主线程执行。使用 `ConcurrentQueue<Action>` + `EditorApplication.update` 在主线程排空工作队列。 |
| **MCPFunctionInvoker** | 扫描所有已加载程序集中带 `[Tool]` 特性的静态方法，构建工具注册表，按名称调用。 |

### 后台稳定性（Windows）

当 Unity 编辑器未获得焦点或最小化时，`EditorApplication.update` 可能不会频繁触发。MCP4Unity 使用 Win32 API 唤醒编辑器：

- `PostMessage(WM_NULL)` — 推动消息泵
- `SetTimer` — 向 Unity 消息循环注入周期性 `WM_TIMER` 消息
- `InvalidateRect` — 触发重绘

一个托管 `System.Threading.Timer` 在队列有工作项时每 200ms 轮询，队列清空后自动停止。确保即使 Unity 在后台，工具调用也能在约 100-200ms 内完成。

### Domain Reload 安全

Unity 的 Domain Reload（脚本重编译时触发）会销毁所有托管状态。MCP4Unity 通过以下方式处理：

- `AssemblyReloadEvents.beforeAssemblyReload` — 在 reload 前干净地停止 HTTP 监听器
- `EditorApplication.quitting` — 编辑器退出时停止
- `[InitializeOnLoad]` 静态构造函数 — reload 后重新启动服务

### AssetImportWorker 防护

Unity 6 会派生带 `-batchMode` 参数的 `AssetImportWorker` 子进程。`[InitializeOnLoad]` 静态构造函数通过检查 `Environment.GetCommandLineArgs()` 检测此情况，在 Worker 进程中跳过服务启动，防止端口冲突。

## 主要功能

- 内置 HTTP 服务器，本地动态端口监听（优先 8080，被占用时自动回退）
- 自动扫描和注册带有 `[Tool]` 特性的方法
- 工具管理界面：**Window > MCP Service Manager**
- 编辑器启动时自动开启
- 基于项目隔离的端点发现（`Library/MCP4Unity/mcp_endpoint.json`）
- 支持任意启动顺序（MCPConsole 先启动或 Unity 先启动）
- 后台稳定——即使 Unity 未获焦点，工具调用依然可靠
- Domain Reload 安全——脚本重编译后无需手动重启

## 需求

- Unity 2021.3 或更高版本（已在 Unity 6 / 6000.3.10f1 上测试）
  - Newtonsoft.Json (com.unity.nuget.newtonsoft-json)
- .NET 9.0 运行时（用于 MCPConsole.exe，除非以 self-contained 方式发布）

## 快速开始

1. 将 `MCP4Unity` 文件夹复制到 Unity 项目的 `Assets/` 目录下（或作为 git submodule 添加）

2. 构建 MCPConsole：

```bash
cd Assets/MCP4Unity/MCPConsole~
build.bat
```

这会将 `MCPConsole.exe` 发布到 `MCPConsole~` 文件夹（framework-dependent，需要 .NET 9.0 运行时）。

3. 配置 AI 工具的 MCP 设置：

```json
{
  "mcpServers": {
    "mcp4unity": {
      "command": "<路径>/Assets/MCP4Unity/MCPConsole~/MCPConsole.exe"
    }
  }
}
```

4. 打开 Unity 编辑器——MCP4Unity 自动启动。通过 **Window > MCP Service Manager** 验证，或检查 `Library/MCP4Unity/mcp_endpoint.json` 是否存在。

## 开发工具

创建自己的工具方法：

1. 在 Editor 文件夹中创建 C# 类（如 `Assets/Editor/MCPTools/`）
2. 添加 `public static` 方法并标记 `[Tool("描述")]` 特性
3. 使用 `[Desc("描述")]` 为参数添加文档
4. 工具在下次编译时自动发现——无需手动注册

### 示例

```csharp
using MCP4Unity;
using UnityEngine;
using UnityEditor;

namespace MCP
{
    public class MyTool
    {
        [Tool("回显输入")]
        public static string Echo(
            [Desc("要回显的文本")] string text,
            [Desc("重复次数")] int count = 1)
        {
            return string.Join(", ", Enumerable.Repeat(text, count));
        }

        [Tool("获取场景中所有 GameObject")]
        public static string[] GetAllGameObjects(
            [Desc("是否只返回根物体")] bool topOnly = false)
        {
            var names = new List<string>();
            if (topOnly)
            {
                foreach (var go in UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene().GetRootGameObjects())
                    names.Add(go.name);
            }
            else
            {
                foreach (var go in Object.FindObjectsByType<GameObject>(
                    FindObjectsSortMode.None))
                    names.Add(go.name);
            }
            return names.ToArray();
        }
    }
}
```

### 工具编写规则

| 规则 | 说明 |
|------|------|
| **签名** | `public static`。返回 `string`、`string[]`、`Task<string>` 或任何可 JSON 序列化的类型。 |
| **特性** | 方法上标记 `[Tool("描述")]`（必需）。参数上标记 `[Desc("描述")]`（推荐）。`[ParamDropdown("方法名")]` 用于枚举式选项。 |
| **命名** | MCP 工具名 = 方法名小写化。`GetHierarchy` → `gethierarchy`。 |
| **异步** | 返回 `Task<string>` 并使用 `async`/`await` 处理耗时操作（如编译）。 |
| **错误处理** | 返回描述性错误字符串。不要抛出异常——MCP 桥接层会直接序列化返回值。 |
| **位置** | 项目工具放在 `Assets/Editor/MCPTools/`。不要修改 `Assets/MCP4Unity/`（子模块）。 |

## MCPConsole 技术细节

- **SDK**: [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) v1.1.0
- **目标框架**: .NET 9.0，单文件发布
- **传输**: stdio（MCP 协议）↔ HTTP（Unity 桥接）
- **代理绕过**: `HttpClient` 配置 `UseProxy = false`，避免系统 HTTP 代理干扰
- **端点发现**: 从 Unity 项目读取 `Library/MCP4Unity/mcp_endpoint.json` 获取 HTTP 端口。Unity 未启动时回退为轮询。

## 构建 MCPConsole

```bash
cd MCPConsole~
build.bat
```

`build.bat` 执行：clean → restore → build (Release) → publish (win-x64, 单文件, framework-dependent, ReadyToRun)。输出：`MCPConsole~/MCPConsole.exe`。

如需 self-contained 发布（无需 .NET 运行时依赖）：

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true -o .
```
