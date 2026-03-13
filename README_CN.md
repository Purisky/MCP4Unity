# MCP4Unity

- [中文](./README_CN.md) / [English](./README.md)

## 项目概述

MCP4Unity 是一个 Unity Editor 扩展，将 Unity 方法暴露为 MCP (Model Context Protocol) 工具，可通过 HTTP 被 AI 代理调用。它在 AI 编码助手和 Unity Editor 之间架起桥梁，实现实时场景检查、资源管理、编译和任意 Editor 代码执行——无需离开 AI 工作流。

## 架构

```
AI Agent (stdio) ─► Node.js MCP Server (TypeScript) ─► HTTP POST ─► Unity Editor (MCPService)
```

### 组件

| 组件 | 职责 |
|------|------|
| **MCP Server (TypeScript)** | 通过 stdio 与 AI 代理通信（MCP 协议），将请求转换为 HTTP 调用发送到 Unity。管理 Unity 进程生命周期。 |
| **MCPService** | Unity Editor 侧的 `[InitializeOnLoad]` 单例。在本地端口运行 `HttpListener`（默认 8080，自动回退）。 |
| **EditorMainThread** | 线程编排层。使用 `ConcurrentQueue<Action>` + `EditorApplication.update` 在主线程执行 Unity API 调用。 |
| **MCPFunctionInvoker** | 扫描程序集中带 `[Tool]` 特性的方法，构建工具注册表，按名称调用。 |

### 后台稳定性（Windows）

当 Unity Editor 失焦/最小化时，`EditorApplication.update` 可能不频繁触发。MCP4Unity 使用 Win32 API 唤醒编辑器：

- `PostMessage(WM_NULL)` — 推动消息泵
- `SetTimer` — 注入周期性 `WM_TIMER` 消息
- `InvalidateRect` — 触发重绘

托管的 `System.Threading.Timer` 在有工作项排队时每 200ms 轮询一次，确保即使 Unity 在后台，工具调用也能在 ~100-200ms 内完成。

### 域重载安全

Unity 的域重载（由脚本重新编译触发）会销毁所有托管状态。MCP4Unity 通过以下方式处理：

- `AssemblyReloadEvents.beforeAssemblyReload` — 重载前停止 HTTP 监听器
- `EditorApplication.quitting` — 编辑器退出时停止
- `[InitializeOnLoad]` 静态构造函数 — 重载后重启服务

### AssetImportWorker 保护

Unity 6 会以 `-batchMode` 启动 `AssetImportWorker` 子进程。`[InitializeOnLoad]` 静态构造函数检测到这种情况会跳过服务启动，防止端口冲突。

## 主要特性

- **Unity 进程管理**：从 AI 代理启动/停止/清理 Unity
- **详细状态检测**：区分未运行、batchmode、MCP 就绪和 MCP 无响应状态
- **源码内嵌**：所有 TypeScript 代码在 `MCPServer~/mcp4unity/` — 即改即用
- 内置 HTTP 服务器，动态本地端口（优先 8080，自动回退）
- 自动扫描和注册带 `[Tool]` 特性的方法
- 工具管理 UI：**Window > MCP Service Manager**
- 编辑器启动时自动启动
- 项目隔离的端点发现（`Library/MCP4Unity/mcp_endpoint.json`）
- 后台稳定性 — 即使 Unity 失焦，工具调用也能可靠工作
- 域重载安全 — 在脚本重新编译后存活

## 系统要求

### Unity 侧
- Unity 2021.3 或更高版本（在 Unity 6 / 6000.3.10f1 上测试）
- Newtonsoft.Json (com.unity.nuget.newtonsoft-json)

### MCP 服务器侧
- Node.js 18+（用于运行 TypeScript MCP 服务器）

## 安装

### 给 AI 代理

**自动化安装指南**：查看 [INSTALL_FOR_AGENTS.md](./INSTALL_FOR_AGENTS.md) 获取为 AI 代理优化的分步说明。

### 给人类开发者

1. **克隆此仓库**：
   ```bash
   git clone https://github.com/Purisky/MCP4Unity.git
   ```

2. **复制到 Unity 项目**：
   ```bash
   cp -r MCP4Unity /path/to/YourUnityProject/Assets/
   ```

3. **设置 MCP 服务器**：
   ```bash
   # 将 skill 复制到 MCP 客户端的 skill 目录
   cp -r Assets/MCP4Unity/MCPServer~/mcp4unity /path/to/skills/
   
   # 安装依赖
   cd /path/to/skills/mcp4unity/server
   npm install
   npm run build
   ```

4. **配置 Unity 路径**：
   
   Unity 首次启动时会自动生成 `unity_config.json`。如需手动配置：
   ```
   configureunity unityExePath="/path/to/Unity.exe"
   ```

5. **验证安装**：
   ```
   getunitystatus
   ```

详细说明、故障排除和配置选项，请参阅 [INSTALL_FOR_AGENTS.md](./INSTALL_FOR_AGENTS.md)。

## 快速开始

### Unity 管理工具

```bash
# 配置 Unity 路径（仅首次）
configureunity unityExePath="/path/to/Unity.exe"

# 检查 Unity 状态
getunitystatus

# 启动 Unity
startunity

# 停止 Unity
stopunity

# 清理 Unity 缓存
deletescriptassemblies
```

### Unity 状态检测

`getunitystatus` 返回详细状态：

