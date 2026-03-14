using MCP4Unity;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace MCP
{
    public class SceneTools
    {
        [Tool("获取当前活跃场景信息（名称、路径、是否有未保存修改、根物体数量、已加载场景列表）")]
        public static string GetActiveScene()
        {
            var result = new StringBuilder();
            var activeScene = SceneManager.GetActiveScene();

            result.AppendLine($"活跃场景: {activeScene.name}");
            result.AppendLine($"路径: {activeScene.path}");
            result.AppendLine($"是否dirty: {activeScene.isDirty}");
            result.AppendLine($"根物体数量: {activeScene.rootCount}");
            result.AppendLine($"是否已加载: {activeScene.isLoaded}");

            int sceneCount = SceneManager.sceneCount;
            if (sceneCount > 1)
            {
                result.AppendLine($"\n已加载场景 ({sceneCount}):");
                for (int i = 0; i < sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    string activeMarker = scene == activeScene ? " ← 活跃" : "";
                    result.AppendLine($"  [{i}] {scene.name} ({scene.path}){activeMarker}");
                }
            }

            return result.ToString();
        }

        [Tool("获取当前场景的Hierarchy树结构")]
        public static string GetHierarchy(
            [Desc("是否只返回顶层物体，false则返回完整树结构")] bool topOnly = false,
            [Desc("树结构最大深度（0表示不限制）")] int maxDepth = 0)
        {
            var result = new StringBuilder();
            var activeScene = SceneManager.GetActiveScene();
            var rootObjects = activeScene.GetRootGameObjects();

            result.AppendLine($"场景: {activeScene.name} ({rootObjects.Length} 个根物体)");
            result.AppendLine();

            foreach (var root in rootObjects)
            {
                if (topOnly)
                {
                    string status = root.activeSelf ? "✓" : "✗";
                    result.AppendLine($"[{status}] {root.name}  (children: {root.transform.childCount})");
                }
                else
                {
                    AppendHierarchyNode(result, root.transform, 0, maxDepth);
                }
            }

            return result.ToString();
        }

        [Tool("根据名称或路径获取GameObject的详细信息（Transform、激活状态、Tag、Layer、子物体）")]
        public static string GetGameObjectInfo(
            [Desc("GameObject的名称或Hierarchy路径（如 'Canvas/Panel/Button'）")] string nameOrPath)
        {
            var go = FindGameObject(nameOrPath);
            if (go == null)
                return $"❌ 未找到GameObject: {nameOrPath}";

            var result = new StringBuilder();
            var t = go.transform;

            result.AppendLine($"名称: {go.name}");
            result.AppendLine($"激活(self): {go.activeSelf}");
            result.AppendLine($"激活(hierarchy): {go.activeInHierarchy}");
            result.AppendLine($"Tag: {go.tag}");
            result.AppendLine($"Layer: {LayerMask.LayerToName(go.layer)} ({go.layer})");
            result.AppendLine($"静态: {go.isStatic}");
            result.AppendLine();

            result.AppendLine("Transform:");
            result.AppendLine($"  路径: {GetFullPath(t)}");
            result.AppendLine($"  localPosition: {t.localPosition}");
            result.AppendLine($"  localRotation: {t.localEulerAngles}");
            result.AppendLine($"  localScale: {t.localScale}");
            result.AppendLine($"  worldPosition: {t.position}");
            result.AppendLine();

            int childCount = t.childCount;
            if (childCount > 0)
            {
                result.AppendLine($"子物体 ({childCount}):");
                for (int i = 0; i < childCount && i < 50; i++)
                {
                    var child = t.GetChild(i);
                    string status = child.gameObject.activeSelf ? "✓" : "✗";
                    result.AppendLine($"  [{status}] {child.name}");
                }
                if (childCount > 50)
                    result.AppendLine($"  ... 还有 {childCount - 50} 个子物体");
            }

            var components = go.GetComponents<Component>();
            result.AppendLine($"\n组件 ({components.Length}):");
            foreach (var comp in components)
            {
                if (comp == null)
                {
                    result.AppendLine("  ⚠️ Missing Script");
                    continue;
                }
                result.AppendLine($"  {comp.GetType().FullName}");
            }

            return result.ToString();
        }

        private static void AppendHierarchyNode(StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            if (maxDepth > 0 && depth > maxDepth)
                return;

            string indent = new string(' ', depth * 2);
            string status = t.gameObject.activeSelf ? "✓" : "✗";
            int componentCount = t.GetComponents<Component>().Length;
            sb.AppendLine($"{indent}[{status}] {t.name}  (components: {componentCount}, children: {t.childCount})");

            for (int i = 0; i < t.childCount; i++)
            {
                AppendHierarchyNode(sb, t.GetChild(i), depth + 1, maxDepth);
            }
        }

        private static GameObject FindGameObject(string nameOrPath)
        {
            if (nameOrPath.Contains("/"))
            {
                var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    var found = root.transform.Find(nameOrPath.StartsWith(root.name + "/")
                        ? nameOrPath.Substring(root.name.Length + 1)
                        : nameOrPath);
                    if (found != null)
                        return found.gameObject;
                }

                foreach (var root in rootObjects)
                {
                    if (root.name == nameOrPath.Split('/')[0])
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

        [Tool("保存当前活跃场景")]
        public static string SaveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path))
                return $"❌ 场景未保存过，无法保存。请使用 SaveSceneAs 指定路径";

            try
            {
                bool success = EditorSceneManager.SaveScene(activeScene);
                return success 
                    ? $"✅ 已保存场景: {activeScene.name} ({activeScene.path})" 
                    : $"❌ 保存场景失败: {activeScene.name}";
            }
            catch (Exception ex)
            {
                return $"❌ 保存场景时发生错误: {ex.Message}";
            }
        }

        [Tool("另存为场景到指定路径")]
        public static string SaveSceneAs(
            [Desc("保存路径（相对于项目根目录，如 'Assets/Scenes/NewScene.unity'）")] string path)
        {
            if (!path.StartsWith("Assets/"))
                return $"❌ 路径必须以 'Assets/' 开头";

            if (!path.EndsWith(".unity"))
                path += ".unity";

            try
            {
                var activeScene = SceneManager.GetActiveScene();
                bool success = EditorSceneManager.SaveScene(activeScene, path);
                return success 
                    ? $"✅ 已保存场景到: {path}" 
                    : $"❌ 保存场景失败";
            }
            catch (Exception ex)
            {
                return $"❌ 保存场景时发生错误: {ex.Message}";
            }
        }

        [Tool("加载指定场景")]
        public static string LoadScene(
            [Desc("场景路径（如 'Assets/Scenes/MainScene.unity'）或场景名称")] string scenePathOrName,
            [Desc("加载模式：Single（单场景）或 Additive（叠加）")] string mode = "Single")
        {
            string scenePath = scenePathOrName;
            if (!scenePath.Contains("/"))
            {
                var guids = UnityEditor.AssetDatabase.FindAssets($"t:Scene {scenePathOrName}");
                if (guids.Length == 0)
                    return $"❌ 未找到场景: {scenePathOrName}";
                scenePath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            if (!System.IO.File.Exists(scenePath))
                return $"❌ 场景文件不存在: {scenePath}";

            try
            {
                var loadMode = mode == "Additive" 
                    ? OpenSceneMode.Additive 
                    : OpenSceneMode.Single;

                var scene = EditorSceneManager.OpenScene(scenePath, loadMode);
                return $"✅ 已加载场景: {scene.name} ({scenePath})\n模式: {mode}";
            }
            catch (Exception ex)
            {
                return $"❌ 加载场景失败: {ex.Message}";
            }
        }

        [Tool("创建新场景")]
        public static string CreateScene(
            [Desc("场景设置：Empty（空场景）或 DefaultGameObjects（包含默认对象）")] string setup = "DefaultGameObjects",
            [Desc("创建模式：Single（替换当前场景）或 Additive（叠加）")] string mode = "Single")
        {
            try
            {
                var sceneSetup = setup == "Empty" 
                    ? NewSceneSetup.EmptyScene 
                    : NewSceneSetup.DefaultGameObjects;

                var sceneMode = mode == "Additive" 
                    ? NewSceneMode.Additive 
                    : NewSceneMode.Single;

                var scene = EditorSceneManager.NewScene(sceneSetup, sceneMode);
                return $"✅ 已创建新场景: {scene.name}\n设置: {setup}, 模式: {mode}";
            }
            catch (Exception ex)
            {
                return $"❌ 创建场景失败: {ex.Message}";
            }
        }

        [Tool("关闭指定场景")]
        public static string CloseScene(
            [Desc("场景名称")] string sceneName,
            [Desc("是否移除场景（true=移除, false=仅卸载）")] bool removeScene = true)
        {
            Scene targetScene = default;
            bool found = false;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName)
                {
                    targetScene = scene;
                    found = true;
                    break;
                }
            }

            if (!found)
                return $"❌ 未找到已加载的场景: {sceneName}";

            if (SceneManager.sceneCount == 1)
                return $"❌ 无法关闭唯一的场景";

            try
            {
                bool success = EditorSceneManager.CloseScene(targetScene, removeScene);
                return success 
                    ? $"✅ 已关闭场景: {sceneName}" 
                    : $"❌ 关闭场景失败: {sceneName}";
            }
            catch (Exception ex)
            {
                return $"❌ 关闭场景时发生错误: {ex.Message}";
            }
        }

        [Tool("设置活跃场景")]
        public static string SetActiveScene(
            [Desc("场景名称")] string sceneName)
        {
            Scene targetScene = default;
            bool found = false;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName)
                {
                    targetScene = scene;
                    found = true;
                    break;
                }
            }

            if (!found)
                return $"❌ 未找到已加载的场景: {sceneName}";

            try
            {
                bool success = SceneManager.SetActiveScene(targetScene);
                return success 
                    ? $"✅ 已设置活跃场景: {sceneName}" 
                    : $"❌ 设置活跃场景失败: {sceneName}";
            }
            catch (Exception ex)
            {
                return $"❌ 设置活跃场景时发生错误: {ex.Message}";
            }
        }

        private static string GetFullPath(Transform t)
        {
            var parts = new List<string>();
            var current = t;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
