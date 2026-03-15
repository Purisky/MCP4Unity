---
name: mcp4unity
description: Unity Editor automation via MCP. Manages Unity lifecycle (start/stop/compile), manipulates scenes/GameObjects/components, and executes editor code. Use for Unity compilation errors, scene inspection, hierarchy modifications, or any Unity Editor task.
mcp:
  mcp4unity:
    type: local
    enabled: true
    command: "node"
    args: ["build/index.js"]
    cwd: "{SKILL_ROOT}/server"
---

# MCP4Unity - Unity Editor Integration

通过 TypeScript MCP Server 与 Unity Editor HTTP 服务通信，实现 AI 代理对 Unity 的实时控制。

## 快速开始

### ⚡ 测试项目优先

**重要**: 测试时优先使用 **CodeOnly** 项目（启动快 ~30s），仅在需要完整功能时使用 **FullProject**（~60s）。

```
startunity projectPath="CodeOnly"  # 推荐
startunity projectPath="FullProject"  # 仅在必要时
```

### 首次配置

在多项目根目录创建 `unity_config.json`：

```json
{
  "defaultProject": "ProjectA",
  "projects": {
    "ProjectA": {
      "projectPath": "C:\\Path\\To\\ProjectA",
      "unityExePath": "C:\\Path\\To\\Unity\\Editor\\Unity.exe",
      "mcpPort": 52429
    }
  }
}
```

### 启动 Unity

```
startunity projectPath="ProjectA"  # 使用项目名
startunity  # 使用默认项目
```

等待 30-60s 后检查：`getunitystatus` → 应返回 `editor_mcp_ready`

---

## 核心工作流

### 编译检查（优先 batchmode）

**重要**: 检查编译错误时优先使用 batchmode（更可靠，不依赖 MCP 服务）。

```
流程：
1. stopunity（如果运行中）
2. runbatchmode（启动 batchmode 编译）
3. 检查输出：
   - ✅ 成功 → 完成
   - ❌ 失败 → 查看前 20 条错误
4. 修复后重复 2-3

runbatchmode 返回：
- 成功/失败状态
- 错误/警告统计
- 前 20 条错误详情（文件:行号 + 信息）
- 完整日志路径

Editor 模式编译（仅 batchmode 不可用时）：
getunitystatus → editor_mcp_ready → recompileassemblies
```

**禁止**: 不要直接调用 `Unity.exe -batchmode`，必须通过 MCP 工具。

### Unity 卡死处理

```
stopunity
deletescriptassemblies projectPath="ProjectA"
startunity projectPath="ProjectA"
```

---

## 工具分类

### Unity 管理工具（无需 Unity 运行）

| 工具 | 用途 |
|------|------|
| `startunity` | 启动 Unity Editor（自动清理缓存） |
| `runbatchmode` | batchmode 编译（推荐用于检查错误） |
| `stopunity` | 强制关闭 Unity |
| `getunitystatus` | 检查状态（简洁/详细模式） |
| `deletescenebackups` | 删除场景备份 |
| `deletescriptassemblies` | 删除脚本缓存 |

**详细文档**: `references/management-tools.md`

### 代码工具

| 工具 | 用途 |
|------|------|
| `recompileassemblies` | 触发编译并返回错误 |
| `getunityconsolelog` | 获取控制台日志 |
| `runcode` | 执行任意静态方法 |

**详细文档**: `references/code-tools.md`

### 场景工具

| 工具 | 用途 |
|------|------|
| `getactivescene` | 获取当前场景信息 |
| `gethierarchy` | 获取层级树 |
| `getgameobjectinfo` | 获取 GameObject 详细信息 |
| `savescene` / `loadscene` | 保存/加载场景 |
| `createscene` | 创建新场景 |

**详细文档**: `references/scene-tools.md`

### 资源工具

| 工具 | 用途 |
|------|------|
| `findassets` | 搜索资源（Unity 搜索语法） |
| `getassetinfo` | 获取资源详细信息 |
| `importasset` | 强制重新导入 |

**详细文档**: `references/asset-tools.md`

### 组件工具

| 工具 | 用途 |
|------|------|
| `getcomponents` | 列出 GameObject 组件 |
| `getserializedproperties` | 获取组件属性 |
| `setserializedproperty` | 修改属性值 |
| `createobject` / `deleteobject` | 创建/删除 GameObject |
| `addcomponent` / `removecomponent` | 添加/移除组件 |

**详细文档**: `references/component-tools.md`

---

## Unity 状态检测

### 简洁模式（默认）

```
getunitystatus
```

返回：
- `❌ Unity is not running`
- `⚙️ Unity is running in batchmode`
- `✅ Unity Editor is running and MCP service is ready`
- `⚠️ Unity Editor is running but MCP service is unresponsive`

### 详细模式

```
getunitystatus(detailed=true)
```

返回 JSON 诊断信息：

