# Component Tools Reference

Component tools handle GameObject component inspection and property manipulation via Unity's SerializedObject API.

## Available Tools

### getcomponents

**Purpose**: List all components attached to a GameObject.

**Parameters**:
- `name` (required, string): GameObject name or hierarchy path
  - Simple name: `"Player"`
  - Hierarchy path: `"Canvas/Panel/Button"`

**Returns**: Array of components with type names and enabled/disabled state.

**Example Response**:
```json
{
  "gameObject": "Player",
  "components": [
    {"type": "Transform", "enabled": true},
    {"type": "Rigidbody", "enabled": true},
    {"type": "CapsuleCollider", "enabled": true},
    {"type": "PlayerController", "enabled": true},
    {"type": "Animator", "enabled": false}
  ]
}
```

**Usage Notes**:
- All GameObjects have at least a Transform component
- `enabled` state only applies to MonoBehaviour components
- Transform and other non-MonoBehaviour components always show `enabled: true`
- Use hierarchy paths for nested objects

---

### getserializedproperties

**Purpose**: Get all serialized properties of a specific component using Unity's SerializedObject API.

**Parameters**:
- `name` (required, string): GameObject name or hierarchy path
- `componentNameOrIndex` (required, string): Component type name or index
  - Type name: `"Rigidbody"`, `"PlayerController"`
  - Index: `"0"`, `"1"`, `"2"` (from getcomponents list)

**Returns**: Array of serialized properties with names, types, and current values.

**Example Response** (Rigidbody):
```json
{
  "gameObject": "Player",
  "component": "Rigidbody",
  "properties": [
    {"name": "m_Mass", "type": "float", "value": 1.0},
    {"name": "m_Drag", "type": "float", "value": 0.0},
    {"name": "m_AngularDrag", "type": "float", "value": 0.05},
    {"name": "m_UseGravity", "type": "bool", "value": true},
    {"name": "m_IsKinematic", "type": "bool", "value": false},
    {"name": "m_Interpolate", "type": "enum", "value": "None"},
    {"name": "m_Constraints", "type": "enum", "value": "None"}
  ]
}
```

**Example Response** (Custom Script):
```json
{
  "gameObject": "Player",
  "component": "PlayerController",
  "properties": [
    {"name": "moveSpeed", "type": "float", "value": 5.0},
    {"name": "jumpForce", "type": "float", "value": 10.0},
    {"name": "maxHealth", "type": "int", "value": 100},
    {"name": "currentHealth", "type": "int", "value": 100},
    {"name": "isGrounded", "type": "bool", "value": true},
    {"name": "playerName", "type": "string", "value": "Hero"},
    {"name": "weapon", "type": "ObjectReference", "value": "Sword (GameObject)"}
  ]
}
```

**Property Types**:
- Primitives: `int`, `float`, `bool`, `string`
- Unity types: `Vector2`, `Vector3`, `Color`, `Rect`
- References: `ObjectReference` (GameObject, Component, Asset)
- Collections: `Array`, `List`
- Enums: `enum` (shows current value as string)

**Usage Notes**:
- Only shows serialized fields (public or `[SerializeField]` private)
- Property names use Unity's internal naming (often prefixed with `m_`)
- Use component index for faster access if you already called `getcomponents`
- ObjectReference values show type and name: `"Sword (GameObject)"`

---

### setserializedproperty

**Purpose**: Modify a serialized property value on a component.

**Parameters**:
- `name` (required, string): GameObject name or hierarchy path
- `componentNameOrIndex` (required, string): Component type name or index
- `propertyName` (required, string): Property name (from getserializedproperties)
- `value` (required, string): New value (type-appropriate format)

**Returns**: Success status and updated property info.

**Example Usage**:

```javascript
// Set float property
setserializedproperty("Player", "Rigidbody", "m_Mass", "2.5")

// Set bool property
setserializedproperty("Player", "PlayerController", "isGrounded", "false")

// Set int property
setserializedproperty("Enemy", "HealthSystem", "maxHealth", "150")

// Set string property
setserializedproperty("Player", "PlayerController", "playerName", "NewHero")

// Set enum property (by name)
setserializedproperty("Player", "Rigidbody", "m_Interpolate", "Interpolate")

// Set Vector3 property
setserializedproperty("Player", "Transform", "m_LocalPosition", "0,1,0")
```

**Example Response**:
```json
{
  "success": true,
  "gameObject": "Player",
  "component": "Rigidbody",
  "property": "m_Mass",
  "oldValue": 1.0,
  "newValue": 2.5
}
```

