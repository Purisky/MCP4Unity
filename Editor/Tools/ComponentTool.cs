using MCP4Unity;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace MCP
{
    public class ComponentTools
    {
        [Tool("获取指定GameObject上的所有组件列表（含类型、启用状态）")]
        public static string GetComponents(
            [Desc("GameObject的名称或Hierarchy路径（如 'Canvas/Panel/Button'）")] string nameOrPath)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            var components = go.GetComponents<Component>();
            var result = new StringBuilder();
            result.AppendLine($"GameObject: {go.name}");
            result.AppendLine($"组件数量: {components.Length}");
            result.AppendLine();

            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null)
                {
                    result.AppendLine($"  [{i}] ⚠️ Missing Script (null)");
                    continue;
                }

                string enabledStatus = "";
                if (comp is Behaviour behaviour)
                {
                    enabledStatus = behaviour.enabled ? " [启用]" : " [禁用]";
                }
                else if (comp is Renderer renderer)
                {
                    enabledStatus = renderer.enabled ? " [启用]" : " [禁用]";
                }
                else if (comp is Collider collider)
                {
                    enabledStatus = collider.enabled ? " [启用]" : " [禁用]";
                }

                result.AppendLine($"  [{i}] {comp.GetType().FullName}{enabledStatus}");
            }

            return result.ToString();
        }

        [Tool("获取指定GameObject上指定组件的所有序列化属性及其值")]
        public static string GetSerializedProperties(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("组件类型名称（如 'Transform'、'BoxCollider'），或组件索引（如 '0'、'1'）")] string componentNameOrIndex)
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
                    if (comp == null) continue;
                    if (comp.GetType().Name == componentNameOrIndex ||
                        comp.GetType().FullName == componentNameOrIndex)
                    {
                        targetComponent = comp;
                        break;
                    }
                }
            }

            if (targetComponent == null)
                return $"❌ 在 {go.name} 上未找到组件: {componentNameOrIndex}";

            var result = new StringBuilder();
            result.AppendLine($"GameObject: {go.name}");
            result.AppendLine($"组件: {targetComponent.GetType().FullName}");
            result.AppendLine();

            var so = new SerializedObject(targetComponent);
            var iterator = so.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                string value = GetPropertyValueString(iterator);
                string indent = new string(' ', iterator.depth * 2);
                result.AppendLine($"{indent}{iterator.displayName} ({iterator.propertyType}): {value}");
            }

            so.Dispose();
            return result.ToString();
        }

        private static string GetPropertyValueString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString("F4");
                case SerializedPropertyType.String:
                    return string.IsNullOrEmpty(prop.stringValue) ? "(empty)" : prop.stringValue;
                case SerializedPropertyType.Color:
                    return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})"
                        : "(null)";
                case SerializedPropertyType.Enum:
                    return prop.enumDisplayNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return prop.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue.eulerAngles.ToString();
                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString();
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                default:
                    return $"({prop.propertyType})";
            }
        }

        private static GameObject FindGameObject(string nameOrPath)
        {
            if (nameOrPath.Contains("/"))
            {
                var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    string subPath = nameOrPath.StartsWith(root.name + "/")
                        ? nameOrPath.Substring(root.name.Length + 1)
                        : nameOrPath;
                    var found = root.transform.Find(subPath);
                    if (found != null)
                        return found.gameObject;
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
