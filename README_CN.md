# MCP4Unity

- [中文](./README_CN.md)/[English](./README.md)

## 项目概述

MCP4Unity是一个Unity编辑器扩展。它允许您将Unity方法作为工具暴露出来,可以通过HTTP请求远程调用,支持动态加载和调用工具方法。

## 主要功能

- 内置HTTP服务器,监听8080端口
- 自动扫描和注册带有`[Tool]`特性的方法
- 提供工具管理界面(Window/MCP Service Manager)
- 支持自动启动

## 需求

- Unity 2021.3 or later
  - Newtonsoft.Json

## 快速开始

1.复制`MCP4Unity`文件夹到Unity项目的`Assets`目录下

2.json配置

```json
{
  "mcpServers": {
    "mcp4unity": {
      "command": "[MCPConsole~文件夹路径]/MCPConsole[.exe]",
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
public static string Echo_Tool([Desc("stringArg描述")] string stringArg, [Desc("intArg描述")] int intArg)
{
    return $"echo:{stringArg},{intArg}";
}

[Tool("Retrieves the names of all GameObjects in the hierarchy")]
public static string[] Get_All_GameObject_in_Hierarchy([Desc("If true, only top-level GameObjects are returned; otherwise, all GameObjects are returned.")] bool top)
{
    List<string> gameObjectNames = new();

    if (top)
    {
        foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            gameObjectNames.Add(go.name);
        }
    }
    else
    {
        foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
        {
            gameObjectNames.Add(go.name);
        }
    }
    return gameObjectNames.ToArray();
}

```

## 技术细节

- 基于Model Context Protocol SDK
- 自动发现并加载所有引用MCP4Unity的程序集中的工具
