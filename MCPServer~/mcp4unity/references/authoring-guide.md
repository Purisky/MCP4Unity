# Authoring Custom MCP Tools

This guide covers how to create custom MCP tools for Unity Editor automation.

## Overview

Custom tools are C# static methods decorated with `[Tool]` attribute. They have full access to Unity Editor APIs.

**Key Points**:
- Tools are discovered automatically via reflection
- **Built-in tools**: `Assets/MCP4Unity/Editor/Tools/` (framework defaults)
- **Custom tools**: `Assets/Editor/MCPTools/` (user extensions)
- Tools execute on Unity's main thread
- Return values are JSON-serialized

**Tool Discovery**:
MCP4Unity scans all assemblies that reference the MCP4Unity assembly, so tools in both locations are automatically discovered.

---

## Minimal Template

```csharp
using MCP4Unity;
using UnityEngine;
using UnityEditor;

namespace MCP
{
    public class MyModuleTool
    {
        [Tool("Brief description of what this tool does")]
        public static string MyToolName(
            [Desc("What this parameter is for")] string param1,
            [Desc("Optional parameter")] int param2 = 0)
        {
            // Use any UnityEditor API here
            return "result string";
        }
    }
}
```

**Tool naming**: MCP tool name = method name lowercased. `GetHierarchy` becomes `gethierarchy`.

---

## Method Signature Rules

### 1. Access Modifiers

```csharp
// CORRECT
public static string MyTool() { ... }

// WRONG - must be public and static
private static string MyTool() { ... }
static string MyTool() { ... }
public string MyTool() { ... }
```

### 2. Return Types

Supported return types:
- `string` - Simple text response
- `string[]` - Array of strings
- `Task<string>` - Async operations
- Any JSON-serializable type (classes, structs, arrays)

```csharp
// String return
[Tool("Get scene name")]
public static string GetSceneName()
{
    return SceneManager.GetActiveScene().name;
}

// JSON object return
[Tool("Get scene info")]
public static object GetSceneInfo()
{
    return new {
        name = SceneManager.GetActiveScene().name,
        path = SceneManager.GetActiveScene().path,
        isDirty = SceneManager.GetActiveScene().isDirty
    };
}

// Array return
[Tool("List all scenes")]
public static string[] ListScenes()
{
    return EditorBuildSettings.scenes.Select(s => s.path).ToArray();
}
```

### 3. Parameters

All parameters must be JSON-deserializable:
- Primitives: `int`, `float`, `bool`, `string`
- Arrays: `string[]`, `int[]`
- Optional parameters: use default values

```csharp
[Tool("Create GameObject")]
public static string CreateObject(
    [Desc("Object name")] string name,
    [Desc("Parent path (optional)")] string parent = "")
{
    // ...
}
```

---

## Attributes

### [Tool] - Required

Marks a method as an MCP tool. Description appears in tool listings.

```csharp
[Tool("Recompile Unity scripts and return errors")]
public static string RecompileAssemblies() { ... }
```

### [Desc] - Recommended

Describes parameter purpose. Helps AI understand what to pass.

```csharp
public static string LoadScene(
    [Desc("Scene path or name")] string scenePathOrName,
    [Desc("Load mode: Single or Additive")] string mode = "Single")
{
    // ...
}
```

### [ParamDropdown] - Optional

Provides enum-like options for a parameter. References a static method returning `string[]`.

```csharp
public static string[] GetLoadModes()
{
    return new[] { "Single", "Additive" };
}

[Tool("Load a scene")]
public static string LoadScene(
    [Desc("Scene path")] string scenePath,
    [ParamDropdown("GetLoadModes")][Desc("Load mode")] string mode = "Single")
{
    // ...
}
```

---

## Async Operations

For long-running operations (compilation, asset import), use `async`/`await` and return `Task<string>`.

### Example: Async Compilation

