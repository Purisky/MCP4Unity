using MCP4Unity;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace MCP
{
    public class AssetTools
    {
        [Tool("按类型和名称搜索项目中的资产（基于AssetDatabase）")]
        public static string FindAssets(
            [Desc("搜索过滤器，支持 't:类型 名称' 格式，如 't:Prefab Player'、't:Scene'、't:Material'、'GameManager'")] string filter,
            [Desc("搜索范围（资产文件夹路径），为空则搜索整个项目。如 'Assets/Prefabs'")] string searchInFolder = "")
        {
            string[] guids;
            if (string.IsNullOrEmpty(searchInFolder))
            {
                guids = AssetDatabase.FindAssets(filter);
            }
            else
            {
                guids = AssetDatabase.FindAssets(filter, new[] { searchInFolder });
            }

            if (guids.Length == 0)
                return $"未找到匹配 '{filter}' 的资产";

            var result = new StringBuilder();
            int displayCount = System.Math.Min(guids.Length, 100);
            result.AppendLine($"找到 {guids.Length} 个资产" + (guids.Length > 100 ? "（显示前100个）" : "") + ":");
            result.AppendLine();

            for (int i = 0; i < displayCount; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                string typeName = asset != null ? asset.GetType().Name : "Unknown";
                result.AppendLine($"  [{typeName}] {path}");
            }

            return result.ToString();
        }

        [Tool("获取指定资产的详细信息（类型、GUID、文件大小、依赖项、被引用情况）")]
        public static string GetAssetInfo(
            [Desc("资产路径，如 'Assets/Prefabs/Player.prefab'")] string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
                return $"❌ 未找到资产: {assetPath}";

            var result = new StringBuilder();
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            result.AppendLine($"路径: {assetPath}");
            result.AppendLine($"类型: {asset.GetType().FullName}");
            result.AppendLine($"GUID: {guid}");
            result.AppendLine($"名称: {asset.name}");

            try
            {
                string fullPath = System.IO.Path.GetFullPath(assetPath);
                if (System.IO.File.Exists(fullPath))
                {
                    long size = new System.IO.FileInfo(fullPath).Length;
                    result.AppendLine($"文件大小: {FormatFileSize(size)}");
                }
            }
            catch { }

            if (asset is Texture2D tex)
            {
                result.AppendLine($"纹理尺寸: {tex.width}x{tex.height}");
                result.AppendLine($"纹理格式: {tex.format}");
            }
            else if (asset is GameObject go)
            {
                var components = go.GetComponents<Component>();
                result.AppendLine($"组件数量: {components.Length}");
                int childCount = go.transform.childCount;
                result.AppendLine($"子物体数量: {childCount}");
            }
            else if (asset is ScriptableObject so)
            {
                result.AppendLine($"ScriptableObject类型: {so.GetType().FullName}");
            }

            string[] deps = AssetDatabase.GetDependencies(assetPath, false);
            if (deps.Length > 1)
            {
                result.AppendLine($"\n直接依赖 ({deps.Length - 1}):");
                foreach (string dep in deps)
                {
                    if (dep != assetPath)
                        result.AppendLine($"  {dep}");
                }
            }

            return result.ToString();
        }

        [Tool("强制重新导入指定资产")]
        public static string ImportAsset(
            [Desc("资产路径，如 'Assets/Prefabs/Player.prefab'")] string assetPath,
            [Desc("导入选项: Default, ForceUpdate, ForceSynchronousImport, ForceUncompressedImport")] string importOption = "Default")
        {
            if (!System.IO.File.Exists(assetPath) && !System.IO.Directory.Exists(assetPath))
            {
                string fullPath = System.IO.Path.GetFullPath(assetPath);
                if (!System.IO.File.Exists(fullPath) && !System.IO.Directory.Exists(fullPath))
                    return $"❌ 资产不存在: {assetPath}";
            }

            ImportAssetOptions option = importOption switch
            {
                "ForceUpdate" => ImportAssetOptions.ForceUpdate,
                "ForceSynchronousImport" => ImportAssetOptions.ForceSynchronousImport,
                "ForceUncompressedImport" => ImportAssetOptions.ForceUncompressedImport,
                _ => ImportAssetOptions.Default
            };

            AssetDatabase.ImportAsset(assetPath, option);
            return $"✅ 已重新导入: {assetPath} (选项: {option})";
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
