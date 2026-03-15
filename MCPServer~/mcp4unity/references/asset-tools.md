# Asset Tools Reference

Asset search, inspection, and import management.

## findassets

Search for assets using Unity search syntax.

**Parameters**:
- `filter` (string): Search filter (Unity syntax)
- `folder` (optional, string): Limit to folder path (default: entire project)

**Filter Syntax**:
- By type: `"t:Texture2D"`, `"t:Prefab"`, `"t:AudioClip"`
- By label: `"l:UI"`, `"l:Environment"`
- By name: `"hero"`, `"PlayerController"`
- Combined: `"t:Prefab l:Enemy"`

**Examples**:
```javascript
findassets("t:Texture2D")  // All textures
findassets("t:Prefab", "Assets/Prefabs/Characters")  // Prefabs in folder
findassets("hero")  // Assets named "hero"
findassets("t:ScriptableObject l:Config")  // Combined filters
```

**Returns**:
```json
{
  "assets": [
    "Assets/Sprites/hero.png",
    "Assets/Sprites/enemy_01.png"
  ],
  "count": 2
}
```

**Common Types**:
- `Texture2D`, `Sprite`, `Material`, `Shader`
- `Prefab`, `GameObject`, `ScriptableObject`
- `AudioClip`, `AnimationClip`, `Font`
- `Scene`, `Script`, `TextAsset`

---

## getassetinfo

Get detailed asset information.

**Parameters**:
- `assetPath` (string): Asset path (`"Assets/Sprites/hero.png"`)

**Returns (Texture)**:
```json
{
  "path": "Assets/Sprites/hero.png",
  "type": "Texture2D",
  "guid": "a1b2c3d4...",
  "fileSize": 524288,
  "width": 512,
  "height": 512,
  "format": "RGBA32",
  "dependencies": ["Assets/Materials/SpriteMaterial.mat"]
}
```

**Returns (Prefab)**:
```json
{
  "path": "Assets/Prefabs/Player.prefab",
  "type": "GameObject",
  "guid": "x1y2z3a4...",
  "fileSize": 8192,
  "components": ["Transform", "Rigidbody", "PlayerController"],
  "dependencies": ["Assets/Scripts/PlayerController.cs"]
}
```

**Notes**:
- GUID stable across moves/renames
- Dependencies are direct references only
- File size is on-disk size

---

## importasset

Force reimport an asset.

**Parameters**:
- `assetPath` (string): Asset path
- `options` (optional, string, default: "Default"): Import options
  - `"Default"`: Standard reimport
  - `"ForceUpdate"`: Force full reimport
  - `"ForceSynchronousImport"`: Block until complete
  - `"ImportRecursive"`: Reimport folder and contents

**Examples**:
```javascript
importasset("Assets/Sprites/hero.png")
importasset("Assets/Sprites/hero.png", "ForceUpdate")
importasset("Assets/Sprites", "ImportRecursive")
```

**Returns**:
```json
{
  "success": true,
  "path": "Assets/Sprites/hero.png",
  "duration": "0.234s"
}
```

**Use Cases**:
- Asset modified externally
- Import settings changed
- Fix corrupted asset
- Batch reimport folder

---

## Common Workflows

### Find and Inspect
```
1. findassets("t:Prefab l:Enemy")
2. getassetinfo("Assets/Prefabs/Enemy01.prefab")
```

### Search in Folder
```
1. findassets("t:Texture2D", "Assets/UI")
2. getassetinfo("<path>")
3. Check width/height
```

### Reimport Modified Asset
```
1. importasset("Assets/Sprites/hero.png", "ForceUpdate")
```

### Check Dependencies
```
1. getassetinfo("Assets/Prefabs/Player.prefab")
2. Review dependencies array
```

---

## Troubleshooting

**Asset not found**:
- Verify path starts with "Assets/"
- Check file exists in project
- Use `findassets` to locate

**Search returns nothing**:
- Check filter syntax
- Verify folder path valid
- Try broader search

**Import fails**:
- Check asset not locked
- Verify Unity supports file type
- Check import settings valid
