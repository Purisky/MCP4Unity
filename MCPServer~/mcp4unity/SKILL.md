---
name: mcp4unity
description: (project - Skill) MCP4Unity framework for Unity Editor interaction. Includes Unity management (start/stop/clean), compilation workflow, scene/asset/hierarchy manipulation, component editing, property modification, and tool authoring. Use for: compile/build/fix errors, start/stop Unity, inspect/modify hierarchy/assets/components, create/delete GameObjects, add/remove components, set properties, manage scenes, run editor code, create MCP tools. Triggers: 'compile', 'build', 'start unity', 'stop unity', 'hierarchy', 'scene', 'GameObject', 'component', 'property', 'find asset', 'create object', 'add component', 'set property', 'MCP tool'.
mcp:
  mcp4unity:
    type: local
    enabled: true
    command: "node"
    args: ["build/index.js"]
    cwd: "{SKILL_ROOT}/server"
---

# MCP4Unity - Unity Editor Integration

MCP4Unity 通过 TypeScript MCP Server 与 Unity Editor HTTP 服务通信，实现 AI 代理对 Unity 的实时控制。

## 快速开始

### 首次配置

```
configureunity unityExePath="C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe"
```

项目路径自动检测（从当前工作目录向上查找 Unity 项目根目录）。

### 启动 Unity

```
startunity  # 自动清理缓存并启动
```

等待 30-60 秒后检查状态：

```
getunitystatus  # 应返回 editor_mcp_ready
```

---

## 核心工作流

### 编译检查

```
getunitystatus →
  ├─ editor_mcp_ready → recompileassemblies
  ├─ editor_mcp_unresponsive → 等待或重启
  ├─ not_running → startunity
  └─ batchmode → 等待完成
```

**推荐**: 使用 `unity-compile-fix` skill 进行完整的编译修复循环。

### Unity 卡死处理

```
stopunity
deletescriptassemblies
startunity
```

---

## 工具分类

### Unity 管理工具（无需 Unity 运行）

| 工具 | 用途 |
|------|------|
| `configureunity` | 配置 Unity 路径（首次使用） |
| `startunity` | 启动 Unity（自动清理缓存） |
| `stopunity` | 强制关闭 Unity |
| `isunityrunning` | 简单布尔检查 |
| `getunitystatus` | 详细状态诊断 |
| `deletescenebackups` | 删除场景备份文件 |
| `deletescriptassemblies` | 删除脚本缓存 |

**详细参数和返回值**: 参考 `references/management-tools.md`

### 代码工具

| 工具 | 用途 |
|------|------|
| `recompileassemblies` | 触发编译并返回错误 |
| `getunityconsolelog` | 获取控制台日志 |
| `runcode` | 执行任意静态方法 |

**详细参数和返回值**: 参考 `references/code-tools.md`

### 场景工具

| 工具 | 用途 |
|------|------|
| `getactivescene` | 获取当前场景信息 |
| `gethierarchy` | 获取层级树 |
| `getgameobjectinfo` | 获取 GameObject 详细信息 |
| `savescene` / `savesceneas` | 保存场景 |
| `loadscene` | 加载场景 |
| `createscene` | 创建新场景 |
| `closescene` / `setactivescene` | 多场景管理 |

**详细参数和返回值**: 参考 `references/scene-tools.md`

### 资源工具

| 工具 | 用途 |
|------|------|
| `findassets` | 搜索资源（支持 Unity 搜索语法） |
| `getassetinfo` | 获取资源详细信息 |
| `importasset` | 强制重新导入资源 |

**详细参数和返回值**: 参考 `references/asset-tools.md`

### 组件工具

| 工具 | 用途 |
|------|------|
| `getcomponents` | 列出 GameObject 的所有组件 |
| `getserializedproperties` | 获取组件的序列化属性 |
| `setserializedproperty` | 修改组件属性值 |
| `createobject` | 创建 GameObject |
| `deleteobject` | 删除 GameObject |
| `addcomponent` | 添加组件 |
| `removecomponent` | 移除组件 |
| `setparent` | 设置父对象 |

**详细参数和返回值**: 参考 `references/component-tools.md`

---

## Unity 状态检测

`getunitystatus` 返回详细状态：

