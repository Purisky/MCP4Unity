# Component Tools Reference

Component inspection and property manipulation via Unity's SerializedObject API.

## getcomponents

List all components on a GameObject.

**Parameters**:
- `name` (string): GameObject name or path (`"Player"` or `"Canvas/Panel/Button"`)

**Returns**:
```json
{
  "gameObject": "Player",
  "components": [
    {"type": "Transform", "enabled": true},
    {"type": "Rigidbody", "enabled": true},
    {"type": "PlayerController", "enabled": true}
  ]
}
```

**Notes**:
- All GameObjects have Transform
- `enabled` only applies to MonoBehaviour

---

## getserializedproperties

Get all serialized properties of a component.

**Parameters**:
- `name` (string): GameObject name or path
- `componentNameOrIndex` (string): Component type (`"Rigidbody"`) or index (`"0"`)

**Returns**:
```json
{
  "gameObject": "Player",
  "component": "Rigidbody",
  "properties": [
    {"name": "m_Mass", "type": "float", "value": 1.0},
    {"name": "m_UseGravity", "type": "bool", "value": true},
    {"name": "m_Interpolate", "type": "enum", "value": "None"}
  ]
}
```

**Property Types**:
- Primitives: `int`, `float`, `bool`, `string`
- Unity: `Vector2`, `Vector3`, `Color`, `Rect`
- References: `ObjectReference` (GameObject, Component, Asset)
- Collections: `Array`, `List`
- Enums: `enum` (shows current value)

**Notes**:
- Only serialized fields (public or `[SerializeField]`)
- Names use Unity internal format (often `m_` prefix)
- Use index for faster access

---

## setserializedproperty

Modify a serialized property value.

**Parameters**:
- `name` (string): GameObject name or path
- `componentNameOrIndex` (string): Component type or index
- `propertyName` (string): Property name (from getserializedproperties)
- `value` (string): New value (type-appropriate format)

**Examples**:
```javascript
// Float
setserializedproperty("Player", "Rigidbody", "m_Mass", "2.5")

// Bool
setserializedproperty("Player", "PlayerController", "isGrounded", "false")

// Int
setserializedproperty("Enemy", "HealthSystem", "maxHealth", "150")

// String
setserializedproperty("Player", "PlayerController", "playerName", "Hero")

// Enum (by name)
setserializedproperty("Player", "Rigidbody", "m_Interpolate", "Interpolate")

// Vector3
setserializedproperty("Player", "Transform", "m_LocalPosition", "0,1,0")

// Color
setserializedproperty("Sprite", "SpriteRenderer", "m_Color", "1,0,0,1")
```

**Value Formats**:
- `float`: `"1.5"`, `"0.0"`, `"-3.14"`
- `int`: `"100"`, `"0"`, `"-50"`
- `bool`: `"true"`, `"false"`
- `string`: `"any text"`
- `enum`: `"EnumValueName"`
- `Vector2`: `"x,y"` → `"1.5,2.0"`
- `Vector3`: `"x,y,z"` → `"0,1,0"`
- `Color`: `"r,g,b,a"` → `"1,0,0,1"` (values 0-1)

**Returns**:
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

**Notes**:
- Changes apply immediately
- Scene marked dirty automatically
- Use `savescene()` to persist

---

## createobject

Create a new GameObject.

**Parameters**:
- `name` (string): GameObject name
- `parent` (string, optional): Parent path (empty = root)

**Examples**:
```javascript
createobject("Enemy")  // Root level
createobject("Bullet", "Player/Weapons")  // Under parent
```

**Returns**:
```json
{
  "success": true,
  "name": "Enemy",
  "path": "Enemy",
  "parent": null
}
```

---

## deleteobject

Delete a GameObject.

**Parameters**:
- `name` (string): GameObject name or path

**Example**:
```javascript
deleteobject("Enemy")
deleteobject("Canvas/Panel/OldButton")
```