**Value Format by Type**:
- `float`: `"1.5"`, `"0.0"`, `"-3.14"`
- `int`: `"100"`, `"0"`, `"-50"`
- `bool`: `"true"`, `"false"`
- `string`: `"any text"`
- `Vector2`: `"x,y"` → `"1.0,2.0"`
- `Vector3`: `"x,y,z"` → `"1.0,2.0,3.0"`
- `Color`: `"r,g,b,a"` → `"1.0,0.0,0.0,1.0"` (red)
- `enum`: enum value name as string → `"Interpolate"`
- `ObjectReference`: Asset path or GameObject name → `"Assets/Prefabs/Sword.prefab"`

**Usage Notes**:
- Changes are applied immediately in the Editor
- Scene must be saved to persist changes
- Invalid values return error with type mismatch details
- ObjectReference requires valid asset path or GameObject name
- Use `ApplyModifiedProperties()` internally (automatic)

---

## Hierarchy Tools

### createobject

**Purpose**: Create a new GameObject in the scene.

**Parameters**:
- `name` (required, string): Name for the new GameObject
- `parent` (optional, string): Parent GameObject name or path (empty = root)

**Returns**: Created GameObject info with path.

**Example Usage**:

```javascript
// Create root object
createobject("NewObject")

// Create child object
createobject("ChildObject", "Parent")

// Create nested child
createobject("Button", "Canvas/Panel")
```

**Example Response**:
```json
{
  "success": true,
  "name": "NewObject",
  "path": "NewObject",
  "parent": null
}
```

---

### deleteobject

**Purpose**: Delete a GameObject from the scene.

**Parameters**:
- `name` (required, string): GameObject name or hierarchy path

**Returns**: Success status.

**Example Response**:
```json
{
  "success": true,
  "deleted": "OldObject"
}
```

**Usage Notes**:
- Deletes GameObject and all children
- Cannot be undone via MCP (use Unity's Undo in Editor)
- Scene must be saved to persist deletion

---

### addcomponent

**Purpose**: Add a component to a GameObject.

**Parameters**:
- `name` (required, string): GameObject name or hierarchy path
- `componentType` (required, string): Fully-qualified component type name
  - Built-in: `"UnityEngine.Rigidbody"`, `"UnityEngine.BoxCollider"`
  - Custom: `"MyNamespace.PlayerController"`

**Returns**: Success status and component info.

**Example Usage**:

```javascript
// Add built-in component
addcomponent("Player", "UnityEngine.Rigidbody")

// Add custom script
addcomponent("Enemy", "GameScripts.EnemyAI")

// Add UI component
addcomponent("Canvas/Panel", "UnityEngine.UI.Image")
```

**Example Response**:
```json
{
  "success": true,
  "gameObject": "Player",
  "component": "Rigidbody",
  "fullType": "UnityEngine.Rigidbody"
}
```

**Usage Notes**:
- Component type must exist and be accessible
- Cannot add duplicate components (except for some types like AudioSource)
- Some components have dependencies (e.g., Rigidbody requires Collider for physics)
- Use fully-qualified names to avoid ambiguity

---

### removecomponent

**Purpose**: Remove a component from a GameObject.

**Parameters**:
- `name` (required, string): GameObject name or hierarchy path
- `componentNameOrIndex` (required, string): Component type name or index

**Returns**: Success status.

**Example Response**:
```json
{
  "success": true,
  "gameObject": "Player",
  "removed": "Rigidbody"
}
```

**Usage Notes**:
- Cannot remove Transform (required component)
- Cannot be undone via MCP
- Scene must be saved to persist removal

---

## Workflow: Inspect and Modify Components

**Typical workflow**:

1. List components: `getcomponents("Player")`
2. Inspect properties: `getserializedproperties("Player", "Rigidbody")`
3. Modify property: `setserializedproperty("Player", "Rigidbody", "m_Mass", "2.5")`
4. Save scene: `savescene()`

**Example: Configure Rigidbody**:

```
getcomponents("Player")
  ↓
getserializedproperties("Player", "Rigidbody")
  ↓
setserializedproperty("Player", "Rigidbody", "m_Mass", "2.0")
setserializedproperty("Player", "Rigidbody", "m_Drag", "0.5")
setserializedproperty("Player", "Rigidbody", "m_UseGravity", "true")
  ↓
savescene()
```

---

## Tips and Best Practices

**Property inspection**:
- Use `getcomponents` first to see what's available
- Use component index for faster repeated access
- Property names often have `m_` prefix (Unity internal naming)

**Property modification**:
- Always check current value with `getserializedproperties` first
- Use correct value format for each type
- Save scene after modifications to persist changes

**Component management**:
- Use fully-qualified type names for `addcomponent`
- Check for dependencies before adding components
- Cannot remove Transform or other required components

**Performance**:
- Batch property changes when possible
- Use component index instead of name for repeated access
- Save scene once after multiple changes