| 状态值 | 含义 |
|--------|------|
| `not_running` | Unity 进程未运行 |
| `batchmode` | Unity 运行在无头模式（无 MCP 服务） |
| `editor_mcp_ready` | Unity Editor 运行且 MCP 服务就绪 |
| `editor_mcp_unresponsive` | Unity Editor 运行但 MCP 未响应（编译中/加载中） |

**使用场景**:
- 调用 MCP 工具前检查是否就绪
- 检测编译/加载问题
- 区分 batchmode 和 editor 模式
- 验证端点文件是否过期（检查进程 PID）

---

## 常见工作流示例

### 查找并修改 GameObject

```
1. gethierarchy(topOnly=true)  # 查看根对象
2. getgameobjectinfo("Player")  # 获取详细信息
3. getcomponents("Player")  # 列出所有组件
4. getserializedproperties("Player", "Rigidbody")  # 查看属性
5. setserializedproperty("Player", "Rigidbody", "m_Mass", "2.5")  # 修改属性
6. savescene()  # 保存更改
```

### 搜索并导入资源

```
1. findassets("t:Texture2D", "Assets/Sprites")  # 搜索纹理
2. getassetinfo("Assets/Sprites/hero.png")  # 查看详情
3. importasset("Assets/Sprites/hero.png", "ForceUpdate")  # 强制重新导入
```

### 编译并修复错误

```
1. getunitystatus  # 确认 MCP 就绪
2. recompileassemblies  # 触发编译
3. 如果有错误 → 修复代码
4. 重复步骤 2-3 直到编译通过
```

---

## 扩展功能

### 创建自定义 MCP 工具

自定义工具放在 `Assets/Editor/MCPTools/`，使用 `[Tool]` 特性标记。

**最小模板**:

```csharp
using MCP4Unity;
using UnityEditor;

namespace MCP
{
    public class MyTool
    {
        [Tool("工具描述")]
        public static string MyToolName(
            [Desc("参数描述")] string param)
        {
            // 使用任何 UnityEditor API
            return "结果";
        }
    }
}
```

**完整指南**: 参考 `references/authoring-guide.md`

---

## 故障排除

### MCP 工具无响应

1. `getunitystatus` - 检查 Unity 状态
2. `getunityconsolelog` - 检查 Unity 错误
3. 检查 `Library/MCP4Unity/mcp_endpoint.json` 是否存在
4. 重启 Unity: `stopunity` → `startunity`

### Unity 卡死

```
stopunity
deletescriptassemblies
startunity
```

### 编译错误持续存在

```
stopunity
deletescriptassemblies
startunity
```

**完整故障排除指南**: 参考 `references/troubleshooting.md`

---

## 技术架构

```
AI Agent (stdio) ─► Node.js MCP Server (TypeScript) ─► HTTP POST ─► Unity Editor (MCPService)
```

- **TypeScript Server**: `{SKILL_ROOT}/server/` - 处理 MCP stdio ↔ HTTP 转换
- **Unity MCPService**: `Assets/MCP4Unity/` - HTTP 监听器，主线程执行工具
- **端点发现**: `Library/MCP4Unity/mcp_endpoint.json` - 存储端口和 PID

**详细架构说明**: 参考 `references/architecture.md`

---

## 参考文档

- **`references/management-tools.md`** - Unity 启停和配置工具详细文档
- **`references/code-tools.md`** - 编译、控制台、runcode 工具详细文档
- **`references/scene-tools.md`** - 场景和层级管理工具详细文档
- **`references/asset-tools.md`** - 资源查找和导入工具详细文档
- **`references/component-tools.md`** - 组件和属性操作工具详细文档
- **`references/authoring-guide.md`** - 创建自定义工具的完整指南
- **`references/troubleshooting.md`** - 故障排除详细步骤
- **`references/architecture.md`** - 技术架构和内部实现细节

---

## 开发与修改

### 重新构建 TypeScript Server

```bash
cd {SKILL_ROOT}/server
npm run build
```

更改立即生效，无需重启 AI 代理。

### 项目结构

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
├── unity_config.json          # Unity 路径配置（自动创建）
├── mcp.json                   # MCP server 配置
└── SKILL.md                   # 本文件
```