```csharp
using System.Threading.Tasks;
using UnityEditor.Compilation;

[Tool("Recompile and wait for result")]
public static async Task<string> RecompileAssemblies()
{
    var tcs = new TaskCompletionSource<bool>();
    
    CompilationPipeline.compilationFinished += OnCompilationFinished;
    CompilationPipeline.RequestScriptCompilation();
    
    await tcs.Task;
    
    CompilationPipeline.compilationFinished -= OnCompilationFinished;
    
    // Collect errors
    var errors = GetCompilationErrors();
    return Newtonsoft.Json.JsonConvert.SerializeObject(errors);
    
    void OnCompilationFinished(object obj)
    {
        tcs.TrySetResult(true);
    }
}
```

**Important**: Async tools have a 25-second timeout. If the operation takes longer, it will fail with `TimeoutException`.

---

## Error Handling

Return descriptive error strings rather than throwing exceptions.

```csharp
[Tool("Load a scene")]
public static string LoadScene(string scenePath)
{
    if (string.IsNullOrEmpty(scenePath))
    {
        return "Error: scenePath cannot be empty";
    }
    
    if (!File.Exists(scenePath))
    {
        return $"Error: Scene not found: {scenePath}";
    }
    
    try
    {
        EditorSceneManager.OpenScene(scenePath);
        return $"Success: Loaded {scenePath}";
    }
    catch (Exception ex)
    {
        return $"Error: {ex.Message}";
    }
}
```

**Why not throw?** The MCP bridge serializes return values. Exceptions are caught and logged, but returning error strings gives better control over error messages.

---

## File Organization

Group related tools in one file named `{Module}Tool.cs`.

**Current modules**:
- `CodeTool.cs` - Compilation, console logs, code execution
- `SceneTool.cs` - Scene management, hierarchy inspection
- `AssetTool.cs` - Asset search, import, info
- `ComponentTool.cs` - Component inspection, property manipulation
- `HierarchyTool.cs` - GameObject creation, deletion, parenting

**Example structure**:

```
Assets/Editor/MCPTools/
├── CodeTool.cs
├── SceneTool.cs
├── AssetTool.cs
├── ComponentTool.cs
└── HierarchyTool.cs
```

---

## Full Example: Custom Tool

```csharp
using MCP4Unity;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace MCP
{
    public class PrefabTool
    {
        [Tool("Find all prefabs in project")]
        public static string[] FindAllPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            return guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }
        
        [Tool("Get prefab component summary")]
        public static object GetPrefabInfo(
            [Desc("Prefab asset path")] string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
                return new { error = "prefabPath cannot be empty" };
            
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return new { error = $"Prefab not found: {prefabPath}" };
            
            var components = prefab.GetComponents<Component>()
                .Select(c => c.GetType().Name)
                .ToArray();
            
            return new {
                path = prefabPath,
                name = prefab.name,
                components = components,
                childCount = prefab.transform.childCount
            };
        }
        
        [Tool("Instantiate prefab in scene")]
        public static string InstantiatePrefab(
            [Desc("Prefab asset path")] string prefabPath,
            [Desc("Position (x,y,z)")] string position = "0,0,0")
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return $"Error: Prefab not found: {prefabPath}";
            
            var pos = ParseVector3(position);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.transform.position = pos;
            
            return $"Success: Instantiated {prefab.name} at {pos}";
        }
        
        private static Vector3 ParseVector3(string str)
        {
            var parts = str.Split(',');
            return new Vector3(
                float.Parse(parts[0]),
                float.Parse(parts[1]),
                float.Parse(parts[2])
            );
        }
    }
}
```

---

## Testing Your Tools

### 1. Rebuild TypeScript Server

After adding new tools, rebuild the MCP server:

```bash
cd {SKILL_ROOT}/server
npm run build
```

### 2. Restart Unity

New tools are discovered on Unity startup. Restart Unity to register them.

### 3. Test via MCP

Use the tool from your AI agent:

