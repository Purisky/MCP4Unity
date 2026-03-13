# Scene Tools Reference

Scene tools handle Unity scene management, hierarchy inspection, and GameObject manipulation.

## Available Tools

### getactivescene

**Purpose**: Get information about the currently active scene.

**Parameters**: None

**Returns**: Formatted string containing:
- Scene name
- Scene path (asset path)
- Dirty state (unsaved changes)
- Root GameObject count
- List of all loaded scenes (for multi-scene workflows)

**Example Response**:
```
活跃场景: MainScene
路径: Assets/Scenes/MainScene.unity
是否dirty: True
根物体数量: 12
是否已加载: True

已加载场景 (2):
  [0] MainScene (Assets/Scenes/MainScene.unity) ← 活跃
  [1] UI (Assets/Scenes/UI.unity)
```

---

### gethierarchy

**Purpose**: Get the full hierarchy tree of GameObjects in the active scene.

**Parameters**:
- `topOnly` (optional, bool, default: false): Return only root-level GameObjects
- `maxDepth` (optional, int, default: 0): Limit tree depth (0 = unlimited)

**Returns**: Hierarchical list of GameObjects with indentation showing parent-child relationships.

**Example Response** (topOnly=false, maxDepth=0):
```
Canvas
  Panel
    Button
    Text
  Image
Camera
Directional Light
EventSystem
```

**Example Response** (topOnly=true):
```
Canvas
Camera
Directional Light
EventSystem
```

**Usage Notes**:
- Use `topOnly=true` for quick overview of scene structure
- Use `maxDepth` to limit output for deeply nested hierarchies
- Indentation uses spaces (2 spaces per level)
- GameObject names are unique within their parent scope

---

### getgameobjectinfo

**Purpose**: Get detailed information about a specific GameObject.

**Parameters**:
- `nameOrPath` (required, string): GameObject name or hierarchy path
  - Simple name: `"Player"`
  - Hierarchy path: `"Canvas/Panel/Button"`

**Returns**: Formatted string containing:
- GameObject name and path
- Transform data (position, rotation, scale)
- Tag and layer
- Active state (enabled/disabled)
- List of child GameObjects
- List of all component type names

**Example Response**:
```
名称: Player
激活(self): True
激活(hierarchy): True
Tag: Player
Layer: 0 (Default)
静态: False

Transform:
  路径: Player
  Local位置: (0.00, 1.00, 0.00)
  Local旋转: (0.00, 0.00, 0.00)
  Local缩放: (1.00, 1.00, 1.00)
  World位置: (0.00, 1.00, 0.00)

子物体 (2):
  - PlayerModel
  - Weapon

组件 (4):
  - Transform
  - Rigidbody
  - CapsuleCollider
  - PlayerController
```

**Usage Notes**:
- Use hierarchy paths for nested objects: `"Canvas/Panel/Button"`
- If multiple objects have the same name, returns the first match
- For precise targeting, use full hierarchy path

---

### savescene

**Purpose**: Save the current active scene to its existing path.

**Parameters**: None

**Returns**: Success/failure message string.

**Example Response**:
```
✅ 场景已保存: Assets/Scenes/MainScene.unity
```

**Usage Notes**:
- Only saves the active scene (in multi-scene workflows)
- Fails if scene has never been saved (use `savesceneas` instead)
- Automatically marks scene as clean (isDirty = false)

---

### savesceneas

**Purpose**: Save the current active scene to a specified path.

**Parameters**:
- `path` (required, string): Target save path
  - Must be in Assets folder
  - Must have `.unity` extension
  - Example: `"Assets/Scenes/NewScene.unity"`

**Returns**: Success/failure message string.

**Example Response**:
```
✅ 场景已另存为: Assets/Scenes/NewScene.unity
```

**Usage Notes**:
- Creates parent directories if they don't exist
- Overwrites existing file without warning
- Use for "Save As" functionality or saving new scenes

---

### loadscene

**Purpose**: Load a scene by path or name.

**Parameters**:
- `scenePathOrName` (required, string): Scene path or name
  - Full path: `"Assets/Scenes/Level1.unity"`
  - Scene name: `"Level1"`
- `mode` (optional, string, default: "Single"): Load mode
  - `"Single"`: Replace all currently loaded scenes
  - `"Additive"`: Add to currently loaded scenes

**Returns**: Success/failure message string.

**Example Response**:
```
✅ 场景已加载: Level1 (Assets/Scenes/Level1.unity) [Single模式]
```

**Usage Notes**:
- Scene must be in Build Settings or Addressables
- `Single` mode closes all other scenes
- `Additive` mode keeps existing scenes loaded
- Newly loaded scene becomes active scene (unless using Additive)

---

### createscene

**Purpose**: Create a new empty or default scene.

**Parameters**:
- `setup` (optional, string, default: "DefaultGameObjects"): Scene setup
  - `"Empty"`: Completely empty scene
  - `"DefaultGameObjects"`: Includes Camera and Directional Light
- `mode` (optional, string, default: "Single"): Load mode
  - `"Single"`: Replace current scene
  - `"Additive"`: Add to loaded scenes

**Returns**: Success/failure message string.

**Example Response**:
```
✅ 新场景已创建 [DefaultGameObjects] [Single模式]
```

**Usage Notes**:
- New scene is unsaved (isDirty = true)
- Use `savesceneas` to save it to disk
- `DefaultGameObjects` includes Main Camera and Directional Light

---

### closescene

**Purpose**: Close a loaded scene (multi-scene workflows).

**Parameters**:
- `sceneName` (required, string): Name of scene to close
- `removeScene` (optional, bool, default: true): Remove from scene list or just unload

**Returns**: Success/failure message string.

**Example Response**:
```
✅ 场景已关闭: UI (已移除)
```

**Usage Notes**:
- Cannot close the last loaded scene
- If scene has unsaved changes, prompts user (in Editor)
- Use `removeScene=false` to unload but keep in scene list

---

### setactivescene

**Purpose**: Set which loaded scene is the active scene (multi-scene workflows).

**Parameters**:
- `sceneName` (required, string): Name of scene to make active

**Returns**: Success/failure message string.

**Example Response**:
```
✅ 活跃场景已设置为: MainScene
```

**Usage Notes**:
- Scene must already be loaded
- Active scene is where new GameObjects are created by default
- Only one scene can be active at a time

---

## Common Workflows

### Inspect Scene Structure

```
1. getactivescene - Check current scene
2. gethierarchy(topOnly=true) - Get root objects
3. getgameobjectinfo("ObjectName") - Inspect specific object
```

### Save Scene Changes

```
1. getactivescene - Check if scene is dirty
2. savescene - Save to existing path
   OR
   savesceneas("Assets/Scenes/NewName.unity") - Save to new path
```

### Multi-Scene Workflow

```
1. loadscene("MainScene", mode="Single") - Load base scene
2. loadscene("UI", mode="Additive") - Add UI scene
3. setactivescene("MainScene") - Set active scene
4. closescene("UI") - Close UI scene when done
```

### Create New Scene

```
1. createscene(setup="DefaultGameObjects", mode="Single")
2. [Add GameObjects and components]
3. savesceneas("Assets/Scenes/NewLevel.unity")
```

---

## Error Handling

**Scene not found**:
- `loadscene` fails if scene not in Build Settings
- Check scene is added to Build Settings or Addressables

**Cannot close last scene**:
- `closescene` fails if trying to close the only loaded scene
- Load or create another scene first

**Invalid hierarchy path**:
- `getgameobjectinfo` returns error if path not found
- Use `gethierarchy` to verify correct path
- Check for typos in GameObject names
