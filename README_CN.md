# MCP4Unity

- [中文](./README_CN.md)/[English](./README.md)

## 项目概述

MCP4Unity是一个Unity编辑器扩展。它允许您将Unity方法作为工具暴露出来，可以通过HTTP请求远程调用，支持动态加载和调用工具方法。

## 主要功能

- 内置HTTP服务器，监听8080端口
- 自动扫描和注册带有`[Tool]`特性的方法
- 提供工具管理界面(Window/MCP Service Manager)
- 支持自动启动

## 需求

- Unity 2021.3 or later
- Node.js 18 or later
- npm 9 or later

## 快速开始

1.复制`MCP4Unity`文件夹到Unity项目的`Assets`目录下

2.转到Assets目录

```bash
cd MCP4Unity/mcp.ts
npm install
npm run build
```

3.json配置

```json
{
  "mcpServers": {
    "mcp4unity": {
      "command": "node",
      "args": ["[mcp.ts文件夹路径]/build/index.js"],
      "env": {},
      "disabled": false,
      "autoApprove": []
    },
  }
}

```

## 开发工具

创建自己的工具方法：

1. 在任意类中创建静态方法
2. 使用`[Tool]`特性标记方法
3. 使用`[Tool]`特性为参数添加描述

示例：

```csharp
[Tool("Echo描述")]
public static string Echo_Tool([Tool("stringArg描述")] string stringArg, [Tool("intArg描述")] int intArg)
{
    return $"echo:{stringArg},{intArg}";
}
```

## 技术细节

- 基于Model Context Protocol SDK
- 使用C#和TypeScript开发
- 自动发现并加载所有引用MCP4Unity的程序集中的工具
