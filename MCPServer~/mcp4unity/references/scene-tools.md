# Scene Tools Reference

Scene management, hierarchy inspection, and GameObject manipulation.

## getactivescene

Get current active scene info.

**Parameters**: None

**Returns**:
```
活跃场景: MainScene
路径: Assets/Scenes/MainScene.unity
是否dirty: True
根物体数量: 12

已加载场景 (2):
  [0] MainScene (Assets/Scenes/MainScene.unity) ← 活跃
  [1] UI (Assets/Scenes/UI.unity)
```

---

## gethierarchy

Get GameObject hierarchy tree.

**Parameters**:
- `topOnly` (optional, bool, default: false): Only root GameObjects
- `maxDepth` (optional, int, default: 0): Limit depth (0 = unlimited)

**Returns**:
```
Canvas
  Panel
    Button
    Text
Camera
Directional Light
```

**Notes**:
- Use `topOnly=true` for quick overview
- Indentation: 2 spaces per level

---

## getgameobjectinfo

Get detailed GameObject info.

**Parameters**:
- `nameOrPath` (string): Name (`"Player"`) or path (`"Canvas/Panel/Button"`)

**Returns**:
```
名称: Player
激活: True
Tag: Player
Layer: 0 (Default)

Transform:
  位置: (0.0, 1.0, 0.0)
  旋转: (0.0, 0.0, 0.0)
  缩放: (1.0, 1.0, 1.0)

子物体 (2):
  - Weapon
  - Shield

组件 (4):
  - Transform
  - Rigidbody
  - CapsuleCollider
  - PlayerController
```

---

## savescene

Save current scene.

**Parameters**: None

**Returns**: Success status

**Notes**:
- Saves to current scene path
- Marks scene as not dirty
- Use `savesceneas` for new path

---

## savesceneas

Save scene to new path.

**Parameters**:
- `path` (string): Asset path (`"Assets/Scenes/NewScene.unity"`)

**Returns**: Success status

**Example**:
```javascript
savesceneas("Assets/Scenes/Level2.unity")
```

---

## loadscene

Load a scene.

**Parameters**:
- `scenePathOrName` (string): Scene path or name
- `mode` (optional, string, default: "Single"): Load mode
  - `"Single"`: Close other scenes
  - `"Additive"`: Keep other scenes

**Examples**:
```javascript
loadscene("Assets/Scenes/MainMenu.unity")
loadscene("MainMenu")
loadscene("UI", "Additive")
```

**Returns**: Success status

---

## createscene

Create new scene.

**Parameters**:
- `mode` (optional, string, default: "Single"): Load mode
  - `"Single"`: Replace current scene
  - `"Additive"`: Add to current scenes

**Returns**: Success status

**Notes**:
- Creates empty scene
- Use `savesceneas` to save

---

## closescene

Close a loaded scene.

**Parameters**:
- `scenePathOrName` (string): Scene path or name

**Example**:
```javascript
closescene("UI")
```

**Returns**: Success status

**Notes**:
- Only for multi-scene workflows
- Cannot close last scene

---

## setactivescene

Set active scene.

**Parameters**:
- `scenePathOrName` (string): Scene path or name

**Example**:
```javascript
setactivescene("MainScene")
```

**Returns**: Success status

**Notes**:
- Only for multi-scene workflows
- Active scene receives new GameObjects

---

## Common Workflows

### Inspect Scene Structure
```
1. getactivescene()
2. gethierarchy(topOnly=true)
3. getgameobjectinfo("Player")
```

### Save Scene
```
1. savescene()
# or
2. savesceneas("Assets/Scenes/NewScene.unity")
```

### Multi-Scene Workflow
```
1. loadscene("MainScene")
2. loadscene("UI", "Additive")
3. setactivescene("MainScene")
4. closescene("UI")
```

### Create New Scene
```
1. createscene("Single")
2. createobject("Player")
3. savesceneas("Assets/Scenes/Level1.unity")
```

---

## Troubleshooting

**GameObject not found**:
- Use `gethierarchy()` to find exact name
- Use full path for nested objects
- Check object is in active scene

**Scene not saving**:
- Check scene is dirty: `getactivescene()`
- Verify path valid: `"Assets/Scenes/*.unity"`
- Exit play mode first

**Multi-scene issues**:
- Check scene loaded: `getactivescene()`
- Verify active scene set correctly
- Cannot close last scene