**Returns**:
```json
{
  "success": true,
  "deleted": "Enemy"
}
```

**Notes**:
- Deletes object and all children
- Cannot be undone via MCP

---

## addcomponent

Add a component to a GameObject.

**Parameters**:
- `name` (string): GameObject name or path
- `componentType` (string): Component type name

**Examples**:
```javascript
addcomponent("Player", "Rigidbody")
addcomponent("Enemy", "BoxCollider")
addcomponent("Player", "MyNamespace.CustomScript")
```

**Returns**:
```json
{
  "success": true,
  "gameObject": "Player",
  "component": "Rigidbody"
}
```

**Notes**:
- Component type must exist in project
- Use full namespace for custom scripts
- Some components require others (e.g., Collider needs Rigidbody for physics)

---

## removecomponent

Remove a component from a GameObject.

**Parameters**:
- `name` (string): GameObject name or path
- `componentNameOrIndex` (string): Component type or index

**Examples**:
```javascript
removecomponent("Player", "Rigidbody")
removecomponent("Enemy", "1")  // Remove second component
```

**Returns**:
```json
{
  "success": true,
  "gameObject": "Player",
  "removed": "Rigidbody"
}
```

**Notes**:
- Cannot remove Transform (required)
- Cannot be undone via MCP

---

## setparent

Set GameObject parent.

**Parameters**:
- `name` (string): GameObject name or path
- `parent` (string): Parent path (empty = root)

**Examples**:
```javascript
setparent("Bullet", "Player/Weapons")  // Set parent
setparent("Bullet", "")  // Move to root
```

**Returns**:
```json
{
  "success": true,
  "gameObject": "Bullet",
  "newParent": "Player/Weapons"
}
```

---

## Common Workflows

### Inspect and Modify Component

```
1. getcomponents("Player")
2. getserializedproperties("Player", "Rigidbody")
3. setserializedproperty("Player", "Rigidbody", "m_Mass", "2.5")
4. savescene()
```

### Create and Configure GameObject

```
1. createobject("Enemy", "Enemies")
2. addcomponent("Enemies/Enemy", "Rigidbody")
3. addcomponent("Enemies/Enemy", "BoxCollider")
4. setserializedproperty("Enemies/Enemy", "Rigidbody", "m_Mass", "1.5")
5. savescene()
```

### Clone GameObject Configuration

```
1. getcomponents("SourceObject")
2. getserializedproperties("SourceObject", "Rigidbody")
3. createobject("ClonedObject")
4. addcomponent("ClonedObject", "Rigidbody")
5. setserializedproperty("ClonedObject", "Rigidbody", "m_Mass", "<value>")
```

---

## Limitations

**SerializedObject API**:
- Only accesses serialized fields
- Non-serialized properties require `runcode`
- Some Unity types not fully supported

**Property Modification**:
- ObjectReference properties cannot be set via string
- Complex nested structures may need `runcode`
- Array/List modification limited

**Workarounds**:
```javascript
// For non-serialized properties
runcode("UnityEngine.GameObject.Find('Player').GetComponent<Rigidbody>()", 
  "set_velocity", "new Vector3(1, 0, 0)")

// For ObjectReference
runcode("UnityEngine.GameObject.Find('Player').GetComponent<PlayerController>()", 
  "set_weapon", "UnityEngine.GameObject.Find('Sword')")
```

---

## Error Handling

**GameObject not found**:
```json
{"error": "GameObject not found: InvalidName"}
```
→ Check name/path with `gethierarchy()`

**Component not found**:
```json
{"error": "Component not found: InvalidComponent"}
```
→ Check available components with `getcomponents()`

**Property not found**:
```json
{"error": "Property not found: invalidProperty"}
```
→ Check property names with `getserializedproperties()`

**Invalid value format**:
```json
{"error": "Invalid value format for type float: abc"}
```
→ Check value format matches property type