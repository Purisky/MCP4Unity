using MCP4Unity;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace MCP
{
    public class HierarchyTool
    {
        [Tool("为指定GameObject添加组件")]
        public static string AddComponent(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("组件类型全名（如 'UnityEngine.BoxCollider'、'UnityEngine.Rigidbody'）")] string componentTypeName)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            Type componentType = FindComponentType(componentTypeName);
            if (componentType == null)
                return $"❌ 未找到组件类型: {componentTypeName}\n💡 请使用完整类型名，如 'UnityEngine.BoxCollider'";

            if (!typeof(Component).IsAssignableFrom(componentType))
                return $"❌ 类型 {componentTypeName} 不是有效的Component类型";

            try
            {
                var component = go.AddComponent(componentType);
                Undo.RegisterCreatedObjectUndo(component, $"Add {componentType.Name}");
                EditorUtility.SetDirty(go);
                return $"✅ 已添加组件: {componentType.FullName} 到 {go.name}";
            }
            catch (Exception ex)
            {
                return $"❌ 添加组件失败: {ex.Message}";
            }
        }

        [Tool("从指定GameObject移除组件")]
        public static string RemoveComponent(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("组件类型名称或索引（如 'BoxCollider' 或 '2'）")] string componentNameOrIndex)
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

            if (targetComponent is Transform)
                return $"❌ 不能移除Transform组件";

            try
            {
                string componentName = targetComponent.GetType().Name;
                Undo.DestroyObjectImmediate(targetComponent);
                EditorUtility.SetDirty(go);
                return $"✅ 已移除组件: {componentName} 从 {go.name}";
            }
            catch (Exception ex)
            {
                return $"❌ 移除组件失败: {ex.Message}";
            }
        }

        [Tool("在场景中创建新的GameObject")]
        public static string CreateGameObject(
            [Desc("新GameObject的名称")] string name,
            [Desc("父物体的名称或路径（为空则创建为根物体）")] string parentNameOrPath = "")
        {
            try
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

                if (!string.IsNullOrEmpty(parentNameOrPath))
                {
                    var parent = FindGameObject(parentNameOrPath);
                    if (parent == null)
                    {
                        Undo.DestroyObjectImmediate(go);
                        return $"❌ 未找到父物体: {parentNameOrPath}";
                    }
                    go.transform.SetParent(parent.transform, false);
                }

                Selection.activeGameObject = go;
                return $"✅ 已创建GameObject: {name}" + 
                       (string.IsNullOrEmpty(parentNameOrPath) ? "" : $" (父物体: {parentNameOrPath})");
            }
            catch (Exception ex)
            {
                return $"❌ 创建GameObject失败: {ex.Message}";
            }
        }

        [Tool("删除指定的GameObject")]
        public static string DeleteGameObject(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            try
            {
                string goName = go.name;
                Undo.DestroyObjectImmediate(go);
                return $"✅ 已删除GameObject: {goName}";
            }
            catch (Exception ex)
            {
                return $"❌ 删除GameObject失败: {ex.Message}";
            }
        }

        [Tool("设置GameObject的激活状态")]
        public static string SetActive(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("激活状态（true=激活, false=禁用）")] bool active)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            try
            {
                Undo.RecordObject(go, $"Set Active {active}");
                go.SetActive(active);
                EditorUtility.SetDirty(go);
                return $"✅ 已设置 {go.name} 激活状态为: {active}";
            }
            catch (Exception ex)
            {
                return $"❌ 设置激活状态失败: {ex.Message}";
            }
        }

        [Tool("设置GameObject的父物体")]
        public static string SetParent(
            [Desc("子物体的名称或Hierarchy路径")] string childNameOrPath,
            [Desc("父物体的名称或路径（为空则设为根物体）")] string parentNameOrPath = "",
            [Desc("是否保持世界坐标（true=保持世界坐标, false=保持本地坐标）")] bool worldPositionStays = true)
        {
            var child = FindGameObject(childNameOrPath);
            if (child == null)
                return $"❌ 未找到子物体: {childNameOrPath}";

            try
            {
                Transform newParent = null;
                if (!string.IsNullOrEmpty(parentNameOrPath))
                {
                    var parentGo = FindGameObject(parentNameOrPath);
                    if (parentGo == null)
                        return $"❌ 未找到父物体: {parentNameOrPath}";
                    newParent = parentGo.transform;
                }

                Undo.SetTransformParent(child.transform, newParent, $"Set Parent");
                child.transform.SetParent(newParent, worldPositionStays);
                EditorUtility.SetDirty(child);

                string parentInfo = newParent != null ? newParent.name : "根物体";
                return $"✅ 已设置 {child.name} 的父物体为: {parentInfo}";
            }
            catch (Exception ex)
            {
                return $"❌ 设置父物体失败: {ex.Message}";
            }
        }

        [Tool("重命名GameObject")]
        public static string SetName(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("新名称")] string newName)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            try
            {
                string oldName = go.name;
                Undo.RecordObject(go, $"Rename to {newName}");
                go.name = newName;
                EditorUtility.SetDirty(go);
                return $"✅ 已重命名: {oldName} → {newName}";
            }
            catch (Exception ex)
            {
                return $"❌ 重命名失败: {ex.Message}";
            }
        }

        [Tool("复制GameObject")]
        public static string DuplicateGameObject(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("新GameObject的名称（为空则自动命名）")] string newName = "")
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            try
            {
                var duplicate = GameObject.Instantiate(go, go.transform.parent);
                if (!string.IsNullOrEmpty(newName))
                    duplicate.name = newName;
                else
                    duplicate.name = go.name + " (Clone)";

                Undo.RegisterCreatedObjectUndo(duplicate, $"Duplicate {go.name}");
                Selection.activeGameObject = duplicate;
                return $"✅ 已复制GameObject: {go.name} → {duplicate.name}";
            }
            catch (Exception ex)
            {
                return $"❌ 复制GameObject失败: {ex.Message}";
            }
        }

        [Tool("设置GameObject的Tag")]
        public static string SetTag(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("Tag名称（如 'Player'、'Enemy'、'Untagged'）")] string tag)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            try
            {
                Undo.RecordObject(go, $"Set Tag {tag}");
                go.tag = tag;
                EditorUtility.SetDirty(go);
                return $"✅ 已设置 {go.name} 的Tag为: {tag}";
            }
            catch (Exception ex)
            {
                return $"❌ 设置Tag失败: {ex.Message}\n💡 请确保Tag已在TagManager中定义";
            }
        }

        [Tool("设置GameObject的Layer")]
        public static string SetLayer(
            [Desc("GameObject的名称或Hierarchy路径")] string nameOrPath,
            [Desc("Layer名称或索引（如 'Default'、'UI'、'0'、'5'）")] string layerNameOrIndex,
            [Desc("是否递归设置所有子物体")] bool includeChildren = false)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            int layerIndex;
            if (int.TryParse(layerNameOrIndex, out layerIndex))
            {
                if (layerIndex < 0 || layerIndex > 31)
                    return $"❌ Layer索引必须在0-31之间";
            }
            else
            {
                layerIndex = LayerMask.NameToLayer(layerNameOrIndex);
                if (layerIndex == -1)
                    return $"❌ 未找到Layer: {layerNameOrIndex}";
            }

            try
            {
                Undo.RecordObject(go, $"Set Layer {layerIndex}");
                go.layer = layerIndex;
                
                if (includeChildren)
                {
                    var children = go.GetComponentsInChildren<Transform>(true);
                    foreach (var child in children)
                    {
                        if (child != go.transform)
                        {
                            Undo.RecordObject(child.gameObject, $"Set Layer {layerIndex}");
                            child.gameObject.layer = layerIndex;
                        }
                    }
                }

                EditorUtility.SetDirty(go);
                string layerName = LayerMask.LayerToName(layerIndex);
                return $"✅ 已设置 {go.name} 的Layer为: {layerName} ({layerIndex})" +
                       (includeChildren ? " (包含所有子物体)" : "");
            }
            catch (Exception ex)
            {
                return $"❌ 设置Layer失败: {ex.Message}";
            }
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

        private static Type FindComponentType(string typeName)
        {
            // 尝试直接查找
            Type type = Type.GetType(typeName);
            if (type != null)
                return type;

            // 尝试在UnityEngine命名空间查找
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null)
                return type;

            // 尝试在UnityEngine.UI命名空间查找
            type = Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
            if (type != null)
                return type;

            // 遍历所有程序集查找
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;

                // 尝试简短名称
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == typeName || t.FullName == typeName)
                        return t;
                }
            }

            return null;
        }
    }
}