```javascript
// List tools (verify your tool appears)
// Then call it
findallprefabs()
getprefabinfo("Assets/Prefabs/Player.prefab")
```

---

## Best Practices

### 1. Keep Tools Focused

Each tool should do one thing well. Don't create mega-tools that do multiple unrelated operations.

```csharp
// GOOD - focused
[Tool("Get active scene name")]
public static string GetSceneName() { ... }

[Tool("Get active scene path")]
public static string GetScenePath() { ... }

// BAD - too broad
[Tool("Get scene info and also load another scene and save")]
public static string DoEverything(...) { ... }
```

### 2. Use Descriptive Names

Tool names should clearly indicate what they do.

```csharp
// GOOD
[Tool("Find all textures in Assets/UI folder")]
public static string[] FindUITextures() { ... }

// BAD - vague
[Tool("Find stuff")]
public static string[] Find() { ... }
```

### 3. Validate Inputs

Always validate parameters before using them.

```csharp
[Tool("Delete GameObject")]
public static string DeleteObject(string name)
{
    if (string.IsNullOrEmpty(name))
        return "Error: name cannot be empty";
    
    var obj = GameObject.Find(name);
    if (obj == null)
        return $"Error: GameObject not found: {name}";
    
    Object.DestroyImmediate(obj);
    return $"Success: Deleted {name}";
}
```

### 4. Return Structured Data

For complex results, return JSON objects instead of formatted strings.

```csharp
// GOOD - structured
[Tool("Get GameObject info")]
public static object GetObjectInfo(string name)
{
    var obj = GameObject.Find(name);
    return new {
        name = obj.name,
        position = obj.transform.position,
        components = obj.GetComponents<Component>().Select(c => c.GetType().Name)
    };
}

// BAD - string formatting
[Tool("Get GameObject info")]
public static string GetObjectInfo(string name)
{
    var obj = GameObject.Find(name);
    return $"Name: {obj.name}\nPosition: {obj.transform.position}\n...";
}
```

---

## Common Patterns

### Pattern: Find and Return Paths

```csharp
[Tool("Find all scripts in project")]
public static string[] FindAllScripts()
{
    var guids = AssetDatabase.FindAssets("t:Script");
    return guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();
}
```

### Pattern: Get Detailed Info

```csharp
[Tool("Get asset detailed info")]
public static object GetAssetInfo(string assetPath)
{
    var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
    return new {
        path = assetPath,
        type = asset.GetType().Name,
        guid = AssetDatabase.AssetPathToGUID(assetPath),
        size = new FileInfo(assetPath).Length
    };
}
```

### Pattern: Modify and Confirm

```csharp
[Tool("Set GameObject active state")]
public static string SetActive(string name, bool active)
{
    var obj = GameObject.Find(name);
    if (obj == null)
        return $"Error: GameObject not found: {name}";
    
    obj.SetActive(active);
    return $"Success: Set {name} active={active}";
}
```

---

## Troubleshooting

### Tool Not Appearing

1. Check method is `public static`
2. Check `[Tool]` attribute is present
3. Rebuild TypeScript server: `npm run build`
4. Restart Unity

### Tool Times Out

1. Check if operation is blocking main thread
2. Use `async`/`await` for long operations
3. Verify 25s timeout is sufficient
4. Check Unity Console for errors

### Tool Returns Wrong Data

1. Add debug logging: `Debug.Log(...)`
2. Check Unity Console for errors
3. Verify return type is JSON-serializable
4. Test tool manually in Unity (create test script)

---

## Advanced: Custom Serialization

For complex types, implement custom JSON serialization:

```csharp
using Newtonsoft.Json;

[Tool("Get complex data")]
public static string GetComplexData()
{
    var data = new MyComplexType { ... };
    return JsonConvert.SerializeObject(data);
}
```

**Note**: MCP4Unity uses Newtonsoft.Json for all JSON serialization. Return values are automatically serialized using `JsonConvert.SerializeObject()`.
