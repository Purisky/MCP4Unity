using MCP4Unity;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace MCP
{
    public class PropertyTool
    {
        [Tool("设置指定GameObject上指定组件的序列化属性值")]
        public static string SetSerializedProperty(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("组件类型名称或索引（如 'Transform'、'BoxCollider' 或 '0'、'1'）")] string componentNameOrIndex,
            [Desc("属性路径（如 'm_LocalPosition.x'、'm_Size'、'm_Enabled'）")] string propertyPath,
            [Desc("新值（字符串形式，会自动转换为对应类型）")] string value)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            Component targetComponent = null;

            if (int.TryParse(componentNameOrIndex, out int index))
            {
                var allComponents = go.GetComponents<Component>();
                if (index >= 0 && index < allComponents.Length)
                    targetComponent = allComponents[index];
                else
                    return $"❌ 组件索引 {index} 超出范围（共 {allComponents.Length} 个组件）";
            }
            else
            {
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == componentNameOrIndex)
                    {
                        targetComponent = comp;
                        break;
                    }
                }
            }

            if (targetComponent == null)
                return $"❌ 未找到组件: {componentNameOrIndex}";

            try
            {
                var serializedObject = new SerializedObject(targetComponent);
                var property = serializedObject.FindProperty(propertyPath);

                if (property == null)
                    return $"❌ 未找到属性: {propertyPath}\n💡 使用 GetSerializedProperties 查看可用属性";

                Undo.RecordObject(targetComponent, $"Set {propertyPath}");

                bool success = SetPropertyValue(property, value);
                if (!success)
                    return $"❌ 无法设置属性 {propertyPath} 的值为 '{value}'\n属性类型: {property.propertyType}";

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetComponent);

                return $"✅ 已设置 {go.name}.{targetComponent.GetType().Name}.{propertyPath} = {value}";
            }
            catch (Exception ex)
            {
                return $"❌ 设置属性失败: {ex.Message}";
            }
        }

        [Tool("批量设置多个属性值")]
        public static string SetMultipleProperties(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("组件类型名称或索引")] string componentNameOrIndex,
            [Desc("属性路径列表，用逗号分隔（如 'm_Enabled,m_Size.x,m_Size.y'）")] string propertyPaths,
            [Desc("对应的值列表，用逗号分隔（如 'true,10,20'）")] string values)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            Component targetComponent = null;

            if (int.TryParse(componentNameOrIndex, out int index))
            {
                var allComponents = go.GetComponents<Component>();
                if (index >= 0 && index < allComponents.Length)
                    targetComponent = allComponents[index];
                else
                    return $"❌ 组件索引 {index} 超出范围（共 {allComponents.Length} 个组件）";
            }
            else
            {
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == componentNameOrIndex)
                    {
                        targetComponent = comp;
                        break;
                    }
                }
            }

            if (targetComponent == null)
                return $"❌ 未找到组件: {componentNameOrIndex}";

            string[] pathArray = propertyPaths.Split(',');
            string[] valueArray = values.Split(',');

            if (pathArray.Length != valueArray.Length)
                return $"❌ 属性路径数量({pathArray.Length})与值数量({valueArray.Length})不匹配";

            try
            {
                var serializedObject = new SerializedObject(targetComponent);
                var result = new StringBuilder();
                int successCount = 0;

                Undo.RecordObject(targetComponent, "Set Multiple Properties");

                for (int i = 0; i < pathArray.Length; i++)
                {
                    string path = pathArray[i].Trim();
                    string val = valueArray[i].Trim();

                    var property = serializedObject.FindProperty(path);
                    if (property == null)
                    {
                        result.AppendLine($"⚠️ 未找到属性: {path}");
                        continue;
                    }

                    bool success = SetPropertyValue(property, val);
                    if (success)
                    {
                        result.AppendLine($"✅ {path} = {val}");
                        successCount++;
                    }
                    else
                    {
                        result.AppendLine($"❌ 无法设置 {path} = {val} (类型: {property.propertyType})");
                    }
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetComponent);

                result.Insert(0, $"批量设置完成: {successCount}/{pathArray.Length} 成功\n\n");
                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ 批量设置失败: {ex.Message}";
            }
        }

        private static bool SetPropertyValue(SerializedProperty property, string value)
        {
            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        if (int.TryParse(value, out int intVal))
                        {
                            property.intValue = intVal;
                            return true;
                        }
                        break;

                    case SerializedPropertyType.Boolean:
                        if (bool.TryParse(value, out bool boolVal))
                        {
                            property.boolValue = boolVal;
                            return true;
                        }
                        break;

                    case SerializedPropertyType.Float:
                        if (float.TryParse(value, out float floatVal))
                        {
                            property.floatValue = floatVal;
                            return true;
                        }
                        break;

                    case SerializedPropertyType.String:
                        property.stringValue = value;
                        return true;

                    case SerializedPropertyType.Color:
                        if (TryParseColor(value, out Color color))
                        {
                            property.colorValue = color;
                            return true;
                        }
                        break;

                    case SerializedPropertyType.Vector2:
                        if (TryParseVector2(value, out Vector2 vec2))
                        {
                            property.vector2Value = vec2;
                            return true;
                        }
                        break;

                    case SerializedPropertyType.Vector3:
                        if (TryParseVector3(value, out Vector3 vec3))
                        {
                            property.vector3Value = vec3;
                            return true;
                        }
                        break;

                    case SerializedPropertyType.Vector4:
                        if (TryParseVector4(value, out Vector4 vec4))
                        {
                            property.vector4Value = vec4;
                            return true;
                        }
                        break;

                    case SerializedPropertyType.Enum:
                        if (int.TryParse(value, out int enumIndex))
                        {
                            property.enumValueIndex = enumIndex;
                            return true;
                        }
                        else
                        {
                            for (int i = 0; i < property.enumNames.Length; i++)
                            {
                                if (property.enumNames[i].Equals(value, StringComparison.OrdinalIgnoreCase))
                                {
                                    property.enumValueIndex = i;
                                    return true;
                                }
                            }
                        }
                        break;

                    case SerializedPropertyType.ObjectReference:
                        if (string.IsNullOrEmpty(value) || value.ToLower() == "null")
                        {
                            property.objectReferenceValue = null;
                            return true;
                        }
                        else
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value);
                            if (obj != null)
                            {
                                property.objectReferenceValue = obj;
                                return true;
                            }
                        }
                        break;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryParseColor(string value, out Color color)
        {
            color = Color.white;
            if (ColorUtility.TryParseHtmlString(value, out color))
                return true;

            var parts = value.Split(',');
            if (parts.Length >= 3)
            {
                if (float.TryParse(parts[0], out float r) &&
                    float.TryParse(parts[1], out float g) &&
                    float.TryParse(parts[2], out float b))
                {
                    float a = parts.Length >= 4 && float.TryParse(parts[3], out a) ? a : 1f;
                    color = new Color(r, g, b, a);
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseVector2(string value, out Vector2 vector)
        {
            vector = Vector2.zero;
            var parts = value.Split(',');
            if (parts.Length >= 2 &&
                float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y))
            {
                vector = new Vector2(x, y);
                return true;
            }
            return false;
        }

        private static bool TryParseVector3(string value, out Vector3 vector)
        {
            vector = Vector3.zero;
            var parts = value.Split(',');
            if (parts.Length >= 3 &&
                float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z))
            {
                vector = new Vector3(x, y, z);
                return true;
            }
            return false;
        }

        private static bool TryParseVector4(string value, out Vector4 vector)
        {
            vector = Vector4.zero;
            var parts = value.Split(',');
            if (parts.Length >= 4 &&
                float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z) &&
                float.TryParse(parts[3], out float w))
            {
                vector = new Vector4(x, y, z, w);
                return true;
            }
            return false;
        }

        private static GameObject FindGameObject(string nameOrPath)
        {
            if (nameOrPath.Contains("/"))
            {
                var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    if (nameOrPath.StartsWith(root.name + "/"))
                    {
                        string subPath = nameOrPath.Substring(root.name.Length + 1);
                        var found = root.transform.Find(subPath);
                        if (found != null)
                            return found.gameObject;
                    }
                }
            }

            var go = GameObject.Find(nameOrPath);
            if (go != null)
                return go;

            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name == nameOrPath && obj.scene.IsValid())
                    return obj;
            }

            return null;
        }
    }
}