- `not_running`: Unity 进程未找到
- `batchmode`: Unity 以无头/batchmode 运行（无 MCP 服务）
- `editor_mcp_ready`: Unity Editor 运行，MCP 服务响应
- `editor_mcp_unresponsive`: Unity Editor 运行但 MCP 无响应（编译中/加载中/阻塞）

### Unity 工具

```bash
# 获取层级结构
gethierarchy

# 获取活动场景
getactivescene

# 查找资源
findassets path="Assets/Prefabs"

# 重新编译程序集
recompileassemblies

# 获取 Unity 控制台日志
getunityconsolelog filter="error"
```

## 开发工具

创建自定义工具方法：

1. 在 Editor 文件夹创建 C# 类（例如 `Assets/Editor/MCPTools/`）
2. 添加带 `[Tool("description")]` 特性的 `public static` 方法
3. 在参数上使用 `[Desc("description")]` 添加文档
4. 工具在下次编译时自动发现 — 无需注册

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

        [Tool("获取层级中的所有 GameObject")]
        public static string[] GetAllGameObjects(
            [Desc("如果为 true，仅根对象")] bool topOnly = false)
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

| 规则 | 详情 |
|------|------|
| **签名** | `public static`。返回 `string`、`string[]`、`Task<string>` 或任何 JSON 可序列化类型。 |
| **特性** | 方法上的 `[Tool("desc")]`（必需）。参数上的 `[Desc("desc")]`（推荐）。`[ParamDropdown("method")]` 用于枚举类选项。 |
| **命名** | MCP 工具名 = 方法名小写。`GetHierarchy` → `gethierarchy`。 |
| **异步** | 对于长时间运行的操作（如编译），返回 `Task<string>` 并使用 `async`/`await`。 |
| **错误处理** | 返回描述性错误字符串。不要抛出异常 — MCP 桥接直接序列化返回值。 |
| **位置** | 项目工具放在 `Assets/Editor/MCPTools/`。不要修改 `Assets/MCP4Unity/`。 |

## 内置工具

MCP4Unity 提供了丰富的内置工具，详见 [MCPServer~/mcp4unity/SKILL.md](./MCPServer~/mcp4unity/SKILL.md)：

### 代码工具
- `recompileassemblies` - 触发程序集重新编译
- `getunityconsolelog` - 获取 Unity 控制台日志（支持过滤）
- `clearunityconsolelog` - 清除控制台日志

### 层级工具
- `gethierarchy` - 获取场景层级结构
- `getgameobject` - 获取 GameObject 详细信息
- `creategameobject` - 创建新 GameObject
- `deletegameobject` - 删除 GameObject
- `setgameobjectactive` - 设置 GameObject 激活状态

### 组件工具
- `getcomponents` - 获取 GameObject 上的组件
- `addcomponent` - 添加组件到 GameObject
- `removecomponent` - 从 GameObject 移除组件
- `getcomponentproperties` - 获取组件属性
- `setcomponentproperty` - 设置组件属性

### 场景工具
- `getactivescene` - 获取活动场景信息
- `getallscenes` - 获取所有已加载场景
- `loadscene` - 加载场景
- `savescene` - 保存场景

### 资源工具
- `findassets` - 查找资源
- `getassetinfo` - 获取资源详细信息
- `createasset` - 创建资源
- `deleteasset` - 删除资源

### Unity 管理工具
- `configureunity` - 配置 Unity 路径
- `getunitystatus` - 获取详细 Unity 状态
- `startunity` - 启动 Unity
- `stopunity` - 停止 Unity
- `isunityrunning` - 检查 Unity 是否运行
- `deletescriptassemblies` - 删除编译缓存
- `deletescenebackups` - 删除场景备份

完整工具列表和详细文档请参考 [SKILL.md](./MCPServer~/mcp4unity/SKILL.md)。

## 故障排除

### MCP 服务未启动

1. 检查 Unity 控制台是否有错误
2. 验证 `Library/MCP4Unity/mcp_endpoint.json` 是否存在
3. 检查 **Window > MCP Service Manager** 中的状态

### 工具调用超时

1. 使用 `getunitystatus` 检查状态
2. 如果是 `editor_mcp_unresponsive`，等待编译/加载完成
3. 如果持续无响应超过 5 分钟：
   ```bash
   stopunity
   deletescriptassemblies
   startunity
   ```

### 端口冲突

MCP4Unity 会自动尝试备用端口。如果仍有问题：

1. 检查哪个进程占用了端口 8080
2. 关闭该进程或让 MCP4Unity 使用备用端口

### 编译错误

1. 使用 `getunityconsolelog filter="error"` 获取详细错误
2. 修复错误后使用 `recompileassemblies` 重新编译

## 开发与修改

### 修改 TypeScript 服务器

```bash
cd Assets/MCP4Unity/MCPServer~/mcp4unity/server
npm run build
```

修改后立即生效 — 无需重启 AI 代理。

### 添加新的 Unity 工具

1. 在 `Assets/Editor/MCPTools/` 创建新的 C# 文件
2. 添加带 `[Tool]` 特性的静态方法
3. Unity 重新编译后工具自动可用

### 调试

- **Unity 侧**：使用 Unity 控制台和 **Window > MCP Service Manager**
- **MCP 服务器侧**：查看 AI 代理的日志输出

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！

## 相关链接

- GitHub: https://github.com/Purisky/MCP4Unity
- Model Context Protocol: https://modelcontextprotocol.io/
