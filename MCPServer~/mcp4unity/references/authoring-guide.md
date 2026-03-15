# Authoring Custom MCP Tools

Create custom Unity Editor automation tools using C# static methods with `[Tool]` attribute.

## Quick Start

**Locations**:
- Built-in: `Assets/MCP4Unity/Editor/Tools/`
- Custom: `Assets/Editor/MCPTools/`

**Template**:
```csharp
using MCP4Unity;
using UnityEditor;

namespace MCP
{
    public class MyTools
    {
        [Tool("Description")]
        public static string MyTool([Desc("param info")] string param)
        {
            // Use any UnityEditor API
            return "result";
        }
    }
}
```

**Tool naming**: Method name lowercased (`GetHierarchy` → `gethierarchy`)

---

## Method Signature

**Requirements**:
- Must be `public static`
- Return: `string`, `string[]`, `Task<string>`, or JSON-serializable type
- Parameters: primitives, arrays, optional (with defaults)

**Example**:
```csharp
[Tool("Create GameObject")]
public static string CreateObject(
    [Desc("Object name")] string name,
    [Desc("Parent path")] string parent = "")
{
    var obj = new GameObject(name);
    if (!string.IsNullOrEmpty(parent))
    {
        var parentObj = GameObject.Find(parent);
        if (parentObj) obj.transform.SetParent(parentObj.transform);
    }
    return obj.name;
}
```

---

## Attributes

### [Tool] - Required
Marks method as MCP tool. Description shown in listings.

### [Desc] - Recommended
Describes parameter purpose for AI.

### [ParamDropdown] - Optional
Provides enum-like options:
```csharp
public static string[] GetModes() => new[] { "Single", "Additive" };

[Tool("Load scene")]
public static string LoadScene(
    string path,
    [ParamDropdown("GetModes")] string mode = "Single") { ... }
```

---

## Async Operations

For long-running tasks (>1s), use `async`/`await`:

```csharp
using System.Threading.Tasks;
using UnityEditor.Compilation;

[Tool("Recompile and wait")]
public static async Task<string> Recompile()
{
    var tcs = new TaskCompletionSource<bool>();
    CompilationPipeline.compilationFinished += _ => tcs.SetResult(true);
    CompilationPipeline.RequestScriptCompilation();
    await tcs.Task;
    return "Done";
}
```

**Timeout**: 25 seconds

---

## Error Handling

**Return errors as strings**:
```csharp
[Tool("Load scene")]
public static string LoadScene(string path)
{
    if (!File.Exists(path))
        return $"Error: Scene not found: {path}";
    
    EditorSceneManager.OpenScene(path);
    return $"Loaded: {path}";
}
```

**For exceptions**:
```csharp
try
{
    // risky operation
}
catch (Exception ex)
{
    return $"Error: {ex.Message}";
}
```

---

## Common Patterns

### Scene Manipulation
```csharp
[Tool("Get active scene")]
public static string GetActiveScene()
{
    var scene = SceneManager.GetActiveScene();
    return JsonConvert.SerializeObject(new {
        name = scene.name,
        path = scene.path,
        isDirty = scene.isDirty
    });
}
```

### Hierarchy Operations
```csharp
[Tool("Find GameObject")]
public static string FindObject([Desc("Object name or path")] string name)
{
    var obj = GameObject.Find(name);
    if (!obj) return $"Error: Not found: {name}";
    
    return JsonConvert.SerializeObject(new {
        name = obj.name,
        path = GetPath(obj),
        active = obj.activeSelf
    });
}

static string GetPath(GameObject obj)
{
    var path = obj.name;
    while (obj.transform.parent)
    {
        obj = obj.transform.parent.gameObject;
        path = obj.name + "/" + path;
    }
    return path;
}
```

### Component Access
```csharp
[Tool("Get component")]
public static string GetComponent(
    [Desc("GameObject path")] string path,
    [Desc("Component type")] string typeName)
{
    var obj = GameObject.Find(path);
    if (!obj) return $"Error: GameObject not found: {path}";
    
    var type = Type.GetType(typeName);
    if (type == null) return $"Error: Type not found: {typeName}";
    
    var comp = obj.GetComponent(type);
    if (!comp) return $"Error: Component not found: {typeName}";
    
    return JsonConvert.SerializeObject(comp);
}
```

### Asset Operations
```csharp
[Tool("Find assets")]
public static string[] FindAssets(
    [Desc("Search filter")] string filter,
    [Desc("Folder path")] string folder = "Assets")
{
    var guids = AssetDatabase.FindAssets(filter, new[] { folder });
    return guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();
}
```

---

## Execution Context

**Main Thread**: All tools execute on Unity's main thread via `EditorMainThread` queue.

**Domain Reload**: Tools survive script recompilation. Service auto-restarts.

**Background Stability**: Tools work reliably even when Unity is unfocused (Win32 message pump keeps editor responsive).

---

## Testing

### Manual Test
Create test script in `Assets/Editor/`:
```csharp
using UnityEditor;

public class TestMyTool
{
    [MenuItem("Tools/Test My Tool")]
    static void Test()
    {
        var result = MyTools.MyTool("test");
        Debug.Log(result);
    }
}
```

### Via MCP
1. Restart Unity (tools auto-discovered)
2. Use `listtools` to verify tool appears
3. Call tool via MCP client

---

## Troubleshooting

**Tool not appearing**:
- Check `public static` modifiers
- Verify `[Tool]` attribute present
- Restart Unity

**Tool times out**:
- Use `async`/`await` for long operations
- Check 25s timeout sufficient
- Verify not blocking main thread

**Wrong data returned**:
- Add `Debug.Log()` statements
- Check Unity Console for errors
- Verify JSON-serializable return type

---

## Best Practices

1. **Keep tools focused**: One tool = one operation
2. **Validate inputs**: Check nulls, paths, types
3. **Return structured data**: Use JSON objects, not plain strings
4. **Handle errors gracefully**: Return error messages, don't throw
5. **Document parameters**: Use `[Desc]` for all parameters
6. **Test thoroughly**: Manual + MCP testing before deployment

---

## Advanced: Custom Serialization

For complex types:
```csharp
using Newtonsoft.Json;

[Tool("Get complex data")]
public static string GetData()
{
    var data = new MyComplexType { ... };
    return JsonConvert.SerializeObject(data, Formatting.Indented);
}
```

MCP4Unity uses Newtonsoft.Json for all serialization.