| 状态 | 含义 |
|------|------|
| `not_running` | Unity 未运行 |
| `batchmode` | 无头模式（无 MCP） |
| `editor_mcp_ready` | Editor + MCP 就绪 |
| `editor_mcp_unresponsive` | Editor 运行但 MCP 未响应 |

---

## 常见工作流

### 查找并修改 GameObject

```
1. gethierarchy(topOnly=true)
2. getgameobjectinfo("Player")
3. getcomponents("Player")
4. getserializedproperties("Player", "Rigidbody")
5. setserializedproperty("Player", "Rigidbody", "m_Mass", "2.5")
6. savescene()
```

### 搜索并导入资源

```
1. findassets("t:Texture2D", "Assets/Sprites")
2. getassetinfo("Assets/Sprites/hero.png")
3. importasset("Assets/Sprites/hero.png", "ForceUpdate")
```

### 编译并修复错误

```
1. stopunity
2. runbatchmode
3. 查看错误 → 修复代码
4. 重复 2-3 直到成功
```

### 创建并配置 GameObject

```
1. createobject("Enemy", "Enemies")
2. addcomponent("Enemies/Enemy", "Rigidbody")
3. setserializedproperty("Enemies/Enemy", "Rigidbody", "m_Mass", "1.5")
4. savescene()
```

---

## 多项目管理

### 项目切换

```
# 启动项目 A
startunity projectPath="ProjectA"
getunitystatus projectPath="ProjectA"

# 切换到项目 B
stopunity
startunity projectPath="ProjectB"
```

### 并行运行（不同端口）

```json
{
  "projects": {
    "ProjectA": { "mcpPort": 52429 },
    "ProjectB": { "mcpPort": 52430 }
  }
}
```

```
# 同时运行两个项目
startunity projectPath="ProjectA"
startunity projectPath="ProjectB"
```

---

## 故障排除

### MCP 工具无响应

1. `getunitystatus` - 检查状态
2. `getunityconsolelog` - 检查错误
3. 检查状态文件：
   - `Library/MCP4Unity/mcp_endpoint.json` - 进程标记
   - `Library/MCP4Unity/mcp_alive.json` - 心跳文件（每秒更新）
4. 重启：`stopunity` → `startunity`

### Unity 卡死

```
stopunity
deletescriptassemblies
startunity
```

### 编译错误持续

```
stopunity
deletescriptassemblies
startunity
```

**完整故障排除**: `references/troubleshooting.md`

---

## 技术架构

```
AI Agent (stdio) ─► Node.js MCP Server (TypeScript) ─► HTTP POST ─► Unity Editor (MCPService)
```

- **TypeScript Server**: `{SKILL_ROOT}/server/` - MCP stdio ↔ HTTP 转换
- **Unity MCPService**: `Assets/MCP4Unity/` - HTTP 监听器，主线程执行
- **状态文件系统**:
  - `mcp_endpoint.json` - 进程标记（PID、ProjectPath、StartedAtUtc）
  - `mcp_alive.json` - 心跳文件（Port、ConnectedClients[]，每秒更新）
  - 心跳超时：3 秒

**详细架构**: `references/architecture.md`

---

## 参考文档

- **`references/management-tools.md`** - Unity 启停和配置工具
- **`references/code-tools.md`** - 编译、控制台、runcode 工具
- **`references/scene-tools.md`** - 场景和层级管理
- **`references/asset-tools.md`** - 资源查找和导入
- **`references/component-tools.md`** - 组件和属性操作
- **`references/authoring-guide.md`** - 创建自定义工具
- **`references/troubleshooting.md`** - 故障排除
- **`references/architecture.md`** - 技术架构和内部实现

---

## 开发与修改

### 修改 TypeScript Server

```bash
cd {SKILL_ROOT}/server
# 编辑 src/*.ts
npm run build
# 重启 MCP server
```

### 创建自定义工具

参考 `references/authoring-guide.md` 创建 `[Tool]`-attributed 方法。

---

## 目录结构

```
{SKILL_ROOT}/
├── server/
│   ├── src/
│   │   ├── index.ts           # MCP server 入口
│   │   ├── unity-client.ts    # Unity HTTP 通信
│   │   └── unity-manager.ts   # Unity 进程管理
│   ├── build/                 # 编译输出
│   ├── package.json
│   └── tsconfig.json
├── references/                # 详细参考文档
├── mcp.json                   # MCP server 配置
└── SKILL.md                   # 本文件

{UNITY_PROJECT_ROOT}/
├── Assets/
├── ProjectSettings/
├── Library/
│   └── MCP4Unity/
│       ├── mcp_endpoint.json  # 进程标记
│       └── mcp_alive.json     # 心跳文件（每秒更新）
└── unity_config.json          # Unity 路径配置
```

**注意**: `unity_config.json` 存储在 Unity 项目根目录，支持多项目独立配置。
