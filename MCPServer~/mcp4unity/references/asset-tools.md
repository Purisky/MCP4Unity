# Asset Tools Reference

Asset tools handle Unity asset discovery, inspection, and import operations.

## Available Tools

### findassets

**Purpose**: Search for assets in the project using Unity's search filter syntax.

**Parameters**:
- `filter` (required, string): Unity search filter expression
- `folder` (optional, string): Limit search to specific folder path

**Filter Syntax**:
- `t:Type` - Search by asset type (e.g., `t:Texture2D`, `t:Prefab`, `t:ScriptableObject`)
- `l:Label` - Search by asset label (e.g., `l:Environment`, `l:UI`)
- `name` - Search by asset name (e.g., `Player`, `hero_sprite`)
- Combine filters with spaces (AND logic): `t:Prefab l:Enemy`

**Returns**: Array of asset paths matching the filter.

**Example Usage**:

```javascript
// Find all textures
findassets("t:Texture2D")

// Find all prefabs in specific folder
findassets("t:Prefab", "Assets/Prefabs/Characters")

// Find assets with specific label
findassets("l:UI")

// Find by name
findassets("PlayerController")

// Combine filters
findassets("t:ScriptableObject l:Config")
```

**Example Response**:
```json
{
  "assets": [
    "Assets/Sprites/hero.png",
    "Assets/Sprites/enemy_01.png",
    "Assets/UI/button_bg.png"
  ],
  "count": 3
}
```

**Usage Notes**:
- Search is case-insensitive
- Wildcards not supported (use partial names)
- Returns full asset paths relative to project root
- Empty folder parameter searches entire project

**Common Asset Types**:
- `Texture2D` - Images, sprites
- `Prefab` - Prefab assets
- `ScriptableObject` - ScriptableObject assets
- `AudioClip` - Audio files
- `Material` - Materials
- `Shader` - Shader files
- `Scene` - Scene files
- `AnimationClip` - Animation clips
- `Font` - Font assets

---

### getassetinfo

**Purpose**: Get detailed information about a specific asset.

**Parameters**:
- `assetPath` (required, string): Asset path relative to project root
  - Example: `"Assets/Sprites/hero.png"`

**Returns**:
- Asset type (main type)
- GUID (unique identifier)
- File size (bytes)
- Dependencies (other assets this asset references)
- Type-specific information:
  - **Textures**: width, height, format
  - **Prefabs/GameObjects**: component summary
  - **ScriptableObjects**: type name

**Example Response** (Texture):
```json
{
  "path": "Assets/Sprites/hero.png",
  "type": "Texture2D",
  "guid": "a1b2c3d4e5f6g7h8i9j0",
  "fileSize": 524288,
  "width": 512,
  "height": 512,
  "format": "RGBA32",
  "dependencies": [
    "Assets/Materials/SpriteMaterial.mat"
  ]
}
```

**Example Response** (Prefab):
```json
{
  "path": "Assets/Prefabs/Player.prefab",
  "type": "GameObject",
  "guid": "x1y2z3a4b5c6d7e8f9g0",
  "fileSize": 8192,
  "components": [
    "Transform",
    "Rigidbody",
    "CapsuleCollider",
    "PlayerController",
    "Animator"
  ],
  "dependencies": [
    "Assets/Scripts/PlayerController.cs",
    "Assets/Animations/PlayerAnimator.controller"
  ]
}
```

**Usage Notes**:
- Asset must exist (returns error if not found)
- Dependencies are direct references only (not transitive)
- File size is on-disk size (not runtime memory)
- GUID is stable across moves/renames

---

### importasset

**Purpose**: Force reimport an asset (refresh from source file).

**Parameters**:
- `assetPath` (required, string): Asset path to reimport
- `options` (optional, string, default: "Default"): Import options
  - `"Default"` - Standard reimport
  - `"ForceUpdate"` - Force full reimport even if unchanged
  - `"ForceSynchronousImport"` - Block until import completes
  - `"ImportRecursive"` - Reimport folder and all contents

**Returns**: Success status and import duration.

**Example Usage**:

```javascript
// Reimport single texture
importasset("Assets/Sprites/hero.png")

// Force reimport even if unchanged
importasset("Assets/Sprites/hero.png", "ForceUpdate")

// Reimport entire folder
importasset("Assets/Sprites", "ImportRecursive")

// Synchronous import (wait for completion)
importasset("Assets/Models/character.fbx", "ForceSynchronousImport")
```

**Example Response**:
```json
{
  "success": true,
  "path": "Assets/Sprites/hero.png",
  "duration": "0.234s"
}
```

**Usage Notes**:
- Triggers Unity's asset import pipeline
- Useful after modifying source files externally
- `ImportRecursive` can be slow for large folders
- `ForceSynchronousImport` blocks until complete (use for critical assets)
- Default import is asynchronous (returns immediately)

**Common Use Cases**:
- Refresh texture after editing in external tool
- Reimport model after changing export settings
- Force reimport after changing import settings in code
- Batch reimport folder after bulk file changes

---

## Workflow: Find and Inspect Assets

**Typical workflow**:

1. Search for assets: `findassets("t:Prefab l:Enemy")`
2. Get detailed info: `getassetinfo("Assets/Prefabs/Enemy01.prefab")`
3. If needed, reimport: `importasset("Assets/Prefabs/Enemy01.prefab")`

**Example: Find all UI sprites and check their sizes**:

```
findassets("t:Texture2D", "Assets/UI")
  ↓
For each result:
  getassetinfo(path)
  ↓
  Check width/height
  ↓
  If too large: resize source and importasset(path, "ForceUpdate")
```

---

## Tips and Best Practices

**Efficient searching**:
- Use `folder` parameter to limit scope
- Combine type and label filters for precision
- Cache search results if searching repeatedly

**Asset dependencies**:
- Use `getassetinfo` to find what assets depend on
- Useful before deleting/moving assets
- Dependencies are one-way (A references B, not B references A)

**Import performance**:
- Avoid `ImportRecursive` on large folders
- Use `ForceSynchronousImport` only when necessary
- Batch imports by folder when possible

**GUID stability**:
- GUIDs persist across moves/renames
- Use GUIDs for stable asset references in tools
- Available via `getassetinfo`
